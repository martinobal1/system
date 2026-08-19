import { api } from '../core/api.js';
import { colorForKey, swatchHtml } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { chipHtml, confirmAction, content, makeTable, pageHead, severityBadge, statusBadge } from '../core/ui.js';
import { loadUasOptions, uasSelectHtml } from '../shared/uas.js';

export async function renderDefects() {
      const items = await api('/api/v1/defects');
      const uasList = await loadUasOptions();
      let accounts = [];
      try {
        accounts = await api('/api/v1/accounts');
      } catch (_) { /* optional */ }

      const uasOpts = uasSelectHtml(uasList);
      const rows = items.map(function (d) {
        const resolveBtn = (d.status === 0 || d.status === 'Reported' || d.status === 1 || d.status === 'UnderRepair')
          ? '<button type="button" class="secondary resolve-def" data-id="' + d.id +
            '" data-rv="' + d.rowVersion + '" data-reporter="' + d.reportedByAccountId + '">Vyriesit</button>'
          : '—';
        var report = String(d.originalReport || '');
        if (report.length > 80) report = report.slice(0, 77) + '…';
        var sevColor = ({ 0: '#0d6b3c', 1: '#c47b1a', 2: '#b86e00', 3: '#b42318' })[Number(d.severity)]
          || colorForKey(d.severity);
        return '<tr class="row-tint" style="--row-accent:' + sevColor + '">' +
          '<td><span class="entity-label">' + swatchHtml(sevColor) + '<span>' + d.title + '</span></span></td>' +
          '<td>' + severityBadge(d.severity, 'defect') + '</td>' +
          '<td>' + statusBadge(d.status) + '</td>' +
          '<td>' + (d.blocksOperations ? 'Ano' : 'Nie') + '</td>' +
          '<td>' + report + '</td>' +
          '<td><div class="row-actions">' + resolveBtn + '</div></td></tr>';
      }).join('') || '<tr><td colspan="6">Ziadne poruchy</td></tr>';

      var blocking = items.filter(function (d) { return d.blocksOperations; }).length;
      content.innerHTML =
        '<div class="stack">' +
        pageHead('Poruchy', 'Hlásenia závad a blokovanie prevádzky UAS.',
          chipHtml('Poruchy', items.length, 'chip-soft') +
          chipHtml('Blokujúce', blocking, blocking ? 'chip-danger' : 'chip-ok')) +
        '<div class="toolbar">' +
        '<button type="button" id="def-add-btn">Nahlasit poruchu</button>' +
        '</div>' +
        '<div id="def-msg"></div>' +
        '<div id="def-panel" class="form-panel hidden">' +
        '<h3>Nove hlasenie poruchy</h3>' +
        '<form id="def-form" class="form-grid">' +
        '<label class="full">Nazov<input id="def-title" required placeholder="napr. Vibracie" /></label>' +
        '<label class="full">Povodne hlasenie<textarea id="def-report" required placeholder="Popis poruchy..."></textarea></label>' +
        '<label>UAS<select id="def-uas">' + uasOpts + '</select></label>' +
        '<label>Zavaznost<select id="def-severity">' +
        '<option value="0">Nizka</option><option value="1">Stredna</option>' +
        '<option value="2">Vysoka</option><option value="3" selected>Blokujuca</option>' +
        '</select></label>' +
        '<label>Blokovat operacie<select id="def-block"><option value="true" selected>Ano</option><option value="false">Nie</option></select></label>' +
        '<label>Typ kod<input id="def-type" value="GENERAL" /></label>' +
        '<label class="full">Docasne opatrenie<input id="def-temp" placeholder="napr. Vyradene z prevadzky" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Odoslat hlasenie</button>' +
        '<button type="button" class="secondary" id="def-cancel">Zrusit</button>' +
        '</div></form></div>' +
        makeTable(['Nazov', 'Zavaznost', 'Stav', 'Blokuje', 'Povodne hlasenie', 'Akcie'], rows) +
        '</div>';

      document.getElementById('def-add-btn').onclick = function () {
        document.getElementById('def-msg').innerHTML = '';
        document.getElementById('def-panel').classList.remove('hidden');
        document.getElementById('def-title').focus();
      };
      document.getElementById('def-cancel').onclick = function () {
        document.getElementById('def-panel').classList.add('hidden');
      };

      document.getElementById('def-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('def-msg');
        const uasId = document.getElementById('def-uas').value;
        if (!uasId) {
          msg.innerHTML = '<div class="error">Vyberte UAS zo zoznamu.</div>';
          return;
        }
        try {
          const severity = Number(document.getElementById('def-severity').value);
          const blocks = document.getElementById('def-block').value === 'true' || severity === 3;
          await api('/api/v1/defects', {
            method: 'POST',
            body: JSON.stringify({
              targetType: 0,
              targetId: uasId,
              title: document.getElementById('def-title').value.trim(),
              originalReport: document.getElementById('def-report').value.trim(),
              severity: severity,
              blocksOperations: blocks,
              temporaryMeasure: document.getElementById('def-temp').value.trim() || null,
              relatedFlightId: null,
              relatedUasId: uasId,
              defectTypeCode: document.getElementById('def-type').value.trim() || 'GENERAL'
            })
          });
          msg.innerHTML = '<div class="msg">Porucha nahlasena' +
            (blocks ? ' (blokuje UAS).' : '.') + '</div>';
          navigate('defects');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.querySelectorAll('.resolve-def').forEach(function (btn) {
        btn.onclick = async function () {
          if (!confirmAction('Vyriesit poruchu?')) return;
          const msg = document.getElementById('def-msg');
          const verifier = accounts.find(function (a) { return a.id !== btn.dataset.reporter; })
            || accounts[0];
          if (!verifier) {
            msg.innerHTML = '<div class="error">Chyba overovatel (ziadny iny ucet).</div>';
            return;
          }
          try {
            await api('/api/v1/defects/' + btn.dataset.id + '/resolve', {
              method: 'POST',
              body: JSON.stringify({
                resolutionNotes: 'Opravene z UI',
                technicalAssessment: 'Posudenie OK',
                testPerformed: true,
                verifiedByAccountId: verifier.id,
                rowVersion: Number(btn.dataset.rv)
              })
            });
            msg.innerHTML = '<div class="msg">Porucha vyriesena (overil: ' + verifier.userName + ').</div>';
            navigate('defects');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
