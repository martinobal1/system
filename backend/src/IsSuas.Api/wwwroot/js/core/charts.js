export var UI_PALETTE = [
  '#0f4c6e', '#1a6f9a', '#2b7bb9', '#0d8f7a', '#3aa687',
  '#c47b1a', '#b42318', '#5a6d7e', '#6b4f9a', '#2d6a4f', '#9a3412'
];

export function paletteColor(index) {
  return UI_PALETTE[Math.abs(Number(index) || 0) % UI_PALETTE.length];
}

export function colorForKey(key) {
  var s = String(key == null ? '' : key);
  var h = 0;
  for (var i = 0; i < s.length; i++) h = ((h << 5) - h) + s.charCodeAt(i);
  return paletteColor(Math.abs(h));
}

export function levelColor(level) {
  var map = { 0: '#0f4c6e', 1: '#2b7bb9', 2: '#0d8f7a', 3: '#c47b1a', 4: '#6b4f9a' };
  return map[level] || paletteColor(level + 3);
}

export function swatchHtml(color, cls) {
  return '<span class="swatch' + (cls ? ' ' + cls : '') +
    '" style="background:' + color + '"></span>';
}

export function entityLabelHtml(text, key) {
  var color = colorForKey(key || text);
  return '<span class="entity-label">' + swatchHtml(color) +
    '<span>' + text + '</span></span>';
}

