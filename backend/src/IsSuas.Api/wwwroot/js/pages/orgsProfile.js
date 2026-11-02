// Profil prevádzkovateľa (grafy) a úprava údajov používateľa — modálne okná pre Organizačnú štruktúru.
import { api } from '../core/api.js';
import { ACCESS_LEVEL_LABELS, accountLevel, avatarHtml, certBadgesHtml } from '../core/ui.js';

var COLORS = ['#1f7a8c', '#3b6fd8', '#d98a1c', '#7c5cd6', '#d1495b', '#2e9e5b', '#c2559f', '#5b7083', '#0ea5a4', '#a16207'];
var CERTS = [['A1_A3', 'A1/A3'], ['A2', 'A2'], ['STS1', 'STS-01'], ['STS2', 'STS-02']];

function esc(s) {
  return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}
function fmtH(h) { return (Math.round((h || 0) * 10) / 10).toLocaleString('sk-SK', { minimumFractionDigits: 0, maximumFractionDigits: 1 }); }
function fullName(a) { return ((a.firstName || '') + ' ' + (a.lastName || '')).trim() || a.userName || '—'; }
function normCert(c) { var k = String(c || '').toUpperCase().replace(/[-\/\s]/g, '_'); return k === 'STS01' ? 'STS1' : k === 'STS02' ? 'STS2' : k; }

/** Spoločný obal modálneho okna; vráti { el, close }. */
function openModal(cls, html, onClose) {
  var wrap = document.createElement('div');
  wrap.className = 'ui-dialog-backdrop pm-backdrop';
  wrap.innerHTML = '<div class="pm ' + cls + '" role="dialog" aria-modal="true">' + html + '</div>';
  document.body.appendChild(wrap);
  document.body.classList.add('pm-lock');
  function onKey(ev) { if (ev.key === 'Escape') close(); }
  function close() {
    document.removeEventListener('keydown', onKey);
    document.body.classList.remove('pm-lock');
    wrap.remove();
    if (onClose) onClose();
  }
  document.addEventListener('keydown', onKey);
  wrap.addEventListener('mousedown', function (ev) { if (ev.target === wrap) close(); });
  wrap.querySelectorAll('[data-pm-close]').forEach(function (b) { b.onclick = close; });
  return { el: wrap, close: close };
}

/* ---------------------------------------------------------------- jednoduchý zoznam na výber */

/** opts: { title, subtitle, items: [{ id, title, sub, right (HTML), depth }], onPick(id) } */
export function openListModal(opts) {
  var items = opts.items || [];
  var m = openModal('pm-list',
    '<div class="pm-list-head"><div><h3>' + esc(opts.title) + '</h3>' + (opts.subtitle ? '<span>' + esc(opts.subtitle) + '</span>' : '') + '</div>' +
    '<button type="button" class="pm-x" data-pm-close aria-label="Zavrieť">×</button></div>' +
    (items.length > 6 ? '<div class="pm-list-search"><input type="search" placeholder="Hľadať…" autocomplete="off"></div>' : '') +
    '<div class="pm-list-items">' + (items.length ? items.map(function (it) {
      return '<button type="button" class="pm-list-item" data-pm-pick="' + esc(it.id) + '" data-q="' + esc(String(it.title + ' ' + (it.sub || '')).toLowerCase()) + '"' +
        ' style="--d:' + (it.depth || 0) + '"><span class="pm-list-main"><b>' + esc(it.title) + '</b>' + (it.sub ? '<small>' + esc(it.sub) + '</small>' : '') + '</span>' +
        (it.right ? '<span class="pm-list-right">' + it.right + '</span>' : '') + '<span class="pm-list-chev">›</span></button>';
    }).join('') : '<p class="pm-nodata">Žiadne položky.</p>') + '</div>');
  m.el.querySelectorAll('[data-pm-pick]').forEach(function (b) {
    b.onclick = function () { m.close(); opts.onPick(b.dataset.pmPick); };
  });
  var q = m.el.querySelector('.pm-list-search input');
  if (q) {
    setTimeout(function () { q.focus(); }, 20);
    q.oninput = function () {
      var v = q.value.trim().toLowerCase();
      m.el.querySelectorAll('[data-pm-pick]').forEach(function (b) { b.hidden = v && b.dataset.q.indexOf(v) < 0; });
    };
  }
  return m;
}

