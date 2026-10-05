using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UpgradeModule
{
    public string Name => name;
    public string Description => description;
    public int Price => price;
    public StatUpgrade[] StatUpgrades => statUpgrades ?? new StatUpgrade[0];
    // Behaviour changes beyond plain stats (strategy swaps, cluster bomblets, salvage...); visual upgrades don't count
    public bool HasBehaviourUpgrades
    {
        get
        {
            if (upgrades == null)
                return false;
            foreach (Upgrade upgrade in upgrades)
            {
                if (upgrade != null && !(upgrade is VisualUpgrade))
                    return true;
            }
            return false;
        }
    }

    [SerializeField] private string name;
    [Multiline]
    [SerializeField] private string description;
    [SerializeField] private int price;
    [SerializeField] private Upgrade[] upgrades;
    [SerializeField] private StatUpgrade[] statUpgrades; // needs to be it's own for editor functionality
    [SerializeField] private GameObject[] upgradeVisualGameObjects;
    [SerializeField] private GameObject[] disableVisualGameObjects;
    public bool IsActive => isActive;
    [SerializeField] private bool isActive = false;
    [SerializeField] public bool isAvailable = true;

    public UpgradeModule(string name, int price, Upgrade[] upgrades, StatUpgrade[] statUpgrades)
    {
        this.name = name;
        this.price = price;
        this.upgrades = upgrades;
        this.statUpgrades = statUpgrades;
        this.isActive = false;
        this.isAvailable = true;
    }

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

        foreach (GameObject visual in upgradeVisualGameObjects)
        {
            visual.SetActive(true);
        }

        foreach (GameObject visual in disableVisualGameObjects)
        {
            visual.SetActive(false);
        }

        isActive = true;
    }
}