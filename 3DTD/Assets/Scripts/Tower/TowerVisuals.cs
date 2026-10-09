using System.Collections.Generic;
using UnityEngine;

// The effect prefabs a tower's weapons use can be replaced by its upgrades (VisualUpgrade). Each slot is one
// thing a strategy plays or fires; strategies keep their serialized prefab as the fallback. Projectile towers
// combine a whole-prefab slot with the Muzzle/Flight/Impact part slots, so each upgrade path can style its
// own part of the shot and the paths stay visible together.
// Values are serialized on the upgrade components: only append.
public enum VisualSlot
{
    Projectile,         // the projectile prefab of Laser, Core, Bullet Dispenser and Rocket System
    ClusterProjectile,  // the Rocket System's projectile while it fires cluster rockets
    Cannon,             // Hangar: starfighter cannon bolts
    Ordnance,           // Hangar: starfighter missiles and bombs
    PulseProjectile,    // retired (the Bullet Dispenser's pulse mode)
    AuraHit,            // retired (the Bullet Dispenser's flamethrower)
    MineBlast,          // Mine Factory: blast of a mine
    MineHeavyBlast,     // Mine Factory: blast with heavy explosions
    MineClusterBlast,   // Mine Factory: bomblet blast
    MineLaunch,         // Mine Factory: launch puff
    Muzzle,             // muzzle flash of the Projectile (on top of whole-prefab swaps); Sniper flash; Beam tick pulse
    Impact,             // impact of the Projectile; Sniper and Beam hits
    Flight,             // flight effect of the Projectile
    Bounce,             // Bullet Dispenser: spark where a needle rebounds off the range dome
    Singularity,        // Bullet Dispenser: implosion of the gravity well's singularity
}

// The visual upgrades a tower owns, lowest priority first. A slot resolves to the prefab of the highest
// priority upgrade that sets it; layered settings (beam, tracers) apply in this order, later ones winning.
// Version changes whenever the set changes, so strategies only resolve again after an upgrade.
public sealed class TowerVisuals
{
    private readonly List<VisualUpgrade> applied = new List<VisualUpgrade>();

    public int Version { get; private set; }
    public int Count => applied.Count;
    public VisualUpgrade this[int index] => applied[index];

    public void Add(VisualUpgrade upgrade)
    {
        if (upgrade == null || applied.Contains(upgrade))
            return;
        // Stable: an equal priority goes after the ones already there (the newer purchase wins)
        int index = applied.Count;
        while (index > 0 && applied[index - 1].Priority > upgrade.Priority)
            index--;
        applied.Insert(index, upgrade);
        Version++;
    }

    public void Remove(VisualUpgrade upgrade)
    {
        if (applied.Remove(upgrade))
            Version++;
    }

    public GameObject Resolve(VisualSlot slot, GameObject fallback)
    {
        for (int i = applied.Count - 1; i >= 0; i--)
        {
            GameObject prefab = applied[i].Get(slot);
            if (prefab != null)
                return prefab;
        }
        return fallback;
    }
}

// A strategy's cached view of one slot: resolves again only after the tower's visual upgrades changed
public struct VisualRef
{
    private GameObject prefab;
    private int version;
    private bool resolved;

    public GameObject Get(Tower tower, VisualSlot slot, GameObject fallback)
    {
        if (tower == null)
            return fallback;
        TowerVisuals visuals = tower.Visuals;
        if (!resolved || version != visuals.Version)
        {
            prefab = visuals.Resolve(slot, fallback);
            version = visuals.Version;
            resolved = true;
        }
        return prefab;
    }
}
