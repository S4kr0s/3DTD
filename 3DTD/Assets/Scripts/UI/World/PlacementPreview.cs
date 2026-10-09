using UnityEngine;
using UnityEngine.UI;

// While a build tile is selected, the hovered pad shows a dashed hexagon ghost of the tower, its range
// circle and the tower's icon in a hexagon frame above it. Red when the pad is taken or too expensive.
public class PlacementPreview : MonoBehaviour
{
    [SerializeField] private RectTransform ghostIcon;
    [SerializeField] private Image ghostImage;
    [SerializeField] private float hexRadius = 0.7f;
    [Tooltip("Lift of the icon above the pad, in reference px")]
    [SerializeField] private float iconLift = 24f;

    private static PlacementPreview instance;
    public static PlacementPreview Instance => instance;

    private WorldRing ring;
    private AnchorPoint hovered;
    private bool hoveredValid;
    private bool hoveredAffordable;
    private int lastFrameHovered = -1;
    private Camera mainCamera;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        ring = WorldRing.Create("Placement Ghost");
        ring.StyleAsGhost(true);
        ring.SetVisible(false);
        ghostIcon.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (ring != null)
            Destroy(ring.gameObject);
    }

    public void Hover(AnchorPoint anchor, bool free, bool affordable)
    {
        hovered = anchor;
        lastFrameHovered = Time.frameCount;
        bool valid = free && affordable;
        if (valid != hoveredValid || affordable != hoveredAffordable)
            ring.StyleAsGhost(valid);
        hoveredValid = valid;
        hoveredAffordable = affordable;
    }

    public void Unhover(AnchorPoint anchor)
    {
        if (hovered == anchor)
            hovered = null;
    }

    private void LateUpdate()
    {
        BuildingManager manager = BuildingManager.Instance;
        GameObject prefab = manager != null ? manager.GetSelectedBuilding() : null;
        // OnMouseOver stops when the cursor leaves the pad without an exit message (e.g. over UI)
        bool show = prefab != null && hovered != null && Time.frameCount - lastFrameHovered <= 1;
        if (!show)
        {
            if (ring.gameObject.activeSelf)
                ring.SetVisible(false);
            if (ghostIcon.gameObject.activeSelf)
                ghostIcon.gameObject.SetActive(false);
            return;
        }

        Transform pad = hovered.AnchorPointPosition != null ? hovered.AnchorPointPosition : hovered.transform;
        Vector3 normal = hovered.transform.forward;
        Tower tower = prefab.GetComponent<Tower>();
        float radius = 0f;
        if (tower != null && tower.StatsManager.Config != null)
        {
            // Built towers get the meta range bonus as a modifier (Tower.Start); the ring shows the same range
            float range = tower.StatsManager.Config.Range;
            if (GameManager.Instance != null && !GameManager.Instance.IsMainMenu)
                range *= 1f + MetaUpgrades.ToModifier(Stat.StatType.RANGE, MetaUpgrades.PercentFor(Stat.StatType.RANGE)) / 100f;
            radius = tower.GetGroundRangeRadius(range);
        }

        ring.SetVisible(true);
        ring.Place(pad.position, normal, radius, hexRadius);

        Building building = prefab.GetComponent<Building>();
        ghostImage.sprite = building != null ? building.UISprite : null;
        PlaceIcon(pad.position);
    }

    private void PlaceIcon(Vector3 world)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;
        bool visible = ghostImage.sprite != null && UIScreenPoint.TryPlace(ghostIcon, mainCamera, world, new Vector2(0f, iconLift));
        if (ghostIcon.gameObject.activeSelf != visible)
            ghostIcon.gameObject.SetActive(visible);
        if (!visible)
            return;
        Color color = ghostImage.color;
        color.a = hoveredValid ? 0.85f : 0.45f;
        ghostImage.color = color;
    }
}
