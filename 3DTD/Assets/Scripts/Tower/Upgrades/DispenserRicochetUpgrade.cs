using UnityEngine;

// Bullet Dispenser needles rebound off the inside of the range dome (see BulletDispenserTowerActionStrategy)
public class DispenserRicochetUpgrade : Upgrade
{
    [Tooltip("Rebounds per needle (the highest of the bought modules counts)")]
    [SerializeField] private int bounces = 1;
    [Tooltip("Damage a needle gains with every rebound")]
    [SerializeField] private float damagePerBounce = 0f;
    [Tooltip("Rebounding needles turn towards the tower's target (or the nearest enemy in range)")]
    [SerializeField] private bool seek = false;

    public int Bounces => bounces;
    public float DamagePerBounce => damagePerBounce;
    public bool Seek => seek;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (BulletDispenserTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BulletDispenserTowerActionStrategy>())
        {
            strategy.ricochetBounces = Mathf.Max(strategy.ricochetBounces, bounces);
            strategy.ricochetDamage = Mathf.Max(strategy.ricochetDamage, damagePerBounce);
            strategy.ricochetSeek |= seek;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (BulletDispenserTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BulletDispenserTowerActionStrategy>())
        {
            strategy.ricochetBounces = 0;
            strategy.ricochetDamage = 0f;
            strategy.ricochetSeek = false;
        }
    }
}
