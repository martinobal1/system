import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, confirmAction, content, pageHead } from '../core/ui.js';
import { addTechDocToCatalog, ensureTechDocCatalog, getTechDocCatalog, getTechDocFormSchema, removeTechDocFromCatalog, techDocSlug } from '../shared/techDocs.js';
import { extractPdfText, fileToBase64 } from '../shared/pdfText.js';

function escH(v) {
  return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}
function fmtSize(b) {
  if (!b) return '';
  return b > 1048576 ? (b / 1048576).toFixed(1).replace('.', ',') + ' MB' : Math.round(b / 1024) + ' kB';
}
async function uploadManual(slug, file, doc) {
  if (file.size > 50 * 1024 * 1024) throw new Error('PDF môže mať najviac 50 MB.');
  var b64 = await fileToBase64(file);
  return api('/api/v1/uas-types/' + encodeURIComponent(slug) + '/manual', {
    method: 'POST',
    body: JSON.stringify({ fileName: file.name, contentBase64: b64, manufacturer: doc && doc.manufacturer, model: doc && doc.model })
  });
}
/** PDF manuálu ako blob (s prihlásením). */
async function fetchManualBlob(slug) {
  var res = await fetch('/api/v1/uas-types/' + encodeURIComponent(slug) + '/manual', {
    headers: state.token ? { Authorization: 'Bearer ' + state.token } : {}
  });
  if (!res.ok) throw new Error('Manuál sa nepodarilo načítať (' + res.status + ').');
  return res.blob();
}

