using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Selectable : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private bool isSelected = false;

    private void Start()
    {
        SelectionManager.OnSelectionChange += HandleSelectionChange;
    }

    // The selection event is static: a sold building would otherwise stay subscribed for the rest of the session
    private void OnDestroy()
    {
        SelectionManager.OnSelectionChange -= HandleSelectionChange;
    }

    private void Update()
    {
        if (isSelected)
        {
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                SellThisTower();
            }
        }
    }

    public void SelectThis()
    {
        SelectionManager.CurrentlySelected = this;
    }

    public void Select()
    {
    }

    public void Deselect()
    {
    }

    private void HandleSelectionChange(Selectable oldSelection, Selectable newSelection)
    {
        if (oldSelection == this)
        {
            isSelected = false;
        }

        if (newSelection == this)
        {
            isSelected = true;
        }
    }

    public void SellThisTower()
    {
        if (!CanSell())
            return;

        if (this.gameObject.TryGetComponent<Building>(out Building building))
            GameManager.Instance.Money += GetSellValue(building);

        if (SelectionManager.CurrentlySelected == this)
            SelectionManager.CurrentlySelected = null;
        Destroy(this.gameObject);
    }

    // Towers can always be sold; a building block only when no tower is attached to any of its six sides
    public bool CanSell()
    {
        if (this.gameObject.TryGetComponent<Tower>(out Tower _))
            return true;

        if (this.gameObject.TryGetComponent<BuildingBlock>(out BuildingBlock _))
            return !HasAttachedTower();

        return false;
    }

    private bool HasAttachedTower()
    {
        Vector3[] directions = { transform.forward, -transform.forward, transform.up, -transform.up, transform.right, -transform.right };
        foreach (Vector3 direction in directions)
        {
            Ray ray = new Ray(transform.position, direction);
            if (Physics.Raycast(ray, out RaycastHit hit, 1f, layerMask) && hit.collider.gameObject.TryGetComponent<Tower>(out Tower _))
                return true;
        }
        return false;
    }

    // Refund share of everything invested; the WORTH stat can raise or lower it per tower
    public static int GetSellValue(Building building)
    {
        // Buildings placed in the scene by hand were never bought
        int invested = building.Invested > 0 ? building.Invested : GameManager.Instance.Price(building.Cost);
        float worth = 1f;
        if (building.TryGetComponent<StatsManager>(out StatsManager stats))
        {
            float statWorth = stats.GetStatValue(Stat.StatType.WORTH);
            if (statWorth > 0f)
                worth = statWorth;
        }
        return GameManager.Instance.SellValue(invested, worth);
    }

    public void UpgradeThisTower(UpgradeModule upgradeModule)
    {
        if (upgradeModule == null)
            return;

        this.gameObject.GetComponent<UpgradeManager>().ActivateUpgradeModule(upgradeModule);
    }
}
