using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

// Builds the redesigned UI (tasks/ui-redesign: concept B boards and concept C build menu) as prefabs and
// wires them into GAME_SETUP and the main menu scene. Re-running it regenerates the UI prefabs, so edits
// to those prefabs are overwritten; the theme, level catalog and meta-upgrade tree keep manual edits.
//
// Batch mode: Unity -batchmode -projectPath ... -executeMethod UIRedesignBuilder.BuildAll
public static partial class UIRedesignBuilder
{
    private const string SpriteFolder = "Assets/Sprites/UI";
    private const string FontFolder = "Assets/Fonts";
    private const string ThemePath = "Assets/Resources/UI/UITheme.asset";
    private const string CatalogPath = "Assets/Resources/Progress/LevelCatalog.asset";
    private const string MetaPath = "Assets/Resources/Progress/MetaUpgradeTree.asset";
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string HudPrefabPath = PrefabFolder + "/HUD Canvas.prefab";
    private const string MenuPrefabPath = PrefabFolder + "/Main Menu Canvas.prefab";
    private const string OptionsPrefabPath = PrefabFolder + "/Options Screen.prefab";
    private const string GameSetupPath = "Assets/Prefabs/GAME_SETUP.prefab";
    private const string MenuScenePath = "Assets/Scenes/MainMenuLevel.unity";
    private const string MixerPath = "Assets/MasterAudioMixer.mixer";

    private static UITheme T => UIBuild.Theme;

    [MenuItem("3DTD/UI Redesign/Rebuild UI")]
    public static void BuildAll()
    {
        bool ok = false;
        try
        {
            EnsureFolder(SpriteFolder);
            EnsureFolder("Assets/Resources/UI");
            EnsureFolder("Assets/Resources/Progress");
            EnsureFolder(PrefabFolder);

            ImportSprites();
            // Opening the level scenes unloads assets created earlier in this run, so this comes first
            CreateLevelCatalog();
            List<TMP_FontAsset> fonts = CreateFonts();
            UIBuild.Theme = CreateTheme(fonts);
            CreateMetaTree();

            GameObject options = BuildOptionsPrefab();
            GameObject hud = BuildHudPrefab(options);
            BuildMenuPrefab(options);
            WireGameSetup(hud);
            WireMenuScene();

            int problems = 0;
            foreach (string path in new[] { OptionsPrefabPath, HudPrefabPath, MenuPrefabPath })
                problems += Validate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            AssetDatabase.SaveAssets();
            Debug.Log("UI builder: done, " + problems + " unassigned references");
            ok = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---- sprites ---------------------------------------------------------------------------------------

    private static void ImportSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            string file = Path.GetFileNameWithoutExtension(path);
            bool icon = path.Contains("/Icons/");
            bool repeat = file == "grid_cell" || file == "dash";
            Vector4 border = file == "shadow_soft" ? new Vector4(40f, 40f, 40f, 40f) : Vector4.zero;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = icon;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadSprite(string relative)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + relative + ".png");
        if (sprite == null)
            throw new Exception("Missing sprite " + relative);
        return sprite;
    }

    // ---- fonts -----------------------------------------------------------------------------------------

    private static readonly string[] FontFiles =
    {
        "Sora/Sora-SemiBold", "Sora/Sora-Bold", "Sora/Sora-ExtraBold",
        "Manrope/Manrope-Medium", "Manrope/Manrope-SemiBold", "Manrope/Manrope-Bold", "Manrope/Manrope-ExtraBold",
    };

