using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// EditMode tests for the UI redesign: formatting, stat presentation, gradients, progression rules,
// settings, savegame serialisation and the integrity of the generated UI prefabs.
// They live in Assembly-CSharp-Editor (no asmdef) because the game code is in Assembly-CSharp.
public class UIFormatTests
{
    [Test]
    public void ShortDropsTrailingZeroAndKeepsOneDecimal()
    {
        Assert.AreEqual("3", UIFormat.Short(3f));
        Assert.AreEqual("12", UIFormat.Short(12.01f));
        Assert.AreEqual("3.5", UIFormat.Short(3.46f));
        Assert.AreEqual("0.3", UIFormat.Short(0.28f));
    }

    [Test]
    public void SignedUsesTrueMinus()
    {
        Assert.AreEqual("+5", UIFormat.Signed(5));
        Assert.AreEqual("−5", UIFormat.Signed(-5));
        Assert.AreEqual("+10%", UIFormat.SignedPercent(10f));
        Assert.AreEqual("−8%", UIFormat.SignedPercent(-8f));
    }

    [Test]
    public void TabularWrapsDigitsInMonospace()
    {
        string text = UIFormat.Tabular(350);
        StringAssert.StartsWith("<mspace=", text);
        StringAssert.Contains(">350</mspace>", text);
        StringAssert.Contains(">120</mspace>", UIFormat.TabularLabel(120));
    }

    [Test]
    public void RomanAndTwoDigits()
    {
        Assert.AreEqual("IV", UIFormat.Roman(4));
        Assert.AreEqual("09", UIFormat.TwoDigits(9));
        Assert.AreEqual("12", UIFormat.TwoDigits(12));
    }
}

public class TowerStatInfoTests
{
    [Test]
    public void GridHasTheEightStatsInTheDesignOrder()
    {
        CollectionAssert.AreEqual(new[]
        {
            TowerStatKind.Damage, TowerStatKind.FireRate, TowerStatKind.Range, TowerStatKind.Accuracy,
            TowerStatKind.Speed, TowerStatKind.Pierce, TowerStatKind.Radius, TowerStatKind.Capacity,
        }, TowerStatInfo.Grid);
    }

    [Test]
    public void EveryGridStatHasItsOwnIcon()
    {
        HashSet<string> icons = new HashSet<string>();
        foreach (TowerStatKind kind in TowerStatInfo.Grid)
            Assert.IsTrue(icons.Add(TowerStatInfo.Icon(kind)), "Icon reused: " + TowerStatInfo.Icon(kind));
    }

    [Test]
    public void FireRateIsShownAsShotsPerSecond()
    {
        // FIRERATE is seconds between shots
        Assert.AreEqual("2/s", TowerStatInfo.Format(TowerStatKind.FireRate, 0.5f));
        Assert.AreEqual("0.3/s", TowerStatInfo.Format(TowerStatKind.FireRate, 3.6f));
        // Clamped like StatsManager.GetFireInterval
        Assert.AreEqual(1f / StatsManager.MinFireInterval, TowerStatInfo.Display(TowerStatKind.FireRate, 0.001f), 0.0001f);
    }

    [Test]
    public void MissingOrUnusedStatsShowADash()
    {
        Assert.AreEqual("—", TowerStatInfo.Format(TowerStatKind.Damage, -1f));
        Assert.AreEqual("—", TowerStatInfo.Format(TowerStatKind.Capacity, 0f));
        Assert.AreEqual("—", TowerStatInfo.Format(TowerStatKind.FireRate, 0f));
        Assert.AreEqual("100%", TowerStatInfo.Format(TowerStatKind.Accuracy, 1f));
    }

