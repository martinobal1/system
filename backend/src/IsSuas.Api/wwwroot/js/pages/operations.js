import { api } from '../core/api.js';
import { colorForKey, entityLabelHtml, swatchHtml } from '../core/charts.js';
import { ensureMapLibs } from '../core/maps.js';
import { navigate } from '../core/nav.js';
import { beginFlightRequestFlow } from '../shared/flightRequest.js';
import { state } from '../core/state.js';
import { trashButtonHtml } from '../core/trash.js';
import { chipHtml, confirmAction, content, makeTable, pageHead, searchHighlightBanner, statusBadge, statusLabelSk } from '../core/ui.js';

export async function renderOperations() {
      var isL3 = state.accessLevel === 3;
      var canApprove = state.accessLevel === 1 || state.accessLevel === 2;
      function isSubmitted(o) { return o.status === 'Submitted' || o.status === 1; }
      function isDraftOrReturned(o) {
        return o.status === 'Draft' || o.status === 0 || o.status === 'Returned' || o.status === 3;
      }
      function isReturned(o) { return o.status === 'Returned' || o.status === 3; }
      function isApproved(o) { return o.status === 'Approved' || o.status === 2; }
      function isRejected(o) { return o.status === 'Rejected' || o.status === 4; }
      function isCancelled(o) { return o.status === 'Cancelled' || o.status === 5; }
      function isCompleted(o) { return o.status === 'Completed' || o.status === 6; }
      function isSettled(o) {
        return isCompleted(o) || isRejected(o) || isCancelled(o);
      }
      function opStatusName(o) {
        var map = {
          0: 'Draft', 1: 'Submitted', 2: 'Approved', 3: 'Returned', 4: 'Rejected', 5: 'Cancelled', 6: 'Completed',
          Draft: 'Draft', Submitted: 'Submitted', Approved: 'Approved',
          Returned: 'Returned', Rejected: 'Rejected', Cancelled: 'Cancelled', Completed: 'Completed'
        };
        return map[o.status] || o.status;
      }
      function opStatusBadgeLabel(o) {
        var n = opStatusName(o);
        if (n === 'Completed') return 'Odletené';
        if (n === 'Approved') return 'Schválené — čaká na potvrdenie odletu';
        return statusLabelSk(n);
      }
      function isOwnProposal(o) {
        return String(o.proposedByAccountId || '').toLowerCase() === String(state.accountId || '').toLowerCase()
          && !!state.accountId;
      }
      function canEditOwnReturned(o) {
        return isL3 && isOwnProposal(o) && isReturned(o);
      }
      function canDeleteOwn(o) {
        return isL3 && isOwnProposal(o) && (isDraftOrReturned(o) || isSubmitted(o));
      }
      function parseOpRadiusM(description) {
        var m = String(description || '').match(/Polomer oblasti:\s*(\d+)/i);
        return m ? Number(m[1]) : null;
      }
      function toLocalInput(d) {
        var x = new Date(d);
        if (isNaN(x.getTime())) return '';
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return x.getFullYear() + '-' + pad(x.getMonth() + 1) + '-' + pad(x.getDate()) +
          'T' + pad(x.getHours()) + ':' + pad(x.getMinutes());
      }
      function escapeHtml(s) {
        return String(s || '')
          .replace(/&/g, '&amp;').replace(/</g, '&lt;')
          .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
      }

      if (state.operationDetailId) {
        var detail;
        try {
          detail = await api('/api/v1/operations/' + state.operationDetailId);
        } catch (err) {
          state.operationDetailId = null;
          state.operationEditing = false;
          content.innerHTML = '<div class="error">' + (err.message || 'Žiadosť sa nenašla') +
            '</div><div class="toolbar"><button type="button" class="secondary" id="op-back">Späť</button></div>';
          document.getElementById('op-back').onclick = function () { navigate('operations'); };
          return;
        }
        var op = detail.operation || detail;
        var lat = op.locationLat != null ? Number(op.locationLat) : NaN;
        var lon = op.locationLon != null ? Number(op.locationLon) : NaN;
        var hasCoords = isFinite(lat) && isFinite(lon);
        var radiusM = parseOpRadiusM(detail.description) || 1000;
        if (state.pendingFlightLocation && state.operationEditing) {
          lat = Number(state.pendingFlightLocation.lat);
          lon = Number(state.pendingFlightLocation.lon);
          hasCoords = isFinite(lat) && isFinite(lon);
          if (state.pendingFlightLocation.radiusM != null)
            radiusM = Number(state.pendingFlightLocation.radiusM) || radiusM;
        }
        var resources = detail.resources || [];
        var uasRes = resources.filter(function (r) {
          return r.resourceType === 'Uas' || r.resourceType === 0;
        }).map(function (r) { return r.displayName || String(r.resourceId).substring(0, 8); }).join(', ') || '—';
        var pilotRes = resources.filter(function (r) {
          return r.resourceType === 'Pilot' || r.resourceType === 1;
        }).map(function (r) { return r.displayName || String(r.resourceId).substring(0, 8); }).join(', ') || '—';
        var gpsLabel = hasCoords
          ? (lat.toFixed(5) + ', ' + lon.toFixed(5))
          : (op.locationAddress || '—');
        var returnBlock = '';
        if (isReturned(op) && (detail.decisionReason || op.decisionReason)) {
          returnBlock = '<div class="op-return-note"><strong>Dôvod vrátenia</strong>' +
            escapeHtml(detail.decisionReason || op.decisionReason) + '</div>';
        }
        function metaItem(label, val) {
          return '<div><dt>' + label + '</dt><dd>' + (val || '—') + '</dd></div>';
        }
        var actionBar = '<button type="button" class="secondary" id="op-back">Späť na zoznam</button>';
        if (canApprove && isSubmitted(op)) {
          actionBar +=
            ' <button type="button" class="btn-ok" id="op-d-approve">Schváliť</button>' +
            ' <button type="button" class="btn-warn" id="op-d-return">Vrátiť</button>' +
            ' <button type="button" class="btn-danger" id="op-d-reject">Zamietnuť</button>';
        }
        if (canApprove && isApproved(op)) {
          actionBar +=
            ' <button type="button" class="btn-ok" id="op-d-confirm-flown">Potvrdiť odlet</button>';
        }
        if (isL3 && isDraftOrReturned(op)) {
          actionBar +=
            ' <button type="button" class="secondary" id="op-d-submit">Odoslať na schválenie</button>';
        }
        if (canEditOwnReturned(op)) {
          actionBar +=
            ' <button type="button" class="secondary" id="op-d-edit">' +
            (state.operationEditing ? 'Zrušiť úpravu' : 'Upraviť') + '</button>';
        }
        if (canDeleteOwn(op)) {
          actionBar +=
            ' <button type="button" class="btn-danger" id="op-d-delete">Vymazať</button>';
        }
        if (state.accessLevel === 1 || state.accessLevel === 2) {
          actionBar += ' ' + trashButtonHtml('operations', op.id, op.title);
        }

        var editBlock = '';
        if (state.operationEditing && canEditOwnReturned(op)) {
          var addrVal = escapeHtml(
            (state.pendingFlightLocation && state.pendingFlightLocation.label) ||
            op.locationAddress || ''
          );
          editBlock =
            '<div class="form-panel"><h3>Upraviť žiadosť</h3>' +
            '<form id="op-edit-form" class="form-grid">' +
            '<label class="full">Názov / účel<input id="op-e-title" required value="' +
            escapeHtml(op.title || '') + '" /></label>' +
            '<label>Začiatok<input id="op-e-start" type="datetime-local" required value="' +
            toLocalInput(op.plannedStart) + '" /></label>' +
            '<label>Koniec<input id="op-e-end" type="datetime-local" required value="' +
            toLocalInput(op.plannedEnd) + '" /></label>' +
            '<label class="full">Lokalita (adresa)<input id="op-e-addr" value="' + addrVal + '" /></label>' +
            '<label>Zem. šírka<input id="op-e-lat" value="' + (hasCoords ? lat : '') + '" /></label>' +
            '<label>Zem. dĺžka<input id="op-e-lon" value="' + (hasCoords ? lon : '') + '" /></label>' +
            '<label>Polomer (m)<input id="op-e-radius" type="number" min="1" value="' +
            Math.round(radiusM) + '" /></label>' +
            '<div class="full toolbar">' +
            '<button type="submit">Uložiť zmeny</button>' +
            '<button type="button" class="secondary" id="op-e-map">Vybrať na mape</button>' +
            '</div></form></div>';
        }

        content.innerHTML =
          '<div class="stack">' +
          pageHead(op.title || 'Žiadosť o let',
            (op.orgUnitName || '') + ' · ' + new Date(op.plannedStart).toLocaleString('sk-SK'),
            chipHtml('Stav', opStatusBadgeLabel(op), 'chip-soft') +
            (op.isUrgent ? chipHtml('Neodkladná', '', 'chip-danger') : '')) +
          '<div class="toolbar">' + actionBar + '</div>' +
          '<div id="op-detail-msg"></div>' +
          returnBlock +
          editBlock +
          '<div class="form-panel"><h3>Údaje žiadosti</h3><dl class="flight-meta">' +
          metaItem('Útvar', op.orgUnitName) +
          metaItem('Začiatok', new Date(op.plannedStart).toLocaleString('sk-SK')) +
          metaItem('Koniec', new Date(op.plannedEnd).toLocaleString('sk-SK')) +
          metaItem('Lokalita', op.locationAddress || gpsLabel) +
          metaItem('GPS', hasCoords ? gpsLabel : '—') +
          metaItem('Polomer', hasCoords ? (Math.round(radiusM) + ' m') : '—') +
          metaItem('Dron', uasRes) +
          metaItem('Pilot', pilotRes) +
          metaItem('Popis', detail.description || '—') +
          '</dl></div>' +
          '<div class="chart-panel"><h3 style="margin:0 0 0.65rem">Mapa plánovaného letu</h3>' +
          (hasCoords
            ? '<div id="op-detail-map"></div>'
            : '<div class="flight-map-empty">Táto žiadosť nemá GPS súradnice na mape.</div>') +
          '</div></div>';

        document.getElementById('op-back').onclick = function () {
          state.operationDetailId = null;
          state.operationEditing = false;
          if (window.IsSuasDroneMap) window.IsSuasDroneMap.destroyMap();
          navigate('operations');
        };

        async function detailDecide(action, reason) {
          var msg = document.getElementById('op-detail-msg');
          try {
            await api('/api/v1/operations/' + op.id + '/' + action, {
              method: 'POST',
              body: JSON.stringify({ reason: reason, rowVersion: Number(op.rowVersion), decisionCode: 0 })
            });
            state.operationDetailId = null;
            state.operationEditing = false;
            navigate('operations');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        }
        var apprBtn = document.getElementById('op-d-approve');
        if (apprBtn) {
          apprBtn.onclick = function () {
            if (!confirmAction('Schváliť túto žiadosť o let?')) return;
            detailDecide('approve', 'Schválené');
          };
        }
        var confirmFlownBtn = document.getElementById('op-d-confirm-flown');
        if (confirmFlownBtn) {
          confirmFlownBtn.onclick = async function () {
            if (!confirmAction('Potvrdiť, že tento let sa naozaj uskutočnil? Záznam sa pridá do denníka letov.')) return;
            var msg = document.getElementById('op-detail-msg');
            try {
              await api('/api/v1/operations/' + op.id + '/confirm-flown', {
                method: 'POST',
                body: JSON.stringify({ rowVersion: Number(op.rowVersion) })
              });
              state.operationDetailId = null;
              state.operationEditing = false;
              navigate('operations');
            } catch (err) {
              msg.innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }
        var retBtn = document.getElementById('op-d-return');
        if (retBtn) {
          retBtn.onclick = function () {
            var reason = window.prompt('Dôvod vrátenia:', 'Doplňte údaje');
            if (!reason) return;
            detailDecide('return', reason);
          };
        }
        var rejBtn = document.getElementById('op-d-reject');
        if (rejBtn) {
          rejBtn.onclick = function () {
            var reason = window.prompt('Dôvod zamietnutia:', 'Zamietnuté');
            if (!reason) return;
            detailDecide('reject', reason);
          };
        }
        var subBtn = document.getElementById('op-d-submit');
        if (subBtn) {
          subBtn.onclick = async function () {
            var msg = document.getElementById('op-detail-msg');
            try {
              await api('/api/v1/operations/' + op.id + '/submit', {
                method: 'POST',
                body: JSON.stringify({ rowVersion: Number(op.rowVersion) })
              });
              state.operationEditing = false;
              navigate('operations');
            } catch (err) {
              msg.innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }
        var editBtn = document.getElementById('op-d-edit');
        if (editBtn) {
          editBtn.onclick = function () {
            state.operationEditing = !state.operationEditing;
            if (!state.operationEditing) state.pendingFlightLocation = null;
            navigate('operations');
          };
        }
        var delBtn = document.getElementById('op-d-delete');
        if (delBtn) {
          delBtn.onclick = async function () {
            if (!confirmAction('Naozaj vymazať túto žiadosť?')) return;
            var msg = document.getElementById('op-detail-msg');
            try {
              await api('/api/v1/operations/' + op.id + '/delete', {
                method: 'POST',
                body: JSON.stringify({ rowVersion: Number(op.rowVersion) })
              });
              state.operationDetailId = null;
              state.operationEditing = false;
              navigate('operations');
            } catch (err) {
              msg.innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }

        var editForm = document.getElementById('op-edit-form');
        if (editForm) {
          document.getElementById('op-e-map').onclick = function () {
            state.opEditLocationPick = true;
            state.operationEditing = true;
            state.mapStartPick = true;
            var target = state.accessLevel === 3 ? 'geozones' : 'map';
            document.querySelectorAll('.nav').forEach(function (b) {
              b.classList.toggle('active', b.dataset.page === target);
            });
            navigate(target);
          };
          editForm.onsubmit = async function (ev) {
            ev.preventDefault();
            var msg = document.getElementById('op-detail-msg');
            try {
              var title = document.getElementById('op-e-title').value.trim();
              var startAt = new Date(document.getElementById('op-e-start').value);
              var endAt = new Date(document.getElementById('op-e-end').value);
              var addr = document.getElementById('op-e-addr').value.trim() || null;
              var latV = Number(document.getElementById('op-e-lat').value);
              var lonV = Number(document.getElementById('op-e-lon').value);
              var radV = Number(document.getElementById('op-e-radius').value);
              if (!title) throw new Error('Vyplňte názov');
              if (!(endAt > startAt)) throw new Error('Koniec musí byť po začiatku');
              var desc = detail.description || '';
              if (isFinite(radV) && radV > 0) {
                if (/Polomer oblasti:\s*\d+/i.test(desc)) {
                  desc = desc.replace(/Polomer oblasti:\s*\d+/i, 'Polomer oblasti: ' + Math.round(radV));
                } else {
                  desc = (desc ? desc + '\n' : '') + 'Polomer oblasti: ' + Math.round(radV) + ' m';
                }
              }
              await api('/api/v1/operations/' + op.id, {
                method: 'PATCH',
                body: JSON.stringify({
                  title: title,
                  operationTypeCode: op.operationTypeCode || 'FLIGHT_REQUEST',
                  plannedStart: startAt.toISOString(),
                  plannedEnd: endAt.toISOString(),
                  locationAddress: addr,
                  locationLat: isFinite(latV) ? latV : null,
                  locationLon: isFinite(lonV) ? lonV : null,
                  locationGeoJson: null,
                  description: desc || null,
                  rowVersion: Number(op.rowVersion)
                })
              });
              state.pendingFlightLocation = null;
              state.operationEditing = false;
              msg.innerHTML = '<div class="msg">Zmeny uložené.</div>';
              navigate('operations');
            } catch (err) {
              msg.innerHTML = '<div class="error">' + err.message + '</div>';
            }
          };
        }

        if (hasCoords && !state.operationEditing) {
          try {
            await ensureMapLibs();
            var map = window.IsSuasDroneMap.initMap('op-detail-map', {
              token: state.token,
              center: [lat, lon],
              zoom: 13
            });
            if (map && window.L) {
              var ll = [lat, lon];
              window.L.circle(ll, {
                radius: radiusM,
                color: '#0d8f7a',
                weight: 2,
                fillColor: '#0d8f7a',
                fillOpacity: 0.18
              }).addTo(map).bindPopup(op.locationAddress || gpsLabel);
              window.L.circleMarker(ll, {
                radius: 8,
                color: '#0d8f7a',
                fillColor: '#0d8f7a',
                fillOpacity: 0.9
              }).addTo(map).bindPopup(
                '<b>' + escapeHtml(op.title || 'Žiadosť') + '</b><br>' + gpsLabel +
                '<br>r = ' + Math.round(radiusM) + ' m'
              );
              map.setView(ll, 13);
            }
          } catch (mapErr) {
            var mapEl = document.getElementById('op-detail-map');
            if (mapEl) {
              mapEl.outerHTML = '<div class="flight-map-empty">Mapu sa nepodarilo načítať: ' +
                (mapErr.message || 'chyba') + '</div>';
            }
          }
        }
        return;
      }

      function opSortRank(o) {
        if (isSubmitted(o)) return 0;
        if (isDraftOrReturned(o)) return 1;
        if (isApproved(o)) return 2;
        if (isSettled(o)) return 3;
        return 2;
      }
      const items = (await api('/api/v1/operations') || []).slice().sort(function (a, b) {
        var ra = opSortRank(a), rb = opSortRank(b);
        if (ra !== rb) return ra - rb;
        return new Date(b.plannedStart).getTime() - new Date(a.plannedStart).getTime();
      });

      var pendingCount = items.filter(isSubmitted).length;

      const rows = items.map(function (o) {
        var actions = '<div class="row-actions">';
        if (canApprove && isSubmitted(o)) {
          actions +=
            '<button type="button" class="btn-ok op-approve" data-id="' + o.id +
            '" data-rv="' + o.rowVersion + '">Schváliť</button>' +
            '<button type="button" class="btn-warn op-return" data-id="' + o.id +
            '" data-rv="' + o.rowVersion + '">Vrátiť</button>' +
            '<button type="button" class="btn-danger op-reject" data-id="' + o.id +
            '" data-rv="' + o.rowVersion + '">Zamietnuť</button>';
        } else if (isDraftOrReturned(o)) {
          if (isL3) {
            actions +=
              '<button type="button" class="secondary op-submit" data-id="' + o.id +
              '" data-rv="' + o.rowVersion + '">Odoslať na schválenie</button>';
          }
          if (canApprove) {
            actions +=
              '<button type="button" class="btn-ok op-approve-direct" data-id="' + o.id +
              '" data-rv="' + o.rowVersion + '">Schváliť</button>' +
              '<button type="button" class="btn-danger op-reject-direct" data-id="' + o.id +
              '" data-rv="' + o.rowVersion + '">Zamietnuť</button>';
          }
        }
        if (canEditOwnReturned(o)) {
          actions +=
            '<button type="button" class="secondary op-edit" data-id="' + o.id + '">Upraviť</button>';
        }
        if (canDeleteOwn(o)) {
          actions +=
            '<button type="button" class="btn-danger op-delete" data-id="' + o.id +
            '" data-rv="' + o.rowVersion + '">Vymazať</button>';
        }
        if (canApprove && isApproved(o)) {
          actions +=
            '<button type="button" class="btn-ok op-confirm-flown" data-id="' + o.id +
            '" data-rv="' + o.rowVersion + '">Potvrdiť odlet</button>';
        }
        if (actions === '<div class="row-actions">') actions = '—';
        else actions += '</div>';

        var settledCls = '';
        var accent = colorForKey(o.orgUnitName || o.id);
        if (isCompleted(o)) { settledCls = ' op-done op-approved'; accent = '#6a9a78'; }
        else if (isApproved(o)) { settledCls = ' op-await-flight'; accent = '#5b8fad'; }
        else if (isRejected(o)) { settledCls = ' op-done op-rejected'; accent = '#b07070'; }
        else if (isCancelled(o)) { settledCls = ' op-done op-cancelled'; accent = '#94a3b8'; }
        var returnNote = '';
        if (isReturned(o) && o.decisionReason) {
          returnNote =
            '<div class="op-return-note"><strong>Dôvod vrátenia</strong>' +
            escapeHtml(o.decisionReason) + '</div>';
        }
        return '<tr class="row-tint op-row' + settledCls + '" data-id="' + o.id +
          '" style="--row-accent:' + accent + '" title="Otvoriť detail a mapu">' +
          '<td><span class="entity-label">' + swatchHtml(colorForKey(o.id || o.title)) +
          '<span>' + escapeHtml(o.title) + '</span></span>' + returnNote +
          '</td><td>' + statusBadge(isCompleted(o) ? 'Odletené' : (isApproved(o) ? 'Schválené' : opStatusName(o))) + '</td><td>' +
          entityLabelHtml(o.orgUnitName, o.orgUnitName) +
          '</td><td>' + new Date(o.plannedStart).toLocaleString('sk-SK') +
          '</td><td>' + (o.isUrgent ? statusBadge('Urgent') : 'Nie') +
          '</td><td>' + actions + '</td></tr>';
      }).join('') || '<tr><td colspan="6">Zatiaľ žiadne žiadosti o let</td></tr>';

      content.innerHTML = '<div class="stack">' + searchHighlightBanner() +
        pageHead('Žiadosti o let',
          'Nová žiadosť → schválenie L1/L2 → po skutočnom lete L1/L2 potvrdia odlet (denník + grafy).',
          chipHtml('Žiadosti', items.length, 'chip-soft') +
          chipHtml('Na schválenie', pendingCount, pendingCount ? 'chip-danger' : 'chip-ok')) +
        // Ziadost o let podava pilot (L3); L1/L2 ziadosti len schvaluju.
        (isL3 ? '<div class="toolbar"><button type="button" id="reveal-op-form">Nová žiadosť o let</button></div>' : '') +
        '<div id="op-msg"></div>' +
        makeTable(['Názov', 'Stav', 'Útvar', 'Začiatok', 'Neodkladná', 'Akcie'], rows, true) +
        '</div>';

      var newRequestBtn = document.getElementById('reveal-op-form');
      if (newRequestBtn) {
        newRequestBtn.onclick = function () {
          beginFlightRequestFlow();
        };
      }

      content.querySelectorAll('tr.op-row').forEach(function (row) {
        row.onclick = function (ev) {
          if (ev.target.closest && ev.target.closest('button, .row-actions, a')) return;
          state.operationDetailId = row.dataset.id;
          state.operationEditing = false;
          navigate('operations');
        };
      });

      async function decide(id, rv, action, reason) {
        var msg = document.getElementById('op-msg');
        try {
          await api('/api/v1/operations/' + id + '/' + action, {
            method: 'POST',
            body: JSON.stringify({ reason: reason, rowVersion: Number(rv), decisionCode: 0 })
          });
          msg.innerHTML = '<div class="msg">Hotovo: ' + action + '</div>';
          navigate('operations');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      }

      async function submitThen(id, rv, action, reason) {
        var msg = document.getElementById('op-msg');
        try {
          var submitted = await api('/api/v1/operations/' + id + '/submit', {
            method: 'POST',
            body: JSON.stringify({ rowVersion: Number(rv) })
          });
          await api('/api/v1/operations/' + id + '/' + action, {
            method: 'POST',
            body: JSON.stringify({
              reason: reason,
              rowVersion: Number(submitted.rowVersion),
              decisionCode: 0
            })
          });
          msg.innerHTML = '<div class="msg">Hotovo: ' + action + '</div>';
          navigate('operations');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      }

      document.querySelectorAll('.op-approve').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          if (!confirmAction('Schváliť túto žiadosť o let?')) return;
          decide(btn.dataset.id, btn.dataset.rv, 'approve', 'Schválené');
        };
      });
      document.querySelectorAll('.op-return').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          var reason = window.prompt('Dôvod vrátenia:', 'Doplňte údaje');
          if (!reason) return;
          decide(btn.dataset.id, btn.dataset.rv, 'return', reason);
        };
      });
      document.querySelectorAll('.op-reject').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          var reason = window.prompt('Dôvod zamietnutia:', 'Zamietnuté');
          if (!reason) return;
          decide(btn.dataset.id, btn.dataset.rv, 'reject', reason);
        };
      });
      document.querySelectorAll('.op-approve-direct').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          if (!confirmAction('Odoslať a schváliť túto žiadosť o let?')) return;
          submitThen(btn.dataset.id, btn.dataset.rv, 'approve', 'Schválené');
        };
      });
      document.querySelectorAll('.op-reject-direct').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          var reason = window.prompt('Dôvod zamietnutia:', 'Zamietnuté');
          if (!reason) return;
          submitThen(btn.dataset.id, btn.dataset.rv, 'reject', reason);
        };
      });
      document.querySelectorAll('.op-submit').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          var msg = document.getElementById('op-msg');
          try {
            await api('/api/v1/operations/' + btn.dataset.id + '/submit', {
              method: 'POST',
              body: JSON.stringify({ rowVersion: Number(btn.dataset.rv) })
            });
            msg.innerHTML = '<div class="msg">Žiadosť odoslaná na schválenie.</div>';
            navigate('operations');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      document.querySelectorAll('.op-edit').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          state.operationDetailId = btn.dataset.id;
          state.operationEditing = true;
          navigate('operations');
        };
      });
      document.querySelectorAll('.op-delete').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          if (!confirmAction('Naozaj vymazať túto žiadosť?')) return;
          var msg = document.getElementById('op-msg');
          try {
            await api('/api/v1/operations/' + btn.dataset.id + '/delete', {
              method: 'POST',
              body: JSON.stringify({ rowVersion: Number(btn.dataset.rv) })
            });
            navigate('operations');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      document.querySelectorAll('.op-confirm-flown').forEach(function (btn) {
        btn.onclick = async function (ev) {
          ev.stopPropagation();
          if (!confirmAction('Potvrdiť, že tento let sa naozaj uskutočnil? Záznam sa pridá do denníka letov.')) return;
          var msg = document.getElementById('op-msg');
          try {
            await api('/api/v1/operations/' + btn.dataset.id + '/confirm-flown', {
              method: 'POST',
              body: JSON.stringify({ rowVersion: Number(btn.dataset.rv) })
            });
            msg.innerHTML = '<div class="msg">Odlet potvrdený — záznam je v denníku letov a v grafoch prehľadu.</div>';
            navigate('operations');
          } catch (err) {
            msg.innerHTML = '<div class="error">' + err.message + '</div>';
          }
        };
      });
      return;
}
