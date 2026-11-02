// Grafy pre Register PnD a Osvedčenia (čisté HTML/SVG, bez knižníc).
import { state } from '../core/state.js';

export var CAT = [
  { key: 'A1_A3', label: 'A1/A3', color: '#14a085' },
  { key: 'A2', label: 'A2', color: '#e39b2d' },
  { key: 'STS1', label: 'STS1', color: '#5b5fd6' },
  { key: 'STS2', label: 'STS2', color: '#9b4fd8' }
];
var NONE_COLOR = '#c3ccd5';

function esc(s) {
  return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}
export function normCat(raw) {
  var c = String(raw || '').toUpperCase().replace(/[-\/\s]/g, '_');
  if (c === 'A1_A3' || c === 'A1A3' || c === 'A1' || c === 'A3') return 'A1_A3';
  if (c === 'A2') return 'A2';
  if (c.indexOf('STS2') === 0 || c === 'STS_2' || c === 'STS02' || c === 'STS_02') return 'STS2';
  if (c.indexOf('STS') === 0) return 'STS1';
  return '';
}
/** Najvyššia kategória pilota (STS2 > STS1 > A2 > A1/A3), alebo '' bez osvedčenia. */
export function topCat(p) {
  var have = {};
  (p.certificateCodes || []).forEach(function (c) { var n = normCat(c); if (n) have[n] = 1; });
  for (var i = CAT.length - 1; i >= 0; i--) if (have[CAT[i].key]) return CAT[i].key;
  return '';
}
function isSusp(c) { return !!(c.suspendedUntil && new Date(c.suspendedUntil) > new Date()); }

/* ------------------------------------------------------------ stavebné prvky */

function donut(segs, centerNum, centerLbl) {
  var total = segs.reduce(function (t, s) { return t + s.value; }, 0);
  var R = 52, C = 2 * Math.PI * R, off = 0;
  var arcs = segs.filter(function (s) { return s.value > 0; }).map(function (s, i, arr) {
    var len = s.value / (total || 1) * C, gap = arr.length > 1 ? Math.min(2, len * 0.3) : 0;
    var a = '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="' + s.color + '" stroke-width="18" stroke-dasharray="' +
      Math.max(0, len - gap).toFixed(2) + ' ' + (C - Math.max(0, len - gap)).toFixed(2) + '" stroke-dashoffset="' + (-off).toFixed(2) + '">' +
      '<title>' + esc(s.label) + ': ' + s.value + ' (' + Math.round(s.value / (total || 1) * 100) + ' %)</title></circle>';
    off += len; return a;
  }).join('');
  return '<div class="pc-donut"><svg class="fd-donut" viewBox="0 0 140 140" role="img">' +
    '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="#eef2f6" stroke-width="18"/><g transform="rotate(-90 70 70)">' + arcs + '</g>' +
    '<text x="70" y="67" text-anchor="middle" class="fd-donut-num">' + esc(centerNum) + '</text>' +
    '<text x="70" y="84" text-anchor="middle" class="fd-donut-lbl">' + esc(centerLbl) + '</text></svg>' +
    '<ul class="fd-legend">' + segs.map(function (s) {
      return '<li><span class="fd-dot" style="background:' + s.color + '"></span><span class="fd-legend-name">' + esc(s.label) + '</span>' +
        '<b>' + s.value + '</b><small class="pc-pct">' + Math.round(s.value / (total || 1) * 100) + ' %</small></li>';
    }).join('') + '</ul></div>';
}

/** Skladané stĺpce. cols: [{ label, title, parts: [{ value, color, label }] }] */
function columns(cols, unit) {
  var max = cols.reduce(function (m, c) { return Math.max(m, c.parts.reduce(function (t, p) { return t + p.value; }, 0)); }, 0);
  if (max <= 0) return '<p class="pm-nodata">Žiadne údaje.</p>';
  return '<div class="fd-cols pc-cols" style="grid-template-columns:repeat(' + cols.length + ',minmax(0,1fr))">' + cols.map(function (c) {
    var tot = c.parts.reduce(function (t, p) { return t + p.value; }, 0);
    return '<div class="fd-col" title="' + esc(c.title || c.label) + ': ' + tot + (unit || '') + '">' +
      '<span class="fd-col-val">' + (tot || '') + '</span><div class="fd-col-bar">' +
      c.parts.map(function (p) {
        return p.value ? '<span style="height:' + (p.value / max * 100).toFixed(2) + '%;background:' + p.color + '" title="' + esc(p.label) + ': ' + p.value + '"></span>' : '';
      }).join('') + '</div><span class="fd-col-lbl">' + esc(c.label) + '</span></div>';
  }).join('') + '</div>';
}

