using System;
using UnityEngine;

// One upgrade path in the tower panel: its three modules as cells, tier 1 to 3. Clicking the path's next
// module buys it; every cell explains itself in a tooltip.
public class UpgradeRow : MonoBehaviour
{
    [SerializeField] private UpgradeCell[] cells = new UpgradeCell[3];

    public event Action<UpgradeRow> Purchase;

    private UpgradePath path;
    private UpgradeModule next;
    private bool subscribed;

    // The path's next module (null when the path is maxed) and the cell that buys it
    public UpgradeModule Module => next;
    public BevelButton Button
    {
        get
        {
            foreach (UpgradeCell cell in cells)
            {
                if (cell != null && cell.gameObject.activeSelf && cell.Module == next)
                    return cell.Button;
            }
            return null;
        }
    }

    private void Awake()
    {
        foreach (UpgradeCell cell in cells)
            cell.Clicked += HandleCellClicked;
    }

    private void OnEnable()
    {
        if (!subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnMoneyChanged += HandleMoneyChanged;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
        subscribed = false;
    }

    public void Bind(UpgradePath upgradePath)
    {
        path = upgradePath;
        Refresh();
    }

    public void Refresh()
    {
        UpgradeModule[] modules = path != null && path.UpgradeModules != null ? path.UpgradeModules : new UpgradeModule[0];
        next = null;
        foreach (UpgradeModule module in modules)
        {
            if (module != null && !module.IsActive)
            {
                next = module;
                break;
            }
        }

        for (int i = 0; i < cells.Length; i++)
        {
            UpgradeModule module = i < modules.Length ? modules[i] : null;
            cells[i].gameObject.SetActive(module != null);
            if (module == null)
                continue;
            UpgradeCell.State state;
            if (module.IsActive)
                state = UpgradeCell.State.Owned;
            else if (!module.isAvailable)
                state = UpgradeCell.State.Blocked;
            else
                state = module == next ? UpgradeCell.State.Next : UpgradeCell.State.Later;
            cells[i].Bind(module, i + 1, state);
        }
    }

    private void HandleCellClicked(UpgradeCell cell)
    {
        if (cell.Module != null && cell.Module == next && cell.CurrentState == UpgradeCell.State.Next)
            Purchase?.Invoke(this);
    }

    private void HandleMoneyChanged(int money)
    {
        foreach (UpgradeCell cell in cells)
        {
            if (cell != null && cell.gameObject.activeSelf)
                cell.UpdateProgress();
        }
    }
}
