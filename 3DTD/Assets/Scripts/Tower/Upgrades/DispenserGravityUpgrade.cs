using UnityEngine;

// The Bullet Dispenser's gravity well: drags enemies in range off their path towards the barrel ball and slows
// them; the singularity periodically collapses them all around it (see BulletDispenserTowerActionStrategy)
public class DispenserGravityUpgrade : Upgrade
{
    [Tooltip("How far enemies are dragged off their path (the highest of the bought modules counts)")]
    [SerializeField] private float pull = 0.5f;
    [Tooltip("Units per second the well drags enemies")]
    [SerializeField] private float pullSpeed = 1.5f;
    [Tooltip("Slow of the enemies in range (0.3 = 30% slower)")]
    [Range(0f, 0.9f)]
    [SerializeField] private float slow = 0f;
    [Tooltip("Seconds between collapses of the singularity (0 = none)")]
    [SerializeField] private float singularityInterval = 0f;
    [Tooltip("Seconds a collapse holds the enemies")]
    [SerializeField] private float singularityDuration = 0f;

    public float Pull => pull;
    public float PullSpeed => pullSpeed;
    public float Slow => slow;
    public float SingularityInterval => singularityInterval;
    public float SingularityDuration => singularityDuration;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (BulletDispenserTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BulletDispenserTowerActionStrategy>())
        {
            strategy.gravityPull = Mathf.Max(strategy.gravityPull, pull);
            strategy.gravityPullSpeed = Mathf.Max(strategy.gravityPullSpeed, pullSpeed);
            strategy.gravitySlow = Mathf.Max(strategy.gravitySlow, slow);
            if (singularityInterval > 0f)
            {
                strategy.singularityInterval = singularityInterval;
                strategy.singularityDuration = singularityDuration;
            }
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (BulletDispenserTowerActionStrategy strategy in statsManager.gameObject.GetComponents<BulletDispenserTowerActionStrategy>())
        {
            strategy.gravityPull = 0f;
            strategy.gravityPullSpeed = 0f;
            strategy.gravitySlow = 0f;
            strategy.singularityInterval = 0f;
            strategy.singularityDuration = 0f;
        }
    }
}
