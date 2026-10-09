#!/usr/bin/env python3
"""Generates the wave sets in wave-design.json with wavegen.js and writes them into the Unity project.

For every set it
  - writes one WaveData asset per round to Assets/ScriptableObjects/WaveData/<set>/ (GUIDs of existing
    files are kept, assets of rounds that no longer exist are removed),
  - points the level's Spawner at them (a prefab's waves list, or list overrides on a nested Spawner
    instance inside a container prefab) and updates scene overrides of the list size,
  - sets GameManager.extraStartingMoney on the scene's GAME_SETUP instance.

Run from the repo root after changing wave-design.json or wavegen.js, then run extract.py:
    python3 Tools/BalanceDashboard/write_waves.py [--dry-run] [--set Beginner01]
Needs JavaScriptCore (macOS) or node to run wavegen.js, and balance-data.js from extract.py.
"""
import argparse
import glob
import json
import os
import re
import shutil
import subprocess
import tempfile
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..', '3DTD'))
JSC = '/System/Library/Frameworks/JavaScriptCore.framework/Versions/Current/Helpers/jsc'
WAVE_SCRIPT_GUID = '3017a32a3b6a33e45bebd247e0a6280b'      # WaveData.cs
SPAWNER_SCRIPT_GUID = '5bc9cf058b07a15418a2316b0c7b0cce'   # Spawner.cs
GAME_MANAGER_SCRIPT_GUID = None                            # read from GameManager.cs.meta


def meta_guid(path):
    with open(path + '.meta') as fh:
        return re.search(r'^guid: (\w+)', fh.read(), re.M).group(1)


def run_generator(design):
    """Runs wavegen.js headless and returns {set name: [waves]}."""
    runner = '''
var D = window.BALANCE_DATA;
var E = TDEngine.init(D, {});
var DESIGN = %s;
var out = {};
DESIGN.sets.forEach(function (s) {
  var p = JSON.parse(JSON.stringify(s.params)); p.rounds = s.rounds;
  out[s.name] = generateWaves(E, p);
});
print(JSON.stringify(out));
''' % json.dumps(design)
    with tempfile.TemporaryDirectory() as tmp:
        shim = os.path.join(tmp, 'shim.js')
        main = os.path.join(tmp, 'main.js')
        with open(shim, 'w') as fh:
            fh.write('var window = this; if (typeof print === "undefined") { var print = function (s) { console.log(s); }; }\n')
        with open(main, 'w') as fh:
            fh.write(runner)
        files = [shim] + [os.path.join(HERE, f) for f in ('balance-data.js', 'engine.js', 'wavegen.js')] + [main]
        if os.path.exists(JSC):
            res = subprocess.run([JSC] + files, capture_output=True, text=True)
        elif shutil.which('node'):
            code = ''.join(open(f).read() + '\n' for f in files)
            res = subprocess.run(['node', '-e', code], capture_output=True, text=True)
        else:
            raise SystemExit('Neither JavaScriptCore nor node found to run wavegen.js')
    if res.returncode != 0 or not res.stdout.strip():
        raise SystemExit('wavegen.js failed:\n' + res.stderr + res.stdout)
    return json.loads(res.stdout.strip().splitlines()[-1])


def int_list_hex(values):
    return ''.join(int(v).to_bytes(4, 'little', signed=True).hex() for v in values)


def wave_asset(name, wave, enemy_guids):
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
             '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
             '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
             '  m_Script: {fileID: 11500000, guid: %s, type: 3}' % WAVE_SCRIPT_GUID, '  m_Name: %s' % name,
             '  m_EditorClassIdentifier: ', '  enemiesToSpawn:']
    for e in wave['entries']:
        lines.append('  - {fileID: 11400000, guid: %s, type: 2}' % enemy_guids[e['enemy']])
    lines.append('  enemySpawnCount: %s' % int_list_hex([e['count'] for e in wave['entries']]))
    lines.append('  spawnDelay:')
    for e in wave['entries']:
        lines.append('  - %s' % ('%g' % e['delay']))
    lines.append('  enemyTraits: %s' % int_list_hex([e['traits'] for e in wave['entries']]))
    return '\n'.join(lines) + '\n'


