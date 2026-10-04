using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIBuild;

// Reusable controls of the redesign, built once here so no screen hand-styles its own buttons
public static partial class UIRedesignBuilder
{
    private static Color Alpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // Orange key-cap CTA with the soft glow sprite behind it (one per screen). The returned button sits
    // stretched inside a slot (button.transform.parent) that holds the glow behind it; place the slot.
    private static BevelButton KeyCapButton(Transform parent, string name, string label, string iconName, float width, float height, float chamfer,
        float fontSize, out TextMeshProUGUI labelText, string subLabel = null, float glowWidth = 0f, float glowHeight = 0f)
    {
        RectTransform slot = Node(name, parent).Size(width, height);
        Layout(slot, width, height);
        Image glow = null;
        if (glowWidth > 0f)
        {
            RectTransform glowRect = Node("Glow", slot).Center(0f, 0f, glowWidth, glowHeight);
            glowRect.IgnoreLayout();
            glow = glowRect.Img(T.glow, Alpha(T.accent, 0.42f), false, false);
        }

        RectTransform rect = Node("Key", slot).Stretch();
        BevelButton button = Button(rect, BevelStyle.KeyCap, BevelStyle.KeyCap, BevelStyle.KeyCapPressed, BevelStyle.KeyCapDisabled, BevelStyle.KeyCap, chamfer, 3f);
        if (glow != null)
            Set(button, "glow", glow);

        RectTransform content = Content(button, 0f, 0f, 0f, 5f);
        RectTransform column = Node("Column", content).Stretch();
        column.VLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
        RectTransform row = Node("Row", column);
        row.HLayout(6f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
        labelText = Text(row, "Label", label, UITheme.FontRole.DisplayExtraBold, fontSize, T.textOnAccent, TextAlignmentOptions.Midline);
        Tint(button, labelText, T.textOnAccent, T.textOnAccent, Alpha(T.text, 0.55f));
        if (iconName != null)
        {
            Image icon = Icon(row, iconName, fontSize, T.textOnAccent);
            Tint(button, icon, T.textOnAccent, T.textOnAccent, Alpha(T.text, 0.55f));
        }
        if (subLabel != null)
        {
            TextMeshProUGUI sub = Text(column, "Sub Label", subLabel, UITheme.FontRole.LabelBold, 10f, Alpha(T.textOnAccent, 0.7f), TextAlignmentOptions.Midline, true, 0.14f);
            Tint(button, sub, Alpha(T.textOnAccent, 0.7f), Alpha(T.textOnAccent, 0.7f), Alpha(T.text, 0.45f));
        }
        return button;
    }

    private static RectTransform Slot(BevelButton keyCap)
    {
        return (RectTransform)keyCap.transform.parent;
    }

    // Content size fitters only where no parent layout group already sizes the element
    private static void FitIfFree(RectTransform rect)
    {
        if (rect.parent == null || rect.parent.GetComponent<LayoutGroup>() == null)
            rect.Fit(true, true);
    }

    // Square glass button with a line icon (stepper arrows, collapse, close)
    private static BevelButton GlassIconButton(Transform parent, string name, string iconName, float size, float chamfer, BevelStyle style = BevelStyle.GlassButton, float iconSize = 16f)
    {
        RectTransform rect = Node(name, parent).Size(size, size);
        Layout(rect, size, size);
        BevelButton button = Button(rect, style, BevelStyle.GlassButton, BevelStyle.GlassButtonPressed, style, style, chamfer, 2f);
        RectTransform content = Content(button);
        Image icon = Icon(content, iconName, iconSize, T.text);
        ((RectTransform)icon.transform).Center(0f, 0f, iconSize, iconSize);
        Tint(button, icon, T.text, T.text, Alpha(T.text, 0.35f));
        MinTouchTarget(rect);
        return button;
    }

    // Segmented control: items in a dark chamfered well, the active one a mini key-cap
    private static SegmentedControl Segmented(Transform parent, string name, string[] labels, float itemWidth, float itemHeight, float fontSize = 12.5f, string leadingIcon = null)
    {
        RectTransform well = Node(name, parent);
        well.HLayout(3f, 3, 3, 3, 3, TextAnchor.MiddleLeft);
        Backdrop(well, BevelStyle.SegmentWell, 9f, false, "Well");
        FitIfFree(well);
        Layout(well, -1f, itemHeight + 6f);

        SegmentedControl control = well.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> items = new List<BevelButton>();
        foreach (string label in labels)
        {
            RectTransform rect = Node(label, well).Size(itemWidth, itemHeight);
            Layout(rect, itemWidth, itemHeight);
            BevelButton item = Button(rect, BevelStyle.SegmentItem, BevelStyle.SegmentItem, BevelStyle.KeyCapPressed, BevelStyle.SegmentItem, BevelStyle.KeyCapMini, 8f, 1f);
            RectTransform content = Content(item, 0f, 0f, 0f, 2f);
            TextMeshProUGUI text = Text(content, "Label", label, UITheme.FontRole.DisplayBold, fontSize, T.segmentText, TextAlignmentOptions.Midline);
            ((RectTransform)text.transform).Stretch();
            Tint(item, text, T.segmentText, T.textOnAccent, Alpha(T.textDim, 0.6f));
            MinTouchTarget(rect);
            items.Add(item);
        }
        Set(control, "items", items);
        return control;
    }

    // Switch: chamfered track with a knob; ToggleSwitch swaps the styles
    private static ToggleSwitch Switch(Transform parent, string name)
    {
        RectTransform rect = Node(name, parent).Size(52f, 30f);
        Layout(rect, 52f, 30f);
        rect.gameObject.AddComponent<CanvasGroup>();
        BevelGraphic track = rect.Bevel(BevelStyle.TrackOff, 7f, BevelShape.Chamfer, true);
        RectTransform knobRect = Node("Knob", rect);
        knobRect.anchorMin = knobRect.anchorMax = new Vector2(0f, 0.5f);
        knobRect.pivot = new Vector2(0f, 0.5f);
        knobRect.sizeDelta = new Vector2(22f, 22f);
        knobRect.anchoredPosition = new Vector2(4f, 0f);
        BevelGraphic knob = knobRect.Bevel(BevelStyle.KnobOff, 5f);
        ToggleSwitch toggle = rect.gameObject.AddComponent<ToggleSwitch>();
        Set(toggle, "track", track);
        Set(toggle, "knob", knob);
        MinTouchTarget(rect);
        return toggle;
    }

    // Slider: dark slanted track, orange gradient fill, hexagon knob, value label on the right
    private static Slider SliderControl(Transform parent, string name, float min, float max, float width, out TextMeshProUGUI valueText)
    {
        RectTransform row = Node(name, parent);
        row.HLayout(12f, 0, 0, 0, 0, TextAnchor.MiddleRight);
        Layout(row, -1f, 30f);

        RectTransform rect = Node("Slider", row).Size(width, 24f);
        Layout(rect, width, 24f);
        RectTransform background = Node("Track", rect);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 4f);
        background.Bevel(BevelStyle.SliderTrack, 3f, BevelShape.SlantRight);

        RectTransform fillArea = Node("Fill Area", rect);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 4f);
        RectTransform fill = Node("Fill", fillArea).Stretch();
        fill.Bevel(BevelStyle.SliderFill, 3f, BevelShape.SlantRight);

