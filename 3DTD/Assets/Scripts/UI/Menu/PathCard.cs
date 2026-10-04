using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One upgrade path on the encyclopedia's right page: path letter, the path's first module and its price,
// then chips only: stat changes from base to the full path (before -> after). Paths that only change
// behaviour show the names of their later tiers instead.
public class PathCard : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text price;
    [SerializeField] private RectTransform chipParent;
    [SerializeField] private StatChip chipTemplate;
    [Tooltip("Three path cards share one page, so each shows at most this many stat chips")]
    [SerializeField] private int maxStatChips = 4;

    private readonly List<StatChip> chips = new List<StatChip>();

    public void Set(int pathIndex, UpgradePath path, StatsScriptableObject config)
    {
        UITheme theme = UITheme.Current;
        chipTemplate.gameObject.SetActive(false);
        UpgradeModule[] modules = path.UpgradeModules ?? new UpgradeModule[0];
        UpgradeModule first = modules.Length > 0 ? modules[0] : null;

        caption.text = "Path " + (char)('A' + pathIndex);
        title.text = first != null ? first.Name : "—";
        price.text = first != null ? GameManager.PriceOf(first.Price).ToString() : "";
        icon.sprite = theme.Icon(TowerStatInfo.ModuleIcon(first));

        List<StatUpgrade> all = new List<StatUpgrade>();
        foreach (UpgradeModule module in modules)
        {
            if (module != null)
                all.AddRange(module.StatUpgrades);
        }

        int used = 0;
        foreach (TowerStatKind kind in TowerStatInfo.Grid)
        {
            Stat.StatType type = TowerStatInfo.StatType(kind);
            if (used >= maxStatChips || !all.Exists(u => u != null && u.targetStat == type))
                continue;
            string before = TowerStatInfo.Format(kind, StatsManager.GetBaseValue(config, type));
            string after = TowerStatInfo.Format(kind, TowerStatInfo.After(config, type, all));
            // Changes that round away (e.g. 0.28/s -> 0.35/s) would read "0.3 > 0.3"
            if (before == after)
                continue;
            Chip(used++).SetChange(theme.Icon(TowerStatInfo.Icon(kind)), TowerStatInfo.IconTint(kind), TowerStatInfo.Label(kind), before, after);
        }
        if (used == 0)
        {
            for (int t = 1; t < modules.Length; t++)
            {
                if (modules[t] != null)
                    Chip(used++).SetTier(UIFormat.Roman(t + 1), modules[t].Name);
            }
        }
        for (int i = used; i < chips.Count; i++)
            chips[i].gameObject.SetActive(false);
    }

    private StatChip Chip(int index)
    {
        while (chips.Count <= index)
            chips.Add(Instantiate(chipTemplate, chipParent));
        chips[index].gameObject.SetActive(true);
        return chips[index];
    }
}
