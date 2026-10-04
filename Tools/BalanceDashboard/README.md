# 3DTD Balance Lab

A local dashboard that reads the game's balance data straight from the Unity project and turns it into analytics. It covers towers, every reachable upgrade build, upgrade modules, enemies, waves, economy, difficulty profiles and level geometry. It also includes a wave simulator, a meta agent that plays whole games, and the wave designer that generates the levels' waves.

## Use it

```sh
python3 Tools/BalanceDashboard/extract.py   # run from the repo root after changing prefabs, StatsConfigs, waves, profiles or scenes
open Tools/BalanceDashboard/index.html      # works from disk, no server needed
```

`extract.py` only needs the Python 3 standard library. Unity doesn't have to be installed or running. The script parses the YAML of scenes, prefabs and ScriptableObjects directly, and it resolves nested prefabs and scene overrides. Pass `--project <path>` to point it at another checkout.

## Files

| File | Purpose |
|---|---|
| `extract.py`, `unity_yaml.py` | Extractor; writes `balance-data.json` and `balance-data.js` (same data, loadable from `file://`) |
| `engine.js` | All models, metrics, the simulator and the meta agent; has no DOM access, so it can run headless |
| `ui.js`, `app.js`, `styles.css`, `index.html` | The dashboard |
| `wavegen.js`, `wave-design.json`, `write_waves.py` | Wave designer: generates every level's waves and writes them into the project |
| `acceptance.js`, `acceptance.py` | Headless balance checks (build efficiency, single-tower, pair and triple sweeps per level and difficulty) |

## What it shows

- **Towers / All builds:** for each of the 206 builds the path rules allow, it shows volleys per second, raw/expected/crowd DPS, hit chance, range vs projectile reach, path coverage on the best anchor, damage per enemy pass, value per $100, payback time, overkill and the money lost on sale.
- **Upgrades:** the marginal value of every module, with its advertised effect (parsed from the description) next to the modelled one.
- **Enemies:** layer HP, money, lives on leak and walk times, with a damage resolver.
- **Waves & economy:** per-round HP, traits, income after the difficulty's multiplier, end-of-wave bonus, lives at risk and stream DPS demand vs the DPS money can buy.
- **Levels & coverage:** top and side maps with anchors colored by coverage, plus walk times and per-tower coverage.
- **Simulator / Opportunity cost:** a frame-by-frame replay of a level where you buy copies of one build.
- **Meta & difficulty:** the difficulty profiles, the role-weighted value per $ of every build, and in-browser runs of the meta agent (palette sweeps and full-roster games).
- **Telemetry:** imports the CSV files the game writes (`BalanceTelemetry.cs`) and compares real damage per tower and second with the model.
- **Data & export:** CSV for every table, the computed analytics as JSON, and a Markdown summary for pasting into an AI chat.

The **Methodology** tab documents every formula and assumption. Exact replays of the C# code are separated from approximations: hit chance geometry, crowd density, starfighter flight, how much of a wave a Mine Factory's field sits full (salvage income) and the meta agent. Assumptions can be edited in the top bar under "Assumptions…"; the difficulty selector switches every number to that DifficultyProfile.

## Balancing workflow

1. **Tower numbers** live in the StatsConfig assets (`Scripts/Tower/Towers/*`) and the tower prefabs (`Building.cost`, upgrade modules). FIRERATE is seconds between shots, so "+X% fire rate" is a modifier of −X/(100+X)·100. Module descriptions list the effects in that convention, and the Upgrades tab flags claims that don't match the model.
2. **Prices** follow a power budget. Every legal build should be worth about its price: role-weighted value per $ ≈ 1.1 (tier 1), 1.0 (tier 2) and 0.85 (tier 3) × the base tower's. Check this under Meta & difficulty → Build efficiency. Crosspath combinations multiply stats, so price modules by the builds they complete, not by their effect on a bare tower.
3. **Waves** come from the wave designer. Edit `wave-design.json` (per level: HP scale, growth, check schedule, enemy count slope, lane factor, extra start money) or `wavegen.js`, then run
   ```sh
   python3 Tools/BalanceDashboard/write_waves.py   # writes ScriptableObjects/WaveData/<set>/ and assigns the sets to the Spawners
   python3 Tools/BalanceDashboard/extract.py
   ```
   Income is 1 per popped layer, so more HP also means more money. A uniform HP scale mostly changes the early game. The levers that change difficulty are the enemy count slope (fewer, bigger enemies pay less per HP), the check schedule and the profiles' income multipliers.
4. **Difficulty** lives in `ScriptableObjects/Difficulty/*.asset` (start money, lives, prices, income per round, end-of-wave bonus, refund, enemy speed/HP, armor and shield bonus, win round).
5. **Check** with the acceptance run (a few minutes; it plays several hundred simulated games):
   ```sh
   python3 Tools/BalanceDashboard/acceptance.py                                   # Medium, singles/pairs/triples, all levels
   python3 Tools/BalanceDashboard/acceptance.py --difficulty Easy --difficulty Hard --sizes 1 2
   ```
   Targets on Medium:
   - Single towers lose on at least 3 of 4 levels.
   - Many pairs and triples win.
   - Every tower appears in winning combinations on some level.
   - Easy is clearly easier and Hard clearly harder.
6. **Playtest** with telemetry on (Editor or development build). Import the CSVs in the Telemetry tab, and re-tune the model assumptions (Starfighter duty, crowd spacing) where real and modelled damage differ by more than about 15%.

## For AI agents

- `balance-data.json` is the raw extracted data. `waves-generated.json` is the last wave designer output.
- In the dashboard, Data & export → "Computed analytics (.json)" gives every computed metric.
- The engine also runs without a browser, through JavaScriptCore on macOS (or node):

  ```sh
  JSC=/System/Library/Frameworks/JavaScriptCore.framework/Versions/Current/Helpers/jsc
  echo 'var window = this;' > /tmp/shim.js
  echo 'const E = TDEngine.init(window.BALANCE_DATA, {level: "Beginner Level 01", difficulty: "Medium"}); E.insights().forEach(i => print(i.text));' > /tmp/q.js
  $JSC /tmp/shim.js balance-data.js engine.js /tmp/q.js
  ```

  The meta agent can be called with `E.runAgent({ level, seed, rounds, palette, waves })`, where `palette` is a list of tower keys and `waves` optionally overrides the level's waves with `generateWaves(E, opts)` output.

## Adding a tower type

The extractor picks up any prefab in `Prefabs/Tower/` and any prefab in a level's building palette. The engine models these action strategies: Laser, Drone, Bomb, Sniper, Beam, Bullet Dispenser (normal, aura and pulse modes), Hangar and Mine Factory. A new `ActionStrategy` also needs:

- an entry in `STATS_READ` in `extract.py`;
- a case in `strategyKind` / `buildModel` / the simulator's `towerTick` in `engine.js`, and its damage type in `E.damageTypeOf`;
- role weights in `E.ROLE_WEIGHTS`, so build efficiency and price checks know what the tower is for.
