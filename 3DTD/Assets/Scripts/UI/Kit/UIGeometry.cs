using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Mesh helpers for the procedural UI graphics (bevel plates, hexagons, dashed rings, path lines).
// Everything is plain vertex-coloured geometry drawn with the default UI material.
public static class UIGeometry
{
    private static readonly List<Vector2> clipA = new List<Vector2>(16);
    private static readonly List<Vector2> clipB = new List<Vector2>(16);

    public static void AddVert(VertexHelper vh, Vector2 position, Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }

    // Convex polygon as a triangle fan, one colour
    public static void AddPolygon(VertexHelper vh, List<Vector2> polygon, Color32 color)
    {
        if (polygon.Count < 3 || color.a == 0)
            return;

        int start = vh.currentVertCount;
        for (int i = 0; i < polygon.Count; i++)
            AddVert(vh, polygon[i], color);
        for (int i = 1; i < polygon.Count - 1; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    // Convex polygon filled with a linear gradient. The polygon is split along the stop lines so
    // multi-stop and hard-edged gradients come out exact.
    public static void AddPolygon(VertexHelper vh, List<Vector2> polygon, BevelGradient gradient, Rect rect, Color tint)
    {
        if (polygon.Count < 3 || gradient == null || gradient.stops == null || gradient.stops.Length == 0)
            return;

        if (gradient.stops.Length == 1)
        {
            AddPolygon(vh, polygon, (Color32)(gradient.stops[0].color * tint));
            return;
        }

        gradient.GetAxis(rect, out Vector2 origin, out Vector2 axis);
        float before = float.NegativeInfinity;
        for (int s = 0; s <= gradient.stops.Length; s++)
        {
            float after = s < gradient.stops.Length ? gradient.stops[s].position : float.PositiveInfinity;
            if (after <= before)
                continue;

            Color colorBefore = s == 0 ? gradient.stops[0].color : gradient.stops[s - 1].color;
            Color colorAfter = s < gradient.stops.Length ? gradient.stops[s].color : gradient.stops[s - 1].color;
            float from = before;
            before = after;
            if (colorBefore.a <= 0.001f && colorAfter.a <= 0.001f)
                continue;

            clipA.Clear();
            clipA.AddRange(polygon);
            if (!float.IsNegativeInfinity(from))
                ClipHalfPlane(clipA, clipB, origin, axis, from, true);
            if (!float.IsPositiveInfinity(after))
                ClipHalfPlane(clipA, clipB, origin, axis, after, false);
            if (clipA.Count < 3)
                continue;

            int start = vh.currentVertCount;
            for (int i = 0; i < clipA.Count; i++)
            {
                float t = Vector2.Dot(clipA[i] - origin, axis);
                Color color;
                if (float.IsNegativeInfinity(from) || float.IsPositiveInfinity(after))
                    color = colorBefore;
                else
                    color = Color.Lerp(colorBefore, colorAfter, Mathf.InverseLerp(from, after, t));
                AddVert(vh, clipA[i], (Color32)(color * tint));
            }
            for (int i = 1; i < clipA.Count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    // Keeps the part of the polygon where dot(p - origin, axis) >= value (keepAbove) or <= value
    private static void ClipHalfPlane(List<Vector2> polygon, List<Vector2> scratch, Vector2 origin, Vector2 axis, float value, bool keepAbove)
    {
        scratch.Clear();
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % count];
            float da = Vector2.Dot(a - origin, axis) - value;
            float db = Vector2.Dot(b - origin, axis) - value;
            if (!keepAbove) { da = -da; db = -db; }

            bool aIn = da >= 0f;
            bool bIn = db >= 0f;
            if (aIn)
                scratch.Add(a);
            if (aIn != bIn)
                scratch.Add(Vector2.Lerp(a, b, da / (da - db)));
        }
        polygon.Clear();
        polygon.AddRange(scratch);
    }

    // Keeps the part of the polygon above (keepAbove) or below a horizontal line
    public static void ClipHorizontal(List<Vector2> polygon, float y, bool keepAbove)
    {
        ClipHalfPlane(polygon, clipB, Vector2.zero, Vector2.up, y, keepAbove);
    }

    // Rectangle with the top-left and bottom-right corners cut at 45 degrees (UI space, y up)
    public static void Chamfer(List<Vector2> polygon, Rect rect, float cut)
    {
        polygon.Clear();
        cut = Mathf.Clamp(cut, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        if (cut <= 0.01f)
        {
            polygon.Add(new Vector2(rect.xMin, rect.yMax));
            polygon.Add(new Vector2(rect.xMax, rect.yMax));
            polygon.Add(new Vector2(rect.xMax, rect.yMin));
            polygon.Add(new Vector2(rect.xMin, rect.yMin));
            return;
        }
        polygon.Add(new Vector2(rect.xMin + cut, rect.yMax));
        polygon.Add(new Vector2(rect.xMax, rect.yMax));
        polygon.Add(new Vector2(rect.xMax, rect.yMin + cut));
        polygon.Add(new Vector2(rect.xMax - cut, rect.yMin));
        polygon.Add(new Vector2(rect.xMin, rect.yMin));
        polygon.Add(new Vector2(rect.xMin, rect.yMax - cut));
    }

    // Hexagon; pointy-top when pointyTop is set, flat-top otherwise
    public static void Hexagon(List<Vector2> polygon, Vector2 center, Vector2 radius, bool pointyTop)
    {
        polygon.Clear();
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + (pointyTop ? 90f : 0f));
            polygon.Add(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y));
        }
    }

    // Ring between a convex polygon and the same polygon moved outwards by width, with the outer edge
    // fading to transparent. Used for anti-aliased outer edges.
    public static void AddFeather(VertexHelper vh, List<Vector2> polygon, List<Color32> colors, float width)
    {
        int count = polygon.Count;
        if (count < 3 || width <= 0f)
            return;

        float area = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % count];
            area += a.x * b.y - b.x * a.y;
        }
        // Normal() points to the right of an edge, which is outwards for counter-clockwise polygons
        float side = area < 0f ? -1f : 1f;

