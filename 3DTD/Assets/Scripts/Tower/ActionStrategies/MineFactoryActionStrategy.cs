using System.Collections.Generic;
using UnityEngine;

// Mine Factory: a floating factory that produces mines and lobs them onto the enemy path inside its range.
// A mine hovers on the path until an enemy touches it, then explodes. Strong bursts where it is placed, but the
// production is slow and the minefield is capped, so it holds a line rather than winning on its own.
// Without any path in range the mines hover above the factory and float towards enemies that come close.
//
// Stat usage:
//   FIRERATE = seconds per production cycle (the factory only produces while a wave is running)
//   AMOUNT   = mines per production cycle
//   AMMO     = maximum number of mines on the field
//   DAMAGE   = blast damage (EXPLOSIVE)
//   RADIUS   = blast radius
//   PIERCING = detonations per mine
//   RANGE    = radius around the factory in which mines are placed on the path
//   SPEED    = launch speed of the mines
//   SIZE     = mine size, also scales the contact radius
public class MineFactoryActionStrategy : ActionStrategy
{
    // Hard cap so stacked capacity upgrades can't flood the scene with mines
    public const int MaxMines = 40;

    private struct PathSpan
    {
        public int Lane;
        public Vector3 Start;
        public Vector3 Direction;
        public float Length;
        public float PathDistance;  // distance along the lane at Start
    }

    private struct PendingBlast
    {
        public float Delay;
        public Vector3 Position;
        public float Damage;
        public float Radius;
    }

    public Tower Tower => tower;
    public IReadOnlyList<Mine> Mines => mines;
    public int MineCap => Mathf.Clamp(Mathf.RoundToInt(tower.StatsManager.GetStatValue(Stat.StatType.AMMO)), 1, MaxMines);
    public Vector3 TowerUp => tower.transform.forward;

    [Header("Mines")]
    [SerializeField] private Mine minePrefab;
    [SerializeField] private Transform launchPoint;
    [Tooltip("An enemy closer than this to a mine (times SIZE) sets it off")]
    [SerializeField] private float contactRadius = 0.45f;
    [Tooltip("Seconds a mine with charges left needs before it can go off again")]
    [SerializeField] private float rearmTime = 0.4f;
    [SerializeField] private float minFlightTime = 0.35f;
    [Tooltip("Random points tried per mine; the best spread one wins")]
    [SerializeField] private int placementCandidates = 8;
    [Tooltip("Without a path in range, mines hover in a sphere this far above the factory")]
    [SerializeField] private float hoverZoneHeight = 1.4f;
    [SerializeField] private float hoverZoneRadius = 0.7f;

    [Header("Effects")]
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private GameObject heavyExplosionEffect;
    [SerializeField] private GameObject clusterExplosionEffect;
    [SerializeField] private GameObject launchEffect;
    [SerializeField] private GameObject salvageEffect;
    [SerializeField] private GameObject repairEffect;
    [SerializeField] private float effectLifetime = 3f;
    [Tooltip("The Polygon Arsenal effects are made for human-sized scenes: blasts are scaled by RADIUS times this")]
    [SerializeField] private float explosionScalePerRadius = 1f;
    [SerializeField] private float clusterEffectScale = 0.5f;
    [Tooltip("Scale of the launch, salvage and repair effects")]
    [SerializeField] private float smallEffectScale = 0.3f;

    [Header("Factory Animation")]
    [SerializeField] private Transform hoverBody;
    [SerializeField] private float hoverAmplitude = 0.05f;
    [SerializeField] private float hoverFrequency = 0.6f;
    [SerializeField] private Transform press;
    [Tooltip("Local offset of the press at the bottom of a stroke")]
    [SerializeField] private Vector3 pressStroke = new Vector3(0f, 0f, -0.12f);
    [SerializeField] private float pressReturnSpeed = 4f;
    [SerializeField] private Transform[] spinners;
    [SerializeField] private float spinnerSpeed = 140f;
    [Tooltip("Mine sitting in the launch chute, hidden for a moment after every launch and while the field is full")]
    [SerializeField] private GameObject loadedMineVisual;

    [Header("Warheads (changed by upgrades)")]
    [SerializeField] public int clusterBomblets = 0;
    [SerializeField] public float clusterDamageShare = 0.5f;
    [SerializeField] public float clusterRadiusShare = 0.6f;
    [SerializeField] public float clusterSpread = 1.2f;
    [SerializeField] public float blastSlow = 0f;
    [SerializeField] public float blastSlowDuration = 0f;
    [SerializeField] public bool heavyExplosions = false;
    [Tooltip("Armed mines drift towards enemies closer than this")]
    [SerializeField] public float seekRadius = 0f;
    [SerializeField] public float seekSpeed = 2.5f;