/* ---------------------------------------------------------------- úprava údajov */

/**
 * Formulár na zmenu mena, priezviska, e-mailu (a loginu pre gestora).
 * @returns Promise<AccountDto|null>
 */
export function openUserEditor(account, opts) {
  var o = opts || {};
  return new Promise(function (resolve) {
    var saved = null;
    var lvl = accountLevel(account);
    var m = openModal('pm-edit',
      '<form class="pm-form">' +
      '<div class="pm-edit-head">' + avatarHtml(account.firstName, account.lastName, account.userName) +
      '<div><h3>Upraviť údaje</h3><span>' + esc(fullName(account)) + ' · ' + esc(ACCESS_LEVEL_LABELS[lvl] || '') + '</span></div>' +
      '<button type="button" class="pm-x" data-pm-close aria-label="Zavrieť">×</button></div>' +
      '<div class="pm-fields">' +
      '<label>Meno<input name="firstName" required maxlength="128" autocomplete="off" value="' + esc(account.firstName) + '"></label>' +
      '<label>Priezvisko<input name="lastName" maxlength="128" autocomplete="off" value="' + esc(account.lastName) + '" placeholder="' + (lvl === 2 ? 'nepovinné' : '') + '"></label>' +
      '<label class="pm-wide">E-mail<input name="email" type="email" maxlength="256" autocomplete="off" value="' + esc(account.email || '') + '" placeholder="nepovinné"></label>' +
      '<label class="pm-wide">Login<span class="pm-login"><span>@</span><input name="userName" maxlength="60" autocomplete="off" value="' + esc(account.userName) + '"' +
        (o.canEditLogin ? '' : ' disabled') + '></span>' +
        '<small>' + (o.canEditLogin ? 'Po zmene sa používateľ prihlasuje novým loginom.' : 'Login môže zmeniť iba gestor.') + '</small></label>' +
      '</div>' +
      '<div class="pm-msg"></div>' +
      '<div class="pm-actions"><button type="button" class="secondary" data-pm-close>Zrušiť</button><button type="submit">Uložiť zmeny</button></div>' +
      '</form>',
      function () { resolve(saved); });
    var form = m.el.querySelector('form');
    setTimeout(function () { form.firstName.focus(); form.firstName.select(); }, 20);
    form.onsubmit = async function (ev) {
      ev.preventDefault();
      var btn = form.querySelector('button[type="submit"]');
      var msg = m.el.querySelector('.pm-msg');
      btn.disabled = true; msg.innerHTML = '';
      try {
        saved = await api('/api/v1/accounts/' + account.id, {
          method: 'PUT',
          body: JSON.stringify({
            firstName: form.firstName.value.trim(),
            lastName: form.lastName.value.trim(),
            email: form.email.value.trim() || null,
            userName: o.canEditLogin ? form.userName.value.trim() : null,
            rowVersion: account.rowVersion
          })
        });
        m.close();
      } catch (err) {
        btn.disabled = false;
        msg.innerHTML = '<div class="error">' + esc(err.message || 'Uloženie zlyhalo') + '</div>';
      }
    };
  });
}

/* ---------------------------------------------------------------- profil prevádzkovateľa */

/**
 * ctx: { op, pilots (jeho piloti), flights (všetky lety), hoursByPilot, flightsByPilot,
 *        canEdit, onEdit(op) → Promise, onOpenPilot(pilotId) }
 */
