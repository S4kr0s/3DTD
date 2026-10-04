using UnityEngine;

// Makes the Starfighters of a hangar fly faster and turn sharper
public class StarfighterEngineUpgrade : Upgrade
{
    [SerializeField] private float flightSpeedMultiplier = 1.35f;
    [SerializeField] private float turnRateMultiplier = 1.25f;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (HangarTowerActionStrategy strategy in statsManager.gameObject.GetComponents<HangarTowerActionStrategy>())
        {
            strategy.flightSpeedMultiplier *= flightSpeedMultiplier;
            strategy.turnRateMultiplier *= turnRateMultiplier;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (HangarTowerActionStrategy strategy in statsManager.gameObject.GetComponents<HangarTowerActionStrategy>())
        {
            strategy.flightSpeedMultiplier /= flightSpeedMultiplier;
            strategy.turnRateMultiplier /= turnRateMultiplier;
        }
    }
}
