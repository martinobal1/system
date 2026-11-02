import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { openListModal, openOperatorProfile, openUserEditor } from './orgsProfile.js';
import {
  ACCESS_LEVEL_LABELS, accountLevel, avatarHtml, certBadgesHtml, confirmDialog, content, copyText,
  isAccountRemoved, removeAccountFlow, restoreAccountFlow
} from '../core/ui.js';

// Organizačná štruktúra: vľavo strom útvarov, vpravo detail vybraného útvaru
// s prevádzkovateľmi (úroveň 2), ich pilotmi a nalietanými hodinami.

function esc(s) {
  return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function fmtHours(h) {
  return (Math.round(h * 10) / 10).toLocaleString('sk-SK', { minimumFractionDigits: 0, maximumFractionDigits: 1 });
}

const ICO = {
  plus: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
  user: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0"/><path d="M19 8v6M16 11h6"/></svg>',
  edit: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="m13.5 6.5 4 4"/></svg>',
  trash: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/></svg>',
  restore: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/></svg>',
  chev: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>',
  search: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>',
  units: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-2h12v2"/></svg>',
  ops: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/></svg>',
  pilot: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8" r="3.5"/><path d="M5 20a7 7 0 0 1 14 0"/></svg>',
  chart: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3v18h18"/><path d="M7 15v2M11 11v6M15 7v10M19 12v5"/></svg>',
  clock: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>'
};

export async function renderOrgs() {
  const isL2 = state.accessLevel === 2;
  const canManageOrgs = state.accessLevel === 1;
  const canManageUsers = state.accessLevel === 1 || state.accessLevel === 2;

  const results = await Promise.all([
    api('/api/v1/org-units?includeInactive=true'),
    api('/api/v1/accounts').catch(function () { return []; }),
    api('/api/v1/pilots').catch(function () { return []; }),
    api('/api/v1/flights').catch(function () { return []; })
  ]);
  const items = results[0] || [];
  const accounts = results[1] || [];
  const pilots = (results[2] || []).filter(function (p) { return p.isActive !== false; });
  const flights = results[3] || [];

  // ---------- štruktúra stromu ----------
  const byId = {};
  items.forEach(function (o) { byId[o.id] = o; });
  const childrenMap = {};
  items.forEach(function (o) {
    var pid = o.parentId && byId[o.parentId] ? o.parentId : '__root__';
    (childrenMap[pid] = childrenMap[pid] || []).push(o);
  });
  Object.keys(childrenMap).forEach(function (k) {
    childrenMap[k].sort(function (a, b) {
      var ka = kindOrder(a), kb = kindOrder(b);
      if (ka !== kb) return ka - kb;
      return String(a.name || '').localeCompare(String(b.name || ''), 'sk');
    });
  });

  function kindKey(o) {
    var k = o.kind;
    if (k === 'Root' || k === 0) return 'root';
    if (k === 'BranchPz' || k === 1) return 'pz';
    if (k === 'BranchHazz' || k === 2) return 'hazz';
    if (k === 'BranchCo' || k === 3) return 'co';
    return 'unit';
  }
  function kindOrder(o) {
    return { root: 0, pz: 1, hazz: 2, co: 3, unit: 4 }[kindKey(o)];
  }
  function branchOf(o) {
    var cur = o, guard = 0;
    while (cur && guard++ < 30) {
      var kk = kindKey(cur);
      if (kk === 'pz' || kk === 'hazz' || kk === 'co') return kk;
      if (kk === 'root') return 'root';
      cur = cur.parentId ? byId[cur.parentId] : null;
    }
    return 'unit';
  }
  function typeLabel(o) {
    var k = kindKey(o);
    if (k === 'root') return 'Ministerstvo';
    if (k === 'pz') return 'Zložka · Policajný zbor';
    if (k === 'hazz') return 'Zložka · HaZZ';
    if (k === 'co') return 'Zložka · Civilná ochrana';
    var code = String(o.code || ''), name = String(o.name || '');
    if (/^(KR-|HAZZ-KR-|CO-KR-)/.test(code) || /krajsk/i.test(name)) return 'Krajské riaditeľstvo';
    if (/^(OR-|HAZZ-OR-|CO-OR-)/.test(code) || /okresn/i.test(name)) return 'Okresné riaditeľstvo';
    return 'Útvar';
  }
  function isInSubtree(orgId, rootId) {
    var o = byId[orgId], root = byId[rootId];
    if (!o || !root) return false;
    if (o.path && root.path) return o.path.indexOf(root.path) === 0;
    var guard = 0;
    while (o && guard++ < 30) { if (o.id === rootId) return true; o = o.parentId ? byId[o.parentId] : null; }
    return false;
  }
  function pathOf(o) {
    var chain = [], cur = o, guard = 0;
    while (cur && guard++ < 30) { chain.unshift(cur); cur = cur.parentId ? byId[cur.parentId] : null; }
    return chain;
  }
  function descendants(id) {
    var out = [];
    (childrenMap[id] || []).forEach(function (c) { out.push(c); out = out.concat(descendants(c.id)); });
    return out;
  }

  // ---------- prevádzkovatelia, piloti, hodiny ----------
  const hoursByPilot = {};
  const flightsByPilot = {};
  flights.forEach(function (f) {
    if (!f.pilotId || !f.durationMinutes) return;
    hoursByPilot[f.pilotId] = (hoursByPilot[f.pilotId] || 0) + f.durationMinutes / 60;
    flightsByPilot[f.pilotId] = (flightsByPilot[f.pilotId] || 0) + 1;
  });
  const visibleAccounts = accounts.filter(function (a) { return state.orgShowRemoved || !isAccountRemoved(a); });
  const operators = visibleAccounts.filter(function (a) { return accountLevel(a) === 2; });

  function pilotsUnder(orgId) {
    return pilots.filter(function (p) { return isInSubtree(p.orgUnitId, orgId); });
  }
  function operatorsUnder(orgId) {
    return operators.filter(function (a) { return a.homeOrgUnitId && isInSubtree(a.homeOrgUnitId, orgId); });
  }
  function hoursOf(list) {
    return list.reduce(function (s, p) { return s + (hoursByPilot[p.id] || 0); }, 0);
  }
  // Každý pilot má jedného nadriadeného prevádzkovateľa (operatorAccountId).
  function pilotsOfOperator(op) {
    return pilots.filter(function (p) { return p.operatorAccountId === op.id; });
  }

  // ---------- výber útvaru ----------
  if (!state.orgCollapsed) state.orgCollapsed = {};
  if (!state.orgBranchFilter) state.orgBranchFilter = 'all';
  var roots = childrenMap['__root__'] || [];
  var selected = state.orgSelectedId && byId[state.orgSelectedId] ? byId[state.orgSelectedId] : null;
  if (!selected) {
    if (isL2) {
      var me = accounts.find(function (a) { return a.id === state.accountId; });
      selected = (me && byId[me.homeOrgUnitId]) || items.slice().sort(function (a, b) { return (b.level || 0) - (a.level || 0); })[0];
    } else {
      selected = items.find(function (o) { return kindKey(o) === 'pz'; }) || roots[0] || null;
    }
    if (selected) state.orgSelectedId = selected.id;
  }
  // predkovia vybraného útvaru sú vždy rozbalení
  if (selected) pathOf(selected).forEach(function (o) { if (o.id !== selected.id) state.orgCollapsed[o.id] = false; });

  // ---------- strom ----------
  var q = String(state.orgSearch || '').trim().toLowerCase();
  var matchIds = null;
  if (q) {
    matchIds = {};
    items.forEach(function (o) {
      if ((o.name + ' ' + o.code).toLowerCase().indexOf(q) >= 0) pathOf(o).forEach(function (a) { matchIds[a.id] = true; });
    });
  }
  function inBranchFilter(o) {
    var f = state.orgBranchFilter;
    if (f === 'all' || kindKey(o) === 'root') return true;
    return branchOf(o) === f;
  }
  function treeNode(o, depth) {
    if (!inBranchFilter(o)) return '';
    if (matchIds && !matchIds[o.id]) return '';
    var kids = (childrenMap[o.id] || []).filter(function (c) {
      return inBranchFilter(c) && (!matchIds || matchIds[c.id]) && (state.orgShowInactive || c.isActive !== false);
    });
    var hasKids = kids.length > 0;
    var collapsed = matchIds ? false : (state.orgCollapsed[o.id] !== undefined ? state.orgCollapsed[o.id] : depth >= 2);
    var opCount = operatorsUnder(o.id).length;
    var kk = kindKey(o);
    var html = '<li>' +
      '<div class="ot-row' + (selected && selected.id === o.id ? ' is-selected' : '') + (o.isActive === false ? ' is-inactive' : '') +
      '" data-select="' + o.id + '" style="--d:' + depth + '" role="treeitem" tabindex="0" aria-selected="' + (selected && selected.id === o.id) + '"' +
      (hasKids ? ' aria-expanded="' + !collapsed + '"' : '') + '>' +
      '<button type="button" class="ot-toggle' + (hasKids ? (collapsed ? '' : ' is-open') : ' is-leaf') + '" data-toggle="' + o.id + '" tabindex="-1" aria-label="Rozbaliť">' + ICO.chev + '</button>' +
      '<span class="ot-dot ot-' + (kk === 'unit' ? branchOf(o) : kk) + '"></span>' +
      '<span class="ot-name">' + esc(o.name) + '</span>' +
      (opCount ? '<span class="ot-count" title="Prevádzkovatelia">' + opCount + '</span>' : '') +
      '</div>';
    if (hasKids && !collapsed) html += '<ul>' + kids.map(function (c) { return treeNode(c, depth + 1); }).join('') + '</ul>';
    return html + '</li>';
  }
  var treeHtml = roots.map(function (r) { return treeNode(r, 0); }).join('') ||
    '<li class="ot-empty">Nič sa nenašlo</li>';

  // ---------- detail ----------
  function hbarList(rows, unit) {
    if (!rows.length) return '<p class="muted ot-nodata">Zatiaľ žiadne nalietané hodiny.</p>';
    var max = rows.reduce(function (m, r) { return Math.max(m, r.value); }, 0) || 1;
    return '<div class="hbars">' + rows.map(function (r) {
      return '<div class="hbar" title="' + esc(r.label) + ': ' + fmtHours(r.value) + ' ' + unit + '">' +
        '<span class="hbar-label">' + esc(r.label) + '</span>' +
        '<span class="hbar-track"><span class="hbar-fill" style="width:' + Math.max(2, (r.value / max) * 100).toFixed(1) + '%"></span></span>' +
        '<span class="hbar-val">' + fmtHours(r.value) + ' ' + unit + '</span></div>';
    }).join('') + '</div>';
  }

  // ---------- Prehľad náletu (grafy) ----------
  var CHART_COLORS = ['#1f7a8c', '#3b6fd8', '#d98a1c', '#7c5cd6', '#d1495b', '#2e9e5b', '#c2559f', '#5b7083'];
  /** Farba pre i-teho pilota: najprv ručne vybraná paleta, potom ďalšie výrazné odtiene (zlatý uhol). */
  var PILOT_PALETTE = ['#1f7a8c', '#3b6fd8', '#d98a1c', '#7c5cd6', '#d1495b', '#2e9e5b', '#c2559f', '#0ea5a4', '#e0652e', '#5a8f29', '#8e44ad', '#2a9bd6'];
  function pilotColor(i) {
    if (i < PILOT_PALETTE.length) return PILOT_PALETTE[i];
    var h = Math.round((i * 137.508 + 20) % 360);
    var l = 44 + (i % 3) * 7;
    return 'hsl(' + h + ', 58%, ' + l + '%)';
  }
  function initials(first, last) { return ((first || '').charAt(0) + (last || '').charAt(0)).toUpperCase() || '?'; }
  function pct(v, total) { return total > 0 ? Math.round((v / total) * 100) : 0; }

  function flightDashboard(selPilots, groupsIn) {
    var byOps = !!(groupsIn.length && groupsIn[0].isOperator);
    var inSel = {};
    selPilots.forEach(function (p) { inSel[p.id] = true; });
    var covered = {};
    var groups = groupsIn.map(function (g, i) {
      var ps = g.pilots.filter(function (p) { return inSel[p.id]; });
      ps.forEach(function (p) { covered[p.id] = true; });
      ps.sort(function (a, b) { return (hoursByPilot[b.id] || 0) - (hoursByPilot[a.id] || 0); });
      return { name: g.name, sub: g.sub, pilots: ps, hours: hoursOf(ps), color: CHART_COLORS[i % CHART_COLORS.length] };
    });
    var rest = selPilots.filter(function (p) { return !covered[p.id]; });
    if (hoursOf(rest) > 0) {
      rest.sort(function (a, b) { return (hoursByPilot[b.id] || 0) - (hoursByPilot[a.id] || 0); });
      groups.push({ name: byOps ? 'Bez prevádzkovateľa' : 'Priamo v útvare', sub: '', pilots: rest, hours: hoursOf(rest), color: '#9aa7b4' });
    }
    groups = groups.filter(function (g) { return g.hours > 0; }).sort(function (a, b) { return b.hours - a.hours; });
    var total = groups.reduce(function (t, g) { return t + g.hours; }, 0);
    if (total <= 0) {
      return '<section class="ot-section fd-empty"><div class="ot-section-head"><h3>Prehľad náletu</h3></div>' +
        '<p class="muted ot-nodata">V tomto útvare zatiaľ nie sú zaznamenané žiadne lety.</p></section>';
    }
    var colorOfPilot = {}, groupOfPilot = {};
    groups.forEach(function (g) { g.pilots.forEach(function (p) { colorOfPilot[p.id] = g.color; groupOfPilot[p.id] = g.name; }); });
    // Jediný prevádzkovateľ / útvar → grafy farbíme podľa pilotov (inak by boli celé jednofarebné)
    var perPilot = groups.length === 1;
    var series = groups;
    if (perPilot) {
      // každý pilot vlastnú farbu (žiadna sivá skupina „ostatní“)
      var ranked = groups[0].pilots.filter(function (p) { return (hoursByPilot[p.id] || 0) > 0; });
      series = ranked.map(function (p, i) {
        colorOfPilot[p.id] = pilotColor(i);
        return { name: (p.firstName || '') + ' ' + (p.lastName || ''), color: colorOfPilot[p.id], hours: hoursByPilot[p.id] || 0 };
      });
    }
    var max = groups[0].hours;
    var totalFlights = selPilots.reduce(function (t, p) { return t + (flightsByPilot[p.id] || 0); }, 0);

    // 1) Skladaný pruh: prevádzkovateľ → jeho piloti
    var stacked = groups.map(function (g) {
      var withHours = g.pilots.filter(function (p) { return (hoursByPilot[p.id] || 0) > 0; });
      var segs = withHours.map(function (p, i) {
        var h = hoursByPilot[p.id] || 0;
        var op = perPilot ? 1 : Math.max(0.38, 1 - i * 0.16);
        return '<span class="fd-seg" style="width:' + ((h / max) * 100).toFixed(2) + '%;background:' + (perPilot ? colorOfPilot[p.id] : g.color) + ';opacity:' + op.toFixed(2) + '"' +
          ' title="' + esc(p.firstName + ' ' + p.lastName) + ' — ' + fmtHours(h) + ' h (' + pct(h, g.hours) + ' %)"></span>';
      }).join('');
      var chipMax = perPilot ? 16 : 6;
      var chips = withHours.slice(0, chipMax).map(function (p, i) {
        var op = perPilot ? 1 : Math.max(0.38, 1 - i * 0.16);
        return '<span class="fd-chip"><i style="background:' + (perPilot ? colorOfPilot[p.id] : g.color) + ';opacity:' + op.toFixed(2) + '"></i>' +
          esc(p.firstName + ' ' + p.lastName) + ' <b>' + fmtHours(hoursByPilot[p.id] || 0) + ' h</b></span>';
      }).join('') + (withHours.length > chipMax ? '<span class="fd-chip fd-more">+' + (withHours.length - chipMax) + ' ďalší</span>' : '');
      return '<div class="fd-op">' +
        '<div class="fd-op-head"><span class="fd-dot" style="background:' + g.color + '"></span>' +
        '<b>' + esc(g.name) + '</b>' + (g.sub ? '<small>' + esc(g.sub) + '</small>' : '') +
        '<span class="fd-op-total">' + fmtHours(g.hours) + ' h <small>· ' + withHours.length + ' z ' + g.pilots.length + ' pilotov</small></span></div>' +
        '<div class="fd-track">' + segs + '</div>' +
        '<div class="fd-chips">' + chips + '</div></div>';
    }).join('');

    // 2) Prstencový graf — podiel na celkovom nálete
    var R = 52, C = 2 * Math.PI * R, offset = 0;
    var arcs = series.map(function (g) {
      var len = (g.hours / total) * C;
      var gap = series.length > 1 ? Math.min(2, len * 0.3) : 0;
      var arc = '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="' + g.color + '" stroke-width="18"' +
        ' stroke-dasharray="' + Math.max(0, len - gap).toFixed(2) + ' ' + (C - Math.max(0, len - gap)).toFixed(2) + '"' +
        ' stroke-dashoffset="' + (-offset).toFixed(2) + '"><title>' + esc(g.name) + ': ' + fmtHours(g.hours) + ' h (' + pct(g.hours, total) + ' %)</title></circle>';
      offset += len;
      return arc;
    }).join('');
    var donut = '<div class="fd-donut-wrap"><svg class="fd-donut" viewBox="0 0 140 140" role="img" aria-label="Podiel náletu">' +
      '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="#eef2f6" stroke-width="18"/>' +
      '<g transform="rotate(-90 70 70)">' + arcs + '</g>' +
      '<text x="70" y="67" text-anchor="middle" class="fd-donut-num">' + fmtHours(total) + '</text>' +
      '<text x="70" y="84" text-anchor="middle" class="fd-donut-lbl">hodín spolu</text></svg>' +
      '<ul class="fd-legend">' + series.slice(0, 8).map(function (g) {
        return '<li><span class="fd-dot" style="background:' + g.color + '"></span><span class="fd-legend-name">' + esc(g.name) + '</span>' +
          '<b>' + pct(g.hours, total) + ' %</b></li>';
      }).join('') + (series.length > 8 ? '<li class="fd-legend-more">+ ďalší ' + (series.length - 8) + ' — prejdite myšou na graf</li>' : '') + '</ul></div>';

    // 3) Mesačný nálet za posledných 12 mesiacov (skladaný podľa prevádzkovateľa)
    var now = new Date();
    var months = [];
    for (var m = 11; m >= 0; m--) {
      var d = new Date(now.getFullYear(), now.getMonth() - m, 1);
      months.push({ key: d.getFullYear() + '-' + d.getMonth(), label: d.toLocaleDateString('sk-SK', { month: 'short' }).replace('.', ''), year: d.getFullYear(), byColor: {}, total: 0 });
    }
    var monthIdx = {};
    months.forEach(function (mo, i) { monthIdx[mo.key] = i; });
    flights.forEach(function (f) {
      if (!f.pilotId || !inSel[f.pilotId] || !f.durationMinutes) return;
      var t = f.takeoffAt || f.landingAt; if (!t) return;
      var d = new Date(t); var i = monthIdx[d.getFullYear() + '-' + d.getMonth()];
      if (i === undefined) return;
      var col = colorOfPilot[f.pilotId] || '#9aa7b4';
      months[i].byColor[col] = (months[i].byColor[col] || 0) + f.durationMinutes / 60;
      months[i].total += f.durationMinutes / 60;
    });
    var mMax = months.reduce(function (x, mo) { return Math.max(x, mo.total); }, 0);
    var monthly = mMax <= 0 ? '<p class="muted ot-nodata">Za posledných 12 mesiacov žiadne lety.</p>' :
      '<div class="fd-cols">' + months.map(function (mo) {
        var stack = series.map(function (g) {
          var v = mo.byColor[g.color] || 0;
          return v > 0 ? '<span style="height:' + ((v / mMax) * 100).toFixed(2) + '%;background:' + g.color + '" title="' + esc(g.name) + ': ' + fmtHours(v) + ' h"></span>' : '';
        }).join('');
        return '<div class="fd-col" title="' + esc(mo.label + ' ' + mo.year) + ': ' + fmtHours(mo.total) + ' h">' +
          '<span class="fd-col-val">' + (mo.total > 0 ? fmtHours(mo.total) : '') + '</span>' +
          '<div class="fd-col-bar">' + stack + '</div><span class="fd-col-lbl">' + esc(mo.label) + '</span></div>';
      }).join('') + '</div>';

    // 4) Rebríček pilotov
    var top = selPilots.filter(function (p) { return (hoursByPilot[p.id] || 0) > 0; })
      .sort(function (a, b) { return (hoursByPilot[b.id] || 0) - (hoursByPilot[a.id] || 0); }).slice(0, 6);
    var topMax = top.length ? hoursByPilot[top[0].id] : 1;
    var ranking = '<ol class="fd-rank">' + top.map(function (p, i) {
      var h = hoursByPilot[p.id] || 0, col = colorOfPilot[p.id] || '#9aa7b4';
      return '<li><span class="fd-rank-no' + (i < 3 ? ' is-top' : '') + '">' + (i + 1) + '</span>' +
        '<span class="fd-ava" style="background:' + col + '">' + esc(initials(p.firstName, p.lastName)) + '</span>' +
        '<div class="fd-rank-main"><div class="fd-rank-line"><b>' + esc(p.firstName + ' ' + p.lastName) + '</b>' +
        '<span class="fd-rank-h">' + fmtHours(h) + ' h</span></div>' +
        '<div class="fd-rank-bar"><span style="width:' + ((h / topMax) * 100).toFixed(1) + '%;background:' + col + '"></span></div>' +
        '<small>' + esc(groupOfPilot[p.id] || '—') + ' · ' + (flightsByPilot[p.id] || 0) + ' letov</small></div></li>';
    }).join('') + '</ol>';

    var avg = totalFlights ? (total * 60) / totalFlights : 0;
    return '<section class="ot-section fd">' +
      '<div class="ot-section-head"><h3>Prehľad náletu</h3>' +
      '<div class="fd-kpis"><span><b>' + fmtHours(total) + ' h</b> nálet</span><span><b>' + totalFlights + '</b> letov</span>' +
      '<span><b>' + Math.round(avg) + ' min</b> priemerný let</span></div></div>' +
      '<div class="fd-grid">' +
      '<div class="fd-card fd-span2"><h4>' + (byOps ? 'Nálet podľa prevádzkovateľov a ich pilotov' : 'Nálet podľa útvarov a pilotov') + '</h4>' +
        '<p class="fd-hint">Každý pruh tvoria piloti ' + (byOps ? 'daného prevádzkovateľa' : 'daného útvaru') + ' — prejdite myšou pre detail.</p>' + stacked + '</div>' +
      '<div class="fd-card"><h4>' + (perPilot ? 'Podiel pilotov na nálete' : 'Podiel na celkovom nálete') + '</h4>' + donut + '</div>' +
      '<div class="fd-card fd-span2"><h4>Nálet za posledných 12 mesiacov</h4>' + monthly + '</div>' +
      '<div class="fd-card"><h4>Najviac nalietaní piloti</h4>' + ranking + '</div>' +
      '</div></section>';
  }

  function userRow(a) {
    var removed = isAccountRemoved(a);
    var isMe = a.id === state.accountId;
    var lvl = accountLevel(a);
    var actions = '';
    if (canManageUsers && !isMe && !(isL2 && lvl !== 3)) {
      actions = removed
        ? '<button type="button" class="icon-btn" data-restore="' + a.id + '" title="Obnoviť používateľa">' + ICO.restore + '<span>Obnoviť</span></button>'
        : '<button type="button" class="icon-btn danger" data-remove="' + a.id + '" title="Odstrániť používateľa">' + ICO.trash + '<span>Odstrániť</span></button>';
    }
    if (canEditAccount(a) && !removed) {
      actions = '<button type="button" class="icon-btn" data-edit-user="' + a.id + '" title="Upraviť meno, priezvisko, e-mail">' + ICO.edit + '<span>Upraviť</span></button>' + actions;
    }
    // Pilot: riadok sa dá rozkliknúť — osvedčenia, nálet a zmena nadriadeného prevádzkovateľa.
    var pilot = pilots.find(function (p) { return p.personId === a.personId; }) || null;
    var open = !!(pilot && (state.orgOpenUsers || {})[a.id]);
    var opName = pilot ? (pilot.operatorName || null) : null;
    var row = '<div class="person-row' + (removed ? ' is-removed' : '') + (pilot ? ' is-clickable' : '') + (open ? ' is-open' : '') + '"' +
      (pilot ? ' data-user-toggle="' + a.id + '" role="button" tabindex="0" aria-expanded="' + open + '"' : '') + '>' +
      avatarHtml(a.firstName, a.lastName, a.userName) +
      '<div class="person-main"><b>' + esc((a.firstName || '') + ' ' + (a.lastName || '')) + '</b>' +
      '<span>@' + esc(a.userName) + ' · ' + esc(a.homeOrgUnitName || '—') +
      (pilot ? ' · ' + (opName ? 'Prevádzkovateľ: ' + esc(opName) : '<span class="txt-danger">bez prevádzkovateľa</span>') : '') + '</span></div>' +
      '<span class="lvl-pill lvl-' + lvl + '">' + (ACCESS_LEVEL_LABELS[lvl] || '—') + '</span>' +
      (removed ? '<span class="badge badge-danger">Odstránený</span>' : '') +
      '<div class="person-actions">' + actions + '</div>' +
      (pilot ? '<span class="op-chev">' + ICO.chev + '</span>' : '') + '</div>';

    // Používateľ bez záznamu pilota (napr. gestor) nemá čo rozbaliť.
    if (!pilot) return row;
    // Prevádzkovatelia, ktorí môžu viesť pilota: z jeho útvaru alebo z nadradeného útvaru (server to overí znova).
    var pilotOrg = byId[pilot.orgUnitId];
    var candidates = operators.filter(function (o) {
      var home = o.homeOrgUnitId && byId[o.homeOrgUnitId];
      return !isAccountRemoved(o) && home && pilotOrg && String(pilotOrg.path || '').indexOf(home.path) === 0;
    });
    var canChange = canManageUsers && !removed;
    var opControl = canChange
      ? '<select class="user-op-select" data-pilot="' + pilot.id + '" aria-label="Nadriadený prevádzkovateľ">' +
          '<option value="">— bez prevádzkovateľa —</option>' +
          candidates.map(function (o) {
            var otherUnit = o.homeOrgUnitId !== pilot.orgUnitId ? ' · ' + esc(o.homeOrgUnitName || '') : '';
            return '<option value="' + o.id + '"' + (o.id === pilot.operatorAccountId ? ' selected' : '') + '>' +
              esc((o.firstName || '') + ' ' + (o.lastName || '')) + otherUnit + ' (' + pilotsOfOperator(o).length + ' pil.)</option>';
          }).join('') +
        '</select><button type="button" class="user-op-save" data-pilot="' + pilot.id + '" disabled>Uložiť</button>'
      : '<b>' + esc(opName || '— nepriradený —') + '</b>';
    var certs = (pilot.certificateCodes || []).length
      ? certBadgesHtml(pilot.certificateCodes) : '<span class="badge badge-warn">Bez osvedčenia</span>';
    return row +
      '<div class="person-detail" data-user-detail="' + a.id + '"' + (open ? '' : ' hidden') + '>' +
      '<div class="pd-facts">' +
        '<span class="pd-certs">' + certs + '</span>' +
        '<span class="pd-stat"><b>' + (flightsByPilot[pilot.id] || 0) + '</b> letov</span>' +
        '<span class="pd-stat"><b>' + fmtHours(hoursByPilot[pilot.id] || 0) + '</b> h náletu</span>' +
        '<button type="button" class="pd-link" data-open-pilot="' + pilot.id + '">Karta pilota ' + ICO.chev + '</button>' +
      '</div>' +
      '<div class="pd-op"><span class="pd-label">Nadriadený</span>' + opControl + '</div>' +
      (canChange && !candidates.length ? '<small class="txt-warn">V útvare pilota ani nad ním nie je žiadny prevádzkovateľ.</small>' : '') +
      '<span class="user-op-msg" data-pilot="' + pilot.id + '"></span>' +
      '</div>';
  }

  /** Kto smie meniť meno/priezvisko/e-mail: gestor všetkých, prevádzkovateľ svojich pilotov a seba. */
  function canEditAccount(a) {
    if (a.id === state.accountId) return true;
    if (!canManageUsers) return false;
    if (state.accessLevel === 1) return true;
    return isL2 && accountLevel(a) === 3;
  }

  function statTile(kind, ico, value, label, hint, disabled) {
    return '<button type="button" class="ot-stat ot-stat-link' + (disabled ? ' is-disabled' : '') + '" data-stat="' + kind + '"' + (disabled ? ' disabled' : '') + '>' +
      '<span class="ot-stat-ico">' + ico + '</span><b>' + value + '</b><span>' + label + '</span>' +
      (disabled ? '' : '<small class="ot-stat-go"><em>' + hint + '</em>' + ICO.chev + '</small>') + '</button>';
  }

  function operatorCard(op) {
    var ps = pilotsOfOperator(op).slice().sort(function (a, b) { return (hoursByPilot[b.id] || 0) - (hoursByPilot[a.id] || 0); });
    var total = hoursOf(ps);
    var open = !!(state.orgOpenOperators || {})[op.id];
    var removed = isAccountRemoved(op);
    var isMe = op.id === state.accountId;
    var removeBtn = (canManageUsers && !isL2 && !isMe)
      ? (removed
        ? '<button type="button" class="icon-btn" data-restore="' + op.id + '" title="Obnoviť">' + ICO.restore + '</button>'
        : '<button type="button" class="icon-btn danger" data-remove="' + op.id + '" title="Odstrániť prevádzkovateľa">' + ICO.trash + '</button>')
      : '';
    var top = ps.filter(function (p) { return hoursByPilot[p.id]; }).slice(0, 10).map(function (p) {
      return { label: (p.firstName || '') + ' ' + (p.lastName || ''), value: hoursByPilot[p.id] };
    });
    var pilotRows = ps.map(function (p) {
      return '<tr class="op-pilot" data-pilot="' + p.id + '">' +
        '<td><span class="cell-person">' + avatarHtml(p.firstName, p.lastName, p.id) + '<span>' + esc((p.firstName || '') + ' ' + (p.lastName || '')) + '</span></span></td>' +
        '<td>' + esc(p.orgUnitName || '—') + '</td>' +
        '<td>' + ((p.certificateCodes || []).length ? certBadgesHtml(p.certificateCodes) : '<span class="badge badge-warn">Bez osvedčenia</span>') + '</td>' +
        '<td class="num">' + (flightsByPilot[p.id] || 0) + '</td>' +
        '<td class="num"><b>' + fmtHours(hoursByPilot[p.id] || 0) + ' h</b></td></tr>';
    }).join('');
    return '<div class="op-card' + (open ? ' is-open' : '') + (removed ? ' is-removed' : '') + '">' +
      '<div class="op-card-head" data-op-toggle="' + op.id + '" role="button" tabindex="0" aria-expanded="' + open + '">' +
      avatarHtml(op.firstName, op.lastName, op.userName) +
      '<div class="op-card-title"><b>' + esc((op.firstName || '') + ' ' + (op.lastName || '')) + '</b>' +
      '<span>@' + esc(op.userName) + ' · ' + esc(op.homeOrgUnitName || '—') + '</span></div>' +
      (removed ? '<span class="badge badge-danger">Odstránený</span>' : '') +
      '<div class="op-card-stats"><span><b>' + ps.length + '</b> pilotov</span><span><b>' + fmtHours(total) + ' h</b> nálet</span></div>' +
      '<button type="button" class="op-profile-btn" data-profile="' + op.id + '" title="Profil prevádzkovateľa s grafmi">' + ICO.chart + '<span>Profil</span></button>' +
      (canEditAccount(op) ? '<button type="button" class="icon-btn" data-edit-user="' + op.id + '" title="Upraviť meno, priezvisko, e-mail">' + ICO.edit + '</button>' : '') +
      removeBtn +
      '<span class="op-chev">' + ICO.chev + '</span>' +
      '</div>' +
      (open
        ? '<div class="op-card-body">' +
          (ps.length
            ? '<div class="op-card-grid"><div><h4>Nalietané hodiny pilotov</h4>' + hbarList(top, 'h') + '</div>' +
              '<div class="op-card-table"><table><thead><tr><th>Pilot</th><th>Útvar</th><th>Osvedčenia</th><th class="num">Lety</th><th class="num">Nálet</th></tr></thead><tbody>' +
              pilotRows + '</tbody></table></div></div>'
            : '<p class="muted ot-nodata">Prevádzkovateľ zatiaľ nemá pridelených žiadnych pilotov.</p>') +
          '</div>'
        : '') +
      '</div>';
  }

  var detailHtml = '<div class="ot-detail-empty">Vyberte útvar v strome.</div>';
  if (selected) {
    var subUnits = descendants(selected.id).filter(function (o) { return o.isActive !== false; });
    var selOps = operatorsUnder(selected.id);
    var selPilots = pilotsUnder(selected.id);
    var selHours = hoursOf(selPilots);
    var crumbs = pathOf(selected).map(function (o, i, arr) {
      return i === arr.length - 1 ? '<span>' + esc(o.name) + '</span>'
        : '<button type="button" class="crumb-link" data-select="' + o.id + '">' + esc(o.name) + '</button>';
    }).join('<span class="crumb-sep">/</span>');
    var opsSorted = selOps.slice().sort(function (a, b) { return hoursOf(pilotsOfOperator(b)) - hoursOf(pilotsOfOperator(a)); });
    var opsChart = opsSorted.map(function (op) {
      return { label: (op.firstName || '') + ' ' + (op.lastName || '') + ' · ' + (op.homeOrgUnitName || ''), value: hoursOf(pilotsOfOperator(op)) };
    }).filter(function (r) { return r.value > 0; });
    // keď nie sú prevádzkovatelia, ukáž aspoň hodiny podľa priamych podútvarov
    var unitChart = (childrenMap[selected.id] || []).filter(function (c) { return c.isActive !== false; }).map(function (c) {
      return { label: c.name, value: hoursOf(pilotsUnder(c.id)) };
    }).filter(function (r) { return r.value > 0; }).sort(function (a, b) { return b.value - a.value; });

    var otherUsers = visibleAccounts.filter(function (a) {
      return accountLevel(a) !== 2 && a.homeOrgUnitId === selected.id;
    });
    // Najčastejšia akcia (pridať človeka) je prvá a výrazná; práca s útvarmi je oddelená v skupine „Útvar“.
    var orgActions = '';
    if (canManageUsers && selected.isActive !== false) {
      orgActions += '<button type="button" id="org-add-user" class="ot-main-action" title="' +
        (state.accessLevel === 1 ? 'Pridať pilota alebo prevádzkovateľa do tohto útvaru' : 'Pridať pilota do tohto útvaru') + '">' +
        ICO.user + ' Pridať používateľa</button>';
    }
    if (canManageOrgs) {
      orgActions += selected.isActive === false
        ? '<button type="button" class="secondary" id="org-reactivate">' + ICO.restore + ' Aktivovať útvar</button>'
        : '<div class="ot-unit-menu">' +
            '<button type="button" class="secondary ot-unit-menu-btn" id="org-unit-menu-btn" aria-haspopup="menu" aria-expanded="false">' +
            ICO.units + ' Spravovať útvar <span class="ot-caret">▾</span></button>' +
            '<div class="ot-unit-menu-list hidden" id="org-unit-menu" role="menu">' +
            '<button type="button" role="menuitem" id="org-add-child">' + ICO.plus + '<span><b>Vytvoriť podútvar</b><small>Nová organizačná jednotka pod týmto útvarom</small></span></button>' +
            '<button type="button" role="menuitem" id="org-edit">' + ICO.edit + '<span><b>Upraviť útvar</b><small>Názov alebo nadradený útvar</small></span></button>' +
            (kindKey(selected) === 'root' ? '' :
              '<button type="button" role="menuitem" class="is-danger" id="org-deactivate">' + ICO.trash + '<span><b>Vymazať útvar</b><small>Dá sa neskôr obnoviť</small></span></button>') +
            '</div></div>';
    }

    detailHtml =
      '<div class="ot-detail-head">' +
      '<nav class="ot-crumbs">' + crumbs + '</nav>' +
      '<div class="ot-title-row"><div>' +
      '<p class="ot-type"><span class="ot-dot ot-' + (kindKey(selected) === 'unit' ? branchOf(selected) : kindKey(selected)) + '"></span>' + typeLabel(selected) +
      ' · <code>' + esc(selected.code) + '</code>' + (selected.isActive === false ? ' · <span class="badge badge-danger">Neaktívny</span>' : '') + '</p>' +
      '<h2>' + esc(selected.name) + '</h2></div>' +
      '<div class="ot-actions">' + orgActions + '</div></div></div>' +
      '<div class="ot-stats">' +
      statTile('units', ICO.units, subUnits.length, 'podriadených útvarov', subUnits.length ? 'Zoznam' : '', !subUnits.length) +
      statTile('ops', ICO.ops, selOps.length, 'prevádzkovateľov', 'Zobraziť', !selOps.length) +
      statTile('pilots', ICO.pilot, selPilots.length, 'pilotov', 'Register PnD', !selPilots.length) +
      statTile('hours', ICO.clock, fmtHours(selHours) + ' h', 'nalietaných hodín', 'Denník letov', selHours <= 0) +
      '</div>' +
      '<div id="org-msg"></div>' +
      formsHtml() +
      flightDashboard(selPilots, opsSorted.length
        ? opsSorted.map(function (op) {
            return { isOperator: true, name: ((op.firstName || '') + ' ' + (op.lastName || '')).trim() || op.userName, sub: op.homeOrgUnitName || '', pilots: pilotsOfOperator(op) };
          })
        : (childrenMap[selected.id] || []).filter(function (c) { return c.isActive !== false; }).map(function (c) {
            return { name: c.name, sub: '', pilots: pilotsUnder(c.id) };
          })) +
      '<section class="ot-section" id="ot-ops-section"><div class="ot-section-head"><h3>Prevádzkovatelia <span class="tab-count">' + selOps.length + '</span></h3>' +
      '<p class="muted">Kliknite na prevádzkovateľa — zobrazia sa jeho piloti a ich nálet.</p></div>' +
      (opsSorted.length ? '<div class="op-list">' + opsSorted.map(operatorCard).join('') + '</div>'
        : '<div class="ot-empty-box"><p>V tomto útvare zatiaľ nie je žiadny prevádzkovateľ.</p>' +
          (canManageUsers && state.accessLevel === 1 && selected.isActive !== false ? '<button type="button" class="secondary" id="org-add-operator">' + ICO.user + ' Pridať prevádzkovateľa</button>' : '') + '</div>') +
      '</section>' +
      (function () {
        // Piloti útvaru bez nadriadeného prevádzkovateľa — upozornenie, aby sa im niekto priradil
        var orphans = selPilots.filter(function (p) { return !p.operatorAccountId; });
        if (!orphans.length) return '';
        return '<section class="ot-section ot-orphans"><div class="ot-section-head"><h3>Piloti bez prevádzkovateľa <span class="tab-count">' + orphans.length + '</span></h3>' +
          '<p class="muted">Každý pilot má mať nadriadeného prevádzkovateľa. Kliknite na pilota a priraďte ho v jeho karte.</p></div>' +
          '<div class="op-card-table"><table><tbody>' + orphans.map(function (p) {
            return '<tr class="op-pilot" data-pilot="' + p.id + '"><td><span class="cell-person">' + avatarHtml(p.firstName, p.lastName, p.id) +
              '<span>' + esc((p.firstName || '') + ' ' + (p.lastName || '')) + '</span></span></td><td>' + esc(p.orgUnitName || '—') + '</td></tr>';
          }).join('') + '</tbody></table></div></section>';
      })() +
      (otherUsers.length
        ? '<section class="ot-section"><div class="ot-section-head"><h3>Ďalší používatelia útvaru <span class="tab-count">' + otherUsers.length + '</span></h3></div>' +
          '<div class="person-list">' + otherUsers.map(userRow).join('') + '</div></section>'
        : '');
  }

  function formsHtml() {
    return '<div id="org-panel" class="form-panel hidden ot-form-unit">' +
      '<h3 id="org-panel-title">Nový útvar</h3>' +
      '<p class="ot-form-hint">' + ICO.units + '<span><b>Útvar</b> je organizačná jednotka (napr. „Lynx Komando“, „Krajské riaditeľstvo Nitra“). ' +
      'Ľudí (pilotov, prevádzkovateľov) <b>nepridávate tu</b> — na to slúži tlačidlo <b>„Pridať používateľa“</b>.</span></p>' +
      '<form id="org-form" class="form-grid">' +
      '<input type="hidden" id="org-id" value="" /><input type="hidden" id="org-rv" value="" /><input type="hidden" id="org-orig-parent" value="" />' +
      '<label>Názov<input id="org-name" required placeholder="napr. Okresné riaditeľstvo Bratislava I" maxlength="200" /></label>' +
      '<label>Kód<input id="org-code" required placeholder="napr. OR-BA-1" maxlength="64" /></label>' +
      '<label>Zložka<select id="org-kind"><option value="pz">Policajný zbor</option><option value="hazz">Hasičský a záchranný zbor</option><option value="co">Civilná ochrana</option></select></label>' +
      '<label>Nadradený útvar<select id="org-parent"></select></label>' +
      '<div class="full toolbar"><button type="submit" id="org-submit">Vytvoriť útvar</button><button type="button" class="secondary" id="org-cancel">Zrušiť</button></div>' +
      '</form></div>' +
      '<div id="org-user-panel" class="form-panel hidden ot-form-user">' +
      '<h3>Nový používateľ (osoba s prihlásením)</h3>' +
      '<p class="ot-form-hint">' + ICO.user + '<span>Pridávate <b>človeka</b> do útvaru <b>' + (selected ? esc(selected.name) : '') + '</b>. ' +
      'Dostane prihlasovacie meno a heslo (vygeneruje sa automaticky). Ak chcete vytvoriť nový útvar, zrušte tento formulár a použite <b>„Vytvoriť podútvar“</b>.</span></p>' +
      '<form id="org-user-form" class="form-grid">' +
      // 1. krok: kto to je — až potom údaje
      (state.accessLevel === 1
        ? '<div class="full level-pick"><span class="level-pick-title">1. Kto to je?</span>' +
          '<input type="hidden" id="org-user-level" value="" />' +
          '<div class="level-pick-options" role="radiogroup" aria-label="Úroveň práv">' +
          '<button type="button" class="level-card" role="radio" data-level="3">' + ICO.pilot +
            '<span><b>Pilot</b><small>Lieta s dronmi, žiada o lety (úroveň 3)</small></span></button>' +
          '<button type="button" class="level-card" role="radio" data-level="2">' + ICO.ops +
            '<span><b>Prevádzkovateľ</b><small>Spravuje útvar, pilotov a drony (úroveň 2)</small></span></button>' +
          '</div><span class="level-pick-title level-pick-step2" style="margin-top:0.4rem">2. Údaje</span>' +
          '<span class="level-pick-wait">Najprv vyberte, či vytvárate pilota alebo prevádzkovateľa.</span></div>'
        : '<input type="hidden" id="org-user-level" value="3" />' +
          '<div class="full level-pick"><span class="level-pick-title">Vytvárate účet pre: <b>Pilota (úroveň 3)</b></span></div>') +
      '<label class="full hidden" id="org-user-title-wrap">Názov prevádzkovateľa<input id="org-user-title" autocomplete="off" maxlength="128" placeholder="napr. Letka PZ Bratislava" /></label>' +
      '<label id="org-user-first-wrap">Meno<input id="org-user-first" required autocomplete="off" /></label>' +
      '<label id="org-user-last-wrap">Priezvisko<input id="org-user-last" required autocomplete="off" /></label>' +
      '<label>Login' +
      '<input id="org-user-login" required autocomplete="off" placeholder="vyplní sa automaticky z mena (dá sa zmeniť)" />' +
      '<small id="org-login-hint" class="login-hint"></small></label>' +
      '<label>E-mail<input id="org-user-email" type="email" placeholder="voliteľné" /></label>' +
      '<label class="full hidden" id="org-user-operator-wrap">Nadriadený prevádzkovateľ' +
      '<select id="org-user-operator"><option value="">Načítavam…</option></select>' +
      '<small class="login-hint" id="org-user-operator-hint"></small></label>' +
      '<div class="full toolbar"><button type="submit">Vytvoriť používateľa a vygenerovať heslo</button><button type="button" class="secondary" id="org-user-cancel">Zrušiť</button></div>' +
      '</form><div id="org-user-result" class="hidden"></div></div>';
  }

  var branchBtn = function (key, label) {
    return '<button type="button" data-branch="' + key + '"' + (state.orgBranchFilter === key ? ' class="is-on"' : '') + '>' + label + '</button>';
  };
  var totalOps = operators.length;
  content.innerHTML =
    '<div class="stack org-page">' +
    '<div class="page-head"><div><h2>Organizačná štruktúra</h2>' +
    '<p class="muted">' + (isL2 ? 'Váš útvar, jeho piloti a ich nálet.' : 'Útvary MV SR, ich prevádzkovatelia, piloti a nalietané hodiny.') + '</p></div>' +
    '<div class="chip-row"><span class="chip">Útvary <b>' + items.filter(function (o) { return o.isActive !== false; }).length + '</b></span>' +
    '<span class="chip">Prevádzkovatelia <b>' + totalOps + '</b></span><span class="chip">Piloti <b>' + pilots.length + '</b></span></div></div>' +
    '<div class="ot-layout">' +
    '<aside class="ot-tree-card">' +
    '<div class="ot-search">' + ICO.search + '<input type="search" id="org-search" placeholder="Hľadať útvar…" value="' + esc(state.orgSearch || '') + '" /></div>' +
    (isL2 ? '' : '<div class="segmented ot-branches">' + branchBtn('all', 'Všetko') + branchBtn('pz', 'PZ') + branchBtn('hazz', 'HaZZ') + branchBtn('co', 'CO') + '</div>') +
    '<ul class="ot-tree" role="tree">' + treeHtml + '</ul>' +
    '<div class="ot-tree-foot">' +
    (canManageOrgs ? '<button type="button" class="secondary" id="org-add-btn">' + ICO.plus + ' Vytvoriť nový útvar</button>' : '') +
    '<label class="ot-check"><input type="checkbox" id="org-show-inactive"' + (state.orgShowInactive ? ' checked' : '') + ' /> Neaktívne útvary</label>' +
    (canManageUsers ? '<label class="ot-check"><input type="checkbox" id="org-show-removed"' + (state.orgShowRemoved ? ' checked' : '') + ' /> Odstránení používatelia</label>' : '') +
    '</div></aside>' +
    '<section class="ot-detail">' + detailHtml + '</section>' +
    '</div></div>';

  // ---------- udalosti ----------
  function select(id) {
    state.orgSelectedId = id;
    navigate('orgs').then(function () {
      var d = content.querySelector('.ot-detail');
      if (d && window.innerWidth < 1000) d.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }
  content.querySelectorAll('[data-select]').forEach(function (el) {
    el.addEventListener('click', function (ev) {
      if (ev.target.closest('[data-toggle]')) return;
      select(el.getAttribute('data-select'));
    });
    el.addEventListener('keydown', function (ev) {
      if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); select(el.getAttribute('data-select')); }
    });
  });
  content.querySelectorAll('[data-toggle]').forEach(function (btn) {
    btn.addEventListener('click', function (ev) {
      ev.stopPropagation();
      var id = btn.getAttribute('data-toggle');
      var row = btn.closest('.ot-row');
      var isOpen = btn.classList.contains('is-open');
      state.orgCollapsed[id] = isOpen;
      if (!btn.classList.contains('is-leaf')) navigate('orgs');
      else if (row) select(id);
    });
  });
  content.querySelectorAll('.ot-branches button').forEach(function (btn) {
    btn.onclick = function () { state.orgBranchFilter = btn.dataset.branch; navigate('orgs'); };
  });
  var searchEl = document.getElementById('org-search');
  var searchTimer = null;
  searchEl.addEventListener('input', function () {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(function () {
      state.orgSearch = searchEl.value;
      navigate('orgs').then(function () {
        var s = document.getElementById('org-search');
        if (s) { s.focus(); s.setSelectionRange(s.value.length, s.value.length); }
      });
    }, 250);
  });
  document.getElementById('org-show-inactive').onchange = function () { state.orgShowInactive = this.checked; navigate('orgs'); };
  var removedToggle = document.getElementById('org-show-removed');
  if (removedToggle) removedToggle.onchange = function () { state.orgShowRemoved = this.checked; navigate('orgs'); };

  content.querySelectorAll('[data-op-toggle]').forEach(function (head) {
    function toggle(ev) {
      if (ev.target.closest('button')) return;
      var id = head.getAttribute('data-op-toggle');
      state.orgOpenOperators = state.orgOpenOperators || {};
      state.orgOpenOperators[id] = !state.orgOpenOperators[id];
      var y = window.scrollY;
      navigate('orgs').then(function () { window.scrollTo(0, y); });
    }
    head.addEventListener('click', toggle);
    head.addEventListener('keydown', function (ev) { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); toggle(ev); } });
  });
  content.querySelectorAll('tr.op-pilot').forEach(function (row) {
    row.onclick = function () {
      state.pilotDetailId = row.getAttribute('data-pilot');
      navigate('pilots');
    };
  });

  function showMsg(html) {
    var m = document.getElementById('org-msg');
    if (m) m.innerHTML = html;
  }
  // Klikateľné štatistiky útvaru
  content.querySelectorAll('[data-stat]').forEach(function (btn) {
    btn.onclick = function () {
      if (!selected) return;
      var scopeIds = [selected.id].concat(descendants(selected.id).map(function (o) { return o.id; }));
      var subCount = descendants(selected.id).filter(function (o) { return o.isActive !== false; }).length;
      var kind = btn.dataset.stat;
      if (kind === 'pilots') {
        state.pilotOrgFilter = { name: selected.name, ids: scopeIds, withSubs: subCount > 0 };
        state.pilotRegisterTab = 'all';
        state.pilotDetailId = null;
        navigate('pilots');
      } else if (kind === 'hours') {
        state.flightOrgFilter = { name: selected.name, ids: scopeIds, withSubs: subCount > 0 };
        state.flightDetailId = null;
        navigate('flights');
      } else if (kind === 'ops') {
        var sec = document.getElementById('ot-ops-section');
        if (sec) {
          sec.scrollIntoView({ behavior: 'smooth', block: 'start' });
          sec.classList.remove('ot-flash'); void sec.offsetWidth; sec.classList.add('ot-flash');
        }
      } else if (kind === 'units') {
        var subs = descendants(selected.id).filter(function (o) { return o.isActive !== false; });
        openListModal({
          title: 'Podriadené útvary',
          subtitle: selected.name,
          items: subs.map(function (o) {
            var ps = pilotsUnder(o.id);
            return {
              id: o.id,
              title: o.name,
              sub: typeLabel(o) + (o.code ? ' · ' + o.code : ''),
              depth: pathOf(o).length - pathOf(selected).length - 1,
              right: '<b>' + ps.length + '</b> pilotov · <b>' + fmtHours(hoursOf(ps)) + ' h</b>'
            };
          }),
          onPick: function (id) { select(id); }
        });
      }
    };
  });

  // Úprava údajov používateľa (meno, priezvisko, e-mail, login)
  function refreshKeepScroll() {
    var y = window.scrollY;
    return navigate('orgs').then(function () { window.scrollTo(0, y); });
  }
  function editAccount(acc) {
    return openUserEditor(acc, { canEditLogin: state.accessLevel === 1 }).then(function (saved) {
      if (saved) return refreshKeepScroll();
    });
  }
  content.querySelectorAll('[data-edit-user]').forEach(function (btn) {
    btn.onclick = function (ev) {
      ev.stopPropagation();
      var acc = accounts.find(function (a) { return a.id === btn.dataset.editUser; });
      if (acc) editAccount(acc);
    };
  });
  // Profil prevádzkovateľa s grafmi
  content.querySelectorAll('[data-profile]').forEach(function (btn) {
    btn.onclick = function (ev) {
      ev.stopPropagation();
      var op = accounts.find(function (a) { return a.id === btn.dataset.profile; });
      if (!op) return;
      openOperatorProfile({
        op: op, pilots: pilotsOfOperator(op), flights: flights,
        hoursByPilot: hoursByPilot, flightsByPilot: flightsByPilot,
        canEdit: canEditAccount(op),
        onEdit: editAccount,
        onOpenPilot: function (pid) { state.pilotDetailId = pid; navigate('pilots'); }
      });
    };
  });

  // Rozkliknutie pilota v „Ďalší používatelia útvaru“ a zmena nadriadeného
  content.querySelectorAll('[data-user-toggle]').forEach(function (rowEl) {
    function toggleUser(ev) {
      if (ev.target.closest('[data-remove], [data-restore], button, select')) return;
      var id = rowEl.getAttribute('data-user-toggle');
      state.orgOpenUsers = state.orgOpenUsers || {};
      var nowOpen = !state.orgOpenUsers[id];
      state.orgOpenUsers[id] = nowOpen;
      // Rozbalenie priamo v stránke — bez nového načítania.
      rowEl.classList.toggle('is-open', nowOpen);
      rowEl.setAttribute('aria-expanded', String(nowOpen));
      var det = content.querySelector('[data-user-detail="' + id + '"]');
      if (det) det.hidden = !nowOpen;
    }
    rowEl.addEventListener('click', toggleUser);
    rowEl.addEventListener('keydown', function (ev) { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); toggleUser(ev); } });
  });
  content.querySelectorAll('.user-op-select').forEach(function (sel) {
    var pilot = pilots.find(function (p) { return p.id === sel.dataset.pilot; });
    var btn = content.querySelector('.user-op-save[data-pilot="' + sel.dataset.pilot + '"]');
    sel.onchange = function () { btn.disabled = (sel.value || '') === ((pilot && pilot.operatorAccountId) || ''); };
    btn.onclick = async function () {
      var msgEl = content.querySelector('.user-op-msg[data-pilot="' + sel.dataset.pilot + '"]');
      btn.disabled = true;
      try {
        await api('/api/v1/pilots/' + sel.dataset.pilot + '/operator', { method: 'PUT', body: JSON.stringify({ operatorAccountId: sel.value || null }) });
        var y = window.scrollY;
        navigate('orgs').then(function () { window.scrollTo(0, y); });
      } catch (err) {
        btn.disabled = false;
        if (msgEl) msgEl.innerHTML = '<span class="txt-danger">' + esc(err.message || 'Zmena zlyhala') + '</span>';
      }
    };
  });
  content.querySelectorAll('[data-open-pilot]').forEach(function (btn) {
    btn.onclick = function () { state.pilotDetailId = btn.dataset.openPilot; navigate('pilots'); };
  });

  content.querySelectorAll('[data-remove]').forEach(function (btn) {
    btn.onclick = async function (ev) {
      ev.stopPropagation();
      var acc = accounts.find(function (a) { return a.id === btn.getAttribute('data-remove'); });
      if (!acc) return;
      try {
        if (await removeAccountFlow(acc)) await navigate('orgs');
      } catch (err) { showMsg('<div class="error">' + esc(err.message || 'Odstránenie zlyhalo') + '</div>'); }
    };
  });
  content.querySelectorAll('[data-restore]').forEach(function (btn) {
    btn.onclick = async function (ev) {
      ev.stopPropagation();
      var acc = accounts.find(function (a) { return a.id === btn.getAttribute('data-restore'); });
      if (!acc) return;
      try {
        if (await restoreAccountFlow(acc)) await navigate('orgs');
      } catch (err) { showMsg('<div class="error">' + esc(err.message || 'Obnovenie zlyhalo') + '</div>'); }
    };
  });

  if (!selected) return;

  // formulár útvaru
  // Nadradený útvar sa ponúka len zo zvolenej zložky (PZ / HaZZ / CO) — útvar nemôže byť napr. typu HaZZ pod Policajným zborom.
  function branchNode(branch) {
    return items.find(function (o) { return kindKey(o) === branch && o.isActive !== false; }) || null;
  }
  function parentOptionsHtml(excludeId, branch) {
    var ex = excludeId ? descendants(excludeId).map(function (o) { return o.id; }).concat([excludeId]) : [];
    return items.filter(function (o) {
        return ex.indexOf(o.id) < 0 && o.isActive !== false && kindKey(o) !== 'root' && branchOf(o) === branch;
      })
        .sort(function (a, b) { return String(a.path || '').localeCompare(String(b.path || '')); })
        .map(function (o) {
          return '<option value="' + o.id + '">' + Array((o.level || 0) + 1).join('   ') + esc(o.name) + '</option>';
        }).join('');
  }
  function openOrgForm(mode, org, parentId) {
    document.getElementById('org-user-panel').classList.add('hidden');
    var panel = document.getElementById('org-panel');
    var codeEl = document.getElementById('org-code');
    var parentEl = document.getElementById('org-parent');
    showMsg('');
    if (mode === 'edit') {
      document.getElementById('org-panel-title').textContent = 'Upraviť útvar';
      document.getElementById('org-submit').textContent = 'Uložiť zmeny útvaru';
      document.getElementById('org-id').value = org.id;
      document.getElementById('org-rv').value = String(org.rowVersion);
      document.getElementById('org-orig-parent').value = org.parentId || '';
      codeEl.value = org.code; codeEl.readOnly = true;
      document.getElementById('org-name').value = org.name;
      var kindEl = document.getElementById('org-kind');
      var orgBranch = branchOf(org);
      kindEl.value = orgBranch;
      kindEl.disabled = true;
      if (kindKey(org) === 'unit' && ['pz', 'hazz', 'co'].indexOf(orgBranch) >= 0) {
        parentEl.innerHTML = parentOptionsHtml(org.id, orgBranch);
        parentEl.disabled = false;
      } else {
        // zložka / koreň sa nepresúva
        var cur = org.parentId ? byId[org.parentId] : null;
        parentEl.innerHTML = '<option value="' + (cur ? cur.id : '') + '">' + (cur ? esc(cur.name) : '— bez nadradeného —') + '</option>';
        parentEl.disabled = true;
      }
      parentEl.value = org.parentId || '';
    } else {
      document.getElementById('org-panel-title').textContent = parentId && byId[parentId]
        ? 'Nový podútvar pod: ' + byId[parentId].name : 'Nový útvar';
      document.getElementById('org-submit').textContent = 'Vytvoriť útvar';
      document.getElementById('org-id').value = '';
      document.getElementById('org-rv').value = '';
      document.getElementById('org-orig-parent').value = '';
      codeEl.value = ''; codeEl.readOnly = false;
      document.getElementById('org-name').value = '';
      var kindSel = document.getElementById('org-kind');
      kindSel.disabled = false;
      parentEl.disabled = false;
      var startParent = parentId ? byId[parentId] : null;
      var startBranch = startParent ? branchOf(startParent) : 'pz';
      if (['pz', 'hazz', 'co'].indexOf(startBranch) < 0) startBranch = 'pz';
      kindSel.value = startBranch;
      fillParentsForBranch(startBranch, startParent && branchOf(startParent) === startBranch ? startParent.id : null);
    }
    panel.classList.remove('hidden');
    panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    document.getElementById('org-name').focus();
  }
  function fillParentsForBranch(branch, preferId) {
    var parentEl = document.getElementById('org-parent');
    parentEl.innerHTML = parentOptionsHtml(null, branch);
    var bn = branchNode(branch);
    var want = preferId || (bn ? bn.id : '');
    if (want && parentEl.querySelector('option[value="' + want + '"]')) parentEl.value = want;
  }
  var kindSelEl = document.getElementById('org-kind');
  if (kindSelEl) kindSelEl.addEventListener('change', function () {
    if (document.getElementById('org-id').value) return; // pri úprave sa zložka nemení
    var parentEl = document.getElementById('org-parent');
    var cur = parentEl.value && byId[parentEl.value];
    fillParentsForBranch(kindSelEl.value, cur && branchOf(cur) === kindSelEl.value ? cur.id : null);
  });
  function openUserForm(level) {
    document.getElementById('org-panel').classList.add('hidden');
    var panel = document.getElementById('org-user-panel');
    ['org-user-title', 'org-user-first', 'org-user-last', 'org-user-login', 'org-user-email'].forEach(function (id) { document.getElementById(id).value = ''; });
    loginTouched = false;
    setLoginHint('', '');
    var lvl = document.getElementById('org-user-level');
    if (lvl && state.accessLevel === 1) lvl.value = level ? String(level) : '';
    syncUserNameFields();
    var res = document.getElementById('org-user-result');
    res.classList.add('hidden'); res.innerHTML = '';
    document.getElementById('org-user-form').classList.remove('hidden');
    panel.classList.remove('hidden');
    panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    if (document.getElementById('org-user-level').value)
      document.getElementById(isOperatorForm() ? 'org-user-title' : 'org-user-first').focus();
  }
  // ---------- automatický login z mena / názvu (dá sa prepísať; server overí, že je voľný) ----------
  var loginTouched = false, loginTimer = null, loginSeq = 0;
  function loginBase(text) {
    return String(text || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]+/g, '.').replace(/^\.+|\.+$/g, '').replace(/\.{2,}/g, '.').slice(0, 40).replace(/\.+$/, '');
  }
  function setLoginHint(kind, text) {
    var h = document.getElementById('org-login-hint');
    if (!h) return;
    h.className = 'login-hint' + (kind ? ' is-' + kind : '');
    h.textContent = text || '';
  }
  function proposedBase() {
    if (isOperatorForm()) return loginBase(document.getElementById('org-user-title').value);
    var first = document.getElementById('org-user-first').value, last = document.getElementById('org-user-last').value;
    return loginBase([first, last].filter(function (x) { return String(x || '').trim(); }).join(' '));
  }
  function checkLogin(base, fromName) {
    clearTimeout(loginTimer);
    if (!base) { if (fromName) document.getElementById('org-user-login').value = ''; setLoginHint('', ''); return; }
    var seq = ++loginSeq;
    if (fromName) {
      // Okamžite vyplň (aj keď server ešte neodpovedal); kolízie s účtami, ktoré stránka pozná, rieš hneď.
      var known = {};
      (accounts || []).forEach(function (a) { known[String(a.userName || '').toLowerCase()] = true; });
      var cand = base, n = 2;
      while (known[cand]) cand = base + (n++);
      document.getElementById('org-user-login').value = cand;
    }
    setLoginHint('busy', 'Overujem, či je login voľný…');
    loginTimer = setTimeout(async function () {
      try {
        var r = await api('/api/v1/accounts/username-suggestion?base=' + encodeURIComponent(base));
        if (seq !== loginSeq) return;
        var input = document.getElementById('org-user-login');
        if (fromName) {
          input.value = r.userName;
          setLoginHint('ok', r.baseAvailable ? '✓ Login je voľný' : '✓ „' + base + '“ už existuje, použije sa „' + r.userName + '“');
        } else if (!r.isValid) {
          setLoginHint('err', 'Login môže obsahovať len písmená bez diakritiky, číslice, bodku a pomlčku.');
        } else if (r.baseAvailable && r.userName === input.value.trim().toLowerCase()) {
          setLoginHint('ok', '✓ Login je voľný');
        } else if (r.baseAvailable) {
          setLoginHint('warn', 'Uloží sa ako „' + r.userName + '“ (bez diakritiky a medzier).');
        } else {
          setLoginHint('err', '✗ Login je obsadený — voľný je napr. „' + r.userName + '“');
        }
      } catch (_) { if (seq === loginSeq) setLoginHint('', fromName ? 'Login sa overí pri uložení.' : ''); }
    }, 350);
  }
  function onNameInput() { if (!loginTouched) checkLogin(proposedBase(), true); }
  ['org-user-first', 'org-user-last', 'org-user-title'].forEach(function (id) {
    var el = document.getElementById(id);
    if (el) el.addEventListener('input', onNameInput);
  });
  var loginInput = document.getElementById('org-user-login');
  if (loginInput) loginInput.addEventListener('input', function () {
    loginTouched = loginInput.value.trim() !== '';   // vymazaním sa vráti automatické vypĺňanie
    if (loginTouched) checkLogin(loginInput.value.trim(), false);
    else checkLogin(proposedBase(), true);
  });

  // Úroveň 2 (prevádzkovateľ) má len názov, meno a priezvisko iba úroveň 3 (pilot).
  function isOperatorForm() {
    var lvl = document.getElementById('org-user-level');
    return !!lvl && Number(lvl.value) === 2;
  }
  // ---------- nadriadený prevádzkovateľ pilota ----------
  var operatorOptions = null;
  async function loadOperatorOptions() {
    var sel = document.getElementById('org-user-operator');
    var hint = document.getElementById('org-user-operator-hint');
    if (!sel || !selected) return;
    if (operatorOptions === null) {
      try { operatorOptions = await api('/api/v1/pilots/operator-options?orgUnitId=' + encodeURIComponent(selected.id)); }
      catch (_) { operatorOptions = []; }
    }
    if (document.getElementById('org-user-level').value !== '3') { sel.required = false; return; }
    if (!operatorOptions.length) {
      sel.innerHTML = '<option value="">— v útvare zatiaľ nie je prevádzkovateľ —</option>';
      sel.required = false;
      hint.className = 'login-hint is-warn';
      hint.textContent = 'Pilot bude bez prevádzkovateľa; priradíte ho neskôr v jeho karte (Register PnD).';
      return;
    }
    var mine = operatorOptions.find(function (o) { return o.accountId === state.accountId; });
    var preselect = mine ? mine.accountId : (operatorOptions.length === 1 ? operatorOptions[0].accountId : '');
    sel.innerHTML = (preselect ? '' : '<option value="">— vyberte prevádzkovateľa —</option>') + operatorOptions.map(function (o) {
      return '<option value="' + esc(o.accountId) + '"' + (o.accountId === preselect ? ' selected' : '') + '>' +
        esc(o.name) + (o.orgUnitName ? ' · ' + esc(o.orgUnitName) : '') + ' (' + o.pilotCount + ' pilotov)</option>';
    }).join('');
    sel.required = true;
    hint.className = 'login-hint';
    hint.textContent = 'Pilot bude mať práve jedného nadriadeného. Zmeniť ho môžete neskôr v karte pilota.';
  }

  function syncUserNameFields() {
    var op = isOperatorForm();
    var opWrap = document.getElementById('org-user-operator-wrap');
    var lvlNow = document.getElementById('org-user-level').value;
    if (opWrap) {
      opWrap.classList.toggle('hidden', lvlNow !== '3');
      if (lvlNow === '3') loadOperatorOptions();
      else document.getElementById('org-user-operator').required = false;
    }
    var lvlVal = document.getElementById('org-user-level').value;
    var form = document.getElementById('org-user-form');
    form.classList.toggle('no-level', !lvlVal);
    var submitBtn = form.querySelector('button[type="submit"]');
    if (submitBtn) submitBtn.disabled = !lvlVal;
    content.querySelectorAll('.level-card').forEach(function (c) {
      var on = c.dataset.level === document.getElementById('org-user-level').value;
      c.classList.toggle('is-on', on);
      c.setAttribute('aria-checked', String(on));
    });
    document.getElementById('org-user-title-wrap').classList.toggle('hidden', !op);
    document.getElementById('org-user-first-wrap').classList.toggle('hidden', op);
    document.getElementById('org-user-last-wrap').classList.toggle('hidden', op);
    document.getElementById('org-user-title').required = op;
    document.getElementById('org-user-first').required = !op;
    document.getElementById('org-user-last').required = !op;
    if (typeof loginTouched !== 'undefined' && !loginTouched) checkLogin(proposedBase(), true);
  }
  content.querySelectorAll('.level-card').forEach(function (card) {
    card.addEventListener('click', function () {
      document.getElementById('org-user-level').value = card.dataset.level;
      syncUserNameFields();
      document.getElementById(isOperatorForm() ? 'org-user-title' : 'org-user-first').focus();
    });
  });
  var b;
  // Ponuka „Spravovať útvar“
  var unitMenuBtn = document.getElementById('org-unit-menu-btn');
  var unitMenu = document.getElementById('org-unit-menu');
  if (unitMenuBtn && unitMenu) {
    var closeUnitMenu = function () {
      unitMenu.classList.add('hidden');
      unitMenuBtn.setAttribute('aria-expanded', 'false');
      document.removeEventListener('click', onDocClick, true);
    };
    var onDocClick = function (ev) {
      if (!ev.target.closest('.ot-unit-menu') || ev.target.closest('.ot-unit-menu-list button')) closeUnitMenu();
    };
    unitMenuBtn.onclick = function () {
      var open = unitMenu.classList.contains('hidden');
      if (!open) { closeUnitMenu(); return; }
      unitMenu.classList.remove('hidden');
      unitMenuBtn.setAttribute('aria-expanded', 'true');
      setTimeout(function () { document.addEventListener('click', onDocClick, true); }, 0);
    };
    unitMenu.addEventListener('keydown', function (ev) { if (ev.key === 'Escape') { closeUnitMenu(); unitMenuBtn.focus(); } });
  }
  if ((b = document.getElementById('org-add-btn'))) b.onclick = function () { openOrgForm('create', null, null); };
  if ((b = document.getElementById('org-add-child'))) b.onclick = function () { openOrgForm('create', null, selected.id); };
  if ((b = document.getElementById('org-edit'))) b.onclick = function () { openOrgForm('edit', selected); };
  if ((b = document.getElementById('org-add-user'))) b.onclick = function () { openUserForm(state.accessLevel === 1 ? null : 3); };
  if ((b = document.getElementById('org-add-operator'))) b.onclick = function () { openUserForm(2); };
  if (state.orgLastCreated) {
    var lc = state.orgLastCreated;
    state.orgLastCreated = null;
    var resBox = document.getElementById('org-user-result');
    var userPanel = document.getElementById('org-user-panel');
    if (resBox && userPanel) {
      document.getElementById('org-user-form').classList.add('hidden');
      userPanel.classList.remove('hidden');
      resBox.classList.remove('hidden');
      resBox.innerHTML = '<div class="created-box">' +
        '<b>✓ Účet vytvorený — používateľ je už v zozname útvaru</b>' +
        '<div class="created-grid"><span>Login</span><code>' + esc(lc.userName) + '</code>' +
        '<span>Heslo</span><code>' + esc(lc.password) + '</code></div>' +
        '<p class="muted">Heslo sa zobrazí iba teraz — odovzdajte ho používateľovi.</p>' +
        '<div class="toolbar"><button type="button" class="secondary" id="org-copy-pw">Kopírovať heslo</button>' +
        '<button type="button" class="secondary" id="org-user-another">+ Pridať ďalšieho</button>' +
        '<button type="button" id="org-user-done">Hotovo</button></div>' +
        (lc.level === 3 ? '<p class="muted">Osvedčenie pilotovi pridelíte v menu <b>Osvedčenia</b> (nie je povinné).</p>' : '') +
        '</div>';
      document.getElementById('org-copy-pw').onclick = function () {
        var btn = this;
        copyText(lc.password).then(function () { btn.textContent = 'Skopírované ✓'; }).catch(function () { btn.textContent = 'Nepodarilo sa skopírovať'; });
      };
      document.getElementById('org-user-done').onclick = function () {
        resBox.classList.add('hidden'); resBox.innerHTML = '';
        userPanel.classList.add('hidden');
      };
      document.getElementById('org-user-another').onclick = function () {
        resBox.classList.add('hidden'); resBox.innerHTML = '';
        openUserForm(state.accessLevel === 1 ? lc.level : 3);
      };
      userPanel.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }
  if (state.orgOpenUserForm && canManageUsers && selected.isActive !== false) {
    var lvlWanted = state.orgOpenUserForm;
    state.orgOpenUserForm = null;
    openUserForm(state.accessLevel === 1 ? lvlWanted : 3);
    showMsg('<div class="msg">Vyberte v strome útvar, ku ktorému nový používateľ patrí, a vyplňte formulár.</div>');
  }
  document.getElementById('org-cancel').onclick = function () { document.getElementById('org-panel').classList.add('hidden'); };
  document.getElementById('org-user-cancel').onclick = function () { document.getElementById('org-user-panel').classList.add('hidden'); };

  if ((b = document.getElementById('org-deactivate'))) {
    b.onclick = async function () {
      var ok = await confirmDialog({
        title: 'Vymazať útvar?',
        message: '<p>Útvar <b>' + esc(selected.name) + '</b> sa odstráni zo stromu. Ak ste ho vymazali omylom, obnovíte ho cez zaškrtávacie pole „Neaktívne útvary“ a tlačidlo „Aktivovať útvar“.</p>',
        confirmLabel: 'Vymazať',
        danger: true
      });
      if (!ok) return;
      try {
        await api('/api/v1/org-units/' + selected.id, { method: 'DELETE' });
        state.orgSelectedId = selected.parentId || null;
        navigate('orgs');
      } catch (err) { showMsg('<div class="error">' + esc(err.message || 'Vymazanie zlyhalo') + '</div>'); }
    };
  }
  if ((b = document.getElementById('org-reactivate'))) {
    b.onclick = async function () {
      try {
        await api('/api/v1/org-units/' + selected.id + '/reactivate', { method: 'POST', body: '{}' });
        navigate('orgs');
      } catch (err) { showMsg('<div class="error">' + esc(err.message) + '</div>'); }
    };
  }

  document.getElementById('org-form').onsubmit = async function (ev) {
    ev.preventDefault();
    var id = document.getElementById('org-id').value;
    var name = document.getElementById('org-name').value.trim();
    var parentVal = document.getElementById('org-parent').value || null;
    try {
      if (!name) throw new Error('Vyplňte názov');
      if (!id) {
        var code = document.getElementById('org-code').value.trim();
        if (!code) throw new Error('Vyplňte kód');
        var chosenBranch = document.getElementById('org-kind').value;
        var parentNode = parentVal ? byId[parentVal] : null;
        if (!parentNode) throw new Error('Vyberte nadradený útvar');
        if (branchOf(parentNode) !== chosenBranch) throw new Error('Nadradený útvar musí patriť do zvolenej zložky');
        var created = await api('/api/v1/org-units', {
          method: 'POST',
          body: JSON.stringify({ code: code, name: name, parentId: parentVal, kind: 4 })
        });
        if (created && created.id) state.orgSelectedId = created.id;
      } else {
        var updated = await api('/api/v1/org-units/' + id, {
          method: 'PATCH',
          body: JSON.stringify({ name: name, rowVersion: Number(document.getElementById('org-rv').value) })
        });
        var origParent = document.getElementById('org-orig-parent').value || null;
        if ((parentVal || null) !== (origParent || null)) {
          await api('/api/v1/org-units/' + id + '/move', {
            method: 'POST',
            body: JSON.stringify({ newParentId: parentVal, reason: 'Úprava z UI', rowVersion: updated.rowVersion })
          });
        }
      }
      navigate('orgs');
    } catch (err) {
      showMsg('<div class="error">' + esc(err.message || 'Uloženie zlyhalo') + '</div>');
    }
  };

  document.getElementById('org-user-form').onsubmit = async function (ev) {
    ev.preventDefault();
    var result = document.getElementById('org-user-result');
    var level = Number(document.getElementById('org-user-level').value);
    var isOp = level === 2;
    if (!level) { showMsg('<div class="error">Najprv vyberte, či vytvárate pilota alebo prevádzkovateľa.</div>'); return; }
    try {
      if (isOp && !document.getElementById('org-user-title').value.trim()) throw new Error('Vyplňte názov prevádzkovateľa');
      var created = await api('/api/v1/accounts', {
        method: 'POST',
        body: JSON.stringify({
          userName: document.getElementById('org-user-login').value.trim(),
          operatorAccountId: level === 3 ? (document.getElementById('org-user-operator').value || null) : null,
          firstName: isOp ? document.getElementById('org-user-title').value.trim() : document.getElementById('org-user-first').value.trim(),
          lastName: isOp ? '' : document.getElementById('org-user-last').value.trim(),
          email: document.getElementById('org-user-email').value.trim() || null,
          homeOrgUnitId: selected.id,
          accessLevel: level
        })
      });
      // Stránku hneď prekreslíme (nový používateľ sa objaví v zozname) a výsledok s heslom ukážeme znova.
      state.orgLastCreated = { userName: created.account.userName, password: created.plainPassword || '', level: level };
      navigate('orgs');
    } catch (err) {
      showMsg('<div class="error">' + esc(err.message || 'Vytvorenie zlyhalo') + '</div>');
    }
  };
}
