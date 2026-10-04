using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dev-only balance telemetry: one CSV row per tower type and wave with damage dealt, money invested,
// lives, money and average FPS. Tools/BalanceDashboard can import the files ("Telemetry" tab) to compare
// the engine's models with real play. Attaches itself to every game scene in the Editor and in
// development builds; set Enabled = false to turn it off.
public class BalanceTelemetry : MonoBehaviour
{
    public static bool Enabled = true;

    private struct TypeSnapshot
    {
        public int Count;
        public float Damage;
        public int Invested;
    }

    private Spawner subscribedSpawner;
    private string filePath;
    private Dictionary<string, TypeSnapshot> atWaveStart = new Dictionary<string, TypeSnapshot>();
    private float waveStartTime;
    private float waveStartUnscaled;
    private int waveStartFrame;
    private int livesAtStart;
    private int moneyAtStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += (scene, mode) => Attach();
        Attach();
    }

    private static void Attach()
    {
        if (!Enabled || !(Application.isEditor || Debug.isDebugBuild))
            return;
        if (FindAnyObjectByType<BalanceTelemetry>() != null)
            return;

        new GameObject("BalanceTelemetry").AddComponent<BalanceTelemetry>();
    }

    private void Update()
    {
        // Spawner and GameManager set their singletons in Start/Awake; subscribe once both exist
        if (subscribedSpawner == null && Spawner.Instance != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.IsMainMenu)
            {
                Destroy(gameObject);
                return;
            }

            subscribedSpawner = Spawner.Instance;
            subscribedSpawner.OnWaveStarted += HandleWaveStarted;
            subscribedSpawner.OnWaveEnded += HandleWaveEnded;
        }
    }

    private void OnDestroy()
    {
        if (subscribedSpawner != null)
        {
            subscribedSpawner.OnWaveStarted -= HandleWaveStarted;
            subscribedSpawner.OnWaveEnded -= HandleWaveEnded;
        }
    }

    private static Dictionary<string, TypeSnapshot> Snapshot()
    {
        var result = new Dictionary<string, TypeSnapshot>();
        foreach (Tower tower in FindObjectsByType<Tower>())
        {
            result.TryGetValue(tower.DisplayName, out TypeSnapshot s);
            s.Count++;
            s.Damage += tower.DamageCount;
            s.Invested += tower.Invested;
            result[tower.DisplayName] = s;
        }
        return result;
    }

    private void HandleWaveStarted(int round)
    {
        if (!Enabled)
            return;
        atWaveStart = Snapshot();
        waveStartTime = Time.time;
        waveStartUnscaled = Time.unscaledTime;
        waveStartFrame = Time.frameCount;
        livesAtStart = GameManager.Instance.Lives;
        moneyAtStart = GameManager.Instance.Money;
    }

    private void HandleWaveEnded(int round)
    {
        if (!Enabled)
            return;
        if (filePath == null)
            filePath = CreateFile();

        float gameSeconds = Time.time - waveStartTime;
        float realSeconds = Mathf.Max(0.001f, Time.unscaledTime - waveStartUnscaled);
        float fps = (Time.frameCount - waveStartFrame) / realSeconds;
        GameManager gm = GameManager.Instance;
        int alive = Spawner.Instance != null ? Spawner.Instance.EnemiesAlive.Count : 0;

        var sb = new StringBuilder();
        foreach (var entry in Snapshot())
        {
            atWaveStart.TryGetValue(entry.Key, out TypeSnapshot before);
            sb.AppendLine(string.Join(",",
                round.ToString(CultureInfo.InvariantCulture),
                Quote(entry.Key),
                entry.Value.Count.ToString(CultureInfo.InvariantCulture),
                (entry.Value.Damage - before.Damage).ToString("0.##", CultureInfo.InvariantCulture),
                entry.Value.Invested.ToString(CultureInfo.InvariantCulture),
                gameSeconds.ToString("0.##", CultureInfo.InvariantCulture),
                fps.ToString("0.#", CultureInfo.InvariantCulture),
                Time.timeScale.ToString("0.##", CultureInfo.InvariantCulture),
                livesAtStart.ToString(CultureInfo.InvariantCulture),
                gm.Lives.ToString(CultureInfo.InvariantCulture),
                moneyAtStart.ToString(CultureInfo.InvariantCulture),
                gm.Money.ToString(CultureInfo.InvariantCulture),
                alive.ToString(CultureInfo.InvariantCulture)));
        }
        File.AppendAllText(filePath, sb.ToString());
    }

    private static string Quote(string s)
    {
        return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    }

    private string CreateFile()
    {
        string dir = Path.Combine(Application.persistentDataPath, "telemetry");
        Directory.CreateDirectory(dir);
        string difficulty = GameManager.Instance != null ? GameManager.Instance.Difficulty.ToString() : "Unknown";
        string name = string.Format("{0}_{1}_{2:yyyyMMdd-HHmmss}.csv", SceneManager.GetActiveScene().name.Replace(' ', '_'), difficulty, DateTime.Now);
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, "round,tower,count,damage,invested,wave_seconds,fps,game_speed,lives_start,lives_end,money_start,money_end,enemies_left\n");
        Debug.Log("[BalanceTelemetry] Recording to " + path);
        return path;
    }
}
