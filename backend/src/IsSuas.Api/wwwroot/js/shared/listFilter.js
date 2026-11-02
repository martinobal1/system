// Univerzálny panel filtrov a vyhľadávania nad už vykreslenou tabuľkou.
// Riadky ostávajú v DOM (s pôvodnými obslužnými funkciami) — filter ich len skrýva.
//
// Použitie:
//   html: filterBarHtml('fl', [{ key: 'q', type: 'search', placeholder: 'Hľadať…' },
//                              { key: 'status', type: 'select', label: 'Stav' },
//                              { key: 'date', type: 'daterange', label: 'Dátum' }])
//   riadok: '<tr ' + filterAttrs({ q: 'text na hľadanie', status: ['Closed', 'Uzavretý'], date: iso }) + '>'
//   po vykreslení: bindFilterBar('fl', { rows: 'tr.flight-row', stateKey: 'flights', colspan: 9 })
import { state } from '../core/state.js';

export function foldText(s) {
  return String(s == null ? '' : s).toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
}
function escA(s) {
  return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}
function isoDay(v) {
  if (!v) return '';
  var d = new Date(v);
  if (isNaN(d)) return '';
  return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
}

/** Atribúty riadku: q = text, select polia ako [hodnota, popis] alebo hodnota, date = ISO dátum. */
export function filterAttrs(obj) {
  var out = [];
  Object.keys(obj || {}).forEach(function (k) {
    var v = obj[k];
    if (k === 'q') { out.push('data-f-q="' + escA(foldText(v)) + '"'); return; }
    if (k === 'date') { out.push('data-f-date="' + isoDay(v) + '"'); return; }
    if (Array.isArray(v)) {
      out.push('data-f-' + k + '="' + escA(v[0] == null ? '' : v[0]) + '"');
      out.push('data-f-' + k + '-l="' + escA(v[1] == null ? v[0] : v[1]) + '"');
    } else {
      out.push('data-f-' + k + '="' + escA(v == null ? '' : v) + '"');
    }
  });
  return out.join(' ');
}

var ICON_SEARCH = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>';

export function filterBarHtml(id, fields) {
  var html = '<div class="lf-bar" id="lf-' + id + '">';
  fields.forEach(function (f) {
    if (f.type === 'search') {
      html += '<label class="lf-search">' + ICON_SEARCH +
        '<input type="search" data-lf="' + f.key + '" placeholder="' + escA(f.placeholder || 'Hľadať…') + '" autocomplete="off"></label>';
    } else if (f.type === 'select') {
      html += '<label class="lf-field"><span>' + escA(f.label) + '</span><select data-lf="' + f.key + '"><option value="">Všetko</option></select></label>';
    } else if (f.type === 'daterange') {
      html += '<label class="lf-field"><span>' + escA(f.label || 'Od') + ' od</span><input type="date" data-lf="from"></label>' +
        '<label class="lf-field"><span>do</span><input type="date" data-lf="to"></label>';
    }
  });
  html += '<div class="lf-tail"><span class="lf-count"></span><button type="button" class="lf-reset" hidden>Zrušiť filtre</button></div></div>';
  return html;
}

/**
 * opts: { rows: selektor riadkov, stateKey, colspan, pageSize (default 150), presets: { key: value } }
 * Vráti funkciu apply(), ktorú možno zavolať po zmene.
 */
