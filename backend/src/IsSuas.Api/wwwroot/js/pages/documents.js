import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { bindFormReveals, chipHtml, content, makeTable, pageHead, statusBadge, statusLabelSk, typeLabelSk } from '../core/ui.js';
import { loadUasOptions, uasSelectHtml } from '../shared/uas.js';

export async function renderDocuments() {
      const docs = await api('/api/v1/documents');
      const templates = await api('/api/v1/form-templates');
      const risks = await api('/api/v1/risk-assessments');
      const uasList = await loadUasOptions();
      const uasOpts = uasSelectHtml(uasList);
      const templateOpts = templates.map(function (t) {
        return '<option value="' + t.id + '">' + t.code + ' v' + t.version + ' — ' + t.nameSk + '</option>';
      }).join('') || '<option value="">(ziadne sablony)</option>';

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Dokumenty', 'Dokumenty, šablóny checklistov a posúdenia rizika.',
          chipHtml('Dokumenty', docs.length, 'chip-soft') +
          chipHtml('Šablóny', templates.length) +
          chipHtml('Riziká', risks.length, 'chip-warn')) +
        '<div class="toolbar">' +
        '<button type="button" data-reveal="doc-panel">Nahrat dokument</button>' +
        '<button type="button" data-reveal="chk-panel">Spustit checklist</button>' +
        '<button type="button" data-reveal="risk-panel">Posudenie rizika</button>' +
        '<button type="button" class="secondary" id="doc-refresh">Obnovit exspiracie</button>' +
        '</div>' +
        '<div id="doc-msg"></div>' +
        '<div id="doc-panel" class="form-panel hidden">' +
        '<h3>Nahrat dokument</h3>' +
        '<form id="doc-form" class="form-grid">' +
        '<label class="full">Nazov<input id="doc-title" required placeholder="napr. Poistenie" /></label>' +
        '<label>Typ kod<input id="doc-type" value="INSURANCE" /></label>' +
        '<label>UAS<select id="doc-uas">' + uasOpts + '</select></label>' +
        '<label>Subor (nazov)<input id="doc-file" value="dokument.pdf" required /></label>' +
        '<label>Checksum<input id="doc-hash" value="demo-hash-001" required /></label>' +
        '<label>Platnost do<input id="doc-validto" type="date" /></label>' +
        '<label>Povinny<select id="doc-mandatory"><option value="false">Nie</option><option value="true">Ano</option></select></label>' +
        '<label>Nahradit dokument ID<input id="doc-supersede" placeholder="volitelne GUID aktualnej verzie" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Ulozit dokument</button>' +
        '<button type="button" class="secondary" data-hide="doc-panel">Zrusit</button>' +
        '</div></form></div>' +
        '<div id="chk-panel" class="form-panel hidden">' +
        '<h3>Spustit checklist</h3>' +
        '<form id="chk-form" class="form-grid">' +
        '<label>Sablona<select id="chk-template">' + templateOpts + '</select></label>' +
        '<label>UAS (ciel)<select id="chk-uas">' + uasOpts + '</select></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Spustit a vyplnit (demo Ano)</button>' +
        '<button type="button" class="secondary" data-hide="chk-panel">Zrusit</button>' +
        '</div></form></div>' +
        '<div id="risk-panel" class="form-panel hidden">' +
        '<h3>Posudenie rizika</h3>' +
        '<form id="risk-form" class="form-grid">' +
        '<label>UAS<select id="risk-uas">' + uasOpts + '</select></label>' +
        '<label>Pravdepodobnost 1-5<input id="risk-l" type="number" min="1" max="5" value="3" /></label>' +
        '<label>Zavaznost 1-5<input id="risk-s" type="number" min="1" max="5" value="3" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">Vypocitat riziko</button>' +
        '<button type="button" class="secondary" data-hide="risk-panel">Zrusit</button>' +
        '</div></form></div>' +
        '<h2>Dokumenty</h2>' +
        makeTable(['Nazov', 'Typ', 'Verzia', 'Stav', 'Aktualny', 'Hash'],
          docs.map(function (d) {
            return '<tr><td>' + d.title + '</td><td>' + typeLabelSk(d.documentTypeCode) + '</td><td>' + d.version +
              '</td><td>' + statusBadge(d.status) + '</td><td>' + (d.isCurrent ? 'Ano' : 'Nie') +
              '</td><td>' + String(d.contentHash).substring(0, 12) + '</td></tr>';
          }).join('') || '<tr><td colspan="6">Ziadne dokumenty</td></tr>') +
        '<h2>Sablony</h2>' +
        makeTable(['Kod', 'Nazov', 'Druh', 'Verzia'],
          templates.map(function (t) {
            return '<tr><td>' + t.code + '</td><td>' + t.nameSk + '</td><td>' + typeLabelSk(t.kind) +
              '</td><td>' + t.version + '</td></tr>';
          }).join('') || '<tr><td colspan="4">Ziadne sablony</td></tr>') +
        '<h2>Posudenia rizika</h2>' +
        makeTable(['Skóre', 'Trieda', 'P', 'Z', 'Metodika'],
          risks.map(function (r) {
            return '<tr><td>' + r.score + '</td><td>' + statusLabelSk(r.classification) + '</td><td>' + r.likelihood +
              '</td><td>' + r.severity + '</td><td>' + typeLabelSk(r.methodologyCode) + '</td></tr>';
          }).join('') || '<tr><td colspan="5">Ziadne posudenia</td></tr>') +
        '</div>';

      bindFormReveals();

      document.getElementById('doc-refresh').onclick = async function () {
        const msg = document.getElementById('doc-msg');
        try {
          const res = await api('/api/v1/documents/refresh-expirations', { method: 'POST' });
          msg.innerHTML = '<div class="msg">Exspirovalo: ' + res.expiredCount + '</div>';
          navigate('documents');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.getElementById('doc-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('doc-msg');
        const uasId = document.getElementById('doc-uas').value;
        if (!uasId) {
          msg.innerHTML = '<div class="error">Vyberte UAS (alebo vytvorte v Poruchach / Register).</div>';
          return;
        }
        try {
          const validTo = document.getElementById('doc-validto').value;
          const supersede = document.getElementById('doc-supersede').value.trim();
          await api('/api/v1/documents', {
            method: 'POST',
            body: JSON.stringify({
              title: document.getElementById('doc-title').value.trim(),
              documentTypeCode: document.getElementById('doc-type').value.trim() || 'GENERAL',
              linkedEntityType: 1,
              linkedEntityId: uasId,
              fileName: document.getElementById('doc-file').value.trim(),
              contentType: 'application/pdf',
              sizeBytes: 1024,
              contentHash: document.getElementById('doc-hash').value.trim(),
              sensitivity: 1,
              validFrom: new Date().toISOString(),
              validTo: validTo ? new Date(validTo).toISOString() : null,
              isMandatory: document.getElementById('doc-mandatory').value === 'true',
              blocksWhenInvalid: document.getElementById('doc-mandatory').value === 'true',
              supersedesDocumentId: supersede || null,
              notes: null
            })
          });
          msg.innerHTML = '<div class="msg">Dokument ulozeny.</div>';
          navigate('documents');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.getElementById('chk-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('doc-msg');
        const templateId = document.getElementById('chk-template').value;
        const uasId = document.getElementById('chk-uas').value;
        if (!templateId || !uasId) {
          msg.innerHTML = '<div class="error">Vyberte sablonu a UAS.</div>';
          return;
        }
        try {
          const template = templates.find(function (t) { return t.id === templateId; });
          const started = await api('/api/v1/form-submissions', {
            method: 'POST',
            body: JSON.stringify({
              formTemplateId: templateId,
              linkedEntityType: 1,
              linkedEntityId: uasId
            })
          });
          const answers = (template.items || []).filter(function (i) {
            return i.itemType === 4 || i.itemType === 'YesNo';
          }).map(function (i) {
            return {
              formTemplateItemId: i.id,
              itemCode: i.code,
              valueText: null,
              valueBool: true,
              valueNumber: null,
              valueDate: null
            };
          });
          await api('/api/v1/form-submissions/' + started.id + '/answers', {
            method: 'POST',
            body: JSON.stringify({
              answers: answers,
              markCompleted: true,
              rowVersion: started.rowVersion
            })
          });
          msg.innerHTML = '<div class="msg">Checklist dokonceny (viazany na sablonu v' + started.templateVersion + ').</div>';
          navigate('documents');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      document.getElementById('risk-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('doc-msg');
        const uasId = document.getElementById('risk-uas').value;
        if (!uasId) {
          msg.innerHTML = '<div class="error">Vyberte UAS.</div>';
          return;
        }
        try {
          const res = await api('/api/v1/risk-assessments', {
            method: 'POST',
            body: JSON.stringify({
              linkedEntityType: 1,
              linkedEntityId: uasId,
              likelihood: Number(document.getElementById('risk-l').value),
              severity: Number(document.getElementById('risk-s').value),
              notes: 'UI demo',
              formSubmissionId: null
            })
          });
          msg.innerHTML = '<div class="msg">Riziko: score ' + res.score + ' = ' + res.classification + '</div>';
          navigate('documents');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };
      return;
}
