using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeRotationPointUpgrade : Upgrade
{
    [SerializeField] private GameObject newRotationPoint;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        statsManager.gameObject.GetComponent<Tower>().SetRotationPoint(newRotationPoint);
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        throw new System.NotImplementedException();
    }
}
