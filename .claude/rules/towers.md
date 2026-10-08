---
paths:
  - "3DTD/Assets/Scripts/Tower/**"
  - "3DTD/Assets/Scripts/Entities/**"
  - "3DTD/Assets/Prefabs/Tower/**"
  - "3DTD/Assets/Prefabs/Starfighter.prefab"
  - "3DTD/Assets/Prefabs/Mine.prefab"
---

# Hangar and Mine Factory towers (paths relative to the Unity project `3DTD/`)

- **Hangar Tower** is the one tower whose damage doesn't come from the tower itself. `HangarTowerActionStrategy` launches one `Starfighter` (`Scripts/Entities/Starfighter.cs`, `Prefabs/Starfighter.prefab`) per AMOUNT.
  - Fighters patrol a sphere of radius RANGE (its targetter is a full sphere, not `HalfSphere`) and fly strafing runs above the path, using `Enemy.PathDirection`.
  - Fighters simulate in fixed 1/60 s steps of game time (drawn between the last two poses) with their own seeded random stream. The fighter is in charge of flying and aiming; the strategy turns tower stats into projectiles (`FireCannon`, `FireOrdnance`). Their damage only matches across game speeds statistically (dogfights are chaotic).
  - Upgrades change public loadout fields on the strategy through `StarfighterEngineUpgrade` and `StarfighterLoadoutUpgrade`.
- **Mine Factory** (`MineFactoryActionStrategy`, `Scripts/Entities/Mine.cs`, `Prefabs/Mine.prefab`) is a floating factory that produces mines and lobs them onto the enemy path. It is the AoE "last line of defense" and is meant to be too slow to win alone.
  - Stats: FIRERATE is the seconds per production cycle (it only produces while a wave runs), AMOUNT the mines per cycle, AMMO the field capacity, DAMAGE/RADIUS the blast, PIERCING the detonations per mine, RANGE the placement sphere (full sphere targetter, like the Hangar).
  - Mines are placed on `Spawner.GetLanePath` segments inside RANGE (best spread of a few random candidates, nudged by the tower's TargetBehaviour) and go off when an enemy's movement this frame passes within the contact radius. Without any path in range they hover above the factory and chase enemies in range. The strategy pools the mines and destroys them when the factory is sold.
  - Upgrades set public fields on the strategy: `MineWarheadUpgrade` (cluster bomblets, slowing blasts, seeking, heavy explosion effect) and `MineSalvageUpgrade` (when the field is full, new mines are scrapped for money through `AddIncome`; every n-th one restores a life, capped per wave and at the starting lives; War Bonds pays per mine left at wave end).
  - The prefab is built from Synty props (PolygonSciFiSpace) on a copy of the Hangar's component layout; the hover body, catapult plate, side gears and loaded-mine visual are animated by the strategy. Polygon Arsenal effects are human-sized, so the strategy scales them (`explosionScalePerRadius`, `smallEffectScale`).
