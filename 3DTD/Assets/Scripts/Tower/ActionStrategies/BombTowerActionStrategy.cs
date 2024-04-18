using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombTowerActionStrategy : ActionStrategy
{
    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [SerializeField] public bool aimAtTarget = false;
    [SerializeField] public bool doClustering = false;

    private float internalFireRate;
    private float internalMagazine;
    private float internalReloadSpeed = 0f;
    private Tower tower;
    private GameObject target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);
        internalMagazine = tower.StatsManager.GetStatValue(Stat.StatType.AMMO);

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

            tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);
        }
        else
            target = null;

        ShootAtTarget();
    }

    private void ShootAtTarget()
    {
        if (internalMagazine <= 0)
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

                    GameObject _projectile = projectilePoolManager.GetPooledProjectile();

                    if (_projectile == null)
                    {
                        continue;
                    }

                    _projectile.SetActive(false);
                    _projectile.transform.position = shootingPoint.transform.position;
                    _projectile.transform.rotation = shootingPoint.transform.rotation;
                    _projectile.transform.localScale = Vector3.one * tower.StatsManager.GetStatValue(Stat.StatType.SIZE);

                    ProjectileBomb projectileComponent = _projectile.GetComponent<ProjectileBomb>();
                    projectileComponent.Target = target;
                    projectileComponent.lifetime = tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME);
                    projectileComponent.damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);
                    projectileComponent.penetration = ((int)tower.StatsManager.GetStatValue(Stat.StatType.PIERCING));
                    projectileComponent.maxSpeed = tower.StatsManager.GetStatValue(Stat.StatType.SPEED);
                    projectileComponent.accuracy = tower.StatsManager.GetStatValue(Stat.StatType.ACCURACY);
                    if (projectileComponent.Collider != null)
                        projectileComponent.Collider.enabled = true;
                    projectileComponent.aimAtTarget = aimAtTarget;
                    projectileComponent.doClustering = doClustering;
                    projectileComponent.OnProjectileDeath += ReturnToPool;
                    projectileComponent.radius = tower.StatsManager.GetStatValue(Stat.StatType.RADIUS);
                    _projectile.SetActive(true);

                    _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
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
