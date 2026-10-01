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
        availableUpgrades = new List<IUpgrade>();
        activeUpgrades = new List<IUpgrade>();
    }

    private void Start()
    {
        CheckPathBlocking();
    }

    public void CheckPathBlocking()
    {
        if (upgradePaths == null)
            return;

        int countOfTier1Upgrades = 0;
        int countOfTier2Upgrades = 0;
        bool tier3reached = false;

        foreach (var upgradePath in upgradePaths)
        {
            if (upgradePath == null || upgradePath.UpgradeModules == null)
                continue;

            foreach (var upgradeModule in upgradePath.UpgradeModules)
            {
                if (upgradeModule == null)
                    continue;

                upgradeModule.isAvailable = true;
            }

            upgradePath.IsBlocked = false;

            if (upgradePath.UpgradeModules.Length > 0 && upgradePath.UpgradeModules[0] != null && upgradePath.UpgradeModules[0].IsActive)
                countOfTier1Upgrades++;

            if (upgradePath.UpgradeModules.Length > 1 && upgradePath.UpgradeModules[1] != null && upgradePath.UpgradeModules[1].IsActive)
                countOfTier2Upgrades++;

            if (upgradePath.UpgradeModules.Length > 2 && upgradePath.UpgradeModules[2] != null && upgradePath.UpgradeModules[2].IsActive)
                tier3reached = true;
        }

        foreach (var upgradePath in upgradePaths)
        {
            if (upgradePath == null || upgradePath.UpgradeModules == null)
                continue;

            if (countOfTier1Upgrades > 1 && upgradePath.UpgradeModules.Length > 0 && upgradePath.UpgradeModules[0] != null)
                upgradePath.UpgradeModules[0].isAvailable = false;

            if (countOfTier2Upgrades > 1 && upgradePath.UpgradeModules.Length > 1 && upgradePath.UpgradeModules[1] != null)
                upgradePath.UpgradeModules[1].isAvailable = false;

            if (tier3reached && upgradePath.UpgradeModules.Length > 2 && upgradePath.UpgradeModules[2] != null)
                upgradePath.UpgradeModules[2].isAvailable = false;
        }
    }

    public bool CanUpgradePath(int index)
    {
        if (upgradePaths == null || index < 0 || index >= upgradePaths.Count())
        {
            // Index out of range
            return false;
        }

        var path = upgradePaths[index];
        if (path == null)
            return false;

        return !path.IsBlocked || (path.activeUpgrades < 2);
    }

    public UpgradePath[] GetUpgradePaths()
    {
        return upgradePaths ?? new UpgradePath[0];
    }

    public void ActivateUpgradeModule(UpgradeModule upgradeModule)
    {
        if (upgradePaths == null)
            return;

        foreach (UpgradePath path in upgradePaths)
        {
            if (path == null || path.UpgradeModules == null)
                continue;

            foreach (UpgradeModule module in path.UpgradeModules)
            {
                if (module == null)
                    continue;

                if (module.Equals(upgradeModule))
                {
                    if (module.IsActive)
                        return;

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
        if (upgrade == null || availableUpgrades.Contains(upgrade))
            return;

        availableUpgrades.Add(upgrade);
    }

    public void ActivateUpgrade(IUpgrade upgrade)
    {
        if (!availableUpgrades.Contains(upgrade) || activeUpgrades.Contains(upgrade))
            return;

        upgrade.ApplyUpgrade(statsManager);
        activeUpgrades.Add(upgrade);
    }

    public bool IsActive(IUpgrade upgrade)
    {
        return upgrade != null && activeUpgrades.Contains(upgrade);
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

            if (upgradeModules == null)
                return i;

            foreach (UpgradeModule module in upgradeModules)
            {
                if (module != null && module.IsActive)
                    i++;
            }
            return i;
        } 
    }

    public bool IsBlocked = false;

    public UpgradeModule[] UpgradeModules => upgradeModules;
    [SerializeField] private UpgradeModule[] upgradeModules;
}
