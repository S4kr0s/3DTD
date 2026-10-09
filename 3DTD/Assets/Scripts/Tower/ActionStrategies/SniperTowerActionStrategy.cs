using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hitscan, map-wide. Without AMMO it fires continuously; the Magazines upgrade adds AMMO and a reload.
// Every shot shows a muzzle flash at its barrel, a tracer to the enemy and an impact; upgrades restyle them
// (VisualSlot.Muzzle/Impact and SniperVisualUpgrade).
public class SniperTowerActionStrategy : ActionStrategy
{
    private const float EffectLifetime = 2f;

    [Tooltip("Old shared muzzle particle, only played when no muzzle effect is set")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private bool secondShotStrongTargetting;
    [SerializeField] private bool thirdShotLastTargetting;

    [Header("Shot visuals (base look; upgrades override)")]
    [SerializeField] private GameObject muzzleEffect;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private GameObject tracer;
    [SerializeField] private GameObject secondTracer;
    [SerializeField] private GameObject thirdTracer;
    [SerializeField] private float tracerWidth = 1f;
    [SerializeField] private float tracerDuration = 0.12f;
    [SerializeField] private float muzzleScale = 1f;
    [SerializeField] private float impactScale = 1f;

    private FireCycle fireCycle;
    public override FireCycle Cycle => fireCycle;
    private Tower tower;
    private Enemy target;

    // The look after the tower's visual upgrades (resolved again when they change)
    private int styleVersion = -1;
    private GameObject currentMuzzle;
    private GameObject currentImpact;
    private GameObject currentTracer;
    private GameObject currentSecondTracer;
    private GameObject currentThirdTracer;
    private float currentTracerWidth;
    private float currentTracerDuration;
    private float currentMuzzleScale;
    private float currentImpactScale;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(tower.StatsManager.GetStatValue(Stat.StatType.AMMO));
    }

    public override void ExecuteAction()
    {
        target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

        if (target != null)
            tower.RotationPoint.transform.LookAt(target.transform.position, Vector3.up);

        StatsManager stats = tower.StatsManager;
        int volleys = fireCycle.Tick(Time.deltaTime, target != null, stats.GetFireInterval(), stats.GetStatValue(Stat.StatType.AMMO), stats.GetReloadTime());

        for (int i = 0; i < volleys; i++)
            FireVolley();
    }

    private void ResolveStyle()
    {
        TowerVisuals visuals = tower.Visuals;
        if (styleVersion == visuals.Version)
            return;
        styleVersion = visuals.Version;

        currentMuzzle = visuals.Resolve(VisualSlot.Muzzle, muzzleEffect);
        currentImpact = visuals.Resolve(VisualSlot.Impact, impactEffect);
        currentTracer = tracer;
        currentSecondTracer = secondTracer;
        currentThirdTracer = thirdTracer;
        currentTracerWidth = tracerWidth;
        currentTracerDuration = tracerDuration;
        currentMuzzleScale = muzzleScale;
        currentImpactScale = impactScale;
        for (int i = 0; i < visuals.Count; i++)
        {
            if (!(visuals[i] is SniperVisualUpgrade style))
                continue;
            if (style.tracer != null)
                currentTracer = style.tracer;
            if (style.secondTracer != null)
                currentSecondTracer = style.secondTracer;
            if (style.thirdTracer != null)
                currentThirdTracer = style.thirdTracer;
            if (style.tracerWidth > 0f)
                currentTracerWidth = style.tracerWidth;
            if (style.tracerDuration > 0f)
                currentTracerDuration = style.tracerDuration;
            if (style.muzzleScale > 0f)
                currentMuzzleScale = style.muzzleScale;
            if (style.impactScale > 0f)
                currentImpactScale = style.impactScale;
        }
    }

    private void ShowShot(Transform barrel, Enemy enemy, GameObject line)
    {
        Vector3 from = barrel.position;
        Vector3 to = enemy.transform.position;
        TracerRenderer.Show(line != null ? line : currentTracer, from, to, currentTracerWidth, currentTracerDuration);
        Vector3 back = from - to;
        Quaternion facing = back.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(back) : Quaternion.identity;
        EffectPlayer.Play(currentImpact, to, facing, currentImpactScale, EffectLifetime);
    }

    private void FireVolley()
    {
        float damage = tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE);
        ResolveStyle();

        foreach (ShootingPointReference shootingPoint in tower.ShootingPoints)
        {
            if (!shootingPoint.IsReferenceEnabled)
                continue;

            // The previous shot of this frame may have killed the target
            if (target == null || !target.IsAlive)
                target = tower.Targetter.GetEnemy(tower.TargetBehaviour);

            Transform barrel = shootingPoint.transform;
            if (target != null)
            {
                // Visuals first: the hit may kill and recycle the enemy
                ShowShot(barrel, target, currentTracer);
                target.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
                if (currentMuzzle != null)
                    EffectPlayer.Play(currentMuzzle, barrel.position, barrel.rotation, currentMuzzleScale, EffectLifetime);
                else if (muzzleFlash != null)
                    muzzleFlash.Play();
            }

            if (secondShotStrongTargetting)
            {
                Enemy strongestEnemy = tower.Targetter.GetStrongestEnemyInRadius();
                if (strongestEnemy != null)
                {
                    ShowShot(barrel, strongestEnemy, currentSecondTracer);
                    strongestEnemy.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
                }
            }

            if (thirdShotLastTargetting)
            {
                Enemy lastEnemy = tower.Targetter.GetLastEnemyInRadius();
                if (lastEnemy != null)
                {
                    ShowShot(barrel, lastEnemy, currentThirdTracer);
                    lastEnemy.TakeDamage(damage, DamageType.PROJECTILE, this.tower);
                }
            }
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
