using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using static UIBuild;

public static partial class UIRedesignBuilder
{
    // In-game canvas: HUD (B1), tower panel and world selection (B2), build rail (concept C), pause overlay
    private static GameObject BuildHudPrefab(GameObject optionsPrefab)
    {
        RectTransform root = Node("HUD Canvas", null);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = CanvasScaleOption.ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<CanvasScaleOption>();

        // Contrast vignette over the scene (top, bottom and behind the build rail)
        Node("Vignette", root).Stretch().Bevel(BevelStyle.HudVignette, 0f);
        Node("Rail Fade", root).Stretch().Bevel(BevelStyle.HudVignetteRail, 0f);

        RectTransform safe = Node("Safe Area", root).Stretch();
        safe.gameObject.AddComponent<SafeAreaFitter>();
        RectTransform world = Node("World", safe).Stretch();
        RectTransform hud = Node("HUD", safe).Stretch();

        GameStatDisplay stats = root.gameObject.AddComponent<GameStatDisplay>();
        BuildTopBar(hud, stats);
        BuildBottomBar(hud, stats);
        BuildRail(hud, root.gameObject.AddComponent<BuildingManager>());
        BuildTowerPanel(hud, root.gameObject.AddComponent<UpgradePanelManager>());
        BuildWorldTags(world, root.gameObject.AddComponent<SelectionIndicator>(), root.gameObject.AddComponent<PlacementPreview>());
        BuildPauseOverlay(root, root.gameObject.AddComponent<PauseMenu>(), optionsPrefab);
        // Last, so tooltips also show over the pause menu and its options
        RectTransform tooltipSafe = Node("Tooltip Safe Area", root).Stretch();
        tooltipSafe.gameObject.AddComponent<SafeAreaFitter>();
        BuildTooltip(tooltipSafe, root.gameObject.AddComponent<TooltipService>());

        return SavePrefab(root.gameObject, HudPrefabPath);
    }

    // A floating plate that is also a button (pause, collapsed rail tab)
    private static BevelButton FloatingButton(Transform parent, string name, float chamfer, out RectTransform content)
    {
        RectTransform rect = FloatingPlate(parent, name, BevelStyle.Plate, chamfer, 0f);
        BevelButton button = rect.gameObject.AddComponent<BevelButton>();
        Set(button, "plate", rect.Find("Plate").GetComponent<BevelGraphic>());
        Set(button, "normalStyle", (int)BevelStyle.Plate);
        Set(button, "highlightedStyle", (int)BevelStyle.PlateAccent);
        Set(button, "pressedStyle", (int)BevelStyle.PlateSoft);
        Set(button, "disabledStyle", (int)BevelStyle.Plate);
        Set(button, "onStyle", (int)BevelStyle.Plate);
        Set(button, "pressedOffset", 2f);
        HoverMotion(button, BevelStyle.Plate);
        content = Node("Content", rect).Stretch();
        Set(button, "content", content);
        return button;
    }

    private static void BuildTopBar(RectTransform hud, GameStatDisplay stats)
    {
        RectTransform bar = Node("Top Bar", hud).TopLeft(14f, 12f, 430f, 56f);
        bar.HLayout(8f, 0, 0, 0, 0, TextAnchor.UpperLeft);
        bar.Fit(true, false);

        RectTransform resources = FloatingPlate(bar, "Resources", BevelStyle.Plate, 14f, 16f);
        resources.HLayout(0f, 4, 4, 4, 4, TextAnchor.MiddleLeft);
        Layout(resources, -1f, 56f);
        TextMeshProUGUI money = ResourceGroup(resources, "Scrap", "fx_gem_scrap");
        Hint((RectTransform)money.transform.parent.parent, "Scrap", "Buys towers and upgrades. Every popped layer drops scrap, and each cleared wave pays a bonus.");
        Divider(resources, BevelStyle.DividerVertical, 1f, 26f);
        TextMeshProUGUI lives = ResourceGroup(resources, "Hull", "fx_gem_hull");
        Hint((RectTransform)lives.transform.parent.parent, "Hull", "Enemies that reach the exit damage the hull, bigger shapes more. At zero the run is lost.");
        Divider(resources, BevelStyle.DividerVertical, 1f, 26f);
        TextMeshProUGUI round = ResourceGroup(resources, "Wave", "fx_gem_wave");
        Hint((RectTransform)round.transform.parent.parent, "Wave", "Waves cleared so far. Survive the difficulty's last wave to win the level.");

        BevelButton pause = FloatingButton(bar, "Pause", 12f, out RectTransform pauseContent);
        Layout((RectTransform)pause.transform, 56f, 56f);
        Hint((RectTransform)pause.transform, "Pause", "Hotkey: Esc");
        Image pauseIcon = Icon(pauseContent, "ic_pause", 16f, T.text);
        ((RectTransform)pauseIcon.transform).Center(0f, 0f, 16f, 16f);

        Set(stats, "moneyDisplay", money);
        Set(stats, "livesDisplay", lives);
        Set(stats, "roundDisplay", round);
        Set(stats, "pauseButton", pause);
    }

