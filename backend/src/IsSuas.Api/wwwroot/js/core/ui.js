import { state } from './state.js';
import { api } from './api.js';

export const loginView = document.getElementById('login-view');
export const appView = document.getElementById('app-view');
export const content = document.getElementById('content');
export const pageTitle = document.getElementById('page-title');
export const displayNameEl = document.getElementById('display-name');
export const loginError = document.getElementById('login-error');
export const loginBtn = document.getElementById('login-btn');

export function copyText(text) {
  if (navigator.clipboard && window.isSecureContext) {
    return navigator.clipboard.writeText(text);
  }
  return new Promise(function (resolve, reject) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.position = 'fixed';
    ta.style.top = '0';
    ta.style.left = '-9999px';
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try {
      if (document.execCommand('copy')) resolve();
      else reject(new Error('copy failed'));
    } catch (err) {
      reject(err);
    } finally {
      document.body.removeChild(ta);
    }
  });
}

export function foldText(s) {
  return String(s || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
}

export function isoDateLocal(d) {
  var y = d.getFullYear();
  var m = String(d.getMonth() + 1).padStart(2, '0');
  var day = String(d.getDate()).padStart(2, '0');
  return y + '-' + m + '-' + day;
}

export function addDaysLocal(days) {
  var d = new Date();
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() + days);
  return d;
}

export function searchHighlightBanner() {
  if (!state.searchHighlight) return '';
  var h = state.searchHighlight;
  state.searchHighlight = null;
  return '<div class="msg">Z hladania: <b>' + (h.title || h.id) + '</b> (' + typeLabelSk(h.entityType) + ')</div>';
}

export function formatFlightGps(f, digits) {
  var d = digits == null ? 5 : digits;
  if (!f) return null;
  var lat = f.locationLat != null ? Number(f.locationLat) : NaN;
  var lon = f.locationLon != null ? Number(f.locationLon) : NaN;
  if (isFinite(lat) && isFinite(lon))
    return lat.toFixed(d) + ', ' + lon.toFixed(d);

  var text = String(f.location || '').trim();
  if (!text) return null;

  // „48.12550, 17.11050“
  var m = text.match(/^\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*$/);
  if (m) return Number(m[1]).toFixed(d) + ', ' + Number(m[2]).toFixed(d);

  // „… 48.1255°N, 17.1105°E …“
  m = text.match(/(-?\d+(?:\.\d+)?)\s*°?\s*([NnSs])\s*[,;\s]+\s*(-?\d+(?:\.\d+)?)\s*°?\s*([EeWw])/);
  if (m) {
    lat = Number(m[1]);
    lon = Number(m[3]);
    if (m[2] === 'S' || m[2] === 's') lat = -lat;
    if (m[4] === 'W' || m[4] === 'w') lon = -lon;
    if (isFinite(lat) && isFinite(lon)) return lat.toFixed(d) + ', ' + lon.toFixed(d);
  }

  // súradnice vnorené v starom názve miesta
  m = text.match(/(-?\d{1,2}\.\d{2,})\s*,\s*(-?\d{1,3}\.\d{2,})/);
  if (m) {
    lat = Number(m[1]);
    lon = Number(m[2]);
    if (isFinite(lat) && isFinite(lon) && Math.abs(lat) <= 90 && Math.abs(lon) <= 180)
      return lat.toFixed(d) + ', ' + lon.toFixed(d);
  }
  return null;
}

export function pageHead(title, subtitle, chipsHtml, accent) {
  return '<div class="page-head"' + (accent ? ' style="--sky:' + accent + '"' : '') + '>' +
    '<div><h2>' + title + '</h2>' +
    (subtitle ? '<p class="muted">' + subtitle + '</p>' : '') +
    '</div>' +
    (chipsHtml ? '<div class="chip-row">' + chipsHtml + '</div>' : '') +
    '</div>';
}

export function chipHtml(label, value, cls) {
  return '<span class="chip' + (cls ? ' ' + cls : '') + '">' + label +
    (value != null && value !== '' ? ' <b>' + value + '</b>' : '') + '</span>';
}

export function makeTable(headers, rowsHtml, bare) {
  var html = '<table><thead><tr>' +
    headers.map(function (h) { return '<th>' + h + '</th>'; }).join('') +
    '</tr></thead><tbody>' + rowsHtml + '</tbody></table>';
  // Default: chart-panel grows with rows (page scroll) — same as Denník letov
  return bare ? html : '<div class="chart-panel">' + html + '</div>';
}

