using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombTowerLightAimUpgrade : Upgrade
{
    public override void ApplyUpgrade(StatsManager statsManager)
    {
        BombTowerActionStrategy[] strategies = statsManager.gameObject.GetComponents<BombTowerActionStrategy>();

        foreach (var strategy in strategies)
        {
            strategy.aimAtTarget = true;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        throw new System.NotImplementedException();
    }
}
