#!/usr/bin/env python3
"""Extracts 3DTD's balance data (towers, upgrades, enemies, waves, levels) from the Unity project.

Usage:
    python3 extract.py [--project PATH_TO_UNITY_PROJECT] [--out DIR]

Writes balance-data.json (for tools and AI agents) and balance-data.js (the same data as a script,
so index.html also works when opened straight from disk). Only the Python standard library is needed,
and Unity does not have to be installed or running.
"""

import argparse
import datetime
import json
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_yaml import Graph, Project, decode_int_array, num, parse_asset, parse_documents  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_PROJECT = os.path.normpath(os.path.join(HERE, '..', '..', '3DTD'))

STAT_TYPES = ['WORTH', 'MAXIMUM_HEALTH', 'HEALTH_REGEN', 'MAXIMUM_ARMOR', 'ARMOR_REGEN', 'DAMAGE', 'AMOUNT',
              'AMMO', 'FIRERATE', 'RELOAD_SPEED', 'RANGE', 'RADIUS', 'ACCURACY', 'PIERCING', 'LIFETIME', 'SPEED', 'SIZE']
STAT_FIELDS = {'WORTH': 'Worth', 'MAXIMUM_HEALTH': 'MaximumHealth', 'HEALTH_REGEN': 'HealthRegen',
               'MAXIMUM_ARMOR': 'MaximumArmor', 'ARMOR_REGEN': 'ArmorRegen', 'DAMAGE': 'Damage', 'AMOUNT': 'Amount',
               'AMMO': 'Ammo', 'FIRERATE': 'FireRate', 'RELOAD_SPEED': 'ReloadSpeed', 'RANGE': 'Range',
               'RADIUS': 'Radius', 'ACCURACY': 'Accuracy', 'PIERCING': 'Piercing', 'LIFETIME': 'Lifetime',
               'SPEED': 'Speed', 'SIZE': 'Size'}
SHAPES = ['TETRAHEDRON', 'CUBE', 'OCTAHEDRON', 'DODECAHEDRON', 'ICOSAHEDRON', 'BOSS']
COLORS = ['RED', 'ORANGE', 'YELLOW', 'GREEN', 'CYAN', 'BLUE', 'PURPLE', 'PINK', 'WHITE', 'BLACK']
DAMAGE_TYPES = ['PROJECTILE', 'EXPLOSIVE', 'MAGIC', 'ALL']
TARGET_BEHAVIOURS = ['FIRST', 'LAST', 'STRONGEST', 'NEAREST', 'FARTHEST']
DIFFICULTIES = ['Easy', 'Medium', 'Hard', 'Impossible']

# Which stats each action strategy (and the projectile it spawns) actually reads, taken from the C# code.
# RANGE is always read by Tower.Update to scale the targetter.
STATS_READ = {
    'LaserTowerActionStrategy': ['FIRERATE', 'AMMO', 'RELOAD_SPEED', 'SIZE', 'LIFETIME', 'DAMAGE', 'PIERCING', 'SPEED', 'ACCURACY', 'RANGE'],
    'DroneTowerActionStrategy': ['FIRERATE', 'AMMO', 'RELOAD_SPEED', 'SIZE', 'LIFETIME', 'DAMAGE', 'PIERCING', 'SPEED', 'ACCURACY', 'RANGE'],
    'BombTowerActionStrategy': ['FIRERATE', 'AMMO', 'RELOAD_SPEED', 'SIZE', 'LIFETIME', 'DAMAGE', 'PIERCING', 'SPEED', 'ACCURACY', 'RADIUS', 'RANGE'],
    'BulletDispenserTowerActionStrategy': ['FIRERATE', 'DAMAGE', 'LIFETIME', 'PIERCING', 'SPEED', 'ACCURACY', 'SIZE', 'RANGE'],
    'SniperTowerActionStrategy': ['FIRERATE', 'AMMO', 'RELOAD_SPEED', 'DAMAGE', 'RANGE'],
    'BeamTowerActionStrategy': ['FIRERATE', 'PIERCING', 'DAMAGE', 'RANGE'],
    'HangarTowerActionStrategy': ['AMOUNT', 'RANGE', 'DAMAGE', 'FIRERATE', 'AMMO', 'RELOAD_SPEED', 'RADIUS', 'ACCURACY', 'PIERCING', 'LIFETIME', 'SPEED', 'SIZE'],
    'MineFactoryActionStrategy': ['FIRERATE', 'AMOUNT', 'AMMO', 'DAMAGE', 'RADIUS', 'PIERCING', 'RANGE', 'SPEED', 'SIZE'],
}

warnings = []


def warn(severity, area, message, where=None):
    entry = {'severity': severity, 'area': area, 'message': message, 'where': where}
    if entry not in warnings:
        warnings.append(entry)


def b(v):
    return bool(int(num(v, 0)))


def r3(v):
    return [round(x, 4) for x in v]


def clean(data):
    return {k: v for k, v in data.items() if not k.startswith('m_')}


# --------------------------------------------------------------------------- linear algebra helpers

def mat_inv_rigid_scaled(m):
    """Inverse of an affine 4x4 matrix (general 3x3 part)."""
    a = [[m[r][c] for c in range(3)] for r in range(3)]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
           - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
           + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    if abs(det) < 1e-12:
        return None
    inv = [[0] * 3 for _ in range(3)]
    inv[0][0] = (a[1][1] * a[2][2] - a[1][2] * a[2][1]) / det
    inv[0][1] = (a[0][2] * a[2][1] - a[0][1] * a[2][2]) / det
    inv[0][2] = (a[0][1] * a[1][2] - a[0][2] * a[1][1]) / det
    inv[1][0] = (a[1][2] * a[2][0] - a[1][0] * a[2][2]) / det
    inv[1][1] = (a[0][0] * a[2][2] - a[0][2] * a[2][0]) / det
    inv[1][2] = (a[0][2] * a[1][0] - a[0][0] * a[1][2]) / det
    inv[2][0] = (a[1][0] * a[2][1] - a[1][1] * a[2][0]) / det
    inv[2][1] = (a[0][1] * a[2][0] - a[0][0] * a[2][1]) / det
    inv[2][2] = (a[0][0] * a[1][1] - a[0][1] * a[1][0]) / det
    t = [m[0][3], m[1][3], m[2][3]]
    it = [-sum(inv[r][k] * t[k] for k in range(3)) for r in range(3)]
    return [inv[0] + [it[0]], inv[1] + [it[1]], inv[2] + [it[2]], [0, 0, 0, 1]]


