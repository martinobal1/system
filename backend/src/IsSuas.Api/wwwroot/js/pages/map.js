import { ensureMapLibs } from '../core/maps.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { content } from '../core/ui.js';

export async function renderMap() {
      var startPick = !!state.mapStartPick;
      state.mapStartPick = false;
      content.innerHTML =
        '<div class="map-shell">' +
        '<div class="map-toolbar">' +
        '<h2>VFR Manual · Slovensko</h2>' +
        '<form class="map-search" id="map-search-form">' +
        '<input type="search" id="map-search-q" placeholder="Hladat adresu / miesto (SK)…" />' +
        '<button type="submit">Hladat</button>' +
        '</form>' +
        '<button type="button" class="secondary" id="map-pick-toggle">Označit miesto letu</button>' +
        '<button type="button" class="secondary" id="map-locate">Moja poloha</button>' +
        '<button type="button" class="secondary" id="map-home">Slovensko</button>' +
        '<span class="map-coords" id="map-coords">19.5000°E, 48.7000°N</span>' +
        '</div>' +
        '<div class="map-pick-bar" id="map-pick-bar">' +
        '<label>Polomer' +
        '<input type="range" id="map-flight-radius" min="100" max="5000" step="100" value="' +
        ((state.pendingFlightLocation && state.pendingFlightLocation.radiusM) || 1000) + '" />' +
        '<span id="map-radius-label">1000 m</span></label>' +
        '<span class="map-pick-info" id="map-pick-info">Ziadny bod — zapnite „Označit miesto letu“ a kliknite na mapu</span>' +
        '<button type="button" id="map-use-flight" disabled>Pouzit v denniku letov</button>' +
        '<button type="button" class="secondary" id="map-clear-flight">Zrusit bod</button>' +
        '</div>' +
        '<div class="map-body">' +
        '<aside class="map-legend" id="map-legend"><p class="hint">Nacitavam legendu…</p></aside>' +
        '<div class="map-canvas-wrap">' +
        '<div id="drone-map"></div>' +
        '<div id="map-fallback" class="map-fallback hidden"></div>' +
        '</div></div></div>';

      function syncPickUi(area) {
        var info = document.getElementById('map-pick-info');
        var useBtn = document.getElementById('map-use-flight');
        var radiusLabel = document.getElementById('map-radius-label');
        var radiusEl = document.getElementById('map-flight-radius');
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
      if (!window.IsSuasDroneMap) {
        content.innerHTML = '<div class="stack"><div class="error">Mapovy modul sa nenačítal.</div></div>';
        return;
      }
      window.IsSuasDroneMap.initMap('drone-map', {
        token: state.token,
        pickMode: startPick,
        initialFlightArea: state.pendingFlightLocation,
        onFlightAreaChange: syncPickUi,
        onError: function (msg) {
          var el = document.getElementById('map-fallback');
          if (el) { el.textContent = msg; el.classList.remove('hidden'); }
        }
      });

      var radiusEl = document.getElementById('map-flight-radius');
      radiusEl.oninput = function () {
        document.getElementById('map-radius-label').textContent = Math.round(Number(radiusEl.value)) + ' m';
        window.IsSuasDroneMap.setFlightRadius(Number(radiusEl.value));
      };
      document.getElementById('map-radius-label').textContent = Math.round(Number(radiusEl.value)) + ' m';

      document.getElementById('map-pick-toggle').onclick = function () {
        var next = !(document.getElementById('map-pick-toggle').classList.contains('active-pick'));
        window.IsSuasDroneMap.setPickMode(next);
      };
      if (startPick) window.IsSuasDroneMap.setPickMode(true);

      document.getElementById('map-use-flight').onclick = function () {
        var area = window.IsSuasDroneMap.getFlightArea();
        if (!area) return;
        state.pendingFlightLocation = {
          lat: area.lat,
          lon: area.lon,
          radiusM: area.radiusM,
          label: area.label
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
        document.querySelectorAll('.nav').forEach(function (b) {
          b.classList.toggle('active', b.dataset.page === 'flights');
        });
        navigate('flights');
      };
      document.getElementById('map-clear-flight').onclick = function () {
        window.IsSuasDroneMap.clearFlightArea();
        state.pendingFlightLocation = null;
        syncPickUi(null);
      };
      document.getElementById('map-home').onclick = function () {
        window.IsSuasDroneMap.goHome();
      };
      document.getElementById('map-locate').onclick = function () {
        window.IsSuasDroneMap.locateMe();
      };
      document.getElementById('map-search-form').onsubmit = function (ev) {
        ev.preventDefault();
        window.IsSuasDroneMap.searchPlace(document.getElementById('map-search-q').value);
      };
      return;
}
