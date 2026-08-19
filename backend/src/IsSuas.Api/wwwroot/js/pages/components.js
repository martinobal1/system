import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, confirmAction, content, pageHead } from '../core/ui.js';
import { addTechDocToCatalog, getTechDocCatalog, getTechDocFormSchema, removeTechDocFromCatalog, techDocSlug } from '../shared/techDocs.js';

export async function renderComponents() {
      // Katalog typov spravuje len L1; ostatni maju dokumentaciu na citanie.
      var canManage = state.accessLevel === 1;
      if (!canManage) state.techDocCreating = false;
      var catalog = getTechDocCatalog();
      var uasData = { items: [] };
      try { uasData = await api('/api/v1/uas?page=1&pageSize=500'); } catch (_) {}
      var uasList = uasData.items || [];

      function fleetCountFor(doc) {
        return uasList.filter(function (u) {
          if (u.status === 'Decommissioned' || u.status === 8) return false;
          var model = String(u.model || '');
          return (doc.matchModels || []).some(function (m) {
            return model.toLowerCase() === String(m).toLowerCase();
          });
        }).length;
      }

      if (state.techDocCreating) {
        var schema = getTechDocFormSchema();
        // Editovatelna kopia: kazda sekcia a pole sa da pridat/odobrat.
        if (!state._techFormSections) {
          state._techFormSections = schema.map(function (sec) {
            return {
              title: sec.title,
              fields: sec.fields.map(function (f) {
                return { key: f.key, label: f.label, specLabel: f.specLabel, placeholder: f.placeholder, required: f.required, value: '' };
              })
            };
          });
        }
        var secs = state._techFormSections;

        function renderForm() {
          var sectionsHtml = secs.map(function (sec, si) {
            var fields = sec.fields.map(function (f, fi) {
              var req = f.required ? ' <span style="color:var(--danger)">*</span>' : '';
              var hint = f.placeholder ? ' placeholder="' + f.placeholder + '"' : '';
              return '<div class="tech-field-row">' +
                '<label>' + f.label + req +
                '<input type="text" data-sec="' + si + '" data-field="' + fi + '" data-key="' + f.key + '"' +
                hint + (f.required ? ' required' : '') + ' value="' + (f.value || '').replace(/"/g, '&quot;') + '">' +
                '</label>' +
                (f.required ? '' : '<button type="button" class="btn-danger tech-rm-field" data-sec="' + si + '" data-field="' + fi + '" title="Odobrať pole" style="padding:0.3rem 0.55rem;font-size:0.85rem;align-self:end;margin-bottom:0.3rem">×</button>') +
                '</div>';
            }).join('');
            return '<div class="form-panel spec-section" data-sec-idx="' + si + '">' +
              '<div style="display:flex;align-items:center;justify-content:space-between;margin:0 0 0.75rem">' +
              '<h3 style="margin:0">' + sec.title + '</h3>' +
              '<button type="button" class="secondary tech-rename-sec" data-sec="' + si + '" style="padding:0.3rem 0.7rem;font-size:0.82rem">Premenovať</button> ' +
              '<button type="button" class="btn-danger tech-rm-sec" data-sec="' + si + '" style="padding:0.3rem 0.7rem;font-size:0.82rem">Odobrať sekciu</button>' +
              '</div>' +
              '<div class="form-grid tech-field-grid">' + fields + '</div>' +
              '<button type="button" class="secondary tech-add-field" data-sec="' + si + '" style="margin-top:0.5rem;font-size:0.85rem">+ Pridať pole</button>' +
              '</div>';
          }).join('');

          content.innerHTML = '<div class="stack">' +
            pageHead('Nový typ v katalógu',
              'Povinné sú len výrobca a model. Sekcie aj polia môžete pridávať a odoberať.',
              chipHtml('Povinné', 'výrobca + model', 'chip-soft')) +
            '<div class="toolbar">' +
            '<button type="button" class="secondary" id="tech-create-back">Späť na zoznam</button> ' +
            '<button type="button" id="tech-create-save">Uložiť typ</button>' +
            '</div>' +
            '<div id="tech-create-msg"></div>' +
            sectionsHtml +
            '<button type="button" class="secondary" id="tech-add-sec" style="margin-top:0.5rem">+ Pridať sekciu</button>' +
            '</div>';

          // Uloz aktualne hodnoty z inputov pred rebindom
          function saveValues() {
            content.querySelectorAll('input[data-sec]').forEach(function (inp) {
              var s = Number(inp.dataset.sec), f = Number(inp.dataset.field);
              if (secs[s] && secs[s].fields[f]) secs[s].fields[f].value = inp.value;
            });
          }

          document.getElementById('tech-create-back').onclick = function () {
            state.techDocCreating = false;
            state._techFormSections = null;
            navigate('components');
          };

          document.getElementById('tech-add-sec').onclick = function () {
            saveValues();
            var name = prompt('Názov novej sekcie:');
            if (!name) return;
            secs.push({ title: name.trim(), fields: [] });
            renderForm();
          };

          content.querySelectorAll('.tech-rename-sec').forEach(function (btn) {
            btn.onclick = function () {
              saveValues();
              var si = Number(btn.dataset.sec);
              var name = prompt('Nový názov sekcie:', secs[si].title);
              if (!name || !name.trim()) return;
              secs[si].title = name.trim();
              renderForm();
            };
          });

          content.querySelectorAll('.tech-rm-sec').forEach(function (btn) {
            btn.onclick = function () {
              saveValues();
              var si = Number(btn.dataset.sec);
              if (!confirmAction('Odobrať sekciu „' + secs[si].title + '" aj so všetkými poľami?')) return;
              secs.splice(si, 1);
              renderForm();
            };
          });

          content.querySelectorAll('.tech-add-field').forEach(function (btn) {
            btn.onclick = function () {
              saveValues();
              var si = Number(btn.dataset.sec);
              var label = prompt('Názov nového poľa:');
              if (!label) return;
              var key = 'custom_' + Date.now();
              secs[si].fields.push({ key: key, label: label.trim(), specLabel: label.trim(), placeholder: '', required: false, value: '' });
              renderForm();
            };
          });

          content.querySelectorAll('.tech-rm-field').forEach(function (btn) {
            btn.onclick = function () {
              saveValues();
              var si = Number(btn.dataset.sec), fi = Number(btn.dataset.field);
              secs[si].fields.splice(fi, 1);
              renderForm();
            };
          });

          document.getElementById('tech-create-save').onclick = function () {
            saveValues();
            var msg = document.getElementById('tech-create-msg');
            var mfr = '', model = '';
            secs.forEach(function (sec) {
              sec.fields.forEach(function (f) {
                if (f.key === 'manufacturer') mfr = (f.value || '').trim();
                if (f.key === 'model') model = (f.value || '').trim();
              });
            });
            if (!mfr || !model) {
              msg.innerHTML = '<div class="error">Vyplňte výrobcu a model.</div>';
              return;
            }
            var classEu = '', tagline = '';
            secs.forEach(function (sec) {
              sec.fields.forEach(function (f) {
                if (f.key === 'classEu') classEu = (f.value || '').trim();
                if (f.key === 'tagline') tagline = (f.value || '').trim();
              });
            });
            var sections = secs.map(function (sec) {
              var rows = sec.fields
                .filter(function (f) { return f.specLabel && (f.value || '').trim(); })
                .map(function (f) { return [f.specLabel, f.value.trim()]; });
              return { title: sec.title, rows: rows };
            }).filter(function (s) { return s.rows.length > 0; });

            addTechDocToCatalog({
              manufacturer: mfr,
              model: model,
              classEu: classEu || '—',
              tagline: tagline || '',
              matchModels: [model],
              sections: sections
            });
            state.techDocCreating = false;
            state._techFormSections = null;
            state.techDocModelId = techDocSlug(mfr, model);
            state.techDocTab = 'specs';
            navigate('components');
          };
        }
        renderForm();
        return;
      }

      if (state.techDocModelId) {
        var doc = catalog.find(function (d) { return d.id === state.techDocModelId; });
        if (!doc) {
          state.techDocModelId = null;
          navigate('components');
          return;
        }
        var tab = state.techDocTab || 'specs';
        var count = fleetCountFor(doc);
        var tabs = [
          { id: 'overview', label: 'Prehľad' },
          { id: 'specs', label: 'Technická špecifikácia' },
          { id: 'guide', label: 'Rýchly návod' },
          { id: 'docs', label: 'Dokumenty' }
        ];
        var tabHtml = '<div class="tech-tabs">' + tabs.map(function (t) {
          return '<button type="button" class="tech-tab' + (t.id === tab ? ' active' : '') +
            '" data-tab="' + t.id + '">' + t.label + '</button>';
        }).join('') + '</div>';

        var body = '';
        if (tab === 'overview') {
          body = '<div class="form-panel">' +
            '<p style="margin:0 0 0.75rem">Oficiálna technická dokumentácia typu <b>' +
            doc.manufacturer + ' ' + doc.model + '</b>.</p>' +
            '<dl class="flight-meta">' +
            '<div><dt>EÚ trieda</dt><dd>' + (doc.classEu || '—') + '</dd></div>' +
            '<div><dt>Kategória</dt><dd>' + (doc.tagline || '—') + '</dd></div>' +
            '<div><dt>Vo flotile</dt><dd>' + count + ' ks</dd></div>' +
            '<div><dt>Výrobca</dt><dd>' + doc.manufacturer + '</dd></div>' +
            '</dl>' +
            '<p class="muted" style="margin:0.85rem 0 0">Podrobné parametre nájdete v záložke Technická špecifikácia.</p>' +
            '</div>';
        } else if (tab === 'specs') {
          body = (doc.sections || []).map(function (sec) {
            return '<div class="spec-section form-panel">' +
              '<h3>' + sec.title + '</h3>' +
              '<table class="spec-table"><thead><tr><th>Parameter</th><th>Hodnota</th></tr></thead><tbody>' +
              (sec.rows || []).map(function (r) {
                return '<tr><th>' + r[0] + '</th><td>' + r[1] + '</td></tr>';
              }).join('') +
              '</tbody></table></div>';
          }).join('') || '<div class="form-panel"><p class="muted" style="margin:0">Zatiaľ bez špecifikácie — doplňte neskôr.</p></div>';
        } else if (tab === 'guide') {
          body = '<div class="form-panel"><p class="muted" style="margin:0">Rýchly návod pre tento typ dronu sa pripravuje.</p></div>';
        } else {
          body = '<div class="form-panel"><p class="muted" style="margin:0">Dokumenty (PDF, manuály) sa pripravujú.</p></div>';
        }

        content.innerHTML = '<div class="stack">' +
          pageHead(doc.manufacturer + ' ' + doc.model,
            count + ' ks vo flotile · Trieda ' + (doc.classEu || '—') + ' · ' + (doc.tagline || ''),
            chipHtml('Flotila', count + ' ks', 'chip-soft') +
            chipHtml('Trieda', doc.classEu || '—', 'chip-ok')) +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="tech-back">Späť na zoznam</button>' +
          (canManage ? ' <button type="button" class="btn-danger" id="tech-remove-current">Odobrať z katalógu</button>' : '') +
          '</div>' +
          tabHtml + body + '</div>';

        document.getElementById('tech-back').onclick = function () {
          state.techDocModelId = null;
          navigate('components');
        };
        var removeCurrentBtn = document.getElementById('tech-remove-current');
        if (removeCurrentBtn) {
          removeCurrentBtn.onclick = function () {
            if (!confirmAction('Odobrať „' + doc.manufacturer + ' ' + doc.model + '“ z technickej dokumentácie?')) return;
            removeTechDocFromCatalog(doc.id);
            state.techDocModelId = null;
            navigate('components');
          };
        }
        content.querySelectorAll('.tech-tab').forEach(function (btn) {
          btn.onclick = function () {
            state.techDocTab = btn.dataset.tab;
            navigate('components');
          };
        });
        return;
      }

      var cards = catalog.map(function (doc) {
        var count = fleetCountFor(doc);
        return '<div class="tech-doc-card-wrap">' +
          '<button type="button" class="tech-doc-card tech-open" data-id="' + doc.id + '">' +
          '<div class="td-title">' + doc.manufacturer + ' ' + doc.model + '</div>' +
          '<div class="td-meta">' + count + ' ks vo flotile · Trieda ' + (doc.classEu || '—') +
          ' · ' + (doc.tagline || '') + '</div></button>' +
          (canManage
            ? '<button type="button" class="secondary tech-remove" data-id="' + doc.id +
              '" data-label="' + (doc.manufacturer + ' ' + doc.model).replace(/"/g, '&quot;') +
              '">Odobrať</button>'
            : '') +
          '</div>';
      }).join('') || '<p class="muted">Zatiaľ žiadna technická dokumentácia.</p>';

      content.innerHTML = '<div class="stack">' +
        pageHead('Technická dokumentácia',
          'Katalóg typov dronov vo flotile — kliknutím otvoríte špecifikáciu.',
          chipHtml('Typy', catalog.length, 'chip-soft')) +
        (canManage ? '<div class="toolbar"><button type="button" id="tech-add">Pridať typ</button></div>' : '') +
        '<div class="stack" style="gap:0.75rem">' + cards + '</div></div>';

      var addBtn = document.getElementById('tech-add');
      if (addBtn) {
        addBtn.onclick = function () {
          state.techDocCreating = true;
          state.techDocModelId = null;
          navigate('components');
        };
      }
      content.querySelectorAll('.tech-open').forEach(function (btn) {
        btn.onclick = function () {
          state.techDocModelId = btn.dataset.id;
          state.techDocTab = 'specs';
          navigate('components');
        };
      });
      content.querySelectorAll('.tech-remove').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.preventDefault();
          ev.stopPropagation();
          if (!confirmAction('Odobrať „' + (btn.dataset.label || '') + '“ z katalógu?')) return;
          removeTechDocFromCatalog(btn.dataset.id);
          navigate('components');
        };
      });
      return;
}
