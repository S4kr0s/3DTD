using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fixed-direction beam that ticks every FIRERATE seconds and hits up to PIERCING enemies along RANGE.
// Magic damage, so armor doesn't reduce it.
public class BeamTowerActionStrategy : ActionStrategy
{
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[64];

    [SerializeField] private LayerMask layerMask;
    [SerializeField] private PolygonBeamStatic beam;

    [SerializeField] private float internalFireRate;
    [SerializeField] private float internalPierce;
    [SerializeField] private ParticleSystem pulseParticle;

    [Header("Slow (set by upgrades)")]
    [Tooltip("Share of movement speed removed from enemies hit, 0 = no slow")]
    [Range(0f, 0.9f)]
    [SerializeField] public float slowOnHit = 0f;
    [SerializeField] public float slowDuration = 0.5f;

    private FireCycle fireCycle;
    private Tower tower;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        fireCycle = new FireCycle(0f);
    }

    public override void ExecuteAction()
    {
        int ticks = fireCycle.Tick(Time.deltaTime, true, tower.StatsManager.GetFireInterval(), 0f, 0f);
        for (int i = 0; i < ticks; i++)
            Tick();
    }

    private void Tick()
    {
        StatsManager stats = tower.StatsManager;
        internalPierce = stats.GetStatValue(Stat.StatType.PIERCING);
        float damage = stats.GetStatValue(Stat.StatType.DAMAGE);
        float length = stats.GetStatValue(Stat.StatType.RANGE) + 0.5f;

        if (pulseParticle != null)
            pulseParticle.Play();

        foreach (ShootingPointReference shootingPointReference in tower.ShootingPoints)
        {
            if (!shootingPointReference.IsReferenceEnabled)
                continue;

            Transform origin = shootingPointReference.transform;
            // Only enemies: tower ranges, blocks and anchors used to fill the hit buffer too
            int count = Physics.RaycastNonAlloc(origin.position, origin.forward, hitBuffer, length, GameLayers.EnemyMask, QueryTriggerInteraction.Ignore);
            // Nearest enemies first, so pierce is spent along the beam (insertion sort: Array.Sort with a
            // comparer allocates a delegate per call, and the beam hits only a handful)
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = hitBuffer[i];
                int j = i - 1;
                for (; j >= 0 && hitBuffer[j].distance > hit.distance; j--)
                    hitBuffer[j + 1] = hitBuffer[j];
                hitBuffer[j + 1] = hit;
            }

            for (int i = 0; i < count && internalPierce > 0; i++)
            {
                if (hitBuffer[i].collider.gameObject.TryGetComponent(out Enemy enemy) && enemy.IsAlive)
                {
                    enemy.TakeDamage(damage, DamageType.MAGIC, this.tower);
                    if (slowOnHit > 0f && enemy.IsAlive)
                        enemy.ApplySlowness(-slowOnHit, slowDuration);
                    internalPierce--;
                }
            }
        }
    }

    public override bool CanShoot(GameObject enemy)
    {
        return true;
    }
}
