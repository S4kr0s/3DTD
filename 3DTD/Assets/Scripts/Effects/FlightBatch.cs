using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

// The flight effect of a projectile (e.g. Polygon Arsenal's LaserBlue: a glow and a core that travel with the
// bolt, sparks it leaves behind, a trail) for ALL projectiles of a prefab, in one shared copy of the prefab.
//
// Each flight gets an id. Every frame ProjectileSystem reports where each flight is (SetPose); the batch emits
// what each flight's systems would have emitted (rate over time, rate over distance along the frame's path,
// looping bursts) and moves the fresh particles into place. Particles of systems that simulated in local space
// follow their flight: a Burst particle job moves them with it, shrinks them while it fades and removes them
// when it is gone, like the copy that used to be parented to the projectile. Systems with "Trail" in the name
// are left behind and play out, as PolygonProjectileScript intended. A TrailRenderer becomes a particle trail
// drawn behind one invisible head particle per flight.
//
// Prefabs this can't reproduce (sub-emitters, local-space forces, scripts, meshes, lights) use pooled copies.
public sealed class FlightBatch : IDisposable
{
    private const int InitialFlights = 1024;
    // A trail's head particle lives until its flight ends; the trail points last trail.time of that
    private const float HeadLifetime = 1000f;
    private const int IdBits = 18;
    private const uint IdMask = (1u << IdBits) - 1;
    private const int GenerationMask = 0x3FFF;

    private struct BurstOffset
    {
        public float Time;
        public ParticleSystem.MinMaxCurve Count;
    }

    private sealed class Emitter
    {
        public ParticleSystem System;
        public bool Follows;          // simulated in local space: moves with its flight
        public float RateOverTime;
        public float RateOverDistance;
        public BurstOffset[] Bursts;  // burst times within one loop of the system
        public float Duration;
        public float Delay;
        public bool Rotate3D;
        public float Prewarm;         // particles a prewarmed system already shows when the flight starts
        public bool HasTrails;        // particle trails: emitted at each flight's pose (see BatchedEffect)
        public float[] Owed;          // per flight: fractional particles carried over
        public NativeArray<ParticleSystem.Particle> Buffer;
    }

    private struct Flight
    {
        public float3 Position;
        public float3 Previous;
        public quaternion Rotation;
        public float Scale;
        public float LastScale;
        public float Clock;           // seconds since the flight started
        public bool Live;
        public bool Fresh;            // attached since the last update
    }

    public readonly GameObject Prefab;
    private readonly GameObject shared;
    private readonly Emitter[] emitters;
    private readonly ParticleSystem trailHeads;

    private Flight[] flights = new Flight[InitialFlights];
    private int[] managedGeneration = new int[InitialFlights];
    private readonly Stack<int> freeIds = new Stack<int>();
    private int highestId;
    private readonly List<Vector2Int> counts = new List<Vector2Int>();

    // Read by the follower jobs, written on the main thread before the particle update
    private NativeArray<float3> delta;
    private NativeArray<float3> center;
    private NativeArray<float> scaleRatio;
    private NativeArray<int> generation;
    private NativeArray<byte> alive;
    private JobHandle followJobs;

