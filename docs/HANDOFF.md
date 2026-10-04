# Handoff: performance overhaul (branch `perf/overhaul`)

Started 2026-10-04. The plan is in `~/.claude/plans/analyze-the-codebase-and-dazzling-waterfall.md`.

## Goal
- No tower in any upgrade configuration lags the game for 100+ rounds.
- Mass layer pops don't lag either.
- Combat results are the same at 1x, 3x and 5x. 10x is out of scope.
- Stretch goal: Beginner Level 01 lined with blocks, each topped by a fully upgraded Bullet Dispenser, stays smooth to round 100 at 1x and at 5x.
- Restrictions: all animations still play, nothing is culled or despawned, and stats and prefabs stay editable.

## Benchmark
`Assets/Scripts/Diagnostics/PerfScenario.cs` runs inside the game, so the Editor and a development player build use the same code. `Assets/Editor/Perf/PerfBenchmark.cs` is the batch entry point:

```
Unity -batchmode -projectPath 3DTD/3DTD -executeMethod PerfBenchmark.Run -perfSuite core -logFile <log>
```

Run it with graphics, not `-nographics`. The suite is selected with `-perfSuite`:
- `core`: S1-1x, S1-5x, S2 and S3.
- `parity`: the T-<tower>-1x/3x/5x runs, 24 in total.
- `full`: everything, including S1 at rounds 50 and 75 and the P group.
- A comma-separated list of run ids picks single runs.

`-perfSeed n` reruns the same runs with another random seed.

Results go to `tasks/perf/perf-<suite>-<time>.json`, a per-frame `.csv` and one screenshot per run (`perf-<id>.png`). The log has `PERF RESULT|PASS|FAIL|PARITY` lines.

The scenarios are on Beginner Level 01, Medium, with seeded randomness:

| Run | Towers | Rounds |
|---|---|---|
| S1 | Every tower type in each "one path at tier 3 + another at tier 2" combination (43 towers), spread along the lane | 100, 101, 102 |
| S2 | 150 Bullet Dispensers (path 1 tier 3 + path 2 tier 2), roughly what a Medium game can afford by round 100 | 100, 101, 102 |
| S3 | S2 at 5x | 100, 101, 102 |
| T-<tower>-1x/3x/5x | One tower (path 1 at tier 3 + path 2 at tier 2) alone against an overwhelming wave; its damage measures its effectiveness | 60 |
| P | One of each tower type at tiers 2/2 (8 towers), a weak mixed defense. Informational only: damage and leaks vary about ±9 % between seeds | 60, at 1x/3x/5x |

Blocks are placed on the lane's block grid with the top face just below the enemies; towers on top see them inside their half-sphere range.

Checks:
- Frame p99 ≤ 16.7 ms and max ≤ 33 ms.
- At most 1 KB of garbage per steady frame in the Editor (TMP copies every changed text into a string in the Editor) and 0 B in builds.
- At 5x, the game time ratio is ≥ 98 % of 5.
- No dropped projectiles, death animations or effects (`PerfCounters`).
- Parity: each T run's damage within 3 % of its 1x run.

Measured noise on the old physics-based projectiles (T runs at 1x, three seeds):
- Laser: 467–469 damage, deterministic.
- Rocket: ±9 %. Its aim uses `UnityEngine.Random`, which the enemies' cosmetic spin also draws from once per frame.
- Bullet Dispenser: ±15 %. Hits depend on when physics steps fall relative to frames.

Phase 3 removes both sources.

## Baseline (code before the overhaul, Editor batch mode, M4 Pro)

| Run | Frame p50 / p99 / max (ms) | Game speed | GC per frame | Physics (ms/frame) | Instantiate (ms/frame) | Notes |
|---|---|---|---|---|---|---|
| S1-1x (3 waves) | 93 / 398 / 569 | 0.66x | 563 KB | 48 | 49 | 2,455 of 64,431 death animations played |
| S1-5x (3 waves) | 307 / 1,496 / 1,496 | 1.38x | 3.6 MB | 214 | 195 | no death animations at all |
| S2 (150 dispensers) | 591 / 38,554 / 38,554 | 0.01x | n/a | 4,685 | 5,268 | frozen, the wave never ended (240 s limit) |
| S3 | 2,821 / 59,723 / 59,723 | 0.04x | n/a | 3,561 | 10,139 | frozen |
| P-1x / 3x / 5x | 8.7 / 34.8 / 137 at 1x | 1.00 / 3.00 / 4.99 | 100 % of frames allocate | 1.1 / 2.8 / 4.4 | | parity fails |

