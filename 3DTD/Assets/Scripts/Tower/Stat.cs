using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Stat;

[System.Serializable]
public class Stat
{
    public float baseValue;
    public List<float> modifiers = new List<float>();
    public List<float> bonuses = new List<float>();

    public Stat(float baseValue)
    {
        this.baseValue = baseValue;
    }

    // Cached because strategies read stats several times per shot; Add/Remove invalidate it
    [System.NonSerialized] private bool isDirty = true;
    [System.NonSerialized] private float cachedValue;

    public float GetValue()
    {
        if (!isDirty)
            return cachedValue;

        float finalValue = baseValue;
        for (int i = 0; i < bonuses.Count; i++)
            finalValue += bonuses[i];
        // Percentage modifiers compound: -50% and -50% give 25% of the value
        for (int i = 0; i < modifiers.Count; i++)
            finalValue += finalValue * (modifiers[i] / 100f);

        cachedValue = finalValue;
        isDirty = false;
        return finalValue;
    }

    public void AddModifier(float modifier)
    {
        modifiers.Add(modifier);
        isDirty = true;
    }

    public void RemoveModifier(float modifier)
    {
        modifiers.Remove(modifier);
        isDirty = true;
    }

    public void AddBonus(float modifier)
    {
        bonuses.Add(modifier);
        isDirty = true;
    }

    public void RemoveBonus(float modifier)
    {
        bonuses.Remove(modifier);
        isDirty = true;
    }

    public enum StatType
    {
        #region General
        WORTH, // percentage of how much it is worth to sell
        #endregion

        #region Health & Armor
        MAXIMUM_HEALTH,
        HEALTH_REGEN,
        MAXIMUM_ARMOR,
        ARMOR_REGEN,
        #endregion

        #region Combat-Types
        DAMAGE, // percent per projectile
        AMOUNT, // of projectiles
        AMMO,
        FIRERATE, // seconds between shots. "+X% fire rate" is a modifier of -X/(100+X)*100, e.g. +25% -> -20, +100% -> -50
        RELOAD_SPEED,
        RANGE, // of tower (activation, targetting.. etc)
        RADIUS, // of projectile (bombs for example)
        ACCURACY, // of tower 
        PIERCING, // how many times a projectile can apply damage
        LIFETIME, // lifetime of projectiles spawned
        SPEED, // speed of projectile
        SIZE, // size of projectile
        #endregion
    }
}