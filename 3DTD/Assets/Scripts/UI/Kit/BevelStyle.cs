using System;
using System.Collections.Generic;
using UnityEngine;

// Every look a BevelGraphic can have. The values are the "bevelled glass" tokens from the UI redesign
// guide (tasks/ui-redesign); UITheme stores them so they can be tuned in the inspector.
public enum BevelStyle
{
    None,
    // Plates
    Plate,              // floating panel: lilac-to-orange rim, indigo body, one sheen facet
    PlateAccent,        // tooltip / popup / detail card: orange rim
    PlateSoft,          // secondary card: soft lilac rim
    // Buttons
    KeyCap,             // the one orange CTA per screen
    KeyCapPressed,
    KeyCapDisabled,
    KeyCapMini,         // active item of a segmented control, number badges, icon blocks
    SegmentItem,        // inactive item of a segmented control
    SegmentWell,        // dark well around segmented items
    GlassButton,        // stepper arrows, small square buttons
    GlassButtonPressed,
    GlassFlat,          // close button
    NavButton,          // menu navigation
    NavButtonActive,
    NavButtonPressed,
    ContinueButton,
    QuietButton,        // sell, outline-only actions
    QuietButtonPressed,
    TabWell,
    TabActive,
    CategoryTabActive,
    // Content
    StatCell,
    StepperWell,
    Row,                // upgrade row
    RowAccent,          // hovered / keyboard-focused upgrade row
    RowDisabled,
    Tile,
    TileSelected,
    TileDim,
    HintChip,
    DarkChip,
    DarkBadge,
    MasteredTag,
    TrackOff,
    TrackOn,
    KnobOff,
    KnobOn,
    SliderTrack,
    SliderFill,
    SliderKnob,
    Dropdown,
    LevelCard,
    LevelCardMastered,
    LevelCardSelected,
    LevelCardLocked,
    ThumbWell,
    LockBadge,
    DifficultyTile,
    DifficultyTileSelected,
    DifficultyTileLocked,
    EffectRow,
    PageWell,
    PathCard,
    PathIcon,
    StatChip,
    MeterOn,
    MeterOff,
    NodeOwned,
    NodeAvailable,
    NodeSelected,
    NodeLocked,
    NodeCapstone,
    HeroFrame,
    ProgressTrack,
    ProgressFill,
    MenuBackdrop,
    MenuBackdropLight,
    HudVignette,
    HudVignetteRail,
    Scrim,
    DotOn,
    DotOff,
    QuietGlass,         // secondary text button (Reset to defaults)
    DarkChipRing,       // dark chip with a lilac inset outline (Research counter)
    DividerVertical,    // 1 px line fading out at both ends
    DividerHorizontal,  // 1 px caption rule fading out to the right
    DividerCenter,      // 1 px rule fading out at both ends, horizontal
    PageFold,           // shadow along the fold between the encyclopedia pages
    UpgradeOwned,       // bought upgrade module in the tower panel
    HoverWash,          // hover state of buttons without a plate (tabs, text links)
}

[Serializable]
public struct BevelStop
{
    public Color color;
    [Range(0f, 1f)] public float position;

    public BevelStop(Color color, float position)
    {
        this.color = color;
        this.position = position;
    }
}

// CSS-style linear gradient: angle in degrees (0 = to top, 90 = to right, 180 = to bottom)
[Serializable]
public class BevelGradient
{
    public float angle = 180f;
    public BevelStop[] stops = new BevelStop[0];

    public bool IsVisible
    {
        get
        {
            if (stops == null)
                return false;
            foreach (BevelStop stop in stops)
                if (stop.color.a > 0.001f)
                    return true;
            return false;
        }
    }

    public Color FirstColor => stops != null && stops.Length > 0 ? stops[0].color : Color.clear;

