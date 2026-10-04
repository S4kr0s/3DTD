using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hitscan, map-wide. Without AMMO it fires continuously; the Magazines upgrade adds AMMO and a reload.
public class SniperTowerActionStrategy : ActionStrategy
{
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private bool secondShotStrongTargetting;
    [SerializeField] private bool thirdShotLastTargetting;

    private FireCycle fireCycle;
    private Tower tower;
    private Enemy target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(tower.StatsManager.GetStatValue(Stat.StatType.AMMO));
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (target != null)
            tower.RotationPoint.transform.LookAt(target.transform.position, Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley();
    }

    private void FireVolley()
    {
        float damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (!shootingPoint.IsReferenceEnabled)
                continue;

            // The previous shot of this frame may have killed the target
            if (target == null || !target.IsAlive)
                target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

            if (target != null)
            {
                target.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
                if (muzzleFlash != null)
                    muzzleFlash.Play();
            }

            if (secondShotStrongTargetting)
            {
                Enemy strongestEnemy = tower.Targetter.GetStrongestEnemyInRadius();
                if (strongestEnemy != null)
                    strongestEnemy.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
            }

            if (thirdShotLastTargetting)
            {
                Enemy lastEnemy = tower.Targetter.GetLastEnemyInRadius();
                if (lastEnemy != null)
                    lastEnemy.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
            }
        }
    }

    public override bool CanShoot(GameObject enemy)
    {
        tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (shootingPoint.IsReferenceEnabled)
            {
                if (Physics.Linecast(shootingPoint.transform.position, enemy.transform.position, out RaycastHit hit))
                {
                    if (hit.collider.gameObject == enemy)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
