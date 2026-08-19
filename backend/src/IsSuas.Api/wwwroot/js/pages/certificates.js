import { api } from '../core/api.js';
import { state } from '../core/state.js';
import { certBadgesHtml, chipHtml, content, foldText, makeTable, pageHead, sectionCard } from '../core/ui.js';
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
      function certRowsHtml() {
        var rows = (pilots || []).map(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim() || p.licenseNumber || p.id;
          var certs = p.certificates || [];
          var certHtml = certs.length
            ? certs.map(function (c) {
                return '<div class="row-actions" style="justify-content:flex-start;margin:0.15rem 0">' +
                  certBadgesHtml([c.certificateTypeCode]) +
                  certStatusHtml(c) +
                  '<span class="muted">' + (c.externalNumber || '') +
                  (!isCertSuspended(c) && c.validTo ? ' · do ' + fmtDate(c.validTo) : '') +
                  '</span>' +
                  certActionButtons(p.id, c, name) +
                '</div>';
              }).join('')
            : '<span class="muted">Žiadne osvedčenie</span>';
          return '<tr data-search="' + String(certSearchHay(p)).replace(/"/g, '&quot;') + '"><td>' + name +
            '</td><td>' + (p.orgUnitName || '—') + '</td><td>' +
            licChip(accByPerson[p.personId]) + '</td><td>' + certHtml + '</td></tr>';
        }).join('') || '<tr><td colspan="4">Žiadni piloti (úroveň 3) v dostupných útvaroch.</td></tr>';
        if ((pilots || []).length) {
          rows += '<tr id="cert-search-empty" hidden><td colspan="4">Žiadny pilot podľa hľadania.</td></tr>';
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
      var rows = certRowsHtml();
      var allCertPilotOpts = rebuildCertPilotOpts();
      content.innerHTML = '<div class="stack">' +
        pageHead('Osvedčenia',
          'Gestor (L1) a prevádzkovateľ (L2) prideľujú a odoberajú osvedčenia pilotom (L3). Po pridelení sa licencia potvrdí.',
          chipHtml('Piloti', (pilots || []).length, 'chip-soft') +
          chipHtml('Bez osvedčenia / čaká', pendingCount, pendingCount ? 'chip-danger' : 'chip-ok')) +
        '<div class="form-panel"><h3>Prideliť osvedčenie</h3>' +
        '<p class="muted">Vyššie osvedčenie doplní aj všetky nižšie: STS2 → STS1 + A2 + A1/A3. Odobratie alebo pozastavenie nižšieho stupňa stiahne aj vyššie (A1/A3 odoberie/pozastaví A2, STS1 aj STS2). Po uplynutí doby sa pozastavenie samo zruší.</p>' +
        '<form id="cert-grant-form" class="form-grid">' +
        '<label class="full">Pilot' +
        '<div class="cert-combo">' +
        '<input id="cert-search-q" type="search" placeholder="Hľadať pilota…" autocomplete="off" />' +
        '<div id="cert-pilot-hits" class="cert-pilot-hits hidden"></div>' +
        '</div>' +
        '<input type="hidden" id="cert-pilot" value="" />' +
        '</label>' +
        '<label>Typ osvedčenia<select id="cert-type" required>' +
        '<option value="A1_A3">A1/A3 — otvorená kategória</option>' +
        '<option value="A2">A2 — otvorená kategória</option>' +
        '<option value="STS1">STS1 — štandardný scenár</option>' +
        '<option value="STS2">STS2 — štandardný scenár</option>' +
        '</select></label>' +
        '<label>Číslo osvedčenia<input id="cert-number" placeholder="voliteľné" /></label>' +
        '<label>Platnosť od<input id="cert-from" type="date" /></label>' +
        '<label>Platnosť do<input id="cert-to" type="date" /></label>' +
        '<div class="full toolbar"><button type="submit">Prideliť osvedčenie</button></div>' +
        '</form><div id="cert-msg"></div></div>' +
        certSuspendPanelHtml() +
        sectionCard('Piloti a osvedčenia',
          '<div id="cert-pilot-table">' +
          makeTable(['Pilot', 'Útvar', 'Licencia', 'Osvedčenia'], rows, true) +
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
        tableRows.forEach(function (tr) {
          var ok = !q || foldText(tr.getAttribute('data-search')).indexOf(q) >= 0;
          tr.hidden = !ok;
          if (ok) shown++;
        });
        var emptyRow = document.getElementById('cert-search-empty');
        if (emptyRow) emptyRow.hidden = shown > 0 || !tableRows.length;
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
        wrap.innerHTML = makeTable(['Pilot', 'Útvar', 'Licencia', 'Osvedčenia'], certRowsHtml(), true);
        allCertPilotOpts = rebuildCertPilotOpts();
        bindCertPageActions();
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
        var matches = !q ? [] : allCertPilotOpts.filter(function (opt) {
          return foldText(opt.search).indexOf(q) >= 0;
        });
        renderCertHits(matches, raw.trim());
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
      document.getElementById('cert-grant-form').onsubmit = async function (ev) {
        ev.preventDefault();
        var msg = document.getElementById('cert-msg');
        var pilotId = document.getElementById('cert-pilot').value;
        if (!pilotId) {
          msg.innerHTML = '<div class="error">Vyberte pilota.</div>';
          return;
        }
        var fromVal = document.getElementById('cert-from').value;
        var toVal = document.getElementById('cert-to').value;
        try {
          await api('/api/v1/pilots/' + pilotId + '/certificates/grant', {
            method: 'POST',
            body: JSON.stringify({
              certificateTypeCode: document.getElementById('cert-type').value,
              externalNumber: document.getElementById('cert-number').value.trim() || null,
              validFrom: fromVal ? new Date(fromVal).toISOString() : null,
              validTo: toVal ? new Date(toVal).toISOString() : null
            })
          });
          await reloadCertData();
          paintCertTable();
          msg.innerHTML = '<div class="msg">Osvedčenie pridelené.</div>';
        } catch (err) {
          msg.innerHTML = '<div class="error">' + (err.message || 'Pridelenie zlyhalo') + '</div>';
        }
      };
      bindCertPageActions();
      return;
}
