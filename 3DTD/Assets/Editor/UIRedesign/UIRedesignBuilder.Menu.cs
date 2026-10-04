using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using static UIBuild;

public static partial class UIRedesignBuilder
{
    // Main menu (B3-B6): brand and navigation on the left, one large content plate on the right,
    // over the live demo match of the menu scene
    private static GameObject BuildMenuPrefab(GameObject optionsPrefab)
    {
        RectTransform root = Node("Main Menu Canvas", null);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = CanvasScaleOption.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        MainMenuScreen menu = root.gameObject.AddComponent<MainMenuScreen>();

        Node("Backdrop", root).Stretch().Bevel(BevelStyle.MenuBackdropLight, 0f);
        RectTransform safe = Node("Safe Area", root).Stretch();
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // Brand
        RectTransform brand = Node("Brand", safe).TopLeft(28f, 26f, 200f, 50f);
        brand.HLayout(12f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Icon(brand, "fx_logo_cube", 48f, Color.white);
        RectTransform wordmark = Node("Wordmark", brand);
        wordmark.VLayout(0f);
        TextMeshProUGUI logo = Text(wordmark, "Logo", "<color=#FFB27A>3D</color>TD", UITheme.FontRole.DisplayExtraBold, 38f, Color.white);
        logo.GetComponent<LayoutElement>().preferredHeight = 38f;
        logo.characterSpacing = -2f;
        Caption(wordmark, "Tower defense", BevelStyles.Rgba(189, 178, 255), 9f, 0.2f);

        // Continue (hidden without a savegame)
        RectTransform continueRect = Node("Continue", safe).TopLeft(28f, 118f, 248f, 58f);
        BevelButton continueButton = Button(continueRect, BevelStyle.ContinueButton, BevelStyle.ContinueButton, BevelStyle.NavButtonPressed, BevelStyle.ContinueButton, BevelStyle.ContinueButton, 12f, 2f);
        RectTransform continueContent = Content(continueButton, 0f, 0f, 0f, 3f);
        continueContent.HLayout(10f, 12, 14, 0, 0, TextAnchor.MiddleLeft);
        RectTransform playBlock = Node("Play", continueContent).Size(30f, 30f);
        Layout(playBlock, 30f, 30f);
        playBlock.Bevel(BevelStyle.KeyCapMini, 6f);
        Image play = Icon(playBlock, "ic_play", 14f, T.textOnAccent);
        ((RectTransform)play.transform).Center(1f, -1f, 14f, 14f);
        RectTransform continueText = Node("Text", continueContent);
        continueText.VLayout(3f);
        Text(continueText, "Label", "Continue", UITheme.FontRole.DisplayBold, 15f, T.text);
        TextMeshProUGUI summary = Text(continueText, "Summary", "Level 01 · Medium · Wave 0", UITheme.FontRole.LabelSemiBold, 11f, T.textMuted);
        Hint(continueRect, "Continue", "Resumes your last run after its last cleared wave.", TooltipService.Side.Right);

        // Navigation
        RectTransform nav = Node("Navigation", safe).TopLeft(28f, 198f, 248f, 219f);
        nav.VLayout(9f);
        SegmentedControl navigation = nav.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> navItems = new List<BevelButton>
        {
            NavButton(nav, "Play a game", "ic_play", 248f, 48f),
            NavButton(nav, "Towers", "ic_cube", 248f, 48f),
            NavButton(nav, "Upgrades", "ic_arrow_up", 248f, 48f),
            NavButton(nav, "Options", "ic_sliders", 248f, 48f),
        };
        Set(navigation, "items", navItems);

        // Exit
        RectTransform exitRect = Node("Exit", safe).BottomLeft(28f, 24f, 140f, 44f);
        BevelButton exit = Button(exitRect, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.None, 0f, 1f);
        RectTransform exitContent = Content(exit);
        exitContent.HLayout(10f, 4, 14, 0, 0, TextAnchor.MiddleLeft);
        Image exitIcon = Icon(exitContent, "ic_exit", 18f, T.textMuted);
        TextMeshProUGUI exitLabel = Text(exitContent, "Label", "Exit game", UITheme.FontRole.DisplaySemiBold, 14f, T.textMuted);

        // Content plates, centred in the space right of the navigation
        RectTransform area = Node("Content Area", safe).Stretch(300f, 16f, 16f, 16f);
        GameObject levelSelect = BuildLevelSelect(area);
        GameObject towers = BuildEncyclopedia(area);
        GameObject upgrades = BuildMetaUpgrades(area);
        GameObject options = (GameObject)PrefabUtility.InstantiatePrefab(optionsPrefab, area);
        ((RectTransform)options.transform).Center(0f, 0f, ScreenWidth, ScreenHeight);

        // Hover text layer, above everything else on the menu canvas
        RectTransform tooltipSafe = Node("Tooltip Safe Area", root).Stretch();
        tooltipSafe.gameObject.AddComponent<SafeAreaFitter>();
        BuildTooltip(tooltipSafe, root.gameObject.AddComponent<TooltipService>());

        Set(menu, "continueButton", continueButton);
        Set(menu, "continueSummary", summary);
        Set(menu, "navigation", navigation);
        Set(menu, "screens", new List<Object> { levelSelect, towers, upgrades, options });
        Set(menu, "exitButton", exit);
        Set(menu, "audioMixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
        return SavePrefab(root.gameObject, MenuPrefabPath);
    }

    private static RectTransform ScreenPlate(RectTransform area, string name)
    {
        RectTransform plate = FloatingPlate(area, name, BevelStyle.Plate, 20f, 22f);
        plate.Center(0f, 0f, ScreenWidth, ScreenHeight);
        return plate;
    }

    // ---- B3 level select -------------------------------------------------------------------------------

    // The card strip's mask reaches this close to the plate's sides; the cards rest at the old 22 px margin
    private const float CardViewportInset = 6f;

    private static GameObject BuildLevelSelect(RectTransform area)
    {
        RectTransform plate = ScreenPlate(area, "Level Select");
        LevelSelectScreen screen = plate.gameObject.AddComponent<LevelSelectScreen>();

        TextMeshProUGUI modeCaption = Caption(plate, "Game mode", T.textMuted);
        ((RectTransform)modeCaption.transform).TopLeft(21f, 34f, 80f, 14f);
        SegmentedControl mode = TabBar(plate, "Game Mode", new[] { "Classic", "Prototype" }, new[] { "ic_cube", "ic_flask" }, 48f, 13f, out _, out _);
        ((RectTransform)mode.transform).TopLeft(103f, 17f, 349f, 54f);
        Hint((RectTransform)mode.Items[0].transform, "Classic", "Clear the waves of each level to earn its medals.");
        Hint((RectTransform)mode.Items[1].transform, "Prototype", "Not available yet.");
        foreach (Transform tab in mode.transform)
        {
            if (tab.GetComponent<BevelButton>() != null)
                Layout((RectTransform)tab, 170f, 48f);
        }

        // Medal totals: Impossible crystals · Easy/Medium/Hard dots
        RectTransform chip = Node("Medal Total", plate).TopRight(21f, 27f, 98f, 34f);
        chip.HLayout(8f, 12, 12, 0, 0, TextAnchor.MiddleCenter);
        chip.Fit(true, false);
        Backdrop(chip, BevelStyle.DarkChip, 7f, false);
        Icon(chip, "fx_medal_impossible", 18f, Color.white);
        TextMeshProUGUI impossible = Text(chip, "Impossible", "0", UITheme.FontRole.DisplayBold, 13f, T.text);
        Divider(chip, BevelStyle.DividerVertical, 1f, 14f);
        MedalDot(chip, 9f);
        TextMeshProUGUI medals = Text(chip, "Medals", "0", UITheme.FontRole.DisplayBold, 13f, T.text);
        Hint(chip, "Medals", "Impossible crystals, then Easy, Medium and Hard medals earned on all levels. The first medal of each difficulty also gives Research.", TooltipService.Side.Left);

        // Category tabs: name with the earned / possible medal count under it, so all five fit in one row
        RectTransform categories = Node("Categories", plate).TopLeft(21f, 85f, 742f, 46f);
        categories.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        categories.Fit(true, false);
        SegmentedControl categoryTabs = categories.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> categoryItems = new List<BevelButton>();
        List<Object> labels = new List<Object>();
        List<Object> counts = new List<Object>();
        List<Object> countDots = new List<Object>();
        string[] names = { "Beginner", "Intermediate", "Advanced", "Expert", "Space" };
        string[] icons = { "ic_grid4", "ic_cube", "ic_cluster", "ic_crown", "ic_planet" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform tab = Node("Category " + i, categories);
            tab.HLayout(9f, 14, 14, 0, 0, TextAnchor.MiddleLeft);
            Layout(tab, -1f, 46f);
            BevelButton button = Button(tab, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.CategoryTabActive, 0f, 0f);
            Image icon = Icon(tab, icons[i], 16f, T.tabText);
            RectTransform text = Node("Text", tab);
            text.VLayout(1f, 0, 0, 0, 0, TextAnchor.MiddleLeft, false);
            TextMeshProUGUI label = Text(text, "Label", names[i], UITheme.FontRole.DisplayBold, 14f, T.tabText, TextAlignmentOptions.Left);
            // Line-metric alignment and fixed heights: Midline and the preferred height follow the glyphs,
            // so labels with and without descenders would sit at different heights
            Layout((RectTransform)label.transform, -1f, 18f);
            RectTransform count = Node("Count", text);
            count.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            Layout(count, -1f, 14f);
            Image dot = MedalDot(count, 6f);
            TextMeshProUGUI countText = Text(count, "Text", "0 / 8", UITheme.FontRole.LabelBold, 10f, T.textMuted);
            RectTransform underline = Node("Underline", tab).BottomStretch(0f, 0f, 0f, 3f);
            underline.IgnoreLayout();
            underline.Img(null, T.accent);
            Tint(button, icon, T.tabText, T.accent, T.tabText);
            Tint(button, label, T.tabText, Color.white, T.tabText);
            Set(button, "showWhenOn", new List<Object> { underline.gameObject });
            categoryItems.Add(button);
            labels.Add(label);
            counts.Add(countText);
            countDots.Add(dot.transform.parent.gameObject);
        }
        Set(categoryTabs, "items", categoryItems);
        Node("Rule", plate).TopLeft(21f, 131f, 742f, 1f).Img(null, BevelStyles.Rgba(200, 195, 255, 0.22f));

        // Cards: a horizontal strip in a masked viewport that spans the plate, dragged with mouse or finger
        // (or the wheel). The edges fade so cards slide out of the frame instead of being cut off.
        RectTransform viewport = Node("Card Viewport", plate).TopLeft(CardViewportInset, 144f, ScreenWidth - 2f * CardViewportInset, 236f);
        viewport.gameObject.AddComponent<HitArea>();
        RectMask2D mask = viewport.gameObject.AddComponent<RectMask2D>();
        mask.softness = new Vector2Int(14, 0);
        RectTransform cards = Node("Cards", viewport);
        cards.anchorMin = new Vector2(0f, 0f);
        cards.anchorMax = new Vector2(0f, 1f);
        cards.pivot = new Vector2(0f, 0.5f);
        cards.offsetMin = Vector2.zero;
        cards.offsetMax = new Vector2(ScreenWidth - 2f * CardViewportInset, 0f);
        int pad = Mathf.RoundToInt(22f - CardViewportInset);
        cards.HLayout(12f, pad, pad, 10, 10, TextAnchor.UpperLeft);
        cards.Fit(true, false);
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = cards;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 40f;
        LevelCard card = LevelCardTemplate(cards);

        TextMeshProUGUI empty = Text(viewport, "Empty", "No levels here yet. Coming soon.", UITheme.FontRole.DisplaySemiBold, 15f, T.textMuted, TextAlignmentOptions.Center);
        ((RectTransform)empty.transform).Stretch();
        empty.raycastTarget = false;

        DifficultyPopup popup = BuildDifficultyPopup(plate);

        // Legend
        RectTransform legend = Node("Legend", plate).TopLeft(22f, 540f, 420f, 18f);
        legend.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Caption(legend, "Medals", T.textMuted);
        Spacer(legend, 6f, 1f);
        foreach (string difficulty in new[] { "Easy", "Medium", "Hard" })
        {
            MedalDot(legend, 9f);
            Text(legend, difficulty, difficulty, UITheme.FontRole.LabelSemiBold, 11.5f, T.textMuted);
            Spacer(legend, 4f, 1f);
        }
        Icon(legend, "fx_medal_impossible", 18f, Color.white);
        Text(legend, "Impossible", "Impossible", UITheme.FontRole.LabelBold, 11.5f, T.impossibleText);

        Set(screen, "gameMode", mode);
        Set(screen, "categoryTabs", categoryTabs);
        Set(screen, "categoryLabels", labels);
        Set(screen, "categoryCounts", counts);
        Set(screen, "categoryCountDots", countDots);
        Set(screen, "impossibleTotal", impossible);
        Set(screen, "medalTotal", medals);
        Set(screen, "cardParent", cards);
        Set(screen, "cardScroll", scroll);
        Set(screen, "emptyState", empty.gameObject);
        Set(screen, "cardTemplate", card);
        Set(screen, "popup", popup);
        return plate.gameObject;
    }

    // Difficulty profiles for the difficulty tiles' hover text, ordered by Difficulty
    private static List<Object> DifficultyProfiles()
    {
        List<DifficultyProfile> found = new List<DifficultyProfile>();
        foreach (string guid in AssetDatabase.FindAssets("t:DifficultyProfile", new[] { "Assets/ScriptableObjects/Difficulty" }))
            found.Add(AssetDatabase.LoadAssetAtPath<DifficultyProfile>(AssetDatabase.GUIDToAssetPath(guid)));
        found.Sort((a, b) => a.difficulty.CompareTo(b.difficulty));
        return new List<Object>(found);
    }

    private static Image MedalDot(Transform parent, float size)
    {
        RectTransform holder = Node("Medal", parent).Size(size, size);
        Layout(holder, size, size);
        RectTransform art = Node("Art", holder).Center(0f, 0f, size * 84f / 36f, size * 84f / 36f);
        return art.Img(T.medalDot, Color.white);
    }

    private static LevelCard LevelCardTemplate(RectTransform cards)
    {
        RectTransform rect = Node("Level Card", cards).Size(236f, 216f);
        Layout(rect, 236f, 216f);
        BevelButton button = Button(rect, BevelStyle.LevelCard, BevelStyle.LevelCard, BevelStyle.LevelCard, BevelStyle.LevelCardLocked, BevelStyle.LevelCardSelected, 14f, 0f);
        RectTransform content = Node("Content", rect).Stretch(10f, 10f, 10f, 8f);
        content.VLayout(10f);

        RectTransform thumb = Node("Thumbnail", content);
        Layout(thumb, -1f, 128f);
        thumb.Bevel(BevelStyle.ThumbWell, 8f);
        RectTransform grid = Node("Grid", thumb).Stretch(1f, 1f, 1f, 1f);
        Image gridImage = grid.Img(T.gridCell, BevelStyles.Rgba(170, 160, 255, 0.07f), false, false);
        gridImage.type = Image.Type.Tiled;
        PathGraphic path = Node("Path", thumb).Stretch(4f, 4f, 4f, 4f).gameObject.AddComponent<PathGraphic>();
        path.raycastTarget = false;
        Set(path, "padding", 10f);

        RectTransform badge = Node("Number", thumb).TopLeft(8f, 8f, 30f, 22f);
        badge.HLayout(0f, 8, 8, 5, 5, TextAnchor.MiddleCenter);
        badge.Fit(true, false);
        BevelGraphic badgeGraphic = badge.Bevel(BevelStyle.DarkBadge, 4f);
        TextMeshProUGUI number = Text(badge, "Text", "01", UITheme.FontRole.DisplayExtraBold, 12f, T.text, TextAlignmentOptions.Midline);

        RectTransform mastered = Node("Mastered", thumb).TopRight(8f, 8f, 76f, 20f);
        mastered.HLayout(0f, 8, 8, 4, 4, TextAnchor.MiddleCenter);
        mastered.Fit(true, false);
        mastered.Bevel(BevelStyle.MasteredTag, 4f);
        Text(mastered, "Text", "Mastered", UITheme.FontRole.LabelExtraBold, 9.5f, Color.white, TextAlignmentOptions.Midline, true, 0.12f);

        RectTransform lockBadge = Node("Lock", thumb).Center(0f, 0f, 46f, 46f);
        lockBadge.Bevel(BevelStyle.LockBadge, 0f, BevelShape.Hexagon);
        Image lockIcon = Icon(lockBadge, "ic_lock", 20f, T.textMuted);
        ((RectTransform)lockIcon.transform).Center(0f, 0f, 20f, 20f);

        RectTransform info = Node("Info", content);
        info.VLayout(9f, 4, 4, 0, 0);
        RectTransform titleRow = Node("Title Row", info);
        titleRow.HLayout(8f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        TextMeshProUGUI title = Text(titleRow, "Title", "Level 01", UITheme.FontRole.DisplayBold, 16f, T.text, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        TextMeshProUGUI medalCount = Text(titleRow, "Medal Count", "0 / 4 medals", UITheme.FontRole.LabelSemiBold, 11f, T.textMuted, TextAlignmentOptions.MidlineRight);
        MedalRow medals = Medals(info, 9f, 22f, 5f);
        RectTransform hint = Node("Unlock Hint", info);
        hint.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Icon(hint, "ic_lock", 12f, T.textDim);
        TextMeshProUGUI hintText = Text(hint, "Text", "Clear Level 01 to unlock", UITheme.FontRole.LabelSemiBold, 11f, T.textDim);

        LevelCard card = rect.gameObject.AddComponent<LevelCard>();
        Set(card, "tooltip", Hint(rect, "", "", TooltipService.Side.Above));
        Set(card, "button", button);
        Set(card, "thumbnail", path);
        Set(card, "numberBadge", badgeGraphic);
        Set(card, "number", number);
        Set(card, "masteredTag", mastered.gameObject);
        Set(card, "lockBadge", lockBadge.gameObject);
        Set(card, "title", title);
        Set(card, "medalCount", medalCount);
        Set(card, "medalRow", medals);
        Set(card, "unlockHint", hint.gameObject);
        Set(card, "unlockHintText", hintText);
        return card;
    }

    private static DifficultyPopup BuildDifficultyPopup(RectTransform plateParent)
    {
        RectTransform root = Node("Difficulty Popup", plateParent).Stretch();
        DifficultyPopup popup = root.gameObject.AddComponent<DifficultyPopup>();

        RectTransform plate = FloatingPlate(root, "Plate", BevelStyle.PlateAccent, 14f, 16f);
        plate.Center(0f, 0f, 660f, 124f);
        plate.HLayout(8f, 14, 24, 15, 15, TextAnchor.MiddleLeft);

        RectTransform captionBlock = Node("Caption", plate);
        captionBlock.VLayout(6f);
        Layout(captionBlock, 64f, -1f);
        TextMeshProUGUI levelCaption = Caption(captionBlock, "Level 01", T.textMuted);
        Text(captionBlock, "Title", "Difficulty", UITheme.FontRole.DisplayBold, 14f, T.text);

        RectTransform tilesRow = Node("Tiles", plate);
        tilesRow.HLayout(8f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        SegmentedControl group = tilesRow.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> buttons = new List<BevelButton>();
        DifficultyPopup.Tile[] tiles = new DifficultyPopup.Tile[4];
        string[] names = { "Easy", "Medium", "Hard", "Impossible" };
        for (int i = 0; i < 4; i++)
        {
            RectTransform rect = Node(names[i], tilesRow).Size(100f, 92f);
            Layout(rect, 100f, 92f);
            BevelButton button = Button(rect, BevelStyle.DifficultyTile, BevelStyle.DifficultyTile, BevelStyle.DifficultyTileSelected, BevelStyle.DifficultyTileLocked, BevelStyle.DifficultyTileSelected, 10f, 0f);
            RectTransform content = Node("Content", rect).Stretch();
            content.VLayout(7f, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);

            DifficultyPopup.Tile tile = new DifficultyPopup.Tile();
            RectTransform marks = Node("Marks", content);
            marks.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            Layout(marks, -1f, 26f);
            List<Image> dots = new List<Image>();
            if (i < 3)
            {
                for (int d = 0; d <= i; d++)
                    dots.Add(MedalDot(marks, 9f));
            }
            else
            {
                tile.crystal = Icon(marks, "fx_medal_impossible_empty", 26f, Color.white);
            }
            tile.dots = dots.ToArray();
            tile.title = Text(content, "Title", names[i], UITheme.FontRole.DisplayBold, 13f, T.text, TextAlignmentOptions.Midline);
            RectTransform status = Node("Status", content);
            status.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
            tile.statusIcon = Icon(status, "ic_check", 11f, T.accentText);
            tile.status = Text(status, "Text", "Medal", UITheme.FontRole.LabelBold, 10f, T.accentText);
            tile.button = button;
            tile.tooltip = Hint(rect, "", "", TooltipService.Side.Below);
            tiles[i] = tile;
            buttons.Add(button);
        }
        Set(group, "items", buttons);
        Spacer(plate, 12f, 1f);

        BevelButton start = KeyCapButton(plate, "Start", "Start", "ic_play", 104f, 92f, 12f, 17f, out _, null, 150f, 140f);

        RectTransform notch = Node("Notch", root).Size(12f, 22f);
        notch.Img(T.notch, BevelStyles.Rgba(255, 170, 120, 0.9f), false, false);

        // Tiles is a list of plain serializable classes: assign field by field
        SerializedObject so = new SerializedObject(popup);
        SerializedProperty list = so.FindProperty("tiles");
        list.arraySize = tiles.Length;
        for (int i = 0; i < tiles.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("button").objectReferenceValue = tiles[i].button;
            SerializedProperty dotList = element.FindPropertyRelative("dots");
            dotList.arraySize = tiles[i].dots.Length;
            for (int d = 0; d < tiles[i].dots.Length; d++)
                dotList.GetArrayElementAtIndex(d).objectReferenceValue = tiles[i].dots[d];
            element.FindPropertyRelative("crystal").objectReferenceValue = tiles[i].crystal;
            element.FindPropertyRelative("title").objectReferenceValue = tiles[i].title;
            element.FindPropertyRelative("statusIcon").objectReferenceValue = tiles[i].statusIcon;
            element.FindPropertyRelative("status").objectReferenceValue = tiles[i].status;
            element.FindPropertyRelative("tooltip").objectReferenceValue = tiles[i].tooltip;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Set(popup, "plate", plate);
        Set(popup, "notch", notch);
        Set(popup, "levelCaption", levelCaption);
        Set(popup, "tileGroup", group);
        Set(popup, "startButton", start);
        Set(popup, "profiles", DifficultyProfiles());
        return popup;
    }

    // ---- B6 tower encyclopedia -------------------------------------------------------------------------

    private static GameObject BuildEncyclopedia(RectTransform area)
    {
        RectTransform plate = ScreenPlate(area, "Towers");
        EncyclopediaScreen screen = plate.gameObject.AddComponent<EncyclopediaScreen>();

        Image book = Icon(plate, "ic_book", 18f, T.accent);
        ((RectTransform)book.transform).TopLeft(19f, 33f, 18f, 18f);
        TextMeshProUGUI heading = Text(plate, "Heading", "Tower encyclopedia", UITheme.FontRole.DisplayBold, 15f, T.text);
        ((RectTransform)heading.transform).TopLeft(45f, 31f, 200f, 20f);

        RectTransform rail = Node("Rail", plate).TopRight(19f, 15f, 520f, 54f);
        rail.HLayout(5f, 0, 0, 0, 0, TextAnchor.MiddleRight);
        RectTransform thumbRect = Node("Thumb", rail).Size(54f, 54f);
        Layout(thumbRect, 54f, 54f);
        BevelButton thumb = Button(thumbRect, BevelStyle.Tile, BevelStyle.Tile, BevelStyle.TileSelected, BevelStyle.TileDim, BevelStyle.TileSelected, 8f, 0f);
        Node("Image", thumbRect).Stretch(4f, 4f, 4f, 4f).Img(null, Color.white);
        Hint(thumbRect, "", "", TooltipService.Side.Below);

        RectTransform pages = Node("Pages", plate).TopLeft(19f, 81f, 746f, 481f);
        pages.Bevel(BevelStyle.PageWell, 14f);
        Node("Fold", pages).TopLeft(353f, 1f, 40f, 479f).Bevel(BevelStyle.PageFold, 0f);

        // Left page: hero, number, name, cost, base stats
        RectTransform hero = Node("Hero", pages).TopLeft(24f, 22f, 120f, 120f);
        hero.Bevel(BevelStyle.HeroFrame, 0f, BevelShape.Hexagon);
        Node("Glow", hero).Center(0f, 8f, 120f, 120f).Img(T.glow, Alpha(T.accent, 0.3f));
        Image heroImage = Node("Render", hero).Center(0f, 0f, 84f, 84f).Img(null, Color.white);
        TextMeshProUGUI number = Caption(pages, "No. 01", T.accentText);
        ((RectTransform)number.transform).TopLeft(164f, 40f, 160f, 14f);
        TextMeshProUGUI title = Text(pages, "Title", "Tower", UITheme.FontRole.DisplayExtraBold, 24f, T.text);
        ((RectTransform)title.transform).TopLeft(164f, 58f, 196f, 30f);
        RectTransform cost = Node("Cost", pages).TopLeft(164f, 96f, 72f, 26f);
        cost.HLayout(5f, 9, 9, 0, 0, TextAnchor.MiddleCenter);
        cost.Fit(true, false);
        Backdrop(cost, BevelStyle.DarkChip, 5f, false);
        Icon(cost, "fx_gem_scrap", 14f, Color.white);
        TextMeshProUGUI costText = Text(cost, "Value", "0", UITheme.FontRole.LabelBold, 12f, T.accentText);

        TextMeshProUGUI baseCaption = Caption(pages, "Base stats", T.textMuted);
        ((RectTransform)baseCaption.transform).TopLeft(18f, 154f, 80f, 14f);
        Node("Rule", pages).TopLeft(95f, 161f, 251f, 1f).Bevel(BevelStyle.DividerHorizontal, 0f);

        RectTransform statsBlock = Node("Stats", pages).TopLeft(18f, 180f, 328f, 160f);
        GridLayoutGroup statsGrid = statsBlock.gameObject.AddComponent<GridLayoutGroup>();
        statsGrid.cellSize = new Vector2(155f, 31f);
        statsGrid.spacing = new Vector2(18f, 6f);
        statsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        statsGrid.constraintCount = 2;
        List<StatMeter> meters = new List<StatMeter>();
        for (int i = 0; i < 8; i++)
            meters.Add(StatMeterView(statsBlock, i));
        TextMeshProUGUI description = Paragraph(pages, "Description", "Platform to build towers on.", 12.5f, T.textMuted, 328f);
        ((RectTransform)description.transform).TopLeft(18f, 180f, 328f, 120f);
        TextMeshProUGUI leftPage = Caption(pages, "p. 1", T.textDim, 8.5f);
        ((RectTransform)leftPage.transform).BottomLeft(18f, 10f, 60f, 12f);

        // Right page: one card per upgrade path
        TextMeshProUGUI pathsCaption = Caption(pages, "Upgrade paths", T.textMuted);
        ((RectTransform)pathsCaption.transform).TopLeft(400f, 13f, 100f, 14f);
        Node("Rule", pages).TopLeft(502f, 20f, 226f, 1f).Bevel(BevelStyle.DividerHorizontal, 0f);
        RectTransform paths = Node("Paths", pages).TopLeft(400f, 35f, 328f, 420f);
        paths.VLayout(9f);
        PathCard pathCard = PathCardTemplate(paths);
        TextMeshProUGUI noPaths = Text(pages, "No Paths", "This tower has no upgrades.", UITheme.FontRole.LabelSemiBold, 12.5f, T.textMuted);
        ((RectTransform)noPaths.transform).TopLeft(400f, 40f, 328f, 20f);
        TextMeshProUGUI rightPage = Caption(pages, "p. 2", T.textDim, 8.5f);
        rightPage.alignment = TextAlignmentOptions.MidlineRight;
        ((RectTransform)rightPage.transform).BottomRight(18f, 10f, 60f, 12f);

        Set(screen, "palette", AssetDatabase.LoadAssetAtPath<GameObject>(GameSetupPath).GetComponentInChildren<GameManager>(true));
        Set(screen, "railParent", rail);
        Set(screen, "thumbTemplate", thumb);
        Set(screen, "heroImage", heroImage);
        Set(screen, "number", number);
        Set(screen, "title", title);
        Set(screen, "cost", costText);
        Set(screen, "statsBlock", statsBlock.gameObject);
        Set(screen, "meters", meters);
        Set(screen, "description", description);
        Set(screen, "leftPageNumber", leftPage);
        Set(screen, "pathParent", paths);
        Set(screen, "pathTemplate", pathCard);
        Set(screen, "noPaths", noPaths);
        Set(screen, "rightPageNumber", rightPage);
        return plate.gameObject;
    }

    private static StatMeter StatMeterView(RectTransform parent, int index)
    {
        RectTransform rect = Node("Meter " + index, parent);
        rect.VLayout(5f);
        RectTransform top = Node("Top", rect);
        top.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Image icon = Icon(top, "ic_burst", 13f, T.accentText);
        TextMeshProUGUI label = Text(top, "Label", "Damage", UITheme.FontRole.LabelSemiBold, 11.5f, T.textMuted, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        TextMeshProUGUI value = Text(top, "Value", "0", UITheme.FontRole.DisplayBold, 13f, Color.white, TextAlignmentOptions.MidlineRight);
        RectTransform segments = Node("Segments", rect);
        segments.HLayout(1.6f, 0, 0, 0, 0, TextAnchor.MiddleLeft, true);
        Layout(segments, -1f, 6f);
        List<Object> parts = new List<Object>();
        for (int i = 0; i < StatMeter.Segments; i++)
        {
            RectTransform segment = Node("Segment", segments);
            Layout(segment, -1f, 6f, 1f);
            parts.Add(segment.Bevel(BevelStyle.MeterOff, 2f, BevelShape.Parallelogram));
        }
        StatMeter meter = rect.gameObject.AddComponent<StatMeter>();
        Set(meter, "tooltip", Hint(rect, "", "", TooltipService.Side.Above));
        Set(meter, "icon", icon);
        Set(meter, "label", label);
        Set(meter, "value", value);
        Set(meter, "segments", parts);
        return meter;
    }

    private static PathCard PathCardTemplate(RectTransform parent)
    {
        RectTransform rect = Node("Path Card", parent);
        rect.VLayout(9f, 12, 12, 11, 12);
        Backdrop(rect, BevelStyle.PathCard, 10f, false);

        RectTransform header = Node("Header", rect);
        header.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform iconBlock = Node("Icon Block", header).Size(34f, 34f);
        Layout(iconBlock, 34f, 34f);
        iconBlock.Bevel(BevelStyle.PathIcon, 7f);
        Image icon = Icon(iconBlock, "ic_cluster", 18f, BevelStyles.Hex("#E7E2FF"));
        ((RectTransform)icon.transform).Center(0f, 0f, 18f, 18f);
        RectTransform titles = Node("Titles", header);
        titles.VLayout(3f);
        Layout(titles, -1f, -1f, 1f);
        TextMeshProUGUI caption = Caption(titles, "Path A", T.textDim, 8.5f);
        TextMeshProUGUI title = Text(titles, "Title", "Upgrade", UITheme.FontRole.DisplayBold, 14.5f, T.text);
        RectTransform priceRow = Node("Price", header);
        priceRow.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleRight);
        Icon(priceRow, "fx_gem_scrap", 16f, Color.white);
        TextMeshProUGUI price = Text(priceRow, "Value", "0", UITheme.FontRole.LabelExtraBold, 14f, T.accentText);

        RectTransform chips = Node("Chips", rect);
        FlowLayout flow = chips.gameObject.AddComponent<FlowLayout>();
        RectTransform chipRect = Node("Chip", chips);
        chipRect.HLayout(5f, 9, 9, 0, 0, TextAnchor.MiddleLeft);
        Layout(chipRect, -1f, 28f);
        Backdrop(chipRect, BevelStyle.StatChip, 5f, false);
        Image chipIcon = Icon(chipRect, "ic_burst", 12f, T.accentText);
        TextMeshProUGUI chipText = Text(chipRect, "Text", "Damage 3 › 4", UITheme.FontRole.LabelSemiBold, 11f, T.text);
        StatChip chip = chipRect.gameObject.AddComponent<StatChip>();
        Set(chip, "icon", chipIcon);
        Set(chip, "text", chipText);

        PathCard card = rect.gameObject.AddComponent<PathCard>();
        Set(card, "icon", icon);
        Set(card, "caption", caption);
        Set(card, "title", title);
        Set(card, "price", price);
        Set(card, "chipParent", chips);
        Set(card, "chipTemplate", chip);
        return card;
    }

    // ---- B5 meta upgrades ------------------------------------------------------------------------------

    private static GameObject BuildMetaUpgrades(RectTransform area)
    {
        RectTransform plate = ScreenPlate(area, "Upgrades");
        MetaUpgradesScreen screen = plate.gameObject.AddComponent<MetaUpgradesScreen>();

        SegmentedControl tabs = TabBar(plate, "Trees", new[] { "Towers", "Economy", "Hull", "Global" },
            new[] { "ic_cube", "ic_coin_hex", "ic_shield", "ic_globe" }, 44f, 12f, out List<TextMeshProUGUI> tabLabels, out List<Image> tabIcons);
        ((RectTransform)tabs.transform).TopLeft(21f, 17f, 491f, 50f);

        RectTransform research = Node("Research", plate).TopRight(21f, 20f, 112f, 44f);
        research.HLayout(8f, 14, 12, 0, 0, TextAnchor.MiddleLeft);
        research.Fit(true, false);
        Backdrop(research, BevelStyle.DarkChipRing, 9f, false);
        Icon(research, "fx_gem_research", 22f, Color.white);
        RectTransform researchText = Node("Text", research);
        researchText.VLayout(1f);
        TextMeshProUGUI researchValue = Text(researchText, "Value", "0", UITheme.FontRole.DisplayExtraBold, 16f, T.text);
        Caption(researchText, "Research", T.textMuted, 8.5f);
        Hint(research, "Research", "Earned with the first medal on each difficulty of a level: 1 for Easy, 2 for Medium, 3 for Hard, 4 for Impossible. Spend it on the upgrades here.", TooltipService.Side.Left);

        // Tree: tier labels, connectors and nodes (placed by MetaUpgradesScreen)
        RectTransform tree = Node("Tree", plate).TopLeft(20f, 84f, 460f, 440f);
        PathGraphic connectors = Node("Connectors", tree).Stretch().gameObject.AddComponent<PathGraphic>();
        connectors.raycastTarget = false;
        Set(connectors, "preserveAspect", false);
        Set(connectors, "padding", 0f);
        float[] tierY = { 50f, 150f, 250f, 362f };
        for (int i = 0; i < 4; i++)
        {
            TextMeshProUGUI tier = Caption(tree, "Tier " + UIFormat.Roman(i + 1), T.textDim);
            ((RectTransform)tier.transform).TopLeft(2f, tierY[i] - 7f, 60f, 14f);
        }
        SkillNode node = SkillNodeTemplate(tree, "Node", 58f, 51f, 22f);
        SkillNode capstone = SkillNodeTemplate(tree, "Capstone", 66f, 58f, 26f);

        // Detail card
        RectTransform card = FloatingPlate(plate, "Detail", BevelStyle.PlateAccent, 14f, 16f);
        card.TopLeft(502f, 80f, 262f, 300f);
        card.VLayout(10f, 16, 16, 16, 16);
        RectTransform header = Node("Header", card);
        header.HLayout(12f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform hex = Node("Hex", header).Size(52f, 46f);
        Layout(hex, 52f, 46f);
        hex.Bevel(BevelStyle.NodeAvailable, 0f, BevelShape.Hexagon);
        Image detailIcon = Icon(hex, "ic_bolt", 22f, Color.white);
        ((RectTransform)detailIcon.transform).Center(0f, 0f, 22f, 22f);
        RectTransform titles = Node("Titles", header);
        titles.VLayout(4f);
        Layout(titles, -1f, -1f, 1f);
        TextMeshProUGUI detailCaption = Caption(titles, "Tier I · Towers", T.textMuted);
        TextMeshProUGUI detailTitle = Text(titles, "Title", "Upgrade", UITheme.FontRole.DisplayBold, 17f, T.text);

        RectTransform effects = Node("Effects", card);
        effects.VLayout(6f);
        EffectRow effect = EffectRowTemplate(effects);

        RectTransform requires = Node("Requires", card);
        requires.HLayout(8f, 0, 0, 2, 0, TextAnchor.MiddleLeft);
        Caption(requires, "Requires", T.textMuted);
        Image requiresIcon = Icon(requires, "ic_check", 12f, T.accentText);
        TextMeshProUGUI requiresText = Text(requires, "Text", "Upgrade", UITheme.FontRole.LabelBold, 12f, T.accentText);

        RectTransform fill = Node("Fill", card);
        Layout(fill, -1f, -1f, -1f, 1f);
        RectTransform buy = Node("Buy", card);
        buy.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform costChip = Node("Cost", buy);
        costChip.HLayout(6f, 12, 12, 0, 0, TextAnchor.MiddleCenter);
        Layout(costChip, -1f, 48f);
        Backdrop(costChip, BevelStyle.DarkChip, 9f, false);
        Icon(costChip, "fx_gem_research", 20f, Color.white);
        TextMeshProUGUI costText = Text(costChip, "Value", "1", UITheme.FontRole.DisplayExtraBold, 17f, T.text);
        BevelButton unlock = KeyCapButton(buy, "Unlock", "Unlock", "ic_check", 140f, 48f, 12f, 16f, out TextMeshProUGUI unlockLabel);

        // Capstone teaser
        RectTransform capstoneCard = FloatingPlate(plate, "Capstone Card", BevelStyle.PlateSoft, 12f, 0f);
        capstoneCard.TopLeft(502f, 394f, 262f, 76f);
        capstoneCard.HLayout(12f, 14, 14, 0, 0, TextAnchor.MiddleLeft);
        RectTransform capHex = Node("Hex", capstoneCard).Size(40f, 35f);
        Layout(capHex, 40f, 35f);
        capHex.Bevel(BevelStyle.NodeCapstone, 0f, BevelShape.Hexagon);
        Image capIcon = Icon(capHex, "ic_crown", 18f, T.iconTint);
        ((RectTransform)capIcon.transform).Center(0f, 0f, 18f, 18f);
        RectTransform capText = Node("Text", capstoneCard);
        capText.VLayout(3f);
        Layout(capText, -1f, -1f, 1f);
        Caption(capText, "Capstone", T.impossibleText);
        TextMeshProUGUI capTitle = Text(capText, "Title", "Capstone", UITheme.FontRole.DisplayBold, 13.5f, T.text);
        TextMeshProUGUI capHint = Text(capText, "Hint", "Needs all Tier III", UITheme.FontRole.LabelSemiBold, 11f, T.textMuted);

        // Tree progress
        RectTransform progress = Node("Progress", plate).TopLeft(22f, 544f, 300f, 14f);
        progress.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        TextMeshProUGUI treeCaption = Caption(progress, "Towers tree", T.textMuted);
        treeCaption.GetComponent<LayoutElement>().minWidth = 80f;
        RectTransform track = Node("Track", progress).Size(120f, 4f);
        Layout(track, 120f, 4f);
        track.Bevel(BevelStyle.ProgressTrack, 0f);
        RectTransform trackFill = Node("Fill", track);
        trackFill.anchorMin = Vector2.zero;
        trackFill.anchorMax = new Vector2(0.25f, 1f);
        trackFill.offsetMin = trackFill.offsetMax = Vector2.zero;
        trackFill.Bevel(BevelStyle.ProgressFill, 0f);
        TextMeshProUGUI progressText = Text(progress, "Count", "0 / 8", UITheme.FontRole.LabelBold, 12f, T.textMuted);

        Set(screen, "tabs", tabs);
        Set(screen, "tabLabels", tabLabels);
        Set(screen, "tabIcons", tabIcons);
        Set(screen, "researchText", researchValue);
        Set(screen, "treeArea", tree);
        Set(screen, "connectors", connectors);
        Set(screen, "nodeTemplate", node);
        Set(screen, "capstoneTemplate", capstone);
        Set(screen, "detailIcon", detailIcon);
        Set(screen, "detailCaption", detailCaption);
        Set(screen, "detailTitle", detailTitle);
        Set(screen, "effectList", effects);
        Set(screen, "effectTemplate", effect);
        Set(screen, "requiresRow", requires.gameObject);
        Set(screen, "requiresText", requiresText);
        Set(screen, "requiresIcon", requiresIcon);
        Set(screen, "costChip", costChip.gameObject);
        Set(screen, "costText", costText);
        Set(screen, "unlockButton", unlock);
        Set(screen, "unlockLabel", unlockLabel);
        Set(screen, "capstoneCard", capstoneCard.gameObject);
        Set(screen, "capstoneTitle", capTitle);
        Set(screen, "capstoneHint", capHint);
        Set(screen, "capstoneIcon", capIcon);
        Set(screen, "treeCaption", treeCaption);
        Set(screen, "progressFill", trackFill);
        Set(screen, "progressText", progressText);
        return plate.gameObject;
    }

    private static SkillNode SkillNodeTemplate(RectTransform tree, string name, float width, float height, float iconSize)
    {
        RectTransform root = Node(name, tree);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(width, height);

        RectTransform halo = Node("Halo", root).Center(0f, 0f, width * 2f, width * 2f);
        Image haloImage = halo.Img(T.glow, Alpha(T.accent, 0.38f));
        RectTransform rect = Node("Hex", root).Stretch();
        BevelButton button = Button(rect, BevelStyle.NodeAvailable, BevelStyle.NodeAvailable, BevelStyle.NodeAvailable, BevelStyle.NodeLocked, BevelStyle.NodeSelected, 0f, 0f, BevelShape.Hexagon);
        Image icon = Icon(rect, "ic_bolt", iconSize, Color.white);
        ((RectTransform)icon.transform).Center(0f, 0f, iconSize, iconSize);
        TextMeshProUGUI label = Text(root, "Label", name, UITheme.FontRole.LabelBold, 11f, T.text, TextAlignmentOptions.Midline);
        ((RectTransform)label.transform).Center(0f, height * 0.5f + 12f, 130f, 14f);

        SkillNode node = root.gameObject.AddComponent<SkillNode>();
        Set(node, "tooltip", Hint(rect, "", "", TooltipService.Side.Right));
        Set(node, "button", button);
        Set(node, "icon", icon);
        Set(node, "label", label);
        Set(node, "halo", haloImage);
        return node;
    }

    private static EffectRow EffectRowTemplate(RectTransform parent)
    {
        RectTransform rect = Node("Effect", parent);
        rect.HLayout(8f, 10, 10, 0, 0, TextAnchor.MiddleLeft);
        Layout(rect, -1f, 38f);
        Backdrop(rect, BevelStyle.EffectRow, 7f, false);
        Image icon = Icon(rect, "ic_bolt", 15f, T.accentText);
        TextMeshProUGUI title = Text(rect, "Title", "Fire rate", UITheme.FontRole.DisplaySemiBold, 13f, T.text);
        TextMeshProUGUI scope = Text(rect, "Scope", "all turrets", UITheme.FontRole.LabelSemiBold, 11f, T.textMuted, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        scope.overflowMode = TextOverflowModes.Ellipsis;
        TextMeshProUGUI value = Text(rect, "Value", "+10%", UITheme.FontRole.DisplayExtraBold, 15f, T.accentText, TextAlignmentOptions.MidlineRight);
        EffectRow row = rect.gameObject.AddComponent<EffectRow>();
        Set(row, "icon", icon);
        Set(row, "title", title);
        Set(row, "scope", scope);
        Set(row, "value", value);
        return row;
    }
}