    private static TextMeshProUGUI ResourceGroup(RectTransform plate, string caption, string gem)
    {
        RectTransform group = Node(caption, plate);
        group.HLayout(9f, 14, 14, 0, 0, TextAnchor.MiddleLeft);
        Layout(group, -1f, 48f);
        Icon(group, gem, 30f, Color.white);
        RectTransform column = Node("Column", group);
        column.VLayout(4f);
        TextMeshProUGUI value = Text(column, "Value", "0", UITheme.FontRole.DisplayBold, 19f, T.text, TextAlignmentOptions.MidlineLeft);
        value.GetComponent<LayoutElement>().minWidth = 40f;
        Caption(column, caption, T.textMuted);
        return value;
    }

    private static void BuildBottomBar(RectTransform hud, GameStatDisplay stats)
    {
        // Game speed: x1 / x3 / x5 / x10 in a plate, the active one a mini key-cap
        RectTransform speed = FloatingPlate(hud, "Speed", BevelStyle.Plate, 14f, 16f);
        speed.BottomLeft(14f, 14f, 268f, 58f);
        speed.HLayout(5f, 16, 8, 0, 0, TextAnchor.MiddleLeft);
        Icon(speed, "ic_gauge", 18f, T.textMuted);
        Spacer(speed, 6f, 1f);
        RectTransform items = Node("Items", speed);
        items.HLayout(5f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        SegmentedControl control = items.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> buttons = new List<BevelButton>();
        foreach (string label in new[] { "x1", "x3", "x5", "x10" })
        {
            RectTransform rect = Node(label, items).Size(50f, 40f);
            Layout(rect, 50f, 40f);
            BevelButton item = Button(rect, BevelStyle.SegmentItem, BevelStyle.SegmentItem, BevelStyle.KeyCapPressed, BevelStyle.SegmentItem, BevelStyle.KeyCapMini, 8f, 1f);
            RectTransform content = Content(item, 0f, 0f, 0f, 2f);
            TextMeshProUGUI text = Text(content, "Label", label, UITheme.FontRole.DisplayBold, 15f, T.segmentText, TextAlignmentOptions.Midline);
            ((RectTransform)text.transform).Stretch();
            Tint(item, text, T.segmentText, T.textOnAccent, T.textDim);
            MinTouchTarget(rect);
            Hint(rect, "Game speed " + label.Replace("x", "×"), "", TooltipService.Side.Above);
            buttons.Add(item);
        }
        Set(control, "items", buttons);

        // Next wave: the screen's one key-cap CTA, centred, naming the upcoming wave
        BevelButton nextWave = KeyCapButton(hud, "Next Wave", "Next wave", "ic_chevrons_double", 210f, 68f, 16f, 18f, out _, "Wave 1", 270f, 104f);
        Slot(nextWave).BottomCenter(0f, 15f, 210f, 68f);
        TextMeshProUGUI subLabel = Slot(nextWave).Find("Key/Content/Column/Sub Label").GetComponent<TextMeshProUGUI>();
        Hint((RectTransform)nextWave.transform, "Next wave", "Starts the next wave right away.", TooltipService.Side.Above);

        // Auto-wave switch
        RectTransform auto = FloatingPlate(hud, "Auto Wave", BevelStyle.Plate, 14f, 0f);
        auto.BottomCenter(220f, 14f, 192f, 58f);
        auto.HLayout(12f, 18, 14, 0, 0, TextAnchor.MiddleLeft);
        RectTransform column = Node("Column", auto);
        column.VLayout(4f);
        Layout(column, -1f, -1f, 1f);
        Text(column, "Label", "Auto-wave", UITheme.FontRole.DisplaySemiBold, 14f, T.text);
        TextMeshProUGUI state = Caption(column, "Off", T.textMuted);
        ToggleSwitch toggle = Switch(auto, "Switch");
        Hint(auto, "Auto-wave", "Starts the next wave as soon as the current one is cleared.", TooltipService.Side.Above);

        Set(stats, "speedControl", control);
        Set(stats, "nextWaveButton", nextWave);
        Set(stats, "nextWaveSubLabel", subLabel);
        Set(stats, "autoWaveSwitch", toggle);
        Set(stats, "autoWaveState", state);
    }

    private static void BuildRail(RectTransform hud, BuildingManager manager)
    {
        RectTransform rail = FloatingPlate(hud, "Build Rail", BevelStyle.Plate, 14f, 16f);
        rail.TopRight(14f, 12f, 138f, 483f);
        rail.VLayout(10f, 11, 11, 10, 12);
        rail.Fit(false, true);

        RectTransform header = Node("Header", rail);
        header.HLayout(4f, 4, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(header, -1f, 40f);
        RectTransform titles = Node("Titles", header);
        titles.VLayout(4f);
        Layout(titles, -1f, -1f, 1f);
        Text(titles, "Title", "Build", UITheme.FontRole.DisplayBold, 15f, T.text);
        TextMeshProUGUI count = Caption(titles, "9 towers", T.textMuted, 8.5f);
        BevelButton collapse = GlassIconButton(header, "Collapse", "ic_chevron_right", 40f, 8f);
        Hint((RectTransform)collapse.transform, "Hide build menu", "", TooltipService.Side.Left);

        RectTransform rule = Divider(rail, BevelStyle.DividerCenter, 116f, 1f);
        Layout(rule, -1f, 1f);

        RectTransform grid = Node("Tiles", rail);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(54f, 72f);
        layout.spacing = new Vector2(8f, 10f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        layout.childAlignment = TextAnchor.UpperCenter;

        BuildTile tile = BuildTileTemplate(grid);

        // Collapsed: a slim tab at the right edge
        BevelButton tab = FloatingButton(hud, "Rail Tab", 12f, out RectTransform tabContent);
        ((RectTransform)tab.transform).TopRight(14f, 12f, 44f, 104f);
        tabContent.VLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
        Icon(tabContent, "ic_cube", 18f, T.iconTint);
        Icon(tabContent, "ic_chevron_left", 16f, T.text);
        Hint((RectTransform)tab.transform, "Show build menu", "", TooltipService.Side.Left);

        BuildTooltip tooltip = BuildBuildTooltip(hud);

        Set(manager, "tileParent", grid);
        Set(manager, "tilePrefab", tile);
        Set(manager, "countLabel", count);
        Set(manager, "tooltip", tooltip);
        Set(manager, "expandedRail", rail.gameObject);
        Set(manager, "collapsedTab", tab.gameObject);
        Set(manager, "collapseButton", collapse);
        Set(manager, "expandButton", tab);
    }

    private static BuildTile BuildTileTemplate(RectTransform grid)
    {
        RectTransform root = Node("Tile", grid).Size(54f, 72f);
        root.VLayout(4f, 0, 0, 0, 0, TextAnchor.UpperCenter, false);

        RectTransform rect = Node("Button", root).Size(54f, 54f);
        Layout(rect, 54f, 54f);
        BevelButton button = Button(rect, BevelStyle.Tile, BevelStyle.Tile, BevelStyle.TileSelected, BevelStyle.TileDim, BevelStyle.TileSelected, 9f, 0f);

        RectTransform glow = Node("Glow", rect).Center(0f, 8f, 52f, 52f);
        glow.Img(T.glow, Alpha(T.accent, 0.35f));
        RectTransform imageRect = Node("Icon", rect).Stretch(3f, 3f, 3f, 3f);
        Image image = imageRect.Img(null, Color.white);
        RectTransform fade = Node("Fade", rect).BottomStretch(1f, 1f, 1f, 16f);
        fade.localRotation = Quaternion.Euler(0f, 0f, 180f);
        fade.Img(T.fadeVertical, BevelStyles.Rgba(10, 10, 34, 0.75f), false, false);

        RectTransform badgeRect = Node("Hotkey", rect).TopRight(3f, 3f, 13f, 13f);
        BevelGraphic badge = badgeRect.Bevel(BevelStyle.DarkBadge, 3f);
        TextMeshProUGUI hotkey = Text(badgeRect, "Key", "1", UITheme.FontRole.LabelExtraBold, 9f, T.textMuted, TextAlignmentOptions.Midline);
        ((RectTransform)hotkey.transform).Stretch();

        PriceLabel price = Price(root, 12f, 11.5f, T.text, out _);
        Layout((RectTransform)price.transform, -1f, 14f);

        BuildTile tile = root.gameObject.AddComponent<BuildTile>();
        Set(tile, "button", button);
        Set(tile, "image", image);
        Set(tile, "selectedGlow", glow.GetComponent<Image>());
        Set(tile, "hotkeyBadge", badge);
        Set(tile, "hotkey", hotkey);
        Set(tile, "price", price);
        return tile;
    }

    private static BuildTooltip BuildBuildTooltip(RectTransform hud)
    {
        RectTransform root = Node("Build Tooltip", hud).Stretch();
        BuildTooltip tooltip = root.gameObject.AddComponent<BuildTooltip>();

        RectTransform plate = FloatingPlate(root, "Plate", BevelStyle.PlateAccent, 12f, 14f);
        plate.Center(0f, 0f, 226f, 240f);
        plate.VLayout(10f, 12, 12, 12, 12);
        plate.Fit(false, true);

        RectTransform header = Node("Header", plate);
        header.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform titles = Node("Titles", header);
        titles.VLayout(5f);
        Layout(titles, -1f, -1f, 1f);
        TextMeshProUGUI caption = Caption(titles, "No. 01 · Hotkey 1", T.accentText, 8.5f);
        TextMeshProUGUI title = Text(titles, "Title", "Tower", UITheme.FontRole.DisplayBold, 16f, T.text);
        RectTransform chip = Node("Price Chip", header);
        chip.HLayout(0f, 9, 9, 0, 0, TextAnchor.MiddleCenter);
        Layout(chip, -1f, 30f);
        Backdrop(chip, BevelStyle.DarkChip, 6f, false);
        PriceLabel price = Price(chip, 15f, 14f, T.accentText, out _);

        RectTransform grid = Node("Stats", plate);
        GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(98.5f, 44f);
        layout.spacing = new Vector2(5f, 5f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        Layout(grid, -1f, 93f);
        List<StatTile> tiles = new List<StatTile>();
        for (int i = 0; i < 4; i++)
            tiles.Add(StatCell(grid, "Stat " + i, 98.5f, 44f));

        TextMeshProUGUI description = Paragraph(plate, "Description", "Platform to build towers on.", 12f, T.textMuted);

        RectTransform hint = Node("Hint", plate);
        hint.HLayout(8f, 10, 10, 0, 0, TextAnchor.MiddleLeft);
        Layout(hint, -1f, 34f);
        Backdrop(hint, BevelStyle.HintChip, 7f, false);
        Icon(hint, "ic_crosshair", 15f, T.accent);
        TextMeshProUGUI hintText = Text(hint, "Text", "Click a free pad to place", UITheme.FontRole.LabelSemiBold, 12.5f, T.text);

        RectTransform notch = Node("Notch", root).Size(10f, 18f);
        notch.Img(T.notch, BevelStyles.Rgba(255, 170, 120, 0.9f), false, false);

        Set(tooltip, "plate", plate);
        Set(tooltip, "notch", notch);
        Set(tooltip, "caption", caption);
        Set(tooltip, "title", title);
        Set(tooltip, "price", price);
        Set(tooltip, "statsGrid", grid.gameObject);
        Set(tooltip, "statTiles", tiles);
        Set(tooltip, "description", description);
        Set(tooltip, "hint", hintText);
        root.gameObject.SetActive(false);
        return tooltip;
    }

    private static void BuildTowerPanel(RectTransform hud, UpgradePanelManager manager)
    {
        RectTransform panel = FloatingPlate(hud, "Tower Panel", BevelStyle.Plate, 16f, 18f);
        panel.TopLeft(14f, 76f, 292f, 455f);
        RectTransform viewport = Node("Viewport", panel).Stretch();
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Node("Content", viewport).TopStretch(0f, 0f, 0f, 455f);
        content.VLayout(7f, 12, 12, 12, 12);
        content.Fit(false, true);
        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        // Header: emblem, name, tier pips and kills, close
        RectTransform header = Node("Header", content);
        header.HLayout(10f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(header, -1f, 44f);
        RectTransform emblem = Node("Emblem", header).Size(44f, 44f);
        Layout(emblem, 44f, 44f);
        emblem.Bevel(BevelStyle.StatCell, 9f);
        Node("Glow", emblem).Center(0f, -4f, 44f, 44f).Img(T.glow, Alpha(T.accent, 0.3f));
        Image emblemIcon = Icon(emblem, "fx_emblem_tower", 36f, Color.white);
        ((RectTransform)emblemIcon.transform.parent).Center(0f, 0f, 36f, 36f);

        RectTransform titles = Node("Titles", header);
        titles.VLayout(6f);
        Layout(titles, -1f, -1f, 1f);
        TextMeshProUGUI title = Text(titles, "Title", "Tower", UITheme.FontRole.DisplayBold, 17f, T.text);
        title.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform sub = Node("Sub", titles);
        sub.HLayout(8f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform pips = Node("Pips", sub);
        pips.HLayout(3f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        List<Object> pipImages = new List<Object>();
        for (int i = 0; i < 4; i++)
        {
            RectTransform pip = Node("Pip", pips).Size(9f, 9f);
            Layout(pip, 9f, 9f);
            pipImages.Add(pip.Img(T.diamond, T.accent));
        }
        TextMeshProUGUI subtitle = Text(sub, "Subtitle", "Tier 1 · 0 kills", UITheme.FontRole.LabelSemiBold, 12f, T.textMuted);
        BevelButton close = GlassIconButton(header, "Close", "ic_close", 44f, 8f, BevelStyle.GlassFlat);
        Hint((RectTransform)close.transform, "Close", "Hotkey: Esc", TooltipService.Side.Right, panel);

        // Targeting stepper
        RectTransform targeting = Node("Targeting", content);
        targeting.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(targeting, -1f, 44f);
        BevelButton previous = GlassIconButton(targeting, "Previous", "ic_chevron_left", 44f, 8f);
        RectTransform well = Node("Mode", targeting);
        Layout(well, -1f, 44f, 1f);
        well.Bevel(BevelStyle.StepperWell, 8f);
        well.VLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
        RectTransform modeRow = Node("Row", well);
        modeRow.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
        Icon(modeRow, "ic_crosshair", 14f, T.accent);
        TextMeshProUGUI modeLabel = Text(modeRow, "Label", "First", UITheme.FontRole.DisplayBold, 14f, T.text);
        RectTransform dots = Node("Dots", well);
        // Stepper sets each dot's width (active 16, others 6), so this row only arranges them
        dots.HLayout(3f, 0, 0, 0, 0, TextAnchor.MiddleCenter).childControlWidth = false;
        RectTransform dot = Node("Dot", dots).Size(6f, 4f);
        Layout(dot, -1f, 4f);
        BevelGraphic dotGraphic = dot.Bevel(BevelStyle.DotOff, 2f, BevelShape.Parallelogram);
        BevelButton next = GlassIconButton(targeting, "Next", "ic_chevron_right", 44f, 8f);
        Stepper stepper = targeting.gameObject.AddComponent<Stepper>();
        Set(stepper, "previousButton", previous);
        Set(stepper, "nextButton", next);
        Set(stepper, "label", modeLabel);
        Set(stepper, "dotParent", dots);
        Set(stepper, "dotTemplate", dotGraphic);
        TooltipTrigger targetingTooltip = Hint(well, "Targeting", "", TooltipService.Side.Right, panel);

        // Aim (towers with a configurable firing direction)
        RectTransform aim = Node("Aim", content);
        aim.HLayout(10f, 4, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(aim, -1f, 36f);
        Icon(aim, "ic_rotate", 16f, T.iconTint);
        Text(aim, "Label", "Aim", UITheme.FontRole.LabelSemiBold, 12f, T.textMuted);
        Slider aimSlider = SliderControl(aim, "Aim Slider", 0f, 360f, 190f, out TextMeshProUGUI aimValue);
        Object.DestroyImmediate(aimValue.gameObject);

        // 4 x 2 stat grid in the fixed order of TowerStatInfo.Grid
        RectTransform grid = Node("Stats", content);
        GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(63.25f, 40f);
        gridLayout.spacing = new Vector2(5f, 5f);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        Layout(grid, -1f, 85f);
        List<StatTile> tiles = new List<StatTile>();
        for (int i = 0; i < 8; i++)
            tiles.Add(StatCell(grid, "Stat " + i, 63.25f, 40f, panel));

        // Upgrades
        RectTransform caption = Node("Upgrades Caption", content);
        caption.HLayout(8f, 0, 0, 2, 0, TextAnchor.MiddleLeft);
        Layout(caption, -1f, 14f);
        Caption(caption, "Upgrades", T.textMuted, 9.5f, 0.16f);
        RectTransform captionRule = Divider(caption, BevelStyle.DividerHorizontal, 10f, 1f);
        Layout(captionRule, -1f, 1f, 1f);

        RectTransform list = Node("Upgrade List", content);
        list.VLayout(5f);
        UpgradeRow row = UpgradeRowTemplate(list, panel);

        // Sell: quiet outline button with the refund
        RectTransform sellRect = Node("Sell", content);
        Layout(sellRect, -1f, 40f);
        BevelButton sell = Button(sellRect, BevelStyle.QuietButton, BevelStyle.Dropdown, BevelStyle.QuietButtonPressed, BevelStyle.RowDisabled, BevelStyle.QuietButton, 9f, 1f);
        RectTransform sellContent = Content(sell);
        sellContent.HLayout(5f, 14, 14, 0, 0, TextAnchor.MiddleLeft);
        TextMeshProUGUI sellLabel = Text(sellContent, "Label", "Sell tower", UITheme.FontRole.DisplaySemiBold, 14f, T.text, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        Tint(sell, sellLabel, T.text, T.text, T.textDim);
        RectTransform refund = Node("Refund", sellContent);
        refund.HLayout(5f, 0, 0, 0, 0, TextAnchor.MiddleRight);
        Icon(refund, "fx_gem_scrap", 16f, Color.white);
        TextMeshProUGUI sellValue = Text(refund, "Value", "+0", UITheme.FontRole.LabelExtraBold, 14f, T.accentText, TextAlignmentOptions.MidlineRight);
        TooltipTrigger sellTooltip = Hint(sellRect, "Sell", "", TooltipService.Side.Right, panel);

        Set(manager, "panel", panel);
        Set(manager, "bottomLimit", 80f);
        Set(manager, "content", content);
        Set(manager, "title", title);
        Set(manager, "tierPips", pipImages);
        Set(manager, "subtitle", subtitle);
        Set(manager, "closeButton", close);
        Set(manager, "targetingRow", targeting.gameObject);
        Set(manager, "targetingStepper", stepper);
        Set(manager, "targetingTooltip", targetingTooltip);
        Set(manager, "aimRow", aim.gameObject);
        Set(manager, "aimSlider", aimSlider);
        Set(manager, "statsGrid", grid.gameObject);
        Set(manager, "statTiles", tiles);
        Set(manager, "upgradesCaption", caption.gameObject);
        Set(manager, "upgradeList", list);
        Set(manager, "upgradeRowTemplate", row);
        Set(manager, "sellButton", sell);
        Set(manager, "sellLabel", sellLabel);
        Set(manager, "sellValue", sellValue);
        Set(manager, "sellTooltip", sellTooltip);
    }

    // One upgrade path: its three modules side by side, tier 1 to 3
    private static UpgradeRow UpgradeRowTemplate(RectTransform list, RectTransform panel)
    {
        RectTransform rect = Node("Upgrade Path", list);
        rect.HLayout(5f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(rect, -1f, 52f);
        List<Object> cells = new List<Object>();
        for (int i = 0; i < 3; i++)
            cells.Add(UpgradeCellTemplate(rect, i + 1, panel));
        UpgradeRow row = rect.gameObject.AddComponent<UpgradeRow>();
        Set(row, "cells", cells);
        return row;
    }

    private static UpgradeCell UpgradeCellTemplate(RectTransform row, int tier, RectTransform panel)
    {
        RectTransform rect = Node("Tier " + tier, row);
        Layout(rect, -1f, 52f, 1f);
        BevelButton button = Button(rect, BevelStyle.Row, BevelStyle.RowAccent, BevelStyle.RowAccent, BevelStyle.RowDisabled, BevelStyle.Row, 7f, 1f);
        RectTransform content = Content(button);
        content.VLayout(1f, 8, 6, 5, 5, TextAnchor.UpperLeft);

        // Two lines for the name; longer names end in an ellipsis and the tooltip has the full name
        TextMeshProUGUI title = Text(content, "Title", "Upgrade name", UITheme.FontRole.DisplaySemiBold, 10.5f, T.text, TextAlignmentOptions.TopLeft);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.lineSpacing = -4f;
        LayoutElement titleLayout = title.GetComponent<LayoutElement>();
        titleLayout.preferredHeight = titleLayout.minHeight = 27f;
        titleLayout.flexibleWidth = 1f;

        RectTransform bottom = Node("Bottom", content);
        bottom.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(bottom, -1f, 14f);
        Image icon = Icon(bottom, "ic_cluster", 11f, BevelStyles.Hex("#E7E2FF"));
        Spacer(bottom, -1f, -1f, 1f);
        PriceLabel price = Price(bottom, 12f, 11.5f, T.text, out _);
        RectTransform status = Node("Status", bottom);
        status.HLayout(3f, 0, 0, 0, 0, TextAnchor.MiddleRight);
        Image statusIcon = Icon(status, "ic_check", 10f, T.accentText);
        TextMeshProUGUI statusText = Text(status, "Text", "Owned", UITheme.FontRole.LabelBold, 10f, T.accentText);

        RectTransform track = Node("Progress", rect).BottomStretch(8f, 2f, 8f, 3f);
        track.IgnoreLayout();
        track.Bevel(BevelStyle.ProgressTrack, 2f, BevelShape.SlantRight);
        RectTransform fill = Node("Fill", track);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0.4f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.Bevel(BevelStyle.ProgressFill, 2f, BevelShape.SlantRight);

        TooltipTrigger tooltip = Hint(rect, "", "", TooltipService.Side.Right, panel);

        UpgradeCell cell = rect.gameObject.AddComponent<UpgradeCell>();
        Set(cell, "button", button);
        Set(cell, "title", title);
        Set(cell, "icon", icon);
        Set(cell, "priceGroup", price.gameObject);
        Set(cell, "price", price);
        Set(cell, "statusGroup", status.gameObject);
        Set(cell, "statusIcon", statusIcon);
        Set(cell, "status", statusText);
        Set(cell, "progressTrack", track);
        Set(cell, "progressFill", fill);
        Set(cell, "tooltip", tooltip);
        return cell;
    }

    private static void BuildWorldTags(RectTransform world, SelectionIndicator selection, PlacementPreview placement)
    {
        // "Range X" tag under the selection ring
        RectTransform tag = FloatingPlate(world, "Range Tag", BevelStyle.Plate, 6f, 0f);
        tag.Center(0f, 0f, 96f, 26f);
        tag.HLayout(6f, 10, 10, 0, 0, TextAnchor.MiddleCenter);
        tag.Fit(true, false);
        Icon(tag, "ic_target", 13f, BevelStyles.Hex("#FFA266"));
        TextMeshProUGUI tagText = Text(tag, "Text", "Range 3.5", UITheme.FontRole.LabelBold, 12f, T.text);
        Set(selection, "rangeTag", tag);
        Set(selection, "rangeText", tagText);

        // Placement ghost: the tower's icon in a hexagon frame over the hovered pad
        RectTransform ghost = Node("Ghost Icon", world).Center(0f, 0f, 48f, 42f);
        BevelGraphic frame = ghost.Bevel(BevelStyle.LockBadge, 0f, BevelShape.Hexagon);
        Mask mask = ghost.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        RectTransform art = Node("Art", ghost).Stretch(2f, 0f, 2f, 0f);
        Image image = art.Img(null, Alpha(Color.white, 0.85f));
        Set(placement, "ghostIcon", ghost);
        Set(placement, "iconLift", 24f);
        Set(placement, "ghostImage", image);
    }

    private static void BuildTooltip(RectTransform safe, TooltipService service)
    {
        RectTransform layer = Node("Tooltip Layer", safe).Stretch();
        RectTransform plate = FloatingPlate(layer, "Tooltip", BevelStyle.PlateAccent, 12f, 0f);
        plate.Center(0f, 0f, 240f, 80f);
        plate.VLayout(6f, 12, 12, 11, 12);
        plate.Fit(false, true);
        plate.Find("Plate").GetComponent<BevelGraphic>().raycastTarget = false;
        TextMeshProUGUI title = Text(plate, "Title", "Title", UITheme.FontRole.DisplayBold, 14f, T.text);
        TextMeshProUGUI body = Paragraph(plate, "Body", "Description", 12f, T.textMuted);
        RectTransform notch = Node("Notch", layer).Size(10f, 18f);
        notch.Img(T.notch, BevelStyles.Rgba(255, 170, 120, 0.9f), false, false);
        Set(service, "plate", plate);
        Set(service, "title", title);
        Set(service, "body", body);
        Set(service, "notch", notch);
    }

    private static void BuildPauseOverlay(RectTransform root, PauseMenu pause, GameObject optionsPrefab)
    {
        RectTransform overlay = Node("Pause Overlay", root).Stretch();
        Node("Scrim", overlay).Stretch().Bevel(BevelStyle.Scrim, 0f, BevelShape.Chamfer, true);

        RectTransform plate = FloatingPlate(overlay, "Pause Plate", BevelStyle.Plate, 20f, 22f);
        plate.Center(0f, 0f, 380f, 400f);
        plate.VLayout(12f, 24, 24, 26, 24, TextAnchor.UpperCenter);
        plate.Fit(false, true);
        TextMeshProUGUI title = Text(plate, "Title", "Paused", UITheme.FontRole.DisplayExtraBold, 26f, T.text, TextAlignmentOptions.Midline);
        TextMeshProUGUI subtitle = Text(plate, "Subtitle", "Level 01 · Medium · Wave 0", UITheme.FontRole.LabelSemiBold, 12f, T.textMuted, TextAlignmentOptions.Midline);

        RectTransform reward = Node("Reward", plate);
        reward.HLayout(10f, 0, 0, 2, 2, TextAnchor.MiddleCenter);
        MedalRow medals = Medals(reward, 9f, 22f, 5f);
        TextMeshProUGUI rewardText = Text(reward, "Text", "Hard medal · +3 Research", UITheme.FontRole.LabelBold, 12f, T.accentText);
        Spacer(plate, -1f, 4f);

        BevelButton primary = KeyCapButton(plate, "Primary", "Resume", "ic_play", 332f, 56f, 12f, 17f, out TextMeshProUGUI primaryLabel, null, 380f, 96f);
        BevelButton restart = NavButton(plate, "Restart", "ic_rotate", 332f, 48f);
        BevelButton options = NavButton(plate, "Options", "ic_sliders", 332f, 48f);
        BevelButton menu = NavButton(plate, "Main menu", "ic_home", 332f, 48f);
        Hint((RectTransform)restart.transform, "Restart", "Starts this level again from the first wave.", TooltipService.Side.Right);
        Hint((RectTransform)menu.transform, "Main menu", "Leaves the level. Continue in the main menu resumes from the last cleared wave.", TooltipService.Side.Right);

        GameObject optionsInstance = (GameObject)PrefabUtility.InstantiatePrefab(optionsPrefab, overlay);
        ((RectTransform)optionsInstance.transform).Center(0f, 0f, ScreenWidth, ScreenHeight);

        Set(pause, "menuObjectMain", overlay.gameObject);
        Set(pause, "menuPlate", plate.gameObject);
        Set(pause, "gamePausedText", title);
        Set(pause, "subtitleText", subtitle);
        Set(pause, "primaryButton", primary);
        Set(pause, "primaryLabel", primaryLabel);
        Set(pause, "restartButton", restart);
        Set(pause, "optionsButton", options);
        Set(pause, "mainMenuButton", menu);
        Set(pause, "rewardRow", reward.gameObject);
        Set(pause, "medalRow", medals);
        Set(pause, "rewardText", rewardText);
        Set(pause, "optionsScreen", optionsInstance.GetComponent<OptionsScreen>());
        Set(pause, "audioMixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
    }
}
