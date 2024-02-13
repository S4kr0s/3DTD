using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatUpgrade : IUpgrade
{
    public Stat.StatType targetStat;
    public float upgradeValue;  // this can be a bonus or a modifier
    public bool isModifier;     // to determine if the upgrade is a modifier or direct bonus

    public StatUpgrade(Stat.StatType targetStat, float upgradeValue, bool isModifier)
    {
        this.targetStat = targetStat;
        this.upgradeValue = upgradeValue;
        this.isModifier = isModifier;
    }

    public void ApplyUpgrade(StatsManager statsManager)
    {
        Stat stat = statsManager.GetStat(targetStat);
        if (stat != null)
        {
            if (isModifier)
                stat.AddModifier(upgradeValue);
            else
                stat.AddBonus(upgradeValue);
        }
        statsManager.SetStat(targetStat, stat);
    }

    public void RemoveUpgrade(StatsManager statsManager)
    {
        Stat stat = statsManager.GetStat(targetStat);
        if (stat != null)
        {
            if (isModifier)
                stat.RemoveModifier(upgradeValue);
            else
                stat.RemoveBonus(upgradeValue);
        }
        statsManager.SetStat(targetStat, stat);
    }
}