    [Test]
    public void AfterAppliesBonusesThenCompoundingModifiers()
    {
        StatsScriptableObject config = ScriptableObject.CreateInstance<StatsScriptableObject>();
        config.Damage = 2f;
        List<StatUpgrade> upgrades = new List<StatUpgrade>
        {
            new StatUpgrade(Stat.StatType.DAMAGE, 1f, false),
            new StatUpgrade(Stat.StatType.DAMAGE, 50f, true),
            new StatUpgrade(Stat.StatType.RANGE, 10f, false),
        };
        // (2 + 1) * 1.5; the RANGE upgrade does not touch DAMAGE
        Assert.AreEqual(4.5f, TowerStatInfo.After(config, Stat.StatType.DAMAGE, upgrades), 0.0001f);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void BaseValueMatchesStatsManagerMapping()
    {
        StatsScriptableObject config = ScriptableObject.CreateInstance<StatsScriptableObject>();
        config.Range = 3.5f;
        config.Piercing = 2;
        config.Ammo = 5f;
        Assert.AreEqual(3.5f, StatsManager.GetBaseValue(config, Stat.StatType.RANGE));
        Assert.AreEqual(2f, StatsManager.GetBaseValue(config, Stat.StatType.PIERCING));
        Assert.AreEqual(5f, TowerStatInfo.Base(config, TowerStatKind.Capacity));
        Assert.AreEqual(-1f, StatsManager.GetBaseValue(null, Stat.StatType.RANGE));
        Object.DestroyImmediate(config);
    }
}

public class BevelGradientTests
{
    private static readonly Rect Box = new Rect(0f, 0f, 200f, 100f);

    [Test]
    public void VerticalGradientRunsTopToBottom()
    {
        BevelGradient gradient = BevelGradient.Vertical(Color.white, Color.black);
        // UI space is y-up: the top edge is yMax
        Assert.AreEqual(1f, gradient.Evaluate(Box, new Vector2(50f, 100f)).r, 0.001f);
        Assert.AreEqual(0f, gradient.Evaluate(Box, new Vector2(50f, 0f)).r, 0.001f);
        Assert.AreEqual(0.5f, gradient.Evaluate(Box, new Vector2(50f, 50f)).r, 0.001f);
    }

    [Test]
    public void NinetyDegreesRunsLeftToRight()
    {
        BevelGradient gradient = BevelGradient.Linear(90f, new BevelStop(Color.black, 0f), new BevelStop(Color.white, 1f));
        Assert.AreEqual(0f, gradient.Evaluate(Box, new Vector2(0f, 30f)).r, 0.001f);
        Assert.AreEqual(1f, gradient.Evaluate(Box, new Vector2(200f, 30f)).r, 0.001f);
    }

    [Test]
    public void SheenIsAHardEdgedBandOnTheLeft()
    {
        BevelGradient sheen = BevelGradient.Sheen(0.07f, 0.16f);
        Assert.AreEqual(0.07f, sheen.Evaluate(Box, new Vector2(2f, 98f)).a, 0.001f);
        Assert.AreEqual(0f, sheen.Evaluate(Box, new Vector2(150f, 50f)).a, 0.001f);
    }

    [Test]
    public void EveryStyleHasADefault()
    {
        foreach (BevelStyle style in System.Enum.GetValues(typeof(BevelStyle)))
        {
            if (style != BevelStyle.None)
                Assert.IsNotNull(UITheme.FallbackStyle(style), "No default for " + style);
        }
    }

    [Test]
    public void ChamferCutsTopLeftAndBottomRight()
    {
        List<Vector2> polygon = new List<Vector2>();
        UIGeometry.Chamfer(polygon, new Rect(0f, 0f, 100f, 50f), 10f);
        CollectionAssert.AreEqual(new[]
        {
            new Vector2(10f, 50f), new Vector2(100f, 50f), new Vector2(100f, 10f),
            new Vector2(90f, 0f), new Vector2(0f, 0f), new Vector2(0f, 40f),
        }, polygon);
    }

