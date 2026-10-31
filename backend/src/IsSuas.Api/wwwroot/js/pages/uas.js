import { api } from '../core/api.js';
import { bindChartTooltips, colorForKey, fleetModelsFromUasList, renderFleetModelsPanel, swatchHtml } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { trashButtonHtml } from '../core/trash.js';
import { chipHtml, confirmAction, confirmDialog, content, makeTable, pageHead, searchHighlightBanner, sectionCard, statusLabelSk } from '../core/ui.js';
import { ensureTechDocCatalog, getTechDocCatalog, techDocSlug } from '../shared/techDocs.js';

function escAttr(value) {
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;');
}

export async function renderUas() {
      await ensureTechDocCatalog();
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
        // Prideliť sa dá len útvaru (nie zložke ani ministerstvu) a nie tomu, ktorý dron už má.
        const unitOpts = orgs.filter(function (o) {
          return (o.kind === 'Unit' || o.kind === 4) && o.isActive !== false && o.id !== u.owningOrgUnitId;
        }).sort(function (a, b) { return String(a.path || '').localeCompare(String(b.path || '')); })
          .map(function (o) { return '<option value="' + o.id + '">' + escAttr(o.name) + ' (' + escAttr(o.code) + ')</option>'; }).join('');
        const canEditUas = state.accessLevel === 1
          && u.status !== 'Decommissioned' && u.status !== 8;
        const editOpen = canEditUas && !!state.uasEditing;
        const isDecomUas = u.status === 'Decommissioned' || u.status === 8;
        // Gestor (L1) ponúkne UAS útvaru → správca útvaru (L2) ho prijme alebo odmietne.
        // Správca môže svoj UAS vrátiť na MV SR alebo vyradiť; gestor ho môže odobrať (späť na MV SR).
        const mvRoot = orgs.find(function (o) { return o.kind === 'Root' || o.kind === 0; }) || null;
        const isAtMv = !!mvRoot && u.owningOrgUnitId === mvRoot.id;
        const hasOffer = !!u.pendingOrgUnitId;
        const canOfferUas = state.accessLevel === 1 && !isDecomUas && !hasOffer;
        const canCancelOffer = state.accessLevel === 1 && hasOffer;
        const canAnswerOffer = state.accessLevel === 2 && hasOffer && !!u.pendingForCaller && !isDecomUas;
        const canTakeBackUas = state.accessLevel === 1 && !isDecomUas && !!mvRoot && !isAtMv && !!u.owningOrgUnitId;
        const canReturnUas = state.accessLevel === 2 && !isDecomUas && !!mvRoot && !isAtMv && !!u.owningOrgUnitId;
        const canRetireUas = !isDecomUas && (state.accessLevel === 1 || (state.accessLevel === 2 && !isAtMv && !!u.owningOrgUnitId));
        const offerDate = u.pendingSince ? new Date(u.pendingSince).toLocaleDateString('sk-SK') : '';
        const offerBanner = hasOffer
          ? '<div class="uas-offer-banner"><div><b>' + (canAnswerOffer ? 'Gestor vám ponúka tento dron.' : 'Čaká na prijatie útvarom ' + escAttr(u.pendingOrgUnitName || '') + '.') + '</b>' +
            '<span>' + (canAnswerOffer ? 'Útvar: ' + escAttr(u.pendingOrgUnitName || '') + ' · ' : '') + 'Ponuka od ' + offerDate +
            (u.pendingReason ? ' · ' + escAttr(u.pendingReason) : '') + '</span></div>' +
            '<div class="toolbar" style="margin:0">' +
            (canAnswerOffer
              ? '<button type="button" id="uas-offer-accept">Prijať dron</button>' +
                '<button type="button" class="secondary danger-ghost" id="uas-offer-reject">Odmietnuť</button>'
              : '') +
            (canCancelOffer ? '<button type="button" class="secondary" id="uas-offer-cancel">Zrušiť ponuku</button>' : '') +
            '</div></div>'
          : '';
        state.uasEditing = false;
        content.innerHTML = '<div class="stack">' +
          pageHead('Detail UAS', (u.registrationNumber || u.serialNumber) + ' · ' + u.manufacturer + ' ' + u.model,
            chipHtml('Stav', statusLabelSk(u.status), 'chip-soft') +
            chipHtml('Útvar', u.owningOrgUnitName || '—')) +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="uas-back">Späť na zoznam</button> ' +
          (u.status === 'Draft' || u.status === 0
            ? '<button type="button" id="uas-register">Zaregistrovať</button> '
            : '') +
          (canEditUas
            ? '<button type="button" class="secondary" id="uas-edit">Upraviť</button> '
            : '') +
          (canReturnUas
            ? '<button type="button" class="secondary" id="uas-return">Vrátiť dron na MV SR</button> ' : '') +
          (canTakeBackUas
            ? '<button type="button" class="secondary" id="uas-takeback">Odobrať dron (späť na MV SR)</button> ' : '') +
          (canRetireUas
            ? '<button type="button" class="secondary" id="uas-retire">Vyradiť</button> ' : '') +
          trashButtonHtml('uas', u.id, (u.registrationNumber || u.serialNumber || 'UAS') + ' (' + (u.manufacturer || '') + ' ' + (u.model || '') + ')') +
          '</div>' +
          '<div id="uas-msg"></div>' + offerBanner +
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
          (canOfferUas
            ? '<div class="form-panel"><h3>Prideliť dron útvaru</h3>' +
              '<p class="muted" style="margin:-0.4rem 0 0">Dron prejde útvaru až potom, keď ho správca útvaru prijme.</p>' +
              '<form id="uas-transfer" class="form-grid">' +
              '<label class="full">Útvar<select id="uas-new-org" required>' + unitOpts + '</select></label>' +
              '<label class="full">Poznámka<input id="uas-reason" maxlength="1000" placeholder="voliteľné" /></label>' +
              '<div class="full toolbar"><button type="submit">Ponúknuť útvaru</button></div></form></div>'
            : '') +
          sectionCard('História pridelení', makeTable(['Útvar', 'Od', 'Do', 'Dovod'], hist, true), '#2b7bb9') +
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
        var returnBtn = document.getElementById('uas-return');
        if (returnBtn) returnBtn.onclick = async function () {
          var ok = await confirmDialog({
            title: 'Vrátiť dron na MV SR?',
            message: '<p>UAS <b>' + escAttr(u.registrationNumber || u.serialNumber || '') + '</b> sa vráti Ministerstvu vnútra SR. ' +
              'Váš útvar ho potom už neuvidí a späť ho môže prideliť iba gestor.</p>',
            confirmLabel: 'Vrátiť dron'
          });
          if (!ok) return;
          try {
            await api('/api/v1/uas/' + u.id + '/transfer', {
              method: 'POST',
              body: JSON.stringify({ newOrgUnitId: mvRoot.id, effectiveAt: new Date().toISOString(), reason: 'Vrátenie na MV SR' })
            });
            state.uasDetailId = null; navigate('uas');
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
        function uasMsgError(err) { document.getElementById('uas-msg').innerHTML = '<div class="error">' + escAttr(err.message || 'Akcia zlyhala') + '</div>'; }
        if (canOfferUas) document.getElementById('uas-transfer').onsubmit = async function (ev) {
          ev.preventDefault();
          var target = document.getElementById('uas-new-org').value;
          if (!target) { uasMsgError(new Error('Vyberte útvar')); return; }
          try {
            await api('/api/v1/uas/' + u.id + '/offer', {
              method: 'POST',
              body: JSON.stringify({ orgUnitId: target, reason: document.getElementById('uas-reason').value.trim() || null })
            });
            navigate('uas');
          } catch (err) { uasMsgError(err); }
        };
        var acceptBtn = document.getElementById('uas-offer-accept');
        if (acceptBtn) acceptBtn.onclick = async function () {
          try {
            await api('/api/v1/uas/' + u.id + '/offer/accept', { method: 'POST', body: '{}' });
            navigate('uas');
          } catch (err) { uasMsgError(err); }
        };
        var rejectBtn = document.getElementById('uas-offer-reject') || document.getElementById('uas-offer-cancel');
        if (rejectBtn) rejectBtn.onclick = async function () {
          var isCancel = rejectBtn.id === 'uas-offer-cancel';
          var ok = await confirmDialog({
            title: isCancel ? 'Zrušiť ponuku?' : 'Odmietnuť dron?',
            message: isCancel
              ? '<p>Ponuka pre útvar <b>' + escAttr(u.pendingOrgUnitName || '') + '</b> sa zruší a dron ostane tam, kde je.</p>'
              : '<p>Dron sa vášmu útvaru nepridelí a ostane na MV SR.</p>',
            confirmLabel: isCancel ? 'Zrušiť ponuku' : 'Odmietnuť',
            danger: !isCancel
          });
          if (!ok) return;
          try {
            await api('/api/v1/uas/' + u.id + '/offer/reject', { method: 'POST', body: '{}' });
            if (!isCancel) state.uasDetailId = null;
            navigate('uas');
          } catch (err) { uasMsgError(err); }
        };
        var takeBackBtn = document.getElementById('uas-takeback');
        if (takeBackBtn) takeBackBtn.onclick = async function () {
          var ok = await confirmDialog({
            title: 'Odobrať dron útvaru?',
            message: '<p>Dron sa útvaru <b>' + escAttr(u.owningOrgUnitName || '') + '</b> odoberie a vráti sa na MV SR.</p>',
            confirmLabel: 'Odobrať dron',
            danger: true
          });
          if (!ok) return;
          try {
            await api('/api/v1/uas/' + u.id + '/transfer', {
              method: 'POST',
              body: JSON.stringify({ newOrgUnitId: mvRoot.id, effectiveAt: new Date().toISOString(), reason: 'Odobratie gestorom' })
            });
            navigate('uas');
          } catch (err) { uasMsgError(err); }
        };
        return;
      }
      const data = await api('/api/v1/uas?page=1&pageSize=500&includeOffers=true');
      const allUas = data.items || [];
      // Záložky podľa výrobcu (ako v Registri PnD) — najpočetnejší výrobca prvý, „Všetky“ na konci.
      function mfrKey(u) { return String(u.manufacturer || '').trim() || 'Neuvedený'; }
      var mfrCounts = {};
      allUas.forEach(function (u) { var k = mfrKey(u); mfrCounts[k] = (mfrCounts[k] || 0) + 1; });
      var mfrNames = Object.keys(mfrCounts).sort(function (a, b) {
        return mfrCounts[b] - mfrCounts[a] || a.localeCompare(b, 'sk');
      });
      var mfrTab = state.uasMfrTab === '__all' || mfrCounts[state.uasMfrTab] ? state.uasMfrTab : (mfrNames[0] || '__all');
      if (mfrNames.length <= 1) mfrTab = '__all';
      const list = mfrTab === '__all' ? allUas : allUas.filter(function (u) { return mfrKey(u) === mfrTab; });
      var mfrTabsHtml = mfrNames.length > 1
        ? '<div class="reg-tabs">' + mfrNames.map(function (n) {
            return '<button type="button" class="reg-tab uas-mfr-tab' + (mfrTab === n ? ' active' : '') + '" data-mfr="' + escAttr(n) + '">' +
              escAttr(n) + ' <span class="tab-count">' + mfrCounts[n] + '</span></button>';
          }).join('') +
          '<button type="button" class="reg-tab uas-mfr-tab' + (mfrTab === '__all' ? ' active' : '') + '" data-mfr="__all">Všetky <span class="tab-count">' + allUas.length + '</span></button>' +
          '</div>'
        : '';
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
        var isDecom = u.status === 'Decommissioned' || u.status === 8;
        var accent = isDecom ? '#d92d20' : colorForKey(key);
        return '<tr class="row-tint uas-row' + (isDecom ? ' is-decommissioned' : '') + '" data-id="' + u.id + '" style="--row-accent:' + accent + ';cursor:pointer"' +
          (isDecom ? ' title="Vyradené z prevádzky"' : '') + '>' +
          '<td>' + (idx + 1) + '</td>' +
          '<td>' + (u.certificateNumber || '—') + '</td>' +
          '<td>' + dereg + '</td>' +
          '<td><span class="entity-label">' + swatchHtml(accent) +
          '<span>' + (u.registrationNumber || '—') + '</span>' +
          (isDecom ? '<span class="uas-decom-badge">Vyradené</span>' : '') +
          (u.pendingOrgUnitId && !isDecom
            ? '<span class="uas-offer-badge" title="Čaká na prijatie útvarom ' + escAttr(u.pendingOrgUnitName || '') + '">' +
              (state.accessLevel === 2 && u.pendingForCaller ? 'Na prijatie' : 'Ponúknuté: ' + escAttr(u.pendingOrgUnitName || '')) + '</span>'
            : '') + '</span></td>' +
          '<td><span class="uas-type-cell">' + typeCell + (mfr ? '<small>' + escAttr(mfr) + '</small>' : '') + '</span></td>' +
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
      }).join('') || '<tr><td colspan="' + (canAddUas ? '10' : '9') + '">Zatiaľ žiadne UAS v registri</td></tr>';
      const uasModelsPanel = renderFleetModelsPanel(fleetModelsFromUasList(list), list.length, {
        subtitle: 'Zloženie registra podľa typu UAS',
        limit: 12
      });
      var offersForMe = state.accessLevel === 2
        ? allUas.filter(function (x) { return x.pendingOrgUnitId && x.pendingForCaller && x.status !== 'Decommissioned' && x.status !== 8; })
        : [];
      var offersNotice = offersForMe.length
        ? '<div class="uas-offer-banner"><div><b>Gestor vám ponúka ' + offersForMe.length +
          (offersForMe.length === 1 ? ' dron' : offersForMe.length < 5 ? ' drony' : ' dronov') + ' na prijatie.</b>' +
          '<span>Otvorte dron označený „Na prijatie“ a potvrďte ho tlačidlom Prijať dron.</span></div></div>'
        : '';
      content.innerHTML = '<div class="stack">' + searchHighlightBanner() + offersNotice +
        pageHead('Register UAS',
          canAddUas
            ? 'Register štátnych bezpilotných lietadiel — pridať / upraviť / vyradiť.'
            : 'Register štátnych bezpilotných lietadiel. Pridať môže len L1.',
          chipHtml('UAS', allUas.length, 'chip-soft') +
          (mfrNames.length > 1 ? chipHtml('Výrobcovia', mfrNames.length, 'chip-soft') : '')) +
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
        mfrTabsHtml +
        '<div class="chart-panel table-scroll uas-register">' +
        makeTable([
          'P.č.',
          'Č. osvedčenia o zápise do registra',
          'Dátum výmazu z registra',
          'Poznávacia značka',
          'Typ / výrobca',
          'Výrobné číslo',
          'Ev.č. denníka',
          'Užívateľ (útvar)',
          'Č. IM'
        ].concat(canAddUas ? ['Akcie'] : []), rows, true) +
        '</div></div>';
      bindChartTooltips(content);
      content.querySelectorAll('.uas-mfr-tab').forEach(function (btn) {
        btn.onclick = function () {
          state.uasMfrTab = btn.dataset.mfr;
          navigate('uas');
        };
      });
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
