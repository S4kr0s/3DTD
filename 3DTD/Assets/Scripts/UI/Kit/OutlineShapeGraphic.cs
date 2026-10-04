using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Hexagon or ellipse with an optional (dashed) stroke and a solid or rim-weighted radial fill.
// Used for the selection ring, the range ellipse, the placement ghost and dashed medal outlines.
[RequireComponent(typeof(CanvasRenderer))]
public class OutlineShapeGraphic : MaskableGraphic
{
    public enum ShapeKind
    {
        HexagonFlatTop,
        HexagonPointyTop,
        Ellipse,
    }

    public enum FillKind
    {
        None,
        Solid,
        RimWeighted,    // transparent in the middle, fillColor towards the edge (radial gradient from 55 %)
    }

    [SerializeField] private ShapeKind shape = ShapeKind.Ellipse;
    [SerializeField] private FillKind fill = FillKind.Solid;
    [SerializeField] private Color fillColor = new Color(1f, 0.54f, 0.24f, 0.18f);
    [SerializeField] private Color strokeColor = new Color(1f, 0.64f, 0.4f, 1f);
    [SerializeField] private float strokeWidth = 2f;
    [Tooltip("Dash length; 0 draws a solid stroke")]
    [SerializeField] private float dashOn = 0f;
    [SerializeField] private float dashOff = 0f;
    [SerializeField] private int ellipseSegments = 72;

    private static readonly List<Vector2> points = new List<Vector2>(96);

    public void Configure(ShapeKind newShape, FillKind newFill, Color newFillColor, Color newStrokeColor, float newStrokeWidth, float newDashOn = 0f, float newDashOff = 0f)
    {
        shape = newShape;
        fill = newFill;
        fillColor = newFillColor;
        strokeColor = newStrokeColor;
        strokeWidth = newStrokeWidth;
        dashOn = newDashOn;
        dashOff = newDashOff;
        SetVerticesDirty();
    }

    public void SetColors(Color newFillColor, Color newStrokeColor)
    {
        fillColor = newFillColor;
        strokeColor = newStrokeColor;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        // The stroke is centred on the outline, so pull the outline in by half the stroke width
        float inset = strokeColor.a > 0f ? strokeWidth * 0.5f : 0f;
        Vector2 radius = new Vector2(rect.width * 0.5f - inset, rect.height * 0.5f - inset);
        Vector2 center = rect.center;

        switch (shape)
        {
            case ShapeKind.Ellipse:
                UIGeometry.Ellipse(points, center, radius, Mathf.Max(12, ellipseSegments));
                break;
            default:
                UIGeometry.Hexagon(points, center, radius, shape == ShapeKind.HexagonPointyTop);
                break;
        }

        Color32 fillTinted = fillColor * color;
        if (fill == FillKind.Solid && fillTinted.a > 0)
        {
            UIGeometry.AddPolygon(vh, points, fillTinted);
        }
        else if (fill == FillKind.RimWeighted && fillTinted.a > 0)
        {
            Color32 transparent = fillTinted;
            transparent.a = 0;
            if (shape == ShapeKind.Ellipse)
            {
                UIGeometry.AddRadialEllipse(vh, center, radius, transparent, fillTinted, 0.55f, Mathf.Max(12, ellipseSegments));
            }
            else
            {
                // Hexagon: inner hexagon at 55 % transparent, fading to the colour at the outline
                int start = vh.currentVertCount;
                for (int i = 0; i < points.Count; i++)
                {
                    UIGeometry.AddVert(vh, Vector2.Lerp(center, points[i], 0.55f), transparent);
                    UIGeometry.AddVert(vh, points[i], fillTinted);
                }
                for (int i = 0; i < points.Count; i++)
                {
                    int a = start + i * 2;
                    int b = start + ((i + 1) % points.Count) * 2;
                    vh.AddTriangle(a, a + 1, b + 1);
                    vh.AddTriangle(a, b + 1, b);
                }
            }
        }

        Color32 strokeTinted = strokeColor * color;
        if (strokeWidth > 0f && strokeTinted.a > 0)
            UIGeometry.AddPolyline(vh, points, true, strokeWidth, strokeTinted, dashOn, dashOff, shape != ShapeKind.Ellipse && dashOn <= 0f);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
