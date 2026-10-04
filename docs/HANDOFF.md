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

## Status
- [x] Phase 0: benchmark harness and baseline.
- [x] Phase 1: bugs and cheap structural fixes.
- [ ] Phase 2: physics configuration.
- [ ] Phase 3: projectile system and parity.
- [ ] Phase 4: effect batching.
- [ ] Phase 5: death animations (instanced).
- [ ] Phase 6: wrap-up.

## Needs user verification
- How the batched effects look, and how 5x feels with a full dispenser field (after Phase 4/5).
