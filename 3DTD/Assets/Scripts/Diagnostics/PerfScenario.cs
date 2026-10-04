#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// Performance benchmark that runs inside the game (Editor play mode or a development player build).
// Start Unity or the player with  -perfSuite core|full|<run id>[,<run id>...]  [-perfOut <dir>]
// Editor batch mode: Assets/Editor/Perf/PerfBenchmark.cs opens the first scene and enters play mode.
//
// Runs (see BuildSuite):
//   S1  every tower type in each of its heaviest legal upgrade combinations, on blocks along the lane
//   S2  Bullet Dispensers (path 1 tier 3 + path 2 tier 2) on every block spot along the lane
//   P   a weak mixed defense whose leaks make tower effectiveness visible, for 1x/3x/5x parity
// Each run reloads its scene, builds the layout, jumps to the round and measures the wave(s).
// Log lines: PERF RUN|PASS|FAIL. Results: perf-<suite>-<time>.csv (per frame) and .json (summary).
[DefaultExecutionOrder(-10000)]
public class PerfScenario : MonoBehaviour
{
    public const float FrameBudgetMs = 1000f / 60f;
    public const float HitchMs = 33.3f;
    public const float ParityTolerance = 0.03f;
    public const float ParityTypeTolerance = 0.05f;
    // Dispenser spam: about what a Medium game earns by round 100 (maxed dispenser + block = 1670 scrap)
    public const int SpamTowers = 150;
    public const float MaxEditorGcBytesPerFrame = 1024f;
    // Prefab names in Beginner Level 01's palette
    private static readonly string[] ParityTowers =
    {
        "Default Tower", "Core Tower", "Bomb Tower", "Sniper Tower", "Beam Tower", "Bullet Dispenser Tower", "Hangar Tower", "Mine Factory",
    };

    [Serializable]
    public class Run
    {
        public string id;
        public string layout;            // "mixed", "dispensers", "parity", "single"
        public string tower;             // prefab name for the "single" layout
        public string scene = "Beginner Level 01";
        public float speed = 1f;
        public int round = 100;          // number of the first wave that is played
        public int waves = 3;            // consecutive waves measured (auto-wave), for stable statistics
        public int maxTowers = 0;        // 0: every spot found along the lane
        public int seed = 1234;
        public string parityGroup;       // runs with the same group are compared against their 1x run
        public bool informational;       // parity deviations are logged but don't fail
        public bool visual;              // fixed 1/60 s frames, a series of close-ups, then stop
    }

    [Serializable]
    public class RunResult
    {
        public string id;
        public string layout;
        public float speed;
        public int round;
        public int towers;
        public int frames;
        public float realSeconds;
        public float gameSeconds;
        public float gameSpeedRatio;
        public float frameP50, frameP95, frameP99, frameMax;
        public float mainP50, mainP99, mainMax;
        public float steadyGcFrameShare;
        public float steadyGcPerFrame;
        public long steadyGcBytes;
        public long totalGcBytes;
        public float damage;
        public int kills;
        public int leakedLives;
        public long projectilesRequested, projectilesSpawned;
        public long pops, deathEffectsRequested, deathEffectsPlayed;
        public long effectsRequested, effectsPlayed;
        public bool timedOut;
        public int wrongEnemyScale;
        public List<string> damageByType = new List<string>();
        public List<string> markers = new List<string>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public class SuiteResult
    {
        public string suite;
        public string startedAt;
        public string unityVersion;
        public string device;
        public List<RunResult> runs = new List<RunResult>();
        public List<string> failures = new List<string>();
    }

    private struct FrameSample
    {
        public float time;
        public float realMs;
        public float mainMs;
        public float gameDt;
        public long gcBytes;
        public int enemies;
    }

    // Set when the suite finished; PerfBenchmark (Editor) polls these
    public static bool Finished { get; private set; }
    public static int FailureCount { get; private set; }
    public static string SummaryPath { get; private set; }

    private static readonly string[] MarkerNames =
    {
        "PlayerLoop",
        "Update.ScriptRunBehaviourUpdate",
        "PreLateUpdate.ScriptRunBehaviourLateUpdate",
        "FixedUpdate.PhysicsFixedUpdate",
        "FixedUpdate.ScriptRunBehaviourFixedUpdate",
        "PreUpdate.PhysicsUpdate",
        "Physics.Simulate",
        "Physics.SyncTransforms",
        "Physics.SendTriggerEvents",
        "Physics.SendContactEvents",
        "ParticleSystem.WaitForUpdateThreads",
        "ParticleSystem.EndUpdateAll",
        "GC.Collect",
        "Instantiate",
        "Canvas.SendWillRenderCanvases",
    };

    private string suiteName;
    private string outputDirectory;
    private List<Run> runs;
    private SuiteResult suite;
    private StringBuilder frameCsv;

    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder gcRecorder;
    private ProfilerRecorder[] markerRecorders;
    private double[] markerTotals;

    private RenderTexture batchRenderTarget;
    private bool recording;
    private bool skipNextSample;
    private readonly List<FrameSample> samples = new List<FrameSample>(16384);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string requested = Argument("-perfSuite");
        if (string.IsNullOrEmpty(requested) || FindAnyObjectByType<PerfScenario>() != null)
            return;

