---
paths:
  - "3DTD/Assets/Scripts/Effects/**"
  - "3DTD/Assets/Scripts/Combat/**"
  - "3DTD/Assets/Editor/Effects/**"
  - "3DTD/Assets/Prefabs/Effects/**"
  - "3DTD/Assets/Prefabs/Projectiles/**"
  - "3DTD/Assets/Scripts/Tower/ActionStrategies/**"
---

# Effects and projectile looks (paths relative to the Unity project `3DTD/`)

## Effect tooling
- Effect tooling: `ProjectileVisualsBuilder.Run` (`Assets/Editor/Effects/`, batch mode, also menu 3DTD > Effects > Rebuild Projectile Visuals) generates every tower's projectile and effect looks (see Projectile looks below). Re-running it overwrites the generated prefabs and the visual upgrades on the tower prefabs; edit the recipes in `ProjectileVisualsBuilder.Recipes.cs` instead. Effects made with `Merge` get new local file IDs on every run (same GUIDs, just diff noise). Check the result with `-perfSuite fx`.

## Effect backends (`Scripts/Effects/`)
Effects are never instantiated per shot or hit. Play one-shots with `EffectPlayer.Play(prefab, position, rotation, scale, maxLifetime, age)`, and attach flight effects with `EffectPlayer.Attach(...)`, which returns a `FlightHandle` (`SetPose`, `Release`). `EffectPool` picks the backend per prefab:
- **`BatchedEffect`** (one-shots): one shared world-space copy per prefab. Each frame every system emits everything due in one `Emit`, and a Burst job moves the fresh particles to their play's pose, scale and age. Systems with particle trails emit per play at its pose instead.
- **`FlightBatch`** (flight effects): all flights of a prefab in shared copies, sharded over about a third of the cores (`EffectPool.MaxFlightShards`). Unity updates each particle system and builds its geometry in one job, so a single copy would leave the render thread waiting on one core. Burst jobs compute each flight's emission and place the particles. Local-space systems follow their flight through an `IJobParticleSystemParallelForBatch` (`FlightFollower`). Systems named "Trail" stay behind and play out. The `TrailRenderer` is drawn by `TrailMesh` (one Burst-built mesh for all flights).
- **Pooled `EffectInstance` copies:** the fallback for prefabs these can't reproduce (sub-emitters, local-space forces, other scripts, Light components, meshes). Today that is the rocket muzzle and explosion.
- **Budgets the user allowed:** effects cast no shadows; a batched system shows at most 32 particle lights (`BatchedEffect.MaxLightsPerSystem`) and pooled copies share 64 (`EffectPlayer.LightBudget`); one sound clip plays at most 4 voices at once (`EffectAudio`). Everything else plays at every speed: don't add caps, skips or culls for performance.
- **`DeathEffectRenderer`:** draws every layer pop (`EnemyShape.SpawnDeathEffect`) with `Graphics.RenderMeshInstanced`, in 16 dissolve steps, with the growth sampled from `ExpandingShape.anim`. The enemy `Layer*` materials therefore keep GPU instancing on.
- **`TracerRenderer`:** hitscan tracer lines (Sniper): pooled copies of a LineRenderer prefab that thin out in game time. **`TowerBeam`** draws the Beam Tower's beam (it replaced `PolygonBeamStatic` there): line, start and end effects, width from its stage, a swell on every tick.
- `ProjectileSystem`, `EffectPlayer`, `DeathEffectRenderer` and `TracerRenderer` create themselves on demand, in play mode only.
- The batched backends scale plays by the prefab's own root scale, like the pooled copies, so a scaled copy of an effect looks the same on every backend.

## Projectile looks (`ProjectileVisualsBuilder`)
- Generated prefabs: effect copies in `Prefabs/Effects/<Tower>/` (recoloured, rescaled, stretched, merged Polygon Arsenal effects) and projectile copies in `Prefabs/Projectiles/<Tower>/` (the `Prefabs/ProjectileParent*` wrappers with other effects; colliders untouched, so hit shapes and balance stay the same).
- Base looks: Laser keeps `LaserBlue`; Core is a paler sky blue; Rocket System green; Bullet Dispenser small dark green needles with a neon green outline (a wider copy of the needle sorted behind it, `Outline`), neon rebound sparks and trails (ricochet path) and green black-hole implosions (gravity path); Hangar small red bolts and mini missiles; Beam yellow; Sniper red tracers; Mine Factory red explosions.
- Smoke: the Polygon explosions' opaque lit smoke puffs (`PolyLitSurface`) become a few small `PolySolidGlow` wisps in an ember colour that fade within about half a second. Every generated effect gets that treatment (grey if its recipe didn't pick a colour).
- **Visual upgrades:** each upgrade module gets a `VisualUpgrade` (or `BeamVisualUpgrade` / `SniperVisualUpgrade`) as its first upgrade, keyed by its `Comment` ("fx p2t3"), with priority tier * 10 + path. It registers on `Tower.Visuals` (`TowerVisuals`), and strategies look up `VisualSlot`s through a cached `VisualRef` (only when the set changes). Projectile towers combine the whole-prefab slot with the part slots `Muzzle`/`Flight`/`Impact` (passed in `ProjectileSystem.Shot`), so each path styles its own part and paths stay visible together; the Hangar swaps `Cannon`/`Ordnance` prefabs, the Mine Factory its blast and launch slots; Beam and Sniper layer settings (width, colours, tracers) in priority order. Visual upgrades never change stats; `extract.py` skips them and `UpgradeModule.HasBehaviourUpgrades` ignores them.
