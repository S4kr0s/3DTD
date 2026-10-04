using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-mode entry points for the performance benchmark (project closed in the Editor, WITH graphics):
//
// Unity -batchmode -projectPath ... -executeMethod PerfBenchmark.Run -perfSuite core|full|<ids> [-perfOut <dir>]
//   Opens Beginner Level 01 and enters play mode; PerfScenario (runtime) reads -perfSuite, plays every run
//   and writes tasks/perf/perf-*.csv/.json. Exits with the number of failed checks (PERF PASS|FAIL lines).
//   The player's progress and savegame are backed up and restored.
//
// Unity -batchmode -projectPath ... -executeMethod PerfBenchmark.BuildPlayer
//   Development build of the game to Builds/PerfPlayer/3DTD.app. Run the same suite in the real player with
//   3DTD.app/Contents/MacOS/3DTD -perfSuite core -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
[InitializeOnLoad]
public static class PerfBenchmark
{
    private const string ActiveKey = "PerfBenchmark.Active";
    private const string StartedKey = "PerfBenchmark.Started";
    private const string DoneKey = "PerfBenchmark.Done";
    private const string FailedKey = "PerfBenchmark.Failed";
    private const string DeadlineKey = "PerfBenchmark.Deadline";
    private const string ProgressBackupKey = "PerfBenchmark.ProgressBackup";
    private const string ProgressPrefsKey = "3DTD.Progress";
    private const string FirstScene = "Assets/Scenes/Beginner Level 01.unity";

    static PerfBenchmark()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        BackupPlayerData();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(StartedKey, false);
        SessionState.SetBool(DoneKey, false);
        SessionState.SetInt(FailedKey, 0);
        // A whole suite takes a few minutes per run; give up after an hour
        SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 3600f);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(DoneKey, false))
            {
                Finish();
                return;
            }
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            return;
        }

        bool timedOut = EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, float.MaxValue);
        if (!PerfScenario.Finished && !timedOut)
            return;

        if (timedOut)
            Debug.Log("PERF FAIL benchmark did not finish within the time limit");
        SessionState.SetInt(FailedKey, timedOut ? 1000 : PerfScenario.FailureCount);
        SessionState.SetBool(DoneKey, true);
        EditorApplication.isPlaying = false;
    }

    private static void Finish()
    {
        RestorePlayerData();
        int failed = SessionState.GetInt(FailedKey, 0);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        Debug.Log("PERF benchmark finished, " + failed + " failed checks");
        if (Application.isBatchMode)
            EditorApplication.Exit(Math.Min(failed, 100));
    }

    public static void BuildPlayer()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/PerfPlayer/3DTD.app"));
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Array.ConvertAll(EditorBuildSettings.scenes, scene => scene.path),
            locationPathName = output,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("PERF build " + report.summary.result + " -> " + output + " (" + report.summary.totalTime + ")");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ---- player data -----------------------------------------------------------------------------------

    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    private static string SaveBackupPath => Path.Combine(Application.persistentDataPath, "savegame.perf-backup.json");

    private static void BackupPlayerData()
    {
        SessionState.SetString(ProgressBackupKey, PlayerPrefs.HasKey(ProgressPrefsKey) ? "1" + PlayerPrefs.GetString(ProgressPrefsKey) : "0");
        if (File.Exists(SavePath))
            File.Copy(SavePath, SaveBackupPath, true);
        else if (File.Exists(SaveBackupPath))
            File.Delete(SaveBackupPath);
    }

    private static void RestorePlayerData()
    {
        string backup = SessionState.GetString(ProgressBackupKey, null);
        if (backup == null)
            return;
        if (backup.StartsWith("1"))
            PlayerPrefs.SetString(ProgressPrefsKey, backup.Substring(1));
        else
            PlayerPrefs.DeleteKey(ProgressPrefsKey);
        PlayerPrefs.Save();
        if (File.Exists(SaveBackupPath))
        {
            File.Copy(SaveBackupPath, SavePath, true);
            File.Delete(SaveBackupPath);
        }
        else if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
        SessionState.EraseString(ProgressBackupKey);
    }
}
