using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeModule : MonoBehaviour
{
    [SerializeField] private Upgrade[] upgrades;
    [SerializeField] private StatUpgrade[] statUpgrades; // needs to be it's own for editor functionality

    public void ApplyUpgrade(GameObject gameObject)
    {
        foreach (Upgrade upgrade in upgrades)
        {
            gameObject.GetComponent<UpgradeManager>().AddAndActivateUpgrade(upgrade);
        }

        foreach (StatUpgrade statUpgrade in statUpgrades)
        {
            gameObject.GetComponent<UpgradeManager>().AddAndActivateUpgrade(statUpgrade);
        }
    }
}