export async function renderComponents() {
      // Katalog typov spravuje len L1; ostatni maju dokumentaciu na citanie.
      var canManage = state.accessLevel === 1;
      if (!canManage) state.techDocCreating = false;
      await ensureTechDocCatalog();
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

          var pdf = state._techPdf;
          content.innerHTML = '<div class="stack">' +
            pageHead('Nový typ v katalógu',
              'Nahrajte PDF manuál výrobcu — AI z neho vyplní tabuľky špecifikácií. Pred uložením ich skontrolujte.',
              chipHtml('Povinné', 'výrobca + model', 'chip-soft')) +
            '<div class="toolbar">' +
            '<button type="button" class="secondary" id="tech-create-back">Späť na zoznam</button> ' +
            '<button type="button" id="tech-create-save">Uložiť typ</button>' +
            '</div>' +
            '<div id="tech-create-msg"></div>' +
            '<div class="form-panel tech-ai-panel">' +
            '<h3 style="margin:0 0 0.35rem">PDF manuál výrobcu</h3>' +
            '<p class="muted" style="margin:0 0 0.75rem">Napr. „User Manual“ z oficiálnej stránky výrobcu (DJI Download Center). ' +
            'Z PDF sa prečíta kapitola so špecifikáciami a AI z nej pripraví tabuľky. PDF sa uloží k typu na stiahnutie.</p>' +
            '<div class="tech-ai-row">' +
            '<label class="tech-file"><input type="file" id="tech-pdf" accept="application/pdf,.pdf" />' +
            '<span>' + (pdf ? '📄 ' + escH(pdf.file.name) + ' · ' + fmtSize(pdf.file.size) + (pdf.pages ? ' · ' + pdf.pages + ' strán' : '') : 'Vybrať PDF…') + '</span></label>' +
            '<button type="button" id="tech-ai-run"' + (pdf ? '' : ' disabled') + '>✨ Vyplniť pomocou AI</button>' +
            '</div>' +
            '<div id="tech-ai-status" class="muted" style="margin-top:0.5rem">' + (state._techAiStatus || '') + '</div>' +
            '</div>' +
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
            state._techPdf = null;
            state._techAiStatus = '';
            navigate('components');
          };

          function setAiStatus(html) {
            state._techAiStatus = html;
            var el = document.getElementById('tech-ai-status');
            if (el) el.innerHTML = html;
          }

          document.getElementById('tech-pdf').onchange = async function () {
            saveValues();
            var file = this.files && this.files[0];
            if (!file) return;
            if (!/\.pdf$/i.test(file.name) && file.type !== 'application/pdf') {
              setAiStatus('<span class="error-text">Vyberte súbor PDF.</span>');
              return;
            }
            state._techPdf = { file: file, text: null, pages: 0 };
            setAiStatus('Čítam PDF…');
            renderForm();
            try {
              var res = await extractPdfText(await file.arrayBuffer(), function (n, total) {
                setAiStatus('Čítam PDF… strana ' + n + ' / ' + total);
              });
              state._techPdf.text = res.text;
              state._techPdf.pages = res.pages;
              setAiStatus(res.text.replace(/--- strana \d+ ---/g, '').trim().length < 200
                ? '<span class="error-text">PDF neobsahuje text (asi je to sken). Uloží sa ako príloha, parametre vyplňte ručne.</span>'
                : 'PDF načítané (' + res.pages + ' strán). Kliknite „Vyplniť pomocou AI“.');
              renderForm();
            } catch (err) {
              setAiStatus('<span class="error-text">' + escH(err.message || 'PDF sa nepodarilo prečítať.') + '</span>');
            }
          };

          document.getElementById('tech-ai-run').onclick = async function () {
            saveValues();
            var p = state._techPdf;
            if (!p || !p.text) { setAiStatus('Najprv vyberte PDF a počkajte, kým sa načíta.'); return; }
            var btn = this;
            btn.disabled = true;
            setAiStatus('<span class="tech-spinner"></span> AI spracúva manuál… (zvyčajne do 1 minúty)');
            var mfrHint = '', modelHint = '';
            secs.forEach(function (sec) { sec.fields.forEach(function (f) {
              if (f.key === 'manufacturer') mfrHint = (f.value || '').trim();
              if (f.key === 'model') modelHint = (f.value || '').trim();
            }); });
            try {
              var out = await api('/api/v1/uas-types/extract', {
                method: 'POST',
                body: JSON.stringify({ text: p.text.slice(0, 1500000), fileName: p.file.name, manufacturerHint: mfrHint || null, modelHint: modelHint || null })
              });
              var d = (out && out.data) || {};
              function v(x) { return x == null ? '' : String(x); }
              var basics = {
                title: 'Základné údaje',
                fields: [
                  { key: 'manufacturer', label: 'Výrobca', specLabel: null, required: true, placeholder: 'napr. DJI', value: mfrHint || v(d.manufacturer) },
                  { key: 'model', label: 'Model', specLabel: null, required: true, placeholder: 'napr. Mavic 3T', value: modelHint || v(d.model) },
                  { key: 'classEu', label: 'EÚ trieda', specLabel: null, required: false, placeholder: 'napr. C2', value: v(d.classEu) },
                  { key: 'tagline', label: 'Kategória / popis', specLabel: null, required: false, placeholder: 'napr. Termovízny dron', value: v(d.tagline) }
                ]
              };
              var aiSecs = (Array.isArray(d.sections) ? d.sections : []).map(function (sec, si) {
                return {
                  title: v(sec.title) || ('Sekcia ' + (si + 1)),
                  fields: (Array.isArray(sec.rows) ? sec.rows : []).filter(function (r) { return Array.isArray(r) && r.length >= 2; })
                    .map(function (r, ri) {
                      return { key: 'ai_' + si + '_' + ri, label: v(r[0]), specLabel: v(r[0]), required: false, placeholder: '', value: v(r[1]) };
                    })
                };
              }).filter(function (sec) { return sec.fields.length; });
              secs.length = 0;
              secs.push(basics);
              aiSecs.forEach(function (sec) { secs.push(sec); });
              var rows = aiSecs.reduce(function (n, sec) { return n + sec.fields.length; }, 0);
              setAiStatus('✓ AI vyplnila ' + aiSecs.length + ' sekcií a ' + rows + ' parametrov. <b>Skontrolujte hodnoty</b> a potom kliknite „Uložiť typ“.');
              renderForm();
            } catch (err) {
              btn.disabled = false;
              setAiStatus('<span class="error-text">' + escH(err.message || 'AI spracovanie zlyhalo.') + '</span>');
            }
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

            var saveBtn = this;
            saveBtn.disabled = true;
            msg.innerHTML = '<div class="msg">Ukladám…</div>';
            (async function () {
              try {
                var slug = await addTechDocToCatalog({
                  manufacturer: mfr,
                  model: model,
                  classEu: classEu || '—',
                  tagline: tagline || '',
                  matchModels: [model],
                  sections: sections
                });
                if (state._techPdf && state._techPdf.file) {
                  msg.innerHTML = '<div class="msg">Nahrávam PDF manuál…</div>';
                  await uploadManual(slug, state._techPdf.file, { manufacturer: mfr, model: model });
                  await ensureTechDocCatalog();
                }
                state.techDocCreating = false;
                state._techFormSections = null;
                state._techPdf = null;
                state._techAiStatus = '';
                state.techDocModelId = slug || techDocSlug(mfr, model);
                state.techDocTab = 'specs';
                navigate('components');
              } catch (err) {
                saveBtn.disabled = false;
                msg.innerHTML = '<div class="error">' + escH(err.message || 'Uloženie zlyhalo.') + '</div>';
              }
            })();
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
        var tab = state.techDocTab === 'overview' || state.techDocTab === 'manual' ? state.techDocTab : 'specs';
        if (tab === 'manual' && !doc.hasManual && !canManage) tab = 'specs';
        var count = fleetCountFor(doc);
        var tabs = [
          { id: 'overview', label: 'Prehľad' },
          { id: 'specs', label: 'Technická špecifikácia' }
        ].concat(doc.hasManual || canManage ? [{ id: 'manual', label: 'Manuál (PDF)' }] : []);
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
        } else if (tab === 'manual') {
          body = '<div class="form-panel">' +
            (doc.hasManual
              ? '<div class="tech-manual-head"><div><b>📄 ' + escH(doc.manualFileName || 'manual.pdf') + '</b><span class="muted">' +
                fmtSize(doc.manualSizeBytes) + (doc.manualUploadedAt ? ' · nahraté ' + new Date(doc.manualUploadedAt).toLocaleDateString('sk-SK') : '') +
                '</span></div><div class="toolbar" style="margin:0"><button type="button" class="secondary" id="tech-manual-open">Otvoriť v novom okne</button>' +
                '<button type="button" class="secondary" id="tech-manual-download">Stiahnuť</button></div></div>' +
                '<div id="tech-manual-view" class="tech-manual-view"><p class="muted">Načítavam PDF…</p></div>'
              : '<p class="muted" style="margin:0">K tomuto typu zatiaľ nie je nahraný manuál výrobcu.</p>') +
            (canManage
              ? '<div class="tech-ai-row" style="margin-top:0.85rem"><label class="tech-file"><input type="file" id="tech-manual-file" accept="application/pdf,.pdf" />' +
                '<span>' + (doc.hasManual ? 'Nahradiť PDF…' : 'Nahrať PDF…') + '</span></label><span id="tech-manual-msg" class="muted"></span></div>'
              : '') +
            '</div>';
        } else {
          body = (doc.sections || []).map(function (sec) {
            return '<div class="spec-section form-panel">' +
              '<h3>' + escH(sec.title) + '</h3>' +
              '<table class="spec-table"><thead><tr><th>Parameter</th><th>Hodnota</th></tr></thead><tbody>' +
              (sec.rows || []).map(function (r) {
                return '<tr><th>' + escH(r[0]) + '</th><td>' + escH(r[1]) + '</td></tr>';
              }).join('') +
              '</tbody></table></div>';
          }).join('') || '<div class="form-panel"><p class="muted" style="margin:0">Zatiaľ bez špecifikácie — doplňte neskôr.</p></div>';
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
          removeCurrentBtn.onclick = async function () {
            if (!confirmAction('Odobrať „' + doc.manufacturer + ' ' + doc.model + '“ z technickej dokumentácie?')) return;
            try { await removeTechDocFromCatalog(doc.id); } catch (err) { alert(err.message || 'Odobratie zlyhalo.'); return; }
            state.techDocModelId = null;
            navigate('components');
          };
        }
        if (tab === 'manual') {
          if (doc.hasManual) {
            var manualUrl = null;
            fetchManualBlob(doc.id).then(function (blob) {
              manualUrl = URL.createObjectURL(blob);
              var view = document.getElementById('tech-manual-view');
              if (view) view.innerHTML = '<iframe title="Manuál ' + escH(doc.model) + '" src="' + manualUrl + '"></iframe>';
            }).catch(function (err) {
              var view = document.getElementById('tech-manual-view');
              if (view) view.innerHTML = '<div class="error">' + escH(err.message) + '</div>';
            });
            document.getElementById('tech-manual-open').onclick = function () { if (manualUrl) window.open(manualUrl, '_blank', 'noopener'); };
            document.getElementById('tech-manual-download').onclick = function () {
              if (!manualUrl) return;
              var a = document.createElement('a');
              a.href = manualUrl; a.download = doc.manualFileName || 'manual.pdf';
              document.body.appendChild(a); a.click(); a.remove();
            };
          }
          var manualFile = document.getElementById('tech-manual-file');
          if (manualFile) manualFile.onchange = async function () {
            var file = this.files && this.files[0];
            if (!file) return;
            var m = document.getElementById('tech-manual-msg');
            m.textContent = 'Nahrávam…';
            try {
              await uploadManual(doc.id, file, doc);
              await ensureTechDocCatalog();
              navigate('components');
            } catch (err) { m.innerHTML = '<span class="error-text">' + escH(err.message) + '</span>'; }
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
          '<div class="td-title">' + escH(doc.manufacturer + ' ' + doc.model) + (doc.hasManual ? ' <span class="td-pdf" title="Má PDF manuál">PDF</span>' : '') + '</div>' +
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
          removeTechDocFromCatalog(btn.dataset.id).then(function () { navigate('components'); })
            .catch(function (err) { alert(err.message || 'Odobratie zlyhalo.'); });
        };
      });
      return;
}
