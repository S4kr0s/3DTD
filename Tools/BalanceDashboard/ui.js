/* DOM, table and chart helpers for the balance dashboard. Text always goes in via textContent. */
(function (root) {
  'use strict';
  const SVGNS = 'http://www.w3.org/2000/svg';

  // h('div', {class: 'x', onclick: fn}, 'text', child, [children])
  function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    applyAttrs(el, attrs);
    appendAll(el, children);
    return el;
  }
  function s(tag, attrs, ...children) {
    const el = document.createElementNS(SVGNS, tag);
    applyAttrs(el, attrs, true);
    appendAll(el, children);
    return el;
  }
  function applyAttrs(el, attrs, svg) {
    if (!attrs) return;
    for (const k in attrs) {
      const v = attrs[k];
      if (v == null || v === false) continue;
      if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
      else if (k === 'class') svg ? el.setAttribute('class', v) : (el.className = v);
      else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
      else if (k === 'text') el.textContent = v;
      else if (k === 'dataset') Object.assign(el.dataset, v);
      else if (k === 'value' && !svg) el.value = v;
      else if (k === 'checked' && !svg) el.checked = !!v;
      else el.setAttribute(k, v === true ? '' : v);
    }
  }
  function appendAll(el, children) {
    children.forEach(c => {
      if (c == null || c === false) return;
      if (Array.isArray(c)) appendAll(el, c);
      else if (c instanceof Node) el.appendChild(c);
      else el.appendChild(document.createTextNode(String(c)));
    });
  }
  function clear(el) { while (el.firstChild) el.removeChild(el.firstChild); return el; }

  // ------------------------------------------------------------------ tooltip
  let tipEl = null;
  function tip() {
    if (!tipEl) { tipEl = h('div', { class: 'tooltip', role: 'tooltip' }); document.body.appendChild(tipEl); }
    return tipEl;
  }
  // content: {title, rows: [{key, color, value}], note} or a Node
  function showTip(ev, content) {
    const t = tip();
    clear(t);
    if (content instanceof Node) t.appendChild(content);
    else {
      if (content.title) t.appendChild(h('div', { class: 'tt-title', text: content.title }));
      (content.rows || []).forEach(r => t.appendChild(h('div', { class: 'tt-row' },
        h('span', { class: 'k' }, r.color ? h('span', { class: 'tt-key', style: { background: r.color } }) : null, r.key),
        h('span', { class: 'v', text: r.value }))));
      if (content.note) t.appendChild(h('div', { class: 'muted small', style: { marginTop: '4px' }, text: content.note }));
    }
    t.classList.add('show');
    moveTip(ev);
  }
  function moveTip(ev) {
    const t = tip();
    const pad = 14;
    let x = ev.clientX + pad, y = ev.clientY + pad;
    const r = t.getBoundingClientRect();
    if (x + r.width > window.innerWidth - 8) x = ev.clientX - r.width - pad;
    if (y + r.height > window.innerHeight - 8) y = ev.clientY - r.height - pad;
    t.style.left = Math.max(4, x) + 'px';
    t.style.top = Math.max(4, y) + 'px';
  }
  function hideTip() { if (tipEl) tipEl.classList.remove('show'); }

  // ------------------------------------------------------------------ status label (icon + text, never color alone)
  const ICONS = {
    bad: 'M8 1.5 15 14.5H1z M8 6v4 M8 12v.5',
    error: 'M8 1.5 15 14.5H1z M8 6v4 M8 12v.5',
    warn: 'M8 1.5 15 14.5H1z M8 6v4 M8 12v.5',
    warning: 'M8 1.5 15 14.5H1z M8 6v4 M8 12v.5',
    info: 'M8 1.5a6.5 6.5 0 1 0 0 13a6.5 6.5 0 1 0 0-13z M8 7v4.5 M8 4.5v.5',
    good: 'M2.5 8.5 6 12l7.5-8',
  };
  const STATUS_LABEL = { bad: 'Problem', error: 'Error', warn: 'Warning', warning: 'Warning', info: 'Note', good: 'OK' };
  function status(sev, label) {
    const svg = s('svg', { width: 14, height: 14, viewBox: '0 0 16 16', fill: 'none', stroke: 'currentColor', 'stroke-width': 1.6, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' },
      s('path', { d: ICONS[sev] || ICONS.info }));
    return h('span', { class: 'status ' + sev }, svg, label || STATUS_LABEL[sev] || sev);
  }

  // ------------------------------------------------------------------ table
  /*
   * table({columns: [{key, label, title, value(row), fmt(v,row), render(row) -> Node, n: numeric, heat: 'high'|'low', first}],
   *        rows, sort: {key, dir}, onRowClick, selected(row), id, maxHeight, empty})
   */
  const sortMemory = {};
  function table(opts) {
    const cols = opts.columns;
    const memo = opts.id && sortMemory[opts.id];
    let sortKey = memo ? memo.key : (opts.sort && opts.sort.key);
    let sortDir = memo ? memo.dir : (opts.sort && opts.sort.dir) || 'desc';
    const wrap = h('div', { class: 'table-wrap' + (opts.tall ? ' tall' : '') });
    if (opts.maxHeight) wrap.style.maxHeight = opts.maxHeight;
    const tbl = h('table', { class: 'data' });
    const thead = h('thead');
    const tbody = h('tbody');
    tbl.append(thead, tbody);
    wrap.appendChild(tbl);
    const valueOf = (c, r) => (c.value ? c.value(r) : r[c.key]);

    // heat ranges
    const ranges = {};
    cols.forEach(c => {
      if (!c.heat) return;
      const vals = opts.rows.map(r => valueOf(c, r)).filter(v => typeof v === 'number' && isFinite(v));
      if (!vals.length) return;
      ranges[c.key] = [Math.min(...vals), Math.max(...vals)];
    });

    function renderHead() {
      clear(thead);
      const tr = h('tr');
      cols.forEach(c => {
        const sortable = c.sortable !== false;
        const th = h('th', { class: (c.n ? 'n ' : '') + (c.first ? 'first ' : '') + (sortable ? '' : 'nosort'), title: c.title || null },
          c.label, sortKey === c.key ? h('span', { class: 'arrow', text: sortDir === 'asc' ? '▲' : '▼' }) : null);
        if (sortable) th.addEventListener('click', () => {
          if (sortKey === c.key) sortDir = sortDir === 'asc' ? 'desc' : 'asc';
          else { sortKey = c.key; sortDir = c.n ? 'desc' : 'asc'; }
          if (opts.id) sortMemory[opts.id] = { key: sortKey, dir: sortDir };
          renderHead(); renderBody();
        });
        tr.appendChild(th);
      });
      thead.appendChild(tr);
    }
    function sortedRows() {
      const rows = opts.rows.slice();
      const c = cols.find(x => x.key === sortKey);
      if (!c) return rows;
      const dir = sortDir === 'asc' ? 1 : -1;
      rows.sort((a, b) => {
        let va = c.sortValue ? c.sortValue(a) : valueOf(c, a), vb = c.sortValue ? c.sortValue(b) : valueOf(c, b);
        const na = va == null || (typeof va === 'number' && Number.isNaN(va)), nb = vb == null || (typeof vb === 'number' && Number.isNaN(vb));
        if (na && nb) return 0;
        if (na) return 1;
        if (nb) return -1;
        if (typeof va === 'string' || typeof vb === 'string') return String(va).localeCompare(String(vb), undefined, { numeric: true }) * dir;
        return (va - vb) * dir;
      });
      return rows;
    }
    function renderBody() {
      clear(tbody);
      const rows = sortedRows();
      if (!rows.length) {
        tbody.appendChild(h('tr', null, h('td', { colspan: cols.length, class: 'empty', text: opts.empty || 'No rows match.' })));
        return;
      }
      const frag = document.createDocumentFragment();
      rows.forEach(r => {
        const tr = h('tr', { class: (opts.onRowClick ? 'clickable ' : '') + (opts.selected && opts.selected(r) ? 'selected' : '') });
        if (opts.onRowClick) tr.addEventListener('click', (e) => { if (e.target.closest('a,button,input,select')) return; opts.onRowClick(r, e); });
        cols.forEach(c => {
          const td = h('td', { class: (c.n ? 'n ' : '') + (c.first ? 'first ' : '') + (c.cls || '') });
          if (c.render) {
            const node = c.render(r);
            if (node != null) appendAll(td, [node]);
          } else {
            const v = valueOf(c, r);
            td.textContent = c.fmt ? c.fmt(v, r) : (v == null ? '–' : String(v));
          }
          if (c.heat && ranges[c.key]) {
            const v = valueOf(c, r);
            if (typeof v === 'number' && isFinite(v)) {
              const [lo, hi] = ranges[c.key];
              let t = hi > lo ? (v - lo) / (hi - lo) : 0;
              if (c.heat === 'low') t = 1 - t;
              td.style.background = `rgba(var(--heat-rgb), ${(0.03 + t * 0.22).toFixed(3)})`;
            }
          }
          tr.appendChild(td);
        });
        frag.appendChild(tr);
      });
      tbody.appendChild(frag);
    }
    renderHead();
    renderBody();
    wrap.getRows = sortedRows;
    return wrap;
  }

  function toCSV(columns, rows) {
    const esc = (v) => {
      if (v == null) return '';
      const str = String(v);
      return /[",\n]/.test(str) ? '"' + str.replace(/"/g, '""') + '"' : str;
    };
    const lines = [columns.map(c => esc(c.label)).join(',')];
    rows.forEach(r => lines.push(columns.map(c => {
      let v = c.csv ? c.csv(r) : (c.value ? c.value(r) : r[c.key]);
      if (typeof v === 'number') v = isFinite(v) ? +v.toFixed(6) : (v > 0 ? 'Infinity' : '-Infinity');
      return esc(v);
    }).join(',')));
    return lines.join('\n');
  }
  function download(name, text, type) {
    const blob = new Blob([text], { type: type || 'text/plain' });
    const a = h('a', { href: URL.createObjectURL(blob), download: name });
    document.body.appendChild(a);
    a.click();
    setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
  }

  // ------------------------------------------------------------------ scales & axes
  function niceTicks(lo, hi, count) {
    count = count || 5;
    if (!isFinite(lo) || !isFinite(hi)) return [0, 1];
    if (hi === lo) { hi = lo + 1; }
    const span = hi - lo;
    const step0 = span / count;
    const mag = Math.pow(10, Math.floor(Math.log10(step0)));
    const err = step0 / mag;
    const step = (err >= 7.5 ? 10 : err >= 3.5 ? 5 : err >= 1.5 ? 2 : 1) * mag;
    const t0 = Math.floor(lo / step) * step, t1 = Math.ceil(hi / step) * step;
    const ticks = [];
    for (let t = t0; t <= t1 + step * 0.5; t += step) ticks.push(+t.toFixed(10));
    return ticks;
  }
  function compact(v) {
    if (v == null || !isFinite(v)) return '–';
    const a = Math.abs(v);
    if (a >= 1e6) return (v / 1e6).toFixed(a >= 1e7 ? 0 : 1) + 'M';
    if (a >= 1e4) return (v / 1e3).toFixed(a >= 1e5 ? 0 : 1) + 'K';
    if (a >= 1000) return Math.round(v).toLocaleString('en-US');
    if (a >= 10) return (Math.round(v * 10) / 10).toString();
    if (a >= 1) return (Math.round(v * 100) / 100).toString();
    if (a === 0) return '0';
    return v.toPrecision(2);
  }

  function chartWidth(container, fallback) {
    const w = container && container.clientWidth;
    return Math.max(280, w || fallback || 640);
  }
  function legend(items, kind) {
    return h('div', { class: 'legend' }, items.map(it => h('span', { class: 'item' },
      h('span', { class: kind === 'rect' ? 'rect-key' : 'line-key', style: { background: it.color } }), it.name)));
  }

  /*
   * lineChart(container, {series: [{name, color, values: [{x, y}]}], height, xLabel, yLabel, xFmt, yFmt, yMin, yMax,
   *                       step: bool, markers: bool, xTicks, refs: [{y, label}], endLabels: bool})
   */
  function lineChart(container, o) {
    const W = chartWidth(container, o.width), H = o.height || 260;
    const m = { l: 52, r: o.endLabels ? 110 : 16, t: 10, b: o.xLabel ? 38 : 26 };
    const xs = [], ys = [];
    o.series.forEach(se => se.values.forEach(p => { if (isFinite(p.x)) xs.push(p.x); if (isFinite(p.y)) ys.push(p.y); }));
    if (!xs.length) { container.appendChild(h('div', { class: 'empty', text: 'No data' })); return; }
    let x0 = o.xMin != null ? o.xMin : Math.min(...xs), x1 = o.xMax != null ? o.xMax : Math.max(...xs);
    let y0 = o.yMin != null ? o.yMin : Math.min(0, ...ys), y1 = o.yMax != null ? o.yMax : Math.max(...ys);
    (o.refs || []).forEach(r => { y1 = Math.max(y1, r.y); });
    if (y1 === y0) y1 = y0 + 1;
    const yt = niceTicks(y0, y1, 5);
    y0 = Math.min(y0, yt[0]); y1 = yt[yt.length - 1];
    if (x1 === x0) x1 = x0 + 1;
    const X = (v) => m.l + (v - x0) / (x1 - x0) * (W - m.l - m.r);
    const Y = (v) => H - m.b - (v - y0) / (y1 - y0) * (H - m.t - m.b);
    const yf = o.yFmt || compact, xf = o.xFmt || compact;
    const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': o.ariaLabel || o.yLabel || 'line chart' });
    // grid + y axis
    yt.forEach(t => {
      svg.appendChild(s('line', { class: 'gridline', x1: m.l, x2: W - m.r, y1: Y(t), y2: Y(t) }));
      svg.appendChild(s('text', { x: m.l - 6, y: Y(t) + 3.5, 'text-anchor': 'end', text: yf(t) }));
    });
    const xt = o.xTicks || niceTicks(x0, x1, Math.max(3, Math.floor((W - m.l - m.r) / 90)));
    xt.filter(t => t >= x0 - 1e-9 && t <= x1 + 1e-9).forEach(t => {
      svg.appendChild(s('text', { x: X(t), y: H - m.b + 15, 'text-anchor': 'middle', text: xf(t) }));
    });
    svg.appendChild(s('line', { x1: m.l, x2: W - m.r, y1: Y(y0), y2: Y(y0), stroke: 'var(--axis)' }));
    if (o.xLabel) svg.appendChild(s('text', { x: (m.l + W - m.r) / 2, y: H - 4, 'text-anchor': 'middle', class: 'label-ink', text: o.xLabel }));
    (o.refs || []).forEach(r => {
      svg.appendChild(s('line', { x1: m.l, x2: W - m.r, y1: Y(r.y), y2: Y(r.y), stroke: 'var(--muted)', 'stroke-width': 1 }));
      svg.appendChild(s('text', { x: W - m.r - 4, y: Y(r.y) - 4, 'text-anchor': 'end', class: 'label-ink', text: r.label }));
    });
    // series
    o.series.forEach(se => {
      const pts = se.values.filter(p => isFinite(p.x) && isFinite(p.y));
      if (!pts.length) return;
      let d = '';
      pts.forEach((p, i) => {
        if (o.step && i > 0) d += `L${X(p.x)},${Y(pts[i - 1].y)}`;
        d += (i ? 'L' : 'M') + X(p.x) + ',' + Y(p.y);
      });
      if (se.area) {
        svg.appendChild(s('path', { d: d + `L${X(pts[pts.length - 1].x)},${Y(y0)}L${X(pts[0].x)},${Y(y0)}Z`, fill: se.color, opacity: 0.1 }));
      }
      svg.appendChild(s('path', { d, fill: 'none', stroke: se.color, 'stroke-width': 2, 'stroke-linejoin': 'round', 'stroke-linecap': 'round', 'stroke-dasharray': se.dash || null }));
      if (o.markers || pts.length === 1) pts.forEach(p => svg.appendChild(s('circle', { cx: X(p.x), cy: Y(p.y), r: 4, fill: se.color, stroke: 'var(--surface)', 'stroke-width': 2 })));
      if (o.endLabels) {
        const last = pts[pts.length - 1];
        svg.appendChild(s('circle', { cx: X(last.x), cy: Y(last.y), r: 4, fill: se.color, stroke: 'var(--surface)', 'stroke-width': 2 }));
        se._endY = Y(last.y);
      }
    });
    if (o.endLabels) {
      // nudge-free: only label when they don't collide, otherwise rely on legend + tooltip
      const labs = o.series.filter(se => se._endY != null).map(se => ({ se, y: se._endY })).sort((a, b) => a.y - b.y);
      let lastY = -Infinity;
      labs.forEach(l => {
        if (l.y - lastY < 13) return;
        lastY = l.y;
        svg.appendChild(s('text', { x: W - m.r + 8, y: l.y + 4, class: 'label-ink', text: l.se.name }));
      });
    }
    // crosshair + tooltip
    const allX = Array.from(new Set(xs)).sort((a, b) => a - b);
    const cross = s('line', { class: 'crosshair', y1: m.t, y2: H - m.b, x1: -10, x2: -10, visibility: 'hidden' });
    svg.appendChild(cross);
    const overlay = s('rect', { x: m.l, y: m.t, width: W - m.l - m.r, height: H - m.t - m.b, fill: 'transparent' });
    svg.appendChild(overlay);
    const nearestX = (px) => {
      const vx = x0 + (px - m.l) / (W - m.l - m.r) * (x1 - x0);
      let lo = 0, hi = allX.length - 1;
      while (lo < hi) { const md = (lo + hi) >> 1; if (allX[md] < vx) lo = md + 1; else hi = md; }
      if (lo > 0 && Math.abs(allX[lo - 1] - vx) < Math.abs(allX[lo] - vx)) lo--;
      return allX[lo];
    };
    overlay.addEventListener('pointermove', (ev) => {
      const r = svg.getBoundingClientRect();
      const px = (ev.clientX - r.left) * (W / r.width);
      const xv = nearestX(px);
      cross.setAttribute('x1', X(xv)); cross.setAttribute('x2', X(xv)); cross.setAttribute('visibility', 'visible');
      const rows = o.series.map(se => {
        let p = se.values.find(q => q.x === xv);
        if (!p && o.step) { const prev = se.values.filter(q => q.x <= xv); p = prev[prev.length - 1]; }
        return p ? { key: se.name, color: se.color, value: yf(p.y), y: p.y } : null;
      }).filter(Boolean).sort((a, b) => b.y - a.y);
      showTip(ev, { title: (o.xTipLabel || '') + xf(xv), rows });
    });
    overlay.addEventListener('pointerleave', () => { cross.setAttribute('visibility', 'hidden'); hideTip(); });
    const box = h('div', { class: 'chart' });
    if (o.series.length >= 2 && o.legend !== false) box.appendChild(legend(o.series));
    if (o.yLabel) box.appendChild(h('div', { class: 'muted small', text: o.yLabel }));
    box.appendChild(svg);
    container.appendChild(box);
    return box;
  }

  /*
   * barChart(container, {items: [{label, value, color, tip: {...}}], valueFmt, max, height per bar})
   * Horizontal bars, value at the tip, label on the left. One measure per chart.
   */
  function barChart(container, o) {
    const W = chartWidth(container, o.width);
    const bh = o.barHeight || 18, gap = 8;
    const labW = Math.min(o.labelWidth || 200, W * 0.42);
    const valW = 64;
    const items = o.items;
    const H = items.length * (bh + gap) + 8;
    const vals = items.map(i => i.value).filter(isFinite);
    const lo = Math.min(0, ...vals), hi = Math.max(o.max || 0, ...vals, 1e-9);
    const X = (v) => labW + (v - lo) / (hi - lo) * (W - labW - valW);
    const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': o.ariaLabel || 'bar chart' });
    const vf = o.valueFmt || compact;
    svg.appendChild(s('line', { x1: X(0), x2: X(0), y1: 0, y2: H - 4, stroke: 'var(--axis)' }));
    items.forEach((it, i) => {
      const y = 4 + i * (bh + gap);
      const v = isFinite(it.value) ? it.value : 0;
      const xa = X(Math.min(0, v)), xb = X(Math.max(0, v));
      const w = Math.max(v === 0 ? 0 : 1.5, xb - xa);
      svg.appendChild(s('text', { x: labW - 8, y: y + bh / 2 + 4, 'text-anchor': 'end', class: 'label-ink', text: truncate(it.label, Math.floor(labW / 6.4)) }));
      const r = Math.min(4, w / 2, bh / 2);
      const pos = v >= 0;
      const path = pos
        ? `M${xa},${y}H${xa + w - r}Q${xa + w},${y} ${xa + w},${y + r}V${y + bh - r}Q${xa + w},${y + bh} ${xa + w - r},${y + bh}H${xa}Z`
        : `M${xb},${y}H${xb - w + r}Q${xb - w},${y} ${xb - w},${y + r}V${y + bh - r}Q${xb - w},${y + bh} ${xb - w + r},${y + bh}H${xb}Z`;
      const mark = s('path', { class: 'mark', d: path, fill: it.color || 'var(--s1)' });
      svg.appendChild(mark);
      // negative values are labelled just right of the zero line so they never run into the category label
      svg.appendChild(s('text', { x: xb + 6, y: y + bh / 2 + 4, 'text-anchor': 'start', class: 'value-ink', text: isFinite(it.value) ? vf(it.value) : '–' }));
      const hit = s('rect', { x: 0, y: y - gap / 2, width: W, height: bh + gap, fill: 'transparent', tabindex: 0 });
      const show = (ev) => { mark.classList.add('hover'); showTip(ev, it.tip || { title: it.label, rows: [{ key: o.valueLabel || 'Value', value: isFinite(it.value) ? vf(it.value) : '–', color: it.color }] }); };
      hit.addEventListener('pointermove', show);
      hit.addEventListener('focus', (ev) => { const r = hit.getBoundingClientRect(); show({ clientX: r.left + r.width / 2, clientY: r.top }); });
      hit.addEventListener('pointerleave', () => { mark.classList.remove('hover'); hideTip(); });
      hit.addEventListener('blur', () => { mark.classList.remove('hover'); hideTip(); });
      if (it.onClick) { hit.style.cursor = 'pointer'; hit.addEventListener('click', it.onClick); }
      svg.appendChild(hit);
    });
    const box = h('div', { class: 'chart' });
    if (o.legendItems) box.appendChild(legend(o.legendItems, 'rect'));
    box.appendChild(svg);
    container.appendChild(box);
    return box;
  }
  function truncate(t, n) { t = String(t); return t.length > n ? t.slice(0, Math.max(1, n - 1)) + '…' : t; }

  /*
   * columnChart: vertical columns over an ordinal x (e.g. rounds). {items: [{x, value, color, tip}], xFmt, yFmt, height}
   */
  function columnChart(container, o) {
    const W = chartWidth(container, o.width), H = o.height || 220;
    const m = { l: 52, r: 12, t: 10, b: o.xLabel ? 38 : 26 };
    const n = o.items.length;
    const vals = o.items.map(i => i.value).filter(isFinite);
    const yt = niceTicks(Math.min(0, ...vals), Math.max(1e-9, ...vals), 5);
    const y0 = yt[0], y1 = yt[yt.length - 1];
    const band = (W - m.l - m.r) / Math.max(1, n);
    const bw = Math.max(1, Math.min(24, band - 2));
    const Y = (v) => H - m.b - (v - y0) / (y1 - y0) * (H - m.t - m.b);
    const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': o.ariaLabel || 'column chart' });
    const yf = o.yFmt || compact;
    yt.forEach(t => {
      svg.appendChild(s('line', { class: 'gridline', x1: m.l, x2: W - m.r, y1: Y(t), y2: Y(t) }));
      svg.appendChild(s('text', { x: m.l - 6, y: Y(t) + 3.5, 'text-anchor': 'end', text: yf(t) }));
    });
    const every = Math.ceil(n / Math.max(1, Math.floor((W - m.l - m.r) / 34)));
    o.items.forEach((it, i) => {
      const cx = m.l + band * (i + 0.5);
      const v = isFinite(it.value) ? it.value : 0;
      const ya = Y(Math.max(0, v)), yb = Y(Math.min(0, v));
      const hgt = Math.max(v === 0 ? 0 : 1, yb - ya);
      const r = Math.min(4, bw / 2, hgt);
      const x = cx - bw / 2;
      const d = `M${x},${ya + hgt}V${ya + r}Q${x},${ya} ${x + r},${ya}H${x + bw - r}Q${x + bw},${ya} ${x + bw},${ya + r}V${ya + hgt}Z`;
      const mark = s('path', { class: 'mark', d, fill: it.color || 'var(--s1)' });
      svg.appendChild(mark);
      if (i % every === 0) svg.appendChild(s('text', { x: cx, y: H - m.b + 15, 'text-anchor': 'middle', text: o.xFmt ? o.xFmt(it.x) : it.x }));
      const hit = s('rect', { x: cx - band / 2, y: m.t, width: band, height: H - m.t - m.b, fill: 'transparent' });
      hit.addEventListener('pointermove', (ev) => { mark.classList.add('hover'); showTip(ev, it.tip || { title: String(it.x), rows: [{ key: o.valueLabel || 'Value', value: yf(it.value), color: it.color }] }); });
      hit.addEventListener('pointerleave', () => { mark.classList.remove('hover'); hideTip(); });
      if (it.onClick) { hit.style.cursor = 'pointer'; hit.addEventListener('click', it.onClick); }
      svg.appendChild(hit);
    });
    svg.appendChild(s('line', { x1: m.l, x2: W - m.r, y1: Y(0), y2: Y(0), stroke: 'var(--axis)' }));
    if (o.xLabel) svg.appendChild(s('text', { x: (m.l + W - m.r) / 2, y: H - 4, 'text-anchor': 'middle', class: 'label-ink', text: o.xLabel }));
    const box = h('div', { class: 'chart' });
    if (o.legendItems) box.appendChild(legend(o.legendItems, 'rect'));
    if (o.yLabel) box.appendChild(h('div', { class: 'muted small', text: o.yLabel }));
    box.appendChild(svg);
    container.appendChild(box);
    return box;
  }

  /*
   * heatmap(container, {rows: [{key, label}], cols: [{key, label}], value(r, c), fmt, tip(r, c, v), max, cell})
   * Sequential blue ramp; empty cells stay on the surface.
   */
  function heatmap(container, o) {
    const W = chartWidth(container, o.width);
    const labW = o.labelWidth || 130;
    const nC = o.cols.length, nR = o.rows.length;
    const cw = Math.max(4, Math.min(o.cell || 26, (W - labW - 8) / nC));
    const ch = o.cellHeight || Math.max(10, Math.min(22, cw));
    const H = nR * ch + 30;
    const svg = s('svg', { viewBox: `0 0 ${labW + nC * cw + 8} ${H}`, role: 'img', 'aria-label': o.ariaLabel || 'heatmap' });
    let max = o.max;
    if (max == null) { max = 0; o.rows.forEach(r => o.cols.forEach(c => { const v = o.value(r, c); if (isFinite(v)) max = Math.max(max, v); })); }
    const ramp = ['var(--seq-1)', 'var(--seq-2)', 'var(--seq-3)', 'var(--seq-4)', 'var(--seq-5)', 'var(--seq-6)', 'var(--seq-7)'];
    const colorOf = (v) => {
      if (o.color) return o.color(v);
      if (!v || !isFinite(v) || v <= 0) return null;
      const t = o.log ? Math.log(1 + v) / Math.log(1 + max) : v / max;
      return ramp[Math.min(ramp.length - 1, Math.floor(t * ramp.length * 0.999))];
    };
    o.rows.forEach((r, i) => {
      svg.appendChild(s('text', { x: labW - 6, y: i * ch + ch / 2 + 4, 'text-anchor': 'end', class: 'label-ink', text: truncate(r.label, Math.floor(labW / 6.2)) }));
      o.cols.forEach((c, j) => {
        const v = o.value(r, c);
        const fill = colorOf(v);
        const rect = s('rect', { x: labW + j * cw + 1, y: i * ch + 1, width: Math.max(1, cw - 2), height: Math.max(1, ch - 2), rx: 2, fill: fill || 'var(--surface-2)' });
        rect.addEventListener('pointermove', (ev) => { rect.setAttribute('stroke', 'var(--text)'); showTip(ev, o.tip ? o.tip(r, c, v) : { title: r.label + ' · ' + c.label, rows: [{ key: 'Value', value: (o.fmt || compact)(v) }] }); });
        rect.addEventListener('pointerleave', () => { rect.removeAttribute('stroke'); hideTip(); });
        svg.appendChild(rect);
      });
    });
    const every = Math.ceil(nC / Math.max(1, Math.floor(nC * cw / 30)));
    o.cols.forEach((c, j) => { if (j % every === 0) svg.appendChild(s('text', { x: labW + j * cw + cw / 2, y: nR * ch + 16, 'text-anchor': 'middle', text: c.label })); });
    const box = h('div', { class: 'chart' });
    if (o.scaleNote) box.appendChild(h('div', { class: 'legend' }, h('span', { class: 'item' }, ramp.map(c => h('span', { class: 'rect-key', style: { background: c } }))), o.scaleNote));
    box.appendChild(svg);
    container.appendChild(box);
    return box;
  }

  /*
   * timeline(container, {lanes: [{name, color, times, reloads}], seconds})
   */
  function timeline(container, o) {
    const W = chartWidth(container, o.width);
    const laneH = 30, m = { l: o.labelWidth || 120, r: 12, t: 4, b: 24 };
    const H = m.t + o.lanes.length * laneH + m.b;
    const X = (t) => m.l + t / o.seconds * (W - m.l - m.r);
    const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': 'fire timeline' });
    niceTicks(0, o.seconds, 8).filter(t => t <= o.seconds).forEach(t => {
      svg.appendChild(s('line', { class: 'gridline', x1: X(t), x2: X(t), y1: m.t, y2: H - m.b }));
      svg.appendChild(s('text', { x: X(t), y: H - 8, 'text-anchor': 'middle', text: t + 's' }));
    });
    o.lanes.forEach((ln, i) => {
      const y = m.t + i * laneH;
      svg.appendChild(s('text', { x: m.l - 8, y: y + laneH / 2 + 4, 'text-anchor': 'end', class: 'label-ink', text: truncate(ln.name, 18) }));
      (ln.reloads || []).forEach(([a, b]) => svg.appendChild(s('rect', { x: X(a), y: y + 8, width: Math.max(1, X(b) - X(a)), height: laneH - 16, rx: 3, fill: ln.color, opacity: 0.12 })));
      const dense = ln.times.length > (W - m.l) / 3;
      if (dense) {
        // too many shots to draw individually: draw a density band
        svg.appendChild(s('rect', { x: X(ln.times[0] || 0), y: y + 6, width: Math.max(1, X(ln.times[ln.times.length - 1] || 0) - X(ln.times[0] || 0)), height: laneH - 12, rx: 3, fill: ln.color, opacity: 0.55 }));
      } else {
        ln.times.forEach(t => svg.appendChild(s('line', { x1: X(t), x2: X(t), y1: y + 6, y2: y + laneH - 6, stroke: ln.color, 'stroke-width': 2, 'stroke-linecap': 'round' })));
      }
      const hit = s('rect', { x: m.l, y, width: W - m.l - m.r, height: laneH, fill: 'transparent' });
      hit.addEventListener('pointermove', (ev) => showTip(ev, { title: ln.name, rows: [
        { key: 'Volleys in ' + o.seconds + ' s', value: String(ln.times.length), color: ln.color },
        { key: 'First volley', value: ln.times.length ? ln.times[0].toFixed(2) + ' s' : 'never' },
        { key: 'Reload windows', value: String((ln.reloads || []).length) }] }));
      hit.addEventListener('pointerleave', hideTip);
      svg.appendChild(hit);
    });
    const box = h('div', { class: 'chart' }, svg);
    container.appendChild(box);
    return box;
  }

  /*
   * smallMultiples(container, {panels: [{title, color, points: [{x, y, label}]}], xLabel, yLabel, xFmt, yFmt, shared: bool})
   * Scatter per panel (one series each). Shared axes so panels compare.
   */
  function smallMultiples(container, o) {
    const grid = h('div', { class: 'grid', style: { gridTemplateColumns: `repeat(auto-fill, minmax(${o.minWidth || 260}px, 1fr))` } });
    container.appendChild(grid);
    const all = o.panels.flatMap(p => p.points);
    const xs = all.map(p => p.x).filter(isFinite), ys = all.map(p => p.y).filter(isFinite);
    const xMax = Math.max(1e-9, ...xs), yMax = Math.max(1e-9, ...ys);
    o.panels.forEach(p => {
      const pxMax = o.shared ? xMax : Math.max(1e-9, ...p.points.map(q => q.x).filter(isFinite));
      const pyMax = o.shared ? yMax : Math.max(1e-9, ...p.points.map(q => q.y).filter(isFinite));
      const cell = h('div', { class: 'card', style: { padding: '10px 12px' } }, h('h4', { style: { display: 'flex', alignItems: 'center', gap: '6px' } }, h('span', { class: 'key-dot', style: { background: p.color } }), p.title));
      grid.appendChild(cell);
      const W = 300, H = 180, m = { l: 40, r: 10, t: 6, b: 30 };
      const xt = niceTicks(0, pxMax, 3), yt = niceTicks(0, pyMax, 4);
      const X = (v) => m.l + v / xt[xt.length - 1] * (W - m.l - m.r);
      const Y = (v) => H - m.b - v / yt[yt.length - 1] * (H - m.t - m.b);
      const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': p.title });
      yt.forEach(t => { svg.appendChild(s('line', { class: 'gridline', x1: m.l, x2: W - m.r, y1: Y(t), y2: Y(t) })); svg.appendChild(s('text', { x: m.l - 5, y: Y(t) + 3.5, 'text-anchor': 'end', text: (o.yFmt || compact)(t) })); });
      xt.forEach(t => svg.appendChild(s('text', { x: X(t), y: H - m.b + 14, 'text-anchor': 'middle', text: (o.xFmt || compact)(t) })));
      if (o.xLabel) svg.appendChild(s('text', { x: (m.l + W - m.r) / 2, y: H - 2, 'text-anchor': 'middle', class: 'label-ink', text: o.xLabel }));
      p.points.forEach(pt => {
        if (!isFinite(pt.x) || !isFinite(pt.y)) return;
        const c = s('circle', { cx: X(pt.x), cy: Y(pt.y), r: 4, fill: p.color, stroke: 'var(--surface)', 'stroke-width': 2, opacity: 0.9 });
        svg.appendChild(c);
        const hit = s('circle', { cx: X(pt.x), cy: Y(pt.y), r: 10, fill: 'transparent' });
        hit.addEventListener('pointermove', (ev) => { c.setAttribute('r', 6); showTip(ev, pt.tip || { title: pt.label, rows: [{ key: o.xLabel || 'x', value: (o.xFmt || compact)(pt.x) }, { key: o.yLabel || 'y', value: (o.yFmt || compact)(pt.y), color: p.color }] }); });
        hit.addEventListener('pointerleave', () => { c.setAttribute('r', 4); hideTip(); });
        if (pt.onClick) { hit.style.cursor = 'pointer'; hit.addEventListener('click', pt.onClick); }
        svg.appendChild(hit);
      });
      if (o.yLabel) cell.appendChild(h('div', { class: 'muted small', text: o.yLabel }));
      cell.appendChild(h('div', { class: 'chart' }, svg));
    });
    return grid;
  }

  /*
   * mapPlot(container, {lanes: [[x,y,z]...], anchors: [{pos, value, label, blocked, selected}], blocks, end, spawn, valueFmt, view: 'top'|'side'})
   */
  function mapPlot(container, o) {
    const W = chartWidth(container, o.width);
    const proj = o.view === 'side' ? (p) => [p[0], -p[1]] : (p) => [p[0], p[2]];
    const pts = [];
    o.lanes.forEach(l => l.forEach(p => pts.push(proj(p))));
    o.anchors.forEach(a => pts.push(proj(a.pos)));
    (o.blocks || []).forEach(b => pts.push(proj(b)));
    if (!pts.length) { container.appendChild(h('div', { class: 'empty', text: 'No geometry' })); return; }
    const minX = Math.min(...pts.map(p => p[0])) - 1.5, maxX = Math.max(...pts.map(p => p[0])) + 1.5;
    const minY = Math.min(...pts.map(p => p[1])) - 1.5, maxY = Math.max(...pts.map(p => p[1])) + 1.5;
    const aspect = (maxY - minY) / (maxX - minX);
    const H = Math.max(200, Math.min(620, W * aspect));
    const sc = Math.min((W - 20) / (maxX - minX), (H - 20) / (maxY - minY));
    const ox = (W - sc * (maxX - minX)) / 2, oy = (H - sc * (maxY - minY)) / 2;
    const P = (p) => { const q = proj(p); return [ox + (q[0] - minX) * sc, oy + (q[1] - minY) * sc]; };
    const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': 'level map' });
    (o.blocks || []).forEach(b => { const [x, y] = P(b); svg.appendChild(s('rect', { x: x - sc / 2, y: y - sc / 2, width: sc, height: sc, rx: 2, fill: 'var(--surface-3)', stroke: 'var(--axis)' })); });
    const laneColors = ['var(--s1)', 'var(--s2)', 'var(--s3)', 'var(--s4)'];
    o.lanes.forEach((l, i) => {
      const d = l.map((p, k) => (k ? 'L' : 'M') + P(p).join(',')).join('');
      svg.appendChild(s('path', { d, fill: 'none', stroke: laneColors[i % 4], 'stroke-width': Math.max(2, sc * 0.3), 'stroke-opacity': 0.22, 'stroke-linejoin': 'round', 'stroke-linecap': 'round' }));
      svg.appendChild(s('path', { d, fill: 'none', stroke: laneColors[i % 4], 'stroke-width': 2, 'stroke-linejoin': 'round' }));
      const [sx, sy] = P(l[0]);
      svg.appendChild(s('circle', { cx: sx, cy: sy, r: 5, fill: laneColors[i % 4], stroke: 'var(--surface)', 'stroke-width': 2 }));
      svg.appendChild(s('text', { x: sx + 8, y: sy - 6, class: 'label-ink', text: 'Spawn' + (o.lanes.length > 1 ? ' ' + (i + 1) : '') }));
    });
    (o.end || []).forEach(e => { const [x, y] = P(e.pos); svg.appendChild(s('rect', { x: x - 5, y: y - 5, width: 10, height: 10, fill: 'var(--critical)', rx: 2 })); svg.appendChild(s('text', { x: x + 8, y: y + 4, class: 'label-ink', text: 'End' })); });
    const vals = o.anchors.map(a => a.value).filter(v => isFinite(v));
    const vmax = Math.max(1e-9, ...vals);
    const ramp = ['var(--seq-2)', 'var(--seq-3)', 'var(--seq-4)', 'var(--seq-5)', 'var(--seq-6)', 'var(--seq-7)'];
    o.anchors.slice().sort((a, b) => (a.value || 0) - (b.value || 0)).forEach(a => {
      const [x, y] = P(a.pos);
      const v = a.value || 0;
      const fill = a.blocked ? 'var(--surface-3)' : v <= 0 ? 'var(--surface)' : ramp[Math.min(ramp.length - 1, Math.floor(v / vmax * ramp.length * 0.999))];
      const c = s('circle', { cx: x, cy: y, r: a.selected ? 7 : 5, fill, stroke: a.selected ? 'var(--text)' : 'var(--axis)', 'stroke-width': a.selected ? 2 : 1 });
      svg.appendChild(c);
      const hit = s('circle', { cx: x, cy: y, r: 11, fill: 'transparent' });
      hit.addEventListener('pointermove', (ev) => showTip(ev, a.tip || { title: a.label, rows: [{ key: o.valueLabel || 'Value', value: (o.valueFmt || compact)(v) }] }));
      hit.addEventListener('pointerleave', hideTip);
      svg.appendChild(hit);
    });
    const box = h('div', { class: 'chart' });
    box.appendChild(h('div', { class: 'legend' },
      h('span', { class: 'item' }, h('span', { class: 'line-key', style: { background: 'var(--s1)' } }), 'Enemy path'),
      h('span', { class: 'item' }, ramp.map(c => h('span', { class: 'rect-key', style: { background: c } })), ' Anchor: ' + (o.valueLabel || 'value') + ' (darker = more)'),
      h('span', { class: 'item' }, h('span', { class: 'rect-key', style: { background: 'var(--surface-3)', border: '1px solid var(--axis)' } }), 'Building block'),
      h('span', { class: 'item' }, h('span', { class: 'rect-key', style: { background: 'var(--critical)' } }), 'End')));
    box.appendChild(svg);
    container.appendChild(box);
    return box;
  }

  root.UI = { h, s, clear, status, table, toCSV, download, lineChart, barChart, columnChart, heatmap, timeline, smallMultiples, mapPlot,
              showTip, hideTip, moveTip, compact, legend, niceTicks };
})(window);
