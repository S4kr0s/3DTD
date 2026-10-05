using System.Collections.Generic;
using PolygonArsenal;
using UnityEngine;
using Object = UnityEngine.Object;

// The looks. Every tower has a base look and each upgrade module restyles one part of the shot, so the paths
// read at a glance and stay visible together:
//   Laser             cyan bolts; rate -> longer streaks (minigun tracers), pierce -> impacts (triple darts),
//                     caliber -> muzzle and impact (high-energy antimatter bolt)
//   Core              the Laser's bolt in a paler, lighter blue
//   Rocket System     green rockets with ember wisps instead of smoke; delivery -> exhaust (micro rockets),
//                     area -> explosion (cluster), salvo -> launch (barrage warheads)
//   Bullet Dispenser  small dark green needles; barrels -> muzzle (needle storm), magazine -> streak
//                     (toxic flamethrower), ammo -> tips and impacts (dark green pulse)
//   Hangar            small red bolts and mini missiles; cannons -> plasma and proton bolts, ordnance -> bombs
//   Beam              yellow beam; length -> end flare, tick -> pulse and crackle, damage -> width and core
//   Sniper            red tracer, muzzle and impact; power -> flash and tracer, multi-shot -> tracer colours,
//                     speed -> shorter, thinner tracers
//   Mine Factory      red ember explosions; warheads -> blasts, assembly -> launch
// Explosion scale follows the blast radius and impact scale the SIZE stat (ProjectileSystem), so the upgrades
// that grow those also grow the effects.
public static partial class ProjectileVisualsBuilder
{
    // Smoke wisp colours (PolySolidGlow brightens them about 1.9 times)
    private static readonly Color GreenEmber = new Color(0.3f, 0.55f, 0.1f);
    private static readonly Color RedEmber = new Color(0.62f, 0.24f, 0.05f);
    private static readonly Color AmberEmber = new Color(0.6f, 0.42f, 0.08f);

    private static void BuildEffects()
    {
        Laser();
        Core();
        Rockets();
        Dispenser();
        Hangar();
        Beam();
        Sniper();
        MineFactory();
    }

    private static void WireTowers()
    {
        foreach (System.Action wire in towerWiring)
            wire();
        towerWiring.Clear();
    }

    private static readonly List<System.Action> towerWiring = new List<System.Action>();

    private static (VisualSlot, GameObject)[] Parts(GameObject muzzle, GameObject flight, GameObject impact)
    {
        List<(VisualSlot, GameObject)> parts = new List<(VisualSlot, GameObject)>();
        if (muzzle != null)
            parts.Add((VisualSlot.Muzzle, muzzle));
        if (flight != null)
            parts.Add((VisualSlot.Flight, flight));
        if (impact != null)
            parts.Add((VisualSlot.Impact, impact));
        return parts.ToArray();
    }

    // ---- Laser (Default Tower) -------------------------------------------------------------------------

