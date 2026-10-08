using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Play-mode smoke test of the beginner levels (batch mode WITH a graphics device, project closed in the Editor):
//
// Unity -batchmode -projectPath ... -executeMethod LevelPlaytest.Run [-captureOut <dir>]
//
// Per level it plays wave 1 undefended and checks that it ends with lives lost (every enemy walked the whole
// lane into the End trigger), saving world screenshots from three angles while it is on the track. Then it
// builds the level's featured tower on a starter block through AnchorPoint's build path (beams aimed along
// their lane like the rotation slider would), plays wave 2 and checks that the tower dealt damage.
// Log lines: LEVELTEST PASS|FAIL.
// Exits with the number of failed checks. The player's progress and savegame are backed up and restored.
[InitializeOnLoad]
public static class LevelPlaytest
{
    private const string ActiveKey = "LevelPlaytest.Active";
    private const string PhaseKey = "LevelPlaytest.Phase";
    private const string StartedKey = "LevelPlaytest.Started";
    private const string FailedKey = "LevelPlaytest.Failed";
    private const string OutKey = "LevelPlaytest.Out";
    private const string ProgressBackupKey = "LevelPlaytest.ProgressBackup";
    private const string ProgressPrefsKey = "3DTD.Progress";

    private class Case
    {
        public string Scene;
        public string Tower;          // prefab name in GameManager's palette
        public Vector3 Block;         // starter block the tower goes on
        public Vector3 Face;          // which face (outward normal)
        public Vector3 BeamDirection; // for beams: the direction the slider should point the beam
    }

    private static readonly Case[] Cases =
    {
        new Case { Scene = "Beginner Level 01", Tower = "Default Tower", Block = new Vector3(13, 1, -3.5f), Face = Vector3.up },
        new Case { Scene = "Beginner Level 02", Tower = "Sniper Tower", Block = new Vector3(9, 2, 6), Face = Vector3.up },
        new Case { Scene = "Beginner Level 03", Tower = "Bomb Tower", Block = new Vector3(4, 2.5f, 2.5f), Face = Vector3.back },
        new Case { Scene = "Beginner Level 04", Tower = "Beam Tower", Block = new Vector3(14, 6, 1), Face = Vector3.down, BeamDirection = Vector3.left },
        new Case { Scene = "Beginner Level 05", Tower = "Hangar Tower", Block = new Vector3(0, 4, 0), Face = Vector3.up },
    };

    private static IEnumerator scenario;
    private static int waitUntilFrame;

