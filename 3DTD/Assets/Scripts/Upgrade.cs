using UnityEngine;

public class Upgrade : MonoBehaviour
{
    public bool IsEnabled = false;

    public float DamageModifier { get { return damageModifier; } }
    [SerializeField] private float damageModifier;
    public float FireRateModifier { get { return fireRateModifier; } }
    [SerializeField] private float fireRateModifier;
    public int PenetrationModifier { get { return penetrationModifier; } }
    [SerializeField] private int penetrationModifier;
}
