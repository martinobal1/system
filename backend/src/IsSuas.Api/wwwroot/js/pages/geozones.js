import { api } from '../core/api.js';
import { ensureMapLibs } from '../core/maps.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { content } from '../core/ui.js';

export async function renderGeozones() {
      var mapPayload = null;
      try {
        mapPayload = await api('/api/v1/geo-zones/map');
      } catch (err) {
        content.innerHTML = '<div class="stack"><div class="error">' + (err.message || 'Nepodarilo sa nacitat GEO zony') + '</div></div>';
        return;
      }
      var versionLabel = mapPayload.dataVersionDate
        ? new Date(mapPayload.dataVersionDate).toLocaleDateString('sk-SK')
        : '—';
      var importedLabel = mapPayload.importedAt
        ? new Date(mapPayload.importedAt).toLocaleString('sk-SK')
        : '—';
      var canPickFlight = state.accessLevel === 3 || !!state.mapStartPick;
      var startPick = !!state.mapStartPick;
      state.mapStartPick = false;
      content.innerHTML =
        '<div class="map-shell">' +
        '<div class="map-toolbar">' +
        '<h2>GEO zony · Dopravny urad SR</h2>' +
        '<form class="map-search" id="geo-search-form">' +
        '<input type="search" id="geo-search-q" placeholder="Hladat obec / lokalitu (SK)…" />' +
        '<button type="submit">Hladat</button></form>' +
        (canPickFlight
          ? '<button type="button" class="secondary" id="map-pick-toggle">Označit miesto letu</button>'
          : '') +
        '<button type="button" class="secondary" id="geo-home">Slovensko</button>' +
        '<label class="geo-layer-toggle"><input type="checkbox" id="geo-layer-toggle" checked /> GEO zony</label>' +
        '<span class="map-coords" id="geo-version">Verzia: ' + versionLabel +
        ' · ' + (mapPayload.zoneCount || 0) + ' zon · import ' + importedLabel + '</span>' +
        '</div>' +
        (canPickFlight
          ? '<div class="map-pick-bar" id="map-pick-bar">' +
            '<label>Polomer' +
            '<input type="range" id="map-flight-radius" min="100" max="5000" step="100" value="' +
            ((state.pendingFlightLocation && state.pendingFlightLocation.radiusM) || 1000) + '" />' +
            '<span id="map-radius-label">1000 m</span></label>' +
            '<span class="map-pick-info" id="map-pick-info">Ziadny bod — zapnite „Označit miesto letu“ a kliknite na mapu</span>' +
            '<button type="button" id="map-use-flight" disabled>Pouzit v denniku letov</button>' +
            '<button type="button" class="secondary" id="map-clear-flight">Zrusit bod</button>' +
            '</div>'
          : '') +
        '<div class="map-body geo-map-body">' +
        '<aside class="map-legend geo-detail" id="geo-detail">' +
        '<p class="hint">' + (canPickFlight
          ? 'Zapnite „Označit miesto letu“ a kliknite na mapu (aj do GEO zóny).'
          : 'Kliknite na zonu pre detail.') + '</p></aside>' +
        '<div class="map-canvas-wrap">' +
        '<div id="geo-zones-map"></div>' +
        '<div id="map-fallback" class="map-fallback hidden"></div>' +
        '</div></div>' +
        '<p class="geo-disclaimer">' + (mapPayload.disclaimerSk || '') + '</p>' +
        '</div>';

      function syncGeoPickUi(area) {
        var info = document.getElementById('map-pick-info');
        var useBtn = document.getElementById('map-use-flight');
        var radiusLabel = document.getElementById('map-radius-label');
        var radiusEl = document.getElementById('map-flight-radius');
        if (!info || !useBtn) return;
        if (radiusEl && radiusLabel)
          radiusLabel.textContent = Math.round(Number(radiusEl.value)) + ' m';
        if (!area) {
          info.textContent = 'Ziadny bod — zapnite „Označit miesto letu“ a kliknite na mapu';
          useBtn.disabled = true;
          return;
        }
        info.textContent = area.label;
        useBtn.disabled = false;
        state.pendingFlightLocation = {
          lat: area.lat,
          lon: area.lon,
          radiusM: area.radiusM,
          label: area.label
        };
      }

      await ensureMapLibs();
      if (!window.IsSuasDroneMap || !window.L) {
        content.innerHTML = '<div class="stack"><div class="error">Mapovy modul sa nenačítal.</div></div>';
        return;
      }

      var map = window.IsSuasDroneMap.initMap('geo-zones-map', {
        token: state.token,
        center: [48.7, 19.5],
        zoom: 7,
        pickMode: canPickFlight && startPick,
        initialFlightArea: canPickFlight ? state.pendingFlightLocation : null,
        onFlightAreaChange: canPickFlight ? syncGeoPickUi : null,
        onError: function (msg) {
          var el = document.getElementById('map-fallback');
          if (el) { el.textContent = msg; el.classList.remove('hidden'); }
        }
      });

      // Simplified Slovakia border (approx.)
      var skBorder = {
        type: 'Feature',
        properties: { name: 'Slovensko' },
        geometry: {
          type: 'Polygon',
          coordinates: [[
            [16.833, 48.0], [17.15, 48.58], [18.85, 49.4], [19.7, 49.5],
            [21.3, 49.55], [22.55, 49.08], [22.15, 48.4], [21.2, 48.4],
            [20.2, 48.1], [18.85, 47.75], [17.85, 47.75], [17.1, 47.85],
            [16.833, 48.0]
          ]]
        }
      };
      window.L.geoJSON(skBorder, {
        style: { color: '#334155', weight: 2, fill: false, dashArray: '6 4' },
        interactive: false
      }).addTo(map);

      function zoneColor(type) {
        var t = String(type || '').toLowerCase();
        if (t.indexOf('zakaz') >= 0 || t.indexOf('prohibit') >= 0 || t.indexOf('no') >= 0) return '#b42318';
        if (t.indexOf('obmedz') >= 0 || t.indexOf('restrict') >= 0) return '#c47b1a';
        if (t.indexOf('podmien') >= 0 || t.indexOf('condit') >= 0) return '#2b7bb9';
        return '#0d8f7a';
      }

      function showZoneDetail(props) {
        props = props || {};
        function row(label, val) {
          if (val == null || val === '') return '';
          return '<div class="geo-prop"><dt>' + label + '</dt><dd>' + String(val) + '</dd></div>';
        }
        var html = '<h3>' + (props.name || 'GEO zona') + '</h3><dl class="geo-props">' +
          row('Typ', props.zoneType) +
          row('Obmedzenie', props.restrictionDescription) +
          row('Podmienky letu', props.flightConditions) +
          row('Horizontalne hranice', props.horizontalLimits) +
          row('Vyskove obmedzenie', props.verticalLimits) +
          row('Platnost od', props.validFrom ? new Date(props.validFrom).toLocaleString('sk-SK') : null) +
          row('Platnost do', props.validTo ? new Date(props.validTo).toLocaleString('sk-SK') : null) +
          row('Kontakt / urad', props.authorityContact) +
          row('Zdroj', props.sourceLabel || 'Dopravny urad SR') +
          row('Datum importu', props.importDate ? new Date(props.importDate).toLocaleString('sk-SK') : importedLabel) +
          row('Verzia udajov', props.dataVersionDate || versionLabel) +
          '</dl>';
        document.getElementById('geo-detail').innerHTML = html;
      }

      function isPickActive() {
        var btn = document.getElementById('map-pick-toggle');
        return !!(btn && btn.classList.contains('active-pick'));
      }

      var geoLayer = null;
      try {
        var fc = typeof mapPayload.featureCollectionGeoJson === 'string'
          ? JSON.parse(mapPayload.featureCollectionGeoJson)
          : mapPayload.featureCollectionGeoJson;
        geoLayer = window.L.geoJSON(fc, {
          style: function (feature) {
            var c = zoneColor(feature && feature.properties && feature.properties.zoneType);
            return { color: c, weight: 2, fillColor: c, fillOpacity: 0.28 };
          },
          pointToLayer: function (feature, latlng) {
            return window.L.circleMarker(latlng, {
              radius: 7,
              color: zoneColor(feature.properties && feature.properties.zoneType),
              fillOpacity: 0.85
            });
          },
          onEachFeature: function (feature, layer) {
            layer.on('click', function (e) {
              if (canPickFlight && isPickActive() && e && e.latlng) {
                window.IsSuasDroneMap.setFlightArea(e.latlng.lat, e.latlng.lng);
                if (window.L && window.L.DomEvent) window.L.DomEvent.stopPropagation(e);
                return;
              }
              showZoneDetail(feature.properties || {});
              try { map.fitBounds(layer.getBounds(), { padding: [40, 40], maxZoom: 12 }); } catch (_) {}
            });
            var tip = (feature.properties && feature.properties.name) || 'GEO zona';
            layer.bindTooltip(tip, { sticky: true });
          }
        }).addTo(map);
        if (geoLayer.getLayers().length) {
          try { map.fitBounds(geoLayer.getBounds(), { padding: [30, 30], maxZoom: 10 }); } catch (_) {}
        }
      } catch (err) {
        document.getElementById('geo-detail').innerHTML =
          '<div class="error">Nepodarilo sa vykreslit GEO zony: ' + (err.message || err) + '</div>';
      }

      document.getElementById('geo-layer-toggle').onchange = function () {
        if (!geoLayer || !map) return;
        if (this.checked) {
          if (!map.hasLayer(geoLayer)) geoLayer.addTo(map);
        } else if (map.hasLayer(geoLayer)) {
          map.removeLayer(geoLayer);
        }
      };
      document.getElementById('geo-home').onclick = function () {
        window.IsSuasDroneMap.goHome();
      };
      document.getElementById('geo-search-form').onsubmit = function (ev) {
        ev.preventDefault();
        window.IsSuasDroneMap.searchPlace(document.getElementById('geo-search-q').value);
      };

      if (canPickFlight) {
        var radiusEl = document.getElementById('map-flight-radius');
        if (radiusEl) {
          radiusEl.oninput = function () {
            document.getElementById('map-radius-label').textContent = Math.round(Number(radiusEl.value)) + ' m';
            window.IsSuasDroneMap.setFlightRadius(Number(radiusEl.value));
          };
          document.getElementById('map-radius-label').textContent = Math.round(Number(radiusEl.value)) + ' m';
        }
        var pickToggle = document.getElementById('map-pick-toggle');
        if (pickToggle) {
          pickToggle.onclick = function () {
            var next = !pickToggle.classList.contains('active-pick');
            window.IsSuasDroneMap.setPickMode(next);
          };
        }
        if (startPick) window.IsSuasDroneMap.setPickMode(true);
        document.getElementById('map-use-flight').onclick = function () {
          var area = window.IsSuasDroneMap.getFlightArea();
          if (!area) return;
          state.pendingFlightLocation = {
            lat: area.lat, lon: area.lon, radiusM: area.radiusM, label: area.label
          };
          if (state.opEditLocationPick) {
            state.opEditLocationPick = false;
            state.operationEditing = true;
            document.querySelectorAll('.nav').forEach(function (b) {
              b.classList.toggle('active', b.dataset.page === 'operations');
            });
            navigate('operations');
            return;
          }
          state.openFlightForm = true;
          state.flightRequestMode = true;
          navigate('flights');
        };
        document.getElementById('map-clear-flight').onclick = function () {
          window.IsSuasDroneMap.clearFlightArea();
          state.pendingFlightLocation = null;
          syncGeoPickUi(null);
        };
        if (state.pendingFlightLocation) syncGeoPickUi({
          lat: state.pendingFlightLocation.lat,
          lon: state.pendingFlightLocation.lon,
          radiusM: state.pendingFlightLocation.radiusM,
          label: state.pendingFlightLocation.label
        });
      }

      setTimeout(function () { if (map) map.invalidateSize(); }, 80);
      return;
}
