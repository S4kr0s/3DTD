# Handoff: Bullet Dispenser rework (branch `feature/bullet-dispenser-rework`)

Started 2026-10-09, stacked on `fix/review-s4`. The user asked for:
- needles with a bright neon green outline around the dark green core;
- two new paths about 3D space instead of the flamethrower and pulse paths;
- a barrel path that grows a ball of barrels (not pointing down at the base);
- a thicker, tower-like body in one unit cell. Third-party assets may be used, but only as copies.

## Done
- **Body:** `Editor/Towers/BulletDispenserBuilder.cs` generates the prefab (menu 3DTD > Towers > Rebuild Bullet Dispenser, batch `-executeMethod BulletDispenserBuilder.Run`).
  - Parts: a Synty turret plate, the Synty pipe pillar as the column, a gunmetal seat, a faceted core (PolygonPrototype soccer ball) with Synty gatling barrels and neon muzzle rings.
  - The builder references meshes only. The materials and a generated torus ring mesh live in `Prefabs/Tower/BulletDispenser/`; no third-party asset was edited.
  - The builder rebuilds the barrels and upgrade paths, then runs `ProjectileVisualsBuilder`.
- **Path 1, Barrel Sphere** (130 / 220 / 850): Barrel Ring 6 barrels in the face plane, Barrel Crown 12 (+30°/−20°), Needle Sphere 24 (+60°, +0.5 range, +1 pierce).
- **Path 2, Ricochet** (90 / 380 / 1300, `DispenserRicochetUpgrade`):
  - Needles rebound off a dome of 1.25 × the range and off the face plane: 2 / 4 / 6 times.
  - Kinetic Rebound adds +1 damage per bounce. Trick Shot Matrix aims every rebound at the shot's target (else the nearest enemy).
  - `ProjectileSystem`: `Shot.Dome`, `DomeExit` in the Burst step, `Bounce` on the main thread. The rest of the frame carries over as `Lead`, and a rebound may hit the same enemy again.
- **Path 3, Gravity Well** (120 / 300 / 1000, `DispenserGravityUpgrade`):
  - `Enemy.Pull` drags enemies in range up to 0.5 / 1.2 units off their path towards the barrel ball; they are slowed 15 / 30 %.
  - Singularity: every 5 s with enemies in range, it holds all of them 0.45 from the ball for 1.5 s.
  - Bosses don't move. Gyroscope rings show the tier and spin faster during a collapse.
- **Looks:** the needle's neon outline is a wider copy of the needle sorted behind the dark core (`Outline` edit). The ricochet path gets neon rebound sparks and trails, the gravity path imploding impacts and a green black hole for the singularity. The flamethrower and pulse effects and their projectile copy are deleted; `VisualSlot.PulseProjectile`/`AuraHit` are retired, and `Bounce`/`Singularity` are appended.
- **Balance tools:**
  - `extract.py` reads the new upgrades and the well point. `engine.js` traces every barrel's ricochet legs through the dome for the tables and replays bounces and pulls in the simulator (`fireNeedle`, `gravityTick`).
  - The gravity factors are fitted to the simulator. The dead aura and pulse models are removed, and the Methodology tab and README are updated.
- **Tests:** `Editor/Tests/BulletDispenserTests.cs` (dome maths, a 6-bounce walk, 3/6/12/24 barrels, no barrel below −25°, the tower inside its unit cell, the paths). `ProjectileVisualsTests` now expects a look on every dispenser module.

## Results
- EditMode: 66/66.
- Play-mode smoke test, batch, Beginner 01; a scratch script, since deleted. Each build was aimed with the slider like a player would and played one wave. 0 errors.
  - Ricochet: 763 rebounds in one wave at tier 3.
  - Gravity tier 1: holds enemies exactly 0.5 off the path.
  - Singularity: collapsed 3 times and dragged enemies up to 3.1 units.
- Simulator vs base tower, damage per second over 40 rounds, three levels:
  - Ricochet T1/T2/T3: ×0.8–2.1, ×1.9–6.1, ×5.8–9.7.
  - Gravity T1/T2/T3: ×1.1–1.2, ×1.2–1.5, ×1.4–1.5.
