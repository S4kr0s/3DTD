using UnityEngine;

// Lets a Mine Factory turn mines it can't place (field full) into money and lives
public class MineSalvageUpgrade : Upgrade
{
    [Tooltip("Money per mine finished while the minefield is full")]
    [SerializeField] private float additionalScrapValue = 0f;
    [Tooltip("Every n-th scrapped mine restores a life (0 = no change)")]
    [SerializeField] private int scrapsPerLife = 0;
    [SerializeField] private int additionalMaxLivesPerWave = 0;
    [Tooltip("Money per mine still on the field when a wave ends")]
    [SerializeField] private float additionalWaveEndPayoutPerMine = 0f;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (MineFactoryActionStrategy strategy in statsManager.gameObject.GetComponents<MineFactoryActionStrategy>())
        {
            strategy.scrapValue += additionalScrapValue;
            if (scrapsPerLife > 0)
                strategy.scrapsPerLife = strategy.scrapsPerLife > 0 ? Mathf.Min(strategy.scrapsPerLife, scrapsPerLife) : scrapsPerLife;
            strategy.maxLivesPerWave += additionalMaxLivesPerWave;
            strategy.waveEndPayoutPerMine += additionalWaveEndPayoutPerMine;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (MineFactoryActionStrategy strategy in statsManager.gameObject.GetComponents<MineFactoryActionStrategy>())
        {
            strategy.scrapValue -= additionalScrapValue;
            if (scrapsPerLife > 0)
                strategy.scrapsPerLife = 0;
            strategy.maxLivesPerWave -= additionalMaxLivesPerWave;
            strategy.waveEndPayoutPerMine -= additionalWaveEndPayoutPerMine;
        }
    }
}