export function renderFlightHoursPanel(title, totalHours, slices, subtitle, unitLabel) {
  var list = slices || [];
  var unit = unitLabel || 'hodin';
  if (!list.length) {
    return '<div class="chart-panel"><h2>' + title + '</h2>' +
      '<p class="muted">Zatiaľ žiadne dáta.</p></div>';
  }
  var sum = list.reduce(function (a, s) { return a + Number(s.hours || 0); }, 0) || 1;
  var cx = 100, cy = 100, r = 78, r0 = 44;
  var angle = -Math.PI / 2;
  var paths = '';
  function arcPath(a1, a2) {
    var x1 = cx + r * Math.cos(a1), y1 = cy + r * Math.sin(a1);
    var x2 = cx + r * Math.cos(a2), y2 = cy + r * Math.sin(a2);
    var xi1 = cx + r0 * Math.cos(a1), yi1 = cy + r0 * Math.sin(a1);
    var xi2 = cx + r0 * Math.cos(a2), yi2 = cy + r0 * Math.sin(a2);
    var large = (a2 - a1) > Math.PI ? 1 : 0;
    return 'M ' + x1.toFixed(2) + ' ' + y1.toFixed(2) +
      ' A ' + r + ' ' + r + ' 0 ' + large + ' 1 ' + x2.toFixed(2) + ' ' + y2.toFixed(2) +
      ' L ' + xi2.toFixed(2) + ' ' + yi2.toFixed(2) +
      ' A ' + r0 + ' ' + r0 + ' 0 ' + large + ' 0 ' + xi1.toFixed(2) + ' ' + yi1.toFixed(2) + ' Z';
  }
  list.forEach(function (s, i) {
    var portion = Number(s.hours || 0) / sum;
    var a1 = angle;
    var a2 = angle + portion * Math.PI * 2;
    if (a2 - a1 < 0.02) a2 = a1 + 0.02;
    // SVG neumí plný 360° oblúk jedným path — rozdel na 2 polkruhy
    var d;
    if (a2 - a1 >= Math.PI * 2 - 0.001) {
      var mid = a1 + Math.PI;
      d = arcPath(a1, mid) + ' ' + arcPath(mid, a1 + Math.PI * 2);
    } else {
      d = arcPath(a1, a2);
    }
    var valText = unit === 'hodin'
      ? Number(s.hours).toFixed(1) + ' h'
      : String(Math.round(Number(s.hours))) + ' ' + unit;
    var pct = ((Number(s.hours || 0) / sum) * 100).toFixed(1);
    var tip = s.label + ' — ' + valText + ' (' + pct + ' %)';
    var sliceColor = s.color || colorForKey(s.key || s.label || i);
    paths += '<path class="pie-slice" data-idx="' + i + '" data-tip="' + tip.replace(/"/g, '&quot;') +
      '" data-color="' + sliceColor + '" d="' + d + '" fill="' +
      sliceColor + '"></path>';
    angle = a2;
  });
  var legend = list.map(function (s, i) {
    var val = unit === 'hodin' ? Number(s.hours).toFixed(1) + ' h' : String(Number(s.hours));
    var pct = ((Number(s.hours || 0) / sum) * 100).toFixed(1);
    var tip = s.label + ' — ' + val + (unit === 'hodin' ? '' : ' ' + unit) + ' (' + pct + ' %)';
    var sliceColor = s.color || colorForKey(s.key || s.label || i);
    return '<div class="pie-legend-row" data-idx="' + i + '" data-tip="' + tip.replace(/"/g, '&quot;') +
      '" data-color="' + sliceColor + '">' +
      '<span class="pie-swatch" style="background:' + sliceColor +
      '"></span><span>' + s.label + '</span><b>' + val + '</b></div>';
  }).join('');
  var centerVal = unit === 'hodin'
    ? Number(totalHours || sum).toFixed(1)
    : String(Math.round(Number(totalHours || sum)));
  return '<div class="chart-panel"><h2>' + title + '</h2>' +
    '<p class="muted" style="margin:0">' + (subtitle || '') + '</p>' +
    '<div class="pie-layout">' +
    '<svg viewBox="0 0 200 200" role="img" aria-label="' + title + '">' + paths +
    '<circle cx="100" cy="100" r="40" fill="#fff"/>' +
    '<text x="100" y="96" text-anchor="middle" class="pie-center-label">' +
    centerVal + '</text>' +
    '<text x="100" y="114" text-anchor="middle" class="pie-center-sub">' + unit + '</text>' +
    '</svg>' +
    '<div class="pie-legend">' + legend + '</div></div></div>';
}

export function renderActivityBarsPanel(d) {
  var rows = [
    { label: 'Odletené lety', value: d.totalTakeoffs || 0, color: '#0d8f7a' },
    { label: 'Operácie dnes', value: d.operationsToday || 0, color: '#2b7bb9' },
    { label: 'Poruchy', value: d.openDefects || 0, color: '#c47b1a' },
    { label: 'Incidenty', value: d.openIncidents || 0, color: '#b42318' },
    { label: 'Neprečítané', value: d.unreadNotifications || 0, color: '#5b6b7c' },
    { label: 'UAS používané', value: (d.fleet && d.fleet.inUse) || 0, color: '#0d8f7a' }
  ];
  var max = Math.max.apply(null, rows.map(function (r) { return r.value; }).concat([1]));
  var html = rows.map(function (r) {
    var pct = Math.max(4, Math.round((r.value / max) * 100));
    var tip = r.label + ': ' + r.value;
    return '<div class="bar-row" data-tip="' + tip.replace(/"/g, '&quot;') +
      '" data-color="' + r.color + '"><span>' + r.label + '</span>' +
      '<div class="bar-track"><div class="bar-fill" style="width:' + pct + '%;background:' +
      r.color + '"></div></div><b>' + r.value + '</b></div>';
  }).join('');
  return '<div class="chart-panel"><h2>Aktivita systému</h2>' +
    '<p class="muted" style="margin:0">Vrátane odletených letov z denníka</p>' +
    '<div class="bar-chart">' + html + '</div></div>';
}

export function renderFleetModelsPanel(models, total, opts) {
  var options = opts || {};
  var subtitle = options.subtitle || 'Zoskupenie evidovaných UAS podľa modelu';
  var all = models || [];
  if (!all.length) {
    return '<div class="chart-panel"><h2>Typy dronov vo flotile</h2>' +
      '<p class="muted">Zatiaľ žiadne evidované UAS.</p></div>';
  }
  var limit = Number(options.limit) || 0;
  var list = limit > 0 ? all.slice(0, limit) : all;
  var hiddenModels = all.length - list.length;
  var usedColor = '#0d8f7a';
  var idleColor = '#8a97a6';
  var decomColor = '#d92d20';
  var anyDecom = all.some(function (m) { return Number(m.decommissioned || 0) > 0; });
  var max = Math.max.apply(null, list.map(function (m) { return Number(m.count || 0); }).concat([1]));
  var rows = list.map(function (m) {
    var used = Number(m.inUse || 0);
    var idle = Number(m.idle || 0);
    var decom = Number(m.decommissioned || 0);
    var count = Number(m.count || used + idle + decom);
    var usedPct = Math.max(0, Math.round((used / max) * 100));
    var idlePct = Math.max(0, Math.round((idle / max) * 100));
    if (used > 0 && usedPct === 0) usedPct = 2;
    if (idle > 0 && idlePct === 0) idlePct = 2;
    var decomPct = Math.max(0, Math.round((decom / max) * 100));
    if (decom > 0 && decomPct === 0) decomPct = 2;
    var label = m.label || m.model || '—';
    var mfr = m.manufacturer && m.manufacturer !== '—' ? m.manufacturer : '';
    var tip = label + ' — ' + count + ' ks (používané ' + used + ', nepoužívané ' + idle +
      (decom ? ', vyradené ' + decom : '') + ')';
    return '<div class="bar-row model-bar-row" data-tip="' + tip.replace(/"/g, '&quot;') +
      '" data-color="' + usedColor + '">' +
      '<span class="model-bar-name">' + label +
      (mfr ? '<span class="model-bar-mfr">' + mfr + '</span>' : '') +
      '</span>' +
      '<div class="bar-track is-stack">' +
      (used ? '<div class="bar-fill" style="width:' + usedPct + '%;background:' + usedColor + '"></div>' : '') +
      (idle ? '<div class="bar-fill" style="width:' + idlePct + '%;background:' + idleColor + '"></div>' : '') +
      (decom ? '<div class="bar-fill" style="width:' + decomPct + '%;background:' + decomColor + '"></div>' : '') +
      '</div><span class="model-bar-counts"><b>' + count + '</b>' +
      '<span class="model-bar-detail" style="font-size:0.78rem;font-weight:400;color:var(--muted)">' +
      ' (' + used + '/' + idle + (anyDecom ? '/' + decom : '') + ')' +
      '</span></span></div>';
  }).join('');
  return '<div class="chart-panel"><h2>Typy dronov vo flotile</h2>' +
    '<p class="muted" style="margin:0">' + subtitle + ' · ' +
    (total != null ? total : all.reduce(function (a, m) { return a + Number(m.count || 0); }, 0)) +
    ' ks celkom' + (hiddenModels > 0 ? ' · zobrazených ' + list.length + ' najpočetnejších typov' : '') +
    '</p>' +
    '<div class="model-legend">' +
    '<span><i class="dot" style="background:' + usedColor + '"></i>Používané (s náletom)</span>' +
    '<span><i class="dot" style="background:' + idleColor + '"></i>Nepoužívané (bez letu)</span>' +
    '<span><i class="dot" style="background:' + decomColor + '"></i>Vyradené</span>' +
    '</div>' +
    '<div class="bar-chart">' + rows + '</div>' +
    (hiddenModels > 0
      ? '<p class="muted" style="margin:0;font-size:0.8rem">Ďalších ' + hiddenModels +
        ' typov s menším počtom kusov nájdete v tabuľke nižšie.</p>'
      : '') +
    '</div>';
}

// Používané = má nálet a je v prevádzke; rovnaké pravidlá ako dashboard v ReportingService.
export function fleetModelsFromUasList(list) {
  function hasStatus(u, name, code) {
    return u.status === name || u.status === code;
  }
  function isDecommissioned(u) { return hasStatus(u, 'Decommissioned', 8); }
  function isUnavailable(u) {
    return hasStatus(u, 'InMaintenance', 4) || hasStatus(u, 'Damaged', 6) || hasStatus(u, 'Lost', 7);
  }
  function isInUse(u) {
    if (isDecommissioned(u) || isUnavailable(u)) return false;
    if (!hasStatus(u, 'InService', 2) && !hasStatus(u, 'Registered', 1)) return false;
    return Number(u.totalFlightCount || 0) > 0;
  }

  var groups = {};
  (list || []).forEach(function (u) {
    var maker = String(u.manufacturer || '').trim() || '—';
    var model = String(u.model || '').trim() || 'Neznámy model';
    var key = maker + '|' + model;
    if (!groups[key])
      groups[key] = { label: model, manufacturer: maker, model: model, count: 0, inUse: 0, idle: 0, decommissioned: 0 };
    var group = groups[key];
    group.count++;
    if (isInUse(u)) group.inUse++;
    else if (isDecommissioned(u)) group.decommissioned++;
    else if (!isDecommissioned(u) && !isUnavailable(u)) group.idle++;
  });

  return Object.keys(groups).map(function (k) { return groups[k]; }).sort(function (a, b) {
    return b.count - a.count || a.label.localeCompare(b.label, 'sk');
  });
}

export function bindChartTooltips(root) {
  var tip = document.getElementById('chart-tip');
  if (!tip) {
    tip = document.createElement('div');
    tip.id = 'chart-tip';
    tip.className = 'chart-tip';
    document.body.appendChild(tip);
  }
  function showTip(ev, el) {
    var text = el.getAttribute('data-tip') || '';
    var color = el.getAttribute('data-color') || '#2b7bb9';
    if (!text) return;
    tip.innerHTML = '<span class="tip-swatch" style="background:' + color + '"></span><strong>' +
      text.split(' — ')[0] + '</strong>' + (text.indexOf(' — ') >= 0 ? text.split(' — ').slice(1).join(' — ') : '');
    tip.classList.add('visible');
    var x = ev.clientX + 14;
    var y = ev.clientY + 14;
    if (x + 220 > window.innerWidth) x = ev.clientX - 220;
    if (y + 70 > window.innerHeight) y = ev.clientY - 60;
    tip.style.left = x + 'px';
    tip.style.top = y + 'px';
  }
  function hideTip() { tip.classList.remove('visible'); }

  root.querySelectorAll('.pie-layout').forEach(function (layout) {
    var slices = layout.querySelectorAll('.pie-slice');
    var rows = layout.querySelectorAll('.pie-legend-row');
    function setHot(idx) {
      layout.classList.add('is-hovering');
      slices.forEach(function (s) { s.classList.toggle('is-hot', s.getAttribute('data-idx') === idx); });
      rows.forEach(function (r) { r.classList.toggle('is-hot', r.getAttribute('data-idx') === idx); });
    }
    function clearHot() {
      layout.classList.remove('is-hovering');
      slices.forEach(function (s) { s.classList.remove('is-hot'); });
      rows.forEach(function (r) { r.classList.remove('is-hot'); });
    }
    function bindEl(el) {
      el.addEventListener('mouseenter', function (ev) {
        setHot(el.getAttribute('data-idx'));
        showTip(ev, el);
      });
      el.addEventListener('mousemove', function (ev) { showTip(ev, el); });
      el.addEventListener('mouseleave', function () {
        clearHot();
        hideTip();
      });
    }
    slices.forEach(bindEl);
    rows.forEach(bindEl);
  });

  root.querySelectorAll('.bar-row[data-tip]').forEach(function (el) {
    el.addEventListener('mouseenter', function (ev) { showTip(ev, el); });
    el.addEventListener('mousemove', function (ev) { showTip(ev, el); });
    el.addEventListener('mouseleave', hideTip);
  });

  root.querySelectorAll('.wx-chart').forEach(function (svg) {
    var focus = svg.querySelector('.wx-focus');
    var vline = svg.querySelector('.wx-vline');
    var dot = svg.querySelector('.wx-dot');
    svg.querySelectorAll('.wx-hit').forEach(function (hit) {
      hit.addEventListener('mouseenter', function (ev) {
        var x = hit.getAttribute('data-x');
        var y = hit.getAttribute('data-y');
        if (focus && vline && dot) {
          focus.style.display = '';
          vline.setAttribute('x1', x);
          vline.setAttribute('x2', x);
          dot.setAttribute('cx', x);
          dot.setAttribute('cy', y);
        }
        showTip(ev, hit);
      });
      hit.addEventListener('mousemove', function (ev) { showTip(ev, hit); });
      hit.addEventListener('mouseleave', function () {
        if (focus) focus.style.display = 'none';
        hideTip();
      });
    });
  });
}

export function renderWeatherChart(hourly) {
  var pts = (hourly || []).slice(0, 24);
  if (!pts.length) return '<p class="muted">Graf nedostupný</p>';
  var w = 560, h = 168, padL = 38, padR = 36, padT = 12, padB = 26;
  var temps = pts.map(function (p) { return Number(p.temperatureC); });
  var winds = pts.map(function (p) { return Number(p.windSpeedMs); });
  var tMin = Math.min.apply(null, temps);
  var tMax = Math.max.apply(null, temps);
  var vMin = Math.floor(tMin) - 1;
  var vMax = Math.ceil(tMax) + 1;
  if (vMax <= vMin) vMax = vMin + 4;
  var wMin = Math.min.apply(null, winds);
  var wMax = Math.max.apply(null, winds);
  var wPad = Math.max(0.4, (wMax - wMin) * 0.15);
  wMin = Math.max(0, wMin - wPad);
  wMax = wMax + wPad;
  if (wMax <= wMin) wMax = wMin + 2;

  function xAt(i) {
    return padL + (i / Math.max(pts.length - 1, 1)) * (w - padL - padR);
  }
  function yTemp(v) {
    return padT + (1 - (v - vMin) / (vMax - vMin || 1)) * (h - padT - padB);
  }
  function yWind(v) {
    return padT + (1 - (v - wMin) / (wMax - wMin || 1)) * (h - padT - padB);
  }

  var tempPath = temps.map(function (v, i) {
    return (i === 0 ? 'M' : 'L') + xAt(i).toFixed(1) + ' ' + yTemp(v).toFixed(1);
  }).join(' ');
  var windPath = winds.map(function (v, i) {
    return (i === 0 ? 'M' : 'L') + xAt(i).toFixed(1) + ' ' + yWind(v).toFixed(1);
  }).join(' ');
  var area = tempPath + ' L' + xAt(pts.length - 1).toFixed(1) + ' ' + (h - padB).toFixed(1) +
    ' L' + padL.toFixed(1) + ' ' + (h - padB).toFixed(1) + ' Z';

  var yTicks = '';
  var tickCount = 4;
  for (var ti = 0; ti <= tickCount; ti++) {
    var tv = vMin + (vMax - vMin) * (ti / tickCount);
    var wv = wMin + (wMax - wMin) * (ti / tickCount);
    var yy = yTemp(tv);
    yTicks += '<line x1="' + padL + '" y1="' + yy.toFixed(1) + '" x2="' + (w - padR) +
      '" y2="' + yy.toFixed(1) + '" stroke="#e2eaf0" stroke-width="1"/>';
    yTicks += '<text x="' + (padL - 6) + '" y="' + (yy + 3).toFixed(1) +
      '" text-anchor="end" fill="#2b7bb9" font-size="10" font-weight="600">' +
      Math.round(tv) + '°</text>';
    yTicks += '<text x="' + (w - padR + 6) + '" y="' + (yy + 3).toFixed(1) +
      '" text-anchor="start" fill="#0d8f7a" font-size="10" font-weight="600">' +
      wv.toFixed(1) + '</text>';
  }

  var xLabels = '';
  for (var i = 0; i < pts.length; i += 4) {
    var hh = new Date(pts[i].at).toLocaleTimeString('sk-SK', { hour: '2-digit', minute: '2-digit' });
    xLabels += '<text x="' + xAt(i).toFixed(1) + '" y="' + (h - 8) +
      '" text-anchor="middle" fill="#5a6d7e" font-size="10">' + hh + '</text>';
  }

  var dots = '';
  var hits = '';
  pts.forEach(function (p, i) {
    var time = new Date(p.at).toLocaleTimeString('sk-SK', { hour: '2-digit', minute: '2-digit' });
    var tip = time + ' — ' + Number(p.temperatureC).toFixed(1) + '°C · vietor ' +
      Number(p.windSpeedMs).toFixed(1) + ' m/s';
    var cx = xAt(i).toFixed(1);
    var cy = yTemp(p.temperatureC).toFixed(1);
    if (i % 2 === 0) {
      dots += '<circle cx="' + cx + '" cy="' + cy + '" r="2.4" fill="#2b7bb9"/>';
    }
    hits += '<circle class="wx-hit" cx="' + cx + '" cy="' + cy + '" r="11" fill="transparent" ' +
      'data-tip="' + tip.replace(/"/g, '&quot;') + '" data-color="#2b7bb9" ' +
      'data-x="' + cx + '" data-y="' + cy + '"></circle>';
  });

  var gid = 'tempFill-' + Math.abs(Math.round((temps[0] || 0) * 100 + pts.length));
  return '<svg class="wx-chart" viewBox="0 0 ' + w + ' ' + h +
    '" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Open-Meteo teplota a vietor">' +
    '<defs><linearGradient id="' + gid + '" x1="0" y1="0" x2="0" y2="1">' +
    '<stop offset="0%" stop-color="#2b7bb9" stop-opacity="0.32"/>' +
    '<stop offset="100%" stop-color="#2b7bb9" stop-opacity="0.03"/>' +
    '</linearGradient></defs>' +
    yTicks +
    '<path d="' + area + '" fill="url(#' + gid + ')"/>' +
    '<path d="' + tempPath + '" fill="none" stroke="#2b7bb9" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>' +
    '<path d="' + windPath + '" fill="none" stroke="#0d8f7a" stroke-width="2.1" stroke-dasharray="5 4" stroke-linecap="round" stroke-linejoin="round"/>' +
    dots +
    '<g class="wx-focus" style="display:none">' +
    '<line class="wx-vline" x1="0" y1="' + padT + '" x2="0" y2="' + (h - padB) +
    '" stroke="#0f4c6e" stroke-width="1.2" stroke-dasharray="3 3"/>' +
    '<circle class="wx-dot" cx="0" cy="0" r="4.5" fill="#2b7bb9" stroke="#fff" stroke-width="1.8"/>' +
    '</g>' +
    xLabels +
    hits +
    '</svg>';
}

export function renderWeatherPanel(w) {
  if (!w) {
    return '<div class="weather-panel"><h2>Počasie</h2>' +
      '<p class="muted">Počasie momentálne nedostupné.</p></div>';
  }
  var observed = new Date(w.observedAt).toLocaleString('sk-SK');
  var valid = new Date(w.validUntil).toLocaleTimeString('sk-SK', { hour: '2-digit', minute: '2-digit' });
  return '<div class="weather-panel">' +
    '<div class="weather-head">' +
    '<div><h2>Počasie</h2><p class="muted" style="margin:0.2rem 0 0">' +
    w.stationName + ' · ' + w.conditionLabel + '</p></div>' +
    '<div class="weather-source">' + w.sourceLabel + '<br/>meranie ' + observed +
    '<br/>platné do ' + valid + '</div></div>' +
    '<div class="weather-now">' +
    '<div class="weather-temp">' + w.temperatureC.toFixed(1) + '°C' +
    '<small>pocitovo ' + w.feelsLikeC.toFixed(1) + '°C</small></div>' +
    '<div class="weather-kpis">' +
    '<div>Vietor <strong>' + w.windSpeedMs.toFixed(1) + ' m/s</strong></div>' +
    '<div>Nárazy <strong>' + w.windGustMs.toFixed(1) + ' m/s</strong></div>' +
    '<div>Vlhkosť <strong>' + w.relativeHumidityPct + ' %</strong></div>' +
    '<div>Tlak <strong>' + w.pressureHpa.toFixed(0) + ' hPa</strong></div>' +
    '</div></div>' +
    '<div class="weather-advice">' + w.flightAdvice + '</div>' +
    '<div class="weather-chart-wrap">' + renderWeatherChart(w.hourly) +
    '<div class="weather-legend"><span><i class="leg-temp"></i>Teplota (°C)</span>' +
    '<span><i class="leg-wind"></i>Vietor (m/s)</span></div></div></div>';
}
