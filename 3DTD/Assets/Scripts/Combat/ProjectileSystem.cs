using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

// Simulates the towers' projectiles (bullets, bolts, rockets, cluster bomblets, starfighter weapons) as data
// instead of one GameObject with a Rigidbody each.
//
// Every frame (after the towers fired and the enemies moved) one Burst job puts the enemies into a grid and a
// parallel job moves each projectile and sweeps its hit capsule along everything it covered this frame,
// against the enemies' own movement. Hits are applied on the main thread in time order and checked again
// against the live enemy, so the result doesn't depend on the frame rate or the game speed: a projectile can't
// pass through an enemy between two frames, and one fired late in a frame starts only as far as it flew since.
//
// The projectile prefabs remain the authoring source (see ProjectileArchetype); effects are played by
// EffectPlayer. A projectile that dies keeps flying and shrinks for Projectile.FadeDuration like before.
[DefaultExecutionOrder(1000)]
public class ProjectileSystem : MonoBehaviour
{
    // The job system builds a job type's reflection data on its first schedule, and the first Burst job of the
    // app loads the compiled code (~25 ms in the player): done once at startup instead of on the first shot
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WarmUpJobs()
    {
        IJobExtensions.EarlyJobInit<BuildGridJob>();
        IJobParallelForExtensions.EarlyJobInit<StepJob>();

        NativeArray<float3> none = new NativeArray<float3>(0, Allocator.TempJob);
        NativeArray<float> noRadii = new NativeArray<float>(0, Allocator.TempJob);
        NativeArray<int> noSerials = new NativeArray<int>(0, Allocator.TempJob);
        NativeParallelMultiHashMap<int, int> emptyGrid = new NativeParallelMultiHashMap<int, int>(1, Allocator.TempJob);
        new BuildGridJob { Positions = none, Previous = none, Radii = noRadii, Serials = noSerials, Grid = emptyGrid }.Run();
        emptyGrid.Dispose();
        noSerials.Dispose();
        noRadii.Dispose();
        none.Dispose();
    }

    public const int MaxHitsPerStep = 8;
    private const float CellSize = 1.5f;
    private const float HomingStep = 1f / 120f;
    // Effects play at most as long as the Destroy delays PolygonProjectileScript used
    private const float MuzzleLifetime = 1.5f;
    private const float ImpactLifetime = 5f;
    // The enemy prefab's sphere collider (radius 0.75 at scale 0.5)
    private const float DefaultEnemyRadius = 0.375f;
    // Explosions play at their prefab's size for this blast radius (the Rocket System's base RADIUS) and grow
    // or shrink with the real one, within these bounds
    public const float ReferenceBlastRadius = 0.8f;
    private const float MinImpactScale = 0.4f;
    private const float MaxImpactScale = 2.2f;