        GameObject host = new GameObject("PerfScenario");
        DontDestroyOnLoad(host);
        PerfScenario scenario = host.AddComponent<PerfScenario>();
        scenario.suiteName = requested;
        scenario.outputDirectory = Argument("-perfOut");
        if (string.IsNullOrEmpty(scenario.outputDirectory))
        {
            scenario.outputDirectory = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tasks/perf"))
                : Path.Combine(Application.persistentDataPath, "perf");
        }
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }

    public static List<Run> BuildSuite(string name)
    {
        List<Run> all = new List<Run>
        {
            new Run { id = "S1-1x", layout = "mixed", speed = 1f, round = 100 },
            new Run { id = "S1-5x", layout = "mixed", speed = 5f, round = 100 },
            new Run { id = "S2", layout = "dispensers", speed = 1f, round = 100, maxTowers = SpamTowers },
            new Run { id = "S3", layout = "dispensers", speed = 5f, round = 100, maxTowers = SpamTowers },
            // A weak mixed defense; results vary about 9 % between seeds, so this group is informational
            new Run { id = "P-1x", layout = "parity", speed = 1f, round = 60, waves = 1, parityGroup = "P", informational = true },
            new Run { id = "P-3x", layout = "parity", speed = 3f, round = 60, waves = 1, parityGroup = "P", informational = true },
            new Run { id = "P-5x", layout = "parity", speed = 5f, round = 60, waves = 1, parityGroup = "P", informational = true },
            new Run { id = "S1-r50", layout = "mixed", speed = 1f, round = 50 },
            new Run { id = "S1-r75", layout = "mixed", speed = 1f, round = 75 },
            new Run { id = "S2P-1x", layout = "dispensers", speed = 1f, round = 60, maxTowers = 12, waves = 1, parityGroup = "S2P" },
            new Run { id = "S2P-5x", layout = "dispensers", speed = 5f, round = 60, maxTowers = 12, waves = 1, parityGroup = "S2P" },
            // Same game state every time (fixed frames, seeded): compare the effects with and without batching
            new Run { id = "V", layout = "dispensers", speed = 1f, round = 60, maxTowers = 12, waves = 1, visual = true },
        };

        // Tower parity: one maxed tower of each type alone against an overwhelming wave, at 1x/3x/5x.
        // Its damage measures its effectiveness without the noise of a whole defense.
        List<Run> parity = new List<Run>();
        foreach (string tower in ParityTowers)
        {
            foreach (float speed in new[] { 1f, 3f, 5f })
            {
                parity.Add(new Run
                {
                    id = "T-" + tower.Replace(" Tower", "").Replace(" ", "") + "-" + speed + "x",
                    layout = "single", tower = tower, speed = speed, round = 60, waves = 1, parityGroup = tower,
                });
            }
        }

        List<Run> picked;
        if (name == "full")
        {
            picked = new List<Run>(all);
            picked.AddRange(parity);
        }
        else if (name == "core")
            picked = all.GetRange(0, 4);
        else if (name == "parity")
            picked = parity;
        else
        {
            picked = new List<Run>();
            foreach (string id in name.Split(','))
            {
                Run run = all.Find(r => r.id == id.Trim()) ?? parity.Find(r => r.id == id.Trim());
                if (run != null)
                    picked.Add(run);
            }
        }

        // -perfSeed n: same runs with another random seed, to see how much results vary between runs
        string seed = Argument("-perfSeed");
        if (!string.IsNullOrEmpty(seed) && int.TryParse(seed, out int value))
        {
            foreach (Run run in picked)
                run.seed = value;
        }
        return picked;
    }

    private IEnumerator Start()
    {
        Finished = false;
        runs = BuildSuite(suiteName);
        suite = new SuiteResult
        {
            suite = suiteName,
            startedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
            unityVersion = Application.unityVersion,
            device = SystemInfo.processorType + " / " + SystemInfo.graphicsDeviceName,
        };
        frameCsv = new StringBuilder("run,frame,time,realMs,mainMs,gameDt,gcBytes,enemies\n");
        Directory.CreateDirectory(outputDirectory);

        StartRecorders();
        foreach (Run run in runs)
            yield return RunOne(run);
        StopRecorders();

        EvaluateParity();
        WriteResults();
    }

    // ---- one run ------------------------------------------------------------------------------------------

