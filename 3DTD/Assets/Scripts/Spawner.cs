using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    // A released enemy stays inactive this long (game seconds) before it is reused, so homing
    // projectiles and starfighters that still point at it notice it is gone first
    private const float EnemyReuseDelay = 3f;
    // The enemy prefab has 52 objects; instantiating many in one frame stalls the game, so the pool is
    // filled ahead of demand a few per frame, up to the current or next wave's enemy count
    private const int PrewarmPerFrameIdle = 6;
    private const int PrewarmPerFrameInWave = 2;
    private const int MaxPrewarmedEnemies = 2000;

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private List<WaveData> waves;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Waypoints[] waypoints;
    [SerializeField] private bool autoPlay = false;
    [SerializeField] private GameState currentGameState;
    [SerializeField] private float scalingFactor = 0.1f;

    private List<GameObject> enemiesAlive = new List<GameObject>();
    // Parallel to enemiesAlive, so a dead enemy is swapped out in O(1) through Enemy.AliveIndex
    private readonly List<Enemy> aliveEnemies = new List<Enemy>();
    [SerializeField] private int enemiesInWave = 0;

    public event Action<int> OnWaveStarted;
    public event Action<int> OnWaveEnded;

    private static Spawner instance;
    public static Spawner Instance { get { return instance; } }
    public List<GameObject> EnemiesAlive { get { return enemiesAlive; } }
    // The same enemies as Enemy components (index order matches EnemiesAlive)
    public List<Enemy> AliveEnemies => aliveEnemies;
    public int WaveCount => waves != null ? waves.Count : 0;
    public bool IsWaveActive => currentGameState == GameState.PROGRESSING;
    public bool IsWon => isWon;
    public int LaneCount => spawnPoints != null && waypoints != null ? Mathf.Min(spawnPoints.Length, waypoints.Length) : 0;

    private bool isWon = false;
    private int lanesSpawning = 0;

    private struct PooledEnemy
    {
        public GameObject Enemy;
        public float ReleasedAt;
    }
    private readonly Queue<PooledEnemy> enemyPool = new Queue<PooledEnemy>();
    // Prewarmed enemies that never lived yet: usable at once, unlike released ones. They wait under an
    // inactive container, so they don't run Awake/OnEnable until they are spawned.
    private readonly Stack<GameObject> freshEnemies = new Stack<GameObject>();
    private Transform freshEnemyContainer;
    private int prewarmTarget;

    private class RuntimeWave
    {
        public readonly List<EnemyData> EnemiesToSpawn;
        public readonly List<int> EnemySpawnCount;
        public readonly List<float> SpawnDelay;
        public readonly List<EnemyTrait> Traits;

        public RuntimeWave(WaveData source)
        {
            EnemiesToSpawn = source.EnemiesToSpawn != null ? new List<EnemyData>(source.EnemiesToSpawn) : new List<EnemyData>();
            EnemySpawnCount = source.EnemySpawnCount != null ? new List<int>(source.EnemySpawnCount) : new List<int>();
            SpawnDelay = source.SpawnDelay != null ? new List<float>(source.SpawnDelay) : new List<float>();
            Traits = new List<EnemyTrait>();
            for (int i = 0; i < EnemiesToSpawn.Count; i++)
                Traits.Add(source.TraitsAt(i));
        }

        public int EntryCount
        {
            get
            {
                return Mathf.Min(EnemiesToSpawn.Count, EnemySpawnCount.Count, SpawnDelay.Count);
            }
        }
    }

    private void Start()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        currentGameState = GameState.IDLE;
        SetPrewarmTargetForNextWave();
        DeathEffectRenderer.Prewarm();
    }

    // Enemies instantiated so far (the pool grows until it holds a wave); the benchmark leaves those frames out of
    // its steady-state garbage figure
    public int EnemiesCreated { get; private set; }

    // Whether the enemy pool is still growing towards the next wave's size
    public bool IsPrewarming => enemyPrefab != null && freshEnemies.Count + enemyPool.Count + enemiesAlive.Count < prewarmTarget;

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        // Backwards: an enemy that leaves the list swaps the last one into its place
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
            aliveEnemies[i].Tick(deltaTime);

        PrewarmEnemies();

        if (currentGameState != GameState.PROGRESSING)
            return;

        if (enemiesAlive.Count != 0 || lanesSpawning > 0)
            return;

        currentGameState = GameState.IDLE;

        // The game counts as won as soon as the win round is cleared with lives left; later waves are freeplay
        if (!isWon && GameManager.Instance.Round >= GameManager.Instance.GetWinRound(waves.Count) && GameManager.Instance.Lives > 0)
        {
            isWon = true;
            GameManager.Instance.GameWon();
        }

        OnWaveEnded?.Invoke(GameManager.Instance.Round);
        SetPrewarmTargetForNextWave();

        if (autoPlay)
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (currentGameState != GameState.IDLE || waves == null || waves.Count == 0 || spawnPoints == null || waypoints == null)
            return;

        bool infiniteWave = GameManager.Instance.Round >= waves.Count;
        RuntimeWave wave = BuildRuntimeWave(infiniteWave);
        int laneCount = Mathf.Min(spawnPoints.Length, waypoints.Length);
        int spawnCount = GetSpawnCount(wave);

        if (laneCount == 0 || spawnCount == 0)
            return;

        enemiesInWave = spawnCount * laneCount;
        prewarmTarget = Mathf.Min(MaxPrewarmedEnemies, Mathf.Max(prewarmTarget, enemiesInWave));

        int roundStarted = GameManager.Instance.Round;
        GameManager.Instance.Round++;
        currentGameState = GameState.PROGRESSING;
        OnWaveStarted?.Invoke(roundStarted);

        for (int i = 0; i < laneCount; i++)
        {
            StartCoroutine(SpawningWave(spawnPoints[i], waypoints[i], wave));
        }
    }

    private void SetPrewarmTargetForNextWave()
    {
        if (waves == null || waves.Count == 0 || GameManager.Instance == null)
            return;
        RuntimeWave next = BuildRuntimeWave(GameManager.Instance.Round >= waves.Count);
        prewarmTarget = Mathf.Min(MaxPrewarmedEnemies, Mathf.Max(prewarmTarget, GetSpawnCount(next) * LaneCount));
    }

    private void PrewarmEnemies()
    {
        if (enemyPrefab == null)
            return;
        int available = freshEnemies.Count + enemyPool.Count + enemiesAlive.Count;
        if (available >= prewarmTarget)
            return;

        if (freshEnemyContainer == null)
        {
            // A scene root: the Spawner object itself is scaled, and enemies must keep the prefab's own scale
            GameObject container = new GameObject("Enemy Pool");
            container.SetActive(false);
            freshEnemyContainer = container.transform;
        }

        int budget = currentGameState == GameState.PROGRESSING ? PrewarmPerFrameInWave : PrewarmPerFrameIdle;
        for (int i = 0; i < budget && available < prewarmTarget; i++, available++)
        {
            freshEnemies.Push(Instantiate(enemyPrefab, freshEnemyContainer));
            EnemiesCreated++;
        }
    }

    // A restored savegame continues after its last completed wave; a won game stays won (freeplay)
    public void RestoreProgress(bool won)
    {
        isWon = won;
        currentGameState = GameState.IDLE;
        SetPrewarmTargetForNextWave();
    }

    // The points an enemy of this lane walks through: its spawn point, then every waypoint until it touches
    // the End trigger. Waypoints past the exit are left out: enemies leak at the trigger (or, as a backstop, at the
    // last waypoint) and never walk them.
    public void GetLanePath(int lane, List<Vector3> points)
    {
        points.Clear();
        if (lane < 0 || lane >= LaneCount || spawnPoints[lane] == null || waypoints[lane] == null)
            return;

        Collider[] exits = GetExitColliders();
        float enemyRadius = GetEnemyRadius();
        Vector3 previous = spawnPoints[lane].position;
        points.Add(previous);

        foreach (Transform waypoint in waypoints[lane].WaypointsArray)
        {
            if (waypoint == null)
                continue;

            Vector3 next = waypoint.position;
            if (TryFindExit(previous, next, exits, enemyRadius, out Vector3 exitPoint))
            {
                points.Add(exitPoint);
                return;
            }
            points.Add(next);
            previous = next;
        }
    }

    private static Collider[] GetExitColliders()
    {
        GameObject end = End.Instance != null ? End.Instance.gameObject : GameObject.FindGameObjectWithTag("End");
        return end != null ? end.GetComponents<Collider>() : new Collider[0];
    }

    private float GetEnemyRadius()
    {
        if (enemyPrefab != null && enemyPrefab.TryGetComponent<SphereCollider>(out SphereCollider sphere))
            return sphere.radius * enemyPrefab.transform.localScale.x;
        return 0.375f;
    }

    // First point of the segment where an enemy sphere touches one of the exit colliders
    private static bool TryFindExit(Vector3 from, Vector3 to, Collider[] exits, float enemyRadius, out Vector3 exitPoint)
    {
        exitPoint = to;
        if (exits.Length == 0)
            return false;

        const float step = 0.05f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / step));
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
            foreach (Collider exit in exits)
            {
                if (exit != null && exit.enabled && (exit.ClosestPoint(point) - point).sqrMagnitude <= enemyRadius * enemyRadius)
                {
                    exitPoint = point;
                    return true;
                }
            }
        }
        return false;
    }

    private RuntimeWave BuildRuntimeWave(bool infiniteWave)
    {
        if (infiniteWave)
        {
            RuntimeWave wave = new RuntimeWave(waves[waves.Count - 1]);
            int infiniteRoundOffset = GameManager.Instance.Round - (waves.Count - 1);
            DifficultyProfile profile = GameManager.Instance.Profile;
            float factor = profile != null ? profile.freeplayScaling : scalingFactor;

            for (int i = 0; i < wave.EntryCount; i++)
            {
                float scaling = factor * infiniteRoundOffset;
                wave.EnemySpawnCount[i] += Mathf.RoundToInt(wave.EnemySpawnCount[i] * scaling);
                wave.SpawnDelay[i] = Mathf.Max(0.01f, wave.SpawnDelay[i] - (wave.SpawnDelay[i] * scaling));
            }

            return wave;
        }

        return new RuntimeWave(waves[GameManager.Instance.Round]);
    }

    private int GetSpawnCount(RuntimeWave wave)
    {
        int spawnCount = 0;

        for (int i = 0; i < wave.EntryCount; i++)
        {
            if (wave.EnemiesToSpawn[i] != null)
                spawnCount += Mathf.Max(0, wave.EnemySpawnCount[i]);
        }

        return spawnCount;
    }

    // Spawns on game time: several enemies can be due in one frame, and each one is moved forward
    // by the time it was overdue, so spacing doesn't depend on the frame rate
    IEnumerator SpawningWave(Transform spawnPoint, Waypoints waypoints, RuntimeWave wave)
    {
        lanesSpawning++;
        float timer = 0f;

        for (int i = 0; i < wave.EntryCount; i++)
        {
            if (wave.EnemiesToSpawn[i] == null)
                continue;

            for (int j = 0; j < Mathf.Max(0, wave.EnemySpawnCount[i]); j++)
            {
                while (timer > 0f)
                {
                    yield return null;
                    timer -= Time.deltaTime;
                }

                Enemy enemy = SpawnEnemy(spawnPoint, waypoints, wave.EnemiesToSpawn[i], wave.Traits[i]);
                if (timer < 0f)
                    enemy.Move(-timer);

                timer += wave.SpawnDelay[i];
            }
        }

        lanesSpawning--;
    }

    private Enemy SpawnEnemy(Transform spawnPoint, Waypoints path, EnemyData data, EnemyTrait traits)
    {
        GameObject enemyObject = null;
        if (enemyPool.Count > 0 && Time.time - enemyPool.Peek().ReleasedAt >= EnemyReuseDelay)
            enemyObject = enemyPool.Dequeue().Enemy;

        bool fresh = false;
        while (enemyObject == null && freshEnemies.Count > 0)
        {
            enemyObject = freshEnemies.Pop();
            fresh = enemyObject != null;
        }

        if (enemyObject == null)
        {
            enemyObject = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            EnemiesCreated++;
        }
        else
        {
            // Keeps the prefab's local scale (worldPositionStays would carry a parent's scale over)
            if (fresh)
                enemyObject.transform.SetParent(null, false);
            enemyObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            enemyObject.SetActive(true);
        }

        Enemy enemy = enemyObject.GetComponent<Enemy>();
        enemy.Initialize(data, path, traits);

        enemy.AliveIndex = enemiesAlive.Count;
        enemiesAlive.Add(enemyObject);
        aliveEnemies.Add(enemy);
        return enemy;
    }

    // Called by Enemy when it died or leaked
    public void ReleaseEnemy(Enemy enemy)
    {
        RemoveAlive(enemy);
        enemy.gameObject.SetActive(false);
        enemyPool.Enqueue(new PooledEnemy { Enemy = enemy.gameObject, ReleasedAt = Time.time });
    }

    private void RemoveAlive(Enemy enemy)
    {
        int index = enemy.AliveIndex;
        if (index < 0 || index >= aliveEnemies.Count || aliveEnemies[index] != enemy)
            return;   // placed by hand, not spawned

        int last = aliveEnemies.Count - 1;
        enemiesAlive[index] = enemiesAlive[last];
        aliveEnemies[index] = aliveEnemies[last];
        aliveEnemies[index].AliveIndex = index;
        enemiesAlive.RemoveAt(last);
        aliveEnemies.RemoveAt(last);
        enemy.AliveIndex = -1;
        enemiesInWave--;
    }
}

public enum GameState
{
    IDLE,
    PROGRESSING
}
