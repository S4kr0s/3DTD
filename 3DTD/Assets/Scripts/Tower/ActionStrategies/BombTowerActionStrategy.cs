using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Rocket System. Cadence comes from the magazine: AMMO rockets FIRERATE apart, then RELOAD_SPEED.
// With the base AMMO of 1 the rate is set almost entirely by RELOAD_SPEED.
public class BombTowerActionStrategy : ActionStrategy
{
    [SerializeField] private GameObject projectile;
    [SerializeField] public bool aimAtTarget = false;
    [SerializeField] public bool doClustering = false;

    private FireCycle fireCycle;
    public override FireCycle Cycle => fireCycle;
    private Tower tower;
    private Enemy target;
    private VisualRef projectileVisual;
    private VisualRef muzzleVisual;
    private VisualRef flightVisual;
    private VisualRef impactVisual;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        StatsManager stats = tower.StatsManager;
        fireCycle = new FireCycle(stats.GetStatValue(Stat.StatType.AMMO));

        // Rockets are simulated by ProjectileSystem; drop the pool of an earlier strategy
        foreach (ProjectilePoolManager pool in tower.GetComponents<ProjectilePoolManager>())
            Destroy(pool);
    }

    public override void ExecuteAction()
    {
        Enemy enemy = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        target = enemy;
        if (enemy != null)
            tower.RotationPoint.transform.LookAt(enemy.transform.position, Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley(fireCycle.VolleyAge(i));
    }

    private void FireVolley(float age)
    {
        // Cluster rockets have their own slot: a plain rocket prefab would drop the bomblets
        GameObject prefab = projectileVisual.Get(tower, doClustering ? VisualSlot.ClusterProjectile : VisualSlot.Projectile, projectile);
        StatsManager stats = tower.StatsManager;
        ProjectileSystem.Shot shot = new ProjectileSystem.Shot
        {
            Scale = stats.GetStatValue(Stat.StatType.SIZE),
            Damage = stats.GetStatValue(Stat.StatType.DAMAGE),
            Speed = stats.GetStatValue(Stat.StatType.SPEED),
            Lifetime = stats.GetStatValue(Stat.StatType.LIFETIME),
            Accuracy = stats.GetStatValue(Stat.StatType.ACCURACY),
            Pierce = (int)stats.GetStatValue(Stat.StatType.PIERCING),
            BlastRadius = stats.GetStatValue(Stat.StatType.RADIUS),
            Target = target,
            Homing = aimAtTarget,
            Cluster = doClustering,
            Tower = tower,
            Age = age,
            MuzzleEffect = muzzleVisual.Get(tower, VisualSlot.Muzzle, null),
            FlightEffect = flightVisual.Get(tower, VisualSlot.Flight, null),
            ImpactEffect = impactVisual.Get(tower, VisualSlot.Impact, null),
        };

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (!shootingPoint.IsReferenceEnabled)
                continue;

            shot.Position = shootingPoint.transform.position;
            shot.Rotation = shootingPoint.transform.rotation;
            ProjectileSystem.Fire(prefab, shot);
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
