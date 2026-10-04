using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Small construction helpers for the UI redesign builder. Coordinates follow the 1100 x 611 mockups:
// x to the right, y down from the top-left of the parent.
public static class UIBuild
{
    public static UITheme Theme;

    public static RectTransform Node(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        RectTransform rect = (RectTransform)go.transform;
        if (parent != null)
            rect.SetParent(parent, false);
        return rect;
    }

    // ---- placement -------------------------------------------------------------------------------------

    public static RectTransform TopLeft(this RectTransform rect, float x, float y, float width, float height)
    {
        return Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), width, height);
    }

    public static RectTransform TopRight(this RectTransform rect, float right, float y, float width, float height)
    {
        return Place(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-right, -y), width, height);
    }

    public static RectTransform BottomLeft(this RectTransform rect, float x, float bottom, float width, float height)
    {
        return Place(rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, bottom), width, height);
    }

    public static RectTransform BottomRight(this RectTransform rect, float right, float bottom, float width, float height)
    {
        return Place(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-right, bottom), width, height);
    }

    // Anchored to the bottom centre; dx is the offset of the element's centre from the parent's centre
    public static RectTransform BottomCenter(this RectTransform rect, float dx, float bottom, float width, float height)
    {
        return Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(dx, bottom), width, height);
    }

    public static RectTransform Center(this RectTransform rect, float dx, float dy, float width, float height)
    {
        return Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(dx, -dy), width, height);
    }

    private static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
        return rect;
    }

    public static RectTransform Stretch(this RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // Full width at the top of the parent, growing downwards
    public static RectTransform TopStretch(this RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static RectTransform BottomStretch(this RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
        return rect;
    }

    public static RectTransform Size(this RectTransform rect, float width, float height)
    {
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // ---- layout ----------------------------------------------------------------------------------------

    public static HorizontalLayoutGroup HLayout(this RectTransform rect, float spacing, int left = 0, int right = 0, int top = 0, int bottom = 0,
        TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false)
    {
        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.childAlignment = align;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static VerticalLayoutGroup VLayout(this RectTransform rect, float spacing, int left = 0, int right = 0, int top = 0, int bottom = 0,
        TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
    {
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.childAlignment = align;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = expandWidth;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement Layout(RectTransform rect, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f, bool ignore = false)
    {
        LayoutElement element = rect.gameObject.GetComponent<LayoutElement>();
        if (element == null)
            element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
        element.minWidth = preferredWidth;
        element.minHeight = preferredHeight;
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = flexibleHeight;
        element.ignoreLayout = ignore;
        return element;
    }

    public static RectTransform IgnoreLayout(this RectTransform rect)
    {
        Layout(rect, ignore: true);
        return rect;
    }

    public static ContentSizeFitter Fit(this RectTransform rect, bool horizontal, bool vertical)
    {
        ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        return fitter;
    }

    public static RectTransform Spacer(Transform parent, float width = -1f, float height = -1f, float flexibleWidth = -1f)
    {
        RectTransform rect = Node("Spacer", parent);
        rect.Size(Mathf.Max(0f, width), Mathf.Max(0f, height));
        Layout(rect, width, height, flexibleWidth);
        return rect;
    }

    // ---- graphics --------------------------------------------------------------------------------------

    public static BevelGraphic Bevel(this RectTransform rect, BevelStyle style, float chamfer, BevelShape shape = BevelShape.Chamfer, bool raycast = false)
    {
        BevelGraphic graphic = rect.gameObject.AddComponent<BevelGraphic>();
        graphic.raycastTarget = raycast;
        Set(graphic, "style", (int)style);
        Set(graphic, "shape", (int)shape);
        Set(graphic, "chamfer", chamfer);
        return graphic;
    }

    // Stretched bevel layer behind the content of a container (keeps the container free for layout groups)
    public static BevelGraphic Backdrop(RectTransform parent, BevelStyle style, float chamfer, bool raycast = true, string name = "Plate")
    {
        RectTransform rect = Node(name, parent).Stretch();
        rect.IgnoreLayout();
        rect.SetAsFirstSibling();
        return rect.Bevel(style, chamfer, BevelShape.Chamfer, raycast);
    }

    public static Image Img(this RectTransform rect, Sprite sprite, Color color, bool raycast = false, bool preserveAspect = true)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        image.preserveAspect = preserveAspect;
        return image;
    }

    // Icon of the theme at its design size. Faceted icons carry a glow margin, so their image is larger
    // and ignores the layout (a sized holder keeps the slot).
    public static Image Icon(Transform parent, string iconName, float size, Color tint, string name = null)
    {
        Sprite sprite = Theme.Icon(iconName);
        if (sprite == null)
            Debug.LogError("UI builder: missing icon " + iconName);
        bool faceted = iconName.StartsWith("fx_");
        RectTransform holder = Node(name ?? iconName, parent).Size(size, size);
        Layout(holder, size, size);
        if (!faceted)
            return holder.Img(sprite, tint);

        RectTransform art = Node("Art", holder);
        art.Center(0f, 0f, size * Theme.facetSpriteScale, size * Theme.facetSpriteScale);
        return art.Img(sprite, Color.white);
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, UITheme.FontRole font, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool caps = false, float tracking = 0f, float width = -1f, float flexible = -1f)
    {
        RectTransform rect = Node(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Theme.GetFont(font);
        tmp.fontSize = size;
        tmp.color = color;
        tmp.text = text;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.characterSpacing = tracking * 100f;
        tmp.margin = Vector4.zero;
        if (caps)
            tmp.fontStyle = FontStyles.UpperCase;

        // Height from the font size (Manrope's line is ~1.37 em; a shorter box makes TMP drop ellipsis
        // lines entirely); width from the text (TMP's preferred width) unless given
        float height = Mathf.Ceil(size * 1.42f);
        rect.Size(width > 0f ? width : 120f, height);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
        if (width > 0f)
        {
            element.preferredWidth = width;
            element.minWidth = width;
        }
        element.flexibleWidth = flexible;
        return tmp;
    }

    // Multi-line paragraph text that wraps inside its width
    public static TextMeshProUGUI Paragraph(Transform parent, string name, string text, float size, Color color, float width = -1f)
    {
        TextMeshProUGUI tmp = Text(parent, name, text, UITheme.FontRole.LabelMedium, size, color, TextAlignmentOptions.TopLeft, false, 0f, width);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 4f;
        LayoutElement element = tmp.GetComponent<LayoutElement>();
        element.preferredHeight = -1f;
        element.minHeight = -1f;
        return tmp;
    }

    // Caption style: small caps label with letter spacing (Manrope 700, 9.5 px, 0.14 em)
    public static TextMeshProUGUI Caption(Transform parent, string text, Color color, float size = 9.5f, float tracking = 0.14f)
    {
        return Text(parent, "Caption", text, UITheme.FontRole.LabelBold, size, color, TextAlignmentOptions.MidlineLeft, true, tracking);
    }

    public static Image CornerLine(RectTransform plate, float size)
    {
        RectTransform rect = Node("Corner", plate).TopLeft(0f, 0f, size, size);
        rect.IgnoreLayout();
        return rect.Img(Theme.cornerLine, Theme.accent, false, false);
    }

    // Floating plate: a container with a soft drop shadow (0 10 px 16 px), the bevel plate and the signature
    // corner line as stretched, layout-ignoring children, so the container stays free for a layout group
    public static RectTransform FloatingPlate(Transform parent, string name, BevelStyle style, float chamfer, float cornerSize, bool shadow = true)
    {
        RectTransform root = Node(name, parent);
        if (shadow)
        {
            // the shadow sprite has a 32 px soft margin around its solid core
            RectTransform shadowRect = Node("Shadow", root).Stretch(-32f, -22f, -32f, -42f);
            shadowRect.IgnoreLayout();
            Image image = shadowRect.Img(Theme.shadow, Theme.dropShadow, false, false);
            image.type = Image.Type.Sliced;
        }
        Backdrop(root, style, chamfer, true);
        if (shadow)
            root.Find("Shadow").SetAsFirstSibling();
        if (cornerSize > 0f)
            CornerLine(root, cornerSize);
        return root;
    }

    public static RectTransform Divider(Transform parent, BevelStyle style, float width, float height)
    {
        RectTransform rect = Node("Divider", parent).Size(width, height);
        Layout(rect, width, height);
        rect.Bevel(style, 0f);
        return rect;
    }

    // ---- buttons ---------------------------------------------------------------------------------------

    public static BevelButton Button(RectTransform rect, BevelStyle normal, BevelStyle highlighted, BevelStyle pressed, BevelStyle disabled, BevelStyle on,
        float chamfer, float pressedOffset = 2f, BevelShape shape = BevelShape.Chamfer)
    {
        // Buttons without a plate (tabs, text links) still get a faint wash while hovered
        if (highlighted == BevelStyle.None)
            highlighted = BevelStyle.HoverWash;
        BevelGraphic plate = rect.Bevel(normal, chamfer, shape, true);
        BevelButton button = rect.gameObject.AddComponent<BevelButton>();
        Set(button, "plate", plate);
        HoverMotion(button, normal);
        Set(button, "normalStyle", (int)normal);
        Set(button, "highlightedStyle", (int)highlighted);
        Set(button, "pressedStyle", (int)pressed);
        Set(button, "disabledStyle", (int)disabled);
        Set(button, "onStyle", (int)on);
        Set(button, "pressedOffset", pressedOffset);
        return button;
    }

    // How a button moves while hovered, by kind: key-caps and tiles grow, menu entries slide right,
    // rows and segmented items only light up
    public static void HoverMotion(BevelButton button, BevelStyle normal)
    {
        float scale = 1f;
        Vector2 shift = Vector2.zero;
        switch (normal)
        {
            case BevelStyle.KeyCap:
                scale = 1.03f;
                break;
            case BevelStyle.Tile:
            case BevelStyle.DifficultyTile:
            case BevelStyle.NodeAvailable:
                scale = 1.05f;
                break;
            case BevelStyle.LevelCard:
                scale = 1.025f;
                break;
            case BevelStyle.GlassButton:
            case BevelStyle.GlassFlat:
            case BevelStyle.Plate:
                scale = 1.06f;
                break;
            case BevelStyle.NavButton:
            case BevelStyle.ContinueButton:
                shift = new Vector2(5f, 0f);
                break;
            case BevelStyle.None:
                shift = new Vector2(3f, 0f);
                break;
        }
        Set(button, "hoverScale", scale);
        Set(button, "hoverShift", shift);
    }

    // Hover text: a TooltipTrigger, plus an invisible raycast target when nothing on the element catches
    // the pointer
    public static TooltipTrigger Hint(RectTransform target, string title, string body, TooltipService.Side side = TooltipService.Side.Below, RectTransform outside = null)
    {
        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic == null)
            target.gameObject.AddComponent<HitArea>();
        else
            graphic.raycastTarget = true;
        TooltipTrigger trigger = target.gameObject.AddComponent<TooltipTrigger>();
        Set(trigger, "title", title ?? "");
        Set(trigger, "body", body ?? "");
        Set(trigger, "side", (int)side);
        if (outside != null)
            Set(trigger, "outside", outside);
        return trigger;
    }

    // Content holder inside a button that drops while pressed
    public static RectTransform Content(BevelButton button, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        RectTransform rect = Node("Content", button.transform).Stretch(left, top, right, bottom);
        Set(button, "content", rect);
        return rect;
    }

    public static void Tint(BevelButton button, Graphic graphic, Color normal, Color on, Color disabled)
    {
        SerializedObject so = new SerializedObject(button);
        SerializedProperty list = so.FindProperty("tints");
        int index = list.arraySize;
        list.arraySize++;
        SerializedProperty element = list.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("graphic").objectReferenceValue = graphic;
        element.FindPropertyRelative("normal").colorValue = normal;
        element.FindPropertyRelative("on").colorValue = on;
        element.FindPropertyRelative("disabled").colorValue = disabled;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Enlarges the touch target of a small control to at least 44 x 44 reference px
    public static void MinTouchTarget(RectTransform control, float size = 44f)
    {
        Vector2 current = control.sizeDelta;
        if (current.x >= size && current.y >= size)
            return;
        RectTransform rect = Node("Hit Area", control);
        rect.Center(0f, 0f, Mathf.Max(size, current.x), Mathf.Max(size, current.y));
        rect.IgnoreLayout();
        rect.gameObject.AddComponent<HitArea>();
    }

    // ---- serialized fields -----------------------------------------------------------------------------

    public static void Set(Object target, string field, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("UI builder: " + target.GetType().Name + " has no serialized field '" + field + "'");
            return;
        }
        Assign(property, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Assign(SerializedProperty property, object value)
    {
        switch (value)
        {
            case null:
                property.objectReferenceValue = null;
                break;
            case Object unityObject:
                property.objectReferenceValue = unityObject;
                break;
            case bool b:
                property.boolValue = b;
                break;
            case int i:
                if (property.propertyType == SerializedPropertyType.Enum)
                    property.enumValueIndex = i;
                else
                    property.intValue = i;
                break;
            case float f:
                property.floatValue = f;
                break;
            case string s:
                property.stringValue = s;
                break;
            case Color c:
                property.colorValue = c;
                break;
            case Vector2 v:
                property.vector2Value = v;
                break;
            case IList list:
                property.arraySize = list.Count;
                for (int index = 0; index < list.Count; index++)
                    Assign(property.GetArrayElementAtIndex(index), list[index]);
                break;
            default:
                Debug.LogError("UI builder: unsupported value type " + value.GetType().Name + " for " + property.propertyPath);
                break;
        }
    }
}
