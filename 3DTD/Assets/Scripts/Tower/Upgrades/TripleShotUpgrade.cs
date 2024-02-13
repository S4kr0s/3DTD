using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeFirepointsUpgrade : Upgrade
{
    [SerializeField] private GameObject[] ToDeactivate;
    [SerializeField] private GameObject[] ToActivate;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (GameObject go in ToDeactivate)
        {
            go.SetActive(false);
        }

        foreach (GameObject go in ToActivate)
        {
            go.SetActive(true);
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        throw new System.NotImplementedException();
    }
}
