using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(StatsManager))]
public class UpgradeManager : MonoBehaviour
{
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
    }

    public void AddAndActivateUpgrade(IUpgrade upgrade)
    {
        AddUpgrade(upgrade);
        ActivateUpgrade(upgrade);
    }

    public void DeactivateAndRemoveUpgrade(IUpgrade upgrade)
    {
        DeactivateUpgrade(upgrade);
        RemoveUpgrade(upgrade);
    }

    public void AddUpgrade(IUpgrade upgrade)
    {
        availableUpgrades.Add(upgrade);
    }

    public void RemoveUpgrade(IUpgrade upgrade)
    {
        availableUpgrades.Remove(upgrade);
    }

    public void ActivateUpgrade(IUpgrade upgrade)
    {
        if (!availableUpgrades.Contains(upgrade)) return;

        upgrade.ApplyUpgrade(statsManager);
        activeUpgrades.Add(upgrade);
    }

    public void DeactivateUpgrade(IUpgrade upgrade)
    {
        if (!availableUpgrades.Contains(upgrade)) return;

        upgrade.RemoveUpgrade(statsManager);
        activeUpgrades.Remove(upgrade);
    }

    public bool IsActive(IUpgrade upgrade)
    {
        return activeUpgrades.Contains(upgrade);
    }
}
