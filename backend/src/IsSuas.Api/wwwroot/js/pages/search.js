import { api } from '../core/api.js';
import { openSearchHit } from '../core/nav.js';
import { state } from '../core/state.js';
import { content, statusBadge } from '../core/ui.js';

export async function renderSearch() {
      const q = (state.searchQuery || '').trim();
      document.getElementById('global-search-q').value = q;
      if (q.length < 2) {
        content.innerHTML = '<div class="stack"><div class="msg">Zadajte aspon 2 znaky do hladania v hornom paneli.</div></div>';
        return;
      }
      const res = await api('/api/v1/search?q=' + encodeURIComponent(q) + '&limit=8');
      const hits = res.hits || [];
      if (!hits.length) {
        content.innerHTML = '<div class="stack"><div class="msg">Ziadne vysledky pre „' + q.replace(/</g, '') + '“.</div></div>';
        return;
      }
      const typeLabels = {
        Uas: 'UAS',
        Pilot: 'Piloti',
        Operation: 'Operacie',
        Account: 'Ucty',
        SafetyEvent: 'Incidenty',
        ControlAudit: 'Audit'
      };
      const groups = {};
      hits.forEach(function (h) {
        if (!groups[h.entityType]) groups[h.entityType] = [];
        groups[h.entityType].push(h);
      });
      var html = '<div class="stack"><div class="msg">Hladanie: <b>' + q.replace(/</g, '') +
        '</b> — ' + hits.length + ' vysledkov</div>';
      Object.keys(groups).forEach(function (type) {
        html += '<h2>' + (typeLabels[type] || type) + '</h2>';
        html += groups[type].map(function (h) {
          return '<button type="button" class="search-hit secondary" data-type="' + h.entityType +
            '" data-id="' + h.id + '" style="display:block;width:100%;text-align:left;margin-bottom:0.4rem">' +
            '<strong>' + h.title + '</strong> ' + statusBadge(h.status || '') +
            '<div class="muted">' + (h.subtitle || '') +
            (h.orgUnitName ? ' · ' + h.orgUnitName : '') + '</div></button>';
        }).join('');
      });
      html += '</div>';
      content.innerHTML = html;
      content.querySelectorAll('.search-hit').forEach(function (btn) {
        btn.onclick = function () {
          openSearchHit(btn.dataset.type, btn.dataset.id, btn.querySelector('strong').textContent);
        };
      });
      return;
}