export function openOperatorProfile(ctx) {
  var op = ctx.op;
  var pilots = ctx.pilots.slice().sort(function (a, b) { return (ctx.hoursByPilot[b.id] || 0) - (ctx.hoursByPilot[a.id] || 0); });
  var mine = {}, colorOf = {};
  pilots.forEach(function (p, i) { mine[p.id] = p; colorOf[p.id] = COLORS[i % COLORS.length]; });
  var fl = (ctx.flights || []).filter(function (f) { return f.pilotId && mine[f.pilotId]; });
  var flown = fl.filter(function (f) { return f.durationMinutes > 0; });
  var totalH = flown.reduce(function (t, f) { return t + f.durationMinutes / 60; }, 0);
  var lastAt = flown.reduce(function (m, f) { var t = f.takeoffAt || f.landingAt; return t && (!m || t > m) ? t : m; }, null);
  var withCert = pilots.filter(function (p) { return (p.certificateCodes || []).length; }).length;
  var activePilots = pilots.filter(function (p) { return (ctx.hoursByPilot[p.id] || 0) > 0; });

  // --- mesačný nálet (12 mesiacov, skladaný podľa pilota)
  var now = new Date(), months = [], idx = {};
  for (var k = 11; k >= 0; k--) {
    var d = new Date(now.getFullYear(), now.getMonth() - k, 1);
    idx[d.getFullYear() + '-' + d.getMonth()] = months.length;
    months.push({ label: d.toLocaleDateString('sk-SK', { month: 'short' }).replace('.', ''), year: d.getFullYear(), by: {}, total: 0 });
  }
  var byDow = [0, 0, 0, 0, 0, 0, 0];
  var byHour = {};
  flown.forEach(function (f) {
    var t = f.takeoffAt || f.landingAt; if (!t) return;
    var dt = new Date(t), h = f.durationMinutes / 60;
    var i = idx[dt.getFullYear() + '-' + dt.getMonth()];
    if (i !== undefined) { months[i].by[f.pilotId] = (months[i].by[f.pilotId] || 0) + h; months[i].total += h; }
    byDow[(dt.getDay() + 6) % 7] += 1;
    var slot = Math.floor(dt.getHours() / 3);
    byHour[slot] = (byHour[slot] || 0) + 1;
  });
  var mMax = months.reduce(function (x, mo) { return Math.max(x, mo.total); }, 0);
  var monthly = mMax <= 0 ? '<p class="pm-nodata">Za posledných 12 mesiacov žiadne lety.</p>' :
    '<div class="fd-cols">' + months.map(function (mo) {
      var stack = pilots.map(function (p) {
        var v = mo.by[p.id] || 0;
        return v > 0 ? '<span style="height:' + (v / mMax * 100).toFixed(2) + '%;background:' + colorOf[p.id] + '" title="' + esc(fullName(p)) + ': ' + fmtH(v) + ' h"></span>' : '';
      }).join('');
      return '<div class="fd-col" title="' + esc(mo.label + ' ' + mo.year) + ': ' + fmtH(mo.total) + ' h">' +
        '<span class="fd-col-val">' + (mo.total > 0 ? fmtH(mo.total) : '') + '</span>' +
        '<div class="fd-col-bar">' + stack + '</div><span class="fd-col-lbl">' + esc(mo.label) + '</span></div>';
    }).join('') + '</div>';

  // --- prstenec: podiel pilotov
  var R = 52, C = 2 * Math.PI * R, off = 0;
  var arcs = activePilots.map(function (p) {
    var h = ctx.hoursByPilot[p.id] || 0, len = h / (totalH || 1) * C, gap = activePilots.length > 1 ? Math.min(2, len * 0.3) : 0;
    var a = '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="' + colorOf[p.id] + '" stroke-width="18" stroke-dasharray="' +
      Math.max(0, len - gap).toFixed(2) + ' ' + (C - Math.max(0, len - gap)).toFixed(2) + '" stroke-dashoffset="' + (-off).toFixed(2) + '">' +
      '<title>' + esc(fullName(p)) + ': ' + fmtH(h) + ' h</title></circle>';
    off += len; return a;
  }).join('');
  var donut = !activePilots.length ? '<p class="pm-nodata">Piloti zatiaľ nelietali.</p>' :
    '<div class="pm-donut"><svg class="fd-donut" viewBox="0 0 140 140" role="img" aria-label="Podiel pilotov na nálete">' +
    '<circle r="' + R + '" cx="70" cy="70" fill="none" stroke="#eef2f6" stroke-width="18"/><g transform="rotate(-90 70 70)">' + arcs + '</g>' +
    '<text x="70" y="67" text-anchor="middle" class="fd-donut-num">' + fmtH(totalH) + '</text>' +
    '<text x="70" y="84" text-anchor="middle" class="fd-donut-lbl">hodín spolu</text></svg>' +
    '<ul class="fd-legend">' + activePilots.slice(0, 8).map(function (p) {
      return '<li><span class="fd-dot" style="background:' + colorOf[p.id] + '"></span><span class="fd-legend-name">' + esc(fullName(p)) + '</span>' +
        '<b>' + Math.round((ctx.hoursByPilot[p.id] || 0) / (totalH || 1) * 100) + ' %</b></li>';
    }).join('') + (activePilots.length > 8 ? '<li class="pm-more">+' + (activePilots.length - 8) + ' ďalší</li>' : '') + '</ul></div>';

  // --- pokrytie osvedčeniami
  var certCount = {};
  pilots.forEach(function (p) { (p.certificateCodes || []).forEach(function (c) { var n = normCert(c); certCount[n] = (certCount[n] || 0) + 1; }); });
  var certs = '<div class="pm-certs">' + CERTS.map(function (c) {
    var n = certCount[c[0]] || 0, pc = pilots.length ? n / pilots.length * 100 : 0;
    return '<div class="pm-cert"><span class="pm-cert-name">' + c[1] + '</span>' +
      '<span class="pm-cert-track"><span style="width:' + pc.toFixed(1) + '%"></span></span>' +
      '<span class="pm-cert-val"><b>' + n + '</b> / ' + pilots.length + '</span></div>';
  }).join('') +
    '<div class="pm-cert pm-cert-none"><span class="pm-cert-name">Bez osvedčenia</span>' +
    '<span class="pm-cert-track"><span style="width:' + (pilots.length ? (pilots.length - withCert) / pilots.length * 100 : 0).toFixed(1) + '%"></span></span>' +
    '<span class="pm-cert-val"><b>' + (pilots.length - withCert) + '</b> / ' + pilots.length + '</span></div></div>';

  // --- dni v týždni
  var dMax = Math.max.apply(null, byDow) || 1;
  var dows = ['Po', 'Ut', 'St', 'Št', 'Pi', 'So', 'Ne'];
  var week = '<div class="pm-week">' + byDow.map(function (n, i) {
    return '<div class="pm-day" title="' + dows[i] + ': ' + n + ' letov"><span class="pm-day-bar"><span style="height:' + (n / dMax * 100).toFixed(1) + '%"></span></span>' +
      '<b>' + n + '</b><small>' + dows[i] + '</small></div>';
  }).join('') + '</div>';

  // --- tabuľka pilotov
  var hMax = activePilots.length ? (ctx.hoursByPilot[activePilots[0].id] || 1) : 1;
  var lastByPilot = {};
  flown.forEach(function (f) { var t = f.takeoffAt || f.landingAt; if (t && (!lastByPilot[f.pilotId] || t > lastByPilot[f.pilotId])) lastByPilot[f.pilotId] = t; });
  var table = !pilots.length ? '<p class="pm-nodata">Prevádzkovateľ zatiaľ nemá pridelených žiadnych pilotov.</p>' :
    '<div class="pm-pilots">' + pilots.map(function (p) {
      var h = ctx.hoursByPilot[p.id] || 0;
      return '<button type="button" class="pm-pilot" data-pm-pilot="' + p.id + '">' +
        '<span class="fd-ava" style="background:' + colorOf[p.id] + '">' + esc(((p.firstName || '').charAt(0) + (p.lastName || '').charAt(0)).toUpperCase()) + '</span>' +
        '<span class="pm-pilot-main"><b>' + esc(fullName(p)) + '</b><small>' + esc(p.orgUnitName || '') +
          (lastByPilot[p.id] ? ' · posledný let ' + new Date(lastByPilot[p.id]).toLocaleDateString('sk-SK') : ' · zatiaľ bez letu') + '</small></span>' +
        '<span class="pm-pilot-certs">' + ((p.certificateCodes || []).length ? certBadgesHtml(p.certificateCodes) : '<span class="badge badge-warn">Bez osvedčenia</span>') + '</span>' +
        '<span class="pm-pilot-bar"><span style="width:' + (h / hMax * 100).toFixed(1) + '%;background:' + colorOf[p.id] + '"></span></span>' +
        '<span class="pm-pilot-num"><b>' + fmtH(h) + ' h</b><small>' + (ctx.flightsByPilot[p.id] || 0) + ' letov</small></span></button>';
    }).join('') + '</div>';

  var lvl = accountLevel(op);
  var html =
    '<div class="pm-hero">' +
    '<div class="pm-hero-ava">' + avatarHtml(op.firstName, op.lastName, op.userName) + '</div>' +
    '<div class="pm-hero-main"><span class="pm-kicker">' + esc(ACCESS_LEVEL_LABELS[lvl] || 'Prevádzkovateľ') + '</span>' +
    '<h2>' + esc(fullName(op)) + '</h2>' +
    '<p>@' + esc(op.userName) + ' · ' + esc(op.homeOrgUnitName || '—') + (op.email ? ' · ' + esc(op.email) : '') + '</p></div>' +
    '<div class="pm-hero-actions">' +
    (ctx.canEdit ? '<button type="button" class="pm-hero-btn" data-pm-edit>Upraviť údaje</button>' : '') +
    '<button type="button" class="pm-x" data-pm-close aria-label="Zavrieť">×</button></div></div>' +
    '<div class="pm-body">' +
    '<div class="pm-kpis">' +
    kpi(fmtH(totalH) + ' h', 'celkový nálet') + kpi(flown.length, 'letov') +
    kpi(pilots.length, 'pilotov', activePilots.length + ' aktívnych') +
    kpi(flown.length ? Math.round(totalH * 60 / flown.length) + ' min' : '—', 'priemerný let') +
    kpi(lastAt ? new Date(lastAt).toLocaleDateString('sk-SK') : '—', 'posledný let') +
    '</div>' +
    '<div class="pm-grid">' +
    '<div class="fd-card pm-c4"><h4>Nálet pilotov za posledných 12 mesiacov</h4>' + monthly + '</div>' +
    '<div class="fd-card pm-c2"><h4>Podiel pilotov na nálete</h4>' + donut + '</div>' +
    '<div class="fd-card pm-c3"><h4>Osvedčenia pilotov</h4>' + certs + '</div>' +
    '<div class="fd-card pm-c3"><h4>Lety podľa dňa v týždni</h4>' + week + '</div>' +
    '<div class="fd-card pm-span3"><h4>Piloti <span class="tab-count">' + pilots.length + '</span></h4>' + table + '</div>' +
    '</div></div>';

  var m = openModal('pm-profile', html);
  m.el.querySelectorAll('[data-pm-pilot]').forEach(function (b) {
    b.onclick = function () { m.close(); ctx.onOpenPilot(b.dataset.pmPilot); };
  });
  var editBtn = m.el.querySelector('[data-pm-edit]');
  if (editBtn) editBtn.onclick = function () { m.close(); ctx.onEdit(op); };
  return m;

  function kpi(v, label, sub) {
    return '<div class="pm-kpi"><b>' + esc(v) + '</b><span>' + esc(label) + '</span>' + (sub ? '<small>' + esc(sub) + '</small>' : '') + '</div>';
  }
}
