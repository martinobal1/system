import { api } from '../core/api.js';
import { colorForKey, swatchHtml } from '../core/charts.js';
import { navigate } from '../core/nav.js';
import { trashButtonHtml } from '../core/trash.js';
import { bindFormReveals, chipHtml, content, makeTable, pageHead, searchHighlightBanner, sectionCard, severityBadge, statusBadge, typeLabelSk, enumName } from '../core/ui.js';
import { loadUasOptions, uasSelectHtml } from '../shared/uas.js';

export async function renderIncidents() {
      const items = await api('/api/v1/safety-events');
      const notes = await api('/api/v1/notifications');
      const orgs = await api('/api/v1/org-units');
      const uasList = await loadUasOptions();

      const orgOpts = orgs.map(function (o) {
        return '<option value="' + o.id + '">' + o.code + ' — ' + o.name + '</option>';
      }).join('');
      const uasOpts = '<option value="">— bez UAS —</option>' + uasSelectHtml(uasList).replace(
        '<option value="">(ziadne UAS v databaze)</option>', '');

      const rows = items.map(function (e) {
        const submitBtn = (e.status === 0 || e.status === 'Draft')
          ? '<button type="button" class="secondary submit-ev" data-id="' + e.id +
            '" data-rv="' + e.rowVersion + '">Odoslať</button>'
          : '—';
        return '<tr class="row-tint" style="--row-accent:' +
          ({ 0: '#0d6b3c', 1: '#c47b1a', 2: '#b86e00', 3: '#b42318' }[Number(e.severity)] || colorForKey(e.severity)) +
          '"><td><span class="entity-label">' + swatchHtml(colorForKey(e.id || e.title)) +
          '<span>' + e.title + '</span></span></td><td>' + severityBadge(e.severity, 'safety') + '</td><td>' + statusBadge(enumName('safety', e.status)) +
          '</td><td>' + (e.initialReportLocked ? 'Ano' : 'Nie') + '</td><td>' +
          (e.autoSuspendApplied ? 'Ano' : 'Nie') + '</td><td><div class="row-actions-end">' +
          (submitBtn === '—' ? '' : submitBtn + ' ') + trashButtonHtml('incidents', e.id, e.title, { compact: true }) + '</div></td></tr>';
      }).join('') || '<tr><td colspan="6">Žiadne udalosti — vytvorte prvu nizsie</td></tr>';

      content.innerHTML =
        '<div class="stack">' +
        searchHighlightBanner() +
        pageHead('Incidenty', 'Bezpečnostné udalosti a systémové upozornenia.',
          chipHtml('Udalosti', items.length, 'chip-soft') +
          chipHtml('Upozornenia', notes.length, 'chip-warn')) +
        '<div class="toolbar">' +
        '<button type="button" data-reveal="inc-panel">Nové hlásenie incidentu</button>' +
        '</div>' +
        '<div id="inc-msg"></div>' +
        '<div id="inc-panel" class="form-panel hidden">' +
        '<h3>Nové hlásenie incidentu</h3>' +
        '<form id="incident-form" class="form-grid">' +
        '<label class="full">Názov udalosti<input id="inc-title" required placeholder="napr. Strata spojenia" /></label>' +
        '<label class="full">Prvotne hlásenie (minimalne udaje)<textarea id="inc-report" required placeholder="Co sa stalo, kde, kedy..."></textarea></label>' +
        '<label>Závažnosť<select id="inc-severity">' +
        '<option value="0">Nizka</option><option value="1">Stredna</option>' +
        '<option value="2">Vysoka</option><option value="3" selected>Kriticka</option>' +
        '</select></label>' +
        '<label>Organizacny útvar<select id="inc-org">' + orgOpts + '</select></label>' +
        '<label class="full">Suvisiace UAS (volitelne)<select id="inc-uas">' + uasOpts + '</select></label>' +
        '<label class="full">Miesto<input id="inc-loc" placeholder="napr. Bratislava" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit" id="inc-create">Uložiť koncept</button>' +
        '<button type="button" class="secondary" id="inc-create-submit">Uložiť a hned odoslat</button>' +
        '<button type="button" class="secondary" data-hide="inc-panel">Zrušiť</button>' +
        '</div></form></div>' +
        '<h2>Bezpečnostné udalosti</h2>' +
        makeTable(['Názov', 'Závažnosť', 'Stav', 'Zamknuté', 'Autom. pozastavenie', 'Akcia'], rows) +
        sectionCard('Upozornenia', makeTable(['Priorita', 'Typ', 'Názov', 'Stav'],
          notes.map(function (n) {
            return '<tr class="row-tint" style="--row-accent:' + colorForKey(n.priority || n.typeCode) + '">' +
              '<td>' + statusBadge(n.priority) + '</td><td>' + typeLabelSk(n.typeCode) +
              '</td><td>' + n.title +
              '</td><td>' + statusBadge(enumName('notification', n.status)) + '</td></tr>';
          }).join('') || '<tr><td colspan="4">Žiadne upozornenia</td></tr>', true), '#b42318') +
        '</div>';

      bindFormReveals();

      async function createIncident(alsoSubmit) {
        const msg = document.getElementById('inc-msg');
        const title = document.getElementById('inc-title').value.trim();
        const report = document.getElementById('inc-report').value.trim();
        if (!title || !report) {
          msg.innerHTML = '<div class="error">Vyplnte názov a prvotne hlásenie.</div>';
          return;
        }
        try {
          const uasVal = document.getElementById('inc-uas').value;
          const created = await api('/api/v1/safety-events', {
            method: 'POST',
            body: JSON.stringify({
              title: title,
              initialReport: report,
              severity: Number(document.getElementById('inc-severity').value),
              occurredAt: new Date().toISOString(),
              orgUnitId: document.getElementById('inc-org').value || null,
              relatedFlightId: null,
              relatedOperationId: null,
              relatedUasId: uasVal || null,
              locationDescription: document.getElementById('inc-loc').value.trim() || null
            })
          });
          if (alsoSubmit) {
            await api('/api/v1/safety-events/' + created.id + '/submit', {
              method: 'POST',
              body: JSON.stringify({ rowVersion: created.rowVersion })
            });
            msg.innerHTML = '<div class="msg">Udalost ulozena a odoslana' +
              (Number(document.getElementById('inc-severity').value) === 3
                ? ' (kriticka — upozornenia + pripadne pozastavenie UAS).'
                : '.') + '</div>';
          } else {
            msg.innerHTML = '<div class="msg">Koncept ulozeny. Mozete ho odoslat tlacidlom v tabulke.</div>';
          }
          navigate('incidents');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      }

      document.getElementById('incident-form').onsubmit = function (ev) {
        ev.preventDefault();
        createIncident(false);
      };
      document.getElementById('inc-create-submit').onclick = function () {
        createIncident(true);
      };
      document.querySelectorAll('.submit-ev').forEach(function (btn) {
        btn.onclick = async function () {
          const msg = document.getElementById('inc-msg');
          try {
            await api('/api/v1/safety-events/' + btn.dataset.id + '/submit', {
              method: 'POST',
              body: JSON.stringify({ rowVersion: Number(btn.dataset.rv) })
            });
            msg.innerHTML = '<div class="msg">Udalost odoslana.</div>';
            navigate('incidents');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
