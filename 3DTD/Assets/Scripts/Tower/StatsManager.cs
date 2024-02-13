using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatsManager : MonoBehaviour
{
    [SerializeField] private StatsScriptableObject statsScriptableObject;
    public Dictionary<Stat.StatType, Stat> Stats { get {  return stats; } }
    private Dictionary<Stat.StatType, Stat> stats = new Dictionary<Stat.StatType, Stat>();
    private Dictionary<Stat.StatType, Func<float>> statValueMapping;

    private void Awake()
    {
        InitializeStatMapping();
        InitializeStats();
    }

    private void InitializeStatMapping()
    {
        statValueMapping = new Dictionary<Stat.StatType, Func<float>>
        {
            { Stat.StatType.WORTH, () => statsScriptableObject.Worth },
            { Stat.StatType.MAXIMUM_HEALTH, () => statsScriptableObject.MaximumHealth },
            { Stat.StatType.HEALTH_REGEN, () => statsScriptableObject.HealthRegen },
            { Stat.StatType.MAXIMUM_ARMOR, () => statsScriptableObject.MaximumArmor },
            { Stat.StatType.ARMOR_REGEN, () => statsScriptableObject.ArmorRegen },
            { Stat.StatType.DAMAGE, () => statsScriptableObject.Damage },
            { Stat.StatType.AMOUNT, () => statsScriptableObject.Amount },
            { Stat.StatType.FIRERATE, () => statsScriptableObject.FireRate },
            { Stat.StatType.RANGE, () => statsScriptableObject.Range },
            { Stat.StatType.RADIUS, () => statsScriptableObject.Radius },
            { Stat.StatType.ACCURACY, () => statsScriptableObject.Accuracy },
            { Stat.StatType.PIERCING, () => statsScriptableObject.Piercing },
            { Stat.StatType.LIFETIME, () => statsScriptableObject.Lifetime },
            { Stat.StatType.SPEED, () => statsScriptableObject.Speed },
            { Stat.StatType.SIZE, () => statsScriptableObject.Size },
        };
    }

    private void InitializeStats()
    {
        foreach (var entry in statValueMapping)
        {
            stats[entry.Key] = new Stat(entry.Value());
        }
    }

    public float GetStatValue(Stat.StatType type)
    {
        if (stats.TryGetValue(type, out Stat stat))
        {
            return stat.GetValue();
        }
        return -1;
    }

    public Stat GetStat(Stat.StatType type)
    {
        if (stats.TryGetValue(type, out Stat stat))
        {
            return stat;
        }
        return null;
    }

    public void GetStat(Stat.StatType type, out Stat stat)
    {
        if (stats.TryGetValue(type, out Stat _stat))
        {
            stat = _stat;
        }
        stat = null;
    }

    public void SetStat(Stat.StatType type, Stat stat)
    {
        if (stats.TryGetValue(type, out Stat _stat))
        {
            _stat = stat;
        }
        return;
    }
}
