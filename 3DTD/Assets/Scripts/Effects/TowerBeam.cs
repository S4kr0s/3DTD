using UnityEngine;

// The Beam Tower's beam: a LineRenderer copy of a line prefab from this object along its forward axis for
// RANGE, with looping start and end effects (the job PolygonBeamStatic did). Its look follows the tower's
// BeamVisualUpgrades, layered by priority: line, start and end prefabs, width, flicker, the swell after each
// tick (BeamTowerActionStrategy calls Pulse) and the colour.
public class TowerBeam : MonoBehaviour
{
    [Header("Base look")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private GameObject startPrefab;
    [SerializeField] private GameObject endPrefab;
    [SerializeField] private float width = 1f;
    [SerializeField] private float flicker = 0f;
    [SerializeField] private float tickSwell = 0.35f;
    [SerializeField] private float endScale = 1f;
    [Tooltip("Seconds the swell after a tick takes to settle")]
    [SerializeField] private float swellDecay = 0.18f;

    private Tower tower;
    private int styleVersion = -1;

    private GameObject currentLinePrefab;
    private GameObject currentStartPrefab;
    private GameObject currentEndPrefab;
    private LineRenderer line;
    private float lineWidth;
    private Gradient prefabColor;
    private Transform start;
    private Transform end;
    private Vector3 startScale;
    private Vector3 endBaseScale;

    private float currentWidth;
    private float currentFlicker;
    private float currentSwell;
    private float currentEndScale;
    private float swell;

    public Vector3 EndPoint { get; private set; }

    private void OnEnable()
    {
        tower = GetComponentInParent<Tower>();
        styleVersion = -1;
    }

    private void OnDisable()
    {
        DestroyPart(ref line);
        DestroyPart(ref start);
        DestroyPart(ref end);
        currentLinePrefab = null;
        currentStartPrefab = null;
        currentEndPrefab = null;
    }

    // A tick went out: the beam swells for a moment
    public void Pulse()
    {
        swell = 1f;
    }

    private void Update()
    {
        if (tower == null)
            return;
        ResolveStyle();

        Vector3 origin = transform.position;
        float range = tower.StatsManager.GetStatValue(Stat.StatType.RANGE);
        Vector3 to = origin + transform.forward * Mathf.Max(0f, range);
        EndPoint = to;

        if (line != null)
        {
            line.SetPosition(0, origin);
            line.SetPosition(1, to);
            // Unscaled wobble keeps going while paused would look odd: game time throughout
            float wobble = currentFlicker > 0f ? currentFlicker * (Mathf.PerlinNoise(Time.time * 9f, 0.37f) * 2f - 1f) : 0f;
            line.widthMultiplier = lineWidth * currentWidth * (1f + wobble + currentSwell * swell * swell);
        }
        swell = Mathf.Max(0f, swell - Time.deltaTime / Mathf.Max(0.01f, swellDecay));

        Vector3 direction = to - origin;
        Quaternion forward = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : transform.rotation;
        if (start != null)
            start.SetPositionAndRotation(origin, forward);
        if (end != null)
            end.SetPositionAndRotation(to, direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-direction) : transform.rotation);
    }

    private void ResolveStyle()
    {
        TowerVisuals visuals = tower.Visuals;
        if (styleVersion == visuals.Version)
            return;
        styleVersion = visuals.Version;

        GameObject wantedLine = linePrefab;
        GameObject wantedStart = startPrefab;
        GameObject wantedEnd = endPrefab;
        currentWidth = width;
        currentFlicker = flicker;
        currentSwell = tickSwell;
        currentEndScale = endScale;
        Gradient color = null;
        for (int i = 0; i < visuals.Count; i++)
        {
            if (!(visuals[i] is BeamVisualUpgrade style))
                continue;
            if (style.line != null)
                wantedLine = style.line;
            if (style.start != null)
                wantedStart = style.start;
            if (style.end != null)
                wantedEnd = style.end;
            if (style.width > 0f)
                currentWidth = style.width;
            if (style.flicker > 0f)
                currentFlicker = style.flicker;
            if (style.tickSwell > 0f)
                currentSwell = style.tickSwell;
            if (style.endScale > 0f)
                currentEndScale = style.endScale;
            if (style.overrideColor)
                color = style.color;
        }

        if (wantedLine != currentLinePrefab)
        {
            DestroyPart(ref line);
            currentLinePrefab = wantedLine;
            if (wantedLine != null)
            {
                GameObject copy = Instantiate(wantedLine, transform.position, transform.rotation, transform);
                line = copy.GetComponentInChildren<LineRenderer>();
                if (line != null)
                {
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lineWidth = line.widthMultiplier;
                    prefabColor = line.colorGradient;
                }
            }
        }
        if (line != null && prefabColor != null)
            line.colorGradient = color ?? prefabColor;

        if (wantedStart != currentStartPrefab)
        {
            DestroyPart(ref start);
            currentStartPrefab = wantedStart;
            if (wantedStart != null)
            {
                start = Instantiate(wantedStart).transform;
                startScale = start.localScale;
            }
        }
        if (wantedEnd != currentEndPrefab)
        {
            DestroyPart(ref end);
            currentEndPrefab = wantedEnd;
            if (wantedEnd != null)
            {
                end = Instantiate(wantedEnd).transform;
                endBaseScale = end.localScale;
            }
        }
        if (start != null)
            start.localScale = startScale * currentEndScale;
        if (end != null)
            end.localScale = endBaseScale * currentEndScale;
    }

    private static void DestroyPart<T>(ref T part) where T : Component
    {
        if (part != null)
            Destroy(part.gameObject);
        part = null;
    }
}
