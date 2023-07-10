using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseTowerData", menuName = "TowerDefense/TowerData", order = 0)]
public class TowerData : ScriptableObject
{
    [SerializeField] private int _id;
    [SerializeField] private string _name;
    [SerializeField] [TextArea] private string _description;
    [SerializeField] private DamageType _damageType;
    [SerializeField] private float _baseDamage;
    [SerializeField] private float _baseFireRate;
    [SerializeField] private int _basePenetration;
    [SerializeField] private int _baseCost;
    [SerializeField] private float _lifetime;

    public int Id => _id;
    public string Name => _name;
    public string Description => _description;
    public DamageType DamageType => _damageType;
    public float BaseDamage => _baseDamage;
    public float BaseFireRate => _baseFireRate;
    public int BasePenetration => _basePenetration;
    public int BaseCost => _baseCost;
    public float Lifetime => _lifetime;
}
