using System;
using System.Collections.Generic;
using UnityEngine;

public enum MetaEffectType
{
    TowerStatPercent,   // value: % change of a tower stat (fire rate as "+X% faster")
    StartMoney,         // value: flat scrap at the start of a game
    IncomePercent,      // value: % more scrap per popped layer
    WaveBonus,          // value: flat scrap at the end of every wave
    StartLives,         // value: flat hull at the start of a game
    PricePercent,       // value: % off every price (towers, blocks, upgrades)
    RefundPercent,      // value: percentage points added to the sell refund rate
}

// Meta progression (redesign B5): four skill trees bought with Research, which players earn with medals.
// The effects apply to every game started afterwards (see MetaUpgrades).
[CreateAssetMenu(fileName = "MetaUpgradeTree", menuName = "TowerDefense/MetaUpgradeTree", order = 2)]
public class MetaUpgradeTree : ScriptableObject
{
    public const string ResourcePath = "Progress/MetaUpgradeTree";

    [Serializable]
    public class Effect
    {
        public MetaEffectType type;
        [Tooltip("Only for TowerStatPercent")]
        public Stat.StatType stat;
        public float value;
        [Tooltip("Short scope caption, e.g. 'all turrets'")]
        public string scope = "all turrets";
    }

    [Serializable]
    public class Node
    {
        public string id;
        public string displayName;
        public string icon;
        [Range(1, 4)] public int tier = 1;
        [Tooltip("Horizontal slot: 0 left, 1 centre, 2 right")]
        [Range(0, 2)] public int column = 1;
        public int cost = 1;
        public bool capstone;
        [Tooltip("Node ids that must be owned first")]
        public List<string> requires = new List<string>();
        public List<Effect> effects = new List<Effect>();
    }

    [Serializable]
    public class SkillTree
    {
        public string id;
        public string displayName;
        public string icon;
        public List<Node> nodes = new List<Node>();
    }

    public List<SkillTree> trees = new List<SkillTree>();

    private static MetaUpgradeTree instance;

    public static MetaUpgradeTree Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<MetaUpgradeTree>(ResourcePath);
            return instance;
        }
    }

    public Node FindNode(string nodeId, out SkillTree owner)
    {
        foreach (SkillTree tree in trees)
        {
            foreach (Node node in tree.nodes)
            {
                if (node.id == nodeId)
                {
                    owner = tree;
                    return node;
                }
            }
        }
        owner = null;
        return null;
    }

    public static bool RequirementsMet(Node node)
    {
        foreach (string required in node.requires)
        {
            if (!PlayerProgress.OwnsNode(required))
                return false;
        }
        return true;
    }
}