    public struct Shot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
        public float Damage;
        public float Speed;
        public float Lifetime;
        public float Accuracy;
        public int Pierce;
        public float BlastRadius;
        // Bolts and rockets look at the target when they leave the barrel (then spread by Accuracy)
        public Enemy Target;
        // Rockets that keep turning towards their target
        public bool Homing;
        public bool Cluster;
        public Tower Tower;
        // Seconds since the shot was due (FireCycle.VolleyAge): it starts that far along its way
        public float Age;
        // Effects replacing the prefab's own (upgrade looks, see VisualSlot); null keeps the prefab's
        public GameObject MuzzleEffect;
        public GameObject FlightEffect;
        public GameObject ImpactEffect;
    }

    private struct State
    {
        public float3 Position;
        public float3 Direction;
        public quaternion Rotation;
        public float Speed;
        public float Remaining;
        public float Radius;
        public float HalfLength;
        public float Scale;
        // Size of the impact or explosion effect: SIZE for bolts and bullets, the blast radius for rockets
        public float ImpactScale;
        public float Damage;
        public float BlastRadius;
        public int Pierce;
        public byte Kind;
        public byte Homing;
        public byte Cluster;
        public byte Dying;
        public byte Frozen;
        public float FadeTime;
        public int TargetSlot;
        public int TargetSerial;
        // A projectile fired this frame only moves Lead seconds (the time since its shot); one spawned after
        // this frame's step (bomblets) moves the next frame's time plus Lead
        public int BornFrame;
        public float Lead;
        public FixedList64Bytes<int> HitSerials;
    }

    private struct Step
    {
        public float3 Position;
        public float3 Direction;
        public quaternion Rotation;
        public float Time;
        public byte Expired;
        public int HitCount;
    }

    private struct Hit
    {
        public int Slot;
        public int Serial;
        public float T;
    }

    private static ProjectileSystem instance;
    private static bool quitting;
    private static readonly ProfilerMarker StepMarker = new ProfilerMarker("3DTD.Projectiles.Step");
    private static readonly ProfilerMarker ApplyMarker = new ProfilerMarker("3DTD.Projectiles.Apply");

    public EnemyRegistry Enemies { get; private set; }
    public int Count => states.IsCreated ? states.Length : 0;

    private NativeList<State> states;
    private NativeList<Step> steps;
    private NativeList<Hit> hits;
    // Projectiles whose step needs the main thread (hits, end of lifetime), in index order
    private NativeList<int> events;
    private NativeParallelMultiHashMap<int, int> grid;
    private readonly List<ProjectileArchetype> archetypes = new List<ProjectileArchetype>();
    private readonly List<GameObject> impactEffects = new List<GameObject>();
    private readonly List<Tower> towers = new List<Tower>();
    private readonly List<FlightHandle> flights = new List<FlightHandle>();
    private readonly List<Enemy> overlap = new List<Enemy>();

    // Statics survive play sessions when domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    // Without creating one (scene teardown)
    public static ProjectileSystem Existing => instance;

    public static ProjectileSystem Instance
    {
        get
        {
            // Play mode only (EditMode tests initialise enemies too)
            if (instance == null && !quitting && Application.isPlaying)
                instance = new GameObject("Projectile System").AddComponent<ProjectileSystem>();
            return instance;
        }
    }

    private void Awake()
    {
        quitting = false;
        Enemies = new EnemyRegistry();
        states = new NativeList<State>(1024, Allocator.Persistent);
        steps = new NativeList<Step>(1024, Allocator.Persistent);
        hits = new NativeList<Hit>(1024 * MaxHitsPerStep, Allocator.Persistent);
        events = new NativeList<int>(1024, Allocator.Persistent);
        grid = new NativeParallelMultiHashMap<int, int>(4096, Allocator.Persistent);
    }

    private void OnApplicationQuit()
    {
        quitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        // Scene teardown: the effects go with the scene, nothing to release
        flights.Clear();
        Enemies.Dispose();
        states.Dispose();
        steps.Dispose();
        hits.Dispose();
        events.Dispose();
        grid.Dispose();
    }

    // ---- firing -----------------------------------------------------------------------------------------

    private static readonly ProfilerMarker FireMarker = new ProfilerMarker("3DTD.Projectiles.Fire");

    public static void Fire(GameObject prefab, in Shot shot)
    {
        using var scope = FireMarker.Auto();
        Instance.Spawn(ProjectileArchetype.Get(prefab), shot, Time.frameCount, shot.Age);
    }

    private void Spawn(ProjectileArchetype archetype, in Shot shot, int bornFrame, float lead)
    {
        PerfCounters.ProjectilesRequested++;
        if (archetype == null)
            return;

        Quaternion rotation = shot.Rotation;
        bool aims = archetype.Kind == ProjectileKind.Basic || archetype.Kind == ProjectileKind.Bomb;
        if (aims)
        {
            // Like Transform.LookAt(target, Vector3.right) in ProjectileBasic/ProjectileBomb.OnEnable
            if (shot.Target != null)
            {
                Vector3 toTarget = shot.Target.transform.position - shot.Position;
                if (toTarget.sqrMagnitude > 0.000001f)
                    rotation = Quaternion.LookRotation(toTarget, Vector3.right);
            }

            float maxAngle = (1f - shot.Accuracy) * 25f;
            if (maxAngle > 0f)
                rotation *= Quaternion.Euler(Spread(shot.Tower, maxAngle), Spread(shot.Tower, maxAngle), Spread(shot.Tower, maxAngle));
        }

        float scale = shot.Scale > 0f ? shot.Scale : 1f;
        float impactScale = archetype.Kind == ProjectileKind.Bomb || archetype.Kind == ProjectileKind.Cluster
            ? BlastImpactScale(shot.BlastRadius)
            : scale;
        State state = new State
        {
            Position = shot.Position,
            Direction = rotation * Vector3.forward,
            Rotation = rotation,
            Speed = shot.Speed,
            Remaining = shot.Lifetime,
            Radius = archetype.HitRadius * scale,
            HalfLength = archetype.HitHalfLength * scale,
            Scale = scale,
            ImpactScale = impactScale,
            Damage = shot.Damage,
            BlastRadius = shot.BlastRadius,
            Pierce = shot.Pierce,
            Kind = (byte)archetype.Kind,
            Homing = (byte)(shot.Homing && shot.Target != null ? 1 : 0),
            Cluster = (byte)(shot.Cluster ? 1 : 0),
            TargetSlot = shot.Target != null && shot.Target.IsAlive ? shot.Target.RegistrySlot : -1,
            TargetSerial = shot.Target != null ? shot.Target.SpawnSerial : 0,
            BornFrame = bornFrame,
            Lead = Mathf.Max(0f, lead),
        };
        states.Add(state);
        archetypes.Add(archetype);
        impactEffects.Add(shot.ImpactEffect != null ? shot.ImpactEffect : archetype.ImpactEffect);
        towers.Add(shot.Tower);

        FlightHandle flight = default;
        if (archetype.Kind != ProjectileKind.Cluster)
        {
            GameObject muzzle = shot.MuzzleEffect != null ? shot.MuzzleEffect : archetype.MuzzleEffect;
            GameObject flightEffect = shot.FlightEffect != null ? shot.FlightEffect : archetype.FlightEffect;
            EffectPlayer.Play(muzzle, shot.Position, rotation, scale, MuzzleLifetime, lead);
            flight = EffectPlayer.Attach(flightEffect, shot.Position, rotation, scale, rotation * Vector3.forward * shot.Speed);
        }
        flights.Add(flight);
        PerfCounters.ProjectilesSpawned++;
    }

    // Size of an explosion effect for a blast radius (1 at ReferenceBlastRadius)
    public static float BlastImpactScale(float blastRadius)
    {
        if (blastRadius <= 0f)
            return 1f;
        // Grows a little slower than the radius: the effects' glow spheres reach well past the blast already
        return Mathf.Clamp(Mathf.Pow(blastRadius / ReferenceBlastRadius, 0.7f), MinImpactScale, MaxImpactScale);
    }

    private static float Spread(Tower tower, float maxAngle)
    {
        return tower != null ? tower.Rng.NextFloat(-maxAngle, maxAngle) : UnityEngine.Random.Range(-maxAngle, maxAngle);
    }

    // ---- simulation -------------------------------------------------------------------------------------

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        Enemies.Sync();
        if (deltaTime <= 0f || states.Length == 0)
            return;

        int count = states.Length;
        steps.ResizeUninitialized(count);
        hits.ResizeUninitialized(count * MaxHitsPerStep);
        // An enemy covers at most 2 x 2 x 2 cells unless it is big or fast; leave room for that
        int enemyCount = Enemies.Count;
        if (grid.Capacity < enemyCount * 27 + 64)
            grid.Capacity = enemyCount * 27 + 64;
        grid.Clear();
        events.Clear();
        if (events.Capacity < count)
            events.Capacity = count;

        StepMarker.Begin();
        JobHandle gridJob = new BuildGridJob
        {
            Positions = Enemies.Positions.AsArray(),
            Previous = Enemies.PreviousPositions.AsArray(),
            Radii = Enemies.Radii.AsArray(),
            Serials = Enemies.Serials.AsArray(),
            Grid = grid,
        }.Schedule();

        new StepJob
        {
            States = states.AsArray(),
            EnemyPositions = Enemies.Positions.AsArray(),
            EnemyPrevious = Enemies.PreviousPositions.AsArray(),
            EnemyRadii = Enemies.Radii.AsArray(),
            EnemySerials = Enemies.Serials.AsArray(),
            Grid = grid,
            DeltaTime = deltaTime,
            Frame = Time.frameCount,
            Steps = steps.AsArray(),
            Hits = hits.AsArray(),
            Events = events.AsParallelWriter(),
        }.Schedule(count, 32, gridJob).Complete();
        StepMarker.End();

        using (ApplyMarker.Auto())
            Apply();
    }

    // Main thread: hits, blasts, effects and fading, in the order the projectiles were fired
    // The steps StepJob left to the main thread: hits and ends of lifetime, in projectile order
    private void Apply()
    {
        events.Sort();
        for (int e = 0; e < events.Length; e++)
        {
            int i = events[e];
            State state = states[i];
            Step step = steps[i];
            ProjectileArchetype archetype = archetypes[i];
            GameObject impact = impactEffects[i];
            Tower tower = towers[i];

            float3 start = state.Position;
            bool dead = false;
            float3 deathPoint = step.Position;
            for (int h = 0; h < step.HitCount && !dead; h++)
            {
                Hit hit = hits[i * MaxHitsPerStep + h];
                if (!Enemies.IsSame(hit.Slot, hit.Serial))
                    continue;

                Enemy enemy = Enemies.EnemyAt(hit.Slot);
                float3 point = math.lerp(start, step.Position, hit.T);
                RememberHit(ref state, hit.Serial);

                switch ((ProjectileKind)state.Kind)
                {
                    case ProjectileKind.Round:
                        enemy.TakeDamage(state.Damage, DamageType.PROJECTILE, tower);
                        state.Pierce--;
                        EffectPlayer.Play(impact, point, Quaternion.identity, state.ImpactScale, ImpactLifetime);
                        dead = state.Pierce <= 0;
                        break;

                    case ProjectileKind.Basic:
                        if (state.Pierce > 0)
                        {
                            enemy.TakeDamage(state.Damage, DamageType.PROJECTILE, tower);
                            state.Pierce--;
                        }
                        EffectPlayer.Play(impact, point, Quaternion.identity, state.ImpactScale, ImpactLifetime);
                        dead = state.Pierce <= 0;
                        break;

                    case ProjectileKind.Bomb:
                        // A rocket goes off on its first hit
                        state.Pierce--;
                        Explode(ref state, archetype, impact, tower, point, step.Time * (1f - hit.T));
                        dead = true;
                        break;

                    case ProjectileKind.Cluster:
                        Blast(point, state.BlastRadius, state.Damage, tower);
                        EffectPlayer.Play(impact, point, Quaternion.identity, state.ImpactScale, ImpactLifetime);
                        dead = true;
                        break;
                }

                if (dead)
                    deathPoint = point;
            }

            state.Remaining -= step.Time;
            if (!dead && step.Expired != 0)
            {
                switch ((ProjectileKind)state.Kind)
                {
                    case ProjectileKind.Bomb:
                        // Out of fuel: the rocket goes off where it is and stops (ProjectileBomb.updateDisabled)
                        Explode(ref state, archetype, impact, tower, step.Position, 0f);
                        state.Frozen = 1;
                        break;
                    case ProjectileKind.Cluster:
                        Blast(step.Position, state.BlastRadius, state.Damage, tower);
                        EffectPlayer.Play(impact, step.Position, Quaternion.identity, state.ImpactScale, ImpactLifetime);
                        break;
                }
                dead = true;
            }

            state.Position = dead ? deathPoint : step.Position;
            state.Direction = step.Direction;
            state.Rotation = step.Rotation;
            state.Lead = 0f;
            state.BornFrame = 0;
            if (dead)
            {
                state.Dying = 1;
                state.FadeTime = 0f;
            }
            states[i] = state;
        }

        // Flight effects follow; finished projectiles leave (bomblets at once, the others after their fade)
        for (int i = states.Length - 1; i >= 0; i--)
        {
            State state = states[i];
            bool gone = state.Dying != 0 && (state.Kind == (byte)ProjectileKind.Cluster || state.FadeTime >= Projectile.FadeDuration);
            FlightHandle flight = flights[i];
            if (gone)
            {
                flight.Release();
                RemoveAt(i);
                continue;
            }

            if (flight.IsValid)
            {
                float fade = state.Dying != 0 ? Mathf.Clamp01(1f - state.FadeTime / Projectile.FadeDuration) : 1f;
                flight.SetPose(state.Position, state.Rotation, state.Scale * fade, state.Direction * state.Speed);
            }
        }
    }

    private static void RememberHit(ref State state, int serial)
    {
        if (state.HitSerials.Length >= state.HitSerials.Capacity)
            state.HitSerials.RemoveAt(0);
        state.HitSerials.Add(serial);
    }

    // ProjectileBomb.ExplosionTrigger: blast, impact effect, and the bomblets of a cluster rocket (once)
    private void Explode(ref State state, ProjectileArchetype archetype, GameObject impact, Tower tower, float3 point, float leftover)
    {
        Blast(point, state.BlastRadius, state.Damage, tower);
        EffectPlayer.Play(impact, point, Quaternion.identity, state.ImpactScale, ImpactLifetime);

        if (state.Cluster == 0 || archetype.ClusterBomblet == null)
            return;
        state.Cluster = 0;

        ProjectileArchetype bomblet = archetype.ClusterBomblet;
        Quaternion rotation = state.Rotation;
        for (int k = 0; k < archetype.ClusterPositions.Length; k++)
        {
            Shot shot = new Shot
            {
                Position = (Vector3)point + rotation * (archetype.ClusterPositions[k] * state.Scale),
                Rotation = rotation * archetype.ClusterRotations[k],
                Scale = 1f,
                Damage = state.Damage * archetype.ClusterDamageShare,
                Speed = bomblet.ClusterSpeed,
                Lifetime = bomblet.ClusterLifetime,
                Accuracy = 1f,
                Pierce = 1,
                BlastRadius = state.BlastRadius * archetype.ClusterRadiusShare,
                Tower = tower,
            };
            // Spawned after this frame's step: moves the rest of this frame on top of the next one
            Spawn(bomblet, shot, -1, leftover);
        }
    }

    // ProjectileBomb/Clusterbomb.DamageInArea: every enemy whose collider overlaps the blast sphere
    private void Blast(float3 point, float radius, float damage, Tower tower)
    {
        Enemies.Overlap(point, radius, overlap);
        for (int i = 0; i < overlap.Count; i++)
            overlap[i].TakeDamage(damage, DamageType.EXPLOSIVE, tower);
    }

    private void RemoveAt(int index)
    {
        int last = states.Length - 1;
        states.RemoveAtSwapBack(index);
        archetypes[index] = archetypes[last];
        archetypes.RemoveAt(last);
        impactEffects[index] = impactEffects[last];
        impactEffects.RemoveAt(last);
        towers[index] = towers[last];
        towers.RemoveAt(last);
        flights[index] = flights[last];
        flights.RemoveAt(last);
    }

    // Blast query for others (Mine Factory): every living enemy whose collider overlaps the sphere
    public void OverlapEnemies(Vector3 center, float radius, List<Enemy> results)
    {
        Enemies.Overlap(center, radius, results);
    }

    // ---- jobs -------------------------------------------------------------------------------------------

    private static int CellKey(int3 cell)
    {
        return (int)math.hash(cell);
    }

    // Each enemy goes into every cell its movement this frame (expanded by its radius) touches
    [BurstCompile(CompileSynchronously = true)]
    private struct BuildGridJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> Previous;
        [ReadOnly] public NativeArray<float> Radii;
        [ReadOnly] public NativeArray<int> Serials;
        public NativeParallelMultiHashMap<int, int> Grid;

        public void Execute()
        {
            for (int slot = 0; slot < Positions.Length; slot++)
            {
                if (Serials[slot] == 0)
                    continue;
                float3 min = math.min(Positions[slot], Previous[slot]) - Radii[slot];
                float3 max = math.max(Positions[slot], Previous[slot]) + Radii[slot];
                int3 from = (int3)math.floor(min / CellSize);
                int3 to = (int3)math.floor(max / CellSize);
                for (int x = from.x; x <= to.x; x++)
                    for (int y = from.y; y <= to.y; y++)
                        for (int z = from.z; z <= to.z; z++)
                            Grid.Add(CellKey(new int3(x, y, z)), slot);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct StepJob : IJobParallelFor
    {
        // Each projectile's own state: a step without hits or expiry is applied right here
        public NativeArray<State> States;
        [ReadOnly] public NativeArray<float3> EnemyPositions;
        [ReadOnly] public NativeArray<float3> EnemyPrevious;
        [ReadOnly] public NativeArray<float> EnemyRadii;
        [ReadOnly] public NativeArray<int> EnemySerials;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
        public float DeltaTime;
        public int Frame;

        public NativeArray<Step> Steps;
        [NativeDisableParallelForRestriction] public NativeArray<Hit> Hits;
        public NativeList<int>.ParallelWriter Events;

        public void Execute(int i)
        {
            State s = States[i];
            // This projectile's share of the frame
            float time = s.BornFrame == Frame ? s.Lead : DeltaTime + s.Lead;
            Step step = new Step { Position = s.Position, Direction = s.Direction, Rotation = s.Rotation, Time = time };

            if (s.Dying != 0)
            {
                // A dead projectile drifts (unless it stopped) while its effect fades
                if (s.Frozen == 0)
                {
                    step.Position = s.Position + s.Direction * s.Speed * time;
                    s.Position = step.Position;
                }
                s.FadeTime += time;
                Steps[i] = step;
                States[i] = s;
                return;
            }

            float moveTime = time;
            if (s.Remaining <= time)
            {
                moveTime = math.max(0f, s.Remaining);
                step.Expired = 1;
            }

            // Where this projectile's time window starts inside the enemies' frame movement (0 = frame start)
            float windowStart = math.saturate((DeltaTime - time) / DeltaTime);

            float3 start = s.Position;
            float3 end;
            float3 direction = s.Direction;
            quaternion rotation = s.Rotation;
            if (s.Homing != 0 && s.TargetSlot >= 0 && s.TargetSlot < EnemySerials.Length && EnemySerials[s.TargetSlot] == s.TargetSerial)
            {
                // ProjectileBomb homing (MoveTowards + LookAt) in fixed steps against the target's movement
                float3 e0 = EnemyPrevious[s.TargetSlot];
                float3 e1 = EnemyPositions[s.TargetSlot];
                float3 p = start;
                float elapsed = 0f;
                while (elapsed < moveTime)
                {
                    float h = math.min(HomingStep, moveTime - elapsed);
                    elapsed += h;
                    float f = math.saturate(windowStart + elapsed / DeltaTime);
                    float3 target = math.lerp(e0, e1, f);
                    float3 toTarget = target - p;
                    float distance = math.length(toTarget);
                    float travel = s.Speed * h;
                    if (distance > 1e-5f)
                        direction = toTarget / distance;
                    p = distance <= travel ? target : p + direction * travel;
                }
                end = p;
                rotation = quaternion.LookRotationSafe(direction, new float3(1f, 0f, 0f));
            }
            else
            {
                end = start + direction * s.Speed * moveTime;
            }

            step.Position = end;
            step.Direction = direction;
            step.Rotation = rotation;
            step.HitCount = moveTime > 0f || time == 0f ? FindHits(i, ref s, start, end, windowStart) : 0;
            Steps[i] = step;
            if (step.HitCount > 0 || step.Expired != 0)
            {
                Events.AddNoResize(i);
                return;
            }

            s.Remaining -= time;
            s.Position = end;
            s.Direction = direction;
            s.Rotation = rotation;
            s.Lead = 0f;
            s.BornFrame = 0;
            States[i] = s;
        }

        // Sweeps the projectile's capsule from start to end against every enemy near the path, relative to the
        // enemy's own movement over the same time; keeps the earliest hits, sorted by time
        private int FindHits(int i, ref State s, float3 start, float3 end, float windowStart)
        {
            float3 axis = math.normalizesafe(end - start, s.Direction) * s.HalfLength;
            float3 back = start - axis;
            float3 front = end + axis;
            float3 min = math.min(back, front) - s.Radius;
            float3 max = math.max(back, front) + s.Radius;
            int3 from = (int3)math.floor(min / CellSize);
            int3 to = (int3)math.floor(max / CellSize);

            FixedList128Bytes<int> tested = default;
            int found = 0;
            int baseIndex = i * MaxHitsPerStep;

            for (int x = from.x; x <= to.x; x++)
            for (int y = from.y; y <= to.y; y++)
            for (int z = from.z; z <= to.z; z++)
            {
                if (!Grid.TryGetFirstValue(CellKey(new int3(x, y, z)), out int slot, out NativeParallelMultiHashMapIterator<int> iterator))
                    continue;
                do
                {
                    if (Contains(ref tested, slot))
                        continue;
                    if (tested.Length < tested.Capacity)
                        tested.Add(slot);

                    int serial = EnemySerials[slot];
                    if (serial == 0 || Contains(ref s.HitSerials, serial))
                        continue;

                    float3 e1 = EnemyPositions[slot];
                    float3 e0 = math.lerp(EnemyPrevious[slot], e1, windowStart);
                    float radius = s.Radius + EnemyRadii[slot];
                    if (!Sweep(back - e0, front - e1, radius, out float t))
                        continue;

                    // Insert sorted by time, keep the earliest MaxHitsPerStep
                    int position = found;
                    while (position > 0 && Hits[baseIndex + position - 1].T > t)
                        position--;
                    if (position >= MaxHitsPerStep)
                        continue;
                    int last = math.min(found, MaxHitsPerStep - 1);
                    for (int k = last; k > position; k--)
                        Hits[baseIndex + k] = Hits[baseIndex + k - 1];
                    Hits[baseIndex + position] = new Hit { Slot = slot, Serial = serial, T = t };
                    found = math.min(found + 1, MaxHitsPerStep);
                }
                while (Grid.TryGetNextValue(out slot, ref iterator));
            }
            return found;
        }

        private static bool Contains(ref FixedList128Bytes<int> list, int value)
        {
            for (int k = 0; k < list.Length; k++)
            {
                if (list[k] == value)
                    return true;
            }
            return false;
        }

        private static bool Contains(ref FixedList64Bytes<int> list, int value)
        {
            for (int k = 0; k < list.Length; k++)
            {
                if (list[k] == value)
                    return true;
            }
            return false;
        }
    }

    // First t in [0, 1] where the point a + t (b - a) is within radius of the origin
    public static bool Sweep(float3 a, float3 b, float radius, out float t)
    {
        t = 0f;
        float c = math.dot(a, a) - radius * radius;
        if (c <= 0f)
            return true;
        float3 d = b - a;
        float qa = math.dot(d, d);
        if (qa < 1e-12f)
            return false;
        float qb = 2f * math.dot(a, d);
        float discriminant = qb * qb - 4f * qa * c;
        if (discriminant < 0f)
            return false;
        t = (-qb - math.sqrt(discriminant)) / (2f * qa);
        return t >= 0f && t <= 1f;
    }
}
