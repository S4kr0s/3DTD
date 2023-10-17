using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StatsConfig", menuName = "Config/StatsConfig")]

public class StatsScriptableObject : ScriptableObject
{
    [Header("General")]
    [Tooltip("Money Cost for this Entity")]
    [SerializeField] public float Cost;
    [Tooltip("Money Worth for this Entity")]
    [Range(0f, 1f)]
    [SerializeField] public float Worth;

    [Header("Health & Armor")]
    [Tooltip("Maximum Health")]
    [SerializeField] public float MaximumHealth;
    [Tooltip("Health Regen")]
    [SerializeField] public float HealthRegen;
    [Tooltip("Maximum Armor")]
    [SerializeField] public float MaximumArmor;
    [Tooltip("Armor Regen")]
    [SerializeField] public float ArmorRegen;

    [Header("Combat-Types")]
    [Tooltip("[0.00 - 1.00] Percentage of damage dealt per projectile")]
    [SerializeField] public float Damage;
    [Tooltip("Amount of projectiles per shot")]
    [SerializeField] public float Amount;
    [Tooltip("Time (seconds) between shots")]
    [SerializeField] public float FireRate;
    [Tooltip("Activation / Attacking Range of Entity")]
    [SerializeField] public float Range;
    [Tooltip("Radius of a single projectile (i.e. for bombs)")]
    [SerializeField] public float Radius;
    [Tooltip("Accuracy of Entity")]
    [Range(0f, 1f)]
    [SerializeField] public float Accuracy;
    [Tooltip("How many targets a projectile can hit")]
    [SerializeField] public int Piercing;
    [Tooltip("How long a projectile lives")]
    [SerializeField] public float Lifetime;
}
