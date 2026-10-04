"""Minimal reader for Unity's serialized YAML (scenes, prefabs, assets) without third-party packages.

It understands the subset Unity writes: block mappings and sequences, plain / quoted multi-line
scalars and flow mappings like {fileID: 1, guid: abc, type: 3}. On top of that, `Project` resolves
prefab instances (nested prefabs, variants, scene overrides) into one flat object table per file,
using Unity's rule that an object coming from a prefab instance gets the id (instanceId ^ sourceId).
"""

import copy
import math
import os
import re

MASK = 0x7FFFFFFFFFFFFFFF
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?')


# --------------------------------------------------------------------------- scalar / flow parsing

def _scalar(text):
    text = text.strip()
    if text == '' or text == '~' or text == 'null':
        return None
    if text[0] == "'" and text[-1] == "'" and len(text) >= 2:
        return text[1:-1].replace("''", "'")
    if text[0] == '"' and text[-1] == '"' and len(text) >= 2:
        return bytes(text[1:-1], 'utf-8').decode('unicode_escape', errors='ignore')
    if text[0] in '{[':
        return _flow(text)
    return text


def _flow(text):
    pos = 0

    def skip():
        nonlocal pos
        while pos < len(text) and text[pos] in ' \t\n':
            pos += 1

    def value():
        nonlocal pos
        skip()
        if text[pos] == '{':
            pos += 1
            out = {}
            while True:
                skip()
                if text[pos] == '}':
                    pos += 1
                    return out
                key_end = text.index(':', pos)
                key = text[pos:key_end].strip()
                pos = key_end + 1
                out[key] = value()
                skip()
                if text[pos] == ',':
                    pos += 1
        if text[pos] == '[':
            pos += 1
            out = []
            while True:
                skip()
                if text[pos] == ']':
                    pos += 1
                    return out
                out.append(value())
                skip()
                if text[pos] == ',':
                    pos += 1
        start = pos
        if text[pos] in '\'"':
            quote = text[pos]
            pos += 1
            while pos < len(text):
                if text[pos] == quote:
                    if quote == "'" and pos + 1 < len(text) and text[pos + 1] == "'":
                        pos += 2
                        continue
                    pos += 1
                    break
                pos += 1
            return _scalar(text[start:pos])
        while pos < len(text) and text[pos] not in ',}]':
            pos += 1
        return _scalar(text[start:pos])

    return value()


def _balanced(text):
    depth = 0
    quote = None
    for ch in text:
        if quote:
            if ch == quote:
                quote = None
        elif ch in '\'"':
            quote = ch
        elif ch in '{[':
            depth += 1
        elif ch in '}]':
            depth -= 1
    return depth <= 0


def _quote_closed(text):
    q = text[0]
    body = text[1:]
    if q == "'":
        body = body.replace("''", '')
        return "'" in body
    i = 0
    while i < len(body):
        if body[i] == '\\':
            i += 2
            continue
        if body[i] == '"':
            return True
        i += 1
    return False


# --------------------------------------------------------------------------- block parsing

class _Lines:
    def __init__(self, raw_lines):
        self.items = []
        for line in raw_lines:
            stripped = line.rstrip('\n').rstrip('\r')
            if stripped.strip() == '':
                self.items.append((None, ''))
            else:
                indent = len(stripped) - len(stripped.lstrip(' '))
                self.items.append((indent, stripped[indent:]))
        self.i = 0

    def peek(self):
        while self.i < len(self.items) and self.items[self.i][0] is None:
            self.i += 1
        return self.items[self.i] if self.i < len(self.items) else (None, None)


def _split_key(content):
    # "key: value" / "key:" ; keys may be quoted
    if content.startswith(("'", '"')):
        q = content[0]
        end = content.index(q, 1)
        while q == "'" and end + 1 < len(content) and content[end + 1] == "'":
            end = content.index(q, end + 2)
        key = _scalar(content[:end + 1])
        rest = content[end + 1:]
        if rest.startswith(':'):
            return key, rest[1:].strip()
        return None, None
    m = re.match(r'^([^\'"{\[][^:]*?):(?:\s+(.*)|$)', content)
    if not m:
        return None, None
    return m.group(1), (m.group(2) or '').strip()


