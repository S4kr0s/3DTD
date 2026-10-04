using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Build rail (redesign concept C): one tile per entry of GameManager.buildingPrefabs in a 2-column grid on
// the right, with hotkeys 1-9 and 0. Selecting a tile starts placement: AnchorPoints build the selected
// prefab, the placement preview shows a ghost at the hovered pad. Clicking the tile again or Esc cancels.
public class BuildingManager : MonoBehaviour
{
    private static readonly KeyCode[] Hotkeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
    };

    [SerializeField] private RectTransform tileParent;
    [SerializeField] private BuildTile tilePrefab;
    [SerializeField] private TMP_Text countLabel;
    [SerializeField] private BuildTooltip tooltip;

    [Header("Collapse")]
    [SerializeField] private GameObject expandedRail;
    [SerializeField] private GameObject collapsedTab;
    [SerializeField] private BevelButton collapseButton;
    [SerializeField] private BevelButton expandButton;

    private readonly List<BuildTile> tiles = new List<BuildTile>();
    private int selectedIndex = -1;
    private BuildTile hoveredTile;

    private static BuildingManager instance;
    public static BuildingManager Instance { get { return instance; } }

    public int SelectedIndex => selectedIndex;
    public bool IsPlacing => selectedIndex >= 0;

    public event Action<int> OnSelectedBuildingChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }
    }

    private void Start()
    {
        if (tilePrefab != null)
            tilePrefab.gameObject.SetActive(false);

        List<GameObject> buildings = GameManager.Instance.Buildings;
        for (int index = 0; index < buildings.Count; index++)
        {
            if (buildings[index] == null)
                continue;
            BuildTile tile = Instantiate(tilePrefab, tileParent);
            tile.gameObject.SetActive(true);
            tile.Setup(index, buildings[index]);
            tile.Clicked += HandleTileClicked;
            tile.HoverChanged += HandleTileHover;
            tiles.Add(tile);
        }

        int towers = buildings.Count(b => b != null && b.GetComponent<Tower>() != null);
        if (countLabel != null)
            countLabel.text = towers + (towers == 1 ? " tower" : " towers");

        if (collapseButton != null)
            collapseButton.Clicked += _ => SetCollapsed(true);
        if (expandButton != null)
            expandButton.Clicked += _ => SetCollapsed(false);
        SetCollapsed(false);
        Select(-1);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;
        for (int i = 0; i < Hotkeys.Length && i < tiles.Count; i++)
        {
            if (Input.GetKeyDown(Hotkeys[i]))
                Select(tiles[i].Index == selectedIndex ? -1 : tiles[i].Index);
        }
    }

    public static string HotkeyLabel(int index)
    {
        return index < 9 ? (index + 1).ToString() : index == 9 ? "0" : "";
    }

    private void HandleTileClicked(BuildTile tile)
    {
        Select(tile.Index == selectedIndex ? -1 : tile.Index);
    }

    public void Select(int index)
    {
        selectedIndex = index;
        BuildTile selectedTile = null;
        foreach (BuildTile tile in tiles)
        {
            bool isSelected = tile.Index == index;
            tile.SetSelected(isSelected);
            if (isSelected)
                selectedTile = tile;
        }

        RefreshTooltip();

        if (selectedTile != null && SelectionManager.CurrentlySelected != null)
            SelectionManager.CurrentlySelected = null;

        OnSelectedBuildingChanged?.Invoke(selectedIndex);
    }

    // Hovering a tile previews it in the tooltip; otherwise the tooltip shows the tile being placed
    private void HandleTileHover(BuildTile tile, bool hovered)
    {
        if (hovered)
            hoveredTile = tile;
        else if (hoveredTile == tile)
            hoveredTile = null;
        RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        BuildTile shownTile = hoveredTile != null && hoveredTile.isActiveAndEnabled ? hoveredTile : tiles.Find(t => t.Index == selectedIndex);
        if (shownTile != null && expandedRail.activeSelf)
            tooltip.Show(shownTile, shownTile.Index == selectedIndex);
        else
            tooltip.Hide();
    }

    // Returns true if a placement was cancelled (Esc closes placement before it opens the pause menu)
    public bool CancelPlacement()
    {
        if (!IsPlacing)
            return false;
        Select(-1);
        return true;
    }

    private void SetCollapsed(bool collapsed)
    {
        expandedRail.SetActive(!collapsed);
        collapsedTab.SetActive(collapsed);
        hoveredTile = null;
        RefreshTooltip();
    }

    public void SetSelectedBuilding(GameObject building)
    {
        Select(GameManager.Instance.Buildings.IndexOf(building));
    }

    public GameObject GetSelectedBuilding()
    {
        if (selectedIndex < 0 || selectedIndex >= GameManager.Instance.Buildings.Count)
            return null;
        return GameManager.Instance.Buildings[selectedIndex];
    }
}
