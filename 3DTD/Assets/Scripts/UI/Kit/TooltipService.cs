using TMPro;
using UnityEngine;

// One shared tooltip per canvas: an orange-rimmed plate whose triangular notch points at the element that
// opened it. Placed beside the anchor, kept inside the canvas. Elements open it through TooltipTrigger.
public class TooltipService : MonoBehaviour
{
    public enum Side
    {
        Left,
        Right,
        Above,
        Below,
    }

    [SerializeField] private RectTransform plate;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text body;
    [SerializeField] private RectTransform notch;
    [SerializeField] private float gap = 12f;
    [SerializeField] private float margin = 10f;

    private static TooltipService instance;
    public static TooltipService Instance => instance;

    private RectTransform currentAnchor;
    private RectTransform currentOutside;
    private Side currentSide;

    // The tooltip of the canvas an element is on (the main menu and the HUD each have one)
    public static TooltipService For(Transform element)
    {
        Canvas canvas = element != null ? element.GetComponentInParent<Canvas>(true) : null;
        TooltipService service = canvas != null ? canvas.rootCanvas.GetComponentInChildren<TooltipService>(true) : null;
        return service != null ? service : instance;
    }

    private void Awake()
    {
        instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // outside: optional rect (e.g. the panel the anchor sits in) the tooltip is placed beside instead of the
    // anchor itself, so it doesn't cover the anchor's neighbours; the notch still points at the anchor
    public void Show(RectTransform anchor, Side side, string titleText, string bodyText, RectTransform outside = null)
    {
        if (anchor == null || plate == null)
            return;
        currentAnchor = anchor;
        currentOutside = outside;
        currentSide = side;
        title.text = titleText;
        title.gameObject.SetActive(!string.IsNullOrEmpty(titleText));
        body.text = bodyText;
        body.gameObject.SetActive(!string.IsNullOrEmpty(bodyText));
        plate.gameObject.SetActive(true);
        notch.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(plate);
        Place();
    }

    public void Hide(RectTransform anchor)
    {
        if (anchor == currentAnchor)
            Hide();
    }

    public void Hide()
    {
        currentAnchor = null;
        currentOutside = null;
        if (plate != null)
            plate.gameObject.SetActive(false);
        if (notch != null)
            notch.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (currentAnchor == null)
            return;
        if (!currentAnchor.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }
        Place();
    }

    private void Place()
    {
        RectTransform parent = (RectTransform)plate.parent;
        Rect anchorRect = LocalRect(currentAnchor, parent);
        if (currentOutside != null)
        {
            Rect outsideRect = LocalRect(currentOutside, parent);
            if (currentSide == Side.Left || currentSide == Side.Right)
                anchorRect = Rect.MinMaxRect(outsideRect.xMin, anchorRect.yMin, outsideRect.xMax, anchorRect.yMax);
            else
                anchorRect = Rect.MinMaxRect(anchorRect.xMin, outsideRect.yMin, anchorRect.xMax, outsideRect.yMax);
        }
        Vector2 size = plate.rect.size;
        Rect bounds = parent.rect;

        Vector2 position;
        Vector2 notchPosition;
        float notchAngle;
        switch (currentSide)
        {
            case Side.Left:
                position = new Vector2(anchorRect.xMin - gap - size.x, anchorRect.center.y - size.y * 0.5f);
                notchPosition = new Vector2(anchorRect.xMin - gap, anchorRect.center.y);
                notchAngle = 0f;
                break;
            case Side.Above:
                position = new Vector2(anchorRect.center.x - size.x * 0.5f, anchorRect.yMax + gap);
                notchPosition = new Vector2(anchorRect.center.x, anchorRect.yMax + gap);
                notchAngle = -90f;
                break;
            case Side.Below:
                position = new Vector2(anchorRect.center.x - size.x * 0.5f, anchorRect.yMin - gap - size.y);
                notchPosition = new Vector2(anchorRect.center.x, anchorRect.yMin - gap);
                notchAngle = 90f;
                break;
            default:
                position = new Vector2(anchorRect.xMax + gap, anchorRect.center.y - size.y * 0.5f);
                notchPosition = new Vector2(anchorRect.xMax + gap, anchorRect.center.y);
                notchAngle = 180f;
                break;
        }

        position.x = Mathf.Clamp(position.x, bounds.xMin + margin, Mathf.Max(bounds.xMin + margin, bounds.xMax - margin - size.x));
        position.y = Mathf.Clamp(position.y, bounds.yMin + margin, Mathf.Max(bounds.yMin + margin, bounds.yMax - margin - size.y));

        plate.pivot = Vector2.zero;
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.localPosition = new Vector3(position.x, position.y, 0f);

        // The notch sprite points right; its base sits on the plate edge and the tip points at the anchor
        notch.anchorMin = notch.anchorMax = new Vector2(0.5f, 0.5f);
        notch.pivot = new Vector2(0f, 0.5f);
        notch.localPosition = new Vector3(notchPosition.x, notchPosition.y, 0f);
        notch.localRotation = Quaternion.Euler(0f, 0f, notchAngle);
    }

    private static readonly Vector3[] CornerBuffer = new Vector3[4];

    private static Rect LocalRect(RectTransform target, RectTransform space)
    {
        Vector3[] corners = CornerBuffer;
        target.GetWorldCorners(corners);
        Vector2 min = space.InverseTransformPoint(corners[0]);
        Vector2 max = space.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