def _inline_value(lines, text, key_indent):
    """Parse a value that starts on the current line, consuming continuation lines."""
    if text == '':
        return None
    if text[0] in '{[' and not _balanced(text):
        while not _balanced(text):
            _, cont = lines.items[lines.i]
            lines.i += 1
            text += ' ' + cont
        return _scalar(text)
    if text[0] in '\'"' and not _quote_closed(text):
        parts = [text]
        while True:
            ind, cont = lines.items[lines.i]
            lines.i += 1
            parts.append(cont if ind is not None else '')
            if ind is not None and _quote_closed(' '.join(parts)):
                break
        return _scalar(_fold(parts))
    # plain scalar, may continue on deeper indented lines
    parts = [text]
    while lines.i < len(lines.items):
        ind, cont = lines.items[lines.i]
        if ind is None:
            # blank line inside a folded scalar is a newline, but only if text follows deeper
            j = lines.i
            while j < len(lines.items) and lines.items[j][0] is None:
                j += 1
            if j < len(lines.items) and lines.items[j][0] > key_indent:
                parts.append('')
                lines.i += 1
                continue
            break
        if ind <= key_indent:
            break
        parts.append(cont)
        lines.i += 1
    return _scalar(_fold(parts)) if len(parts) > 1 else _scalar(text)


def _fold(parts):
    out = ''
    for idx, p in enumerate(parts):
        if idx == 0:
            out = p
        elif p == '':
            out += '\n'
        elif out.endswith('\n'):
            out += p
        else:
            out += ' ' + p
    return out


def _parse_block(lines, indent):
    ind, content = lines.peek()
    if ind is None or ind < indent:
        return None
    if content.startswith('- ') or content == '-':
        return _parse_seq(lines, ind)
    return _parse_map(lines, ind)


def _parse_seq(lines, indent):
    out = []
    while True:
        ind, content = lines.peek()
        if ind is None or ind != indent or not (content.startswith('- ') or content == '-'):
            return out
        lines.i += 1
        item = content[2:] if content.startswith('- ') else ''
        if item == '':
            out.append(_parse_block(lines, indent + 1))
            continue
        key, rest = _split_key(item)
        if key is not None and not item.startswith(('{', '[')):
            # mapping inside a sequence item; its keys live at indent + 2
            m = {}
            m[key] = _map_value(lines, rest, indent + 2)
            m.update(_parse_map(lines, indent + 2))
            out.append(m)
        else:
            out.append(_inline_value(lines, item, indent))


def _map_value(lines, rest, key_indent):
    if rest != '':
        return _inline_value(lines, rest, key_indent)
    ind, content = lines.peek()
    if ind is None:
        return None
    if ind > key_indent:
        return _parse_block(lines, ind)
    if ind == key_indent and (content.startswith('- ') or content == '-'):
        return _parse_seq(lines, ind)
    return None


def _parse_map(lines, indent):
    out = {}
    while True:
        ind, content = lines.peek()
        if ind is None or ind != indent or content.startswith('- '):
            return out
        key, rest = _split_key(content)
        lines.i += 1
        if key is None:
            continue
        out[key] = _map_value(lines, rest, indent)


def parse_documents(path):
    """Returns {fileID: {'cls', 'type', 'stripped', 'data'}} for one Unity file."""
    with open(path, encoding='utf-8', errors='ignore') as fh:
        raw = fh.readlines()
    docs = {}
    starts = [i for i, l in enumerate(raw) if l.startswith('--- !u!')]
    for n, s in enumerate(starts):
        e = starts[n + 1] if n + 1 < len(starts) else len(raw)
        m = HEADER.match(raw[s])
        cls, fid, stripped = int(m.group(1)), int(m.group(2)), bool(m.group(3))
        body = _Lines(raw[s + 1:e])
        top = _parse_map(body, 0)
        type_name = next(iter(top)) if top else None
        docs[fid] = {'cls': cls, 'type': type_name, 'stripped': stripped,
                     'data': (top.get(type_name) if type_name else None) or {}}
    return docs


def parse_asset(path):
    """Shortcut for a single ScriptableObject asset: returns its MonoBehaviour data."""
    for doc in parse_documents(path).values():
        if doc['type'] == 'MonoBehaviour':
            return doc['data']
    return {}