    [Test]
    public void GradientPolygonGetsVertexColoursAtTheStops()
    {
        List<Vector2> polygon = new List<Vector2>();
        Rect rect = new Rect(0f, 0f, 100f, 100f);
        UIGeometry.Chamfer(polygon, rect, 0f);
        BevelGradient gradient = BevelGradient.Linear(180f, new BevelStop(Color.white, 0f), new BevelStop(Color.white, 0.5f), new BevelStop(Color.black, 0.5f));
        VertexHelper vh = new VertexHelper();
        UIGeometry.AddPolygon(vh, polygon, gradient, rect, Color.white);
        // Hard stop at half height: the mesh is split there, white above and black below
        bool sawWhite = false;
        bool sawBlack = false;
        UIVertex vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            if (vertex.position.y > 50.01f)
                Assert.AreEqual(255, vertex.color.r, "above the stop");
            if (vertex.position.y < 49.99f)
                Assert.AreEqual(0, vertex.color.r, "below the stop");
            sawWhite |= vertex.color.r == 255;
            sawBlack |= vertex.color.r == 0;
        }
        Assert.IsTrue(sawWhite && sawBlack);
        vh.Dispose();
    }
}

public class ProgressionTests
{
    private string backup;
    private bool hadBackup;
    private LevelCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        hadBackup = PlayerPrefs.HasKey("3DTD.Progress");
        backup = PlayerPrefs.GetString("3DTD.Progress", "");
        PlayerProgress.ResetAll();

        catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        catalog.categories = new List<LevelCatalog.Category>
        {
            new LevelCatalog.Category { id = "a", displayName = "Beginner" },
            new LevelCatalog.Category { id = "b", displayName = "Space" },
        };
        catalog.levels = new List<LevelCatalog.Level>
        {
            new LevelCatalog.Level { sceneName = "L1", displayName = "Level 01", category = "a" },
            new LevelCatalog.Level { sceneName = "L2", displayName = "Level 02", category = "a" },
            new LevelCatalog.Level { sceneName = "L3", displayName = "Level 01", category = "b" },
        };
    }

    [TearDown]
    public void TearDown()
    {
        if (hadBackup)
            PlayerPrefs.SetString("3DTD.Progress", backup);
        else
            PlayerPrefs.DeleteKey("3DTD.Progress");
        PlayerPrefs.Save();
        PlayerProgress.Reload();
        Object.DestroyImmediate(catalog);
    }

    [Test]
    public void FirstMedalPaysResearchOnce()
    {
        Assert.AreEqual(3, PlayerProgress.RecordWin("L1", Difficulty.Hard));
        Assert.AreEqual(0, PlayerProgress.RecordWin("L1", Difficulty.Hard));
        Assert.AreEqual(3, PlayerProgress.Research);
        Assert.IsTrue(PlayerProgress.HasMedal("L1", Difficulty.Hard));
        Assert.IsFalse(PlayerProgress.HasMedal("L1", Difficulty.Easy));
        Assert.AreEqual(PlayerProgress.MedalBit(Difficulty.Hard), PlayerProgress.GetMedals("L1"));
    }

    [Test]
    public void ProgressSurvivesAReload()
    {
        PlayerProgress.RecordWin("L1", Difficulty.Easy);
        PlayerProgress.Reload();
        Assert.IsTrue(PlayerProgress.HasMedal("L1", Difficulty.Easy));
        Assert.AreEqual(1, PlayerProgress.Research);
    }

    [Test]
    public void LevelsUnlockAfterThePreviousOneIsCleared()
    {
        Assert.IsTrue(catalog.IsUnlocked(0));
        Assert.IsFalse(catalog.IsUnlocked(1));
        PlayerProgress.RecordWin("L1", Difficulty.Easy);
        Assert.IsTrue(catalog.IsUnlocked(1));
        Assert.IsFalse(catalog.IsUnlocked(2));
    }

    [Test]
    public void UnlockHintNamesTheCategoryAcrossCategories()
    {
        Assert.AreEqual("Clear Level 01 to unlock", catalog.UnlockHint(1));
        Assert.AreEqual("Clear Beginner Level 02 to unlock", catalog.UnlockHint(2));
    }

    [Test]
    public void ImpossibleUnlocksAfterClearingHard()
    {
        Assert.IsFalse(LevelCatalog.IsDifficultyUnlocked("L1", Difficulty.Impossible));
        Assert.IsTrue(LevelCatalog.IsDifficultyUnlocked("L1", Difficulty.Hard));
        PlayerProgress.RecordWin("L1", Difficulty.Medium);
        Assert.IsFalse(LevelCatalog.IsDifficultyUnlocked("L1", Difficulty.Impossible));
        PlayerProgress.RecordWin("L1", Difficulty.Hard);
        Assert.IsTrue(LevelCatalog.IsDifficultyUnlocked("L1", Difficulty.Impossible));
    }

    [Test]
    public void NodesCostResearchAndCanOnlyBeBoughtOnce()
    {
        PlayerProgress.RecordWin("L1", Difficulty.Medium);
        Assert.IsFalse(PlayerProgress.TryUnlockNode("x", 3), "not enough Research");
        Assert.IsTrue(PlayerProgress.TryUnlockNode("x", 2));
        Assert.AreEqual(0, PlayerProgress.Research);
        PlayerProgress.RecordWin("L1", Difficulty.Easy);
        Assert.IsFalse(PlayerProgress.TryUnlockNode("x", 1), "already owned");
        Assert.IsTrue(PlayerProgress.OwnsNode("x"));
    }

    [Test]
    public void MetaTreeRequirementsReferToExistingNodes()
    {
        MetaUpgradeTree tree = MetaUpgradeTree.Instance;
        Assert.IsNotNull(tree, "Resources/Progress/MetaUpgradeTree is missing");
        HashSet<string> ids = new HashSet<string>();
        foreach (MetaUpgradeTree.SkillTree skillTree in tree.trees)
        {
            foreach (MetaUpgradeTree.Node node in skillTree.nodes)
                Assert.IsTrue(ids.Add(node.id), "Duplicate node id " + node.id);
        }
        foreach (MetaUpgradeTree.SkillTree skillTree in tree.trees)
        {
            Assert.LessOrEqual(skillTree.nodes.FindAll(n => n.capstone).Count, 1, skillTree.id + " has several capstones");
            foreach (MetaUpgradeTree.Node node in skillTree.nodes)
            {
                foreach (string required in node.requires)
                    Assert.IsTrue(skillTree.nodes.Exists(n => n.id == required && n.tier < node.tier), node.id + " requires " + required);
            }
        }
    }

    [Test]
    public void FireRateBonusBecomesANegativeIntervalModifier()
    {
        // "+X% fire rate" is a FIRERATE modifier of -X/(100+X)*100
        Assert.AreEqual(-20f, MetaUpgrades.ToModifier(Stat.StatType.FIRERATE, 25f), 0.0001f);
        Assert.AreEqual(-50f, MetaUpgrades.ToModifier(Stat.StatType.FIRERATE, 100f), 0.0001f);
        Assert.AreEqual(5f, MetaUpgrades.ToModifier(Stat.StatType.RANGE, 5f), 0.0001f);
    }
}

