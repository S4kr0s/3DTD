using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletDispenserTowerActionStrategy : ActionStrategy
{
    // Hit sparks of the flamethrower play as long as the prefab's own particles (it destroyed itself before)
    private const float AuraHitEffectLifetime = 5f;
    private const float PulseMuzzleLifetime = 1.5f;

    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [SerializeField] public bool AuraMode = false;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] public bool PulseMode = false;
    [SerializeField] private GameObject pulseFirePoint;

    private FireCycle fireCycle;
    private Tower tower;
    private Enemy target;
    private readonly List<Enemy> auraTargets = new List<Enemy>();

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(0f);

        if (AuraMode || !PulseMode)
        {
            // The flamethrower doesn't shoot and bullets are simulated by ProjectileSystem: drop the pool of the
            // previous strategy
            foreach (ProjectilePoolManager pool in tower.GetComponents<ProjectilePoolManager>())
                Destroy(pool);
            return;
        }

        int inFlight = Mathf.CeilToInt((tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME) + 0.6f) / tower.StatsManager.GetFireInterval());
        projectilePoolManager = ProjectilePoolManager.GetOrCreate(tower.gameObject, projectile, inFlight + 2);
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        int volleys = fireCycle.Tick(Time.deltaTime, target != null, tower.StatsManager.GetFireInterval(), 0f, 0f);
        for (int i = 0; i < volleys; i++)
            FireVolley(fireCycle.VolleyAge(i));
    }

    private void FireVolley(float age)
    {
        if (AuraMode)
            FireAura();
        else if (PulseMode)
            FirePulse();
        else
            FireBullets(age);
    }

    private void FireAura()
    {
        float damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);

        // Copy: kills remove enemies from the Targetter while we iterate
        auraTargets.Clear();
        auraTargets.AddRange(tower.Targetter.GetAllEnemiesInRadius());

        foreach (Enemy enemy in auraTargets)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            // Every enemy gets its sparks, at any game speed; their sound is limited by EffectAudio
            Transform enemyTransform = enemy.transform;
            EffectPlayer.Play(hitParticle, enemyTransform.position, enemyTransform.rotation, 1f, AuraHitEffectLifetime);
            enemy.TakeDamage(damage, DamageType.MAGIC, this.tower);
        }
    }

    private void FirePulse()
    {
        if (pulseFirePoint == null || projectilePoolManager == null)
            return;

        GameObject _projectile = projectilePoolManager.GetPooledProjectile();

        if (_projectile == null)
            return;

        Transform firePoint = pulseFirePoint.transform;
        _projectile.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
        _projectile.transform.localScale = Vector3.one;

        Projectile projectileComponent = _projectile.GetComponent<Projectile>();
        StatsManager stats = tower.StatsManager;
        projectileComponent.Target = target != null ? target.gameObject : null;
        projectileComponent.lifetime = stats.GetStatValue(Stat.StatType.LIFETIME);
        projectileComponent.damage = stats.GetStatValue(Stat.StatType.DAMAGE);
        projectileComponent.penetration = (int)stats.GetStatValue(Stat.StatType.PIERCING);
        projectileComponent.maxSpeed = stats.GetStatValue(Stat.StatType.SPEED);
        projectileComponent.accuracy = stats.GetStatValue(Stat.StatType.ACCURACY);
        projectileComponent.tower = tower;
        if (projectileComponent.Collider != null)
            projectileComponent.Collider.enabled = true;
        _projectile.SetActive(true);

        if (_projectile.TryGetComponent(out PolygonProjectileScript visuals))
            EffectPlayer.Play(visuals.muzzleParticle, firePoint.position, firePoint.rotation, 1f, PulseMuzzleLifetime);
    }

    private void FireBullets(float age)
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
            Target = target,
            Tower = tower,
            Age = age,
        };

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (!shootingPoint.IsReferenceEnabled)
                continue;

            Transform barrel = shootingPoint.transform;
            shot.Position = barrel.position;
            shot.Rotation = barrel.rotation;
            ProjectileSystem.Fire(projectile, shot);
        }
    }

    public override bool CanShoot(GameObject enemy)
    {
        return true;
    }
}