    private static void Laser()
    {
        const string bolt = "Missiles/Sci-Fi/Laser/LaserBlue";
        const string muzzle = "Muzzleflash/Sci-Fi/Laser/LaserMuzzleBlue";
        const string impact = "Explosions/Sci-Fi/Laser/LaserExplosionBlue";

        // Fire rate: the bolts stretch into streaks, the minigun spits small tracer rounds
        GameObject streak = Fx("Laser/LaserStreak", bolt, Stretch("<root>", 0.85f, 1.5f), Count("SparkTrail", 1.5f));
        GameObject streak2 = Fx("Laser/LaserStreakLong", bolt, Stretch("<root>", 0.7f, 2.2f), Count("SparkTrail", 2.5f), Lifetime("SparkTrail", 1.5f), TrailTime(1.5f, 0.8f));
        GameObject minigunRound = Fx("Laser/MinigunRound", "Missiles/Sci-Fi/Bullet/BulletBlue", Stretch("<root>", 0.8f, 1.8f));
        GameObject minigunMuzzle = Fx("Laser/MinigunMuzzle", "Muzzleflash/Sci-Fi/Bullet/BulletMuzzleBlue", Scale(0.6f));
        GameObject minigunImpact = Fx("Laser/MinigunImpact", "Explosions/Sci-Fi/Bullet/BulletExplosionBlue", Scale(0.5f));

        // Range and pierce: hits throw sharp sparks through the enemy, the triple shot fires slim darts
        GameObject pierceImpact = Fx("Laser/PierceImpact", impact, Count("Sparks", 2.5f), Speed("Sparks", 1.4f), Stretch("Sparks", 1f, 1.8f));
        GameObject pierceImpact2 = Fx("Laser/PierceImpactLong", impact, Count("Sparks", 4f), Speed("Sparks", 1.7f), Stretch("Sparks", 1f, 2.6f), Size("SphereGlow", 1.3f));
        GameObject dart = Fx("Laser/TripleDart", "Missiles/Magic/Spike/SpikeMissileBlue", Scale(0.45f));
        GameObject dartMuzzle = Fx("Laser/TripleMuzzle", "Muzzleflash/Sci-Fi/Spike/SpikeMuzzleBlue", Scale(0.5f));
        GameObject dartImpact = Fx("Laser/TripleImpact", "Explosions/Sci-Fi/Spike/SpikeExplosionBlue", Scale(0.4f));

        // Caliber: a heavier muzzle flash, then plasma impacts, then the high-energy antimatter bolt
        GameObject caliberMuzzle = Fx("Laser/CaliberMuzzle", muzzle, Scale(1.35f), Count("MuzzleSparks", 2.5f));
        GameObject heavyImpact = Fx("Laser/HeavyImpact", "Explosions/Sci-Fi/Plasma/PlasmaExplosionBlue", Scale(0.4f));
        GameObject energyBolt = Fx("Laser/HighEnergyBolt", "Missiles/Sci-Fi/Antimatter/AntimatterMissileBlue", Scale(0.45f));
        GameObject energyMuzzle = Fx("Laser/HighEnergyMuzzle", "Muzzleflash/Sci-Fi/Antimatter/AntimatterMuzzleBlue", Scale(0.55f));
        GameObject energyImpact = Fx("Laser/HighEnergyImpact", "Explosions/Sci-Fi/Energy/EnergyExplosionBlue", Scale(0.35f));

        towerWiring.Add(() => EditTower("Default Tower", tower =>
        {
            Visual<VisualUpgrade>(tower, 1, 1, Parts(null, streak, null));
            Visual<VisualUpgrade>(tower, 1, 2, Parts(null, streak2, null));
            Visual<VisualUpgrade>(tower, 1, 3, Parts(minigunMuzzle, minigunRound, minigunImpact));
            Visual<VisualUpgrade>(tower, 2, 1, Parts(null, null, pierceImpact));
            Visual<VisualUpgrade>(tower, 2, 2, Parts(null, null, pierceImpact2));
            Visual<VisualUpgrade>(tower, 2, 3, Parts(dartMuzzle, dart, dartImpact));
            Visual<VisualUpgrade>(tower, 3, 1, Parts(caliberMuzzle, null, null));
            Visual<VisualUpgrade>(tower, 3, 2, Parts(caliberMuzzle, null, heavyImpact));
            Visual<VisualUpgrade>(tower, 3, 3, Parts(energyMuzzle, energyBolt, energyImpact));
        }));
    }

    // ---- Core Tower ------------------------------------------------------------------------------------

    private static void Core()
    {
        // Paler and lighter than the Laser's cyan: sky blue with white cores
        Edit coreBlue = Tint(208f, 0.55f, 1.05f, 0.12f);
        GameObject muzzle = Fx("Core/CoreMuzzle", "Muzzleflash/Sci-Fi/Laser/LaserMuzzleBlue", coreBlue);
        GameObject bolt = Fx("Core/CoreBolt", "Missiles/Sci-Fi/Laser/LaserBlue", coreBlue);
        GameObject impact = Fx("Core/CoreImpact", "Explosions/Sci-Fi/Laser/LaserExplosionBlue", coreBlue);
        GameObject projectile = Projectile("Core/CoreBolt", "ProjectileParent", muzzle, bolt, impact);

        towerWiring.Add(() => EditTower("Core Tower", tower =>
        {
            foreach (LaserTowerActionStrategy strategy in tower.Root.GetComponents<LaserTowerActionStrategy>())
                SetField(strategy, "projectile", projectile);
        }));
    }

    // ---- Rocket System (Bomb Tower) --------------------------------------------------------------------