- Build efficiency (Beginner 01): 7/33 builds within the band (before: 17/33).
  - Barrels × Kinetic/Trick Shot are above budget (up to 1.9).
  - Gravity is far below (0.08–0.57), because its slow helps other towers, which the value metric doesn't count. The meta agent therefore rarely buys it.
- `acceptance.py` (Medium; singles / pairs / triples won; full roster over three seeds) against the sprint 4 baseline:

| Level | Sprint 4 | After the rework |
|---|---|---|
| Beginner 01 | 1/8, 8/28, 21/56; L L W | 1/8, 8/28, 20/56; W L W |
| Beginner 02 | 1/8, 7/28, 21/56; W W W | unchanged |
| Beginner 03 | 2/8, 12/28, 30/56; W W W | 2/8, 12/28, 28/56; W W W |
| Beginner 04 | 2/8, 10/28, 22/56; W W W | 2/8, 10/28, 22/56; W W L (R48) |
| Beginner 05 | 0/8, 5/28, 20/56; L L L | unchanged |
| Intermediate 01 | 1/8, 8/28, 20/56; W W W | 1/8, 9/28, 22/56; W W W |
| Intermediate 02 | 1/8, 5/28, 10/56; L L L | 1/8, 5/28, 7/56; L L L |

  The Bullet Dispenser alone loses everywhere (R5–R20) and shows up in winning combinations on every level (3–11).

