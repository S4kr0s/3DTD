using UnityEngine;

// Changes the mines a Mine Factory builds: cluster bomblets, slowing shockwaves, seeking and the heavy blast effect
public class MineWarheadUpgrade : Upgrade
{
    [Tooltip("Bomblets every detonation scatters along the path")]
    [SerializeField] private int additionalClusterBomblets = 0;
    [Tooltip("Slow applied to enemies that survive a blast (0.5 = half speed)")]
    [Range(0f, 0.9f)]
    [SerializeField] private float blastSlow = 0f;
    [SerializeField] private float blastSlowDuration = 1.5f;
    [Tooltip("Armed mines drift towards enemies closer than this (0 = no change)")]
    [SerializeField] private float seekRadius = 0f;
    [Tooltip("Use the factory's heavy explosion effect")]
    [SerializeField] private bool heavyExplosions = false;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (MineFactoryActionStrategy strategy in statsManager.gameObject.GetComponents<MineFactoryActionStrategy>())
        {
            strategy.clusterBomblets += additionalClusterBomblets;

            if (blastSlow > strategy.blastSlow)
            {
                strategy.blastSlow = blastSlow;
                strategy.blastSlowDuration = blastSlowDuration;
            }

            strategy.seekRadius = Mathf.Max(strategy.seekRadius, seekRadius);

            if (heavyExplosions)
                strategy.heavyExplosions = true;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (MineFactoryActionStrategy strategy in statsManager.gameObject.GetComponents<MineFactoryActionStrategy>())
        {
            strategy.clusterBomblets -= additionalClusterBomblets;

            if (blastSlow > 0f)
                strategy.blastSlow = 0f;

            if (seekRadius > 0f)
                strategy.seekRadius = 0f;

            if (heavyExplosions)
                strategy.heavyExplosions = false;
        }
    }
}