    private static void Rockets()
    {
        const string rocket = "Missiles/Sci-Fi/Rocket/RocketMissileGreen";
        const string launch = "Muzzleflash/Sci-Fi/Rocket/RocketMuzzleGreen";
        const string blast = "Explosions/Sci-Fi/Rocket/RocketExplosionGreen";

        GameObject muzzle = Fx("Bomb/RocketMuzzle", launch, Smoke(GreenEmber, 0.35f));
        GameObject flight = Fx("Bomb/Rocket", rocket, Smoke(GreenEmber, 0.3f, 0.5f, 0.6f));
        GameObject explosion = Fx("Bomb/RocketExplosion", blast, Smoke(GreenEmber));
        GameObject projectile = Projectile("Bomb/Rocket", "ProjectileParentBomb", muzzle, flight, explosion);

        // Cluster rockets: a bigger mother rocket that bursts into green bomblets (blast radius 0.6 of the
        // rocket's, so their explosion is drawn larger to match the old look)
        GameObject bombletFlight = Fx("Bomb/Bomblet", "Missiles/Sci-Fi/Grenade/GrenadeGreen", Smoke(GreenEmber, 0.3f));
        GameObject bombletBlast = Fx("Bomb/BombletExplosion", "Explosions/Sci-Fi/Grenade/GrenadeExplosionGreen", Smoke(GreenEmber), Scale(1.2f));
        GameObject bomblet = Projectile("Bomb/Bomblet", "GrenadeBlueOBJ", null, bombletFlight, bombletBlast);
        GameObject clusterFlight = Fx("Bomb/ClusterRocket", rocket, Smoke(GreenEmber, 0.3f, 0.5f, 0.6f), Scale(1.3f), Count("SparkTrail", 1.5f));
        GameObject cluster = Projectile("Bomb/ClusterRocket", "ProjectileParentBombClustering", muzzle, clusterFlight, explosion, bomblet);

        // Delivery: guided rockets trail lime sparks instead of smoke, faster ones burn hotter, rapid fire
        // launches micro rockets that pop in acid bursts
        GameObject guided = Fx("Bomb/GuidedRocket", rocket, Remove("SmokeTrail"), Count("SparkTrail", 2.5f), Tint(80f, 1f, 1.2f, 0f, "SparkTrail"));
        GameObject hot = Fx("Bomb/HotRocket", rocket, Smoke(new Color(0.55f, 0.55f, 0.12f), 0.3f, 0.45f, 0.5f), Size("<root>", 1.2f), Count("SparkTrail", 2f), Tint(58f, 1f, 1.2f, 0.4f, "SparkTrail"));
        GameObject micro = Fx("Bomb/MicroRocket", rocket, Smoke(GreenEmber, 0.2f, 0.4f, 0.5f), Scale(0.6f));
        GameObject microMuzzle = Fx("Bomb/MicroMuzzle", launch, Smoke(GreenEmber, 0.2f), Scale(0.6f));
        GameObject microBlast = Fx("Bomb/MicroExplosion", "Explosions/Mini/MiniExploAcid", Scale(0.9f));

        // Area: more and faster sparks, then a green shockwave ring, then the cluster rockets
        GameObject wide = Fx("Bomb/WideExplosion", blast, Smoke(GreenEmber), Count("Sparks", 1.6f), Speed("Sparks", 1.25f));
        GameObject area = Fx("Bomb/AreaExplosion", blast, Smoke(GreenEmber), Count("Sparks", 1.6f), Speed("Sparks", 1.25f),
            Merge("Explosions/Sci-Fi/Energy/EnergyExplosionGreen", 0.45f));

        // Salvo: a twin flare at launch, a ripple of launch sparks, then heavy proton warheads with a small
        // green mushroom flash
        GameObject twin = Fx("Bomb/TwinMuzzle", launch, Smoke(GreenEmber, 0.3f), Size("Glow", 1.4f), Count("Sparks", 1.6f));
        GameObject ripple = Fx("Bomb/ArrayMuzzle", launch, Smoke(GreenEmber, 0.2f), Count("Sparks", 2f),
            Merge("Muzzleflash/Sci-Fi/Grenade/GrenadeMuzzleGreen", 0.7f, default, default, Smoke(GreenEmber, 0.2f)));
        GameObject warhead = Fx("Bomb/BarrageWarhead", "Missiles/Sci-Fi/Proton/ProtonMissileGreen", Scale(0.5f));
        GameObject warheadMuzzle = Fx("Bomb/BarrageMuzzle", "Muzzleflash/Sci-Fi/Proton/ProtonMuzzleGreen", Scale(0.6f));
        GameObject warheadBlast = Fx("Bomb/BarrageExplosion", "Explosions/Sci-Fi/NukeSimple/NukeSimpleExplosionGreen", Smoke(GreenEmber), Scale(0.3f));

        towerWiring.Add(() => EditTower("Bomb Tower", tower =>
        {
            foreach (BombTowerActionStrategy strategy in tower.Root.GetComponents<BombTowerActionStrategy>())
                SetField(strategy, "projectile", GetBool(strategy, "doClustering") ? cluster : projectile);

            Visual<VisualUpgrade>(tower, 1, 1, Parts(null, guided, null));
            Visual<VisualUpgrade>(tower, 1, 2, Parts(null, hot, null));
            Visual<VisualUpgrade>(tower, 1, 3, Parts(microMuzzle, micro, microBlast));
            Visual<VisualUpgrade>(tower, 2, 1, Parts(null, null, wide));
            Visual<VisualUpgrade>(tower, 2, 2, Parts(null, null, area));
            Visual<VisualUpgrade>(tower, 3, 1, Parts(twin, null, null));
            Visual<VisualUpgrade>(tower, 3, 2, Parts(ripple, null, null));
            Visual<VisualUpgrade>(tower, 3, 3, Parts(warheadMuzzle, warhead, warheadBlast));
        }));
    }

    // ---- Bullet Dispenser ------------------------------------------------------------------------------