    [Header("Salvage (changed by upgrades)")]
    [Tooltip("Money per mine finished while the minefield is full (goes through GameManager.AddIncome)")]
    [SerializeField] public float scrapValue = 0f;
    [Tooltip("Every n-th scrapped mine restores a life, up to the starting lives (0 = never)")]
    [SerializeField] public int scrapsPerLife = 0;
    [SerializeField] public int maxLivesPerWave = 0;
    [Tooltip("Money per mine still on the field when a wave ends")]
    [SerializeField] public float waveEndPayoutPerMine = 0f;

    private Tower tower;
    private VisualRef blastVisual;
    private VisualRef heavyBlastVisual;
    private VisualRef clusterBlastVisual;
    private VisualRef launchVisual;
    private FireCycle fireCycle;
    public override FireCycle Cycle => fireCycle;
    private readonly List<Mine> mines = new List<Mine>();
    private readonly Stack<Mine> minePool = new Stack<Mine>();
    private readonly List<PendingBlast> pendingBlasts = new List<PendingBlast>();
    private readonly List<Enemy> enemyBuffer = new List<Enemy>();
    private readonly List<Enemy> blastTargets = new List<Enemy>();

    private readonly List<PathSpan> spans = new List<PathSpan>();
    private readonly List<Vector3> lanePoints = new List<Vector3>();
    private float spansTotalLength;
    private float spansMinDistance;
    private float spansMaxDistance;
    private float cachedRange = -1f;
    private Vector3 cachedCenter;
    private int cachedLaneCount = -1;

    private Spawner subscribedSpawner;
    private int scrappedMines;
    private int livesRestoredThisWave;

    private float animationTime;
    private float pressPosition;
    private float loadedHiddenTimer;
    private Vector3 pressRestPosition;
    private Vector3 hoverBodyRestPosition;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(0f);