    // TMP dynamic SDF font assets, made with TextMesh Pro's own "Create > Font Asset > SDF" code path
    private static List<TMP_FontAsset> CreateFonts()
    {
        Type menu = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TMPro.TMP_FontAsset_CreationMenu"))
            .FirstOrDefault(t => t != null);
        MethodInfo create = menu?.GetMethod("CreateFontAssetFromSelectedObject", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (create == null)
            throw new Exception("TextMesh Pro font asset creation is not available");

        List<TMP_FontAsset> result = new List<TMP_FontAsset>();
        foreach (string file in FontFiles)
        {
            string assetPath = FontFolder + "/" + file + " SDF.asset";
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset == null)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + file + ".ttf");
                if (font == null)
                    throw new Exception("Missing font " + file);
                create.Invoke(null, new object[] { font, GlyphRenderMode.SDFAA });
                AssetDatabase.Refresh();
                asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (asset == null)
                    throw new Exception("Font asset was not created: " + assetPath);
            }
            asset.isMultiAtlasTexturesEnabled = true;
            DropFontFeatures(asset);
            EditorUtility.SetDirty(asset);
            result.Add(asset);
        }
        return result;
    }

    // The font engine imports wrong kerning pairs for Sora and Manrope (e.g. "al" and "ad" pulled together by
    // 0.16 em, records for glyphs the font doesn't have), which made words look squished. The UI fonts go
    // without OpenType kerning, ligature and mark tables instead, and TMP must not import them again.
    private static void DropFontFeatures(TMP_FontAsset asset)
    {
        asset.getFontFeatures = false;
        TMP_FontFeatureTable table = asset.fontFeatureTable;
        table.glyphPairAdjustmentRecords.Clear();
        table.MarkToBaseAdjustmentRecords.Clear();
        table.MarkToMarkAdjustmentRecords.Clear();
        table.ligatureRecords.Clear();
        SerializedObject so = new SerializedObject(asset);
        SerializedProperty reimport = so.FindProperty("m_ShouldReimportFontFeatures");
        if (reimport != null)
            reimport.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        asset.ReadFontAssetDefinition();
    }

    // ---- theme -----------------------------------------------------------------------------------------

    private static UITheme CreateTheme(List<TMP_FontAsset> fonts)
    {
        UITheme theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        bool created = theme == null;
        if (created)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.displaySemiBold = fonts[0];
        theme.displayBold = fonts[1];
        theme.displayExtraBold = fonts[2];
        theme.labelMedium = fonts[3];
        theme.labelSemiBold = fonts[4];
        theme.labelBold = fonts[5];
        theme.labelExtraBold = fonts[6];

        theme.glow = LoadSprite("Decor/glow_radial");
        theme.shadow = LoadSprite("Decor/shadow_soft");
        theme.cornerLine = LoadSprite("Decor/corner_line");
        theme.medalDot = LoadSprite("Decor/medal_dot");
        theme.medalRing = LoadSprite("Decor/medal_ring");
        theme.dotPlain = LoadSprite("Decor/dot_plain");
        theme.circle = LoadSprite("Decor/circle");
        theme.diamond = LoadSprite("Decor/diamond");
        theme.notch = LoadSprite("Decor/notch");
        theme.gridCell = LoadSprite("Decor/grid_cell");
        theme.dash = LoadSprite("Decor/dash");
        theme.fadeVertical = LoadSprite("Decor/fade_vertical");
        theme.fadeHorizontal = LoadSprite("Decor/fade_horizontal");

        theme.icons = new List<UITheme.NamedSprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteFolder + "/Icons" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            theme.icons.Add(new UITheme.NamedSprite { name = Path.GetFileNameWithoutExtension(path), sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
        }
        theme.icons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // Styles: keep tuned values and add styles the asset doesn't know yet, unless the code defaults
        // changed since the asset was written (BevelStyles.Version)
        List<BevelStyleDef> defaults = BevelStyles.CreateDefaults();
        if (created || theme.styles == null || theme.stylesVersion < BevelStyles.Version)
        {
            theme.styles = defaults;
            theme.stylesVersion = BevelStyles.Version;
        }
        else
        {
            foreach (BevelStyleDef def in defaults)
            {
                if (!theme.styles.Exists(s => s != null && s.id == def.id))
                    theme.styles.Add(def);
            }
        }

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        return theme;
    }

    // ---- level catalog ---------------------------------------------------------------------------------

    // Level-select tabs in order. Intermediate, Advanced and Expert have no levels yet.
    private static List<LevelCatalog.Category> LevelCategories => new List<LevelCatalog.Category>
    {
        new LevelCatalog.Category { id = "beginner", displayName = "Beginner", icon = "ic_grid4" },
        new LevelCatalog.Category { id = "intermediate", displayName = "Intermediate", icon = "ic_cube" },
        new LevelCatalog.Category { id = "advanced", displayName = "Advanced", icon = "ic_cluster" },
        new LevelCatalog.Category { id = "expert", displayName = "Expert", icon = "ic_crown" },
        new LevelCatalog.Category { id = "space", displayName = "Space", icon = "ic_planet" },
    };

    private static void CreateLevelCatalog()
    {
        // Names edited in the asset survive; the lane paths are always re-baked from the scenes
        Dictionary<string, LevelCatalog.Level> previous = new Dictionary<string, LevelCatalog.Level>();
        LevelCatalog existing = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (existing != null)
        {
            foreach (LevelCatalog.Level level in existing.levels)
                previous[level.sceneName] = level;
        }

        List<LevelCatalog.Level> levels = new List<LevelCatalog.Level>();
        Dictionary<string, int> perCategory = new Dictionary<string, int>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || buildScene.path == MenuScenePath)
                continue;
            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            string category = sceneName.StartsWith("Beginner") ? "beginner" : "space";
            perCategory.TryGetValue(category, out int count);
            perCategory[category] = ++count;

            LevelCatalog.Level level = new LevelCatalog.Level
            {
                sceneName = sceneName,
                category = category,
                displayName = "Level " + count.ToString("00"),
            };
            if (previous.TryGetValue(sceneName, out LevelCatalog.Level old))
            {
                level.displayName = old.displayName;
                // "prototype" was renamed to "beginner"; other unknown categories fall back to the default
                string oldCategory = old.category == "prototype" ? "beginner" : old.category;
                if (LevelCategories.Exists(c => c.id == oldCategory))
                    level.category = oldCategory;
            }
            BakeLanes(buildScene.path, level);
            levels.Add(level);
        }

        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.categories = LevelCategories;
        catalog.levels = levels;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    // The path every lane's enemies walk (Spawner.GetLanePath), seen from the level's start camera angle
    // (OrbitCamera starts at 45 degrees pitch, no yaw) and normalised to the thumbnail
    private static void BakeLanes(string scenePath, LevelCatalog.Level level)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Spawner spawner = Object.FindAnyObjectByType<Spawner>();
        if (spawner == null)
        {
            Debug.LogWarning("UI builder: no Spawner in " + scenePath);
            return;
        }

        // Waypoints append their tagged children in Awake; do the same here so the lists match play mode
        MethodInfo awake = typeof(Waypoints).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (Waypoints waypoints in Object.FindObjectsByType<Waypoints>(FindObjectsInactive.Include))
            awake.Invoke(waypoints, null);

        Quaternion view = Quaternion.Euler(45f, 0f, 0f);
        Vector3 right = view * Vector3.right;
        Vector3 up = view * Vector3.up;
        List<List<Vector2>> lanes = new List<List<Vector2>>();
        List<Vector3> points = new List<Vector3>();
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int lane = 0; lane < spawner.LaneCount; lane++)
        {
            spawner.GetLanePath(lane, points);
            List<Vector2> projected = new List<Vector2>();
            foreach (Vector3 point in points)
            {
                Vector2 p = new Vector2(Vector3.Dot(point, right), Vector3.Dot(point, up));
                if (projected.Count > 0 && (projected[projected.Count - 1] - p).sqrMagnitude < 0.0001f)
                    continue;
                projected.Add(p);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            lanes.Add(projected);
        }

        Vector2 size = Vector2.Max(max - min, new Vector2(0.5f, 0.5f));
        level.thumbnailAspect = size.x / size.y;
        level.lanes = new List<LevelCatalog.Lane>();
        foreach (List<Vector2> lane in lanes)
        {
            LevelCatalog.Lane baked = new LevelCatalog.Lane();
            foreach (Vector2 p in lane)
                baked.points.Add(new Vector2((p.x - min.x) / size.x, (p.y - min.y) / size.y));
            level.lanes.Add(baked);
        }
    }

    // ---- wiring ----------------------------------------------------------------------------------------

    private static void WireGameSetup(GameObject hudPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameSetupPath);
        try
        {
            foreach (string old in new[] { "Canvas", "HUD Canvas" })
            {
                Transform child = root.transform.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, root.transform);
            hud.name = "HUD Canvas";
            GameManager gameManager = root.GetComponentInChildren<GameManager>(true);
            UIBuild.Set(gameManager, "canvas", hud.transform.Find("Safe Area/HUD").gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, GameSetupPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            // The old Modern UI menu, the unused in-game canvas of the menu scene and earlier builds of the new menu
            bool oldHud = root.name == "Canvas" && root.GetComponent<GameStatDisplay>() != null;
            if (root.name == "MainMenuCanvas" || root.name == "Main Menu Canvas" || oldHud)
                Object.DestroyImmediate(root);
        }

        GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, scene);
        menu.name = "Main Menu Canvas";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---- validation ------------------------------------------------------------------------------------

    // Fields that are allowed to stay empty
    private static readonly HashSet<string> OptionalFields = new HashSet<string>
    {
        "glow", "content", "halo", "crystal", "backButton", "resetProgressButton", "resetProgressLabel", "selectedGlow",
        "outside", "tooltip",
        "m_Material", "m_Sprite", "m_OverrideSprite", "m_Script", "m_TargetGraphic", "m_fontAsset", "m_sharedMaterial",
        "m_baseMaterial", "m_spriteAsset", "m_StyleSheet", "m_TextStyleSheet", "m_textStyle", "m_ParentLinkedComponent",
        "m_HorizontalScrollbar", "m_VerticalScrollbar", "m_FillRect", "m_HandleRect", "m_ColorPreset", "m_CachedStyle",
    };

    private static readonly HashSet<string> OptionRowControls = new HashSet<string> { "segmented", "toggle", "slider", "sliderValue", "dropdown" };

    private static int Validate(GameObject prefab)
    {
        int problems = 0;
        foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                Debug.LogWarning("UI builder: missing script in " + prefab.name);
                problems++;
                continue;
            }
            string ns = behaviour.GetType().Namespace ?? "";
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro"))
                continue;

            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != null)
                    continue;
                string field = property.propertyPath.Split('.')[0];
                // Unity base-class fields (navigation targets, sprite states, ...) are optional
                if (field.StartsWith("m_") || OptionalFields.Contains(field) || OptionalFields.Contains(property.name))
                    continue;
                // An option row needs exactly the control of its kind; a click-catching blocker has no plate
                if (behaviour is OptionRow && OptionRowControls.Contains(field))
                    continue;
                if (behaviour is BevelButton && field == "plate" && behaviour.GetComponent<HitArea>() != null)
                    continue;
                Debug.LogWarning("UI builder: " + prefab.name + "/" + HierarchyPath(behaviour.transform) + " " + behaviour.GetType().Name + "." + property.propertyPath + " is not assigned");
                problems++;
            }
        }
        return problems;
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
