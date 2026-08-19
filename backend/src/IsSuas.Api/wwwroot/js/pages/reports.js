import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { bindFormReveals, chipHtml, confirmAction, content, makeTable, pageHead, statusBadge, typeLabelSk } from '../core/ui.js';

export async function renderReports() {
      const official = await api('/api/v1/official-reports');
      content.innerHTML =
        '<div class="stack">' +
        pageHead('Reporty', 'Preddefinované výkazy a oficiálne zamrznuté snímky.',
          chipHtml('Oficiálne', official.length, 'chip-soft')) +
        '<div class="toolbar">' +
        '<button type="button" data-reveal="rep-panel">Generovat report</button>' +
        '<button type="button" data-reveal="off-panel">Oficialny vykaz</button>' +
        '</div>' +
        '<div id="rep-msg"></div>' +
        '<div id="rep-panel" class="form-panel hidden">' +
        '<h3>Preddefinovany report</h3>' +
        '<form id="rep-form" class="form-grid">' +
        '<label>Typ<select id="rep-type">' +
        '<option value="MONTHLY">Mesacne vykazy</option>' +
        '<option value="UAS">UAS</option><option value="PILOTS">Piloti</option>' +
        '<option value="FLIGHTS">Lety</option><option value="OPERATIONS">Operacie</option>' +
        '<option value="BATTERIES">Baterie</option>' +
        '<option value="DEFECTS">Poruchy</option><option value="INCIDENTS">Incidenty</option>' +
        '</select></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Zobrazit</button>' +
        '<button type="button" id="rep-csv">Export CSV</button>' +
        '<button type="button" class="secondary" data-hide="rep-panel">Zrusit</button>' +
        '</div></form>' +
        '<div id="rep-result"></div></div>' +
        '<div id="off-panel" class="form-panel hidden">' +
        '<h3>Pripravit oficialny vykaz (zamrznuty snimok)</h3>' +
        '<form id="off-form" class="form-grid">' +
        '<label>Typ<select id="off-type">' +
        '<option value="UAS">UAS</option><option value="FLIGHTS">Lety</option>' +
        '<option value="OPERATIONS">Operacie</option><option value="INCIDENTS">Incidenty</option>' +
        '</select></label>' +
        '<label class="full">Nazov<input id="off-title" placeholder="napr. Mesacny vykaz flotily" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Vytvorit koncept</button>' +
        '<button type="button" class="secondary" data-hide="off-panel">Zrusit</button>' +
        '</div></form></div>' +
        '<h2>Oficialne vykazy</h2>' +
        makeTable(['Nazov', 'Typ', 'Verzia', 'Stav', 'Udaje k', 'Akcia'],
          official.map(function (r) {
            var approve = (r.status === 0 || r.status === 'Draft')
              ? '<button type="button" class="secondary approve-off" data-id="' + r.id +
                '" data-rv="' + r.rowVersion + '">Schvalit (iny ucet)</button>'
              : '—';
            return '<tr><td>' + r.titleSk + '</td><td>' + typeLabelSk(r.reportTypeCode) + '</td><td>' + r.version +
              '</td><td>' + statusBadge(r.status) + '</td><td>' + new Date(r.dataAsOf).toLocaleString('sk-SK') +
              '</td><td>' + approve + '</td></tr>';
          }).join('') || '<tr><td colspan="6">Ziadne vykazy</td></tr>') +
        '</div>';

      bindFormReveals();

      document.getElementById('rep-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('rep-msg');
        try {
          const type = document.getElementById('rep-type').value;
          const res = await api('/api/v1/reports/generate', {
            method: 'POST',
            body: JSON.stringify({ reportTypeCode: type, from: null, to: null, orgUnitId: null })
          });
          const rows = (res.rows || []).map(function (r) {
            return '<tr><td>' + r.label + '</td><td>' + r.value + '</td><td>' + (r.unit || '') + '</td></tr>';
          }).join('');
          document.getElementById('rep-result').innerHTML =
            '<div class="msg">' + res.titleSk + ' · údaje k ' + new Date(res.dataAsOf).toLocaleString('sk-SK') +
            ' · pravidlá ' + res.rulesVersion + '</div>' +
            makeTable(['Ukazovateľ', 'Hodnota', 'Jednotka'], rows);
          msg.innerHTML = '';
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.getElementById('rep-csv').onclick = async function () {
        const msg = document.getElementById('rep-msg');
        try {
          const type = document.getElementById('rep-type').value;
          const res = await fetch('/api/v1/reports/export.csv', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: 'Bearer ' + state.token
            },
            body: JSON.stringify({ reportTypeCode: type, from: null, to: null, orgUnitId: null })
          });
          if (!res.ok) throw new Error('Export zlyhal ' + res.status);
          const blob = await res.blob();
          const url = URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = type + '.csv';
          a.click();
          URL.revokeObjectURL(url);
          msg.innerHTML = '<div class="msg">CSV stiahnute.</div>';
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.getElementById('off-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('rep-msg');
        try {
          await api('/api/v1/official-reports', {
            method: 'POST',
            body: JSON.stringify({
              reportTypeCode: document.getElementById('off-type').value,
              titleSk: document.getElementById('off-title').value.trim() || 'Oficialny vykaz',
              from: null,
              to: null,
              orgUnitId: null,
              supersedesReportId: null
            })
          });
          msg.innerHTML = '<div class="msg">Koncept oficialneho vykazu vytvoreny. Schvalenie: ucet schvalovatel.</div>';
          navigate('reports');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.querySelectorAll('.approve-off').forEach(function (btn) {
        btn.onclick = async function () {
          if (!confirmAction('Schvalit oficialny vykaz?')) return;
          const msg = document.getElementById('rep-msg');
          msg.innerHTML = '<div class="msg">Prihlaste sa ako <b>schvalovatel</b> a skuste znova, ak ste gestor (SoD).</div>';
          try {
            await api('/api/v1/official-reports/' + btn.dataset.id + '/approve', {
              method: 'POST',
              body: JSON.stringify({
                approvalReason: 'Schvalenie z UI',
                rowVersion: Number(btn.dataset.rv)
              })
            });
            msg.innerHTML = '<div class="msg">Vykaz schvaleny (snimok zostava zamrznuty).</div>';
            navigate('reports');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
