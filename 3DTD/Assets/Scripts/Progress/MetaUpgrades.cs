using System.Collections.Generic;
using UnityEngine;

// Totals of the meta upgrades the player owns, applied to every game (not the main-menu backdrop).
// GameManager reads the economy totals; towers get their stat bonuses as modifiers when they start.
public static class MetaUpgrades
{
    private static bool dirty = true;
    private static readonly Dictionary<Stat.StatType, float> statPercent = new Dictionary<Stat.StatType, float>();
    private static float startMoney;
    private static float incomePercent;
    private static float waveBonus;
    private static float startLives;
    private static float pricePercent;
    private static float refundPercent;

    static MetaUpgrades()
    {
        PlayerProgress.Changed += () => dirty = true;
    }

    public static int StartMoney { get { Refresh(); return Mathf.RoundToInt(startMoney); } }
    public static int StartLives { get { Refresh(); return Mathf.RoundToInt(startLives); } }
    public static int WaveBonus { get { Refresh(); return Mathf.RoundToInt(waveBonus); } }
    public static float IncomeMultiplier { get { Refresh(); return 1f + incomePercent / 100f; } }
    public static float PriceMultiplier { get { Refresh(); return Mathf.Max(0.1f, 1f - pricePercent / 100f); } }
    public static float RefundBonus { get { Refresh(); return refundPercent / 100f; } }

    private static void Refresh()
    {
        if (!dirty)
            return;
        dirty = false;
        statPercent.Clear();
        startMoney = incomePercent = waveBonus = startLives = pricePercent = refundPercent = 0f;

        MetaUpgradeTree tree = MetaUpgradeTree.Instance;
        if (tree == null)
            return;

        foreach (string nodeId in PlayerProgress.OwnedNodes)
        {
            MetaUpgradeTree.Node node = tree.FindNode(nodeId, out _);
            if (node == null)
                continue;
            foreach (MetaUpgradeTree.Effect effect in node.effects)
            {
                switch (effect.type)
                {
                    case MetaEffectType.TowerStatPercent:
                        statPercent.TryGetValue(effect.stat, out float current);
                        statPercent[effect.stat] = current + effect.value;
                        break;
                    case MetaEffectType.StartMoney: startMoney += effect.value; break;
                    case MetaEffectType.IncomePercent: incomePercent += effect.value; break;
                    case MetaEffectType.WaveBonus: waveBonus += effect.value; break;
                    case MetaEffectType.StartLives: startLives += effect.value; break;
                    case MetaEffectType.PricePercent: pricePercent += effect.value; break;
                    case MetaEffectType.RefundPercent: refundPercent += effect.value; break;
                }
            }
        }
    }

    // Adds the owned tower bonuses to a freshly built tower. FIRERATE is seconds between shots, so
    // "+X% fire rate" becomes a modifier of -X/(100+X)*100 like the upgrade modules use.
    public static void ApplyTo(StatsManager stats)
    {
        Refresh();
        if (stats == null || statPercent.Count == 0)
            return;
        foreach (KeyValuePair<Stat.StatType, float> entry in statPercent)
        {
            Stat stat = stats.GetStat(entry.Key);
            if (stat == null || Mathf.Approximately(entry.Value, 0f))
                continue;
            stat.AddModifier(ToModifier(entry.Key, entry.Value));
        }
    }

    // The owned meta bonus for one tower stat, in percent (0 without one)
    public static float PercentFor(Stat.StatType type)
    {
        Refresh();
        return statPercent.TryGetValue(type, out float percent) ? percent : 0f;
    }

    public static float ToModifier(Stat.StatType type, float percent)
    {
        if (type == Stat.StatType.FIRERATE)
            return -percent / (100f + percent) * 100f;
        return percent;
    }
}