## Round 2 (user feedback, same day)
- **Ball lower:** the head sits 0.46 above the face (was 0.7), on a shorter column, so the base and ring barrels fire at the height of enemies passing blocks beside the track (levels: typically 0.25–0.5 above such faces). Base RANGE 3 → 4.
- **Rebounds chain:** every rebound gives the needle at least its LIFETIME again (`ProjectileSystem.Bounce`). Before, most needles expired before their second bounce. In play: 1.5 bounces per needle at Rebound Rounds (max 2), 3.1 at Kinetic Rebound (max 4). Kinetic Rebound and Trick Shot lost their lifetime bonus; the bounce damage is +0.5 (was +1: Needle Sphere + Kinetic dealt 14–65 × the base tower's damage in the simulator).
- **Turning needles:** `FlightBatch`'s follower job now turns a flight's local-space particles with it (positions, velocities, 3D rotations), so mesh needles point along their new direction after a rebound (seeking ones flew sideways).
- **Gravity path:** Graviton Field (220) only slows 20 %; the pull starts at Event Horizon (600, pull 1.2, 30 %), Singularity 1700.
- **Ricochet dome shimmer:** while a tower with the ricochet path is selected, an additive neon dome (rim, grid, a band sweeping up; `RicochetDome.shader`) fades in at the rebound radius.
- **Prices:** barrels 130 / 220 / 1000, ricochet 90 / 450 / 1300, gravity 220 / 600 / 1700. `engine.js` counts reflected legs at half and seeking rebounds' damage at 40 % (fitted to the simulator).
- **Results:** EditMode 66/66. Play smoke test 0 exceptions (Graviton Field max pull 0, Event Horizon 1.2). Acceptance stays at the baseline (B01 1/8, 8/28, 21/56; B03 2/8, 11/28, 28/56; B04 2/8, 10/28, 22/56, roster W W W; B05 0/8, 5/28, 18/56; I01 1/8, 8/28, 21/56; I02 1/8, 5/28, 7/56). Build efficiency 6/33 in band: barrels × Kinetic/Trick Shot are still above budget (simulator ~1–2.5 ×), gravity far below (support value not modelled).

## Needs user verification
- The look in a real scene with bloom: neon outline strength, ring brightness, the dark core ball, how the barrel ball reads at game zoom.
- How the ricochet and the gravity well feel; whether the pull or the singularity is confusing to watch.
- The dome shimmer's brightness with bloom, and the turned needles after rebounds (not visible in headless renders).
- Decisions:
  - Price the gravity path for its support value (today by judgement next to the Beam's slow).
  - Accept or nerf the barrels × Kinetic Rebound / Trick Shot synergy.

## Not run (machine busy; the user asked to skip on-screen runs)
- `-perfSuite core`/`S2`/`S3`: the 150-dispenser benchmark (path 1 tier 3 + path 2 tier 2) now fires 24 barrels with 4 rebounds each, and needles live 1.9 s instead of 0.9 s. Expect more projectiles and rebound sparks than the old baseline.
- `-perfSuite fx`/`V` screenshots of the new looks, and a dispenser 10x parity run (rebounds carry their leftover time as `Lead`).

---

# Handoff: fixes from the 2026-10-08 code review (branches `fix/review-s1` … `fix/review-s4`)

Started 2026-10-09. The plan is in `~/.claude/plans/please-develop-a-plan-glittery-ripple.md`. Four sprints, each on its own branch stacked on the previous one (`fix/review-s1` starts from `fx/projectile-visuals`), one commit per task.

## Sprint 1: gameplay at high game speed (`fix/review-s1`)
Done:
- **Exit skip and second lap:** `Waypoints.Awake` skips transforms the serialized list already holds (and is safe to call twice). An enemy that reaches the last waypoint reports itself through `End.ReportExit`; the leak handler ignores enemies that are no longer alive.
- **Laser and Core lead:** a bolt fired `age` seconds ago leads from where the enemy was back then.
- **Mines:** candidates come from `Spawner.AliveEnemies` (new accessor), rejected with the armed mines' bounding sphere. `Enemy.Tick` records `TickStartPosition` and the waypoint corner it passed (`TickCorner`/`HasTickCorner`); the contact test checks start→corner→now.
- **FireCycle:** a frame that hit `MaxVolleysPerFrame` carries up to another capped frame of debt; after a reload starts the one-interval clamp stays. Mirrored in `engine.js` `tickFireCycle`.
- **Strategy swap:** `ActionStrategy.Cycle` + `FireCycle.CopyStateFrom` (cooldown, reload, magazine clamped to the new capacity), called by `Tower.SetActionStrategy`.
- **Starfighter:** `MaxStepsPerFrame` 60 (0.1 s × 10x); a capped frame drops its backlog.
- **Hit order:** projectile events are sorted by the time of their first hit (packed `long` key, allocation-free `NativeList.Sort`).
- **Duplicate hits:** past the 31-slot tested list, `FindHits` checks the hits found so far.
- **Perf tooling:** results are paired with their run (visual runs used to shift the pairing). The `parity` suite adds 10x per tower and `L-1x`/`L-10x` (Beginner 01 undefended, leaked lives must match).
- **Tests:** `Editor/Tests/GameplayTimingTests.cs` (FireCycle debt, reload clamp, volley ages, `CopyStateFrom`, age-compensated lead, `Waypoints.Awake`).

Results:
- EditMode 55/55.
- `-perfSuite parity` (Editor): leak parity exact (1082 vs 1082 lives at 1x and 10x). Damage at 3x/5x/10x vs 1x: Laser −0.2 %, Core 0, Rocket ≤ +0.4 %, Sniper 0, Mine Factory ≤ +1.3 %, Bullet Dispenser +0.8 % (3x/5x) and **+3.0 % at 10x (143 vs 139, just over the 3 % limit)**, Hangar +7…+29 % (informational, as before), Beam 0 (benchmark aim, known).
- `acceptance.py` passes after the `tickFireCycle` change.
- Not run: `LevelPlaytest.Run` (machine tests were stopped because of other load on the machine).

Open:
- **Bullet Dispenser at 10x:** +3.0 % on one seed. A rerun with `-perfSeed 2` was planned but skipped. A likely cause: ProjectileSystem sweeps against a straight line between an enemy's previous and current position, but at 10x an enemy can turn a waypoint corner inside one frame (0.16 s of game time). `Enemy.TickCorner` now records that corner, so the sweep could use it too.

## Sprint 2: game state, saves, input and UI (`fix/review-s2`)
Done:
- **Placement clicks:** the main camera's `eventMask` leaves out the Enemy layer (`OrbitCamera.Awake`).
- **Auto-wave:** `GameStatDisplay.HandleWaveEnded` sets a flag; `Update` starts the wave when the Spawner is idle and the game isn't over.
- **Autosave:** `GameManager` captures the save in `LateUpdate`, after every `OnWaveEnded` handler (War Bonds included).
- **Continue after "Reset progress":** `SaveGame.Deleted` event; `MainMenuScreen` hides Continue.
- **Aim slider:** `Tower.AimAngle` (−1 until aimed); the panel starts the slider from it; `SavedBuilding.aimAngle` (−1 in old saves, which keep the quaternion restore).
- **Pause menu:** closing it in Victory mode returns it to Paused.
- **Sell hotkey:** Delete or Backspace, not while paused, behind the pause menu or after game over (CLAUDE.md updated).
- **Placement ring:** includes the meta RANGE bonus (`MetaUpgrades.PercentFor`).
- **Options:** quality −1 means the project's level, captured once before options are applied, so Reset restores it; `GameOptions.EffectiveQuality` for the Options row and the change count. HUD scales are 0.9/1/1.1 (the 110 % label was 1.15).
- **Camera:** the focus follows with unscaled time.
- **Level select** opens on the furthest unlocked level's category once per session. **Continue and pause subtitles** use `LevelCatalog.FullName` ("Beginner · Level 01 · …"). **DifficultyPopup** reuses a corner buffer. **BevelGraphic** skips the rim quads when inner and outer shapes differ in point count.
- **Spawner.Start** no longer resets the game state. Every Spawner prefab serializes IDLE; the main-menu backdrop wave now runs as PROGRESSING. It spawns 10,000 enemies one second apart (`WaveData/MainMenuLevel`), so `OnWaveEnded` only fires after about 10,000 s of game time. A 280 s run at 10x confirmed the state and the stream, but not the end.
- **Tests:** aimAngle round trip and old-save default, `Defaults()` quality, scale labels, the new Summary text.

Results:
- EditMode 58/58, UIPlaytest 34/34.
- UICapture screenshots are clean (no magenta, all TMP text visible; full-name subtitles show). Its log has the known NullReferenceException from the legacy "Minigun Tower" shooting points (see the Unity 6 upgrade notes).

## Sprint 3: effects (`fix/review-s3`)
Done (the perf parity pairing fix moved to sprint 1):
- **Paused frames:** `FlightBatch.Update` with deltaTime 0 zeroes the follower deltas and scale ratios and still draws the trails.
- **Glow lag:** fresh flights get `Delta = Position − Previous` (their distance stays 0).
- **Trail length:** `TrailMesh.Writer.Add` compares with the last fixed point (head − 1) and fixes a point only after `minVertexDistance` and `trailTime / (PointsPerTrail − 2)`, so the 8-point ring always spans the authored trail time.
- **Trail redraw:** built only up to the highest live or fading id (`FlightBatch.TrailTop`), skipped at 0; the unread `vertexCount` array is gone.
- **BatchedEffect:** poses are stored inline in `Request`/`Stream`; the `plays` list is gone.
- **Light budget:** `EffectPlayer` keeps a fixed array of `LightBudget` slots (free slot or steal the oldest ticket).
- **Shared setup:** `Effects/ParticleBatching.cs` (`CanBatch`, `NeedsRotate3D`, `ConfigureShared`, `EnsureBuffer`) used by both backends; FlightBatch now also clamps particle lights to `MaxLightsPerSystem`.
- **Dead code:** `ProjectileSystem.DefaultEnemyRadius` and `ProjectileArchetype.IsSimulated` removed; `UnityEngine.Object` qualified in `EffectPool` and `TrailMesh`.

Results:
- EditMode 58/58; the dev player builds.
- `-perfSuite core` in the dev player ran while the machine was busy with other work (load average 18–23), so its numbers can't be compared with the baselines: S1-1x 8.3/13.1/16.6/361.8 ms, S1-5x 9.1/13.7/16.4/21.7, S2 11.5/13.5/14.6/29.7, S3 12.2/13.8/14.5/16.8 (p50/p95/p99/max). That is 2–3 ms slower at p50 than the projectile visuals baseline. Game speed held (0.99x/5.00x/1.00x/5.00x), and every projectile and effect played.
- Not run (machine tests stopped because of other load): the `V` and `fx` screenshot suites, `-perfProfile` + `ProfileReport`, and a core rerun on an idle machine.

## Sprint 4: balance tools (`fix/review-s4`)
Done (in `Tools/BalanceDashboard/`):
- **`write_waves.py`:** `assign_to_nested` parses `m_Modifications` as blocks, including Unity-wrapped continuation lines, and checks the block structure before and after writing (it fails loudly on a cut wrap, an orphan line, a missing `value:` or a wrong guid). Tested on a scratch copy of BeginnerLevel02.prefab with 965 wrapped lines, where the old regex left 50 orphan lines. On the real assets the output is byte-identical.
- **Hangar period:** `(A−2)·I + max(2I, R)` for A ≥ 2 (`max(I, R)` for A = 1), checked against a frame-by-frame replay of `Starfighter.cs`. The Methodology text is updated.
- **Pools:** ProjectileSystem kinds are uncapped; the Pulse pool uses `ProjectilePoolManager.MaxPoolSize` (4096).
- **Mine Factory:** placement inside the RANGE − 0.1 sphere (as in `RefreshPathSpans`, with 3D spacing and the full targeting bias), flight time `max(0.35, distance / max(0.5, SPEED))` from the launch point.
- **Pulse:** a shrinking sphere that hits each enemy once while it lasts. It shrinks 2.5 → 0.5, not → 0: the collider keeps scale.y = 1, so its radius never goes below 0.5. Not modelled: a pooled pulse keeps the `origLifetime` it was first created with.
- **Small fixes:** `--seeds N` = seeds 1..N (0 rejected), `quick` option removed, `leadEfficiency` keeps 0, cluster fire points counted by `fileID`, missing enemy entries keep placeholders (with a warning), README level count.
- **Veteran mode:** `extract.py` reads `Resources/Progress/MetaUpgradeTree.asset`; `engine.js` setting `meta: none|full`, `acceptance.py --meta none|full` and a dashboard Meta selector. It mirrors `MetaUpgrades.ApplyTo`/`ToModifier`, the GameManager economy hooks, the lives bonus (skipped on Impossible) and the `Price()` round-to-5 rule. The default stays `none`. CLAUDE.md is updated.

Rebaseline (`acceptance.py`, Medium; singles / pairs / triples won; full roster over three seeds). Before = sprint 1 engine (FireCycle change only), after = sprint 4:

| Level | Before | After (`--meta none`) | Veteran (`--meta full`) |
|---|---|---|---|
| Beginner 01 | 1/8, 8/28, 22/56; L L W | 1/8, 8/28, 21/56; L L W | 7/8, 24/28, 54/56; W W W |
| Beginner 02 | 1/8, 7/28, 21/56; W W W | unchanged | 2/8, 13/28, 36/56; W W W |
| Beginner 03 | 3/8, 14/28, 43/56; L W W | 2/8, 12/28, 30/56; W W W | 5/8, 22/28, 46/56; W W W |
| Beginner 04 | 1/8, 9/28, 21/56; W W W | 2/8, 10/28, 22/56; W W W | 5/8, 24/28, 54/56; W W W |
| Beginner 05 | 2/8, 11/28, 37/56; W W W | **0/8, 5/28, 20/56; L L L (R45)** | 5/8, 21/28, 46/56; W W W |
| Intermediate 01 | 1/8, 8/28, 21/56; W W W | 1/8, 8/28, 20/56; W W W | 5/8, 22/28, 53/56; W W W |
| Intermediate 02 | 1/8, 6/28, 13/56; L L L | 1/8, 5/28, 10/56; L L L | 4/8, 21/28, 52/56; W W W |

- Beginner 03 (Mine Factory, Rockets, Hangar) and Beginner 05 (Hangar) shift the most. Both models changed: the Mine Factory's placement sphere and flight time, and the Hangar's cannon period (the banked shot after a reload). Beginner 05's full roster now loses at round 45.
- Veteran mode wins every level with all 220 lives, and single towers win on 2–7 of 8. That's far above the "single towers lose" target, so the meta tree is strong enough to trivialise the current waves.

## Needs user verification
- Placement clicks with enemies in front of an anchor.
- Backspace selling on a Mac.
- Camera panning and focus while paused.
- Trails and glows while paused, and new bolts' glows (centred from their first frame).
- How 10x play feels after the timing fixes.
- Decisions:
  - Beginner 05 (and Beginner 03) after the corrected Hangar and Mine Factory models: retune the waves, or accept?
  - How strong veteran mode should be; today it wins everything.

## Not run yet (stopped because of other load on the machine)
- `LevelPlaytest.Run` (sprint 1).
- `-perfSuite parity` reseed of the Bullet Dispenser at 10x (`T-BulletDispenser-1x,T-BulletDispenser-10x -perfSeed 2`).
- `-perfSuite V` and `fx` screenshots (glows centred on bolts, full-length trails), and `-perfProfile` + `ProfileReport` for GC in the effect paths.
- `-perfSuite core` in the dev player on an idle machine, compared with the projectile visuals baseline.

---

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
Unity -batchmode -projectPath 3DTD -executeMethod PerfBenchmark.Run -perfSuite core -logFile <log>
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
- At most 1 KB of garbage per steady frame in the Editor (TMP copies every changed text into a string in the Editor) and 0 B in builds. A steady frame is one in the second wave or later, more than 1 s from that wave's start and end, in which the Spawner created no enemy. The first wave grows pools and buffers to the new peak once, as the first waves of any session do.
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

## After Phase 5: the player build and its render-thread wait
From here on the reference is the development player (`PerfBenchmark.BuildPlayer`); the Editor adds about 10 ms per frame.

**Main-thread effect work (commits 5401eec, 8ddf739):**
- Batched particles are placed by Burst jobs.
- Each bullet used to emit an invisible trail-head particle, about 30 µs per `Emit` and up to 24 ms in a heavy frame. That is replaced by `TrailMesh`: every flight keeps a ring of recent points, and one Burst job builds all the camera-facing ribbons with the `TrailRenderer`'s width, gradient (in linear space) and material.
- The batched copy emulates Inherit Velocity at emission, so the stretched spark dashes behind each bullet are back.

**Render-thread wait:**
- In the player, S3 still had frames of 30–50 ms. Unity's `ParticleSystem.WaitForPreviousRenderingToFinish` blocked the first `Emit` of each frame for up to 17 ms, because the render thread was waiting for particle geometry.
- Unity builds the geometry of each particle system in one job, and all bullet flights lived in one shared copy.
- Fix: flights are now spread over shards, about a third of the cores (`EffectPool.MaxFlightShards`). On the M4 Pro, 4 shards measured best; 3 brought the wait back and 7 or 12 were slower.
- The per-flight work runs in Burst jobs, and the place jobs of all emitters run in parallel.

**Other frame-time work:**
- Projectiles that only move or fade are advanced inside the Burst step job; the main thread handles only hits and expiries.
- Enemies are ticked in one Spawner loop that writes position and rotation together.
- Batched systems show at most 32 particle lights each (was 64).
- The first Burst job and each job type's reflection data are warmed up at app start. That was 25 ms on the first shot.
- The death-effect renderer is created at level start (it allocates 0.5 MB).
- The benchmark keeps the window resize, screenshot frames and enemy-pool prewarming out of the measurement.

**Garbage** (found with `-perfProfile` captures and `ProfileReport`'s allocation callstacks):
- HUD counters keep a width that only follows their digit count. Each resize during a layout rebuild made UGUI start a coroutine.
- `PhysicsRaycaster` uses a fixed 256-hit buffer (`m_MaxRayIntersections`) instead of `RaycastAll`.
- Starfighter separation no longer boxes an enumerator.
- The beam and missile sorts no longer allocate a delegate per call.
- The trail mesh reuses its vertex layout.
- Effect queues and pop lists start with room for a busy frame.

## Phase 6 results (macOS development player, M4 Pro, 1920×1080 window)

| Run | p50 / p95 / p99 / max (ms) | Game speed | Steady GC | Frames that allocate |
|---|---|---|---|---|
| S1-1x | 5.8 / 9.1 / 11.3 / 31.7 | 1.00x | 18 B/frame | 0.6 % |
| S1-5x | 6.7 / 10.6 / 13.1 / 18.9 | 5.00x | 33 B/frame | 2.3 % |
| S2 (150 dispensers) | 9.7 / 12.4 / 13.4 / 15.4 | 1.00x | 8 B/frame | 0.4 % |
| S3 (S2 at 5x) | 10.7 / 14.7 / 15.8 / 16.8 | 5.00x | 192 B/frame | 2.4 % |

- **Frame-time criteria:** all met (p99 ≤ 16.7 ms, max ≤ 33 ms, ≥ 98 % of 5x).
- **No culling:** every requested projectile, effect and pop animation played. S3: 386k projectiles, 589k effects, 44k pops.
- **GC:** the remaining steady garbage is list-capacity growth while the load still rises from round 100 to 102 (a handful of doublings per session), not per-frame garbage, so the strict 0 B criterion is still reported as failed. In the second half of S2, 34 of 6,294 frames allocated.
- **Run-to-run variation:** S3's p99 varies by about ±0.5 ms. S1-1x's max varies between 24 and 32 ms (one hitch per run).

Parity in the player (T runs, damage at 1x / 3x / 5x):

| Tower | 1x | 3x | 5x |
|---|---|---|---|
| Laser | 435 | 435 | 435 |
| Core | 7 | 7 | 7 |
| Rocket | 7,600 | 7,620 (+0.3 %) | 7,624 (+0.3 %) |
| Sniper | 1,080 | 1,080 | 1,080 |
| Bullet Dispenser | 123 | 123 | 123 |
| Mine Factory | 15,901 | 15,856 (−0.3 %) | 15,823 (−0.5 %) |
| Hangar (informational) | 556 | 486 (−12.6 %) | 544 (−2.2 %) |
| Beam | 0 | 0 | 0 |

The Beam's 0 comes from the benchmark's aim (its beam runs beside the lane). LevelPlaytest builds a Beam Tower on Level 04, where it deals damage.

Other checks:
- EditMode 45/45, LevelPlaytest 35/35, UIPlaytest 34/34.
- `extract.py`: the balance data is unchanged.
- `acceptance.py` runs. `engine.js` needs no change: damage, armor, income and spawning rules are the same. Bullets now pass through anchors, which the engine never modelled. Enemies no longer lose movement at waypoints, which matches the engine better.

## Status
- [x] Phase 0: benchmark harness and baseline.
- [x] Phase 1: bugs and cheap structural fixes.
- [x] Phase 2: physics configuration.
- [x] Phase 3: projectile system and parity (the Hangar matches only statistically, see above).
- [x] Phase 4: effect batching.
- [x] Phase 5: death animations (instanced).
- [x] Phase 6: wrap-up, player build measurements, docs (CLAUDE.md lives outside the repo).

## Open items and ideas
- **S3 margin:** p99 15.8 ms on an M4 Pro, so slower machines will exceed 16.7 ms in that extreme scenario. The rest of the frame is spread over many 0.5–1 ms items: URP culling and light setup, physics triggers for 150 tower ranges, shadows, and particle geometry on efficiency cores.
  - The next large step would be range queries through `EnemyRegistry` instead of trigger colliders (about 1.5 ms of physics per frame in S3).
- **One-time hitches:** Mono JIT compiles methods on their first call, and an effect prefab's shared copy is created on its first use (about 6 ms). IL2CPP for release builds would remove the JIT part.
- **Hangar:** its damage differs across game speeds by up to about 13 % between single runs, within the dogfight's seed noise. Fighters still decide on end-of-frame enemy positions.
- **Beam T run:** fix the benchmark's aim so it measures the Beam.

## Needs user verification
- How the batched effects look next to the originals, in normal play at 1x and 5x:
  - bullet trails and spark dashes, muzzle starbursts, impacts, pops;
  - the 32-light cap per system, which leaves less muzzle light on nearby blocks than the originals.
  - `-perfSuite V` with and without `-perfNoBatching 1` writes matching close-ups to `tasks/perf/`.
- How 5x feels with a full dispenser field.
- That hover and selection outlines still show.
- Whether the flamethrower's spark sound, limited to 4 voices per clip, sounds right.

---

# Handoff: projectile visuals revamp (branch `fx/projectile-visuals`)

Started 2026-10-05. The plan is in `~/.claude/plans/plan-a-revamp-of-distributed-moon.md`.

## Done
- **Colours per tower:** Laser keeps its blue bolt; Core is a paler sky blue; Rocket System green; Bullet Dispenser small dark green needles; Hangar small red bolts and mini missiles; Beam yellow; Sniper red tracers; Mine Factory red.
- **Smoke:** the opaque lit smoke puffs of every generated explosion, muzzle and rocket trail are now a few small ember wisps that fade within about half a second.
- **A look per upgrade module:** `VisualUpgrade` components (generated, first in each module) replace the muzzle, flight or impact part of the shot, or the Hangar's cannon/ordnance, the Mine Factory's blasts and launch, the Beam's stage (width, colour, flicker, tick swell, end effects) and the Sniper's tracers. Each path styles its own part, so combined paths stay visible. The exceptions are the Hangar's squadron path, the Mine Factory's salvage path, the cluster rockets and the pulse, which keep their own base look.
- **Stat-driven sizes:** explosions grow with the blast radius (`ProjectileSystem.BlastImpactScale`), and impacts and muzzles grow with SIZE.
- **Sniper:** a muzzle flash per barrel, a tracer line (`TracerRenderer`) and an impact; Double- and Triple-Shot add amber and violet tracers. **Beam:** `TowerBeam` replaces `PolygonBeamStatic`.
- **Tooling:** `ProjectileVisualsBuilder.Run` generates everything; `-perfSuite fx` captures a gallery of every tower and tier; `ProjectileVisualsTests` covers it in EditMode. `extract.py` skips visual upgrades, so the balance data is unchanged apart from the new prefab paths.

## Results
- EditMode 49/49, UIPlaytest 34/34, LevelPlaytest 35/35.
- **Parity:** damage of every tower matches within 1 % at 1x/3x/5x. The exception is the Hangar, whose dogfight noise is informational. Four seeds averaged 608 (1x) and 654 (5x), and the sign of the difference flips between seeds.
- **Particle collision:** generated effects have world collision off. The Bullet and Spike impacts collide their sparks with the world, which first made S2 run at 0.4x (900 ms spikes in `ParticleSystem.UpdateJob`).
- **Player, M4 Pro, `-perfSuite core`** (p50 / p95 / p99 / max ms):

| Run | Before (Phase 6) | Now |
|---|---|---|
| S1-1x | 5.8 / 9.1 / 11.3 / 31.7 | 6.4 / 10.3 / 12.1 / 228.6 |
| S1-5x | 6.7 / 10.6 / 13.1 / 18.9 | 7.7 / 11.1 / 13.4 / 21.1 |
| S2 | 9.7 / 12.4 / 13.4 / 15.4 | 8.7 / 10.9 / 11.7 / 157.4 |
| S3 | 10.7 / 14.7 / 15.8 / 16.8 | 9.2 / 11.9 / 13.2 / 28.4 |

The dispensers are cheaper than before. S1 is about 1 ms slower at p50 because of the heavier tier-3 looks. The single max frames are first-use hitches: about 70 new effect prefabs each create their shared copy or pool on first play.

## Open items
- **First-use hitches:** prewarm every effect pool of the level's towers at level start, like `DeathEffectRenderer.Prewarm`.
- **Diff noise:** effects built with `Merge` get new local file IDs on every builder run (same GUIDs).
- **Pooled effects:** the rocket and grenade explosions (and their merged variants) keep their sub-emitters, so they stay pooled.
- **Repository size:** the generated effects add about 46 MB of prefab YAML.

## Needs user verification
- The look of every tower and tier in normal play (`-perfSuite fx` writes the gallery to `tasks/perf/`).
- Whether the dark green needles read well against the space background.
- Whether there's still too much or too little smoke; the ember wisps render as small solid glowing shards rather than soft haze.
- The Beam widths at Fusion and UNLIMITED POWER, and the Sniper tracer widths and durations.
- The toxic green flamethrower.