Parity at baseline:
- 5x deals 8.8 % more damage than 1x and leaks 26 % fewer lives; Rocket damage is +20 %.
- 3x deals 4.3 % more damage and leaks 11.6 % fewer lives.

Other findings from the baseline:
- 372–467 real-time particle lights are visible at once (URP supports 256 and logs a warning every frame).
- Icosahedron Black enemies are invisible: `allPossibleShapes[49]` and `[50]` point at the same child, and `SetLayer` switched it off again.

## Phase 1 results (bugs and cheap structural fixes)

| Run | Frame p50 / p99 / max (ms) | Game speed | GC per frame | Physics (ms/frame) | Instantiate (ms/frame) |
|---|---|---|---|---|---|
| S1-1x | 34 / 142 / 313 (was 93 / 398 / 569) | 0.96x (was 0.66x) | 195 KB (was 563 KB) | 12.5 (was 48) | (dominated by projectile effects) |
| S1-5x | 213 / 671 / 671 (was 307 / 1,496 / 1,496) | 1.84x (was 1.38x) | 2.3 MB | 145 (was 214) | |

What changed:
- **Enemy shapes:** `Enemy.SetLayer` toggles only the outgoing and the incoming shape child, by object. This also fixes the invisible Icosahedron Black without a prefab change.
- **Spawner:** O(1) alive list, and the enemy pool is prewarmed a few per frame ahead of the next wave, under an inactive container.
- **Targetter:** keeps entries with spawn serials, prunes them lazily and has an O(1) `Contains`. There are no per-enemy `OnDeath` subscriptions any more.
- **Money:** pop income raises `OnMoneyChanged` once per frame. Build tiles and price labels only restyle when affordability flips, and the scrap counter is built without allocating.
- **Outlines:** the `Outlinable` component itself is toggled, so nothing is drawn while nothing is hovered or selected.
- **Projectiles:** each one returns itself to its pool after the fade (no coroutines, no delegates per shot), and the pool's shot-dropping cap is gone.
- **Leaks and allocations:** static event unsubscription in `Selectable` and `SelectionManager`, the `Debug.Log`s are gone, and `UIPointer` reuses its event data. `End` uses `CompareTag`, and the Hangar's range check no longer uses LINQ.
- **GC:** incremental GC is on.

Checks: UIPlaytest 34/34, LevelPlaytest 35/35, EditMode 45/45. The new tests check that every enemy id shows exactly its own shape, and that the allocation-free counter matches `UIFormat.Tabular`.

Known flakiness: the Burst compiler crashed once (SIGBUS, inside package code) while a batch run was starting. A rerun passed.

## Phase 2 results (physics configuration)

- **Enemy root:** on layer 11 "Enemy", with a kinematic Rigidbody. Enemies no longer generate contacts with each other, with blocks or with towers; their trigger events with tower ranges and the End trigger still fire.
- **Collision matrix:** Enemy only collides with Default (the End trigger) and Ignore Raycast. Ignore Raycast (tower ranges, the collider-based projectiles) only collides with Enemy, so projectiles no longer hit each other, tower ranges or anchors.
- **Physics:** `SimulationMode.Update`, one step per frame at any game speed (it used to be 4–5 steps per frame at 5x). OnTriggerStay events are off; nothing listens for them.
- **Beam raycasts:** masked to enemies (`GameLayers.EnemyMask`). Tower ranges and blocks used to fill the 64-hit buffer.
- **Pulse/Hindrance:** grow their collider in Update.

Results:
- **Frame times:** S1-1x 36 / 152 / 211 ms (p50 / p99 / max); S1-5x 167 ms p50 at 1.91x. `FixedUpdate.PhysicsFixedUpdate` dropped to 0.
- **Remaining cost:** per-shot effect instantiation, projectiles and death effects (Phases 3–5).
- **Checks:** LevelPlaytest 35/35 (enemies reach the exit; the featured towers deal damage) and UIPlaytest 34/34.

