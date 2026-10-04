using System.Collections.Generic;
using UnityEngine;

public enum TowerStatKind
{
    Damage,
    FireRate,
    Range,
    Accuracy,
    Speed,
    Pierce,
    Radius,
    Capacity,
}

// How tower stats are shown in the tower panel, the build tooltip and the encyclopedia: label, icon,
// icon tint and formatting. The grid order is fixed by the redesign; "Burn" from the mockups has no
// stat in the game, so its slot shows the blast radius.
public static class TowerStatInfo
{
    public static readonly TowerStatKind[] Grid =
    {
        TowerStatKind.Damage, TowerStatKind.FireRate, TowerStatKind.Range, TowerStatKind.Accuracy,
        TowerStatKind.Speed, TowerStatKind.Pierce, TowerStatKind.Radius, TowerStatKind.Capacity,
    };

    // The four key stats of the build tooltip
    public static readonly TowerStatKind[] Key = { TowerStatKind.Damage, TowerStatKind.FireRate, TowerStatKind.Range, TowerStatKind.Capacity };

    public static string Label(TowerStatKind kind)
    {
        switch (kind)
        {
            case TowerStatKind.Damage: return "Damage";
            case TowerStatKind.FireRate: return "Fire rate";
            case TowerStatKind.Range: return "Range";
            case TowerStatKind.Accuracy: return "Accuracy";
            case TowerStatKind.Speed: return "Speed";
            case TowerStatKind.Pierce: return "Pierce";
            case TowerStatKind.Radius: return "Radius";
            default: return "Capacity";
        }
    }

    // Hover text of stat cells and meters
    public static string Description(TowerStatKind kind)
    {
        switch (kind)
        {
            case TowerStatKind.Damage: return "Damage of each hit. Armored enemies shrug off part of projectile and explosive hits.";
            case TowerStatKind.FireRate: return "Shots per second.";
            case TowerStatKind.Range: return "How far the tower reaches.";
            case TowerStatKind.Accuracy: return "How tightly shots group around the aim point.";
            case TowerStatKind.Speed: return "How fast the tower's projectiles fly.";
            case TowerStatKind.Pierce: return "How many enemies one projectile can hit before it is spent.";
            case TowerStatKind.Radius: return "Blast radius of explosive hits.";
            default: return "Shots per magazine before the tower reloads. Mine Factory: mines the field can hold.";
        }
    }

    // Every stat has its own glyph (the mockups reuse the bolt for speed)
    public static string Icon(TowerStatKind kind)
    {
        switch (kind)
        {
            case TowerStatKind.Damage: return "ic_burst";
            case TowerStatKind.FireRate: return "ic_bolt";
            case TowerStatKind.Range: return "ic_target";
            case TowerStatKind.Accuracy: return "ic_crosshair";
            case TowerStatKind.Speed: return "ic_speed";
            case TowerStatKind.Pierce: return "ic_drop";
            case TowerStatKind.Radius: return "ic_radius";
            default: return "ic_capsule";
        }
    }

    // Orange for offence stats, lilac for range / handling stats, as in the mockups
    public static Color IconTint(TowerStatKind kind)
    {
        UITheme theme = UITheme.Current;
        switch (kind)
        {
            case TowerStatKind.Damage:
            case TowerStatKind.FireRate:
            case TowerStatKind.Capacity:
            case TowerStatKind.Radius:
                return theme.accentText;
            default:
                return theme.iconTint;
        }
    }

    public static Stat.StatType StatType(TowerStatKind kind)
    {
        switch (kind)
        {
            case TowerStatKind.Damage: return Stat.StatType.DAMAGE;
            case TowerStatKind.FireRate: return Stat.StatType.FIRERATE;
            case TowerStatKind.Range: return Stat.StatType.RANGE;
            case TowerStatKind.Accuracy: return Stat.StatType.ACCURACY;
            case TowerStatKind.Speed: return Stat.StatType.SPEED;
            case TowerStatKind.Pierce: return Stat.StatType.PIERCING;
            case TowerStatKind.Radius: return Stat.StatType.RADIUS;
            default: return Stat.StatType.AMMO;
        }
    }

    public static TowerStatKind? FromStatType(Stat.StatType type)
    {
        foreach (TowerStatKind kind in Grid)
        {
            if (StatType(kind) == type)
                return kind;
        }
        return null;
    }

    // Raw stat value -> displayed number. Fire rate is shown as shots per second (FIRERATE is seconds between shots).
    public static float Display(TowerStatKind kind, float raw)
    {
        if (raw < 0f)
            return -1f;
        switch (kind)
        {
            case TowerStatKind.FireRate:
                return raw > 0f ? 1f / Mathf.Max(StatsManager.MinFireInterval, raw) : -1f;
            case TowerStatKind.Accuracy:
                return raw * 100f;
            default:
                return raw;
        }
    }

    public static string Format(TowerStatKind kind, float raw)
    {
        float value = Display(kind, raw);
        if (value < 0f)
            return "—";
        switch (kind)
        {
            case TowerStatKind.FireRate:
                return UIFormat.Short(value) + "/s";
            case TowerStatKind.Accuracy:
                return Mathf.RoundToInt(value) + "%";
            case TowerStatKind.Speed:
            case TowerStatKind.Radius:
            case TowerStatKind.Capacity:
                return value <= 0f ? "—" : UIFormat.Short(value);
            default:
                return UIFormat.Short(value);
        }
    }

    public static float Live(Tower tower, TowerStatKind kind)
    {
        return tower.StatsManager.GetStatValue(StatType(kind));
    }

    public static float Base(StatsScriptableObject config, TowerStatKind kind)
    {
        return StatsManager.GetBaseValue(config, StatType(kind));
    }

    // Value of a stat after a list of stat upgrades, computed like Stat.GetValue (bonuses, then compounding modifiers)
    public static float After(StatsScriptableObject config, Stat.StatType type, IEnumerable<StatUpgrade> upgrades)
    {
        float baseValue = StatsManager.GetBaseValue(config, type);
        if (baseValue < 0f)
            return -1f;
        Stat stat = new Stat(baseValue);
        foreach (StatUpgrade upgrade in upgrades)
        {
            if (upgrade == null || upgrade.targetStat != type)
                continue;
            if (upgrade.isModifier)
                stat.AddModifier(upgrade.upgradeValue);
            else
                stat.AddBonus(upgrade.upgradeValue);
        }
        return stat.GetValue();
    }

    // Icon for an upgrade module, picked from the first stat it changes (behaviour-only modules get a chip)
    public static string ModuleIcon(UpgradeModule module)
    {
        if (module == null)
            return "ic_cube";
        foreach (StatUpgrade upgrade in module.StatUpgrades)
        {
            if (upgrade == null)
                continue;
            switch (upgrade.targetStat)
            {
                case Stat.StatType.DAMAGE: return "ic_burst";
                case Stat.StatType.FIRERATE: return "ic_bolt";
                case Stat.StatType.RANGE: return "ic_target";
                case Stat.StatType.ACCURACY: return "ic_crosshair";
                case Stat.StatType.SPEED: return "ic_speed";
                case Stat.StatType.PIERCING: return "ic_drop";
                case Stat.StatType.RADIUS: return "ic_radius";
                case Stat.StatType.AMMO: return "ic_capsule";
                case Stat.StatType.AMOUNT: return "ic_cluster";
                case Stat.StatType.RELOAD_SPEED: return "ic_rotate";
                case Stat.StatType.WORTH: return "ic_recycle";
            }
        }
        return module.HasBehaviourUpgrades ? "ic_seeker" : "ic_cube";
    }
}
