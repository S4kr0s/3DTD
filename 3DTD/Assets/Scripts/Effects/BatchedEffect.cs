using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

// A one-shot effect prefab (muzzle flash, impact, explosion) played through ONE shared, always running copy:
// every play of the prefab emits its particles into the same particle systems, so a thousand impacts cost what
// one costs in systems, renderers and draw calls instead of a thousand copies.
//
// The shared copy simulates in world space at the origin with emission switched off. A play queues the
// prefab's own bursts (start delay, burst times, cycles, counts, probability) and its rate over time; once per
// frame each system emits everything that is due in one Emit call, and the fresh particles are moved to their
// play's position, rotation and scale (velocity, size and 3D rotation included) before the particle update
// simulates them. Everything else (lifetime curves, colour, trails, collision, lights) stays the prefab's.
//
// Prefabs whose systems can't be reproduced this way (sub-emitters, forces in local space, trail renderers,
// static lights or meshes, scripts) keep using pooled copies (EffectPool).
public sealed class BatchedEffect : IDisposable
{
    // Particle lights share a budget: each lit system of a batched effect may show this many at once
    public const int MaxLightsPerSystem = 32;

    // See ProjectileSystem.WarmUpJobs
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmUpJobs()
    {
        IJobParallelForExtensions.EarlyJobInit<PlaceJob>();
    }

