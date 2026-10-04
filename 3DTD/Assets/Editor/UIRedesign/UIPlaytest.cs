using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

// End-to-end play test of the gameplay flows behind the redesigned UI, run in batch mode:
//   Unity -batchmode -projectPath ... -executeMethod UIPlaytest.Run
// Builds through an anchor, buys an upgrade from the tower panel, changes targeting and speed, pauses,
// plays a wave (autosave), sells, then restores the autosave through the main menu's Continue button.
// Every check logs "UIPLAYTEST PASS|FAIL"; the exit code is the number of failures. The player's progress
// and savegame are backed up and restored.
[InitializeOnLoad]
public static class UIPlaytest
{
    private const string ActiveKey = "UIPlaytest.Active";
    private const string PhaseKey = "UIPlaytest.Phase";
    private const string StartedKey = "UIPlaytest.Started";
    private const string FailuresKey = "UIPlaytest.Failures";
    private const string ExpectKey = "UIPlaytest.Expect";
    private const string ProgressKey = "3DTD.Progress";
    private const string BackupKey = "UIPlaytest.ProgressBackup";

    private static readonly string[] Scenes = { "Assets/Scenes/Beginner Level 01.unity", "Assets/Scenes/MainMenuLevel.unity" };
    private static IEnumerator scenario;
    private static float resumeAt;

    static UIPlaytest()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetBool(StartedKey, false);
        SessionState.SetInt(FailuresKey, 0);
        BackupPlayerData();
        // Two owned meta upgrades: +5 % damage and +50 start scrap
        PlayerPrefs.SetString(ProgressKey, "{\"levels\":[],\"research\":0,\"metaNodes\":[\"t-hardened\",\"e-salvage\"]}");
        PlayerPrefs.Save();
        SaveGame.Delete();
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
            if (phase >= Scenes.Length)
            {
                Finish();
                return;
            }
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                EditorSceneManager.OpenScene(Scenes[phase], OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            return;
        }

        if (scenario == null)
        {
            scenario = phase == 0 ? LevelScenario() : ContinueScenario();
            resumeAt = Time.realtimeSinceStartup + 1.5f;
        }
        if (Time.realtimeSinceStartup < resumeAt)
            return;

        try
        {
            if (scenario.MoveNext())
            {
                resumeAt = Time.realtimeSinceStartup + (scenario.Current is float seconds ? seconds : 0.1f);
                return;
            }
        }
        catch (Exception exception)
        {
            Check(false, "scenario threw " + exception);
        }

        scenario = null;
        SessionState.SetInt(PhaseKey, phase + 1);
        SessionState.SetBool(StartedKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool condition, string what)
    {
        Debug.Log("UIPLAYTEST " + (condition ? "PASS " : "FAIL ") + what);
        if (!condition)
            SessionState.SetInt(FailuresKey, SessionState.GetInt(FailuresKey, 0) + 1);
    }

    private static void Finish()
    {
        int failures = SessionState.GetInt(FailuresKey, 0);
        RestorePlayerData();
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        Debug.Log("UIPLAYTEST DONE failures=" + failures);
        if (Application.isBatchMode)
            EditorApplication.Exit(failures);
    }

    // ---- phase 1: a game -------------------------------------------------------------------------------

