/*
 * 3DTD balance engine: every number the dashboard shows is computed here, from balance-data.js.
 * It has no DOM access, so it can also run headless (e.g. `jsc engine.js test.js`).
 *
 * The models mirror the C# code paths (file references in the Methodology tab). Where the game
 * depends on physics or AI behaviour that can't be derived from data alone, the assumption is a
 * named setting (see DEFAULT_SETTINGS) so it can be changed and is visible in the UI.
 */
(function (root) {
  'use strict';

  const DEFAULT_SETTINGS = {
    level: null,                // level name used for geometry, wave-mix and spacing based metrics
    difficulty: null,           // null = what the level's GameManager uses
    fps: 60,                    // frames per real second; fire timers carry their remainder, so this only affects the simulator step
    gameSpeed: 1,               // Time.timeScale; one frame advances gameSpeed / fps game seconds
    overflow: 'intended',       // 'intended' = Enemy.cs carry-over (current code), 'coded' = the pre-fix overflow bug, kept for comparison
    refDistance: 0.6,           // reference shot distance as a fraction of the detection radius
    spacing: null,              // enemy spacing along the path for crowd metrics; null = from the level's waves
    lifeValue: null,            // $ per life for the combined opportunity cost; null = start money / start lives
    hangarDuty: 0.5,            // share of time a starfighter has an enemy inside its fire cone
    hangarRunTime: 4.0,         // seconds per attack run (approach + strafe + break away) at 1x engine speed
    hangarStrafeTime: 1.5,      // seconds of strafing per run at 1x engine speed
    extraRounds: 10,            // rounds shown after the last authored wave (scaled repeats)
    leadEfficiency: 0.9,        // share of reachable leading shots that still hit (paths turn after the shot)
    mineWaveSeconds: 30,        // game seconds a wave runs, for Mine Factory salvage income per wave
    mineFieldFullShare: 0.5,    // share of a wave a Mine Factory's field is full (salvage only pays then)
    mineEcoWaves: 8,            // waves of salvage income counted in a Mine Factory build's value
  };

  const ENEMY_RADIUS_FALLBACK = 0.375;
  const MIN_FIRE_INTERVAL = 0.05;      // StatsManager.MinFireInterval
  const MAX_VOLLEYS_PER_FRAME = 8;     // FireCycle.MaxVolleysPerFrame
  const MIN_ARMOR_SHARE = 0.4;         // Enemy.MinArmorDamageShare
  const REGEN_DELAY = 3, REGEN_INTERVAL = 2.5;      // Enemy.RegenDelay / RegenInterval
  const TRAIT = { Armored: 1, Shielded: 2, Regenerating: 4 };
  const PULSE_RADIUS = 2.5;            // ProjectilePulse: SphereCollider r=0.5 scaled up to 5 on x/z
  const CYCLE_WINDOW = 120;            // seconds of simulated firing used to measure cycle rates
  // MineFactoryActionStrategy / Mine
  const MINE = { maxMines: 40, contactRadius: 0.45, rearmTime: 0.4, flightTime: 0.5, candidates: 8 };

  // ------------------------------------------------------------------------------------- small utils

  const sum = (a, f) => a.reduce((s, x) => s + (f ? f(x) : x), 0);
  const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
  const v3 = {
    add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]],
    sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
    mul: (a, s) => [a[0] * s, a[1] * s, a[2] * s],
    dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
    len: (a) => Math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]),
    norm: (a) => { const l = Math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; },
    cross: (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]],
    dist: (a, b) => Math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2),
  };
  // Mathf.RoundToInt uses banker's rounding
  function roundHalfEven(x) {
    if (!isFinite(x)) return NaN;
    const f = Math.floor(x), d = x - f;
    if (Math.abs(d - 0.5) < 1e-9) return f % 2 === 0 ? f : f + 1;
    return Math.round(x);
  }
  function mulberry32(seed) {
    return function () {
      seed |= 0; seed = seed + 0x6D2B79F5 | 0;
      let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }
  function median(arr) {
    if (!arr.length) return 0;
    const s = arr.slice().sort((a, b) => a - b);
    const m = s.length >> 1;
    return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2;
  }

  // ------------------------------------------------------------------------------------- engine

  const E = {
    data: null,
    settings: Object.assign({}, DEFAULT_SETTINGS),
    DEFAULT_SETTINGS,
    PULSE_RADIUS,
    util: { sum, clamp, v3, roundHalfEven, mulberry32, median },
    _cache: new Map(),
  };

  E.init = function (data, settings) {
    E.data = data;
    E.towers = data.towers.filter(t => t.kind === 'tower');
    E.towerByKey = Object.fromEntries(E.towers.map(t => [t.key, t]));
    E.enemies = data.enemies;
    E.enemyRadius = (data.constants.enemy && data.constants.enemy.colliderRadius) || ENEMY_RADIUS_FALLBACK;
    E.levels = data.levels;
    E.playableLevels = data.levels.filter(l => !l.isMainMenu && l.lanes && l.lanes.length);
    E.levelByName = Object.fromEntries(data.levels.map(l => [l.name, l]));
    E.settings = Object.assign({}, DEFAULT_SETTINGS, settings || {});
    if (!E.settings.level || !E.levelByName[E.settings.level])
      E.settings.level = (E.playableLevels[0] || data.levels[0] || {}).name;
    E._cache.clear();
    E._laneCache = new Map();
    return E;
  };

  E.setSettings = function (patch) {
    Object.assign(E.settings, patch);
    E._cache.clear();
  };

  function cached(key, fn) {
    if (E._cache.has(key)) return E._cache.get(key);
    const v = fn();
    E._cache.set(key, v);
    return v;
  }
  E.cached = cached;

  E.level = () => E.levelByName[E.settings.level];
  E.difficultyOf = (level) => E.settings.difficulty || (level && level.economy.difficulty) || 'Medium';
  E.TRAIT = TRAIT;

  // DifficultyProfile asset of the selected difficulty (GameManager.difficultyProfiles), or null
  E.profile = function (level) {
    level = level || E.level();
    const profiles = E.data.profiles || {};
    if (level && level.isMainMenu) return null;
    if (level && level.economy && level.economy.hasProfiles === false) return null;
    return profiles[E.difficultyOf(level)] || null;
  };
  E.economyOf = function (level) {
    level = level || E.level();
    const diff = E.difficultyOf(level);
    const p = E.profile(level);
    if (p) {
      return { difficulty: diff, money: p.startMoney + (level.economy.extraStartingMoney || 0), lives: p.lives, endOfWaveMoney: level.economy.endOfWaveMoney, profile: p,
               priceMultiplier: p.priceMultiplier, refundRate: p.refundRate, winRound: p.winRound };
    }
    // GameManager.Awake without profiles (legacy fields)
    const base = level.economy.baseStartingMoney, lives = level.economy.baseLives;
    const legacy = { Easy: [roundHalfEven(base * 1.5), lives * 2], Medium: [base, lives], Hard: [base, Math.max(1, roundHalfEven(lives * 0.5))], Impossible: [base, 1] }[diff] || [base, lives];
    return { difficulty: diff, money: legacy[0], lives: legacy[1], endOfWaveMoney: level.economy.endOfWaveMoney, profile: null,
             priceMultiplier: 1, refundRate: 1, winRound: Infinity };
  };
  // GameManager.Price: difficulty multiplier, rounded to 5 from 20 up
  E.price = function (base, level) {
    const p = E.profile(level);
    if (!p || Math.abs(p.priceMultiplier - 1) < 1e-9 || base <= 0) return base;
    const scaled = base * p.priceMultiplier;
    return scaled >= 20 ? roundHalfEven(scaled / 5) * 5 : roundHalfEven(scaled);
  };
  // DifficultyProfile.IncomeMultiplier: bracket with the highest fromRound <= round (1-based)
  E.incomeMultiplier = function (round, level) {
    const p = E.profile(level);
    if (!p || !p.incomeBrackets || !p.incomeBrackets.length) return 1;
    let m = 1, best = -Infinity;
    p.incomeBrackets.forEach(b => { if (b.fromRound <= round && b.fromRound >= best) { best = b.fromRound; m = b.multiplier; } });
    return m;
  };
  E.endOfWaveBonus = function (round, level) {
    const p = E.profile(level);
    const legacy = (level || E.level()).economy.endOfWaveMoney || 0;
    return legacy + (p ? Math.max(0, roundHalfEven(p.endOfWaveBonusBase + p.endOfWaveBonusPerRound * round)) : 0);
  };
  // Rounds the player has to clear to win (GameManager.GetWinRound)
  E.winRound = function (level) {
    level = level || E.level();
    const W = level.spawner ? level.spawner.waves.length : 0;
    const p = E.profile(level);
    return p ? clamp(p.winRound, 1, W) : W;
  };
  E.lifeValue = function (level) {
    if (E.settings.lifeValue != null && E.settings.lifeValue !== '') return +E.settings.lifeValue;
    const eco = E.economyOf(level);
    return eco.lives ? eco.money / eco.lives : 0;
  };

  // ===================================================================================== enemies

  E.enemyName = (id) => {
    const e = E.enemies[id];
    if (!e) return 'Id ' + id;
    return e.special ? 'Boss (' + e.color.toLowerCase() + ')' : cap(e.color) + ' ' + cap(e.shape);
  };
  function cap(s) { return s.charAt(0) + s.slice(1).toLowerCase(); }

  // Layers an enemy goes through until it dies for good (intended order: id, id-1, ..., 0)
  E.enemyLayers = function (id) {
    const e = E.enemies[id];
    if (e && e.special) return [e];
    const out = [];
    for (let i = id; i >= 0; i--) out.push(E.enemies[i]);
    return out;
  };
  // Enemy.LayerHealth: profile multipliers, 1-HP layers are never scaled
  E.layerHealth = function (id) {
    const e = E.enemies[id];
    const p = E.profile();
    if (!p) return e.health;
    if (e.special) return e.health * p.bossHealthMultiplier;
    return e.health >= 5 ? e.health * p.layerHealthMultiplier : e.health;
  };
  E.enemyTotalHP = (id) => sum(E.enemyLayers(id), e => E.layerHealth(e.id));
  E.enemySpeed = (id) => E.enemies[id].speed * ((E.profile() || {}).enemySpeedMultiplier || 1);
  // Enemy.ArmorValue / BaseShieldHits
  E.armorValue = (shapeIndex) => shapeIndex === 5 ? 2 : 1;
  E.baseShieldHits = (shapeIndex) => shapeIndex === 5 ? 8 : 2 + shapeIndex;
  E.damageTypeOf = function (model) {
    if (!model) return 'PROJECTILE';
    if (model.kind === 'beam' || model.kind === 'aura') return 'MAGIC';
    if (model.kind === 'pulse' || model.kind === 'mines') return 'EXPLOSIVE';
    if (model.strategy && model.strategy.type === 'BombTowerActionStrategy') return 'EXPLOSIVE';
    return 'PROJECTILE';
  };
  // Enemy.ApplyArmor for one hit
  E.armoredDamage = function (dmg, dtype, shapeIndex) {
    const p = E.profile();
    let armor = E.armorValue(shapeIndex) + (p ? p.armorBonus : 0);
    if (dtype === 'EXPLOSIVE') armor *= 0.75;
    else if (dtype !== 'PROJECTILE') armor = 0;
    return Math.max(dmg - armor, dmg * MIN_ARMOR_SHARE);
  };
  E.enemyMoney = (id) => (E.enemies[id] && E.enemies[id].special) ? 0 : id + 1;   // 1 per popped layer, bosses pay nothing
  E.enemyLivesCost = (id) => id + 1;                                                // GameManager: Lives -= enemy.Id + 1

  // Time to walk a path while being popped at a constant rate is hard to express; these are the
  // two bounds: never damaged, and stripped to the slowest layer immediately.
  E.traversalTime = (id, length) => length / E.enemies[id].speed;

  /*
   * Damage resolution. 'coded' replays Enemy.HandleDamageTaken / HandleDeathOfSingleShape /
   * HandleShapeOrColorChanged exactly, including the order in which the property setters fire events.
   * When a RED layer of shape >= 1 pops with overflow, `CurrentColor = BLACK` fires first and the
   * overflow lands on the *same* shape's BLACK layer; `CurrentShape--` only runs afterwards.
   * Indexing below 0 throws IndexOutOfRangeException in Unity: we flag the enemy as corrupted.
   */
  E.newEnemyState = function (id, traits) {
    const d = E.enemies[id];
    const p = E.profile();
    traits = traits || 0;
    return { shape: d.shapeIndex, color: d.colorIndex, hp: E.layerHealth(id), over: 0, dead: false, corrupt: false,
             special: d.special, data: id, depth: 0, traits, spawnId: id,
             shield: (traits & TRAIT.Shielded) ? E.baseShieldHits(d.shapeIndex) + (p ? p.shieldBonus : 0) : 0 };
  };
  E.enemyStateId = (s) => s.shape * 10 + s.color;

  class EnemyIndexError extends Error {}

  function codedTake(s, dmg, ctx) {
    if (++ctx.calls > 2000) throw new EnemyIndexError('recursion');
    s.hp -= dmg;
    s.over = -s.hp;
    if (s.hp <= 0) {
      if (s.special) { s.dead = true; return; }
      codedDie(s, ctx);
    }
  }
  function codedDie(s, ctx) {
    ctx.money += 1;
    ctx.pops += 1;
    const id = s.shape * 10 + s.color;
    if (s.corrupt) throw new EnemyIndexError('corrupt');
    if (id === 0) { s.dead = true; return; }
    if (s.color === 0) {
      codedSetColor(s, 9, ctx);
      codedSetShape(s, s.shape - 1, ctx);
    } else {
      codedSetColor(s, s.color - 1, ctx);
    }
  }
  function codedSetColor(s, c, ctx) { s.color = c; codedChanged(s, ctx); }
  function codedSetShape(s, sh, ctx) { s.shape = sh; codedChanged(s, ctx); }
  function codedChanged(s, ctx) {
    const id = s.shape * 10 + s.color;
    if (id < 0 || id >= E.enemies.length) { s.corrupt = true; throw new EnemyIndexError('id ' + id); }
    s.data = id;
    s.hp = E.enemies[id].health;
    if (s.over > 0) codedTake(s, s.over, ctx);
  }

  function intendedTake(s, dmg, ctx) {
    s.hp -= dmg;
    while (s.hp <= 0 && !s.dead) {
      if (s.special) { s.dead = true; return; }
      ctx.money += 1;
      ctx.pops += 1;
      const id = s.shape * 10 + s.color;
      if (id === 0) { s.dead = true; return; }
      const next = id - 1;
      s.shape = Math.floor(next / 10);
      s.color = next % 10;
      s.data = next;
      s.hp += E.layerHealth(next);
    }
  }

  // Applies one damage instance (Enemy.TakeDamage); returns {money, pops, exception, absorbed, dealt}
  E.applyDamage = function (s, dmg, mode, dtype) {
    const ctx = { money: 0, pops: 0, calls: 0, exception: false, absorbed: false };
    if (s.dead || dmg <= 0) return ctx;
    mode = mode || E.settings.overflow;
    if (s.shield > 0) { s.shield--; ctx.absorbed = true; return ctx; }
    if (s.traits & TRAIT.Armored) dmg = E.armoredDamage(dmg, dtype || 'PROJECTILE', s.shape);
    ctx.dealt = dmg;
    if (mode === 'intended') { intendedTake(s, dmg, ctx); return ctx; }
    if (s.corrupt) {
      // Corrupted enemies keep their stale health; every hit that leaves it <= 0 pays 1 and throws again
      s.hp -= dmg;
      if (s.hp <= 0) { ctx.money += 1; ctx.exception = true; }
      return ctx;
    }
    try { codedTake(s, dmg, ctx); } catch (e) { if (e instanceof EnemyIndexError) ctx.exception = true; else throw e; }
    return ctx;
  };

  // Repeated hits of a fixed size until the enemy dies (or 500 hits)
  E.resolveKill = function (id, dmg, mode) {
    return cached('kill|' + id + '|' + dmg + '|' + (mode || E.settings.overflow), () => {
      const s = E.newEnemyState(id);
      let money = 0, hits = 0, exception = false;
      const trace = [];
      while (!s.dead && hits < 500 && dmg > 0) {
        const before = E.enemyStateId(s);
        const r = E.applyDamage(s, dmg, mode);
        hits++;
        money += r.money;
        if (r.exception) exception = true;
        if (trace.length < 40) trace.push({ hit: hits, from: before, to: s.dead ? null : E.enemyStateId(s), hp: s.hp, money: r.money, exception: r.exception });
        if (s.corrupt) break;
      }
      return { id, dmg, hits: s.dead ? hits : Infinity, money, dead: s.dead, corrupt: s.corrupt, exception,
               endId: s.dead ? null : E.enemyStateId(s), trace,
               wasted: s.dead ? hits * dmg - E.enemyTotalHP(id) : null };
    });
  };

  // ===================================================================================== stats & builds

  // All upgrade states reachable under UpgradeManager.CheckPathBlocking: tiers are bought in order,
  // at most two paths ever get tier 1 (and therefore tier 2) and only one path can reach tier 3.
  E.allBuildLevels = function (tower) {
    const n = tower.upgradePaths.length;
    const maxTier = tower.upgradePaths.map(p => p.length);
    const out = [];
    const rec = (i, cur) => {
      if (i === n) {
        if (cur.filter(x => x >= 1).length <= 2 && cur.filter(x => x >= 3).length <= 1) out.push(cur.slice());
        return;
      }
      for (let t = 0; t <= maxTier[i]; t++) { cur.push(t); rec(i + 1, cur); cur.pop(); }
    };
    rec(0, []);
    if (!n) out.push([]);
    return out;
  };
  E.buildId = (tower, levels) => tower.key + '|' + levels.join('');
  E.buildLabel = (tower, levels) => tower.displayName + (levels.length ? ' ' + levels.join('-') : '');

  function statValue(base, bonuses, modifiers) {
    let v = base;
    bonuses.forEach(b => { v += b; });
    modifiers.forEach(m => { v += v * (m / 100); });
    return v;
  }

  // Purchase order: tier by tier, path by path (the tier 3 module is bought last)
  function modulesFor(tower, levels) {
    const mods = [];
    const maxT = Math.max(0, ...levels);
    for (let t = 0; t < maxT; t++)
      levels.forEach((lv, p) => { if (lv > t && tower.upgradePaths[p][t]) mods.push(tower.upgradePaths[p][t]); });
    return mods;
  }
  E.modulesFor = modulesFor;

  function strategyKind(st) {
    switch (st.type) {
      case 'LaserTowerActionStrategy': return 'magazine';
      case 'DroneTowerActionStrategy': return 'magazine';
      case 'BombTowerActionStrategy': return 'magazine';
      case 'SniperTowerActionStrategy': return 'sniper';
      case 'BeamTowerActionStrategy': return 'beam';
      case 'HangarTowerActionStrategy': return 'hangar';
      case 'MineFactoryActionStrategy': return 'mines';
      case 'BulletDispenserTowerActionStrategy': return st.AuraMode ? 'aura' : st.PulseMode ? 'pulse' : 'interval';
      default: return 'unknown';
    }
  }

  // Applies the upgrades in purchase order and returns the tower's configuration (no performance yet)
  E.configure = function (tower, levels) {
    const baseStats = tower.statsConfig.stats;
    const bonuses = {}, modifiers = {};
    E.data.constants.statTypes.forEach(s => { bonuses[s] = []; modifiers[s] = []; });
    const statsNow = () => {
      const o = {};
      E.data.constants.statTypes.forEach(s => { o[s] = statValue(baseStats[s] || 0, bonuses[s], modifiers[s]); });
      return o;
    };
    let strategy = Object.assign({}, tower.strategy);
    const issues = [];
    const goActive = {};
    tower.shootingPoints.forEach(sp => { if (sp.ref != null) goActive[sp.ref] = sp.active; });
    const totalPoints = tower.shootingPoints.length;
    // pool built in SetupActionStrategy from the fire rate at that moment
    let poolSetupFR = baseStats.FIRERATE;
    const hangar = { twin: !!strategy.twinLinkedCannons, missiles: strategy.missilesPerRun || 0, carpet: !!strategy.carpetBombing,
                     flightMult: strategy.flightSpeedMultiplier || 1, turnMult: strategy.turnRateMultiplier || 1 };
    const num = (v, d) => (v != null && v !== '' ? +v : d);
    const mines = { bomblets: num(strategy.clusterBomblets, 0), clusterDamageShare: num(strategy.clusterDamageShare, 0.5),
                    clusterRadiusShare: num(strategy.clusterRadiusShare, 0.6), clusterSpread: num(strategy.clusterSpread, 1.2),
                    blastSlow: num(strategy.blastSlow, 0), blastSlowDuration: num(strategy.blastSlowDuration, 0), seekRadius: num(strategy.seekRadius, 0),
                    scrapValue: num(strategy.scrapValue, 0), scrapsPerLife: num(strategy.scrapsPerLife, 0), maxLivesPerWave: num(strategy.maxLivesPerWave, 0),
                    waveEndPayout: num(strategy.waveEndPayoutPerMine, 0) };
    let aimAll = !!strategy.aimAtTarget;
    let slow = null;
    const mods = modulesFor(tower, levels);
    // prices after the difficulty multiplier (GameManager.Price)
    let cost = E.price(tower.cost);
    mods.forEach(m => {
      cost += E.price(m.price);
      m.statUpgrades.forEach(su => (su.isModifier ? modifiers : bonuses)[su.stat].push(su.value));
      m.activates.forEach(a => { goActive[a.go] = true; });
      m.deactivates.forEach(a => { goActive[a.go] = false; });
      m.behaviours.forEach(bh => {
        if (bh.type === 'ChangeActionStrategyUpgrade' && bh.strategy) {
          strategy = Object.assign({}, bh.strategy);
          poolSetupFR = statsNow().FIRERATE;
        } else if (bh.type === 'BombTowerLightAimUpgrade') {
          aimAll = true;   // sets aimAtTarget on every BombTowerActionStrategy component of the tower
        } else if (bh.type === 'BeamSlowUpgrade') {
          slow = { slowOnHit: bh.slowOnHit, slowDuration: bh.slowDuration };
        } else if (bh.type === 'StarfighterEngineUpgrade') {
          hangar.flightMult *= bh.flightSpeedMultiplier; hangar.turnMult *= bh.turnRateMultiplier;
        } else if (bh.type === 'StarfighterLoadoutUpgrade') {
          if (bh.twinLinkedCannons) hangar.twin = true;
          if (bh.carpetBombing) hangar.carpet = true;
          hangar.missiles += bh.additionalMissilesPerRun;
        } else if (bh.type === 'MineWarheadUpgrade') {
          mines.bomblets += bh.additionalClusterBomblets || 0;
          if ((bh.blastSlow || 0) > mines.blastSlow) { mines.blastSlow = bh.blastSlow; mines.blastSlowDuration = bh.blastSlowDuration; }
          mines.seekRadius = Math.max(mines.seekRadius, bh.seekRadius || 0);
        } else if (bh.type === 'MineSalvageUpgrade') {
          mines.scrapValue += bh.additionalScrapValue || 0;
          if (bh.scrapsPerLife > 0) mines.scrapsPerLife = mines.scrapsPerLife > 0 ? Math.min(mines.scrapsPerLife, bh.scrapsPerLife) : bh.scrapsPerLife;
          mines.maxLivesPerWave += bh.additionalMaxLivesPerWave || 0;
          mines.waveEndPayout += bh.additionalWaveEndPayoutPerMine || 0;
        }
      });
    });
    const stats = statsNow();
    if (strategy.type === 'BombTowerActionStrategy' && aimAll) strategy.aimAtTarget = 1;
    if (slow && strategy.type === 'BeamTowerActionStrategy') Object.assign(strategy, slow);
    const enabled = tower.shootingPoints.filter(sp => !sp.broken && goActive[sp.ref]);
    return { tower, levels, mods, cost, stats, strategy, kind: strategyKind(strategy), enabledPoints: enabled,
             totalPoints, poolSetupFR, hangar, mines, issues };
  };

  // ----------------------------------------------------------------------------------- fire cycle

  /*
   * FireCycle.cs: leftover time carries over between volleys and FIRERATE is clamped to MIN_FIRE_INTERVAL,
   * so the rate no longer depends on FPS or game speed.
   *   magazine/sniper (AMMO > 0): AMMO volleys FIRERATE apart, then RELOAD_SPEED; the first volley after a
   *     reload follows FIRERATE later (cooldown doesn't tick while reloading): period = AMMO*I + R.
   *   continuous (AMMO <= 0 or interval kinds): one volley every I.
   *   hangar (Starfighter.TryFireCannons): the cooldown keeps ticking during the reload and banks at most one shot
   *     (it never drops below -I), so a reload of R >= 2I ends with two shots at once:
   *     period = (AMMO-2)*I + max(2I, R) for AMMO >= 2, max(I, R) for AMMO = 1.
   */
  E.fireInterval = (st) => Math.max(MIN_FIRE_INTERVAL, st.FIRERATE);
  E.fireCycle = function (kind, st, opts) {
    const I = E.fireInterval(st);
    const A = st.AMMO, R = Math.max(0, st.RELOAD_SPEED || 0);
    const key = ['cyc', kind, I, A, R].join('|');
    return cached(key, () => {
      if (kind === 'unknown') return { rate: 0, first: Infinity, burstRate: 0, frameLimited: false, never: true, period: Infinity, perPeriod: 0 };
      let period = I, perPeriod = 1;
      if ((kind === 'magazine' || kind === 'sniper') && A > 0 && R > 0) {
        perPeriod = Math.ceil(A);
        period = perPeriod * I + R;
      } else if (kind === 'hangar' && A > 0 && R > 0) {
        perPeriod = Math.ceil(A);
        period = perPeriod >= 2 ? (perPeriod - 2) * I + Math.max(2 * I, R) : Math.max(I, R);
      }
      const rate = perPeriod / period;
      return { rate, first: 0, burstRate: 1 / I, frameLimited: false, never: rate <= 0, period, perPeriod,
               clamped: st.FIRERATE < MIN_FIRE_INTERVAL };
    });
  };

  // Volley times during the first `seconds` (target always present), for timeline charts
  E.fireTimes = function (model, seconds) {
    const st = model.stats, kind = model.kind;
    const c = E.fireCycle(kind, st, {});
    const I = E.fireInterval(st);
    const times = [], reloads = [];
    if (c.never) return { times, reloads };
    if (kind === 'hangar') return hangarFireTimes(st, I, seconds);
    let t = 0;
    while (t <= seconds && times.length < 4000) {
      for (let k = 0; k < c.perPeriod && t <= seconds; k++) {
        times.push(t);
        if (k < c.perPeriod - 1) t += I;
      }
      if (c.perPeriod > 1 || c.period > I + 1e-9) {
        const reloadEnd = t + st.RELOAD_SPEED;
        if (st.RELOAD_SPEED > 0) reloads.push([t, Math.min(seconds, reloadEnd)]);
        t = reloadEnd + I;
      } else t += I;
    }
    return { times, reloads };
  };
  // Starfighter.TryFireCannons / UpdateCannonCooldown replayed in continuous time (target always in the cone)
  function hangarFireTimes(st, I, seconds) {
    const A = Math.ceil(st.AMMO), R = Math.max(0, st.RELOAD_SPEED || 0);
    const times = [], reloads = [];
    let t = 0, cd = 0, mag = A;
    while (t <= seconds && times.length < 4000) {
      times.push(t);
      cd += I;
      if (--mag <= 0) {
        mag = A;
        if (R > 0) {
          reloads.push([t, Math.min(seconds, t + R)]);
          cd = Math.max(cd - R, -I);
          t += R;
        }
      }
      if (cd > 0) { t += cd; cd = 0; }
    }
    return { times, reloads };
  }

  // ----------------------------------------------------------------------------------- hit chance

  /*
   * Straight, non-homing projectile (ProjectileBasic, ProjectileBomb without aimAtTarget):
   * aimed at the enemy's current position (no lead), deviated by Euler(pitch, yaw, roll) with
   * pitch/yaw uniform in [-a, a], a = (1 - accuracy) * 25 degrees, then flying straight for `lifetime`.
   * Hit when the closest approach between projectile and enemy centre is <= enemy radius + projectile radius.
   * phi = angle between the enemy's movement and the line of sight.
   */
  E.hitChanceStraight = function (d, v, phi, speed, lifetime, rHit, spreadDeg, grid) {
    grid = grid || 9;
    const a = Math.max(0, spreadDeg) * Math.PI / 180;
    const vel = [v * Math.cos(phi), v * Math.sin(phi), 0];
    const n = a > 1e-6 ? grid : 1;
    let hits = 0;
    for (let i = 0; i < n; i++) {
      const al = n === 1 ? 0 : -a + 2 * a * (i + 0.5) / n;
      for (let j = 0; j < n; j++) {
        const be = n === 1 ? 0 : -a + 2 * a * (j + 0.5) / n;
        const u = [Math.cos(al) * Math.cos(be), Math.cos(al) * Math.sin(be), Math.sin(al)];
        const w = [vel[0] - speed * u[0], vel[1] - speed * u[1], vel[2] - speed * u[2]];
        const ww = v3.dot(w, w);
        let t = ww > 1e-9 ? -(d * w[0]) / ww : 0;
        t = clamp(t, 0, lifetime);
        const r = [d + w[0] * t, w[1] * t, w[2] * t];
        if (v3.len(r) <= rHit) hits++;
      }
    }
    return hits / (n * n);
  };

  // Average over crossing angles for an isotropic stream (weight sin(phi))
  E.hitChanceIsotropic = function (d, v, speed, lifetime, rHit, spreadDeg) {
    const key = ['hci', d, v, speed, lifetime, rHit, spreadDeg].join('|');
    return cached(key, () => {
      let num = 0, den = 0;
      const N = 18;
      for (let k = 0; k < N; k++) {
        const phi = Math.PI * (k + 0.5) / N;
        const w = Math.sin(phi);
        num += w * E.hitChanceStraight(d, v, phi, speed, lifetime, rHit, spreadDeg, 7);
        den += w;
      }
      return num / den;
    });
  };

  // Homing projectiles (MoveTowards the live target) hit as long as they can catch it before lifetime ends
  E.hitChanceHoming = function (d, v, speed, lifetime) {
    if (speed <= 0) return 0;
    const closing = speed - v * 0.5;   // average of head-on and chase
    if (closing <= 0) return 0;
    return d / closing <= lifetime ? 1 : 0;
  };

  // Leading shots (AimUtility.PredictIntercept): reachable if the intercept comes before the lifetime ends;
  // spread is then judged against a stationary target at the intercept distance
  E.hitChanceLead = function (d, v, speed, lifetime, rHit, spreadDeg) {
    if (speed <= 0) return 0;
    const closing = speed - v * 0.5;   // average of head-on and chase, as for homing
    if (closing <= 0) return 0;
    const t = d / closing;
    if (t > lifetime) return 0;
    return (+E.settings.leadEfficiency || 1) * E.hitChanceStraight(speed * t, 0, 0, speed, lifetime, rHit, spreadDeg, 9);
  };

  // ===================================================================================== geometry

  // Polyline sampling for a lane: cumulative distance, positions, directions
  E.laneSamples = function (lane, step) {
    step = step || 0.1;
    const key = lane.path.length + '|' + lane.length + '|' + step + '|' + lane.path[0].join(',');
    if (E._laneCache.has(key)) return E._laneCache.get(key);
    const pts = lane.path;
    const cum = [0];
    for (let i = 1; i < pts.length; i++) cum.push(cum[i - 1] + v3.dist(pts[i - 1], pts[i]));
    const total = cum[cum.length - 1];
    const samples = [];
    let seg = 1;
    for (let d = 0; d <= total + 1e-9; d += step) {
      while (seg < pts.length - 1 && cum[seg] < d) seg++;
      const a = pts[seg - 1], b = pts[seg];
      const L = cum[seg] - cum[seg - 1];
      const t = L > 0 ? (d - cum[seg - 1]) / L : 0;
      samples.push({ d, p: [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t],
                     dir: L > 0 ? v3.norm(v3.sub(b, a)) : [0, 0, 0] });
    }
    const out = { cum, total, samples, step, pts };
    E._laneCache.set(key, out);
    return out;
  };

  // Position and direction at path distance d
  E.lanePos = function (laneS, d) {
    const { cum, pts } = laneS;
    let lo = 1, hi = cum.length - 1;
    while (lo < hi) { const m = (lo + hi) >> 1; if (cum[m] < d) lo = m + 1; else hi = m; }
    const i = Math.max(1, lo);
    const a = pts[i - 1], b = pts[i], L = cum[i] - cum[i - 1];
    const t = L > 0 ? clamp((d - cum[i - 1]) / L, 0, 1) : 0;
    return { p: [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t], dir: L > 0 ? v3.norm(v3.sub(b, a)) : [0, 0, 0], seg: i };
  };

  // Tower-local -> world for a tower built on an anchor (Instantiate(prefab, anchorPointPosition, anchor.rotation))
  E.anchorToWorld = (anchor, local) => [
    anchor.pos[0] + local[0] * anchor.right[0] + local[1] * anchor.up[0] + local[2] * anchor.fwd[0],
    anchor.pos[1] + local[0] * anchor.right[1] + local[1] * anchor.up[1] + local[2] * anchor.fwd[1],
    anchor.pos[2] + local[0] * anchor.right[2] + local[1] * anchor.up[2] + local[2] * anchor.fwd[2],
  ];
  E.anchorDir = (anchor, local) => v3.norm([
    local[0] * anchor.right[0] + local[1] * anchor.up[0] + local[2] * anchor.fwd[0],
    local[0] * anchor.right[1] + local[1] * anchor.up[1] + local[2] * anchor.fwd[1],
    local[0] * anchor.right[2] + local[1] * anchor.up[2] + local[2] * anchor.fwd[2],
  ]);

  // Detection volume of a tower on an anchor: Targetter trigger scaled to RANGE, touched by the enemy sphere
  E.detectionVolume = function (model, anchor) {
    const tg = model.tower.targetter;
    const center = E.anchorToWorld(anchor, tg.center);
    const axis = E.anchorDir(anchor, tg.axis);
    return { center, axis, radius: model.range.radius, detect: model.range.radius + E.enemyRadius, hemisphere: tg.shape === 'hemisphere',
             muzzle: v3.add(center, v3.mul(axis, 0.3)) };
  };
  function insideVolume(vol, p) {
    const rel = v3.sub(p, vol.center);
    if (v3.len(rel) > vol.detect) return false;
    if (vol.hemisphere && v3.dot(rel, vol.axis) < -E.enemyRadius) return false;
    return true;
  }

  // Beam: ray from the barrel, parallel to the block face, rotatable around the face normal.
  // We pick the rotation that covers the most path (the player sets it with the rotation slider).
  E.beamGeometry = function (model, anchor, level) {
    const sp = model.cfg.enabledPoints[0] || model.tower.shootingPoints[0];
    const origin = E.anchorToWorld(anchor, [0, 0, sp && sp.pos ? sp.pos[2] : 0]);
    const length = model.stats.RANGE + 0.5;
    let best = { len: 0, dir: null, intervals: [] };
    for (let deg = 0; deg < 360; deg += 5) {
      const r = deg * Math.PI / 180;
      const local = [Math.cos(r), Math.sin(r), 0];
      const dir = E.anchorDir(anchor, local);
      const intervals = level.lanes.map(lane => {
        const S = E.laneSamples(lane);
        return toIntervals(S, s => {
          const rel = v3.sub(s.p, origin);
          const t = v3.dot(rel, dir);
          if (t < 0 || t > length) return false;
          return v3.len(v3.sub(rel, v3.mul(dir, t))) <= E.enemyRadius;
        });
      });
      const len = sum(intervals, iv => sum(iv, x => x[1] - x[0]));
      if (len > best.len) best = { len, dir, intervals, deg };
    }
    best.origin = origin;
    best.length = length;
    return best;
  };

  function toIntervals(S, pred) {
    const out = [];
    let start = null;
    S.samples.forEach((s, i) => {
      const inside = pred(s);
      if (inside && start === null) start = s.d;
      if (!inside && start !== null) { out.push([start, S.samples[i - 1].d + S.step]); start = null; }
    });
    if (start !== null) out.push([start, S.total]);
    return out;
  }

  // Bullet Dispenser (not in aura/pulse mode): barrels fire along fixed directions, it never aims
  E.isDispenser = (model) => model.kind === 'interval' && model.strategy && model.strategy.type === 'BulletDispenserTowerActionStrategy';

  function geoKey(model) {
    const first = model.cfg && model.cfg.enabledPoints[0];
    const sp = model.kind === 'beam' && first ? first.pos : null;
    const disp = E.isDispenser(model) && model.projectile
      ? [model.cfg.enabledPoints.map(p => p.ref).join(','), model.projectile.reach, model.projectile.rHit].join(';') : '';
    return [model.tower.key, model.kind === 'beam' ? 'beam' : 'vol', model.range.radius, sp ? sp.join(',') : '', disp].join('/');
  }

  // Path length inside each barrel's bullet tube (radius rHit, length = projectile reach) on an anchor
  E.dispenserRays = function (model, anchor, level) {
    const P = model.projectile;
    return model.cfg.enabledPoints.map(sp => {
      const origin = E.anchorToWorld(anchor, sp.pos || [0, 0, 0]);
      const dir = E.anchorDir(anchor, sp.fwd || [0, 1, 0]);
      const intervals = level.lanes.map(lane => toIntervals(E.laneSamples(lane), s => {
        const rel = v3.sub(s.p, origin);
        const t = v3.dot(rel, dir);
        if (t < 0 || t > P.reach) return false;
        return v3.len(v3.sub(rel, v3.mul(dir, t))) <= P.rHit;
      }));
      return { origin, dir, intervals, length: sum(intervals, iv => sum(iv, x => x[1] - x[0])) };
    });
  };

  // Path intervals (per lane) inside the tower's detection volume, plus geometry-based hit chances
  E.coverage = function (model, level, anchorIndex) {
    // depends only on the tower's geometry and range, not on the rest of the build
    const key = ['cov', geoKey(model), level.name, anchorIndex].join('|');
    return cached(key, () => {
      const anchor = level.anchors[anchorIndex];
      if (model.kind === 'beam') {
        const bg = E.beamGeometry(model, anchor, level);
        return { anchor: anchorIndex, intervals: bg.intervals, length: bg.len, beam: bg };
      }
      const vol = E.detectionVolume(model, anchor);
      const intervals = level.lanes.map(lane => toIntervals(E.laneSamples(lane), s => insideVolume(vol, s.p)));
      const out = { anchor: anchorIndex, intervals, length: sum(intervals, iv => sum(iv, x => x[1] - x[0])), vol };
      if (E.isDispenser(model) && model.projectile) {
        out.rays = E.dispenserRays(model, anchor, level);
        out.rayLength = sum(out.rays, r => r.length);
      }
      return out;
    });
  };

  // Best and median anchors for a model on a level
  E.levelCoverage = function (model, level) {
    level = level || E.level();
    const P = model.projectile;
    const straight = P && !P.homing && model.kind !== 'hangar';
    const vRef = E.levelMix(level).meanLayerSpeed || 2;
    const key = ['lcov', geoKey(model), level.name, straight ? [P.speed, P.lifetime, P.rHit, P.spreadDeg, P.lead].join(',') : ''].join('|');
    return cached(key, () => {
      const free = level.anchors.map((a, i) => i).filter(i => !level.anchors[i].blocked);
      const covs = free.map(i => E.coverage(model, level, i));
      // straight projectiles: rank anchors by hit-weighted coverage, not raw length (far, crossing paths are mostly missed)
      covs.forEach(c => {
        c.score = c.length;
        // the dispenser only hits what crosses its barrels' lines
        if (c.rays) { c.score = c.length > 0 ? c.rayLength : 0; return; }
        if (!straight || !c.vol || c.length <= 0) return;
        let sc = 0;
        level.lanes.forEach((lane, li) => {
          const S = E.laneSamples(lane);
          c.intervals[li].forEach(([a, b]) => {
            for (let d = a; d <= b; d += 0.25) {
              const smp = S.samples[Math.min(S.samples.length - 1, Math.round(d / S.step))];
              const rel = v3.sub(smp.p, c.vol.muzzle);
              const dist = v3.len(rel);
              const phi = dist < 1e-3 ? 0 : Math.acos(clamp(v3.dot(v3.norm(rel), smp.dir), -1, 1));
              sc += 0.25 * (dist < 1e-3 ? 1 : P.lead ? E.hitChanceLead(dist, vRef, P.speed, P.lifetime, P.rHit, P.spreadDeg)
                                                     : E.hitChanceStraight(dist, vRef, phi, P.speed, P.lifetime, P.rHit, P.spreadDeg, 5));
            }
          });
        });
        c.score = sc;
      });
      covs.sort((a, b) => b.score - a.score || b.length - a.length);
      const useful = covs.filter(c => c.length > 0);
      return { best: covs[0] || null, ranked: covs, usefulAnchors: useful.length, freeAnchors: free.length,
               bestLength: covs[0] ? covs[0].length : 0, medianLength: median(covs.map(c => c.length)),
               medianUseful: median(useful.map(c => c.length)) };
    });
  };

  // Average hit chance of a straight projectile across the best anchor's coverage (actual path angles)
  E.levelHitChance = function (model, level, speed) {
    const lc = E.levelCoverage(model, level);
    if (!lc.best || !lc.best.vol) return null;
    const p = model.projectile;
    const key = ['lhc', geoKey(model), level.name, speed, p.speed, p.lifetime, p.rHit, p.spreadDeg, p.homing, p.lead].join('|');
    return cached(key, () => {
      let num = 0, den = 0;
      level.lanes.forEach((lane, li) => {
        const S = E.laneSamples(lane);
        lc.best.intervals[li].forEach(([a, b]) => {
          for (let d = a; d <= b; d += 0.25) {
            const s = S.samples[Math.min(S.samples.length - 1, Math.round(d / S.step))];
            const rel = v3.sub(s.p, lc.best.vol.muzzle);
            const dist = v3.len(rel);
            if (dist < 1e-3) { num += 1; den += 1; continue; }
            const cosphi = clamp(v3.dot(v3.norm(rel), s.dir), -1, 1);
            num += p.homing ? E.hitChanceHoming(dist, speed, p.speed, p.lifetime)
                 : p.lead ? E.hitChanceLead(dist, speed, p.speed, p.lifetime, p.rHit, p.spreadDeg)
                 : E.hitChanceStraight(dist, speed, Math.acos(cosphi), p.speed, p.lifetime, p.rHit, p.spreadDeg, 7);
            den += 1;
          }
        });
      });
      return den ? num / den : null;
    });
  };

  // ===================================================================================== waves

  E.waveDef = (guid) => E.data.waves[guid];

  // Runtime wave for round index r (0-based), including the scaled repeats after the last wave
  E.runtimeWave = function (level, r) {
    const waves = level.spawner.waves;
    const W = waves.length;
    if (!W) return null;
    const prof = E.profile(level);
    const sf = prof ? prof.freeplayScaling : level.spawner.scalingFactor;
    if (r < W) {
      const def = waves[r] ? E.waveDef(waves[r]) : null;
      return def ? { round: r, name: def.name, infinite: false, entries: def.entries.map(e => Object.assign({}, e)) } : null;
    }
    const last = waves[W - 1] ? E.waveDef(waves[W - 1]) : null;
    if (!last) return null;
    const k = r - (W - 1);
    const scaling = sf * k;
    return {
      round: r, name: last.name + ' +' + k, infinite: true, scale: k,
      entries: last.entries.map(e => ({ enemy: e.enemy, count: e.count + roundHalfEven(e.count * scaling),
                                        delay: Math.max(0.01, e.delay - e.delay * scaling), traits: e.traits || 0 })),
    };
  };

  E.waveSummary = function (level, r) {
    const key = ['wave', level.name, r, E.settings.difficulty].join('|');
    return cached(key, () => {
      const w = E.runtimeWave(level, r);
      if (!w) return null;
      const lanes = level.lanes.length || 1;
      let t = 0, count = 0, hp = 0, layers = 0, lives = 0, peakStream = 0, money = 0;
      const comp = {};
      let lastArrival = 0, firstArrival = Infinity, slowestTraverse = 0;
      const meanLen = sum(level.lanes, l => l.length) / lanes;
      const traitCounts = {};
      w.entries.forEach(e => {
        const speed = E.enemySpeed(e.enemy);
        const H = E.enemyTotalHP(e.enemy);
        for (let i = 0; i < e.count; i++) {
          // Spawner.SpawningWave: the first enemy spawns at once, each spawn then waits its entry's delay
          const arrive = t + meanLen / speed;
          lastArrival = Math.max(lastArrival, arrive);
          firstArrival = Math.min(firstArrival, arrive);
          t += e.delay;
        }
        if (e.traits) Object.keys(TRAIT).forEach(k => { if (e.traits & TRAIT[k]) traitCounts[k] = (traitCounts[k] || 0) + e.count * lanes; });
        slowestTraverse = Math.max(slowestTraverse, meanLen / speed);
        count += e.count;
        hp += H * e.count;
        layers += E.enemyMoney(e.enemy) * e.count;
        money += E.enemyMoney(e.enemy) * e.count;
        lives += E.enemyLivesCost(e.enemy) * e.count;
        comp[e.enemy] = (comp[e.enemy] || 0) + e.count * lanes;
        if (e.count >= 3) peakStream = Math.max(peakStream, H / e.delay);
      });
      const spawnDuration = t;
      const incomeMult = E.incomeMultiplier(r + 1, level);
      return {
        round: r, name: w.name, infinite: w.infinite, entries: w.entries, lanes, traits: traitCounts,
        incomeMult, income: layers * lanes * incomeMult, bonus: E.endOfWaveBonus(r + 1, level),
        count: count * lanes, hp: hp * lanes, layers: layers * lanes, money: money * lanes, lives: lives * lanes,
        spawnDuration, firstArrival: isFinite(firstArrival) ? firstArrival : 0, lastArrival, composition: comp,
        avgDPS: lastArrival > 0 ? hp * lanes / lastArrival : 0,
        streamDPS: peakStream * lanes,
        hpPerSecond: spawnDuration > 0 ? hp * lanes / spawnDuration : hp * lanes,
        maxId: Math.max(...w.entries.map(e => e.enemy)),
        meanSpeed: count ? sum(w.entries, e => E.enemySpeed(e.enemy) * e.count) / count : 0,
      };
    });
  };

  E.levelWaveTable = function (level) {
    level = level || E.level();
    const key = ['wavetable', level.name, E.settings.extraRounds, E.difficultyOf(level)].join('|');
    return cached(key, () => {
      const eco = E.economyOf(level);
      const W = level.spawner ? level.spawner.waves.length : 0;
      const rows = [];
      let money = eco.money, cumHP = 0, cumLayers = 0, cumLives = 0, cumTime = 0;
      for (let r = 0; r < W + (+E.settings.extraRounds || 0); r++) {
        const s = E.waveSummary(level, r);
        if (!s) { rows.push({ round: r, missing: true }); continue; }
        cumHP += s.hp; cumLayers += s.layers; cumLives += s.lives; cumTime += s.spawnDuration;
        rows.push(Object.assign({}, s, {
          moneyAtStart: money, cumHP, cumLayers, cumLives, cumTime,
          incomePerMin: s.spawnDuration > 0 ? s.layers / (s.spawnDuration / 60) : 0,
          dpsPerDollar: money > 0 ? s.streamDPS / money : 0,
        }));
        money += Math.floor(s.income) + s.bonus;
      }
      return rows;
    });
  };

  // HP-weighted enemy mix of a level's authored waves (drives money/HP, reference speeds and spacing)
  E.levelMix = function (level) {
    level = level || E.level();
    return cached('mix|' + level.name, () => {
      const byId = {};
      let hpTotal = 0, spacingNum = 0, spacingDen = 0;
      (level.spawner ? level.spawner.waves : []).forEach(g => {
        const w = g && E.waveDef(g);
        if (!w) return;
        w.entries.forEach(e => {
          const H = E.enemyTotalHP(e.enemy) * e.count;
          byId[e.enemy] = (byId[e.enemy] || 0) + e.count;
          hpTotal += H;
          if (e.count > 1) { spacingNum += E.enemySpeed(e.enemy) * e.delay * e.count; spacingDen += e.count; }
        });
      });
      const ids = Object.keys(byId).map(Number);
      const money = sum(ids, id => E.enemyMoney(id) * byId[id]);
      // HP-weighted speed distribution of the *layers* the towers actually shoot at
      const speedHP = {};
      ids.forEach(id => E.enemyLayers(id).forEach(l => { const sp = E.enemySpeed(l.id); speedHP[sp] = (speedHP[sp] || 0) + E.layerHealth(l.id) * byId[id]; }));
      return { counts: byId, hpTotal, moneyPerHP: hpTotal ? money / hpTotal : 0, speedHP,
               spacing: spacingDen ? spacingNum / spacingDen : 1.5,
               meanLayerSpeed: hpTotal ? sum(Object.keys(speedHP), s => +s * speedHP[s]) / hpTotal : 2 };
    });
  };
  E.spacing = (level) => (E.settings.spacing != null && E.settings.spacing !== '') ? +E.settings.spacing : E.levelMix(level).spacing;

  // ===================================================================================== tower models

  /*
   * The full performance model of one build. Everything a table shows comes from here.
   */
  E.model = function (tower, levels) {
    if (typeof tower === 'string') tower = E.towerByKey[tower];
    const s = E.settings;
    const key = ['model', E.buildId(tower, levels), s.fps, s.gameSpeed, s.level, s.overflow, s.refDistance, s.spacing,
                 s.hangarDuty, s.hangarRunTime, s.hangarStrafeTime, s.leadEfficiency, E.difficultyOf(E.level())].join('|');
    return cached(key, () => buildModel(tower, levels));
  };

  function buildModel(tower, levels) {
    const s = E.settings;
    const cfg = E.configure(tower, levels);
    const st = cfg.stats;
    const kind = cfg.kind;
    const strat = cfg.strategy;
    const flags = [];
    const level = E.level();
    const mix = E.levelMix(level);
    const spacing = E.spacing(level);
    const rE = E.enemyRadius;
    const m = {
      id: E.buildId(tower, levels), tower, levels, label: E.buildLabel(tower, levels), kind, cfg, stats: st, flags,
      cost: cfg.cost, upgradeSpend: cfg.cost - E.price(tower.cost),
      strategy: strat, enabledPoints: cfg.enabledPoints.length, totalPoints: cfg.totalPoints,
    };
    const flag = (sev, text) => flags.push({ sev, text });
    // Selectable.GetSellValue: refund share of everything invested (WORTH scales it per tower)
    const eco = E.economyOf(level);
    const worth = st.WORTH > 0 ? st.WORTH : 1;
    m.refund = roundHalfEven(cfg.cost * eco.refundRate * worth);
    m.sunk = cfg.cost - m.refund;
    m.damageType = null;   // set below once the kind is known

    // ---- range
    const tg = tower.targetter || { radiusPerRange: 1, shape: 'sphere' };
    const radius = kind === 'beam' ? st.RANGE + 0.5 : st.RANGE * tg.radiusPerRange;
    m.range = { radius, detect: radius + rE, shape: kind === 'beam' ? 'ray' : tg.shape };
    if (kind === 'pulse') m.range.effective = Math.min(radius + rE, PULSE_RADIUS + rE);

    // ---- projectile
    let projInfo = strat.projectile || null;
    if (kind === 'hangar') projInfo = strat.cannonProjectile;
    const pierceInt = Math.trunc(st.PIERCING);
    const spreadDeg = (1 - st.ACCURACY) * (E.data.constants.maxSpreadDegrees || 25);
    if (projInfo && (kind === 'magazine' || kind === 'interval' || kind === 'hangar')) {
      const size = st.SIZE;
      m.projectile = {
        script: projInfo.script, prefab: projInfo.prefab,
        speed: st.SPEED, lifetime: st.LIFETIME, reach: Math.max(0, st.SPEED * st.LIFETIME),
        radius: (projInfo.colliderRadius || 0.075) * Math.max(0, size),
        homing: projInfo.script === 'ProjectileBomb' && !!strat.aimAtTarget,
        // LaserTowerActionStrategy.aimWithLead (default on): aims at the intercept point
        lead: strat.type === 'LaserTowerActionStrategy' && strat.aimWithLead !== 0 && strat.aimWithLead !== false,
        spreadDeg, pierce: pierceInt,
      };
      m.projectile.rHit = rE + m.projectile.radius;
      if (kind !== 'hangar' && m.projectile.reach + m.projectile.rHit < m.range.detect - 0.05)
        flag('warn', `Projectiles reach ${fmt(m.projectile.reach)} but enemies are detected up to ${fmt(m.range.detect)}: shots at the edge of the range expire before arriving.`);
      if (st.SPEED <= 0) flag('bad', 'Projectile SPEED is ' + fmt(st.SPEED) + ': projectiles never leave the barrel.');
      if (projInfo.script === 'ProjectileBasic' && pierceInt <= 0) flag('bad', 'PIERCING truncates to 0: projectiles die on the first enemy without dealing damage.');
    }

    // ---- fire cycle
    m.damageType = E.damageTypeOf({ kind, strategy: strat });
    const cyc = Object.assign({}, E.fireCycle(kind, st, {}));
    m.cycle = cyc;
    m.cycle.magazine = (kind === 'magazine' || kind === 'sniper' || kind === 'hangar') ? st.AMMO : null;
    m.cycle.reload = (kind === 'magazine' || kind === 'sniper' || kind === 'hangar') ? st.RELOAD_SPEED : null;
    if (cyc.clamped) flag('warn', `FIRERATE is ${fmt(st.FIRERATE, 3)} s; StatsManager clamps it to ${MIN_FIRE_INTERVAL} s. Percentage modifiers stacked this far are wasted.`);
    if (cyc.never) flag('bad', 'This configuration never fires (magazine of ' + fmt(st.AMMO) + ').');
    if ((kind === 'magazine') && st.AMMO > 0 && st.AMMO !== Math.floor(st.AMMO)) flag('info', 'AMMO is fractional; the magazine fires ceil(AMMO) volleys.');

    // ---- projectile pool: ProjectilePoolManager grows on demand up to MaxPoolSize
    if (kind === 'magazine' || kind === 'interval' || kind === 'pulse') {
      m.pool = { size: E.data.constants.maxPoolSize || 256, grows: true };
    }

    // ---- per volley and hit chance
    const P = m.projectile;
    const dRef = Math.max(0.3, s.refDistance * m.range.detect);
    const speeds = { red: 1.25, pink: 4.0, mix: mix.meanLayerSpeed };
    const hc = (v) => {
      if (kind === 'sniper' || kind === 'aura' || kind === 'pulse' || kind === 'beam') return 1;
      if (!P) return 1;
      if (P.homing) return E.hitChanceHoming(kind === 'hangar' ? 2 : dRef, v, P.speed, P.lifetime);
      // aimed at the intercept point: like a stationary target at the intercept distance
      if (P.lead) return E.hitChanceLead(dRef, v, P.speed, P.lifetime, P.rHit, P.spreadDeg);
      if (kind === 'hangar') return E.hitChanceStraight(2.0, v, 20 * Math.PI / 180, P.speed, P.lifetime, P.rHit, P.spreadDeg, 9);
      return E.hitChanceIsotropic(dRef, v, P.speed, P.lifetime, P.rHit, P.spreadDeg);
    };
    m.hit = { red: hc(speeds.red), pink: hc(speeds.pink), mix: 0, refDistance: dRef };
    // HP-weighted over the level's layer speed distribution
    const spHP = mix.speedHP;
    const spTot = sum(Object.keys(spHP), k => spHP[k]);
    m.hit.mix = spTot ? sum(Object.keys(spHP), k => spHP[k] * hc(+k)) / spTot : hc(2);
    // geometry based on the selected level's best anchor
    if (E.isDispenser(m) && P) {
      // Fixed barrels: a lone enemy is hit when it sits inside a barrel's bullet tube as a volley leaves,
      // i.e. with the share of its in-range path that each tube covers
      const lc = level && level.anchors && level.anchors.length ? E.levelCoverage(m, level) : null;
      const best = lc && lc.best;
      const nP = Math.max(1, cfg.enabledPoints.length);
      if (best && best.rays && best.length > 0) {
        const perVolley = sum(best.rays, r => Math.min(1, r.length / best.length));
        m.dispenser = { rayLengths: best.rays.map(r => r.length), rayLength: best.rayLength, perVolleySingle: perVolley, coverage: best.length };
        m.hit.level = m.hit.mix = m.hit.red = m.hit.pink = perVolley / nP;
      } else {
        m.dispenser = { rayLengths: [], rayLength: 0, perVolleySingle: 0, coverage: 0 };
        m.hit.level = m.hit.mix = m.hit.red = m.hit.pink = 0;
      }
    } else if (P && !P.homing && kind !== 'hangar' && level && level.anchors && level.anchors.length) {
      const lh = spTot ? sum(Object.keys(spHP), k => spHP[k] * (E.levelHitChance(m, level, +k) || 0)) / spTot : null;
      m.hit.level = lh;
    } else m.hit.level = m.hit.mix;

    // ---- damage per volley (single target) and crowd multipliers
    const dmg = st.DAMAGE;
    const n = (stretch) => 1 + Math.max(0, stretch) / spacing;    // enemies on a straight stretch of path
    let projectiles = 1, perHitTargets = 1, extra = 0;
    m.damagePerHit = dmg;
    switch (kind) {
      case 'magazine':
      case 'interval': {
        projectiles = cfg.enabledPoints.length;
        if (strat.type === 'BombTowerActionStrategy') {
          const R = st.RADIUS;
          m.aoe = { radius: R };
          perHitTargets = n(2 * (Math.max(0, R) + rE));
          if (strat.doClustering && projInfo && projInfo.cluster) {
            // ProjectileBomb.Clustering: every bomblet gets a share of the rocket's damage and radius
            const c = Object.assign({}, projInfo.cluster, { damage: dmg * (projInfo.cluster.damageShare != null ? projInfo.cluster.damageShare : 0.5),
                                                            radius: Math.max(0, R) * (projInfo.cluster.radiusShare != null ? projInfo.cluster.radiusShare : 0.6) });
            m.aoe.cluster = c;
            // bomblets fly c.speed * c.lifetime from the blast and explode with c.radius; ~half land on the path
            extra = c.count * 0.5 * n(2 * (c.radius + rE)) * c.damage;
          }
        } else {
          perHitTargets = Math.min(Math.max(1, pierceInt), n(2 * (P ? P.rHit : rE)));
        }
        break;
      }
      case 'sniper':
        projectiles = cfg.enabledPoints.length * (1 + (strat.secondShotStrongTargetting ? 1 : 0) + (strat.thirdShotLastTargetting ? 1 : 0));
        break;
      case 'beam': {
        projectiles = 1;
        // enemies inside the beam: only the part of the beam that runs along the path counts
        const blc = level && level.anchors && level.anchors.length ? E.levelCoverage({ tower, kind, range: m.range, cfg, stats: st, strategy: strat }, level) : null;
        const overlap = blc && blc.best ? Math.min(m.range.radius, blc.best.length) : m.range.radius;
        perHitTargets = Math.min(Math.max(0, pierceInt), n(overlap));
        m.beamTargets = Math.max(0, pierceInt);
        break;
      }
      case 'aura':
        perHitTargets = n(2 * m.range.detect * 0.75);
        break;
      case 'pulse':
        perHitTargets = n(2 * (PULSE_RADIUS + rE));
        break;
      case 'hangar':
        break;
      case 'mines': {
        // MineFactoryActionStrategy.Detonate: one EXPLOSIVE blast of DAMAGE in RADIUS, then bomblets along the path
        const R = Math.max(0, st.RADIUS), M = cfg.mines;
        m.aoe = { radius: R };
        perHitTargets = n(2 * (R + rE));
        if (M.bomblets > 0) {
          const cr = R * M.clusterRadiusShare;
          m.aoe.cluster = { count: M.bomblets, damage: dmg * M.clusterDamageShare, radius: cr, spread: M.clusterSpread };
          // bomblets land up to clusterSpread ahead of and behind the mine; most still catch part of the group
          extra = M.bomblets * 0.7 * n(2 * (cr + rE)) * dmg * M.clusterDamageShare;
        }
        break;
      }
    }
    m.projectilesPerVolley = projectiles;
    m.crowdTargets = perHitTargets;

    // ---- pool throughput cap
    let rate = cyc.rate;
    if (m.pool && !m.pool.error && P && projectiles > 0) {
      const flight = P.speed > 0 ? Math.min(P.lifetime, dRef / P.speed) : P.lifetime;
      const alive = m.hit.mix * flight + (1 - m.hit.mix) * Math.max(0, P.lifetime);
      const cap = m.pool.size / (alive + E.data.constants.poolReturnDelay + s.gameSpeed / s.fps) / projectiles;
      m.pool.capVolleysPerSec = cap;
      if (cap < rate) {
        flag('warn', `Projectile pool (${m.pool.size}) limits the tower to ${fmt(cap)} volleys/s instead of ${fmt(rate)}.`);
        rate = cap;
      }
    }
    if (m.pool && m.pool.error) rate = 0;
    m.volleysPerSec = rate;

    // ---- hangar
    if (kind === 'hangar') {
      const fighters = clamp(roundHalfEven(st.AMOUNT), 0, strat.maxStarfighters || 6);
      const muzzles = (strat.starfighter && strat.starfighter.cannonMuzzles) || 2;
      const H = cfg.hangar;
      const runTime = s.hangarRunTime / H.flightMult;
      const strafe = s.hangarStrafeTime / H.flightMult;
      const bombsPerRun = H.carpet ? Math.floor(strafe / ((strat.starfighter && strat.starfighter.bombDropInterval) || 0.3)) + 1 : 0;
      const ordDmg = dmg * (strat.ordnanceDamageMultiplier || 3);
      const ordTargets = n(2 * (Math.max(0, st.RADIUS) + rE));
      m.hangar = { fighters, twin: H.twin, missilesPerRun: H.missiles, carpet: H.carpet, flightMult: H.flightMult, turnMult: H.turnMult,
                   runTime, strafeTime: strafe, bombsPerRun, ordnanceDamage: ordDmg, ordnanceTargets: ordTargets,
                   cannonRate: cyc.rate, duty: s.hangarDuty };
      projectiles = H.twin ? muzzles : 1;
      m.projectilesPerVolley = projectiles;
      if (fighters < roundHalfEven(st.AMOUNT)) flag('info', `AMOUNT ${fmt(st.AMOUNT)} exceeds maxStarfighters ${strat.maxStarfighters}; extra fighters are ignored.`);
      flag('info', 'Starfighter numbers use the flight assumptions in Settings (duty, run time); fighters fly real AI paths in game.');
    }

    // ---- mine factory: production limits the damage, the stocked field adds a burst per pass
    if (kind === 'mines') {
      const M = cfg.mines;
      const perCycle = Math.max(1, roundHalfEven(st.AMOUNT));
      const cap = clamp(roundHalfEven(st.AMMO), 1, MINE.maxMines);
      const charges = Math.max(1, Math.trunc(st.PIERCING));
      const minesPerSec = perCycle / E.fireInterval(st);
      m.mines = Object.assign({}, M, { perCycle, cap, charges, minesPerSec, placementRadius: Math.max(0, st.RANGE - 0.1),
                                       contact: MINE.contactRadius * Math.max(0.1, st.SIZE),
                                       salvagePerSecFull: minesPerSec * M.scrapValue });
      projectiles = perCycle * charges;
      m.projectilesPerVolley = projectiles;
      flag('info', 'Mines only go off when enemies walk into them: DPS is the production rate (an upper bound while the field is being used up); the stocked field adds a burst to the damage per pass.');
    }

    // ---- DPS
    const single = (hitP) => {
      if (kind === 'hangar') {
        const h = m.hangar;
        const cannon = h.fighters * cyc.rate * s.hangarDuty * projectiles * dmg * hitP;
        const missiles = h.fighters * h.missilesPerRun * ordDmgOf(m) / h.runTime;
        const bombs = h.fighters * h.bombsPerRun * ordDmgOf(m) / h.runTime;
        return { cannon, missiles, bombs, total: cannon + missiles + bombs };
      }
      return { total: rate * projectiles * dmg * hitP };
    };
    function ordDmgOf(mm) { return mm.hangar.ordnanceDamage; }
    m.dps = {
      raw: single(1).total,
      red: single(m.hit.red).total,
      pink: single(m.hit.pink).total,
      expected: single(m.hit.level != null ? m.hit.level : m.hit.mix).total,
    };
    if (kind === 'hangar') {
      const h = m.hangar, parts = single(m.hit.mix);
      m.dps.parts = parts;
      m.dps.crowd = parts.cannon * Math.min(Math.max(1, pierceInt), n(2 * P.rHit)) + (parts.missiles + parts.bombs) * h.ordnanceTargets;
    } else if (m.dispenser) {
      // every barrel hits up to PIERCING of the enemies inside its tube
      const perVolley = sum(m.dispenser.rayLengths, L => Math.min(Math.max(1, pierceInt), L / spacing));
      m.dps.crowd = Math.max(m.dps.expected, rate * dmg * perVolley);
    } else {
      m.dps.crowd = m.dps.expected * perHitTargets + rate * projectiles * extra * (m.hit.level != null ? m.hit.level : m.hit.mix);
    }
    m.dps.perDollar = m.cost > 0 ? m.dps.expected / m.cost * 100 : 0;
    m.dps.crowdPerDollar = m.cost > 0 ? m.dps.crowd / m.cost * 100 : 0;
    m.incomePerSec = m.dps.expected * mix.moneyPerHP;   // pop income while saturated with targets
    m.paybackSec = m.incomePerSec > 0 ? m.cost / m.incomePerSec : Infinity;

    // ---- FPS / game-speed sensitivity (raw single-target dps at other frame rates)
    // FireCycle carries leftover time, so the rate is the same at every frame rate and game speed
    m.fpsSensitivity = [30, 60, 144].map(f => ({ fps: f, rate: cyc.rate }));
    m.speedSensitivity = [1, 2, 3].map(g => ({ gameSpeed: g, rate: cyc.rate }));
    const r30 = m.fpsSensitivity[0].rate, r144 = m.fpsSensitivity[2].rate;
    m.fpsSwing = r30 > 0 ? r144 / r30 : 1;

    // ---- level coverage
    if (level && level.anchors && level.anchors.length && level.lanes.length) {
      const lc = E.levelCoverage(m, level);
      m.coverage = { best: lc.bestLength, median: lc.medianLength, useful: lc.usefulAnchors, free: lc.freeAnchors,
                     bestAnchor: lc.best ? lc.best.anchor : null };
      const speedMix = mix.meanLayerSpeed || 2;
      // seconds a single enemy spends inside the best anchor's coverage, and damage it takes per pass
      m.coverage.timeInRange = lc.bestLength / speedMix;
      m.coverage.passDamage = m.dps.expected * m.coverage.timeInRange;
      m.coverage.passDamageRed = m.dps.red * (lc.bestLength / 1.25);
      m.coverage.passDamagePink = m.dps.pink * (lc.bestLength / 4.0);
      if (kind === 'mines' && lc.bestLength > 0) {
        // a lone enemy walking through a full field sets off every mine once (charged mines re-arm too late for most)
        const burst = m.mines.cap * dmg * (1 + (m.mines.charges - 1) * 0.5);
        m.coverage.passDamage += burst; m.coverage.passDamageRed += burst; m.coverage.passDamagePink += burst;
        m.mines.burst = burst;
      }
      // strongest enemy a single copy pops completely in one pass (ignoring other enemies)
      let best = -1;
      for (let id = 0; id < E.enemies.length; id++) {
        const e = E.enemies[id];
        if (e.special) continue;
        // time in range while being popped: approximate with the layer-HP-weighted average speed
        const layers = E.enemyLayers(id);
        const hp = sum(layers, l => E.layerHealth(l.id));
        const avgSpeed = hp / sum(layers, l => E.layerHealth(l.id) / E.enemySpeed(l.id));
        const t = lc.bestLength / avgSpeed;
        const hitV = m.hit.level != null ? m.hit.level : m.hit.mix;
        if (single(hitV).total * t >= hp) best = id; else break;
      }
      m.coverage.maxSoloKill = best;
      if (lc.bestLength <= 0) flag('warn', `No free anchor on ${level.name} reaches the path with this range.`);
    }

    // ---- overkill / overflow bug impact against the level's enemy mix
    const killDmg = dmg;
    if (killDmg > 0) {
      let hitsC = 0, hitsI = 0, moneyC = 0, moneyI = 0, wN = 0, exc = 0;
      Object.keys(mix.counts).forEach(k => {
        const id = +k, c = mix.counts[k];
        const rc = E.resolveKill(id, killDmg, 'coded'), ri = E.resolveKill(id, killDmg, 'intended');
        if (rc.exception) exc += c;
        hitsC += (isFinite(rc.hits) ? rc.hits : 500) * c; hitsI += ri.hits * c;
        moneyC += rc.money * c; moneyI += ri.money * c; wN += c;
      });
      const hpTot = sum(Object.keys(mix.counts), k => E.enemyTotalHP(+k) * mix.counts[k]);
      m.overkill = {
        wasteIntended: hitsI ? 1 - hpTot / (hitsI * killDmg) : 0,
        extraHitsCoded: hitsI ? hitsC / hitsI - 1 : 0,
        moneyDeltaCoded: moneyI ? moneyC / moneyI - 1 : 0,
        exceptionShare: wN ? exc / wN : 0,
      };
      if (s.overflow === 'coded') {
        if (m.overkill.exceptionShare > 0) flag('bad', `With ${fmt(killDmg)} damage per hit, ${pct(m.overkill.exceptionShare)} of this level's enemies hit the pre-fix overflow bug's IndexOutOfRangeException.`);
        else if (Math.abs(m.overkill.extraHitsCoded) > 0.005) flag('warn', `Pre-fix overflow bug: ${pct(m.overkill.extraHitsCoded, true)} hits to clear this level's enemy mix compared to correct carry-over.`);
      }
    }

    // ---- unread stats changed by upgrades
    const read = new Set((E.data.constants.statsRead[strat.type] || []).concat(['RANGE']));
    cfg.mods.forEach(mod => mod.statUpgrades.forEach(su => {
      if (!read.has(su.stat)) flag('warn', `"${mod.name}" changes ${su.stat}, which ${strat.type.replace('ActionStrategy', '')} never reads.`);
    }));
    if (tower.shootingPoints.some(sp => sp.broken)) flag('bad', 'Prefab is stale: shootingPoints reference GameObjects, the tower throws at runtime.');
    if (kind !== 'hangar' && kind !== 'mines' && kind !== 'aura' && kind !== 'pulse' && kind !== 'sniper' && kind !== 'beam' && cfg.enabledPoints.length === 0)
      flag('bad', 'No shooting point is enabled: the tower never spawns projectiles.');
    return m;
  }

  E.models = function (towers) {
    towers = towers || E.towers.filter(t => t.inPalette);
    const out = [];
    towers.forEach(t => E.allBuildLevels(t).forEach(lv => out.push(E.model(t, lv))));
    return out;
  };

  // Marginal value of each upgrade module: compared to the same build one tier lower on that path
  E.moduleRows = function (towers) {
    towers = towers || E.towers.filter(t => t.inPalette);
    const rows = [];
    towers.forEach(t => {
      t.upgradePaths.forEach((path, p) => path.forEach((mod, ti) => {
        const lvBefore = t.upgradePaths.map((_, i) => (i === p ? ti : 0));
        const lvAfter = t.upgradePaths.map((_, i) => (i === p ? ti + 1 : 0));
        const a = E.model(t, lvBefore), b = E.model(t, lvAfter);
        const d = b.dps.expected - a.dps.expected;
        const dc = b.dps.crowd - a.dps.crowd;
        const dRaw = b.dps.raw - a.dps.raw;
        const passA = a.coverage ? a.coverage.passDamage : 0, passB = b.coverage ? b.coverage.passDamage : 0;
        const mix = E.levelMix();
        rows.push({
          tower: t, path: p, tier: ti, module: mod, before: a, after: b,
          price: mod.price, cumulative: sum(path.slice(0, ti + 1), x => x.price),
          dDPS: d, dCrowd: dc, dRaw, dPass: passB - passA, passPer100: mod.price > 0 ? (passB - passA) / mod.price * 100 : 0, dRate: a.volleysPerSec > 0 ? b.volleysPerSec / a.volleysPerSec - 1 : (b.volleysPerSec > 0 ? Infinity : 0),
          dRange: b.range.radius - a.range.radius,
          dpsPer100: mod.price > 0 ? d / mod.price * 100 : 0,
          crowdPer100: mod.price > 0 ? dc / mod.price * 100 : 0,
          payback: d * mix.moneyPerHP > 0 ? mod.price / (d * mix.moneyPerHP) : Infinity,
          advertised: parseAdvertised(mod.description),
        });
      }));
    });
    return rows;
  };

  /*
   * Role-weighted value: each tower is judged on what it is for. Metrics are relative to the tower's base
   * build, weighted per role; the upgrade prices were fitted so that every legal build costs about
   * value / tier target (tier targets: T1 1.1, T2 1.0, T3 0.85 × the base tower's value per $).
   */
  E.ROLE_WEIGHTS = {
    'Laser Tower': { st: .4, crowd: .25, armor: .15, pass: .2 }, 'Bullet Dispenser': { crowd: .7, st: .1, shield: .2 },
    'Rocket System': { crowd: .55, st: .25, armor: .1, pass: .1 }, 'Beam Tower': { crowd: .5, st: .3, pass: .2 },
    'Sniper Tower': { st: .6, armor: .4 }, 'Hangar': { st: .45, crowd: .3, pass: .25 },
    'Mine Factory': { crowd: .45, pass: .35, armor: .2 },
  };
  // Money a Mine Factory's salvage path is worth over `mineEcoWaves` waves (an assumption, see Settings):
  // scrap while the field is full, the end-of-wave payout and restored lives at the life value
  E.mineEcoValue = function (m) {
    if (!m || m.kind !== 'mines' || !m.mines) return 0;
    const s = E.settings, M = m.mines;
    const scrapped = M.minesPerSec * s.mineWaveSeconds * s.mineFieldFullShare;
    const money = scrapped * M.scrapValue + M.cap * 0.8 * M.waveEndPayout;
    const lives = M.scrapsPerLife > 0 ? Math.min(M.maxLivesPerWave, scrapped / M.scrapsPerLife) * 0.3 * E.lifeValue() : 0;
    return (money + lives) * s.mineEcoWaves;
  };
  E.TIER_TARGETS = [1.1, 1.0, 0.85];
  // st/crowd = expected and crowd DPS, pass = damage per pass, armor/shield = DPS against armored octahedra and
  // shielded black cubes, pink = DPS against pink-speed layers
  E.valueMetrics = function (m) {
    const d = m.stats.DAMAGE > 0 ? m.stats.DAMAGE : 1;
    const armorF = E.armoredDamage(d, m.damageType, 2) / d;
    const H = 60, S = E.baseShieldHits(1), hits = H / d, shieldF = hits / (S + hits);
    let armor = m.dps.expected * armorF, shield = m.dps.expected * shieldF;
    if (m.kind === 'hangar' && m.dps.parts) {
      const p = m.dps.parts, od = m.hangar.ordnanceDamage || 1;
      armor = p.cannon * armorF + (p.missiles + p.bombs) * E.armoredDamage(od, 'EXPLOSIVE', 2) / od;
      shield = p.cannon * shieldF + (p.missiles + p.bombs) * (H / od) / (S + H / od);
    }
    return { st: m.dps.expected, crowd: m.dps.crowd, pass: m.coverage ? m.coverage.passDamage : 0, armor, shield, pink: m.dps.pink };
  };
  E.roleValue = function (m, base) {
    const w = E.ROLE_WEIGHTS[m.tower.displayName] || { st: 1 };
    const v = E.valueMetrics(m), b = E.valueMetrics(base);
    const combat = Object.keys(w).reduce((s, k) => s + w[k] * (b[k] > 0 ? v[k] / b[k] : 1), 0);
    // salvage income counts as money: its worth in base towers
    return combat + (m.kind === 'mines' && base.cost > 0 ? (E.mineEcoValue(m) - E.mineEcoValue(base)) / base.cost : 0);
  };
  // Value per $ of every legal build relative to the base tower (1 = as efficient as buying the base tower)
  E.buildEfficiency = function (tower) {
    const base = E.model(tower, tower.upgradePaths.map(() => 0));
    return E.allBuildLevels(tower).map(lv => {
      const m = E.model(tower, lv);
      const value = E.roleValue(m, base);
      const tier = Math.max(0, ...lv);
      return { tower, levels: lv, label: m.label, cost: m.cost, value, tier, efficiency: (value / m.cost) / (1 / base.cost),
               target: tier ? E.TIER_TARGETS[tier - 1] : 1 };
    });
  };

  // Pulls "+25% Fire-Rate"-style claims from descriptions, to compare with the modelled effect
  function parseAdvertised(text) {
    const out = [];
    const re = /([+-]?\d+(?:\.\d+)?)\s*(%?)\s*([A-Za-z][A-Za-z -]*)/g;
    let mm;
    (text || '').split('\n').forEach(line => {
      re.lastIndex = 0;
      while ((mm = re.exec(line))) {
        if (!/[+-]/.test(mm[1].charAt(0))) continue;
        out.push({ value: +mm[1], percent: mm[2] === '%', what: mm[3].trim() });
      }
    });
    return out;
  }
  E.parseAdvertised = parseAdvertised;

  // ===================================================================================== simulation

  /*
   * Discrete-time replay of a level: waves spawn per Spawner, enemies walk the lanes, towers fire with
   * the same timers as the C# strategies (one tick = one frame), and every shot is resolved against the
   * enemies' actual positions and velocities. Purchases happen between waves following `plan`.
   *
   * config: { level, builds: [{tower, levels, weight}], maxCopies, rounds, seed, dt }
   */
  // FireCycle.cs replay: returns the volleys to fire this frame and mutates fc
  E.newFireCycle = (ammo) => ({ cooldown: 0, mag: ammo, reloading: false, reloadTimer: 0 });
  E.tickFireCycle = function (fc, dt, hasTarget, interval, ammo, reload) {
    interval = Math.max(MIN_FIRE_INTERVAL, interval);
    const usesMag = ammo > 0;
    if (fc.reloading) {
      fc.reloadTimer -= dt;
      if (fc.reloadTimer > 0) return 0;
      fc.mag = ammo; fc.reloading = false;
      dt = -fc.reloadTimer;
    } else if (usesMag && fc.mag <= 0) fc.mag = ammo;
    fc.cooldown -= dt;
    if (!hasTarget) { if (fc.cooldown < 0) fc.cooldown = 0; return 0; }
    let volleys = 0, reloadStarted = false;
    while (fc.cooldown <= 0 && volleys < MAX_VOLLEYS_PER_FRAME) {
      volleys++;
      fc.cooldown += interval;
      if (usesMag) {
        fc.mag--;
        if (fc.mag <= 0) {
          fc.reloading = reload > 0;
          fc.reloadTimer = reload;
          if (!fc.reloading) fc.mag = ammo; else { reloadStarted = true; break; }
        }
      }
    }
    // A capped frame carries its debt (up to one more capped frame); after a reload starts, at most one volley
    const maxDebt = volleys >= MAX_VOLLEYS_PER_FRAME && !reloadStarted ? MAX_VOLLEYS_PER_FRAME * interval : interval;
    if (fc.cooldown < -maxDebt) fc.cooldown = -maxDebt;
    return volleys;
  };

  E.createSimulation = function (config) {
    const level = typeof config.level === 'string' ? E.levelByName[config.level] : (config.level || E.level());
    const s = E.settings;
    const eco = E.economyOf(level);
    const rng = mulberry32(config.seed || 1234);
    const dt = config.dt || s.gameSpeed / s.fps;
    const lanes = level.lanes.map(l => E.laneSamples(l));
    const W = level.spawner.waves.length;
    const rounds = config.rounds || (config.waves ? config.waves.length : W + (+s.extraRounds || 0));
    const mode = config.overflow || s.overflow;
    const rE = E.enemyRadius;

    const startMoney = config.money != null ? config.money : eco.money;
    const startLives = config.lives != null ? config.lives : eco.lives;
    const sim = {
      level, t: 0, round: 0, money: startMoney, lives: startLives, startLives, startMoney,
      towers: [], enemies: [], events: [], log: [], done: false, gameOverRound: null, waveActive: false,
      totals: { income: 0, spent: 0, leakedLayers: 0, leakedHP: 0, livesLost: 0, kills: 0, exceptions: 0, shots: 0, misses: 0, poolStarved: 0, damage: 0 },
      purchases: [],
    };

    // ---- purchase queue: copy 1 base, its upgrades, copy 2 base, ...
    const builds = (config.builds || []).map(b => Object.assign({}, b, { model: E.model(b.tower, b.levels) }));
    const anchorsTaken = new Set();
    const queue = [];
    const maxCopies = config.maxCopies || 999;
    let copyIndex = 0;
    function enqueueCopy() {
      if (copyIndex >= maxCopies * builds.length) return false;
      const b = builds[copyIndex % builds.length];
      copyIndex++;
      queue.push({ type: 'tower', build: b });
      E.modulesFor(b.model.tower, b.levels).forEach(mod => queue.push({ type: 'module', build: b, module: mod }));
      return true;
    }
    builds.length && enqueueCopy();

    function bestFreeAnchor(model) {
      const lc = E.levelCoverage(model, level);
      for (const c of lc.ranked) {
        if (c.length <= 0) return null;
        if (!anchorsTaken.has(c.anchor)) return c;
      }
      return null;
    }

    // API for custom buy policies (config.buyPolicy), e.g. the meta agent
    // Building blocks: once the level's own anchors are used up, players stack blocks to make new ones.
    // A virtual anchor copies the geometry of a typical (median useful) anchor for that tower and costs
    // blocks; it gets dearer the more of them exist.
    let virtualCount = 0;
    const blockCost = E.price(((E.data.blocks || [])[0] || {}).cost || 50, level);
    function virtualAnchor(model) {
      const lc = E.levelCoverage(model, level);
      const useful = lc.ranked.filter(c => c.length > 0);
      if (!useful.length) return null;
      const c = useful[Math.floor(useful.length / 2)];
      return Object.assign({}, c, { virtual: true, blockCost: blockCost * (1 + Math.floor(virtualCount / 6)) });
    }
    const api = {
      sim, level,
      freeAnchor: (model) => bestFreeAnchor(model) || (config.allowBlocks === false ? null : virtualAnchor(model)),
      // best free anchor overall plus the best free anchor of every lane
      freeAnchors(model) {
        const lc = E.levelCoverage(model, level);
        const out = [];
        const best = bestFreeAnchor(model);
        if (best) out.push(best);
        if (level.lanes.length > 1) {
          level.lanes.forEach((_, l) => {
            const c = lc.ranked.find(x => !anchorsTaken.has(x.anchor) && x.intervals && x.intervals[l] && sum(x.intervals[l], iv => iv[1] - iv[0]) > 0);
            if (c && out.indexOf(c) < 0) out.push(c);
          });
        }
        if (!out.length && config.allowBlocks !== false) { const v = virtualAnchor(model); if (v) out.push(v); }
        return out;
      },
      build(tower, cov) {
        const baseModel = E.model(tower, tower.upgradePaths.map(() => 0));
        const extra = cov && cov.virtual ? cov.blockCost : 0;
        if (!cov || sim.money < baseModel.cost + extra || (!cov.virtual && anchorsTaken.has(cov.anchor))) return null;
        sim.money -= baseModel.cost + extra; sim.totals.spent += baseModel.cost + extra;
        if (cov.virtual) { virtualCount++; sim.totals.blocks = (sim.totals.blocks || 0) + 1; }
        else anchorsTaken.add(cov.anchor);
        const tw = createTower(baseModel, { tower, levels: tower.upgradePaths.map(() => 0) }, cov);
        tw.cov = cov;
        tw.spent = baseModel.cost;
        sim.towers.push(tw);
        sim.purchases.push({ round: sim.round, t: sim.t, what: tower.displayName, cost: baseModel.cost, tower: tower.key });
        return tw;
      },
      upgrade(tw, path) {
        const t = tw.model.tower;
        const tier = tw.levelsNow[path];
        const mod = t.upgradePaths[path] && t.upgradePaths[path][tier];
        if (!mod) return false;
        const price = E.price(mod.price, level);
        if (sim.money < price) return false;
        sim.money -= price; sim.totals.spent += price;
        tw.levelsNow = tw.levelsNow.slice();
        tw.levelsNow[path] = tier + 1;
        upgradeTower(tw, E.model(t, tw.levelsNow));
        tw.spent = (tw.spent || 0) + price;
        sim.purchases.push({ round: sim.round, t: sim.t, what: t.displayName + ': ' + mod.name, cost: price, tower: t.key, path, tier });
        return true;
      },
    };

    function buyPhase() {
      if (config.buyPolicy) { config.buyPolicy(api); return; }
      if (config.buyOnce && sim.round > 0) return;
      let guard = 0;
      while (queue.length && guard++ < 500) {
        const item = queue[0];
        if (item.type === 'tower') {
          const baseModel = E.model(item.build.model.tower, item.build.levels.map(() => 0));
          const cov = bestFreeAnchor(item.build.model);
          if (!cov) { queue.length = 0; break; }
          if (sim.money < baseModel.cost) break;
          sim.money -= baseModel.cost; sim.totals.spent += baseModel.cost;
          anchorsTaken.add(cov.anchor);
          const tw = createTower(baseModel, item.build, cov);
          sim.towers.push(tw);
          item.build._last = tw;
          queue.shift();
          sim.purchases.push({ round: sim.round, t: sim.t, what: baseModel.tower.displayName, cost: baseModel.cost });
          if (!queue.some(q => q.type === 'tower')) enqueueCopy();
        } else {
          const price = E.price(item.module.price, level);
          if (sim.money < price) break;
          sim.money -= price; sim.totals.spent += price;
          const tw = item.build._last;
          tw.levelsNow = tw.levelsNow.slice();
          tw.levelsNow[item.module.path] = item.module.tier + 1;
          const newModel = E.model(tw.model.tower, tw.levelsNow);
          upgradeTower(tw, newModel);
          queue.shift();
          sim.purchases.push({ round: sim.round, t: sim.t, what: tw.model.tower.displayName + ': ' + item.module.name, cost: price });
          if (!queue.some(q => q.type === 'tower')) enqueueCopy();
        }
      }
    }

    function createTower(model, build, cov) {
      const anchor = level.anchors[cov.anchor];
      const tw = { model, build, anchor, anchorIndex: cov.anchor, levelsNow: build.levels.map(() => 0),
                   fc: E.newFireCycle(model.stats.AMMO), pool: [], damage: 0, shots: 0, fighters: [], mines: [], scrapped: 0, livesThisWave: 0 };
      setupGeometry(tw);
      setupPool(tw);
      return tw;
    }
    function upgradeTower(tw, newModel) {
      const swapped = newModel.strategy.type !== tw.model.strategy.type || newModel.kind !== tw.model.kind ||
        newModel.strategy.fileID !== tw.model.strategy.fileID;
      tw.model = newModel;
      setupGeometry(tw);
      if (swapped) {
        // ChangeActionStrategyUpgrade -> SetupActionStrategy: fresh timers and a new pool
        tw.fc = E.newFireCycle(newModel.stats.AMMO);
        setupPool(tw);
      }
    }
    function setupGeometry(tw) {
      const cov = E.coverage(tw.model, level, tw.anchorIndex);
      tw.intervals = cov.intervals;
      tw.vol = cov.vol || E.detectionVolume(tw.model, tw.anchor);
      tw.beam = cov.beam || null;
      tw.rays = cov.rays || null;
      tw.muzzle = tw.vol.muzzle;
    }
    function setupPool(tw) {
      const m = tw.model;
      if (m.pool) tw.pool = m.pool.error ? null : new Array(Math.max(0, m.pool.size)).fill(0);
      else tw.pool = null;
      if (m.kind === 'hangar') {
        tw.fighters = [];
        tw.launchAt = sim.t + 0.5;
      }
    }

    // ---- enemies
    function spawnSchedule(r) {
      const w = config.waves ? config.waves[r] : E.runtimeWave(level, r);
      if (!w) return [];
      const list = [];
      level.lanes.forEach((lane, li) => {
        let t = 0;
        w.entries.forEach(e => {
          for (let i = 0; i < e.count; i++) {
            // Spawner.SpawningWave: spawn first, then wait the entry's delay
            list.push({ at: sim.t + t, lane: li, id: e.enemy, traits: e.traits || 0 });
            t += e.delay;
          }
        });
      });
      list.sort((a, b) => a.at - b.at);
      return list;
    }
    function activate(sp) {
      const st = E.newEnemyState(sp.id, sp.traits);
      const en = { state: st, lane: sp.lane, d: 0, alive: true, uid: sim.nextUid = (sim.nextUid || 0) + 1, startId: sp.id,
                   slow: 0, slowT: 0, sinceHit: 0, regenT: 0 };
      updateEnemyPos(en);
      sim.enemies.push(en);
      sim.lanesSorted = false;
    }
    function updateEnemyPos(en) {
      const lp = E.lanePos(lanes[en.lane], en.d);
      en.p = lp.p;
      en.dir = lp.dir;
    }
    function enemySpeed(en) { return E.enemySpeed(en.state.data) * (1 - (en.slow || 0)); }

    // Enemy.TakeDamage; income goes through GameManager.AddIncome (round multiplier, fractional remainder)
    function damage(en, amount, tw, dtype) {
      if (!en.alive) return;
      en.sinceHit = 0; en.regenT = 0;
      const r = E.applyDamage(en.state, amount, mode, dtype || (tw ? tw.model.damageType : 'PROJECTILE'));
      if (r.absorbed) { sim.totals.absorbed = (sim.totals.absorbed || 0) + 1; return; }
      sim.incomeRemainder = (sim.incomeRemainder || 0) + r.money * E.incomeMultiplier(sim.round + 1, level);
      const whole = Math.floor(sim.incomeRemainder);
      sim.incomeRemainder -= whole;
      sim.money += whole;
      sim.totals.income += whole;
      sim.wave.income += whole;
      const dealt = r.dealt != null ? r.dealt : amount;
      sim.totals.damage += dealt;
      if (tw) tw.damage += dealt;
      if (r.exception) { sim.totals.exceptions++; sim.wave.exceptions++; }
      if (en.state.dead) { en.alive = false; sim.totals.kills++; sim.wave.kills++; }
    }

    // ---- targeting helpers
    function enemiesIn(tw) {
      const out = [];
      for (const en of sim.enemies) {
        if (!en.alive) continue;
        const iv = tw.intervals[en.lane];
        if (!iv) continue;
        for (const x of iv) if (en.d >= x[0] && en.d <= x[1]) { out.push(en); break; }
      }
      return out;
    }
    function pickTarget(list, how) {
      if (!list.length) return null;
      if (how === 'LAST') return list.reduce((a, b) => (b.d < a.d ? b : a));
      if (how === 'STRONGEST') {
        const sorted = list.slice().sort((a, b) => b.d - a.d);
        return sorted.reduce((a, b) => (E.enemyStateId(b.state) > E.enemyStateId(a.state) ? b : a));
      }
      return list.reduce((a, b) => (b.d > a.d ? b : a));   // FIRST
    }

    // ---- shots
    function takePoolSlot(tw) {
      if (!tw.pool) return true;
      for (let i = 0; i < tw.pool.length; i++) if (tw.pool[i] <= sim.t) return i;
      sim.totals.poolStarved++;
      return -1;
    }
    function spreadDir(base, spreadDeg) {
      if (spreadDeg <= 0) return base;
      const a = spreadDeg * Math.PI / 180;
      const al = -a + 2 * a * rng(), be = -a + 2 * a * rng();
      const up = Math.abs(base[1]) < 0.95 ? [0, 1, 0] : [1, 0, 0];
      const right = v3.norm(v3.cross(up, base));
      const up2 = v3.cross(base, right);
      return v3.norm(v3.add(v3.add(v3.mul(base, Math.cos(al) * Math.cos(be)), v3.mul(right, Math.sin(be))), v3.mul(up2, Math.sin(al))));
    }
    function fireStraight(tw, target, origin, fixedDir) {
      const m = tw.model, P = m.projectile;
      const slot = takePoolSlot(tw);
      if (slot === -1) return;
      sim.totals.shots++; tw.shots++;
      let aim = target ? target.p : v3.add(origin, fixedDir);
      if (P.lead && target) {
        // AimUtility.PredictIntercept against the enemy's current straight-line velocity
        const vel = v3.mul(target.dir, enemySpeed(target));
        const rel = v3.sub(target.p, origin);
        const a = v3.dot(vel, vel) - P.speed * P.speed, b = 2 * v3.dot(rel, vel), c = v3.dot(rel, rel);
        let t = -1;
        if (Math.abs(a) < 1e-4) t = Math.abs(b) > 1e-4 ? -c / b : -1;
        else {
          const disc = b * b - 4 * a * c;
          if (disc >= 0) { const r1 = (-b - Math.sqrt(disc)) / (2 * a), r2 = (-b + Math.sqrt(disc)) / (2 * a); t = Math.min(r1, r2); if (t < 0) t = Math.max(r1, r2); }
        }
        if (t > 0) aim = v3.add(target.p, v3.mul(vel, t));
      }
      const dir = spreadDir(fixedDir || v3.norm(v3.sub(aim, origin)), P.spreadDeg);
      const homing = P.homing && !!target;
      let deathT = P.lifetime;
      if (homing) {
        const dd = v3.dist(target.p, origin);
        const closing = P.speed - enemySpeed(target) * 0.5;
        if (closing > 0 && dd / closing <= P.lifetime) {
          deathT = dd / closing;
          scheduleExplosion(tw, sim.t + deathT, null, target, m);
        } else {
          // runs out of lifetime while chasing: ProjectileBomb explodes wherever it is
          sim.totals.misses++;
          const tgt = target;
          deathT = P.lifetime;
          schedule(sim.t + deathT, () => {
            const to = v3.sub(tgt.p, origin);
            const L = v3.len(to);
            const pos = v3.add(origin, v3.mul(v3.norm(to), Math.min(L, P.reach)));
            explode(tw, pos, m.stats.RADIUS, m.stats.DAMAGE);
          });
        }
      } else {
        // all enemies whose extrapolated path passes within rHit of the projectile line
        const hits = [];
        for (const en of sim.enemies) {
          if (!en.alive) continue;
          const rel = v3.sub(en.p, origin);
          if (v3.len(rel) > P.reach + 3) continue;
          const v = v3.mul(en.dir, enemySpeed(en));
          const w = v3.sub(v, v3.mul(dir, P.speed));
          const ww = v3.dot(w, w);
          let t = ww > 1e-9 ? -v3.dot(rel, w) / ww : 0;
          t = clamp(t, 0, P.lifetime);
          const dmin = v3.len(v3.add(rel, v3.mul(w, t)));
          if (dmin <= P.rHit) hits.push({ en, t: Math.max(0, t - Math.sqrt(Math.max(0, P.rHit * P.rHit - dmin * dmin)) / Math.max(1e-6, Math.sqrt(ww))) });
        }
        hits.sort((a, b) => a.t - b.t);
        if (!hits.length) {
          sim.totals.misses++;
          if (m.aoe) scheduleExplosion(tw, sim.t + P.lifetime, v3.add(origin, v3.mul(dir, P.reach)), null, m);
        } else if (m.aoe) {
          deathT = hits[0].t;
          scheduleExplosion(tw, sim.t + deathT, v3.add(origin, v3.mul(dir, P.speed * deathT)), null, m);
        } else {
          const n = Math.max(0, P.pierce);
          const used = hits.slice(0, Math.max(1, n));
          if (n > 0) used.forEach(h => schedule(sim.t + h.t, () => damage(h.en, m.stats.DAMAGE, tw)));
          deathT = n > 0 && hits.length >= n ? hits[n - 1].t : (n <= 0 ? hits[0].t : P.lifetime);
        }
      }
      if (tw.pool && slot >= 0) tw.pool[slot] = sim.t + deathT + E.data.constants.poolReturnDelay;
    }
    function scheduleExplosion(tw, at, pos, target, m) {
      schedule(at, () => {
        const center = pos || (target ? target.p : null);
        if (!center) return;
        explode(tw, center, m.stats.RADIUS, m.stats.DAMAGE);
        if (m.aoe && m.aoe.cluster) {
          const c = m.aoe.cluster;
          const dirs = [[1, 0, 0], [-1, 0, 0], [0, 0, 1], [0, 0, -1], [0.7, 0.7, 0], [-0.7, 0.7, 0]];
          for (let k = 0; k < c.count; k++) {
            const dd = dirs[k % dirs.length];
            const p = v3.add(center, v3.mul(dd, c.speed * c.lifetime));
            // bomblets get a share of the rocket's damage and radius
            schedule(at + c.lifetime, () => explode(tw, p, c.radius, c.damage));
          }
        }
      });
    }
    function explode(tw, center, radius, dmg) {
      for (const en of sim.enemies) if (en.alive && v3.dist(en.p, center) <= radius + rE) damage(en, dmg, tw, 'EXPLOSIVE');
    }
    function schedule(at, fn) {
      const ev = { at, fn };
      const h = sim.events;
      h.push(ev);
      let i = h.length - 1;
      while (i > 0) { const p = (i - 1) >> 1; if (h[p].at <= ev.at) break; h[i] = h[p]; h[p] = ev; i = p; }
    }
    function popEvent() {
      const h = sim.events;
      const top = h[0];
      const last = h.pop();
      if (h.length) {
        h[0] = last;
        let i = 0;
        for (;;) {
          const l = 2 * i + 1, r = l + 1;
          let m = i;
          if (l < h.length && h[l].at < h[m].at) m = l;
          if (r < h.length && h[r].at < h[m].at) m = r;
          if (m === i) break;
          [h[i], h[m]] = [h[m], h[i]];
          i = m;
        }
      }
      return top;
    }

    // ---- tower update (one frame)
    function towerTick(tw) {
      const m = tw.model, st = m.stats;
      const kind = m.kind;
      const inRange = (kind === 'beam') ? null : enemiesIn(tw);
      let target = inRange ? pickTarget(inRange, m.tower.targetBehaviour) : null;
      if (kind === 'hangar') { hangarTick(tw, inRange, target); return; }
      if (kind === 'mines') { minesTick(tw); return; }
      const usesMag = kind === 'magazine' || kind === 'sniper';
      const volleys = E.tickFireCycle(tw.fc, dt, kind === 'beam' ? true : !!target, st.FIRERATE,
                                      usesMag ? st.AMMO : 0, usesMag ? st.RELOAD_SPEED : 0);
      for (let v = 0; v < volleys; v++) {
        if (kind === 'sniper') {
          if (!target || !target.alive) target = pickTarget(enemiesIn(tw), m.tower.targetBehaviour);
          for (let k = 0; k < Math.max(1, m.enabledPoints); k++) {
            sim.totals.shots++; tw.shots++;
            if (target && target.alive) damage(target, st.DAMAGE, tw);
            if (m.strategy.secondShotStrongTargetting) { const t2 = pickTarget(enemiesIn(tw), 'STRONGEST'); if (t2) damage(t2, st.DAMAGE, tw); }
            if (m.strategy.thirdShotLastTargetting) { const t3 = pickTarget(enemiesIn(tw), 'LAST'); if (t3) damage(t3, st.DAMAGE, tw); }
          }
        } else if (kind === 'magazine' || kind === 'interval') {
          if (!target || !target.alive) target = pickTarget(enemiesIn(tw), m.tower.targetBehaviour);
          if (!target) break;
          if (tw.rays) tw.rays.forEach(r => fireStraight(tw, null, r.origin, r.dir));
          else for (let k = 0; k < m.enabledPoints; k++) fireStraight(tw, target, tw.muzzle);
        } else if (kind === 'aura') {
          sim.totals.shots++;
          enemiesIn(tw).forEach(en => damage(en, st.DAMAGE, tw, 'MAGIC'));
        } else if (kind === 'pulse') {
          sim.totals.shots++;
          const c = tw.vol.center;
          for (const en of sim.enemies) if (en.alive && v3.dist(en.p, c) <= PULSE_RADIUS + rE) damage(en, st.DAMAGE, tw, 'EXPLOSIVE');
        } else if (kind === 'beam') {
          let pierce = st.PIERCING;
          const b = tw.beam;
          if (!b || !b.dir) continue;
          const hits = [];
          for (const en of sim.enemies) {
            if (!en.alive) continue;
            const rel = v3.sub(en.p, b.origin);
            const t = v3.dot(rel, b.dir);
            if (t < 0 || t > b.length) continue;
            if (v3.len(v3.sub(rel, v3.mul(b.dir, t))) <= rE) hits.push({ en, t });
          }
          // RaycastNonAlloc sorted by distance: nearest enemies take the pierce first
          hits.sort((x, y) => x.t - y.t);
          sim.totals.shots++;
          const slow = m.strategy.slowOnHit || 0;
          for (const h of hits) {
            if (pierce <= 0) break;
            damage(h.en, st.DAMAGE, tw, 'MAGIC');
            if (slow > 0 && h.en.alive) applySlow(h.en, slow, m.strategy.slowDuration || 0.5);
            pierce--;
          }
        }
      }
    }
    // Enemy.UpdateRegeneration: one layer back every REGEN_INTERVAL after REGEN_DELAY without damage
    function regenerate(en) {
      const s = en.state;
      en.sinceHit += dt;
      const id = E.enemyStateId(s);
      if (en.sinceHit < REGEN_DELAY || id >= s.spawnId || s.special) return;
      en.regenT += dt;
      if (en.regenT < REGEN_INTERVAL) return;
      en.regenT -= REGEN_INTERVAL;
      const next = id + 1;
      s.shape = Math.floor(next / 10); s.color = next % 10; s.data = next; s.hp = E.layerHealth(next);
      sim.totals.regenerated = (sim.totals.regenerated || 0) + 1;
    }
    // Enemy.ApplySlowness: strongest active slow wins
    function applySlow(en, strength, duration) {
      strength = clamp(strength, 0, 0.9);
      if (strength > en.slow || en.slowT <= 0) { en.slow = strength; en.slowT = duration; }
      else if (Math.abs(strength - en.slow) < 1e-6) en.slowT = Math.max(en.slowT, duration);
    }

    // Starfighters: launch every timeBetweenLaunches, then cycle attack runs while enemies are in range.
    function hangarTick(tw, inRange, target) {
      const m = tw.model, h = m.hangar, st = m.stats, strat = m.strategy;
      while (tw.fighters.length < h.fighters && sim.t >= tw.launchAt) {
        tw.fighters.push({ phase: 'patrol', timer: 0, cooldown: 0, reload: 0, mag: st.AMMO, missiles: 0, missileT: 0, bombT: 0 });
        tw.launchAt = sim.t + (strat.timeBetweenLaunches || 1.25);
      }
      const I = E.fireInterval(st);
      for (const f of tw.fighters) {
        // Starfighter.UpdateCannonCooldown: at most one shot banked
        f.cooldown = Math.max(f.cooldown - dt, -I);
        if (f.reload > 0) { f.reload -= dt; if (f.reload <= 0) f.mag = st.AMMO; }
        f.timer += dt;
        if (f.phase === 'patrol') {
          if (target) { f.phase = 'run'; f.timer = 0; f.target = target; f.missiles = h.missilesPerRun; f.missileT = 0; f.bombT = 0; }
          continue;
        }
        if (!f.target || !f.target.alive) f.target = target;
        if (!f.target) { f.phase = 'patrol'; continue; }
        const strafing = f.timer >= (h.runTime - h.strafeTime) / 2 && f.timer < (h.runTime + h.strafeTime) / 2;
        // cannons: fire whenever an enemy is in the cone; modelled as the duty share of frames
        // the target is inside the fire cone for a `hangarDuty` share of every run
        const inCone = (f.timer / h.runTime) % 1 < s.hangarDuty;
        let volleys = 0;
        while (inCone && !(f.reload > 0 || f.cooldown > 0) && volleys < MAX_VOLLEYS_PER_FRAME) {
          volleys++;
          f.cooldown += I;
          const shots = h.twin ? ((strat.starfighter && strat.starfighter.cannonMuzzles) || 2) : 1;
          for (let k = 0; k < shots; k++) {
            sim.totals.shots++; tw.shots++;
            const v = enemySpeed(f.target);
            const pHit = E.hitChanceStraight(2.0, v, 20 * Math.PI / 180, m.projectile.speed, m.projectile.lifetime, m.projectile.rHit, m.projectile.spreadDeg, 5);
            if (rng() < pHit) { const tgt = f.target; schedule(sim.t + 2 / Math.max(1, m.projectile.speed), () => damage(tgt, st.DAMAGE, tw)); }
            else sim.totals.misses++;
          }
          f.mag--;
          if (f.mag <= 0) { if (st.RELOAD_SPEED > 0) f.reload = st.RELOAD_SPEED; else f.mag = st.AMMO; }
        }
        if (strafing) {
          f.missileT -= dt;
          if (f.missiles > 0 && f.missileT <= 0) {
            const near = inRange.length ? inRange.slice().sort((a, b) => v3.dist(a.p, f.target.p) - v3.dist(b.p, f.target.p)) : [f.target];
            const tgt = near[(h.missilesPerRun - f.missiles) % near.length];
            const travel = 3 / (strat.ordnanceSpeed || 7);
            schedule(sim.t + travel, () => explode(tw, tgt.p, st.RADIUS, h.ordnanceDamage));
            f.missiles--; f.missileT = (strat.starfighter && strat.starfighter.missileLaunchInterval) || 0.2;
          }
          if (h.carpet) {
            f.bombT -= dt;
            if (f.bombT <= 0) {
              const tgt = f.target;
              schedule(sim.t + 1.1 / (strat.ordnanceSpeed || 7), () => explode(tw, tgt.p, st.RADIUS, h.ordnanceDamage));
              f.bombT = (strat.starfighter && strat.starfighter.bombDropInterval) || 0.3;
            }
          }
        }
        if (f.timer >= h.runTime) { f.timer = 0; f.missiles = h.missilesPerRun; f.target = target; if (!target) f.phase = 'patrol'; }
      }
    }

    // MineFactoryActionStrategy: produce while the wave runs, place mines on the path inside the range,
    // set them off when an enemy on that lane crosses them, scrap mines that don't fit on a full field
    function minesTick(tw) {
      const m = tw.model, st = m.stats, M = m.mines;
      const salvage = M.scrapValue > 0 || M.scrapsPerLife > 0;
      const volleys = E.tickFireCycle(tw.fc, dt, tw.mines.length < M.cap || salvage, st.FIRERATE, 0, 0);
      for (let v = 0; v < volleys; v++)
        for (let k = 0; k < M.perCycle; k++) {
          if (tw.mines.length < M.cap) placeMine(tw); else scrapMine(tw);
        }
      // seeking mines drift onto enemies passing close by: modelled as a larger contact radius
      const contact = M.contact + 0.25 * M.seekRadius;
      for (let i = tw.mines.length - 1; i >= 0; i--) {
        const mine = tw.mines[i];
        if (mine.lane < 0 || sim.t < mine.armAt) continue;
        for (const en of sim.enemies) {
          if (!en.alive || en.lane !== mine.lane) continue;
          const step = enemySpeed(en) * dt;
          if (en.d + contact >= mine.d && en.d - step - contact <= mine.d) { detonateMine(tw, mine, i); break; }
        }
      }
    }
    function placeMine(tw) {
      const M = tw.model.mines;
      const spans = [];
      let total = 0, lo = Infinity, hi = -Infinity;
      (tw.intervals || []).forEach((list, lane) => (list || []).forEach(([a, b]) => {
        if (b > a) { spans.push({ lane, a, b }); total += b - a; lo = Math.min(lo, a); hi = Math.max(hi, b); }
      }));
      if (total <= 0) {
        // no path in range: the mine hovers above the factory and never meets an enemy
        tw.mines.push({ lane: -1, d: 0, armAt: Infinity, charges: M.charges });
        return;
      }
      const spacing = Math.max(0.5, 2 * tw.model.stats.RADIUS);
      const how = tw.model.tower.targetBehaviour;
      let best = null, bestScore = -Infinity;
      for (let c = 0; c < MINE.candidates; c++) {
        let pick = rng() * total, sp = spans[spans.length - 1];
        for (const x of spans) { if (pick <= x.b - x.a) { sp = x; break; } pick -= x.b - x.a; }
        const d = sp.a + clamp(pick, 0, sp.b - sp.a);
        let near = Infinity;
        for (const other of tw.mines) if (other.lane === sp.lane) near = Math.min(near, Math.abs(other.d - d));
        const progress = hi > lo ? (d - lo) / (hi - lo) : 0.5;
        const bias = how === 'FIRST' ? progress : how === 'LAST' ? 1 - progress : 0;
        const score = clamp(near / spacing, 0, 1) + 0.5 * bias;
        if (score > bestScore) { bestScore = score; best = { lane: sp.lane, d }; }
      }
      tw.mines.push({ lane: best.lane, d: best.d, armAt: sim.t + MINE.flightTime, charges: M.charges });
    }
    function detonateMine(tw, mine, index) {
      const st = tw.model.stats, M = tw.model.mines, lane = lanes[mine.lane];
      sim.totals.shots++; tw.shots++;
      mineBlast(tw, E.lanePos(lane, mine.d).p, st.RADIUS, st.DAMAGE, M);
      for (let b = 0; b < M.bomblets; b++) {
        const dist = M.clusterSpread * (0.4 + 0.6 * (Math.floor(b / 2) + 1) / Math.max(1, Math.ceil(M.bomblets / 2)));
        const p = E.lanePos(lane, clamp(mine.d + (b % 2 === 0 ? dist : -dist), 0, lane.total)).p;
        schedule(sim.t + 0.15 + 0.08 * b, () => mineBlast(tw, p, st.RADIUS * M.clusterRadiusShare, st.DAMAGE * M.clusterDamageShare, M));
      }
      mine.charges--;
      if (mine.charges > 0) mine.armAt = sim.t + MINE.rearmTime;
      else tw.mines.splice(index, 1);
    }
    function mineBlast(tw, center, radius, dmg, M) {
      for (const en of sim.enemies) {
        if (!en.alive || v3.dist(en.p, center) > radius + rE) continue;
        damage(en, dmg, tw, 'EXPLOSIVE');
        if (M.blastSlow > 0 && en.alive) applySlow(en, M.blastSlow, M.blastSlowDuration || 1.5);
      }
    }
    function scrapMine(tw) {
      const M = tw.model.mines;
      if (M.scrapValue > 0) addIncome(M.scrapValue, 'salvage');
      tw.scrapped++;
      if (M.scrapsPerLife > 0 && tw.scrapped % M.scrapsPerLife === 0 && tw.livesThisWave < M.maxLivesPerWave && sim.lives < sim.startLives) {
        sim.lives++; tw.livesThisWave++;
        sim.totals.livesRestored = (sim.totals.livesRestored || 0) + 1;
      }
    }
    // GameManager.AddIncome: the round's income multiplier, fractional money banked
    function addIncome(amount, kind) {
      sim.incomeRemainder = (sim.incomeRemainder || 0) + amount * E.incomeMultiplier(sim.round + 1, level);
      const whole = Math.floor(sim.incomeRemainder);
      sim.incomeRemainder -= whole;
      sim.money += whole;
      sim.totals.income += whole;
      if (sim.wave) sim.wave.income += whole;
      if (kind) sim.totals[kind] = (sim.totals[kind] || 0) + whole;
    }

    // ---- waves
    function startWave() {
      buyPhase();
      sim.schedule = spawnSchedule(sim.round);
      sim.spawnIdx = 0;
      sim.waveActive = true;
      sim.wave = { round: sim.round, name: ((config.waves ? config.waves[sim.round] : E.runtimeWave(level, sim.round)) || {}).name, startT: sim.t, startMoney: sim.money,
                   startLives: sim.lives, income: 0, kills: 0, leaks: 0, leakedLayers: 0, livesLost: 0, exceptions: 0,
                   towers: sim.towers.length, spentBefore: sim.totals.spent };
    }
    function endWave() {
      const w = sim.wave;
      // Mine factories: dividends for every mine still on the field, then the per-wave life limit resets
      for (const tw of sim.towers) {
        if (tw.model.kind !== 'mines') continue;
        if (tw.model.mines.waveEndPayout > 0 && tw.mines.length) addIncome(tw.model.mines.waveEndPayout * tw.mines.length, 'salvage');
        tw.livesThisWave = 0;
      }
      w.endT = sim.t; w.duration = sim.t - w.startT; w.endMoney = sim.money; w.endLives = sim.lives;
      sim.money += E.endOfWaveBonus(w.round + 1, level);
      sim.log.push(w);
      sim.waveActive = false;
      sim.round++;
      if (sim.lives <= 0) { sim.done = true; sim.gameOverRound = w.round; }
      if (sim.round >= rounds) sim.done = true;
    }

    sim.step = function (maxTicks) {
      for (let k = 0; k < maxTicks && !sim.done; k++) {
        if (!sim.waveActive) startWave();
        sim.t += dt;
        // spawns
        while (sim.spawnIdx < sim.schedule.length && sim.schedule[sim.spawnIdx].at <= sim.t) activate(sim.schedule[sim.spawnIdx++]);
        // movement + leaks
        for (const en of sim.enemies) {
          if (!en.alive) continue;
          en.d += enemySpeed(en) * dt;
          if (en.slowT > 0) { en.slowT -= dt; if (en.slowT <= 0) en.slow = 0; }
          if (en.state.traits & TRAIT.Regenerating) regenerate(en);
          if (en.d >= lanes[en.lane].total) {
            en.alive = false;
            const id = E.enemyStateId(en.state);
            sim.lives -= id + 1;
            const layersLeft = en.state.corrupt ? 0 : id + 1;
            sim.wave.leaks++; sim.wave.leakedLayers += layersLeft; sim.wave.livesLost += id + 1;
            (sim.wave.leakedIds = sim.wave.leakedIds || []).push(en.startId + '>' + id + (en.state.traits ? '[' + en.state.traits + ']' : ''));
            sim.totals.leakedLayers += layersLeft; sim.totals.livesLost += id + 1;
            sim.totals.leakedHP += en.state.corrupt ? 0 : Math.max(0, en.state.hp) + (id > 0 ? E.enemyTotalHP(id - 1) : 0);
            continue;
          }
          updateEnemyPos(en);
        }
        // towers
        for (const tw of sim.towers) towerTick(tw);
        // scheduled hits / explosions
        while (sim.events.length && sim.events[0].at <= sim.t) popEvent().fn();
        if (sim.enemies.length > 64 && sim.enemies.some(e => !e.alive)) sim.enemies = sim.enemies.filter(e => e.alive);
        const waveTime = sim.t - sim.wave.startT;
        if (sim.spawnIdx >= sim.schedule.length && !sim.enemies.some(e => e.alive) && !sim.events.length) {
          sim.enemies = [];
          endWave();
        } else if (waveTime > 900) {
          sim.enemies.forEach(e => { e.alive = false; });
          sim.enemies = [];
          sim.events = [];
          sim.wave.timedOut = true;
          endWave();
        }
        if (sim.lives <= 0 && sim.waveActive) {
          sim.wave.gameOver = true;
          endWave();
          sim.done = true;
        }
      }
      return sim.done;
    };

    sim.result = function () {
      return {
        level: level.name, builds: builds.map(b => b.model.label), rounds: sim.log.length, gameOverRound: sim.gameOverRound,
        survived: sim.gameOverRound == null, lives: sim.lives, money: sim.money, totals: sim.totals, waves: sim.log,
        towers: sim.towers.map(t => ({ label: t.model.label, key: t.model.tower.key, levels: t.levelsNow, anchor: t.anchorIndex, damage: t.damage, shots: t.shots, spent: t.spent })),
        purchases: sim.purchases, time: sim.t,
      };
    };
    return sim;
  };

  // ===================================================================================== meta agent

  /*
   * What the next `n` waves ask for, as weights the agent uses to value towers:
   * crowd (dense streams), armor/shield/regen (HP share with the trait), fast (HP share on pink-speed layers),
   * density (enemies per path unit while the stream passes).
   */
  E.waveFeatures = function (level, r, n, waves) {
    // per wave shares, then 0.6 x the peak + 0.4 x the mean: players prepare for the worst wave ahead
    const per = [];
    for (let k = r; k < r + (n || 5); k++) {
      const w = waves ? waves[k] : E.runtimeWave(level, k);
      if (!w) continue;
      let hp = 0, armor = 0, shield = 0, regen = 0, fast = 0, boss = 0, densNum = 0, densDen = 0, armorShape = 0;
      w.entries.forEach(e => {
        const H = E.enemyTotalHP(e.enemy) * e.count;
        hp += H;
        if (e.traits & TRAIT.Armored) { armor += H; armorShape += H * E.enemies[e.enemy].shapeIndex; }
        if (e.traits & TRAIT.Shielded) shield += H;
        if (e.traits & TRAIT.Regenerating) regen += H;
        if (E.enemies[e.enemy].special) boss += H;
        E.enemyLayers(e.enemy).forEach(l => { if (E.enemySpeed(l.id) >= 3.5) fast += E.layerHealth(l.id) * e.count; });
        if (e.count > 1 && e.delay > 0) { densNum += e.count / (E.enemySpeed(e.enemy) * e.delay); densDen += e.count; }
      });
      if (hp > 0) per.push({ hp, armor: armor / hp, shield: shield / hp, regen: regen / hp, fast: fast / hp, boss: boss / hp,
                            density: densDen ? densNum / densDen : 0.3, armorShape: armor > 0 ? armorShape / armor : 1 });
    }
    if (!per.length) return { hp: 0, armor: 0, shield: 0, regen: 0, fast: 0, boss: 0, density: 0.3, crowd: 0, armorShape: 1 };
    const mix = (k) => 0.6 * Math.max(...per.map(p => p[k])) + 0.4 * sum(per, p => p[k]) / per.length;
    const density = sum(per, p => p.density * p.hp) / sum(per, p => p.hp);
    const armored = per.filter(p => p.armor > 0);
    return { hp: sum(per, p => p.hp), armor: mix('armor'), shield: mix('shield'), regen: mix('regen'), fast: mix('fast'),
             boss: mix('boss'), density, crowd: clamp((density - 0.4) / 1.6, 0, 0.85),
             armorShape: armored.length ? Math.max(...armored.map(p => p.armorShape)) : 1 };
  };

  // Value of a tower model on a placement against upcoming waves (the agent's heuristic, not a game rule)
  E.agentValue = function (m, cov, feats) {
    if (!m || !cov) return 0;
    const st = m.dps.expected, crowd = Math.max(st, m.dps.crowd);
    let v = (1 - feats.crowd) * st + feats.crowd * crowd;
    const d = m.stats.DAMAGE > 0 ? m.stats.DAMAGE : 1;
    const armorF = E.armoredDamage(d, m.damageType, Math.round(feats.armorShape != null ? feats.armorShape : 1)) / d;
    const hits = 60 / d, shieldF = hits / (E.baseShieldHits(1) + hits);
    const fastF = m.hit && m.hit.mix > 0 ? clamp(m.hit.pink / m.hit.mix, 0, 1) : 1;
    v *= 1 - feats.armor * (1 - armorF) - feats.shield * (1 - shieldF) - feats.fast * (1 - fastF);
    // beams and dispensers only do their modelled damage on a placement as aligned as the best one
    const best = m.coverage || {};
    if (cov.beam && best.best > 0) v *= clamp(cov.length / best.best, 0, 1);
    if (cov.rays && m.dispenser && m.dispenser.rayLength > 0) v *= clamp(cov.rayLength / m.dispenser.rayLength, 0, 1);
    const utilization = clamp(cov.length * Math.max(0.05, feats.density), 0.15, 1);
    return Math.max(0, v) * utilization;
  };

  /*
   * Greedy player: before every wave it keeps buying the option with the best value gain per $ for the next
   * waves (new tower on the best free anchor, or the next tier of an existing tower), with a seeded epsilon
   * chance to pick among the top 4 instead. Used to find dominant strategies, not to play optimally.
   * config: { level, seed, epsilon, rounds, palette (tower keys), waves (optional override), lookahead }
   */
  // Average enemy spacing (path units) of a wave list, HP-weighted like E.levelMix
  E.wavesSpacing = function (waves) {
    let num = 0, den = 0;
    (waves || []).forEach(w => w && w.entries.forEach(e => {
      if (e.count > 1) { num += E.enemySpeed(e.enemy) * e.delay * e.count; den += e.count; }
    }));
    return den ? num / den : null;
  };

  /*
   * Agent calibration: the analytic values above miss placement, enemy presence and overlap.
   * Short probe games (4 base copies of one tower, endless lives) measure the damage each tower type
   * really deals per second; the agent scales its values by realized / expected.
   */
  E.agentCalibration = function (level, waves, rounds) {
    const key = ['agentcal', level.name, E.difficultyOf(level), E.settings.spacing, waves ? waves.length + '|' + JSON.stringify(waves[Math.min(waves.length - 1, 20)]) : 'level'].join('|');
    return cached(key, () => {
      const R = Math.min(rounds || 60, waves ? waves.length : E.winRound(level), E.winRound(level));
      const feats = E.waveFeatures(level, 0, R, waves);
      const out = {};
      (level.palette || []).forEach(k => {
        const t = E.towerByKey[k];
        if (!t) return;
        const lv = t.upgradePaths.map(() => 0);
        const m = E.model(t, lv);
        const cov = E.levelCoverage(m, level).best;
        const expected = E.agentValue(m, cov, feats);
        const r = E.runSimulation({ level, rounds: R, waves, builds: [{ tower: t, levels: lv }], maxCopies: 4, lives: 1e9, money: 1e6 });
        const n = Math.max(1, r.towers.length);
        const realized = sum(r.towers, x => x.damage) / Math.max(1, r.time) / n;
        out[k] = expected > 0 ? clamp(realized / expected, 0.2, 3) : 1;
      });
      return out;
    });
  };

  E.runAgent = function (config) {
    const level = typeof config.level === 'string' ? E.levelByName[config.level] : (config.level || E.level());
    // crowd values must use the spacing of the waves actually played
    const prevSpacing = E.settings.spacing;
    if (config.waves && (E.settings.spacing == null || E.settings.spacing === '')) {
      const sp = E.wavesSpacing(config.waves);
      if (sp) E.setSettings({ spacing: +sp.toFixed(2) });
    }
    try { return runAgentInner(config, level); } finally { if (E.settings.spacing !== prevSpacing) E.setSettings({ spacing: prevSpacing }); }
  };
  function runAgentInner(config, level) {
    const rng = mulberry32((config.seed || 1) * 7919 + 17);
    const eps = config.epsilon != null ? config.epsilon : 0.15;
    const palette = (config.palette || level.palette || []).map(k => E.towerByKey[k]).filter(Boolean);
    // A new tower also uses up an anchor. Once the good ones are gone, the next needs a building block,
    // so placements get dearer as the level fills up.
    const blockCost = E.price(((E.data.blocks || [])[0] || {}).cost || 50, level);
    const usefulAnchors = Math.max(1, (level.anchors || []).filter(a => !a.blocked).length);
    const cal = config.calibrate === false ? {} : E.agentCalibration(level, config.waves, config.calibrationRounds);
    const factor = (t) => cal[t.key] || 1;
    const placementCost = (sim) => blockCost * Math.min(2, sim.towers.length / (usefulAnchors * 0.6));
    // lane balance: placements on lanes that little damage reaches are worth more
    const nLanes = level.lanes.length;
    const laneLen = (cov, l) => cov && cov.intervals && cov.intervals[l] ? sum(cov.intervals[l], x => x[1] - x[0]) : 0;
    const laneWeight = (sim, cov) => {
      if (nLanes < 2 || !cov) return 1;
      const cover = level.lanes.map((_, l) => sum(sim.towers, tw => laneLen(tw.cov, l) * (tw.model.dps.expected || 0)));
      const mean = sum(cover) / nLanes;
      const tot = sum(level.lanes.map((_, l) => laneLen(cov, l)));
      if (tot <= 0) return 1;
      return sum(level.lanes.map((_, l) => laneLen(cov, l) / tot * clamp((mean + 1) / (cover[l] + 1), 0.5, 3)));
    };
    // players spread their money to be ready for different checks: more of an already dominant tower type is worth a bit less
    const diversity = config.diversity != null ? config.diversity : 1.5;
    const typeShare = (sim, key) => {
      let mine = 0, all = 0;
      sim.towers.forEach(tw => { all += tw.spent || 0; if (tw.model.tower.key === key) mine += tw.spent || 0; });
      return all > 0 ? mine / all : 0;
    };
    const policy = (api) => {
      const sim = api.sim;
      const feats = E.waveFeatures(level, sim.round, config.lookahead || 5, config.waves);
      const div = (t) => palette.length > 1 ? 1 / (1 + diversity * typeShare(sim, t.key)) : 1;
      for (let guard = 0; guard < 80; guard++) {
        const options = [];
        palette.forEach(t => {
          const mb = E.model(t, t.upgradePaths.map(() => 0));
          if (mb.cost > sim.money) return;
          api.freeAnchors(mb).forEach(cov => {
            const cost = mb.cost + (cov.virtual ? cov.blockCost : 0);
            if (cost > sim.money) return;
            options.push({ kind: 'build', t, cov, cost, rankCost: cost + (cov.virtual ? 0 : placementCost(sim)), gain: div(t) * factor(t) * laneWeight(sim, cov) * E.agentValue(mb, cov, feats) });
          });
        });
        if (!config.noUpgrades) sim.towers.forEach(tw => {
          const t = tw.model.tower;
          t.upgradePaths.forEach((path, p) => {
            const tier = tw.levelsNow[p];
            if (tier >= path.length) return;
            const next = tw.levelsNow.slice(); next[p]++;
            if (next.filter(x => x >= 1).length > 2 || next.filter(x => x >= 3).length > 1) return;
            const price = E.price(path[tier].price, level);
            if (price > sim.money) return;
            const mn = E.model(t, next);
            options.push({ kind: 'upgrade', tw, p, cost: price, gain: div(t) * factor(t) * laneWeight(sim, tw.cov) * (E.agentValue(mn, tw.cov, feats) - E.agentValue(tw.model, tw.cov, feats)) });
          });
        });
        const useful = options.filter(o => o.gain > 0);
        if (!useful.length) break;
        useful.sort((a, b) => b.gain / (b.rankCost || b.cost) - a.gain / (a.rankCost || a.cost));
        const pick = rng() < eps ? useful[Math.floor(rng() * Math.min(4, useful.length))] : useful[0];
        const ok = pick.kind === 'build' ? api.build(pick.t, pick.cov) : api.upgrade(pick.tw, pick.p);
        if (!ok) break;
      }
    };
    const r = E.runSimulation(Object.assign({}, config, { level, buyPolicy: policy, builds: [] }));
    // spend by tower type and the tier-3 modules bought
    const spend = {}, t3 = {};
    r.purchases.forEach(p => { spend[p.tower] = (spend[p.tower] || 0) + p.cost; if (p.tier === 2) t3[p.tower + ' P' + (p.path + 1)] = (t3[p.tower + ' P' + (p.path + 1)] || 0) + 1; });
    r.spendByTower = spend;
    r.tier3 = t3;
    return r;
  }

  E.runSimulation = function (config) {
    const sim = E.createSimulation(config);
    let guard = 0;
    while (!sim.step(5000) && guard++ < 100000) { /* run */ }
    return sim.result();
  };

  // ===================================================================================== insights

  E.insights = function () {
    const out = [];
    const models = E.models();
    const level = E.level();
    const bases = E.towers.filter(t => t.inPalette).map(t => E.model(t, t.upgradePaths.map(() => 0)));
    const byVal = bases.slice().sort((a, b) => b.dps.perDollar - a.dps.perDollar);
    if (byVal.length) {
      out.push({ sev: 'info', area: 'Towers', text: `Best base tower for single-target damage per $ on ${level.name}: ${byVal[0].tower.displayName} (${fmt(byVal[0].dps.perDollar, 2)} DPS per $100). Worst: ${byVal[byVal.length - 1].tower.displayName} (${fmt(byVal[byVal.length - 1].dps.perDollar, 2)}).` });
    }
    const clamped = models.filter(m => m.cycle.clamped);
    if (clamped.length) out.push({ sev: 'warn', area: 'Upgrades', text: `${clamped.length} builds stack FIRERATE modifiers below the ${MIN_FIRE_INTERVAL} s clamp, so part of what they paid for does nothing: ${uniq(clamped.map(m => m.label)).slice(0, 6).join(', ')}${clamped.length > 6 ? ', …' : ''}.` });
    const reachShort = bases.filter(m => m.projectile && m.projectile.reach + m.projectile.rHit < m.range.detect - 0.05);
    if (reachShort.length) out.push({ sev: 'warn', area: 'Towers', text: `Projectiles can't reach the edge of the detection range for: ${reachShort.map(m => m.tower.displayName).join(', ')}.` });
    const mods = E.moduleRows();
    const bad = mods.filter(r => r.dRaw <= 1e-9 && r.dCrowd <= 1e-9 && r.dRange <= 0 && r.dPass <= 1e-9).map(r => `${r.tower.displayName} / ${r.module.name}`);
    if (bad.length) out.push({ sev: 'warn', area: 'Upgrades', text: `${bad.length} upgrade${bad.length === 1 ? '' : 's'} add${bad.length === 1 ? 's' : ''} no modelled damage, crowd damage, range or per-pass damage on their own: ${bad.slice(0, 6).join('; ')}${bad.length > 6 ? '; …' : ''}.` });
    const worse = mods.filter(r => r.dRaw < -1e-9).map(r => `${r.tower.displayName} / ${r.module.name} (${pct(r.after.dps.raw / r.before.dps.raw - 1, true)})`);
    if (worse.length) out.push({ sev: 'warn', area: 'Upgrades', text: `Upgrades that lower raw single-target DPS: ${worse.join('; ')}.` });
    const adv = mods.filter(r => r.advertised.some(a => /fire/i.test(a.what) && a.percent && a.value > 0) && isFinite(r.dRate))
      .map(r => ({ r, claim: Math.max(...r.advertised.filter(a => /fire/i.test(a.what) && a.percent).map(a => a.value)) / 100 }))
      .filter(x => x.r.dRate < x.claim * 0.5);
    if (adv.length) out.push({ sev: 'warn', area: 'Upgrades', text: `Advertised fire-rate boosts that deliver less than half of the claim: ${adv.map(x => `${x.r.tower.displayName} / ${x.r.module.name} (claims +${Math.round(x.claim * 100)}%, gets ${pct(x.r.dRate, true)})`).join('; ')}.` });
    const best = mods.filter(r => isFinite(r.dpsPer100)).sort((a, b) => b.dpsPer100 - a.dpsPer100)[0];
    if (best) out.push({ sev: 'info', area: 'Upgrades', text: `Highest single-target DPS gain per $100: ${best.tower.displayName} / ${best.module.name} (+${fmt(best.dpsPer100, 2)}).` });
    // overflow bug
    if (E.settings.overflow === 'coded') {
      const over = models.filter(m => m.overkill && m.overkill.exceptionShare > 0);
      if (over.length) out.push({ sev: 'bad', area: 'Enemies', text: `Pre-fix overflow model: the old Enemy.HandleDeathOfSingleShape would throw for ${over.length} builds against ${level.name}'s enemies.` });
    }
    // economy
    const rows = E.levelWaveTable(level).filter(r => !r.missing);
    if (rows.length) {
      const hardest = rows.slice(0, level.spawner.waves.length).reduce((a, b) => (b.dpsPerDollar > a.dpsPerDollar ? b : a));
      out.push({ sev: 'info', area: 'Waves', text: `On ${level.name}, the steepest DPS demand relative to available money is round ${hardest.round + 1} (${hardest.name}): ${fmt(hardest.streamDPS, 1)} stream DPS with ${fmt(hardest.moneyAtStart, 0)} money banked (no leaks assumed).` });
    }
    return out;
  };

  function uniq(a) { return Array.from(new Set(a)); }

  // ===================================================================================== formatting

  function fmt(v, digits) {
    if (v === Infinity) return '∞';
    if (v === -Infinity) return '-∞';
    if (v == null || Number.isNaN(v)) return '–';
    digits = digits == null ? 2 : digits;
    const a = Math.abs(v);
    if (a >= 10000) return Math.round(v).toLocaleString('en-US');
    if (a >= 100) return v.toFixed(Math.min(digits, 0));
    if (a >= 10) return v.toFixed(Math.min(digits, 1));
    return v.toFixed(digits);
  }
  function pct(v, signed) {
    if (v == null || Number.isNaN(v)) return '–';
    if (!isFinite(v)) return v > 0 ? '+∞%' : '-∞%';
    const s = (v * 100).toFixed(Math.abs(v) < 0.1 ? 1 : 0) + '%';
    return signed && v > 0 ? '+' + s : s;
  }
  E.fmt = fmt;
  E.pct = pct;

  root.TDEngine = E;
})(typeof window !== 'undefined' ? window : this);
