using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserTowerActionStrategy : ActionStrategy
{
    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [Tooltip("Aim where the enemy will be when the shot arrives instead of where it is now")]
    [SerializeField] private bool aimWithLead = true;

    private FireCycle fireCycle;
    private Tower tower;
    private Enemy target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        StatsManager stats = tower.StatsManager;
        fireCycle = new FireCycle(stats.GetStatValue(Stat.StatType.AMMO));

        projectilePoolManager = ProjectilePoolManager.GetOrCreate(tower.gameObject, projectile, EstimatePoolSize());
    }

    // Projectiles in flight at once: lifetime + the pool's return delay, divided by the fire interval
    private int EstimatePoolSize()
    {
        int points = 0;
        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (shootingPoint.IsReferenceEnabled)
                points++;
        }

        float inFlight = (tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME) + 0.6f) / tower.StatsManager.GetFireInterval();
        return Mathf.CeilToInt(inFlight) * Mathf.Max(1, points) + 2;
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (target != null)
            tower.RotationPoint.transform.LookAt(AimPoint(tower.RotationPoint.transform.position), Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley();
    }

    private Vector3 AimPoint(Vector3 from)
    {
        if (!aimWithLead)
            return target.transform.position;

        return AimUtility.PredictIntercept(from, target.transform.position, target.Velocity, tower.StatsManager.GetStatValue(Stat.StatType.SPEED));
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
            _projectile.transform.localScale = Vector3.one * stats.GetStatValue(Stat.StatType.SIZE);

            Projectile projectileComponent = _projectile.GetComponent<Projectile>();
            if (aimWithLead)
            {
                // The projectile flies straight along its rotation when it has no target
                Vector3 aim = AimPoint(shootingPoint.transform.position) - shootingPoint.transform.position;
                _projectile.transform.rotation = aim.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aim) : shootingPoint.transform.rotation;
                projectileComponent.Target = null;
            }
            else
            {
                _projectile.transform.rotation = shootingPoint.transform.rotation;
                projectileComponent.Target = target.gameObject;
            }

            projectileComponent.lifetime = stats.GetStatValue(Stat.StatType.LIFETIME);
            projectileComponent.damage = stats.GetStatValue(Stat.StatType.DAMAGE);
            projectileComponent.penetration = ((int)stats.GetStatValue(Stat.StatType.PIERCING));
            projectileComponent.maxSpeed = stats.GetStatValue(Stat.StatType.SPEED);
            projectileComponent.accuracy = stats.GetStatValue(Stat.StatType.ACCURACY);
            projectileComponent.tower = tower;
            if (projectileComponent.Collider != null)
                projectileComponent.Collider.enabled = true;
            projectileComponent.OnProjectileDeath += ReturnToPool;
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
