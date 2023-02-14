using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseEnemyData", menuName = "TowerDefense/EnemyData", order = 0)]
public class EnemyData : ScriptableObject
{
    [SerializeField] private int _id;
    [SerializeField] private string _name;
    [SerializeField] [TextArea] private string _description;
    [SerializeField] private DamageType _damageType;
    [SerializeField] private Shape _startShape;
    [SerializeField] private EnemyColor _startColor;
    [SerializeField] private float _health;
    [SerializeField] private float _movementSpeed;

    public int Id => _id;
    public string Name => _name;
    public string Description => _description;
    public DamageType DamageType => _damageType;
    public Shape StartShape => _startShape;
    public EnemyColor StartColor => _startColor;
    public float Health => _health;
    public float MovementSpeed => _movementSpeed;
}
