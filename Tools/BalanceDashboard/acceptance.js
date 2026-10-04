/*
 * Balance acceptance checks, headless. Run through acceptance.py (JavaScriptCore or node).
 * Uses the levels' real wave assets and difficulty profiles from balance-data.js.
 *
 * ACCEPTANCE_OPTS: { levels, difficulties, sizes, seeds, quick }
 */
(function (root) {
  'use strict';
  const D = root.BALANCE_DATA;
  const E = root.TDEngine.init(D, {});
  const O = root.ACCEPTANCE_OPTS || {};
  const out = [];
  const say = (s) => { out.push(s); print(s); };
  const fmt = E.fmt;
  const levels = (O.levels || E.playableLevels.map(l => l.name)).filter(n => E.levelByName[n]);
  const short = (k) => E.towerByKey[k].displayName.replace(' Tower', '').replace('Rocket System', 'Rocket').replace('Bullet Dispenser', 'BD');

  // ------------------------------------------------------------------ 1. upgrade curve (static)
  if (!O.skipStatic) {
    E.setSettings({ level: levels[0], difficulty: 'Medium' });
    say('## Builds: role-weighted value per $ relative to the base tower (target T1 1.1, T2 1.0, T3 0.85)');
    E.towers.filter(t => t.inPalette && t.upgradePaths.length).forEach(t => {
      const rows = E.buildEfficiency(t).filter(r => r.tier > 0);
      const inBand = rows.filter(r => r.efficiency >= r.target * 0.7 && r.efficiency <= r.target * 1.4).length;
      const effs = rows.map(r => r.efficiency).sort((a, b) => a - b);
      say(`  ${t.displayName.padEnd(18)} ${inBand}/${rows.length} builds within ±30-40% of target | min ${fmt(effs[0], 2)}, median ${fmt(effs[effs.length >> 1], 2)}, max ${fmt(effs[effs.length - 1], 2)}`);
    });
    let worse = 0, mismatch = 0;
    E.moduleRows().forEach(r => {
      if (r.dRaw < -1e-9 && r.dCrowd <= 0 && r.dRange <= 0) worse++;
      r.advertised.filter(a => /fire rate/i.test(a.what) && a.percent).forEach(a => { if (isFinite(r.dRate) && Math.abs(r.dRate - a.value / 100) > 0.1 * Math.max(0.1, Math.abs(a.value / 100))) mismatch++; });
    });
    say(`  ${worse} modules lower raw damage without adding crowd damage or range; ${mismatch} fire-rate claims differ from the sustained rate by more than 10% (burst rate vs. magazine/reload)`);
    const clamped = E.models().filter(m => m.cycle.clamped).length;
    say(`  FPS independence: every fire timer carries its remainder; ${clamped} builds hit the ${D.constants.minFireInterval || 0.05} s FIRERATE clamp`);
    say('');
  }

  // ------------------------------------------------------------------ 2. palette-subset sweep per level and difficulty
  const sizes = O.sizes || [1, 2, 3];
  const difficulties = O.difficulties || ['Medium'];
  const monoWins = {};
  difficulties.forEach(diff => {
    levels.forEach(name => {
      E.setSettings({ level: name, difficulty: diff });
      const level = E.levelByName[name];
      const win = E.winRound(level);
      const keys = level.palette.filter(k => E.towerByKey[k]);
      const combos = [];
      const rec = (start, cur) => {
        if (sizes.indexOf(cur.length) >= 0) combos.push(cur.slice());
        if (cur.length >= Math.max(...sizes)) return;
        for (let i = start; i < keys.length; i++) { cur.push(keys[i]); rec(i + 1, cur); cur.pop(); }
      };
      rec(0, []);
      const bySize = {};
      const part = {}; keys.forEach(k => part[k] = 0);
      const monoLost = [];
      combos.forEach(c => {
        const r = E.runAgent({ level, seed: 1, rounds: win, palette: c });
        const n = c.length;
        bySize[n] = bySize[n] || { won: 0, total: 0 };
        bySize[n].total++;
        if (r.survived) { bySize[n].won++; c.forEach(k => part[k]++); }
        if (n === 1) {
          if (r.survived) { (monoWins[c[0]] = monoWins[c[0]] || []).push(name + ' ' + diff); }
          else monoLost.push(short(c[0]) + ' R' + (r.gameOverRound + 1));
        }
      });
      const full = [1, 2, 3].slice(0, O.seeds || 3).map(seed => E.runAgent({ level, seed, rounds: win }));
      say(`## ${name} — ${diff} (win at round ${win})`);
      say('  ' + Object.keys(bySize).map(n => `${n === '1' ? 'single towers' : n === '2' ? 'pairs' : n === '3' ? 'triples' : n + ' towers'} ${bySize[n].won}/${bySize[n].total}`).join(', ') +
          ` | full roster: ${full.map(r => r.survived ? 'won (' + r.lives + ' lives)' : 'lost R' + (r.gameOverRound + 1)).join(', ')}`);
      say('  towers in winning combinations: ' + keys.map(k => short(k) + ' ' + part[k]).join(', '));
      if (monoLost.length) say('  single towers lose at: ' + monoLost.join(', '));
      say('');
    });
  });
  say('## Single-tower wins');
  const anyMono = Object.keys(monoWins);
  if (!anyMono.length) say('  none');
  anyMono.forEach(k => say(`  ${short(k)} wins alone on: ${monoWins[k].join('; ')}`));
  root.ACCEPTANCE_REPORT = out.join('\n');
})(typeof window !== 'undefined' ? window : this);
