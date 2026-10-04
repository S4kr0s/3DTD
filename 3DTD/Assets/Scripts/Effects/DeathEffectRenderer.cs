using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The pop animation of every enemy layer (EnemyShape.SpawnDeathEffect): a copy of the layer's shape that grows
// with ExpandingShape.anim while its Decal material dissolves (_ShowPercent 0 -> 1). Every pop plays, at any game
// speed, without a GameObject each: all running pops of one shape and colour are drawn with
// Graphics.RenderMeshInstanced. The dissolve advances in DissolveSteps steps, so each step is one instanced
// draw with its own _ShowPercent; the growth is exact per pop.
//
// The look still comes from the enemy prefab (shape meshes, Layer*Animation materials) and the clip.
[DefaultExecutionOrder(1200)]
public class DeathEffectRenderer : MonoBehaviour
{
    public const int DissolveSteps = 16;
    private const int MaxInstancesPerDraw = 500;
    private static readonly int ShowPercent = Shader.PropertyToID("_ShowPercent");

    private struct Pop
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Start;
    }

    // One shape + colour: its mesh, material and the shape renderer's offset inside the shape object
    private sealed class Group
    {
        public Mesh Mesh;
        public Material Material;
        public int SubMeshCount;
        public Matrix4x4 RendererOffset;
        public int Layer;
        public readonly List<Pop> Pops = new List<Pop>();
    }

    private static DeathEffectRenderer instance;
    private static bool quitting;

    private readonly Dictionary<int, Group> groups = new Dictionary<int, Group>();
    private readonly List<Group> groupList = new List<Group>();
    private readonly Matrix4x4[][] stepMatrices = new Matrix4x4[DissolveSteps][];
    private readonly int[] stepCounts = new int[DissolveSteps];
    private readonly MaterialPropertyBlock[] stepProperties = new MaterialPropertyBlock[DissolveSteps];
    private AnimationCurve growth;
    private float duration = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    // Called at level start: creating the renderer allocates its draw buffers (~0.5 MB), not mid-wave
    public static void Prewarm()
    {
        _ = Instance;
    }

    private static DeathEffectRenderer Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
                instance = new GameObject("Death Effects").AddComponent<DeathEffectRenderer>();
            return instance;
        }
    }

    public static int ActivePops
    {
        get
        {
            if (instance == null)
                return 0;
            int count = 0;
            foreach (Group group in instance.groupList)
                count += group.Pops.Count;
            return count;
        }
    }

    private void Awake()
    {
        for (int step = 0; step < DissolveSteps; step++)
        {
            stepMatrices[step] = new Matrix4x4[MaxInstancesPerDraw];
            stepProperties[step] = new MaterialPropertyBlock();
            stepProperties[step].SetFloat(ShowPercent, (step + 0.5f) / DissolveSteps);
        }
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

    // source: the enemy's shape object for this id (it carries the clip); false if nothing could be drawn
    public static bool Play(GameObject source, int id, Vector3 position, Quaternion rotation)
    {
        DeathEffectRenderer renderer = Instance;
        if (renderer == null)
            return false;
        Group group = renderer.GetGroup(source, id);
        if (group == null)
            return false;
        group.Pops.Add(new Pop { Position = position, Rotation = rotation, Start = Time.time });
        return true;
    }

    private Group GetGroup(GameObject source, int id)
    {
        if (groups.TryGetValue(id, out Group group))
            return group;

        Renderer shapeRenderer = source.GetComponentInChildren<Renderer>(true);
        MeshFilter filter = shapeRenderer != null ? shapeRenderer.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null || shapeRenderer.sharedMaterial == null)
        {
            groups.Add(id, null);
            return null;
        }

        if (growth == null)
            ReadClip(source);

        group = new Group
        {
            Mesh = filter.sharedMesh,
            Material = shapeRenderer.sharedMaterial,
            SubMeshCount = Mathf.Min(filter.sharedMesh.subMeshCount, shapeRenderer.sharedMaterials.Length),
            // The pop copies the shape object: its renderer sits at this offset inside it
            RendererOffset = source.transform.worldToLocalMatrix * shapeRenderer.transform.localToWorldMatrix,
            Layer = source.layer,
        };
        groups.Add(id, group);
        groupList.Add(group);
        return group;
    }

    // ExpandingShape.anim, read once: the copy's local scale over time (it had no parent, so that is its size)
    private void ReadClip(GameObject source)
    {
        Animation animation = source.GetComponent<Animation>();
        AnimationClip clip = animation != null ? animation.clip : null;
        growth = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.5f, 0.5f));
        if (clip == null)
            return;

        duration = clip.length;
        GameObject probe = new GameObject("Death effect clip probe");
        probe.hideFlags = HideFlags.HideAndDontSave;
        Keyframe[] keys = new Keyframe[17];
        for (int i = 0; i < keys.Length; i++)
        {
            float time = duration * i / (keys.Length - 1);
            clip.SampleAnimation(probe, time);
            keys[i] = new Keyframe(time, probe.transform.localScale.x);
        }
        growth = new AnimationCurve(keys);
        DestroyImmediate(probe);
    }

    private static readonly Unity.Profiling.ProfilerMarker DrawMarker = new Unity.Profiling.ProfilerMarker("3DTD.DeathEffects");

    private void Update()
    {
        using var scope = DrawMarker.Auto();
        float now = Time.time;
        for (int g = 0; g < groupList.Count; g++)
            Draw(groupList[g], now);
    }

    private void Draw(Group group, float now)
    {
        List<Pop> pops = group.Pops;
        if (pops.Count == 0)
            return;

        System.Array.Clear(stepCounts, 0, DissolveSteps);
        Bounds bounds = new Bounds(pops[0].Position, Vector3.zero);
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            Pop pop = pops[i];
            float age = now - pop.Start;
            if (age >= duration)
            {
                pops[i] = pops[pops.Count - 1];
                pops.RemoveAt(pops.Count - 1);
                continue;
            }

            int step = Mathf.Clamp((int)(age / duration * DissolveSteps), 0, DissolveSteps - 1);
            float scale = growth.Evaluate(age);
            Matrix4x4 matrix = Matrix4x4.TRS(pop.Position, pop.Rotation, new Vector3(scale, scale, scale)) * group.RendererOffset;
            stepMatrices[step][stepCounts[step]++] = matrix;
            bounds.Encapsulate(pop.Position);
            if (stepCounts[step] == MaxInstancesPerDraw)
                Flush(group, step, bounds);
        }

        for (int step = 0; step < DissolveSteps; step++)
            Flush(group, step, bounds);
    }

    private void Flush(Group group, int step, Bounds bounds)
    {
        int count = stepCounts[step];
        if (count == 0)
            return;
        stepCounts[step] = 0;

        bounds.Expand(2f);
        RenderParams parameters = new RenderParams(group.Material)
        {
            layer = group.Layer,
            matProps = stepProperties[step],
            worldBounds = bounds,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = true,
            // The scene's ambient probe (instanced draws can't blend light probes per instance)
            lightProbeUsage = LightProbeUsage.Off,
        };
        for (int subMesh = 0; subMesh < Mathf.Max(1, group.SubMeshCount); subMesh++)
            Graphics.RenderMeshInstanced(parameters, group.Mesh, subMesh, stepMatrices[step], count);
    }
}
