using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BevelShape
{
    Chamfer,        // top-left and bottom-right corners cut at 45 degrees
    Hexagon,        // flat-top hexagon stretched to the rect (CSS polygon 25% 0, 75% 0, 100% 50% ...)
    Parallelogram,  // top edge shifted right, bottom edge shifted left by the chamfer size
    SlantRight,     // only the bottom-right end cut (progress bars)
}

// Plate, button, chip or tile in the "bevelled glass" style: a rim gradient around a translucent body with
// one sheen facet, inset light and shade lines and an optional bottom band. The shape is real geometry with
// exact gradients at any size, drawn with the default UI material in one batch, so no 9-slice stretching
// distorts the gradients and no custom shader is needed. Outer edges get a one-pixel feather for anti-aliasing.
[RequireComponent(typeof(CanvasRenderer))]
public class BevelGraphic : MaskableGraphic
{
    [SerializeField] private BevelStyle style = BevelStyle.Plate;
    [SerializeField] private BevelShape shape = BevelShape.Chamfer;
    [Tooltip("Corner cut in reference px")]
    [SerializeField] private float chamfer = 14f;
    [SerializeField] private bool antiAlias = true;

    private static readonly List<Vector2> outer = new List<Vector2>(8);
    private static readonly List<Vector2> inner = new List<Vector2>(8);
    private static readonly List<Vector2> scratch = new List<Vector2>(16);
    private static readonly List<Color32> featherColors = new List<Color32>(8);
    // Hover: a lighter body and a warm line along the rim, scaled by Highlight
    private static readonly BevelGradient HoverWash = BevelGradient.Vertical(new Color(1f, 1f, 1f, 0.11f), new Color(1f, 1f, 1f, 0.035f));
    private static readonly Color HoverRim = new Color(1f, 0.69f, 0.48f, 0.55f);

    private float highlight;

    public BevelStyle Style => style;
    public BevelShape Shape => shape;

    public float Chamfer
    {
        get { return chamfer; }
        set
        {
            if (Mathf.Approximately(chamfer, value))
                return;
            chamfer = value;
            SetVerticesDirty();
        }
    }