/** Vodorovné skladané pruhy. rows: [{ label, sub, parts: [{ value, color, label }] }] */
function stackBars(rows, max) {
  max = max || rows.reduce(function (m, r) { return Math.max(m, r.parts.reduce(function (t, p) { return t + p.value; }, 0)); }, 0) || 1;
  return '<div class="pc-bars">' + rows.map(function (r) {
    var tot = r.parts.reduce(function (t, p) { return t + p.value; }, 0);
    return '<div class="pc-bar"><span class="pc-bar-lbl" title="' + esc(r.label) + '">' + esc(r.label) + (r.sub ? '<small>' + esc(r.sub) + '</small>' : '') + '</span>' +
      '<span class="pc-bar-track">' + r.parts.map(function (p) {
        return p.value ? '<span style="width:' + (p.value / max * 100).toFixed(2) + '%;background:' + p.color + '" title="' + esc(p.label) + ': ' + p.value + '"></span>' : '';
      }).join('') + '</span><b class="pc-bar-val">' + tot + '</b></div>';
  }).join('') + '</div>';
}

function kpi(value, label, sub, cls) {
  return '<div class="pc-kpi' + (cls ? ' ' + cls : '') + '"><b>' + esc(value) + '</b><span>' + esc(label) + '</span>' + (sub ? '<small>' + esc(sub) + '</small>' : '') + '</div>';
}
function legend(extraNone) {
  return '<div class="pc-legend">' + CAT.map(function (c) { return '<span><i style="background:' + c.color + '"></i>' + c.label + '</span>'; }).join('') +
    (extraNone ? '<span><i style="background:' + NONE_COLOR + '"></i>Bez osvedčenia</span>' : '') + '</div>';
}

/** Zbaliteľný obal sekcie s grafmi (stav sa pamätá počas relácie). */
function wrap(id, title, body) {
  state.chartsOpen = state.chartsOpen || {};
  var open = state.chartsOpen[id] !== false;
  return '<section class="pc-wrap' + (open ? ' is-open' : '') + '" data-pc="' + id + '">' +
    '<button type="button" class="pc-head" data-pc-toggle="' + id + '" aria-expanded="' + open + '">' +
    '<span class="pc-head-ico"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3v18h18"/><path d="M7 15v2M11 11v6M15 7v10M19 12v5"/></svg></span>' +
    '<b>' + esc(title) + '</b><span class="pc-head-hint">' + (open ? 'Skryť grafy' : 'Zobraziť grafy') + '</span>' +
    '<svg class="pc-chev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg></button>' +
    '<div class="pc-body"' + (open ? '' : ' hidden') + '>' + body + '</div></section>';
}
export function bindChartToggles(root) {
  (root || document).querySelectorAll('[data-pc-toggle]').forEach(function (b) {
    b.onclick = function () {
      var id = b.dataset.pcToggle, sec = b.closest('.pc-wrap'), body = sec.querySelector('.pc-body');
      state.chartsOpen = state.chartsOpen || {};
      var open = body.hidden;
      body.hidden = !open;
      state.chartsOpen[id] = open;
      sec.classList.toggle('is-open', open);
      b.setAttribute('aria-expanded', String(open));
      b.querySelector('.pc-head-hint').textContent = open ? 'Skryť grafy' : 'Zobraziť grafy';
    };
  });
}

function monthsBack(n) {
  var now = new Date(), out = [];
  for (var i = n - 1; i >= 0; i--) {
    var d = new Date(now.getFullYear(), now.getMonth() - i, 1);
    out.push({ key: d.getFullYear() + '-' + d.getMonth(), label: d.toLocaleDateString('sk-SK', { month: 'short' }).replace('.', ''), title: d.toLocaleDateString('sk-SK', { month: 'long', year: 'numeric' }) });
  }
  return out;
}
function monthKey(iso) { var d = new Date(iso); return d.getFullYear() + '-' + d.getMonth(); }

