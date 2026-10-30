import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { openDependentsDialog } from '../core/trash.js';
import { confirmDialog, content, pageHead } from '../core/ui.js';

// Kos (iba gestor): vymazane zaznamy podla kategorii, obnovenie a trvale vymazanie — jednotlivo aj hromadne.

function escBin(s) {
  return String(s == null ? '' : s)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

export async function renderTrash() {
  var summary;
  try {
    summary = await api('/api/v1/trash/summary');
  } catch (err) {
    content.innerHTML = '<div class="error">' + escBin(err.message || 'Kôš nie je dostupný.') + '</div>';
    return;
  }

  var total = summary.reduce(function (s, c) { return s + c.count; }, 0);
  var active = state.trashCategory;
  if (!active || !summary.some(function (c) { return c.category === active; })) {
    var firstWithItems = summary.find(function (c) { return c.count > 0; });
    active = (firstWithItems || summary[0] || {}).category;
  }
  state.trashCategory = active;

  var items = active ? await api('/api/v1/trash?category=' + encodeURIComponent(active)) : [];
  var byId = {};
  items.forEach(function (t) { byId[t.id] = t; });

  var tabs = summary.map(function (c) {
    return '<button type="button" class="reg-tab' + (c.category === active ? ' active' : '') + '" data-bin-tab="' + escBin(c.category) + '">' +
      escBin(c.nameSk) + ' <span class="tab-count">' + c.count + '</span></button>';
  }).join('');

  var rows = items.map(function (t) {
    return '<tr>' +
      '<td class="bin-check"><input type="checkbox" data-bin-select="' + t.id + '" aria-label="Označiť" /></td>' +
      '<td><b>' + escBin(t.title) + '</b></td>' +
      '<td>' + escBin(t.deletedBy) + '</td>' +
      '<td>' + new Date(t.deletedAt).toLocaleString('sk-SK') + '</td>' +
      '<td>' + escBin(t.reason || '—') + '</td>' +
      '<td><div class="row-actions-end">' +
      '<button type="button" class="secondary" data-bin-restore="' + t.id + '">Obnoviť</button> ' +
      '<button type="button" class="btn-danger" data-bin-purge="' + t.id + '">Vymazať natrvalo</button>' +
      '</div></td></tr>';
  }).join('') || '<tr><td colspan="6">V tejto kategórii je kôš prázdny.</td></tr>';

  content.innerHTML = '<div class="stack">' +
    pageHead('Kôš', 'Vymazané záznamy. Obnovením sa vrátia na pôvodné miesto, trvalé vymazanie sa nedá vrátiť.',
      '<span class="chip chip-soft">Spolu ' + total + '</span>') +
    '<div class="reg-tabs">' + tabs + '</div>' +
    '<div class="toolbar bin-bulk">' +
    '<button type="button" class="secondary" id="bin-restore-sel" disabled>Obnoviť vybrané</button>' +
    '<button type="button" class="btn-danger" id="bin-purge-sel" disabled>Vymazať natrvalo vybrané</button>' +
    '</div>' +
    '<div id="bin-msg"></div>' +
    '<div class="chart-panel table-wide"><table><thead><tr>' +
    '<th class="bin-check"><input type="checkbox" id="bin-select-all" aria-label="Označiť všetko"' + (items.length ? '' : ' disabled') + ' /></th>' +
    '<th>Záznam</th><th>Vymazal</th><th>Kedy</th><th>Dôvod</th><th></th></tr></thead>' +
    '<tbody>' + rows + '</tbody></table></div>' +
    '</div>';

  function showMsg(html) { document.getElementById('bin-msg').innerHTML = html; }
  function showError(err) { showMsg('<div class="error">' + escBin(err.message || 'Akcia zlyhala.') + '</div>'); }

  var boxes = Array.prototype.slice.call(content.querySelectorAll('[data-bin-select]'));
  var allBox = document.getElementById('bin-select-all');
  var restoreSel = document.getElementById('bin-restore-sel');
  var purgeSel = document.getElementById('bin-purge-sel');

  function selectedIds() {
    return boxes.filter(function (b) { return b.checked; }).map(function (b) { return b.getAttribute('data-bin-select'); });
  }
  function refreshBulk() {
    var n = selectedIds().length;
    restoreSel.disabled = n === 0;
    purgeSel.disabled = n === 0;
    restoreSel.textContent = n ? 'Obnoviť vybrané (' + n + ')' : 'Obnoviť vybrané';
    purgeSel.textContent = n ? 'Vymazať natrvalo vybrané (' + n + ')' : 'Vymazať natrvalo vybrané';
    allBox.checked = n > 0 && n === boxes.length;
    allBox.indeterminate = n > 0 && n < boxes.length;
  }
  boxes.forEach(function (b) { b.onchange = refreshBulk; });
  allBox.onchange = function () {
    boxes.forEach(function (b) { b.checked = allBox.checked; });
    refreshBulk();
  };

  content.querySelectorAll('[data-bin-tab]').forEach(function (btn) {
    btn.onclick = function () {
      state.trashCategory = btn.getAttribute('data-bin-tab');
      navigate('trash');
    };
  });

  async function restore(ids) {
    var label = ids.length === 1 ? '<b>' + escBin(byId[ids[0]].title) + '</b> sa vráti' : '<b>' + ids.length + ' záznamov</b> sa vráti';
    var ok = await confirmDialog({
      title: 'Obnoviť?',
      message: '<p>' + label + ' späť do aplikácie.</p>',
      confirmLabel: 'Obnoviť'
    });
    if (!ok) return;
    try {
      await api('/api/v1/trash/restore-batch', { method: 'POST', body: JSON.stringify({ entryIds: ids }) });
      navigate('trash');
    } catch (err) {
      showError(err);
    }
  }

  // Jednotlive trvale vymazanie: ak sa na zaznam nieco viaze, ponukne zaskrtavaci zoznam.
  async function purgeOne(entryId) {
    var entry = byId[entryId];
    var deps;
    try {
      deps = await api('/api/v1/trash/' + entryId + '/dependents');
    } catch (err) {
      showError(err);
      return;
    }
    var hasDeps = (deps.items || []).length > 0 || (deps.notDeletable || []).length > 0;
    if (!hasDeps) {
      await purgeEntries([entryId], '<b>' + escBin(entry.title) + '</b>');
      return;
    }
    openDependentsDialog({
      title: 'Vymazať natrvalo „' + entry.title + '“?',
      intro: '<p>Na tento záznam sa viažu ďalšie záznamy (niektoré môžu byť aj v koši). Označte tie, ktoré sa majú natrvalo vymazať spolu s ním.</p>' +
        '<p class="muted"><b>Toto sa nedá vrátiť.</b> V audite zostane záznam o tom, kto a kedy ich vymazal.</p>',
      data: deps,
      typedConfirm: true,
      reasonLabel: 'Na potvrdenie napíšte VYMAZAŤ',
      reasonPlaceholder: 'VYMAZAŤ',
      confirmLabel: function (n) { return n > 0 ? 'Natrvalo vymazať vybrané (' + n + ') aj tento záznam' : 'Natrvalo vymazať len tento záznam'; },
      onSubmit: async function (selected, confirmText) {
        await api('/api/v1/trash/purge-batch', {
          method: 'POST',
          body: JSON.stringify({
            entryIds: [entryId],
            items: selected.map(function (it) { return { category: it.category, id: it.id }; }),
            confirm: confirmText
          })
        });
        navigate('trash');
      }
    });
  }

  async function purgeEntries(ids, labelHtml) {
    var res = await confirmDialog({
      title: 'Vymazať natrvalo?',
      message: '<p>' + labelHtml + ' sa natrvalo odstráni z databázy. <b>Toto sa nedá vrátiť.</b></p>' +
        '<p class="muted">V audite zostane záznam o tom, kto a kedy ho vymazal.</p>',
      confirmLabel: 'Vymazať natrvalo',
      danger: true,
      reasonLabel: 'Na potvrdenie napíšte VYMAZAŤ',
      reasonPlaceholder: 'VYMAZAŤ',
      reasonRequired: true
    });
    if (!res) return;
    try {
      await api('/api/v1/trash/purge-batch', {
        method: 'POST',
        body: JSON.stringify({ entryIds: ids, items: [], confirm: res.reason })
      });
      navigate('trash');
    } catch (err) {
      showError(err);
    }
  }

  content.querySelectorAll('[data-bin-restore]').forEach(function (btn) {
    btn.onclick = function () { restore([btn.getAttribute('data-bin-restore')]); };
  });
  content.querySelectorAll('[data-bin-purge]').forEach(function (btn) {
    btn.onclick = function () { purgeOne(btn.getAttribute('data-bin-purge')); };
  });
  restoreSel.onclick = function () { restore(selectedIds()); };
  purgeSel.onclick = function () {
    var ids = selectedIds();
    purgeEntries(ids, ids.length === 1 ? '<b>' + escBin(byId[ids[0]].title) + '</b>' : '<b>' + ids.length + ' vybraných záznamov</b>');
  };
}
