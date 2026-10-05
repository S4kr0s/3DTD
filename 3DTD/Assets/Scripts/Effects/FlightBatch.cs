using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;
using UnityEngine.Rendering;

// The flight effect of a projectile (e.g. Polygon Arsenal's LaserBlue: a glow and a core that travel with the
// bolt, sparks it leaves behind, a trail) for ALL projectiles of a prefab, in one shared copy of the prefab.
//
// Each flight gets an id. Every frame ProjectileSystem reports where each flight is (SetPose); the batch emits
// what each flight's systems would have emitted (rate over time, rate over distance along the frame's path,
// looping bursts) and moves the fresh particles into place. Particles of systems that simulated in local space
// follow their flight: a Burst particle job moves them with it, shrinks them while it fades and removes them
// when it is gone, like the copy that used to be parented to the projectile. Systems with "Trail" in the name
// are left behind and play out, as PolygonProjectileScript intended. A TrailRenderer is drawn by TrailMesh.
//
// Prefabs this can't reproduce (sub-emitters, local-space forces, scripts, meshes, lights) use pooled copies.
public sealed class FlightBatch : IDisposable
{
    private const int InitialFlights = 1024;
    private const int IdBits = 18;
    private const uint IdMask = (1u << IdBits) - 1;
    private const int GenerationMask = 0x3FFF;

    // See ProjectileSystem.WarmUpJobs
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmUpJobs()
    {
        IJobExtensions.EarlyJobInit<PrepareJob>();
        IJobExtensions.EarlyJobInit<FinishJob>();
        IJobExtensions.EarlyJobInit<TrailJob>();
        IJobParallelForExtensions.EarlyJobInit<PlaceJob>();
        IJobParticleSystemParallelForBatchExtensions.EarlyJobInit<FollowJob>();
    }

    private sealed class Emitter
    {
        public ParticleSystem System;
        public bool Follows;          // simulated in local space: moves with its flight
        public bool Rotate3D;
        public float Prewarm;         // particles a prewarmed system already shows when the flight starts
        public float InheritVelocity; // share of the flight's velocity new particles get (Inherit Velocity module)
        public bool HasTrails;        // particle trails: emitted at each flight's pose (see BatchedEffect)
        public NativeArray<ParticleSystem.Particle> Buffer;
        // This frame's emission, between Emit and SetParticles
        public int Existing;
        public int Read;
    }

    // What an emitter emits per flight, for PrepareJob
    private struct EmitterRates
    {
        public float RateOverTime;
        public float RateOverDistance;
        public float Duration;
        public float Delay;
        public int FirstBurst;        // its bursts in the bursts array
        public int BurstCount;
    }

    // A burst within one loop of its system, with the particle count range
    private struct BurstOffset
    {
        public float Time;
        public float Min;
        public float Max;
    }

    private struct Flight
    {
        public float3 Position;
        public float3 Previous;
        public quaternion Rotation;
        public float Scale;
        public float LastScale;
        public float3 Velocity;
        public float Clock;           // seconds since the flight started
        public bool Live;
        public bool Fresh;            // attached since the last update
        public int Generation;
    }

    public readonly GameObject Prefab;
    // The prefab's own (uniform) scale: flights scale relative to it, like the pooled copies
    private readonly float rootScale;
    private readonly GameObject shared;
    private readonly Emitter[] emitters;
    private readonly TrailMesh trail;
    // Ids of finished flights whose trail is still fading; reused once it is gone
    private readonly List<int> fading = new List<int>(256);

    private int capacity;
    private NativeArray<Flight> flights;
    private NativeArray<int> flightGeneration;
    private readonly Stack<int> freeIds = new Stack<int>();
    private int highestId;
    public int LiveFlights { get; private set; }

    // Emission, per emitter: rates and bursts; per flight and emitter (id * emitters + e): fractional particles
    // carried over; per emitter: this frame's entries (flight id, first particle, count) in a block of capacity
    private NativeArray<EmitterRates> rates;
    private NativeArray<BurstOffset> bursts;
    private NativeArray<float> owed;
    private NativeArray<int3> entries;
    private NativeArray<int> entryCounts;
    private NativeArray<int> totals;
    private uint frameSeed = 1;

