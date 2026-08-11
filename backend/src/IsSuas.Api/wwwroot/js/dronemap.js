/**
 * IS ŠUAS — dronová / VFR mapa Slovenska (Leaflet + Esri-Leaflet + LPS ArcGIS).
 * Verejné API: window.IsSuasDroneMap = { initMap, toggleLayer, destroyMap }
 */
(function (global) {
  'use strict';

  var LPS = 'https://gis.lps.sk/server/rest/services';
  var URL_CACHE = LPS + '/VFRM/VFRM_cache/MapServer';
  var URL_TOPO = LPS + '/VFRM/VFRM_topo/MapServer';
  var URL_POPUP = LPS + '/VFRM/VFRM_popup/MapServer';
  // Fallback cez vlastný API proxy (ak CORS/CSP zablokuje priamy prístup)
  var PROXY_PREFIX = '/api/v1/maps/proxy/';

  /** Sublayery VFRM_popup (overené cez MapServer?f=json). */
  var POPUP_LAYERS = [
    { id: 0, name: 'ADHP_SRSS', label: 'Letiská SRSS', swatch: '#2b7bb9' },
    { id: 1, name: 'ADHP', label: 'Letiská / heliporty', swatch: '#2b7bb9' },
    { id: 2, name: 'Obstacle_Join_ENR', label: 'Prekážky ENR', swatch: '#c47b1a' },
    { id: 3, name: 'Navaid', label: 'Navigačné prostriedky', swatch: '#6b4f9a' },
    { id: 4, name: 'Airspace_point', label: 'Vzdušný priestor (body)', swatch: '#1a6f9a' },
    { id: 5, name: 'Airspace', label: 'Vzdušné priestory', swatch: '#b42318' },
    { id: 6, name: 'NONaixm_linie', label: 'Línie mimo AIXM', swatch: '#5a6d7e' },
    { id: 7, name: 'NONaixm_plochy', label: 'Plochy mimo AIXM', swatch: '#0d8f7a' },
    { id: 8, name: 'Obstacles_ENR_0 az 199 000', label: 'Prekážky (detail)', swatch: '#9a3412' }
  ];

  var state = {
    map: null,
    layers: {},
    visiblePopupLayers: POPUP_LAYERS.map(function (l) { return l.id; }),
    authToken: null,
    onError: null,
    pickMode: false,
    flightPick: null, // { lat, lon, radiusM, marker, circle }
    onFlightAreaChange: null
  };

  function showError(msg) {
    var el = document.getElementById('map-fallback');
    if (el) {
      el.textContent = msg;
      el.classList.remove('hidden');
    }
    if (typeof state.onError === 'function') state.onError(msg);
  }

  function hideError() {
    var el = document.getElementById('map-fallback');
    if (el) el.classList.add('hidden');
  }

  function attr(attrs, keys) {
    if (!attrs) return null;
    for (var i = 0; i < keys.length; i++) {
      var k = keys[i];
      var v = attrs[k];
      if (v != null && String(v) !== '' && String(v) !== 'Null' && String(v) !== 'null')
        return String(v);
    }
    return null;
  }

  function popupHtml(feature) {
    var a = (feature && feature.properties) || {};
    var title = attr(a, ['NAME_TXT', 'name', 'label', 'IDENT_TXT_NAME_TXT', 'IDENT_TXT']) || 'Objekt';
    var type = attr(a, ['TYPE_CODE', 'TYPE_CODE_1', 'layerName']);
    var klass = attr(a, ['CLASS_CODE', 'class']);
    var lower = attr(a, ['lower', 'DistVertLowerVal_Txt', 'DISTVERTLOWER_VAL']);
    var upper = attr(a, ['upper', 'DistVertUpperVal_Txt', 'DISTVERTUPPER_VAL']);
    var remarks = attr(a, ['REMARKS_TXT', 'remarks']);
    var mil = attr(a, ['MIL_CODE']);
    var activity = attr(a, ['activity', 'ACTIVITY_CODE']);

    var rows = '';
    function row(label, val) {
      if (!val) return;
      rows += '<dt>' + label + '</dt><dd>' + val + '</dd>';
    }
    row('Typ', type);
    row('Trieda', klass);
    row('Spodná hranica', lower);
    row('Horná hranica', upper);
    row('Aktivita', activity);
    row('Vojenský', mil);
    row('Poznámka', remarks);

    return '<h4>' + title + '</h4>' + (rows ? '<dl>' + rows + '</dl>' : '<p>Bez ďalších atribútov.</p>');
  }

  function visibleLayersParam() {
    return state.visiblePopupLayers.length
      ? 'visible:' + state.visiblePopupLayers.join(',')
      : 'visible:';
  }

  function identifyAt(latlng) {
    if (!state.map || !global.L || !global.L.esri) return;

    var layersParam = visibleLayersParam();
    if (!layersParam || layersParam === 'visible:') {
      global.L.popup().setLatLng(latlng).setContent('Všetky popup vrstvy sú vypnuté.').openOn(state.map);
      return;
    }

    global.L.esri.identifyFeatures({ url: URL_POPUP })
      .on(state.map)
      .at(latlng)
      .layers(layersParam)
      .tolerance(6)
      .returnGeometry(false)
      .run(function (error, featureCollection) {
        if (error) {
          // Fallback cez backend identify
          identifyViaApi(latlng);
          return;
        }
        var feats = (featureCollection && featureCollection.features) || [];
        if (!feats.length) {
          global.L.popup().setLatLng(latlng)
            .setContent('Na tomto mieste nie je žiadna zóna / objekt.')
            .openOn(state.map);
          return;
        }
        var html = feats.slice(0, 5).map(popupHtml).join('<hr style="border:0;border-top:1px solid #d3dde6;margin:0.55rem 0"/>');
        if (feats.length > 5) html += '<p style="margin:0.35rem 0 0;color:#5a6d7e">… a ďalších ' + (feats.length - 5) + '</p>';
        global.L.popup().setLatLng(latlng).setContent(html).openOn(state.map);
      });
  }

  function identifyViaApi(latlng) {
    var url = '/api/v1/maps/identify?lat=' + encodeURIComponent(latlng.lat) +
      '&lon=' + encodeURIComponent(latlng.lng);
    var headers = { 'Accept': 'application/json' };
    if (state.authToken) headers.Authorization = 'Bearer ' + state.authToken;

    fetch(url, { headers: headers })
      .then(function (r) { return r.json(); })
      .then(function (data) {
        if (data.errorMessage && (!data.zones || !data.zones.length)) {
          global.L.popup().setLatLng(latlng).setContent(data.errorMessage).openOn(state.map);
          return;
        }
        var zones = data.zones || [];
        if (!zones.length) {
          global.L.popup().setLatLng(latlng)
            .setContent('Na tomto mieste nie je žiadna zóna / objekt.')
            .openOn(state.map);
          return;
        }
        var html = zones.slice(0, 5).map(function (z) {
          return popupHtml({ properties: Object.assign({}, z.rawAttributes || {}, {
            NAME_TXT: z.name,
            IDENT_TXT: z.ident,
            TYPE_CODE: z.typeCode,
            CLASS_CODE: z.classCode,
            lower: z.lowerLimit,
            upper: z.upperLimit,
            REMARKS_TXT: z.remarks,
            activity: z.activity,
            MIL_CODE: z.militaryCode
          })});
        }).join('<hr style="border:0;border-top:1px solid #d3dde6;margin:0.55rem 0"/>');
        global.L.popup().setLatLng(latlng).setContent(html).openOn(state.map);
      })
      .catch(function () {
        global.L.popup().setLatLng(latlng)
          .setContent('Identifikácia zlyhala (služba LPS alebo proxy nedostupná).')
          .openOn(state.map);
      });
  }

  function buildLegend(container) {
    if (!container) return;
    var html = '<h3>Podklad (reálne lokality)</h3>';
    html += '<label><input type="checkbox" data-base="worldTopo" checked /> Topografia Esri (mestá, cesty, terén)</label>';
    html += '<label><input type="checkbox" data-base="topo" /> Topografia LPS (ANC 1:500k)</label>';
    html += '<label><input type="checkbox" data-base="osm" /> Otvorená mapa (OSM)</label>';
    html += '<h3 style="margin-top:0.9rem">Letecká vrstva</h3>';
    html += '<label><input type="checkbox" data-base="cache" checked /> VFR manuál LPS (zóny, CTR/TMA, letiská)</label>';
    html += '<label>Priehľadnosť VFR <input type="range" id="map-cache-opacity" min="20" max="100" value="62" style="width:100%"/></label>';
    html += '<h3 style="margin-top:0.9rem">Identifikácia (klik)</h3>';
    html += '<p class="hint">Klik na mapu zobrazí názov zóny, triedu a FL.</p>';
    POPUP_LAYERS.forEach(function (l) {
      html += '<label><span class="swatch" style="background:' + l.swatch +
        '"></span><input type="checkbox" data-popup-layer="' + l.id + '" checked /> ' +
        l.label + '</label>';
    });
    html += '<p class="hint">Zdroj: VFR Manual, LPS SR š. p. · Esri · ' +
      '<a href="https://gis.lps.sk/vfrm/" target="_blank" rel="noopener">gis.lps.sk/vfrm</a></p>';
    container.innerHTML = html;

    container.querySelectorAll('[data-base]').forEach(function (cb) {
      cb.addEventListener('change', function () {
        toggleLayer(cb.getAttribute('data-base'), cb.checked);
      });
    });
    var opacity = document.getElementById('map-cache-opacity');
    if (opacity) {
      opacity.addEventListener('input', function () {
        var layer = state.layers.cache;
        if (layer && layer.setOpacity) layer.setOpacity(Number(opacity.value) / 100);
      });
    }
    container.querySelectorAll('[data-popup-layer]').forEach(function (cb) {
      cb.addEventListener('change', function () {
        var id = Number(cb.getAttribute('data-popup-layer'));
        if (cb.checked) {
          if (state.visiblePopupLayers.indexOf(id) < 0) state.visiblePopupLayers.push(id);
        } else {
          state.visiblePopupLayers = state.visiblePopupLayers.filter(function (x) { return x !== id; });
        }
      });
    });
  }

  function attachBasemap(key, layer, add) {
    state.layers[key] = layer;
    if (add && state.map) layer.addTo(state.map);
  }

  /**
   * @param {string} containerId
   * @param {{ token?: string, onError?: function }} options
   */
  function initMap(containerId, options) {
    options = options || {};
    state.authToken = options.token || null;
    state.onError = options.onError || null;
    state.preview = !!options.preview;
    hideError();

    if (!global.L || !global.L.esri) {
      showError('Knižnica mapy sa nenačítala. Skontrolujte CDN a bezpečnostné hlavičky.');
      return null;
    }

    destroyMap();

    var el = document.getElementById(containerId);
    if (!el) {
      showError('Kontajner mapy neexistuje.');
      return null;
    }

    try {
      var map = global.L.map(containerId, {
        center: options.center || [48.55, 17.85],
        zoom: options.zoom != null ? options.zoom : 9,
        zoomControl: !options.preview,
        attributionControl: true,
        dragging: !options.preview,
        scrollWheelZoom: !options.preview,
        doubleClickZoom: !options.preview,
        boxZoom: !options.preview,
        keyboard: !options.preview
      });
      state.map = map;

      // 1) Podklad s reálnymi lokalitami (mestá, cesty, lesy, reliéf) — ako oficiálny VFR Manual
      var worldTopo = global.L.esri.tiledMapLayer({
        url: 'https://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer',
        maxZoom: 18,
        attribution: 'Esri, HERE, Garmin, USGS, TOPÚ'
      });
      attachBasemap('worldTopo', worldTopo, true);

      // OSM záloha
      var osm = global.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 18,
        attribution: '&copy; OpenStreetMap'
      });
      attachBasemap('osm', osm, false);

      // 2) LPS topografia (voliteľne — ANC chart)
      var topo = global.L.esri.tiledMapLayer({
        url: URL_TOPO,
        maxZoom: 12,
        minZoom: 8,
        opacity: 0.55,
        attribution: 'TOPÚ / LPS SR'
      });
      attachBasemap('topo', topo, false);

      // 3) VFR cache LPS — farebné zóny, CTR/TMA, letiská/frekvencie (polopriehľadné nad topom)
      var cache = global.L.esri.tiledMapLayer({
        url: URL_CACHE,
        maxZoom: 17,
        minZoom: 7,
        opacity: options.preview ? 0.72 : 0.62,
        attribution: 'VFR Manual, LPS SR š. p.'
      });
      attachBasemap('cache', cache, true);

      var popupDyn = global.L.esri.dynamicMapLayer({
        url: URL_POPUP,
        opacity: 0.25,
        layers: [5, 7, 1],
        f: 'image'
      });
      attachBasemap('popupDyn', popupDyn, false);

      map.on('click', function (e) {
        if (state.preview) return;
        if (state.pickMode) {
          setFlightArea(e.latlng.lat, e.latlng.lng, getPickRadiusM());
          return;
        }
        identifyAt(e.latlng);
        var coords = document.getElementById('map-coords');
        if (coords) {
          coords.textContent = e.latlng.lng.toFixed(4) + '°E, ' + e.latlng.lat.toFixed(4) + '°N';
        }
      });

      map.on('mousemove', function (e) {
        if (state.preview) return;
        var coords = document.getElementById('map-coords');
        if (coords) {
          coords.textContent = e.latlng.lng.toFixed(4) + '°E, ' + e.latlng.lat.toFixed(4) + '°N';
        }
      });

      worldTopo.on('loaderror', function () {
        showError('Topografia Esri nie je dostupná — zapínam otvorenú mapu.');
        if (!map.hasLayer(osm)) osm.addTo(map);
      });
      cache.on('loaderror', function () {
        showError('VFR vrstva LPS nie je dostupná. Podklad (topo) ostáva viditeľný.');
      });

      setTimeout(function () { map.invalidateSize(); }, 80);
      setTimeout(function () { if (state.map) state.map.invalidateSize(); }, 300);
      if (!options.preview) buildLegend(document.getElementById('map-legend'));

      // Obnovenie rozpracovaného výberu / predvyplnenie z options
      if (!options.preview && options.initialFlightArea) {
        setFlightArea(
          options.initialFlightArea.lat,
          options.initialFlightArea.lon,
          options.initialFlightArea.radiusM || 1000
        );
      }
      if (!options.preview && options.pickMode) setPickMode(true);
      if (!options.preview && typeof options.onFlightAreaChange === 'function')
        state.onFlightAreaChange = options.onFlightAreaChange;

      return map;
    } catch (err) {
      showError('Mapa sa nenačítala: ' + (err && err.message ? err.message : err));
      return null;
    }
  }

  function toggleLayer(key, visible) {
    var layer = state.layers[key];
    if (!state.map || !layer) return;
    if (visible) {
      if (!state.map.hasLayer(layer)) layer.addTo(state.map);
    } else if (state.map.hasLayer(layer)) {
      state.map.removeLayer(layer);
    }
    // VFR overlay vždy navrchu nad podkladom
    var cache = state.layers.cache;
    if (cache && state.map.hasLayer(cache) && cache.bringToFront) cache.bringToFront();
  }

  function locateMe() {
    if (!state.map) return;
    if (!navigator.geolocation) {
      showError('Prehliadač nepodporuje geolokáciu.');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      function (pos) {
        hideError();
        var ll = [pos.coords.latitude, pos.coords.longitude];
        state.map.setView(ll, 11);
        global.L.circleMarker(ll, {
          radius: 7,
          color: '#0d8f7a',
          fillColor: '#0d8f7a',
          fillOpacity: 0.85
        }).addTo(state.map).bindPopup('Moja poloha').openPopup();
      },
      function () {
        showError('Nepodarilo sa získať polohu (povolenie / HTTPS).');
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  function searchPlace(query) {
    if (!state.map || !query || !String(query).trim()) return;
    var q = String(query).trim();
    var url = 'https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=sk&q=' +
      encodeURIComponent(q);
    fetch(url, { headers: { 'Accept': 'application/json' } })
      .then(function (r) {
        if (!r.ok) throw new Error('Vyhľadávanie zlyhalo (' + r.status + ')');
        return r.json();
      })
      .then(function (hits) {
        if (!hits || !hits.length) {
          showError('Miesto „' + q + '“ sa nenašlo.');
          return;
        }
        hideError();
        var hit = hits[0];
        var ll = [Number(hit.lat), Number(hit.lon)];
        state.map.setView(ll, 11);
        global.L.marker(ll).addTo(state.map).bindPopup(hit.display_name || q).openPopup();
      })
      .catch(function () {
        showError('Vyhľadávanie adresy zlyhalo (služba máp / sieť).');
      });
  }

  function getPickRadiusM() {
    var el = document.getElementById('map-flight-radius');
    var v = el ? Number(el.value) : 1000;
    if (!isFinite(v) || v < 50) v = 50;
    if (v > 20000) v = 20000;
    return v;
  }

  function clearFlightArea(silent) {
    if (!state.flightPick) {
      if (!silent && typeof state.onFlightAreaChange === 'function') state.onFlightAreaChange(null);
      return;
    }
    if (state.map) {
      if (state.flightPick.marker) state.map.removeLayer(state.flightPick.marker);
      if (state.flightPick.circle) state.map.removeLayer(state.flightPick.circle);
    }
    state.flightPick = null;
    if (!silent && typeof state.onFlightAreaChange === 'function') state.onFlightAreaChange(null);
  }

  function setFlightArea(lat, lon, radiusM) {
    if (!state.map || !global.L) return null;
    radiusM = radiusM || getPickRadiusM();
    clearFlightArea(true);

    var ll = [lat, lon];
    var marker = global.L.marker(ll, { draggable: true }).addTo(state.map);
    var circle = global.L.circle(ll, {
      radius: radiusM,
      color: '#0d8f7a',
      weight: 2,
      fillColor: '#0d8f7a',
      fillOpacity: 0.18
    }).addTo(state.map);

    function syncFromMarker() {
      var p = marker.getLatLng();
      circle.setLatLng(p);
      state.flightPick.lat = p.lat;
      state.flightPick.lon = p.lng;
      notifyFlightArea();
    }

    marker.on('drag', syncFromMarker);
    marker.on('dragend', syncFromMarker);

    state.flightPick = {
      lat: lat,
      lon: lon,
      radiusM: radiusM,
      marker: marker,
      circle: circle
    };
    notifyFlightArea();
    marker.bindPopup(
      'Miesto letu<br/>' + lat.toFixed(5) + '°N, ' + lon.toFixed(5) + '°E<br/>r = ' +
      Math.round(radiusM) + ' m'
    ).openPopup();
    return getFlightArea();
  }

  function notifyFlightArea() {
    if (typeof state.onFlightAreaChange === 'function')
      state.onFlightAreaChange(getFlightArea());
  }

  function getFlightArea() {
    if (!state.flightPick) return null;
    return {
      lat: state.flightPick.lat,
      lon: state.flightPick.lon,
      radiusM: state.flightPick.radiusM,
      label: state.flightPick.lat.toFixed(5) + ', ' + state.flightPick.lon.toFixed(5)
    };
  }

  function setFlightRadius(radiusM) {
    radiusM = Number(radiusM);
    if (!isFinite(radiusM)) return;
    if (radiusM < 50) radiusM = 50;
    if (radiusM > 20000) radiusM = 20000;
    if (!state.flightPick || !state.flightPick.circle) return;
    state.flightPick.radiusM = radiusM;
    state.flightPick.circle.setRadius(radiusM);
    notifyFlightArea();
  }

  function setPickMode(enabled) {
    state.pickMode = !!enabled;
    var mapEl = document.getElementById('drone-map') || document.getElementById('geo-zones-map');
    if (mapEl) mapEl.classList.toggle('map-pick-mode', state.pickMode);
    if (state.map && state.map.getContainer) {
      var container = state.map.getContainer();
      container.classList.toggle('map-pick-mode', state.pickMode);
      container.style.cursor = state.pickMode ? 'crosshair' : '';
    }
    var btn = document.getElementById('map-pick-toggle');
    if (btn) {
      btn.classList.toggle('active-pick', state.pickMode);
      btn.textContent = state.pickMode ? 'Kliknite na mapu…' : 'Označit miesto letu';
    }
  }

  function destroyMap() {
    clearFlightArea(true);
    state.pickMode = false;
    state.preview = false;
    state.onFlightAreaChange = null;
    if (state.map) {
      try { state.map.remove(); } catch (_) { /* ignore */ }
      state.map = null;
    }
    state.layers = {};
  }

  function goHome() {
    if (state.map) state.map.setView([48.55, 17.85], 9);
  }

  global.IsSuasDroneMap = {
    initMap: initMap,
    toggleLayer: toggleLayer,
    destroyMap: destroyMap,
    locateMe: locateMe,
    searchPlace: searchPlace,
    goHome: goHome,
    setPickMode: setPickMode,
    setFlightArea: setFlightArea,
    setFlightRadius: setFlightRadius,
    getFlightArea: getFlightArea,
    clearFlightArea: clearFlightArea,
    /** Export pre dokumentáciu / UI */
    popupLayers: POPUP_LAYERS.slice()
  };
})(typeof window !== 'undefined' ? window : globalThis);
