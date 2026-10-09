/* 3DTD Balance Lab: views. All numbers come from TDEngine (engine.js); this file only presents them. */
(function () {
  'use strict';
  const { h, clear, status, table } = UI;
  const E = window.TDEngine;
  const D = window.BALANCE_DATA;
  const fmt = (v, d) => E.fmt(v, d);
  const pct = (v, signed) => E.pct(v, signed);
  const money = (v) => (v == null || !isFinite(v) ? (v === Infinity ? '∞' : '–') : '$' + Math.round(v).toLocaleString('en-US'));
  const secs = (v) => (v == null || Number.isNaN(v) ? '–' : !isFinite(v) ? '∞' : v >= 100 ? Math.round(v) + ' s' : v.toFixed(1) + ' s');
  const STORE = 'tdbalance.v1';

  // ------------------------------------------------------------------ prefs
  function loadPrefs() {
    try { return JSON.parse(localStorage.getItem(STORE)) || {}; } catch (e) { return {}; }
  }
  function savePrefs() {
    try { localStorage.setItem(STORE, JSON.stringify({ settings: E.settings, theme: state.theme })); } catch (e) { /* storage blocked */ }
  }

  const prefs = loadPrefs();
  const state = { route: 'overview', param: null, query: {}, theme: prefs.theme || 'auto', filters: {}, simResult: null, compare: null };

  if (!D) {
    document.body.appendChild(h('div', { class: 'empty' }, 'balance-data.js is missing. Run: python3 Tools/BalanceDashboard/extract.py'));
    return;
  }
  E.init(D, prefs.settings || {});
  applyTheme();

  // ------------------------------------------------------------------ colors
  const towerOrder = [];
  (E.playableLevels[0] ? E.playableLevels[0].palette : []).forEach(k => { if (E.towerByKey[k] && !towerOrder.includes(k)) towerOrder.push(k); });
  E.towers.forEach(t => { if (!towerOrder.includes(t.key)) towerOrder.push(t.key); });
  const towerColor = (key) => 'var(--s' + ((towerOrder.indexOf(key) % 8) + 1) + ')';
  const GAME_COLORS = { RED: '#d93a3a', ORANGE: '#f08a24', YELLOW: '#e8c51c', GREEN: '#3fae49', CYAN: '#2bb8c9', BLUE: '#2f6fd6', PURPLE: '#8a4fd1', PINK: '#e86fb0', WHITE: '#f4f4f4', BLACK: '#222222' };
  const palette = () => E.towers.filter(t => t.inPalette).sort((a, b) => towerOrder.indexOf(a.key) - towerOrder.indexOf(b.key));
  const baseLevels = (t) => t.upgradePaths.map(() => 0);
  const baseModel = (t) => E.model(t, baseLevels(t));

  function enemyChip(id, count) {
    const e = E.enemies[id];
    if (!e) return h('span', { class: 'tag', text: 'Id ' + id });
    return h('span', { class: 'tag', title: E.enemyName(id) + ' (Id ' + id + ')' },
      h('span', { class: 'swatch', style: { background: GAME_COLORS[e.color] || '#888', border: '1px solid var(--border-strong)' } }),
      (count != null ? count + '× ' : '') + E.enemyName(id));
  }
  function towerName(t) {
    return h('span', { class: 'nowrap' }, h('span', { class: 'key-dot', style: { background: towerColor(t.key), marginRight: '6px' } }), t.displayName);
  }
  function buildHref(t, levels) { return '#/towers/' + encodeURIComponent(t.key) + (levels && levels.length ? '?b=' + levels.join('-') : ''); }
  function buildLink(m) { return h('a', { href: buildHref(m.tower, m.levels) }, towerName(m.tower), m.levels.length ? h('span', { class: 'muted', text: ' ' + m.levels.join('-') }) : null); }
  function flagSummary(m) {
    const bad = m.flags.filter(f => f.sev === 'bad').length, warn = m.flags.filter(f => f.sev === 'warn').length;
    if (!bad && !warn) return h('span', { class: 'muted', text: '–' });
    return h('span', { title: m.flags.filter(f => f.sev !== 'info').map(f => f.text).join('\n') }, bad ? status('bad', String(bad)) : null, bad && warn ? ' ' : null, warn ? status('warn', String(warn)) : null);
  }
  function soloName(id) { return id == null ? '–' : id < 0 ? 'none' : E.enemyName(id); }

  // ------------------------------------------------------------------ routing
  const ROUTES = [
    ['overview', 'Overview'], ['towers', 'Towers'], ['builds', 'All builds'], ['upgrades', 'Upgrades'],
    ['enemies', 'Enemies'], ['waves', 'Waves & economy'], ['levels', 'Levels & coverage'],
    ['simulator', 'Simulator'], ['compare', 'Opportunity cost'], ['meta', 'Meta & difficulty'], ['telemetry', 'Telemetry'],
    ['warnings', 'Warnings'], ['method', 'Methodology'], ['data', 'Data & export'],
  ];
  function parseHash() {
    const raw = location.hash.replace(/^#\/?/, '');
    const [path, qs] = raw.split('?');
    const parts = path.split('/').map(decodeURIComponent);
    state.route = ROUTES.some(r => r[0] === parts[0]) ? parts[0] : 'overview';
    state.param = parts[1] || null;
    state.query = {};
    (qs || '').split('&').filter(Boolean).forEach(kv => { const [k, v] = kv.split('='); state.query[decodeURIComponent(k)] = decodeURIComponent(v || ''); });
  }
  window.addEventListener('hashchange', () => { parseHash(); renderMain(); renderNav(); window.scrollTo(0, 0); });

  // ------------------------------------------------------------------ shell
  const root = document.getElementById('app');
  const topbar = h('header', { class: 'topbar' });
  const nav = h('nav', { class: 'nav', 'aria-label': 'Sections' });
  const main = h('main', { id: 'main' });
  root.append(topbar, nav, main);

  function applyTheme() {
    if (state.theme === 'auto') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', state.theme);
  }

  function setSetting(patch) {
    E.setSettings(patch);
    savePrefs();
    state.compare = null;
    renderTopbar();
    renderNav();
    renderMain();
  }

  // Veteran mode totals in words, for the Methodology tab
  function metaSummary() {
    const M = E.metaTotalsOf('full');
    if (!M.owned) return 'no meta-upgrade tree in the data';
    return M.owned + ' nodes: +' + M.startMoney + ' start money, +' + M.startLives + ' lives, income ×' + fmt(M.incomeMultiplier, 2) + ', +' + M.waveBonus +
      ' per wave, prices ×' + fmt(M.priceMultiplier, 2) + ', refund +' + Math.round(M.refundBonus * 100) + ' points, ' +
      Object.keys(M.statPercent).map(s => s + ' +' + M.statPercent[s] + '%').join(', ');
  }

  function selectEl(value, options, onchange, attrs) {
    const sel = h('select', attrs || null, options.map(([v, l]) => h('option', { value: v, text: l })));
    sel.value = value == null ? '' : String(value);
    sel.addEventListener('change', () => onchange(sel.value));
    return sel;
  }

  let popover = null;
  function renderTopbar() {
    clear(topbar);
    const meta = D.meta || {};
    topbar.appendChild(h('div', { class: 'brand' }, h('h1', { text: '3DTD Balance Lab' }),
      h('span', { class: 'meta', text: 'data ' + (meta.generatedAt || '').replace('T', ' ') })));
    const st = E.settings;
    const levels = E.levels.map(l => [l.name, l.name + (l.isMainMenu ? ' (menu)' : '')]);
    topbar.appendChild(h('div', { class: 'settings' },
      h('label', null, 'Level', selectEl(st.level, levels, v => setSetting({ level: v }))),
      h('label', null, 'Difficulty', selectEl(st.difficulty || '', [['', 'Level default (' + (E.level() ? E.level().economy.difficulty : '') + ')'], ['Easy', 'Easy'], ['Medium', 'Medium'], ['Hard', 'Hard'], ['Impossible', 'Impossible']], v => setSetting({ difficulty: v || null }))),
      h('label', { title: 'Meta upgrades owned. Fresh: none (default). Veteran: every node of the meta-upgrade tree, applied like MetaUpgrades (tower stat bonuses, start money and lives, income, wave bonus, price discount, refund).' }, 'Meta',
        selectEl(st.meta || 'none', [['none', 'Fresh profile'], ['full', 'Veteran (all upgrades)']], v => setSetting({ meta: v }))),
      h('label', null, 'FPS', selectEl(st.fps, [30, 60, 90, 120, 144, 240].map(f => [f, f + ' fps']), v => setSetting({ fps: +v }))),
      h('label', null, 'Game speed', selectEl(st.gameSpeed, [0.5, 1, 2, 3, 4].map(g => [g, g + '×']), v => setSetting({ gameSpeed: +v }))),
      h('label', { title: 'How overflow damage carries to the next layer. "Current code" is Enemy.cs today; "pre-fix bug" replays the old shape-boundary bug for comparison.' }, 'Layer overflow',
        selectEl(st.overflow, [['intended', 'Current code (carry-over)'], ['coded', 'Pre-fix bug (comparison)']], v => setSetting({ overflow: v }))),
      h('button', { class: 'ghost', onclick: toggleAssumptions, 'aria-expanded': popover ? 'true' : 'false' }, 'Assumptions…'),
      h('span', { style: { flex: 1 } }),
      selectEl(state.theme, [['auto', 'Theme: system'], ['light', 'Theme: light'], ['dark', 'Theme: dark']], v => { state.theme = v; applyTheme(); savePrefs(); }, { 'aria-label': 'Theme' })));
  }

  function toggleAssumptions() {
    if (popover) { popover.remove(); popover = null; return; }
    const st = E.settings;
    const mix = E.levelMix();
    const row = (label, key, placeholder, step) => {
      const inp = h('input', { type: 'number', step: step || 'any', value: st[key] == null ? '' : st[key], placeholder: placeholder || '' });
      inp.addEventListener('change', () => setSetting({ [key]: inp.value === '' ? null : +inp.value }));
      return h('div', { class: 'row' }, h('span', { text: label }), inp);
    };
    popover = h('div', { class: 'popover', role: 'dialog', 'aria-label': 'Model assumptions' },
      h('h3', { text: 'Model assumptions' }),
      h('p', { class: 'small muted', text: 'Values the code cannot determine on its own. Empty fields use the level-derived default shown as placeholder.' }),
      row('Reference shot distance (× detection radius)', 'refDistance', '0.6', 0.05),
      row('Enemy spacing along path (units)', 'spacing', fmt(mix.spacing) + ' from waves', 0.1),
      row('Value of one life ($)', 'lifeValue', fmt(E.lifeValue()) + ' = start money / lives', 0.5),
      row('Starfighter fire-cone duty (0–1)', 'hangarDuty', '0.5', 0.05),
      row('Starfighter attack run (s)', 'hangarRunTime', '4', 0.1),
      row('Starfighter strafe time per run (s)', 'hangarStrafeTime', '1.5', 0.1),
      row('Mine Factory: wave length for salvage (game s)', 'mineWaveSeconds', '30', 1),
      row('Mine Factory: share of a wave the field is full (0–1)', 'mineFieldFullShare', '0.5', 0.05),
      row('Mine Factory: waves of salvage counted as value', 'mineEcoWaves', '8', 1),
      row('Rounds shown after the last wave', 'extraRounds', '10', 1),
      h('div', { class: 'toolbar', style: { marginTop: '10px', marginBottom: 0 } },
        h('button', { onclick: () => { const lvl = E.settings.level; E.settings = Object.assign({}, E.DEFAULT_SETTINGS, { level: lvl }); E._cache.clear(); savePrefs(); popover.remove(); popover = null; setSetting({}); } }, 'Reset all'),
        h('span', { class: 'spacer' }),
        h('button', { onclick: toggleAssumptions }, 'Close')));
    document.body.appendChild(popover);
  }

  function renderNav() {
    clear(nav);
    const warnCount = D.warnings.filter(w => w.severity !== 'info').length;
    ROUTES.forEach(([key, label], i) => {
      if (key === 'simulator' || key === 'warnings') nav.appendChild(h('div', { class: 'sep' }));
      const count = key === 'warnings' ? warnCount : key === 'builds' ? E.models().length : key === 'enemies' ? E.enemies.length : key === 'towers' ? palette().length : null;
      nav.appendChild(h('a', { href: '#/' + key, class: state.route === key ? 'active' : '', 'aria-current': state.route === key ? 'page' : null },
        h('span', { text: label }), count != null ? h('span', { class: 'count', text: String(count) }) : null));
    });
    nav.appendChild(h('div', { class: 'sep' }));
    nav.appendChild(h('div', { class: 'hint', text: 'Level-dependent numbers use ' + E.settings.level + '.' }));
  }

  function renderMain() {
    clear(main);
    UI.hideTip();
    const view = VIEWS[state.route] || VIEWS.overview;
    try { view(main); } catch (err) {
      console.error(err);
      main.appendChild(h('div', { class: 'callout bad' }, 'This view failed to render: ' + err.message));
    }
  }

  function head(title, sub, actions) {
    return h('div', { class: 'view-head' }, h('div', null, h('h2', { text: title }), sub ? h('p', { class: 'sub' }, sub) : null), actions || null);
  }
  function card(title, sub, ...body) {
    return h('div', { class: 'card' }, h('div', { class: 'card-head' }, h('h3', { text: title }), sub ? h('span', { class: 'sub', text: sub }) : null), ...body);
  }
  function chartCard(title, sub, draw) {
    const body = h('div');
    const c = card(title, sub, body);
    requestAnimationFrame(() => { try { draw(body); } catch (e) { console.error(e); body.appendChild(h('div', { class: 'empty', text: 'Chart failed: ' + e.message })); } });
    return c;
  }
  function tile(label, value, note) { return h('div', { class: 'tile' }, h('div', { class: 'label', text: label }), h('div', { class: 'value', text: value }), note ? h('div', { class: 'note', text: note }) : null); }
  function metric(label, value, note) { return h('div', { class: 'metric' }, h('div', { class: 'label', text: label }), h('div', { class: 'value num', text: value }), note ? h('div', { class: 'note', text: note }) : null); }
  function csvButton(name, columns, getRows) {
    return h('button', { onclick: () => UI.download(name, UI.toCSV(columns.filter(c => c.csv !== false), getRows()), 'text/csv') }, 'Export CSV');
  }
  function chipFilter(key, options, initial) {
    if (!state.filters[key]) state.filters[key] = new Set(initial || options.map(o => o[0]));
    const set = state.filters[key];
    const wrap = h('div', { class: 'chips', role: 'group' });
    options.forEach(([val, label, color]) => {
      const chip = h('button', { class: 'chip' + (set.has(val) ? ' on' : ''), 'aria-pressed': set.has(val) ? 'true' : 'false' },
        color ? h('span', { class: 'dot', style: { background: color } }) : null, label);
      chip.addEventListener('click', (ev) => {
        if (ev.altKey || ev.metaKey) { set.clear(); set.add(val); } else if (set.has(val)) set.delete(val); else set.add(val);
        renderMain();
      });
      wrap.appendChild(chip);
    });
    return wrap;
  }

  // ================================================================== column sets
  function buildColumns(opts) {
    opts = opts || {};
    const cols = [
      { key: 'label', label: 'Build', first: true, value: m => m.label, render: m => buildLink(m), csv: m => m.label },
      { key: 'cost', label: 'Cost', n: true, value: m => m.cost, fmt: money, title: 'Base cost plus all upgrades in this build' },
      { key: 'sunk', label: 'Lost on sell', n: true, value: m => m.sunk, fmt: money, title: 'Money lost when selling: everything invested minus the refund (difficulty refund rate × WORTH)' },
      { key: 'kind', label: 'Mode', value: m => m.kind, title: 'Strategy behaviour after strategy-swapping upgrades' },
      { key: 'rate', label: 'Volleys/s', n: true, value: m => m.volleysPerSec, fmt: v => fmt(v, 2), title: 'Measured by replaying the strategy timers frame by frame at the selected FPS and game speed (incl. magazine, reload and pool limits)' },
      { key: 'proj', label: 'Proj./volley', n: true, value: m => m.projectilesPerVolley, fmt: v => fmt(v, 0), title: 'Enabled shooting points (or shots per trigger for sniper/hangar)' },
      { key: 'dmg', label: 'Damage', n: true, value: m => m.stats.DAMAGE, fmt: v => fmt(v, 2) },
      { key: 'raw', label: 'Raw DPS', n: true, heat: 'high', value: m => m.dps.raw, fmt: v => fmt(v, 2), title: 'Single target, every shot hits' },
      { key: 'hit', label: 'Hit %', n: true, value: m => m.hit.level, fmt: v => pct(v), title: 'Share of projectiles that hit, averaged over the best anchor\'s coverage on the selected level and the level\'s HP-weighted enemy speeds. Projectiles fly straight at the enemy\'s current position (no lead).' },
      { key: 'exp', label: 'Exp. DPS', n: true, heat: 'high', value: m => m.dps.expected, fmt: v => fmt(v, 2), title: 'Raw DPS × hit chance' },
      { key: 'crowd', label: 'Crowd DPS', n: true, value: m => m.dps.crowd, fmt: v => fmt(v, 2), title: 'Expected DPS × enemies hit per projectile/pulse in a stream with the configured spacing (pierce, AoE, beams, aura)' },
      { key: 'dpd', label: 'DPS/$100', n: true, heat: 'high', value: m => m.dps.perDollar, fmt: v => fmt(v, 3), title: 'Expected single-target DPS per $100 spent' },
      { key: 'cpd', label: 'Crowd/$100', n: true, value: m => m.dps.crowdPerDollar, fmt: v => fmt(v, 3) },
      { key: 'range', label: 'Range', n: true, value: m => m.range.radius, fmt: v => fmt(v, 2), title: 'World radius of the targetter (hemisphere for most towers, sphere for the Hangar, ray length for the Beam)' },
      { key: 'reach', label: 'Reach', n: true, value: m => m.projectile ? m.projectile.reach : null, fmt: v => v == null ? '–' : fmt(v, 2), title: 'SPEED × LIFETIME: how far a projectile can fly' },
      { key: 'cov', label: 'Coverage', n: true, value: m => m.coverage ? m.coverage.best : null, fmt: v => v == null ? '–' : fmt(v, 1), title: 'Path length inside the detection volume of the best free anchor on the selected level (all lanes)' },
      { key: 'tir', label: 'Time in range', n: true, value: m => m.coverage ? m.coverage.timeInRange : null, fmt: secs, title: 'Coverage ÷ HP-weighted layer speed of the level\'s enemies' },
      { key: 'pass', label: 'Pass dmg', n: true, heat: 'high', value: m => m.coverage ? m.coverage.passDamage : null, fmt: v => fmt(v, 2), title: 'Expected damage one copy deals to a lone enemy walking through its best coverage' },
      { key: 'ppd', label: 'Pass/$100', n: true, heat: 'high', value: m => m.coverage && m.cost ? m.coverage.passDamage / m.cost * 100 : null, fmt: v => fmt(v, 3), title: 'Pass damage per $100: combines range and damage into one value-for-money number' },
      { key: 'solo', label: 'Solo kills up to', value: m => m.coverage ? m.coverage.maxSoloKill : null, sortValue: m => m.coverage ? m.coverage.maxSoloKill : null, render: m => m.coverage && m.coverage.maxSoloKill >= 0 ? enemyChip(m.coverage.maxSoloKill) : h('span', { class: 'muted', text: 'nothing' }), csv: m => m.coverage ? soloName(m.coverage.maxSoloKill) : '', title: 'Strongest enemy one copy pops completely during a single pass of its best coverage' },
      { key: 'payback', label: 'Payback', n: true, heat: 'low', value: m => m.paybackSec, fmt: secs, title: 'Seconds of saturated firing until pop income (1 per layer) repays the cost, using the level\'s money per HP' },
      { key: 'fps', label: 'FPS swing', n: true, value: m => m.fpsSwing, fmt: v => '×' + fmt(v, 2), title: 'Volley rate at 144 fps ÷ rate at 30 fps. 1 = frame-rate independent' },
      { key: 'waste', label: 'Overkill', n: true, value: m => m.overkill ? m.overkill.wasteIntended : null, fmt: v => pct(v), title: 'Share of damage wasted on already-dead enemies against the level\'s enemy mix (correct carry-over)' },
      { key: 'bug', label: 'Pre-fix Δhits', n: true, value: m => m.overkill ? m.overkill.extraHitsCoded : null, fmt: v => v == null ? '–' : pct(v, true), title: 'Extra hits the old overflow bug in Enemy.cs needed (fixed; kept for comparison)' },
      { key: 'flags', label: 'Flags', value: m => m.flags.filter(f => f.sev === 'bad').length * 10 + m.flags.filter(f => f.sev === 'warn').length, render: flagSummary, csv: m => m.flags.filter(f => f.sev !== 'info').map(f => f.text).join(' | ') },
    ];
    return opts.only ? cols.filter(c => opts.only.includes(c.key)) : cols;
  }

  // ================================================================== views
  const VIEWS = {};

  // ------------------------------------------------------------------ overview
  VIEWS.overview = function (el) {
    const level = E.level();
    const eco = E.economyOf(level);
    const models = E.models();
    const mods = E.moduleRows();
    const W = level.spawner ? level.spawner.waves.length : 0;
    const errs = D.warnings.filter(w => w.severity === 'error').length, warns = D.warnings.filter(w => w.severity === 'warning').length;
    el.appendChild(head('Overview',
      ['Everything here is computed from the Unity project data (', h('code', { text: 'balance-data.json' }), ') with models that replay the C# logic. Level-dependent numbers use ', h('b', { text: level.name }), '. Hover any column header or chart for details.']));
    el.appendChild(h('div', { class: 'tiles' },
      tile('Buildable towers', String(palette().length), 'plus building block ' + money((D.blocks[0] || {}).cost)),
      tile('Reachable builds', String(models.length), 'upgrade combos allowed by path rules'),
      tile('Upgrade modules', String(mods.length)),
      tile('Enemy types', String(E.enemies.length), '5 shapes × 10 colors + boss'),
      tile('Waves on this level', String(W), '+ scaled repeats after that'),
      tile('Start money', money(eco.money), eco.difficulty + ' difficulty'),
      tile('Start lives', String(eco.lives), 'leak cost = enemy Id + 1'),
      tile('Data issues', errs + (errs === 1 ? ' error' : ' errors'), warns + (warns === 1 ? ' warning' : ' warnings'))));

    const f = card('Key findings', 'auto-generated from the current settings');
    const list = h('div', { class: 'findings' });
    E.insights().forEach(i => list.appendChild(h('div', { class: 'finding' }, status(i.sev), h('span', { class: 'area', text: i.area }), h('span', { text: i.text }))));
    f.appendChild(list);
    el.appendChild(h('div', { class: 'section' }, f));

    const bases = palette().map(baseModel);
    const g1 = h('div', { class: 'grid cols-2 section' });
    g1.appendChild(chartCard('Expected single-target DPS per $100', 'base towers, ' + level.name, (c) => UI.barChart(c, {
      items: bases.slice().sort((a, b) => b.dps.perDollar - a.dps.perDollar).map(m => ({ label: m.tower.displayName, value: m.dps.perDollar, color: towerColor(m.tower.key),
        tip: { title: m.tower.displayName, rows: [{ key: 'DPS/$100', value: fmt(m.dps.perDollar, 3), color: towerColor(m.tower.key) }, { key: 'Expected DPS', value: fmt(m.dps.expected, 3) }, { key: 'Hit chance', value: pct(m.hit.level) }, { key: 'Cost', value: money(m.cost) }] },
        onClick: () => { location.hash = buildHref(m.tower); } })),
      valueFmt: v => fmt(v, 3) })));
    g1.appendChild(chartCard('Damage per enemy pass per $100', 'base towers on their best anchor', (c) => UI.barChart(c, {
      items: bases.filter(m => m.coverage).sort((a, b) => b.coverage.passDamage / b.cost - a.coverage.passDamage / a.cost).map(m => ({ label: m.tower.displayName, value: m.coverage.passDamage / m.cost * 100, color: towerColor(m.tower.key),
        tip: { title: m.tower.displayName, rows: [{ key: 'Pass dmg/$100', value: fmt(m.coverage.passDamage / m.cost * 100, 3), color: towerColor(m.tower.key) }, { key: 'Coverage', value: fmt(m.coverage.best, 1) + ' units' }, { key: 'Time in range', value: secs(m.coverage.timeInRange) }, { key: 'Solo kills up to', value: soloName(m.coverage.maxSoloKill) }] } })),
      valueFmt: v => fmt(v, 2) })));
    el.appendChild(g1);

    const g2 = h('div', { class: 'grid cols-2 section' });
    g2.appendChild(chartCard('DPS the waves demand vs DPS money can buy', 'per round, no leaks assumed', (c) => drawDemandChart(c, level)));
    g2.appendChild(chartCard('Best upgrades by pass damage per $100', 'each module vs one tier lower', (c) => UI.barChart(c, {
      items: mods.filter(r => isFinite(r.passPer100)).sort((a, b) => b.passPer100 - a.passPer100).slice(0, 12).map(r => ({
        label: r.tower.displayName + ': ' + r.module.name, value: r.passPer100, color: towerColor(r.tower.key),
        tip: { title: r.tower.displayName + ' · ' + r.module.name, rows: [{ key: 'Pass dmg/$100', value: fmt(r.passPer100, 3), color: towerColor(r.tower.key) }, { key: 'Price', value: money(r.price) }, { key: 'Δ expected DPS', value: fmt(r.dDPS, 3) }, { key: 'Path / tier', value: (r.path + 1) + ' / ' + (r.tier + 1) }] } })),
      valueFmt: v => fmt(v, 2), labelWidth: 260 })));
    el.appendChild(g2);
  };

  function drawDemandChart(c, level) {
    const rows = E.levelWaveTable(level).filter(r => !r.missing);
    const bases = palette().map(baseModel);
    const allModels = E.models();
    const bestBase = Math.max(...bases.map(m => m.dps.perDollar / 100));
    const pts = (f) => rows.map(r => ({ x: r.round + 1, y: f(r) }));
    UI.lineChart(c, {
      series: [
        { name: 'Stream DPS needed', color: 'var(--s2)', values: pts(r => r.streamDPS) },
        { name: 'Avg DPS needed', color: 'var(--s4)', values: pts(r => r.avgDPS) },
        { name: 'Money × best base DPS/$', color: 'var(--s1)', values: pts(r => r.moneyAtStart * bestBase) },
        { name: 'Best affordable build', color: 'var(--s3)', values: pts(r => Math.max(0, ...allModels.filter(m => m.cost <= r.moneyAtStart).map(m => m.dps.expected * Math.floor(r.moneyAtStart / m.cost)))) },
      ],
      xLabel: 'Round', yLabel: 'DPS', xFmt: v => String(Math.round(v)), xTipLabel: 'Round ', height: 280,
    });
    c.appendChild(h('p', { class: 'small muted', style: { marginTop: '6px' }, text: 'Stream DPS: highest HP-per-second of any sustained entry (≥3 enemies) in the wave. Avg DPS: total HP ÷ time until the last enemy would reach the end. Affordable lines assume all money banked so far is spent at once, with zero leaks.' }));
  }

  // ------------------------------------------------------------------ towers
  VIEWS.towers = function (el) {
    const t = state.param && E.towerByKey[state.param];
    if (t) return towerDetail(el, t);
    el.appendChild(head('Towers', 'Base towers in the build palette. Click a tower to explore its upgrade tree, fire cycle, hit chances and coverage.'));
    const towers = E.towers.slice().sort((a, b) => towerOrder.indexOf(a.key) - towerOrder.indexOf(b.key));
    const rows = towers.map(baseModel);
    const cols = buildColumns({ only: ['label', 'cost', 'kind', 'rate', 'proj', 'dmg', 'range', 'reach', 'raw', 'hit', 'exp', 'crowd', 'dpd', 'cov', 'pass', 'ppd', 'solo', 'payback', 'flags'] });
    cols[0] = { key: 'label', label: 'Tower', first: true, value: m => m.tower.displayName, render: m => h('span', null, h('a', { href: buildHref(m.tower) }, towerName(m.tower)), m.tower.inPalette ? null : h('span', { class: 'tag', style: { marginLeft: '6px' }, text: 'not buildable' })) };
    el.appendChild(table({ id: 'towers', columns: cols, rows, sort: { key: 'ppd', dir: 'desc' }, onRowClick: m => { location.hash = buildHref(m.tower); } }));
    // best build per tower
    const g = h('div', { class: 'grid cols-2 section' });
    g.appendChild(chartCard('Best build per tower by pass damage per $100', 'any reachable upgrade combination', (c) => {
      const items = palette().map(t2 => {
        const best = E.allBuildLevels(t2).map(lv => E.model(t2, lv)).filter(m => m.coverage).sort((a, b) => b.coverage.passDamage / b.cost - a.coverage.passDamage / a.cost)[0];
        return best ? { label: best.label, value: best.coverage.passDamage / best.cost * 100, color: towerColor(t2.key), onClick: () => { location.hash = buildHref(t2, best.levels); } } : null;
      }).filter(Boolean).sort((a, b) => b.value - a.value);
      UI.barChart(c, { items, valueFmt: v => fmt(v, 2), labelWidth: 220 });
    }));
    g.appendChild(chartCard('Strongest build per tower (expected DPS)', 'regardless of cost', (c) => {
      const items = palette().map(t2 => {
        const best = E.allBuildLevels(t2).map(lv => E.model(t2, lv)).sort((a, b) => b.dps.expected - a.dps.expected)[0];
        return { label: best.label, value: best.dps.expected, color: towerColor(t2.key), tip: { title: best.label, rows: [{ key: 'Expected DPS', value: fmt(best.dps.expected, 2), color: towerColor(t2.key) }, { key: 'Cost', value: money(best.cost) }, { key: 'Frame-limited', value: best.cycle.frameLimited ? 'yes' : 'no' }] }, onClick: () => { location.hash = buildHref(t2, best.levels); } };
      }).sort((a, b) => b.value - a.value);
      UI.barChart(c, { items, valueFmt: v => fmt(v, 1), labelWidth: 220 });
    }));
    el.appendChild(g);
  };

  function levelsFromQuery(t) {
    const q = state.query.b;
    const lv = q ? q.split('-').map(Number) : baseLevels(t);
    if (lv.length !== t.upgradePaths.length || lv.some(x => !(x >= 0))) return baseLevels(t);
    return lv;
  }
  function validLevels(lv) { return lv.filter(x => x >= 1).length <= 2 && lv.filter(x => x >= 3).length <= 1; }

  function towerDetail(el, t) {
    const lv = levelsFromQuery(t);
    const m = E.model(t, lv);
    const base = baseModel(t);
    const setLv = (nlv) => { location.hash = buildHref(t, nlv); };
    el.appendChild(head(t.displayName + (lv.length ? ' ' + lv.join('-') : ''),
      [t.description || '', h('br'), h('span', { class: 'small muted' }, h('code', { text: t.prefab }), ' · ', h('code', { text: t.statsConfig.asset }), ' · ', t.strategy.type)],
      h('a', { href: '#/towers', class: 'btn' }, '← All towers')));

    // upgrade grid
    if (t.upgradePaths.length) {
      const grid = h('div', { class: 'upgrade-grid' });
      const rows = E.moduleRows([t]);
      t.upgradePaths.forEach((path, p) => {
        const col = h('div', { class: 'upath' }, h('h4', { text: 'Path ' + (p + 1) }));
        path.forEach((mod, ti) => {
          const on = lv[p] > ti;
          const next = lv.slice(); next[p] = on ? ti : ti + 1;
          const allowed = validLevels(next);
          const r = rows.find(x => x.path === p && x.tier === ti);
          const effects = mod.statUpgrades.map(su => h('span', { class: 'tag ' + (statGood(su) ? 'up' : 'down'), text: `${su.stat} ${su.value > 0 ? '+' : ''}${fmt(su.value, 2)}${su.isModifier ? '%' : ''}` }));
          mod.behaviours.forEach(bh => effects.push(h('span', { class: 'tag', text: behaviourLabel(bh) })));
          const shots = mod.activates.filter(a => t.shootingPoints.some(sp => sp.ref === a.go)).length;
          const c = h('div', { class: 'ucard' + (on ? ' on' : '') + (!allowed ? ' blocked' : ''), tabindex: 0, role: 'button', 'aria-pressed': on ? 'true' : 'false',
            title: mod.description + (allowed ? '' : '\n\nBlocked by the path rules (max two paths, one tier 3).') },
          h('div', { class: 'uhead' }, h('span', { text: 'T' + (ti + 1) + ' ' + mod.name }), h('span', { class: 'uprice', text: money(mod.price) })),
          h('div', { class: 'ueff' }, effects, shots ? h('span', { class: 'tag', text: 'enables shooting points' }) : null),
          r ? h('div', { class: 'udelta' }, `Δ exp. DPS ${r.dDPS >= 0 ? '+' : ''}${fmt(r.dDPS, 3)} · Δ pass ${r.dPass >= 0 ? '+' : ''}${fmt(r.dPass, 2)} · ${fmt(r.passPer100, 3)} pass/$100`) : null);
          const act = () => { if (allowed) setLv(next); };
          c.addEventListener('click', act);
          c.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); act(); } });
          col.appendChild(c);
        });
        grid.appendChild(col);
      });
      el.appendChild(card('Upgrade tree', 'click a tier to build up to it; deltas compare each tier with the one below (other paths at 0)', grid));
    }

    // metrics
    const mg = h('div', { class: 'metric-grid' },
      metric('Total cost', money(m.cost), m.sunk ? money(m.sunk) + ' lost when sold' : 'refund = full cost'),
      metric('Volleys per second', fmt(m.volleysPerSec, 3), m.cycle.frameLimited ? 'frame-limited!' : 'interval ' + fmt(m.stats.FIRERATE, 3) + ' s'),
      metric('Raw DPS', fmt(m.dps.raw, 3), 'all shots hit one target'),
      metric('Hit chance', pct(m.hit.level), 'red ' + pct(m.hit.red) + ' · pink ' + pct(m.hit.pink)),
      metric('Expected DPS', fmt(m.dps.expected, 3), fmt(m.dps.perDollar, 3) + ' per $100'),
      metric('Crowd DPS', fmt(m.dps.crowd, 3), '× ' + fmt(m.crowdTargets, 2) + ' targets per hit'),
      metric('Range', fmt(m.range.radius, 2), m.range.shape + (m.projectile ? ' · reach ' + fmt(m.projectile.reach, 2) : '')),
      m.coverage ? metric('Best coverage', fmt(m.coverage.best, 1) + ' u', m.coverage.useful + ' of ' + m.coverage.free + ' anchors reach the path') : null,
      m.coverage ? metric('Damage per pass', fmt(m.coverage.passDamage, 2), 'red ' + fmt(m.coverage.passDamageRed, 1) + ' · pink ' + fmt(m.coverage.passDamagePink, 1)) : null,
      m.coverage ? metric('Solo kills up to', soloName(m.coverage.maxSoloKill), 'one copy, one pass') : null,
      metric('Payback', secs(m.paybackSec), 'saturated pop income'),
      metric('FPS dependence', '×' + fmt(m.fpsSwing, 2), m.fpsSensitivity.map(x => x.fps + 'fps ' + fmt(x.rate, 2)).join(' · ')),
      m.pool ? metric('Pulse pool', m.pool.error ? 'broken' : String(m.pool.size), m.pool.capVolleysPerSec ? 'caps at ' + fmt(m.pool.capVolleysPerSec, 2) + ' volleys/s' : '') : null,
      m.hangar ? metric('Starfighters', String(m.hangar.fighters), (m.hangar.twin ? 'twin cannons · ' : '') + m.hangar.missilesPerRun + ' missiles/run' + (m.hangar.carpet ? ' · carpet bombs' : '')) : null,
      m.overkill ? metric('Overkill waste', pct(m.overkill.wasteIntended), 'bug: ' + pct(m.overkill.extraHitsCoded, true) + ' hits, ' + pct(m.overkill.moneyDeltaCoded, true) + ' money') : null);
    el.appendChild(h('div', { class: 'section' }, card('This build', E.settings.fps + ' fps · ' + E.settings.gameSpeed + '× speed · ' + E.settings.level, mg)));

    if (m.flags.length) {
      const fl = h('div', { class: 'flags' });
      m.flags.forEach(f => fl.appendChild(h('div', { class: 'flag' }, status(f.sev), h('span', { text: f.text }))));
      el.appendChild(h('div', { class: 'section' }, card('Flags', null, fl)));
    }

    // stats sheet + charts
    const g = h('div', { class: 'grid cols-2 section' });
    const read = new Set((D.constants.statsRead[m.strategy.type] || []).concat(['RANGE']));
    const statRows = D.constants.statTypes.map(sname => {
      const changes = m.cfg.mods.flatMap(mod => mod.statUpgrades.filter(su => su.stat === sname).map(su => `${su.value > 0 ? '+' : ''}${fmt(su.value, 2)}${su.isModifier ? '%' : ''} ${mod.name}`));
      return { stat: sname, base: t.statsConfig.stats[sname], now: m.stats[sname], read: read.has(sname), changes: changes.join(', ') };
    }).filter(r => r.read || r.base || r.now || r.changes);
    g.appendChild(card('Stats', 'StatsConfig base → this build (bonuses added, then % modifiers compounded)', table({
      columns: [
        { key: 'stat', label: 'Stat', render: r => h('code', { text: r.stat }) },
        { key: 'base', label: 'Base', n: true, fmt: v => fmt(v, 3) },
        { key: 'now', label: 'Build', n: true, fmt: v => fmt(v, 3) },
        { key: 'read', label: 'Used?', value: r => (r.read ? 1 : 0), render: r => r.read ? status('good', 'read') : status('warn', 'unused'), title: 'Whether the action strategy (or its projectile) reads this stat' },
        { key: 'changes', label: 'Changed by', render: r => h('span', { class: 'small', text: r.changes || '–' }) },
      ], rows: statRows, tall: true })));
    g.appendChild(chartCard('Fire timeline, first 20 seconds', 'target always in range; shaded = reloading', (c) => {
      const lanes = [{ name: 'Base', color: towerColor(t.key), m: base }];
      if (m !== base) lanes.push({ name: 'This build', color: towerColor(t.key), m });
      UI.timeline(c, { seconds: 20, lanes: lanes.map(l => Object.assign({ name: l.name, color: l.color }, E.fireTimes(l.m, 20))) });
      c.appendChild(h('p', { class: 'small muted', text: `Magazine ${m.cycle.magazine == null ? '–' : fmt(m.cycle.magazine, 2)} · reload ${m.cycle.reload == null ? '–' : fmt(m.cycle.reload, 2) + ' s'} · first volley after ${secs(m.cycle.first)} (placement)` }));
    }));
    el.appendChild(g);

    const g2 = h('div', { class: 'grid cols-2 section' });
    if (m.projectile && !m.projectile.homing && m.kind !== 'hangar') {
      g2.appendChild(chartCard('Hit chance vs distance', 'straight projectile, isotropic crossing angles', (c) => {
        const P = m.projectile;
        const speeds = [[1.25, 'Red (1.25)'], [2, 'Green (2)'], [3, 'Purple/White (3)'], [4, 'Pink (4)']];
        const maxD = Math.max(1, m.range.detect);
        const series = speeds.map(([v, name], i) => ({ name, color: 'var(--s' + (i + 1) + ')', values: Array.from({ length: 25 }, (_, k) => { const d = 0.2 + k * (maxD - 0.2) / 24; return { x: d, y: E.hitChanceIsotropic(d, v, P.speed, P.lifetime, P.rHit, P.spreadDeg) }; }) }));
        UI.lineChart(c, { series, xLabel: 'Distance to target (units)', yLabel: 'Hit chance', yFmt: v => Math.round(v * 100) + '%', xFmt: v => fmt(v, 1), yMax: 1, yMin: 0, height: 240 });
        c.appendChild(h('p', { class: 'small muted', text: `Projectile speed ${fmt(P.speed, 2)}, lifetime ${fmt(P.lifetime, 2)} s, hit radius ${fmt(P.rHit, 3)}, spread ±${fmt(P.spreadDeg, 1)}°. Towers aim at the current position, so fast crossing enemies are missed.` }));
      }));
    } else {
      g2.appendChild(card('Hit chance', null, h('p', { class: 'sub', text: m.kind === 'hangar' ? 'Starfighter cannons fire from ~2 units during strafing runs; homing ordnance always connects.' : m.projectile && m.projectile.homing ? 'Homing projectile: hits whenever it can catch the target within its lifetime.' : 'Hitscan / area effect: no projectile to miss.' })));
    }
    g2.appendChild(card('Frame-rate and game-speed sensitivity', 'volleys per game second', table({
      columns: [{ key: 'k', label: 'Setting' }, { key: 'rate', label: 'Volleys/s', n: true, fmt: v => fmt(v, 3) }, { key: 'rel', label: 'vs 60 fps / 1×', n: true, fmt: v => pct(v, true) }],
      rows: m.fpsSensitivity.map(x => ({ k: x.fps + ' fps', rate: x.rate, rel: x.rate / m.fpsSensitivity[1].rate - 1 }))
        .concat(m.speedSensitivity.map(x => ({ k: x.gameSpeed + '× game speed', rate: x.rate, rel: x.rate / m.speedSensitivity[0].rate - 1 }))), tall: true })));
    el.appendChild(g2);

    // coverage per level
    const lvRows = E.playableLevels.map(L => {
      const lc = E.levelCoverage(m, L);
      const mix = E.levelMix(L);
      return { level: L.name, best: lc.bestLength, median: lc.medianUseful, useful: lc.usefulAnchors, free: lc.freeAnchors,
               tRed: lc.bestLength / 1.25, tPink: lc.bestLength / 4, tMix: lc.bestLength / mix.meanLayerSpeed };
    });
    el.appendChild(h('div', { class: 'section' }, card('Coverage on every level', 'best and median free anchor for this build', table({
      columns: [
        { key: 'level', label: 'Level', render: r => h('a', { href: '#/levels', onclick: () => setSetting({ level: r.level }) }, r.level) },
        { key: 'useful', label: 'Useful anchors', n: true, value: r => r.useful, fmt: (v, r) => v + ' / ' + r.free },
        { key: 'best', label: 'Best coverage', n: true, fmt: v => fmt(v, 1) },
        { key: 'median', label: 'Median (useful)', n: true, fmt: v => fmt(v, 1) },
        { key: 'tRed', label: 'Time in range (red)', n: true, fmt: secs },
        { key: 'tPink', label: 'Time in range (pink)', n: true, fmt: secs },
        { key: 'tMix', label: 'Time in range (level mix)', n: true, fmt: secs },
      ], rows: lvRows, tall: true }))));

    // all builds of this tower
    const all = E.allBuildLevels(t).map(l => E.model(t, l));
    el.appendChild(h('div', { class: 'section' }, card('All ' + all.length + ' reachable builds of ' + t.displayName, 'click to select', table({
      id: 'tower-builds', columns: buildColumns({ only: ['label', 'cost', 'sunk', 'kind', 'rate', 'proj', 'dmg', 'raw', 'hit', 'exp', 'crowd', 'dpd', 'range', 'pass', 'ppd', 'solo', 'fps', 'bug', 'flags'] }),
      rows: all, sort: { key: 'ppd', dir: 'desc' }, onRowClick: r => setLv(r.levels), selected: r => r.levels.join() === lv.join(), maxHeight: '60vh' }))));
  }

  // Lower is better for the timer stats (seconds between shots / reload time)
  function statGood(su) { return ['FIRERATE', 'RELOAD_SPEED'].includes(su.stat) ? su.value <= 0 : su.value >= 0; }

  function behaviourLabel(bh) {
    switch (bh.type) {
      case 'ChangeActionStrategyUpgrade': {
        const st = bh.strategy || {};
        const mode = st.AuraMode ? 'aura mode' : st.PulseMode ? 'pulse mode' : st.doClustering ? 'cluster rockets' : st.secondShotStrongTargetting ? (st.thirdShotLastTargetting ? '+strongest +last shots' : '+strongest shot') : st.type;
        return 'strategy → ' + mode;
      }
      case 'BombTowerLightAimUpgrade': return 'homing rockets';
      case 'StarfighterEngineUpgrade': return `flight ×${bh.flightSpeedMultiplier}, turn ×${bh.turnRateMultiplier}`;
      case 'StarfighterLoadoutUpgrade': return [bh.twinLinkedCannons ? 'twin cannons' : null, bh.additionalMissilesPerRun ? '+' + bh.additionalMissilesPerRun + ' missiles/run' : null, bh.carpetBombing ? 'carpet bombing' : null].filter(Boolean).join(', ');
      case 'ChangeFirepointsUpgrade': case 'TripleShotUpgrade': return 'shooting points +' + (bh.activates || 0) + (bh.deactivates ? ' −' + bh.deactivates : '');
      default: return bh.type;
    }
  }

  // ------------------------------------------------------------------ builds
  VIEWS.builds = function (el) {
    const all = E.models();
    const towers = palette();
    el.appendChild(head('All builds', 'Every upgrade combination the path rules allow (two paths, at most one at tier 3), with the full performance model. Sort by any column; hover headers for definitions.'));
    const tb = h('div', { class: 'toolbar' });
    tb.appendChild(chipFilter('builds.towers', towers.map(t => [t.key, t.displayName, towerColor(t.key)])));
    const q = h('input', { type: 'search', placeholder: 'Filter by name or levels (e.g. 3-0-1)', value: state.filters.buildsQ || '', style: { width: '230px' } });
    const maxCost = h('input', { type: 'number', placeholder: 'max $', value: state.filters.buildsMax || '' });
    const flagSel = selectEl(state.filters.buildsFlag || '', [['', 'All builds'], ['flagged', 'With problems or warnings'], ['frame', 'Frame-rate limited'], ['clean', 'No problems']], v => { state.filters.buildsFlag = v; renderMain(); });
    q.addEventListener('input', () => { state.filters.buildsQ = q.value; drawTable(); });
    maxCost.addEventListener('change', () => { state.filters.buildsMax = maxCost.value; drawTable(); });
    tb.append(q, maxCost, flagSel, h('span', { class: 'spacer' }));
    const cols = buildColumns();
    const filtered = () => {
      const set = state.filters['builds.towers'];
      const qq = (state.filters.buildsQ || '').toLowerCase();
      const mc = +state.filters.buildsMax || Infinity;
      const ff = state.filters.buildsFlag;
      return all.filter(m => set.has(m.tower.key) && m.cost <= mc && (!qq || m.label.toLowerCase().includes(qq)) &&
        (!ff || (ff === 'flagged' ? m.flags.some(f => f.sev !== 'info') : ff === 'frame' ? m.cycle.frameLimited : !m.flags.some(f => f.sev !== 'info'))));
    };
    tb.appendChild(csvButton('3dtd-builds.csv', cols, () => (holder.firstChild && holder.firstChild.getRows ? holder.firstChild.getRows() : filtered())));
    el.appendChild(tb);
    const holder = h('div');
    el.appendChild(holder);
    function drawTable() {
      clear(holder);
      const rows = filtered();
      holder.appendChild(table({ id: 'builds', columns: cols, rows, sort: { key: 'ppd', dir: 'desc' }, onRowClick: m => { location.hash = buildHref(m.tower, m.levels); }, maxHeight: '64vh' }));
      holder.appendChild(h('div', { class: 'table-foot' }, h('span', { text: rows.length + ' of ' + all.length + ' builds' }), h('span', { text: 'Alt/⌘-click a tower chip to show only that tower' })));
    }
    drawTable();
    el.appendChild(h('div', { class: 'section' }, chartCard('Cost vs damage per enemy pass', 'one panel per tower, each with its own scale; each dot is a build', (c) => UI.smallMultiples(c, {
      panels: towers.filter(t => state.filters['builds.towers'].has(t.key)).map(t => ({ title: t.displayName, color: towerColor(t.key),
        points: all.filter(m => m.tower === t && m.coverage).map(m => ({ x: m.cost, y: m.coverage.passDamage, label: m.label, onClick: () => { location.hash = buildHref(t, m.levels); },
          tip: { title: m.label, rows: [{ key: 'Cost', value: money(m.cost) }, { key: 'Pass dmg', value: fmt(m.coverage.passDamage, 2), color: towerColor(t.key) }, { key: 'Expected DPS', value: fmt(m.dps.expected, 2) }] } })) })),
      xLabel: 'Cost ($)', yLabel: 'Damage per pass', xFmt: UI.compact,
    }))));
  };

  // ------------------------------------------------------------------ upgrades
  VIEWS.upgrades = function (el) {
    const towers = palette();
    const all = E.moduleRows();
    el.appendChild(head('Upgrades', 'Each module compared to the same tower one tier lower on that path (other paths at 0). "Advertised" is parsed from the in-game description, so you can compare claims with the modelled effect.'));
    const tb = h('div', { class: 'toolbar' });
    tb.appendChild(chipFilter('upgrades.towers', towers.map(t => [t.key, t.displayName, towerColor(t.key)])));
    tb.appendChild(h('span', { class: 'spacer' }));
    const rows = all.filter(r => state.filters['upgrades.towers'].has(r.tower.key));
    const cols = [
      { key: 'tower', label: 'Tower', first: true, value: r => r.tower.displayName, render: r => h('a', { href: buildHref(r.tower, r.tower.upgradePaths.map((_, i) => (i === r.path ? r.tier + 1 : 0))) }, towerName(r.tower)) },
      { key: 'pt', label: 'Path/Tier', value: r => r.path * 10 + r.tier, fmt: (v, r) => (r.path + 1) + ' / T' + (r.tier + 1) },
      { key: 'name', label: 'Module', value: r => r.module.name, render: r => h('span', { title: r.module.description, text: r.module.name }) },
      { key: 'price', label: 'Price', n: true, fmt: money },
      { key: 'cumulative', label: 'Path total', n: true, fmt: money, title: 'Sum of this path up to and including this tier' },
      { key: 'effects', label: 'Effects', sortable: false, render: r => h('span', { class: 'chips' }, r.module.statUpgrades.map(su => h('span', { class: 'tag ' + (statGood(su) ? 'up' : 'down'), text: `${su.stat} ${su.value > 0 ? '+' : ''}${fmt(su.value, 2)}${su.isModifier ? '%' : ''}` })), r.module.behaviours.map(b => h('span', { class: 'tag', text: behaviourLabel(b) }))), csv: r => r.module.statUpgrades.map(su => `${su.stat} ${su.value}${su.isModifier ? '%' : ''}`).concat(r.module.behaviours.map(behaviourLabel)).join('; ') },
      { key: 'adv', label: 'Advertised', sortable: false, render: r => h('span', { class: 'small', style: { display: 'inline-block', minWidth: '190px' }, text: r.advertised.map(a => `${a.value > 0 ? '+' : ''}${a.value}${a.percent ? '%' : ''} ${a.what}`).join(', ') || '–' }), csv: r => r.advertised.map(a => `${a.value}${a.percent ? '%' : ''} ${a.what}`).join('; ') },
      { key: 'dRate', label: 'Δ volley rate', n: true, value: r => r.dRate, fmt: v => pct(v, true), title: 'Measured change of volleys per second (frame-accurate)' },
      { key: 'dRaw', label: 'Δ raw DPS', n: true, fmt: v => fmt(v, 3) },
      { key: 'dDPS', label: 'Δ exp. DPS', n: true, heat: 'high', fmt: v => fmt(v, 3) },
      { key: 'dCrowd', label: 'Δ crowd DPS', n: true, fmt: v => fmt(v, 3) },
      { key: 'dRange', label: 'Δ range', n: true, fmt: v => fmt(v, 2) },
      { key: 'dPass', label: 'Δ pass dmg', n: true, heat: 'high', fmt: v => fmt(v, 2) },
      { key: 'passPer100', label: 'Pass/$100', n: true, heat: 'high', fmt: v => fmt(v, 3), title: 'Δ damage per enemy pass per $100 of this module' },
      { key: 'dpsPer100', label: 'DPS/$100', n: true, fmt: v => fmt(v, 3) },
      { key: 'payback', label: 'Payback', n: true, fmt: secs, title: 'Seconds of saturated firing until the extra pop income repays the module' },
      { key: 'flags', label: 'Flags', value: r => r.after.flags.filter(f => f.sev !== 'info').length, render: r => flagSummary(r.after), csv: r => r.after.flags.map(f => f.text).join(' | ') },
    ];
    tb.appendChild(csvButton('3dtd-upgrades.csv', cols, () => rows));
    el.appendChild(tb);
    el.appendChild(table({ id: 'upgrades', columns: cols, rows, sort: { key: 'passPer100', dir: 'desc' }, maxHeight: '62vh' }));
    const g = h('div', { class: 'grid cols-2 section' });
    g.appendChild(chartCard('Pass damage gained per $100', 'top 15 modules', (c) => UI.barChart(c, {
      items: rows.filter(r => isFinite(r.passPer100)).sort((a, b) => b.passPer100 - a.passPer100).slice(0, 15).map(r => ({ label: r.tower.displayName + ': ' + r.module.name, value: r.passPer100, color: towerColor(r.tower.key) })),
      valueFmt: v => fmt(v, 2), labelWidth: 260 })));
    g.appendChild(chartCard('Worst value for money', 'bottom 15 modules by pass damage per $100', (c) => UI.barChart(c, {
      items: rows.filter(r => isFinite(r.passPer100)).sort((a, b) => a.passPer100 - b.passPer100).slice(0, 15).map(r => ({ label: r.tower.displayName + ': ' + r.module.name, value: r.passPer100, color: towerColor(r.tower.key) })),
      valueFmt: v => fmt(v, 3), labelWidth: 260 })));
    el.appendChild(g);
  };

  // ------------------------------------------------------------------ enemies
  VIEWS.enemies = function (el) {
    const level = E.level();
    const len = level.lanes.length ? level.lanes[0].length : 0;
    const mix = E.levelMix(level);
    el.appendChild(head('Enemies', ['Bloons-style layers: popping a layer pays 1 and drops one color; after RED the shape drops to the previous shape\'s BLACK. Traversal times use ', h('b', { text: level.name }), ' (lane 1, ' + fmt(len, 1) + ' units).']));
    const rows = E.enemies.map(e => {
      const layers = E.enemyLayers(e.id);
      const hp = E.enemyTotalHP(e.id);
      const walk = len / e.speed;
      const minWalk = len / Math.max(...layers.map(l => l.speed));
      const strippedWalk = sum(layers, l => l.health / l.speed) / Math.max(1e-9, hp) * len;
      return { e, id: e.id, name: E.enemyName(e.id), shape: e.shape, color: e.color, hp: e.health, speed: e.speed, total: hp, layers: layers.length,
        money: E.enemyMoney(e.id), mph: hp ? E.enemyMoney(e.id) / hp : 0, lives: E.enemyLivesCost(e.id), walk, minWalk, strippedWalk,
        req: walk > 0 ? hp / walk : 0, seen: mix.counts[e.id] || 0, peakSpeed: Math.max(...layers.map(l => l.speed)) };
    });
    const cols = [
      { key: 'id', label: 'Id', n: true, first: true },
      { key: 'name', label: 'Enemy', render: r => enemyChip(r.id), csv: r => r.name },
      { key: 'hp', label: 'HP / layer', n: true, fmt: v => fmt(v, 0) },
      { key: 'speed', label: 'Speed', n: true, fmt: v => fmt(v, 2) },
      { key: 'peakSpeed', label: 'Fastest layer', n: true, fmt: v => fmt(v, 2), title: 'Fastest speed of any layer it turns into (pink layers are fastest)' },
      { key: 'total', label: 'Total HP', n: true, heat: 'high', fmt: v => fmt(v, 0), title: 'HP of all layers until it dies for good (RBE)' },
      { key: 'layers', label: 'Layers', n: true },
      { key: 'money', label: 'Money', n: true, fmt: money, title: '1 per popped layer; bosses pay nothing' },
      { key: 'mph', label: 'Money/HP', n: true, fmt: v => fmt(v, 3) },
      { key: 'lives', label: 'Lives if leaked', n: true, heat: 'high', title: 'Lives -= Id + 1 of the layer that reaches the End' },
      { key: 'walk', label: 'Walk time', n: true, fmt: secs, title: 'Time to reach the End when never damaged' },
      { key: 'strippedWalk', label: 'Walk (popped evenly)', n: true, fmt: secs, title: 'Walk time if its layers are popped evenly along the path (speeds weighted by HP)' },
      { key: 'req', label: 'DPS to stop', n: true, heat: 'high', fmt: v => fmt(v, 2), title: 'Total HP ÷ undamaged walk time: DPS needed if a tower covered the whole path' },
      { key: 'seen', label: 'In waves', n: true, title: 'How often it spawns in this level\'s authored waves (per lane)' },
    ];
    el.appendChild(h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }), csvButton('3dtd-enemies.csv', cols, () => rows)));
    el.appendChild(table({ id: 'enemies', columns: cols, rows, sort: { key: 'id', dir: 'asc' }, maxHeight: '55vh' }));

    // damage resolver
    const g = h('div', { class: 'grid cols-2 section' });
    const sel = selectEl(state.filters.resolveId || 10, E.enemies.map(e => [e.id, e.id + ' ' + E.enemyName(e.id)]), v => { state.filters.resolveId = +v; renderMain(); });
    const dmgIn = h('input', { type: 'number', min: 0.1, step: 'any', value: state.filters.resolveDmg || 10 });
    dmgIn.addEventListener('change', () => { state.filters.resolveDmg = +dmgIn.value; renderMain(); });
    const rid = +(state.filters.resolveId || 10), rdmg = +(state.filters.resolveDmg || 10);
    const rc = E.resolveKill(rid, rdmg, 'coded'), ri = E.resolveKill(rid, rdmg, 'intended');
    const outcome = (r) => r.corrupt ? 'throws IndexOutOfRangeException, enemy left corrupted' : r.dead ? `dies after ${r.hits} hit${r.hits === 1 ? '' : 's'}` : 'survives';
    g.appendChild(card('Damage resolver', 'repeated hits of one size until the enemy dies',
      h('div', { class: 'inline-form' }, h('label', null, 'Enemy', sel), h('label', null, 'Damage per hit', dmgIn)),
      h('div', { class: 'grid cols-2', style: { marginTop: '12px' } },
        h('div', null, h('h4', { text: 'As coded (Enemy.cs)' }), h('dl', { class: 'kv' }, h('dt', { text: 'Outcome' }), h('dd', { text: outcome(rc) }), h('dt', { text: 'Money paid' }), h('dd', { text: money(rc.money) }), h('dt', { text: 'Total HP' }), h('dd', { text: fmt(E.enemyTotalHP(rid), 0) }))),
        h('div', null, h('h4', { text: 'Intended carry-over' }), h('dl', { class: 'kv' }, h('dt', { text: 'Outcome' }), h('dd', { text: outcome(ri) }), h('dt', { text: 'Money paid' }), h('dd', { text: money(ri.money) }), h('dt', { text: 'Wasted damage' }), h('dd', { text: fmt(ri.wasted, 1) })))),
      h('h4', { style: { marginTop: '12px' }, text: 'Hit trace (as coded)' }),
      table({ columns: [
        { key: 'hit', label: 'Hit', n: true },
        { key: 'from', label: 'Before', render: r => enemyChip(r.from) },
        { key: 'to', label: 'After', render: r => r.exception ? status('bad', 'exception') : r.to == null ? h('span', { text: 'dead' }) : enemyChip(r.to) },
        { key: 'hp', label: 'HP left', n: true, fmt: v => fmt(v, 1) },
        { key: 'money', label: 'Money', n: true },
      ], rows: rc.trace, tall: true })));

    // bug heatmap
    g.appendChild(chartCard('Overflow bug impact', 'extra hits needed (as coded vs intended) for each enemy and damage per hit', (c) => {
      const dmgs = [1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 50, 100];
      const ids = E.enemies.filter(e => !e.special).map(e => e.id);
      UI.heatmap(c, {
        rows: ids.filter(id => id % 2 === 0 || id >= 10).map(id => ({ key: id, label: id + ' ' + E.enemyName(id) })),
        cols: dmgs.map(d => ({ key: d, label: String(d) })),
        value: (r, col) => { const a = E.resolveKill(r.key, col.key, 'coded'), b = E.resolveKill(r.key, col.key, 'intended'); return a.corrupt ? 99 : Math.max(0, a.hits / b.hits - 1) * 10; },
        color: (v) => v >= 99 ? 'var(--critical)' : v <= 0.0001 ? null : ['var(--seq-2)', 'var(--seq-3)', 'var(--seq-4)', 'var(--seq-5)', 'var(--seq-6)'][Math.min(4, Math.floor(v / 2))],
        tip: (r, col) => { const a = E.resolveKill(r.key, col.key, 'coded'), b = E.resolveKill(r.key, col.key, 'intended');
          return { title: r.label + ' · ' + col.key + ' dmg', rows: [{ key: 'Hits as coded', value: a.corrupt ? 'exception' : String(a.hits) }, { key: 'Hits intended', value: String(b.hits) }, { key: 'Money as coded', value: money(a.money) }, { key: 'Money intended', value: money(b.money) }] }; },
        labelWidth: 150, cellHeight: 12,
      });
      c.appendChild(h('p', { class: 'small muted', style: { marginTop: '6px' } }, 'Blue = more hits than needed (darker = worse). ', status('bad', 'Red'), ' = the hit throws IndexOutOfRangeException in Enemy.HandleShapeOrColorChanged and the enemy keeps an invalid shape. X axis: damage per hit.'));
    }));
    el.appendChild(g);

    el.appendChild(h('div', { class: 'section' }, chartCard('Layer journey', 'speed of each layer from spawn to death (x = cumulative HP removed)', (c) => {
      const pick = [9, 19, 29, 39, 49].filter(id => E.enemies[id]);
      UI.lineChart(c, {
        series: pick.map((id, i) => {
          let cum = 0;
          const vals = [];
          E.enemyLayers(id).forEach(l => { vals.push({ x: cum, y: l.speed }); cum += l.health; vals.push({ x: cum - 1e-6, y: l.speed }); });
          return { name: E.enemyName(id), color: 'var(--s' + (i + 1) + ')', values: vals };
        }), xLabel: 'HP removed so far', yLabel: 'Movement speed', height: 240, xTipLabel: 'HP ', yMin: 0,
      });
      c.appendChild(h('p', { class: 'small muted', text: 'Popping a white layer reveals a faster pink one (4.0): damaged enemies can speed up before they slow down.' }));
    })));
  };
  const sum = (a, f) => a.reduce((x, y) => x + f(y), 0);

  // ------------------------------------------------------------------ waves
  VIEWS.waves = function (el) {
    const level = E.level();
    const rows = E.levelWaveTable(level);
    const eco = E.economyOf(level);
    const W = level.spawner ? level.spawner.waves.length : 0;
    const prof = eco.profile;
    el.appendChild(head('Waves & economy', [h('b', { text: level.name }), `: ${W} authored waves + ${E.settings.extraRounds} scaled repeats, ${level.lanes.length} lane(s), start money ${money(eco.money)}, ${eco.lives} lives (${eco.difficulty}${prof ? ', won after round ' + E.winRound(level) : ''}). ` +
      (prof ? `Income is 1 per popped layer × the profile's multiplier (${prof.incomeBrackets.map(b => '×' + b.multiplier + ' from round ' + b.fromRound).join(', ')}), plus ${prof.endOfWaveBonusBase} + ${prof.endOfWaveBonusPerRound} × round after every wave; prices ×${prof.priceMultiplier}, selling refunds ${Math.round(prof.refundRate * 100)}%.` : `Income is 1 per popped layer; end-of-wave money ${money(eco.endOfWaveMoney)}.`)]));
    const ok = rows.filter(r => !r.missing);
    const g = h('div', { class: 'grid cols-2' });
    g.appendChild(chartCard('Total HP per round', 'all lanes; scaled repeats after round ' + W, (c) => UI.columnChart(c, {
      items: ok.map(r => ({ x: r.round + 1, value: r.hp, color: r.infinite ? 'var(--s2)' : 'var(--s1)',
        tip: { title: 'Round ' + (r.round + 1) + ' · ' + r.name, rows: [{ key: 'Total HP', value: fmt(r.hp, 0) }, { key: 'Enemies', value: String(r.count) }, { key: 'Income if all popped', value: money(r.layers) }, { key: 'Lives at risk', value: String(r.lives) }] } })),
      legendItems: [{ name: 'Authored wave', color: 'var(--s1)' }, { name: 'Scaled repeat', color: 'var(--s2)' }], xLabel: 'Round', valueLabel: 'Total HP' })));
    g.appendChild(chartCard('DPS demand vs DPS money can buy', 'no leaks assumed', (c) => drawDemandChart(c, level)));
    el.appendChild(g);
    const g2 = h('div', { class: 'grid cols-2 section' });
    g2.appendChild(chartCard('Money over the level', 'banked at round start if every layer is popped', (c) => UI.lineChart(c, {
      series: [{ name: 'Money at round start', color: 'var(--s1)', values: ok.map(r => ({ x: r.round + 1, y: r.moneyAtStart })), area: true }],
      xLabel: 'Round', yLabel: '$', xFmt: v => String(Math.round(v)), xTipLabel: 'Round ', height: 240 })));
    g2.appendChild(chartCard('Income per minute of spawning', 'layers ÷ spawn duration', (c) => UI.columnChart(c, {
      items: ok.map(r => ({ x: r.round + 1, value: r.incomePerMin, color: r.infinite ? 'var(--s2)' : 'var(--s1)' })), xLabel: 'Round', valueLabel: '$ / min' })));
    el.appendChild(g2);

    el.appendChild(h('div', { class: 'section' }, chartCard('Composition', 'enemies per round (all lanes), rows = enemy type', (c) => {
      const ids = Array.from(new Set(ok.flatMap(r => Object.keys(r.composition).map(Number)))).sort((a, b) => a - b);
      UI.heatmap(c, { rows: ids.map(id => ({ key: id, label: id + ' ' + E.enemyName(id) })), cols: ok.map(r => ({ key: r.round, label: String(r.round + 1) })),
        value: (r, col) => (ok[col.key] || ok.find(x => x.round === col.key)).composition[r.key] || 0, log: true, labelWidth: 160, cellHeight: 16,
        tip: (r, col, v) => ({ title: r.label + ' · round ' + (col.key + 1), rows: [{ key: 'Count', value: String(v || 0) }, { key: 'HP', value: fmt((v || 0) * E.enemyTotalHP(r.key), 0) }] }),
        scaleNote: 'count (log scale)' });
    })));

    const cols = [
      { key: 'round', label: '#', n: true, first: true, fmt: v => String(v + 1) },
      { key: 'name', label: 'Wave', render: r => h('span', { class: 'nowrap' }, r.name, r.infinite ? h('span', { class: 'tag', style: { marginLeft: '6px' }, text: 'scaled' }) : null) },
      { key: 'comp', label: 'Composition', sortable: false, render: r => h('span', { class: 'chips', style: { minWidth: '340px' } }, Object.keys(r.composition).map(Number).sort((a, b) => r.composition[b] - r.composition[a]).slice(0, 4).map(id => enemyChip(id, r.composition[id]))), csv: r => Object.keys(r.composition).map(id => r.composition[id] + 'x' + E.enemyName(+id)).join('; ') },
      { key: 'traits', label: 'Traits', sortable: false, render: r => h('span', { class: 'chips' }, Object.keys(r.traits || {}).map(k => h('span', { class: 'tag', text: r.traits[k] + '× ' + k.toLowerCase() }))), csv: r => Object.keys(r.traits || {}).map(k => r.traits[k] + 'x ' + k).join('; ') },
      { key: 'count', label: 'Enemies', n: true },
      { key: 'hp', label: 'Total HP', n: true, heat: 'high', fmt: v => fmt(v, 0) },
      { key: 'income', label: 'Income', n: true, fmt: money, title: 'Money if every layer is popped, after the round\'s income multiplier' },
      { key: 'incomeMult', label: 'Income ×', n: true, fmt: v => '×' + fmt(v, 2) },
      { key: 'bonus', label: 'Wave bonus', n: true, fmt: money, title: 'End-of-wave bonus' },
      { key: 'lives', label: 'Lives at risk', n: true, title: 'Lives lost if everything leaks untouched' },
      { key: 'spawnDuration', label: 'Spawn time', n: true, fmt: secs },
      { key: 'lastArrival', label: 'Last arrival', n: true, fmt: secs, title: 'When the last enemy reaches the End if nothing is damaged (lane 1)' },
      { key: 'hpPerSecond', label: 'HP/s', n: true, fmt: v => fmt(v, 1) },
      { key: 'streamDPS', label: 'Stream DPS', n: true, heat: 'high', fmt: v => fmt(v, 1), title: 'Highest HP per second of any sustained entry (≥3 enemies): DPS needed to keep up with it' },
      { key: 'avgDPS', label: 'Avg DPS', n: true, fmt: v => fmt(v, 1) },
      { key: 'moneyAtStart', label: 'Money', n: true, fmt: money, title: 'Banked at the start of this round with zero leaks' },
      { key: 'dpsPerDollar', label: 'DPS needed per $', n: true, heat: 'high', fmt: v => fmt(v, 4), title: 'Stream DPS ÷ money banked: rising values mean the waves outpace the economy' },
      { key: 'incomePerMin', label: '$/min', n: true, fmt: v => fmt(v, 0) },
      { key: 'meanSpeed', label: 'Mean speed', n: true, fmt: v => fmt(v, 2) },
      { key: 'cumHP', label: 'Cum. HP', n: true, fmt: v => fmt(v, 0) },
    ];
    el.appendChild(h('div', { class: 'section' }, h('div', { class: 'toolbar' }, h('h3', { text: 'Round table', style: { margin: 0 } }), h('span', { class: 'spacer' }), csvButton('3dtd-waves-' + level.name.replace(/\s+/g, '-') + '.csv', cols, () => ok)),
      table({ id: 'waves', columns: cols, rows: ok, sort: { key: 'round', dir: 'asc' }, maxHeight: '60vh' })));
  };

  // ------------------------------------------------------------------ levels
  VIEWS.levels = function (el) {
    el.appendChild(head('Levels & coverage', 'Map geometry from the scenes: enemy paths (cut where enemies touch the End trigger), building blocks and their free anchor faces. Coverage = path length inside a tower\'s detection volume.'));
    const tiles = h('div', { class: 'grid cols-4' });
    E.levels.forEach(L => {
      const eco = E.economyOf(L);
      const sel = L.name === E.settings.level;
      const c = h('div', { class: 'card', style: { cursor: 'pointer', borderColor: sel ? 'var(--accent)' : null }, onclick: () => setSetting({ level: L.name }) },
        h('h3', { text: L.name + (L.isMainMenu ? ' (menu backdrop)' : '') }),
        h('dl', { class: 'kv' },
          h('dt', { text: 'Lanes' }), h('dd', { text: String(L.lanes.length) }),
          h('dt', { text: 'Path length' }), h('dd', { text: L.lanes.map(l => fmt(l.length, 1)).join(' / ') }),
          h('dt', { text: 'Anchors (free)' }), h('dd', { text: L.anchors.filter(a => !a.blocked).length + ' on ' + L.blocks.length + ' blocks' }),
          h('dt', { text: 'Waves' }), h('dd', { text: String(L.spawner ? L.spawner.waves.length : 0) }),
          h('dt', { text: 'Start' }), h('dd', { text: money(eco.money) + ' · ' + eco.lives + ' lives' }),
          h('dt', { text: 'Palette' }), h('dd', { text: L.palette.length + ' buildings' })));
      tiles.appendChild(c);
    });
    el.appendChild(tiles);

    const level = E.level();
    if (!level.lanes.length) return;
    const towers = palette();
    const tk = state.filters.levelTower && E.towerByKey[state.filters.levelTower] ? state.filters.levelTower : towers[0].key;
    const t = E.towerByKey[tk];
    const m = baseModel(t);
    const lc = E.levelCoverage(m, level);
    const covByAnchor = {};
    lc.ranked.forEach(c => { covByAnchor[c.anchor] = c.length; });
    const tsel = selectEl(tk, towers.map(x => [x.key, x.displayName]), v => { state.filters.levelTower = v; renderMain(); });
    const g = h('div', { class: 'grid cols-2 section' });
    const anchors = level.anchors.map((a, i) => ({ pos: a.pos, blocked: a.blocked, value: covByAnchor[i] || 0, selected: lc.best && lc.best.anchor === i,
      tip: { title: 'Anchor ' + i + (a.blocked ? ' (blocked)' : ''), rows: [{ key: 'Coverage (' + t.displayName + ')', value: fmt(covByAnchor[i] || 0, 1) + ' u' }, { key: 'Face normal', value: a.fwd.map(v => Math.round(v)).join(', ') }, { key: 'Position', value: a.pos.map(v => fmt(v, 1)).join(', ') }] } }));
    g.appendChild(card('Top view', 'anchors colored by ' + t.displayName + ' coverage; ringed = best anchor',
      h('div', { class: 'inline-form', style: { marginBottom: '8px' } }, h('label', null, 'Tower', tsel)),
      (() => { const c = h('div'); requestAnimationFrame(() => UI.mapPlot(c, { lanes: level.lanes.map(l => l.path), anchors, blocks: level.blocks, end: level.end, valueLabel: 'coverage', valueFmt: v => fmt(v, 1) + ' u' })); return c; })()));
    g.appendChild(card('Side view', 'x vs height', (() => { const c = h('div'); requestAnimationFrame(() => UI.mapPlot(c, { lanes: level.lanes.map(l => l.path), anchors, blocks: level.blocks, end: level.end, view: 'side', valueLabel: 'coverage', valueFmt: v => fmt(v, 1) + ' u' })); return c; })()));
    el.appendChild(g);

    const speeds = Array.from(new Set(E.enemies.map(e => e.speed))).sort((a, b) => a - b);
    el.appendChild(h('div', { class: 'section' }, card('Walk time to the End', 'undamaged, per lane', table({
      columns: [{ key: 'speed', label: 'Speed', n: true, fmt: v => fmt(v, 2) }, { key: 'who', label: 'Layers with this speed', render: r => h('span', { class: 'small', text: r.who }) }]
        .concat(level.lanes.map((l, i) => ({ key: 'l' + i, label: 'Lane ' + (i + 1) + ' (' + fmt(l.length, 1) + ' u)', n: true, fmt: secs }))),
      rows: speeds.map(sp => Object.assign({ speed: sp, who: Array.from(new Set(E.enemies.filter(e => e.speed === sp).map(e => (e.special ? 'Boss' : cap(e.color))))).join(', ') }, Object.fromEntries(level.lanes.map((l, i) => ['l' + i, l.length / sp])))),
      tall: true }))));

    const rows = towers.map(t2 => {
      const mm = baseModel(t2);
      const c = E.levelCoverage(mm, level);
      return { t: t2, m: mm, best: c.bestLength, median: c.medianUseful, useful: c.usefulAnchors, free: c.freeAnchors, share: c.freeAnchors ? c.usefulAnchors / c.freeAnchors : 0,
        tRed: c.bestLength / 1.25, tPink: c.bestLength / 4, pass: mm.coverage ? mm.coverage.passDamage : 0, solo: mm.coverage ? mm.coverage.maxSoloKill : -1 };
    });
    el.appendChild(h('div', { class: 'section' }, card('Base tower coverage on ' + level.name, 'how much path each tower can watch from the existing anchors', table({
      columns: [
        { key: 'tower', label: 'Tower', first: true, value: r => r.t.displayName, render: r => h('a', { href: buildHref(r.t) }, towerName(r.t)) },
        { key: 'useful', label: 'Useful anchors', n: true, fmt: (v, r) => v + ' / ' + r.free },
        { key: 'best', label: 'Best coverage', n: true, heat: 'high', fmt: v => fmt(v, 1) },
        { key: 'median', label: 'Median useful', n: true, fmt: v => fmt(v, 1) },
        { key: 'tRed', label: 'In range (red)', n: true, fmt: secs },
        { key: 'tPink', label: 'In range (pink)', n: true, fmt: secs },
        { key: 'pass', label: 'Pass dmg', n: true, heat: 'high', fmt: v => fmt(v, 2) },
        { key: 'solo', label: 'Solo kills up to', render: r => r.solo >= 0 ? enemyChip(r.solo) : h('span', { class: 'muted', text: 'nothing' }) },
      ], rows, sort: { key: 'best', dir: 'desc' }, tall: true }))));
  };
  function cap(s) { return s.charAt(0) + s.slice(1).toLowerCase(); }

  // ------------------------------------------------------------------ simulator
  function buildPicker(prefix) {
    const towers = palette();
    const tk = state.filters[prefix + 'Tower'] && E.towerByKey[state.filters[prefix + 'Tower']] ? state.filters[prefix + 'Tower'] : towers[0].key;
    const t = E.towerByKey[tk];
    let lv = state.filters[prefix + 'Levels'] && state.filters[prefix + 'Levels'].length === t.upgradePaths.length ? state.filters[prefix + 'Levels'] : baseLevels(t);
    const wrap = h('div', { class: 'inline-form' });
    wrap.appendChild(h('label', null, 'Tower', selectEl(tk, towers.map(x => [x.key, x.displayName]), v => { state.filters[prefix + 'Tower'] = v; state.filters[prefix + 'Levels'] = null; renderMain(); })));
    t.upgradePaths.forEach((p, i) => {
      wrap.appendChild(h('label', null, 'Path ' + (i + 1), selectEl(lv[i], [0, 1, 2, 3].slice(0, p.length + 1).map(x => [x, x === 0 ? 'none' : 'T' + x + ' ' + p[x - 1].name]), v => {
        const n = lv.slice(); n[i] = +v;
        if (!validLevels(n)) { alert('Not reachable: at most two paths can be upgraded and only one can reach tier 3.'); renderMain(); return; }
        state.filters[prefix + 'Levels'] = n; renderMain();
      })));
    });
    return { el: wrap, tower: t, levels: lv };
  }

  VIEWS.simulator = function (el) {
    const level = E.level();
    const W = level.spawner ? level.spawner.waves.length : 0;
    el.appendChild(head('Simulator', ['Plays ', h('b', { text: level.name }), ' frame by frame: waves spawn as in Spawner.cs, enemies walk the lanes, towers use the real timers and every shot is resolved against actual enemy positions and velocities. Copies of the chosen build are bought between waves on the best free anchors (tower first, then its upgrades in purchase order).']));
    const pick = buildPicker('sim');
    const rounds = h('input', { type: 'number', min: 1, value: state.filters.simRounds || Math.min(W + (+E.settings.extraRounds || 0), 60) });
    const copies = h('input', { type: 'number', min: 1, value: state.filters.simCopies || 99 });
    const seed = h('input', { type: 'number', value: state.filters.simSeed || 1234 });
    const prog = h('div', { class: 'progress' }, h('div'));
    const out = h('div');
    const run = h('button', { class: 'primary' }, 'Run simulation');
    run.addEventListener('click', () => {
      state.filters.simRounds = +rounds.value; state.filters.simCopies = +copies.value; state.filters.simSeed = +seed.value;
      run.disabled = true;
      const sim = E.createSimulation({ level, builds: [{ tower: pick.tower, levels: pick.levels }], rounds: +rounds.value, maxCopies: +copies.value, seed: +seed.value });
      const total = +rounds.value;
      const tick = () => {
        const done = sim.step(4000);
        prog.firstChild.style.width = Math.min(100, sim.round / total * 100) + '%';
        if (done) { run.disabled = false; state.simResult = sim.result(); state.simResult.levels = pick.levels; state.simResult.towerKey = pick.tower.key; drawSim(out, state.simResult); }
        else setTimeout(tick, 0);
      };
      tick();
    });
    el.appendChild(card('Setup', null, h('div', { class: 'inline-form' }, pick.el, h('label', null, 'Rounds', rounds), h('label', null, 'Max copies', copies), h('label', null, 'Seed', seed), run), prog));
    el.appendChild(out);
    if (state.simResult && state.simResult.level === level.name) drawSim(out, state.simResult);
    else if (state.query.run === '1') setTimeout(() => run.click(), 0);
  };

  function drawSim(out, r) {
    clear(out);
    const lifeVal = E.lifeValue();
    const opp = r.totals.leakedLayers + r.totals.livesLost * lifeVal;
    const hitRate = r.totals.shots ? 1 - r.totals.misses / r.totals.shots : null;
    out.appendChild(h('div', { class: 'tiles section' },
      tile('Result', r.survived ? 'Survived' : 'Game over', r.survived ? r.rounds + ' rounds played' : 'in round ' + (r.gameOverRound + 1)),
      tile('Lives left', String(Math.max(0, r.lives)), r.totals.livesLost + ' lost'),
      tile('Money lost to leaks', money(r.totals.leakedLayers), 'layers that leaked unpopped'),
      tile('Opportunity cost', money(opp), 'leaks + lives × ' + money(lifeVal)),
      tile('Income captured', money(r.totals.income), pct(r.totals.income / Math.max(1, r.totals.income + r.totals.leakedLayers)) + ' of available'),
      tile('Money spent', money(r.totals.spent), money(r.money) + ' unspent at end'),
      tile('Towers', String(r.towers.length), r.builds.join(', ')),
      tile('Hit rate', hitRate == null ? '–' : pct(hitRate), r.totals.shots + ' shots'),
      r.totals.exceptions ? tile('Engine exceptions', String(r.totals.exceptions), 'overflow bug hits') : null,
      r.totals.poolStarved ? tile('Pool starved', String(r.totals.poolStarved), 'pulses skipped (pool at its cap)') : null));
    const g = h('div', { class: 'grid cols-2 section' });
    g.appendChild(chartCard('Lives and money', 'at the end of each round', (c) => {
      UI.lineChart(c, { series: [{ name: 'Lives', color: 'var(--s8)', values: r.waves.map(w => ({ x: w.round + 1, y: Math.max(0, w.endLives) })) }], xLabel: 'Round', yLabel: 'Lives', height: 180, xFmt: v => String(Math.round(v)), xTipLabel: 'Round ', yMin: 0 });
      UI.lineChart(c, { series: [{ name: 'Money', color: 'var(--s1)', values: r.waves.map(w => ({ x: w.round + 1, y: w.endMoney })) }], xLabel: 'Round', yLabel: 'Money banked ($)', height: 180, xFmt: v => String(Math.round(v)), xTipLabel: 'Round ' });
    }));
    g.appendChild(chartCard('Leaks per round', 'money (layers) that walked out', (c) => UI.columnChart(c, {
      items: r.waves.map(w => ({ x: w.round + 1, value: w.leakedLayers, color: 'var(--s2)', tip: { title: 'Round ' + (w.round + 1) + ' · ' + w.name, rows: [{ key: 'Leaked layers ($)', value: String(w.leakedLayers) }, { key: 'Enemies leaked', value: String(w.leaks) }, { key: 'Lives lost', value: String(w.livesLost) }, { key: 'Income', value: money(w.income) }, { key: 'Towers', value: String(w.towers) }] } })),
      xLabel: 'Round', valueLabel: 'Leaked layers' })));
    out.appendChild(g);
    const cols = [
      { key: 'round', label: '#', n: true, fmt: v => String(v + 1) },
      { key: 'name', label: 'Wave' },
      { key: 'duration', label: 'Duration', n: true, fmt: secs },
      { key: 'towers', label: 'Towers', n: true },
      { key: 'income', label: 'Income', n: true, fmt: money },
      { key: 'kills', label: 'Kills', n: true },
      { key: 'leaks', label: 'Leaked', n: true, heat: 'high' },
      { key: 'leakedLayers', label: 'Leaked $', n: true, fmt: money },
      { key: 'livesLost', label: 'Lives lost', n: true, heat: 'high' },
      { key: 'endLives', label: 'Lives', n: true },
      { key: 'endMoney', label: 'Money', n: true, fmt: money },
      { key: 'exceptions', label: 'Exceptions', n: true },
    ];
    out.appendChild(h('div', { class: 'section' }, h('div', { class: 'toolbar' }, h('h3', { text: 'Per round', style: { margin: 0 } }), h('span', { class: 'spacer' }), csvButton('3dtd-sim.csv', cols, () => r.waves)),
      table({ columns: cols, rows: r.waves, sort: { key: 'round', dir: 'asc' }, maxHeight: '50vh' })));
    const totalDmg = r.towers.reduce((s2, t) => s2 + t.damage, 0) || 1;
    out.appendChild(h('div', { class: 'grid cols-2 section' },
      card('Towers', 'damage share', table({ columns: [
        { key: 'label', label: 'Build' }, { key: 'anchor', label: 'Anchor', n: true },
        { key: 'damage', label: 'Damage', n: true, fmt: v => fmt(v, 0), heat: 'high' }, { key: 'share', label: 'Share', n: true, value: t => t.damage / totalDmg, fmt: v => pct(v) }, { key: 'shots', label: 'Shots', n: true }],
        rows: r.towers, sort: { key: 'damage', dir: 'desc' }, maxHeight: '40vh' })),
      card('Purchases', r.purchases.length + ' items', table({ columns: [
        { key: 'round', label: 'Round', n: true, fmt: v => String(v + 1) }, { key: 'what', label: 'Item' }, { key: 'cost', label: 'Cost', n: true, fmt: money }],
        rows: r.purchases, sort: { key: 'round', dir: 'asc' }, maxHeight: '40vh' }))));
  }

  // ------------------------------------------------------------------ compare / opportunity cost
  VIEWS.compare = function (el) {
    const level = E.level();
    const W = level.spawner ? level.spawner.waves.length : 0;
    el.appendChild(head('Opportunity cost', ['Simulates "spend everything on one build" for every tower on ', h('b', { text: level.name }), '. Cheap towers that leak lose the layers that walk out (money they would have paid) and lives. Opportunity cost = leaked layers + lives lost × value per life (', money(E.lifeValue()), ', change under Assumptions).']));
    const mode = selectEl(state.filters.cmpMode || 'base', [['base', 'Base towers (no upgrades)'], ['value', 'Each tower\'s best pass-damage-per-$ build'], ['strong', 'Each tower\'s strongest affordable build ≤ $1500']], v => { state.filters.cmpMode = v; state.compare = null; renderMain(); });
    const rounds = h('input', { type: 'number', min: 1, value: state.filters.cmpRounds || Math.min(W, 30) });
    const prog = h('div', { class: 'progress' }, h('div'));
    const run = h('button', { class: 'primary' }, 'Run comparison');
    const out = h('div');
    el.appendChild(card('Setup', 'each run buys copies (and their upgrades) between waves on the best free anchors', h('div', { class: 'inline-form' }, h('label', null, 'Builds', mode), h('label', null, 'Rounds', rounds), run), prog));
    el.appendChild(out);
    const candidates = () => palette().map(t => {
      const all = E.allBuildLevels(t).map(lv => E.model(t, lv));
      const m = state.filters.cmpMode === 'value' ? all.filter(x => x.coverage).sort((a, b) => b.coverage.passDamage / b.cost - a.coverage.passDamage / a.cost)[0]
        : state.filters.cmpMode === 'strong' ? all.filter(x => x.cost <= 1500).sort((a, b) => b.dps.crowd - a.dps.crowd)[0] : all.find(x => x.levels.every(v => v === 0));
      return m;
    }).filter(Boolean);
    run.addEventListener('click', () => {
      state.filters.cmpRounds = +rounds.value;
      run.disabled = true;
      const list = candidates();
      const results = [];
      let i = 0, sim = null;
      const tick = () => {
        if (!sim) sim = E.createSimulation({ level, builds: [{ tower: list[i].tower, levels: list[i].levels }], rounds: +rounds.value, seed: 1234 });
        const done = sim.step(5000);
        prog.firstChild.style.width = ((i + Math.min(1, sim.round / +rounds.value)) / list.length * 100) + '%';
        if (done) { const r = sim.result(); r.model = list[i]; results.push(r); i++; sim = null; }
        if (i < list.length) setTimeout(tick, 0);
        else { run.disabled = false; state.compare = { level: level.name, results, rounds: +rounds.value }; drawCompare(out, state.compare); }
      };
      tick();
    });
    if (state.compare && state.compare.level === level.name) drawCompare(out, state.compare);
    else if (state.query.run === '1') setTimeout(() => run.click(), 0);
    else out.appendChild(h('div', { class: 'callout section', text: 'Press "Run comparison". It takes a few seconds; every build plays the same waves with the same random seed.' }));
  };

  function drawCompare(out, cmp) {
    clear(out);
    const lifeVal = E.lifeValue();
    const level = E.levelByName[cmp.level];
    const waveRows = E.levelWaveTable(level);
    const K = Math.max(1, Math.min(cmp.rounds, +state.filters.cmpHorizon || Math.min(10, cmp.rounds)));
    const horizonIn = h('input', { type: 'number', min: 1, max: cmp.rounds, value: K });
    horizonIn.addEventListener('change', () => { state.filters.cmpHorizon = +horizonIn.value; drawCompare(out, cmp); });
    const rows = cmp.results.map(r => {
      const upTo = r.waves.filter(w => w.round < K);
      const leakedK = upTo.reduce((s2, w) => s2 + w.leakedLayers, 0);
      const livesK = upTo.reduce((s2, w) => s2 + w.livesLost, 0);
      // a dead player forfeits every layer of the rounds it never got to play
      const forfeited = waveRows.filter(w => !w.missing && w.round < K && w.round >= r.waves.length).reduce((s2, w) => s2 + w.layers, 0);
      const opp = leakedK + livesK * lifeVal + forfeited;
      const survived = r.survived ? cmp.rounds : r.gameOverRound;
      return { r, m: r.model, label: r.model.label, survived, livesLost: livesK, leaked: leakedK, forfeited, opp,
        spent: r.totals.spent, income: r.totals.income, capture: r.totals.income / Math.max(1, r.totals.income + r.totals.leakedLayers), towers: r.towers.length, idle: r.money,
        perRound: r.totals.spent / Math.max(1, survived), firstLeak: (r.waves.find(w => w.leaks > 0) || {}).round, exceptions: r.totals.exceptions };
    });
    out.appendChild(h('div', { class: 'card section' }, h('div', { class: 'inline-form' }, h('label', null, 'Count costs through round', horizonIn),
      h('p', { class: 'small muted', style: { margin: 0, maxWidth: '640px' } }, 'Every build eventually loses everything, so costs are compared up to a common round. Opportunity cost = layers leaked + lives lost × ' + money(lifeVal) + ' + all income of rounds a build never reached because it was already game over.'))));
    out.appendChild(h('div', { class: 'section' }, table({ id: 'compare', columns: [
      { key: 'label', label: 'Build', first: true, render: x => buildLink(x.m) },
      { key: 'survived', label: 'Rounds survived', n: true, heat: 'high', fmt: (v, x) => x.r.survived ? v + ' (all)' : String(v) },
      { key: 'firstLeak', label: 'First leak', n: true, fmt: v => v == null ? 'never' : 'round ' + (v + 1) },
      { key: 'livesLost', label: 'Lives lost ≤ K', n: true, heat: 'low' },
      { key: 'leaked', label: 'Money leaked ≤ K', n: true, heat: 'low', fmt: money },
      { key: 'forfeited', label: 'Income forfeited', n: true, fmt: money, title: 'Income of rounds up to K that were never played because of game over' },
      { key: 'opp', label: 'Opportunity cost ≤ K', n: true, heat: 'low', fmt: money, title: 'Leaked layers + lives lost × value per life + forfeited income, through round K' },
      { key: 'perRound', label: '$ per round survived', n: true, heat: 'low', fmt: money, title: 'Money spent ÷ rounds survived' },
      { key: 'capture', label: 'Income captured', n: true, heat: 'high', fmt: v => pct(v), title: 'Popped layers ÷ (popped + leaked)' },
      { key: 'income', label: 'Income', n: true, fmt: money },
      { key: 'spent', label: 'Spent', n: true, fmt: money },
      { key: 'idle', label: 'Unspent at end', n: true, fmt: money, title: 'Money that could not be turned into towers (no useful free anchor or not enough for the next item)' },
      { key: 'towers', label: 'Towers', n: true },
      { key: 'exceptions', label: 'Bug exceptions', n: true },
    ], rows, sort: { key: 'survived', dir: 'desc' }, tall: true })));
    const g = h('div', { class: 'grid cols-2 section' });
    g.appendChild(chartCard('Lives remaining after each round', 'one line per build', (c) => UI.lineChart(c, {
      series: rows.map(x => ({ name: x.m.tower.displayName, color: towerColor(x.m.tower.key), values: [{ x: 0, y: x.r.waves.length ? x.r.waves[0].startLives : 0 }].concat(x.r.waves.map(w => ({ x: w.round + 1, y: Math.max(0, w.endLives) }))) })),
      xLabel: 'Round', yLabel: 'Lives', xFmt: v => String(Math.round(v)), xTipLabel: 'Round ', yMin: 0, height: 280 })));
    g.appendChild(chartCard('Opportunity cost through round ' + K, 'leaks + lives × ' + money(lifeVal) + ' + forfeited income', (c) => UI.barChart(c, {
      items: rows.slice().sort((a, b) => a.opp - b.opp).map(x => ({ label: x.label, value: x.opp, color: towerColor(x.m.tower.key),
        tip: { title: x.label, rows: [{ key: 'Opportunity cost', value: money(x.opp), color: towerColor(x.m.tower.key) }, { key: 'Money leaked', value: money(x.leaked) }, { key: 'Lives lost', value: String(x.livesLost) }, { key: 'Income forfeited', value: money(x.forfeited) }, { key: 'Rounds survived', value: String(x.survived) }] } })),
      valueFmt: money, labelWidth: 200 })));
    out.appendChild(g);
    out.appendChild(h('div', { class: 'section' }, chartCard('Money leaked per round', 'heat = layers lost', (c) => {
      const maxR = Math.max(...rows.map(x => x.r.waves.length));
      UI.heatmap(c, { rows: rows.map(x => ({ key: x, label: x.label })), cols: Array.from({ length: maxR }, (_, i) => ({ key: i, label: String(i + 1) })),
        value: (rw, col) => { const w = rw.key.r.waves[col.key]; return w ? w.leakedLayers : NaN; }, labelWidth: 190, cellHeight: 20,
        tip: (rw, col, v) => ({ title: rw.label + ' · round ' + (col.key + 1), rows: [{ key: 'Leaked layers', value: isFinite(v) ? String(v) : 'game over' }] }), scaleNote: 'leaked layers' });
    })));
  }

  // ------------------------------------------------------------------ warnings
  // ------------------------------------------------------------------ meta & difficulty
  // Long agent batches run one game per tick so the page stays responsive
  const metaRuns = { sweep: null, full: null };
  function runQueue(tasks, onResult, onDone) {
    let i = 0, cancelled = false;
    const step = () => {
      if (cancelled) return;
      if (i >= tasks.length) { onDone && onDone(); return; }
      onResult(tasks[i](), i, tasks.length);
      i++;
      setTimeout(step, 0);
    };
    setTimeout(step, 0);
    return { cancel: () => { cancelled = true; } };
  }

  VIEWS.meta = function (el) {
    const level = E.level();
    const eco = E.economyOf(level);
    el.appendChild(head('Meta & difficulty', [h('b', { text: level.name }), ` · ${eco.difficulty}: which strategies can win? The meta agent is a greedy simulated player (see Methodology). A tower combination it wins with is viable; single towers should lose on most levels, and many pairs and triples should win.`]));

    // difficulty profiles
    const profs = (D.constants.difficulties || ['Easy', 'Medium', 'Hard', 'Impossible']).map(d => (D.profiles || {})[d]).filter(Boolean);
    if (profs.length) {
      el.appendChild(card('Difficulty profiles', 'ScriptableObjects/Difficulty; GameManager picks one per game (main menu selection)', table({
        columns: [
          { key: 'difficulty', label: 'Difficulty', first: true },
          { key: 'startMoney', label: 'Start money', n: true, fmt: money },
          { key: 'lives', label: 'Lives', n: true },
          { key: 'priceMultiplier', label: 'Prices', n: true, fmt: v => '×' + fmt(v, 2) },
          { key: 'income', label: 'Income × by round', sortable: false, value: p => p.incomeBrackets.map(b => 'R' + b.fromRound + ' ×' + b.multiplier).join(', ') },
          { key: 'bonus', label: 'Wave bonus', sortable: false, value: p => p.endOfWaveBonusBase + ' + ' + p.endOfWaveBonusPerRound + '×round' },
          { key: 'refundRate', label: 'Refund', n: true, fmt: v => pct(v) },
          { key: 'enemySpeedMultiplier', label: 'Enemy speed', n: true, fmt: v => '×' + fmt(v, 2) },
          { key: 'layerHealthMultiplier', label: 'Layer HP', n: true, fmt: v => '×' + fmt(v, 2) },
          { key: 'bossHealthMultiplier', label: 'Boss HP', n: true, fmt: v => '×' + fmt(v, 2) },
          { key: 'armorBonus', label: '+Armor', n: true },
          { key: 'shieldBonus', label: '+Shield', n: true },
          { key: 'winRound', label: 'Win round', n: true },
        ], rows: profs, tall: true })));
    }

    // build efficiency
    const effRows = palette().filter(t => t.upgradePaths.length).flatMap(t => E.buildEfficiency(t).filter(r => r.tier > 0));
    el.appendChild(h('div', { class: 'section' }, card('Build efficiency', 'role-weighted value per $ of every legal build, relative to its base tower (1 = as efficient as another base tower). Prices were fitted so builds land near the tier target.',
      h('div', { class: 'toolbar' }, h('span', { class: 'spacer' }), csvButton('3dtd-build-efficiency.csv', [{ key: 'label', label: 'Build' }, { key: 'cost', label: 'Cost' }, { key: 'tier', label: 'Max tier' }, { key: 'efficiency', label: 'Efficiency' }, { key: 'target', label: 'Target' }], () => effRows)),
      table({ id: 'buildeff', columns: [
        { key: 'label', label: 'Build', first: true, render: r => h('a', { href: buildHref(r.tower, r.levels) }, r.label) },
        { key: 'cost', label: 'Cost', n: true, fmt: money },
        { key: 'tier', label: 'Max tier', n: true },
        { key: 'value', label: 'Role value', n: true, fmt: v => '×' + fmt(v, 2), title: 'Weighted improvement over the base tower in its role metrics' },
        { key: 'efficiency', label: 'Value per $ vs base', n: true, heat: 'high', fmt: v => fmt(v, 2) },
        { key: 'target', label: 'Target', n: true, fmt: v => fmt(v, 2) },
        { key: 'dev', label: 'vs target', n: true, value: r => r.efficiency / r.target - 1, fmt: v => pct(v, true) },
      ], rows: effRows, sort: { key: 'efficiency', dir: 'desc' }, maxHeight: '45vh' }))));

    // palette sweep
    const keys = (level.palette || []).filter(k => E.towerByKey[k]);
    const short = (k) => E.towerByKey[k].displayName;
    const sweepOut = h('div');
    const sizeBoxes = [1, 2, 3].map(n => { const c = h('input', { type: 'checkbox' }); c.checked = n <= 2; return [n, c]; });
    const status = h('span', { class: 'muted small' });
    const runSweep = () => {
      if (metaRuns.sweep) metaRuns.sweep.cancel();
      const sizes = sizeBoxes.filter(([, c]) => c.checked).map(([n]) => n);
      const combos = [];
      const rec = (start, cur) => { if (sizes.indexOf(cur.length) >= 0) combos.push(cur.slice()); if (cur.length >= Math.max(0, ...sizes)) return; for (let i = start; i < keys.length; i++) { cur.push(keys[i]); rec(i + 1, cur); cur.pop(); } };
      rec(0, []);
      const rows = [];
      const win = E.winRound(level);
      const render = () => {
        clear(sweepOut);
        const won = rows.filter(r => r.won);
        const part = keys.map(k => ({ k, n: won.filter(r => r.combo.includes(k)).length }));
        sweepOut.appendChild(h('div', { class: 'grid cols-4', style: { marginTop: '10px' } },
          ...sizes.map(n => tile(n === 1 ? 'Single towers' : n === 2 ? 'Pairs' : 'Triples', rows.filter(r => r.size === n && r.won).length + ' / ' + rows.filter(r => r.size === n).length, 'won so far'))));
        sweepOut.appendChild(h('p', { class: 'small', text: 'Towers in winning combinations: ' + part.map(p => short(p.k) + ' ' + p.n).join(', ') }));
        sweepOut.appendChild(table({ id: 'sweep', columns: [
          { key: 'label', label: 'Towers', first: true },
          { key: 'size', label: 'Size', n: true },
          { key: 'result', label: 'Result', value: r => r.won ? 'won' : 'lost', render: r => r.won ? status_('good', 'won (' + r.lives + ' lives)') : status_('bad', 'lost at round ' + r.round) },
          { key: 'spent', label: 'Spent', n: true, fmt: money },
          { key: 'tier3', label: 'Tier 3 bought', sortable: false, value: r => Object.keys(r.tier3).map(k => k + ' ×' + r.tier3[k]).join(', ') },
        ], rows, sort: { key: 'size', dir: 'asc' }, maxHeight: '50vh' }));
      };
      status.textContent = 'running ' + combos.length + ' games…';
      metaRuns.sweep = runQueue(combos.map(c => () => {
        const r = E.runAgent({ level, seed: 1, rounds: win, palette: c });
        return { combo: c, size: c.length, label: c.map(short).join(' + '), won: r.survived, lives: r.lives, round: r.gameOverRound != null ? r.gameOverRound + 1 : null,
                 spent: r.totals.spent, tier3: r.tier3 };
      }), (row, i, n) => { rows.push(row); status.textContent = (i + 1) + ' / ' + n + ' games'; if (i % 4 === 3 || i === n - 1) render(); },
      () => { status.textContent = 'done: ' + rows.length + ' games'; render(); });
    };
    const status_ = (sev, text) => UI.status(sev, text);
    el.appendChild(h('div', { class: 'section' }, card('Palette sweep', 'the agent plays with every single tower, pair and triple of the palette (one game each, seed 1) up to round ' + E.winRound(level),
      h('div', { class: 'inline-form' }, h('span', { class: 'small', text: 'Sizes' }), ...sizeBoxes.map(([n, c]) => h('label', { class: 'small' }, c, ' ' + n)),
        h('button', { class: 'primary', onclick: runSweep }, 'Run sweep'), status),
      sweepOut)));

    // full roster
    const fullOut = h('div');
    const fullStatus = h('span', { class: 'muted small' });
    const runFull = () => {
      if (metaRuns.full) metaRuns.full.cancel();
      const results = [];
      fullStatus.textContent = 'running…';
      metaRuns.full = runQueue([1, 2, 3, 4, 5].map(seed => () => E.runAgent({ level, seed, rounds: E.winRound(level) })), (r) => {
        results.push(r);
        clear(fullOut);
        const spend = {}; let tot = 0;
        results.forEach(x => Object.keys(x.spendByTower).forEach(k => { spend[k] = (spend[k] || 0) + x.spendByTower[k]; tot += x.spendByTower[k]; }));
        fullOut.appendChild(h('p', { class: 'small', text: results.map((x, i) => 'seed ' + (i + 1) + ': ' + (x.survived ? 'won (' + x.lives + ' lives)' : 'lost at round ' + (x.gameOverRound + 1))).join(' · ') }));
        fullOut.appendChild(chartCard('Spend share', 'all seeds', (c) => UI.barChart(c, {
          items: Object.keys(spend).sort((a, b) => spend[b] - spend[a]).map(k => ({ label: E.towerByKey[k] ? E.towerByKey[k].displayName : k, value: spend[k] / tot, color: towerColor(k) })),
          valueFmt: v => pct(v) })));
        fullStatus.textContent = results.length + ' / 5 games';
      }, () => { fullStatus.textContent = 'done'; });
    };
    el.appendChild(h('div', { class: 'section' }, card('Full roster', 'five seeded games with every tower available; the spend share shows what the agent leans on',
      h('div', { class: 'inline-form' }, h('button', { class: 'primary', onclick: runFull }, 'Run 5 games'), fullStatus), fullOut)));
  };

  // ------------------------------------------------------------------ telemetry
  const telemetry = { rows: [] };
  function parseCSV(text) {
    const lines = text.split(/\r?\n/).filter(l => l.trim());
    const split = (l) => { const out = []; let cur = '', q = false; for (const ch of l) { if (ch === '"') q = !q; else if (ch === ',' && !q) { out.push(cur); cur = ''; } else cur += ch; } out.push(cur); return out; };
    const head_ = split(lines[0]);
    return lines.slice(1).map(l => { const v = split(l); const o = {}; head_.forEach((k, i) => { o[k] = isNaN(+v[i]) || v[i] === '' ? v[i] : +v[i]; }); return o; });
  }
  VIEWS.telemetry = function (el) {
    el.appendChild(head('Telemetry', 'CSV files written by BalanceTelemetry.cs (Editor and development builds) to Application.persistentDataPath/telemetry. Real damage per tower type and wave, compared with the engine\'s base-build model, plus FPS per wave.'));
    const input = h('input', { type: 'file', accept: '.csv', multiple: true });
    input.addEventListener('change', async () => {
      telemetry.rows = [];
      for (const f of input.files) parseCSV(await f.text()).forEach(r => telemetry.rows.push(Object.assign({ file: f.name }, r)));
      renderMain();
    });
    el.appendChild(card('Import', 'one or more CSV files', input));
    const rows = telemetry.rows;
    if (!rows.length) return;
    const byTower = {};
    rows.forEach(r => {
      const b = byTower[r.tower] = byTower[r.tower] || { tower: r.tower, waves: 0, dps: 0, damage: 0 };
      if (r.count > 0 && r.wave_seconds > 0) { b.waves++; b.dps += r.damage / r.wave_seconds / r.count; b.damage += r.damage; }
    });
    const towerRows = Object.values(byTower).map(b => {
      const t = E.towers.find(x => x.displayName === b.tower);
      const m = t ? E.model(t, t.upgradePaths.map(() => 0)) : null;
      return Object.assign(b, { real: b.waves ? b.dps / b.waves : 0, model: m ? m.dps.expected : null });
    });
    el.appendChild(h('div', { class: 'section' }, card('Damage per tower and second', 'average over waves; the model column is the base build, so upgraded towers read higher. Large gaps on base towers point at a model assumption to recalibrate (hangar duty, crowd spacing).', table({
      columns: [
        { key: 'tower', label: 'Tower', first: true },
        { key: 'waves', label: 'Waves', n: true },
        { key: 'damage', label: 'Damage', n: true, fmt: v => fmt(v, 0) },
        { key: 'real', label: 'Real DPS / tower', n: true, fmt: v => fmt(v, 2) },
        { key: 'model', label: 'Model DPS (base)', n: true, fmt: v => v == null ? '–' : fmt(v, 2) },
        { key: 'ratio', label: 'Real / model', n: true, value: r => r.model ? r.real / r.model : null, fmt: v => v == null ? '–' : '×' + fmt(v, 2) },
      ], rows: towerRows, sort: { key: 'damage', dir: 'desc' }, tall: true }))));
    const waves = {};
    rows.forEach(r => { const k = r.file + '|' + r.round; waves[k] = waves[k] || { round: r.round, fps: r.fps, lives: r.lives_end, money: r.money_end, file: r.file }; });
    const wl = Object.values(waves).sort((a, b) => a.round - b.round);
    el.appendChild(h('div', { class: 'grid cols-2 section' },
      chartCard('FPS per wave', 'average frames per real second', (c) => UI.lineChart(c, { series: [{ name: 'FPS', color: 'var(--s1)', values: wl.map(w => ({ x: w.round, y: w.fps })) }], xLabel: 'Round', yLabel: 'FPS', height: 220 })),
      chartCard('Lives after each wave', '', (c) => UI.lineChart(c, { series: [{ name: 'Lives', color: 'var(--s3)', values: wl.map(w => ({ x: w.round, y: w.lives })) }], xLabel: 'Round', yLabel: 'Lives', height: 220 }))));
  };

  VIEWS.warnings = function (el) {
    el.appendChild(head('Warnings', 'Problems found while extracting the data (prefab references, wave lists, level wiring) and by the models for each reachable build.'));
    const sevMap = { error: 'bad', warning: 'warn', info: 'info' };
    el.appendChild(card('Data checks', D.warnings.length + ' from extract.py', table({ columns: [
      { key: 'severity', label: 'Severity', value: w => ({ error: 0, warning: 1, info: 2 })[w.severity], render: w => status(sevMap[w.severity] || 'info', w.severity) },
      { key: 'area', label: 'Area' },
      { key: 'message', label: 'Message' },
      { key: 'where', label: 'Where', render: w => w.where ? h('code', { text: w.where }) : '–' },
    ], rows: D.warnings, sort: { key: 'severity', dir: 'asc' }, tall: true })));
    // model flags grouped by text
    const groups = new Map();
    E.models().forEach(m => m.flags.forEach(f => {
      if (f.sev === 'info') return;
      const key = f.text.replace(/[-\d.]+/g, '#');
      if (!groups.has(key)) groups.set(key, { sev: f.sev, example: f.text, builds: [] });
      groups.get(key).builds.push(m);
    }));
    const rows = Array.from(groups.values());
    el.appendChild(h('div', { class: 'section' }, card('Build model flags', rows.length + ' distinct issues across reachable builds', table({ columns: [
      { key: 'sev', label: 'Severity', value: r => (r.sev === 'bad' ? 0 : 1), render: r => status(r.sev) },
      { key: 'example', label: 'Issue (example)' },
      { key: 'n', label: 'Builds', n: true, value: r => r.builds.length },
      { key: 'which', label: 'Affected builds', sortable: false, render: r => h('span', { class: 'chips' }, r.builds.slice(0, 8).map(m => h('a', { class: 'tag', href: buildHref(m.tower, m.levels), text: m.label })), r.builds.length > 8 ? h('span', { class: 'muted small', text: '+' + (r.builds.length - 8) + ' more' }) : null) },
    ], rows, sort: { key: 'sev', dir: 'asc' }, tall: true }))));
  };

  // ------------------------------------------------------------------ methodology
  VIEWS.method = function (el) {
    el.appendChild(head('Methodology', 'How each number is derived, with the code it mirrors. Use this to judge which numbers are exact and which rest on assumptions.'));
    const P = (t) => h('p', null, t);
    const code = (t) => h('code', { text: t });
    const sec = (title, ...body) => h('div', { class: 'section' }, h('h3', { text: title }), ...body);
    const st = E.settings;
    el.appendChild(h('div', { class: 'prose' },
      sec('Pipeline',
        P(['extract.py parses the Unity YAML directly (scenes, prefabs, ScriptableObjects; nested prefabs and scene overrides are resolved), and writes ', code('balance-data.json'), '. The page loads the same data from ', code('balance-data.js'), ' and computes everything in ', code('engine.js'), ', a DOM-free module you can also run headless.']),
        h('pre', { text: 'python3 Tools/BalanceDashboard/extract.py      # refresh data after changing prefabs/assets\nopen Tools/BalanceDashboard/index.html' })),
      sec('Stats (exact)',
        P(['Stat.GetValue: base + Σ bonuses, then every % modifier compounds: v += v × m / 100 (cached until an upgrade changes the stat). Modules apply in purchase order (tier by tier, path by path; tier 3 last). FIRERATE is seconds between shots; "+X% fire rate" is written as a modifier of −X/(100+X)·100 (+25% → −20, +100% → −50). StatsManager.GetFireInterval clamps FIRERATE to ' + (D.constants.minFireInterval || 0.05) + ' s.']),
        P(['Reachable builds follow UpgradeManager.CheckPathBlocking and the upgrade panel: tiers in order, at most two paths with tier ≥ 1, one path at tier 3. That gives 34 builds per 3×3 tower. Prices go through GameManager.Price: × the difficulty\'s price multiplier (and the meta discount in veteran mode), rounded to 5 from 20 up whenever that multiplier is not 1.'])),
      sec('Fire rate (exact)',
        P(['FireCycle.cs carries leftover time from volley to volley (several volleys per frame are possible), so the rate does not depend on FPS or game speed:']),
        h('ul', null,
          h('li', null, 'Magazine towers (Laser, Core, Rocket, Sniper with Magazines): AMMO volleys FIRERATE apart, then RELOAD_SPEED; the first volley after a reload follows FIRERATE later. Period = AMMO × FIRERATE + RELOAD_SPEED.'),
          h('li', null, 'Continuous (Bullet Dispenser, Beam, AMMO ≤ 0): one volley per FIRERATE. Idle towers don\'t bank shots.'),
          h('li', null, 'Starfighter cannons: the cooldown keeps running during the reload and banks at most one shot, so a reload of at least 2 × FIRERATE ends with two shots at once: period = (AMMO − 2) × FIRERATE + max(2 × FIRERATE, RELOAD_SPEED) for AMMO ≥ 2 (max(FIRERATE, RELOAD_SPEED) for AMMO = 1).'),
          h('li', null, 'Laser, Core, rocket and Bullet Dispenser projectiles are data in ProjectileSystem, which has no cap. Pulses are pooled GameObjects; the pool grows on demand up to ' + (D.constants.maxPoolSize || 4096) + ' pulses, so neither caps the fire rate in practice.'))),
      sec('Hit chance (model)',
        P(['Laser and Core towers lead their shots (AimUtility.PredictIntercept): a shot hits if it can reach the intercept point before LIFETIME ends; spread is judged against a target at the intercept distance, and ' + Math.round((Number.isFinite(+st.leadEfficiency) ? +st.leadEfficiency : 0.9) * 100) + '% of reachable shots are assumed to hit because paths turn after the shot (setting "leadEfficiency"). Un-guided rockets and starfighter cannons aim at the current position (no lead), get a random pitch/yaw of ±(1 − ACCURACY) × 25°, then fly straight; a hit happens when the closest approach is ≤ enemy radius (' + fmt(E.enemyRadius, 3) + ') + projectile radius.']),
        P(['The Bullet Dispenser never aims: every barrel fires along its fixed direction in the face plane. For each anchor the engine measures how much path lies inside each barrel\'s bullet tube (radius = hit radius, length = SPEED × LIFETIME). A lone enemy is hit by a volley with the share of its in-range path that a tube covers; in crowds every barrel hits up to PIERCING of the enemies inside its tube. Placement next to the path, with the ring plane along it, is everything for this tower.']),
        P(['"Hit %" averages over the best anchor\'s coverage on the selected level and the level\'s HP-weighted layer speeds. Guided rockets and starfighter ordnance home in and hit if they can catch the target before LIFETIME ends.'])),
      sec('Range, coverage and damage per pass (geometry)',
        P(['Towers are instantiated at the anchor\'s anchorPointPosition with the anchor\'s rotation. The Targetter (HalfSphere mesh, radius 1 × RANGE; Hangar: sphere) is placed with the prefab\'s offsets. An enemy is in range when its sphere touches that volume. Coverage = path length inside it for the best free anchor. Beam towers use a ray of RANGE + 0.5 in the face plane, rotated to cover the most path; their crowd damage only counts the part of the ray that runs along the path.']),
        P(['Damage per pass = expected DPS × coverage ÷ the level\'s HP-weighted layer speed: what one copy deals to a lone enemy walking through. "Solo kills up to" checks every enemy type against its own total HP and layer speeds.'])),
      sec('Crowd damage (approximation)',
        P(['Enemies on a straight stretch of path are spaced by the configured spacing (default: count-weighted speed × spawn delay of the level\'s waves = ' + fmt(E.levelMix().spacing, 2) + ' units). A hit then reaches 1 + stretch ÷ spacing enemies, where the stretch is 2 × (blast radius + enemy radius) for rockets, the hit diameter for piercing projectiles (capped by PIERCING), the path overlap of the beam (capped by PIERCING), for pulses everything inside the 2.5 radius sphere plus what walks into it while it shrinks to 0.5 over LIFETIME (2.5 + max(2.5, speed × LIFETIME + 0.5) + 2 enemy radii) and most of the range for the flamethrower aura. Cluster bomblets: 6 per rocket with half the rocket\'s damage and 60% of its blast radius, about half land on the path.'])),
      sec('Starfighters (assumption based)',
        P(['Fighters = RoundToInt(AMOUNT), max ' + 6 + '. The share of time a target sits in the 30° fire cone is the "duty" assumption (' + st.hangarDuty + '). Missiles fire at strafe start, carpet bombs every 0.3 s while strafing; both deal ordnanceDamageMultiplier × DAMAGE (explosive) in RADIUS. Attack run ' + st.hangarRunTime + ' s, strafe ' + st.hangarStrafeTime + ' s, both divided by the Afterburners speed multiplier. Compare with the Telemetry tab to calibrate these.'])),
      sec('Mine Factory (production exact, use assumed)',
        P(['MineFactoryActionStrategy produces AMOUNT mines every FIRERATE seconds (FireCycle, only while a wave runs) up to AMMO mines on the field, and lobs each onto a random spot of the path inside a sphere of RANGE − 0.1 around the factory (best spread of ' + 8 + ' candidates, nudged by the targeting: FIRST towards the exit, LAST towards the entrance, NEAREST / FARTHEST by distance to the factory). The mine arms when it lands, after max(0.35 s, flight distance ÷ max(0.5, SPEED)). A mine goes off when an enemy comes within 0.45 × SIZE (the segment the enemy covered that frame counts): DAMAGE as EXPLOSIVE in RADIUS, PIERCING detonations per mine with 0.4 s to re-arm. Cluster bomblets land up to clusterSpread ahead of and behind the mine with clusterDamageShare / clusterRadiusShare. Seeker mines drift towards enemies within seekRadius; the simulator models that as a contact radius bigger by a quarter of it.']),
        P(['DPS is the production rate × damage: an upper bound that only holds while enemies use the mines up. A lone enemy walking through a full field sets every mine off, so the damage per pass adds the stocked burst (capacity × DAMAGE). When the field is full, Salvage upgrades scrap new mines for money (through the income multiplier) and every n-th one restores a life, never above the starting lives and at most maxLivesPerWave per wave. War Bonds pays per mine left on the field at the end of a wave.']),
        P(['Build value counts salvage as money: mines produced per wave (' + st.mineWaveSeconds + ' s) × the share of the wave the field is full (' + st.mineFieldFullShare + ') × scrap value, plus the dividends and restored lives (30% used, at the life value), over ' + st.mineEcoWaves + ' waves, expressed in base towers. The meta agent judges towers by combat value only, so it never buys the Salvage path.'])),
      sec('Enemies, damage types and traits (exact)',
        P(['Enemy.HandleDamageTaken applies a hit layer by layer: damage pops the current layer (1 money through GameManager.AddIncome), the enemy steps down one layer (RED of a shape becomes BLACK of the shape below) and the remainder carries over until it is used up. Id 0 and bosses die for good. "Layer overflow: pre-fix bug" replays the old recursive code for comparison.']),
        P(['Damage types: PROJECTILE (Laser, Core, Bullet Dispenser, Sniper, starfighter cannons), EXPLOSIVE (rockets, bomblets, pulse, starfighter ordnance, mines), MAGIC (Beam, flamethrower).']),
        h('ul', null,
          h('li', null, 'Armored: PROJECTILE hits lose the armor value (1 for regular shapes, 2 for bosses, plus the profile\'s armor bonus), EXPLOSIVE hits three quarters of it, MAGIC ignores it; a hit always deals at least 40%.'),
          h('li', null, 'Shielded: the first 2 + shape index hits (bosses 8, plus the profile\'s shield bonus) are absorbed completely, whatever their damage.'),
          h('li', null, 'Regenerating: after 3 s without damage the enemy regrows one layer every 2.5 s, up to the layer it spawned with.'),
          h('li', null, 'Profiles also scale enemy speed, the health of layers with ≥ 5 HP and boss health.'))),
      sec('Waves, economy and difficulty (exact)',
        P(['Spawner.SpawningWave spawns the first enemy at once and then waits each entry\'s delay, on game time (several spawns per frame are possible); every lane gets the full wave. After the last authored wave the last one repeats with counts += RoundToInt(count × factor × k) and delays −= delay × factor × k (min 0.01), factor = the profile\'s freeplay scaling. The game counts as won after the profile\'s win round (or the level\'s last wave).']),
        P(['Income: 1 per popped layer × the profile\'s income multiplier for the round (fractions are banked), plus the end-of-wave bonus (base + per-round × round). Lives −= Id + 1 per leak. Selling refunds the profile\'s refund rate × everything invested (purchase and upgrades at the prices paid). Start money = profile start money + the level\'s extraStartingMoney.']),
        P(['Meta upgrades (top bar "Meta"): a fresh profile owns none. Veteran mode owns every node of Resources/Progress/MetaUpgradeTree (' + metaSummary() + '). As in MetaUpgrades and GameManager they add start money, lives (not on Impossible), income (× the profile\'s multiplier), the wave bonus, a price discount (prices are then rounded to 5 from 20 up even on Medium) and refund points (refund rate capped at 100%); tower stat bonuses become % modifiers when a tower is built ("+X% fire rate" as −X/(100+X)·100). The main-menu backdrop never gets them.']),
        P(['Stream DPS = max over entries with ≥ 3 enemies of (total HP ÷ spawn delay) × lanes. Avg DPS = total HP ÷ time until the last undamaged enemy reaches the End.'])),
      sec('Simulator and meta agent',
        P(['Discrete frames (dt = game speed ÷ FPS). Enemies move along the cut path; towers run FireCycle and pick targets with their TargetBehaviour (FIRST = furthest along). Straight shots are resolved at fire time against every enemy\'s extrapolated motion; rockets explode at the first contact or at the end of their lifetime; damage lands after the flight time. Traits, slows, regeneration, income multipliers and end-of-wave bonuses are applied as in the game. Not modelled: line of sight through map geometry and enemy-enemy pushing.']),
        P(['The meta agent (Meta & difficulty tab, acceptance.py) is a greedy simulated player. Before every wave it keeps buying the option with the best value gain per $ for the next five waves (worst trait share weighted most): a new tower on the best free anchor of each lane, or the next tier of an existing tower. Its values are calibrated per level with short probe games, scaled by lane balance and by a mild preference for not piling everything into one tower type; once the anchors run out it buys building blocks for typical anchors. It is a stand-in for a reasonable player, not an optimal one: a combination it can win with is viable; one it loses with may still be viable for a human.'])),
      sec('Current assumptions', table({ columns: [{ key: 'k', label: 'Setting' }, { key: 'v', label: 'Value' }], rows: Object.keys(st).map(k => ({ k, v: st[k] == null ? 'auto' : String(st[k]) })), tall: true }))));
  };

  // ------------------------------------------------------------------ data & export
  VIEWS.data = function (el) {
    const meta = D.meta || {};
    el.appendChild(head('Data & export', 'Raw data and computed tables for spreadsheets or AI agents.'));
    el.appendChild(card('Source', null, h('dl', { class: 'kv' },
      h('dt', { text: 'Generated' }), h('dd', { text: meta.generatedAt || '–' }),
      h('dt', { text: 'Unity' }), h('dd', { text: meta.unityVersion || '–' }),
      h('dt', { text: 'Project' }), h('dd', null, h('code', { text: meta.project || '–' })),
      h('dt', { text: 'Contents' }), h('dd', { text: `${E.towers.length} towers, ${E.enemies.length} enemies, ${Object.keys(D.waves).length} wave assets, ${E.levels.length} levels, ${D.warnings.length} data warnings` }),
      h('dt', { text: 'Refresh' }), h('dd', null, h('code', { text: 'python3 Tools/BalanceDashboard/extract.py' })))));
    const btns = h('div', { class: 'toolbar section' },
      h('button', { class: 'primary', onclick: () => UI.download('3dtd-balance-summary.md', aiSummary(), 'text/markdown') }, 'Download AI summary (.md)'),
      h('button', { onclick: () => { navigator.clipboard && navigator.clipboard.writeText(aiSummary()).then(() => { copyBtn.textContent = 'Copied'; setTimeout(() => { copyBtn.textContent = 'Copy AI summary'; }, 1500); }); } }, 'Copy AI summary'),
      h('button', { onclick: () => UI.download('3dtd-analytics.json', JSON.stringify(analyticsJSON(), null, 1), 'application/json') }, 'Computed analytics (.json)'),
      h('button', { onclick: () => UI.download('balance-data.json', JSON.stringify(D, null, 1), 'application/json') }, 'Raw extracted data (.json)'));
    const copyBtn = btns.children[1];
    el.appendChild(btns);
    el.appendChild(h('div', { class: 'callout' }, 'The AI summary is a compact Markdown brief (settings, findings, tower and build tables, worst/best upgrades, wave pressure, warnings) meant to be pasted into a chat with an AI assistant. The analytics JSON contains every computed metric for every build, module, enemy and round.'));
    const pre = h('pre', { class: 'card section', style: { whiteSpace: 'pre-wrap', fontFamily: 'var(--mono)', fontSize: '12px', maxHeight: '60vh', overflow: 'auto' }, text: aiSummary() });
    el.appendChild(pre);
  };

  function modelRow(m) {
    return {
      build: m.label, tower: m.tower.key, levels: m.levels, cost: m.cost, sellLoss: m.sunk, mode: m.kind,
      volleysPerSec: r4(m.volleysPerSec), projectilesPerVolley: m.projectilesPerVolley, damage: m.stats.DAMAGE,
      rawDPS: r4(m.dps.raw), hitChance: r4(m.hit.level), expectedDPS: r4(m.dps.expected), crowdDPS: r4(m.dps.crowd),
      dpsPer100: r4(m.dps.perDollar), range: r4(m.range.radius), reach: m.projectile ? r4(m.projectile.reach) : null,
      coverage: m.coverage ? r4(m.coverage.best) : null, passDamage: m.coverage ? r4(m.coverage.passDamage) : null,
      passPer100: m.coverage ? r4(m.coverage.passDamage / m.cost * 100) : null, soloKillsUpTo: m.coverage ? soloName(m.coverage.maxSoloKill) : null,
      paybackSec: isFinite(m.paybackSec) ? r4(m.paybackSec) : null, fpsSwing: r4(m.fpsSwing), frameLimited: m.cycle.frameLimited,
      stats: Object.fromEntries(Object.entries(m.stats).map(([k, v]) => [k, r4(v)])), flags: m.flags.map(f => f.sev + ': ' + f.text),
    };
  }
  function r4(v) { return v == null || !isFinite(v) ? v : Math.round(v * 10000) / 10000; }
  function analyticsJSON() {
    const level = E.level();
    return {
      settings: E.settings, generatedFrom: D.meta,
      insights: E.insights(),
      builds: E.models().map(modelRow),
      modules: E.moduleRows().map(r => ({ tower: r.tower.key, path: r.path + 1, tier: r.tier + 1, name: r.module.name, price: r.price, description: r.module.description,
        statUpgrades: r.module.statUpgrades, deltaVolleyRate: r4(r.dRate), deltaRawDPS: r4(r.dRaw), deltaExpectedDPS: r4(r.dDPS), deltaCrowdDPS: r4(r.dCrowd),
        deltaRange: r4(r.dRange), deltaPassDamage: r4(r.dPass), passPer100: r4(r.passPer100), advertised: r.advertised })),
      enemies: E.enemies.map(e => ({ id: e.id, name: E.enemyName(e.id), health: e.health, speed: e.speed, totalHP: E.enemyTotalHP(e.id), money: E.enemyMoney(e.id), livesOnLeak: E.enemyLivesCost(e.id) })),
      waves: Object.fromEntries(E.playableLevels.map(L => [L.name, E.levelWaveTable(L).filter(r => !r.missing).map(r => ({ round: r.round + 1, wave: r.name, scaled: r.infinite, enemies: r.count, totalHP: r.hp, income: r.layers, livesAtRisk: r.lives, spawnDuration: r4(r.spawnDuration), streamDPS: r4(r.streamDPS), avgDPS: r4(r.avgDPS), moneyAtStart: r.moneyAtStart }))])),
      level: level.name, warnings: D.warnings,
    };
  }

  function aiSummary() {
    const level = E.level();
    const eco = E.economyOf(level);
    const st = E.settings;
    const lines = [];
    const md = (cols, rows) => {
      lines.push('| ' + cols.join(' | ') + ' |');
      lines.push('|' + cols.map(() => '---').join('|') + '|');
      rows.forEach(r => lines.push('| ' + r.join(' | ') + ' |'));
      lines.push('');
    };
    lines.push('# 3DTD balance summary', '');
    lines.push(`Generated from ${D.meta.generatedAt} data. Level: ${level.name} (${level.lanes.length} lane(s), path ${level.lanes.map(l => fmt(l.length, 1)).join('/')} units, ${level.spawner.waves.length} waves). Difficulty ${eco.difficulty}: ${eco.money} money, ${eco.lives} lives. Settings: ${st.fps} fps, ${st.gameSpeed}x speed, overflow model "${st.overflow}", meta upgrades "${st.meta || 'none'}".`, '');
    lines.push(eco.profile
      ? `Income is 1 per popped layer × the difficulty's round multiplier (${eco.profile.incomeBrackets.map(b => 'R' + b.fromRound + ' ×' + b.multiplier).join(', ')}) plus an end-of-wave bonus of ${eco.profile.endOfWaveBonusBase} + ${eco.profile.endOfWaveBonusPerRound} × round. Prices ×${eco.profile.priceMultiplier}; selling refunds ${Math.round(eco.profile.refundRate * 100)}% of everything invested. A leaked enemy costs its Id + 1 lives. Enemy traits: armored, shielded, regenerating (see Methodology).`
      : 'Income is 1 per popped layer. A leaked enemy costs its Id + 1 lives.', '');
    lines.push('## Key findings', '');
    E.insights().forEach(i => lines.push(`- [${i.sev}] ${i.area}: ${i.text}`));
    lines.push('');
    lines.push('## Base towers', '');
    md(['Tower', 'Cost', 'Volleys/s', 'Raw DPS', 'Hit %', 'Exp DPS', 'DPS/$100', 'Range', 'Coverage', 'Pass dmg', 'Pass/$100', 'Solo kills up to'],
      palette().map(baseModel).map(m => [m.tower.displayName, m.cost, fmt(m.volleysPerSec, 3), fmt(m.dps.raw, 3), pct(m.hit.level), fmt(m.dps.expected, 3), fmt(m.dps.perDollar, 3), fmt(m.range.radius, 2), m.coverage ? fmt(m.coverage.best, 1) : '-', m.coverage ? fmt(m.coverage.passDamage, 2) : '-', m.coverage ? fmt(m.coverage.passDamage / m.cost * 100, 3) : '-', m.coverage ? soloName(m.coverage.maxSoloKill) : '-']));
    const models = E.models().filter(m => m.coverage);
    lines.push('## Top 15 builds by pass damage per $100', '');
    md(['Build', 'Cost', 'Exp DPS', 'Crowd DPS', 'Pass dmg', 'Pass/$100', 'Frame-limited'],
      models.slice().sort((a, b) => b.coverage.passDamage / b.cost - a.coverage.passDamage / a.cost).slice(0, 15).map(m => [m.label, m.cost, fmt(m.dps.expected, 2), fmt(m.dps.crowd, 2), fmt(m.coverage.passDamage, 2), fmt(m.coverage.passDamage / m.cost * 100, 3), m.cycle.frameLimited ? 'yes' : 'no']));
    lines.push('## Strongest builds (expected DPS)', '');
    md(['Build', 'Cost', 'Exp DPS', 'Volleys/s', 'Frame-limited', 'Flags'],
      models.slice().sort((a, b) => b.dps.expected - a.dps.expected).slice(0, 10).map(m => [m.label, m.cost, fmt(m.dps.expected, 2), fmt(m.volleysPerSec, 2), m.cycle.frameLimited ? 'yes' : 'no', m.flags.filter(f => f.sev !== 'info').length]));
    const mods = E.moduleRows();
    lines.push('## Upgrade modules (all)', '');
    md(['Tower', 'Path/Tier', 'Module', 'Price', 'Δ volley rate', 'Δ exp DPS', 'Δ pass dmg', 'Pass/$100', 'Advertised'],
      mods.map(r => [r.tower.displayName, (r.path + 1) + '/' + (r.tier + 1), r.module.name, r.price, pct(r.dRate, true), fmt(r.dDPS, 3), fmt(r.dPass, 2), fmt(r.passPer100, 3), r.advertised.map(a => `${a.value > 0 ? '+' : ''}${a.value}${a.percent ? '%' : ''} ${a.what}`).join(', ')]));
    const rows = E.levelWaveTable(level).filter(r => !r.missing);
    lines.push('## Wave pressure (' + level.name + ')', '');
    md(['Round', 'Wave', 'Enemies', 'Total HP', 'Income', 'Lives at risk', 'Stream DPS', 'Money at start', 'DPS per $'],
      rows.map(r => [r.round + 1, r.name, r.count, fmt(r.hp, 0), r.layers, r.lives, fmt(r.streamDPS, 1), r.moneyAtStart, fmt(r.dpsPerDollar, 4)]));
    lines.push('## Data warnings', '');
    D.warnings.filter(w => w.severity !== 'info').forEach(w => lines.push(`- [${w.severity}] ${w.area}: ${w.message}`));
    lines.push('', 'Model notes: hit chance assumes projectiles aimed at the current enemy position without lead; starfighter numbers depend on the duty/run-time assumptions; crowd damage assumes evenly spaced streams.');
    return lines.join('\n');
  }

  // ------------------------------------------------------------------ boot
  parseHash();
  if (state.query.theme === 'light' || state.query.theme === 'dark') { state.theme = state.query.theme; applyTheme(); }
  renderTopbar();
  renderNav();
  renderMain();
  let resizeTimer = null;
  let lastW = window.innerWidth;
  window.addEventListener('resize', () => {
    if (Math.abs(window.innerWidth - lastW) < 40) return;
    lastW = window.innerWidth;
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(renderMain, 200);
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && popover) { popover.remove(); popover = null; } });
})();
