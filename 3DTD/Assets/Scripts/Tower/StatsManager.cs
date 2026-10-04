using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatsManager : MonoBehaviour
{
    // Lower bound for FIRERATE. Stacked percentage modifiers can push the raw value to 0 or below,
    // which used to make a tower fire every frame (damage depending on FPS).
    public const float MinFireInterval = 0.05f;

    [SerializeField] private StatsScriptableObject statsScriptableObject;
    public Dictionary<Stat.StatType, Stat> Stats { get {  return stats; } }
    // The authored base values; readable on prefabs, which never run Awake
    public StatsScriptableObject Config => statsScriptableObject;
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
            { Stat.StatType.AMMO, () => statsScriptableObject.Ammo },
            { Stat.StatType.FIRERATE, () => statsScriptableObject.FireRate },
            { Stat.StatType.RELOAD_SPEED, () => statsScriptableObject.ReloadSpeed },
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

    // Base value of a stat on a config asset, -1 when the config is missing (like GetStatValue)
    public static float GetBaseValue(StatsScriptableObject config, Stat.StatType type)
    {
        if (config == null)
            return -1f;
        switch (type)
        {
            case Stat.StatType.WORTH: return config.Worth;
            case Stat.StatType.MAXIMUM_HEALTH: return config.MaximumHealth;
            case Stat.StatType.HEALTH_REGEN: return config.HealthRegen;
            case Stat.StatType.MAXIMUM_ARMOR: return config.MaximumArmor;
            case Stat.StatType.ARMOR_REGEN: return config.ArmorRegen;
            case Stat.StatType.DAMAGE: return config.Damage;
            case Stat.StatType.AMOUNT: return config.Amount;
            case Stat.StatType.AMMO: return config.Ammo;
            case Stat.StatType.FIRERATE: return config.FireRate;
            case Stat.StatType.RELOAD_SPEED: return config.ReloadSpeed;
            case Stat.StatType.RANGE: return config.Range;
            case Stat.StatType.RADIUS: return config.Radius;
            case Stat.StatType.ACCURACY: return config.Accuracy;
            case Stat.StatType.PIERCING: return config.Piercing;
            case Stat.StatType.LIFETIME: return config.Lifetime;
            case Stat.StatType.SPEED: return config.Speed;
            case Stat.StatType.SIZE: return config.Size;
            default: return -1f;
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

    // Seconds between shots, never below MinFireInterval
    public float GetFireInterval()
    {
        return Mathf.Max(MinFireInterval, GetStatValue(Stat.StatType.FIRERATE));
    }

    public float GetReloadTime()
    {
        return Mathf.Max(0f, GetStatValue(Stat.StatType.RELOAD_SPEED));
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
        stats.TryGetValue(type, out stat);
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