    private static void Dispenser()
    {
        const string bullet = "Missiles/Sci-Fi/Bullet/BulletGreen";
        // Dark green: the alpha blended cores turn dark, the additive glows dim
        Edit dark = Tint(135f, 1f, 0.45f, 0.85f);

        // The dispenser fires by far the most shots (150 maxed ones are a benchmark): its effects stay within the
        // particle counts of the laser set it used before (about 13 per muzzle, 12 per impact)
        GameObject muzzle = Fx("Dispenser/NeedleMuzzle", "Muzzleflash/Sci-Fi/Bullet/BulletMuzzleGreen", dark, Scale(0.45f), Count(null, 0.5f));
        GameObject needle = Fx("Dispenser/Needle", bullet, dark, Stretch("<root>", 0.55f, 2.2f), Scale(0.6f));
        GameObject impact = Fx("Dispenser/NeedleImpact", "Explosions/Sci-Fi/Bullet/BulletExplosionGreen", dark, Scale(0.45f), Remove("Smoke"),
            Count("Sparks", 0.5f), Lifetime("Sparks", 0.6f));
        GameObject projectile = Projectile("Dispenser/Needle", "ProjectileParentRound", muzzle, needle, impact);

        // Pulse mode: a dark green energy nova
        GameObject nova = Fx("Dispenser/PulseNova", "Assets/EnergyNovaBlue 1.prefab", Tint(135f, 1f, 0.6f, 0.6f));
        GameObject pulse = Projectile("Dispenser/Pulse", "ProjectileParentPulse", nova, null, null);

        // Barrels: compact puffs, then a ring of sparks, then needles that leave short trails (a needle storm)
        GameObject compact = Fx("Dispenser/CompactMuzzle", "Muzzleflash/Sci-Fi/Bullet/BulletMuzzleGreen", dark, Scale(0.32f), Count(null, 0.35f));
        GameObject ring = Fx("Dispenser/RingMuzzle", "Muzzleflash/Sci-Fi/Bullet/BulletMuzzleGreen", dark, Scale(0.4f), Remove("MuzzleSparks"),
            Count("<root>", 0.4f), Size("StartRing", 1.6f), Tint(115f, 1f, 0.7f, 0.85f, "StartRing"));
        GameObject storm = Fx("Dispenser/StormNeedle", bullet, dark, Stretch("<root>", 0.55f, 2.2f), Scale(0.6f), TrailTime(3f, 0.7f));

        // Magazine: longer streaks, hot tips, and a toxic green flamethrower
        GameObject longNeedle = Fx("Dispenser/LongNeedle", bullet, dark, Stretch("<root>", 0.5f, 3f), Scale(0.6f), Count("SparkTrail", 2f));
        GameObject hotNeedle = Fx("Dispenser/HotNeedle", bullet, dark, Stretch("<root>", 0.5f, 3f), Scale(0.6f), Count("SparkTrail", 4f),
            Tint(75f, 1f, 1.4f, 0.5f, "SparkTrail"));
        GameObject toxicFire = Fx("Dispenser/ToxicFire", "Assets/Prefabs/MiniExploFire.prefab", Tint(110f, 1f, 0.9f, 0.3f));

        // Ammo: diamond tips shatter in white-green shards, aerodynamic needles leave a vapour trail
        GameObject shards = Fx("Dispenser/DiamondImpact", "Explosions/Sci-Fi/Bullet/BulletExplosionGreen", Tint(140f, 0.35f, 1.2f), Scale(0.45f), Remove("Smoke"),
            Count("Sparks", 0.6f), Lifetime("Sparks", 0.6f));
        GameObject vapour = Fx("Dispenser/VapourNeedle", bullet, dark, Stretch("<root>", 0.5f, 2.6f), Scale(0.6f), TrailTime(2f, 1.1f), Tint(140f, 0.35f, 1f, 0f, "Glow"));

        towerWiring.Add(() => EditTower("Bullet Dispenser Tower", tower =>
        {
            foreach (BulletDispenserTowerActionStrategy strategy in tower.Root.GetComponents<BulletDispenserTowerActionStrategy>())
            {
                if (GetBool(strategy, "PulseMode"))
                    SetField(strategy, "projectile", pulse);
                else if (GetBool(strategy, "AuraMode"))
                    SetField(strategy, "hitParticle", toxicFire);
                else
                    SetField(strategy, "projectile", projectile);
            }
            // The flamethrower's own flames (a nested FlamethrowerAura) burn toxic green too; recoloured from
            // its source each run, so the tint doesn't stack
            foreach (Transform child in tower.Root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "FlamethrowerAura")
                    ToxicFlames(child.gameObject);
            }

            Visual<VisualUpgrade>(tower, 1, 1, Parts(compact, null, null));
            Visual<VisualUpgrade>(tower, 1, 2, Parts(ring, null, null));
            Visual<VisualUpgrade>(tower, 1, 3, Parts(ring, storm, null));
            Visual<VisualUpgrade>(tower, 2, 1, Parts(null, longNeedle, null));
            Visual<VisualUpgrade>(tower, 2, 2, Parts(null, hotNeedle, null));
            Visual<VisualUpgrade>(tower, 3, 1, Parts(null, null, shards));
            Visual<VisualUpgrade>(tower, 3, 2, Parts(null, vapour, shards));
        }));
    }

    private static void ToxicFlames(GameObject aura)
    {
        GameObject source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(aura);
        ParticleSystem[] sourceSystems = source != null ? source.GetComponentsInChildren<ParticleSystem>(true) : null;
        ParticleSystem[] systems = aura.GetComponentsInChildren<ParticleSystem>(true);
        if (sourceSystems == null || sourceSystems.Length != systems.Length)
        {
            Fail("FlamethrowerAura: no matching source prefab to recolour from");
            return;
        }
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.startColor = Map(sourceSystems[i].main.startColor, ToxicColor);
            ParticleSystem.ColorOverLifetimeModule overLifetime = systems[i].colorOverLifetime;
            if (sourceSystems[i].colorOverLifetime.enabled)
                overLifetime.color = Map(sourceSystems[i].colorOverLifetime.color, ToxicColor);
        }
    }

    private static Color ToxicColor(Color c)
    {
        return Shift(c, 110f / 360f, 1f, 0.9f, 0.3f);
    }

    // ---- Hangar Tower ----------------------------------------------------------------------------------

    private static void Hangar()
    {
        // Red and small, to fit the starfighters (the cannon's SIZE stat shrinks it further)
        GameObject cannonMuzzle = Fx("Hangar/CannonMuzzle", "Muzzleflash/Sci-Fi/Laser/LaserMuzzleRed", Scale(0.6f));
        GameObject cannonBolt = Fx("Hangar/CannonBolt", "Missiles/Sci-Fi/Laser/LaserRed", Scale(0.6f));
        GameObject cannonImpact = Fx("Hangar/CannonImpact", "Explosions/Sci-Fi/Laser/LaserExplosionRed", Scale(0.7f));
        GameObject cannon = Projectile("Hangar/CannonBolt", "ProjectileParent", cannonMuzzle, cannonBolt, cannonImpact);

        GameObject launch = Fx("Hangar/MissileLaunch", "Muzzleflash/Sci-Fi/Rocket/RocketMuzzleRed", Smoke(RedEmber, 0.2f), Scale(0.4f));
        GameObject missileFlight = Fx("Hangar/Missile", "Missiles/Sci-Fi/Rocket/RocketMissileRed", Smoke(RedEmber, 0.25f, 0.45f, 0.5f), Scale(0.6f));
        GameObject missileBlast = Fx("Hangar/MissileExplosion", "Explosions/Sci-Fi/Rocket/RocketExplosionRed", Smoke(RedEmber), Scale(0.5f));
        GameObject missile = Projectile("Hangar/Missile", "ProjectileParentBomb", launch, missileFlight, missileBlast);

        // Cannons: twin muzzle sparks, then plasma bolts, then crackling proton bolts
        GameObject twinMuzzle = Fx("Hangar/TwinMuzzle", "Muzzleflash/Sci-Fi/Laser/LaserMuzzleRed", Scale(0.6f), Count("MuzzleSparks", 2.5f));
        GameObject twin = Projectile("Hangar/TwinBolt", "ProjectileParent", twinMuzzle, cannonBolt, cannonImpact);
        GameObject plasma = Projectile("Hangar/PlasmaBolt", "ProjectileParent",
            Fx("Hangar/PlasmaMuzzle", "Muzzleflash/Sci-Fi/Plasma/PlasmaMuzzleRed", Scale(0.4f)),
            Fx("Hangar/PlasmaBolt", "Missiles/Sci-Fi/Plasma/PlasmaMissileRed", Scale(0.35f)),
            Fx("Hangar/PlasmaImpact", "Explosions/Sci-Fi/Plasma/PlasmaExplosionRed", Scale(0.35f)));
        GameObject proton = Projectile("Hangar/ProtonBolt", "ProjectileParent",
            Fx("Hangar/ProtonMuzzle", "Muzzleflash/Sci-Fi/Proton/ProtonMuzzleRed", Scale(0.4f)),
            Fx("Hangar/ProtonBolt", "Missiles/Sci-Fi/Proton/ProtonMissileRed", Scale(0.35f)),
            Fx("Hangar/ProtonImpact", "Explosions/Magic/Lightning/LightningExplosionRed", Scale(0.35f)));

        // Ordnance: smokeless pod missiles with spark trails, bigger blasts, then falling bombs
        GameObject podFlight = Fx("Hangar/PodMissile", "Missiles/Sci-Fi/Rocket/RocketMissileRed", Remove("SmokeTrail"), Count("SparkTrail", 2f), Scale(0.6f));
        GameObject pods = Projectile("Hangar/PodMissile", "ProjectileParentBomb", launch, podFlight, missileBlast);
        GameObject barrageBlast = Fx("Hangar/BarrageExplosion", "Explosions/Sci-Fi/Rocket/RocketExplosionRed", Smoke(RedEmber), Scale(0.55f), Count("Sparks", 1.6f));
        GameObject barrage = Projectile("Hangar/BarrageMissile", "ProjectileParentBomb", launch, podFlight, barrageBlast);
        GameObject bomb = Projectile("Hangar/Bomb", "ProjectileParentBomb",
            Fx("Hangar/BombDrop", "Muzzleflash/Sci-Fi/Grenade/GrenadeMuzzleRed", Smoke(RedEmber, 0.2f), Scale(0.4f)),
            Fx("Hangar/BombFlight", "Missiles/Sci-Fi/Grenade/GrenadeRed", Smoke(RedEmber, 0.3f), Scale(0.5f)),
            Fx("Hangar/BombExplosion", "Explosions/Sci-Fi/Grenade/GrenadeExplosionRed", Smoke(RedEmber), Scale(0.55f)));

        towerWiring.Add(() => EditTower("Hangar Tower", tower =>
        {
            foreach (HangarTowerActionStrategy strategy in tower.Root.GetComponents<HangarTowerActionStrategy>())
            {
                SetField(strategy, "cannonProjectile", cannon);
                SetField(strategy, "ordnanceProjectile", missile);
            }
            Visual<VisualUpgrade>(tower, 2, 1, (VisualSlot.Cannon, twin));
            Visual<VisualUpgrade>(tower, 2, 2, (VisualSlot.Cannon, plasma));
            Visual<VisualUpgrade>(tower, 2, 3, (VisualSlot.Cannon, proton));
            Visual<VisualUpgrade>(tower, 3, 1, (VisualSlot.Ordnance, pods));
            Visual<VisualUpgrade>(tower, 3, 2, (VisualSlot.Ordnance, barrage));
            Visual<VisualUpgrade>(tower, 3, 3, (VisualSlot.Ordnance, bomb));
        }));
    }

    // ---- Beam Tower ------------------------------------------------------------------------------------

    private static void Beam()
    {
        const string setup = "Assets/Polygon Arsenal/Prefabs/Combat/Beams/Setup/";
        GameObject line = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(setup + "BeamYellow.prefab");
        GameObject start = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(setup + "BeamYellowStart.prefab");
        GameObject end = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(setup + "BeamYellowEnd.prefab");
        GameObject tick = Fx("Beam/TickPulse", "Explosions/Mini/MiniExploHoly", Scale(0.3f));
        GameObject hit = Fx("Beam/HitSparks", "Explosions/Sci-Fi/Laser/LaserExplosionYellow", Scale(0.35f));

        // Length: a brighter end, a sparkling end flare, then a charged orb where the beam ends
        GameObject flare = Fx("Beam/EndFlare", setup + "BeamYellowEnd.prefab", Merge("Aura/SparkleAura/SparkleAuraYellow", 0.4f));
        GameObject orb = Fx("Beam/EndOrb", setup + "BeamYellowEnd.prefab", Merge("Charge/V1/ChargeSphereYellow", 0.35f));
        GameObject pierceGlow = Fx("Beam/PierceGlow", "Explosions/Sci-Fi/Laser/LaserExplosionYellow", Scale(0.45f), Count("Sparks", 2.5f), Speed("Sparks", 1.5f));

        // Tick: stronger pulses, a flickering core, then lightning crackle on every hit
        GameObject strongTick = Fx("Beam/TickPulseStrong", "Explosions/Mini/MiniExploHoly", Scale(0.45f));
        GameObject crackle = Fx("Beam/Crackle", "Explosions/Magic/Lightning/LightningExplosionYellow", Scale(0.3f));

        // Damage: wider, then an orange-white fusion core, then a white-hot storm beam
        GameObject stormStart = Fx("Beam/StormStart", setup + "BeamYellowStart.prefab", Merge("Aura/ChargeAura/AuraChargeYellow", 0.3f));
        GameObject novaTick = Fx("Beam/TickNova", "Nova/EnergyNova/EnergyNovaYellow", Scale(0.25f));

        towerWiring.Add(() => EditTower("Beam Tower", tower =>
        {
            TowerBeam beam = tower.Root.GetComponentInChildren<TowerBeam>(true);
            if (beam == null)
            {
                PolygonBeamStatic old = tower.Root.GetComponentInChildren<PolygonBeamStatic>(true);
                if (old == null)
                {
                    Fail("Beam Tower: neither TowerBeam nor PolygonBeamStatic");
                    return;
                }
                GameObject host = old.gameObject;
                Object.DestroyImmediate(old);
                beam = host.AddComponent<TowerBeam>();
            }
            SetField(beam, "linePrefab", line);
            SetField(beam, "startPrefab", start);
            SetField(beam, "endPrefab", end);
            foreach (BeamTowerActionStrategy strategy in tower.Root.GetComponents<BeamTowerActionStrategy>())
            {
                SetField(strategy, "beam", beam);
                SetField(strategy, "tickEffect", tick);
                SetField(strategy, "hitEffect", hit);
            }

            Visual<BeamVisualUpgrade>(tower, 1, 1).endScale = 1.3f;
            BeamVisualUpgrade length2 = Visual<BeamVisualUpgrade>(tower, 1, 2);
            length2.end = flare;
            length2.endScale = 1.4f;
            BeamVisualUpgrade length3 = Visual<BeamVisualUpgrade>(tower, 1, 3, (VisualSlot.Impact, pierceGlow));
            length3.end = orb;
            length3.endScale = 1.5f;

            Visual<BeamVisualUpgrade>(tower, 2, 1, (VisualSlot.Muzzle, strongTick)).tickSwell = 0.7f;
            BeamVisualUpgrade tick2 = Visual<BeamVisualUpgrade>(tower, 2, 2, (VisualSlot.Muzzle, strongTick));
            tick2.tickSwell = 0.7f;
            tick2.flicker = 0.12f;
            BeamVisualUpgrade tick3 = Visual<BeamVisualUpgrade>(tower, 2, 3, (VisualSlot.Muzzle, strongTick), (VisualSlot.Impact, crackle));
            tick3.tickSwell = 0.8f;
            tick3.flicker = 0.2f;

            Visual<BeamVisualUpgrade>(tower, 3, 1).width = 1.35f;
            BeamVisualUpgrade damage2 = Visual<BeamVisualUpgrade>(tower, 3, 2);
            damage2.width = 1.7f;
            damage2.overrideColor = true;
            damage2.color = TwoKeys(Color.white, new Color(1f, 0.55f, 0.12f));
            BeamVisualUpgrade damage3 = Visual<BeamVisualUpgrade>(tower, 3, 3, (VisualSlot.Muzzle, novaTick));
            damage3.width = 2.3f;
            damage3.overrideColor = true;
            damage3.color = TwoKeys(Color.white, new Color(1f, 0.92f, 0.6f));
            damage3.start = stormStart;
            damage3.endScale = 1.6f;
        }));
    }

    private static Gradient TwoKeys(Color from, Color to)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    // ---- Sniper Tower ----------------------------------------------------------------------------------

    private static void Sniper()
    {
        const string beamLine = "Beams/Setup/BeamYellow";
        const string flash = "Muzzleflash/Sci-Fi/Sniper/SniperMuzzleRed";
        // Tracers are the yellow beam line recoloured: white core, coloured edge
        GameObject red = Fx("Sniper/TracerRed", beamLine, Tint(358f, 1.35f));
        GameObject orange = Fx("Sniper/TracerOrange", beamLine, Tint(24f));
        GameObject whiteHot = Fx("Sniper/TracerWhiteHot", beamLine, Tint(30f, 0.35f));
        GameObject amber = Fx("Sniper/TracerAmber", beamLine, Tint(42f));
        GameObject violet = Fx("Sniper/TracerViolet", beamLine, Tint(278f));

        GameObject muzzle = Fx("Sniper/Muzzle", flash, Smoke(RedEmber, 0.3f), Merge("Muzzleflash/Sci-Fi/Proton/ProtonMuzzleRed", 0.4f));
        GameObject impact = Fx("Sniper/Impact", "Explosions/Sci-Fi/Sniper/SniperExplosionRed", Scale(0.5f));

        // Power: bigger flash and tracer, an orange proton flash, then a shockwave with a white-hot tracer
        GameObject overload = Fx("Sniper/OverloadMuzzle", "Muzzleflash/Sci-Fi/Proton/ProtonMuzzleRed", Scale(0.6f));
        GameObject catastrophic = Fx("Sniper/CatastrophicMuzzle", flash, Smoke(RedEmber, 0.3f), Scale(1.4f), Merge("Nova/EnergyNova/EnergyNovaRed", 0.35f));
        GameObject catastrophicImpact = Fx("Sniper/CatastrophicImpact", "Explosions/Sci-Fi/Proton/ProtonExplosionRed", Scale(0.5f));
        // Magazines: a shell eject spark next to the flash
        GameObject magazine = Fx("Sniper/MagazineMuzzle", flash, Smoke(RedEmber, 0.3f), Merge("Muzzleflash/Sci-Fi/Bullet/BulletMuzzleYellow", 0.5f, new Vector3(0.15f, 0.1f, 0f), new Vector3(0f, 90f, 0f)));

        towerWiring.Add(() => EditTower("Sniper Tower", tower =>
        {
            foreach (SniperTowerActionStrategy strategy in tower.Root.GetComponents<SniperTowerActionStrategy>())
            {
                SetField(strategy, "muzzleEffect", muzzle);
                SetField(strategy, "impactEffect", impact);
                SetField(strategy, "tracer", red);
                SetField(strategy, "secondTracer", red);
                SetField(strategy, "thirdTracer", red);
                SetField(strategy, "tracerWidth", 0.6f);
                SetField(strategy, "tracerDuration", 0.16f);
                SetField(strategy, "muzzleScale", 1.5f);
                SetField(strategy, "impactScale", 1f);
            }

            SniperVisualUpgrade power1 = Visual<SniperVisualUpgrade>(tower, 1, 1);
            power1.muzzleScale = 1.9f;
            power1.tracerWidth = 0.85f;
            SniperVisualUpgrade power2 = Visual<SniperVisualUpgrade>(tower, 1, 2, (VisualSlot.Muzzle, overload));
            power2.tracer = orange;
            power2.tracerWidth = 1f;
            power2.impactScale = 1.3f;
            SniperVisualUpgrade power3 = Visual<SniperVisualUpgrade>(tower, 1, 3, (VisualSlot.Muzzle, catastrophic), (VisualSlot.Impact, catastrophicImpact));
            power3.tracer = whiteHot;
            power3.tracerWidth = 1.5f;
            power3.tracerDuration = 0.2f;

            Visual<SniperVisualUpgrade>(tower, 2, 1, (VisualSlot.Muzzle, magazine));
            Visual<SniperVisualUpgrade>(tower, 2, 2).secondTracer = amber;
            Visual<SniperVisualUpgrade>(tower, 2, 3).thirdTracer = violet;

            Visual<SniperVisualUpgrade>(tower, 3, 1).tracerDuration = 0.11f;
            SniperVisualUpgrade speed2 = Visual<SniperVisualUpgrade>(tower, 3, 2);
            speed2.tracerDuration = 0.09f;
            speed2.muzzleScale = 1.2f;
            SniperVisualUpgrade speed3 = Visual<SniperVisualUpgrade>(tower, 3, 3);
            speed3.tracerDuration = 0.08f;
            speed3.tracerWidth = 0.35f;
            speed3.muzzleScale = 1f;
        }));
    }

    // ---- Mine Factory ----------------------------------------------------------------------------------

    private static void MineFactory()
    {
        const string rocketBlast = "Explosions/Sci-Fi/Rocket/RocketExplosionRed";
        const string grenadeBlast = "Explosions/Sci-Fi/Grenade/GrenadeExplosionRed";
        const string launch = "Muzzleflash/Sci-Fi/Grenade/GrenadeMuzzleRed";

        GameObject blast = Fx("Mine/MineExplosion", rocketBlast, Smoke(RedEmber));
        GameObject heavy = Fx("Mine/HeavyExplosion", grenadeBlast, Smoke(RedEmber));
        GameObject puff = Fx("Mine/LaunchPuff", launch, Smoke(RedEmber));

        // Warheads: a focused core flash, grenade-like bomblet blasts, then a heavy blast with a shockwave
        GameObject shaped = Fx("Mine/ShapedExplosion", rocketBlast, Smoke(RedEmber), Merge("Explosions/Sci-Fi/Laser/LaserExplosionRed", 0.8f));
        GameObject clusterBlast = Fx("Mine/ClusterBlast", grenadeBlast, Smoke(RedEmber, 0.2f), Scale(0.9f));
        GameObject devastator = Fx("Mine/DevastatorExplosion", grenadeBlast, Smoke(RedEmber), Merge("Explosions/Sci-Fi/Energy/EnergyExplosionRed", 0.5f));
        // Assembly: a cyan seeker ping at launch, a faster amber puff, then a white launch flash
        GameObject seeker = Fx("Mine/SeekerLaunch", launch, Smoke(RedEmber), Merge("Muzzleflash/Sci-Fi/Laser/LaserMuzzleBlue", 0.8f));
        GameObject overdrive = Fx("Mine/OverdriveLaunch", launch, Smoke(AmberEmber), Count("<root>", 2f), Speed("<root>", 1.3f));
        GameObject minefield = Fx("Mine/MinefieldLaunch", "Muzzleflash/Sci-Fi/Grenade/GrenadeMuzzleWhite", Smoke(RedEmber));

        towerWiring.Add(() => EditTower("Mine Factory", tower =>
        {
            foreach (MineFactoryActionStrategy strategy in tower.Root.GetComponents<MineFactoryActionStrategy>())
            {
                SetField(strategy, "explosionEffect", blast);
                SetField(strategy, "heavyExplosionEffect", heavy);
                SetField(strategy, "launchEffect", puff);
            }
            Visual<VisualUpgrade>(tower, 1, 1, (VisualSlot.MineBlast, shaped));
            Visual<VisualUpgrade>(tower, 1, 2, (VisualSlot.MineClusterBlast, clusterBlast));
            Visual<VisualUpgrade>(tower, 1, 3, (VisualSlot.MineHeavyBlast, devastator));
            Visual<VisualUpgrade>(tower, 2, 1, (VisualSlot.MineLaunch, seeker));
            Visual<VisualUpgrade>(tower, 2, 2, (VisualSlot.MineLaunch, overdrive));
            Visual<VisualUpgrade>(tower, 2, 3, (VisualSlot.MineLaunch, minefield));
        }));
    }
}
