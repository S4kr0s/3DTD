using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Screenshots of the redesigned UI for review. Runs play mode in batch mode (with a graphics device),
// drives the HUD and menu into each state and saves PNGs.
//
// Unity -batchmode -projectPath ... -executeMethod UICapture.Run [-captureOut <dir>]
//
// Screen Space Overlay canvases are not drawn into camera targets, so for a capture they are switched to a
// separate UI camera without post-processing (like the overlay in the game). The UI is rendered over black
// and over white to recover its alpha and composited over the world render in linear space.
// The player's progress and savegame are backed up and restored around the menu captures.
[InitializeOnLoad]
public static class UICapture
{
    private const string ActiveKey = "UICapture.Active";
    private const string PhaseKey = "UICapture.Phase";
    private const string StartedKey = "UICapture.Started";
    private const string OutKey = "UICapture.Out";
    private const string ProgressBackupKey = "UICapture.ProgressBackup";
    private const string ProgressPrefsKey = "3DTD.Progress";

    private static readonly string[] Scenes = { "Assets/Scenes/Beginner Level 01.unity", "Assets/Scenes/MainMenuLevel.unity" };

    private static IEnumerator scenario;
    private static int waitUntilFrame;

    static UICapture()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.update += Tick;
    }

    public static void Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tasks/ui-redesign/screenshots"));
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
            if (phase >= Scenes.Length)
            {
                Finish(0);
                return;
            }
            if (!SessionState.GetBool(StartedKey, false))
            {
                SessionState.SetBool(StartedKey, true);
                if (phase == 0)
                    PrepareLevelData();
                else
                    PrepareMenuData();
                EditorSceneManager.OpenScene(Scenes[phase], OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            return;
        }

        if (scenario == null)
        {
            scenario = phase == 0 ? LevelScenario() : MenuScenario();
            waitUntilFrame = Time.frameCount + 90;
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
            Finish(1);
            return;
        }

        scenario = null;
        SessionState.SetInt(PhaseKey, phase + 1);
        SessionState.SetBool(StartedKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void Finish(int code)
    {
        RestorePlayerData();
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
        Debug.Log("UICapture: finished with code " + code);
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    // ---- scenarios -------------------------------------------------------------------------------------

    private static IEnumerator LevelScenario()
    {
        foreach (object step in Shot("01_hud", 1980, 1100)) yield return step;
        foreach (object step in Shot("01b_hud_20x9", 2400, 1080)) yield return step;
        foreach (object step in Shot("01c_hud_16x10", 1920, 1200)) yield return step;

        // Build rail: select the last palette entry and hover a free pad for the placement ghost
        BuildingManager rail = BuildingManager.Instance;
        int last = GameManager.Instance.Buildings.Count - 1;
        rail.Select(last);
        AnchorPoint pad = UIEditorDrive.PickPad(new Vector2(0.38f, 0.42f));
        hoverPad = pad;
        yield return 3;
        foreach (object step in Shot("03_build_tooltip_ghost", 1980, 1100)) yield return step;
        hoverPad = null;
        rail.Select(-1);

        // Tower selected: build the last tower on a pad, buy one upgrade, select it
        GameManager.Instance.Money = 5000;
        AnchorPoint towerPad = UIEditorDrive.PickPad(new Vector2(0.55f, 0.45f));
        GameObject prefab = GameManager.Instance.Buildings[last];
        GameObject built = Object.Instantiate(prefab, towerPad.AnchorPointPosition.position, towerPad.transform.rotation);
        Building building = built.GetComponent<Building>();
        building.AddInvestment(GameManager.Instance.Price(building.Cost));
        building.MarkPlacedByPlayer(last);
        yield return 2;
        Tower tower = built.GetComponent<Tower>();
        if (tower != null)
        {
            UpgradePath[] paths = tower.UpgradeManager.GetUpgradePaths();
            if (paths.Length > 1 && paths[1].UpgradeModules.Length > 0)
                tower.UpgradeManager.ActivateUpgradeModule(paths[1].UpgradeModules[0]);
        }
        GameManager.Instance.Money = 120;
        SelectionManager.CurrentlySelected = built.GetComponent<Selectable>();
        yield return 3;
        foreach (object step in Shot("02_tower_selected", 1980, 1100)) yield return step;

        // Hover the next module of the upgraded path: orange rim and the tooltip beside the panel
        UpgradeCell hovered = null;
        foreach (UpgradeCell cell in Object.FindObjectsByType<UpgradeCell>(FindObjectsInactive.Exclude))
        {
            if (cell.CurrentState == UpgradeCell.State.Next && cell.Tier == 2)
                hovered = cell;
        }
        if (hovered != null)
        {
            foreach (object step in Hover(hovered.gameObject)) yield return step;
            foreach (object step in Shot("02b_upgrade_hover", 1980, 1100)) yield return step;
            Unhover(hovered.gameObject);
        }

        // A HUD tooltip: hovering the hull counter
        GameObject hull = GameObject.Find("Hull");
        if (hull != null)
        {
            foreach (object step in Hover(hull)) yield return step;
            foreach (object step in Shot("02c_hud_tooltip", 1980, 1100)) yield return step;
            Unhover(hull);
        }
        SelectionManager.CurrentlySelected = null;

        // Collapsed rail
        UIEditorDrive.Click(GameObject.Find("Collapse"));
        yield return 2;
        foreach (object step in Shot("04_rail_collapsed", 1980, 1100)) yield return step;
        UIEditorDrive.Click(GameObject.Find("Rail Tab"));
        yield return 2;

        // Pause, options over the game, victory and game over
        PauseMenu.Instance.ToggleMenu();
        yield return 2;
        foreach (object step in Shot("05_pause", 1980, 1100)) yield return step;
        UIEditorDrive.Click(UIEditorDrive.FindInactive<BevelButton>("Options", "Pause Plate"));
        yield return 3;
        foreach (object step in Shot("06_options_ingame", 1980, 1100)) yield return step;
        Object.FindAnyObjectByType<OptionsScreen>().Close();
        PauseMenu.Instance.ToggleMenu();
        yield return 2;
        // A real win: records the Medium medal in the (temporary) progress and shows its Research reward
        GameManager.Instance.GameWon();
        yield return 2;
        foreach (object step in Shot("07_victory", 1980, 1100)) yield return step;
        PauseMenu.Instance.ToggleMenu();
        yield return 2;
        PauseMenu.Instance.GameOver();
        yield return 2;
        foreach (object step in Shot("08_game_over", 1980, 1100)) yield return step;
    }

    // A left drag that starts on target and bubbles to the enclosing ScrollRect, spread over a few frames
    private static IEnumerable<object> DragLeft(GameObject target, float distance)
    {
        Vector2 start = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
        PointerEventData data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = start, pressPosition = start };
        GameObject handler = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.beginDragHandler);
        const int steps = 8;
        for (int i = 1; i <= steps; i++)
        {
            Vector2 previous = data.position;
            data.position = start + new Vector2(-distance * i / steps, 0f);
            data.delta = data.position - previous;
            ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.dragHandler);
            yield return 1;
        }
        ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.endDragHandler);
        if (handler == null)
            Debug.LogWarning("UICapture: nothing handles a drag on " + target.name);
    }

    private static IEnumerator MenuScenario()
    {
        MainMenuScreen menu = Object.FindAnyObjectByType<MainMenuScreen>();
        foreach (object step in Shot("10_menu_level_select", 1980, 1100)) yield return step;

        LevelCard[] cards = Object.FindObjectsByType<LevelCard>(FindObjectsInactive.Exclude);
        foreach (LevelCard card in cards)
        {
            if (card.Level != null && card.Level.sceneName == "Beginner Level 01")
                UIEditorDrive.Click(card.gameObject.GetComponentInChildren<BevelButton>().gameObject);
        }
        yield return 3;
        foreach (object step in Shot("10b_menu_level01_selected", 1980, 1100)) yield return step;

        // Hover feedback: a nav entry slides and lights up, a level card shows its tooltip
        GameObject towersNav = UIEditorDrive.FindInactive<BevelButton>("Towers", "Navigation");
        foreach (object step in Hover(towersNav)) yield return step;
        LevelCard secondCard = null;
        foreach (LevelCard card in cards)
        {
            if (card.Level != null && card.Level.sceneName == "Beginner Level 02")
                secondCard = card;
        }
        if (secondCard != null)
            foreach (object step in Hover(secondCard.gameObject)) yield return step;
        foreach (object step in Shot("10c_menu_hover", 1980, 1100)) yield return step;
        Unhover(towersNav);
        if (secondCard != null)
            Unhover(secondCard.gameObject);

        // Card strip: drag it to the end like a mouse or finger would, then an empty category
        LevelCard lastCard = null;
        foreach (LevelCard card in Object.FindObjectsByType<LevelCard>(FindObjectsInactive.Exclude))
        {
            if (card.Level != null && (lastCard == null || card.CatalogIndex > lastCard.CatalogIndex))
                lastCard = card;
        }
        if (lastCard != null)
        {
            UnityEngine.UI.ScrollRect strip = lastCard.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            foreach (object step in DragLeft(lastCard.gameObject, 900f)) yield return step;
            yield return 30;
            Debug.Log("UICapture: card strip after drag at " + strip.horizontalNormalizedPosition.ToString("0.00") + " (1 = end)");
            foreach (object step in Shot("10d_menu_cards_scrolled", 1980, 1100)) yield return step;
        }
        UIEditorDrive.Click(UIEditorDrive.FindInactive<BevelButton>("Category 1", "Categories"));
        yield return 3;
        foreach (object step in Shot("10e_menu_empty_category", 1980, 1100)) yield return step;
        UIEditorDrive.Click(UIEditorDrive.FindInactive<BevelButton>("Category 0", "Categories"));
        yield return 3;

        menu.Show(1);
        yield return 3;
        BevelButton[] thumbs = GameObject.Find("Rail").GetComponentsInChildren<BevelButton>();
        UIEditorDrive.Click(thumbs[thumbs.Length - 1].gameObject);
        yield return 3;
        foreach (object step in Shot("11_menu_towers", 1980, 1100)) yield return step;
        UIEditorDrive.Click(thumbs[1].gameObject);
        yield return 3;
        foreach (object step in Shot("11b_menu_towers_laser", 1980, 1100)) yield return step;

        menu.Show(2);
        yield return 3;
        foreach (object step in Shot("12_menu_upgrades", 1980, 1100)) yield return step;
        foreach (object step in Shot("12e_menu_upgrades_1280x720", 1280, 720)) yield return step;
        foreach (object step in Shot("12f_menu_upgrades_1024x768", 1024, 768)) yield return step;
        SegmentedControl trees = Object.FindAnyObjectByType<MetaUpgradesScreen>().GetComponentInChildren<SegmentedControl>();
        string[] treeShots = { "12b_menu_upgrades_economy", "12c_menu_upgrades_hull", "12d_menu_upgrades_global" };
        for (int i = 1; i < trees.Items.Count && i <= treeShots.Length; i++)
        {
            UIEditorDrive.Click(trees.Items[i].gameObject);
            yield return 3;
            if (i == 1)
            {
                SkillNode node = Object.FindObjectsByType<SkillNode>(FindObjectsInactive.Exclude)[0];
                foreach (object step in Hover(node.GetComponentInChildren<BevelButton>().gameObject)) yield return step;
                foreach (object step in Shot(treeShots[i - 1], 1980, 1100)) yield return step;
                Unhover(node.GetComponentInChildren<BevelButton>().gameObject);
            }
            else
            {
                foreach (object step in Shot(treeShots[i - 1], 1980, 1100)) yield return step;
            }
        }
        UIEditorDrive.Click(trees.Items[0].gameObject);
        yield return 2;

        menu.Show(3);
        yield return 2;
        OptionsScreen options = Object.FindAnyObjectByType<OptionsScreen>();
        SegmentedControl tabs = options.GetComponentInChildren<SegmentedControl>();
        UIEditorDrive.Click(tabs.Items[1].gameObject);
        yield return 3;
        foreach (object step in Shot("13_menu_options_video", 1980, 1100)) yield return step;
        UIEditorDrive.Click(tabs.Items[0].gameObject);
        yield return 3;
        foreach (object step in Shot("14_menu_options_general", 1980, 1100)) yield return step;
        UIEditorDrive.Click(tabs.Items[3].gameObject);
        yield return 3;
        foreach (object step in Shot("15_menu_options_controls", 1980, 1100)) yield return step;
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    // Pointer enter on an element (hover state of its button, tooltip opened at once), then time for the
    // hover animation
    private static IEnumerable<object> Hover(GameObject target)
    {
        if (target == null)
            yield break;
        ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        TooltipTrigger trigger = target.GetComponent<TooltipTrigger>();
        if (trigger != null)
            trigger.ShowNow();
        float until = Time.realtimeSinceStartup + 0.4f;
        while (Time.realtimeSinceStartup < until)
            yield return 1;
    }

    private static void Unhover(GameObject target)
    {
        if (target != null)
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
    }

    private static AnchorPoint hoverPad;

    // One screenshot: switch the overlay canvases to a capture camera, wait for layout, render, composite
    private static IEnumerable<object> Shot(string name, int width, int height)
    {
        Camera worldCamera = Camera.main;
        RenderTexture worldTarget = NewTarget(width, height);
        RenderTexture uiTarget = NewTarget(width, height);
        RenderTexture previousTarget = worldCamera.targetTexture;
        worldCamera.targetTexture = worldTarget;

        GameObject uiCameraObject = new GameObject("UI Capture Camera");
        Camera uiCamera = uiCameraObject.AddComponent<Camera>();
        uiCamera.clearFlags = CameraClearFlags.SolidColor;
        uiCamera.backgroundColor = Color.black;
        uiCamera.cullingMask = 1 << 5;
        uiCamera.nearClipPlane = 0.1f;
        uiCamera.farClipPlane = 10f;
        uiCamera.depth = 100f;
        uiCamera.targetTexture = uiTarget;
        uiCameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

        List<Canvas> switched = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
                switched.Add(canvas);
            }
        }

        yield return 4;
        if (hoverPad != null && PlacementPreview.Instance != null)
            PlacementPreview.Instance.Hover(hoverPad, true, true);
        yield return 1;
        if (hoverPad != null && PlacementPreview.Instance != null)
            PlacementPreview.Instance.Hover(hoverPad, true, true);
        yield return 1;
        Color[] world = Read(worldTarget);
        Color[] overBlack = Read(uiTarget);
        uiCamera.backgroundColor = Color.white;
        if (hoverPad != null && PlacementPreview.Instance != null)
            PlacementPreview.Instance.Hover(hoverPad, true, true);
        yield return 1;
        Color[] overWhite = Read(uiTarget);

        Texture2D output = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[world.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            float coverage = 1f - ((overWhite[i].r - overBlack[i].r) + (overWhite[i].g - overBlack[i].g) + (overWhite[i].b - overBlack[i].b)) / 3f;
            coverage = Mathf.Clamp01(coverage);
            Color linear = overBlack[i] + world[i] * (1f - coverage);
            pixels[i] = new Color(Gamma(linear.r), Gamma(linear.g), Gamma(linear.b), 1f);
        }
        output.SetPixels(pixels);
        output.Apply();
        string path = Path.Combine(SessionState.GetString(OutKey, "."), name + ".png");
        File.WriteAllBytes(path, output.EncodeToPNG());
        Debug.Log("UICapture: wrote " + path);

        foreach (Canvas canvas in switched)
        {
            if (canvas != null)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        worldCamera.targetTexture = previousTarget;
        Object.Destroy(uiCameraObject);
        Object.Destroy(output);
        worldTarget.Release();
        uiTarget.Release();
        yield return 2;
    }

    private static float Gamma(float linear)
    {
        return Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.Max(0f, linear)));
    }

    private static RenderTexture NewTarget(int width, int height)
    {
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        target.antiAliasing = 4;
        target.Create();
        return target;
    }

    private static Color[] Read(RenderTexture source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = source;
        Texture2D texture = new Texture2D(source.width, source.height, TextureFormat.RGBAHalf, false, true);
        texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        Color[] pixels = texture.GetPixels();
        Object.Destroy(texture);
        return pixels;
    }

    // ---- sample progress for the menu captures ---------------------------------------------------------

    private static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");
    private static string SaveBackupPath => Path.Combine(Application.persistentDataPath, "savegame.capture-backup.json");

    private static void BackupPlayerData()
    {
        SessionState.SetString(ProgressBackupKey, PlayerPrefs.HasKey(ProgressPrefsKey) ? "1" + PlayerPrefs.GetString(ProgressPrefsKey) : "0");
        if (File.Exists(SavePath))
            File.Copy(SavePath, SaveBackupPath, true);
    }

    // The level captures start from empty progress, so the victory screen earns a fresh medal
    private static void PrepareLevelData()
    {
        PlayerPrefs.SetString(ProgressPrefsKey, "{\"levels\":[],\"research\":0,\"metaNodes\":[]}");
        PlayerPrefs.Save();
    }

    private static void PrepareMenuData()
    {
        // Level 01 mastered, Level 02 on Easy and Medium, some Research and two owned nodes
        PlayerPrefs.SetString(ProgressPrefsKey,
            "{\"levels\":[{\"id\":\"Beginner Level 01\",\"medals\":15},{\"id\":\"Beginner Level 02\",\"medals\":3}],\"research\":12,\"metaNodes\":[\"t-hardened\",\"t-barrels\"]}");
        PlayerPrefs.Save();
        File.WriteAllText(SavePath,
            "{\"sceneName\":\"Beginner Level 01\",\"difficulty\":2,\"round\":12,\"money\":640,\"lives\":88,\"won\":false,\"savedAt\":\"\",\"buildings\":[]}");
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
            File.Copy(SaveBackupPath, SavePath, true);
        else if (File.Exists(SavePath))
            File.Delete(SavePath);
        if (File.Exists(SaveBackupPath))
            File.Delete(SaveBackupPath);
        SessionState.EraseString(ProgressBackupKey);
    }
}
