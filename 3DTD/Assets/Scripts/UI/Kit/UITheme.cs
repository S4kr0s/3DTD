using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Design tokens of the "bevelled glass" UI: colours, fonts, sprites, icons and bevel styles.
// One asset lives in Resources (UI/UITheme) so graphics can look their style up at runtime and in the editor.
[CreateAssetMenu(fileName = "UITheme", menuName = "TowerDefense/UI Theme", order = 10)]
public class UITheme : ScriptableObject
{
    public const string ResourcePath = "UI/UITheme";

    [Serializable]
    public struct NamedSprite
    {
        public string name;
        public Sprite sprite;
    }

    public enum FontRole
    {
        DisplaySemiBold,    // Sora 600
        DisplayBold,        // Sora 700
        DisplayExtraBold,   // Sora 800
        LabelMedium,        // Manrope 500
        LabelSemiBold,      // Manrope 600
        LabelBold,          // Manrope 700
        LabelExtraBold,     // Manrope 800
    }

    [Header("Colours")]
    public Color ground = BevelStyles.Hex("#0B0D2A");
    public Color accent = BevelStyles.Hex("#FF8A3D");
    public Color accentLight = BevelStyles.Hex("#FFBE92");
    public Color accentDark = BevelStyles.Hex("#EC6527");
    public Color accentBand = BevelStyles.Hex("#9C3A12");
    public Color textOnAccent = BevelStyles.Hex("#2A0E02");
    public Color accentText = BevelStyles.Hex("#FFB98A");
    public Color text = BevelStyles.Hex("#F3F2FF");
    public Color textMuted = BevelStyles.Hex("#B4B4E4");
    public Color textDim = BevelStyles.Hex("#8A8BC0");
    public Color textLocked = BevelStyles.Hex("#9B9CCF");
    public Color segmentText = BevelStyles.Hex("#DCDDFF");
    public Color tabText = BevelStyles.Hex("#A9AAD8");
    public Color cantAfford = BevelStyles.Hex("#FF7088");
    public Color iconTint = BevelStyles.Hex("#CFC5FF");
    public Color impossibleText = BevelStyles.Hex("#F3B8FF");
    public Color divider = BevelStyles.Rgba(196, 184, 255, 0.35f);
    public Color dropShadow = BevelStyles.Rgba(2, 3, 18, 0.55f);

    [Header("Fonts (TextMesh Pro)")]
    public TMP_FontAsset displaySemiBold;
    public TMP_FontAsset displayBold;
    public TMP_FontAsset displayExtraBold;
    public TMP_FontAsset labelMedium;
    public TMP_FontAsset labelSemiBold;
    public TMP_FontAsset labelBold;
    public TMP_FontAsset labelExtraBold;
    [Tooltip("Digit slot in em for tabular figures (<mspace>) in Sora: close to its typical digit, its round zero is wider")]
    public float tabularDigitWidth = 0.66f;
    [Tooltip("Digit slot in em for tabular figures in Manrope (prices)")]
    public float tabularLabelDigitWidth = 0.6f;

    [Header("Sprites")]
    public Sprite glow;
    public Sprite shadow;
    public Sprite cornerLine;
    public Sprite medalDot;
    public Sprite medalRing;
    [Tooltip("White dot with the medal dot's geometry, for tinted difficulty dots")]
    public Sprite dotPlain;
    public Sprite circle;
    public Sprite diamond;
    public Sprite notch;
    public Sprite gridCell;
    public Sprite dash;
    public Sprite fadeVertical;
    public Sprite fadeHorizontal;
    [Tooltip("Faceted icon sprites carry a margin for their glow; an icon drawn at size s uses an image of s * this")]
    public float facetSpriteScale = 1.8f;
    public List<NamedSprite> icons = new List<NamedSprite>();

    [Header("Bevel styles")]
    public List<BevelStyleDef> styles = new List<BevelStyleDef>();
    [Tooltip("BevelStyles.Version the styles were written from")]
    public int stylesVersion;

    private static UITheme current;
    private Dictionary<string, Sprite> iconLookup;
    private Dictionary<BevelStyle, BevelStyleDef> styleLookup;
    private static Dictionary<BevelStyle, BevelStyleDef> fallbackStyles;

    public static UITheme Current
    {
        get
        {
            if (current == null)
                current = Resources.Load<UITheme>(ResourcePath);
            return current;
        }
    }

    private void OnValidate()
    {
        iconLookup = null;
        styleLookup = null;
    }

    public Sprite Icon(string iconName)
    {
        if (iconLookup == null)
        {
            iconLookup = new Dictionary<string, Sprite>();
            foreach (NamedSprite entry in icons)
            {
                if (!string.IsNullOrEmpty(entry.name) && entry.sprite != null)
                    iconLookup[entry.name] = entry.sprite;
            }
        }
        return iconName != null && iconLookup.TryGetValue(iconName, out Sprite sprite) ? sprite : null;
    }

    public BevelStyleDef Style(BevelStyle id)
    {
        if (styleLookup == null)
        {
            styleLookup = new Dictionary<BevelStyle, BevelStyleDef>();
            foreach (BevelStyleDef def in styles)
            {
                if (def != null)
                    styleLookup[def.id] = def;
            }
        }
        if (styleLookup.TryGetValue(id, out BevelStyleDef found))
            return found;
        return FallbackStyle(id);
    }

    // Code defaults, used when the theme asset is missing or doesn't know a style yet
    public static BevelStyleDef FallbackStyle(BevelStyle id)
    {
        if (fallbackStyles == null)
        {
            fallbackStyles = new Dictionary<BevelStyle, BevelStyleDef>();
            foreach (BevelStyleDef def in BevelStyles.CreateDefaults())
                fallbackStyles[def.id] = def;
        }
        return fallbackStyles.TryGetValue(id, out BevelStyleDef found) ? found : null;
    }

    public TMP_FontAsset GetFont(FontRole font)
    {
        switch (font)
        {
            case FontRole.DisplaySemiBold: return displaySemiBold;
            case FontRole.DisplayBold: return displayBold;
            case FontRole.DisplayExtraBold: return displayExtraBold;
            case FontRole.LabelMedium: return labelMedium;
            case FontRole.LabelSemiBold: return labelSemiBold;
            case FontRole.LabelBold: return labelBold;
            default: return labelExtraBold;
        }
    }
}