export function bindFilterBar(id, opts) {
  var bar = document.getElementById('lf-' + id);
  if (!bar) return function () {};
  var rows = Array.prototype.slice.call(document.querySelectorAll(opts.rows));
  var tbody = rows.length ? rows[0].parentNode : null;
  var pageSize = opts.pageSize || 150;
  var limit = pageSize;
  state.listFilters = state.listFilters || {};
  var saved = state.listFilters[opts.stateKey] = Object.assign({}, state.listFilters[opts.stateKey] || {}, opts.presets || {});

  // Možnosti selectov z hodnôt v riadkoch (zoradené podľa popisu)
  bar.querySelectorAll('select[data-lf]').forEach(function (sel) {
    var key = sel.dataset.lf, seen = {};
    rows.forEach(function (tr) {
      var v = tr.getAttribute('data-f-' + key);
      if (v == null || v === '' || seen[v]) return;
      seen[v] = tr.getAttribute('data-f-' + key + '-l') || v;
    });
    Object.keys(seen).sort(function (a, b) { return String(seen[a]).localeCompare(String(seen[b]), 'sk'); }).forEach(function (v) {
      var o = document.createElement('option');
      o.value = v; o.textContent = seen[v];
      sel.appendChild(o);
    });
  });
  bar.querySelectorAll('[data-lf]').forEach(function (el) {
    if (saved[el.dataset.lf] != null) el.value = saved[el.dataset.lf];
    el.addEventListener(el.tagName === 'SELECT' || el.type === 'date' ? 'change' : 'input', function () {
      saved[el.dataset.lf] = el.value;
      limit = pageSize;
      apply();
    });
  });

  // pomocné riadky: „nič nenájdené“ a „zobraziť viac“
  var emptyRow = null, moreRow = null;
  if (tbody) {
    emptyRow = document.createElement('tr');
    emptyRow.className = 'lf-empty';
    emptyRow.innerHTML = '<td colspan="' + (opts.colspan || 1) + '">Nič nevyhovuje zadaným filtrom.</td>';
    moreRow = document.createElement('tr');
    moreRow.className = 'lf-more';
    moreRow.innerHTML = '<td colspan="' + (opts.colspan || 1) + '"><button type="button">Zobraziť ďalšie</button></td>';
    tbody.appendChild(emptyRow);
    tbody.appendChild(moreRow);
    moreRow.querySelector('button').onclick = function () { limit += pageSize; apply(); };
  }
  var resetBtn = bar.querySelector('.lf-reset');
  resetBtn.onclick = function () {
    bar.querySelectorAll('[data-lf]').forEach(function (el) { el.value = ''; saved[el.dataset.lf] = ''; });
    limit = pageSize;
    apply();
  };

  function apply() {
    var words = foldText(saved.q || '').trim().split(/\s+/).filter(Boolean);
    var selects = [];
    bar.querySelectorAll('select[data-lf]').forEach(function (sel) { if (sel.value) selects.push([sel.dataset.lf, sel.value]); });
    var from = saved.from || '', to = saved.to || '';
    var matched = 0, shown = 0;
    rows.forEach(function (tr) {
      var ok = true;
      if (words.length) {
        var hay = tr.getAttribute('data-f-q') || '';
        for (var i = 0; i < words.length && ok; i++) if (hay.indexOf(words[i]) < 0) ok = false;
      }
      for (var j = 0; j < selects.length && ok; j++) if ((tr.getAttribute('data-f-' + selects[j][0]) || '') !== selects[j][1]) ok = false;
      if (ok && (from || to)) {
        var d = tr.getAttribute('data-f-date') || '';
        if (!d || (from && d < from) || (to && d > to)) ok = false;
      }
      if (ok) matched++;
      var visible = ok && matched <= limit;
      if (visible) shown++;
      tr.hidden = !visible;
      // detailové podriadky (napr. rozbalené riadky) skryjeme spolu s riadkom
      var next = tr.nextElementSibling;
      if (next && next.classList.contains('lf-child')) next.hidden = !visible;
    });
    var active = !!(words.length || selects.length || from || to);
    if (emptyRow) emptyRow.hidden = matched > 0 || !rows.length;
    if (moreRow) {
      moreRow.hidden = matched <= limit;
      moreRow.querySelector('button').textContent = 'Zobraziť ďalšie (' + Math.min(pageSize, matched - limit) + ' z ' + (matched - limit) + ')';
    }
    bar.querySelector('.lf-count').textContent = active
      ? 'Nájdené: ' + matched + ' z ' + rows.length
      : rows.length + (rows.length === 1 ? ' záznam' : rows.length >= 2 && rows.length <= 4 ? ' záznamy' : ' záznamov');
    resetBtn.hidden = !active;
    bar.classList.toggle('is-active', active);
    if (opts.onApply) opts.onApply(matched, active);
  }
  apply();
  return apply;
}
