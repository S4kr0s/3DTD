using System.Collections.Generic;
using UnityEngine;

// Short-lived lines of hitscan shots (Sniper): a pooled copy of a line prefab (a LineRenderer, e.g. the Polygon
// Arsenal beam lines) from the barrel to the hit point that thins out over its duration, in game time.
// Every shot shows its tracer, at any game speed; copies are reused, nothing is allocated per shot once a
// prefab's pool has grown to the number of tracers alive at once.
[DefaultExecutionOrder(1100)]
public class TracerRenderer : MonoBehaviour
{
    private struct Tracer
    {
        public LineRenderer Line;
        public float Width;
        public float Duration;
        public float Age;
    }

    private sealed class Pool
    {
        public GameObject Prefab;
        public float PrefabWidth;
        public readonly Stack<LineRenderer> Free = new Stack<LineRenderer>(32);
    }

    private static TracerRenderer instance;
    private static bool quitting;

    private readonly Dictionary<GameObject, Pool> pools = new Dictionary<GameObject, Pool>();
    private readonly Dictionary<LineRenderer, Pool> owners = new Dictionary<LineRenderer, Pool>();
    private readonly List<Tracer> live = new List<Tracer>(128);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    private static TracerRenderer Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
                instance = new GameObject("Tracers").AddComponent<TracerRenderer>();
            return instance;
        }
    }

    public static int ActiveTracers => instance != null ? instance.live.Count : 0;

    // widthScale multiplies the prefab's own line width
    public static void Show(GameObject linePrefab, Vector3 from, Vector3 to, float widthScale, float duration)
    {
        if (linePrefab == null || duration <= 0f)
            return;
        TracerRenderer renderer = Instance;
        if (renderer != null)
            renderer.Add(linePrefab, from, to, widthScale, duration);
    }

    private void OnApplicationQuit()
    {
        quitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Add(GameObject prefab, Vector3 from, Vector3 to, float widthScale, float duration)
    {
        if (!pools.TryGetValue(prefab, out Pool pool))
        {
            LineRenderer source = prefab.GetComponentInChildren<LineRenderer>(true);
            if (source == null)
                return;
            pool = new Pool { Prefab = prefab, PrefabWidth = source.widthMultiplier };
            pools.Add(prefab, pool);
        }

        LineRenderer line = pool.Free.Count > 0 ? pool.Free.Pop() : Create(pool);
        if (line == null)
            return;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        float width = pool.PrefabWidth * widthScale;
        line.widthMultiplier = width;
        line.enabled = true;
        live.Add(new Tracer { Line = line, Width = width, Duration = duration, Age = 0f });
    }

    private LineRenderer Create(Pool pool)
    {
        GameObject copy = Instantiate(pool.Prefab, transform);
        LineRenderer line = copy.GetComponentInChildren<LineRenderer>(true);
        if (line == null)
        {
            Destroy(copy);
            return null;
        }
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        owners.Add(line, pool);
        return line;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Tracer tracer = live[i];
            tracer.Age += deltaTime;
            if (tracer.Age >= tracer.Duration)
            {
                tracer.Line.enabled = false;
                owners[tracer.Line].Free.Push(tracer.Line);
                live[i] = live[live.Count - 1];
                live.RemoveAt(live.Count - 1);
                continue;
            }

            // Full width for the first quarter, then thins out quadratically
            float t = Mathf.Clamp01((tracer.Age / tracer.Duration - 0.25f) / 0.75f);
            float remaining = 1f - t;
            tracer.Line.widthMultiplier = tracer.Width * remaining * remaining;
            live[i] = tracer;
        }
    }
}
