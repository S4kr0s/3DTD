using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Tower panel (redesign B2): opens top-left under the HUD for the selected tower or building block.
// Fixed order: header (emblem, name, tier pips, kills, close), target stepper, 4 x 2 stat grid, upgrades, sell.
public class UpgradePanelManager : MonoBehaviour
{
    private static readonly string[] TargetingNames = { "First", "Last", "Strongest", "Nearest", "Farthest" };
    private static readonly string[] TargetingDescriptions =
    {
        "Shoots the enemy furthest along the path, the one closest to the exit.",
        "Shoots the enemy that has travelled the least.",
        "Shoots the toughest enemy in range: largest shape, darkest colour.",
        "Shoots the enemy closest to the tower.",
        "Shoots the enemy farthest from the tower that is still in range.",
    };

    [SerializeField] private RectTransform panel;
    [Tooltip("Top of the bottom HUD row; the panel never grows below it and scrolls instead")]
    [SerializeField] private float bottomLimit = 74f;
    [SerializeField] private RectTransform content;

    [Header("Header")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private Image[] tierPips = new Image[4];
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private BevelButton closeButton;

    [Header("Targeting")]
    [SerializeField] private GameObject targetingRow;
    [SerializeField] private Stepper targetingStepper;
    [SerializeField] private TooltipTrigger targetingTooltip;
    [SerializeField] private GameObject aimRow;
    [SerializeField] private Slider aimSlider;

    [Header("Stats")]
    [SerializeField] private GameObject statsGrid;
    [SerializeField] private StatTile[] statTiles = new StatTile[8];

    [Header("Upgrades")]
    [SerializeField] private GameObject upgradesCaption;
    [SerializeField] private RectTransform upgradeList;
    [SerializeField] private UpgradeRow upgradeRowTemplate;

    [Header("Sell")]
    [SerializeField] private BevelButton sellButton;
    [SerializeField] private TMP_Text sellLabel;
    [SerializeField] private TMP_Text sellValue;
    [SerializeField] private TooltipTrigger sellTooltip;

    private readonly List<UpgradeRow> rows = new List<UpgradeRow>();
    private Selectable shown;
    private Tower shownTower;
    private float nextRefresh;

    private static UpgradePanelManager instance;
    public static UpgradePanelManager Instance { get { return instance; } }

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

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

        if (upgradeRowTemplate != null)
            upgradeRowTemplate.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }

    private void Start()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChanged;

