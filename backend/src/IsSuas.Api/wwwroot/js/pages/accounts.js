import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import {
  accountLevel, avatarHtml, chipHtml, content, isAccountRemoved, pageHead,
  removeAccountFlow, restoreAccountFlow, searchHighlightBanner
} from '../core/ui.js';

// Prevádzkovatelia = používatelia úrovne 2. Každý je naviazaný na útvar v organizačnej
// štruktúre; jeho piloti sú piloti v tomto útvare a pod ním.

function esc(s) {
  return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function fmtHours(h) {
  return (Math.round(h * 10) / 10).toLocaleString('sk-SK', { maximumFractionDigits: 1 });
}

export async function renderAccounts() {
  const results = await Promise.all([
    api('/api/v1/accounts'),
    api('/api/v1/org-units').catch(function () { return []; }),
    api('/api/v1/pilots').catch(function () { return []; }),
    api('/api/v1/flights').catch(function () { return []; })
  ]);
  const accounts = results[0] || [];
  const orgs = results[1] || [];
  const pilots = (results[2] || []).filter(function (p) { return p.isActive !== false; });
  const flights = results[3] || [];
  const canManage = state.accessLevel === 1;

  const byId = {};
  orgs.forEach(function (o) { byId[o.id] = o; });
  function inSubtree(orgId, rootId) {
    var o = byId[orgId], r = byId[rootId];
    if (!o || !r) return orgId === rootId;
    return String(o.path || '').indexOf(String(r.path || '')) === 0;
  }
  function branchName(orgId) {
    var o = byId[orgId], guard = 0;
    while (o && guard++ < 30) {
      if (o.kind === 1 || o.kind === 'BranchPz') return 'Policajný zbor';
      if (o.kind === 2 || o.kind === 'BranchHazz') return 'HaZZ';
      if (o.kind === 3 || o.kind === 'BranchCo') return 'Civilná ochrana';
      o = o.parentId ? byId[o.parentId] : null;
    }
    return '—';
  }
  const hoursByPilot = {};
  flights.forEach(function (f) {
    if (f.pilotId && f.durationMinutes) hoursByPilot[f.pilotId] = (hoursByPilot[f.pilotId] || 0) + f.durationMinutes / 60;
  });

  const showRemoved = !!state.accountsShowRemoved;
  const q = String(state.accountsSearch || '').trim().toLowerCase();
  const allOps = accounts.filter(function (a) { return accountLevel(a) === 2; });
  const removedCount = allOps.filter(isAccountRemoved).length;
  const list = allOps.filter(function (a) {
    if (!showRemoved && isAccountRemoved(a)) return false;
    if (!q) return true;
    return [a.firstName, a.lastName, a.userName, a.email, a.homeOrgUnitName].join(' ').toLowerCase().indexOf(q) >= 0;
  }).map(function (a) {
    var ps = a.homeOrgUnitId ? pilots.filter(function (p) { return inSubtree(p.orgUnitId, a.homeOrgUnitId); }) : [];
    return { a: a, pilots: ps.length, hours: ps.reduce(function (s, p) { return s + (hoursByPilot[p.id] || 0); }, 0) };
  }).sort(function (x, y) { return y.hours - x.hours; });

  const rows = list.map(function (r) {
    var a = r.a, removed = isAccountRemoved(a), isMe = a.id === state.accountId;
    var actions = '<button type="button" class="icon-btn" data-open-org="' + (a.homeOrgUnitId || '') + '" data-op="' + a.id + '" title="Zobraziť v organizačnej štruktúre">' +
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-2h12v2"/></svg><span>Štruktúra</span></button>';
    if (canManage && !isMe) {
      actions += removed
        ? '<button type="button" class="icon-btn" data-restore="' + a.id + '"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/></svg><span>Obnoviť</span></button>'
        : '<button type="button" class="icon-btn danger" data-remove="' + a.id + '"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/></svg><span>Odstrániť</span></button>';
    }
    return '<tr' + (removed ? ' class="is-removed"' : '') + '>' +
      '<td><span class="cell-person">' + avatarHtml(a.firstName, a.lastName, a.userName) +
      '<span class="cell-person-text"><b>' + esc((a.firstName || '') + ' ' + (a.lastName || '')) + '</b><span>@' + esc(a.userName) + '</span></span></span></td>' +
      '<td>' + esc(a.homeOrgUnitName || '—') + '<div class="cell-sub">' + esc(branchName(a.homeOrgUnitId)) + '</div></td>' +
      '<td>' + esc(a.email || '—') + '</td>' +
      '<td class="num">' + r.pilots + '</td>' +
      '<td class="num"><b>' + fmtHours(r.hours) + ' h</b></td>' +
      '<td>' + (removed ? '<span class="badge badge-danger">Odstránený</span>' : '<span class="badge badge-ok">Aktívny</span>') + '</td>' +
      '<td><div class="row-actions-end">' + actions + '</div></td></tr>';
  }).join('') || '<tr><td colspan="7">' + (q ? 'Nič sa nenašlo.' : 'Zatiaľ žiadni prevádzkovatelia. Pridajte ich v organizačnej štruktúre.') + '</td></tr>';

  const activeCount = allOps.length - removedCount;
  content.innerHTML = '<div class="stack">' + searchHighlightBanner() +
    pageHead('Prevádzkovatelia',
      'Používatelia úrovne 2 — každý patrí k útvaru v organizačnej štruktúre a spravuje jeho pilotov.',
      chipHtml('Aktívni', activeCount, 'chip-ok') + (removedCount ? chipHtml('Odstránení', removedCount) : '')) +
    (canManage ? '<div class="toolbar"><button type="button" id="acc-add">+ Pridať prevádzkovateľa</button></div>' : '') +
    '<div id="acc-msg"></div>' +
    '<div class="list-search">' +
    '<input type="search" id="acc-search" placeholder="Hľadať meno, login, útvar…" value="' + esc(state.accountsSearch || '') + '" />' +
    '<label class="ot-check"><input type="checkbox" id="acc-show-removed"' + (showRemoved ? ' checked' : '') + ' /> Zobraziť odstránených</label>' +
    '</div>' +
    '<div class="chart-panel table-wide"><table><thead><tr><th>Prevádzkovateľ</th><th>Útvar</th><th>E-mail</th><th class="num">Piloti</th><th class="num">Nálet</th><th>Stav</th><th></th></tr></thead>' +
    '<tbody>' + rows + '</tbody></table></div>' +
    '</div>';

  var searchEl = document.getElementById('acc-search');
  var t = null;
  searchEl.addEventListener('input', function () {
    clearTimeout(t);
    t = setTimeout(function () {
      state.accountsSearch = searchEl.value;
      navigate('accounts').then(function () {
        var s = document.getElementById('acc-search');
        if (s) { s.focus(); s.setSelectionRange(s.value.length, s.value.length); }
      });
    }, 250);
  });
  document.getElementById('acc-show-removed').onchange = function () {
    state.accountsShowRemoved = this.checked;
    navigate('accounts');
  };
  var addBtn = document.getElementById('acc-add');
  if (addBtn) {
    addBtn.onclick = function () {
      // prevádzkovateľ sa zakladá priamo k útvaru v organizačnej štruktúre
      state.orgOpenUserForm = 2;
      navigate('orgs');
    };
  }
  content.querySelectorAll('[data-open-org]').forEach(function (btn) {
    btn.onclick = function () {
      var orgId = btn.getAttribute('data-open-org');
      if (orgId) state.orgSelectedId = orgId;
      state.orgOpenOperators = state.orgOpenOperators || {};
      state.orgOpenOperators[btn.getAttribute('data-op')] = true;
      navigate('orgs');
    };
  });
  function showErr(err) {
    document.getElementById('acc-msg').innerHTML = '<div class="error">' + esc(err.message || 'Akcia zlyhala') + '</div>';
  }
  content.querySelectorAll('[data-remove]').forEach(function (btn) {
    btn.onclick = async function () {
      var a = accounts.find(function (x) { return x.id === btn.getAttribute('data-remove'); });
      try { if (a && await removeAccountFlow(a)) navigate('accounts'); } catch (err) { showErr(err); }
    };
  });
  content.querySelectorAll('[data-restore]').forEach(function (btn) {
    btn.onclick = async function () {
      var a = accounts.find(function (x) { return x.id === btn.getAttribute('data-restore'); });
      try { if (a && await restoreAccountFlow(a)) navigate('accounts'); } catch (err) { showErr(err); }
    };
  });
}
