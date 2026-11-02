import { navigate } from '../core/nav.js';
import { api } from '../core/api.js';
import { state } from '../core/state.js';
import { bindChartToggles, certChartsHtml } from '../shared/pilotCharts.js';
import { avatarHtml, certBadgesHtml, chipHtml, content, foldText, makeTable, pageHead, sectionCard } from '../core/ui.js';
import { bindCertLifecycleButtons, certActionButtons, certStatusHtml, certSuspendPanelHtml, isCertSuspended } from '../shared/certs.js';

export async function renderCertificates() {
      var pilots = [];
      var accounts = [];
      try { pilots = await api('/api/v1/pilots'); } catch (_) { pilots = []; }
      try { accounts = await api('/api/v1/accounts'); } catch (_) { accounts = []; }
      var accByPerson = {};
      (accounts || []).forEach(function (a) { accByPerson[a.personId] = a; });
      function licChip(a) {
        if (!a) return chipHtml('Licencia', '—', 'chip-soft');
        var lic = a.licenseStatus;
        if (lic === 'Confirmed' || lic === 2) return chipHtml('Licencia', 'áno', 'chip-ok');
        if (lic === 'Pending' || lic === 1) return chipHtml('Licencia', 'čaká', 'chip-danger');
        if (lic === 'Rejected' || lic === 3) return chipHtml('Licencia', 'zamietnutá', 'chip-danger');
        return chipHtml('Licencia', String(lic), 'chip-soft');
      }
      function fmtDate(iso) {
        return iso ? new Date(iso).toLocaleDateString('sk-SK') : 'neobmedzene';
      }
      var CERT_LABELS = { A1_A3: 'A1/A3', A2: 'A2', STS1: 'STS1', STS2: 'STS2' };
      var CERT_ORDER = { A1_A3: 0, A2: 1, STS1: 2, STS2: 3 };
      function escH(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
          return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
      }
      function requestsOf(p) { return (p && p.requestedCertificateCodes) || []; }
      // Žiadosti pilotov o osvedčenie (pilot klikol „Požiadať o osvedčenie na lietanie“).
      function certRequestsHtml() {
        var reqPilots = (pilots || []).filter(function (p) { return requestsOf(p).length; });
        if (!reqPilots.length) return '';
        var rowsHtml = reqPilots.map(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim() || p.licenseNumber || p.id;
          var codes = requestsOf(p).slice().sort(function (a, b) { return (CERT_ORDER[a] || 0) - (CERT_ORDER[b] || 0); });
          return '<tr><td>' + escH(name) + '</td><td>' + escH(p.orgUnitName || '—') + '</td><td>' +
            certBadgesHtml(codes) + '</td><td>' +
            (p.certificateRequestedAt ? new Date(p.certificateRequestedAt).toLocaleDateString('sk-SK') : '—') + '</td><td>' +
            '<button type="button" class="cert-req-handle" data-id="' + p.id + '" data-code="' + escH(codes[codes.length - 1]) + '">Vybaviť</button>' +
            '</td></tr>';
        }).join('');
        return sectionCard('Žiadosti o osvedčenie (' + reqPilots.length + ')',
          '<p class="muted" style="margin:0 0 0.5rem">Piloti, ktorí požiadali o osvedčenie na lietanie. „Vybaviť“ predvyplní formulár nižšie.</p>' +
          makeTable(['Pilot', 'Útvar', 'Žiada o', 'Dátum žiadosti', ''], rowsHtml, true), '#c47b1a');
      }
      var pendingCount = (pilots || []).filter(function (p) {
        var a = accByPerson[p.personId];
        var lic = a && a.licenseStatus;
        var noCert = !(p.certificateCodes && p.certificateCodes.length);
        return noCert || lic === 'Pending' || lic === 1;
      }).length;
      function certSearchHay(p) {
        var a = accByPerson[p.personId];
        var lic = a && a.licenseStatus;
        var licTxt = (lic === 'Confirmed' || lic === 2) ? 'ano licencia' :
          (lic === 'Pending' || lic === 1) ? 'caka pending' :
          (lic === 'Rejected' || lic === 3) ? 'zamietnuta' : '';
        var certs = p.certificates || [];
        return [
          p.firstName, p.lastName, p.licenseNumber, p.orgUnitName,
          licTxt, (p.certificateCodes || []).join(' ')
        ].concat(certs.map(function (c) {
          var code = c.certificateTypeCode || '';
          return code + ' ' + code.replace(/_/g, '/') + ' ' + (c.externalNumber || '') + ' ' + (c.titleSk || '');
        })).join(' ');
      }
      // ---------- filter (útvar, kategória, stav) — kombinuje sa s hľadaním pilota ----------
      if (!state.certFilter) state.certFilter = { org: '', cat: '', st: '' };
      function certFilterBarHtml() {
        var orgs = {};
        (pilots || []).forEach(function (p) { if (p.orgUnitId) orgs[p.orgUnitId] = p.orgUnitName || p.orgUnitId; });
        var orgOpts = Object.keys(orgs).sort(function (a, b) { return String(orgs[a]).localeCompare(String(orgs[b]), 'sk'); })
          .map(function (id) { return '<option value="' + escH(id) + '"' + (state.certFilter.org === id ? ' selected' : '') + '>' + escH(orgs[id]) + '</option>'; }).join('');
        function o(v, label, cur) { return '<option value="' + v + '"' + (cur === v ? ' selected' : '') + '>' + label + '</option>'; }
        return '<div class="cert-filter-bar">' +
          '<label class="cf-search">Hľadať pilota<input id="cert-search-q" type="search" placeholder="Meno, priezvisko, ČOZ, útvar, číslo osvedčenia…" autocomplete="off" /></label>' +
          '<div id="cert-pilot-hits" class="hidden"></div><input type="hidden" id="cert-pilot" value="" />' +
          '<label>Útvar<select id="cf-org"><option value="">Všetky útvary</option>' + orgOpts + '</select></label>' +
          '<label>Kategória<select id="cf-cat">' + o('', 'Všetky', state.certFilter.cat) + o('A1_A3', 'A1/A3', state.certFilter.cat) +
            o('A2', 'A2', state.certFilter.cat) + o('STS1', 'STS1', state.certFilter.cat) + o('STS2', 'STS2', state.certFilter.cat) + '</select></label>' +
          '<label>Stav<select id="cf-st">' + o('', 'Všetky', state.certFilter.st) + o('valid', 'S platným osvedčením', state.certFilter.st) +
            o('suspended', 'Pozastavené', state.certFilter.st) + o('none', 'Bez osvedčenia', state.certFilter.st) +
            o('request', 'Požiadali o osvedčenie', state.certFilter.st) + o('pending', 'Licencia čaká', state.certFilter.st) + '</select></label>' +
          '<button type="button" class="secondary" id="cf-reset">Zrušiť filter</button>' +
          '<span class="cert-filter-count" id="cf-count"></span>' +
          '</div>';
      }
      function bindCertFilter() {
        ['cf-org', 'cf-cat', 'cf-st'].forEach(function (id) {
          var el = document.getElementById(id);
          if (el) el.onchange = function () {
            state.certFilter = { org: document.getElementById('cf-org').value, cat: document.getElementById('cf-cat').value, st: document.getElementById('cf-st').value };
            filterCertTable(foldText(certSearchInput && certSearchInput.value));
          };
        });
        var reset = document.getElementById('cf-reset');
        if (reset) reset.onclick = function () {
          state.certFilter = { org: '', cat: '', st: '' };
          ['cf-org', 'cf-cat', 'cf-st'].forEach(function (id) { var el = document.getElementById(id); if (el) el.value = ''; });
          filterCertTable(foldText(certSearchInput && certSearchInput.value));
        };
      }
      function certRowsHtml() {
        var rows = (pilots || []).map(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim() || p.licenseNumber || p.id;
          var certs = p.certificates || [];
          var open = !!(state.certOpen || {})[p.id];
          // Kompaktný prehľad: jeden odznak na osvedčenie, farba podľa stavu (detail a akcie až po rozbalení)
          var soon = Date.now() + 60 * 86400000;
          // Odznaky v rovnakých farbách ako v Registri PnD; pozastavené sú prečiarknuté a stlmené.
          var pills = certs.map(function (c) {
            var susp = isCertSuspended(c);
            var tip = (c.externalNumber || '') + (c.validTo ? ' · do ' + fmtDate(c.validTo) : '') + (susp ? ' · pozastavené' : '');
            return '<span class="cp-wrap' + (susp ? ' is-susp' : '') + '" title="' + escH(tip) + '">' + certBadgesHtml([c.certificateTypeCode]) + '</span>';
          }).join('') || '<span class="badge badge-warn">Bez osvedčenia</span>';
          if (requestsOf(p).length) {
            pills += '<span class="badge badge-pending" title="Pilot požiadal o osvedčenie">Žiada o ' +
              requestsOf(p).map(function (c) { return escH(CERT_LABELS[c] || c); }).join(', ') + '</span>';
          }
          // Platnosť: najbližší koniec platnosti spomedzi platných osvedčení
          var activeCerts = certs.filter(function (c) { return !isCertSuspended(c) && c.validTo; });
          var nearest = activeCerts.reduce(function (m, c) { return !m || new Date(c.validTo) < new Date(m.validTo) ? c : m; }, null);
          var validHtml = '<span class="muted">—</span>';
          if (nearest) {
            var t = new Date(nearest.validTo).getTime(), now = Date.now();
            var vcls = t < now ? 'cv-exp' : t < now + 60 * 86400000 ? 'cv-soon' : 'cv-ok';
            var vlbl = String(nearest.certificateTypeCode || '').toUpperCase() === 'A1_A3' ? 'A1/A3' : String(nearest.certificateTypeCode || '').toUpperCase();
            validHtml = '<span class="cert-valid ' + vcls + '" title="Najskôr vyprší: ' + escH(vlbl) + '">' +
              (t < now ? 'neplatné od ' : 'do ') + fmtDate(nearest.validTo) + '</span>';
          } else if (certs.some(function (c) { return isCertSuspended(c); })) {
            validHtml = '<span class="cert-valid cv-susp">pozastavené</span>';
          }
          var certHtml = certs.length
            ? certs.map(function (c) {
                // Mriežka: kategória | stav | číslo | platnosť | akcie — rovnaké stĺpce v každom riadku.
                return '<div class="cert-line">' +
                  '<span class="cert-line-badge">' + certBadgesHtml([c.certificateTypeCode]) + '</span>' +
                  '<span class="cert-line-status">' + certStatusHtml(c) + '</span>' +
                  '<span class="cert-line-num" title="' + escH(c.externalNumber || '') + '">' + escH(c.externalNumber || '—') + '</span>' +
                  '<span class="cert-line-valid">' + (!isCertSuspended(c) && c.validTo ? 'do ' + fmtDate(c.validTo) : '') + '</span>' +
                  '<span class="cert-line-actions">' + certActionButtons(p.id, c, name) + '</span>' +
                '</div>';
              }).join('')
            : '<span class="muted">Žiadne osvedčenie</span>';
          if (requestsOf(p).length) {
            certHtml += '<div style="margin-top:0.25rem"><span class="badge badge-warn">Žiada o: ' +
              requestsOf(p).map(function (c) { return escH(CERT_LABELS[c] || c); }).join(', ') + '</span></div>';
          }
          var fCodes = (p.certificateCodes || []).map(function (c) { return String(c).toUpperCase(); });
          var fStates = [];
          if (!fCodes.length) fStates.push('none');
          if (certs.some(function (c) { return isCertSuspended(c); })) fStates.push('suspended');
          if (fCodes.length) fStates.push('valid');
          if (requestsOf(p).length) fStates.push('request');
          var lic = accByPerson[p.personId] && accByPerson[p.personId].licenseStatus;
          if (lic === 'Pending' || lic === 1) fStates.push('pending');
          return '<tr class="cert-pilot-row' + (open ? ' is-open' : '') + '" data-pilot="' + p.id + '" data-org="' + escH(p.orgUnitId || '') + '" data-codes="' + fCodes.join(' ') +
            '" data-states="' + fStates.join(' ') + '" title="Rozbaliť osvedčenia pilota" data-search="' +
            String(certSearchHay(p)).replace(/"/g, '&quot;') + '"><td><div class="cert-name-cell"><span class="cert-person">' + avatarHtml(p.firstName, p.lastName, p.id) +
            '<span class="cert-person-txt"><b>' + escH(name) + '</b>' +
            '<small>' + escH(p.personalBadgeNumber || p.email || '') + '</small></span></span>' +
            '</div></td>' +
            '<td><span class="cert-org" title="' + escH(p.orgUnitName || '') + '">' + escH(p.orgUnitName || '—') + '</span></td>' +
            '<td>' + licChip(accByPerson[p.personId]) + '</td>' +
            '<td><div class="cp-list">' + pills + '</div></td>' +
            '<td>' + validHtml + '</td>' +
            '<td class="cert-acts"><div class="cert-acts-in">' +
            '<button type="button" class="cert-act cert-act-grant cert-quick-grant" data-id="' + p.id + '" title="Prideliť osvedčenie pilotovi ' + escH(name) + '">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg><span>Prideliť</span></button>' +
            '<button type="button" class="cert-act cert-act-open cert-toggle" data-id="' + p.id + '" title="Zobraziť detail osvedčení" aria-expanded="' + open + '">' +
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg></button>' +
            '</div></td></tr>' +
            '<tr class="cert-detail-row" data-detail="' + p.id + '"' + (open ? '' : ' hidden') + '><td colspan="6"><div class="cert-detail">' +
            '<div class="cert-detail-lines">' + certHtml + '</div>' +
            '<div class="cert-detail-foot"><button type="button" class="linkish cert-open-pilot" data-id="' + p.id + '">Otvoriť kartu pilota ›</button></div>' +
            '</div></td></tr>';
        }).join('') || '<tr><td colspan="6">Žiadni piloti (úroveň 3) v dostupných útvaroch.</td></tr>';
        if ((pilots || []).length) {
          rows += '<tr id="cert-search-empty" hidden><td colspan="6">Žiadny pilot nevyhovuje hľadaniu alebo filtru.</td></tr>';
        }
        return rows;
      }
      function rebuildCertPilotOpts() {
        return (pilots || []).map(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim() || p.licenseNumber || p.id;
          return {
            value: p.id,
            name: name,
            org: p.orgUnitName || '—',
            text: name + ' — ' + (p.orgUnitName || ''),
            search: certSearchHay(p)
          };
        });
      }
      function certChartsFor() {
        var licPending = (pilots || []).filter(function (p) {
          var a = accByPerson[p.personId]; return a && (a.licenseStatus === 'Pending' || a.licenseStatus === 1);
        }).length;
        return certChartsHtml(pilots || [], { licPending: licPending });
      }
      var rows = certRowsHtml();
      var allCertPilotOpts = rebuildCertPilotOpts();
      content.innerHTML = '<div class="stack">' +
        pageHead('Osvedčenia',
          'Gestor (L1) a prevádzkovateľ (L2) prideľujú a odoberajú osvedčenia pilotom (L3). Po pridelení sa licencia potvrdí.',
          chipHtml('Piloti', (pilots || []).length, 'chip-soft') +
          chipHtml('Bez osvedčenia / čaká', pendingCount, pendingCount ? 'chip-danger' : 'chip-ok')) +
        '<div id="cert-charts">' + certChartsFor() + '</div>' +
        '<div id="cert-msg"></div>' +
        certSuspendPanelHtml() +
        '<div id="cert-requests">' + certRequestsHtml() + '</div>' +
        sectionCard('Piloti a osvedčenia',
          certFilterBarHtml() +
          '<div id="cert-pilot-table">' +
          makeTable(['Pilot', 'Útvar', 'Licencia', 'Osvedčenia', 'Platnosť', ''], rows, true) +
          '</div>', '#0d8f7a') +
        '</div>';
      var certSearchInput = document.getElementById('cert-search-q');
      var certPilotHidden = document.getElementById('cert-pilot');
      var certHits = document.getElementById('cert-pilot-hits');
      var selectedPilotId = '';
      function hideCertHits() {
        if (!certHits) return;
        certHits.classList.add('hidden');
        certHits.innerHTML = '';
      }
      function filterCertTable(q) {
        var tableRows = content.querySelectorAll('#cert-pilot-table tbody tr[data-search]');
        var shown = 0;
        var f = state.certFilter || {};
        tableRows.forEach(function (tr) {
          var ok = !q || foldText(tr.getAttribute('data-search')).indexOf(q) >= 0;
          if (ok && f.org) ok = tr.getAttribute('data-org') === f.org;
          if (ok && f.cat) ok = (' ' + (tr.getAttribute('data-codes') || '') + ' ').indexOf(' ' + f.cat + ' ') >= 0;
          if (ok && f.st) ok = (' ' + (tr.getAttribute('data-states') || '') + ' ').indexOf(' ' + f.st + ' ') >= 0;
          tr.hidden = !ok;
          if (ok) shown++;
          var det = tr.nextElementSibling;
          if (det && det.classList.contains('cert-detail-row')) det.hidden = !ok || !tr.classList.contains('is-open');
        });
        var emptyRow = document.getElementById('cert-search-empty');
        if (emptyRow) emptyRow.hidden = shown > 0 || !tableRows.length;
        var cnt = document.getElementById('cf-count');
        if (cnt) cnt.textContent = (q || f.org || f.cat || f.st) ? 'Zobrazených ' + shown + ' z ' + tableRows.length + ' pilotov' : tableRows.length + ' pilotov';
      }
      async function reloadCertData() {
        try { pilots = await api('/api/v1/pilots'); } catch (_) { pilots = []; }
        try { accounts = await api('/api/v1/accounts'); } catch (_) { accounts = []; }
        accByPerson = {};
        (accounts || []).forEach(function (a) { accByPerson[a.personId] = a; });
      }
      function paintCertTable() {
        var wrap = document.getElementById('cert-pilot-table');
        if (!wrap) return;
        wrap.innerHTML = makeTable(['Pilot', 'Útvar', 'Licencia', 'Osvedčenia', 'Platnosť', ''], certRowsHtml(), true);
        var reqWrap = document.getElementById('cert-requests');
        if (reqWrap) { reqWrap.innerHTML = certRequestsHtml(); bindCertRequestButtons(); }
        allCertPilotOpts = rebuildCertPilotOpts();
        var chartsBox = document.getElementById('cert-charts');
        if (chartsBox) { chartsBox.innerHTML = certChartsFor(); bindChartToggles(chartsBox); }
        bindCertPageActions();
        bindCertRowActions();
        filterCertTable(foldText(certSearchInput && certSearchInput.value));
      }
      function bindCertPageActions() {
        bindCertLifecycleButtons(async function (okMsg, errMsg) {
          var box = document.getElementById('cert-msg');
          if (box) {
            box.innerHTML = errMsg
              ? '<div class="error">' + errMsg + '</div>'
              : '<div class="msg">' + okMsg + '</div>';
          }
          if (!errMsg) {
            await reloadCertData();
            paintCertTable();
          }
        });
      }
      function bindCertRequestButtons() {
        content.querySelectorAll('.cert-req-handle').forEach(function (btn) {
          btn.onclick = function () { openGrantDialog(btn.dataset.id, btn.dataset.code); };
        });
      }
      // Klik na pilota vo výsledkoch: „+ Prideliť“ predvyplní formulár hore, riadok / „Detail pilota“ otvorí kartu pilota.
      function bindCertRowActions() {
        content.querySelectorAll('.cert-quick-grant').forEach(function (btn) {
          btn.onclick = function (ev) {
            ev.stopPropagation();
            openGrantDialog(btn.dataset.id);
          };
        });
        content.querySelectorAll('.cert-open-pilot').forEach(function (btn) {
          btn.onclick = function (ev) {
            ev.stopPropagation();
            state.pilotDetailId = btn.dataset.id;
            navigate('pilots');
          };
        });
        function toggleCertRow(id) {
          var tr = content.querySelector('tr.cert-pilot-row[data-pilot="' + id + '"]');
          var det = content.querySelector('tr.cert-detail-row[data-detail="' + id + '"]');
          if (!tr || !det) return;
          state.certOpen = state.certOpen || {};
          var nowOpen = !tr.classList.contains('is-open');
          state.certOpen[id] = nowOpen;
          tr.classList.toggle('is-open', nowOpen);
          det.hidden = !nowOpen;
          var b = tr.querySelector('.cert-toggle');
          if (b) b.setAttribute('aria-expanded', String(nowOpen));
        }
        content.querySelectorAll('.cert-toggle').forEach(function (btn) {
          btn.onclick = function (ev) { ev.stopPropagation(); toggleCertRow(btn.dataset.id); };
        });
        content.querySelectorAll('tr.cert-pilot-row').forEach(function (tr) {
          tr.onclick = function (ev) {
            if (ev.target.closest('button, a, input, select')) return;
            toggleCertRow(tr.dataset.pilot);
          };
        });
      }
      function pickCertPilot(opt) {
        if (!opt) return;
        selectedPilotId = opt.value;
        if (certPilotHidden) certPilotHidden.value = selectedPilotId;
        if (certSearchInput) certSearchInput.value = opt.name;
        state.certSearchQ = opt.name;
        hideCertHits();
        filterCertTable(foldText(opt.name));
      }
      function renderCertHits(matches, q) {
        if (!certHits) return;
        if (!q) {
          hideCertHits();
          return;
        }
        certHits.classList.remove('hidden');
        if (!matches.length) {
          certHits.innerHTML = '<p class="hint muted">Žiadna zhoda</p>';
          return;
        }
        var max = 12;
        var extra = matches.length > max ? matches.length - max : 0;
        certHits.innerHTML = matches.slice(0, max).map(function (opt) {
          return '<button type="button" class="cert-pilot-hit" data-id="' + opt.value + '">' +
            opt.name + '<span class="meta">' + opt.org + '</span></button>';
        }).join('') + (extra ? '<p class="hint muted">…a ďalších ' + extra + '</p>' : '');
        certHits.querySelectorAll('.cert-pilot-hit').forEach(function (btn) {
          btn.onmousedown = function (ev) { ev.preventDefault(); };
          btn.onclick = function () {
            var picked = allCertPilotOpts.find(function (o) { return o.value === btn.dataset.id; });
            pickCertPilot(picked);
          };
        });
      }
      function applyCertSearch() {
        var raw = (certSearchInput && certSearchInput.value) || '';
        var q = foldText(raw);
        state.certSearchQ = raw;
        if (selectedPilotId) {
          var current = allCertPilotOpts.find(function (o) { return o.value === selectedPilotId; });
          if (!current || foldText(current.name) !== q) {
            selectedPilotId = '';
            if (certPilotHidden) certPilotHidden.value = '';
          }
        }
        filterCertTable(q);
        if (selectedPilotId) {
          hideCertHits();
          return;
        }
      }
      if (certSearchInput) {
        certSearchInput.value = '';
        state.certSearchQ = '';
        certSearchInput.oninput = applyCertSearch;
        certSearchInput.onkeydown = function (ev) {
          if (ev.key === 'Escape') {
            hideCertHits();
            return;
          }
          if (ev.key === 'Enter' && !selectedPilotId) {
            var q = foldText(certSearchInput.value);
            var matches = q ? allCertPilotOpts.filter(function (opt) {
              return foldText(opt.search).indexOf(q) >= 0;
            }) : [];
            if (matches.length) {
              ev.preventDefault();
              pickCertPilot(matches[0]);
            }
          }
        };
        certSearchInput.onblur = function () {
          setTimeout(hideCertHits, 120);
        };
        hideCertHits();
      }
      // Okno „Prideliť osvedčenie“ — otvorí sa až pri konkrétnom pilotovi (tlačidlo „+ Prideliť“ / „Vybaviť“).
      var CERT_ORDER = ['A1_A3', 'A2', 'STS1', 'STS2'];
      var CERT_LABEL = { A1_A3: 'A1/A3 — otvorená kategória', A2: 'A2 — otvorená kategória', STS1: 'STS1 — štandardný scenár', STS2: 'STS2 — štandardný scenár' };
      function normCertCode(c) {
        var t = String(c || '').toUpperCase().replace(/[-\/\s]/g, '_');
        if (t.indexOf('STS2') === 0 || t === 'STS_2' || t === 'STS02') return 'STS2';
        if (t.indexOf('STS') === 0) return 'STS1';
        if (t === 'A1A3' || t === 'A1_A3') return 'A1_A3';
        return t;
      }
      function openGrantDialog(pilotId, requestedCode) {
        var p = (pilots || []).find(function (x) { return x.id === pilotId; });
        if (!p) return;
        var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim();
        var have = (p.certificateCodes || []).map(normCertCode);
        var top = -1;
        have.forEach(function (c) { top = Math.max(top, CERT_ORDER.indexOf(c)); });
        var def = requestedCode ? normCertCode(requestedCode) : CERT_ORDER[Math.min(top + 1, CERT_ORDER.length - 1)];
        var wrap = document.createElement('div');
        wrap.className = 'ui-dialog-backdrop pm-backdrop';
        wrap.innerHTML = '<div class="pm pm-edit" role="dialog" aria-modal="true"><form class="pm-form">' +
          '<div class="pm-edit-head"><span class="cg-ico" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="9" r="5"/><path d="m8.5 13.5-1.5 7 5-2.5 5 2.5-1.5-7"/></svg></span>' +
          '<div><h3>Prideliť osvedčenie</h3><span>' + escH(name) + ' · ' + escH(p.orgUnitName || '—') + '</span></div>' +
          '<button type="button" class="pm-x" data-close aria-label="Zavrieť">×</button></div>' +
          '<div class="cg-current"><span>Má teraz:</span> ' + (have.length ? certBadgesHtml(p.certificateCodes) : '<span class="badge badge-warn">Bez osvedčenia</span>') +
          ((p.requestedCertificateCodes || []).length ? ' <span class="badge badge-pending">Žiada o ' + (p.requestedCertificateCodes || []).map(function (c) { return c === 'A1_A3' ? 'A1/A3' : c; }).join(', ') + '</span>' : '') + '</div>' +
          '<div class="pm-fields">' +
          '<label class="pm-wide">Typ osvedčenia<select name="type">' + CERT_ORDER.map(function (c) {
            var owned = have.indexOf(c) >= 0;
            return '<option value="' + c + '"' + (c === def ? ' selected' : '') + '>' + CERT_LABEL[c] + (owned ? ' (už má — obnoviť)' : '') + '</option>';
          }).join('') + '</select><small>Vyššie osvedčenie doplní aj všetky nižšie (STS2 → STS1 + A2 + A1/A3).</small></label>' +
          '<label class="pm-wide">Číslo osvedčenia<input name="number" placeholder="voliteľné" autocomplete="off"></label>' +
          '<label>Platnosť od<input name="from" type="date"></label>' +
          '<label>Platnosť do<input name="to" type="date"></label>' +
          '</div><div class="pm-msg"></div>' +
          '<div class="pm-actions"><button type="button" class="secondary" data-close>Zrušiť</button><button type="submit">Prideliť osvedčenie</button></div>' +
          '</form></div>';
        document.body.appendChild(wrap);
        document.body.classList.add('pm-lock');
        function close() { document.removeEventListener('keydown', onKey); document.body.classList.remove('pm-lock'); wrap.remove(); }
        function onKey(ev) { if (ev.key === 'Escape') close(); }
        document.addEventListener('keydown', onKey);
        wrap.addEventListener('mousedown', function (ev) { if (ev.target === wrap) close(); });
        wrap.querySelectorAll('[data-close]').forEach(function (b) { b.onclick = close; });
        var form = wrap.querySelector('form');
        setTimeout(function () { form.type.focus(); }, 20);
        form.onsubmit = async function (ev) {
          ev.preventDefault();
          var btn = form.querySelector('button[type="submit"]');
          var box = wrap.querySelector('.pm-msg');
          btn.disabled = true; box.innerHTML = '';
          try {
            await api('/api/v1/pilots/' + pilotId + '/certificates/grant', {
              method: 'POST',
              body: JSON.stringify({
                certificateTypeCode: form.type.value,
                externalNumber: form.number.value.trim() || null,
                validFrom: form.from.value ? new Date(form.from.value).toISOString() : null,
                validTo: form.to.value ? new Date(form.to.value).toISOString() : null
              })
            });
            close();
            await reloadCertData();
            paintCertTable();
            var msg = document.getElementById('cert-msg');
            if (msg) msg.innerHTML = '<div class="msg">Osvedčenie ' + escH(form.type.value === 'A1_A3' ? 'A1/A3' : form.type.value) + ' pridelené pilotovi ' + escH(name) + '.</div>';
          } catch (err) {
            btn.disabled = false;
            box.innerHTML = '<div class="error">' + escH(err.message || 'Pridelenie zlyhalo') + '</div>';
          }
        };
      }
      bindChartToggles(content);
      bindCertPageActions();
      bindCertRequestButtons();
      bindCertRowActions();
      bindCertFilter();
      filterCertTable('');
      return;
}