def mat_mul(a, b_):
    return [[sum(a[r][k] * b_[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def apply_point(m, p):
    return [sum(m[r][k] * p[k] for k in range(3)) + m[r][3] for r in range(3)]


def apply_dir(m, d):
    return [sum(m[r][k] * d[k] for k in range(3)) for r in range(3)]


def norm(v):
    n = math.sqrt(sum(x * x for x in v)) or 1.0
    return [x / n for x in v]


def dist(a, c):
    return math.sqrt(sum((a[i] - c[i]) ** 2 for i in range(3)))


# --------------------------------------------------------------------------- enemies & waves

META_EFFECT_TYPES = ['TowerStatPercent', 'StartMoney', 'IncomePercent', 'WaveBonus', 'StartLives', 'PricePercent', 'RefundPercent']


def extract_meta_upgrades(pr):
    """Resources/Progress/MetaUpgradeTree.asset: the meta-upgrade trees (veteran mode applies every node)."""
    path = os.path.join(pr.assets, 'Resources', 'Progress', 'MetaUpgradeTree.asset')
    if not os.path.exists(path):
        warn('warning', 'Meta', 'No MetaUpgradeTree asset; veteran mode has no effect.', pr.rel(path))
        return {'asset': None, 'trees': []}
    trees = []
    for tree in parse_asset(path).get('trees') or []:
        nodes = []
        for node in tree.get('nodes') or []:
            effects = []
            for eff in node.get('effects') or []:
                t, st = int(num(eff.get('type'))), int(num(eff.get('stat')))
                if t >= len(META_EFFECT_TYPES):
                    warn('warning', 'Meta', 'Meta node %s has unknown effect type %d.' % (node.get('id'), t), pr.rel(path))
                    continue
                effects.append({'type': META_EFFECT_TYPES[t], 'stat': STAT_TYPES[st] if st < len(STAT_TYPES) else str(st),
                                'value': num(eff.get('value')), 'scope': eff.get('scope') or ''})
            nodes.append({'id': node.get('id'), 'displayName': node.get('displayName'), 'tier': int(num(node.get('tier'))),
                          'cost': num(node.get('cost')), 'capstone': b(node.get('capstone')),
                          'requires': [r for r in node.get('requires') or [] if r], 'effects': effects})
        trees.append({'id': tree.get('id'), 'displayName': tree.get('displayName'), 'nodes': nodes})
    return {'asset': pr.rel(path), 'trees': trees}


def extract_enemies(pr):
    enemy_prefab = os.path.join(pr.assets, 'Prefabs', 'Enemies', 'Default Enemy.prefab')
    g = Graph(pr, pr.resolve(enemy_prefab))
    enemy_script = g.scripts('Enemy')[0][2]
    by_guid = {}
    enemies = []
    for idx, ref in enumerate(enemy_script.get('allPossibleEnemyData') or []):
        guid = (ref or {}).get('guid')
        data = pr.asset(guid) if guid else None
        if not data:
            # keep a placeholder so the list stays indexed by Enemy.Id
            msg = 'Default Enemy.allPossibleEnemyData[%d] is empty or missing; using a 1 HP placeholder.' % idx
            warn('error', 'Enemies', msg, pr.rel(enemy_prefab))
            print('! ' + msg)
            enemies.append({'id': idx, 'asset': None, 'guid': guid, 'assetId': idx, 'missing': True,
                            'shape': SHAPES[idx // 10] if idx // 10 < len(SHAPES) else str(idx // 10), 'shapeIndex': idx // 10,
                            'color': COLORS[idx % 10] if idx % 10 < len(COLORS) else str(idx % 10), 'colorIndex': idx % 10,
                            'health': 1.0, 'speed': 1.0, 'damageType': DAMAGE_TYPES[0], 'special': idx // 10 == 5})
            continue
        shape, color = int(num(data.get('_startShape'))), int(num(data.get('_startColor')))
        e = {
            'id': idx,
            'asset': pr.rel(pr.path(guid)),
            'guid': guid,
            'assetId': int(num(data.get('_id'))),
            'shape': SHAPES[shape] if shape < len(SHAPES) else str(shape),
            'shapeIndex': shape,
            'color': COLORS[color] if color < len(COLORS) else str(color),
            'colorIndex': color,
            'health': num(data.get('_health')),
            'speed': num(data.get('_movementSpeed')),
            'damageType': DAMAGE_TYPES[int(num(data.get('_damageType')))],
            'special': shape == 5,
        }
        if shape * 10 + color != idx:
            warn('error', 'Enemies', 'allPossibleEnemyData[%d] is %s %s (Id %d); Enemy.Id-based lookups will use the wrong data.'
                 % (idx, e['shape'], e['color'], shape * 10 + color), e['asset'])
        enemies.append(e)
        by_guid[guid] = idx
    shapes_count = len(enemy_script.get('allPossibleShapes') or [])
    if shapes_count != len(enemies):
        warn('warning', 'Enemies', 'Default Enemy has %d shape meshes but %d enemy data entries.' % (shapes_count, len(enemies)))

    # Sanity checks on progression
    for e in enemies:
        if e['special']:
            continue
        nxt = next((x for x in enemies if x['id'] == e['id'] + 1 and not x['special']), None)
        if nxt and nxt['shapeIndex'] == e['shapeIndex'] and nxt['speed'] < e['speed']:
            warn('info', 'Enemies', '%s %s (speed %.2f) is faster than the next stronger layer %s %s (speed %.2f).'
                 % (e['shape'], e['color'], e['speed'], nxt['shape'], nxt['color'], nxt['speed']))

    enemy_collider = None
    for fid, doc in g.o.items():
        if doc['type'] == 'SphereCollider':
            go = g.go_of(fid)
            enemy_collider = num(doc['data'].get('m_Radius'), 0.5) * max(g.world_scale(g.transform_of[go]))
    return enemies, by_guid, {'colliderRadius': enemy_collider or 0.375,
                              'rotationSpeed': num(enemy_script.get('speedRandomRotation'), 2)}


def extract_wave(pr, guid, enemy_by_guid):
    path = pr.path(guid)
    data = parse_asset(path)
    refs = data.get('enemiesToSpawn') or []
    counts = decode_int_array(data.get('enemySpawnCount'))
    delays = [num(x) for x in (data.get('spawnDelay') or [])]
    n = min(len(refs), len(counts), len(delays))
    if not (len(refs) == len(counts) == len(delays)):
        warn('warning', 'Waves', '%s has mismatched list lengths (enemies %d, counts %d, delays %d); only the first %d entries spawn.'
             % (os.path.basename(path), len(refs), len(counts), len(delays), n), pr.rel(path))
    traits = decode_int_array(data.get('enemyTraits'))
    entries = []
    for i in range(n):
        g = (refs[i] or {}).get('guid')
        if not g:
            continue
        eid = enemy_by_guid.get(g)
        if eid is None:
            warn('warning', 'Waves', '%s entry %d references an EnemyData that is not in Default Enemy.allPossibleEnemyData.' % (os.path.basename(path), i), pr.rel(path))
            continue
        if counts[i] > 0:
            entries.append({'enemy': eid, 'count': counts[i], 'delay': delays[i], 'traits': traits[i] if i < len(traits) else 0})
    return {'guid': guid, 'name': os.path.splitext(os.path.basename(path))[0], 'path': pr.rel(path), 'entries': entries}


# --------------------------------------------------------------------------- towers

def projectile_info(pr, guid):
    if not guid or not pr.path(guid):
        return None
    g = Graph(pr, pr.resolve(guid))
    info = {'prefab': pr.rel(pr.path(guid)), 'script': None, 'colliderRadius': None, 'defaults': {}}
    roots = [fid for fid, d in g.o.items() if d['type'] in ('Transform', 'RectTransform') and int((d['data'].get('m_Father') or {}).get('fileID', 0)) == 0]
    root_go = g.go_of(roots[0]) if roots else None
    for fid, name, data in g.scripts():
        if name and (name.startswith('Projectile') or name == 'Clusterbomb'):
            info['script'] = name
            info['defaults'] = {k: num(v) for k, v in clean(data).items() if not isinstance(v, (dict, list)) and re.fullmatch(r'-?[\d.]+', str(v))}
            if name == 'ProjectileBomb':
                cluster = (data.get('clusterProjectilePrefab') or {}).get('guid')
                if cluster:
                    cg = Graph(pr, pr.resolve(cluster))
                    cb = cg.scripts('Clusterbomb')
                    info['cluster'] = {
                        'prefab': pr.rel(pr.path(cluster)),
                        'count': len([x for x in data.get('clusterProjectileFirePoints') or [] if int((x or {}).get('fileID', 0)) != 0]),
                        'damageShare': num(data.get('clusterDamageShare'), 0.5),
                        'radiusShare': num(data.get('clusterRadiusShare'), 0.6),
                        **({k: num(v) for k, v in clean(cb[0][2]).items() if k in ('lifetime', 'speed', 'damage', 'radius')} if cb else {}),
                    }
    for fid, doc in g.o.items():
        if doc['type'] in ('CapsuleCollider', 'SphereCollider') and g.go_of(fid) == root_go:
            info['colliderRadius'] = num(doc['data'].get('m_Radius'), 0.5)
    return info


def strategy_info(pr, g, comp_id):
    doc = g.doc(comp_id)
    if not doc:
        return None
    name = g.script_of(comp_id)
    data = clean(doc['data'])
    out = {'type': name, 'fileID': comp_id}
    for k, v in data.items():
        if isinstance(v, dict) and v.get('guid'):
            if k in ('projectile', 'cannonProjectile', 'ordnanceProjectile'):
                out[k] = projectile_info(pr, v['guid'])
            elif k == 'starfighterPrefab':
                sg = Graph(pr, pr.resolve(v['guid']))
                sf = sg.scripts('Starfighter')
                out['starfighter'] = {kk: num(vv) for kk, vv in clean(sf[0][2]).items() if not isinstance(vv, (dict, list))} if sf else {}
                if sf:
                    out['starfighter']['cannonMuzzles'] = len(sf[0][2].get('cannonMuzzles') or [])
            continue
        if isinstance(v, (dict, list)):
            continue
        try:
            out[k] = num(v) if re.fullmatch(r'-?[\d.eE+-]+', str(v)) else v
        except TypeError:
            out[k] = v
    return out


def relative_matrix(g, root_tr, tr):
    root_inv = mat_inv_rigid_scaled(g.world_matrix(root_tr))
    return mat_mul(root_inv, g.world_matrix(tr))


def extract_tower(pr, path, palette_guids):
    guid = pr.path_to_guid.get(path)
    g = Graph(pr, pr.resolve(path))
    rel = pr.rel(path)
    towers = g.scripts('Tower')
    blocks = g.scripts('BuildingBlock')
    if not towers:
        building = g.scripts('Building')
        if blocks and building:
            bd = building[0][2]
            return {'kind': 'block', 'key': os.path.splitext(os.path.basename(path))[0], 'prefab': rel, 'guid': guid,
                    'displayName': bd.get('displayName'), 'description': bd.get('description'), 'cost': num(bd.get('cost')),
                    'anchors': len(g.scripts('AnchorPoint'))}
        return None
    tid, _, td = towers[0]
    root_go = g.go_of(tid)
    root_tr = g.transform_of[root_go]
    key = os.path.splitext(os.path.basename(path))[0]

    stats_cfg = None
    for _, _, sd in g.scripts('StatsManager'):
        sg = (sd.get('statsScriptableObject') or {}).get('guid')
        if sg:
            raw = pr.asset(sg) or {}
            stats_cfg = {'asset': pr.rel(pr.path(sg)), 'cost': num(raw.get('Cost')),
                         'stats': {s: num(raw.get(STAT_FIELDS[s])) for s in STAT_TYPES},
                         'missingFields': [s for s in STAT_TYPES if STAT_FIELDS[s] not in raw]}
    if not stats_cfg:
        warn('error', 'Towers', '%s has no StatsConfig.' % key, rel)
        return None

    # Shooting points: enabled = activeSelf of the referenced GameObject
    shooting_points = []
    for sp in td.get('shootingPoints') or []:
        sid = int((sp or {}).get('fileID', 0))
        sdoc = g.doc(sid)
        if not sdoc:
            warn('warning', 'Towers', '%s has a missing shooting point reference.' % key, rel)
            continue
        if sdoc['type'] != 'MonoBehaviour':
            warn('error', 'Towers', '%s.shootingPoints references a %s instead of a ShootingPointReference (stale prefab, the tower would throw at runtime).'
                 % (key, sdoc['type']), rel)
            shooting_points.append({'name': g.go_name(sid), 'ref': sid, 'active': g.go_active(sid), 'broken': True})
            continue
        ref = int((sdoc['data'].get('shootingPointReference') or {}).get('fileID', 0))
        sp_go = g.go_of(sid)
        m = relative_matrix(g, root_tr, g.transform_of[sp_go])
        shooting_points.append({'name': g.go_name(sp_go), 'ref': ref, 'refName': g.go_name(ref), 'active': g.go_active(ref),
                                'pos': r3([m[0][3], m[1][3], m[2][3]]), 'fwd': r3(norm([m[0][2], m[1][2], m[2][2]]))})

    # Targetter geometry, relative to the tower root (whose rotation is replaced by the anchor rotation on build)
    targetter = None
    tg = int((td.get('targetter') or {}).get('fileID', 0))
    if g.doc(tg):
        tgo = g.go_of(tg)
        ttr = g.transform_of[tgo]
        m = relative_matrix(g, root_tr, ttr)
        parent = int(g.doc(ttr)['data']['m_Father']['fileID'])
        pm = relative_matrix(g, root_tr, parent) if parent else [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
        parent_scale = max(math.sqrt(sum(pm[r][c] ** 2 for r in range(3))) for c in range(3))
        shape, radius = 'sphere', 0.5
        for cid in g.components_on(tgo):
            cd = g.doc(cid)
            if cd['type'] == 'SphereCollider':
                shape, radius = 'sphere', num(cd['data'].get('m_Radius'), 0.5)
            elif cd['type'] == 'MeshCollider':
                mesh = pr.path((cd['data'].get('m_Mesh') or {}).get('guid'))
                if mesh and 'HalfSphere' in mesh:
                    shape, radius = 'hemisphere', 1.0
                else:
                    shape, radius = 'mesh', 1.0
        targetter = {'shape': shape, 'radiusPerRange': radius * parent_scale,
                     'center': r3([m[0][3], m[1][3], m[2][3]]), 'axis': r3(norm([m[0][1], m[1][1], m[2][1]]))}

    strategy_id = int((td.get('actionStrategy') or {}).get('fileID', 0))
    strategy = strategy_info(pr, g, strategy_id)
    if not strategy:
        warn('error', 'Towers', '%s has no action strategy.' % key, rel)
        return None
    if strategy['type'] not in STATS_READ:
        warn('warning', 'Towers', '%s uses %s, which the dashboard has no model for.' % (key, strategy['type']), rel)
    # Mine Factory: mines fly from the launch point, so their flight time depends on where it sits
    launch = int((g.doc(strategy_id)['data'].get('launchPoint') or {}).get('fileID', 0))
    if launch and g.doc(launch):
        m = relative_matrix(g, root_tr, launch)
        strategy['launchPoint'] = r3([m[0][3], m[1][3], m[2][3]])

    sp_by_ref = {sp['ref']: i for i, sp in enumerate(shooting_points)}

    def toggles(ids):
        return [{'go': i, 'name': g.go_name(i), 'shootingPoint': sp_by_ref.get(i)} for i in ids]

    paths = []
    for _, _, ud in g.scripts('UpgradeManager'):
        for pi, upath in enumerate(ud.get('upgradePaths') or []):
            modules = []
            for ti, mod in enumerate((upath or {}).get('upgradeModules') or []):
                if not mod:
                    continue
                stat_ups = []
                for su in mod.get('statUpgrades') or []:
                    st = int(num(su.get('targetStat')))
                    stat_ups.append({'stat': STAT_TYPES[st], 'value': num(su.get('upgradeValue')), 'isModifier': b(su.get('isModifier'))})
                behaviours = []
                activates = [int(x['fileID']) for x in mod.get('upgradeVisualGameObjects') or [] if x and int(x.get('fileID', 0))]
                deactivates = [int(x['fileID']) for x in mod.get('disableVisualGameObjects') or [] if x and int(x.get('fileID', 0))]
                for u in mod.get('upgrades') or []:
                    uid = int((u or {}).get('fileID', 0))
                    udoc = g.doc(uid)
                    if not udoc:
                        warn('error', 'Upgrades', '%s / %s references a missing Upgrade component.' % (key, mod.get('name')), rel)
                        continue
                    uname = g.script_of(uid)
                    if uname in ('VisualUpgrade', 'BeamVisualUpgrade', 'SniperVisualUpgrade'):
                        continue    # projectile and effect looks (ProjectileVisualsBuilder), no gameplay
                    udata = clean(udoc['data'])
                    beh = {'type': uname, 'comment': udata.get('Comment')}
                    if uname == 'ChangeActionStrategyUpgrade':
                        ns = int((udata.get('newActionStrategy') or {}).get('fileID', 0))
                        beh['strategy'] = strategy_info(pr, g, ns)
                        if g.go_of(ns) != root_go:
                            warn('warning', 'Upgrades', '%s / %s swaps to a strategy that is not on the tower root.' % (key, mod.get('name')), rel)
                    elif uname in ('ChangeFirepointsUpgrade', 'TripleShotUpgrade'):
                        act = [int(x['fileID']) for x in udata.get('ToActivate') or [] if x]
                        de = [int(x['fileID']) for x in udata.get('ToDeactivate') or [] if x]
                        activates += act
                        deactivates += de
                        beh['activates'] = len(act)
                        beh['deactivates'] = len(de)
                    elif uname == 'BombTowerLightAimUpgrade':
                        beh['aimAtTarget'] = True
                    elif uname == 'BeamSlowUpgrade':
                        beh['slowOnHit'] = num(udata.get('slowOnHit'), 0.2)
                        beh['slowDuration'] = num(udata.get('slowDuration'), 0.5)
                    elif uname == 'StarfighterEngineUpgrade':
                        beh['flightSpeedMultiplier'] = num(udata.get('flightSpeedMultiplier'), 1)
                        beh['turnRateMultiplier'] = num(udata.get('turnRateMultiplier'), 1)
                    elif uname == 'StarfighterLoadoutUpgrade':
                        beh['twinLinkedCannons'] = b(udata.get('enableTwinLinkedCannons'))
                        beh['additionalMissilesPerRun'] = int(num(udata.get('additionalMissilesPerRun')))
                        beh['carpetBombing'] = b(udata.get('enableCarpetBombing'))
                    elif uname == 'MineWarheadUpgrade':
                        beh['additionalClusterBomblets'] = int(num(udata.get('additionalClusterBomblets')))
                        beh['blastSlow'] = num(udata.get('blastSlow'))
                        beh['blastSlowDuration'] = num(udata.get('blastSlowDuration'), 1.5)
                        beh['seekRadius'] = num(udata.get('seekRadius'))
                        beh['heavyExplosions'] = b(udata.get('heavyExplosions'))
                    elif uname == 'MineSalvageUpgrade':
                        beh['additionalScrapValue'] = num(udata.get('additionalScrapValue'))
                        beh['scrapsPerLife'] = int(num(udata.get('scrapsPerLife')))
                        beh['additionalMaxLivesPerWave'] = int(num(udata.get('additionalMaxLivesPerWave')))
                        beh['additionalWaveEndPayoutPerMine'] = num(udata.get('additionalWaveEndPayoutPerMine'))
                    else:
                        warn('info', 'Upgrades', '%s / %s uses %s, which the dashboard treats as cosmetic.' % (key, mod.get('name'), uname), rel)
                    behaviours.append(beh)
                modules.append({
                    'path': pi, 'tier': ti, 'name': mod.get('name'),
                    'description': re.sub(r'\s*<br>\s*', '\n', str(mod.get('description') or '')),
                    'price': num(mod.get('price')), 'statUpgrades': stat_ups, 'behaviours': behaviours,
                    'activates': toggles(activates), 'deactivates': toggles(deactivates),
                    'startsActive': b(mod.get('isActive')),
                })
                if b(mod.get('isActive')):
                    warn('warning', 'Upgrades', '%s / %s is serialized as already active.' % (key, mod.get('name')), rel)
            paths.append(modules)

    return {
        'kind': 'tower', 'key': key, 'prefab': rel, 'guid': guid,
        'displayName': td.get('displayName') or key, 'description': td.get('description'),
        'cost': num(td.get('cost')), 'statsConfig': stats_cfg,
        'targetBehaviour': TARGET_BEHAVIOURS[int(num(td.get('targetBehaviour')))],
        'useRotationSlider': b(td.get('useRotationSlider')),
        'strategy': strategy, 'shootingPoints': shooting_points, 'targetter': targetter,
        'upgradePaths': paths, 'inPalette': guid in palette_guids,
    }


# --------------------------------------------------------------------------- levels

def active_in_hierarchy(g, go):
    t = g.transform_of.get(go)
    guard = 0
    while t and guard < 64:
        if not g.go_active(g.go_of(t)):
            return False
        t = int((g.doc(t)['data'].get('m_Father') or {}).get('fileID', 0))
        guard += 1
    return True


def box_colliders(g):
    """World-space oriented boxes of all enabled, solid box colliders that are active in the hierarchy."""
    boxes = []
    for fid, doc in g.o.items():
        if doc['type'] != 'BoxCollider':
            continue
        d = doc['data']
        if b(d.get('m_IsTrigger')) or not b(d.get('m_Enabled', 1)):
            continue
        go = g.go_of(fid)
        if not go or not active_in_hierarchy(g, go):
            continue
        m = g.world_matrix(g.transform_of[go])
        inv = mat_inv_rigid_scaled(m)
        if not inv:
            continue
        c = d.get('m_Center') or {}
        s = d.get('m_Size') or {}
        boxes.append({'inv': inv, 'center': [num(c.get('x')), num(c.get('y')), num(c.get('z'))],
                      'half': [num(s.get('x'), 1) / 2, num(s.get('y'), 1) / 2, num(s.get('z'), 1) / 2],
                      'layer': int(num(g.doc(go)['data'].get('m_Layer'))), 'go': go})
    return boxes


def point_in_boxes(p, boxes, mask, ignore_go=None):
    for bx in boxes:
        if ignore_go is not None and bx['go'] in ignore_go:
            continue
        if mask is not None and not (mask >> bx['layer']) & 1:
            continue
        lp = apply_point(bx['inv'], p)
        if all(abs(lp[i] - bx['center'][i]) <= bx['half'][i] + 1e-4 for i in range(3)):
            return True
    return False


def waypoint_list(g, waypoints_comp_id):
    doc = g.doc(waypoints_comp_id)
    if not doc:
        return [], 0, 0
    serialized = [int(x['fileID']) for x in (doc['data'].get('waypoints') or []) if x and int(x.get('fileID', 0))]
    serialized = [t for t in serialized if g.doc(t)]
    go = g.go_of(waypoints_comp_id)
    tagged = [t for t in g.transform_children_dfs(g.transform_of[go]) if g.go_tag(g.go_of(t)) == 'Waypoint']
    return serialized + tagged, len(serialized), len(tagged)


def end_volumes(g):
    out = []
    for fid, name, data in g.scripts('End'):
        go = g.go_of(fid)
        if not active_in_hierarchy(g, go):
            continue
        m = g.world_matrix(g.transform_of[go])
        inv = mat_inv_rigid_scaled(m)
        for cid in g.components_on(go):
            cd = g.doc(cid)
            if cd['type'] == 'BoxCollider':
                c = cd['data'].get('m_Center') or {}
                s = cd['data'].get('m_Size') or {}
                out.append({'pos': r3(apply_point(m, [0, 0, 0])), 'inv': inv, 'scale': g.world_scale(g.transform_of[go]),
                            'center': [num(c.get('x')), num(c.get('y')), num(c.get('z'))],
                            'half': [num(s.get('x'), 1) / 2, num(s.get('y'), 1) / 2, num(s.get('z'), 1) / 2],
                            'tag': g.go_tag(go)})
    return out


def sphere_hits_box(p, radius, box):
    lp = apply_point(box['inv'], p)
    # radius in local units (approximate with the smallest axis scale)
    s = min(box['scale']) or 1
    rl = radius / s
    d2 = 0.0
    for i in range(3):
        lo, hi = box['center'][i] - box['half'][i], box['center'][i] + box['half'][i]
        v = lp[i]
        if v < lo:
            d2 += (lo - v) ** 2
        elif v > hi:
            d2 += (v - hi) ** 2
    return d2 <= rl * rl


def effective_path(points, end_boxes, enemy_radius):
    """Cuts the polyline where the enemy sphere first touches an End trigger (enemies are removed there)."""
    if not points:
        return points, None
    out = [points[0]]
    step = 0.05
    for i in range(1, len(points)):
        a, c = points[i - 1], points[i]
        seg = dist(a, c)
        n = max(1, int(seg / step))
        for k in range(1, n + 1):
            p = [a[j] + (c[j] - a[j]) * k / n for j in range(3)]
            if any(sphere_hits_box(p, enemy_radius, bx) for bx in end_boxes):
                out.append(p)
                return out, i
        out.append(c)
    return out, None


def extract_profile(pr, guid):
    path = pr.path(guid)
    data = parse_asset(path)
    brackets = []
    for b_ in data.get('incomeBrackets') or []:
        brackets.append({'fromRound': int(num(b_.get('fromRound'), 1)), 'multiplier': num(b_.get('multiplier'), 1)})
    out = {'guid': guid, 'path': pr.rel(path), 'name': os.path.splitext(os.path.basename(path))[0],
           'difficulty': DIFFICULTIES[int(num(data.get('difficulty'), 1))], 'incomeBrackets': brackets}
    for k in ('startMoney', 'lives', 'priceMultiplier', 'endOfWaveBonusBase', 'endOfWaveBonusPerRound', 'refundRate',
              'enemySpeedMultiplier', 'layerHealthMultiplier', 'bossHealthMultiplier', 'armorBonus', 'shieldBonus',
              'winRound', 'freeplayScaling'):
        out[k] = num(data.get(k))
    return out


def extract_level(pr, scene_path, build_index, tower_keys_by_guid, enemy_radius, wave_cache, enemy_by_guid, profile_cache):
    g = Graph(pr, pr.resolve(scene_path))
    name = os.path.splitext(os.path.basename(scene_path))[0]
    level = {'name': name, 'scene': pr.rel(scene_path), 'buildIndex': build_index}
    gms = [x for x in g.scripts('GameManager') if active_in_hierarchy(g, g.go_of(x[0]))]
    if not gms:
        return None
    gm = gms[0][2]
    level['economy'] = {
        'difficulty': DIFFICULTIES[int(num(gm.get('difficulty'), 1))],
        'baseStartingMoney': num(gm.get('baseStartingMoney')),
        'baseLives': num(gm.get('baseLives')),
        'endOfWaveMoney': num(gm.get('endOfWaveMoney')),
        'extraStartingMoney': num(gm.get('extraStartingMoney')),
        'gameSpeed': num(gm.get('gameSpeed'), 1),
    }
    profile_refs = [(ref or {}).get('guid') for ref in (gm.get('difficultyProfiles') or [])]
    level['economy']['hasProfiles'] = any(profile_refs)
    for gid in profile_refs:
        if gid and gid not in profile_cache and pr.path(gid):
            profile_cache[gid] = extract_profile(pr, gid)
    level['isMainMenu'] = b(gm.get('isMainMenu'))
    palette = []
    for ref in gm.get('buildingPrefabs') or []:
        gid = (ref or {}).get('guid')
        if gid in tower_keys_by_guid:
            palette.append(tower_keys_by_guid[gid])
        elif gid:
            warn('warning', 'Levels', '%s: building palette contains %s, which is not a known tower/block prefab.' % (name, pr.rel(pr.path(gid) or gid)), level['scene'])
    level['palette'] = palette

    spawners = [x for x in g.scripts('Spawner') if active_in_hierarchy(g, g.go_of(x[0])) and g.go_tag(g.go_of(x[0])) == 'Spawner']
    if not spawners:
        warn('warning', 'Levels', '%s has no active Spawner tagged "Spawner".' % name, level['scene'])
        return level
    if len(spawners) > 1:
        warn('info', 'Levels', '%s has %d active spawners tagged "Spawner"; GameManager only listens to the first one it finds.' % (name, len(spawners)), level['scene'])
    sid, _, sp = spawners[0]
    waves = []
    for i, ref in enumerate(sp.get('waves') or []):
        gid = (ref or {}).get('guid')
        if not gid or not pr.path(gid):
            warn('error', 'Levels', '%s: Spawner.waves[%d] is empty; StartNextWave would throw when that round starts.' % (name, i), level['scene'])
            waves.append(None)
            continue
        if gid not in wave_cache:
            wave_cache[gid] = extract_wave(pr, gid, enemy_by_guid)
        waves.append(gid)
    scaling = sp.get('scalingFactor')
    level['spawner'] = {'waves': waves, 'scalingFactor': num(scaling, 0.1), 'scalingFactorSerialized': scaling is not None,
                        'autoPlay': b(sp.get('autoPlay'))}

    ends = end_volumes(g)
    if not ends:
        warn('error', 'Levels', '%s has no active End trigger.' % name, level['scene'])
    level['end'] = [{'pos': e['pos'], 'size': r3([e['half'][i] * 2 * e['scale'][i] for i in range(3)])} for e in ends]

    lanes = []
    spawn_points = [int((x or {}).get('fileID', 0)) for x in sp.get('spawnPoints') or []]
    way_comps = [int((x or {}).get('fileID', 0)) for x in sp.get('waypoints') or []]
    for i in range(min(len(spawn_points), len(way_comps))):
        if not g.doc(spawn_points[i]) or not g.doc(way_comps[i]):
            warn('error', 'Levels', '%s: lane %d has a missing spawn point or waypoint list.' % (name, i), level['scene'])
            continue
        start = g.world_position(spawn_points[i])
        wps, n_ser, n_tag = waypoint_list(g, way_comps[i])
        pts = [start] + [g.world_position(t) for t in wps]
        eff, cut_at = effective_path(pts, ends, enemy_radius)
        length = sum(dist(eff[k - 1], eff[k]) for k in range(1, len(eff)))
        if n_ser and n_tag:
            warn('info', 'Levels', '%s lane %d: Waypoints has %d serialized entries and Awake() appends %d tagged children, so the runtime list has %d points%s.'
                 % (name, i, n_ser, n_tag, n_ser + n_tag, ' (harmless: enemies reach the End trigger first)' if cut_at is not None else ''), level['scene'])
        if cut_at is None:
            warn('warning', 'Levels', '%s lane %d: the path never reaches an End trigger; enemies would idle at the last waypoint.' % (name, i), level['scene'])
        lanes.append({'spawn': r3(start), 'waypoints': [r3(p) for p in pts], 'path': [r3(p) for p in eff],
                      'length': round(length, 3), 'rawWaypointCount': len(wps)})
    if len(spawn_points) != len(way_comps):
        warn('info', 'Levels', '%s: %d spawn points but %d waypoint lists; only %d lanes spawn.' % (name, len(spawn_points), len(way_comps), len(lanes)), level['scene'])
    level['lanes'] = lanes

    # Buildable anchors (one per free block face)
    boxes = box_colliders(g)
    anchors = []
    blocks = []
    for fid, _, bd in g.scripts('BuildingBlock'):
        go = g.go_of(fid)
        if active_in_hierarchy(g, go):
            blocks.append(r3(g.world_position(g.transform_of[go])))
    for fid, _, ad in g.scripts('AnchorPoint'):
        go = g.go_of(fid)
        if not active_in_hierarchy(g, go):
            continue
        tr = g.transform_of[go]
        m = g.world_matrix(tr)
        rot = [norm([m[0][c], m[1][c], m[2][c]]) for c in range(3)]  # right, up, forward
        app = int((ad.get('anchorPointPosition') or {}).get('fileID', 0))
        pos = g.world_position(app) if g.doc(app) else g.world_position(tr)
        father = int((g.doc(tr)['data'].get('m_Father') or {}).get('fileID', 0))
        block_center = g.world_position(father) if father else pos
        mask = int(num(((ad.get('layerMask') or {}).get('m_Bits')), 0xFFFFFFFF))
        probe = [block_center[i] + rot[2][i] * 0.75 for i in range(3)]
        own = {g.go_of(father)} if father else set()
        blocked = point_in_boxes(probe, boxes, mask, ignore_go=own)
        anchors.append({'pos': r3(pos), 'right': r3(rot[0]), 'up': r3(rot[1]), 'fwd': r3(rot[2]),
                        'block': r3(block_center), 'blocked': blocked})
    level['anchors'] = anchors
    level['blocks'] = blocks
    if anchors and all(a['blocked'] for a in anchors):
        warn('warning', 'Levels', '%s: every anchor looks blocked (geometry heuristic).' % name, level['scene'])
    return level


# --------------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--project', default=DEFAULT_PROJECT, help='Unity project root (folder containing Assets/)')
    ap.add_argument('--out', default=HERE, help='output folder for balance-data.json/.js')
    args = ap.parse_args()

    pr = Project(os.path.abspath(args.project))
    print('Indexed %d assets in %s' % (len(pr.guid_to_path), pr.root))

    enemies, enemy_by_guid, enemy_info = extract_enemies(pr)
    enemy_radius = enemy_info['colliderRadius']

    # Build settings
    build = []
    bs_path = os.path.join(pr.root, 'ProjectSettings', 'EditorBuildSettings.asset')
    for doc in parse_documents(bs_path).values():
        for i, sc in enumerate(doc['data'].get('m_Scenes') or []):
            build.append({'path': sc.get('path'), 'enabled': b(sc.get('enabled'))})
    version = ''
    vp = os.path.join(pr.root, 'ProjectSettings', 'ProjectVersion.txt')
    if os.path.isfile(vp):
        m = re.search(r'm_EditorVersion: (\S+)', open(vp).read())
        version = m.group(1) if m else ''

    # Palette across all build scenes (collected first, so towers know whether they are buildable)
    scene_paths = []
    for i, sc in enumerate(build):
        if sc['enabled'] and sc['path']:
            scene_paths.append((os.path.join(pr.root, sc['path']), i))
    palette_guids = set()
    for sp, _ in scene_paths:
        for _, _, gm in Graph(pr, pr.resolve(sp)).scripts('GameManager'):
            for ref in gm.get('buildingPrefabs') or []:
                if (ref or {}).get('guid'):
                    palette_guids.add(ref['guid'])

    towers = []
    blocks = []
    tower_dir = os.path.join(pr.assets, 'Prefabs', 'Tower')
    candidates = sorted(os.path.join(tower_dir, f) for f in os.listdir(tower_dir) if f.endswith('.prefab'))
    candidates += [pr.path(gid) for gid in palette_guids if pr.path(gid) and pr.path(gid) not in candidates]
    for path in candidates:
        t = extract_tower(pr, path, palette_guids)
        if not t:
            continue
        (blocks if t['kind'] == 'block' else towers).append(t)
        if t['kind'] == 'tower' and t['statsConfig']['cost'] != t['cost']:
            warn('info', 'Towers', '%s: StatsConfig.Cost (%g) differs from Building.cost (%g). Only Building.cost is used for buying and selling.'
                 % (t['displayName'], t['statsConfig']['cost'], t['cost']), t['prefab'])
    tower_keys_by_guid = {t['guid']: t['key'] for t in towers + blocks}

    wave_cache = {}
    profile_cache = {}
    levels = []
    for sp, idx in scene_paths:
        lvl = extract_level(pr, sp, idx, tower_keys_by_guid, enemy_radius, wave_cache, enemy_by_guid, profile_cache)
        if lvl:
            levels.append(lvl)
            print('Level %-24s lanes=%d waves=%d anchors=%d' % (lvl['name'], len(lvl.get('lanes', [])), len(lvl.get('spawner', {}).get('waves', [])), len(lvl.get('anchors', []))))

    # Unused-stat and cross-cutting checks
    for t in towers:
        read = set(STATS_READ.get(t['strategy']['type'], []))
        for path in t['upgradePaths']:
            for mod in path:
                strategies = [t['strategy']['type']] + [bh['strategy']['type'] for bh in mod['behaviours'] if bh.get('strategy')]
                read_any = set().union(*[set(STATS_READ.get(s, [])) for s in strategies])
                for su in mod['statUpgrades']:
                    if su['stat'] not in read_any:
                        warn('warning', 'Upgrades', '%s / %s changes %s, which %s never reads (no gameplay effect).'
                             % (t['displayName'], mod['name'], su['stat'], t['strategy']['type']), t['prefab'])
        if not t['inPalette']:
            warn('info', 'Towers', '%s is not in any build scene\'s building palette.' % t['displayName'], t['prefab'])

    data = {
        'meta': {
            'generatedAt': datetime.datetime.now().isoformat(timespec='seconds'),
            'unityVersion': version,
            'project': pr.root,
            'generator': 'Tools/BalanceDashboard/extract.py',
            'schema': 1,
        },
        'constants': {
            'statTypes': STAT_TYPES,
            'statsRead': STATS_READ,
            'shapes': SHAPES,
            'colors': COLORS,
            'targetBehaviours': TARGET_BEHAVIOURS,
            'enemy': enemy_info,
            'difficulties': DIFFICULTIES,
            'poolReturnDelay': 0.5,
            'maxPoolSize': 4096,       # ProjectilePoolManager.MaxPoolSize (Pulse only; ProjectileSystem kinds have no pool)
            'minFireInterval': 0.05,
            'projectileFadeTime': 0.1,
            'maxSpreadDegrees': 25,
            'moneyPerLayer': 1,
            'buildScenes': build,
        },
        'enemies': enemies,
        'towers': towers,
        'blocks': blocks,
        'waves': wave_cache,
        'profiles': {p['difficulty']: p for p in profile_cache.values()},
        'metaUpgrades': extract_meta_upgrades(pr),
        'levels': levels,
        'warnings': warnings,
    }
    profiles = data['profiles']
    missing = [d for d in DIFFICULTIES if d not in profiles]
    if missing:
        warn('warning', 'Economy', 'No DifficultyProfile for %s: GameManager falls back to the legacy start money and lives.' % ', '.join(missing), 'Assets/Prefabs/GAME_SETUP.prefab')
    order = [profiles[d] for d in DIFFICULTIES if d in profiles]
    for a_, b_ in zip(order, order[1:]):
        if b_['priceMultiplier'] < a_['priceMultiplier'] or b_['lives'] > a_['lives'] or b_['startMoney'] > a_['startMoney']:
            warn('warning', 'Economy', 'DifficultyProfile %s is easier than %s in prices, lives or start money.' % (b_['name'], a_['name']), b_['path'])
    for l in levels:
        if not l['isMainMenu'] and not l['economy'].get('hasProfiles'):
            warn('warning', 'Economy', '%s: GameManager has no difficulty profiles; it uses the legacy start money and lives.' % l['name'], l['scene'])

    os.makedirs(args.out, exist_ok=True)
    jpath = os.path.join(args.out, 'balance-data.json')
    with open(jpath, 'w') as fh:
        json.dump(data, fh, indent=1)
    with open(os.path.join(args.out, 'balance-data.js'), 'w') as fh:
        fh.write('// Generated by extract.py, do not edit. Regenerate with: python3 Tools/BalanceDashboard/extract.py\n')
        fh.write('window.BALANCE_DATA = ')
        json.dump(data, fh, separators=(',', ':'))
        fh.write(';\n')
    print('Wrote %s (%d towers, %d enemies, %d waves, %d levels, %d warnings)'
          % (jpath, len(towers), len(enemies), len(wave_cache), len(levels), len(warnings)))


if __name__ == '__main__':
    main()
