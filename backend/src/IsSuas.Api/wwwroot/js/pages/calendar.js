import { api } from '../core/api.js';
import { bindChartTooltips, colorForKey, entityLabelHtml, renderFlightHoursPanel, swatchHtml } from '../core/charts.js';
import { chipHtml, content, makeTable, pageHead, statusBadge, statusLabelSk } from '../core/ui.js';

export async function renderCalendar() {
      const from = new Date();
      from.setDate(1);
      const to = new Date(from);
      to.setMonth(to.getMonth() + 2);
      const items = await api('/api/v1/calendar?from=' + encodeURIComponent(from.toISOString()) +
        '&to=' + encodeURIComponent(to.toISOString()));

      function groupCounts(list, keyFn) {
        var map = {};
        list.forEach(function (o) {
          var k = keyFn(o) || '—';
          map[k] = (map[k] || 0) + 1;
        });
        return Object.keys(map).map(function (k) {
          return { key: k, label: k, hours: map[k] };
        }).sort(function (a, b) { return b.hours - a.hours; });
      }

      var byStatus = groupCounts(items, function (o) {
        return statusLabelSk(o.status);
      });
      var byOrg = groupCounts(items, function (o) { return o.orgUnitName; });
      var urgentCount = items.filter(function (o) { return o.isUrgent; }).length;
      var statusPie = renderFlightHoursPanel(
        'Operácie podľa stavu',
        items.length,
        byStatus,
        'Obdobie kalendara — rozdelenie stavov',
        'operacii'
      );
      var orgPie = renderFlightHoursPanel(
        'Operácie podľa útvaru',
        items.length,
        byOrg,
        'Kde su naplanovane aktivity',
        'operacii'
      );

      const rows = items.map(function (o) {
        return '<tr class="row-tint" style="--row-accent:' + colorForKey(o.orgUnitName || o.title) + '">' +
          '<td>' + new Date(o.start).toLocaleString('sk-SK') + '</td><td>' +
          new Date(o.end).toLocaleString('sk-SK') + '</td><td><span class="entity-label">' +
          swatchHtml(colorForKey(o.title)) + '<span>' + o.title + '</span></span>' +
          '</td><td>' + statusBadge(o.status) + '</td><td>' +
          entityLabelHtml(o.orgUnitName, o.orgUnitName) +
          '</td><td>' + (o.isUrgent ? statusBadge('Urgent') : 'Nie') + '</td></tr>';
      }).join('') || '<tr><td colspan="6">Žiadne operácie v kalendari</td></tr>';

      content.innerHTML =
        '<div class="stack">' +
        pageHead('Operacny kalendar',
          'Obdobie: ' + from.toLocaleDateString('sk-SK') + ' – ' + to.toLocaleDateString('sk-SK'),
          chipHtml('Operácie', items.length, 'chip-soft') +
          chipHtml('Neodkladné', urgentCount, urgentCount ? 'chip-danger' : 'chip-ok')) +
        '<div class="dash-grid">' + statusPie + orgPie + '</div>' +
        makeTable(['Od', 'Do', 'Názov', 'Stav', 'Útvar', 'Neodkladná'], rows) +
        '</div>';
      bindChartTooltips(content);
      return;
}