    private static IEnumerator LevelScenario()
    {
        GameManager game = GameManager.Instance;
        Check(game.Money == game.Profile.startMoney + 50, "meta upgrade adds 50 start scrap (money " + game.Money + ")");

        // Build the laser tower (palette 1) through an anchor
        game.Money = 2000;
        BuildingManager.Instance.Select(1);
        Check(BuildingManager.Instance.IsPlacing, "selecting a tile starts placement");
        AnchorPoint pad = UIEditorDrive.PickPad(new Vector2(0.5f, 0.5f));
        // Clicks on the map must reach the anchors: the camera's PhysicsRaycaster hits must not count as UI
        Vector3 padScreen = Camera.main.WorldToScreenPoint(pad.transform.position);
        Check(!UIPointer.IsOverUI(padScreen), "a pad on the map is not under the UI (" + padScreen + ")");
        RectTransform nextWaveRect = (RectTransform)UIEditorDrive.FindInactive<BevelButton>("Key", "Next Wave").transform;
        Vector3[] corners = new Vector3[4];
        nextWaveRect.GetWorldCorners(corners);
        Check(UIPointer.IsOverUI((corners[0] + corners[2]) * 0.5f), "the Next wave button is UI");
        int price = game.Price(game.Buildings[1].GetComponent<Building>().Cost);
        pad.SendMessage("OnMouseDown");
        yield return 0.3f;
        Tower tower = null;
        foreach (Tower candidate in Object.FindObjectsByType<Tower>(FindObjectsInactive.Exclude))
        {
            if (candidate.PlacedByPlayer)
                tower = candidate;
        }
        Check(tower != null && tower.PaletteIndex == 1, "anchor builds the selected tower and records its palette index");
        Check(game.Money == 2000 - price && tower != null && tower.Invested == price, "build charges the price and records the investment");
        if (tower == null)
            yield break;
        float baseDamage = tower.StatsManager.Config.Damage;
        Check(Mathf.Approximately(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), baseDamage * 1.05f), "meta upgrade gives towers +5 % damage");
        Check(!pad.IsFree(), "the pad is taken after building");
        BuildingManager.Instance.CancelPlacement();
        Check(!BuildingManager.Instance.IsPlacing, "placement can be cancelled");

        // Tower panel
        SelectionManager.CurrentlySelected = tower.GetComponent<Selectable>();
        yield return 0.3f;
        Check(UpgradePanelManager.Instance.IsOpen, "selecting a tower opens the panel");
        int cells = 0;
        int modules = 0;
        foreach (UpgradePath path in tower.UpgradeManager.GetUpgradePaths())
            modules += path != null && path.UpgradeModules != null ? path.UpgradeModules.Length : 0;
        foreach (UpgradeCell cell in Object.FindObjectsByType<UpgradeCell>(FindObjectsInactive.Exclude))
        {
            cells++;
            if (cell.CurrentState == UpgradeCell.State.Later)
                Check(!cell.Button.interactable, "later tier " + cell.Module.Name + " can't be bought yet");
        }
        Check(cells == modules && modules > 3, "the tower panel lists all " + modules + " upgrades (" + cells + " cells)");
        UpgradeCell firstCell = Object.FindAnyObjectByType<UpgradeCell>();
        firstCell.GetComponent<TooltipTrigger>().ShowNow();
        TooltipService tooltip = TooltipService.For(firstCell.transform);
        TMPro.TMP_Text tooltipTitle = tooltip.transform.Find("Tooltip Safe Area/Tooltip Layer/Tooltip/Title").GetComponent<TMPro.TMP_Text>();
        Check(tooltipTitle.gameObject.activeInHierarchy && tooltipTitle.text == firstCell.Module.Name, "hovering an upgrade shows its name in a tooltip");
        tooltip.Hide();

        UpgradeRow row = Object.FindAnyObjectByType<UpgradeRow>();
        UpgradeModule module = row.Module;
        int money = game.Money;
        UIEditorDrive.Click(row.Button.gameObject);
        yield return 0.2f;
        Check(module != null && module.IsActive && game.Money == money - game.Price(module.Price), "clicking an upgrade row buys it");
        Check(tower.Tier == 2, "tier pips follow the bought upgrade (tier " + tower.Tier + ")");

        Stepper stepper = Object.FindAnyObjectByType<Stepper>();
        UIEditorDrive.Click(UIEditorDrive.FindInactive<BevelButton>("Next", "Targeting"));
        yield return 0.1f;
        Check(tower.TargetBehaviour == TargetBehaviour.LAST && stepper.Index == 1, "stepper switches targeting to Last");

        // Speed and pause
        SegmentedControl speed = GameObject.Find("Speed").GetComponentInChildren<SegmentedControl>();
        UIEditorDrive.Click(speed.Items[1].gameObject);
        yield return 0.1f;
        Check(Mathf.Approximately(game.GameSpeed, 3f), "speed x3");
        PauseMenu.Instance.ToggleMenu();
        yield return 0.1f;
        Check(PauseMenu.Instance.IsOpen && Mathf.Approximately(Time.timeScale, 0f), "pause stops time");
        PauseMenu.Instance.ToggleMenu();
        yield return 0.1f;
        Check(Mathf.Approximately(game.GameSpeed, 3f), "resume restores x3 instead of x1");