        // As tall as the knob, so the slider doesn't stretch the hexagon vertically
        RectTransform handleArea = Node("Handle Area", rect).Stretch(9f, 3f, 9f, 3f);
        RectTransform handle = Node("Knob", handleArea);
        handle.sizeDelta = new Vector2(18f, 18f);
        BevelGraphic knob = handle.Bevel(BevelStyle.SliderKnob, 0f, BevelShape.Hexagon, true);
        MinTouchTarget(rect);

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.transition = UnityEngine.UI.Selectable.Transition.None;
        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;

        valueText = Text(row, "Value", "100%", UITheme.FontRole.DisplayBold, 13f, T.text, TextAlignmentOptions.MidlineRight, false, 0f, 44f);
        return slider;
    }

    private static SimpleDropdown Dropdown(Transform parent, string name, float width)
    {
        RectTransform rect = Node(name, parent).Size(width, 40f);
        Layout(rect, width, 40f);
        SimpleDropdown dropdown = rect.gameObject.AddComponent<SimpleDropdown>();

        RectTransform fieldRect = Node("Field", rect).Stretch();
        BevelButton field = Button(fieldRect, BevelStyle.Dropdown, BevelStyle.Dropdown, BevelStyle.Dropdown, BevelStyle.Dropdown, BevelStyle.Dropdown, 8f, 0f);
        RectTransform fieldContent = Node("Content", fieldRect).Stretch();
        fieldContent.HLayout(8f, 14, 12, 0, 0, TextAnchor.MiddleLeft);
        TextMeshProUGUI label = Text(fieldContent, "Label", "1920 × 1080", UITheme.FontRole.DisplaySemiBold, 13.5f, T.text, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        Icon(fieldContent, "ic_chevron_down", 16f, T.accent);

        // The list lives on its own canvas so it draws above the following rows
        RectTransform popup = Node("Popup", rect);
        popup.anchorMin = new Vector2(0f, 0f);
        popup.anchorMax = new Vector2(1f, 0f);
        popup.pivot = new Vector2(0.5f, 1f);
        popup.anchoredPosition = new Vector2(0f, -4f);
        popup.sizeDelta = new Vector2(0f, 220f);
        Canvas canvas = popup.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 300;
        popup.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform blockerRect = Node("Blocker", popup).Center(0f, 0f, 6000f, 6000f);
        blockerRect.gameObject.AddComponent<HitArea>();
        BevelButton blocker = blockerRect.gameObject.AddComponent<BevelButton>();

        RectTransform plateRect = Node("List Plate", popup).Stretch();
        plateRect.Bevel(BevelStyle.Plate, 8f, BevelShape.Chamfer, true);
        RectTransform viewport = Node("Viewport", plateRect).Stretch(4f, 4f, 4f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform list = Node("List", viewport).TopStretch(0f, 0f, 0f, 0f);
        list.VLayout(0f);
        list.Fit(false, true);
        ScrollRect scroll = plateRect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = list;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        RectTransform itemRect = Node("Item", list).Size(width - 8f, 36f);
        Layout(itemRect, -1f, 36f);
        BevelButton item = Button(itemRect, BevelStyle.None, BevelStyle.SegmentItem, BevelStyle.SegmentItem, BevelStyle.None, BevelStyle.KeyCapMini, 6f, 0f);
        TextMeshProUGUI itemLabel = Text(itemRect, "Label", "1920 × 1080", UITheme.FontRole.DisplaySemiBold, 13f, T.text, TextAlignmentOptions.MidlineLeft);
        ((RectTransform)itemLabel.transform).Stretch(12f, 0f, 8f, 0f);
        Tint(item, itemLabel, T.text, T.textOnAccent, T.textDim);

        Set(dropdown, "field", field);
        Set(dropdown, "label", label);
        Set(dropdown, "popup", popup);
        Set(dropdown, "list", list);
        Set(dropdown, "itemTemplate", item);
        Set(dropdown, "blocker", blocker);
        return dropdown;
    }

    // Primary tabs grouped in a dark well: active = lighter body, 2 px orange underline and orange icon
    private static SegmentedControl TabBar(Transform parent, string name, string[] labels, string[] icons, float height, float fontSize,
        out List<TextMeshProUGUI> labelTexts, out List<Image> iconImages)
    {
        RectTransform well = Node(name, parent);
        well.HLayout(3f, 3, 3, 3, 3, TextAnchor.MiddleLeft);
        Backdrop(well, BevelStyle.TabWell, 11f, false, "Well");
        FitIfFree(well);

        SegmentedControl control = well.gameObject.AddComponent<SegmentedControl>();
        List<BevelButton> items = new List<BevelButton>();
        labelTexts = new List<TextMeshProUGUI>();
        iconImages = new List<Image>();
        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform rect = Node(labels[i], well);
            rect.HLayout(9f, 18, 18, 0, 0, TextAnchor.MiddleCenter);
            Layout(rect, -1f, height);
            BevelButton tab = Button(rect, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.None, BevelStyle.TabActive, 9f, 0f);
            Image icon = Icon(rect, icons[i], 16f, T.tabText);
            TextMeshProUGUI text = Text(rect, "Label", labels[i], UITheme.FontRole.DisplayBold, fontSize, T.tabText, TextAlignmentOptions.MidlineLeft, true, 0.1f);
            Tint(tab, icon, T.tabText, T.accent, Alpha(T.tabText, 0.5f));
            Tint(tab, text, T.tabText, Color.white, Alpha(T.tabText, 0.5f));

            RectTransform underline = Node("Underline", rect).BottomStretch(14f, 7f, 14f, 2f);
            underline.IgnoreLayout();
            underline.Img(null, T.accent);
            Set(tab, "showWhenOn", new List<Object> { underline.gameObject });
            items.Add(tab);
            labelTexts.Add(text);
            iconImages.Add(icon);
        }
        Set(control, "items", items);
        return control;
    }

    // Menu navigation entry: lilac-rimmed glass, active = orange rim, left-to-right orange tint and a chevron
    private static BevelButton NavButton(Transform parent, string label, string iconName, float width, float height)
    {
        RectTransform rect = Node(label, parent).Size(width, height);
        Layout(rect, width, height);
        BevelButton button = Button(rect, BevelStyle.NavButton, BevelStyle.NavButton, BevelStyle.NavButtonPressed, BevelStyle.NavButton, BevelStyle.NavButtonActive, 11f, 2f);
        RectTransform content = Content(button, 0f, 0f, 0f, 3f);
        content.HLayout(10f, 16, 16, 0, 0, TextAnchor.MiddleLeft);
        Image icon = Icon(content, iconName, 19f, T.iconTint);
        TextMeshProUGUI text = Text(content, "Label", label, UITheme.FontRole.DisplaySemiBold, 15f, T.text, TextAlignmentOptions.MidlineLeft, false, 0f, -1f, 1f);
        Image chevron = Icon(content, "ic_chevron_right", 16f, T.accent, "Chevron");
        Tint(button, icon, T.iconTint, T.accent, Alpha(T.iconTint, 0.4f));
        Tint(button, text, T.text, T.text, Alpha(T.text, 0.4f));
        Set(button, "showWhenOn", new List<Object> { chevron.gameObject });
        return button;
    }

    // Quiet secondary button: dark body, faint lilac rim, muted label
    private static BevelButton QuietGlassButton(Transform parent, string name, string label, string iconName, float width, float height, out TextMeshProUGUI labelText)
    {
        RectTransform rect = Node(name, parent).Size(width, height);
        Layout(rect, width, height);
        BevelButton button = Button(rect, BevelStyle.QuietGlass, BevelStyle.Dropdown, BevelStyle.QuietButtonPressed, BevelStyle.QuietGlass, BevelStyle.QuietGlass, 9f, 1f);
        RectTransform content = Content(button);
        content.HLayout(10f, 16, 16, 0, 0, TextAnchor.MiddleCenter);
        if (iconName != null)
        {
            Image icon = Icon(content, iconName, 16f, T.textMuted);
            Tint(button, icon, T.textMuted, T.textMuted, Alpha(T.textMuted, 0.4f));
        }
        labelText = Text(content, "Label", label, UITheme.FontRole.DisplaySemiBold, 13.5f, T.textMuted, TextAlignmentOptions.MidlineLeft);
        Tint(button, labelText, T.textMuted, T.textMuted, Alpha(T.textMuted, 0.4f));
        return button;
    }

    // Scrap price: faceted gem and a tabular number
    private static PriceLabel Price(Transform parent, float gemSize, float fontSize, Color color, out TextMeshProUGUI text, string name = "Price")
    {
        RectTransform rect = Node(name, parent);
        rect.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
        Icon(rect, "fx_gem_scrap", gemSize, Color.white);
        text = Text(rect, "Value", "0", UITheme.FontRole.LabelExtraBold, fontSize, color, TextAlignmentOptions.MidlineLeft);
        PriceLabel price = rect.gameObject.AddComponent<PriceLabel>();
        Set(price, "text", text);
        Set(price, "affordableColor", color);
        return price;
    }

    // Cell of a stat grid: icon + value, caption below
    // tooltipOutside: the panel the cell sits in; its hover text opens beside that panel
    private static StatTile StatCell(Transform parent, string name, float width, float height, RectTransform tooltipOutside = null)
    {
        RectTransform rect = Node(name, parent).Size(width, height);
        Layout(rect, width, height);
        rect.Bevel(BevelStyle.StatCell, 6f);
        rect.VLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter, false);
        RectTransform top = Node("Top", rect);
        top.HLayout(4f, 0, 0, 0, 0, TextAnchor.MiddleCenter);
        Image icon = Icon(top, "ic_burst", 13f, T.accentText);
        TextMeshProUGUI value = Text(top, "Value", "0", UITheme.FontRole.DisplayBold, 14f, Color.white, TextAlignmentOptions.MidlineLeft);
        TextMeshProUGUI caption = Text(rect, "Caption", "Damage", UITheme.FontRole.LabelSemiBold, 9.5f, T.textMuted, TextAlignmentOptions.Midline);
        StatTile tile = rect.gameObject.AddComponent<StatTile>();
        Set(tile, "icon", icon);
        Set(tile, "value", value);
        Set(tile, "caption", caption);
        if (tooltipOutside != null)
            Set(tile, "tooltip", Hint(rect, "", "", TooltipService.Side.Right, tooltipOutside));
        return tile;
    }

    private static MedalRow Medals(Transform parent, float dotSize, float crystalSize, float gap)
    {
        RectTransform rect = Node("Medals", parent);
        rect.HLayout(8f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        RectTransform dots = Node("Dots", rect);
        dots.HLayout(gap, 0, 0, 0, 0, TextAnchor.MiddleLeft);
        List<Object> dotImages = new List<Object>();
        for (int i = 0; i < 3; i++)
        {
            // medal sprites carry a glow margin: the 9 px dot sits in a 21 px image
            RectTransform holder = Node("Dot", dots).Size(dotSize, dotSize);
            Layout(holder, dotSize, dotSize);
            RectTransform art = Node("Art", holder).Center(0f, 0f, dotSize * 84f / 36f, dotSize * 84f / 36f);
            dotImages.Add(art.Img(T.medalDot, Color.white));
        }
        Divider(rect, BevelStyle.DividerVertical, 1f, 16f);
        Image crystal = Icon(rect, "fx_medal_impossible", crystalSize, Color.white);
        MedalRow row = rect.gameObject.AddComponent<MedalRow>();
        Set(row, "dots", dotImages);
        Set(row, "impossible", crystal);
        return row;
    }
}