    static LevelPlaytest()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tasks/levels/screenshots"));
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-captureOut")
                output = args[i + 1];
        }
        Directory.CreateDirectory(output);
        SessionState.SetString(OutKey, output);
        BackupPlayerData();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetInt(FailedKey, 0);
        SessionState.SetBool(StartedKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (phase >= Cases.Length)
            {
                Finish();
                return;
            }
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                SaveGame.Delete();
                EditorSceneManager.OpenScene("Assets/Scenes/" + Cases[phase].Scene + ".unity", OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            return;
        }

        if (scenario == null)
        {
            BalanceTelemetry.Enabled = false;
            scenario = LevelScenario(Cases[phase]);
            waitUntilFrame = Time.frameCount + 60;
        }
        if (Time.frameCount < waitUntilFrame)
            return;

        try
        {
            if (scenario.MoveNext())
            {
                waitUntilFrame = Time.frameCount + (scenario.Current is int frames ? frames : 1);
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Check(false, Cases[phase].Scene + ": scenario threw " + exception.Message);
        }

        scenario = null;
        SessionState.SetInt(PhaseKey, phase + 1);
        SessionState.SetBool(StartedKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void Finish()
    {
        RestorePlayerData();
        int failed = SessionState.GetInt(FailedKey, 0);
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        Debug.Log("LEVELTEST finished, " + failed + " failed checks");
        if (Application.isBatchMode)
            EditorApplication.Exit(failed);
    }

    private static void Check(bool ok, string what)
    {
        Debug.Log((ok ? "LEVELTEST PASS " : "LEVELTEST FAIL ") + what);
        if (!ok)
            SessionState.SetInt(FailedKey, SessionState.GetInt(FailedKey, 0) + 1);
    }

    // ---- scenario --------------------------------------------------------------------------------------

    private static IEnumerator LevelScenario(Case c)
    {
        GameManager game = GameManager.Instance;
        Check(game != null && Spawner.Instance != null && End.Instance != null, c.Scene + ": GameManager, Spawner and End are up");
        if (game == null || Spawner.Instance == null)
            yield break;
        Check(Spawner.Instance.LaneCount == 1, c.Scene + ": one lane (" + Spawner.Instance.LaneCount + ")");

        // Wave 1 without towers: every enemy has to walk the whole lane into the End trigger
        int livesBefore = game.Lives;
        Spawner.Instance.StartNextWave();
        // let the first enemies walk most of the track, then photograph it
        float until = Time.time + 9f;
        while (Time.time < until)
            yield return 1;
        string output = SessionState.GetString(OutKey, ".");
        string prefix = c.Scene.Replace("Beginner Level ", "L");
        Capture(Path.Combine(output, prefix + "_a_start_view.png"), Quaternion.Euler(45f, 0f, 0f), StartDistance());
        yield return 1;
        Capture(Path.Combine(output, prefix + "_b_oblique.png"), Quaternion.Euler(32f, 135f, 0f), 23f);
        yield return 1;
        Capture(Path.Combine(output, prefix + "_c_below.png"), Quaternion.Euler(-28f, 220f, 0f), 21f);
        yield return 1;

        game.ChangeGameSpeed(5f);
        float timeout = Time.realtimeSinceStartup + 120f;
        while (Spawner.Instance.IsWaveActive && Time.realtimeSinceStartup < timeout)
            yield return 10;
        Check(!Spawner.Instance.IsWaveActive, c.Scene + ": wave 1 ended (round " + game.Round + ")");
        Check(game.Lives < livesBefore, c.Scene + ": undefended, the enemies reached the exit (lives " + livesBefore + " -> " + game.Lives + ")");

        // Wave 2 with the level's featured tower
        game.ChangeGameSpeed(1f);
        Tower tower = Build(c);
        Check(tower != null, c.Scene + ": built " + c.Tower + " on the " + c.Face + " face of the block at " + c.Block);
        if (tower != null && c.BeamDirection != Vector3.zero)
            AimBeam(tower, c.BeamDirection);
        Spawner.Instance.StartNextWave();
        game.ChangeGameSpeed(5f);
        timeout = Time.realtimeSinceStartup + 120f;
        while (Spawner.Instance.IsWaveActive && Time.realtimeSinceStartup < timeout)
            yield return 10;
        Check(!Spawner.Instance.IsWaveActive, c.Scene + ": wave 2 ended (round " + game.Round + ")");
        game.ChangeGameSpeed(1f);
        if (tower != null)
            Check(tower.DamageCount > 0, c.Scene + ": " + c.Tower + " dealt " + tower.DamageCount + " damage, " + tower.Kills + " kills");
    }

    private static float StartDistance()
    {
        OrbitCamera orbit = Camera.main.GetComponent<OrbitCamera>();
        FieldInfo distance = typeof(OrbitCamera).GetField("distance", BindingFlags.Instance | BindingFlags.NonPublic);
        return orbit != null && distance != null ? (float)distance.GetValue(orbit) : 20f;
    }

    // Builds through AnchorPoint's own build path, like a click on the face
    private static Tower Build(Case c)
    {
        GameObject prefab = GameManager.Instance.Buildings.Find(b => b != null && b.name == c.Tower);
        if (prefab == null)
            return null;
        AnchorPoint anchor = null;
        foreach (AnchorPoint candidate in Object.FindObjectsByType<AnchorPoint>(FindObjectsInactive.Exclude))
        {
            Transform block = candidate.transform.parent;
            if (block != null && (block.position - c.Block).sqrMagnitude < 0.01f && Vector3.Dot(candidate.transform.forward, c.Face) > 0.9f)
                anchor = candidate;
        }
        if (anchor == null || !anchor.IsFree())
            return null;
        MethodInfo buildHere = typeof(AnchorPoint).GetMethod("BuildHere", BindingFlags.Instance | BindingFlags.NonPublic);
        HashSet<Tower> before = new HashSet<Tower>(Object.FindObjectsByType<Tower>(FindObjectsInactive.Exclude));
        buildHere.Invoke(anchor, new object[] { prefab });
        foreach (Tower tower in Object.FindObjectsByType<Tower>(FindObjectsInactive.Exclude))
        {
            if (!before.Contains(tower))
                return tower;
        }
        return null;
    }

    // What the rotation slider does: pick the slider value whose beam points closest to the wanted direction
    private static void AimBeam(Tower tower, Vector3 direction)
    {
        float best = 0f, bestDot = -2f;
        for (float value = 0f; value < 360f; value += 5f)
        {
            tower.RotateTower(value);
            float dot = Vector3.Dot(tower.ShootingPoints[0].transform.forward, direction);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = value;
            }
        }
        tower.RotateTower(best);
        Transform origin = tower.ShootingPoints[0].transform;
        Debug.Log("LevelPlaytest: beam at " + origin.position.ToString("F2") + " pointing " + origin.forward.ToString("F2") + " (slider " + best + ")");
    }

    // World render from a pose around the level's camera anchor (OrbitCamera's start view is pitch 45, yaw 0)
    private static void Capture(string path, Quaternion rotation, float distance)
    {
        Camera camera = Camera.main;
        Transform anchor = camera.transform.parent;
        Vector3 center = anchor != null ? anchor.position : Vector3.zero;
        List<Behaviour> controllers = new List<Behaviour>();
        foreach (MonoBehaviour behaviour in camera.GetComponents<MonoBehaviour>())
        {
            if (behaviour is OrbitCamera || behaviour is CameraController || behaviour is UnityTemplateProjects.SimpleCameraController)
            {
                if (behaviour.enabled)
                    controllers.Add(behaviour);
                behaviour.enabled = false;
            }
        }
        Vector3 position = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);

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
        Object.DestroyImmediate(image);
        target.Release();
        Object.DestroyImmediate(target);

        camera.transform.SetPositionAndRotation(position, previousRotation);
        foreach (Behaviour behaviour in controllers)
            behaviour.enabled = true;
        Debug.Log("LevelPlaytest: wrote " + path);
    }

    // ---- player data -----------------------------------------------------------------------------------

    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    private static string SaveBackupPath => Path.Combine(Application.persistentDataPath, "savegame.leveltest-backup.json");

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
