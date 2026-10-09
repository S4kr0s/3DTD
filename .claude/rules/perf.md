---
paths:
  - "3DTD/Assets/Editor/Perf/**"
  - "3DTD/Assets/Scripts/Diagnostics/**"
  - "tasks/perf/**"
  - "3DTD/docs/HANDOFF.md"
---

# Performance tooling (paths relative to the Unity project `3DTD/`)

- Performance tooling (`Assets/Editor/Perf/`, `Scripts/Diagnostics/`; needs graphics, project closed in the Editor). Results and their history are in `docs/HANDOFF.md` (repo root).
  - `PerfBenchmark.Run -perfSuite <suite>` plays the benchmark scenarios on Beginner Level 01 (`PerfScenario`) and exits with the number of failed checks (`PERF RESULT|PASS|FAIL|PARITY` lines; JSON, per-frame CSV and screenshots in `tasks/perf/`). Suites: `core` (S1-1x/S1-5x: every tower type maxed, S2/S3: 150 maxed Bullet Dispensers at 1x/5x, rounds 100-102), `parity` (each tower alone at 1x/3x/5x/10x; damage must match within 3 %; plus `L-1x`/`L-10x`: the level undefended, leaked lives must match), `full`, `V` (fixed-step close-ups of the effects; add `-perfNoBatching 1` for the pooled originals), `fx` (effect gallery: per tower type an unupgraded copy and one per single path at tier 1-3, close-ups when enemies pass, `fx-<tower>-<path><tier>-<n>.png`; runs `FX-Default`, `FX-Bomb`, ...) or comma-separated run ids. Flags: `-perfSeed n`, `-perfOut <dir>`, `-perfSpikeMs <ms>` (logs what slower frames spent their time on), `-perfMarkers <text|text>` (lists profiler markers), `-perfProfile <file> [-perfProfileStart n] [-perfProfileFrames n]` (profiler capture with allocation callstacks). It backs up and restores progress and savegame.
  - `PerfBenchmark.BuildPlayer` builds a development player to `Builds/PerfPlayer/3DTD.app` (gitignored); run it with the same flags, `Builds/PerfPlayer/3DTD.app/Contents/MacOS/3DTD -perfSuite core -perfOut <dir> -logFile <file>`, with absolute paths (relative ones land inside the app bundle). The player is the reference for frame times: the Editor adds about 10 ms per frame.
  - `ProfileReport.Run -profileFile <file.raw>` lists the allocation callstacks, the average self times and the slowest frames of a capture.