export function sectionCard(title, bodyHtml, accent) {
  return '<div class="section-card"' +
    (accent ? ' style="--section-accent:' + accent + '"' : '') + '>' +
    (title ? '<h2>' + title + '</h2>' : '') + bodyHtml + '</div>';
}

export function confirmAction(message) {
  return window.confirm(message || 'Pokracovat?');
}

export function statusLabelSk(text) {
  var raw = (text == null ? '' : String(text));
  var map = {
    Draft: 'Koncept', Submitted: 'Na schválenie', Approved: 'Schválené',
    Returned: 'Vrátené', Rejected: 'Zamietnuté', Cancelled: 'Zrušené',
    Odletene: 'Odletené', 'Odletené': 'Odletené',
    Urgent: 'Neodkladná', Active: 'Aktívne', Revoked: 'Odvolané',
    Issued: 'Platné', Superseded: 'Nahradené', Expired: 'Exspirované',
    Open: 'Otvorené', Closed: 'Uzavreté', Resolved: 'Vyriešené',
    InProgress: 'Prebieha', Pending: 'Čaká', Ready: 'Pripravené',
    Reported: 'Nahlásené', UnderRepair: 'V oprave',
    Low: 'Nízka', Medium: 'Stredná', High: 'Vysoká', Blocking: 'Blokujúca',
    Critical: 'Kritická', Normal: 'Bežná', Info: 'Informácia',
    InService: 'V prevádzke', Suspended: 'Pozastavené', InMaintenance: 'V údržbe',
    Deactivated: 'Deaktivovaný', TemporarilyBlocked: 'Dočasne blokovaný',
    Prepared: 'Pripravený', Eligible: 'Spôsobilý', NotEligible: 'Nespôsobilý',
    DuplicateIgnored: 'Duplicita ignorovaná', Processed: 'Spracované',
    Received: 'Prijaté', Failed: 'Zlyhané', NeedsPairing: 'Vyžaduje párovanie',
    Overdue: 'Po termíne', Planned: 'Plánované', Completed: 'Dokončené',
    Available: 'Dostupný', InUse: 'Používaný', Quarantine: 'Karanténa',
    Retired: 'Vyradený', Reserved: 'Rezervovaný', Damaged: 'Poškodený',
    Unserviceable: 'Nespôsobilý', Serviceable: 'Spôsobilý',
    Decommissioned: 'Vyradený z prevádzky',
    Unread: 'Neprečítané', Read: 'Prečítané', Published: 'Zverejnené',
    Withdrawn: 'Stiahnuté', Superseded: 'Nahradené', Sent: 'Odoslané',
    Skipped: 'Preskočené', New: 'Nové', Assessed: 'Posúdené', Realized: 'Realizované',
    UnderInvestigation: 'Vo vyšetrovaní', ActionsInProgress: 'Opatrenia prebiehajú',
    Done: 'Hotové', Verified: 'Overené', Waived: 'Upuštené',
    Compatible: 'Kompatibilné', Conditional: 'Podmienečné', Incompatible: 'Nekompatibilné',
    User: 'Používateľ', Approver: 'Schvaľovateľ',
    CentralGestor: 'Centrálny gestor', UnitAdministrator: 'Správca útvaru',
    ControllerAuditor: 'Kontrolór / auditor', ReadOnlyUser: 'Len na čítanie',
    SystemAdministrator: 'Systémový správca', Pilot: 'Pilot',
    RemotePilot: 'Pilot na diaľku', TechnicalWorker: 'Technický pracovník',
    SafetyOfficer: 'Bezpečnostný pracovník',
    DEFECT: 'Porucha', OPERATION: 'Operácia', PILOT: 'Pilot',
    AIRSPACE: 'Vzdušný priestor', UAS: 'UAS', SAFETY: 'Bezpečnosť',
    GENERAL: 'Všeobecné', PATROL: 'Hliadka', URGENT: 'Neodkladná',
    MULTI_ROTOR: 'Multirotor', FIXED_WING: 'Pevné krídlo', OTHER: 'Ostatné',
    PILOTS: 'Piloti', FLIGHTS: 'Lety', OPERATIONS: 'Operácie',
    BATTERIES: 'Batérie', DEFECTS: 'Poruchy', INCIDENTS: 'Incidenty',
    Checklist: 'Kontrolný zoznam', Uas: 'UAS', Operation: 'Operácia',
    Account: 'Účet', SafetyEvent: 'Bezpečnostná udalosť', ControlAudit: 'Kontrola',
    Defect: 'Porucha', Document: 'Dokument', Battery: 'Batéria', Component: 'Komponent',
    OrgUnit: 'Útvar', Success: 'Úspech', Failure: 'Zlyhanie', Denied: 'Zamietnuté',
    Ok: 'V poriadku', Error: 'Chyba', Warning: 'Upozornenie'
  };
  return map[raw] || raw || '—';
}

