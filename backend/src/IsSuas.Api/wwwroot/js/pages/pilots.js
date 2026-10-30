import { api } from '../core/api.js';
import { colorForKey } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { trashButtonHtml } from '../core/trash.js';
import { certBadgesHtml, chipHtml, content, makeTable, pageHead, searchHighlightBanner, sectionCard, statusBadge, enumName } from '../core/ui.js';
import { bindCertLifecycleButtons, certActionButtons, certStatusHtml, certSuspendPanelHtml, isCertIssued, isCertSuspended } from '../shared/certs.js';

export async function renderPilots() {
      if (state.pilotDetailId) {
        const items = await api('/api/v1/pilots');
        const pilot = items.find(function (p) { return p.id === state.pilotDetailId; });
        if (!pilot) {
          state.pilotDetailId = null;
          navigate('pilots');
          return;
        }
        let flights = [];
        let ops = [];
        try { flights = await api('/api/v1/flights'); } catch (_) { flights = []; }
        try { ops = await api('/api/v1/operations'); } catch (_) { ops = []; }
        const name = ((pilot.firstName || '') + ' ' + (pilot.lastName || '')).trim();
        const myFlights = (flights || []).filter(function (f) {
          return f.pilotId === pilot.id || f.pilotName === name;
        });
        const myOps = (ops || []).filter(function (o) {
          return (o.title || '').indexOf(name) >= 0 || o.orgUnitId === pilot.orgUnitId;
        }).slice(0, 30);
        const flightRows = myFlights.map(function (f) {
          return '<tr class="flight-row" data-id="' + f.id + '"><td><button type="button" class="linkish fl-open" data-id="' + f.id + '">' +
            (f.flightNumber || String(f.id).substring(0, 8)) + '</button></td><td>' +
            (f.takeoffAt ? new Date(f.takeoffAt).toLocaleString('sk-SK') : '—') + '</td><td>' +
            (f.uasLabel || '—') + '</td><td>' + (f.purpose || '—') + '</td><td>' + statusBadge(enumName('flight', f.status)) + '</td></tr>';
        }).join('') || '<tr><td colspan="5">Žiadne lety tohto pilota</td></tr>';
        const opRows = myOps.map(function (o) {
          return '<tr><td>' + o.title + '</td><td>' + statusBadge(enumName('operation', o.status)) + '</td><td>' +
            new Date(o.plannedStart).toLocaleString('sk-SK') + '</td></tr>';
        }).join('') || '<tr><td colspan="3">Žiadne operácie</td></tr>';
        var mapBtnLabel = state.accessLevel === 3 ? 'Otvoriť mapu GEO zón' : 'Otvoriť VFR mapu';
        var mapBtnHint = state.accessLevel === 3
          ? 'Denník letov, operácie a mapa GEO zón.'
          : 'Denník letov, operácie a VFR mapa pre tohto pilota.';
        var detail = null;
        try { detail = await api('/api/v1/pilots/' + pilot.id); } catch (_) { detail = null; }
        var certs = ((detail && detail.certificates) || []).filter(function (c) {
          return isCertIssued(c) || isCertSuspended(c);
        });
        var seenCert = {};
        certs = certs.filter(function (c) {
          var k = String(c.certificateTypeCode || '').toUpperCase();
          if (seenCert[k]) return false;
          seenCert[k] = 1;
          return true;
        });
        var certCodes = pilot.certificateCodes || [];
        var certRows = certs.map(function (c) {
          return '<tr><td>' + certBadgesHtml([c.certificateTypeCode]) + '</td><td>' + (c.titleSk || c.certificateTypeCode || '—') +
            '</td><td>' + (c.externalNumber || '—') + '</td><td>' + certStatusHtml(c) +
            '</td><td>' + (c.validFrom ? new Date(c.validFrom).toLocaleDateString('sk-SK') : '—') +
            '</td><td>' + (c.validTo ? new Date(c.validTo).toLocaleDateString('sk-SK') : '—') +
            '</td><td>' + (c.mtowOrValidityText || '—') + '</td>' +
            '<td class="row-actions">' + certActionButtons(pilot.id, c, name) + '</td></tr>';
        }).join('') || '<tr><td colspan="8">Žiadne preukazy</td></tr>';
        content.innerHTML = '<div class="stack">' +
          pageHead('Pilot · ' + name, mapBtnHint,
            chipHtml('Evid. č.', pilot.licenseNumber || '—', 'chip-soft') +
            chipHtml('Útvar', pilot.orgUnitName || '—') +
            chipHtml('Č. denníka PnD', pilot.logbookNumber || '—', 'chip-soft') +
            '<span class="chip chip-soft">Kategórie ' + certBadgesHtml(certCodes) + '</span>') +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="pilot-back">Späť na pilotov</button> ' +
          '<button type="button" class="secondary" id="pilot-map">' + mapBtnLabel + '</button> ' +
          trashButtonHtml('pilots', pilot.id, name) +
          '</div>' +
          certSuspendPanelHtml() +
          '<div id="pilot-cert-msg"></div>' +
          sectionCard('Preukazy PnD (A1/A3 · A2 · STS1 · STS2)', makeTable(['Kategória', 'Názov', 'Číslo', 'Stav', 'Platnosť od', 'Platnosť do', 'MTOW / platnosť', 'Akcie'], certRows, true), '#0d8f7a') +
          sectionCard('Denník letov', makeTable(['Číslo', 'Čas', 'Dron', 'Účel', 'Stav'], flightRows, true), '#2b7bb9') +
          sectionCard('Operácie (útvar)', makeTable(['Názov', 'Stav', 'Začiatok'], opRows, true), '#c47b1a') +
          '</div>';
        document.getElementById('pilot-back').onclick = function () {
          state.pilotDetailId = null;
          navigate('pilots');
        };
        document.getElementById('pilot-map').onclick = function () {
          state.mapStartPick = false;
          navigate(state.accessLevel === 3 ? 'geozones' : 'map');
        };
        bindCertLifecycleButtons(async function (okMsg, errMsg) {
          var box = document.getElementById('pilot-cert-msg');
          if (box) {
            box.innerHTML = errMsg
              ? '<div class="error">' + errMsg + '</div>'
              : '<div class="msg">' + okMsg + '</div>';
          }
          if (!errMsg) navigate('pilots');
        });
        content.querySelectorAll('.fl-open').forEach(function (btn) {
          btn.onclick = function () {
            state.flightDetailId = btn.dataset.id;
            navigate('flights');
          };
        });
        return;
      }
      const items = await api('/api/v1/pilots');
      var canAddPilot = state.accessLevel === 1 || state.accessLevel === 2;
      var orgOptsHtml = '';
      if (state.accessLevel === 1) {
        var orgList = [];
        try { orgList = await api('/api/v1/org-units'); } catch (_) { orgList = []; }
        orgOptsHtml = orgList.filter(function (o) { return o.isActive !== false; })
          .sort(function (a, b) { return String(a.path || '').localeCompare(String(b.path || '')); })
          .map(function (o) {
            return '<option value="' + o.id + '">' + Array((o.level || 0) + 1).join('\u00a0\u00a0') + o.name + '</option>';
          }).join('');
      }
      var allowedTabs = { a1: 1, a2: 1, sts1: 1, sts2: 1, none: 1 };
      var tab = allowedTabs[state.pilotRegisterTab] ? state.pilotRegisterTab : 'a1';
      function normCode(raw) {
        var t = String(raw || '').toUpperCase().replace(/[-\/\s]/g, '_');
        if (t === 'A1_A3' || t === 'A1A3' || t === 'A1' || t === 'A3' || t === 'OPEN_A1_A3') return 'A1_A3';
        if (t === 'A2' || t === 'OPEN_A2') return 'A2';
        if (t === 'STS2' || t === 'STS_2' || t === 'STS02' || t === 'STS_02') return 'STS2';
        if (t === 'STS1' || t === 'STS_1' || t === 'STS01' || t === 'STS_01' || t === 'STS' || t.indexOf('STS') === 0) return 'STS1';
        return t;
      }
      function hasCode(p, code) {
        return (p.certificateCodes || []).some(function (c) { return normCode(c) === code; });
      }
      function certOf(p, code) {
        return (p.certificates || []).find(function (c) { return normCode(c.certificateTypeCode) === code; }) || null;
      }
      function fmtDate(iso) {
        return iso ? new Date(iso).toLocaleDateString('sk-SK') : '—';
      }
      function ladderOf(p) {
        var a13 = hasCode(p, 'A1_A3');
        var a2 = hasCode(p, 'A2');
        var sts1 = hasCode(p, 'STS1');
        var sts2 = hasCode(p, 'STS2');
        if (a13 && a2 && sts1 && sts2) return 'sts2';
        if (a13 && a2 && sts1 && !sts2) return 'sts1';
        if (a13 && a2 && !sts1 && !sts2) return 'a2';
        if (a13 && !a2 && !sts1 && !sts2) return 'a1';
        return '';
      }
      var groups = { a1: [], a2: [], sts1: [], sts2: [], none: [] };
      (items || []).forEach(function (p) {
        if (p.isActive === false) return;
        var k = ladderOf(p) || 'none';
        groups[k].push(p);
      });
      var list = groups[tab] || [];
      var headers = [
        'P.č.', 'ČOZ', 'Priezvisko', 'Meno', 'Útvar', 'e-mail do práce',
        'Č. povolenia DÚ', 'Platnosť od', 'Platnosť do', 'Kategória', 'Osv. ges.'
      ];
      if (tab === 'sts1' || tab === 'sts2') {
        headers = [
          'P.č.', 'ČOZ', 'Priezvisko', 'Meno', 'Útvar', 'e-mail do práce',
          'Číslo povolenia / osvedčenia', 'Platnosť od', 'Kategória',
          'MTOW alebo platnosť', 'Číslo osvedčenia gestora'
        ];
      }
      var tabBadges = {
        a1: ['A1_A3'],
        a2: ['A1_A3', 'A2'],
        sts1: ['A1_A3', 'A2', 'STS1'],
        sts2: ['A1_A3', 'A2', 'STS1', 'STS2'],
        none: []
      };
      var emptyText = {
        a1: 'Žiadni piloti iba s A1/A3',
        a2: 'Žiadni piloti s A1/A3 a A2',
        sts1: 'Žiadni piloti s A1/A3, A2 a STS1',
        sts2: 'Žiadni piloti s A1/A3, A2, STS1 a STS2',
        none: 'Žiadni piloti bez osvedčenia'
      };
      var rows = list.map(function (p, idx) {
        var a2 = certOf(p, 'A2');
        var a13 = certOf(p, 'A1_A3');
        var s1 = certOf(p, 'STS1');
        var s2 = certOf(p, 'STS2');
        var permit = (s2 && s2.externalNumber) || (s1 && s1.externalNumber)
          || (a2 && a2.externalNumber) || (a13 && a13.externalNumber) || '—';
        var topCert = s2 || s1 || a2 || a13;
        var validFrom = (topCert && topCert.validFrom) || null;
        var validTo = (topCert && topCert.validTo) || null;
        var mtow = (s2 && s2.mtowOrValidityText) || (s1 && s1.mtowOrValidityText)
          || (a2 && a2.mtowOrValidityText) || (validTo ? ('platné do ' + fmtDate(validTo)) : '—');
        var common = '<tr class="row-tint pilot-row" data-id="' + p.id +
          '" style="--row-accent:' + colorForKey(p.orgUnitName || p.id) +
          '" title="Otvoriť detail pilota">' +
          '<td>' + (idx + 1) + '</td>' +
          '<td>' + (p.personalBadgeNumber || '—') + '</td>' +
          '<td>' + (p.lastName || '—') + '</td>' +
          '<td>' + (p.firstName || '—') + '</td>' +
          '<td>' + (p.orgUnitName || '—') + '</td>' +
          '<td>' + (p.email || '—') + '</td>';
        if (tab === 'sts1' || tab === 'sts2') {
          return common +
            '<td>' + permit + '</td>' +
            '<td>' + fmtDate(validFrom) + '</td>' +
            '<td>' + certBadgesHtml(tabBadges[tab]) + '</td>' +
            '<td>' + mtow + '</td>' +
            '<td>' + (p.gestorCertificateNumber || '—') + '</td>' +
            '</tr>';
        }
        return common +
          '<td>' + permit + '</td>' +
          '<td>' + fmtDate(validFrom) + '</td>' +
          '<td>' + fmtDate(validTo) + '</td>' +
          '<td>' + (tab === 'none' ? ((p.certificateCodes || []).length ? certBadgesHtml(p.certificateCodes) : '<span class="badge badge-warn">Bez osvedčenia</span>') : certBadgesHtml(tabBadges[tab])) + '</td>' +
          '<td>' + (p.gestorCertificateNumber || '—') + '</td>' +
          '</tr>';
      }).join('') || '<tr><td colspan="11">' + (emptyText[tab] || 'Žiadni piloti') + '</td></tr>';

      content.innerHTML = '<div class="stack">' + searchHighlightBanner() +
        pageHead('Register PnD',
          'Piloti rozdelení podľa osvedčení. Pilota môžete pridať aj bez osvedčenia.',
          chipHtml('A1/A3', groups.a1.length, 'chip-soft') +
          chipHtml('A1/A3 + A2', groups.a2.length, 'chip-soft') +
          chipHtml('+ STS1', groups.sts1.length, 'chip-soft') +
          chipHtml('+ STS1 + STS2', groups.sts2.length, 'chip-soft') +
          chipHtml('Bez osvedčenia', groups.none.length, groups.none.length ? 'chip-warn' : 'chip-soft')) +
        (canAddPilot ? '<div class="toolbar"><button type="button" id="pilot-add-btn">+ Pridať pilota</button></div>' : '') +
        '<div class="reg-tabs">' +
        '<button type="button" class="reg-tab' + (tab === 'a1' ? ' active' : '') + '" data-tab="a1">A1/A3</button>' +
        '<button type="button" class="reg-tab' + (tab === 'a2' ? ' active' : '') + '" data-tab="a2">A1/A3 + A2</button>' +
        '<button type="button" class="reg-tab' + (tab === 'sts1' ? ' active' : '') + '" data-tab="sts1">A1/A3 + A2 + STS1</button>' +
        '<button type="button" class="reg-tab' + (tab === 'sts2' ? ' active' : '') + '" data-tab="sts2">A1/A3 + A2 + STS1 + STS2</button>' +
        '<button type="button" class="reg-tab' + (tab === 'none' ? ' active' : '') + '" data-tab="none">Bez osvedčenia <span class="tab-count">' + groups.none.length + '</span></button>' +
        '</div>' +
        '<div id="pilot-msg"></div>' +
        (canAddPilot
          ? '<div id="pilot-create-panel" class="form-panel hidden">' +
            '<h3>Nový pilot</h3>' +
            '<p class="muted" style="margin:-0.4rem 0 0">Osvedčenie nie je povinné — pilot sa zaradí do karty <b>Bez osvedčenia</b> a osvedčenie mu môžete doplniť neskôr v menu Osvedčenia.</p>' +
            '<form id="pilot-create-form" class="form-grid">' +
            '<label>Meno<input id="pc-first" required autocomplete="off" /></label>' +
            '<label>Priezvisko<input id="pc-last" required autocomplete="off" /></label>' +
            '<label>E-mail do práce<input id="pc-email" type="email" placeholder="voliteľné" /></label>' +
            (state.accessLevel === 1
              ? '<label>Útvar<select id="pc-org" required>' + orgOptsHtml + '</select></label>'
              : '<label>Útvar<input value="Váš útvar" readonly /></label>') +
            '<label>Číslo licencie / povolenia<input id="pc-license" placeholder="voliteľné" /></label>' +
            '<label>Poznámka<input id="pc-notes" placeholder="voliteľné" /></label>' +
            '<div class="full toolbar"><button type="submit">Pridať pilota</button>' +
            '<button type="button" class="secondary" id="pc-cancel">Zrušiť</button></div>' +
            '</form></div>'
          : '') +
        '<div class="chart-panel table-scroll">' +
        makeTable(headers, rows, true) +
        '</div></div>';

      content.querySelectorAll('.reg-tab').forEach(function (btn) {
        btn.onclick = function () {
          state.pilotRegisterTab = btn.dataset.tab;
          navigate('pilots');
        };
      });
      content.querySelectorAll('tr.pilot-row').forEach(function (row) {
        row.onclick = function () {
          state.pilotDetailId = row.dataset.id;
          navigate('pilots');
        };
      });
      var addBtn = document.getElementById('pilot-add-btn');
      var createPanel = document.getElementById('pilot-create-panel');
      if (addBtn && createPanel) {
        addBtn.onclick = function () {
          createPanel.classList.toggle('hidden');
          if (!createPanel.classList.contains('hidden')) document.getElementById('pc-first').focus();
        };
        document.getElementById('pc-cancel').onclick = function () { createPanel.classList.add('hidden'); };
        document.getElementById('pilot-create-form').onsubmit = async function (ev) {
          ev.preventDefault();
          var msg = document.getElementById('pilot-msg');
          var orgEl = document.getElementById('pc-org');
          try {
            var created = await api('/api/v1/pilots', {
              method: 'POST',
              body: JSON.stringify({
                firstName: document.getElementById('pc-first').value.trim(),
                lastName: document.getElementById('pc-last').value.trim(),
                email: document.getElementById('pc-email').value.trim() || null,
                // L2: server pilota vždy zaradí do útvaru prevádzkovateľa
                orgUnitId: orgEl ? orgEl.value : '00000000-0000-0000-0000-000000000000',
                licenseNumber: document.getElementById('pc-license').value.trim() || null,
                notes: document.getElementById('pc-notes').value.trim() || null
              })
            });
            state.pilotRegisterTab = (created && created.certificateCodes && created.certificateCodes.length) ? state.pilotRegisterTab : 'none';
            await navigate('pilots');
            var m = document.getElementById('pilot-msg');
            if (m) m.innerHTML = '<div class="msg">Pilot <b>' + (created.firstName || '') + ' ' + (created.lastName || '') + '</b> bol pridaný.</div>';
          } catch (err) {
            msg.innerHTML = '<div class="error">' + (err.message || 'Pridanie zlyhalo') + '</div>';
          }
        };
      }
      return;
}
