using PolygonArsenal;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeamTowerActionStrategy : ActionStrategy
{
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private PolygonBeamStatic beam;

    [SerializeField] private float internalFireRate;
    [SerializeField] private float internalPierce;
    [SerializeField] private ParticleSystem pulseParticle;
    private Tower tower;
    private GameObject target;

    public override void SetupActionStrategy(Tower tower)
    {
        this.tower = tower;
        internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);
    }

    public override void ExecuteAction()
    {
        if (internalFireRate <= 0)
        {
            internalFireRate = tower.StatsManager.GetStatValue(Stat.StatType.FIRERATE);
            internalPierce = tower.StatsManager.GetStatValue(Stat.StatType.PIERCING);

            pulseParticle.Play();

            foreach (ShootingPointReference shootingPointReference in tower.ShootingPoints)
            {
                if (!shootingPointReference.IsReferenceEnabled) return;

                RaycastHit[] hitInfos = Physics.RaycastAll(shootingPointReference.transform.position, shootingPointReference.transform.forward, tower.StatsManager.GetStatValue(Stat.StatType.RANGE) + 0.5f);
                
                if (hitInfos != null)
                {
                    foreach (RaycastHit hitInfo in hitInfos)
                    {
                        if (hitInfo.collider.gameObject.TryGetComponent(out Enemy enemy))
                        {
                            if (internalPierce > 0)
                            {
                                enemy.TakeDamage(tower.StatsManager.GetStatValue(Stat.StatType.DAMAGE), DamageType.PROJECTILE, this.tower);
                                internalPierce--;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            internalFireRate -= Time.deltaTime;
        }
    }

    public override bool CanShoot(GameObject enemy)
    {
        return true;
    }
}
