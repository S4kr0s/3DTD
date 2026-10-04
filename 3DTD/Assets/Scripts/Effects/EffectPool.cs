using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Plays one effect prefab (see EffectPlayer): through a batched shared copy when the prefab allows it
// (BatchedEffect for one-shots, FlightBatch for flight effects), otherwise through pooled copies
public class EffectPool
{
    // A looping one-shot effect without a lifetime cap still ends after this long
    private const float MaxOpenEndedLifetime = 5f;

    private readonly EffectPlayer owner;
    private readonly GameObject prefab;
    private readonly Transform container;
    private readonly Transform staging;
    private readonly Stack<EffectInstance> free = new Stack<EffectInstance>();
    private readonly List<EffectInstance> playing = new List<EffectInstance>();
    // Flights are spread over several shared copies: Unity updates each particle system and builds its geometry
    // in one job, so all flights of a prefab in one system would leave the render thread waiting on one core.
    // About a third of the cores measured best (M4 Pro, S3 benchmark: 3 and 7 shards were both slower than 4).
    private const int FlightsPerShard = 128;
    private static readonly int MaxFlightShards = Mathf.Clamp(SystemInfo.processorCount / 3, 1, 8);

    private BatchedEffect batch;
    private readonly List<FlightBatch> flightBatches = new List<FlightBatch>();
    private bool batchChecked;
    private bool flightBatchChecked;

    // Seconds from Play until the last particle of a one-shot is gone, and how long the trail parts of a
    // flight effect still show after it stopped emitting
    private readonly float naturalLifetime;
    private readonly float trailLinger;
    // Callers scale relative to the prefab's own scale (projectiles used to scale their child effect)
    private readonly Vector3 prefabScale;

