using UnityEngine;

// Changes the weapons the Starfighters of a hangar carry
public class StarfighterLoadoutUpgrade : Upgrade
{
    [Tooltip("Fire all cannons at once instead of alternating between them")]
    [SerializeField] private bool enableTwinLinkedCannons = false;
    [Tooltip("Homing missiles fired at the start of every attack run")]
    [SerializeField] private int additionalMissilesPerRun = 0;
    [Tooltip("Drop bombs on enemies below the fighter during attack runs")]
    [SerializeField] private bool enableCarpetBombing = false;

    public override void ApplyUpgrade(StatsManager statsManager)
    {
        foreach (HangarTowerActionStrategy strategy in statsManager.gameObject.GetComponents<HangarTowerActionStrategy>())
        {
            if (enableTwinLinkedCannons)
                strategy.twinLinkedCannons = true;

            if (enableCarpetBombing)
                strategy.carpetBombing = true;

            strategy.missilesPerRun += additionalMissilesPerRun;
        }
    }

    public override void RemoveUpgrade(StatsManager statsManager)
    {
        foreach (HangarTowerActionStrategy strategy in statsManager.gameObject.GetComponents<HangarTowerActionStrategy>())
        {
            if (enableTwinLinkedCannons)
                strategy.twinLinkedCannons = false;

            if (enableCarpetBombing)
                strategy.carpetBombing = false;

            strategy.missilesPerRun -= additionalMissilesPerRun;
        }
    }
}