        if (press != null)
            pressRestPosition = press.localPosition;
        if (hoverBody != null)
            hoverBodyRestPosition = hoverBody.localPosition;
        animationTime = Random.value * 10f;
    }

    public override void ExecuteAction()
    {
        if (tower == null)
            return;

        float deltaTime = Time.deltaTime;
        SubscribeToSpawner();
        Animate(deltaTime);
        if (deltaTime <= 0f)
            return;

        RefreshPathSpans();
        UpdateMines(deltaTime);
        CheckContacts();
        UpdatePendingBlasts(deltaTime);
        Produce(deltaTime);
    }

    public override bool CanShoot(GameObject enemy)
    {
        return enemy != null
            && enemy.TryGetComponent<Enemy>(out Enemy enemyComponent)
            && tower.Targetter.Contains(enemyComponent);
    }

    #region Production

    private void Produce(float deltaTime)
    {
        Spawner spawner = Spawner.Instance;
        bool waveActive = spawner != null && spawner.IsWaveActive;
        bool canSalvage = scrapValue > 0f || scrapsPerLife > 0;
        bool canProduce = waveActive && (mines.Count < MineCap || canSalvage);

        StatsManager stats = tower.StatsManager;
        int cycles = fireCycle.Tick(deltaTime, canProduce, stats.GetFireInterval(), 0f, 0f);
        int perCycle = Mathf.Max(1, Mathf.RoundToInt(stats.GetStatValue(Stat.StatType.AMOUNT)));

        for (int cycle = 0; cycle < cycles; cycle++)
        {
            for (int i = 0; i < perCycle; i++)
            {
                if (mines.Count < MineCap)
                    LaunchMine();
                else if (canSalvage)
                    ScrapMine();
            }
        }
    }

    private void LaunchMine()
    {
        if (minePrefab == null)
            return;

        StatsManager stats = tower.StatsManager;
        Vector3 from = launchPoint != null ? launchPoint.position : tower.transform.position;
        PickMineSpot(out Vector3 spot, out int lane, out Vector3 pathDirection);

        Mine mine = GetPooledMine();
        mine.Launch(from, spot, TowerUp,
            Mathf.Max(0.5f, stats.GetStatValue(Stat.StatType.SPEED)),
            minFlightTime,
            Mathf.Max(0.1f, stats.GetStatValue(Stat.StatType.SIZE)),
            lane, pathDirection,
            Mathf.Max(1, (int)stats.GetStatValue(Stat.StatType.PIERCING)));
        mines.Add(mine);

        pressPosition = 1f;
        loadedHiddenTimer = 0.35f;
        SpawnEffect(launchVisual.Get(tower, VisualSlot.MineLaunch, launchEffect), from, 1.5f, smallEffectScale);
    }

    // The field is full: the finished mine is sold for scrap (and maybe patches up the base)
    private void ScrapMine()
    {
        pressPosition = 1f;
        Vector3 position = launchPoint != null ? launchPoint.position : tower.transform.position;

        if (scrapValue > 0f && GameManager.Instance != null)
        {
            GameManager.Instance.AddIncome(scrapValue);
            SpawnEffect(salvageEffect, position, effectLifetime, smallEffectScale);
        }

        scrappedMines++;
        if (scrapsPerLife > 0 && scrappedMines % scrapsPerLife == 0 && livesRestoredThisWave < maxLivesPerWave && GameManager.Instance != null)
        {
            int restored = GameManager.Instance.RestoreLives(1);
            livesRestoredThisWave += restored;
            if (restored > 0)
                SpawnEffect(repairEffect, position, effectLifetime, smallEffectScale);
        }
    }

    private Mine GetPooledMine()
    {
        while (minePool.Count > 0)
        {
            Mine pooled = minePool.Pop();
            if (pooled != null)
                return pooled;
        }

        Mine mine = Instantiate(minePrefab);
        mine.name = tower.DisplayName + " Mine";
        mine.gameObject.SetActive(false);
        return mine;
    }

    private void ReleaseMine(Mine mine)
    {
        mines.Remove(mine);
        mine.Deactivate();
        minePool.Push(mine);
    }

    #endregion

    #region Placement

    // Spots on the path inside the range are weighted by length; of a few random candidates the one furthest
    // away from the other mines wins, nudged by the tower's targeting (FIRST = towards the exit, LAST = towards
    // the entrance, NEAREST / FARTHEST = distance to the factory, STRONGEST = spread only)
    private void PickMineSpot(out Vector3 spot, out int lane, out Vector3 pathDirection)
    {
        if (spans.Count == 0 || spansTotalLength <= 0f)
        {
            spot = HoverZoneCenter() + InsideUnitSphere() * hoverZoneRadius;
            lane = -1;
            pathDirection = Vector3.zero;
            return;
        }

        float spacing = Mathf.Max(0.5f, 2f * tower.StatsManager.GetStatValue(Stat.StatType.RADIUS));
        float range = Mathf.Max(0.5f, PlacementRadius());
        float bestScore = float.MinValue;
        spot = spans[0].Start;
        lane = spans[0].Lane;
        pathDirection = spans[0].Direction;

        int candidates = Mathf.Max(1, placementCandidates);
        for (int c = 0; c < candidates; c++)
        {
            float pick = tower.Rng.NextFloat() * spansTotalLength;
            PathSpan span = spans[spans.Count - 1];
            for (int i = 0; i < spans.Count; i++)
            {
                if (pick <= spans[i].Length)
                {
                    span = spans[i];
                    break;
                }
                pick -= spans[i].Length;
            }

            float along = Mathf.Clamp(pick, 0f, span.Length);
            Vector3 candidate = span.Start + span.Direction * along;

            float nearestMine = float.MaxValue;
            for (int i = 0; i < mines.Count; i++)
                nearestMine = Mathf.Min(nearestMine, Vector3.Distance(mines[i].Home, candidate));
            float spread = Mathf.Clamp01(nearestMine / spacing);

            float score = spread + 0.5f * TargetingBias(span.PathDistance + along, candidate, range);
            if (score > bestScore)
            {
                bestScore = score;
                spot = candidate;
                lane = span.Lane;
                pathDirection = span.Direction;
            }
        }
    }

    private float TargetingBias(float pathDistance, Vector3 candidate, float range)
    {
        float distanceSpan = spansMaxDistance - spansMinDistance;
        float progress = distanceSpan > 0.01f ? (pathDistance - spansMinDistance) / distanceSpan : 0.5f;
        float closeness = 1f - Mathf.Clamp01(Vector3.Distance(candidate, cachedCenter) / range);

        switch (tower.TargetBehaviour)
        {
            case TargetBehaviour.FIRST: return progress;
            case TargetBehaviour.LAST: return 1f - progress;
            case TargetBehaviour.NEAREST: return closeness;
            case TargetBehaviour.FARTHEST: return 1f - closeness;
            default: return 0f;
        }
    }

    private Vector3 HoverZoneCenter()
    {
        return tower.transform.position + TowerUp * hoverZoneHeight;
    }

    private float PlacementRadius()
    {
        // Matches the Targetter sphere (radius 1 scaled by RANGE), a bit inside so enemies are detected at the mines
        return tower.StatsManager.GetStatValue(Stat.StatType.RANGE) - 0.1f;
    }

    // The parts of every lane's path inside the placement sphere. Rebuilt when RANGE or the lanes change.
    private void RefreshPathSpans()
    {
        Spawner spawner = Spawner.Instance;
        int laneCount = spawner != null ? spawner.LaneCount : 0;
        float range = PlacementRadius();
        Vector3 center = tower.Targetter != null ? tower.Targetter.transform.position : tower.transform.position;

        if (range == cachedRange && laneCount == cachedLaneCount && center == cachedCenter)
            return;

        cachedRange = range;
        cachedLaneCount = laneCount;
        cachedCenter = center;
        spans.Clear();
        spansTotalLength = 0f;
        spansMinDistance = float.MaxValue;
        spansMaxDistance = float.MinValue;

        if (range <= 0f)
            return;

        for (int lane = 0; lane < laneCount; lane++)
        {
            spawner.GetLanePath(lane, lanePoints);
            float laneDistance = 0f;
            for (int i = 0; i + 1 < lanePoints.Count; i++)
            {
                Vector3 a = lanePoints[i];
                Vector3 segment = lanePoints[i + 1] - a;
                float length = segment.magnitude;
                if (length < 0.0001f)
                    continue;

                Vector3 direction = segment / length;
                // Line-sphere intersection, clipped to the segment
                Vector3 offset = a - center;
                float b = Vector3.Dot(offset, direction);
                float discriminant = b * b - (offset.sqrMagnitude - range * range);
                if (discriminant > 0f)
                {
                    float root = Mathf.Sqrt(discriminant);
                    float t0 = Mathf.Max(0f, -b - root);
                    float t1 = Mathf.Min(length, -b + root);
                    if (t1 - t0 > 0.01f)
                    {
                        spans.Add(new PathSpan { Lane = lane, Start = a + direction * t0, Direction = direction, Length = t1 - t0, PathDistance = laneDistance + t0 });
                        spansTotalLength += t1 - t0;
                        spansMinDistance = Mathf.Min(spansMinDistance, laneDistance + t0);
                        spansMaxDistance = Mathf.Max(spansMaxDistance, laneDistance + t1);
                    }
                }
                laneDistance += length;
            }
        }
    }

    #endregion

    #region Mines and detonation

    private void UpdateMines(float deltaTime)
    {
        float range = Mathf.Max(0.5f, PlacementRadius());
        bool anySeeking = seekRadius > 0f;
        for (int i = 0; i < mines.Count; i++)
        {
            if (mines[i].Lane < 0)
                anySeeking = true;
        }

        if (anySeeking)
        {
            enemyBuffer.Clear();
            enemyBuffer.AddRange(tower.Targetter.GetAllEnemiesInRadius());
        }

        for (int i = 0; i < mines.Count; i++)
        {
            Mine mine = mines[i];
            mine.Tick(deltaTime);

            // Hover mines (no path in range) chase anything inside the range, upgraded mines what comes close
            float radius = mine.Lane < 0 ? range * 2f : seekRadius;
            if (radius <= 0f || mine.State != Mine.MineState.ARMED)
                continue;

            Enemy nearest = null;
            float nearestDistance = radius;
            for (int e = 0; e < enemyBuffer.Count; e++)
            {
                Enemy enemy = enemyBuffer[e];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                float distance = Vector3.Distance(enemy.transform.position, mine.Home);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy;
                }
            }

            if (nearest == null)
                continue;

            Vector3 home = Vector3.MoveTowards(mine.Home, nearest.transform.position, seekSpeed * deltaTime);
            // Mines never leave the factory's range
            Vector3 fromCenter = home - cachedCenter;
            if (fromCenter.magnitude > range)
                home = cachedCenter + fromCenter.normalized * range;
            mine.MoveHome(home);
        }
    }

    // Enemies move far per frame at high game speed, so the contact test uses the path the enemy walked this
    // frame (tick start, the waypoint corner it passed, its position now) instead of its current position only.
    // Candidates come from the Spawner, not the Targetter: the range trigger only learns about an enemy in the
    // physics step after it entered, which is too late for one that crossed the range within a frame.
    private void CheckContacts()
    {
        if (mines.Count == 0 || Spawner.Instance == null)
            return;

        float size = Mathf.Max(0.1f, tower.StatsManager.GetStatValue(Stat.StatType.SIZE));
        float contact = contactRadius * size;
        float contactSqr = contact * contact;

        // Bounding sphere of the armed mines around the factory, for a cheap rejection of far enemies
        float reach = -1f;
        for (int i = 0; i < mines.Count; i++)
        {
            if (mines[i].IsArmed)
                reach = Mathf.Max(reach, Vector3.Distance(mines[i].transform.position, cachedCenter));
        }
        if (reach < 0f)
            return;
        reach += contact;
        float reachSqr = reach * reach;

        enemyBuffer.Clear();
        List<Enemy> alive = Spawner.Instance.AliveEnemies;
        for (int e = 0; e < alive.Count; e++)
        {
            Enemy enemy = alive[e];
            if (SqrDistanceToTickPath(cachedCenter, enemy) <= reachSqr)
                enemyBuffer.Add(enemy);
        }
        if (enemyBuffer.Count == 0)
            return;

        for (int i = mines.Count - 1; i >= 0; i--)
        {
            if (i >= mines.Count)
                continue;

            Mine mine = mines[i];
            if (!mine.IsArmed)
                continue;

            Vector3 minePosition = mine.transform.position;
            for (int e = 0; e < enemyBuffer.Count; e++)
            {
                Enemy enemy = enemyBuffer[e];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                if (SqrDistanceToTickPath(minePosition, enemy) <= contactSqr)
                {
                    Detonate(mine);
                    break;
                }
            }
        }
    }

    // Squared distance from a point to the path the enemy walked during its last tick
    private static float SqrDistanceToTickPath(Vector3 point, Enemy enemy)
    {
        Vector3 now = enemy.transform.position;
        Vector3 start = enemy.TickStartPosition;
        if (!enemy.HasTickCorner)
            return SqrDistanceToSegment(point, start, now);

        Vector3 corner = enemy.TickCorner;
        return Mathf.Min(SqrDistanceToSegment(point, start, corner), SqrDistanceToSegment(point, corner, now));
    }

    private static float SqrDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float lengthSqr = ab.sqrMagnitude;
        float t = lengthSqr > 0.000001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr) : 0f;
        return (a + ab * t - point).sqrMagnitude;
    }

    private void Detonate(Mine mine)
    {
        StatsManager stats = tower.StatsManager;
        float damage = stats.GetStatValue(Stat.StatType.DAMAGE);
        float radius = Mathf.Max(0.1f, stats.GetStatValue(Stat.StatType.RADIUS));
        Vector3 position = mine.transform.position;

        GameObject heavy = heavyBlastVisual.Get(tower, VisualSlot.MineHeavyBlast, heavyExplosionEffect);
        GameObject effect = heavyExplosions && heavy != null ? heavy : blastVisual.Get(tower, VisualSlot.MineBlast, explosionEffect);
        Blast(position, damage, radius, effect, radius * explosionScalePerRadius);

        // Bomblets bounce along the path (or in all directions for hover mines) and go off a moment later
        for (int i = 0; i < clusterBomblets; i++)
        {
            Vector3 direction = mine.PathDirection.sqrMagnitude > 0.5f ? mine.PathDirection * (i % 2 == 0 ? 1f : -1f) : OnUnitSphere();
            float distance = clusterSpread * (0.4f + 0.6f * ((i / 2) + 1f) / Mathf.Max(1f, Mathf.Ceil(clusterBomblets / 2f)));
            pendingBlasts.Add(new PendingBlast
            {
                Delay = 0.15f + 0.08f * i,
                Position = position + direction * distance + InsideUnitSphere() * 0.2f,
                Damage = damage * clusterDamageShare,
                Radius = radius * clusterRadiusShare,
            });
        }

        mine.Charges--;
        if (mine.Charges > 0)
            mine.Rearm(rearmTime);
        else
            ReleaseMine(mine);
    }

    private void UpdatePendingBlasts(float deltaTime)
    {
        for (int i = pendingBlasts.Count - 1; i >= 0; i--)
        {
            PendingBlast blast = pendingBlasts[i];
            blast.Delay -= deltaTime;
            if (blast.Delay > 0f)
            {
                pendingBlasts[i] = blast;
                continue;
            }

            pendingBlasts.RemoveAt(i);
            Blast(blast.Position, blast.Damage, blast.Radius, clusterBlastVisual.Get(tower, VisualSlot.MineClusterBlast, clusterExplosionEffect), clusterEffectScale);
        }
    }

    private void Blast(Vector3 position, float damage, float radius, GameObject effect, float effectScale)
    {
        SpawnEffect(effect, position, effectLifetime, effectScale);

        // Every enemy whose collider overlaps the blast; physics overlaps capped at 128 colliders, which a
        // dense field of blocks and tower ranges could fill before any enemy
        ProjectileSystem.Instance.OverlapEnemies(position, radius, blastTargets);
        for (int i = 0; i < blastTargets.Count; i++)
        {
            Enemy enemy = blastTargets[i];
            if (!enemy.IsAlive)
                continue;

            enemy.TakeDamage(damage, DamageType.EXPLOSIVE, tower);
            if (blastSlow > 0f && enemy.IsAlive)
                enemy.ApplySlowness(-blastSlow, blastSlowDuration);
        }
    }

    private void SpawnEffect(GameObject effect, Vector3 position, float lifetime, float scale)
    {
        EffectPlayer.Play(effect, position, Quaternion.identity, scale, lifetime);
    }

    // Gameplay randomness from the tower's own stream (see Tower.Rng)
    private Vector3 OnUnitSphere()
    {
        return tower.Rng.NextFloat3Direction();
    }

    private Vector3 InsideUnitSphere()
    {
        return tower.Rng.NextFloat3Direction() * Mathf.Pow(tower.Rng.NextFloat(), 1f / 3f);
    }

    #endregion

    #region Waves

    private void SubscribeToSpawner()
    {
        Spawner spawner = Spawner.Instance;
        if (spawner == subscribedSpawner)
            return;

        if (subscribedSpawner != null)
            subscribedSpawner.OnWaveEnded -= HandleWaveEnded;
        subscribedSpawner = spawner;
        if (subscribedSpawner != null)
            subscribedSpawner.OnWaveEnded += HandleWaveEnded;
    }

    private void HandleWaveEnded(int round)
    {
        livesRestoredThisWave = 0;

        if (waveEndPayoutPerMine > 0f && mines.Count > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AddIncome(waveEndPayoutPerMine * mines.Count);
            SpawnEffect(salvageEffect, launchPoint != null ? launchPoint.position : tower.transform.position, effectLifetime, smallEffectScale);
        }
    }

    #endregion

    #region Factory animation

    private void Animate(float deltaTime)
    {
        animationTime += deltaTime;

        if (hoverBody != null && tower != null)
        {
            // Bob along the tower's up axis, whichever face the factory was built on
            Vector3 localUp = hoverBody.parent != null ? hoverBody.parent.InverseTransformDirection(TowerUp) : TowerUp;
            hoverBody.localPosition = hoverBodyRestPosition + localUp * (Mathf.Sin(animationTime * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude);
        }

        if (press != null)
        {
            pressPosition = Mathf.MoveTowards(pressPosition, 0f, pressReturnSpeed * deltaTime);
            press.localPosition = pressRestPosition + pressStroke * Mathf.Sin(pressPosition * Mathf.PI * 0.5f);
        }

        bool producing = Spawner.Instance != null && Spawner.Instance.IsWaveActive;
        if (spinners != null && producing)
        {
            foreach (Transform spinner in spinners)
            {
                if (spinner != null)
                    spinner.Rotate(Vector3.up, spinnerSpeed * deltaTime, Space.Self);
            }
        }

        if (loadedMineVisual != null)
        {
            loadedHiddenTimer -= deltaTime;
            bool show = loadedHiddenTimer <= 0f && (tower == null || mines.Count < MineCap);
            if (loadedMineVisual.activeSelf != show)
                loadedMineVisual.SetActive(show);
        }
    }

    #endregion

    private void OnDestroy()
    {
        if (subscribedSpawner != null)
            subscribedSpawner.OnWaveEnded -= HandleWaveEnded;

        foreach (Mine mine in mines)
        {
            if (mine != null)
                Destroy(mine.gameObject);
        }
        foreach (Mine mine in minePool)
        {
            if (mine != null)
                Destroy(mine.gameObject);
        }
        mines.Clear();
        minePool.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (PathSpan span in spans)
            Gizmos.DrawLine(span.Start, span.Start + span.Direction * span.Length);
    }
}