Parity:
- Hit detection is discrete per frame. In batch mode 1x runs at about 300 fps, so detection there is much finer than at 5x.
- Rockets: 14.3k damage at 1x vs 20.5k at 5x. The coarser the step, the deeper a rocket gets into the pack before it explodes.
- Dispenser: 132 vs 282.
- Phase 3's swept hit tests remove this. Before Phase 2, 5x was already "once per frame", because projectiles only moved once per frame.

## Regression found and fixed: half-size enemies (commit 72609b3)
- **Cause:** Phase 1's enemy prewarm parented its container to the Spawner object. That object (the `SpawnerNew` root) is scaled 0.5, and `SetParent(null, true)` carried the scale over, so prewarmed enemies spawned at world scale 0.25 instead of 0.5: half size, in looks and in collision.
- **Effect:** the Phase 1 and Phase 2 numbers above were measured with mostly half-size enemies.
- **Fix:** the container is now a scene root and spawning keeps the prefab's local scale. The benchmark checks every spawned enemy's scale (`wrongEnemyScale`).

## Phase 3 results (projectile system and parity)

What changed:
- **`ProjectileSystem`** (`Scripts/Combat/`) simulates Round, Basic, Bomb and Cluster projectiles as data.
  - Each frame a Burst job puts the enemies into a grid, and a parallel job moves the projectiles and sweeps their capsules against the enemies' own movement.
  - Rockets home in 1/120 s sub-steps.
  - Hits are applied on the main thread in time order.
- **Prefabs stay the source:** `ProjectileArchetype` reads the projectile prefabs once (behaviour component, collider as hit shape, `PolygonProjectileScript` effect fields).
- **`EnemyRegistry`:** enemies register on spawn. Blasts (rockets, bomblets, Mine Factory) query it instead of the unmasked 128-collider `OverlapSphere`.
- **`FireCycle.VolleyAge`:** projectiles start as far along as the time since their shot was due. Starfighter cannons do the same.
- **`Enemy.Move`:** leftover movement at a waypoint carries on to the next one.
- **`Tower.Rng`:** each tower has its own seeded random stream for spread and mine spots.
- **`EffectPlayer`:** effects play from pooled copies (no Instantiate/Destroy per shot, no leaking flight children).
  - Particle lights share a budget of 64.
  - Effect renderers cast no shadows.
  - The flamethrower's sparks play for every enemy at every speed; their sound is limited to 4 voices per clip.
- **Unchanged:** Pulse projectiles stay pooled GameObjects.

Parity (T runs, damage at 1x / 3x / 5x):

| Tower | 1x | 3x | 5x |
|---|---|---|---|
| Laser | 480 | 480 | 481 |
| Core | 7 | 7 | 7 |
| Rocket | 8,161 | 8,165 | 8,165 |
| Sniper | 1,134 | 1,134 | 1,134 |
| Bullet Dispenser | 139 | 140 | 139 |
| Mine Factory | 14,934 | 14,979 | 15,030 |
| Hangar | 567 | 687 | 563 |
| Beam | 0 | 0 | 0 |

- **Hangar:** still off. Starfighters integrate their flight with the frame's time step.
- **Beam:** the harness aims it parallel to the lane but one block width to the side, so the run is uninformative.

Frame times (Editor):

| Run | p50 / p99 / max (ms) | Game speed |
|---|---|---|
| S1-1x | 10.9 / 16.4 / 44.9 | 1.00x |
| S1-5x | 13.8 / 28.7 / 34.4 | 5.00x |
| S2 | 147 | 0.51x |
| S3 | 507 | 0.61x |

S2 and S3 are dominated by pooled effect copies (thousands of muzzle, impact and flight effects), which is Phase 4.

Checks: EditMode 45/45, LevelPlaytest 35/35, UIPlaytest pass.

## Phase 4 results (effect batching)
- **`BatchedEffect`** (one-shot effects): one shared, world-space copy per prefab, with its emission off.
  - A play queues the prefab's bursts and rates.
  - Once per frame each system emits everything due in one `Emit`, and the fresh particles are moved to their play's pose, scale and rotation.
  - Systems with particle trails emit at each play's pose instead. Otherwise the trail would start at the shared copy's origin, which showed up as long white streaks.
