using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeActionStrategyUpgrade : Upgrade
{
    [SerializeField] private ActionStrategy newActionStrategy;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        statsManager.gameObject.GetComponent<Tower>().SetActionStrategy(newActionStrategy);
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        throw new System.NotImplementedException();
    }
}