def decode_int_array(value, size=4):
    """Unity writes List<int> as a hex blob of little-endian int32s."""
    if isinstance(value, list):
        return [int(v) for v in value]
    if value is None or value == '':
        return []
    hexstr = str(value)
    out = []
    for i in range(0, len(hexstr), size * 2):
        chunk = hexstr[i:i + size * 2]
        n = int.from_bytes(bytes.fromhex(chunk), 'little', signed=True)
        out.append(n)
    return out


def num(v, default=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return default


# --------------------------------------------------------------------------- prefab resolution

def _remap_refs(node, mapping):
    if isinstance(node, dict):
        if 'fileID' in node and 'guid' not in node and len(node) <= 2:
            fid = int(node['fileID'])
            if fid in mapping:
                return {'fileID': mapping[fid]}
            return node
        return {k: _remap_refs(v, mapping) for k, v in node.items()}
    if isinstance(node, list):
        return [_remap_refs(v, mapping) for v in node]
    return node


class _XorMap(dict):
    """Lazily maps any local id of a source prefab to its id inside an instance."""
    def __init__(self, instance_id, valid):
        super().__init__()
        self.instance_id = instance_id
        self.valid = valid

    def __contains__(self, fid):
        return fid in self.valid

    def __getitem__(self, fid):
        return (self.instance_id ^ fid) & MASK


def _coerce(value):
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return value
    s = str(value)
    try:
        return int(s)
    except ValueError:
        try:
            return float(s)
        except ValueError:
            return s


def set_property(root, path, value, obj_ref):
    toks = path.split('.')
    cur = root
    i = 0
    while i < len(toks):
        t = toks[i]
        if t == 'Array':
            nt = toks[i + 1]
            if not isinstance(cur, list):
                return
            if nt == 'size':
                n = int(value)
                if len(cur) > n:
                    del cur[n:]
                else:
                    template = copy.deepcopy(cur[-1]) if cur and isinstance(cur[-1], dict) else None
                    cur.extend(copy.deepcopy(template) for _ in range(n - len(cur)))
                return
            idx = int(re.match(r'data\[(\d+)\]', nt).group(1))
            while len(cur) <= idx:
                cur.append(None)
            if i + 1 == len(toks) - 1:
                cur[idx] = _leaf(cur[idx], value, obj_ref, True)
                return
            if not isinstance(cur[idx], (dict, list)):
                cur[idx] = {}
            cur = cur[idx]
            i += 2
            continue
        if not isinstance(cur, dict):
            return
        last = i == len(toks) - 1
        if last:
            cur[t] = _leaf(cur.get(t), value, obj_ref, False)
            return
        nxt = toks[i + 1]
        if nxt == 'Array':
            if isinstance(cur.get(t), str) and re.fullmatch(r'[0-9a-fA-F]*', cur.get(t) or ''):
                cur[t] = decode_int_array(cur[t])
            if not isinstance(cur.get(t), list):
                cur[t] = []
        elif not isinstance(cur.get(t), (dict, list)):
            cur[t] = {}
        cur = cur[t]
        i += 1


def _leaf(existing, value, obj_ref, is_array_elem):
    ref_set = isinstance(obj_ref, dict) and (int(obj_ref.get('fileID', 0) or 0) != 0 or obj_ref.get('guid'))
    if ref_set or isinstance(existing, dict) and 'fileID' in existing:
        return obj_ref
    if is_array_elem and (value is None or value == '') and isinstance(obj_ref, dict):
        return obj_ref
    return _coerce(value)


class Project:
    def __init__(self, project_root):
        self.root = project_root
        self.assets = os.path.join(project_root, 'Assets')
        self.guid_to_path = {}
        for base, _, files in os.walk(self.assets):
            for f in files:
                if not f.endswith('.meta'):
                    continue
                p = os.path.join(base, f)
                with open(p, encoding='utf-8', errors='ignore') as fh:
                    for line in fh:
                        if line.startswith('guid:'):
                            self.guid_to_path[line.split()[1]] = p[:-5]
                            break
        self.path_to_guid = {p: g for g, p in self.guid_to_path.items()}
        self._resolved = {}
        self._assets = {}

    def rel(self, path):
        return os.path.relpath(path, self.root)

    def path(self, guid):
        return self.guid_to_path.get(guid)

    def script_name(self, guid):
        p = self.path(guid)
        if p and p.endswith('.cs'):
            return os.path.basename(p)[:-3]
        return None

    def asset(self, guid):
        if guid not in self._assets:
            p = self.path(guid)
            self._assets[guid] = parse_asset(p) if p and os.path.isfile(p) else None
        return self._assets[guid]

    def resolve(self, guid_or_path):
        """Flattened {fileID: doc} for a prefab or scene, with all prefab instances expanded."""
        if guid_or_path in self.guid_to_path:
            guid, path = guid_or_path, self.guid_to_path[guid_or_path]
        else:
            path, guid = guid_or_path, self.path_to_guid.get(guid_or_path)
        key = guid or path
        if key in self._resolved:
            return self._resolved[key]
        self._resolved[key] = {}  # cycle guard
        raw = parse_documents(path)
        result = {}
        alias = {}
        for fid, doc in raw.items():
            if doc['stripped']:
                src = doc['data'].get('m_CorrespondingSourceObject') or {}
                inst = doc['data'].get('m_PrefabInstance') or {}
                alias[fid] = (int(inst.get('fileID', 0)) ^ int(src.get('fileID', 0))) & MASK
            elif doc['type'] != 'PrefabInstance':
                result[fid] = copy.deepcopy(doc)

        for fid, doc in raw.items():
            if doc['type'] != 'PrefabInstance' or doc['stripped']:
                continue
            data = doc['data']
            src_guid = (data.get('m_SourcePrefab') or {}).get('guid')
            if not src_guid or not self.path(src_guid):
                continue
            src = self.resolve(src_guid)
            mapping = _XorMap(fid, set(src.keys()))
            expanded = {}
            for sid, sdoc in src.items():
                nd = {'cls': sdoc['cls'], 'type': sdoc['type'], 'stripped': False,
                      'data': _remap_refs(copy.deepcopy(sdoc['data']), mapping),
                      'source': (sdoc.get('source') or (src_guid, sid)), 'instance': fid}
                expanded[(fid ^ sid) & MASK] = nd
            mod = data.get('m_Modification') or {}
            for m in mod.get('m_Modifications') or []:
                target = m.get('target') or {}
                tid = (fid ^ int(target.get('fileID', 0))) & MASK
                if tid not in expanded:
                    continue
                obj_ref = m.get('objectReference')
                set_property(expanded[tid]['data'], str(m.get('propertyPath')), m.get('value'), obj_ref)
            for removed in (mod.get('m_RemovedComponents') or []) + (mod.get('m_RemovedGameObjects') or []):
                rid = (fid ^ int((removed or {}).get('fileID', 0))) & MASK
                expanded.pop(rid, None)
            # hook the instance root under its parent
            parent = mod.get('m_TransformParent') or {}
            for nid, nd in expanded.items():
                if nd['type'] in ('Transform', 'RectTransform') and int((nd['data'].get('m_Father') or {}).get('fileID', 0)) == 0:
                    nd['data']['m_Father'] = {'fileID': int(parent.get('fileID', 0))}
            result.update(expanded)

        if alias:
            result = {fid: dict(doc, data=_remap_refs(doc['data'], alias)) for fid, doc in result.items()}
        self._resolved[key] = result
        return result


# --------------------------------------------------------------------------- scene graph helpers

class Graph:
    """Convenience queries over a resolved object table."""

    def __init__(self, project, objects):
        self.p = project
        self.o = objects
        self.components = {}
        self.transform_of = {}
        self.children = {}
        for fid, doc in objects.items():
            go = (doc['data'].get('m_GameObject') or {}).get('fileID') if isinstance(doc['data'], dict) else None
            if go:
                go = int(go)
                self.components.setdefault(go, []).append(fid)
                if doc['type'] in ('Transform', 'RectTransform'):
                    self.transform_of[go] = fid
        for fid, doc in objects.items():
            if doc['type'] in ('Transform', 'RectTransform'):
                father = int((doc['data'].get('m_Father') or {}).get('fileID', 0))
                self.children.setdefault(father, []).append(fid)
        # respect the serialized child order where available
        for fid, kids in self.children.items():
            doc = objects.get(fid)
            order = [int((c or {}).get('fileID', 0)) for c in (doc['data'].get('m_Children') or [])] if doc else []
            rank = {c: i for i, c in enumerate(order)}
            kids.sort(key=lambda c: rank.get(c, len(order) + _root_order(objects.get(c))))

    def doc(self, fid):
        return self.o.get(int(fid)) if fid is not None else None

    def go_of(self, comp_id):
        d = self.doc(comp_id)
        return int(d['data']['m_GameObject']['fileID']) if d else None

    def go_name(self, go_id):
        d = self.doc(go_id)
        return d['data'].get('m_Name') if d else None

    def go_active(self, go_id):
        d = self.doc(go_id)
        return bool(int(d['data'].get('m_IsActive', 1) or 0)) if d else False

    def go_tag(self, go_id):
        d = self.doc(go_id)
        return d['data'].get('m_TagString') if d else None

    def scripts(self, name=None):
        out = []
        for fid, doc in self.o.items():
            if doc['type'] != 'MonoBehaviour':
                continue
            sname = self.p.script_name((doc['data'].get('m_Script') or {}).get('guid'))
            if name is None or sname == name:
                out.append((fid, sname, doc['data']))
        return out

    def script_of(self, comp_id):
        d = self.doc(comp_id)
        if not d or d['type'] != 'MonoBehaviour':
            return None
        return self.p.script_name((d['data'].get('m_Script') or {}).get('guid'))

    def components_on(self, go_id):
        return self.components.get(int(go_id), [])

    def transform_children_dfs(self, transform_id):
        out = [transform_id]
        for c in self.children.get(transform_id, []):
            out.extend(self.transform_children_dfs(c))
        return out

    def world_matrix(self, transform_id):
        chain = []
        t = transform_id
        guard = 0
        while t and t in self.o and guard < 64:
            chain.append(self.o[t]['data'])
            t = int((self.o[t]['data'].get('m_Father') or {}).get('fileID', 0))
            guard += 1
        m = _identity()
        for data in reversed(chain):
            m = _mul(m, _trs(data))
        return m

    def world_position(self, transform_id):
        m = self.world_matrix(transform_id)
        return [m[0][3], m[1][3], m[2][3]]

    def world_forward(self, transform_id):
        m = self.world_matrix(transform_id)
        v = [m[0][2], m[1][2], m[2][2]]
        n = math.sqrt(sum(x * x for x in v)) or 1
        return [x / n for x in v]

    def world_scale(self, transform_id):
        m = self.world_matrix(transform_id)
        return [math.sqrt(sum(m[r][c] ** 2 for r in range(3))) for c in range(3)]


def _root_order(doc):
    if not doc:
        return 0
    return int(num(doc['data'].get('m_RootOrder'), 0))


def _identity():
    return [[1.0 if r == c else 0.0 for c in range(4)] for r in range(4)]


def _mul(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def _trs(data):
    p = data.get('m_LocalPosition') or {}
    q = data.get('m_LocalRotation') or {}
    s = data.get('m_LocalScale') or {}
    px, py, pz = num(p.get('x')), num(p.get('y')), num(p.get('z'))
    qx, qy, qz, qw = num(q.get('x')), num(q.get('y')), num(q.get('z')), num(q.get('w'), 1.0)
    sx, sy, sz = num(s.get('x'), 1.0), num(s.get('y'), 1.0), num(s.get('z'), 1.0)
    n = math.sqrt(qx * qx + qy * qy + qz * qz + qw * qw) or 1.0
    qx, qy, qz, qw = qx / n, qy / n, qz / n, qw / n
    r = [
        [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
        [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
        [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)],
    ]
    return [
        [r[0][0] * sx, r[0][1] * sy, r[0][2] * sz, px],
        [r[1][0] * sx, r[1][1] * sy, r[1][2] * sz, py],
        [r[2][0] * sx, r[2][1] * sy, r[2][2] * sz, pz],
        [0, 0, 0, 1],
    ]
