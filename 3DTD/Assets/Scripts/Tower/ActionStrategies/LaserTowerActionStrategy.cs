using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserTowerActionStrategy : ActionStrategy
{
    [SerializeField] private GameObject projectile;
    [Tooltip("Aim where the enemy will be when the shot arrives instead of where it is now")]
    [SerializeField] private bool aimWithLead = true;

    private FireCycle fireCycle;
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

        // Bolts are simulated by ProjectileSystem; drop the pool of an earlier strategy
        foreach (ProjectilePoolManager pool in tower.GetComponents<ProjectilePoolManager>())
            Destroy(pool);
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (target != null)
            tower.RotationPoint.transform.LookAt(AimPoint(tower.RotationPoint.transform.position, 0f), Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley(fireCycle.VolleyAge(i));
    }

    // Where to aim a bolt fired age seconds ago (its volley was due earlier in the frame). The bolt starts that far
    // along its flight, so it has to lead from where the enemy was back then, not from where it is now.
    private Vector3 AimPoint(Vector3 from, float age)
    {
        if (!aimWithLead)
            return target.transform.position;

        Vector3 velocity = target.Velocity;
        return AimUtility.PredictIntercept(from, target.transform.position - velocity * age, velocity, tower.StatsManager.GetStatValue(Stat.StatType.SPEED));
    }

    private void FireVolley(float age)
    {
        GameObject prefab = projectileVisual.Get(tower, VisualSlot.Projectile, projectile);
        StatsManager stats = tower.StatsManager;
        ProjectileSystem.Shot shot = new ProjectileSystem.Shot
        {
            Scale = stats.GetStatValue(Stat.StatType.SIZE),
            Damage = stats.GetStatValue(Stat.StatType.DAMAGE),
            Speed = stats.GetStatValue(Stat.StatType.SPEED),
            Lifetime = stats.GetStatValue(Stat.StatType.LIFETIME),
            Accuracy = stats.GetStatValue(Stat.StatType.ACCURACY),
            Pierce = (int)stats.GetStatValue(Stat.StatType.PIERCING),
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

            Transform barrel = shootingPoint.transform;
            shot.Position = barrel.position;
            if (aimWithLead)
            {
                // The bolt flies straight along its rotation when it has no target
                Vector3 aim = AimPoint(barrel.position, age) - barrel.position;
                shot.Rotation = aim.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aim) : barrel.rotation;
                shot.Target = null;
            }
            else
            {
                shot.Rotation = barrel.rotation;
                shot.Target = target;
            }
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
