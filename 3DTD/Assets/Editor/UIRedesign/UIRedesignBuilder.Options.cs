using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIBuild;

public static partial class UIRedesignBuilder
{
    private const float ScreenWidth = 784f;
    private const float ScreenHeight = 579f;

    // Options (B4) as a self-contained 784 x 579 plate, used in the main menu and on top of the pause menu
    private static GameObject BuildOptionsPrefab()
    {
        RectTransform root = FloatingPlate(null, "Options Screen", BevelStyle.Plate, 20f, 22f);
        root.Center(0f, 0f, ScreenWidth, ScreenHeight);
        OptionsScreen screen = root.gameObject.AddComponent<OptionsScreen>();
        List<OptionRow> rows = new List<OptionRow>();

        SegmentedControl tabs = TabBar(root, "Tabs", new[] { "General", "Video", "Audio", "Controls" },
            new[] { "ic_sun", "ic_monitor", "ic_speaker", "ic_gamepad" }, 48f, 13f, out _, out _);
        ((RectTransform)tabs.transform).TopLeft(23f, 17f, 525f, 54f);

        // Back is only shown when the screen opens over the game (OptionsScreen hides it in the main menu)
        RectTransform backRect = Node("Back", root).TopRight(23f, 22f, 104f, 44f);
        BevelButton back = Button(backRect, BevelStyle.QuietGlass, BevelStyle.Dropdown, BevelStyle.QuietButtonPressed, BevelStyle.QuietGlass, BevelStyle.QuietGlass, 9f, 1f);
        RectTransform backContent = Content(back);
        backContent.HLayout(8f, 14, 16, 0, 0, TextAnchor.MiddleCenter);
        Icon(backContent, "ic_chevron_left", 16f, T.textMuted);
        Text(backContent, "Label", "Back", UITheme.FontRole.DisplaySemiBold, 13.5f, T.textMuted);
        Hint(backRect, "Back", "Returns to the pause menu. Hotkey: Esc", TooltipService.Side.Left);

        RectTransform pages = Node("Pages", root).Stretch(23f, 93f, 23f, 84f);
        List<GameObject> pageObjects = new List<GameObject>();

        // General
        RectTransform general = Page(pages, "General", out RectTransform generalLeft, out RectTransform generalRight);
        SectionCaption(generalLeft, "Gameplay");
        rows.Add(SwitchRow(generalLeft, "Auto-wave at start", OptionSetting.AutoWaveOnStart));
        rows.Add(SwitchRow(generalLeft, "Range preview on hover", OptionSetting.RangeOnHover));
        SectionCaption(generalRight, "Interface");
        rows.Add(SegmentedRow(generalRight, "HUD scale", OptionSetting.InterfaceScale, new[] { "90%", "100%", "110%" }, 52f));
        SectionCaption(generalRight, "Progress");
        RectTransform resetRow = OptionLine(generalRight, "Saved progress");
        BevelButton resetProgress = QuietGlassButton(resetRow, "Reset Progress", "Reset progress", "ic_recycle", 168f, 40f, out TextMeshProUGUI resetProgressLabel);
        OptionHint(resetRow, "Deletes all medals, Research, unlocked upgrades and the Continue savegame. Click twice to confirm.");
        pageObjects.Add(general.gameObject);

        // Video
        RectTransform video = Page(pages, "Video", out RectTransform videoLeft, out RectTransform videoRight);
        SectionCaption(videoLeft, "Display");
        rows.Add(SegmentedRow(videoLeft, "Window mode", OptionSetting.WindowMode, new[] { "Full", "Border", "Window" }, 62f));
        rows.Add(DropdownRow(videoLeft, "Resolution", OptionSetting.Resolution));
        rows.Add(SegmentedRow(videoLeft, "Frame limit", OptionSetting.FrameLimit, new[] { "30", "60", "120", "Off" }, 42f));
        rows.Add(SwitchRow(videoLeft, "V-Sync", OptionSetting.VSync));
        SectionCaption(videoRight, "Graphics");
        string[] qualityNames = QualitySettings.names;
        string[] qualityLabels = new string[qualityNames.Length];
        for (int i = 0; i < qualityNames.Length; i++)
            qualityLabels[i] = qualityNames[i].Length > 4 ? qualityNames[i].Substring(0, 3) : qualityNames[i];
        rows.Add(SegmentedRow(videoRight, "Quality", OptionSetting.Quality, qualityLabels, 46f));
        rows.Add(SliderRow(videoRight, "Render scale", OptionSetting.RenderScale, 0.5f, 1f));
        rows.Add(SwitchRow(videoRight, "Bloom & glow", OptionSetting.Bloom));
        rows.Add(SegmentedRow(videoRight, "Anti-aliasing", OptionSetting.AntiAliasing, new[] { "Off", "2×", "4×" }, 46f));
        pageObjects.Add(video.gameObject);

        // Audio
        RectTransform audio = Page(pages, "Audio", out RectTransform audioLeft, out RectTransform audioRight);
        SectionCaption(audioLeft, "Volume");
        rows.Add(SliderRow(audioLeft, "Master", OptionSetting.MasterVolume, 0f, 1f));
        rows.Add(SliderRow(audioLeft, "Tower fire", OptionSetting.TowersVolume, 0f, 1f));
        SectionCaption(audioRight, "Note");
        Paragraph(audioRight, "Hint", "Tower fire stays muted behind the main menu so the live backdrop match stays quiet.", 12f, T.textMuted, 333f);
        pageObjects.Add(audio.gameObject);

        // Controls
        RectTransform controls = Page(pages, "Controls", out RectTransform controlsLeft, out RectTransform controlsRight);
        SectionCaption(controlsLeft, "Camera");
        rows.Add(SliderRow(controlsLeft, "Rotate speed", OptionSetting.CameraRotateSpeed, 0.5f, 2f));
        rows.Add(SliderRow(controlsLeft, "Zoom speed", OptionSetting.CameraZoomSpeed, 0.5f, 2f));
        rows.Add(SliderRow(controlsLeft, "Pan speed", OptionSetting.CameraPanSpeed, 0.5f, 2f));
        rows.Add(SwitchRow(controlsLeft, "Invert vertical", OptionSetting.InvertCameraY));
        SectionCaption(controlsRight, "Keys");
        KeyRow(controlsRight, "Pause / back", "Esc");
        KeyRow(controlsRight, "Build hotkeys", "1–9, 0");
        KeyRow(controlsRight, "Sell selected", "Delete");
        KeyRow(controlsRight, "Rotate camera", "Right mouse");
        KeyRow(controlsRight, "Move camera", "WASD  Q E");
        KeyRow(controlsRight, "Reset camera", "Space");
        KeyRow(controlsRight, "Hide HUD", "O");
        pageObjects.Add(controls.gameObject);

        // Bottom bar: Reset to defaults, unsaved count and the Apply CTA
        RectTransform rule = Node("Rule", root).BottomStretch(23f, 81f, 23f, 1f);
        rule.Bevel(BevelStyle.DividerCenter, 0f);
        BevelButton reset = QuietGlassButton(root, "Reset Defaults", "Reset to defaults", "ic_recycle", 177f, 44f, out _);
        ((RectTransform)reset.transform).BottomLeft(23f, 21f, 177f, 44f);
        Hint((RectTransform)reset.transform, "Reset to defaults", "Sets every option on all tabs back to its default. Apply saves it.", TooltipService.Side.Above);
        TextMeshProUGUI unsaved = Text(root, "Unsaved", "All changes saved", UITheme.FontRole.LabelSemiBold, 12f, T.textMuted, TextAlignmentOptions.MidlineRight);
        ((RectTransform)unsaved.transform).BottomRight(175f, 33f, 220f, 20f);
        BevelButton apply = KeyCapButton(root, "Apply", "Apply", "ic_check", 140f, 48f, 12f, 16f, out _);
        Slot(apply).BottomRight(23f, 19f, 140f, 48f);
        Hint((RectTransform)apply.transform, "Apply", "Saves and applies the changed options.", TooltipService.Side.Above);

        Set(screen, "tabs", tabs);
        Set(screen, "pages", pageObjects);
        Set(screen, "rows", rows);
        Set(screen, "applyButton", apply);
        Set(screen, "unsavedLabel", unsaved);
        Set(screen, "resetButton", reset);
        Set(screen, "backButton", back);
        Set(screen, "resetProgressButton", resetProgress);
        Set(screen, "resetProgressLabel", resetProgressLabel);
        return SavePrefab(root.gameObject, OptionsPrefabPath);
    }

