using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class UIRedesignBuilder
{
    // Starter meta-progression for the Upgrades screen. Written only when the asset doesn't exist yet,
    // so tuning done in the inspector survives later builder runs. Values are deliberately small: the
    // balance tools (Tools/BalanceDashboard) don't model meta upgrades.
    private static void CreateMetaTree()
    {
        if (AssetDatabase.LoadAssetAtPath<MetaUpgradeTree>(MetaPath) != null)
            return;

        MetaUpgradeTree tree = ScriptableObject.CreateInstance<MetaUpgradeTree>();
        tree.trees = new List<MetaUpgradeTree.SkillTree>
        {
            SkillTreeOf("towers", "Towers", "ic_cube",
                MetaNode("t-hardened", "Hardened rounds", "ic_burst", 1, 1, 1, null, StatBonus(Stat.StatType.DAMAGE, 5f)),
                MetaNode("t-barrels", "Extended barrels", "ic_target", 2, 0, 2, new[] { "t-hardened" }, StatBonus(Stat.StatType.RANGE, 5f)),
                MetaNode("t-loaders", "Rapid loaders", "ic_bolt", 2, 1, 2, new[] { "t-hardened" }, StatBonus(Stat.StatType.FIRERATE, 5f)),
                MetaNode("t-payloads", "Heavy payloads", "ic_radius", 2, 2, 2, new[] { "t-hardened" }, StatBonus(Stat.StatType.RADIUS, 10f)),
                MetaNode("t-optics", "Long-range optics", "ic_zoom", 3, 0, 3, new[] { "t-barrels" }, StatBonus(Stat.StatType.RANGE, 5f)),
                MetaNode("t-feeds", "Overclocked feeds", "ic_bolt", 3, 1, 3, new[] { "t-loaders" }, StatBonus(Stat.StatType.FIRERATE, 5f)),
                MetaNode("t-warheads", "Dense warheads", "ic_burst", 3, 2, 3, new[] { "t-payloads" }, StatBonus(Stat.StatType.DAMAGE, 5f)),
                Capstone("t-armory", "Master armory", "ic_crown", 5, new[] { "t-optics", "t-feeds", "t-warheads" },
                    StatBonus(Stat.StatType.DAMAGE, 5f), StatBonus(Stat.StatType.FIRERATE, 5f))),

            SkillTreeOf("economy", "Economy", "ic_coin_hex",
                MetaNode("e-salvage", "Salvage crews", "ic_coin", 1, 1, 1, null, MetaEffect(MetaEffectType.StartMoney, 50f, "every game")),
                MetaNode("e-refinery", "Efficient refinery", "ic_recycle", 2, 0, 2, new[] { "e-salvage" }, MetaEffect(MetaEffectType.IncomePercent, 5f, "every pop")),
                MetaNode("e-contracts", "Wave contracts", "ic_chevrons_double", 2, 1, 2, new[] { "e-salvage" }, MetaEffect(MetaEffectType.WaveBonus, 10f, "per wave")),
                MetaNode("e-resale", "Resale network", "ic_coin", 2, 2, 2, new[] { "e-salvage" }, MetaEffect(MetaEffectType.RefundPercent, 5f, "when selling")),
                MetaNode("e-bulk", "Bulk orders", "ic_cube", 3, 0, 3, new[] { "e-refinery" }, MetaEffect(MetaEffectType.PricePercent, 3f, "towers & upgrades")),
                MetaNode("e-chest", "War chest", "ic_coin_hex", 3, 1, 3, new[] { "e-contracts" }, MetaEffect(MetaEffectType.StartMoney, 75f, "every game")),
                MetaNode("e-brokers", "Scrap brokers", "ic_recycle", 3, 2, 3, new[] { "e-resale" }, MetaEffect(MetaEffectType.RefundPercent, 5f, "when selling")),
                Capstone("e-empire", "Trade empire", "ic_crown", 5, new[] { "e-bulk", "e-chest", "e-brokers" },
                    MetaEffect(MetaEffectType.IncomePercent, 5f, "every pop"), MetaEffect(MetaEffectType.PricePercent, 2f, "towers & upgrades"))),

            SkillTreeOf("hull", "Hull", "ic_shield",
                MetaNode("h-plating", "Reinforced plating", "ic_shield", 1, 1, 1, null, MetaEffect(MetaEffectType.StartLives, 10f, "not on Impossible")),
                MetaNode("h-bulkheads", "Bulkheads", "ic_shield", 2, 0, 2, new[] { "h-plating" }, MetaEffect(MetaEffectType.StartLives, 10f, "not on Impossible")),
                MetaNode("h-funds", "Emergency funds", "ic_coin", 2, 2, 2, new[] { "h-plating" }, MetaEffect(MetaEffectType.StartMoney, 25f, "every game")),
                MetaNode("h-core", "Armored core", "ic_cube", 3, 1, 3, new[] { "h-bulkheads", "h-funds" }, MetaEffect(MetaEffectType.StartLives, 15f, "not on Impossible")),
                Capstone("h-fortress", "Fortress", "ic_crown", 5, new[] { "h-core" },
                    MetaEffect(MetaEffectType.StartLives, 25f, "not on Impossible"), MetaEffect(MetaEffectType.WaveBonus, 5f, "per wave"))),

            SkillTreeOf("global", "Global", "ic_globe",
                MetaNode("g-manual", "Field manual", "ic_book", 1, 1, 1, null, StatBonus(Stat.StatType.RANGE, 3f), MetaEffect(MetaEffectType.StartMoney, 25f, "every game")),
                MetaNode("g-supply", "Supply lines", "ic_chevrons_double", 2, 0, 2, new[] { "g-manual" }, MetaEffect(MetaEffectType.WaveBonus, 5f, "per wave")),
                MetaNode("g-deploy", "Quick deploy", "ic_arrow_up", 2, 2, 2, new[] { "g-manual" }, MetaEffect(MetaEffectType.PricePercent, 2f, "towers & upgrades")),
                MetaNode("g-combined", "Combined arms", "ic_cluster", 3, 1, 3, new[] { "g-supply", "g-deploy" }, StatBonus(Stat.StatType.DAMAGE, 3f), StatBonus(Stat.StatType.FIRERATE, 3f)),
                Capstone("g-strategy", "Grand strategy", "ic_crown", 5, new[] { "g-combined" },
                    StatBonus(Stat.StatType.DAMAGE, 5f), MetaEffect(MetaEffectType.StartLives, 10f, "not on Impossible"), MetaEffect(MetaEffectType.StartMoney, 50f, "every game"))),
        };

        AssetDatabase.CreateAsset(tree, MetaPath);
        AssetDatabase.SaveAssets();
    }

    private static MetaUpgradeTree.SkillTree SkillTreeOf(string id, string name, string icon, params MetaUpgradeTree.Node[] nodes)
    {
        return new MetaUpgradeTree.SkillTree { id = id, displayName = name, icon = icon, nodes = new List<MetaUpgradeTree.Node>(nodes) };
    }

    private static MetaUpgradeTree.Node MetaNode(string id, string name, string icon, int tier, int column, int cost, string[] requires, params MetaUpgradeTree.Effect[] effects)
    {
        return new MetaUpgradeTree.Node
        {
            id = id,
            displayName = name,
            icon = icon,
            tier = tier,
            column = column,
            cost = cost,
            requires = requires != null ? new List<string>(requires) : new List<string>(),
            effects = new List<MetaUpgradeTree.Effect>(effects),
        };
    }

    private static MetaUpgradeTree.Node Capstone(string id, string name, string icon, int cost, string[] requires, params MetaUpgradeTree.Effect[] effects)
    {
        MetaUpgradeTree.Node node = MetaNode(id, name, icon, 4, 1, cost, requires, effects);
        node.capstone = true;
        return node;
    }

    private static MetaUpgradeTree.Effect StatBonus(Stat.StatType stat, float percent)
    {
        return new MetaUpgradeTree.Effect { type = MetaEffectType.TowerStatPercent, stat = stat, value = percent, scope = "all turrets" };
    }

    private static MetaUpgradeTree.Effect MetaEffect(MetaEffectType type, float value, string scope)
    {
        return new MetaUpgradeTree.Effect { type = type, value = value, scope = scope };
    }
}