    public EffectPool(EffectPlayer owner, GameObject prefab, Transform container)
    {
        this.owner = owner;
        this.prefab = prefab;
        this.container = container;

        GameObject stagingObject = new GameObject(prefab.name + " (staging)");
        stagingObject.SetActive(false);
        stagingObject.transform.SetParent(container, false);
        staging = stagingObject.transform;

        prefabScale = prefab.transform.localScale;
        naturalLifetime = 0f;
        trailLinger = 0f;
        foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            float lifetime = MaxOf(system.main.startLifetime);
            naturalLifetime = Mathf.Max(naturalLifetime, MaxOf(system.main.startDelay) + EmissionEnd(system) + lifetime);
            if (EffectInstance.IsTrailName(system.name))
                trailLinger = Mathf.Max(trailLinger, lifetime);
        }
        foreach (TrailRenderer trail in prefab.GetComponentsInChildren<TrailRenderer>(true))
        {
            naturalLifetime = Mathf.Max(naturalLifetime, trail.time);
            trailLinger = Mathf.Max(trailLinger, trail.time);
        }
        naturalLifetime += 0.05f;
    }

    public void PlayOneShot(Vector3 position, Quaternion rotation, float scale, float maxLifetime, float age)
    {
        if (!batchChecked)
        {
            using var scope = CreateMarker.Auto();
            batchChecked = true;
            batch = EffectPlayer.BatchingEnabled ? BatchedEffect.TryCreate(prefab, container) : null;
            EffectPlayer.LogBackend(prefab, batch != null ? "batched one-shot" : "pooled one-shot");
        }
        if (batch != null)
        {
            batch.Play(position, rotation, scale, age);
            return;
        }

        EffectInstance effect = Take();
        effect.SetPose(position, rotation, scale);
        effect.Play();
        owner.TakeLight(effect);
        float lifetime = Mathf.Min(naturalLifetime, maxLifetime);
        if (float.IsInfinity(lifetime))
            lifetime = MaxOpenEndedLifetime;
        effect.EndTime = Time.time + Mathf.Max(0f, lifetime - age);
        playing.Add(effect);
    }

    public FlightHandle Attach(Vector3 position, Quaternion rotation, float scale, Vector3 velocity)
    {
        if (!flightBatchChecked)
        {
            using var scope = CreateMarker.Auto();
            flightBatchChecked = true;
            FlightBatch first = EffectPlayer.BatchingEnabled ? FlightBatch.TryCreate(prefab, container) : null;
            if (first != null)
                flightBatches.Add(first);
            EffectPlayer.LogBackend(prefab, first != null ? "batched flight" : "pooled flight");
        }
        if (flightBatches.Count > 0)
        {
            FlightBatch shard = PickShard();
            return new FlightHandle(shard, shard.Attach(position, rotation, scale, velocity));
        }

        EffectInstance effect = Take();
        effect.SetPose(position, rotation, scale);
        effect.Play();
        owner.TakeLight(effect);
        effect.Attached = true;
        return new FlightHandle(effect);
    }

    // The projectile is gone: trails play out, everything else vanishes
    public void Detach(EffectInstance effect)
    {
        effect.Attached = false;
        effect.StopEmitting();
        effect.EndTime = Time.time + trailLinger + 0.05f;
        playing.Add(effect);
    }

    // Once per frame: batched effects emit what is due, finished pooled copies go back to the pool
    public void Update(float now, float deltaTime)
    {
        if (batch != null)
            batch.Emit(now, deltaTime);
        for (int i = 0; i < flightBatches.Count; i++)
            flightBatches[i].Update(deltaTime);
        Retire(now);
    }

    public void Dispose()
    {
        batch?.Dispose();
        foreach (FlightBatch shard in flightBatches)
            shard.Dispose();
    }

    // The shard with the fewest live flights; a new one once all are busy
    private FlightBatch PickShard()
    {
        FlightBatch best = flightBatches[0];
        for (int i = 1; i < flightBatches.Count; i++)
        {
            if (flightBatches[i].LiveFlights < best.LiveFlights)
                best = flightBatches[i];
        }
        if (best.LiveFlights >= FlightsPerShard && flightBatches.Count < MaxFlightShards)
        {
            using var scope = CreateMarker.Auto();
            FlightBatch shard = FlightBatch.TryCreate(prefab, container);
            if (shard != null)
            {
                flightBatches.Add(shard);
                best = shard;
            }
        }
        return best;
    }

    // Puts finished effects back into the pool
    private void Retire(float now)
    {
        using var scope = EffectMarkers.Retire.Auto();
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            EffectInstance effect = playing[i];
            if (effect.EndTime > now)
                continue;

            int last = playing.Count - 1;
            playing[i] = playing[last];
            playing.RemoveAt(last);

            effect.StopAndClear();
            owner.ReturnLight(effect);
            free.Push(effect);
        }
    }

    private EffectInstance Take()
    {
        return free.Count > 0 ? free.Pop() : Create();
    }

    private static readonly Unity.Profiling.ProfilerMarker CreateMarker = new Unity.Profiling.ProfilerMarker("3DTD.Effects.Create");

    private EffectInstance Create()
    {
        using var scope = CreateMarker.Auto();
        // Built under an inactive parent so nothing plays on awake before it is configured
        GameObject instance = Object.Instantiate(prefab, staging);
        EffectInstance effect = new EffectInstance(this, instance, prefabScale);
        instance.transform.SetParent(container, false);
        return effect;
    }

    private static float EmissionEnd(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        if (!emission.enabled)
            return 0f;

        bool continuous = MaxOf(emission.rateOverTime) > 0f || MaxOf(emission.rateOverDistance) > 0f;
        if (main.loop && (continuous || emission.burstCount > 0))
            return float.PositiveInfinity;
        if (continuous)
            return main.duration;

        float end = 0f;
        for (int i = 0; i < emission.burstCount; i++)
        {
            ParticleSystem.Burst burst = emission.GetBurst(i);
            float last = burst.cycleCount <= 0 ? main.duration : burst.time + (burst.cycleCount - 1) * burst.repeatInterval;
            end = Mathf.Max(end, Mathf.Min(last, main.duration));
        }
        return end;
    }

    public static float MaxOf(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(curve.constantMin, curve.constantMax);
            case ParticleSystemCurveMode.Curve:
                return curve.curveMultiplier * MaxOfCurve(curve.curve);
            default:
                return curve.curveMultiplier * Mathf.Max(MaxOfCurve(curve.curveMin), MaxOfCurve(curve.curveMax));
        }
    }

    private static float MaxOfCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0)
            return 1f;
        float max = float.MinValue;
        for (int i = 0; i < curve.length; i++)
            max = Mathf.Max(max, curve[i].value);
        return max;
    }
}