def write_meta(path, guid=None):
    if os.path.exists(path + '.meta'):
        return meta_guid(path)
    guid = guid or uuid.uuid4().hex
    with open(path + '.meta', 'w') as fh:
        fh.write('fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
                 '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid)
    return guid


def write_folder_meta(path):
    if not os.path.exists(path + '.meta'):
        with open(path + '.meta', 'w') as fh:
            fh.write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                     '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)


def read_text(path):
    with open(path, 'rb') as fh:
        raw = fh.read()
    return raw.decode('utf-8'), b'\r\n' in raw


def write_text(path, text, crlf):
    text = text.replace('\r\n', '\n')
    if crlf:
        text = text.replace('\n', '\r\n')
    with open(path, 'wb') as fh:
        fh.write(text.encode('utf-8'))


def spawner_component_id(text):
    m = re.search(r'--- !u!114 &(-?\d+)\nMonoBehaviour:\n(?:  .*\n)*?  m_Script: \{fileID: 11500000, guid: %s' % SPAWNER_SCRIPT_GUID, text)
    if not m:
        raise SystemExit('No Spawner component found')
    return m.group(1)


def assign_to_prefab(prefab, guids):
    text, crlf = read_text(prefab)
    text = text.replace('\r\n', '\n')
    comp = spawner_component_id(text)
    start = text.index('--- !u!114 &%s\n' % comp)
    w = text.index('\n  waves:', start) + 1
    end = w + len('  waves:')
    rest = text[end:]
    m = re.match(r'( \[\])?\n((?:  - \{[^\n]*\}\n)*)', rest)
    new = '  waves:\n' + ''.join('  - {fileID: 11400000, guid: %s, type: 2}\n' % g for g in guids)
    text = text[:w] + new + rest[m.end():]
    write_text(prefab, text, crlf)
    return comp


MOD_ITEM = '    - target:'
MOD_FIELDS = ('target', 'propertyPath', 'value', 'objectReference')


def split_modifications(block):
    """Splits a PrefabInstance block at its m_Modifications list.

    Returns (head, items, tail): head ends with the '    m_Modifications:' line, items is one string per
    '    - target:' entry including Unity's wrapped continuation lines (indented deeper than the entry's
    fields), tail starts at the next field of m_Modification ('    m_RemovedComponents:' ...)."""
    lines = block.splitlines(True)
    try:
        k = next(i for i, l in enumerate(lines) if l.rstrip('\n') in ('    m_Modifications:', '    m_Modifications: []'))
    except StopIteration:
        raise SystemExit('PrefabInstance block has no m_Modifications list')
    head = ''.join(lines[:k]) + '    m_Modifications:\n'
    items = []
    i = k + 1
    if lines[k].rstrip('\n') == '    m_Modifications:':
        while i < len(lines) and lines[i].startswith('    - '):
            if not lines[i].startswith(MOD_ITEM):
                raise SystemExit('Unexpected m_Modifications entry: %r' % lines[i])
            j = i + 1
            while j < len(lines) and lines[j].startswith('      '):
                j += 1
            items.append(''.join(lines[i:j]))
            i = j
    return head, items, ''.join(lines[i:])


def parse_modification(item):
    """Parses one m_Modifications entry into {field: value}, joining wrapped continuation lines.
    Raises SystemExit when the entry doesn't have exactly the four fields Unity writes."""
    fields = {}
    key = None
    for n, line in enumerate(item.splitlines()):
        if n == 0:
            line = '      ' + line[len('    - '):]
        m = re.match(r'      (\w+):(?: (.*))?$', line)
        if m:
            key = m.group(1)
            if key in fields:
                raise SystemExit('Duplicate %s in m_Modifications entry:\n%s' % (key, item))
            fields[key] = m.group(2) or ''
        elif key and line.startswith('        '):
            fields[key] += ' ' + line.strip()
        else:
            raise SystemExit('Malformed m_Modifications entry:\n%s' % item)
    if tuple(fields) != MOD_FIELDS:
        raise SystemExit('m_Modifications entry has fields %s, expected %s:\n%s' % (list(fields), list(MOD_FIELDS), item))
    for f in ('target', 'objectReference'):
        if not re.match(r'^\{[^{}]*\}$', fields[f]):
            raise SystemExit('m_Modifications entry has a malformed %s:\n%s' % (f, item))
    return fields


def is_waves_override(fields, comp, sp_guid):
    t = re.match(r'\{fileID: (-?\d+), guid: (\w+), type: 3\}$', fields['target'])
    return bool(t) and t.group(1) == comp and t.group(2) == sp_guid and fields['propertyPath'].strip("'").startswith('waves.Array.')


def find_prefab_instance(text, source_guid):
    """(start, end) of the PrefabInstance block whose m_SourcePrefab is source_guid (m_SourcePrefab may be wrapped)."""
    m = re.search(r'--- !u!1001 &-?\d+\nPrefabInstance:\n(?:  .*\n)*?  m_SourcePrefab: \{fileID: 100100000, guid: %s,\s+type: 3\}\n' % source_guid, text)
    return (m.start(), m.end()) if m else None


def check_nested_waves(text, comp, sp_guid, guids, other_count):
    """Re-parses the written block and fails loudly unless every entry is well formed, the other overrides are
    untouched and the waves overrides are exactly the new list."""
    span = find_prefab_instance(text, sp_guid)
    if not span:
        raise SystemExit('Spawner instance vanished after writing')
    _, items, tail = split_modifications(text[span[0]:span[1]])
    if not tail.startswith('    m_RemovedComponents:'):
        raise SystemExit('m_Modifications is not followed by m_RemovedComponents:\n%s' % tail[:200])
    parsed = [parse_modification(it) for it in items]
    waves = [f for f in parsed if is_waves_override(f, comp, sp_guid)]
    if len(parsed) - len(waves) != other_count:
        raise SystemExit('Other overrides changed: %d before, %d after' % (other_count, len(parsed) - len(waves)))
    expected = [('waves.Array.size', str(len(guids)), '{fileID: 0}')] + [
        ('waves.Array.data[%d]' % i, '', '{fileID: 11400000, guid: %s, type: 2}' % g) for i, g in enumerate(guids)]
    got = [(f['propertyPath'].strip("'"), f['value'], f['objectReference']) for f in waves]
    if got != expected:
        raise SystemExit('waves overrides were not written as expected')


def assign_to_nested(container, spawner_prefab, guids):
    """List overrides on the Spawner prefab instance nested in a container prefab."""
    sp_text, _ = read_text(spawner_prefab)
    comp = spawner_component_id(sp_text.replace('\r\n', '\n'))
    sp_guid = meta_guid(spawner_prefab)
    text, crlf = read_text(container)
    text = text.replace('\r\n', '\n')
    span = find_prefab_instance(text, sp_guid)
    if not span:
        raise SystemExit('%s has no instance of %s' % (container, spawner_prefab))
    head, items, tail = split_modifications(text[span[0]:span[1]])
    # drop old waves overrides, keep everything else as written (wrapped lines included)
    keep = [it for it in items if not is_waves_override(parse_modification(it), comp, sp_guid)]
    target = '    - target: {fileID: %s, guid: %s, type: 3}\n' % (comp, sp_guid)
    mods = target + '      propertyPath: waves.Array.size\n      value: %d\n      objectReference: {fileID: 0}\n' % len(guids)
    for i, g in enumerate(guids):
        mods += target + "      propertyPath: 'waves.Array.data[%d]'\n      value: \n      objectReference: {fileID: 11400000, guid: %s, type: 2}\n" % (i, g)
    text = text[:span[0]] + head + ''.join(keep) + mods + tail + text[span[1]:]
    check_nested_waves(text, comp, sp_guid, guids, len(keep))
    write_text(container, text, crlf)
    written, _ = read_text(container)
    check_nested_waves(written.replace('\r\n', '\n'), comp, sp_guid, guids, len(keep))
    return comp


def update_scene_size_overrides(prefab_guid, comp, size):
    for scene in glob.glob(os.path.join(PROJECT, 'Assets', 'Scenes', '*.unity')):
        text, crlf = read_text(scene)
        pat = re.compile(r'(    - target: \{fileID: %s, guid: %s,\s*type: 3\}\n      propertyPath: waves\.Array\.size\n      value: )(\d+)' % (comp, prefab_guid))
        if pat.search(text.replace('\r\n', '\n')):
            new = pat.sub(lambda mm: mm.group(1) + str(size), text.replace('\r\n', '\n'))
            write_text(scene, new, crlf)
            print('   updated list size override in', os.path.relpath(scene, PROJECT))


def set_extra_money(scene, amount):
    gm_cs = os.path.join(PROJECT, 'Assets', 'Scripts', 'GameManager.cs')
    gm_guid = meta_guid(gm_cs)
    setup = os.path.join(PROJECT, 'Assets', 'Prefabs', 'GAME_SETUP.prefab')
    setup_guid = meta_guid(setup)
    st, _ = read_text(setup)
    st = st.replace('\r\n', '\n')
    m = re.search(r'--- !u!114 &(-?\d+)\nMonoBehaviour:\n(?:  .*\n)*?  m_Script: \{fileID: 11500000, guid: %s' % gm_guid, st)
    comp = m.group(1)
    text, crlf = read_text(scene)
    text = text.replace('\r\n', '\n')
    pat = re.compile(r'(    - target: \{fileID: %s, guid: %s,\s*type: 3\}\n      propertyPath: extraStartingMoney\n      value: )(-?\d+)' % (comp, setup_guid))
    if pat.search(text):
        text = pat.sub(lambda mm: mm.group(1) + str(amount), text)
    elif amount == 0:
        return
    else:
        mi = re.search(r'--- !u!1001 &-?\d+\nPrefabInstance:\n((?:  .*\n)*?)  m_SourcePrefab: \{fileID: 100100000, guid: %s, type: 3\}\n' % setup_guid, text)
        if not mi:
            print('   ! %s has no GAME_SETUP instance; extraStartingMoney not set' % os.path.basename(scene))
            return
        block = mi.group(0)
        entry = ('    - target: {fileID: %s, guid: %s, type: 3}\n      propertyPath: extraStartingMoney\n      value: %d\n'
                 '      objectReference: {fileID: 0}\n') % (comp, setup_guid, amount)
        k = block.index('    m_RemovedComponents:')
        text = text.replace(block, block[:k] + entry + block[k:], 1)
    write_text(scene, text, crlf)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--dry-run', action='store_true', help='only print the generated waves')
    ap.add_argument('--set', action='append', help='only these sets (default: all)')
    args = ap.parse_args()

    with open(os.path.join(HERE, 'wave-design.json')) as fh:
        design = json.load(fh)
    if args.set:
        design['sets'] = [s for s in design['sets'] if s['name'] in args.set]
    with open(os.path.join(HERE, 'balance-data.json')) as fh:
        data = json.load(fh)
    enemy_guids = [e['guid'] for e in data['enemies']]

    generated = run_generator(design)
    with open(os.path.join(HERE, 'waves-generated.json'), 'w') as fh:
        json.dump(generated, fh, indent=1)

    for s in design['sets']:
        waves = generated[s['name']]
        print('%s: %d waves, %d enemies per lane in total' % (s['name'], len(waves), sum(e['count'] for w in waves for e in w['entries'])))
        if args.dry_run:
            continue
        folder = os.path.join(PROJECT, 'Assets', 'ScriptableObjects', 'WaveData', s['name'])
        os.makedirs(folder, exist_ok=True)
        write_folder_meta(folder)
        keep = set()
        guids = []
        for w in waves:
            name = '%s %s' % (s['name'], w['name'])
            path = os.path.join(folder, name + '.asset')
            with open(path, 'w') as fh:
                fh.write(wave_asset(name, w, enemy_guids))
            guids.append(write_meta(path))
            keep.add(path)
        for old in glob.glob(os.path.join(folder, '*.asset')):
            if old not in keep:
                os.remove(old)
                if os.path.exists(old + '.meta'):
                    os.remove(old + '.meta')
        a = s['assign']
        if 'prefab' in a:
            prefab = os.path.join(PROJECT, a['prefab'])
            comp = assign_to_prefab(prefab, guids)
            update_scene_size_overrides(meta_guid(prefab), comp, len(guids))
        else:
            assign_to_nested(os.path.join(PROJECT, a['container']), os.path.join(PROJECT, a['spawnerPrefab']), guids)
        scene = os.path.join(PROJECT, 'Assets', 'Scenes', s['level'] + '.unity')
        set_extra_money(scene, int(s.get('extraStartingMoney', 0)))
        print('   assigned to', a.get('prefab') or a.get('container'))


if __name__ == '__main__':
    main()