        closeButton.Clicked += _ => ClearSelection();
        sellButton.Clicked += _ => Sell();
        targetingStepper.SetOptions(TargetingNames, 0);
        targetingStepper.OnIndexChanged += HandleTargetingChanged;
        aimSlider.onValueChanged.AddListener(HandleAimChanged);
        if (targetingTooltip != null)
            targetingTooltip.Provider = () =>
            {
                int index = Mathf.Clamp(targetingStepper.Index, 0, TargetingNames.Length - 1);
                return ("Targeting: " + TargetingNames[index], TargetingDescriptions[index] + "\nThe arrows switch the mode.");
            };
        if (sellTooltip != null)
            sellTooltip.Provider = SellTooltipText;
    }

    private (string title, string body) SellTooltipText()
    {
        if (shown == null)
            return ("", "");
        Building building = shown.GetComponent<Building>();
        if (!shown.CanSell())
            return ("Sell block", "Remove the towers on its sides first.");
        int refund = building != null ? Selectable.GetSellValue(building) : 0;
        string what = shownTower != null ? "tower" : "block";
        return ("Sell " + what, "Returns " + refund + " scrap of what you spent on it and its upgrades.\nHotkey: Delete");
    }

    private void OnDestroy()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChanged;
    }

    public void HandleSelectionChanged(Selectable oldSelection, Selectable newSelection)
    {
        if (newSelection != null && (newSelection.TryGetComponent(out Tower _) || newSelection.TryGetComponent(out BuildingBlock _)))
            Show(newSelection);
        else
            ClearUI();
    }

    public void ClearSelection()
    {
        SelectionManager.CurrentlySelected = null;
    }

    public void ClearUI()
    {
        shown = null;
        shownTower = null;
        panel.gameObject.SetActive(false);
        TooltipService tooltip = TooltipService.For(panel);
        if (tooltip != null)
            tooltip.Hide();
    }

    private void Show(Selectable selection)
    {
        shown = selection;
        shownTower = selection.GetComponent<Tower>();
        panel.gameObject.SetActive(true);

        bool isTower = shownTower != null;
        targetingRow.SetActive(isTower);
        statsGrid.SetActive(isTower);
        upgradesCaption.SetActive(isTower && shownTower.UpgradeManager.GetUpgradePaths().Length > 0);
        upgradeList.gameObject.SetActive(isTower);
        aimRow.SetActive(isTower && shownTower.UseRotationSlider && shownTower.Rotationbase != null);

        if (isTower)
        {
            targetingStepper.SetIndex((int)shownTower.TargetBehaviour, false);
            if (aimRow.activeSelf)
                // The euler angles of the rotation base only match the slider for towers on top faces
                aimSlider.SetValueWithoutNotify(shownTower.AimAngle >= 0f ? shownTower.AimAngle : shownTower.Rotationbase.transform.localEulerAngles.y);
            BuildRows();
            for (int i = 0; i < statTiles.Length && i < TowerStatInfo.Grid.Length; i++)
                statTiles[i].Set(TowerStatInfo.Grid[i], "");
        }
        Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        FitHeight();
        if (TooltipService.For(panel) != null)
            TooltipService.For(panel).Hide();
    }

    private void BuildRows()
    {
        UpgradePath[] paths = shownTower.UpgradeManager.GetUpgradePaths();
        shownTower.UpgradeManager.CheckPathBlocking();
        while (rows.Count < paths.Length)
        {
            UpgradeRow row = Instantiate(upgradeRowTemplate, upgradeList);
            row.Purchase += Buy;
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < paths.Length && paths[i] != null;
            rows[i].gameObject.SetActive(used);
            if (used)
                rows[i].Bind(paths[i]);
        }
    }

    private void Update()
    {
        if (shown == null)
        {
            if (panel.gameObject.activeSelf)
                ClearUI();
            return;
        }
        // Kills, damage-dependent stats and the refund change while the panel is open
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }
    }

    private void LateUpdate()
    {
        if (IsOpen)
            FitHeight();
    }

    private void Refresh()
    {
        UITheme theme = UITheme.Current;
        Building building = shown.GetComponent<Building>();
        title.text = building != null && !string.IsNullOrEmpty(building.DisplayName) ? building.DisplayName : shown.name;

        int tier = shownTower != null ? shownTower.Tier : 1;
        for (int i = 0; i < tierPips.Length; i++)
        {
            tierPips[i].gameObject.SetActive(shownTower != null);
            tierPips[i].color = i < tier ? theme.accent : new Color(1f, 1f, 1f, 0.22f);
        }
        subtitle.text = shownTower != null
            ? "Tier " + tier + " · " + shownTower.Kills + (shownTower.Kills == 1 ? " kill" : " kills")
            : "Platform for towers";

        if (shownTower != null)
        {
            for (int i = 0; i < statTiles.Length && i < TowerStatInfo.Grid.Length; i++)
            {
                TowerStatKind kind = TowerStatInfo.Grid[i];
                statTiles[i].SetValue(TowerStatInfo.Format(kind, TowerStatInfo.Live(shownTower, kind)));
            }
        }

        bool canSell = shown.CanSell();
        sellButton.SetInteractable(canSell);
        sellLabel.text = shownTower != null ? "Sell tower" : (canSell ? "Sell block" : "Remove its towers first");
        sellValue.text = building != null ? "+" + UIFormat.TabularLabel(Selectable.GetSellValue(building)) : "";
        sellValue.gameObject.SetActive(canSell);
    }

    private void Buy(UpgradeRow row)
    {
        if (shownTower == null || row.Module == null)
            return;
        SelectionManager.Instance.UpgradeCurrentTower(row.Module);
        shownTower.UpgradeManager.CheckPathBlocking();
        foreach (UpgradeRow other in rows)
        {
            if (other.gameObject.activeSelf)
                other.Refresh();
        }
        Refresh();
    }

    private void Sell()
    {
        if (shown != null)
            shown.SellThisTower();
    }

    private void HandleTargetingChanged(int index)
    {
        if (shownTower != null)
            shownTower.ChangeTargettingBehaviour((TargetBehaviour)index);
    }

    private void HandleAimChanged(float value)
    {
        if (shownTower != null && shownTower.UseRotationSlider)
            shownTower.RotateTower(value);
    }

    // Grows with the content, but stops above the bottom HUD row; the content scrolls inside
    private void FitHeight()
    {
        RectTransform parent = (RectTransform)panel.parent;
        float available = parent.rect.height + panel.anchoredPosition.y - bottomLimit;
        float wanted = LayoutUtility.GetPreferredHeight(content);
        float height = Mathf.Min(wanted, available);
        if (!Mathf.Approximately(panel.rect.height, height))
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
