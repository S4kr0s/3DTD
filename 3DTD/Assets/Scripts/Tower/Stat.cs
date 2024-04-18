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

    public float GetValue()
    {
        float finalValue = baseValue;
        bonuses.ForEach(x => finalValue += x);  // assuming modifiers are multiplicative. If additive: finalValue += x;
        modifiers.ForEach(x => finalValue += (finalValue * (x / 100f)));  // assuming modifiers are multiplicative. If additive: finalValue += x;
        return finalValue;
    }

    public void AddModifier(float modifier)
    {
        modifiers.Add(modifier);
    }

    public void RemoveModifier(float modifier)
    {
        modifiers.Remove(modifier);
    }

    public void AddBonus(float modifier)
    {
        bonuses.Add(modifier);
    }

    public void RemoveBonus(float modifier)
    {
        bonuses.Remove(modifier);
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
        FIRERATE, // of tower
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