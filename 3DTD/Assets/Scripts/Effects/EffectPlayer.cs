using System.Collections.Generic;
using UnityEngine;

// Plays the particle effect prefabs of projectiles, towers and mines (Polygon Arsenal muzzle flashes, impacts,
// explosions and the flight effects that follow a projectile) without Instantiate/Destroy per effect.
//
// Every prefab gets a pool of instances that stay active and are replayed in place. Nothing is cut short:
// a one-shot effect plays until its last particle is gone (or until the lifetime cap a caller passes, which
// mirrors the Destroy delay the old code used). A flight effect follows its projectile; when the projectile
// dies, its systems with "Trail" in the name stop emitting and play out, the rest vanish with the projectile.
//
// Particle lights (the Lights module) share a budget: the newest LightBudget effects keep their lights.
// Effect renderers cast no shadows. AudioSources that played on awake are replayed through EffectAudio.
[DefaultExecutionOrder(1100)]
public class EffectPlayer : MonoBehaviour
{
    public const int LightBudget = 64;

    // Off: every effect plays through pooled copies (for comparing the looks; -perfNoBatching)
    public static bool BatchingEnabled = true;

    private static EffectPlayer instance;
    private static bool quitting;

    private readonly Dictionary<GameObject, EffectPool> pools = new Dictionary<GameObject, EffectPool>();
    private readonly List<EffectPool> poolList = new List<EffectPool>();
    // Lit effects in the order they took their light; an entry is stale once its ticket no longer matches
    private readonly Queue<(EffectInstance effect, int ticket)> lightHolders = new Queue<(EffectInstance, int)>();
    private int lightsInUse;
    private int nextLightTicket;
    private Transform container;

    // Statics survive play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    private static EffectPlayer Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                GameObject host = new GameObject("Effect Player");
                instance = host.AddComponent<EffectPlayer>();
            }
            return instance;
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
        foreach (EffectPool pool in poolList)
            pool.Dispose();
    }

    // One-shot effect. maxLifetime caps how long it may play (the Destroy delay of the code it replaces);
    // age pre-ages it, for effects that started earlier in the frame (game time).
    public static void Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, float maxLifetime = float.PositiveInfinity, float age = 0f)
    {
        if (prefab == null)
            return;
        PerfCounters.EffectsRequested++;
        EffectPlayer player = Instance;
        if (player == null)
            return;
        player.GetPool(prefab).PlayOneShot(position, rotation, scale, maxLifetime, age);
        PerfCounters.EffectsPlayed++;
    }

    // Looping effect that follows a projectile until Release
    public static FlightHandle Attach(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
    {
        EffectPlayer player = Instance;
        if (prefab == null || player == null)
            return default;
        return player.GetPool(prefab).Attach(position, rotation, scale);
    }

    internal static void LogBackend(GameObject prefab, string backend)
    {
        if (Debug.isDebugBuild)
            Debug.Log("EffectPlayer: " + prefab.name + " plays " + backend);
    }

    private EffectPool GetPool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out EffectPool pool))
        {
            if (container == null)
            {
                container = new GameObject("Effects").transform;
                container.SetParent(transform, false);
            }
            pool = new EffectPool(this, prefab, container);
            pools.Add(prefab, pool);
            poolList.Add(pool);
        }
        return pool;
    }

    private static readonly Unity.Profiling.ProfilerMarker UpdateMarker = new Unity.Profiling.ProfilerMarker("3DTD.Effects.Update");

    private void Update()
    {
        using var scope = UpdateMarker.Auto();
        float now = Time.time;
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < poolList.Count; i++)
            poolList[i].Update(now, deltaTime);
    }

    // ---- light budget ------------------------------------------------------------------------------------

    internal void TakeLight(EffectInstance effect)
    {
        if (!effect.HasLights)
            return;

        // Give the light of the oldest still lit effect to the new one
        while (lightsInUse >= LightBudget && lightHolders.Count > 0)
        {
            (EffectInstance oldest, int ticket) = lightHolders.Dequeue();
            if (oldest.LightsOn && oldest.LightTicket == ticket)
            {
                oldest.SetLights(false);
                lightsInUse--;
            }
        }
        effect.LightTicket = ++nextLightTicket;
        effect.SetLights(true);
        lightsInUse++;
        lightHolders.Enqueue((effect, effect.LightTicket));
    }

    internal void ReturnLight(EffectInstance effect)
    {
        if (effect.LightsOn)
        {
            effect.SetLights(false);
            lightsInUse--;
        }
    }
}
