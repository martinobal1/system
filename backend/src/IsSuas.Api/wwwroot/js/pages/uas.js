import { api } from '../core/api.js';
import { bindChartTooltips, colorForKey, fleetModelsFromUasList, renderFleetModelsPanel, swatchHtml } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, confirmAction, content, makeTable, pageHead, searchHighlightBanner, sectionCard, statusLabelSk } from '../core/ui.js';
import { getTechDocCatalog, techDocSlug } from '../shared/techDocs.js';

function escAttr(value) {
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;');
}

export async function renderUas() {
      if (state.uasDetailId) {
        const detail = await api('/api/v1/uas/' + state.uasDetailId);
        const u = detail.uas;
        const orgs = await api('/api/v1/org-units');
        const hist = (detail.assignments || []).map(function (a) {
          return '<tr><td>' + a.orgUnitName + '</td><td>' + new Date(a.validFrom).toLocaleDateString('sk-SK') +
            '</td><td>' + (a.validTo ? new Date(a.validTo).toLocaleDateString('sk-SK') : '—') +
            '</td><td>' + (a.reason || '') + '</td></tr>';
        }).join('') || '<tr><td colspan="4">Bez historie</td></tr>';
        const pilots = (detail.authorizedPilots || []).map(function (p) {
          return '<tr><td>' + p.pilotName + '</td><td>' + new Date(p.validFrom).toLocaleDateString('sk-SK') +
            '</td><td>' + (p.validTo ? new Date(p.validTo).toLocaleDateString('sk-SK') : 'platne') + '</td></tr>';
        }).join('') || '<tr><td colspan="3">Ziadni opravneni piloti</td></tr>';
        const orgOpts = orgs.map(function (o) {
          return '<option value="' + o.id + '"' + (o.id === u.owningOrgUnitId ? ' selected' : '') + '>' +
            o.code + ' — ' + o.name + '</option>';
        }).join('');
        const canEditUas = state.accessLevel === 1
          && u.status !== 'Decommissioned' && u.status !== 8;
        const editOpen = canEditUas && !!state.uasEditing;
        state.uasEditing = false;
        content.innerHTML = '<div class="stack">' +
          pageHead('Detail UAS', (u.registrationNumber || u.serialNumber) + ' · ' + u.manufacturer + ' ' + u.model,
            chipHtml('Stav', statusLabelSk(u.status), 'chip-soft') +
            chipHtml('Utvar', u.owningOrgUnitName || '—')) +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="uas-back">Spat na zoznam</button> ' +
          (u.status === 'Draft' || u.status === 0
            ? '<button type="button" id="uas-register">Zaregistrovať</button> '
            : '') +
          (canEditUas
            ? '<button type="button" class="secondary" id="uas-edit">Upraviť</button> '
            : '') +
          (u.status !== 'Decommissioned' && u.status !== 8
            ? '<button type="button" class="secondary" id="uas-retire">Vyradiť</button>' : '') +
          '</div>' +
          '<div id="uas-msg"></div>' +
          (canEditUas
            ? '<div id="uas-edit-panel" class="form-panel' + (editOpen ? '' : ' hidden') + '"><h3>Upraviť kartu</h3>' +
              '<form id="uas-edit-form" class="form-grid">' +
              '<label>Výrobné číslo<input id="uas-e-serial" required maxlength="128" value="' + escAttr(u.serialNumber) + '" /></label>' +
              '<label>Výrobca<input id="uas-e-mfr" required maxlength="128" value="' + escAttr(u.manufacturer) + '" /></label>' +
              '<label>Typ (model)<input id="uas-e-model" required maxlength="128" value="' + escAttr(u.model) + '" /></label>' +
              '<label>Č. IM<input id="uas-e-im" maxlength="64" value="' + escAttr(u.inventoryAssetNumber) + '" /></label>' +
              '<label>Ev.č. denníka<input id="uas-e-logbook" maxlength="64" value="' + escAttr(u.logbookNumber) + '" /></label>' +
              '<div class="full toolbar"><button type="submit">Uložiť zmeny</button>' +
              '<button type="button" class="secondary" id="uas-e-cancel">Zrušiť</button></div></form></div>'
            : '') +
          '<div class="form-panel"><h3>Údaje registra</h3><dl class="flight-meta">' +
          '<div><dt>Č. osvedčenia</dt><dd>' + (u.certificateNumber || '—') + '</dd></div>' +
          '<div><dt>Dátum výmazu</dt><dd>' + (u.deregisteredAt ? new Date(u.deregisteredAt).toLocaleDateString('sk-SK') : '—') + '</dd></div>' +
          '<div><dt>Poznávacia značka</dt><dd>' + (u.registrationNumber || '—') + '</dd></div>' +
          '<div><dt>Typ</dt><dd id="uas-detail-type">' + (u.model || '—') + '</dd></div>' +
          '<div><dt>Výrobca</dt><dd>' + (u.manufacturer || '—') + '</dd></div>' +
          '<div><dt>Výrobné číslo</dt><dd>' + (u.serialNumber || '—') + '</dd></div>' +
          '<div><dt>Ev.č. denníka</dt><dd>' + (u.logbookNumber || '—') + '</dd></div>' +
          '<div><dt>Užívateľ (útvar)</dt><dd>' + (u.owningOrgUnitName || '—') + '</dd></div>' +
          '<div><dt>Č. IM</dt><dd>' + (u.inventoryAssetNumber || '—') + '</dd></div>' +
          '</dl></div>' +
          '<div class="form-panel"><h3>Transfer na iny utvar</h3>' +
          '<form id="uas-transfer" class="form-grid">' +
          '<label class="full">Novy utvar<select id="uas-new-org">' + orgOpts + '</select></label>' +
          '<label class="full">Dovod<input id="uas-reason" required placeholder="Dovod transferu" /></label>' +
          '<div class="full toolbar"><button type="submit">Presunut UAS</button></div></form></div>' +
          sectionCard('Historia pridelení', makeTable(['Utvar', 'Od', 'Do', 'Dovod'], hist, true), '#2b7bb9') +
          sectionCard('Opravneni piloti', makeTable(['Pilot', 'Od', 'Do'], pilots, true), '#0d8f7a') +
          '</div>';
        document.getElementById('uas-back').onclick = function () {
          state.uasDetailId = null;
          state.uasEditing = false;
          navigate('uas');
        };
        var editBtn = document.getElementById('uas-edit');
        var editPanel = document.getElementById('uas-edit-panel');
        if (editBtn && editPanel) {
          editBtn.onclick = function () {
            editPanel.classList.toggle('hidden');
            if (!editPanel.classList.contains('hidden')) {
              var serialEl = document.getElementById('uas-e-serial');
              if (serialEl) serialEl.focus();
            }
          };
        }
        var editCancel = document.getElementById('uas-e-cancel');
        if (editCancel && editPanel) {
          editCancel.onclick = function () { editPanel.classList.add('hidden'); };
        }
        var editForm = document.getElementById('uas-edit-form');
        if (editForm) {
          editForm.onsubmit = async function (ev) {
            ev.preventDefault();
            try {
              await api('/api/v1/uas/' + u.id, {
                method: 'PATCH',
                body: JSON.stringify({
                  serialNumber: document.getElementById('uas-e-serial').value.trim(),
                  manufacturer: document.getElementById('uas-e-mfr').value.trim(),
                  model: document.getElementById('uas-e-model').value.trim(),
                  logbookNumber: document.getElementById('uas-e-logbook').value.trim() || null,
                  inventoryAssetNumber: document.getElementById('uas-e-im').value.trim() || null,
                  rowVersion: u.rowVersion
                })
              });
              state.uasEditing = false;
              navigate('uas');
            } catch (err) {
              document.getElementById('uas-msg').innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }
        if (editOpen) {
          var serialFocus = document.getElementById('uas-e-serial');
          if (serialFocus) serialFocus.focus();
        }
        var registerBtn = document.getElementById('uas-register');
        if (registerBtn) registerBtn.onclick = async function () {
          try {
            await api('/api/v1/uas/' + u.id + '/submit', { method: 'POST', body: '{}' }).catch(function () {});
            await api('/api/v1/uas/' + u.id + '/approve', { method: 'POST', body: '{}' });
            navigate('uas');
          } catch (err) { document.getElementById('uas-msg').innerHTML = '<div class="error">' + err.message + '</div>'; }
        };
        var retireBtn = document.getElementById('uas-retire');
        if (retireBtn) retireBtn.onclick = async function () {
          if (!confirmAction('Vyradiť tento UAS?')) return;
          try {
            await api('/api/v1/uas/' + u.id + '/transition', {
              method: 'POST',
              body: JSON.stringify({ targetStatus: 8, reason: 'Vyradenie', rowVersion: u.rowVersion })
            });
            state.uasDetailId = null; navigate('uas');
          } catch (err) { document.getElementById('uas-msg').innerHTML = '<div class="error">' + err.message + '</div>'; }
        };
        var detailCatalog = getTechDocCatalog();
        var detailModel = u.model || '';
        var detailMfr = u.manufacturer || '';
        var detailDoc = detailModel ? detailCatalog.find(function (d) {
          if (d.id === techDocSlug(detailMfr, detailModel)) return true;
          return (d.matchModels || []).some(function (m) { return m.toLowerCase() === detailModel.toLowerCase(); });
        }) : null;
        if (detailDoc) {
          var typeEl = document.getElementById('uas-detail-type');
          typeEl.innerHTML = '<button type="button" class="linkish" id="uas-detail-type-link">' + detailModel + '</button>';
          document.getElementById('uas-detail-type-link').onclick = function () {
            state.techDocModelId = detailDoc.id;
            state.techDocTab = 'specs';
            navigate('components');
          };
        }
        document.getElementById('uas-transfer').onsubmit = async function (ev) {
          ev.preventDefault();
          try {
            await api('/api/v1/uas/' + u.id + '/transfer', {
              method: 'POST',
              body: JSON.stringify({
                newOrgUnitId: document.getElementById('uas-new-org').value,
                effectiveAt: new Date().toISOString(),
                reason: document.getElementById('uas-reason').value.trim()
              })
            });
            navigate('uas');
          } catch (err) {
            document.getElementById('uas-msg').innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
        return;
      }
      const data = await api('/api/v1/uas?page=1&pageSize=500');
      const list = data.items || [];
      const canAddUas = state.accessLevel === 1;
      var orgCreateOpts = '';
      if (canAddUas) {
        const orgsCreate = await api('/api/v1/org-units');
        orgCreateOpts = orgsCreate.map(function (o) {
          return '<option value="' + o.id + '">' + o.code + ' — ' + o.name + '</option>';
        }).join('');
      }
      var catalog = getTechDocCatalog();
      const rows = list.map(function (u, idx) {
        var key = u.registrationNumber || u.serialNumber || u.id;
        var dereg = u.deregisteredAt
          ? new Date(u.deregisteredAt).toLocaleDateString('sk-SK')
          : '—';
        var mfr = u.manufacturer || '';
        var mdl = u.model || '';
        var docEntry = mdl ? catalog.find(function (d) {
          if (d.id === techDocSlug(mfr, mdl)) return true;
          return (d.matchModels || []).some(function (m) {
            return m.toLowerCase() === mdl.toLowerCase();
          });
        }) : null;
        var typeCell = docEntry
          ? '<button type="button" class="linkish uas-type-link" data-slug="' + docEntry.id + '">' + (mdl || '—') + '</button>'
          : (mdl || '—');
        return '<tr class="row-tint uas-row" data-id="' + u.id + '" style="--row-accent:' + colorForKey(key) + ';cursor:pointer">' +
          '<td>' + (idx + 1) + '</td>' +
          '<td>' + (u.certificateNumber || '—') + '</td>' +
          '<td>' + dereg + '</td>' +
          '<td><span class="entity-label">' + swatchHtml(colorForKey(key)) +
          '<span>' + (u.registrationNumber || '—') + '</span></span></td>' +
          '<td>' + typeCell + '</td>' +
          '<td>' + (mfr || '—') + '</td>' +
          '<td>' + (u.serialNumber || '—') + '</td>' +
          '<td>' + (u.logbookNumber || '—') + '</td>' +
          '<td>' + (u.owningOrgUnitName || '—') + '</td>' +
          '<td>' + (u.inventoryAssetNumber || '—') + '</td>' +
          (canAddUas
            ? '<td>' + (u.status !== 'Decommissioned' && u.status !== 8
              ? '<button type="button" class="secondary uas-edit" data-id="' + u.id + '">Upraviť</button>'
              : '—') + '</td>'
            : '') +
          '</tr>';
      }).join('') || '<tr><td colspan="' + (canAddUas ? '11' : '10') + '">Zatiaľ žiadne UAS v registri</td></tr>';
      const uasModelsPanel = renderFleetModelsPanel(fleetModelsFromUasList(list), list.length, {
        subtitle: 'Zloženie registra podľa typu UAS',
        limit: 12
      });
      content.innerHTML = '<div class="stack">' + searchHighlightBanner() +
        pageHead('Register UAS',
          canAddUas
            ? 'Register štátnych bezpilotných lietadiel — pridať / upraviť / vyradiť.'
            : 'Register štátnych bezpilotných lietadiel. Pridať môže len L1.',
          chipHtml('UAS', list.length, 'chip-soft')) +
        (canAddUas
          ? '<div class="toolbar"><button type="button" id="uas-add-btn">Pridať UAS</button></div>' +
            '<div id="uas-list-msg"></div>' +
            '<div id="uas-create-panel" class="form-panel hidden"><h3>Nový UAS</h3>' +
            '<form id="uas-create-form" class="form-grid">' +
            '<label>Výrobné číslo<input id="uas-c-serial" required /></label>' +
            '<label>Výrobca<input id="uas-c-mfr" required /></label>' +
            '<label>Typ (model)<input id="uas-c-model" required /></label>' +
            '<label>Č. IM<input id="uas-c-im" placeholder="voliteľné" /></label>' +
            '<label>Ev.č. denníka<input id="uas-c-logbook" placeholder="voliteľné" /></label>' +
            '<label>Útvar<select id="uas-c-org">' + orgCreateOpts + '</select></label>' +
            '<div class="full toolbar"><button type="submit">Uložiť</button>' +
            '<button type="button" class="secondary" id="uas-c-cancel">Zrušiť</button></div></form></div>'
          : '') +
        uasModelsPanel +
        '<div class="chart-panel table-scroll">' +
        makeTable([
          'P.č.',
          'Č. osvedčenia o zápise do registra',
          'Dátum výmazu z registra',
          'Poznávacia značka',
          'Typ',
          'Výrobca',
          'Výrobné číslo',
          'Ev.č. denníka',
          'Užívateľ (útvar)',
          'Č. IM'
        ].concat(canAddUas ? ['Akcie'] : []), rows, true) +
        '</div></div>';
      bindChartTooltips(content);
      if (canAddUas) {
        document.getElementById('uas-add-btn').onclick = function () {
          var panel = document.getElementById('uas-create-panel');
          panel.classList.remove('hidden');
          document.getElementById('uas-add-btn').classList.add('hidden');
        };
        document.getElementById('uas-c-cancel').onclick = function () {
          document.getElementById('uas-create-panel').classList.add('hidden');
          document.getElementById('uas-add-btn').classList.remove('hidden');
        };
        document.getElementById('uas-create-form').onsubmit = async function (ev) {
          ev.preventDefault();
          var msg = document.getElementById('uas-list-msg');
          try {
            var created = await api('/api/v1/uas', {
              method: 'POST',
              body: JSON.stringify({
                serialNumber: document.getElementById('uas-c-serial').value.trim(),
                manufacturer: document.getElementById('uas-c-mfr').value.trim(),
                model: document.getElementById('uas-c-model').value.trim(),
                owningOrgUnitId: document.getElementById('uas-c-org').value || null,
                logbookNumber: document.getElementById('uas-c-logbook').value.trim() || null,
                inventoryAssetNumber: document.getElementById('uas-c-im').value.trim() || null
              })
            });
            await api('/api/v1/uas/' + created.id + '/submit', { method: 'POST', body: '{}' });
            await api('/api/v1/uas/' + created.id + '/approve', { method: 'POST', body: '{}' });
            navigate('uas');
          } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
        };
      }
      content.querySelectorAll('.uas-row').forEach(function (row) {
        row.onclick = function (ev) {
          if (ev.target.closest('.uas-type-link') || ev.target.closest('.uas-edit')) return;
          state.uasDetailId = row.dataset.id;
          state.uasEditing = false;
          navigate('uas');
        };
      });
      content.querySelectorAll('.uas-edit').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          state.uasDetailId = btn.dataset.id;
          state.uasEditing = true;
          navigate('uas');
        };
      });
      content.querySelectorAll('.uas-type-link').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          state.techDocModelId = btn.dataset.slug;
          state.techDocTab = 'specs';
          navigate('components');
        };
      });
      return;
}