/* ------------------------------------------------------------ Register PnD */

/**
 * pilots: aktuálne zobrazení piloti (rešpektuje filtre zložky/útvaru)
 * ctx.branchOf(orgUnitId) → { key, label } ; ctx.branchFilter (aktívna zložka) ; ctx.unitFilter
 */
export function registerChartsHtml(pilots, ctx) {
  var total = pilots.length;
  if (!total) return '';
  var byTop = { '': 0 };
  CAT.forEach(function (c) { byTop[c.key] = 0; });
  pilots.forEach(function (p) { byTop[topCat(p)]++; });
  var withCert = total - byTop[''];
  var now = Date.now();
  var active90 = pilots.filter(function (p) { return p.lastFlightAt && now - new Date(p.lastFlightAt).getTime() < 90 * 86400000; }).length;
  var thisMonth = pilots.filter(function (p) { return p.createdAt && monthKey(p.createdAt) === monthKey(new Date()); }).length;
  var noOp = pilots.filter(function (p) { return !p.operatorAccountId; }).length;

  // Kategórie — prstenec podľa najvyššieho osvedčenia
  var segs = CAT.map(function (c) { return { label: c.label + (c.key === 'A1_A3' ? ' (len)' : ''), value: byTop[c.key], color: c.color }; })
    .concat([{ label: 'Bez osvedčenia', value: byTop[''], color: NONE_COLOR }]);

  // Rozdelenie podľa zložky (ak nie je filter zložky), inak podľa útvarov
  var groupBy = ctx.branchFilter || ctx.unitFilter ? 'unit' : 'branch';
  var groups = {};
  pilots.forEach(function (p) {
    var g = groupBy === 'branch' ? (ctx.branchOf(p.orgUnitId) || { key: '-', label: 'Ostatné' }) : { key: p.orgUnitId, label: p.orgUnitName || '—' };
    var e = groups[g.key] = groups[g.key] || { label: g.label, by: {} };
    var t = topCat(p);
    e.by[t] = (e.by[t] || 0) + 1;
  });
  var rows = Object.keys(groups).map(function (k) {
    var e = groups[k];
    return {
      label: e.label,
      parts: CAT.map(function (c) { return { value: e.by[c.key] || 0, color: c.color, label: c.label }; })
        .concat([{ value: e.by[''] || 0, color: NONE_COLOR, label: 'Bez osvedčenia' }])
    };
  }).sort(function (a, b) {
    return b.parts.reduce(function (t, p) { return t + p.value; }, 0) - a.parts.reduce(function (t, p) { return t + p.value; }, 0);
  }).slice(0, 10);

  // Noví piloti za 12 mesiacov
  var months = monthsBack(12), idx = {};
  months.forEach(function (m, i) { idx[m.key] = i; m.n = 0; });
  pilots.forEach(function (p) { if (p.createdAt) { var i = idx[monthKey(p.createdAt)]; if (i !== undefined) months[i].n++; } });
  var newCols = columns(months.map(function (m) { return { label: m.label, title: m.title, parts: [{ value: m.n, color: '#1f7a8c', label: 'Noví piloti' }] }; }), ' pilotov');

  // Aktivita: kedy pilot naposledy letel
  var buckets = [
    { label: 'do 30 dní', test: function (d) { return d < 30; }, color: '#1f7a8c' },
    { label: '30–90 dní', test: function (d) { return d >= 30 && d < 90; }, color: '#5aa3b5' },
    { label: '3–12 mesiacov', test: function (d) { return d >= 90 && d < 365; }, color: '#a7cfd9' },
    { label: 'viac ako rok', test: function (d) { return d >= 365; }, color: '#e2b65c' }
  ];
  var never = 0;
  buckets.forEach(function (b) { b.n = 0; });
  pilots.forEach(function (p) {
    if (!p.lastFlightAt) { never++; return; }
    var d = (now - new Date(p.lastFlightAt).getTime()) / 86400000;
    buckets.forEach(function (b) { if (b.test(d)) b.n++; });
  });
  var actRows = buckets.map(function (b) { return { label: b.label, parts: [{ value: b.n, color: b.color, label: b.label }] }; })
    .concat([{ label: 'ešte neletel', parts: [{ value: never, color: NONE_COLOR, label: 'ešte neletel' }] }]);

  var body =
    '<div class="pc-kpis">' +
    kpi(total, 'pilotov') +
    kpi(Math.round(withCert / total * 100) + ' %', 'má osvedčenie', withCert + ' z ' + total) +
    kpi(byTop[''], 'bez osvedčenia', null, byTop[''] ? 'is-warn' : '') +
    kpi(active90, 'aktívnych', 'letel za 90 dní') +
    kpi(noOp, 'bez prevádzkovateľa', null, noOp ? 'is-warn' : '') +
    kpi(thisMonth, 'nových tento mesiac') +
    '</div>' +
    '<div class="pc-grid">' +
    '<div class="fd-card pc-c4"><h4>Piloti podľa ' + (groupBy === 'branch' ? 'zložiek' : 'útvarov') + ' a najvyššieho osvedčenia</h4>' + stackBars(rows) + legend(true) + '</div>' +
    '<div class="fd-card pc-c2"><h4>Najvyššie osvedčenie</h4>' + donut(segs, total, 'pilotov') + '</div>' +
    '<div class="fd-card pc-c3"><h4>Noví piloti za 12 mesiacov</h4>' + newCols + '</div>' +
    '<div class="fd-card pc-c3"><h4>Posledný let pilota</h4>' + stackBars(actRows, total) + '</div>' +
    '</div>';
  return wrap('register', 'Prehľad registra', body);
}

