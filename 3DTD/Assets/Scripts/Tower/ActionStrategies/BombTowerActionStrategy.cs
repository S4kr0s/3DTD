using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rocket System. Cadence comes from the magazine: AMMO rockets FIRERATE apart, then RELOAD_SPEED.
// With the base AMMO of 1 the rate is set almost entirely by RELOAD_SPEED.
public class BombTowerActionStrategy : ActionStrategy
{
    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [SerializeField] public bool aimAtTarget = false;
    [SerializeField] public bool doClustering = false;

    private FireCycle fireCycle;
    private Tower tower;
    private GameObject target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        StatsManager stats = tower.StatsManager;
        fireCycle = new FireCycle(stats.GetStatValue(Stat.StatType.AMMO));

        float cycle = stats.GetFireInterval() + stats.GetReloadTime() / Mathf.Max(1f, stats.GetStatValue(Stat.StatType.AMMO));
        int inFlight = Mathf.CeilToInt((stats.GetStatValue(Stat.StatType.LIFETIME) + 0.6f) / Mathf.Max(0.05f, cycle));
        projectilePoolManager = ProjectilePoolManager.GetOrCreate(tower.gameObject, projectile, inFlight * Mathf.Max(1, tower.ShootingPoints.Length) + 2);
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

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley();
    }

    private void FireVolley()
    {
        StatsManager stats = tower.StatsManager;

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
            _projectile.transform.localScale = Vector3.one * stats.GetStatValue(Stat.StatType.SIZE);

            ProjectileBomb projectileComponent = _projectile.GetComponent<ProjectileBomb>();
            projectileComponent.Target = target;
            projectileComponent.lifetime = stats.GetStatValue(Stat.StatType.LIFETIME);
            projectileComponent.damage = stats.GetStatValue(Stat.StatType.DAMAGE);
            projectileComponent.penetration = ((int)stats.GetStatValue(Stat.StatType.PIERCING));
            projectileComponent.maxSpeed = stats.GetStatValue(Stat.StatType.SPEED);
            projectileComponent.accuracy = stats.GetStatValue(Stat.StatType.ACCURACY);
            projectileComponent.tower = tower;
            if (projectileComponent.Collider != null)
                projectileComponent.Collider.enabled = true;
            projectileComponent.aimAtTarget = aimAtTarget;
            projectileComponent.doClustering = doClustering;
            projectileComponent.OnProjectileDeath += ReturnToPool;
            projectileComponent.radius = stats.GetStatValue(Stat.StatType.RADIUS);
            _projectile.SetActive(true);

            _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
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