    private static RectTransform Page(RectTransform pages, string name, out RectTransform left, out RectTransform right)
    {
        RectTransform page = Node(name, pages).Stretch();
        left = Node("Left", page).TopLeft(0f, 0f, 333f, 404f);
        left.VLayout(0f);
        RectTransform divider = Node("Divider", page).TopLeft(369f, 0f, 1f, 251f);
        divider.Bevel(BevelStyle.DividerVertical, 0f);
        right = Node("Right", page).TopLeft(405f, 0f, 333f, 404f);
        right.VLayout(0f);
        return page;
    }

    private static void SectionCaption(RectTransform column, string text)
    {
        // Later sections in a column keep some air above their caption
        if (column.childCount > 0)
            Spacer(column, -1f, 18f);
        TextMeshProUGUI caption = Caption(column, text, T.accentText);
        LayoutElement element = caption.GetComponent<LayoutElement>();
        element.preferredHeight = element.minHeight = 24f;
        caption.alignment = TextAlignmentOptions.TopLeft;
    }

    // 58 px row: label left, control right, a faint rule below
    private static RectTransform OptionLine(RectTransform column, string label, float height = 58f)
    {
        RectTransform row = Node(label, column);
        row.HLayout(12f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        Layout(row, -1f, height);
        Text(row, "Label", label, UITheme.FontRole.DisplaySemiBold, 14f, T.text, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        RectTransform rule = Node("Rule", row).BottomStretch(0f, 0f, 0f, 1f);
        rule.IgnoreLayout();
        rule.Img(null, BevelStyles.Rgba(200, 195, 255, 0.1f));
        return row;
    }

    private static OptionRow Row(RectTransform line, OptionSetting setting)
    {
        OptionRow row = line.gameObject.AddComponent<OptionRow>();
        Set(row, "setting", (int)setting);
        OptionHint(line, OptionDescription(setting));
        return row;
    }

    // Hover text over the whole row; rows of the left column open it to the right and vice versa
    private static void OptionHint(RectTransform line, string description)
    {
        bool leftColumn = line.parent != null && line.parent.name == "Left";
        string label = line.Find("Label").GetComponent<TextMeshProUGUI>().text;
        Hint(line, label, description, leftColumn ? TooltipService.Side.Right : TooltipService.Side.Left);
    }

    private static string OptionDescription(OptionSetting setting)
    {
        switch (setting)
        {
            case OptionSetting.AutoWaveOnStart: return "Levels start with auto-wave on, so each wave follows the last without pressing Next wave.";
            case OptionSetting.RangeOnHover: return "Shows a tower's range on the map while you hover a build pad with it.";
            case OptionSetting.InterfaceScale: return "Size of the in-game interface. The menus keep their size.";
            case OptionSetting.WindowMode: return "Full: exclusive fullscreen. Border: borderless window at desktop size. Window: a movable window.";
            case OptionSetting.Resolution: return "Screen resolution in fullscreen and window mode.";
            case OptionSetting.FrameLimit: return "Caps the frame rate to save power. Has no effect while V-Sync is on.";
            case OptionSetting.VSync: return "Matches frames to the monitor's refresh rate to avoid tearing.";
            case OptionSetting.Quality: return "Overall graphics quality: shadows, textures and effects.";
            case OptionSetting.RenderScale: return "Renders the 3D view at a lower resolution for more speed. The interface stays sharp.";
            case OptionSetting.Bloom: return "Glow around lasers, explosions and bright lights.";
            case OptionSetting.AntiAliasing: return "Smooths jagged edges (MSAA). Higher costs more performance.";
            case OptionSetting.MasterVolume: return "Overall volume.";
            case OptionSetting.TowersVolume: return "Volume of tower shots and explosions.";
            case OptionSetting.CameraRotateSpeed: return "How fast the camera turns while you hold the right mouse button.";
            case OptionSetting.CameraZoomSpeed: return "How fast the scroll wheel zooms.";
            case OptionSetting.CameraPanSpeed: return "How fast W A S D and Q E move the camera.";
            case OptionSetting.InvertCameraY: return "Inverts up and down while turning the camera.";
            default: return "";
        }
    }

    private static OptionRow SwitchRow(RectTransform column, string label, OptionSetting setting)
    {
        RectTransform line = OptionLine(column, label);
        ToggleSwitch toggle = Switch(line, "Switch");
        OptionRow row = Row(line, setting);
        Set(row, "toggle", toggle);
        return row;
    }

    private static OptionRow SegmentedRow(RectTransform column, string label, OptionSetting setting, string[] items, float itemWidth)
    {
        RectTransform line = OptionLine(column, label);
        SegmentedControl control = Segmented(line, "Segmented", items, itemWidth, 36f);
        OptionRow row = Row(line, setting);
        Set(row, "segmented", control);
        return row;
    }

    private static OptionRow SliderRow(RectTransform column, string label, OptionSetting setting, float min, float max)
    {
        RectTransform line = OptionLine(column, label);
        Slider slider = SliderControl(line, "Slider", min, max, 150f, out TextMeshProUGUI value);
        OptionRow row = Row(line, setting);
        Set(row, "slider", slider);
        Set(row, "sliderValue", value);
        return row;
    }

    private static OptionRow DropdownRow(RectTransform column, string label, OptionSetting setting)
    {
        RectTransform line = OptionLine(column, label);
        SimpleDropdown dropdown = Dropdown(line, "Dropdown", 168f);
        OptionRow row = Row(line, setting);
        Set(row, "dropdown", dropdown);
        return row;
    }

    // Read-only key binding: action left, key cap chip right
    private static void KeyRow(RectTransform column, string action, string keys)
    {
        RectTransform line = OptionLine(column, action, 44f);
        RectTransform chip = Node("Key", line);
        chip.HLayout(0f, 10, 10, 0, 0, TextAnchor.MiddleCenter);
        Layout(chip, -1f, 28f);
        Backdrop(chip, BevelStyle.DarkChip, 5f, false);
        Text(chip, "Keys", keys, UITheme.FontRole.LabelExtraBold, 11.5f, T.segmentText, TextAlignmentOptions.Midline);
    }
}
