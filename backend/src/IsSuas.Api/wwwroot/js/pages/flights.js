import { api } from '../core/api.js';
import { colorForKey, entityLabelHtml, swatchHtml } from '../core/charts.js';
import { ensureMapLibs } from '../core/maps.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { chipHtml, content, formatFlightGps, makeTable, pageHead, sectionCard, statusBadge, statusLabelSk, enumName } from '../core/ui.js';
import { submitFlightRequestFromForm } from '../shared/flightRequest.js';
import { loadUasOptions, uasSelectHtml } from '../shared/uas.js';

export async function renderFlights() {
      if (state.flightDetailId) {
        let detail;
        try {
          detail = await api('/api/v1/flights/' + state.flightDetailId);
        } catch (err) {
          state.flightDetailId = null;
          content.innerHTML = '<div class="error">' + (err.message || 'Let sa nenasiel') +
            '</div><div class="toolbar"><button type="button" class="secondary" id="fl-back">Späť</button></div>';
          document.getElementById('fl-back').onclick = function () { navigate('flights'); };
          return;
        }
        const f = detail.flight;

        const imports = detail.telemetryImports || [];
        function fmtDt(iso) {
          if (!iso) return '—';
          return new Date(iso).toLocaleString('sk-SK');
        }
        function fmtDur(m) {
          if (m == null) return '—';
          return Math.floor(m / 60) + 'h ' + (m % 60) + 'm';
        }
        function metaItem(label, val) {
          return '<div><dt>' + label + '</dt><dd>' + (val || '—') + '</dd></div>';
        }
        var hasCoords = f.locationLat != null && f.locationLon != null &&
          isFinite(Number(f.locationLat)) && isFinite(Number(f.locationLon));
        var telemRows = imports.map(function (t) {
          return '<tr><td>' + (t.fileName || '—') + '</td><td>' + statusBadge(enumName('telemetry', t.status)) +
            '</td><td>' + fmtDt(t.receivedAt) + '</td><td>' + (t.processingMessage || '—') + '</td></tr>';
        }).join('') || '<tr><td colspan="4">Bez importov telemetrie</td></tr>';
        var notesBlock = '';
        if (detail.postFlightNotes || detail.checklistResult || detail.deviationNotes) {
          notesBlock =
            '<div class="form-panel"><h3>Poznamky po lete</h3><dl class="flight-meta">' +
            metaItem('Checklist', detail.checklistResult) +
            metaItem('Poznamky', detail.postFlightNotes) +
            metaItem('Odchylky', detail.deviationNotes) +
            '</dl></div>';
        }
        content.innerHTML =
          '<div class="stack">' +
          pageHead('Detail letu', (f.flightNumber || String(f.id).substring(0, 8)) +
            (f.purpose ? ' · ' + f.purpose : ''),
            chipHtml('Stav', statusLabelSk(f.status), 'chip-soft') +
            chipHtml('Útvar', f.orgUnitName || '—')) +
          '<div class="toolbar">' +
          '<button type="button" class="secondary" id="fl-back">Späť na dennik</button>' +
          '</div>' +
          '<div class="form-panel"><h3>Zakladne udaje</h3><dl class="flight-meta">' +
          metaItem('Cislo letu', f.flightNumber) +
          metaItem('Dron', f.uasLabel) +
          metaItem('Pilot', f.pilotName) +
          metaItem('Útvar', f.orgUnitName) +
          metaItem('Vzlet', fmtDt(f.takeoffAt)) +
          metaItem('Pristatie', fmtDt(f.landingAt)) +
          metaItem('Trvanie', fmtDur(f.durationMinutes)) +
          metaItem('Účel', f.purpose || f.standaloneReason) +
          metaItem('Lokalita (GPS)', formatFlightGps(f) || '—') +
          metaItem('Polomer', f.locationRadiusMeters != null
            ? Math.round(Number(f.locationRadiusMeters)) + ' m' : null) +
          metaItem('Vzdialenost', f.distanceMeters != null
            ? Math.round(Number(f.distanceMeters)) + ' m' : null) +
          metaItem('Max. vyska', f.maxAltitudeMeters != null
            ? Math.round(Number(f.maxAltitudeMeters)) + ' m' : null) +
          '</dl></div>' +
          '<div class="chart-panel"><h3 style="margin:0 0 0.65rem">Mapa letu</h3>' +
          (hasCoords
            ? '<div id="flight-detail-map"></div>'
            : '<div class="flight-map-empty">Tento let nema suradnice na mape.</div>') +
          '</div>' +
          notesBlock +
          sectionCard('Telemetria',
            makeTable(['Subor', 'Stav', 'Prijate', 'Sprava'], telemRows, true), '#0d8f7a') +
          '</div>';

        document.getElementById('fl-back').onclick = function () {
          state.flightDetailId = null;
          if (window.IsSuasDroneMap) window.IsSuasDroneMap.destroyMap();
          navigate('flights');
        };
        if (hasCoords) {
          try {
            await ensureMapLibs();
            var ll = [Number(f.locationLat), Number(f.locationLon)];
            var radius = Number(f.locationRadiusMeters) || 1000;
            var map = window.IsSuasDroneMap.initMap('flight-detail-map', {
              token: state.token,
              center: ll,
              zoom: 13
            });
            if (map && window.L) {
              window.L.circle(ll, {
                radius: radius,
                color: '#0d8f7a',
                weight: 2,
                fillColor: '#0d8f7a',
                fillOpacity: 0.18
              }).addTo(map).bindPopup(formatFlightGps(f) || f.flightNumber || 'Oblast letu');
              window.L.circleMarker(ll, {
                radius: 8,
                color: '#0d8f7a',
                fillColor: '#0d8f7a',
                fillOpacity: 0.9
              }).addTo(map).bindPopup(
                '<b>' + (f.flightNumber || 'Let') + '</b><br>' +
                (f.purpose || '') +
                (formatFlightGps(f) ? '<br>' + formatFlightGps(f) : '')
              );
              map.setView(ll, 13);
              setTimeout(function () { map.invalidateSize(); }, 80);
            }
          } catch (_) { /* mapa volitelna */ }
        }
        return;
      }

      const items = await api('/api/v1/flights');
      let orgs = [];
      try { orgs = await api('/api/v1/org-units'); } catch (_) { orgs = []; }
      const uasList = await loadUasOptions(200);
      let pilots = [];
      try { pilots = await api('/api/v1/pilots'); } catch (_) { pilots = []; }

      // L3: predvyplň vlastný pilotný profil (podľa personId / mena účtu)
      var myPilot = null;
      if (state.accessLevel === 3) {
        var pid = String(state.personId || '').toLowerCase();
        var dn = String(state.displayName || '').trim().toLowerCase().replace(/\s+/g, ' ');
        var un = String(state.userName || '').trim().toLowerCase();
        myPilot = pilots.find(function (p) {
          return pid && String(p.personId || '').toLowerCase() === pid;
        }) || pilots.find(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim().toLowerCase().replace(/\s+/g, ' ');
          return dn && name === dn;
        }) || pilots.find(function (p) {
          var email = String(p.email || '').toLowerCase();
          var lic = String(p.licenseNumber || '').toLowerCase();
          return (un && (email.indexOf(un) >= 0 || lic.indexOf(un) >= 0));
        }) || null;
      }

      // Domovský útvar = najhlbší v dostupnom strome
      var homeOrg = orgs.slice().sort(function (a, b) {
        return (b.level || 0) - (a.level || 0);
      })[0] || null;
      var isL3 = state.accessLevel === 3;

      const orgOpts = orgs.map(function (o) {
        return '<option value="' + o.id + '">' + o.code + ' — ' + o.name + '</option>';
      }).join('') || '<option value="">(ziadne utvary)</option>';
      const pilotOpts = isL3 && myPilot
        ? '<option value="' + myPilot.id + '" selected>' +
          (((myPilot.firstName || '') + ' ' + (myPilot.lastName || '')).trim() || myPilot.licenseNumber) +
          (myPilot.licenseNumber ? ' (' + myPilot.licenseNumber + ')' : '') + '</option>'
        : '<option value="">— bez pilota —</option>' + pilots.slice(0, 250).map(function (p) {
          var name = ((p.firstName || '') + ' ' + (p.lastName || '')).trim() || p.licenseNumber || p.id;
          return '<option value="' + p.id + '">' + name +
            (p.licenseNumber ? ' (' + p.licenseNumber + ')' : '') + '</option>';
        }).join('');
      const uasOpts = '<option value="">— bez dronu —</option>' + uasSelectHtml(uasList);

      const rows = items.map(function (f) {
        var dateStr = f.takeoffAt
          ? new Date(f.takeoffAt).toLocaleDateString('sk-SK')
          : '—';
        var timeStr = f.takeoffAt
          ? new Date(f.takeoffAt).toLocaleTimeString('sk-SK', { hour: '2-digit', minute: '2-digit' })
          : '—';
        var dur = f.durationMinutes != null
          ? (Math.floor(f.durationMinutes / 60) + 'h ' + (f.durationMinutes % 60) + 'm')
          : '—';
        return '<tr class="row-tint flight-row" data-id="' + f.id + '" style="--row-accent:' +
          colorForKey(f.uasLabel || f.flightNumber || f.id) + '" title="Otvoriť detail letu">' +
          '<td><button type="button" class="linkish flight-open" data-id="' + f.id + '">' +
          '<span class="entity-label">' + swatchHtml(colorForKey(f.flightNumber || f.id)) +
          '<b>' + (f.flightNumber || String(f.id).substring(0, 8)) + '</b></span></button></td>' +
          '<td>' + dateStr + '</td>' +
          '<td>' + timeStr + '</td>' +
          '<td>' + (f.uasLabel ? entityLabelHtml(f.uasLabel, f.uasLabel) : '—') + '</td>' +
          '<td>' + (f.pilotName || '—') + '</td>' +
          '<td>' + dur + '</td>' +
          '<td>' + (formatFlightGps(f) || '—') + '</td>' +
          '<td>' + (f.purpose || '—') + '</td>' +
          '<td>' + statusBadge(enumName('flight', f.status)) + '</td>' +
          '</tr>';
      }).join('') || '<tr><td colspan="9">Zatial ziadne lety v denniku</td></tr>';

      var nowLocal = new Date();
      var hourAgo = new Date(Date.now() - 3600000);
      function toLocalInput(d) {
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) +
          'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
      }

      var orgFieldHtml = isL3 && homeOrg
        ? '<input type="hidden" id="fl-org" value="' + homeOrg.id + '" />' +
          '<label>Útvar<input value="' +
          String(homeOrg.code + ' — ' + homeOrg.name).replace(/"/g, '&quot;') +
          '" readonly /></label>'
        : '<label>Útvar<select id="fl-org" required>' + orgOpts + '</select></label>';

      var pilotFieldHtml = isL3 && myPilot
        ? '<input type="hidden" id="fl-pilot" value="' + myPilot.id + '" />' +
          '<label>Pilot<input value="' +
          String((((myPilot.firstName || '') + ' ' + (myPilot.lastName || '')).trim() || myPilot.licenseNumber || '')).replace(/"/g, '&quot;') +
          '" readonly /></label>'
        : '<label>Pilot<select id="fl-pilot">' + pilotOpts + '</select></label>';

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Denník letov', isL3
          ? 'Váš osobný denník. Nový let začnite výberom miesta na mape GEO zón.'
          : 'Kliknite na let pre detail, mapu a trasu',
          chipHtml('Lety', items.length, 'chip-soft') +
          (state.licenseStatus === 'Pending' || state.licenseStatus === '1'
            ? chipHtml('Licencia', 'čaká na potvrdenie', 'chip-danger')
            : (state.licenseStatus === 'Confirmed' || state.licenseStatus === '2'
              ? chipHtml('Licencia', 'potvrdená', 'chip-ok') : ''))) +
        '<div class="chart-panel">' +
        '<div class="toolbar" style="margin:0">' +
        '<button type="button" id="reveal-flight-form">+ Nový samostatný let</button> ' +
        (isL3 ? '' : '<button type="button" class="secondary" id="fl-pick-map">Vybrať miesto na mape</button> ') +
        (isL3 ? '' : '<button type="button" class="secondary" id="import-telem">Import demo telemetrie</button>') +
        '</div>' +
        '<div id="flight-msg"></div>' +
        '<div id="flight-form-panel" class="form-panel hidden">' +
        '<h3>' + (state.flightRequestMode ? 'Nová žiadosť o let' : 'Nový let do denníka') + '</h3>' +
        '<form id="flight-create-form" class="form-grid">' +
        orgFieldHtml +
        '<label>Dron<select id="fl-uas">' + uasOpts + '</select></label>' +
        pilotFieldHtml +
        '<label class="full muted" style="align-self:end">Číslo letu sa priradí automaticky (LET-' + new Date().getFullYear() + '-XXXX)</label>' +
        '<label class="full">GPS súradnice' +
        '<input id="fl-location" readonly placeholder="Vyberte bod na mape (lat, lon)" />' +
        '</label>' +
        '<input type="hidden" id="fl-lat" value="" />' +
        '<input type="hidden" id="fl-lon" value="" />' +
        '<input type="hidden" id="fl-radius" value="" />' +
        '<p class="full muted" id="fl-map-hint" style="margin:0">' +
        (isL3
          ? 'GPS sa nastaví klikom na mape GEO zón (otvorí sa pred formulárom).'
          : 'GPS nastavíte klikom na mape (polomer napr. 1 km).') +
        '</p>' +
        '<label class="full">Účel<input id="fl-purpose" required placeholder="napr. Hliadkovanie" /></label>' +
        '<label>Vzlet<input id="fl-takeoff" type="datetime-local" required value="' + toLocalInput(hourAgo) + '" /></label>' +
        '<label>Pristátie<input id="fl-landing" type="datetime-local" required value="' + toLocalInput(nowLocal) + '" /></label>' +
        '<div class="full toolbar">' +
        '<button type="submit">' + (state.flightRequestMode ? 'Odoslať na schválenie' : 'Uložiť let') + '</button>' +
        '<button type="button" class="secondary" id="fl-cancel">Zrušiť</button>' +
        '<button type="button" class="secondary" id="fl-clear-loc">Zrušiť bod mapy</button>' +
        '</div></form></div>' +
        makeTable(['Číslo letu', 'Dátum', 'Čas', 'Dron', 'Pilot', 'Trvanie', 'GPS', 'Účel', 'Stav'], rows, true) +
        '</div></div>';

      if (!isL3) {
        var defaultOrg = orgs.find(function (o) { return o.code === 'UTVAR-A'; })
          || orgs.find(function (o) { return o.code && o.code.indexOf('KR-') === 0; })
          || orgs[0];
        if (defaultOrg) document.getElementById('fl-org').value = defaultOrg.id;
      }

      function applyPendingLocation() {
        var loc = state.pendingFlightLocation;
        var input = document.getElementById('fl-location');
        var hint = document.getElementById('fl-map-hint');
        if (loc) {
          var gps = Number(loc.lat).toFixed(5) + ', ' + Number(loc.lon).toFixed(5);
          input.value = gps;
          document.getElementById('fl-lat').value = String(loc.lat);
          document.getElementById('fl-lon').value = String(loc.lon);
          document.getElementById('fl-radius').value = String(loc.radiusM);
          hint.textContent = 'GPS: ' + gps +
            (loc.radiusM != null ? ' · polomer ' + Math.round(Number(loc.radiusM)) + ' m' : '');
        } else {
          input.value = '';
          document.getElementById('fl-lat').value = '';
          document.getElementById('fl-lon').value = '';
          document.getElementById('fl-radius').value = '';
          hint.textContent = isL3
            ? 'GPS sa nastaví klikom na mape GEO zón (otvorí sa pred formulárom).'
            : 'GPS nastavíte klikom na mape (polomer napr. 1 km).';
        }
      }
      applyPendingLocation();

      if (state.openFlightForm || state.pendingFlightLocation) {
        document.getElementById('flight-form-panel').classList.remove('hidden');
        state.openFlightForm = false;
      }

      function goPickFlightMap() {
        state.mapStartPick = true;
        state.openFlightForm = true;
        var target = isL3 ? 'geozones' : 'map';
        document.querySelectorAll('.nav').forEach(function (b) {
          b.classList.toggle('active', b.dataset.page === target);
        });
        navigate(target);
      }

      document.getElementById('reveal-flight-form').onclick = function () {
        if (isL3) {
          // L3: najprv mapa → po výbere bodu sa otvorí formulár
          goPickFlightMap();
          return;
        }
        document.getElementById('flight-form-panel').classList.remove('hidden');
      };
      var pickMapBtn = document.getElementById('fl-pick-map');
      if (pickMapBtn) pickMapBtn.onclick = goPickFlightMap;
      document.getElementById('fl-cancel').onclick = function () {
        document.getElementById('flight-form-panel').classList.add('hidden');
        state.flightRequestMode = false;
      };
      document.getElementById('fl-clear-loc').onclick = function () {
        state.pendingFlightLocation = null;
        applyPendingLocation();
      };

      document.getElementById('flight-create-form').onsubmit = async function (ev) {
        ev.preventDefault();
        const msg = document.getElementById('flight-msg');
        msg.innerHTML = '<div class="msg">Ukladam...</div>';
        try {
          if (state.flightRequestMode) {
            await submitFlightRequestFromForm();
            return;
          }
          var orgId = document.getElementById('fl-org').value;
          var uasId = document.getElementById('fl-uas').value || null;
          var pilotId = document.getElementById('fl-pilot').value || null;
          var purpose = document.getElementById('fl-purpose').value.trim();
          var latRaw = document.getElementById('fl-lat').value;
          var lonRaw = document.getElementById('fl-lon').value;
          var radiusRaw = document.getElementById('fl-radius').value;
          var lat = latRaw !== '' ? Number(latRaw) : null;
          var lon = lonRaw !== '' ? Number(lonRaw) : null;
          var radiusM = radiusRaw !== '' ? Number(radiusRaw) : null;
          var location = (isFinite(lat) && isFinite(lon))
            ? (lat.toFixed(5) + ', ' + lon.toFixed(5))
            : null;
          var takeoff = new Date(document.getElementById('fl-takeoff').value);
          var landing = new Date(document.getElementById('fl-landing').value);
          if (!orgId) throw new Error('Vyberte útvar');
          if (!purpose) throw new Error('Vyplnte ucel letu');
          if (lat == null || lon == null || !isFinite(lat) || !isFinite(lon))
            throw new Error('Najprv vyberte miesto letu na mape');
          if (!(landing > takeoff)) throw new Error('Pristatie musi byt po vzlete');

          var created = await api('/api/v1/flights/standalone', {
            method: 'POST',
            body: JSON.stringify({
              orgUnitId: orgId,
              standaloneReason: purpose,
              location: location || null,
              purpose: purpose,
              locationLat: lat,
              locationLon: lon,
              locationRadiusMeters: radiusM || 1000,
              uasId: uasId,
              pilotId: pilotId,
              batteryId: null,
              takeoffAt: takeoff.toISOString(),
              landingAt: landing.toISOString()
            })
          });

          await api('/api/v1/flights/' + created.id + '/close', {
            method: 'POST',
            body: JSON.stringify({
              postFlightNotes: purpose,
              checklistResult: 'OK',
              deviationNotes: null,
              rowVersion: created.rowVersion
            })
          });

          state.pendingFlightLocation = null;
          msg.innerHTML = '<div class="msg">Let ' + (created.flightNumber || '') + ' bol pridany do dennika.</div>';
          navigate('flights');
        } catch (err) {
          msg.innerHTML = '<div class="error">' + (err.message || 'Ulozenie zlyhalo') + '</div>';
        }
      };

      var importTelemBtn = document.getElementById('import-telem');
      if (importTelemBtn) importTelemBtn.onclick = async function () {
        const msg = document.getElementById('flight-msg');
        try {
          const flights = await api('/api/v1/flights');
          if (!flights.length) throw new Error('Najprv vytvorte let');
          const flightId = flights[0].id;
          const payload = JSON.stringify({
            points: [
              { lat: 48.14, lon: 17.10, alt: 100, time: new Date(Date.now() - 600000).toISOString() },
              { lat: 48.141, lon: 17.102, alt: 110, time: new Date(Date.now() - 300000).toISOString() },
              { lat: 48.142, lon: 17.104, alt: 105, time: new Date().toISOString() }
            ]
          });
          const res = await api('/api/v1/telemetry-imports', {
            method: 'POST',
            body: JSON.stringify({
              fileName: 'demo-track.json',
              format: 'json',
              payload: payload,
              flightId: flightId,
              uasId: null,
              operationId: null,
              pilotId: null,
              batteryId: null
            })
          });
          msg.innerHTML = '<div class="msg">Import: ' + statusLabelSk(res.status) +
            ' (kontrolný súčet ' + res.contentHash.substring(0, 8) + '...)</div>';
        } catch (err) {
          msg.innerHTML = '<div class="error">' + err.message + '</div>';
        }
      };

      function openFlightDetail(id) {
        if (!id) return;
        state.flightDetailId = id;
        navigate('flights');
      }
      content.querySelectorAll('.flight-row').forEach(function (row) {
        row.onclick = function (ev) {
          if (ev.target.closest('button') && !ev.target.closest('.flight-open')) return;
          openFlightDetail(row.dataset.id);
        };
      });
      content.querySelectorAll('.flight-open').forEach(function (btn) {
        btn.onclick = function (ev) {
          ev.stopPropagation();
          openFlightDetail(btn.dataset.id);
        };
      });
      return;
}