    private IEnumerator RunOne(Run run)
    {
        Debug.Log("PERF RUN " + run.id + " (" + run.layout + ", " + run.speed + "x, round " + run.round + ")");
        SceneManager.LoadScene(run.scene);
        yield return null;
        yield return null;
        yield return null;

        ApplyBenchmarkSettings();
        Random.InitState(run.seed);

        GameManager game = GameManager.Instance;
        Spawner spawner = Spawner.Instance;
        RunResult result = new RunResult { id = run.id, layout = run.layout, speed = run.speed, round = run.round };
        if (!run.visual)
            suite.runs.Add(result);
        if (game == null || spawner == null)
        {
            result.failures.Add(run.id + ": scene has no GameManager or Spawner");
            yield break;
        }

        game.Money = 100000000;
        game.Lives = 1000000;
        spawner.RestoreProgress(true);   // freeplay: no victory screen at the win round

        List<Vector3> spots = FindBlockSpots(spawner);
        List<TowerPlan> plans = PlanTowers(run, game, spots.Count);
        List<Tower> towers = new List<Tower>();
        GameObject blockPrefab = game.Buildings.Find(b => b != null && b.GetComponent<BuildingBlock>() != null);
        for (int i = 0; i < plans.Count && i < spots.Count; i++)
        {
            // Spread the towers evenly along the lane when there are more spots than towers; a single tower
            // stands a third of the way along
            int spot = run.layout == "single" ? spots.Count / 3
                : plans.Count < spots.Count ? Mathf.FloorToInt(i * spots.Count / (float)plans.Count) : i;
            Tower tower = BuildOnBlock(blockPrefab, plans[i].prefab, spots[spot]);
            if (tower != null)
            {
                towers.Add(tower);
                plans[i].instance = tower;
            }
        }
        result.towers = towers.Count;
        Debug.Log("PERF layout " + run.id + ": " + spots.Count + " block spots, " + plans.Count + " towers planned, " + towers.Count + " built");

        // Towers set up their action strategy in Start; strategy-swapping upgrades must come after it
        yield return null;
        yield return null;
        foreach (TowerPlan plan in plans)
        {
            if (plan.instance != null)
                ApplyTiers(plan.instance, plan.tiers);
        }
        AimBeams(towers, spawner);
        yield return null;

        game.Round = run.round - 1;
        int livesBefore = game.Lives;
        game.ChangeGameSpeed(run.speed);
        PerfCounters.Reset();

        if (run.visual)
        {
            yield return CaptureSeries(run, game, spawner, towers);
            yield break;
        }

        samples.Clear();
        recording = true;
        float startReal = Time.realtimeSinceStartup;
        float timeout = startReal + 300f;
        bool captured = false;
        for (int wave = 0; wave < Mathf.Max(1, run.waves) && Time.realtimeSinceStartup < timeout; wave++)
        {
            spawner.StartNextWave();
            while (spawner.IsWaveActive && Time.realtimeSinceStartup < timeout)
            {
                // One screenshot per run once the first wave is in full swing; its frame stays out of the statistics
                if (!captured && Time.realtimeSinceStartup - startReal > 4f / Mathf.Max(1f, run.speed) + 2f)
                {
                    captured = true;
                    result.wrongEnemyScale = CountWrongEnemyScales(spawner);
                    string suffix = EffectPlayer.BatchingEnabled ? "" : "-pooled";
                    Capture(Path.Combine(outputDirectory, "perf-" + run.id + suffix + ".png"), null);
                    // Close-up of the busiest stretch: the tower nearest to the enemies
                    Tower focus = NearestTower(towers, spawner);
                    if (focus != null)
                        Capture(Path.Combine(outputDirectory, "perf-" + run.id + suffix + "-close.png"), focus.transform.position);
                    skipNextSample = true;
                }
                yield return null;
            }
        }
        recording = false;
        result.timedOut = spawner.IsWaveActive;
        game.ChangeGameSpeed(1f);

        Summarize(run, result, towers, livesBefore - game.Lives);
        yield return null;
    }

