using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BulletDispenserTowerActionStrategy : ActionStrategy
{
    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [SerializeField] public bool AuraMode = false;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] public bool PulseMode = false;
    [SerializeField] private GameObject pulseFirePoint;

    private float internalFireRate;
    private Tower tower;
    private GameObject target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);

        // Object Pool
        projectilePoolManager = tower.gameObject.AddComponent<ProjectilePoolManager>();
        projectilePoolManager.Setup(projectile, Mathf.RoundToInt(10 / internalFireRate * tower.ShootingPoints.Length));
    }

    public override void ExecuteAction()
    {
        Enemy enemy = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (enemy != null)
        {
            target = enemy.gameObject;

            //tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);
        }
        else
            target = null;

        ShootAtTarget();
    }

    private void ShootAtTarget()
    {
        if (internalFireRate <= 0 && target != null)
        {
            internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);

            if (AuraMode)
            {
                List<Enemy> enemies = tower.Targetter.GetAllEnemiesInRadius().ToList();
                foreach (Enemy enemy in enemies)
                {
                    if (enemy != null)
                    {
                        enemy.TakeDamage(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), DamageType.MAGIC, this.tower);
                        Instantiate(hitParticle, enemy.transform.position, enemy.transform.rotation, null);
                    }
                }
            }
            else if (PulseMode)
            {
                if (pulseFirePoint == null)
                    return;

                GameObject _projectile = projectilePoolManager.GetPooledProjectile();

                if (_projectile == null)
                    return;

                _projectile.SetActive(false);
                _projectile.transform.position = pulseFirePoint.transform.position;
                _projectile.transform.rotation = pulseFirePoint.transform.rotation;
                _projectile.transform.localScale = Vector3.one;

                Projectile projectileComponent = _projectile.GetComponent<Projectile>();
                projectileComponent.Target = target;
                projectileComponent.lifetime = tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME);
                projectileComponent.damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);
                projectileComponent.penetration = ((int)tower.StatsManager.GetStatValue(Stat.StatType.PIERCING));
                projectileComponent.maxSpeed = tower.StatsManager.GetStatValue(Stat.StatType.SPEED);
                projectileComponent.accuracy = tower.StatsManager.GetStatValue(Stat.StatType.ACCURACY);
                projectileComponent.tower = tower;
                if (projectileComponent.Collider != null)
                    projectileComponent.Collider.enabled = true;
                projectileComponent.OnProjectileDeath += ReturnToPool;
                _projectile.SetActive(true);

                _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
            }
            else
            {
                foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
                {
                    if (!shootingPoint.IsReferenceEnabled)
                        continue; 

                    GameObject _projectile = projectilePoolManager.GetPooledProjectile();

                    if (_projectile == null)
                    {
                        continue;
                    }

                    _projectile.SetActive(false);
                    _projectile.transform.position = shootingPoint.transform.position;
                    _projectile.transform.rotation = shootingPoint.transform.rotation;
                    _projectile.transform.localScale = Vector3.one * tower.StatsManager.GetStatValue(Stat.StatType.SIZE);

                    Projectile projectileComponent = _projectile.GetComponent<Projectile>();
                    projectileComponent.Target = target;
                    projectileComponent.lifetime = tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME);
                    projectileComponent.damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);
                    projectileComponent.penetration = ((int)tower.StatsManager.GetStatValue(Stat.StatType.PIERCING));
                    projectileComponent.maxSpeed = tower.StatsManager.GetStatValue(Stat.StatType.SPEED);
                    projectileComponent.accuracy = tower.StatsManager.GetStatValue(Stat.StatType.ACCURACY);
                    projectileComponent.tower = tower;
                    if (projectileComponent.Collider != null)
                        projectileComponent.Collider.enabled = true;
                    projectileComponent.OnProjectileDeath += ReturnToPool;
                    _projectile.SetActive(true);

                    _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
                }
            }
        }
        else
        {
            internalFireRate -= Time.deltaTime;
        }
    }

    public void ReturnToPool(GameObject obj)
    {
        obj.GetComponent<Projectile>().OnProjectileDeath -= ReturnToPool;
        projectilePoolManager.ReturnToPool(obj);
    }

    public override bool CanShoot(GameObject enemy)
    {
        return true;
    }
}
