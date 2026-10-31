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

  // ===================== VFR Manual (LPS SR) — rovnaká skladba mapy ako gis.lps.sk/vfrm =====================
  var ESRI = 'https://services.arcgisonline.com/arcgis/rest/services';
  var URL_VFRM_BASEMAP = LPS + '/basemaps/VFRM_basemap/MapServer';
  var URL_DPN = LPS + '/VFRM/DPN/MapServer';
  var URL_RESERVATION = LPS + '/Hosted/Reservation_(Public)2/FeatureServer/0';
  var URL_STATUS = LPS + '/Hosted/Status_(Public)/FeatureServer/0';
  var URL_METAR = 'https://services9.arcgis.com/RHVPKKiFTONKtxq3/arcgis/rest/services/NOAA_METAR_current_wind_speed_direction_v1/FeatureServer/0';
  // Rovnaké filtre ako oficiálna aplikácia
  var RESERVATION_WHERE = "(lower_fl <> 'FL' OR lower_val <= 195) AND (localtype_txt IS NULL OR localtype_txt <> 'NPZ') AND " +
    "(status = 'ACTIVE' OR status = 'APPROVED' OR status = 'ALLOCATED' OR status = 'REFERENCE_ALLOCATED' OR status = 'PENDING')";
  var STATUS_WHERE = "(latestupdate NOT BETWEEN CURRENT_TIMESTAMP - INTERVAL '3' MINUTE AND CURRENT_TIMESTAMP) OR " +
    "(statussource = 'NAM' AND statusnam <> 'success') OR (statussource = 'AMA' AND statusama <> 'success')";
  var AIRSPACE_DEF = "(LOCALTYPE_TXT IS NULL OR LOCALTYPE_TXT = 'ATZ' OR LOCALTYPE_TXT = 'RMZ') AND (lower_FL <> 'FL' OR lower_VAL <= 195) AND (IDENT_TXT <> 'C' OR NAME_TXT <> 'EJECTION AREA')";
  // Predvolený výrez = rozsah z konfigurácie VFR Manual
  var VFRM_BOUNDS = [[47.6, 16.7], [49.7, 22.7]];
  // Referenčný dátum AIRAC (VFR Manual: Effective Date 29OCT2026), cyklus 28 dní
  var AIRAC_REF = Date.UTC(2026, 9, 29);

  function escHtml(v) {
    return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  /** Dátum účinnosti aktuálneho cyklu AIRAC (ku ktorému sú letecké údaje aktuálne). */
  function airacEffectiveDate(now) {
    var cycle = 28 * 86400000;
    var t = (now || new Date()).getTime();
    return new Date(AIRAC_REF + Math.floor((t - AIRAC_REF) / cycle) * cycle);
  }

  function fmtAltitude(val, uom, ref) {
    if (val == null || val === '') return null;
    var v = String(val);
    if (String(ref || '').toUpperCase() === 'FL' || String(uom || '').toUpperCase() === 'FL') return 'FL ' + v;
    return v + (uom ? ' ' + String(uom).toUpperCase() : '');
  }

  function fmtDateTime(ms) {
    if (ms == null || ms === '') return null;
    var d = new Date(typeof ms === 'number' ? ms : String(ms));
    if (isNaN(d.getTime())) return String(ms);
    return d.toLocaleString('sk-SK', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'UTC' }) + ' UTC';
  }

  function rowsHtml(rows) {
    var html = rows.filter(function (r) { return r[1] != null && String(r[1]) !== '' && String(r[1]) !== 'null'; })
      .map(function (r) { return '<div><dt>' + escHtml(r[0]) + '</dt><dd>' + escHtml(r[1]) + '</dd></div>'; }).join('');
    return html ? '<dl class="vfrm-attrs">' + html + '</dl>' : '';
  }

  // ---------- zemepisná sieť (ako „Geographic grid“ vo VFR Manual) ----------
  function graticuleStep(zoom) {
    if (zoom <= 8) return 1;
    if (zoom <= 10) return 0.5;
    if (zoom <= 12) return 1 / 6;
    if (zoom <= 14) return 1 / 12;
    return 1 / 60;
  }
  function fmtDeg(v, pos, neg) {
    var a = Math.abs(v);
    var d = Math.floor(a + 1e-9);
    var m = Math.round((a - d) * 60);
    if (m === 60) { d += 1; m = 0; }
    return d + '°' + (m ? (m < 10 ? '0' : '') + m + "'" : '') + ' ' + (v >= 0 ? pos : neg);
  }
  function createGraticule(map) {
    var L = global.L;
    if (!map.getPane('vfrmGrid')) {
      var pane = map.createPane('vfrmGrid');
      pane.style.zIndex = 390;
      pane.style.pointerEvents = 'none';
    }
    var group = L.layerGroup();
    function redraw() {
      group.clearLayers();
      if (!map.hasLayer(group)) return;
      var b = map.getBounds();
      var step = graticuleStep(map.getZoom());
      var line = { pane: 'vfrmGrid', color: '#3a4652', weight: 0.8, opacity: 0.55, interactive: false };
      var south = Math.floor(b.getSouth() / step) * step;
      var west = Math.floor(b.getWest() / step) * step;
      var labelLon = b.getWest() + (b.getEast() - b.getWest()) * 0.015;
      var labelLat = b.getNorth() - (b.getNorth() - b.getSouth()) * 0.03;
      for (var lat = south; lat <= b.getNorth(); lat += step) {
        group.addLayer(L.polyline([[lat, b.getWest() - 1], [lat, b.getEast() + 1]], line));
        group.addLayer(L.marker([lat, labelLon], { pane: 'vfrmGrid', interactive: false, keyboard: false,
          icon: L.divIcon({ className: 'vfrm-grid-label', html: '<span>' + fmtDeg(lat, 'N', 'S') + '</span>', iconSize: null }) }));
      }
      for (var lon = west; lon <= b.getEast(); lon += step) {
        group.addLayer(L.polyline([[b.getSouth() - 1, lon], [b.getNorth() + 1, lon]], line));
        group.addLayer(L.marker([labelLat, lon], { pane: 'vfrmGrid', interactive: false, keyboard: false,
          icon: L.divIcon({ className: 'vfrm-grid-label is-lon', html: '<span>' + fmtDeg(lon, 'E', 'W') + '</span>', iconSize: null }) }));
      }
    }
    map.on('moveend zoomend', redraw);
    group.on('add', function () { setTimeout(redraw, 0); });
    return group;
  }

  // ---------- aktivácia vzdušných priestorov (AMC) ----------
  function reservationStyle(feature) {
    var active = String((feature.properties || {}).status || '').toUpperCase() === 'ACTIVE';
    return active
      ? { color: '#e60000', weight: 2.25, fillColor: '#ff0000', fillOpacity: 72 / 255, opacity: 1 }
      : { color: '#ffaa00', weight: 2.25, fillColor: '#ffaa00', fillOpacity: 129 / 255, opacity: 1 };
  }
  function reservationRows(p) {
    return [
      ['Stav', String(p.status || '').toUpperCase() === 'ACTIVE' ? 'Aktívny teraz' : 'Plánovaná aktivácia (' + (p.status || '—') + ')'],
      ['Typ', p.airspace_type || p.type_code],
      ['Spodná hranica', fmtAltitude(p.lower_val, p.lower_uom, p.lower_fl) || p.base],
      ['Horná hranica', fmtAltitude(p.upper_val, p.upper_uom, p.upper_fl) || p.top_],
      ['Od', fmtDateTime(p.startdate)],
      ['Do', fmtDateTime(p.enddate)],
      ['Aktualizované', fmtDateTime(p.lastupdate)]
    ];
  }

  // ---------- vietor (METAR) ----------
  function metarIcon(p) {
    var L = global.L;
    var spd = Number(p.WIND_SPEED);
    var dir = Number(p.WIND_DIRECT);
    if (!isFinite(spd) || spd <= 0 || !isFinite(dir)) {
      return L.divIcon({ className: 'vfrm-wind is-calm', iconSize: [16, 16], iconAnchor: [8, 8],
        html: '<svg viewBox="0 0 16 16" width="16" height="16"><circle cx="8" cy="8" r="6" fill="#fff" stroke="#1f3a5f" stroke-width="1.6"/><path d="M5 5l6 6M11 5l-6 6" stroke="#1f3a5f" stroke-width="1.4"/></svg>' });
    }
    var size = spd < 5 ? 20 : spd < 10 ? 26 : spd < 20 ? 32 : 38;
    var rot = (dir + 180) % 360; // šípka ukazuje, kam vietor fúka (ako vo VFR Manual)
    return L.divIcon({ className: 'vfrm-wind', iconSize: [size, size], iconAnchor: [size / 2, size / 2],
      html: '<svg viewBox="0 0 24 24" width="' + size + '" height="' + size + '" style="transform:rotate(' + rot + 'deg)">' +
        '<path d="M12 2l5 8h-3.2v12h-3.6V10H7z" fill="#1f3a5f" stroke="#fff" stroke-width="0.8"/></svg>' });
  }
  function metarRows(p) {
    var spd = Number(p.WIND_SPEED);
    return [
      ['Stanica', [p.ICAO, p.STATION_NAME].filter(Boolean).join(' · ')],
      ['Vietor', spd > 0 && isFinite(Number(p.WIND_DIRECT))
        ? Math.round(Number(p.WIND_DIRECT)) + '° / ' + Math.round(spd) + ' kt' + (Number(p.WIND_GUST) > 0 ? ' (nárazy ' + Math.round(Number(p.WIND_GUST)) + ' kt)' : '')
        : 'bezvetrie'],
      ['Teplota', p.TEMP != null ? p.TEMP + ' °C' : null],
      ['Dohľadnosť', p.VISIBILITY],
      ['Oblačnosť', p.SKY_CONDTN],
      ['Kategória letu', p.FLT_CATEGORY],
      ['Pozorovanie', fmtDateTime(p.OBS_DATETIME)]
    ];
  }

  function setStatusWarning(text) {
    var el = state.map && state.map.getContainer().querySelector('.vfrm-status-warning');
    if (!el) return;
    el.textContent = text || '';
    el.classList.toggle('hidden', !text);
  }

  // Ako VFR Manual: keď sa údaje o aktivácii priestorov neaktualizujú, používateľ je upozornený.
  function checkAmcStatus() {
    if (!state.vfrm || !global.L.esri || !global.L.esri.query) return;
    global.L.esri.query({ url: URL_STATUS }).where(STATUS_WHERE).count(function (err, count) {
      if (err) {
        setStatusWarning('Stav aktivácie vzdušných priestorov sa nepodarilo overiť. Pred vstupom do priestoru overte jeho stav na stanovišti ATS.');
        return;
      }
      setStatusWarning(count > 0
        ? 'Údaje o aktivácii vzdušných priestorov nie sú aktuálne. Pred vstupom do priestoru overte jeho stav na stanovišti ATS.'
        : '');
    });
  }

  function setVfrmBase(kind) {
    var v = state.vfrm, map = state.map;
    if (!v || !map) return;
    var groups = { vfrm: v.baseVfrm, topo: [v.baseTopo], osm: [v.baseOsm] };
    Object.keys(groups).forEach(function (k) {
      groups[k].forEach(function (layer) {
        if (k === kind) { if (!map.hasLayer(layer)) layer.addTo(map); }
        else if (map.hasLayer(layer)) map.removeLayer(layer);
      });
    });
    v.base = kind;
  }

  // ---------- ovládacie prvky (Vrstvy, domov, poloha, mierka, súradnice, AIRAC) ----------
  function addVfrmControls(map) {
    var L = global.L;
    var LayersCtl = L.Control.extend({
      options: { position: 'topright' },
      onAdd: function () {
        var box = L.DomUtil.create('div', 'vfrm-layers leaflet-bar');
        box.innerHTML =
          '<button type="button" class="vfrm-layers-toggle" title="Vrstvy" aria-expanded="false">' +
          '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"><path d="M12 3 2 8l10 5 10-5-10-5z"/><path d="m2 13 10 5 10-5"/></svg><span>Vrstvy</span></button>' +
          '<div class="vfrm-layers-panel hidden">' +
          '<h4>Podkladová mapa</h4>' +
          '<label><input type="radio" name="vfrm-base" value="vfrm" checked /> VFR (LPS SR)</label>' +
          '<label><input type="radio" name="vfrm-base" value="topo" /> Topografická (Esri)</label>' +
          '<label><input type="radio" name="vfrm-base" value="osm" /> OpenStreetMap</label>' +
          '<h4>Doplnkové vrstvy</h4>' +
          '<label><input type="checkbox" data-vfrm="cache" checked /> Letecké informácie VFR</label>' +
          '<label><input type="checkbox" data-vfrm="reservations" checked /> Aktivácia vzdušných priestorov</label>' +
          '<label><input type="checkbox" data-vfrm="metar" checked /> Vietor (METAR)</label>' +
          '<label><input type="checkbox" data-vfrm="grid" checked /> Zemepisná sieť</label>' +
          '<label><input type="checkbox" data-vfrm="dpn" /> Významné body (ICAO)</label>' +
          '<p class="vfrm-legend"><span class="sw is-active"></span>aktívny priestor<br/><span class="sw is-planned"></span>plánovaná aktivácia</p>' +
          '<p class="vfrm-src">Zdroj: VFR Manual, LPS SR, š. p. · Esri · NOAA</p>' +
          '</div>';
        L.DomEvent.disableClickPropagation(box);
        L.DomEvent.disableScrollPropagation(box);
        var btn = box.querySelector('.vfrm-layers-toggle');
        var panel = box.querySelector('.vfrm-layers-panel');
        btn.onclick = function () {
          panel.classList.toggle('hidden');
          btn.setAttribute('aria-expanded', String(!panel.classList.contains('hidden')));
        };
        box.querySelectorAll('input[name="vfrm-base"]').forEach(function (r) {
          r.onchange = function () { if (r.checked) setVfrmBase(r.value); };
        });
        box.querySelectorAll('input[data-vfrm]').forEach(function (cb) {
          cb.onchange = function () { toggleLayer(cb.getAttribute('data-vfrm'), cb.checked); };
        });
        return box;
      }
    });
    new LayersCtl().addTo(map);

    var NavCtl = L.Control.extend({
      options: { position: 'topleft' },
      onAdd: function () {
        var box = L.DomUtil.create('div', 'leaflet-bar vfrm-nav');
        box.innerHTML =
          '<a href="#" role="button" title="Predvolený pohľad" data-act="home"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"><path d="M3 11 12 4l9 7"/><path d="M5 10v10h14V10"/></svg></a>' +
          '<a href="#" role="button" title="Moja poloha" data-act="locate"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="4"/><path d="M12 2v3M12 19v3M2 12h3M19 12h3"/></svg></a>';
        L.DomEvent.disableClickPropagation(box);
        box.querySelectorAll('a').forEach(function (a) {
          a.onclick = function (ev) {
            ev.preventDefault();
            if (a.getAttribute('data-act') === 'home') goHome(); else locateMe();
          };
        });
        return box;
      }
    });
    new NavCtl().addTo(map);

    L.control.scale({ position: 'bottomleft', metric: true, imperial: true }).addTo(map);

    var CoordCtl = L.Control.extend({
      options: { position: 'bottomleft' },
      onAdd: function () {
        var el = L.DomUtil.create('div', 'vfrm-coords');
        el.textContent = '—';
        map.on('mousemove', function (e) {
          el.textContent = e.latlng.lng.toFixed(4) + '  ' + e.latlng.lat.toFixed(4) + '  stupne';
        });
        return el;
      }
    });
    new CoordCtl().addTo(map);

    var InfoCtl = L.Control.extend({
      options: { position: 'bottomright' },
      onAdd: function () {
        var el = L.DomUtil.create('div', 'vfrm-effective');
        var d = airacEffectiveDate();
        var months = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC'];
        el.innerHTML = 'VFR Manual (GND – FL195) · účinnosť <b>' + (d.getUTCDate() < 10 ? '0' : '') + d.getUTCDate() + months[d.getUTCMonth()] + d.getUTCFullYear() + '</b>';
        return el;
      }
    });
    new InfoCtl().addTo(map);

    var warn = L.DomUtil.create('div', 'vfrm-status-warning hidden', map.getContainer());
    L.DomEvent.disableClickPropagation(warn);
    state.vfrm.timers.push(setInterval(checkAmcStatus, 60000));
    checkAmcStatus();
  }

  function buildVfrmLayers(map) {
    var L = global.L;
    var v = state.vfrm = { timers: [], base: 'vfrm' };

    // Podkladové mapy v rovnakom poradí a mierkach ako VFR Manual (LPS SR)
    var lightGray = L.esri.tiledMapLayer({ url: ESRI + '/Canvas/World_Light_Gray_Base/MapServer', maxZoom: 10, zIndex: 1, attribution: 'Esri' });
    var hillshade = L.esri.tiledMapLayer({ url: ESRI + '/Elevation/World_Hillshade/MapServer', maxZoom: 10, opacity: 0.4, zIndex: 2, attribution: 'Esri, USGS' });
    var grayRef = L.esri.tiledMapLayer({ url: ESRI + '/Canvas/World_Light_Gray_Reference/MapServer', maxZoom: 10, zIndex: 3 });
    var vfrmBase = L.esri.tiledMapLayer({ url: URL_VFRM_BASEMAP, minZoom: 10, maxZoom: 13, zIndex: 4, attribution: 'LPS SR' });
    var street = L.esri.tiledMapLayer({ url: ESRI + '/World_Street_Map/MapServer', minZoom: 14, zIndex: 5 });
    v.baseVfrm = [lightGray, hillshade, grayRef, vfrmBase, street];
    v.baseTopo = L.esri.tiledMapLayer({ url: ESRI + '/World_Topo_Map/MapServer', zIndex: 1, attribution: 'Esri, HERE, Garmin, USGS' });
    v.baseOsm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { zIndex: 1, maxZoom: 19, attribution: '&copy; OpenStreetMap' });
    v.baseVfrm.forEach(function (l) { l.addTo(map); });

    // Letecké informácie VFR — v plnej sýtosti ako na gis.lps.sk/vfrm
    var cache = L.esri.tiledMapLayer({ url: URL_CACHE, minZoom: 7, opacity: 1, zIndex: 10, attribution: 'VFR Manual, LPS SR š. p.' });
    attachBasemap('cache', cache, true);
    cache.on('loaderror', function () { showError('VFR vrstva LPS nie je dostupná.'); });

    attachBasemap('grid', createGraticule(map), true);
    attachBasemap('dpn', L.esri.dynamicMapLayer({ url: URL_DPN, f: 'image', opacity: 1 }), false);

    var reservations = L.esri.featureLayer({
      url: URL_RESERVATION,
      where: RESERVATION_WHERE,
      style: reservationStyle,
      onEachFeature: function (feature, layer) {
        var p = feature.properties || {};
        layer.bindTooltip(escHtml(p.label || p.airspace || 'Vzdušný priestor'), { sticky: true });
      }
    });
    attachBasemap('reservations', reservations, true);
    v.timers.push(setInterval(function () {
      if (state.map && state.map.hasLayer(reservations)) reservations.refresh();
    }, 30000));

    var metar = L.esri.featureLayer({
      url: URL_METAR,
      where: '1=1',
      pointToLayer: function (feature, latlng) {
        return L.marker(latlng, { icon: metarIcon(feature.properties || {}), keyboard: false });
      },
      onEachFeature: function (feature, layer) {
        var p = feature.properties || {};
        layer.bindTooltip(escHtml((p.ICAO || '') + ' ' + (Number(p.WIND_SPEED) > 0 ? Math.round(Number(p.WIND_DIRECT)) + '°/' + Math.round(Number(p.WIND_SPEED)) + ' kt' : 'bezvetrie')));
        layer.on('click', function (e) {
          if (state.pickMode) return;
          L.DomEvent.stopPropagation(e);
          renderIdentifyPanel(e.latlng, [{ title: 'METAR ' + (p.ICAO || ''), kind: 'Vietor (METAR)', html: rowsHtml(metarRows(p)) }]);
        });
      }
    });
    attachBasemap('metar', metar, true);
    v.timers.push(setInterval(function () {
      if (state.map && state.map.hasLayer(metar)) metar.refresh();
    }, 60000));

    addVfrmControls(map);
  }

  // ---------- identifikácia do ľavého panelu (ako „Select Feature(s) in the map“) ----------
  function renderIdentifyPanel(latlng, items, loading) {
    var panel = state.identifyPanelId && document.getElementById(state.identifyPanelId);
    if (!panel) {
      if (!items || !items.length) return;
      var html = items.map(function (it) { return '<h4>' + escHtml(it.title) + '</h4>' + (it.html || ''); }).join('<hr/>');
      global.L.popup().setLatLng(latlng).setContent(html).openOn(state.map);
      return;
    }
    var head = '<div class="vfrm-identify-head"><b>Objekty v mieste</b><span>' +
      latlng.lat.toFixed(4) + '° N, ' + latlng.lng.toFixed(4) + '° E</span></div>';
    if (loading) { panel.innerHTML = head + '<p class="hint">Načítavam…</p>'; return; }
    if (!items.length) { panel.innerHTML = head + '<p class="hint">Na tomto mieste nie je žiadny objekt.</p>'; return; }
    panel.innerHTML = head + '<p class="hint">' + items.length + (items.length === 1 ? ' objekt' : items.length < 5 ? ' objekty' : ' objektov') + '</p>' +
      items.map(function (it, i) {
        return '<details class="vfrm-identify-item"' + (i === 0 ? ' open' : '') + '><summary><span>' + escHtml(it.title) +
          '</span><small>' + escHtml(it.kind || '') + '</small></summary>' + (it.html || '') + '</details>';
      }).join('');
  }

  function popupFeatureToItem(feature, layerName) {
    var a = (feature && feature.properties) || {};
    var meta = POPUP_LAYERS.find(function (l) { return l.name === layerName; });
    var title = attr(a, ['NAME_TXT', 'name', 'label', 'IDENT_TXT_NAME_TXT', 'IDENT_TXT']) || (meta && meta.label) || 'Objekt';
    return {
      title: title,
      kind: meta ? meta.label : (layerName || ''),
      html: rowsHtml([
        ['Identifikátor', attr(a, ['IDENT_TXT', 'CODE_ID'])],
        ['Typ', attr(a, ['LOCALTYPE_TXT', 'TYPE_CODE', 'TYPE_CODE_1'])],
        ['Trieda', attr(a, ['CLASS_CODE', 'class'])],
        ['Spodná hranica', attr(a, ['lower', 'DistVertLowerVal_Txt', 'DISTVERTLOWER_VAL']) || fmtAltitude(a.lower_VAL, a.lower_UOM, a.lower_FL)],
        ['Horná hranica', attr(a, ['upper', 'DistVertUpperVal_Txt', 'DISTVERTUPPER_VAL']) || fmtAltitude(a.upper_VAL, a.upper_UOM, a.upper_FL)],
        ['Frekvencia', attr(a, ['FREQ', 'FREQUENCY', 'VALFREQ'])],
        ['Nadmorská výška', attr(a, ['ELEV_VAL', 'VALELEV', 'HEIGHT_VAL'])],
        ['Aktivita', attr(a, ['activity', 'ACTIVITY_CODE'])],
        ['Vojenský', attr(a, ['MIL_CODE'])],
        ['Poznámka', attr(a, ['REMARKS_TXT', 'remarks'])]
      ])
    };
  }

  function identifyVfrm(latlng) {
    var L = global.L;
    renderIdentifyPanel(latlng, [], true);
    var results = { popup: null, res: null };
    function done() {
      if (results.popup === null || results.res === null) return;
      renderIdentifyPanel(latlng, results.res.concat(results.popup));
    }
    var reservations = state.layers.reservations;
    if (reservations && state.map.hasLayer(reservations)) {
      L.esri.query({ url: URL_RESERVATION }).where(RESERVATION_WHERE).intersects(latlng).run(function (err, fc) {
        results.res = err ? [] : ((fc && fc.features) || []).map(function (f) {
          var p = f.properties || {};
          return { title: p.label || p.airspace || 'Vzdušný priestor', kind: 'Aktivácia vzdušného priestoru', html: rowsHtml(reservationRows(p)) };
        });
        done();
      });
    } else {
      results.res = [];
    }
    L.esri.identifyFeatures({ url: URL_POPUP })
      .on(state.map).at(latlng)
      .layers('visible:1,2,3,4,5,8')
      .layerDef(5, AIRSPACE_DEF)
      .tolerance(4).returnGeometry(false)
      .run(function (error, fc, raw) {
        if (error) { results.popup = []; done(); return; }
        var feats = (fc && fc.features) || [];
        var rawResults = (raw && raw.results) || [];
        results.popup = feats.map(function (f, i) {
          return popupFeatureToItem(f, rawResults[i] ? rawResults[i].layerName : '');
        });
        done();
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
    state.identifyPanelId = options.identifyPanelId || null;
    var vfrm = options.style === 'vfrm';
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
        minZoom: vfrm ? 7 : 0,
        maxZoom: vfrm ? 17 : 18,
        zoomControl: !options.preview,
        attributionControl: true,
        dragging: !options.preview,
        scrollWheelZoom: !options.preview,
        doubleClickZoom: !options.preview,
        boxZoom: !options.preview,
        keyboard: !options.preview
      });
      state.map = map;

      if (vfrm) {
        buildVfrmLayers(map);
        if (!options.center) map.fitBounds(VFRM_BOUNDS);
      } else {
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
      }

      map.on('click', function (e) {
        if (state.preview) return;
        if (state.pickMode) {
          setFlightArea(e.latlng.lat, e.latlng.lng, getPickRadiusM());
          return;
        }
        if (vfrm) identifyVfrm(e.latlng); else identifyAt(e.latlng);
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

      if (!vfrm) {
        worldTopo.on('loaderror', function () {
          showError('Topografia Esri nie je dostupná — zapínam otvorenú mapu.');
          if (!map.hasLayer(osm)) osm.addTo(map);
        });
        cache.on('loaderror', function () {
          showError('VFR vrstva LPS nie je dostupná. Podklad (topo) ostáva viditeľný.');
        });
      }

      setTimeout(function () { map.invalidateSize(); }, 80);
      setTimeout(function () { if (state.map) state.map.invalidateSize(); }, 300);
      if (!options.preview && !vfrm) buildLegend(document.getElementById('map-legend'));

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
    if (!state.vfrm && cache && state.map.hasLayer(cache) && cache.bringToFront) cache.bringToFront();
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
    if (state.vfrm) {
      (state.vfrm.timers || []).forEach(function (t) { clearInterval(t); });
      state.vfrm = null;
    }
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
    if (!state.map) return;
    if (state.vfrm) state.map.fitBounds(VFRM_BOUNDS);
    else state.map.setView([48.55, 17.85], 9);
  }

  global.IsSuasDroneMap = {
    initMap: initMap,
    toggleLayer: toggleLayer,
    destroyMap: destroyMap,
    locateMe: locateMe,
    searchPlace: searchPlace,
    goHome: goHome,
    /** Aktuálna inštancia Leaflet mapy (napr. na doplnenie vrstiev zo stránky). */
    getMap: function () { return state.map; },
    setPickMode: setPickMode,
    setFlightArea: setFlightArea,
    setFlightRadius: setFlightRadius,
    getFlightArea: getFlightArea,
    clearFlightArea: clearFlightArea,
    /** Export pre dokumentáciu / UI */
    popupLayers: POPUP_LAYERS.slice()
  };
})(typeof window !== 'undefined' ? window : globalThis);
