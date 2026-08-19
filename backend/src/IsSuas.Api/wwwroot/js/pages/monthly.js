import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, confirmAction, content, makeTable, pageHead } from '../core/ui.js';

export async function renderMonthly() {
      const now = new Date();
      const year = state.monthlyYear || (now.getMonth() === 0 ? now.getFullYear() - 1 : now.getFullYear());
      const month = state.monthlyMonth || (now.getMonth() === 0 ? 12 : now.getMonth());
      state.monthlyYear = year;
      state.monthlyMonth = month;

      const orgs = await api('/api/v1/org-units');
      const unitOrgs = orgs.filter(function (o) {
        return o.isActive !== false && (o.kind === 'Unit' || o.kind === 4 || o.level >= 2);
      });
      if (!state.monthlyOrgUnitId) {
        var prefer = unitOrgs.find(function (o) { return String(o.code || '').indexOf('KR-') === 0; })
          || unitOrgs.find(function (o) { return o.code === 'UTVAR-A'; })
          || unitOrgs[0];
        state.monthlyOrgUnitId = prefer ? prefer.id : null;
      }
      if (!state.monthlyOrgUnitId) {
        content.innerHTML = '<div class="error">Žiadny útvar na výkaz. Najprv vytvorte útvar v organizačnej štruktúre.</div>';
        return;
      }

      const draftUrl = '/api/v1/monthly-flight-reports/draft?year=' + year + '&month=' + month +
        '&orgUnitId=' + encodeURIComponent(state.monthlyOrgUnitId);
      const report = await api(draftUrl, { method: 'POST', body: '{}' });
      const auths = await api('/api/v1/pilot-uas-authorizations?orgUnitId=' + encodeURIComponent(report.orgUnitId) + '&activeOnly=true');
      const statusText = report.status === 'Confirmed' || report.status === 1 ? 'Potvrdeny'
        : (report.status === 'NoActivity' || report.status === 2 ? 'Bez letovej cinnosti' : 'Koncept');
      const lineRows = (report.lines || []).map(function (l, idx) {
        return '<tr data-idx="' + idx + '">' +
          '<td>' + l.pilotName + '</td><td>' + l.uasLabel + '</td>' +
          '<td>' + l.hours + ':' + String(l.minutes).padStart(2, '0') + '</td>' +
          '<td>' + l.takeoffs + '/' + l.landings + '</td>' +
          '<td>' + l.operationalFlights + '/' + l.trainingFlights + '/' + l.otherFlights + '</td></tr>';
      }).join('') || '<tr><td colspan="5">Ziadne riadky — pridajte pilot×UAS</td></tr>';

      const authOpts = (auths || []).map(function (a) {
        return '<option value="' + a.pilotId + '|' + a.uasId + '">' + a.pilotName + ' × ' + a.uasLabel + '</option>';
      }).join('') || '<option value="">— žiadne oprávnenia pre tento útvar —</option>';

      const orgOpts = unitOrgs.map(function (o) {
        return '<option value="' + o.id + '"' + (o.id === report.orgUnitId ? ' selected' : '') + '>' +
          o.code + ' — ' + o.name + '</option>';
      }).join('');

      content.innerHTML = '<div class="stack">' +
        pageHead('Mesacny vykaz', 'Súhrn letovej činnosti útvaru za mesiac. Štatistiky na Prehľade čítajú len potvrdené výkazy.',
          chipHtml('Utvar', report.orgUnitName || '—', 'chip-soft') +
          chipHtml('Obdobie', month + '/' + year) +
          chipHtml('Stav', statusText, 'chip-ok') +
          chipHtml('Oprávnenia', (auths || []).length)) +
        '<div class="toolbar">' +
        '<label>Utvar <select id="m-org" style="width:auto;min-width:220px">' + orgOpts + '</select></label>' +
        '<label>Rok <input id="m-year" type="number" value="' + year + '" style="width:90px" /></label>' +
        '<label>Mesiac <input id="m-month" type="number" min="1" max="12" value="' + month + '" style="width:70px" /></label>' +
        '<button type="button" class="secondary" id="m-reload">Nacitat</button>' +
        '</div><div id="m-msg"></div>' +
        ((auths || []).length ? '' : '<div class="error">Pre vybraný útvar nie sú žiadne oprávnenia pilot×UAS. Vyberte krajský útvar (KR-…) alebo reštartujte API kvôli seedu.</div>') +
        makeTable(['Pilot', 'UAS', 'Cas', 'Vzlet/prist.', 'Ucely O/V/I'], lineRows) +
        '<div class="toolbar">' +
        '<button type="button" id="m-reveal-add">Pridat riadok</button> ' +
        '<button type="button" id="m-confirm">Potvrdit vykaz</button>' +
        '<button type="button" class="secondary" id="m-none">Bez letovej cinnosti</button>' +
        '</div>' +
        '<div id="m-add-panel" class="form-panel hidden">' +
        '<div class="toolbar" style="justify-content:space-between;margin:0">' +
        '<h3 style="margin:0">Novy riadok vykazu</h3>' +
        '<button type="button" class="secondary" id="m-hide-add">Skryt</button></div>' +
        '<form id="m-add" class="form-grid">' +
        '<label class="full">Hladat pilota / UAS' +
        '<input id="m-auth-q" type="search" placeholder="Zadajte meno pilota alebo oznacenie UAS…" autocomplete="off" />' +
        '</label>' +
        '<label class="full">Opravnenie (pilot × UAS)' +
        '<select id="m-auth" size="10" class="m-auth-select">' + authOpts + '</select>' +
        '<span class="muted" id="m-auth-count" style="font-size:0.8rem">Zobrazenych ' + (auths || []).length + ' z ' + (auths || []).length + '</span>' +
        '</label>' +
        '<label>Hodiny<input id="m-h" type="number" min="0" value="1" /></label>' +
        '<label>Minuty<input id="m-min" type="number" min="0" max="59" value="0" /></label>' +
        '<label>Vzlety<input id="m-to" type="number" min="0" value="1" /></label>' +
        '<label>Pristatia<input id="m-la" type="number" min="0" value="1" /></label>' +
        '<label>Operacne<input id="m-op" type="number" min="0" value="1" /></label>' +
        '<label>Vycvik<input id="m-tr" type="number" min="0" value="0" /></label>' +
        '<label>Ine<input id="m-ot" type="number" min="0" value="0" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Ulozit riadok</button>' +
        '<button type="button" class="secondary" id="m-cancel-add">Zrusit</button>' +
        '</div></form></div>' +
        '</div>';

      var authSelect = document.getElementById('m-auth');
      var authCountEl = document.getElementById('m-auth-count');
      var allAuthOptions = Array.prototype.slice.call(authSelect.options).map(function (o) {
        return { value: o.value, text: (o.textContent || '').trim() };
      });

      function filterAuthOptions() {
        var q = (document.getElementById('m-auth-q').value || '').trim().toLowerCase();
        var prev = authSelect.value;
        authSelect.innerHTML = '';
        var visible = 0;
        var keep = null;
        allAuthOptions.forEach(function (opt) {
          if (q && opt.text.toLowerCase().indexOf(q) < 0) return;
          var o = document.createElement('option');
          o.value = opt.value;
          o.textContent = opt.text || '—';
          authSelect.appendChild(o);
          visible++;
          if (opt.value === prev) keep = o;
        });
        if (keep) keep.selected = true;
        else if (authSelect.options.length) authSelect.selectedIndex = 0;
        authCountEl.textContent = 'Zobrazenych ' + visible + ' z ' + allAuthOptions.length +
          (q ? ' (filter: „' + q.replace(/[<>]/g, '') + '“)' : '') +
          ' — zoznam sa da skrolovat';
      }

      document.getElementById('m-auth-q').oninput = filterAuthOptions;

      function showAddPanel() {
        document.getElementById('m-add-panel').classList.remove('hidden');
        document.getElementById('m-reveal-add').classList.add('hidden');
        document.getElementById('m-auth-q').focus();
      }
      function hideAddPanel() {
        document.getElementById('m-add-panel').classList.add('hidden');
        document.getElementById('m-reveal-add').classList.remove('hidden');
      }
      document.getElementById('m-reveal-add').onclick = showAddPanel;
      document.getElementById('m-hide-add').onclick = hideAddPanel;
      document.getElementById('m-cancel-add').onclick = hideAddPanel;

      document.getElementById('m-reload').onclick = function () {
        state.monthlyYear = Number(document.getElementById('m-year').value);
        state.monthlyMonth = Number(document.getElementById('m-month').value);
        state.monthlyOrgUnitId = document.getElementById('m-org').value;
        navigate('monthly');
      };
      document.getElementById('m-org').onchange = function () {
        state.monthlyOrgUnitId = document.getElementById('m-org').value;
        navigate('monthly');
      };
      document.getElementById('m-add').onsubmit = async function (ev) {
        ev.preventDefault();
        var msg = document.getElementById('m-msg');
        try {
          var pair = (document.getElementById('m-auth').value || '').split('|');
          if (pair.length !== 2 || !pair[0] || !pair[1]) throw new Error('Vyberte opravnenie pilot×UAS');
          var lines = (report.lines || []).map(function (l) {
            return {
              lineId: l.id, pilotId: l.pilotId, uasId: l.uasId, hours: l.hours, minutes: l.minutes,
              takeoffs: l.takeoffs, landings: l.landings, operationalFlights: l.operationalFlights,
              trainingFlights: l.trainingFlights, otherFlights: l.otherFlights, note: l.note
            };
          });
          lines.push({
            pilotId: pair[0], uasId: pair[1],
            hours: Number(document.getElementById('m-h').value) || 0,
            minutes: Number(document.getElementById('m-min').value) || 0,
            takeoffs: Number(document.getElementById('m-to').value) || 0,
            landings: Number(document.getElementById('m-la').value) || 0,
            operationalFlights: Number(document.getElementById('m-op').value) || 0,
            trainingFlights: Number(document.getElementById('m-tr').value) || 0,
            otherFlights: Number(document.getElementById('m-ot').value) || 0,
            note: null
          });
          await api('/api/v1/monthly-flight-reports/' + report.id, {
            method: 'PUT',
            body: JSON.stringify({ note: report.note, lines: lines })
          });
          navigate('monthly');
        } catch (err) { msg.innerHTML = '<div class="error">' + err.message + '</div>'; }
      };
      document.getElementById('m-confirm').onclick = async function () {
        try {
          await api('/api/v1/monthly-flight-reports/' + report.id + '/confirm', {
            method: 'POST', body: JSON.stringify({ rowVersion: report.rowVersion })
          });
          navigate('monthly');
        } catch (err) {
          document.getElementById('m-msg').innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };
      document.getElementById('m-none').onclick = async function () {
        if (!confirmAction('Označit mesiac ako bez letovej činnosti?')) return;
        try {
          await api('/api/v1/monthly-flight-reports/' + report.id + '/no-activity', {
            method: 'POST', body: JSON.stringify({ rowVersion: report.rowVersion, note: 'Bez letovej činnosti' })
          });
          navigate('monthly');
        } catch (err) {
          document.getElementById('m-msg').innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };
      return;
}
