import { api } from './api.js';
import { navigate, pageFromPath } from './nav.js';
import { state } from './state.js';
import { confirmDialog } from './ui.js';

// Kos: tlacidlo "Vymazat" presunie zaznam do kosa (POST /api/v1/trash).
// Obnovit a natrvalo vymazat moze iba gestor na stranke Kos. O pravach rozhoduje vzdy server.

// Kategorie, ktore smu mazat aj piloti (uroven 3) — server im dovoli len vlastne zaznamy.
var OPEN_TO_PILOTS = { flights: true, operations: true, defects: true, incidents: true };

// Ktory detail sa ma po vymazani zavriet.
var DETAIL_STATE = {
  pilots: 'pilotDetailId',
  flights: 'flightDetailId',
  operations: 'operationDetailId',
  uas: 'uasDetailId',
  defects: 'defectDetailId'
};

function escTrash(s) {
  return String(s == null ? '' : s)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

export function canTrash(category) {
  var level = Number(state.accessLevel) || 3;
  if (level === 1 || level === 2) return true;
  return !!OPEN_TO_PILOTS[category];
}

/** HTML tlacidla "Vymazat". Klik obsluhuje initTrashButtons (jeden spolocny listener). */
export function trashButtonHtml(category, id, title, opts) {
  if (!id || !canTrash(category)) return '';
  var o = opts || {};
  return '<button type="button" class="' + (o.compact ? 'icon-btn danger' : 'btn-danger') + '"' +
    ' data-trash-category="' + escTrash(category) + '" data-trash-id="' + escTrash(id) + '"' +
    ' data-trash-title="' + escTrash(title || '') + '" title="Presunúť do koša">' +
    '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
    '<path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/></svg>' +
    '<span>' + (o.label || 'Vymazať') + '</span></button>';
}

/**
 * Dialog so zaskrtavacim zoznamom vazieb (napr. lety pilota), ktore sa zmazu spolu so zaznamom.
 * opts: { title, intro, data (TrashDependentsDto), confirmLabel(n), typedConfirm, reasonLabel, onSubmit(items, text) }
 * onSubmit vracia Promise; ak zlyha, chyba sa ukaze v dialogu a dialog zostane otvoreny.
 */
export function openDependentsDialog(opts) {
  var o = opts || {};
  var data = o.data || { items: [], notDeletable: [] };
  var items = data.items || [];
  var blocked = (data.notDeletable || []).length > 0;

  var groups = {};
  var order = [];
  items.forEach(function (it, idx) {
    if (!groups[it.category]) { groups[it.category] = { name: it.categoryNameSk, rows: [] }; order.push(it.category); }
    groups[it.category].rows.push({ it: it, idx: idx });
  });

  var listHtml = order.map(function (cat) {
    var g = groups[cat];
    return '<div class="trash-dep-group">' +
      '<label class="trash-dep-head"><input type="checkbox" data-dep-all="' + escTrash(cat) + '" checked /> ' +
      '<b>' + escTrash(g.name) + '</b> <span class="muted">(' + g.rows.length + ')</span></label>' +
      '<ul>' + g.rows.map(function (r) {
        return '<li style="padding-left:' + ((r.it.depth || 1) - 1) * 1.1 + 'rem"><label>' +
          '<input type="checkbox" data-dep-idx="' + r.idx + '" data-dep-cat="' + escTrash(cat) + '" checked /> ' +
          escTrash(r.it.title) + (r.it.inTrash ? ' <span class="trash-dep-badge">v koši</span>' : '') +
          ((r.it.depth || 1) > 1 ? ' <span class="muted">(väzba ďalej)</span>' : '') +
          '</label></li>';
      }).join('') + '</ul></div>';
  }).join('');

  var wrap = document.createElement('div');
  wrap.className = 'ui-dialog-backdrop';
  wrap.innerHTML =
    '<form class="ui-dialog trash-dep-dialog" role="dialog" aria-modal="true">' +
    '<div class="ui-dialog-icon is-danger" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/></svg></div>' +
    '<h3>' + escTrash(o.title || 'Vymazať?') + '</h3>' +
    '<div class="ui-dialog-msg">' + (o.intro || '') + '</div>' +
    (items.length ? '<div class="trash-dep-tools"><button type="button" class="secondary" data-dep-toggle="1">Označiť všetko</button>' +
      '<button type="button" class="secondary" data-dep-toggle="0">Zrušiť označenie</button></div>' +
      '<div class="trash-dep-list">' + listHtml + '</div>' : '') +
    (blocked ? '<div class="trash-dep-warn"><b>Toto sa nemaže, preto záznam teraz nepôjde vymazať:</b><ul>' +
      data.notDeletable.map(function (t) { return '<li>' + escTrash(t) + '</li>'; }).join('') + '</ul></div>' : '') +
    (o.reasonLabel ? '<label class="ui-dialog-reason">' + escTrash(o.reasonLabel) +
      '<input name="reason" autocomplete="off" placeholder="' + escTrash(o.reasonPlaceholder || '') + '"' + (o.typedConfirm ? ' required' : '') + ' /></label>' : '') +
    '<p class="error hidden" data-dep-error></p>' +
    '<div class="ui-dialog-actions">' +
    '<button type="button" class="secondary" data-act="cancel">Zrušiť</button>' +
    '<button type="submit" class="btn-solid-danger" data-dep-submit' + (blocked ? ' disabled' : '') + '></button>' +
    '</div></form>';
  document.body.appendChild(wrap);

  var form = wrap.querySelector('form');
  var submitBtn = wrap.querySelector('[data-dep-submit]');
  var errEl = wrap.querySelector('[data-dep-error]');
  var input = wrap.querySelector('input[name="reason"]');

  function selected() {
    return Array.prototype.slice.call(wrap.querySelectorAll('[data-dep-idx]'))
      .filter(function (cb) { return cb.checked; })
      .map(function (cb) { return items[Number(cb.getAttribute('data-dep-idx'))]; });
  }
  function refresh() {
    var n = selected().length;
    submitBtn.textContent = o.confirmLabel ? o.confirmLabel(n) : 'Vymazať';
    order.forEach(function (cat) {
      var boxes = wrap.querySelectorAll('[data-dep-cat="' + cat + '"]');
      var on = Array.prototype.filter.call(boxes, function (b) { return b.checked; }).length;
      var all = wrap.querySelector('[data-dep-all="' + cat + '"]');
      all.checked = on === boxes.length;
      all.indeterminate = on > 0 && on < boxes.length;
    });
  }
  function close() {
    document.removeEventListener('keydown', onKey);
    wrap.remove();
  }
  function onKey(ev) { if (ev.key === 'Escape') close(); }
  document.addEventListener('keydown', onKey);

  wrap.addEventListener('change', function (ev) {
    var t = ev.target;
    if (t.hasAttribute('data-dep-all')) {
      var cat = t.getAttribute('data-dep-all');
      wrap.querySelectorAll('[data-dep-cat="' + cat + '"]').forEach(function (b) { b.checked = t.checked; });
    }
    refresh();
  });
  wrap.querySelectorAll('[data-dep-toggle]').forEach(function (btn) {
    btn.onclick = function () {
      var on = btn.getAttribute('data-dep-toggle') === '1';
      wrap.querySelectorAll('[data-dep-idx]').forEach(function (b) { b.checked = on; });
      refresh();
    };
  });
  wrap.addEventListener('click', function (ev) { if (ev.target === wrap) close(); });
  wrap.querySelector('[data-act="cancel"]').onclick = close;
  form.onsubmit = async function (ev) {
    ev.preventDefault();
    if (blocked) return;
    errEl.classList.add('hidden');
    submitBtn.disabled = true;
    try {
      await o.onSubmit(selected(), input ? input.value.trim() : '');
      close();
    } catch (err) {
      errEl.textContent = err.message || 'Akcia zlyhala.';
      errEl.classList.remove('hidden');
      submitBtn.disabled = false;
    }
  };
  refresh();
  setTimeout(function () { (input || submitBtn).focus(); }, 20);
}

export async function moveToTrashFlow(category, id, title) {
  var deps = await api('/api/v1/trash/dependents?category=' + encodeURIComponent(category) + '&id=' + encodeURIComponent(id));
  var hasDeps = (deps.items || []).length > 0 || (deps.notDeletable || []).length > 0;

  if (!hasDeps) {
    var res = await confirmDialog({
      title: 'Vymazať?',
      message: '<p><b>' + escTrash(title || deps.title || 'Záznam') + '</b> sa presunie do koša a zmizne zo všetkých zoznamov.</p>' +
        '<p class="muted">Gestor ho môže v koši obnoviť alebo natrvalo vymazať.</p>',
      confirmLabel: 'Vymazať',
      danger: true,
      reasonLabel: 'Dôvod (nepovinné, zapíše sa do auditu)',
      reasonPlaceholder: 'napr. duplicitný záznam'
    });
    if (!res) return false;
    await api('/api/v1/trash', {
      method: 'POST',
      body: JSON.stringify({ category: category, id: id, reason: res.reason || null })
    });
    return true;
  }

  return new Promise(function (resolve) {
    openDependentsDialog({
      title: 'Vymazať „' + (deps.title || title || 'záznam') + '“?',
      intro: '<p>Na tento záznam sa viažu ďalšie záznamy. Označte tie, ktoré sa majú presunúť do koša spolu s ním.</p>' +
        '<p class="muted">Ak niečo neoznačíte, záznam sa nevymaže. Všetko sa dá v koši obnoviť.</p>',
      data: deps,
      reasonLabel: 'Dôvod (nepovinné, zapíše sa do auditu)',
      reasonPlaceholder: 'napr. pilot odišiel z útvaru',
      confirmLabel: function (n) { return n > 0 ? 'Vymazať vybrané (' + n + ') aj tento záznam' : 'Vymazať len tento záznam'; },
      onSubmit: async function (items, reason) {
        var batch = items.map(function (it) { return { category: it.category, id: it.id }; });
        batch.push({ category: category, id: id });
        await api('/api/v1/trash/batch', {
          method: 'POST',
          body: JSON.stringify({ items: batch, reason: reason || null })
        });
        resolve(true);
      }
    });
    // Zatvorenie bez akcie: dialog sa odstrani z DOM — Promise ostane neuzavrety, co nevadi (nic sa nedeje).
  });
}

function showTrashError(message) {
  confirmDialog({
    title: 'Nedá sa vymazať',
    message: '<p>' + escTrash(message) + '</p>',
    confirmLabel: 'Rozumiem'
  });
}

export function initTrashButtons() {
  // Capture faza: tlacidla byvaju v klikatelnych riadkoch tabuliek, ktore by inak otvorili detail.
  document.addEventListener('click', async function (ev) {
    var btn = ev.target && ev.target.closest ? ev.target.closest('[data-trash-category]') : null;
    if (!btn) return;
    ev.preventDefault();
    ev.stopPropagation();
    var category = btn.getAttribute('data-trash-category');
    try {
      var done = await moveToTrashFlow(category, btn.getAttribute('data-trash-id'), btn.getAttribute('data-trash-title'));
      if (!done) return;
      if (DETAIL_STATE[category]) state[DETAIL_STATE[category]] = null;
      var page = pageFromPath(window.location.pathname);
      // Vymazaný detail nahradíme zoznamom (bez kroku v histórii, aby Späť neotváral zmazaný záznam).
      if (page) navigate(page, { replaceUrl: true });
    } catch (err) {
      showTrashError(err.message || 'Vymazanie zlyhalo.');
    }
  }, true);
}
