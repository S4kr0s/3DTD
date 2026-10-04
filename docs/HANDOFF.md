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
- `core`: S1-1x, S1-5x, S2, S3 and P-1x/3x/5x.
- `full`: adds S1 at rounds 50 and 75, and a small dispenser parity group.
- A comma-separated list of run ids picks single runs.

Results go to `tasks/perf/perf-<suite>-<time>.json`, a per-frame `.csv` and one screenshot per run (`perf-<id>.png`). The log has `PERF RESULT|PASS|FAIL|PARITY` lines.

The scenarios are on Beginner Level 01, Medium, with seeded randomness:

| Run | Towers | Rounds |
|---|---|---|
| S1 | Every tower type in each "one path at tier 3 + another at tier 2" combination (43 towers), spread along the lane | 100, 101, 102 |
| S2 | 150 Bullet Dispensers (path 1 tier 3 + path 2 tier 2), roughly what a Medium game can afford by round 100 | 100, 101, 102 |
| S3 | S2 at 5x | 100, 101, 102 |
| P | One of each tower type at tiers 2/2 (8 towers): a weak defense whose leaks show tower effectiveness | 60, at 1x/3x/5x |

Blocks are placed on the lane's block grid with the top face just below the enemies; towers on top see them inside their half-sphere range.

Checks:
- Frame p99 ≤ 16.7 ms and max ≤ 33 ms.
- At most 1 KB of garbage per steady frame in the Editor (TMP copies every changed text into a string in the Editor) and 0 B in builds.
- At 5x, the game time ratio is ≥ 98 % of 5.
- No dropped projectiles, death animations or effects (`PerfCounters`).
- Parity: damage and leaked lives within 3 % of the 1x run, damage per tower type within 5 %.

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

## Status
- [x] Phase 0: benchmark harness and baseline.
- [ ] Phase 1: bugs and cheap structural fixes.
- [ ] Phase 2: physics configuration.
- [ ] Phase 3: projectile system and parity.
- [ ] Phase 4: effect batching.
- [ ] Phase 5: death animations (instanced).
- [ ] Phase 6: wrap-up.

## Needs user verification
- How the batched effects look, and how 5x feels with a full dispenser field (after Phase 4/5).
