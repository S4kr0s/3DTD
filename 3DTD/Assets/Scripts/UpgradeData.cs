using UnityEngine;


[CreateAssetMenu(fileName = "BaseUpgradeData", menuName = "TowerDefense/UpgradeData", order = 0)]
public class UpgradeData : ScriptableObject
{
    public bool IsEnabled = false;

    public float BaseCost { get { return BaseCost; } }
    [SerializeField] private float baseCost;

    public float DamageModifier { get { return damageModifier; } }
    [SerializeField] private float damageModifier;
    public float FireRateModifier { get { return fireRateModifier; } }
    [SerializeField] private float fireRateModifier;
    public int PenetrationModifier { get { return penetrationModifier; } }
    [SerializeField] private int penetrationModifier;
}