    public static FlightBatch TryCreate(GameObject prefab, Transform container)
    {
        int trails = 0;
        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            switch (component)
            {
                case Transform _:
                case ParticleSystemRenderer _:
                case PolygonArsenal.PolygonSoundSpawn sound when !sound.ShouldActivate:
                    continue;
                case TrailRenderer _:
                    trails++;
                    continue;
                case ParticleSystem system:
                    if (system.subEmitters.enabled && system.subEmitters.subEmittersCount > 0)
                        return null;
                    if (system.velocityOverLifetime.enabled && system.velocityOverLifetime.space == ParticleSystemSimulationSpace.Local)
                        return null;
                    if (system.forceOverLifetime.enabled && system.forceOverLifetime.space == ParticleSystemSimulationSpace.Local)
                        return null;
                    continue;
                default:
                    return null;
            }
        }
        return trails > 1 ? null : new FlightBatch(prefab, container);
    }

    private FlightBatch(GameObject prefab, Transform container)
    {
        Prefab = prefab;
        GameObject staging = new GameObject(prefab.name + " (flight staging)");
        staging.SetActive(false);
        staging.transform.SetParent(container, false);
        shared = UnityEngine.Object.Instantiate(prefab, staging.transform);
        shared.name = prefab.name + " (flights)";
        shared.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        shared.transform.localScale = Vector3.one;

        ParticleSystem[] systems = shared.GetComponentsInChildren<ParticleSystem>(true);
        emitters = new Emitter[systems.Length];
        for (int i = 0; i < systems.Length; i++)
            emitters[i] = Configure(systems[i]);

        TrailRenderer trail = shared.GetComponentInChildren<TrailRenderer>(true);
        if (trail != null)
            trailHeads = CreateTrailHeads(trail);

        delta = new NativeArray<float3>(InitialFlights, Allocator.Persistent);
        center = new NativeArray<float3>(InitialFlights, Allocator.Persistent);
        scaleRatio = new NativeArray<float>(InitialFlights, Allocator.Persistent);
        generation = new NativeArray<int>(InitialFlights, Allocator.Persistent);
        alive = new NativeArray<byte>(InitialFlights, Allocator.Persistent);

        shared.transform.SetParent(container, false);
        UnityEngine.Object.Destroy(staging);
        foreach (Emitter emitter in emitters)
        {
            if (emitter.Follows)
                FlightFollower.Attach(emitter.System, this, false);
            emitter.System.Play(false);
        }
        if (trailHeads != null)
        {
            FlightFollower.Attach(trailHeads, this, true);
            trailHeads.Play(false);
        }
    }

    private static Emitter Configure(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();

        Emitter emitter = new Emitter
        {
            System = system,
            HasTrails = system.trails.enabled,
            Follows = main.simulationSpace == ParticleSystemSimulationSpace.Local,
            Duration = Mathf.Max(0.01f, main.duration),
            Delay = EffectPool.MaxOf(main.startDelay),
            Owed = new float[InitialFlights],
        };

        List<BurstOffset> bursts = new List<BurstOffset>();
        if (emission.enabled)
        {
            emitter.RateOverTime = EffectPool.MaxOf(emission.rateOverTime);
            emitter.RateOverDistance = EffectPool.MaxOf(emission.rateOverDistance);
            for (int b = 0; b < emission.burstCount; b++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(b);
                int cycles = burst.cycleCount <= 0 ? int.MaxValue : burst.cycleCount;
                float interval = Mathf.Max(0.01f, burst.repeatInterval);
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    float time = burst.time + cycle * interval;
                    if (time >= emitter.Duration)
                        break;
                    bursts.Add(new BurstOffset { Time = time, Count = burst.count });
                }
            }
        }
        emitter.Bursts = bursts.ToArray();
        if (main.prewarm && main.loop)
            emitter.Prewarm = emitter.RateOverTime * EffectPool.MaxOf(main.startLifetime);
        // A non-looping flight system only emits during its first loop
        if (!main.loop)
            emitter.Duration = float.PositiveInfinity;

        bool mesh = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh;
        bool localAligned = renderer != null && renderer.alignment == ParticleSystemRenderSpace.Local;
        emitter.Rotate3D = (mesh || localAligned) && (renderer == null || renderer.alignment != ParticleSystemRenderSpace.Velocity);

        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = 0f;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = Mathf.Max(main.maxParticles, 4096);
        if (emitter.Rotate3D && !main.startRotation3D)
        {
            ParticleSystem.MinMaxCurve roll = main.startRotation;
            main.startRotation3D = true;
            main.startRotationZ = roll;
        }
        emission.enabled = false;
        // The flight's own movement reaches its particles through the follower job
        ParticleSystem.InheritVelocityModule inherit = system.inheritVelocity;
        inherit.enabled = false;

        if (renderer != null)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        return emitter;
    }

    // A TrailRenderer becomes a particle trail behind one invisible head particle per flight
    private ParticleSystem CreateTrailHeads(TrailRenderer trail)
    {
        GameObject host = new GameObject("Trail heads");
        host.transform.SetParent(shared.transform, false);
        ParticleSystem system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = HeadLifetime;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.maxParticles = 4096;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.TrailModule trails = system.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.lifetime = trail.time / HeadLifetime;
        trails.minVertexDistance = trail.minVertexDistance;
        trails.worldSpace = true;
        trails.dieWithParticles = false;
        trails.sizeAffectsWidth = false;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(trail.widthMultiplier, trail.widthCurve);
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(trail.colorGradient);
        trails.inheritParticleColor = false;
        trails.textureMode = trail.textureMode == LineTextureMode.Tile ? ParticleSystemTrailTextureMode.Tile : ParticleSystemTrailTextureMode.Stretch;

        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.None;
        renderer.trailMaterial = trail.sharedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        trail.enabled = false;
        return system;
    }

    public void Dispose()
    {
        followJobs.Complete();
        foreach (Emitter emitter in emitters)
        {
            if (emitter.Buffer.IsCreated)
                emitter.Buffer.Dispose();
        }
        delta.Dispose();
        center.Dispose();
        scaleRatio.Dispose();
        generation.Dispose();
        alive.Dispose();
    }

    // ---- flights ----------------------------------------------------------------------------------------

    public int Attach(Vector3 position, Quaternion rotation, float scale)
    {
        int id = freeIds.Count > 0 ? freeIds.Pop() : highestId++;
        EnsureCapacity(id + 1);
        managedGeneration[id] = (managedGeneration[id] + 1) & GenerationMask;
        flights[id] = new Flight
        {
            Position = position,
            Previous = position,
            Rotation = rotation,
            Scale = scale,
            LastScale = scale,
            Clock = -1e-6f,
            Live = true,
            Fresh = true,
        };
        foreach (Emitter emitter in emitters)
            emitter.Owed[id] = emitter.Prewarm;
        if (trailHeads != null)
            EmitHead(id, position);
        return id;
    }

    public void SetPose(int id, Vector3 position, Quaternion rotation, float scale)
    {
        Flight flight = flights[id];
        flight.Position = position;
        flight.Rotation = rotation;
        flight.Scale = scale;
        flights[id] = flight;
    }

    // The projectile is gone: followers vanish with it, trails and left-behind sparks play out
    public void Release(int id)
    {
        Flight flight = flights[id];
        flight.Live = false;
        flights[id] = flight;
        freeIds.Push(id);
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= flights.Length)
            return;
        followJobs.Complete();
        int size = Mathf.NextPowerOfTwo(needed);
        Array.Resize(ref flights, size);
        Array.Resize(ref managedGeneration, size);
        foreach (Emitter emitter in emitters)
            Array.Resize(ref emitter.Owed, size);
        Grow(ref delta, size);
        Grow(ref center, size);
        Grow(ref scaleRatio, size);
        Grow(ref generation, size);
        Grow(ref alive, size);
    }

    private static void Grow<T>(ref NativeArray<T> array, int size) where T : struct
    {
        NativeArray<T> grown = new NativeArray<T>(size, Allocator.Persistent);
        NativeArray<T>.Copy(array, grown, array.Length);
        array.Dispose();
        array = grown;
    }

    // ---- per frame --------------------------------------------------------------------------------------

    // Once per frame after the projectiles moved and before the particle update
    public void Update(float deltaTime)
    {
        followJobs.Complete();
        followJobs = default;
        if (deltaTime <= 0f)
            return;

        for (int id = 0; id < highestId; id++)
        {
            Flight flight = flights[id];
            if (!flight.Live && alive[id] == 0 && generation[id] == managedGeneration[id])
                continue;

            delta[id] = flight.Fresh ? float3.zero : flight.Position - flight.Previous;
            center[id] = flight.Previous;
            scaleRatio[id] = flight.Fresh || flight.LastScale <= 0f ? 1f : flight.Scale / flight.LastScale;
            generation[id] = managedGeneration[id];
            alive[id] = (byte)(flight.Live ? 1 : 0);
        }

        foreach (Emitter emitter in emitters)
            Emit(emitter, deltaTime);

        for (int id = 0; id < highestId; id++)
        {
            Flight flight = flights[id];
            if (!flight.Live)
                continue;
            flight.Previous = flight.Position;
            flight.LastScale = flight.Scale;
            flight.Clock += deltaTime;
            flight.Fresh = false;
            flights[id] = flight;
        }
    }

    private void Emit(Emitter emitter, float deltaTime)
    {
        counts.Clear();
        int total = 0;
        for (int id = 0; id < highestId; id++)
        {
            Flight flight = flights[id];
            if (!flight.Live)
                continue;

            float distance = flight.Fresh ? 0f : math.distance(flight.Position, flight.Previous);
            float amount = emitter.Owed[id] + emitter.RateOverTime * deltaTime + emitter.RateOverDistance * distance;
            int count = (int)amount;
            emitter.Owed[id] = amount - count;

            float before = flight.Clock - emitter.Delay;
            float after = before + deltaTime;
            for (int b = 0; b < emitter.Bursts.Length; b++)
            {
                int crossings = Crossings(before, after, emitter.Bursts[b].Time, emitter.Duration);
                for (int c = 0; c < crossings; c++)
                    count += BurstCount(emitter.Bursts[b].Count);
            }

            if (count > 0)
            {
                counts.Add(new Vector2Int(id, count));
                total += count;
            }
        }
        if (total == 0)
            return;

        if (emitter.HasTrails)
        {
            EmitAtPoses(emitter);
            return;
        }

        ParticleSystem system = emitter.System;
        int existing = system.particleCount;
        ParticleSystem.MainModule main = system.main;
        if (existing + total > main.maxParticles)
            main.maxParticles = Mathf.NextPowerOfTwo(existing + total);
        system.Emit(total);
        int emitted = system.particleCount - existing;
        if (emitted <= 0)
            return;

        if (!emitter.Buffer.IsCreated || emitter.Buffer.Length < emitted)
        {
            if (emitter.Buffer.IsCreated)
                emitter.Buffer.Dispose();
            emitter.Buffer = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(emitted), Allocator.Persistent);
        }

        NativeArray<ParticleSystem.Particle> buffer = emitter.Buffer;
        int read = system.GetParticles(buffer, emitted, existing);
        int index = 0;
        foreach (Vector2Int entry in counts)
        {
            Flight flight = flights[entry.x];
            Quaternion rotation = flight.Rotation;
            float scale = flight.Scale;
            uint tag = ((uint)managedGeneration[entry.x] << IdBits) | (uint)entry.x;
            for (int k = 0; k < entry.y && index < read; k++, index++)
            {
                ParticleSystem.Particle particle = buffer[index];
                // Followers start where the flight was last frame (the follower job moves them on with it);
                // left-behind particles are spread along the path it covered this frame
                Vector3 origin = emitter.Follows || flight.Fresh ? (Vector3)flight.Previous
                    : Vector3.Lerp(flight.Previous, flight.Position, (k + 0.5f) / entry.y);
                particle.position = origin + rotation * (particle.position * scale);
                particle.velocity = rotation * (particle.velocity * scale);
                particle.startSize3D *= scale;
                if (emitter.Rotate3D)
                    particle.rotation3D = (rotation * Quaternion.Euler(particle.rotation3D)).eulerAngles;
                if (emitter.Follows)
                    particle.randomSeed = tag;
                buffer[index] = particle;
            }
        }
        system.SetParticles(buffer, read, existing);
        PerfCounters.BatchedParticles += read;
    }

    // Particle trails start where a particle is emitted, so these systems emit at each flight's pose
    private void EmitAtPoses(Emitter emitter)
    {
        ParticleSystem system = emitter.System;
        Transform transform = system.transform;
        ParticleSystem.MainModule main = system.main;
        foreach (Vector2Int entry in counts)
        {
            Flight flight = flights[entry.x];
            int before = system.particleCount;
            if (before + entry.y > main.maxParticles)
                main.maxParticles = Mathf.NextPowerOfTwo(before + entry.y);
            transform.SetPositionAndRotation(emitter.Follows ? (Vector3)flight.Previous : (Vector3)flight.Position, flight.Rotation);
            system.Emit(entry.y);
            int emitted = system.particleCount - before;
            PerfCounters.BatchedParticles += emitted;
            if (emitted <= 0 || (!emitter.Follows && Mathf.Approximately(flight.Scale, 1f)))
                continue;

            if (!emitter.Buffer.IsCreated || emitter.Buffer.Length < emitted)
            {
                if (emitter.Buffer.IsCreated)
                    emitter.Buffer.Dispose();
                emitter.Buffer = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(emitted), Allocator.Persistent);
            }
            uint tag = ((uint)managedGeneration[entry.x] << IdBits) | (uint)entry.x;
            int read = system.GetParticles(emitter.Buffer, emitted, before);
            for (int i = 0; i < read; i++)
            {
                ParticleSystem.Particle particle = emitter.Buffer[i];
                particle.velocity *= flight.Scale;
                particle.startSize3D *= flight.Scale;
                if (emitter.Follows)
                    particle.randomSeed = tag;
                emitter.Buffer[i] = particle;
            }
            system.SetParticles(emitter.Buffer, read, before);
        }
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    // How many times offset + n * duration (n >= 0) lies in (before, after]
    private static int Crossings(float before, float after, float offset, float duration)
    {
        if (after < offset)
            return 0;
        if (float.IsInfinity(duration))
            return before < offset ? 1 : 0;
        int last = (int)math.floor((after - offset) / duration);
        int first = before < offset ? 0 : (int)math.floor((before - offset) / duration) + 1;
        return math.max(0, last - first + 1);
    }

    private static int BurstCount(ParticleSystem.MinMaxCurve count)
    {
        return count.mode == ParticleSystemCurveMode.TwoConstants
            ? Mathf.RoundToInt(Random.Range(count.constantMin, count.constantMax))
            : Mathf.RoundToInt(EffectPool.MaxOf(count));
    }

    private void EmitHead(int id, Vector3 position)
    {
        ParticleSystem.MainModule main = trailHeads.main;
        if (trailHeads.particleCount + 1 > main.maxParticles)
            main.maxParticles = Mathf.NextPowerOfTwo(trailHeads.particleCount + 1);
        trailHeads.Emit(new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startLifetime = HeadLifetime,
            startSize = 1f,
            randomSeed = ((uint)managedGeneration[id] << IdBits) | (uint)id,
            applyShapeToPosition = false,
        }, 1);
    }

    // ---- follower job -----------------------------------------------------------------------------------

    internal void ScheduleFollow(ParticleSystem system, bool heads)
    {
        JobHandle handle = new FollowJob
        {
            Delta = delta,
            Center = center,
            ScaleRatio = scaleRatio,
            Generation = generation,
            Alive = alive,
            Heads = heads,
        }.ScheduleBatch(system, 512);
        followJobs = JobHandle.CombineDependencies(followJobs, handle);
    }

    [BurstCompile]
    private struct FollowJob : IJobParticleSystemParallelForBatch
    {
        [ReadOnly] public NativeArray<float3> Delta;
        [ReadOnly] public NativeArray<float3> Center;
        [ReadOnly] public NativeArray<float> ScaleRatio;
        [ReadOnly] public NativeArray<int> Generation;
        [ReadOnly] public NativeArray<byte> Alive;
        public bool Heads;

        public void Execute(ParticleSystemJobData particles, int startIndex, int count)
        {
            NativeArray<uint> seeds = particles.randomSeeds;
            NativeArray<float> lifetime = particles.aliveTimePercent;
            ParticleSystemNativeArray3 positions = particles.positions;
            ParticleSystemNativeArray3 sizes = particles.sizes;
            int end = startIndex + count;
            for (int i = startIndex; i < end; i++)
            {
                uint seed = seeds[i];
                int id = (int)(seed & IdMask);
                int gen = (int)(seed >> IdBits);
                if (id >= Generation.Length || Generation[id] != gen || Alive[id] == 0)
                {
                    // The flight is gone (or its id went to another projectile): the particle goes with it
                    lifetime[i] = 100f;
                    continue;
                }

                float3 moved = Center[id] + Delta[id];
                if (Heads)
                {
                    positions[i] = moved;
                    continue;
                }

                float3 position = positions[i];
                float ratio = ScaleRatio[id];
                if (ratio != 1f)
                {
                    position = Center[id] + (position - Center[id]) * ratio;
                    sizes[i] = (float3)sizes[i] * ratio;
                }
                positions[i] = position + Delta[id];
            }
        }
    }
}

// Lets a particle system schedule FlightBatch's follower job right after its own update job
public sealed class FlightFollower : MonoBehaviour
{
    private FlightBatch batch;
    private ParticleSystem system;
    private bool heads;

    public static void Attach(ParticleSystem system, FlightBatch batch, bool heads)
    {
        FlightFollower follower = system.gameObject.AddComponent<FlightFollower>();
        follower.batch = batch;
        follower.system = system;
        follower.heads = heads;
    }

    private void OnParticleUpdateJobScheduled()
    {
        if (batch != null)
            batch.ScheduleFollow(system, heads);
    }
}