export function typeLabelSk(text) {
  return statusLabelSk(text);
}

export function severityLabelSk(value, kind) {
  var n = Number(value);
  if (!isNaN(n) && String(value).trim() !== '') {
    var byNum = { 0: 'Nízka', 1: 'Stredná', 2: 'Vysoká', 3: kind === 'defect' ? 'Blokujúca' : 'Kritická' };
    if (byNum[n] != null) return byNum[n];
  }
  return statusLabelSk(value);
}

export function certBadgesHtml(codes) {
  var list = Array.isArray(codes) ? codes : [];
  var seen = {};
  var html = '';
  list.forEach(function (raw) {
    var c = String(raw || '').toUpperCase().replace(/[-\/\s]/g, '_');
    var key = null;
    var label = null;
    var cls = null;
    if (c === 'A1_A3' || c === 'A1A3' || c === 'A1' || c === 'A3' || c === 'OPEN_A1_A3') {
      key = 'A1_A3'; label = 'A1/A3'; cls = 'a1a3';
    } else if (c === 'A2' || c === 'OPEN_A2') {
      key = 'A2'; label = 'A2'; cls = 'a2';
    } else if (c === 'STS2' || c === 'STS_2' || c === 'STS02' || c === 'STS_02') {
      key = 'STS2'; label = 'STS2'; cls = 'sts2';
    } else if (c === 'STS1' || c === 'STS_1' || c === 'STS01' || c === 'STS_01' || c === 'STS' || c.indexOf('STS') === 0 || c === 'STANDARD_SCENARIO') {
      key = 'STS1'; label = 'STS1'; cls = 'sts1';
    }
    if (!key || seen[key]) return;
    seen[key] = true;
    html += '<span class="cert-badge ' + cls + '">' + label + '</span>';
  });
  if (!html) return '<span class="cert-badge none">—</span>';
  return '<span class="cert-badges">' + html + '</span>';
}

export function statusBadge(text) {
  var raw = (text == null ? '' : String(text));
  var key = raw.replace(/\s+/g, '').toLowerCase();
  var cls = 'badge';
  if (/inservice|active|approved|closed|resolved|sent|clear|ok|compliant|frozen|completed|verified|low|processed|eligible|registered|available|done|success|read|published|realized|issued|odletene|odletené|schvalene|schválené|platne|platné/.test(key)) cls += ' badge-ok';
  else if (/draft|open|reported|pending|submitted|underrepair|high|warn|planned|inprogress|findingsreview|medium|urgent|returned|ready|received|normal|info|needspairing|unread|underinvestigation|actionsinprogress|new|assessed|conditional/.test(key)) cls += ' badge-warn';
  else if (/suspended|critical|rejected|cancelled|overdue|conflict|revoked|danger|noncompliant|blocking|failed|deactivated|noteligible|failure|denied|error|incompatible|withdrawn/.test(key)) cls += ' badge-danger';
  return '<span class="' + cls + '">' + statusLabelSk(raw) + '</span>';
}

export function severityBadge(value, kind) {
  var label = severityLabelSk(value, kind);
  var raw = (value == null ? '' : String(value));
  var key = raw.replace(/\s+/g, '').toLowerCase();
  var n = Number(value);
  var cls = 'badge';
  if (n === 0 || /low/.test(key)) cls += ' badge-ok';
  else if (n === 1 || n === 2 || /medium|high/.test(key)) cls += ' badge-warn';
  else if (n === 3 || /critical|blocking/.test(key)) cls += ' badge-danger';
  return '<span class="' + cls + '">' + label + '</span>';
}

