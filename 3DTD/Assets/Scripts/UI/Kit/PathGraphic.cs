using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Polylines and dots in local UI space: level path thumbnails and skill-tree connectors.
// Points are normalised (0..1 of the rect) so the drawing scales with the element.
[RequireComponent(typeof(CanvasRenderer))]
public class PathGraphic : MaskableGraphic
{
    [Serializable]
    public class Line
    {
        public List<Vector2> points = new List<Vector2>();
        public float width = 2f;
        public Color color = Color.white;
        public float dashOn;
        public float dashOff;
        public bool roundJoins = true;
    }

    [Serializable]
    public struct Dot
    {
        public Vector2 position;
        public float radius;
        public Color color;
    }

    [SerializeField] private List<Line> lines = new List<Line>();
    [SerializeField] private List<Dot> dots = new List<Dot>();
    [Tooltip("Keep the drawing's aspect ratio and centre it instead of stretching it to the rect")]
    [SerializeField] private bool preserveAspect = true;
    [SerializeField] private float contentAspect = 1f;
    [SerializeField] private float padding = 8f;

    private static readonly List<Vector2> scaled = new List<Vector2>(64);

    public void Clear()
    {
        lines.Clear();
        dots.Clear();
        SetVerticesDirty();
    }

    // Aspect ratio (width / height) of the normalised drawing space
    public void SetContentAspect(float aspect)
    {
        contentAspect = Mathf.Max(0.01f, aspect);
        SetVerticesDirty();
    }

    public void AddLine(IList<Vector2> normalisedPoints, float width, Color lineColor, float dashOn = 0f, float dashOff = 0f, bool roundJoins = true)
    {
        Line line = new Line { width = width, color = lineColor, dashOn = dashOn, dashOff = dashOff, roundJoins = roundJoins };
        line.points.AddRange(normalisedPoints);
        lines.Add(line);
        SetVerticesDirty();
    }

    public void AddDot(Vector2 normalisedPosition, float radius, Color dotColor)
    {
        dots.Add(new Dot { position = normalisedPosition, radius = radius, color = dotColor });
        SetVerticesDirty();
    }

    private Rect DrawRect()
    {
        Rect rect = GetPixelAdjustedRect();
        rect = new Rect(rect.x + padding, rect.y + padding, Mathf.Max(1f, rect.width - padding * 2f), Mathf.Max(1f, rect.height - padding * 2f));
        if (!preserveAspect)
            return rect;

        float rectAspect = rect.width / rect.height;
        if (rectAspect > contentAspect)
        {
            float width = rect.height * contentAspect;
            return new Rect(rect.center.x - width * 0.5f, rect.y, width, rect.height);
        }
        float height = rect.width / contentAspect;
        return new Rect(rect.x, rect.center.y - height * 0.5f, rect.width, height);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = DrawRect();

        foreach (Line line in lines)
        {
            scaled.Clear();
            foreach (Vector2 p in line.points)
                scaled.Add(new Vector2(rect.x + p.x * rect.width, rect.y + p.y * rect.height));
            UIGeometry.AddPolyline(vh, scaled, false, line.width, line.color * color, line.dashOn, line.dashOff, line.roundJoins);
            if (line.roundJoins && line.dashOn <= 0f && scaled.Count > 1)
            {
                // round caps
                UIGeometry.AddDisc(vh, scaled[0], line.width * 0.5f, line.color * color, 12);
                UIGeometry.AddDisc(vh, scaled[scaled.Count - 1], line.width * 0.5f, line.color * color, 12);
            }
        }

        foreach (Dot dot in dots)
        {
            Vector2 center = new Vector2(rect.x + dot.position.x * rect.width, rect.y + dot.position.y * rect.height);
            UIGeometry.AddDisc(vh, center, dot.radius, dot.color * color, 16);
        }
    }
}