    // Read by the follower jobs, written by PrepareJob before the particle update
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
        rootScale = prefab.transform.localScale.x;
        GameObject staging = new GameObject(prefab.name + " (flight staging)");
        staging.SetActive(false);
        staging.transform.SetParent(container, false);
        shared = UnityEngine.Object.Instantiate(prefab, staging.transform);
        shared.name = prefab.name + " (flights)";
        shared.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        shared.transform.localScale = Vector3.one;

        ParticleSystem[] systems = shared.GetComponentsInChildren<ParticleSystem>(true);
        emitters = new Emitter[systems.Length];
        rates = new NativeArray<EmitterRates>(systems.Length, Allocator.Persistent);
        List<BurstOffset> burstList = new List<BurstOffset>();
        for (int i = 0; i < systems.Length; i++)
        {
            emitters[i] = Configure(systems[i], burstList, out EmitterRates emitterRates);
            rates[i] = emitterRates;
        }
        bursts = new NativeArray<BurstOffset>(Mathf.Max(1, burstList.Count), Allocator.Persistent);
        for (int i = 0; i < burstList.Count; i++)
            bursts[i] = burstList[i];
        entryCounts = new NativeArray<int>(systems.Length, Allocator.Persistent);
        totals = new NativeArray<int>(systems.Length, Allocator.Persistent);

        TrailRenderer trailRenderer = shared.GetComponentInChildren<TrailRenderer>(true);
        if (trailRenderer != null)
        {
            trail = new TrailMesh(trailRenderer, InitialFlights);
            trailRenderer.enabled = false;
        }

        capacity = InitialFlights;
        flights = new NativeArray<Flight>(capacity, Allocator.Persistent);
        flightGeneration = new NativeArray<int>(capacity, Allocator.Persistent);
        owed = new NativeArray<float>(capacity * emitters.Length, Allocator.Persistent);
        entries = new NativeArray<int3>(capacity * emitters.Length, Allocator.Persistent);
        delta = new NativeArray<float3>(capacity, Allocator.Persistent);
        center = new NativeArray<float3>(capacity, Allocator.Persistent);
        scaleRatio = new NativeArray<float>(capacity, Allocator.Persistent);
        generation = new NativeArray<int>(capacity, Allocator.Persistent);
        alive = new NativeArray<byte>(capacity, Allocator.Persistent);

