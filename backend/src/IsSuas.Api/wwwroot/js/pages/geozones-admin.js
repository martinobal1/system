import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { chipHtml, confirmAction, content, makeTable, pageHead, sectionCard, statusBadge } from '../core/ui.js';

export async function renderGeozonesAdmin() {
      var imports = [];
      var loadErr = null;
      try {
        imports = await api('/api/v1/geo-zones/imports');
      } catch (err) {
        loadErr = err.message || 'Nemate opravnenie na spravu GEO zon (vyžaduje sa centralny spravca).';
      }

      function statusSk(s) {
        var n = Number(s);
        if (s === 'Preview' || n === 0) return 'Nahľad';
        if (s === 'Active' || n === 1) return 'Aktivna';
        if (s === 'Superseded' || n === 2) return 'Nahradena';
        if (s === 'Failed' || n === 3) return 'Chyba';
        if (s === 'Removed' || n === 4) return 'Odstranena';
        return String(s);
      }

      var rows = (imports || []).map(function (i) {
        var active = i.isActiveVersion || i.status === 1 || i.status === 'Active';
        var actions = '—';
        if (i.status === 0 || i.status === 'Preview') {
          actions = '<button type="button" class="secondary geo-activate" data-id="' + i.id + '">Aktivovat</button>' +
            '<button type="button" class="secondary geo-discard" data-id="' + i.id + '">Zrušiť</button>';
        } else if (active) {
          actions = '<button type="button" class="secondary geo-remove" data-id="' + i.id + '">Odstrániť z mapy</button>';
        }
        return '<tr class="row-tint" style="--row-accent:' + (active ? '#0d8f7a' : '#94a3b8') + '">' +
          '<td>' + (i.fileName || '—') + '</td>' +
          '<td>' + (i.dataVersionDate ? new Date(i.dataVersionDate).toLocaleDateString('sk-SK') : '—') + '</td>' +
          '<td>' + (i.importedAt ? new Date(i.importedAt).toLocaleString('sk-SK') : '—') + '</td>' +
          '<td>' + (i.importedByUserName || '—') + '</td>' +
          '<td>' + (i.zoneCount != null ? i.zoneCount : '—') + '</td>' +
          '<td>' + statusBadge(statusSk(i.status)) + (active ? ' <span class="chip chip-soft">aktualna</span>' : '') + '</td>' +
          '<td>' + (i.resultMessage || '—') + '</td>' +
          '<td><div class="row-actions">' + actions + '</div></td></tr>';
      }).join('') || '<tr><td colspan="8">Zatial ziadne importy</td></tr>';

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Sprava GEO zon', 'Import ZIP/KML zo stranok Dopravneho uradu SR. Iba centralny spravca.',
          chipHtml('Importy', imports.length, 'chip-soft')) +
        (loadErr ? '<div class="error">' + loadErr + '</div>' : '') +
        '<div class="form-panel">' +
        '<h3>Importovat GEO zony</h3>' +
        '<p class="muted">Stiahnite aktualny ZIP/KML z <a href="https://letectvo.nsat.sk/bezpilotne-letectvo/zemepisne-oblasti-uas/" target="_blank" rel="noopener">letectvo.nsat.sk</a>, nahrajte sem a potvrdte aktivaciu.</p>' +
        '<form id="geo-import-form" class="form-grid">' +
        '<label>Dátum verzie udajov<input id="geo-version-date" type="date" /></label>' +
        '<label class="full">Subor ZIP alebo KML<input id="geo-file" type="file" accept=".kml,.zip,application/vnd.google-earth.kml+xml,application/zip" required /></label>' +
        '<div class="full toolbar"><button type="submit">Importovat (nahľad)</button></div>' +
        '</form>' +
        '<div id="geo-import-msg"></div>' +
        '<div id="geo-preview" class="hidden"></div>' +
        '</div>' +
        sectionCard('História importov',
          makeTable(['Subor', 'Verzia udajov', 'Import', 'Používateľ', 'Zony', 'Vysledok', 'Sprava', 'Akcie'], rows, true),
          '#2b7bb9') +
        '</div>';

      if (loadErr) return;

      document.getElementById('geo-import-form').onsubmit = async function (ev) {
        ev.preventDefault();
        var msg = document.getElementById('geo-import-msg');
        var preview = document.getElementById('geo-preview');
        var fileInput = document.getElementById('geo-file');
        var file = fileInput.files && fileInput.files[0];
        if (!file) {
          msg.innerHTML = '<div class="error">Vyberte subor</div>';
          return;
        }
        msg.innerHTML = '<div class="msg">Spracuvam subor…</div>';
        preview.classList.add('hidden');
        try {
          var buf = await file.arrayBuffer();
          var bytes = new Uint8Array(buf);
          var chunk = 0x8000;
          var parts = [];
          for (var i = 0; i < bytes.length; i += chunk) {
            parts.push(String.fromCharCode.apply(null, bytes.subarray(i, Math.min(i + chunk, bytes.length))));
          }
          var b64 = btoa(parts.join(''));
          var versionDate = document.getElementById('geo-version-date').value || null;
          var created = await api('/api/v1/geo-zones/imports/preview', {
            method: 'POST',
            body: JSON.stringify({
              fileName: file.name,
              contentBase64: b64,
              dataVersionDate: versionDate
            })
          });
          msg.innerHTML = '<div class="msg">Nahľad pripraveny.</div>';
          preview.classList.remove('hidden');
          preview.innerHTML =
            '<div class="form-panel" style="margin-top:0.75rem">' +
            '<h3>Nahľad importu</h3>' +
            '<dl class="flight-meta">' +
            '<div><dt>Subor</dt><dd>' + created.fileName + '</dd></div>' +
            '<div><dt>Verzia udajov</dt><dd>' +
            (created.dataVersionDate ? new Date(created.dataVersionDate).toLocaleDateString('sk-SK') : '—') +
            '</dd></div>' +
            '<div><dt>Počet zon</dt><dd>' + created.zoneCount + '</dd></div>' +
            '<div><dt>Stav</dt><dd>' + statusSk(created.status) + '</dd></div>' +
            '</dl>' +
            '<div class="toolbar" style="margin-top:0.75rem">' +
            '<button type="button" id="geo-preview-activate">Potvrdiť aktivaciu</button> ' +
            '<button type="button" class="secondary" id="geo-preview-discard">Zrušiť nahľad</button> ' +
            '<button type="button" class="secondary" id="geo-preview-map">Otvoriť mapu</button>' +
            '</div></div>';
          document.getElementById('geo-preview-activate').onclick = async function () {
            try {
              await api('/api/v1/geo-zones/imports/' + created.id + '/activate', { method: 'POST', body: '{}' });
              navigate('geozones-admin');
            } catch (e) {
              msg.innerHTML = '<div class="error">' + e.message + '</div>';
            }
          };
          document.getElementById('geo-preview-discard').onclick = async function () {
            try {
              await api('/api/v1/geo-zones/imports/' + created.id + '/discard', { method: 'POST', body: '{}' });
              navigate('geozones-admin');
            } catch (e) {
              msg.innerHTML = '<div class="error">' + e.message + '</div>';
            }
          };
          document.getElementById('geo-preview-map').onclick = function () {
            document.querySelectorAll('.nav').forEach(function (b) {
              b.classList.toggle('active', b.dataset.page === 'geozones');
            });
            navigate('geozones');
          };
        } catch (err) {
          msg.innerHTML = '<div class="error">' + (err.message || 'Import zlyhal') +
            '. Aktivna verzia nebola nahradena.</div>';
        }
      };

      content.querySelectorAll('.geo-activate').forEach(function (btn) {
        btn.onclick = async function () {
          try {
            await api('/api/v1/geo-zones/imports/' + btn.dataset.id + '/activate', { method: 'POST', body: '{}' });
            navigate('geozones-admin');
          } catch (err) {
            document.getElementById('geo-import-msg').innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      content.querySelectorAll('.geo-discard').forEach(function (btn) {
        btn.onclick = async function () {
          try {
            await api('/api/v1/geo-zones/imports/' + btn.dataset.id + '/discard', { method: 'POST', body: '{}' });
            navigate('geozones-admin');
          } catch (err) {
            document.getElementById('geo-import-msg').innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      content.querySelectorAll('.geo-remove').forEach(function (btn) {
        btn.onclick = async function () {
          if (!confirmAction('Odstrániť aktívny KML/GEO import z mapy?')) return;
          try {
            await api('/api/v1/geo-zones/imports/' + btn.dataset.id + '/remove', { method: 'POST', body: '{}' });
            navigate('geozones-admin');
          } catch (err) {
            document.getElementById('geo-import-msg').innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
