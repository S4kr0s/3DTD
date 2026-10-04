using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletDispenserTowerActionStrategy : ActionStrategy
{
    // Hit particles per aura tick; a flamethrower in a dense wave would otherwise spawn hundreds per second
    private const int MaxAuraHitParticlesPerTick = 6;

    private ProjectilePoolManager projectilePoolManager;

    [SerializeField] private GameObject projectile;
    [SerializeField] public bool AuraMode = false;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] public bool PulseMode = false;
    [SerializeField] private GameObject pulseFirePoint;

    private FireCycle fireCycle;
    private Tower tower;
    private GameObject target;
    private readonly List<Enemy> auraTargets = new List<Enemy>();

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(0f);

        if (AuraMode)
        {
            // The flamethrower doesn't shoot; drop the bullet pool of the previous strategy
            foreach (ProjectilePoolManager pool in tower.GetComponents<ProjectilePoolManager>())
                Destroy(pool);
            return;
        }

        int points = PulseMode ? 1 : tower.ShootingPoints.Length;
        int inFlight = Mathf.CeilToInt((tower.StatsManager.GetStatValue(Stat.StatType.LIFETIME) + 0.6f) / tower.StatsManager.GetFireInterval());
        projectilePoolManager = ProjectilePoolManager.GetOrCreate(tower.gameObject, projectile, inFlight * Mathf.Max(1, points) + 2);
    }

    public override void ExecuteAction()
    {
        Enemy enemy = tower.Targetter.GetEnemy(tower.TargetBehaviour);
        target = enemy != null ? enemy.gameObject : null;

        int volleys = fireCycle.Tick(Time.deltaTime, target != null, tower.StatsManager.GetFireInterval(), 0f, 0f);
        for (int i = 0; i < volleys; i++)
            FireVolley();
    }

    private void FireVolley()
    {
        if (AuraMode)
            FireAura();
        else if (PulseMode)
            FirePulse();
        else
            FireBullets();
    }

    private void FireAura()
    {
        float damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);

        // Copy: kills remove enemies from the Targetter while we iterate
        auraTargets.Clear();
        auraTargets.AddRange(tower.Targetter.GetAllEnemiesInRadius());

        int particles = 0;
        foreach (Enemy enemy in auraTargets)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            if (hitParticle != null)
                PerfCounters.EffectsRequested++;
            if (hitParticle != null && particles < MaxAuraHitParticlesPerTick && Time.timeScale <= 2f)
            {
                Instantiate(hitParticle, enemy.transform.position, enemy.transform.rotation, null);
                PerfCounters.EffectsPlayed++;
                particles++;
            }

            enemy.TakeDamage(damage, DamageType.MAGIC, this.tower);
        }
    }

    private void FirePulse()
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

        ConfigureProjectile(_projectile.GetComponent<Projectile>());
        _projectile.SetActive(true);

        _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
    }

    private void FireBullets()
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

            ConfigureProjectile(_projectile.GetComponent<Projectile>());
            _projectile.SetActive(true);

            _projectile.GetComponent<PolygonProjectileScript>().VisualsStart();
        }
    }

    private void ConfigureProjectile(Projectile projectileComponent)
    {
        StatsManager stats = tower.StatsManager;
        projectileComponent.Target = target;
        projectileComponent.lifetime = stats.GetStatValue(Stat.StatType.LIFETIME);
        projectileComponent.damage = stats.GetStatValue(Stat.StatType.DAMAGE);
        projectileComponent.penetration = ((int)stats.GetStatValue(Stat.StatType.PIERCING));
        projectileComponent.maxSpeed = stats.GetStatValue(Stat.StatType.SPEED);
        projectileComponent.accuracy = stats.GetStatValue(Stat.StatType.ACCURACY);
        projectileComponent.tower = tower;
        if (projectileComponent.Collider != null)
            projectileComponent.Collider.enabled = true;
        projectileComponent.OnProjectileDeath += ReturnToPool;
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