        int start = vh.currentVertCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 previous = polygon[(i + count - 1) % count];
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % count];
            Vector2 n0 = Normal(previous, current) * side;
            Vector2 n1 = Normal(current, next) * side;
            Vector2 miter = (n0 + n1).normalized;
            float scale = 1f / Mathf.Max(0.3f, Vector2.Dot(miter, n1));
            Color32 inner = colors[i];
            Color32 outer = inner;
            outer.a = 0;
            AddVert(vh, current, inner);
            AddVert(vh, current + miter * width * scale, outer);
        }
        for (int i = 0; i < count; i++)
        {
            int a = start + i * 2;
            int b = start + ((i + 1) % count) * 2;
            vh.AddTriangle(a, b, b + 1);
            vh.AddTriangle(a, b + 1, a + 1);
        }
    }

    private static Vector2 Normal(Vector2 a, Vector2 b)
    {
        Vector2 d = (b - a).normalized;
        return new Vector2(d.y, -d.x);
    }

    // Quad strip along a polyline. dashOn/dashOff of 0 draw a solid line. Round joins add a small disc
    // at every inner vertex so thick lines don't show gaps.
    public static void AddPolyline(VertexHelper vh, IList<Vector2> points, bool closed, float width, Color32 color,
        float dashOn = 0f, float dashOff = 0f, bool roundJoins = false, float dashOffset = 0f)
    {
        int count = points.Count;
        if (count < 2 || color.a == 0)
            return;

        float half = width * 0.5f;
        int segments = closed ? count : count - 1;
        bool dashed = dashOn > 0f && dashOff > 0f;
        float period = dashOn + dashOff;
        float travelled = dashOffset;

        for (int i = 0; i < segments; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % count];
            float length = Vector2.Distance(a, b);
            if (length < 0.0001f)
                continue;
            Vector2 direction = (b - a) / length;
            Vector2 normal = new Vector2(-direction.y, direction.x) * half;

            if (!dashed)
            {
                AddQuad(vh, a - normal, a + normal, b + normal, b - normal, color);
            }
            else
            {
                float position = 0f;
                while (position < length)
                {
                    float phase = Mathf.Repeat(travelled + position, period);
                    if (phase < dashOn)
                    {
                        float end = Mathf.Min(length, position + dashOn - phase);
                        Vector2 p0 = a + direction * position;
                        Vector2 p1 = a + direction * end;
                        AddQuad(vh, p0 - normal, p0 + normal, p1 + normal, p1 - normal, color);
                        position = end;
                    }
                    else
                    {
                        position += period - phase;
                    }
                }
            }
            travelled += length;
        }

        if (roundJoins && !dashed)
        {
            int first = closed ? 0 : 1;
            int last = closed ? count : count - 1;
            for (int i = first; i < last; i++)
                AddDisc(vh, points[i % count], half, color, 10);
        }
    }

    public static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        int start = vh.currentVertCount;
        AddVert(vh, a, color);
        AddVert(vh, b, color);
        AddVert(vh, c, color);
        AddVert(vh, d, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    public static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color32 color, int segments)
    {
        int start = vh.currentVertCount;
        AddVert(vh, center, color);
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            AddVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start, start + i + 1, start + i + 2);
    }

    // Ellipse filled with a radial falloff from inner to outer colour
    public static void AddRadialEllipse(VertexHelper vh, Vector2 center, Vector2 radius, Color32 inner, Color32 outer, float innerRadius01, int segments)
    {
        int start = vh.currentVertCount;
        AddVert(vh, center, inner);
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
            AddVert(vh, center + direction * innerRadius01, inner);
            AddVert(vh, center + direction, outer);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = start + 1 + i * 2;
            int b = a + 2;
            vh.AddTriangle(start, a, b);
            vh.AddTriangle(a, a + 1, b + 1);
            vh.AddTriangle(a, b + 1, b);
        }
    }

    public static void Ellipse(List<Vector2> points, Vector2 center, Vector2 radius, int segments)
    {
        points.Clear();
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            points.Add(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y));
        }
    }
}