    // Axis so that dot(p - origin, axis) gives the gradient position (0..1) of a point in the rect,
    // matching the length of the CSS gradient line for this angle
    public void GetAxis(Rect rect, out Vector2 origin, out Vector2 axis)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        float length = Mathf.Abs(rect.width * direction.x) + Mathf.Abs(rect.height * direction.y);
        length = Mathf.Max(0.0001f, length);
        origin = rect.center - direction * (length * 0.5f);
        axis = direction / length;
    }

    public Color Evaluate(Rect rect, Vector2 point)
    {
        if (stops == null || stops.Length == 0)
            return Color.clear;
        GetAxis(rect, out Vector2 origin, out Vector2 axis);
        float t = Vector2.Dot(point - origin, axis);
        if (t <= stops[0].position)
            return stops[0].color;
        for (int i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].position)
                return Color.Lerp(stops[i - 1].color, stops[i].color, Mathf.InverseLerp(stops[i - 1].position, stops[i].position, t));
        }
        return stops[stops.Length - 1].color;
    }

    public static BevelGradient Solid(Color color)
    {
        return new BevelGradient { angle = 180f, stops = new[] { new BevelStop(color, 0f) } };
    }

    public static BevelGradient Linear(float angle, params BevelStop[] stops)
    {
        return new BevelGradient { angle = angle, stops = stops };
    }

    public static BevelGradient Vertical(Color top, Color bottom)
    {
        return Linear(180f, new BevelStop(top, 0f), new BevelStop(bottom, 1f));
    }

    // Hard-edged white band over the first part of the width (the single "facet of light")
    public static BevelGradient Sheen(float alpha, float width)
    {
        Color white = new Color(1f, 1f, 1f, alpha);
        return Linear(118f, new BevelStop(white, 0f), new BevelStop(white, width), new BevelStop(Color.clear, width));
    }
}

[Serializable]
public class BevelStyleDef
{
    public BevelStyle id;
    [Tooltip("Rim thickness in reference px; 0 = no rim")]
    public float rimWidth;
    [Tooltip("CSS 'padding' rim: the rim gradient also fills the area under the translucent body. Off = inset outline only.")]
    public bool rimUnderBody = true;
    public BevelGradient rim = new BevelGradient();
    [Tooltip("Body layers, bottom-most first")]
    public BevelGradient[] body = new BevelGradient[0];
    [Tooltip("Hard bottom band (key-cap / glass buttons), in reference px")]
    public float band;
    public Color bandColor;
    public float insetTop;
    public Color insetTopColor;
    public float insetBottom;
    public Color insetBottomColor;

    public BevelStyleDef Clone()
    {
        return (BevelStyleDef)MemberwiseClone();
    }
}

public static class BevelStyles
{
    // Bump when the defaults below change; the UI builder then replaces the styles stored in the theme
    public const int Version = 3;