export async function showHistory(entityType, entityId, containerEl) {
  if (!containerEl) return;
  containerEl.classList.remove('hidden');
  containerEl.innerHTML = '<h4>Historia</h4><p class="muted">Nacitavam...</p>';
  try {
    var q = '/api/v1/audit-events?entityType=' + encodeURIComponent(entityType) +
      '&entityId=' + encodeURIComponent(entityId);
    var events = await api(q);
    if (!events || !events.length) {
      containerEl.innerHTML = '<h4>História — ' + typeLabelSk(entityType) + '</h4><p class="muted">Žiadne audit záznamy.</p>';
      return;
    }
    var rows = events.slice(0, 40).map(function (e) {
      return '<tr><td>' + new Date(e.occurredAt).toLocaleString('sk-SK') +
        '</td><td>' + (e.actorUserName || '—') +
        '</td><td>' + typeLabelSk(e.action) +
        '</td><td>' + statusBadge(e.result) + '</td></tr>';
    }).join('');
    containerEl.innerHTML = '<h4>História — ' + typeLabelSk(entityType) + '</h4>' +
      makeTable(['Čas', 'Aktér', 'Akcia', 'Výsledok'], rows);
  } catch (err) {
    containerEl.innerHTML = '<h4>Historia</h4><div class="error">' + err.message + '</div>';
  }
}

/** Formuláre sú skryté; tlačidlá data-reveal / data-hide ich otvárajú a zatvárajú. */
export function bindFormReveals() {
  content.querySelectorAll('[data-reveal]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var id = btn.getAttribute('data-reveal');
      var panel = document.getElementById(id);
      if (!panel) return;
      var willOpen = panel.classList.contains('hidden');
      content.querySelectorAll('.form-panel').forEach(function (p) { p.classList.add('hidden'); });
      if (willOpen) panel.classList.remove('hidden');
    });
  });
  content.querySelectorAll('[data-hide]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var panel = document.getElementById(btn.getAttribute('data-hide'));
      if (panel) panel.classList.add('hidden');
    });
  });
}