    // 0..1, animated by BevelButton while hovered
    public float Highlight
    {
        get { return highlight; }
        set
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(highlight, value))
                return;
            highlight = value;
            SetVerticesDirty();
        }
    }

    public void SetStyle(BevelStyle newStyle)
    {
        if (style == newStyle)
            return;
        style = newStyle;
        SetVerticesDirty();
    }

    public void SetShape(BevelShape newShape)
    {
        if (shape == newShape)
            return;
        shape = newShape;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (style == BevelStyle.None)
            return;

        UITheme theme = UITheme.Current;
        BevelStyleDef def = theme != null ? theme.Style(style) : UITheme.FallbackStyle(style);
        if (def == null)
            return;

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        Color tint = color;
        BuildShape(outer, rect);

        bool hasRim = def.rimWidth > 0f && def.rim != null && def.rim.IsVisible;
        Rect bodyRect = rect;
        List<Vector2> body = outer;
        if (def.rimWidth > 0f)
        {
            bodyRect = new Rect(rect.x + def.rimWidth, rect.y + def.rimWidth, rect.width - def.rimWidth * 2f, rect.height - def.rimWidth * 2f);
            BuildShape(inner, bodyRect);
            body = inner;
        }

        if (hasRim)
        {
            if (def.rimUnderBody)
            {
                UIGeometry.AddPolygon(vh, outer, def.rim, rect, tint);
            }
            else
            {
                int count = outer.Count;
                for (int i = 0; i < count; i++)
                {
                    scratch.Clear();
                    scratch.Add(outer[i]);
                    scratch.Add(outer[(i + 1) % count]);
                    scratch.Add(inner[(i + 1) % count]);
                    scratch.Add(inner[i]);
                    UIGeometry.AddPolygon(vh, scratch, def.rim, rect, tint);
                }
            }
        }

        if (def.body != null)
        {
            foreach (BevelGradient layer in def.body)
            {
                if (layer != null && layer.IsVisible)
                    UIGeometry.AddPolygon(vh, body, layer, bodyRect, tint);
            }
        }

        if (def.band > 0f && def.bandColor.a > 0f)
            AddClipped(vh, body, bodyRect.yMin + def.band, false, def.bandColor * tint);
        if (def.insetTop > 0f && def.insetTopColor.a > 0f)
            AddClipped(vh, body, bodyRect.yMax - def.insetTop, true, def.insetTopColor * tint);
        if (def.insetBottom > 0f && def.insetBottomColor.a > 0f)
            AddClipped(vh, body, bodyRect.yMin + def.insetBottom, false, def.insetBottomColor * tint);

        if (highlight > 0.001f)
        {
            Color wash = tint;
            wash.a *= highlight;
            UIGeometry.AddPolygon(vh, body, HoverWash, bodyRect, wash);
            if (def.rimWidth > 0f)
            {
                Color32 rimColor = HoverRim * tint * new Color(1f, 1f, 1f, highlight);
                int count = outer.Count;
                for (int i = 0; i < count; i++)
                {
                    scratch.Clear();
                    scratch.Add(outer[i]);
                    scratch.Add(outer[(i + 1) % count]);
                    scratch.Add(inner[(i + 1) % count]);
                    scratch.Add(inner[i]);
                    UIGeometry.AddPolygon(vh, scratch, rimColor);
                }
            }
        }

        if (antiAlias)
        {
            float feather = 1f;
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas != null && rootCanvas.renderMode != RenderMode.WorldSpace && rootCanvas.scaleFactor > 0f)
                feather = 1f / rootCanvas.scaleFactor;

            featherColors.Clear();
            BevelGradient edge = hasRim ? def.rim : FirstVisible(def.body);
            for (int i = 0; i < outer.Count; i++)
                featherColors.Add(edge != null ? (Color32)(edge.Evaluate(rect, outer[i]) * tint) : (Color32)Color.clear);
            UIGeometry.AddFeather(vh, outer, featherColors, feather);
        }
    }

    private static BevelGradient FirstVisible(BevelGradient[] layers)
    {
        if (layers == null)
            return null;
        foreach (BevelGradient layer in layers)
        {
            if (layer != null && layer.IsVisible)
                return layer;
        }
        return null;
    }

    private static void AddClipped(VertexHelper vh, List<Vector2> polygon, float y, bool keepAbove, Color color)
    {
        scratch.Clear();
        scratch.AddRange(polygon);
        UIGeometry.ClipHorizontal(scratch, y, keepAbove);
        UIGeometry.AddPolygon(vh, scratch, (Color32)color);
    }

    private void BuildShape(List<Vector2> polygon, Rect rect)
    {
        switch (shape)
        {
            case BevelShape.Hexagon:
                polygon.Clear();
                polygon.Add(new Vector2(rect.xMin + rect.width * 0.25f, rect.yMax));
                polygon.Add(new Vector2(rect.xMin + rect.width * 0.75f, rect.yMax));
                polygon.Add(new Vector2(rect.xMax, rect.center.y));
                polygon.Add(new Vector2(rect.xMin + rect.width * 0.75f, rect.yMin));
                polygon.Add(new Vector2(rect.xMin + rect.width * 0.25f, rect.yMin));
                polygon.Add(new Vector2(rect.xMin, rect.center.y));
                break;
            case BevelShape.Parallelogram:
            {
                float cut = Mathf.Min(chamfer, rect.width * 0.5f);
                polygon.Clear();
                polygon.Add(new Vector2(rect.xMin + cut, rect.yMax));
                polygon.Add(new Vector2(rect.xMax, rect.yMax));
                polygon.Add(new Vector2(rect.xMax - cut, rect.yMin));
                polygon.Add(new Vector2(rect.xMin, rect.yMin));
                break;
            }
            case BevelShape.SlantRight:
            {
                float cut = Mathf.Min(chamfer, rect.width);
                polygon.Clear();
                polygon.Add(new Vector2(rect.xMin, rect.yMax));
                polygon.Add(new Vector2(rect.xMax, rect.yMax));
                polygon.Add(new Vector2(rect.xMax - cut, rect.yMin));
                polygon.Add(new Vector2(rect.xMin, rect.yMin));
                break;
            }
            default:
                UIGeometry.Chamfer(polygon, rect, chamfer);
                break;
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