        shared.transform.SetParent(container, false);
        UnityEngine.Object.Destroy(staging);
        foreach (Emitter emitter in emitters)
        {
            if (emitter.Follows)
                FlightFollower.Attach(emitter.System, this);
            emitter.System.Play(false);
        }
    }

    private static Emitter Configure(ParticleSystem system, List<BurstOffset> burstList, out EmitterRates emitterRates)
    {
        ParticleSystem.MainModule main = system.main;
        ParticleSystem.EmissionModule emission = system.emission;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();

        Emitter emitter = new Emitter
        {
            System = system,
            HasTrails = system.trails.enabled,
            Follows = main.simulationSpace == ParticleSystemSimulationSpace.Local,
        };
        emitterRates = new EmitterRates
        {
            Duration = Mathf.Max(0.01f, main.duration),
            Delay = EffectPool.MaxOf(main.startDelay),
            FirstBurst = burstList.Count,
        };

        if (emission.enabled)
        {
            emitterRates.RateOverTime = EffectPool.MaxOf(emission.rateOverTime);
            emitterRates.RateOverDistance = EffectPool.MaxOf(emission.rateOverDistance);
            for (int b = 0; b < emission.burstCount; b++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(b);
                ParticleSystem.MinMaxCurve count = burst.count;
                bool range = count.mode == ParticleSystemCurveMode.TwoConstants;
                float min = range ? count.constantMin : EffectPool.MaxOf(count);
                float max = range ? count.constantMax : min;
                int cycles = burst.cycleCount <= 0 ? int.MaxValue : burst.cycleCount;
                float interval = Mathf.Max(0.01f, burst.repeatInterval);
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    float time = burst.time + cycle * interval;
                    if (time >= emitterRates.Duration)
                        break;
                    burstList.Add(new BurstOffset { Time = time, Min = min, Max = max });
                }
            }
        }
        emitterRates.BurstCount = burstList.Count - emitterRates.FirstBurst;
        if (main.prewarm && main.loop)
            emitter.Prewarm = emitterRates.RateOverTime * EffectPool.MaxOf(main.startLifetime);
        // A non-looping flight system only emits during its first loop
        if (!main.loop)
            emitterRates.Duration = float.PositiveInfinity;

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
        // The shared copy stands still: new particles get the flight's velocity from PlaceJob instead
        // (Unity only applies the module in world space; Current mode is treated like Initial)
        ParticleSystem.InheritVelocityModule inherit = system.inheritVelocity;
        if (inherit.enabled && !emitter.Follows)
            emitter.InheritVelocity = EffectPool.MaxOf(inherit.curve);
        inherit.enabled = false;

        if (renderer != null)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        return emitter;
    }

    public void Dispose()
    {
        followJobs.Complete();
        trail?.Dispose();
        foreach (Emitter emitter in emitters)
        {
            if (emitter.Buffer.IsCreated)
                emitter.Buffer.Dispose();
        }
        flights.Dispose();
        flightGeneration.Dispose();
        rates.Dispose();
        bursts.Dispose();
        owed.Dispose();
        entries.Dispose();
        entryCounts.Dispose();
        totals.Dispose();
        delta.Dispose();
        center.Dispose();
        scaleRatio.Dispose();
        generation.Dispose();
        alive.Dispose();
    }

    // ---- flights ----------------------------------------------------------------------------------------

    public int Attach(Vector3 position, Quaternion rotation, float scale, Vector3 velocity)
    {
        scale *= rootScale;
        int id = freeIds.Count > 0 ? freeIds.Pop() : highestId++;
        LiveFlights++;
        EnsureCapacity(id + 1);
        int flightGen = (flightGeneration[id] + 1) & GenerationMask;
        flightGeneration[id] = flightGen;
        flights[id] = new Flight
        {
            Position = position,
            Previous = position,
            Rotation = rotation,
            Scale = scale,
            LastScale = scale,
            Clock = -1e-6f,
            Velocity = velocity,
            Live = true,
            Fresh = true,
            Generation = flightGen,
        };
        for (int e = 0; e < emitters.Length; e++)
            owed[id * emitters.Length + e] = emitters[e].Prewarm;
        if (trail != null)
        {
            trail.EnsureCapacity(id + 1);
            trail.Begin(id, position, Time.time);
        }
        return id;
    }

    public void SetPose(int id, Vector3 position, Quaternion rotation, float scale, Vector3 velocity)
    {
        Flight flight = flights[id];
        flight.Position = position;
        flight.Velocity = velocity;
        flight.Rotation = rotation;
        flight.Scale = scale * rootScale;
        flights[id] = flight;
    }

    // The projectile is gone: followers vanish with it, trails and left-behind sparks play out
    public void Release(int id)
    {
        Flight flight = flights[id];
        flight.Live = false;
        flights[id] = flight;
        LiveFlights--;
        if (trail != null)
            fading.Add(id);
        else
            freeIds.Push(id);
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= capacity)
            return;
        followJobs.Complete();
        capacity = Mathf.NextPowerOfTwo(needed);
        Grow(ref flights, capacity);
        Grow(ref flightGeneration, capacity);
        // Indexed id * emitters + e, so the existing values keep their place
        Grow(ref owed, capacity * emitters.Length);
        entries.Dispose();
        entries = new NativeArray<int3>(capacity * emitters.Length, Allocator.Persistent);
        Grow(ref delta, capacity);
        Grow(ref center, capacity);
        Grow(ref scaleRatio, capacity);
        Grow(ref generation, capacity);
        Grow(ref alive, capacity);
    }

    private static void Grow<T>(ref NativeArray<T> array, int size) where T : struct
    {
        NativeArray<T> grown = new NativeArray<T>(size, Allocator.Persistent);
        NativeArray<T>.Copy(array, grown, array.Length);
        array.Dispose();
        array = grown;
    }

    // ---- per frame --------------------------------------------------------------------------------------

    private static readonly Unity.Profiling.ProfilerMarker FlightMarker = new Unity.Profiling.ProfilerMarker("3DTD.Effects.Flights");

    // Once per frame after the projectiles moved and before the particle update
    public void Update(float deltaTime)
    {
        using var scope = FlightMarker.Auto();
        followJobs.Complete();
        followJobs = default;
        if (deltaTime <= 0f)
            return;

        frameSeed = frameSeed * 747796405u + 2891336453u;
        new PrepareJob
        {
            Flights = flights,
            FlightGeneration = flightGeneration,
            HighestId = highestId,
            DeltaTime = deltaTime,
            Rates = rates,
            Bursts = bursts,
            Owed = owed,
            Entries = entries,
            EntryCounts = entryCounts,
            Totals = totals,
            Capacity = capacity,
            Random = new Unity.Mathematics.Random(frameSeed | 1u),
            Delta = delta,
            Center = center,
            ScaleRatio = scaleRatio,
            Generation = generation,
            Alive = alive,
        }.Run();

        // Systems with particle trails emit at each flight's pose; the others emit their whole frame at once and
        // place the particles in parallel jobs
        JobHandle placing = default;
        for (int e = 0; e < emitters.Length; e++)
        {
            Emitter emitter = emitters[e];
            emitter.Read = 0;
            if (totals[e] == 0)
                continue;
            if (emitter.HasTrails)
                EmitAtPoses(emitter, e);
            else
                placing = JobHandle.CombineDependencies(placing, EmitAll(emitter, e));
        }
        placing.Complete();
        for (int e = 0; e < emitters.Length; e++)
        {
            Emitter emitter = emitters[e];
            if (emitter.HasTrails || emitter.Read == 0)
                continue;
            EffectMarkers.Particles.Begin();
            emitter.System.SetParticles(emitter.Buffer, emitter.Read, emitter.Existing);
            EffectMarkers.Particles.End();
            PerfCounters.BatchedParticles += emitter.Read;
        }

        if (trail != null)
        {
            new TrailJob
            {
                Flights = flights,
                HighestId = highestId,
                Trail = trail.GetWriter(),
                Now = Time.time,
            }.Run();
        }
        new FinishJob
        {
            Flights = flights,
            HighestId = highestId,
            DeltaTime = deltaTime,
        }.Run();

        if (trail != null)
            UpdateTrails();
    }

    // Trails keep fading after their flights and free the id once they are gone
    private void UpdateTrails()
    {
        float now = Time.time;
        for (int i = fading.Count - 1; i >= 0; i--)
        {
            int id = fading[i];
            if (!trail.IsFaded(id, now))
                continue;
            trail.End(id);
            freeIds.Push(id);
            fading[i] = fading[fading.Count - 1];
            fading.RemoveAt(fading.Count - 1);
        }
        trail.Draw(highestId, now, Camera.main);
    }

    // Emits the frame's particles of every flight at once and schedules the job that puts them in place
    private JobHandle EmitAll(Emitter emitter, int e)
    {
        int total = totals[e];
        ParticleSystem system = emitter.System;
        int existing = system.particleCount;
        ParticleSystem.MainModule main = system.main;
        if (existing + total > main.maxParticles)
            main.maxParticles = Mathf.NextPowerOfTwo(existing + total);
        EffectMarkers.Emit.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        long emitStart = EffectStats.Now;
#endif
        system.Emit(total);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        EffectStats.Record(system, Prefab, total, existing, emitStart);
#endif
        EffectMarkers.Emit.End();
        int emitted = system.particleCount - existing;
        if (emitted <= 0)
            return default;

        if (!emitter.Buffer.IsCreated || emitter.Buffer.Length < emitted)
        {
            if (emitter.Buffer.IsCreated)
                emitter.Buffer.Dispose();
            emitter.Buffer = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(emitted), Allocator.Persistent);
        }

        EffectMarkers.Particles.Begin();
        emitter.Read = system.GetParticles(emitter.Buffer, emitted, existing);
        EffectMarkers.Particles.End();
        emitter.Existing = existing;
        int entryCount = entryCounts[e];
        return new PlaceJob
        {
            Flights = flights,
            Entries = entries.GetSubArray(e * capacity, entryCount),
            Particles = emitter.Buffer,
            Read = emitter.Read,
            Follows = emitter.Follows,
            Rotate3D = emitter.Rotate3D,
            InheritVelocity = emitter.InheritVelocity,
        }.Schedule(entryCount, 16);
    }

    // Follower data for the particle jobs, and how many particles each emitter owes each live flight this frame
    // (rate over time, rate over distance along the frame's path, looping bursts)
    [BurstCompile]
    private struct PrepareJob : IJob
    {
        [ReadOnly] public NativeArray<Flight> Flights;
        [ReadOnly] public NativeArray<int> FlightGeneration;
        public int HighestId;
        public float DeltaTime;
        [ReadOnly] public NativeArray<EmitterRates> Rates;
        [ReadOnly] public NativeArray<BurstOffset> Bursts;
        public NativeArray<float> Owed;
        public NativeArray<int3> Entries;
        public NativeArray<int> EntryCounts;
        public NativeArray<int> Totals;
        public int Capacity;
        public Unity.Mathematics.Random Random;

        public NativeArray<float3> Delta;
        public NativeArray<float3> Center;
        public NativeArray<float> ScaleRatio;
        public NativeArray<int> Generation;
        public NativeArray<byte> Alive;

        public void Execute()
        {
            int emitterCount = Rates.Length;
            for (int e = 0; e < emitterCount; e++)
            {
                EntryCounts[e] = 0;
                Totals[e] = 0;
            }

            for (int id = 0; id < HighestId; id++)
            {
                Flight flight = Flights[id];
                if (!flight.Live && Alive[id] == 0 && Generation[id] == FlightGeneration[id])
                    continue;

                Delta[id] = flight.Fresh ? float3.zero : flight.Position - flight.Previous;
                Center[id] = flight.Previous;
                ScaleRatio[id] = flight.Fresh || flight.LastScale <= 0f ? 1f : flight.Scale / flight.LastScale;
                Generation[id] = FlightGeneration[id];
                Alive[id] = (byte)(flight.Live ? 1 : 0);
                if (!flight.Live)
                    continue;

                float distance = flight.Fresh ? 0f : math.distance(flight.Position, flight.Previous);
                for (int e = 0; e < emitterCount; e++)
                {
                    EmitterRates rates = Rates[e];
                    int slot = id * emitterCount + e;
                    float amount = Owed[slot] + rates.RateOverTime * DeltaTime + rates.RateOverDistance * distance;
                    int count = (int)amount;
                    Owed[slot] = amount - count;

                    float before = flight.Clock - rates.Delay;
                    float after = before + DeltaTime;
                    for (int b = rates.FirstBurst; b < rates.FirstBurst + rates.BurstCount; b++)
                    {
                        BurstOffset burst = Bursts[b];
                        int crossings = Crossings(before, after, burst.Time, rates.Duration);
                        for (int c = 0; c < crossings; c++)
                            count += (int)math.round(burst.Min < burst.Max ? Random.NextFloat(burst.Min, burst.Max) : burst.Min);
                    }

                    if (count > 0)
                    {
                        int k = EntryCounts[e];
                        Entries[e * Capacity + k] = new int3(id, Totals[e], count);
                        EntryCounts[e] = k + 1;
                        Totals[e] += count;
                    }
                }
            }
        }
    }

    // Trails follow their flights
    [BurstCompile]
    private struct TrailJob : IJob
    {
        [ReadOnly] public NativeArray<Flight> Flights;
        public int HighestId;
        public TrailMesh.Writer Trail;
        public float Now;

        public void Execute()
        {
            for (int id = 0; id < HighestId; id++)
            {
                if (Flights[id].Live)
                    Trail.Add(id, Flights[id].Position, Now);
            }
        }
    }

    // After the emission each live flight's frame is done
    [BurstCompile]
    private struct FinishJob : IJob
    {
        public NativeArray<Flight> Flights;
        public int HighestId;
        public float DeltaTime;

        public void Execute()
        {
            for (int id = 0; id < HighestId; id++)
            {
                Flight flight = Flights[id];
                if (!flight.Live)
                    continue;
                flight.Previous = flight.Position;
                flight.LastScale = flight.Scale;
                flight.Clock += DeltaTime;
                flight.Fresh = false;
                Flights[id] = flight;
            }
        }
    }

    // Moves each flight's block of fresh particles to it: followers start where the flight was last frame (the
    // follower job moves them on with it), left-behind particles spread along the path it covered this frame
    [BurstCompile]
    private struct PlaceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Flight> Flights;
        [ReadOnly] public NativeArray<int3> Entries;
        [NativeDisableParallelForRestriction] public NativeArray<ParticleSystem.Particle> Particles;
        public int Read;
        public bool Follows;
        public bool Rotate3D;
        public float InheritVelocity;

        public void Execute(int e)
        {
            int3 entry = Entries[e];
            Flight flight = Flights[entry.x];
            uint tag = ((uint)flight.Generation << IdBits) | (uint)entry.x;
            for (int k = 0; k < entry.z; k++)
            {
                int index = entry.y + k;
                if (index >= Read)
                    return;
                ParticleSystem.Particle particle = Particles[index];
                float3 origin = Follows || flight.Fresh ? flight.Previous
                    : math.lerp(flight.Previous, flight.Position, (k + 0.5f) / entry.z);
                particle.position = origin + math.mul(flight.Rotation, (float3)particle.position * flight.Scale);
                particle.velocity = math.mul(flight.Rotation, (float3)particle.velocity * flight.Scale) + flight.Velocity * InheritVelocity;
                particle.startSize3D = (float3)particle.startSize3D * flight.Scale;
                if (Rotate3D)
                    particle.rotation3D = EffectMath.ComposeEulerDegrees(flight.Rotation, particle.rotation3D);
                if (Follows)
                    particle.randomSeed = tag;
                Particles[index] = particle;
            }
        }
    }

    // Particle trails start where a particle is emitted, so these systems emit at each flight's pose
    private void EmitAtPoses(Emitter emitter, int e)
    {
        ParticleSystem system = emitter.System;
        Transform transform = system.transform;
        ParticleSystem.MainModule main = system.main;
        int entryCount = entryCounts[e];
        for (int n = 0; n < entryCount; n++)
        {
            int3 entry = entries[e * capacity + n];
            Flight flight = flights[entry.x];
            int before = system.particleCount;
            if (before + entry.z > main.maxParticles)
                main.maxParticles = Mathf.NextPowerOfTwo(before + entry.z);
            transform.SetPositionAndRotation(emitter.Follows ? (Vector3)flight.Previous : (Vector3)flight.Position, flight.Rotation);
            EffectMarkers.Emit.Begin();
            system.Emit(entry.z);
            EffectMarkers.Emit.End();
            int emitted = system.particleCount - before;
            PerfCounters.BatchedParticles += emitted;
            if (emitted <= 0 || (!emitter.Follows && Mathf.Approximately(flight.Scale, 1f) && emitter.InheritVelocity == 0f))
                continue;

            if (!emitter.Buffer.IsCreated || emitter.Buffer.Length < emitted)
            {
                if (emitter.Buffer.IsCreated)
                    emitter.Buffer.Dispose();
                emitter.Buffer = new NativeArray<ParticleSystem.Particle>(Mathf.NextPowerOfTwo(emitted), Allocator.Persistent);
            }
            uint tag = ((uint)flightGeneration[entry.x] << IdBits) | (uint)entry.x;
            EffectMarkers.Particles.Begin();
            int read = system.GetParticles(emitter.Buffer, emitted, before);
            EffectMarkers.Particles.End();
            for (int i = 0; i < read; i++)
            {
                ParticleSystem.Particle particle = emitter.Buffer[i];
                particle.velocity = particle.velocity * flight.Scale + (Vector3)flight.Velocity * emitter.InheritVelocity;
                particle.startSize3D *= flight.Scale;
                if (emitter.Follows)
                    particle.randomSeed = tag;
                emitter.Buffer[i] = particle;
            }
            EffectMarkers.Particles.Begin();
            system.SetParticles(emitter.Buffer, read, before);
            EffectMarkers.Particles.End();
        }
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    // How many times offset + n * duration (n >= 0) lies in (before, after]
    private static int Crossings(float before, float after, float offset, float duration)
    {
        if (after < offset)
            return 0;
        if (math.isinf(duration))
            return before < offset ? 1 : 0;
        int last = (int)math.floor((after - offset) / duration);
        int first = before < offset ? 0 : (int)math.floor((before - offset) / duration) + 1;
        return math.max(0, last - first + 1);
    }

    // ---- follower job -----------------------------------------------------------------------------------

    internal void ScheduleFollow(ParticleSystem system)
    {
        JobHandle handle = new FollowJob
        {
            Delta = delta,
            Center = center,
            ScaleRatio = scaleRatio,
            Generation = generation,
            Alive = alive,
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
    public static void Attach(ParticleSystem system, FlightBatch batch)
    {
        FlightFollower follower = system.gameObject.AddComponent<FlightFollower>();
        follower.batch = batch;
        follower.system = system;
    }

    private void OnParticleUpdateJobScheduled()
    {
        if (batch != null)
            batch.ScheduleFollow(system);
    }
}
