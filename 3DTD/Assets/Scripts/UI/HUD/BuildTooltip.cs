using TMPro;
using UnityEngine;

// Tooltip left of the build rail for the selected tile: number and hotkey, name, price, four key stats
// and the placement hint. Its notch points at the tile.
public class BuildTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform plate;
    [SerializeField] private RectTransform notch;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private TMP_Text title;
    [SerializeField] private PriceLabel price;
    [SerializeField] private GameObject statsGrid;
    [SerializeField] private StatTile[] statTiles = new StatTile[4];
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private float gap = 14f;
    [SerializeField] private float margin = 12f;

    private RectTransform anchor;

    // placing: the tile is the selected one, so the hint explains placement instead of selection
    public void Show(BuildTile tile, bool placing = true)
    {
        anchor = tile.Rect;
        Building building = tile.Building;
        caption.text = "No. " + UIFormat.TwoDigits(tile.Index + 1) + " · Hotkey " + BuildingManager.HotkeyLabel(tile.Index);
        title.text = building != null ? building.DisplayName : tile.Prefab.name;
        price.SetPrice(tile.Cost);

        Tower tower = tile.Prefab.GetComponent<Tower>();
        StatsScriptableObject config = tower != null ? tower.StatsManager.Config : null;
        statsGrid.SetActive(config != null);
        description.gameObject.SetActive(config == null);
        if (config != null)
        {
            for (int i = 0; i < statTiles.Length && i < TowerStatInfo.Key.Length; i++)
            {
                TowerStatKind kind = TowerStatInfo.Key[i];
                statTiles[i].Set(kind, TowerStatInfo.Format(kind, TowerStatInfo.Base(config, kind)));
            }
        }
        else
        {
            description.text = building != null ? building.Description : "";
        }

        bool touch = Input.touchSupported && !Application.isEditor;
        string hotkey = BuildingManager.HotkeyLabel(tile.Index);
        if (placing)
            hint.text = (touch ? "Tap" : "Click") + " a free pad to place";
        else
            hint.text = (touch ? "Tap" : "Click") + (string.IsNullOrEmpty(hotkey) ? "" : " or press " + hotkey) + " to build";

        gameObject.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(plate);
        Place();
    }

    public void Hide()
    {
        anchor = null;
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (anchor != null)
            Place();
    }

    // Left of the tile, vertically centred on it but kept on screen; the notch stays on the tile's centre
    private void Place()
    {
        RectTransform space = (RectTransform)plate.parent;
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector2 min = space.InverseTransformPoint(corners[0]);
        Vector2 max = space.InverseTransformPoint(corners[2]);
        float centerY = (min.y + max.y) * 0.5f;

        Rect bounds = space.rect;
        Vector2 size = plate.rect.size;
        float x = min.x - gap - size.x;
        float y = Mathf.Clamp(centerY - size.y * 0.5f, bounds.yMin + margin, bounds.yMax - margin - size.y);

        plate.pivot = Vector2.zero;
        plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
        plate.localPosition = new Vector3(x, y, 0f);

        notch.pivot = new Vector2(0f, 0.5f);
        notch.anchorMin = notch.anchorMax = new Vector2(0.5f, 0.5f);
        notch.localPosition = new Vector3(x + size.x - 2f, centerY, 0f);
    }
}
