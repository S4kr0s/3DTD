using TMPro;
using UnityEngine;

// World selection (redesign B2): the selected tower gets a flat hexagon ring on its base plane, a dashed
// range circle at its real range and a small "Range X" tag below it in the HUD.
public class SelectionIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform rangeTag;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private float hexRadius = 0.8f;
    [Tooltip("Gap between the bottom of the range circle and the tag, in reference px")]
    [SerializeField] private float tagGap = 10f;

    private WorldRing ring;
    private Tower tower;
    private float shownRange = -1f;
    private Camera mainCamera;

    private void Start()
    {
        ring = WorldRing.Create("Selection Ring");
        ring.StyleAsSelection();
        ring.SetVisible(false);
        rangeTag.gameObject.SetActive(false);
        SelectionManager.OnSelectionChange += HandleSelectionChange;
    }

    private void OnDestroy()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChange;
        if (ring != null)
            Destroy(ring.gameObject);
    }

    private void HandleSelectionChange(Selectable oldSelection, Selectable newSelection)
    {
        tower = newSelection != null ? newSelection.GetComponent<Tower>() : null;
        shownRange = -1f;
        if (tower == null)
        {
            ring.SetVisible(false);
            rangeTag.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (tower == null)
        {
            if (ring != null && ring.gameObject.activeSelf)
            {
                ring.SetVisible(false);
                rangeTag.gameObject.SetActive(false);
            }
            return;
        }

        // The ring lies on the tower's base plane (buildings face along their anchor's forward axis),
        // at the targetting sphere's radius there
        float range = tower.StatsManager.GetStatValue(Stat.StatType.RANGE);
        float radius = tower.GetGroundRangeRadius(range);
        ring.SetVisible(true);
        ring.Place(tower.transform.position, tower.transform.forward, radius, hexRadius);

        if (!Mathf.Approximately(range, shownRange))
        {
            shownRange = range;
            rangeText.text = "Range " + UIFormat.Short(range);
        }
        PlaceTag(radius);
    }

    private void PlaceTag(float radius)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 world = ring.LowestScreenPoint(mainCamera, radius);
        rangeTag.pivot = new Vector2(0.5f, 1f);
        bool placed = UIScreenPoint.TryPlace(rangeTag, mainCamera, world, new Vector2(0f, -tagGap));
        if (rangeTag.gameObject.activeSelf != placed)
            rangeTag.gameObject.SetActive(placed);
    }
}
