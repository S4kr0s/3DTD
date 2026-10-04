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
            tower.RotationPoint.transform.LookAt(AimPoint(tower.RotationPoint.transform.position), Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley(fireCycle.VolleyAge(i));
    }

    private Vector3 AimPoint(Vector3 from)
    {
        if (!aimWithLead)
            return target.transform.position;

        return AimUtility.PredictIntercept(from, target.transform.position, target.Velocity, tower.StatsManager.GetStatValue(Stat.StatType.SPEED));
    }

    private void FireVolley(float age)
    {
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
                Vector3 aim = AimPoint(barrel.position) - barrel.position;
                shot.Rotation = aim.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(aim) : barrel.rotation;
                shot.Target = null;
            }
            else
            {
                shot.Rotation = barrel.rotation;
                shot.Target = target;
            }
            ProjectileSystem.Fire(projectile, shot);
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
