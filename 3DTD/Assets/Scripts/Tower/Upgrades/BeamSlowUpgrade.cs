using UnityEngine;

// Beam ticks slow the enemies they hit (strongest active slow wins, see Enemy.ApplySlowness)
public class BeamSlowUpgrade : Upgrade
{
    [Range(0f, 0.9f)]
    [SerializeField] private float slowOnHit = 0.2f;
    [SerializeField] private float slowDuration = 0.5f;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (BeamTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BeamTowerActionStrategy>())
        {
            strategy.slowOnHit = slowOnHit;
            strategy.slowDuration = slowDuration;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (BeamTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BeamTowerActionStrategy>())
            strategy.slowOnHit = 0f;
    }
}
