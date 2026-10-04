using UnityEngine;

// A flat world-space canvas lying on a tower's base plane: hexagon ring(s) around the tower and the dashed
// range circle at its real range. Built at runtime; all sizes are in world units.
public class WorldRing : MonoBehaviour
{
    // UI units per world unit on the ring canvas
    private const float UnitsPerWorld = 100f;
    // Lift off the surface so the ring doesn't z-fight with the block face
    private const float SurfaceOffset = 0.03f;

    private RectTransform root;
    private OutlineShapeGraphic range;
    private OutlineShapeGraphic outerHex;
    private OutlineShapeGraphic innerHex;

    public static WorldRing Create(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        WorldRing ring = go.AddComponent<WorldRing>();
        ring.Build();
        return ring;
    }

    private void Build()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        root = (RectTransform)transform;
        root.sizeDelta = new Vector2(UnitsPerWorld, UnitsPerWorld);
        root.localScale = Vector3.one / UnitsPerWorld;

        range = CreateShape("Range");
        outerHex = CreateShape("HexOuter");
        innerHex = CreateShape("HexInner");
    }

    private OutlineShapeGraphic CreateShape(string shapeName)
    {
        GameObject go = new GameObject(shapeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(OutlineShapeGraphic));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        OutlineShapeGraphic graphic = go.GetComponent<OutlineShapeGraphic>();
        graphic.raycastTarget = false;
        return graphic;
    }

    // Selected tower: flat hexagon ring, dashed range circle with an orange rim wash
    public void StyleAsSelection()
    {
        // The scene's tonemapping darkens the wash, so it stays a little lighter than the mockup's 0.16
        range.Configure(OutlineShapeGraphic.ShapeKind.Ellipse, OutlineShapeGraphic.FillKind.RimWeighted,
            BevelStyles.Rgba(255, 138, 61, 0.12f), BevelStyles.Rgba(255, 170, 120, 0.6f), 4f, 25f, 17.5f);
        outerHex.Configure(OutlineShapeGraphic.ShapeKind.HexagonFlatTop, OutlineShapeGraphic.FillKind.None,
            Color.clear, BevelStyles.Rgba(255, 170, 120, 0.35f), 2.5f);
        innerHex.Configure(OutlineShapeGraphic.ShapeKind.HexagonFlatTop, OutlineShapeGraphic.FillKind.Solid,
            BevelStyles.Rgba(255, 138, 61, 0.18f), BevelStyles.Hex("#FFA266"), 6f);
    }

    // Placement ghost: dashed hexagon and a faint range disc
    public void StyleAsGhost(bool valid)
    {
        Color stroke = valid ? BevelStyles.Hex("#FFA266") : BevelStyles.Hex("#FF7088");
        Color fill = valid ? BevelStyles.Rgba(255, 138, 61, 0.22f) : BevelStyles.Rgba(255, 112, 136, 0.22f);
        range.Configure(OutlineShapeGraphic.ShapeKind.Ellipse, OutlineShapeGraphic.FillKind.Solid,
            BevelStyles.Rgba(255, 138, 61, valid ? 0.06f : 0.04f), BevelStyles.Rgba(255, 170, 120, valid ? 0.5f : 0.3f), 4f, 25f, 17.5f);
        outerHex.Configure(OutlineShapeGraphic.ShapeKind.HexagonFlatTop, OutlineShapeGraphic.FillKind.None, Color.clear, Color.clear, 0f);
        innerHex.Configure(OutlineShapeGraphic.ShapeKind.HexagonFlatTop, OutlineShapeGraphic.FillKind.Solid, fill, stroke, 5f, 15f, 10f);
    }

    public void Place(Vector3 center, Vector3 normal, float rangeRadius, float hexRadius)
    {
        normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
        transform.SetPositionAndRotation(center + normal * SurfaceOffset, Quaternion.LookRotation(normal, reference));

        float diameter = Mathf.Max(rangeRadius, hexRadius * 1.3f) * 2f * UnitsPerWorld;
        root.sizeDelta = new Vector2(diameter, diameter);
        SetSize(range, rangeRadius * 2f * UnitsPerWorld, 1f);
        range.gameObject.SetActive(rangeRadius > 0.01f);
        // A regular flat-top hexagon is sqrt(3)/2 as tall as it is wide
        SetSize(innerHex, hexRadius * 2f * UnitsPerWorld, 0.866f);
        SetSize(outerHex, hexRadius * 2.5f * UnitsPerWorld, 0.866f);
    }

    private static void SetSize(OutlineShapeGraphic graphic, float width, float aspect)
    {
        RectTransform rect = graphic.rectTransform;
        rect.sizeDelta = new Vector2(width, width * aspect);
        rect.anchoredPosition = Vector2.zero;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    // World point on the range circle (or hexagon) that appears lowest on screen; anchors the "Range X" tag
    public Vector3 LowestScreenPoint(Camera camera, float radius)
    {
        Vector3 center = transform.position;
        Vector3 best = center;
        float bestY = float.MaxValue;
        for (int i = 0; i < 32; i++)
        {
            float angle = Mathf.PI * 2f * i / 32f;
            Vector3 point = center + (transform.right * Mathf.Cos(angle) + transform.up * Mathf.Sin(angle)) * radius;
            float y = camera.WorldToScreenPoint(point).y;
            if (y < bestY)
            {
                bestY = y;
                best = point;
            }
        }
        return best;
    }
}
