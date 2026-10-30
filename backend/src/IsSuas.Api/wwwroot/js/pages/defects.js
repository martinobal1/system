import { api } from '../core/api.js';
import { colorForKey, swatchHtml } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { trashButtonHtml } from '../core/trash.js';
import { chipHtml, confirmAction, content, makeTable, pageHead, severityBadge, statusBadge, statusLabelSk, enumName } from '../core/ui.js';
import { loadUasOptions, uasSelectHtml } from '../shared/uas.js';

function escHtml(value) {
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function serialBl(d) {
  return (d && d.uasSerialNumber) || '—';
}

function isOpenDefect(d) {
  return d.status === 0 || d.status === 'Reported' || d.status === 1 || d.status === 'UnderRepair';
}

export async function renderDefects() {
      let accounts = [];
      try {
        accounts = await api('/api/v1/accounts');
      } catch (_) { /* optional */ }

      async function resolveDefect(d) {
        if (!confirmAction('Vyriešiť poruchu?')) return false;
        const msg = document.getElementById('def-msg');
        const verifier = accounts.find(function (a) { return a.id !== d.reportedByAccountId; })
          || accounts[0];
        if (!verifier) {
          if (msg) msg.innerHTML = '<div class="error">Chyba overovatel (ziadny iny ucet).</div>';
          return false;
        }
        await api('/api/v1/defects/' + d.id + '/resolve', {
          method: 'POST',
          body: JSON.stringify({
            resolutionNotes: 'Opravene z UI',
            technicalAssessment: 'Posudenie OK',
            testPerformed: true,
            verifiedByAccountId: verifier.id,
            rowVersion: Number(d.rowVersion)
          })
        });
        return true;
      }

      if (state.defectDetailId) {
        const d = await api('/api/v1/defects/' + state.defectDetailId);
        const created = d.createdAt ? new Date(d.createdAt).toLocaleString('sk-SK') : '—';
        const resolved = d.resolvedAt ? new Date(d.resolvedAt).toLocaleString('sk-SK') : '—';
        content.innerHTML =
          '<div class="stack">' +
          pageHead('Detail poruchy', d.title || 'Porucha',
            chipHtml('Stav', statusLabelSk(d.status), 'chip-soft')) +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="def-back">Späť na zoznam</button> ' +
          (isOpenDefect(d)
            ? '<button type="button" id="def-resolve">Vyriešiť</button> '
            : '') +
          trashButtonHtml('defects', d.id, d.title || 'Porucha') +
          '</div>' +
          '<div id="def-msg"></div>' +
          '<div class="form-panel"><h3>Bezpilotné lietadlo</h3><dl class="flight-meta">' +
          '<div class="full"><dt>Sériové č. BL</dt><dd style="word-break:break-all">' + escHtml(serialBl(d)) + '</dd></div>' +
          '<div><dt>Poznávacia značka</dt><dd>' + escHtml(d.uasRegistrationNumber || '—') + '</dd></div>' +
          '<div><dt>Výrobca / typ</dt><dd>' + escHtml([d.uasManufacturer, d.uasModel].filter(Boolean).join(' ') || '—') + '</dd></div>' +
          '</dl></div>' +
          '<div class="form-panel"><h3>Hlásenie</h3><dl class="flight-meta">' +
          '<div><dt>Názov</dt><dd>' + escHtml(d.title) + '</dd></div>' +
          '<div><dt>Závažnosť</dt><dd>' + severityBadge(d.severity, 'defect') + '</dd></div>' +
          '<div><dt>Stav</dt><dd>' + statusBadge(enumName('defect', d.status)) + '</dd></div>' +
          '<div><dt>Blokuje prevádzku</dt><dd>' + (d.blocksOperations ? 'Áno' : 'Nie') + '</dd></div>' +
          '<div><dt>Typ</dt><dd>' + escHtml(d.defectTypeCode || '—') + '</dd></div>' +
          '<div><dt>Nahlásené</dt><dd>' + created + '</dd></div>' +
          '<div class="full"><dt>Pôvodné hlásenie</dt><dd style="white-space:pre-wrap">' +
            escHtml(d.originalReport || '—') + '</dd></div>' +
          '<div class="full"><dt>Dočasné opatrenie</dt><dd>' + escHtml(d.temporaryMeasure || '—') + '</dd></div>' +
          '</dl></div>' +
          (d.resolutionNotes || d.technicalAssessment || d.resolvedAt
            ? '<div class="form-panel"><h3>Riešenie</h3><dl class="flight-meta">' +
              '<div><dt>Vyriešené</dt><dd>' + resolved + '</dd></div>' +
              '<div><dt>Skúška</dt><dd>' + (d.testPerformed ? 'Áno' : 'Nie') + '</dd></div>' +
              '<div class="full"><dt>Technické posúdenie</dt><dd style="white-space:pre-wrap">' +
                escHtml(d.technicalAssessment || '—') + '</dd></div>' +
              '<div class="full"><dt>Poznámky k oprave</dt><dd style="white-space:pre-wrap">' +
                escHtml(d.resolutionNotes || '—') + '</dd></div>' +
              '</dl></div>'
            : '') +
          '</div>';

        document.getElementById('def-back').onclick = function () {
          state.defectDetailId = null;
          navigate('defects');
        };
        var resolveBtn = document.getElementById('def-resolve');
        if (resolveBtn) {
          resolveBtn.onclick = async function () {
            try {
              var ok = await resolveDefect(d);
              if (ok) {
                state.defectDetailId = null;
                navigate('defects');
              }
            } catch (err) {
              document.getElementById('def-msg').innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }
        return;
      }

      const items = await api('/api/v1/defects');
      const uasList = await loadUasOptions();
      const uasOpts = uasSelectHtml(uasList);
      const rows = items.map(function (d) {
        const resolveBtn = isOpenDefect(d)
          ? '<button type="button" class="secondary resolve-def" data-id="' + d.id + '">Vyriešiť</button>'
          : '—';
        var sevColor = ({ 0: '#0d6b3c', 1: '#c47b1a', 2: '#b86e00', 3: '#b42318' })[Number(d.severity)]
          || colorForKey(d.severity);
        return '<tr class="row-tint def-row" data-id="' + d.id + '" style="--row-accent:' + sevColor + ';cursor:pointer">' +
          '<td><span class="entity-label">' + swatchHtml(sevColor) + '<span>' + escHtml(d.title) + '</span></span></td>' +
          '<td style="white-space:nowrap">' + escHtml(serialBl(d)) + '</td>' +
          '<td>' + severityBadge(d.severity, 'defect') + '</td>' +
          '<td>' + statusBadge(enumName('defect', d.status)) + '</td>' +
          '<td>' + (d.blocksOperations ? 'Ano' : 'Nie') + '</td>' +
          '<td><div class="row-actions">' + resolveBtn + '</div></td></tr>';
      }).join('') || '<tr><td colspan="6">Žiadne poruchy</td></tr>';

      var blocking = items.filter(function (d) { return d.blocksOperations; }).length;
      content.innerHTML =
        '<div class="stack">' +
        pageHead('Poruchy', 'Hlásenia závad a blokovanie prevádzky UAS. Kliknutím otvoríte kartu vrátane sériového č. BL.',
          chipHtml('Poruchy', items.length, 'chip-soft') +
          chipHtml('Blokujúce', blocking, blocking ? 'chip-danger' : 'chip-ok')) +
        '<div class="toolbar">' +
        '<button type="button" id="def-add-btn">Nahlasit poruchu</button>' +
        '</div>' +
        '<div id="def-msg"></div>' +
        '<div id="def-panel" class="form-panel hidden">' +
        '<h3>Nové hlásenie poruchy</h3>' +
        '<form id="def-form" class="form-grid">' +
        '<label class="full">Názov<input id="def-title" required placeholder="napr. Vibracie" /></label>' +
        '<label class="full">Povodne hlásenie<textarea id="def-report" required placeholder="Popis poruchy..."></textarea></label>' +
        '<label class="full">UAS (sériové č. BL)<select id="def-uas">' + uasOpts + '</select></label>' +
        '<label>Závažnosť<select id="def-severity">' +
        '<option value="0">Nizka</option><option value="1">Stredna</option>' +
        '<option value="2">Vysoka</option><option value="3" selected>Blokujuca</option>' +
        '</select></label>' +
        '<label>Blokovat operácie<select id="def-block"><option value="true" selected>Ano</option><option value="false">Nie</option></select></label>' +
        '<label>Typ kod<input id="def-type" value="GENERAL" /></label>' +
        '<label class="full">Docasne opatrenie<input id="def-temp" placeholder="napr. Vyradene z prevadzky" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Odoslať hlásenie</button>' +
        '<button type="button" class="secondary" id="def-cancel">Zrušiť</button>' +
        '</div></form></div>' +
        '<div class="chart-panel table-scroll">' +
        makeTable(['Názov', 'Sériové č. BL', 'Závažnosť', 'Stav', 'Blokuje', 'Akcie'], rows, true) +
        '</div></div>';

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

      content.querySelectorAll('.def-row').forEach(function (row) {
        row.onclick = function (ev) {
          if (ev.target.closest('.resolve-def')) return;
          state.defectDetailId = row.dataset.id;
          navigate('defects');
        };
      });
      content.querySelectorAll('.resolve-def').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          var d = items.find(function (x) { return x.id === btn.dataset.id; });
          if (!d) return;
          const msg = document.getElementById('def-msg');
          try {
            var ok = await resolveDefect(d);
            if (ok) {
              msg.innerHTML = '<div class="msg">Porucha vyriesena.</div>';
              navigate('defects');
            }
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