/* ------------------------------------------------------------ Osvedčenia */

export function certChartsHtml(pilots, opts) {
  opts = opts || {};
  var now = Date.now(), soon = now + 60 * 86400000;
  var all = [];
  pilots.forEach(function (p) {
    (p.certificates || []).forEach(function (c) { all.push({ p: p, c: c, cat: normCat(c.certificateTypeCode) }); });
  });
  var valid = all.filter(function (x) { return !isSusp(x.c) && (!x.c.validTo || new Date(x.c.validTo).getTime() >= now); });
  var expSoon = valid.filter(function (x) { return x.c.validTo && new Date(x.c.validTo).getTime() < soon; });
  var susp = all.filter(function (x) { return isSusp(x.c); });
  var withoutCert = pilots.filter(function (p) { return !(p.certificateCodes || []).length; }).length;
  var requests = pilots.filter(function (p) { return (p.requestedCertificateCodes || []).length; }).length;
  var licPending = opts.licPending || 0;

  // Prstenec: platné osvedčenia podľa kategórie
  var segs = CAT.map(function (c) {
    return { label: c.label, value: valid.filter(function (x) { return x.cat === c.key; }).length, color: c.color };
  });

  // Pokrytie: koľko % pilotov má danú kategóriu
  var cover = CAT.map(function (c) {
    var n = pilots.filter(function (p) { return (p.certificateCodes || []).some(function (k) { return normCat(k) === c.key; }); }).length;
    return '<div class="pm-cert"><span class="pm-cert-name">' + c.label + '</span>' +
      '<span class="pm-cert-track"><span style="width:' + (pilots.length ? n / pilots.length * 100 : 0).toFixed(1) + '%;background:' + c.color + '"></span></span>' +
      '<span class="pm-cert-val"><b>' + n + '</b> / ' + pilots.length + '</span></div>';
  }).join('');

  // Expirácie v nasledujúcich 24 mesiacoch (po štvrťrokoch), skladané podľa kategórie
  var quarters = [], qIdx = {};
  var d0 = new Date();
  var qStart = new Date(d0.getFullYear(), Math.floor(d0.getMonth() / 3) * 3, 1);
  for (var q = 0; q < 8; q++) {
    var qs = new Date(qStart.getFullYear(), qStart.getMonth() + q * 3, 1);
    var key = qs.getFullYear() + '-Q' + (Math.floor(qs.getMonth() / 3) + 1);
    qIdx[key] = q;
    quarters.push({ label: 'Q' + (Math.floor(qs.getMonth() / 3) + 1) + " '" + String(qs.getFullYear()).slice(2), title: (Math.floor(qs.getMonth() / 3) + 1) + '. štvrťrok ' + qs.getFullYear(), by: {} });
  }
  valid.forEach(function (x) {
    if (!x.c.validTo) return;
    var t = new Date(x.c.validTo);
    var k = t.getFullYear() + '-Q' + (Math.floor(t.getMonth() / 3) + 1);
    var i = qIdx[k];
    if (i !== undefined) quarters[i].by[x.cat] = (quarters[i].by[x.cat] || 0) + 1;
  });
  var expCols = columns(quarters.map(function (qq) {
    return { label: qq.label, title: qq.title, parts: CAT.map(function (c) { return { value: qq.by[c.key] || 0, color: c.color, label: c.label }; }) };
  }), ' osvedčení');

  // Vydané za 12 mesiacov (podľa dátumu platnosti od)
  var months = monthsBack(12), mIdx = {};
  months.forEach(function (m, i) { mIdx[m.key] = i; m.by = {}; });
  all.forEach(function (x) {
    if (!x.c.validFrom) return;
    var i = mIdx[monthKey(x.c.validFrom)];
    if (i !== undefined) months[i].by[x.cat] = (months[i].by[x.cat] || 0) + 1;
  });
  var issuedCols = columns(months.map(function (m) {
    return { label: m.label, title: m.title, parts: CAT.map(function (c) { return { value: m.by[c.key] || 0, color: c.color, label: c.label }; }) };
  }), ' osvedčení');

  // Najbližšie expirácie
  var upcoming = valid.filter(function (x) { return x.c.validTo; })
    .sort(function (a, b) { return new Date(a.c.validTo) - new Date(b.c.validTo); }).slice(0, 6);
  var upHtml = upcoming.length ? '<ol class="pc-up">' + upcoming.map(function (x) {
    var days = Math.ceil((new Date(x.c.validTo).getTime() - now) / 86400000);
    var col = (CAT.find(function (c) { return c.key === x.cat; }) || {}).color || NONE_COLOR;
    return '<li data-pc-pilot="' + x.p.id + '"><span class="pc-up-cat" style="background:' + col + '">' + esc((CAT.find(function (c) { return c.key === x.cat; }) || { label: x.cat }).label) + '</span>' +
      '<span class="pc-up-name"><b>' + esc(((x.p.firstName || '') + ' ' + (x.p.lastName || '')).trim()) + '</b><small>' + esc(x.p.orgUnitName || '') + '</small></span>' +
      '<span class="pc-up-days' + (days <= 60 ? ' is-soon' : '') + '">' + (days <= 0 ? 'dnes' : 'o ' + days + ' dní') + '<small>' + new Date(x.c.validTo).toLocaleDateString('sk-SK') + '</small></span></li>';
  }).join('') + '</ol>' : '<p class="pm-nodata">Žiadne osvedčenia s dátumom platnosti.</p>';

  var body =
    '<div class="pc-kpis">' +
    kpi(valid.length, 'platných osvedčení') +
    kpi(expSoon.length, 'vyprší do 60 dní', null, expSoon.length ? 'is-warn' : '') +
    kpi(susp.length, 'pozastavených', null, susp.length ? 'is-warn' : '') +
    kpi(withoutCert, 'pilotov bez osvedčenia', null, withoutCert ? 'is-warn' : '') +
    kpi(requests, 'žiadostí čaká', null, requests ? 'is-alert' : '') +
    kpi(licPending, 'licencií čaká', null, licPending ? 'is-alert' : '') +
    '</div>' +
    '<div class="pc-grid">' +
    '<div class="fd-card pc-c4"><h4>Kedy osvedčenia vyprší (najbližších 24 mesiacov)</h4>' + expCols + legend(false) + '</div>' +
    '<div class="fd-card pc-c2"><h4>Platné osvedčenia podľa kategórie</h4>' + donut(segs, valid.length, 'platných') + '</div>' +
    '<div class="fd-card pc-c2"><h4>Pokrytie pilotov</h4><div class="pm-certs">' + cover + '</div></div>' +
    '<div class="fd-card pc-c2"><h4>Vydané za 12 mesiacov</h4>' + issuedCols + '</div>' +
    '<div class="fd-card pc-c2"><h4>Najbližšie vypršia</h4>' + upHtml + '</div>' +
    '</div>';
  return wrap('certs', 'Prehľad osvedčení', body);
}