        // A wave: the CTA is disabled while it runs, the autosave is written when it ends
        SelectionManager.CurrentlySelected = null;
        BevelButton nextWave = UIEditorDrive.FindInactive<BevelButton>("Key", "Next Wave").GetComponent<BevelButton>();
        UIEditorDrive.Click(nextWave.gameObject);
        yield return 0.2f;
        Check(Spawner.Instance.IsWaveActive && !nextWave.interactable, "Next wave starts the wave and disables the CTA");
        game.ChangeGameSpeed(10f);
        float timeout = Time.realtimeSinceStartup + 120f;
        while (Spawner.Instance.IsWaveActive && Time.realtimeSinceStartup < timeout)
            yield return 0.5f;
        Check(!Spawner.Instance.IsWaveActive && nextWave.interactable, "after the wave the CTA is enabled again");
        SaveGame save = SaveGame.Load();
        Check(save != null && save.round == 1 && save.buildings.Count == 1 && save.buildings[0].pathTiers.Contains(1), "wave end autosaves round, tower and upgrade");
        Check(save != null && save.buildings[0].targetBehaviour == (int)TargetBehaviour.LAST, "autosave keeps the targeting mode");
        if (save != null)
            SessionState.SetString(ExpectKey, save.round + "|" + save.money + "|" + save.lives);

        // Selling refunds and closes the panel (on a copy, so the autosave still holds the tower)
        game.ChangeGameSpeed(1f);
        SelectionManager.CurrentlySelected = tower.GetComponent<Selectable>();
        yield return 0.2f;
        int refund = Selectable.GetSellValue(tower);
        money = game.Money;
        UIEditorDrive.Click(UIEditorDrive.FindInactive<BevelButton>("Sell", "Content"));
        yield return 0.2f;
        Check(tower == null && game.Money == money + refund, "Sell refunds " + refund + " and removes the tower");
        Check(!UpgradePanelManager.Instance.IsOpen, "selling closes the panel");
    }

    // ---- phase 2: Continue ----------------------------------------------------------------------------

    private static IEnumerator ContinueScenario()
    {
        GameObject continueButton = GameObject.Find("Continue");
        Check(continueButton != null && continueButton.activeInHierarchy, "Continue is shown when a savegame exists");
        if (continueButton == null)
            yield break;
        UIEditorDrive.Click(continueButton);
        yield return 3f;

        string[] expected = SessionState.GetString(ExpectKey, "0|0|0").Split('|');
        GameManager game = GameManager.Instance;
        Check(game != null && !game.IsMainMenu, "Continue loads the level");
        if (game == null)
            yield break;
        Check(game.Round.ToString() == expected[0] && game.Money.ToString() == expected[1] && game.Lives.ToString() == expected[2],
            "Continue restores round, scrap and hull (" + game.Round + "/" + game.Money + "/" + game.Lives + ")");
        Tower restored = null;
        foreach (Tower tower in Object.FindObjectsByType<Tower>(FindObjectsInactive.Exclude))
        {
            if (tower.PlacedByPlayer)
                restored = tower;
        }
        Check(restored != null && restored.Tier == 2 && restored.TargetBehaviour == TargetBehaviour.LAST, "Continue rebuilds the tower with its upgrade and targeting");
    }

    // ---- player data ----------------------------------------------------------------------------------

    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    private static string SaveBackup => Path.Combine(Application.persistentDataPath, "savegame.playtest-backup.json");

    private static void BackupPlayerData()
    {
        SessionState.SetString(BackupKey, PlayerPrefs.HasKey(ProgressKey) ? "1" + PlayerPrefs.GetString(ProgressKey) : "0");
        if (File.Exists(SavePath))
            File.Copy(SavePath, SaveBackup, true);
    }

    private static void RestorePlayerData()
    {
        string backup = SessionState.GetString(BackupKey, "0");
        if (backup.StartsWith("1"))
            PlayerPrefs.SetString(ProgressKey, backup.Substring(1));
        else
            PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
        if (File.Exists(SaveBackup))
        {
            File.Copy(SaveBackup, SavePath, true);
            File.Delete(SaveBackup);
        }
        else if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
    }
}