function escAttr(s) {
  return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/**
 * Moderný potvrdzovací dialóg (namiesto window.confirm).
 * opts: { title, message (HTML), confirmLabel, danger, reasonLabel, reasonRequired }
 * Vráti Promise: null pri zrušení, inak { reason }.
 */
export function confirmDialog(opts) {
  var o = opts || {};
  return new Promise(function (resolve) {
    var wrap = document.createElement('div');
    wrap.className = 'ui-dialog-backdrop';
    wrap.innerHTML =
      '<form class="ui-dialog" role="dialog" aria-modal="true">' +
      '<div class="ui-dialog-icon' + (o.danger ? ' is-danger' : '') + '" aria-hidden="true">' +
      (o.danger
        ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/></svg>'
        : '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16h.01"/></svg>') +
      '</div>' +
      '<h3>' + (o.title || 'Potvrdenie') + '</h3>' +
      (o.message ? '<div class="ui-dialog-msg">' + o.message + '</div>' : '') +
      (o.reasonLabel
        ? '<label class="ui-dialog-reason">' + o.reasonLabel +
          '<input name="reason" autocomplete="off"' + (o.reasonRequired ? ' required' : '') + ' placeholder="' + escAttr(o.reasonPlaceholder || '') + '" /></label>'
        : '') +
      '<div class="ui-dialog-actions">' +
      '<button type="button" class="secondary" data-act="cancel">Zrušiť</button>' +
      '<button type="submit" class="' + (o.danger ? 'btn-solid-danger' : '') + '">' + (o.confirmLabel || 'Potvrdiť') + '</button>' +
      '</div></form>';
    document.body.appendChild(wrap);
    var form = wrap.querySelector('form');
    var input = wrap.querySelector('input[name="reason"]');
    function close(val) {
      document.removeEventListener('keydown', onKey);
      wrap.remove();
      resolve(val);
    }
    function onKey(ev) { if (ev.key === 'Escape') close(null); }
    document.addEventListener('keydown', onKey);
    wrap.addEventListener('click', function (ev) { if (ev.target === wrap) close(null); });
    wrap.querySelector('[data-act="cancel"]').onclick = function () { close(null); };
    form.onsubmit = function (ev) {
      ev.preventDefault();
      close({ reason: input ? input.value.trim() : '' });
    };
    setTimeout(function () { (input || form.querySelector('button[type="submit"]')).focus(); }, 20);
  });
}

/** Spoločné údaje o používateľoch. */
export const ACCESS_LEVEL_LABELS = { 1: 'Gestor', 2: 'Prevádzkovateľ', 3: 'Pilot' };
export const ACCOUNT_STATUS = { Prepared: 0, Active: 1, TemporarilyBlocked: 2, Deactivated: 3 };

export function accountLevel(a) {
  var v = a && a.accessLevel;
  if (v === 'Level1') return 1;
  if (v === 'Level2') return 2;
  if (v === 'Level3') return 3;
  return Number(v) || 3;
}

export function isAccountRemoved(a) {
  return !!a && (a.isActive === false || a.status === 3 || a.status === 'Deactivated');
}

export function initialsOf(first, last) {
  var a = String(first || '').trim().charAt(0);
  var b = String(last || '').trim().charAt(0);
  return (a + b).toUpperCase() || '?';
}

export function avatarHtml(first, last, seed) {
  var palette = ['#0e86a3', '#2563eb', '#7c3aed', '#db2777', '#d97706', '#16a34a', '#0f9488', '#475569'];
  var key = String(seed || (first || '') + (last || ''));
  var h = 0;
  for (var i = 0; i < key.length; i++) h = (h * 31 + key.charCodeAt(i)) >>> 0;
  return '<span class="mini-avatar" style="--av:' + palette[h % palette.length] + '">' + initialsOf(first, last) + '</span>';
}

/**
 * „Odstránenie“ používateľa = deaktivácia účtu (záznam aj história letov zostávajú kvôli auditu).
 * Vráti true, ak sa používateľ odstránil.
 */
export async function removeAccountFlow(account) {
  var name = ((account.firstName || '') + ' ' + (account.lastName || '')).trim() || account.userName;
  var res = await confirmDialog({
    title: 'Odstrániť používateľa?',
    message: '<p><b>' + name + '</b> (@' + account.userName + ') sa už nebude môcť prihlásiť a zmizne zo zoznamov.</p>' +
      '<p class="muted">Jeho lety, žiadosti a história zostanú zachované kvôli auditu. Účet môžete neskôr obnoviť.</p>',
    confirmLabel: 'Odstrániť',
    danger: true,
    reasonLabel: 'Dôvod (zapíše sa do auditu)',
    reasonPlaceholder: 'napr. odchod z útvaru',
    reasonRequired: true
  });
  if (!res) return false;
  await api('/api/v1/accounts/' + account.id + '/status', {
    method: 'PATCH',
    body: JSON.stringify({ status: ACCOUNT_STATUS.Deactivated, reason: res.reason, rowVersion: account.rowVersion })
  });
  return true;
}

export async function restoreAccountFlow(account) {
  var name = ((account.firstName || '') + ' ' + (account.lastName || '')).trim() || account.userName;
  var res = await confirmDialog({
    title: 'Obnoviť používateľa?',
    message: '<p><b>' + name + '</b> (@' + account.userName + ') sa bude môcť znova prihlásiť.</p>',
    confirmLabel: 'Obnoviť',
    reasonLabel: 'Dôvod',
    reasonPlaceholder: 'napr. návrat na útvar',
    reasonRequired: true
  });
  if (!res) return false;
  await api('/api/v1/accounts/' + account.id + '/status', {
    method: 'PATCH',
    body: JSON.stringify({ status: ACCOUNT_STATUS.Active, reason: res.reason, rowVersion: account.rowVersion })
  });
  return true;
}

// Server posiela stavy ako čísla (enum). Preklad na názvy, ktoré pozná statusBadge / statusLabelSk.
const ENUM_NAMES = {
  flight: ['Draft', 'Ready', 'Closed', 'Cancelled'],
  defect: ['Reported', 'UnderRepair', 'Resolved', 'Closed'],
  operation: ['Draft', 'Submitted', 'Approved', 'Returned', 'Rejected', 'Cancelled', 'Completed'],
  safety: ['Draft', 'Submitted', 'UnderInvestigation', 'ActionsInProgress', 'Closed', 'Cancelled'],
  notification: ['Unread', 'Read', 'Resolved'],
  telemetry: ['Received', 'Processed', 'NeedsPairing', 'Failed', 'DuplicateIgnored']
};
export function enumName(kind, value) {
  if (typeof value === 'number' && ENUM_NAMES[kind] && ENUM_NAMES[kind][value] != null) return ENUM_NAMES[kind][value];
  return value;
}
