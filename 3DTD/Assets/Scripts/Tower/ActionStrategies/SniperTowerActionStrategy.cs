using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SniperTowerActionStrategy : ActionStrategy
{
    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private bool secondShotStrongTargetting;
    [SerializeField] private bool thirdShotLastTargetting;

    private float internalMagazine;
    private float internalReloadSpeed = 0f;
    private float internalFireRate;
    private Tower tower;
    private GameObject target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);
    }

    public override void ExecuteAction()
    {
        Enemy enemy = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (enemy != null)
        {
            target = enemy.gameObject;

            tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);
        }
        else
            target = null;

        ShootAtTarget();
    }

    private void ShootAtTarget()
    {
        if (tower.StatsManager.GetStatValue(Stat.StatType.AMMO) != 0 && internalMagazine <= 0)
        {
            internalReloadSpeed += Time.deltaTime;
            if (internalReloadSpeed > tower.StatsManager.GetStatValue(Stat.StatType.RELOAD_SPEED))
            {
                internalMagazine = tower.StatsManager.GetStatValue(Stat.StatType.AMMO);
                internalReloadSpeed = 0;
            }
        }
        else
        {
            internalFireRate -= Time.deltaTime;
            if (internalFireRate <= 0 && target != null)
            {
                internalMagazine--;
                foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
                {
                    if (!shootingPoint.IsReferenceEnabled)
                        continue;

                    internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);

                    if (target.TryGetComponent<Enemy>(out Enemy enemy))
                    {
                        enemy.TakeDamage(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), DamageType.PROJECTILE, this.tower);
                        muzzleFlash.Play();
                    }

                    if (secondShotStrongTargetting)
                    {
                        Enemy strongestEnemy = tower.Targetter.GetStrongestEnemyInRadius();
                        if (strongestEnemy != null)
                            strongestEnemy.TakeDamage(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), DamageType.PROJECTILE, this.tower);
                    }

                    if (thirdShotLastTargetting)
                    {
                        Enemy lastEnemy = tower.Targetter.GetLastEnemyInRadius();
                        if (lastEnemy != null)
                            lastEnemy.TakeDamage(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), DamageType.PROJECTILE, this.tower);
                    }
                }
            }
        }
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.GetComponent<Projectile>().OnProjectileDeath -= ReturnToPool;
        projectilePoolManager.ReturnToPool(obj);
    }

    public override bool CanShoot(GameObject enemy)
    {
        tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (shootingPoint.IsReferenceEnabled)
            {
                Vector3 raycastDirection = enemy.transform.position - shootingPoint.transform.position;
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
