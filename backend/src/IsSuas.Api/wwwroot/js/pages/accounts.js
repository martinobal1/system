import { api } from '../core/api.js';
import { colorForKey, swatchHtml } from '../core/charts.js';
import { chipHtml, content, makeTable, pageHead, searchHighlightBanner, statusBadge } from '../core/ui.js';

export async function renderAccounts() {
      const items = await api('/api/v1/accounts');
      var activeCount = items.filter(function (a) {
        return a.status === 'Active' || a.status === 0;
      }).length;
      content.innerHTML = '<div class="stack">' + searchHighlightBanner() +
        pageHead('Prevadzkovatelia', 'Účty s prístupom do systému.',
          chipHtml('Celkom', items.length, 'chip-soft') +
          chipHtml('Aktívne', activeCount, 'chip-ok')) +
        makeTable(
        ['Prevadzkovatel', 'Meno', 'Email', 'Stav'],
        items.map(function (a) {
          return '<tr class="row-tint" style="--row-accent:' + colorForKey(a.userName) + '">' +
            '<td><span class="entity-label">' + swatchHtml(colorForKey(a.userName)) +
            '<span>' + a.userName + '</span></span></td><td>' + a.firstName + ' ' + a.lastName +
            '</td><td>' + (a.email || '-') + '</td><td>' + statusBadge(a.status) + '</td></tr>';
        }).join('')
      ) + '</div>';
      return;
}