    public static Color Rgba(int r, int g, int b, float a = 1f)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    public static Color Hex(string hex, float a = 1f)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        color.a = a;
        return color;
    }

    private static BevelStop S(Color color, float position)
    {
        return new BevelStop(color, position);
    }

    // Rim gradients
    private static BevelGradient RimPlate => BevelGradient.Linear(165f, S(Rgba(236, 228, 255, 0.78f), 0f), S(Rgba(150, 138, 230, 0.28f), 0.38f), S(Rgba(84, 76, 170, 0.22f), 0.72f), S(Rgba(255, 138, 61, 0.62f), 1f));
    private static BevelGradient RimSoft => BevelGradient.Linear(165f, S(Rgba(220, 212, 255, 0.38f), 0f), S(Rgba(150, 138, 230, 0.12f), 0.6f), S(Rgba(150, 138, 230, 0.26f), 1f));
    private static BevelGradient RimOrange => BevelGradient.Linear(165f, S(Rgba(255, 200, 160, 0.95f), 0f), S(Rgba(255, 138, 61, 0.3f), 0.55f), S(Rgba(255, 138, 61, 0.85f), 1f));
    private static BevelGradient RimMastered => BevelGradient.Linear(165f, S(Rgba(255, 200, 235, 0.9f), 0f), S(Rgba(190, 90, 220, 0.35f), 0.5f), S(Rgba(255, 138, 61, 0.8f), 1f));

    // Body gradients
    private static BevelGradient BodyPlate => BevelGradient.Vertical(Rgba(48, 47, 112, 0.9f), Rgba(19, 20, 58, 0.93f));
    private static BevelGradient BodyRow => BevelGradient.Vertical(Rgba(56, 54, 124, 0.95f), Rgba(28, 28, 76, 0.96f));
    private static BevelGradient BodyNav => BevelGradient.Vertical(Rgba(48, 47, 112, 0.88f), Rgba(22, 22, 62, 0.9f));
    private static BevelGradient BodyContinue => BevelGradient.Vertical(Rgba(56, 54, 124, 0.95f), Rgba(26, 26, 70, 0.96f));
    private static BevelGradient KeyCapGradient => BevelGradient.Linear(180f, S(Hex("#FFBE92"), 0f), S(Hex("#FF8A3D"), 0.52f), S(Hex("#EC6527"), 1f));
    private static BevelGradient KeyCapMiniGradient => BevelGradient.Linear(180f, S(Hex("#FFBE92"), 0f), S(Hex("#FF8A3D"), 0.55f), S(Hex("#EC6527"), 1f));

    private static BevelStyleDef Def(BevelStyle id, params BevelGradient[] body)
    {
        return new BevelStyleDef { id = id, body = body };
    }

    private static BevelStyleDef WithRim(this BevelStyleDef def, float width, BevelGradient rim, bool underBody = true)
    {
        def.rimWidth = width;
        def.rim = rim;
        def.rimUnderBody = underBody;
        return def;
    }

    private static BevelStyleDef WithBand(this BevelStyleDef def, float height, Color color)
    {
        def.band = height;
        def.bandColor = color;
        return def;
    }

    private static BevelStyleDef WithInset(this BevelStyleDef def, float top, Color topColor, float bottom = 0f, Color bottomColor = default)
    {
        def.insetTop = top;
        def.insetTopColor = topColor;
        def.insetBottom = bottom;
        def.insetBottomColor = bottomColor;
        return def;
    }

    private static BevelStyleDef PlateInsets(this BevelStyleDef def)
    {
        return def.WithInset(1f, new Color(1f, 1f, 1f, 0.16f), 2f, new Color(0f, 0f, 0f, 0.3f));
    }

    public static List<BevelStyleDef> CreateDefaults()
    {
        Color clear = Color.clear;
        Color bandDark = new Color(0f, 0f, 0f, 0.3f);
        Color keyBand = Hex("#9C3A12");

        return new List<BevelStyleDef>
        {
            Def(BevelStyle.Plate, BodyPlate, BevelGradient.Sheen(0.07f, 0.16f)).WithRim(1f, RimPlate).PlateInsets(),
            Def(BevelStyle.PlateAccent, BodyPlate, BevelGradient.Sheen(0.07f, 0.16f)).WithRim(1f, RimOrange).PlateInsets(),
            Def(BevelStyle.PlateSoft, BodyPlate, BevelGradient.Sheen(0.07f, 0.16f)).WithRim(1f, RimSoft).PlateInsets(),

            Def(BevelStyle.KeyCap, KeyCapGradient, BevelGradient.Sheen(0.26f, 0.22f)).WithBand(5f, keyBand).WithInset(1f, new Color(1f, 1f, 1f, 0.7f)),
            Def(BevelStyle.KeyCapPressed, KeyCapGradient, BevelGradient.Sheen(0.2f, 0.22f)).WithBand(2f, keyBand).WithInset(1f, new Color(1f, 1f, 1f, 0.45f)),
            Def(BevelStyle.KeyCapDisabled, BevelGradient.Vertical(Rgba(120, 120, 150), Rgba(78, 78, 108)), BevelGradient.Sheen(0.08f, 0.22f)).WithBand(5f, Rgba(52, 52, 78)).WithInset(1f, new Color(1f, 1f, 1f, 0.2f)),
            Def(BevelStyle.KeyCapMini, KeyCapMiniGradient).WithBand(3f, keyBand).WithInset(1f, new Color(1f, 1f, 1f, 0.65f)),
            Def(BevelStyle.SegmentItem, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.07f))).WithBand(3f, new Color(0f, 0f, 0f, 0.28f)),
            Def(BevelStyle.SegmentWell, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.32f))),
            Def(BevelStyle.GlassButton, BevelGradient.Vertical(new Color(1f, 1f, 1f, 0.14f), new Color(1f, 1f, 1f, 0.05f))).WithBand(3f, bandDark),
            Def(BevelStyle.GlassButtonPressed, BevelGradient.Vertical(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.02f))).WithBand(1f, bandDark),
            Def(BevelStyle.GlassFlat, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.08f))),
            Def(BevelStyle.NavButton, BodyNav, BevelGradient.Sheen(0.06f, 0.18f)).WithRim(1f, RimSoft).WithBand(3f, bandDark),
            Def(BevelStyle.NavButtonActive, BodyContinue, BevelGradient.Linear(90f, S(Rgba(255, 138, 61, 0.24f), 0f), S(Rgba(255, 138, 61, 0.04f), 0.7f))).WithRim(1f, RimOrange),
            Def(BevelStyle.NavButtonPressed, BevelGradient.Vertical(Rgba(36, 35, 90, 0.92f), Rgba(16, 16, 46, 0.94f)), BevelGradient.Sheen(0.04f, 0.18f)).WithRim(1f, RimSoft).WithBand(1f, bandDark),
            Def(BevelStyle.ContinueButton, BodyContinue, BevelGradient.Sheen(0.08f, 0.18f)).WithRim(1f, RimPlate).WithBand(3f, bandDark),
            Def(BevelStyle.QuietButton, BevelGradient.Solid(Rgba(14, 15, 44, 0.85f))).WithRim(1f, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.18f))),
            Def(BevelStyle.QuietButtonPressed, BevelGradient.Solid(Rgba(8, 9, 30, 0.92f))).WithRim(1f, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.12f))),
            Def(BevelStyle.TabWell, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.3f))),
            Def(BevelStyle.TabActive, BevelGradient.Vertical(new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0.08f))).WithBand(3f, new Color(0f, 0f, 0f, 0.25f)),
            Def(BevelStyle.CategoryTabActive, BevelGradient.Vertical(clear, Rgba(255, 138, 61, 0.12f))),

            Def(BevelStyle.StatCell, BevelGradient.Vertical(new Color(1f, 1f, 1f, 0.09f), new Color(1f, 1f, 1f, 0.03f))).WithInset(1f, new Color(1f, 1f, 1f, 0.08f)),
            Def(BevelStyle.StepperWell, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.22f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.12f)), false),
            Def(BevelStyle.Row, BodyRow).WithRim(1f, RimSoft),
            Def(BevelStyle.RowAccent, BodyRow).WithRim(1f, RimOrange),
            Def(BevelStyle.RowDisabled, BevelGradient.Solid(Rgba(18, 18, 52, 0.8f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.16f))),
            Def(BevelStyle.Tile, BevelGradient.Solid(Hex("#14153F"))).WithRim(1f, RimSoft),
            Def(BevelStyle.TileSelected, BevelGradient.Solid(Hex("#14153F"))).WithRim(2f, RimOrange),
            Def(BevelStyle.TileDim, BevelGradient.Solid(Hex("#14153F"))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.14f))),
            Def(BevelStyle.HintChip, BevelGradient.Solid(Rgba(255, 138, 61, 0.12f))).WithRim(1f, BevelGradient.Solid(Rgba(255, 138, 61, 0.35f)), false),
            Def(BevelStyle.DarkChip, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.3f))),
            Def(BevelStyle.DarkBadge, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.5f))),
            Def(BevelStyle.MasteredTag, BevelGradient.Linear(90f, S(Hex("#C04AC8"), 0f), S(Hex("#FF8A3D"), 1f))),
            Def(BevelStyle.TrackOff, BevelGradient.Vertical(new Color(0f, 0f, 0f, 0.35f), new Color(1f, 1f, 1f, 0.06f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.3f)), false),
            Def(BevelStyle.TrackOn, KeyCapMiniGradient).WithBand(3f, keyBand).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.3f)), false),
            Def(BevelStyle.KnobOff, BevelGradient.Vertical(Hex("#E4E2FF"), Hex("#9A97D6"))),
            Def(BevelStyle.KnobOn, BevelGradient.Vertical(Hex("#FFFFFF"), Hex("#FFE0CC"))),
            Def(BevelStyle.SliderTrack, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.4f))),
            Def(BevelStyle.SliderFill, BevelGradient.Linear(90f, S(Hex("#E5622A"), 0f), S(Hex("#FFB98A"), 1f))),
            Def(BevelStyle.SliderKnob, BevelGradient.Vertical(Hex("#FFFFFF"), Hex("#FFC9A0"))),
            Def(BevelStyle.Dropdown, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.3f))).WithRim(1f, RimSoft),
            Def(BevelStyle.LevelCard, BodyRow).WithRim(1f, RimSoft),
            Def(BevelStyle.LevelCardMastered, BodyRow).WithRim(1f, RimMastered),
            Def(BevelStyle.LevelCardSelected, BodyRow).WithRim(2f, RimOrange),
            Def(BevelStyle.LevelCardLocked, BevelGradient.Vertical(Rgba(26, 26, 70, 0.85f), Rgba(16, 16, 48, 0.88f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.18f))),
            Def(BevelStyle.ThumbWell, BevelGradient.Solid(Rgba(8, 9, 34, 0.6f))),
            Def(BevelStyle.LockBadge, BevelGradient.Solid(Rgba(30, 30, 80, 0.95f))),
            Def(BevelStyle.DifficultyTile, BodyRow).WithRim(1f, RimSoft),
            Def(BevelStyle.DifficultyTileSelected, BevelGradient.Linear(180f, S(Rgba(255, 138, 61, 0.2f), 0f), S(Rgba(40, 36, 100, 0.95f), 0.6f))).WithRim(2f, RimOrange),
            Def(BevelStyle.DifficultyTileLocked, BevelGradient.Solid(Rgba(18, 18, 52, 0.8f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.16f))),
            Def(BevelStyle.EffectRow, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.06f))),
            Def(BevelStyle.PageWell, BevelGradient.Vertical(Rgba(30, 30, 84, 0.85f), Rgba(16, 16, 50, 0.9f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.16f)), false),
            Def(BevelStyle.PathCard, BevelGradient.Vertical(Rgba(60, 58, 132, 0.7f), Rgba(30, 30, 80, 0.7f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.16f)), false),
            Def(BevelStyle.PathIcon, BevelGradient.Linear(135f, S(Hex("#4B45A8"), 0f), S(Hex("#4B45A8"), 0.5f), S(Hex("#322E80"), 0.5f), S(Hex("#322E80"), 1f))),
            Def(BevelStyle.StatChip, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.07f))),
            Def(BevelStyle.MeterOn, BevelGradient.Vertical(Hex("#FFB98A"), Hex("#E5622A"))),
            Def(BevelStyle.MeterOff, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.1f))),
            Def(BevelStyle.NodeOwned, BevelGradient.Vertical(Hex("#FFD3B2"), Hex("#E5622A"))),
            Def(BevelStyle.NodeAvailable, BevelGradient.Vertical(Rgba(70, 66, 150, 0.98f), Rgba(34, 32, 90, 0.98f))).WithRim(2f, BevelGradient.Vertical(Rgba(220, 212, 255, 0.9f), Rgba(150, 138, 230, 0.5f))),
            Def(BevelStyle.NodeSelected, BevelGradient.Vertical(Rgba(70, 66, 150, 0.98f), Rgba(34, 32, 90, 0.98f))).WithRim(2f, RimOrange),
            Def(BevelStyle.NodeLocked, BevelGradient.Solid(Rgba(20, 20, 56, 0.92f))).WithRim(2f, BevelGradient.Solid(Rgba(196, 184, 255, 0.22f))),
            Def(BevelStyle.NodeCapstone, BevelGradient.Solid(Rgba(20, 20, 56, 0.92f))).WithRim(2f, BevelGradient.Vertical(Rgba(232, 140, 230, 0.6f), Rgba(255, 138, 61, 0.5f))),
            Def(BevelStyle.HeroFrame, BevelGradient.Vertical(Rgba(120, 90, 130, 0.75f), Rgba(60, 56, 140, 0.6f))).WithRim(2f, BevelGradient.Vertical(Rgba(220, 212, 255, 0.5f), Rgba(150, 138, 230, 0.2f))),
            Def(BevelStyle.ProgressTrack, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.35f))),
            Def(BevelStyle.ProgressFill, BevelGradient.Linear(90f, S(Hex("#E5622A"), 0f), S(Hex("#FFB98A"), 1f))),
            Def(BevelStyle.MenuBackdrop, BevelGradient.Linear(90f, S(Rgba(9, 10, 34, 0.95f), 0f), S(Rgba(9, 10, 34, 0.88f), 0.22f), S(Rgba(9, 10, 34, 0.62f), 0.34f), S(Rgba(9, 10, 34, 0.372f), 1f))),
            Def(BevelStyle.MenuBackdropLight, BevelGradient.Linear(90f, S(Rgba(9, 10, 34, 0.95f), 0f), S(Rgba(9, 10, 34, 0.88f), 0.22f), S(Rgba(9, 10, 34, 0.5f), 0.34f), S(Rgba(9, 10, 34, 0.3f), 1f))),
            Def(BevelStyle.HudVignette, BevelGradient.Linear(180f, S(Rgba(8, 9, 30, 0.35f), 0f), S(Rgba(8, 9, 30, 0f), 0.16f), S(Rgba(8, 9, 30, 0f), 0.78f), S(Rgba(8, 9, 30, 0.45f), 1f))),
            Def(BevelStyle.HudVignetteRail, BevelGradient.Linear(270f, S(Rgba(8, 9, 30, 0.45f), 0f), S(Rgba(8, 9, 30, 0f), 0.22f))),
            Def(BevelStyle.Scrim, BevelGradient.Solid(Rgba(6, 7, 24, 0.74f))),
            Def(BevelStyle.DotOn, BevelGradient.Solid(Hex("#FF8A3D"))),
            Def(BevelStyle.DotOff, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.25f))),
            Def(BevelStyle.QuietGlass, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.25f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.2f))),
            Def(BevelStyle.DarkChipRing, BevelGradient.Solid(new Color(0f, 0f, 0f, 0.3f))).WithRim(1f, BevelGradient.Solid(Rgba(196, 184, 255, 0.2f)), false),
            Def(BevelStyle.DividerVertical, BevelGradient.Linear(180f, S(Rgba(196, 184, 255, 0f), 0f), S(Rgba(196, 184, 255, 0.35f), 0.5f), S(Rgba(196, 184, 255, 0f), 1f))),
            Def(BevelStyle.DividerHorizontal, BevelGradient.Linear(90f, S(Rgba(196, 184, 255, 0.35f), 0f), S(Rgba(196, 184, 255, 0f), 1f))),
            Def(BevelStyle.PageFold, BevelGradient.Linear(90f, S(new Color(0f, 0f, 0f, 0f), 0f), S(new Color(0f, 0f, 0f, 0.35f), 0.45f), S(new Color(1f, 1f, 1f, 0.08f), 0.5f), S(new Color(0f, 0f, 0f, 0.3f), 0.55f), S(new Color(0f, 0f, 0f, 0f), 1f))),
            Def(BevelStyle.UpgradeOwned, BevelGradient.Linear(180f, S(Rgba(255, 138, 61, 0.3f), 0f), S(Rgba(56, 40, 104, 0.95f), 0.75f))).WithRim(1f, RimOrange),
            Def(BevelStyle.HoverWash, BevelGradient.Solid(new Color(1f, 1f, 1f, 0.06f))),
            Def(BevelStyle.DividerCenter, BevelGradient.Linear(90f, S(Rgba(196, 184, 255, 0f), 0f), S(Rgba(196, 184, 255, 0.35f), 0.5f), S(Rgba(196, 184, 255, 0f), 1f))),
        };
    }
}