    private void ApplyBenchmarkSettings()
    {
        // -perfNoBatching: every effect through pooled copies, to compare the looks
        EffectPlayer.BatchingEnabled = Argument("-perfNoBatching") == null;
        BalanceTelemetry.Enabled = false;
        SaveGame.SuppressWrites = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        if (QualitySettings.GetQualityLevel() != 2 && QualitySettings.names.Length > 2)
            QualitySettings.SetQualityLevel(2, true);
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.renderScale = 1f;
            urp.msaaSampleCount = 1;
        }
    }

    // ---- layout -------------------------------------------------------------------------------------------

    private class TowerPlan
    {
        public GameObject prefab;
        public int[] tiers;
        public Tower instance;
    }

    private static List<TowerPlan> PlanTowers(Run run, GameManager game, int spotCount)
    {
        List<GameObject> towerPrefabs = new List<GameObject>();
        foreach (GameObject building in game.Buildings)
        {
            if (building != null && building.GetComponent<Tower>() != null)
                towerPrefabs.Add(building);
        }

        List<TowerPlan> plans = new List<TowerPlan>();
        if (run.layout == "dispensers")
        {
            GameObject dispenser = towerPrefabs.Find(p => p.name == "Bullet Dispenser Tower");
            int count = run.maxTowers > 0 ? Mathf.Min(run.maxTowers, spotCount) : spotCount;
            for (int i = 0; i < count && dispenser != null; i++)
                plans.Add(new TowerPlan { prefab = dispenser, tiers = new[] { 3, 2, 0 } });
            return plans;
        }

        if (run.layout == "single")
        {
            GameObject prefab = towerPrefabs.Find(p => p.name == run.tower);
            if (prefab != null)
                plans.Add(new TowerPlan { prefab = prefab, tiers = HeaviestTiers(prefab) });
            return plans;
        }

        if (run.layout == "parity")
        {
            foreach (GameObject prefab in towerPrefabs)
                plans.Add(new TowerPlan { prefab = prefab, tiers = new[] { 2, 2, 0 } });
            return plans;
        }

        // mixed: every tower type in every "one path to the top, another to tier 2" combination,
        // interleaved by type so each type is spread along the lane
        List<List<int[]>> combos = new List<List<int[]>>();
        int longest = 0;
        foreach (GameObject prefab in towerPrefabs)
        {
            UpgradePath[] paths = prefab.GetComponent<UpgradeManager>().GetUpgradePaths();
            List<int[]> list = new List<int[]>();
            for (int a = 0; a < paths.Length; a++)
            {
                for (int b = 0; b < paths.Length; b++)
                {
                    if (a == b)
                        continue;
                    int[] tiers = new int[paths.Length];
                    tiers[a] = ModuleCount(paths[a]);
                    tiers[b] = Mathf.Min(2, ModuleCount(paths[b]));
                    list.Add(tiers);
                }
            }
            if (list.Count == 0)
                list.Add(new int[paths.Length]);
            combos.Add(list);
            longest = Mathf.Max(longest, list.Count);
        }

        for (int c = 0; c < longest; c++)
        {
            for (int t = 0; t < towerPrefabs.Count; t++)
            {
                if (c < combos[t].Count)
                    plans.Add(new TowerPlan { prefab = towerPrefabs[t], tiers = combos[t][c] });
            }
        }
        return plans;
    }

    // Path 1 to the top and path 2 to tier 2 (the Bullet Dispenser's bullet build); towers whose path 1 tier 3
    // swaps the action strategy still get a real, legal maximum this way
    private static int[] HeaviestTiers(GameObject prefab)
    {
        UpgradePath[] paths = prefab.GetComponent<UpgradeManager>().GetUpgradePaths();
        int[] tiers = new int[paths.Length];
        if (paths.Length > 0)
            tiers[0] = ModuleCount(paths[0]);
        if (paths.Length > 1)
            tiers[1] = Mathf.Min(2, ModuleCount(paths[1]));
        return tiers;
    }

    private static int ModuleCount(UpgradePath path)
    {
        return path != null && path.UpgradeModules != null ? Mathf.Min(3, path.UpgradeModules.Length) : 0;
    }

    // Block centres beside the lane, on the map's block grid, with the top face just below the passing enemies
    // (towers on top only see enemies above their face) and the block itself clear of the lane
    private static List<Vector3> FindBlockSpots(Spawner spawner)
    {
        List<Vector3> path = new List<Vector3>();
        List<Vector3> allPoints = new List<Vector3>();
        List<Vector3> spots = new List<Vector3>();
        HashSet<Vector3Int> taken = new HashSet<Vector3Int>();

        BuildingBlock anyBlock = FindAnyObjectByType<BuildingBlock>();
        Vector3 origin = anyBlock != null ? anyBlock.transform.position : Vector3.zero;
        Vector3 gridOffset = new Vector3(Frac(origin.x), Frac(origin.y), Frac(origin.z));

        Physics.SyncTransforms();
        int solidMask = ~((1 << 2) | (1 << 8));

        for (int lane = 0; lane < spawner.LaneCount; lane++)
        {
            spawner.GetLanePath(lane, path);
            allPoints.AddRange(path);
            allPoints.Add(new Vector3(float.NaN, 0f, 0f));   // lane separator
        }

        for (int lane = 0; lane < spawner.LaneCount; lane++)
        {
            spawner.GetLanePath(lane, path);
            for (int s = 0; s + 1 < path.Count; s++)
            {
                Vector3 a = path[s];
                Vector3 b = path[s + 1];
                Vector3 direction = b - a;
                float length = direction.magnitude;
                if (length < 0.001f)
                    continue;
                direction /= length;
                bool vertical = Mathf.Abs(direction.y) >= 0.9f;
                Vector3 side = vertical ? Vector3.right : Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 side2 = vertical ? Vector3.forward : side;

                for (float t = 0f; t <= length; t += 0.5f)
                {
                    Vector3 point = a + direction * t;
                    foreach (Vector3 offset in new[] { side, -side, side2, -side2 })
                    {
                        foreach (float distance in new[] { 1.1f })
                        {
                            Vector3 centre = Snap(point + offset * distance + Vector3.down * 0.8f, gridOffset);
                            Vector3Int key = Vector3Int.RoundToInt(centre * 2f);
                            if (taken.Contains(key))
                                continue;
                            float top = centre.y + 0.5f;
                            if (top > point.y - 0.05f || top < point.y - 1.6f)
                                continue;
                            float pathDistance = DistanceToPolyline(centre, allPoints);
                            if (pathDistance < 1.0f || pathDistance > 2.6f)
                                continue;
                            if (Physics.CheckBox(centre, Vector3.one * 0.45f, Quaternion.identity, solidMask, QueryTriggerInteraction.Ignore))
                                continue;
                            if (Physics.CheckBox(centre + Vector3.up, Vector3.one * 0.4f, Quaternion.identity, solidMask, QueryTriggerInteraction.Ignore))
                                continue;
                            // room above for the tower, and keep the top face away from the lane
                            if (DistanceToPolyline(centre + Vector3.up, allPoints) < 1.0f)
                                continue;
                            taken.Add(key);
                            spots.Add(centre);
                        }
                    }
                }
            }
        }

        // Neighbouring spots on the grid would overlap the blocks; keep spots at least one unit apart
        List<Vector3> spaced = new List<Vector3>();
        foreach (Vector3 spot in spots)
        {
            bool clear = true;
            foreach (Vector3 kept in spaced)
            {
                if ((kept - spot).sqrMagnitude < 0.99f)
                {
                    clear = false;
                    break;
                }
            }
            if (clear)
                spaced.Add(spot);
        }
        return spaced;
    }

    private static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }

    private static Vector3 Snap(Vector3 point, Vector3 offset)
    {
        return new Vector3(
            Mathf.Round(point.x - offset.x) + offset.x,
            Mathf.Round(point.y - offset.y) + offset.y,
            Mathf.Round(point.z - offset.z) + offset.z);
    }

    private static float DistanceToPolyline(Vector3 point, List<Vector3> polyline)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < polyline.Count; i++)
        {
            Vector3 a = polyline[i];
            Vector3 b = polyline[i + 1];
            if (float.IsNaN(a.x) || float.IsNaN(b.x))
                continue;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector3.Distance(point, a + ab * t));
        }
        return best;
    }

    private static Tower BuildOnBlock(GameObject blockPrefab, GameObject towerPrefab, Vector3 centre)
    {
        if (blockPrefab == null || towerPrefab == null)
            return null;

        GameObject block = Instantiate(blockPrefab, centre, Quaternion.identity);
        Physics.SyncTransforms();
        AnchorPoint top = null;
        foreach (AnchorPoint anchor in block.GetComponentsInChildren<AnchorPoint>())
        {
            if (Vector3.Dot(anchor.transform.forward, Vector3.up) > 0.9f)
                top = anchor;
        }
        if (top == null)
        {
            Debug.LogWarning("PERF layout: block at " + centre + " has no top anchor");
            return null;
        }

        GameObject built = Instantiate(towerPrefab, top.AnchorPointPosition.position, top.transform.rotation);
        Physics.SyncTransforms();
        return built.GetComponent<Tower>();
    }

    private static void ApplyTiers(Tower tower, int[] tiers)
    {
        UpgradePath[] paths = tower.UpgradeManager.GetUpgradePaths();
        for (int p = 0; p < paths.Length && p < tiers.Length; p++)
        {
            if (paths[p] == null || paths[p].UpgradeModules == null)
                continue;
            for (int t = 0; t < tiers[p] && t < paths[p].UpgradeModules.Length; t++)
            {
                UpgradeModule module = paths[p].UpgradeModules[t];
                if (module != null && !module.IsActive)
                    module.ApplyUpgrade(tower.gameObject);
            }
        }
        tower.UpgradeManager.CheckPathBlocking();
    }

    // Beams fire along their face: point each one along the nearest lane segment, like the rotation slider would
    private static void AimBeams(List<Tower> towers, Spawner spawner)
    {
        List<Vector3> path = new List<Vector3>();
        spawner.GetLanePath(0, path);
        foreach (Tower tower in towers)
        {
            if (!(tower.ActionStrategy is BeamTowerActionStrategy) || !tower.UseRotationSlider || tower.ShootingPoints.Length == 0)
                continue;

            // Across the lane at a slant (towards a point a little further along it), so the beam crosses the
            // enemies' path instead of running beside it
            Vector3 position = tower.ShootingPoints[0].transform.position;
            Vector3 wanted = Vector3.forward;
            float bestDistance = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 ab = path[i + 1] - path[i];
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(position - path[i], ab) / ab.sqrMagnitude) : 0f;
                Vector3 nearest = path[i] + ab * t;
                float distance = Vector3.Distance(position, nearest);
                if (distance < bestDistance && ab.sqrMagnitude > 0f)
                {
                    bestDistance = distance;
                    wanted = (nearest + ab.normalized * 1.5f - position).normalized;
                }
            }

            float best = 0f, bestDot = -2f;
            for (float value = 0f; value < 360f; value += 5f)
            {
                tower.RotateTower(value);
                float dot = Vector3.Dot(tower.ShootingPoints[0].transform.forward, wanted);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = value;
                }
            }
            tower.RotateTower(best);
        }
    }

    // ---- recording ----------------------------------------------------------------------------------------

    private void StartRecorders()
    {
        // -perfMarkers <text>: log the available profiler markers whose name contains the text
        string filter = Argument("-perfMarkers");
        if (!string.IsNullOrEmpty(filter))
        {
            List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    Debug.Log("PERF MARKER " + description.Category.Name + " / " + description.Name);
            }
        }

        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        markerRecorders = new ProfilerRecorder[MarkerNames.Length];
        markerTotals = new double[MarkerNames.Length];
        for (int i = 0; i < MarkerNames.Length; i++)
            markerRecorders[i] = new ProfilerRecorder(MarkerNames[i], 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
    }

    private void StopRecorders()
    {
        mainThreadRecorder.Dispose();
        gcRecorder.Dispose();
        foreach (ProfilerRecorder recorder in markerRecorders)
            recorder.Dispose();
    }

    // Runs first in the frame and records the previous frame
    private void Update()
    {
        if (!recording)
            return;
        if (skipNextSample)
        {
            skipNextSample = false;
            return;
        }

        FrameSample sample = new FrameSample
        {
            time = Time.realtimeSinceStartup,
            realMs = Time.unscaledDeltaTime * 1000f,
            mainMs = mainThreadRecorder.Valid ? mainThreadRecorder.LastValue / 1e6f : 0f,
            gameDt = Time.deltaTime,
            gcBytes = gcRecorder.Valid ? gcRecorder.LastValue : 0,
            enemies = Spawner.Instance != null ? Spawner.Instance.EnemiesAlive.Count : 0,
        };
        samples.Add(sample);
        for (int i = 0; i < markerRecorders.Length; i++)
        {
            if (markerRecorders[i].Valid)
                markerTotals[i] += markerRecorders[i].LastValue / 1e6;
        }
    }

    // Batch mode draws no Game view; render the main camera so rendering stays in the measurement
    private void LateUpdate()
    {
        if (!recording || !Application.isBatchMode || Camera.main == null)
            return;
        if (batchRenderTarget == null)
            batchRenderTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        Camera camera = Camera.main;
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = batchRenderTarget;
        camera.Render();
        camera.targetTexture = previous;
    }

    // Pooled and prewarmed enemies must look and collide like a fresh copy of the prefab
    private static int CountWrongEnemyScales(Spawner spawner)
    {
        int wrong = 0;
        foreach (GameObject enemy in spawner.EnemiesAlive)
        {
            if (enemy != null && Mathf.Abs(enemy.transform.lossyScale.x - 0.5f) > 0.001f)
                wrong++;
        }
        return wrong;
    }

    // Fixed 1/60 s frames from the start of the wave, so batched and pooled runs reach the same moment;
    // four close-ups 0.05 s apart around the tower nearest the enemies
    private IEnumerator CaptureSeries(Run run, GameManager game, Spawner spawner, List<Tower> towers)
    {
        Time.captureDeltaTime = 1f / 60f;
        spawner.StartNextWave();
        for (int frame = 0; frame < 360; frame++)
            yield return null;

        Tower focus = NearestTower(towers, spawner);
        string suffix = EffectPlayer.BatchingEnabled ? "batched" : "pooled";
        for (int shot = 0; shot < 4 && focus != null; shot++)
        {
            Capture(Path.Combine(outputDirectory, "visual-" + suffix + "-" + shot + ".png"), focus.transform.position);
            for (int frame = 0; frame < 3; frame++)
                yield return null;
        }
        Time.captureDeltaTime = 0f;
        game.ChangeGameSpeed(1f);
        Debug.Log("PERF VISUAL " + suffix + " captured around " + (focus != null ? focus.transform.position.ToString() : "nothing"));
    }

    private static Tower NearestTower(List<Tower> towers, Spawner spawner)
    {
        Tower best = null;
        float bestDistance = float.MaxValue;
        foreach (GameObject enemy in spawner.EnemiesAlive)
        {
            foreach (Tower tower in towers)
            {
                if (tower == null || enemy == null)
                    continue;
                float distance = (tower.transform.position - enemy.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tower;
                }
            }
        }
        return best;
    }

    // Main camera view, or a close-up looking at focus
    private static void Capture(string path, Vector3? focus)
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        Vector3 previousPosition = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        if (focus.HasValue)
        {
            Quaternion look = Quaternion.Euler(35f, 30f, 0f);
            camera.transform.SetPositionAndRotation(focus.Value - look * Vector3.forward * 5f, look);
        }
        const int width = 1600, height = 900;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        RenderTexture.active = previousActive;
        camera.targetTexture = previousTarget;
        File.WriteAllBytes(path, image.EncodeToPNG());
        Destroy(image);
        target.Release();
        Destroy(target);
        camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
    }

    private void Summarize(Run run, RunResult result, List<Tower> towers, int leaked)
    {
        // The first sample belongs to the frame before the wave started
        if (samples.Count > 0)
            samples.RemoveAt(0);

        result.frames = samples.Count;
        List<float> real = new List<float>(samples.Count);
        List<float> main = new List<float>(samples.Count);
        float realSeconds = 0f, gameSeconds = 0f;
        long totalGc = 0, steadyGc = 0;
        int steadyFrames = 0, steadyGcFrames = 0;
        float start = samples.Count > 0 ? samples[0].time : 0f;
        float end = samples.Count > 0 ? samples[samples.Count - 1].time : 0f;

        for (int i = 0; i < samples.Count; i++)
        {
            FrameSample sample = samples[i];
            real.Add(sample.realMs);
            main.Add(sample.mainMs);
            realSeconds += sample.realMs / 1000f;
            gameSeconds += sample.gameDt;
            totalGc += sample.gcBytes;
            // Steady state: one second after the wave started until one second before it ended
            if (sample.time - start > 1f && end - sample.time > 1f)
            {
                steadyFrames++;
                steadyGc += sample.gcBytes;
                if (sample.gcBytes > 0)
                    steadyGcFrames++;
            }
            frameCsv.Append(run.id).Append(',').Append(i).Append(',')
                .Append((sample.time - start).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.realMs.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.mainMs.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.gameDt.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.gcBytes).Append(',').Append(sample.enemies).Append('\n');
        }

        result.realSeconds = realSeconds;
        result.gameSeconds = gameSeconds;
        result.gameSpeedRatio = realSeconds > 0f ? gameSeconds / realSeconds : 0f;
        result.frameP50 = Percentile(real, 0.5f);
        result.frameP95 = Percentile(real, 0.95f);
        result.frameP99 = Percentile(real, 0.99f);
        result.frameMax = Percentile(real, 1f);
        result.mainP50 = Percentile(main, 0.5f);
        result.mainP99 = Percentile(main, 0.99f);
        result.mainMax = Percentile(main, 1f);
        result.totalGcBytes = totalGc;
        result.steadyGcBytes = steadyGc;
        result.steadyGcFrameShare = steadyFrames > 0 ? steadyGcFrames / (float)steadyFrames : 0f;
        result.steadyGcPerFrame = steadyFrames > 0 ? steadyGc / (float)steadyFrames : 0f;

        Dictionary<string, float> byType = new Dictionary<string, float>();
        foreach (Tower tower in towers)
        {
            if (tower == null)
                continue;
            result.damage += tower.DamageCount;
            result.kills += tower.Kills;
            string type = tower.name.Replace("(Clone)", "").Trim();
            byType.TryGetValue(type, out float sum);
            byType[type] = sum + tower.DamageCount;
        }
        foreach (KeyValuePair<string, float> entry in byType)
            result.damageByType.Add(entry.Key + "=" + entry.Value.ToString("F0", CultureInfo.InvariantCulture));
        result.leakedLives = leaked;

        result.projectilesRequested = PerfCounters.ProjectilesRequested;
        result.projectilesSpawned = PerfCounters.ProjectilesSpawned;
        result.pops = PerfCounters.Pops;
        result.deathEffectsRequested = PerfCounters.DeathEffectsRequested;
        result.deathEffectsPlayed = PerfCounters.DeathEffectsPlayed;
        result.effectsRequested = PerfCounters.EffectsRequested;
        result.effectsPlayed = PerfCounters.EffectsPlayed;

        for (int i = 0; i < MarkerNames.Length; i++)
        {
            if (markerRecorders[i].Valid && result.frames > 0)
                result.markers.Add(MarkerNames[i] + "=" + (markerTotals[i] / result.frames).ToString("F2", CultureInfo.InvariantCulture) + "ms");
            markerTotals[i] = 0;
        }

        // Frame-time and culling criteria (parity is checked once all runs are done)
        if (run.layout != "parity" && run.parityGroup == null)
        {
            Require(result, result.frameP99 <= FrameBudgetMs, "frame p99 " + result.frameP99.ToString("F1") + " ms > " + FrameBudgetMs.ToString("F1"));
            Require(result, result.frameMax <= HitchMs, "frame max " + result.frameMax.ToString("F1") + " ms > " + HitchMs);
            // TMP copies every changed text into a new string in the Editor (not in builds), so the Editor
            // allows a little garbage per frame; builds must not allocate in steady state
            float allowedGcPerFrame = Application.isEditor ? MaxEditorGcBytesPerFrame : 0f;
            Require(result, result.steadyGcPerFrame <= allowedGcPerFrame,
                "GC " + result.steadyGcPerFrame.ToString("F0") + " B per steady frame > " + allowedGcPerFrame.ToString("F0"));
            if (run.speed > 1f)
                Require(result, result.gameSpeedRatio >= run.speed * 0.98f, "game ran at " + result.gameSpeedRatio.ToString("F2") + "x instead of " + run.speed + "x");
        }
        Require(result, !result.timedOut, "wave did not end within the time limit");
        Require(result, result.wrongEnemyScale == 0, result.wrongEnemyScale + " enemies don't have the prefab's scale");
        Require(result, result.projectilesSpawned == result.projectilesRequested, "projectiles dropped: " + (result.projectilesRequested - result.projectilesSpawned) + " of " + result.projectilesRequested);
        Require(result, result.deathEffectsPlayed == result.deathEffectsRequested, "death animations skipped: " + (result.deathEffectsRequested - result.deathEffectsPlayed) + " of " + result.deathEffectsRequested);
        Require(result, result.effectsPlayed == result.effectsRequested, "effects skipped: " + (result.effectsRequested - result.effectsPlayed) + " of " + result.effectsRequested);

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "PERF RESULT {0}: towers {1}, frames {2}, frame p50/p95/p99/max {3:F1}/{4:F1}/{5:F1}/{6:F1} ms, main p50/p99 {7:F1}/{8:F1} ms, " +
            "speed {9:F2}x, GC steady {10:F1}% frames ({11} B, {24:F0} B/frame), damage {12:F0}, kills {13}, leaked {14}, pops {15}, " +
            "projectiles {16}/{17}, death fx {18}/{19}, fx {20}/{21}, markers [{22}], by type [{23}]",
            result.id, result.towers, result.frames, result.frameP50, result.frameP95, result.frameP99, result.frameMax,
            result.mainP50, result.mainP99, result.gameSpeedRatio, result.steadyGcFrameShare * 100f, result.steadyGcBytes,
            result.damage, result.kills, result.leakedLives, result.pops,
            result.projectilesSpawned, result.projectilesRequested, result.deathEffectsPlayed, result.deathEffectsRequested,
            result.effectsPlayed, result.effectsRequested, string.Join(", ", result.markers), string.Join(", ", result.damageByType),
            result.steadyGcPerFrame));
    }

    private static void Require(RunResult result, bool ok, string what)
    {
        if (!ok)
            result.failures.Add(result.id + ": " + what);
    }

    private static float Percentile(List<float> values, float percentile)
    {
        if (values.Count == 0)
            return 0f;
        List<float> sorted = new List<float>(values);
        sorted.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(percentile * sorted.Count) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    // Damage, kills and leaks of every run in a parity group against the group's 1x run
    private void EvaluateParity()
    {
        Dictionary<string, RunResult> baseline = new Dictionary<string, RunResult>();
        for (int i = 0; i < runs.Count && i < suite.runs.Count; i++)
        {
            if (runs[i].parityGroup != null && Mathf.Approximately(runs[i].speed, 1f))
                baseline[runs[i].parityGroup] = suite.runs[i];
        }

        for (int i = 0; i < runs.Count && i < suite.runs.Count; i++)
        {
            Run run = runs[i];
            RunResult result = suite.runs[i];
            if (run.parityGroup == null || Mathf.Approximately(run.speed, 1f) || !baseline.TryGetValue(run.parityGroup, out RunResult reference))
                continue;

            bool info = run.informational;
            CompareParity(result, "damage", result.damage, reference.damage, ParityTolerance, info);
            if (run.layout != "single")
                CompareParity(result, "leaked lives", result.leakedLives, reference.leakedLives, ParityTolerance, info);
            float referenceTotal = Mathf.Max(1f, reference.damage);
            foreach (string entry in reference.damageByType)
            {
                string[] parts = entry.Split('=');
                float referenceDamage = float.Parse(parts[1], CultureInfo.InvariantCulture);
                if (referenceDamage < referenceTotal * 0.05f)
                    continue;
                string match = result.damageByType.Find(e => e.StartsWith(parts[0] + "=", StringComparison.Ordinal));
                float damage = match != null ? float.Parse(match.Split('=')[1], CultureInfo.InvariantCulture) : 0f;
                CompareParity(result, parts[0] + " damage", damage, referenceDamage, ParityTypeTolerance, info);
            }
            if (reference.leakedLives == 0 && run.layout != "single")
                Debug.LogWarning("PERF " + run.parityGroup + ": the 1x run leaked nothing, so its parity check only compares damage");
        }
    }

    private static void CompareParity(RunResult result, string what, float value, float reference, float tolerance, bool informational)
    {
        float scale = Mathf.Max(Mathf.Abs(reference), 1f);
        float deviation = Mathf.Abs(value - reference) / scale;
        string line = string.Format(CultureInfo.InvariantCulture, "{0} {1}: {2:F0} vs {3:F0} at 1x ({4:+0.0;-0.0}%)",
            result.id, what, value, reference, (value - reference) / scale * 100f);
        Debug.Log("PERF PARITY " + line);
        if (deviation > tolerance && Mathf.Abs(value - reference) > 2f && !informational)
            result.failures.Add(line + " exceeds " + (tolerance * 100f) + "%");
    }

    private void WriteResults()
    {
        foreach (RunResult result in suite.runs)
        {
            foreach (string failure in result.failures)
            {
                suite.failures.Add(failure);
                Debug.Log("PERF FAIL " + failure);
            }
            if (result.failures.Count == 0)
                Debug.Log("PERF PASS " + result.id);
        }

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string baseName = Path.Combine(outputDirectory, "perf-" + suiteName.Replace(',', '_') + "-" + stamp);
        File.WriteAllText(baseName + ".csv", frameCsv.ToString());
        SummaryPath = baseName + ".json";
        File.WriteAllText(SummaryPath, JsonUtility.ToJson(suite, true));
        Debug.Log("PERF finished, " + suite.failures.Count + " failed checks, results in " + SummaryPath);

        if (batchRenderTarget != null)
        {
            batchRenderTarget.Release();
            Destroy(batchRenderTarget);
        }

        FailureCount = suite.failures.Count;
        Finished = true;
        if (!Application.isEditor)
            Application.Quit(Mathf.Min(FailureCount, 100));
    }
}
#endif
