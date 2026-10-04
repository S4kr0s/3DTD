/*
 * 3DTD wave designer. Generates themed, budgeted waves for a level from a few parameters
 * (see wave-design.json); write_waves.py turns the result into WaveData assets.
 *
 *  - Every wave has an HP budget that grows per round (`growth`: [fromRound, factor per round]).
 *  - A target enemy count (count0 + countSlope * round) decides how the budget is split: the enemy
 *    type of each group is the one whose total HP fits budget / count. Fewer, bigger enemies pay
 *    less money per HP (1 per popped layer), so countSlope is the main per-map difficulty lever.
 *    A uniform `scale` mostly matters early: more HP also means more income.
 *  - The newest shape available rises every 10 rounds.
 *  - Every 5th wave is a themed check (swarm, heavy, armor, shield, regen, fast, boss, combinations),
 *    the wave after it a lighter relief wave. Traits are introduced by a single telegraph enemy first.
 *  - `checks` picks a check schedule from CHECK_SETS or gives one explicitly ({round: theme}).
 *
 * Runs in the dashboard and headless (JavaScriptCore / node): generateWaves(E, opts), E = TDEngine.
 */
(function (root) {
  'use strict';

  const CHECK_SETS = {
    // every trait gets a check, bosses every 20 rounds
    default: {
      5: 'swarm', 10: 'heavy', 15: 'armor', 20: 'boss', 25: 'regen', 30: 'shield', 35: 'fast', 40: 'boss',
      45: 'armor+shield', 50: 'swarm+heavy', 55: 'regen+armor', 60: 'boss', 65: 'shield-swarm', 70: 'fast+armor',
      75: 'regen-heavy', 80: 'boss', 85: 'all', 90: 'armor-swarm', 95: 'shield-heavy', 100: 'boss',
    },
    // long single path where splash damage shines: more single big or fast targets
    heavy: {
      5: 'swarm', 10: 'heavy', 15: 'armor', 20: 'boss', 25: 'fast', 30: 'heavy', 35: 'fast', 40: 'boss', 45: 'shield-heavy',
      50: 'fast+armor', 55: 'regen-heavy', 60: 'boss', 65: 'heavy', 70: 'fast+armor', 75: 'shield-heavy', 80: 'boss', 85: 'all',
      90: 'fast', 95: 'regen-heavy', 100: 'boss',
    },
    // several lanes where long-range towers shine: more crowds of shielded or armored small targets
    swarm: {
      5: 'swarm', 10: 'heavy', 15: 'armor', 20: 'boss', 25: 'shield', 30: 'swarm', 35: 'shield-swarm', 40: 'boss', 45: 'armor-swarm',
      50: 'swarm+heavy', 55: 'shield-swarm', 60: 'boss', 65: 'armor-swarm', 70: 'shield-swarm', 75: 'swarm', 80: 'boss', 85: 'all',
      90: 'armor-swarm', 95: 'shield-swarm', 100: 'boss',
    },
  };
  // EnemyTrait flags (Enemy.cs)
  const A = 1, S = 2, R = 4;
  // first appearance of each trait: a single enemy a few rounds before its check
  const TELEGRAPH = { 12: A, 17: S, 23: R };
  const BOSSES = { 20: 1, 40: 2, 60: 4, 80: 6, 100: 10 };
  const FAST = [6, 7];   // purple and pink layers

  function generateWaves(E, opts) {
    opts = opts || {};
    const rounds = opts.rounds || 100;
    const growth = opts.growth || [[1, 1.165], [31, 1.09], [61, 1.05]];
    const lane = opts.laneFactor || 1;
    const bossId = opts.bossId != null ? opts.bossId : E.enemies.findIndex(e => e.special);
    const checks = (opts.checks && typeof opts.checks === 'object') ? opts.checks : CHECK_SETS[opts.checks || 'default'];
    const checkMult = opts.checkMult || 1.1;
    // raw layer health, so the waves don't depend on the difficulty selected in the dashboard
    const hpOf = (id) => E.enemyLayers(id).reduce((sum, l) => sum + l.health, 0);
    const countAt = (r) => (opts.count0 || 8) + (opts.countSlope != null ? opts.countSlope : 1.4) * r;

    // enemy whose total HP is closest to h (log scale), at most `cap`, optionally only some colors
    const pick = (h, cap, colors) => {
      let best = 0, bestErr = Infinity;
      for (let id = 0; id <= Math.min(49, cap); id++) {
        if (colors && colors.indexOf(id % 10) < 0) continue;
        const err = Math.abs(Math.log(hpOf(id) / Math.max(0.5, h)));
        if (err < bestErr) { bestErr = err; best = id; }
      }
      return best;
    };

    const waves = [];
    let budget = (opts.base || 24) * (opts.scale || 1) * lane;
    for (let r = 1; r <= rounds; r++) {
      if (r > 1) {
        let g = 1;
        growth.forEach(([from, f]) => { if (r >= from) g = f; });
        budget *= g;
      }
      const topShape = Math.min(4, Math.floor((r - 1) / 10));
      const cap = topShape * 10 + (r > 50 ? 9 : Math.min(9, (r - 1) % 10 + (topShape > 0 ? 2 : 0)));
      const duration = (opts.durScale || 1) * Math.min(40, 9 + 0.3 * r);   // seconds of spawning per lane
      const theme = checks[r] || (checks[r - 1] ? 'relief' : 'standard');
      const wb = budget * (theme === 'relief' ? 0.75 : theme === 'standard' ? 1 : checkMult);
      const N = countAt(r) * lane;
      const entries = [];

      // one spawn group: share of the wave budget, count multiplier, share of the duration, traits,
      // colors to pick from and an optional lower shape cap
      const group = (share, countMult, durShare, traits, colors, capOverride) => {
        const hp = wb * share, n = Math.max(1, N * countMult * share);
        const id = pick(hp / n, capOverride != null ? Math.max(0, capOverride) : cap, colors);
        const count = Math.max(1, Math.min(600, Math.round(hp / hpOf(id))));
        entries.push({ enemy: id, count, delay: +(duration * durShare / count).toFixed(3), traits: traits || 0 });
      };
      // after their introduction, traits also show up in standard waves
      const sprinkle = r >= 46 ? [A, S, R, A | S, 0][r % 5] : r >= 36 ? [A, S, R, 0][r % 4] : r >= 26 ? [A, 0, S, 0, R][r % 5] : 0;

      switch (theme) {
        case 'swarm':        group(1, 3, 0.7); break;
        case 'heavy':        group(0.8, 0.3, 1); group(0.2, 1, 0.5); break;
        case 'armor':        group(0.7, 0.6, 1, A); group(0.3, 1, 0.6); break;
        case 'shield':       group(0.7, 1.4, 0.8, S); group(0.3, 1, 0.6); break;
        case 'regen':        group(0.7, 0.6, 1.1, R); group(0.3, 1, 0.5); break;
        case 'fast':         group(0.8, 1, 0.7, 0, FAST); group(0.2, 1, 0.5, 0, FAST); break;
        // combined traits use the shape below the newest one
        case 'armor+shield': group(0.5, 0.8, 1, A, null, cap - 10); group(0.5, 1.4, 0.8, S); break;
        case 'swarm+heavy':  group(0.4, 3, 0.5); group(0.6, 0.3, 1); break;
        case 'regen+armor':  group(0.6, 0.8, 1, R | A, null, cap - 10); group(0.4, 1, 0.7); break;
        case 'shield-swarm': group(1, 2.5, 0.7, S); break;
        case 'fast+armor':   group(0.7, 0.8, 0.8, A, FAST, cap - 10); group(0.3, 1.5, 0.4, 0, FAST); break;
        case 'regen-heavy':  group(1, 0.4, 1.1, R); break;
        case 'all':          group(0.4, 0.8, 1, A | R, null, cap - 10); group(0.3, 1.4, 0.8, S); group(0.3, 1, 0.6, 0, FAST); break;
        case 'armor-swarm':  group(1, 2.5, 0.7, A); break;
        case 'shield-heavy': group(1, 0.4, 1, S); break;
        case 'boss': {
          const n = Math.max(1, Math.round((BOSSES[r] || 1) * lane));
          const bossTraits = r >= 100 ? A | S | R : r >= 80 ? A | S : r >= 60 ? A : 0;
          const escort = Math.max(0.3, 1 - hpOf(bossId) * n / wb);
          group(escort, 1, 0.8);
          entries.push({ enemy: bossId, count: n, delay: +(duration / n).toFixed(3), traits: bossTraits });
          break;
        }
        default:             group(0.65, 0.8, 1, sprinkle); group(0.35, 1.2, 0.7);
      }
      if (TELEGRAPH[r]) entries.push({ enemy: Math.max(10, Math.min(cap, topShape * 10 + 3)), count: 1, delay: 1, traits: TELEGRAPH[r] });
      waves.push({ round: r, name: String(r).padStart(3, '0') + ' ' + theme, theme, entries });
    }
    return waves;
  }

  root.TDWaves = { generateWaves, CHECK_SETS };
  // the tuning scripts call it as a global
  root.generateWaves = generateWaves;
})(typeof window !== 'undefined' ? window : this);