    private struct PlayRecord
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
    }

    // Requests and streams carry their play's pose, so nothing has to keep a list of plays that only grows
    // while any request is pending
    private struct Request
    {
        public float Time;      // game time the particles are due
        public int Count;
        public PlayRecord Pose;
    }

    private struct Stream
    {
        public float Start;
        public float End;
        public float Carry;
        public PlayRecord Pose;
    }

    private sealed class Emitter
    {
        public ParticleSystem System;
        public float Delay;
        public ParticleSystem.Burst[] Bursts;
        public float Rate;
        public float Duration;
        public bool Rotate3D;
        // Particle trails remember where a particle was emitted, so these emit at each play's pose directly
        // (moving a fresh particle afterwards would draw a trail from the shared copy's origin)
        public bool HasTrails;
        // Sized for a busy frame up front, so they don't grow (allocate) mid-wave
        public readonly List<Request> Pending = new List<Request>(256);
        public readonly List<Stream> Streams = new List<Stream>(64);
        public readonly List<Request> Due = new List<Request>(256);
        public NativeArray<ParticleSystem.Particle> Buffer;
    }

    public readonly GameObject Prefab;
    // The prefab's own (uniform) scale: plays scale relative to it, like the pooled copies
    private readonly float rootScale;
    private readonly GameObject shared;
    private readonly Emitter[] emitters;
    private readonly AudioSource[] sounds;
    private NativeList<DueEntry> dueEntries = new NativeList<DueEntry>(256, Allocator.Persistent);

    private struct DueEntry
    {
        public PlayRecord Pose;
        public int Start;
        public int Count;
        public float Age;
    }

    public static BatchedEffect TryCreate(GameObject prefab, Transform container)
    {
        if (!IsBatchable(prefab))
            return null;
        return new BatchedEffect(prefab, container);
    }

    // Everything in the prefab must be reproducible by emitting into shared world-space systems
    public static bool IsBatchable(GameObject prefab)
    {
        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            switch (component)
            {
                case Transform _:
                case ParticleSystemRenderer _:
                case AudioSource _:
                case PolygonArsenal.PolygonSoundSpawn sound when !sound.ShouldActivate:
                    continue;
                case ParticleSystem system:
                    if (!IsBatchable(system))
                        return false;
                    continue;
                default:
                    return false;   // trail renderers, meshes, lights, other scripts
            }
        }
        return true;
    }

    private static bool IsBatchable(ParticleSystem system)
    {
        if (!ParticleBatching.CanBatch(system))
            return false;
        // A one-shot effect that keeps emitting forever has no defined end
        if (system.main.loop && (EffectPool.MaxOf(system.emission.rateOverTime) > 0f || system.emission.burstCount > 0) && system.emission.enabled)
            return false;
        return true;
    }

    private BatchedEffect(GameObject prefab, Transform container)
    {
        Prefab = prefab;
        rootScale = prefab.transform.localScale.x;

        // Built under an inactive parent so nothing plays on awake before it is configured
        GameObject staging = new GameObject(prefab.name + " (batched staging)");
        staging.SetActive(false);
        staging.transform.SetParent(container, false);
        shared = UnityEngine.Object.Instantiate(prefab, staging.transform);
        shared.name = prefab.name + " (batched)";
        shared.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        shared.transform.localScale = Vector3.one;

        ParticleSystem[] systems = shared.GetComponentsInChildren<ParticleSystem>(true);
        emitters = new Emitter[systems.Length];
        for (int i = 0; i < systems.Length; i++)
            emitters[i] = Configure(systems[i]);

        List<AudioSource> audio = new List<AudioSource>();
        foreach (AudioSource source in shared.GetComponentsInChildren<AudioSource>(true))
        {
            if (source.playOnAwake)
                audio.Add(source);
            source.playOnAwake = false;
            source.enabled = false;
        }
        sounds = audio.ToArray();

        shared.transform.SetParent(container, false);
        UnityEngine.Object.Destroy(staging);
        foreach (Emitter emitter in emitters)
            emitter.System.Play(false);
    }

    private static Emitter Configure(ParticleSystem system)
    {
        Emitter emitter = new Emitter { System = system, HasTrails = system.trails.enabled };
        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;

        emitter.Delay = EffectPool.MaxOf(main.startDelay);
        emitter.Duration = main.duration;
        if (emission.enabled)
        {
            emitter.Rate = EffectPool.MaxOf(emission.rateOverTime);
            emitter.Bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(emitter.Bursts);
        }
        else
        {
            emitter.Bursts = Array.Empty<ParticleSystem.Burst>();
        }

        emitter.Rotate3D = ParticleBatching.NeedsRotate3D(system);
        // Every play emits from a standing copy (inherit velocity off); the shared transform only moves while emitting
        ParticleBatching.ConfigureShared(system, emitter.Rotate3D, 1000);
        return emitter;
    }

    public void Dispose()
    {
        dueEntries.Dispose();
        foreach (Emitter emitter in emitters)
        {
            if (emitter.Buffer.IsCreated)
                emitter.Buffer.Dispose();
        }
    }

    // Queues one play of the effect; age = how long ago (game time) it started
    public void Play(Vector3 position, Quaternion rotation, float scale, float age)
    {
        PlayRecord pose = new PlayRecord { Position = position, Rotation = rotation, Scale = scale * rootScale };
        float start = Time.time - age;

        foreach (Emitter emitter in emitters)
        {
            float begin = start + emitter.Delay;
            foreach (ParticleSystem.Burst burst in emitter.Bursts)
            {
                int cycles = burst.cycleCount <= 0 ? Mathf.Max(1, Mathf.FloorToInt(emitter.Duration / Mathf.Max(0.0001f, burst.repeatInterval))) : burst.cycleCount;
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    float time = burst.time + cycle * burst.repeatInterval;
                    if (time > emitter.Duration)
                        break;
                    if (burst.probability < 1f && Random.value > burst.probability)
                        continue;
                    int count = BurstCount(burst.count);
                    if (count > 0)
                        emitter.Pending.Add(new Request { Time = begin + time, Count = count, Pose = pose });
                }
            }
            if (emitter.Rate > 0f)
                emitter.Streams.Add(new Stream { Start = begin, End = begin + emitter.Duration, Pose = pose });
        }

        for (int i = 0; i < sounds.Length; i++)
            EffectAudio.PlayAt(sounds[i], position);
    }

    private static readonly Unity.Profiling.ProfilerMarker TrailEmitMarker = new Unity.Profiling.ProfilerMarker("3DTD.Effects.TrailEmit");

    // One Emit per play at the play's pose, so trails start where the particle does
    private void EmitAtPoses(Emitter emitter, float now)
    {
        using var scope = TrailEmitMarker.Auto();
        ParticleSystem system = emitter.System;
        Transform transform = system.transform;
        ParticleSystem.MainModule main = system.main;
        for (int r = 0; r < emitter.Due.Count; r++)
        {
            Request request = emitter.Due[r];
            PlayRecord play = request.Pose;
            int before = system.particleCount;
            if (before + request.Count > main.maxParticles)
                main.maxParticles = Mathf.NextPowerOfTwo(before + request.Count);

            transform.SetPositionAndRotation(play.Position, play.Rotation);
            EffectMarkers.Emit.Begin();
            system.Emit(request.Count);
            EffectMarkers.Emit.End();
            int emitted = system.particleCount - before;
            PerfCounters.BatchedParticles += emitted;

            // Size and speed follow the play's scale; positions stay where the trails began
            if (emitted <= 0 || Mathf.Approximately(play.Scale, 1f))
                continue;
            ParticleBatching.EnsureBuffer(ref emitter.Buffer, emitted);
            EffectMarkers.Particles.Begin();
            int read = system.GetParticles(emitter.Buffer, emitted, before);
            EffectMarkers.Particles.End();
            for (int i = 0; i < read; i++)
            {
                ParticleSystem.Particle particle = emitter.Buffer[i];
                particle.velocity *= play.Scale;
                particle.startSize3D *= play.Scale;
                emitter.Buffer[i] = particle;
            }
            EffectMarkers.Particles.Begin();
            system.SetParticles(emitter.Buffer, read, before);
            EffectMarkers.Particles.End();
        }
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private static int BurstCount(ParticleSystem.MinMaxCurve count)
    {
        switch (count.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return Mathf.RoundToInt(count.constant);
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.RoundToInt(Random.Range(count.constantMin, count.constantMax));
            default:
                return Mathf.RoundToInt(count.Evaluate(0f, Random.value));
        }
    }

    // Once per frame, before the particle systems update: emit what is due and move it into place
    public void Emit(float now, float deltaTime)
    {
        using var scope = EffectMarkers.Batched.Auto();
        foreach (Emitter emitter in emitters)
        {
            emitter.Due.Clear();
            for (int i = emitter.Pending.Count - 1; i >= 0; i--)
            {
                Request request = emitter.Pending[i];
                if (request.Time > now)
                    continue;
                emitter.Due.Add(request);
                emitter.Pending[i] = emitter.Pending[emitter.Pending.Count - 1];
                emitter.Pending.RemoveAt(emitter.Pending.Count - 1);
            }

            for (int i = emitter.Streams.Count - 1; i >= 0; i--)
            {
                Stream stream = emitter.Streams[i];
                float from = Mathf.Max(stream.Start, now - deltaTime);
                float to = Mathf.Min(stream.End, now);
                if (to > from)
                {
                    stream.Carry += emitter.Rate * (to - from);
                    int count = Mathf.FloorToInt(stream.Carry);
                    stream.Carry -= count;
                    if (count > 0)
                        emitter.Due.Add(new Request { Time = to, Count = count, Pose = stream.Pose });
                }
                if (stream.End <= now)
                {
                    emitter.Streams[i] = emitter.Streams[emitter.Streams.Count - 1];
                    emitter.Streams.RemoveAt(emitter.Streams.Count - 1);
                }
                else
                {
                    emitter.Streams[i] = stream;
                }
            }

            EmitDue(emitter, now);
        }
    }

    private void EmitDue(Emitter emitter, float now)
    {
        if (emitter.HasTrails)
        {
            EmitAtPoses(emitter, now);
            return;
        }

        int total = 0;
        for (int i = 0; i < emitter.Due.Count; i++)
            total += emitter.Due[i].Count;
        if (total == 0)
            return;

        ParticleSystem system = emitter.System;
        int before = system.particleCount;
        ParticleSystem.MainModule main = system.main;
        if (before + total > main.maxParticles)
            main.maxParticles = Mathf.NextPowerOfTwo(before + total);

        EffectMarkers.Emit.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long emitStart = EffectStats.Now;
#endif
        system.Emit(total);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EffectStats.Record(system, Prefab, total, before, emitStart);
#endif
        EffectMarkers.Emit.End();
        int emitted = system.particleCount - before;
        if (emitted <= 0)
            return;

        ParticleBatching.EnsureBuffer(ref emitter.Buffer, emitted);

        NativeArray<ParticleSystem.Particle> buffer = emitter.Buffer;
        EffectMarkers.Particles.Begin();
        int read = system.GetParticles(buffer, emitted, before);
        EffectMarkers.Particles.End();
        dueEntries.Clear();
        int start = 0;
        for (int r = 0; r < emitter.Due.Count; r++)
        {
            Request request = emitter.Due[r];
            dueEntries.Add(new DueEntry { Pose = request.Pose, Start = start, Count = request.Count, Age = Mathf.Max(0f, now - request.Time) });
            start += request.Count;
        }
        new PlaceJob
        {
            Entries = dueEntries.AsArray(),
            Particles = buffer,
            Read = read,
            Rotate3D = emitter.Rotate3D,
        }.Schedule(dueEntries.Length, 16).Complete();
        EffectMarkers.Particles.Begin();
        system.SetParticles(buffer, read, before);
        EffectMarkers.Particles.End();
        PerfCounters.BatchedParticles += read;
    }

    // Moves each play's block of fresh particles to the play's pose, scale and age
    [BurstCompile]
    private struct PlaceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<DueEntry> Entries;
        [NativeDisableParallelForRestriction] public NativeArray<ParticleSystem.Particle> Particles;
        public int Read;
        public bool Rotate3D;

        public void Execute(int e)
        {
            DueEntry entry = Entries[e];
            PlayRecord play = entry.Pose;
            quaternion rotation = play.Rotation;
            for (int k = 0; k < entry.Count; k++)
            {
                int index = entry.Start + k;
                if (index >= Read)
                    return;
                ParticleSystem.Particle particle = Particles[index];
                float3 velocity = math.mul(rotation, (float3)particle.velocity * play.Scale);
                particle.position = (float3)play.Position + math.mul(rotation, (float3)particle.position * play.Scale) + velocity * entry.Age;
                particle.velocity = velocity;
                particle.startSize3D = (float3)particle.startSize3D * play.Scale;
                if (Rotate3D)
                    particle.rotation3D = EffectMath.ComposeEulerDegrees(rotation, particle.rotation3D);
                particle.remainingLifetime = math.max(0.0001f, particle.remainingLifetime - entry.Age);
                Particles[index] = particle;
            }
        }
    }
}
