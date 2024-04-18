using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(StatsManager))]
public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private UpgradePath[] upgradePaths;

    public List<IUpgrade> availableUpgrades { get; private set; }
    public List<IUpgrade> activeUpgrades { get; private set; }

    private StatsManager statsManager;

    private void Awake()
    {
        statsManager = GetComponent<StatsManager>();
    }

    private void Start()
    {
        availableUpgrades = new List<IUpgrade>();
        CheckPathBlocking();
    }

    public void CheckPathBlocking()
    {
        int countOfTier1Upgrades = 0;
        int countOfTier2Upgrades = 0;
        bool tier3reached = false;

        foreach (var upgradePath in upgradePaths)
        {
            foreach (var upgradeModule in upgradePath.UpgradeModules)
                upgradeModule.isAvailable = true;

            upgradePath.IsBlocked = false;

            if (upgradePath.UpgradeModules[0].IsActive)
                countOfTier1Upgrades++;

            if (upgradePath.UpgradeModules[1].IsActive)
                countOfTier2Upgrades++;

            if (upgradePath.UpgradeModules[2].IsActive)
                tier3reached = true;
        }

        foreach (var upgradePath in upgradePaths)
        {
            if (countOfTier1Upgrades > 1)
                upgradePath.UpgradeModules[0].isAvailable = false;

            if (countOfTier2Upgrades > 1)
                upgradePath.UpgradeModules[1].isAvailable = false;

            if (tier3reached)
                upgradePath.UpgradeModules[2].isAvailable = false;
        }
    }

    public bool CanUpgradePath(int index)
    {
        if (index < 0 || index >= upgradePaths.Count())
        {
            // Index out of range
            return false;
        }

        var path = upgradePaths[index];
        return !path.IsBlocked || (path.activeUpgrades < 2);
    }

    public UpgradePath[] GetUpgradePaths()
    {
        return upgradePaths;
    }

    public void ActivateUpgradeModule(UpgradeModule upgradeModule)
    {
        foreach (UpgradePath path in upgradePaths)
        {
            foreach (UpgradeModule module in path.UpgradeModules)
            {
                if (module.Equals(upgradeModule))
                {
                    if (!module.isAvailable) 
                        return;

                    if (upgradeModule.Price > GameManager.Instance.Money)
                        return;

                    GameManager.Instance.Money -= upgradeModule.Price;

                    module.ApplyUpgrade(this.gameObject);
                    return;
                }
            }
        }
    }

    public void AddAndActivateUpgrade(IUpgrade upgrade)
    {
        AddUpgrade(upgrade);
        ActivateUpgrade(upgrade);
    }

    public void AddUpgrade(IUpgrade upgrade)
    {
        availableUpgrades.Add(upgrade);
    }

    public void ActivateUpgrade(IUpgrade upgrade)
    {
        if (!availableUpgrades.Contains(upgrade)) return;

        upgrade.ApplyUpgrade(statsManager);
    }

    public bool IsActive(IUpgrade upgrade)
    {
        return activeUpgrades.Contains(upgrade);
    }
}

[System.Serializable]
public class UpgradePath
{
    public int activeUpgrades 
    { 
        get 
        {
            int i = 0;
            foreach (UpgradeModule module in upgradeModules)
            {
                if (module.IsActive)
                    i++;
            }
            return i;
        } 
    }

    public bool IsBlocked = false;

    public UpgradeModule[] UpgradeModules => upgradeModules;
    [SerializeField] private UpgradeModule[] upgradeModules;
}