public class OptionsAndSaveTests
{
    [Test]
    public void CountDifferencesCountsEachChangedSetting()
    {
        GameOptions.Values a = new GameOptions.Values();
        GameOptions.Values b = a.Clone();
        Assert.AreEqual(0, a.CountDifferences(b));
        b.vSync = !a.vSync;
        b.masterVolume = 0.25f;
        b.resolutionWidth = 1280;
        b.resolutionHeight = 720;
        // Resolution width and height count as one setting
        Assert.AreEqual(3, a.CountDifferences(b));
        Assert.AreNotSame(a, b);
    }

    [Test]
    public void VolumeMapsToDecibels()
    {
        Assert.AreEqual(0f, GameOptions.ToDecibel(1f), 0.001f);
        Assert.AreEqual(-80f, GameOptions.ToDecibel(0f), 0.001f);
        Assert.AreEqual(-6.0206f, GameOptions.ToDecibel(0.5f), 0.001f);
    }

    [Test]
    public void SaveGameRoundTripsThroughJson()
    {
        SaveGame save = new SaveGame { sceneName = "Beginner Level 01", difficulty = 2, round = 12, money = 640, lives = 88 };
        SaveGame.SavedBuilding building = new SaveGame.SavedBuilding
        {
            palette = 3,
            position = new Vector3(1f, 2f, 3f),
            rotation = Quaternion.Euler(0f, 90f, 0f),
            invested = 300,
            targetBehaviour = (int)TargetBehaviour.STRONGEST,
        };
        building.pathTiers.AddRange(new[] { 2, 0, 1 });
        save.buildings.Add(building);

        SaveGame loaded = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
        Assert.AreEqual("Beginner Level 01", loaded.sceneName);
        Assert.AreEqual(12, loaded.round);
        Assert.AreEqual(1, loaded.buildings.Count);
        Assert.AreEqual(3, loaded.buildings[0].palette);
        CollectionAssert.AreEqual(new[] { 2, 0, 1 }, loaded.buildings[0].pathTiers);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), loaded.buildings[0].position);
        Assert.AreEqual(300, loaded.buildings[0].invested);
    }

    [Test]
    public void SummaryUsesTheCatalogName()
    {
        SaveGame save = new SaveGame { sceneName = "Beginner Level 01", difficulty = (int)Difficulty.Hard, round = 12 };
        Assert.AreEqual("Level 01 · Hard · Wave 12", save.Summary());
    }
}

