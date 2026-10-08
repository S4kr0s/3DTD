# Roadmap

`main` is the 2024 version. The 2026 work is on `fx/projectile-visuals` (stacked on `perf/overhaul`); its handoff notes are in `docs/HANDOFF.md` on that branch.

Done in 2026: the Unity 6 upgrade, the UI redesign, the beginner levels 01–05 with a balance dashboard, and the performance overhaul (phases 0–6: batched effects, data-driven projectiles, instanced pop animations).

## Now

- Projectile visuals revamp: a colour per tower, ember smoke, a look per upgrade module, the Sniper tracers and the new Beam. The code is done; I still have to judge the looks in normal play.
- Prewarm every effect pool of a level's towers at level start, so the first shot of a new effect doesn't hitch.

## Next

- Merge `perf/overhaul` and `fx/projectile-visuals` into `main`.
- Balance the beginner levels on Medium: in the acceptance runs, Level 01 is lost even with the full roster.
- Verify by hand what the overhaul changed: the batched effects next to the originals, 5x speed with a full dispenser field, hover and selection outlines, the flamethrower's spark sound.

## Later

- Range queries through `EnemyRegistry` instead of trigger colliders, for slower machines in the heaviest scenario.
- IL2CPP for release builds, to drop the Mono JIT hitches.
- Hangar parity across game speeds, and a fixed Beam benchmark run (its aim misses the lane today).
- Keep an eye on repository size (the generated effects add about 46 MB of prefab YAML).