// A flight effect attached to a projectile, played batched or by a pooled copy
public readonly struct FlightHandle
{
    private readonly FlightBatch batch;
    private readonly int id;
    private readonly EffectInstance pooled;

    public FlightHandle(FlightBatch batch, int id)
    {
        this.batch = batch;
        this.id = id;
        pooled = null;
    }

    public FlightHandle(EffectInstance pooled)
    {
        batch = null;
        id = -1;
        this.pooled = pooled;
    }

    public bool IsValid => batch != null || pooled != null;

    public void SetPose(Vector3 position, Quaternion rotation, float scale, Vector3 velocity)
    {
        if (batch != null)
            batch.SetPose(id, position, rotation, scale, velocity);
        else
            pooled?.SetPose(position, rotation, scale);
    }

    public void Release()
    {
        if (batch != null)
            batch.Release(id);
        else
            pooled?.Release();
    }
}

// One pooled, always active copy of an effect prefab
public class EffectInstance
{
    public readonly GameObject GameObject;
    public readonly Transform Transform;
    public float EndTime;
    public bool Attached;
    public int LightTicket;
    public bool LightsOn { get; private set; }
    public bool HasLights => lightSystems.Length > 0;

    private readonly EffectPool pool;
    private readonly Vector3 prefabScale;
    private readonly ParticleSystem[] systems;
    private readonly bool[] isTrail;
    private readonly TrailRenderer[] trailRenderers;
    private readonly ParticleSystem[] lightSystems;
    private readonly AudioSource[] audioSources;

    public static bool IsTrailName(string name)
    {
        return name.IndexOf("Trail", System.StringComparison.Ordinal) >= 0;
    }

    public EffectInstance(EffectPool pool, GameObject instance, Vector3 prefabScale)
    {
        this.pool = pool;
        this.prefabScale = prefabScale;
        GameObject = instance;
        Transform = instance.transform;

        systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        isTrail = new bool[systems.Length];
        List<ParticleSystem> lit = new List<ParticleSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            // Pooled: the effect must survive its end ("Destroy" was the prefab's stop action)
            main.stopAction = ParticleSystemStopAction.None;
            isTrail[i] = IsTrailName(system.name);
            if (system.TryGetComponent(out ParticleSystemRenderer renderer))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (system.lights.enabled)
                lit.Add(system);
        }
        lightSystems = lit.ToArray();

        trailRenderers = instance.GetComponentsInChildren<TrailRenderer>(true);
        foreach (TrailRenderer trail in trailRenderers)
            trail.shadowCastingMode = ShadowCastingMode.Off;

        List<AudioSource> sounds = new List<AudioSource>();
        foreach (AudioSource source in instance.GetComponentsInChildren<AudioSource>(true))
        {
            if (source.playOnAwake)
            {
                source.playOnAwake = false;
                sounds.Add(source);
            }
        }
        audioSources = sounds.ToArray();
    }

    public void SetPose(Vector3 position, Quaternion rotation, float scale)
    {
        Transform.SetPositionAndRotation(position, rotation);
        Transform.localScale = prefabScale * scale;
    }

    public void Play()
    {
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            trailRenderers[i].Clear();
            trailRenderers[i].emitting = true;
        }
        for (int i = 0; i < systems.Length; i++)
            systems[i].Play(false);
        for (int i = 0; i < audioSources.Length; i++)
            EffectAudio.Play(audioSources[i]);
    }

    // Trails keep their particles and fade out; everything else vanishes with the projectile
    public void StopEmitting()
    {
        for (int i = 0; i < systems.Length; i++)
            systems[i].Stop(false, isTrail[i] ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
        for (int i = 0; i < trailRenderers.Length; i++)
            trailRenderers[i].emitting = false;
    }

    public void StopAndClear()
    {
        for (int i = 0; i < systems.Length; i++)
            systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            trailRenderers[i].emitting = false;
            trailRenderers[i].Clear();
        }
    }

    public void SetLights(bool on)
    {
        LightsOn = on;
        for (int i = 0; i < lightSystems.Length; i++)
        {
            ParticleSystem.LightsModule lights = lightSystems[i].lights;
            lights.enabled = on;
        }
    }

    // Called by the owner of an attached (flight) effect when its projectile is gone
    public void Release()
    {
        if (Attached)
            pool.Detach(this);
    }
}