- **`FlightBatch`** (flight effects):
  - Per-flight emission: rate over time, rate over distance, looping bursts, and prewarm.
  - A Burst particle job moves the particles of local-space systems with their projectile, shrinks them with its fade and removes them when it is gone.
  - Systems named "Trail" play out.
  - The `TrailRenderer` becomes a shared particle trail behind one invisible head particle per flight.
- **Fallback:** prefabs these can't reproduce (sub-emitters, local-space forces, other scripts, lights or meshes) keep pooled copies. Today that is the rocket muzzle and the rocket explosion.
- **Light budget:** particle lights are capped at 64 per batched system.
- **Comparing the looks:** run `-perfSuite V` with and without `-perfNoBatching 1`. It uses fixed 1/60 s frames and seeded gameplay, and writes `tasks/perf/visual-{batched,pooled}-0..3.png`, close-ups of the same tower at the same moments.
  - The bullets, muzzle starbursts, impacts and sparks match.
  - The pooled run lights the tower top a little more.

| Run | p50 / p99 / max (ms) | Game speed |
|---|---|---|
| S1-1x | 10.0 / 16.7 / 41.7 | 1.00x |
| S1-5x | 10.8 / 24.5 / 44.5 | 5.00x |
| S2 (150 dispensers) | 19.9 / 31.0 / 77.2 | 1.00x |
| S3 (S2 at 5x) | 21.2 / 113 / 121 | 4.95x |

In S2, 384k projectiles and 582k effects were requested and none was dropped. About 12 ms of each S2/S3 frame is the harness's own `camera.Render()` of the particle load in the Editor; a player build is needed for real numbers.

## Phase 5 results (pop animations)
- **`DeathEffectRenderer`** draws every running pop with `Graphics.RenderMeshInstanced`, one call per shape+colour and dissolve step (16 steps). There is no cap and no speed limit any more (it used to be 64 at once, and none above 2x).
  - The scale curve is sampled from `ExpandingShape.anim`.
  - Meshes and materials come from the enemy prefab's shapes.
  - 40 shapes use the `Layer*Animation` dissolve materials. 10 use plain `Layer*` Lit materials and, like before, only grow.
  - Pops cast no shadows and use the scene's ambient probe. `BlendProbes` tinted the whole frame blue for instanced draws.
- **Materials:** instancing is enabled on all 20 `Materials/Enemies/Layer*.mat`, so the variants are in builds.
- **`EnemyShape`** only forwards to the renderer.

| Run | p50 / p99 / max (ms) | Pop animations played | GC per steady frame |
|---|---|---|---|
| S1-1x | 10.7 / 18.0 / 764 | 64,431 of 64,431 | 2.3 KB |
| S1-5x | 12.6 / 24.4 / 32.2 | 64,431 of 64,431 | 6.7 KB |
| S2 | 18.6 / 31.2 / 63.3 | 44,151 of 44,151 | 3.0 KB |
| S3 | 21.5 / 112.5 / 122.7 at 4.95x | 45,722 of 45,722 | 15.0 KB |

The 764 ms frame at S1-1x is a single hitch, probably the Editor compiling the instancing shader variant on first use; the player build will tell.

## Hangar parity
Starfighters now fly in fixed 1/60 s steps of game time and are drawn between the last two simulated poses. Their decisions use their own seeded random stream, and cannon shots get their age within the step.

A squadron's dogfight is still chaotic: fighters see enemies and the tower's range list as they are at the end of each frame. Damage of one hangar against round 60:

| Speed | Seed 1234 | Seed 11 | Seed 22 | Seed 33 | Mean |
|---|---|---|---|---|---|
| 1x | 668 | 624 | 596 | 657 | 636 |
| 5x | 651 | 578 | 573 | 651 | 613 |

The spread between seeds (about ±6 %) is as large as the difference between speeds (3.6 % between the means, about 1.5 standard errors). The `T-Hangar` parity runs are therefore informational.

## Status
- [x] Phase 0: benchmark harness and baseline.
- [x] Phase 1: bugs and cheap structural fixes.
- [x] Phase 2: physics configuration.
- [x] Phase 3: projectile system and parity (the Hangar matches only statistically, see above).
- [x] Phase 4: effect batching.
- [x] Phase 5: death animations (instanced).
- [ ] Phase 6: wrap-up.

## Needs user verification
- How the batched effects look, and how 5x feels with a full dispenser field (after Phase 4/5).
