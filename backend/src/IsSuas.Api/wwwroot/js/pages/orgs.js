import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
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
  // Pilot patrí prevádzkovateľovi, ak je v útvare prevádzkovateľa alebo pod ním.
  function pilotsOfOperator(op) {
    return op.homeOrgUnitId ? pilotsUnder(op.homeOrgUnitId) : [];
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
    return '<div class="person-row' + (removed ? ' is-removed' : '') + '">' +
      avatarHtml(a.firstName, a.lastName, a.userName) +
      '<div class="person-main"><b>' + esc((a.firstName || '') + ' ' + (a.lastName || '')) + '</b>' +
      '<span>@' + esc(a.userName) + ' · ' + esc(a.homeOrgUnitName || '—') + '</span></div>' +
      '<span class="lvl-pill lvl-' + lvl + '">' + (ACCESS_LEVEL_LABELS[lvl] || '—') + '</span>' +
      (removed ? '<span class="badge badge-danger">Odstránený</span>' : '') +
      '<div class="person-actions">' + actions + '</div></div>';
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
      removeBtn +
      '<span class="op-chev">' + ICO.chev + '</span>' +
      '</div>' +
      (open
        ? '<div class="op-card-body">' +
          (ps.length
            ? '<div class="op-card-grid"><div><h4>Nalietané hodiny pilotov</h4>' + hbarList(top, 'h') + '</div>' +
              '<div class="op-card-table"><table><thead><tr><th>Pilot</th><th>Útvar</th><th>Osvedčenia</th><th class="num">Lety</th><th class="num">Nálet</th></tr></thead><tbody>' +
              pilotRows + '</tbody></table></div></div>'
            : '<p class="muted ot-nodata">Prevádzkovateľ zatiaľ nemá žiadnych pilotov vo svojom útvare.</p>') +
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
    var orgActions = '';
    if (canManageOrgs) {
      orgActions = selected.isActive === false
        ? '<button type="button" class="secondary" id="org-reactivate">' + ICO.restore + ' Aktivovať útvar</button>'
        : '<button type="button" class="secondary" id="org-add-child">' + ICO.plus + ' Podútvar</button>' +
          '<button type="button" class="secondary" id="org-edit">' + ICO.edit + ' Upraviť</button>' +
          (kindKey(selected) === 'root' ? '' : '<button type="button" class="secondary danger-ghost" id="org-deactivate">' + ICO.trash + ' Vymazať útvar</button>');
    }
    if (canManageUsers && selected.isActive !== false) {
      orgActions += '<button type="button" id="org-add-user">' + ICO.user + ' Pridať používateľa</button>';
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
      '<div class="ot-stat"><span class="ot-stat-ico">' + ICO.units + '</span><b>' + subUnits.length + '</b><span>podriadených útvarov</span></div>' +
      '<div class="ot-stat"><span class="ot-stat-ico">' + ICO.ops + '</span><b>' + selOps.length + '</b><span>prevádzkovateľov</span></div>' +
      '<div class="ot-stat"><span class="ot-stat-ico">' + ICO.pilot + '</span><b>' + selPilots.length + '</b><span>pilotov</span></div>' +
      '<div class="ot-stat"><span class="ot-stat-ico">' + ICO.clock + '</span><b>' + fmtHours(selHours) + ' h</b><span>nalietaných hodín</span></div>' +
      '</div>' +
      '<div id="org-msg"></div>' +
      formsHtml() +
      '<section class="ot-section"><div class="ot-section-head"><h3>Nalietané hodiny ' + (opsChart.length ? 'podľa prevádzkovateľov' : 'podľa útvarov') + '</h3></div>' +
      hbarList(opsChart.length ? opsChart : unitChart, 'h') + '</section>' +
      '<section class="ot-section"><div class="ot-section-head"><h3>Prevádzkovatelia <span class="tab-count">' + selOps.length + '</span></h3>' +
      '<p class="muted">Kliknite na prevádzkovateľa — zobrazia sa jeho piloti a ich nálet.</p></div>' +
      (opsSorted.length ? '<div class="op-list">' + opsSorted.map(operatorCard).join('') + '</div>'
        : '<div class="ot-empty-box"><p>V tomto útvare zatiaľ nie je žiadny prevádzkovateľ.</p>' +
          (canManageUsers && state.accessLevel === 1 && selected.isActive !== false ? '<button type="button" class="secondary" id="org-add-operator">' + ICO.user + ' Pridať prevádzkovateľa</button>' : '') + '</div>') +
      '</section>' +
      (otherUsers.length
        ? '<section class="ot-section"><div class="ot-section-head"><h3>Ďalší používatelia útvaru <span class="tab-count">' + otherUsers.length + '</span></h3></div>' +
          '<div class="person-list">' + otherUsers.map(userRow).join('') + '</div></section>'
        : '');
  }

  function formsHtml() {
    return '<div id="org-panel" class="form-panel hidden">' +
      '<h3 id="org-panel-title">Nový útvar</h3>' +
      '<form id="org-form" class="form-grid">' +
      '<input type="hidden" id="org-id" value="" /><input type="hidden" id="org-rv" value="" /><input type="hidden" id="org-orig-parent" value="" />' +
      '<label>Názov<input id="org-name" required placeholder="napr. Okresné riaditeľstvo Bratislava I" maxlength="200" /></label>' +
      '<label>Kód<input id="org-code" required placeholder="napr. OR-BA-1" maxlength="64" /></label>' +
      '<label>Zložka<select id="org-kind"><option value="pz">Policajný zbor</option><option value="hazz">Hasičský a záchranný zbor</option><option value="co">Civilná ochrana</option></select></label>' +
      '<label>Nadradený útvar<select id="org-parent"></select></label>' +
      '<div class="full toolbar"><button type="submit">Uložiť</button><button type="button" class="secondary" id="org-cancel">Zrušiť</button></div>' +
      '</form></div>' +
      '<div id="org-user-panel" class="form-panel hidden">' +
      '<h3>Nový používateľ</h3>' +
      '<p class="muted" style="margin:-0.4rem 0 0">Útvar: <b>' + (selected ? esc(selected.name) : '') + '</b>. Heslo sa vygeneruje automaticky.</p>' +
      '<form id="org-user-form" class="form-grid">' +
      '<label class="full hidden" id="org-user-title-wrap">Názov prevádzkovateľa<input id="org-user-title" autocomplete="off" maxlength="128" placeholder="napr. Letka PZ Bratislava" /></label>' +
      '<label id="org-user-first-wrap">Meno<input id="org-user-first" required autocomplete="off" /></label>' +
      '<label id="org-user-last-wrap">Priezvisko<input id="org-user-last" required autocomplete="off" /></label>' +
      '<label>Login<input id="org-user-login" required autocomplete="off" placeholder="napr. janko.novak" /></label>' +
      '<label>E-mail<input id="org-user-email" type="email" placeholder="voliteľné" /></label>' +
      (state.accessLevel === 1
        ? '<label class="full">Úroveň práv<select id="org-user-level"><option value="2">Prevádzkovateľ (úroveň 2)</option><option value="3">Pilot (úroveň 3)</option></select></label>'
        : '<input type="hidden" id="org-user-level" value="3" /><label class="full">Úroveň práv<input value="Pilot (úroveň 3)" readonly /></label>') +
      '<div class="full toolbar"><button type="submit">Vytvoriť a vygenerovať heslo</button><button type="button" class="secondary" id="org-user-cancel">Zrušiť</button></div>' +
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
    (canManageOrgs ? '<button type="button" class="secondary" id="org-add-btn">' + ICO.plus + ' Nový útvar</button>' : '') +
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
      if (ev.target.closest('[data-remove], [data-restore]')) return;
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
      document.getElementById('org-panel-title').textContent = parentId ? 'Nový podútvar' : 'Nový útvar';
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
    var lvl = document.getElementById('org-user-level');
    if (lvl && lvl.tagName === 'SELECT') lvl.value = String(level || 2);
    syncUserNameFields();
    var res = document.getElementById('org-user-result');
    res.classList.add('hidden'); res.innerHTML = '';
    document.getElementById('org-user-form').classList.remove('hidden');
    panel.classList.remove('hidden');
    panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    document.getElementById(isOperatorForm() ? 'org-user-title' : 'org-user-first').focus();
  }
  // Úroveň 2 (prevádzkovateľ) má len názov, meno a priezvisko iba úroveň 3 (pilot).
  function isOperatorForm() {
    var lvl = document.getElementById('org-user-level');
    return !!lvl && Number(lvl.value) === 2;
  }
  function syncUserNameFields() {
    var op = isOperatorForm();
    document.getElementById('org-user-title-wrap').classList.toggle('hidden', !op);
    document.getElementById('org-user-first-wrap').classList.toggle('hidden', op);
    document.getElementById('org-user-last-wrap').classList.toggle('hidden', op);
    document.getElementById('org-user-title').required = op;
    document.getElementById('org-user-first').required = !op;
    document.getElementById('org-user-last').required = !op;
  }
  var lvlSel = document.getElementById('org-user-level');
  if (lvlSel && lvlSel.tagName === 'SELECT') lvlSel.addEventListener('change', syncUserNameFields);
  var b;
  if ((b = document.getElementById('org-add-btn'))) b.onclick = function () { openOrgForm('create', null, null); };
  if ((b = document.getElementById('org-add-child'))) b.onclick = function () { openOrgForm('create', null, selected.id); };
  if ((b = document.getElementById('org-edit'))) b.onclick = function () { openOrgForm('edit', selected); };
  if ((b = document.getElementById('org-add-user'))) b.onclick = function () { openUserForm(state.accessLevel === 1 ? 2 : 3); };
  if ((b = document.getElementById('org-add-operator'))) b.onclick = function () { openUserForm(2); };
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
    try {
      if (isOp && !document.getElementById('org-user-title').value.trim()) throw new Error('Vyplňte názov prevádzkovateľa');
      var created = await api('/api/v1/accounts', {
        method: 'POST',
        body: JSON.stringify({
          userName: document.getElementById('org-user-login').value.trim(),
          firstName: isOp ? document.getElementById('org-user-title').value.trim() : document.getElementById('org-user-first').value.trim(),
          lastName: isOp ? '' : document.getElementById('org-user-last').value.trim(),
          email: document.getElementById('org-user-email').value.trim() || null,
          homeOrgUnitId: selected.id,
          accessLevel: level
        })
      });
      var pw = created.plainPassword || '';
      document.getElementById('org-user-form').classList.add('hidden');
      result.classList.remove('hidden');
      result.innerHTML = '<div class="created-box">' +
        '<b>Účet vytvorený</b>' +
        '<div class="created-grid"><span>Login</span><code>' + esc(created.account.userName) + '</code>' +
        '<span>Heslo</span><code>' + esc(pw) + '</code></div>' +
        '<p class="muted">Heslo sa zobrazí iba teraz — odovzdajte ho používateľovi.</p>' +
        '<div class="toolbar"><button type="button" class="secondary" id="org-copy-pw">Kopírovať heslo</button>' +
        '<button type="button" id="org-user-done">Hotovo</button></div>' +
        (level === 3 ? '<p class="muted">Osvedčenie pilotovi pridelíte v menu <b>Osvedčenia</b> (nie je povinné).</p>' : '') +
        '</div>';
      document.getElementById('org-copy-pw').onclick = function () {
        var btn = this;
        copyText(pw).then(function () { btn.textContent = 'Skopírované ✓'; }).catch(function () { btn.textContent = 'Nepodarilo sa skopírovať'; });
      };
      document.getElementById('org-user-done').onclick = function () { navigate('orgs'); };
    } catch (err) {
      showMsg('<div class="error">' + esc(err.message || 'Vytvorenie zlyhalo') + '</div>');
    }
  };
}