public class UIPrefabTests
{
    private static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(prefab, path + " is missing; run 3DTD > UI Redesign > Rebuild UI");
        return prefab;
    }

    [Test]
    public void HudCanvasHasEveryController()
    {
        GameObject hud = Load("Assets/Prefabs/UI/HUD Canvas.prefab");
        Assert.IsNotNull(hud.GetComponent<GameStatDisplay>());
        Assert.IsNotNull(hud.GetComponent<BuildingManager>());
        Assert.IsNotNull(hud.GetComponent<UpgradePanelManager>());
        Assert.IsNotNull(hud.GetComponent<PauseMenu>());
        Assert.IsNotNull(hud.GetComponent<SelectionIndicator>());
        Assert.IsNotNull(hud.GetComponent<PlacementPreview>());
        Assert.IsNotNull(hud.GetComponent<TooltipService>());
        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        Assert.AreEqual(new Vector2(1100f, 611f), scaler.referenceResolution);
        Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
        Assert.IsNotNull(hud.GetComponentInChildren<SafeAreaFitter>(true));
    }

    [Test]
    public void TowerPanelHasEightStatTilesAndOneUpgradeRowTemplate()
    {
        GameObject hud = Load("Assets/Prefabs/UI/HUD Canvas.prefab");
        Transform stats = hud.transform.Find("Safe Area/HUD/Tower Panel/Viewport/Content/Stats");
        Assert.IsNotNull(stats);
        Assert.AreEqual(8, stats.GetComponentsInChildren<StatTile>(true).Length);
        Assert.AreEqual(1, hud.GetComponentsInChildren<UpgradeRow>(true).Length);
        // One template path row with its three tiers
        Assert.AreEqual(3, hud.GetComponentsInChildren<UpgradeCell>(true).Length);
    }

    [Test]
    public void TooltipLayerIsAboveEverythingElse()
    {
        foreach (string path in new[] { "Assets/Prefabs/UI/HUD Canvas.prefab", "Assets/Prefabs/UI/Main Menu Canvas.prefab" })
        {
            GameObject canvas = Load(path);
            Assert.IsNotNull(canvas.GetComponent<TooltipService>(), path);
            Transform last = canvas.transform.GetChild(canvas.transform.childCount - 1);
            Assert.AreEqual("Tooltip Safe Area", last.name, path);
        }
    }

    [Test]
    public void HudResourcesAndTowerPanelHaveHoverText()
    {
        GameObject hud = Load("Assets/Prefabs/UI/HUD Canvas.prefab");
        foreach (string resource in new[] { "Scrap", "Hull", "Wave" })
        {
            Transform group = hud.transform.Find("Safe Area/HUD/Top Bar/Resources/" + resource);
            Assert.IsNotNull(group, resource);
            TooltipTrigger trigger = group.GetComponent<TooltipTrigger>();
            Assert.IsNotNull(trigger, resource);
            Assert.AreEqual(resource, trigger.Title);
            Assert.IsNotEmpty(trigger.Body);
            Assert.IsNotNull(group.GetComponent<UnityEngine.UI.Graphic>(), resource + " needs a raycast target");
        }
        Transform stats = hud.transform.Find("Safe Area/HUD/Tower Panel/Viewport/Content/Stats");
        foreach (StatTile tile in stats.GetComponentsInChildren<StatTile>(true))
            Assert.IsNotNull(tile.GetComponent<TooltipTrigger>(), tile.name);
    }

    [Test]
    public void UiFontsCarryNoImportedKerning()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMPro.TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path);
            Assert.IsFalse(font.getFontFeatures, path);
            Assert.AreEqual(0, font.fontFeatureTable.glyphPairAdjustmentRecords.Count, path);
            Assert.AreEqual(0, font.fontFeatureTable.ligatureRecords.Count, path);
        }
    }

    [Test]
    public void EveryStatHasADescription()
    {
        foreach (TowerStatKind kind in System.Enum.GetValues(typeof(TowerStatKind)))
            Assert.IsNotEmpty(TowerStatInfo.Description(kind), kind.ToString());
    }

    [Test]
    public void BlockedUpgradeReasonsFollowThePathLimits()
    {
        StringAssert.Contains("two paths", UpgradeCell.BlockedReason(1));
        StringAssert.Contains("tier 2", UpgradeCell.BlockedReason(2));
        StringAssert.Contains("one path", UpgradeCell.BlockedReason(3));
    }

    [Test]
    public void OnlyOneKeyCapPerScreen()
    {
        GameObject hud = Load("Assets/Prefabs/UI/HUD Canvas.prefab");
        int hudKeyCaps = 0;
        foreach (BevelButton button in hud.transform.Find("Safe Area/HUD").GetComponentsInChildren<BevelButton>(true))
        {
            if (button.Plate != null && button.Plate.Style == BevelStyle.KeyCap)
                hudKeyCaps++;
        }
        Assert.AreEqual(1, hudKeyCaps, "the in-game HUD's only CTA is Next wave");
    }

    [Test]
    public void OptionsScreenHasARowForEverySetting()
    {
        GameObject options = Load("Assets/Prefabs/UI/Options Screen.prefab");
        HashSet<OptionSetting> settings = new HashSet<OptionSetting>();
        foreach (OptionRow row in options.GetComponentsInChildren<OptionRow>(true))
            Assert.IsTrue(settings.Add(row.Setting), "Duplicate row for " + row.Setting);
        foreach (OptionSetting setting in System.Enum.GetValues(typeof(OptionSetting)))
            Assert.IsTrue(settings.Contains(setting), "No row for " + setting);
    }

    [Test]
    public void MainMenuHasFourScreensAndContinue()
    {
        GameObject menu = Load("Assets/Prefabs/UI/Main Menu Canvas.prefab");
        Assert.IsNotNull(menu.GetComponent<MainMenuScreen>());
        Assert.IsNotNull(menu.GetComponentInChildren<LevelSelectScreen>(true));
        Assert.IsNotNull(menu.GetComponentInChildren<EncyclopediaScreen>(true));
        Assert.IsNotNull(menu.GetComponentInChildren<MetaUpgradesScreen>(true));
        Assert.IsNotNull(menu.GetComponentInChildren<OptionsScreen>(true));
        Assert.IsNotNull(menu.transform.Find("Safe Area/Continue"));
    }

    [Test]
    public void LevelCardsScrollInsideAMaskedViewport()
    {
        GameObject menu = Load("Assets/Prefabs/UI/Main Menu Canvas.prefab");
        LevelSelectScreen screen = menu.GetComponentInChildren<LevelSelectScreen>(true);
        LevelCard template = screen.GetComponentInChildren<LevelCard>(true);
        ScrollRect scroll = template.GetComponentInParent<ScrollRect>(true);
        Assert.IsNotNull(scroll, "The level cards are not in a ScrollRect");
        Assert.IsTrue(scroll.horizontal);
        Assert.IsFalse(scroll.vertical);
        Assert.AreSame(template.transform.parent, scroll.content);
        Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>(), "The card viewport doesn't mask");
        // Drags that start between cards need a raycast target on the viewport
        Assert.IsNotNull(scroll.viewport.GetComponent<Graphic>());
    }

    [Test]
    public void LevelCatalogListsTheFiveCategoriesInOrder()
    {
        LevelCatalog catalog = LevelCatalog.Instance;
        CollectionAssert.AreEqual(new[] { "Beginner", "Intermediate", "Advanced", "Expert", "Space" },
            catalog.categories.ConvertAll(c => c.displayName).ToArray());
        foreach (LevelCatalog.Level level in catalog.levels)
            Assert.IsNotNull(catalog.FindCategory(level.category), level.sceneName + " has an unknown category " + level.category);
        Assert.IsNotEmpty(catalog.LevelsIn("beginner"));
    }

    [Test]
    public void SmallControlsKeepA44PxTouchTarget()
    {
        foreach (string path in new[] { "Assets/Prefabs/UI/HUD Canvas.prefab", "Assets/Prefabs/UI/Options Screen.prefab", "Assets/Prefabs/UI/Main Menu Canvas.prefab" })
        {
            GameObject prefab = Load(path);
            foreach (ToggleSwitch toggle in prefab.GetComponentsInChildren<ToggleSwitch>(true))
                AssertTouchTarget(toggle.transform, path);
            foreach (SegmentedControl control in prefab.GetComponentsInChildren<SegmentedControl>(true))
            {
                foreach (BevelButton item in control.Items)
                    AssertTouchTarget(item.transform, path);
            }
        }
    }

    private static void AssertTouchTarget(Transform control, string path)
    {
        RectTransform rect = (RectTransform)control;
        LayoutElement layout = control.GetComponent<LayoutElement>();
        float width = layout != null && layout.preferredWidth > 0f ? layout.preferredWidth : rect.sizeDelta.x;
        float height = layout != null && layout.preferredHeight > 0f ? layout.preferredHeight : rect.sizeDelta.y;
        if (width >= 44f && height >= 44f)
            return;
        Transform hit = control.Find("Hit Area");
        Assert.IsNotNull(hit, control.name + " in " + path + " is smaller than 44 px and has no hit area");
        Vector2 size = ((RectTransform)hit).sizeDelta;
        Assert.GreaterOrEqual(Mathf.Min(size.x, size.y), 44f, control.name + " hit area in " + path);
    }

    [Test]
    public void ThemeKnowsEveryIconTheUiAsksFor()
    {
        UITheme theme = UITheme.Current;
        Assert.IsNotNull(theme, "Resources/UI/UITheme is missing");
        foreach (TowerStatKind kind in TowerStatInfo.Grid)
            Assert.IsNotNull(theme.Icon(TowerStatInfo.Icon(kind)), TowerStatInfo.Icon(kind));
        foreach (string icon in new[] { "fx_gem_scrap", "fx_gem_hull", "fx_gem_wave", "fx_medal_impossible", "fx_medal_impossible_empty", "fx_gem_research", "ic_lock", "ic_check" })
            Assert.IsNotNull(theme.Icon(icon), icon);
        foreach (MetaUpgradeTree.SkillTree tree in MetaUpgradeTree.Instance.trees)
        {
            Assert.IsNotNull(theme.Icon(tree.icon), tree.icon);
            foreach (MetaUpgradeTree.Node node in tree.nodes)
                Assert.IsNotNull(theme.Icon(node.icon), node.id + " icon " + node.icon);
        }
        foreach (TMP_FontAsset font in new[] { theme.displayBold, theme.displayExtraBold, theme.labelBold, theme.labelExtraBold })
            Assert.IsNotNull(font);
    }

    [Test]
    public void LevelCatalogMatchesTheBuildSettings()
    {
        LevelCatalog catalog = LevelCatalog.Instance;
        Assert.IsNotNull(catalog, "Resources/Progress/LevelCatalog is missing");
        int levels = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || scene.path.EndsWith("MainMenuLevel.unity"))
                continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            Assert.GreaterOrEqual(catalog.IndexOf(name), 0, name + " is not in the catalog");
            LevelCatalog.Level level = catalog.Find(name);
            Assert.IsNotEmpty(level.lanes, name + " has no lane path for its thumbnail");
            Assert.GreaterOrEqual(level.lanes[0].points.Count, 2, name);
            levels++;
        }
        Assert.AreEqual(levels, catalog.levels.Count);
    }
}
