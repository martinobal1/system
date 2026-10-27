import { api } from '../core/api.js';
import { bindChartTooltips, colorForKey, entityLabelHtml, renderActivityBarsPanel, renderFleetModelsPanel, renderFlightHoursPanel, renderWeatherPanel, swatchHtml } from '../core/charts.js';
import { ensureMapLibs } from '../core/maps.js';
import { isPageAllowedForRole, navigate } from '../core/nav.js';
import { state } from '../core/state.js';
import { content, makeTable, statusBadge, statusLabelSk, typeLabelSk } from '../core/ui.js';

export async function renderDashboard() {
      const orgs = await api('/api/v1/org-units');
      const scope = state.dashboardScope || 'central';
      let selectedOrg = (typeof state.dashboardOrgUnitId === 'string') ? state.dashboardOrgUnitId : '';
      const branchOrgs = orgs.filter(function (o) {
        return o.kind === 'BranchPz' || o.kind === 'BranchHazz' || o.kind === 'BranchCo' || o.kind === 1 || o.kind === 2 || o.kind === 3 || o.level === 1;
      });
      const unitOrgs = orgs.filter(function (o) {
        return o.kind === 'Unit' || o.kind === 4 || o.level >= 2;
      });
      if (scope === 'branch' && !selectedOrg && branchOrgs.length)
        selectedOrg = branchOrgs[0].id;
      if (scope === 'unit' && !selectedOrg && unitOrgs.length)
        selectedOrg = unitOrgs[0].id;

      var dashUrl = '/api/v1/dashboard?scope=' + encodeURIComponent(scope);
      if (scope !== 'central' && selectedOrg)
        dashUrl += '&orgUnitId=' + encodeURIComponent(selectedOrg);

      const d = await api(dashUrl);
      state.dashboardOrgUnitId = selectedOrg;

      let weather = null;
      try {
        weather = await api('/api/v1/weather');
      } catch (_) { /* weather optional for overview */ }

      const scopeLabel = scope === 'branch' ? 'Zložka' : (scope === 'unit' ? 'Útvar' : 'Centrálny prehľad');
      const scopeHint = d.scopeOrgUnitName
        ? scopeLabel + ': ' + d.scopeOrgUnitName
        : scopeLabel;

      var orgSelectHtml = '';
      if (scope === 'branch') {
        const opts = branchOrgs.map(function (o) {
          return '<option value="' + o.id + '"' + (o.id === selectedOrg ? ' selected' : '') + '>' +
            o.code + ' — ' + o.name + '</option>';
        }).join('');
        orgSelectHtml = '<label class="dash-org-pick">Zložka ' +
          '<select id="dash-org">' + opts + '</select></label>';
      } else if (scope === 'unit') {
        const opts = unitOrgs.map(function (o) {
          return '<option value="' + o.id + '"' + (o.id === selectedOrg ? ' selected' : '') + '>' +
            o.code + ' — ' + o.name + '</option>';
        }).join('');
        orgSelectHtml = '<label class="dash-org-pick">Útvar ' +
          '<select id="dash-org">' + opts + '</select></label>';
      }

      const upcoming = (d.upcomingOperations || []).map(function (o) {
        return '<tr class="row-tint" style="--row-accent:' + colorForKey(o.orgUnitName || o.title) + '">' +
          '<td><span class="entity-label">' + swatchHtml(colorForKey(o.title)) +
          '<span>' + o.title + '</span></span></td><td>' + statusBadge(o.status) + '</td><td>' +
          entityLabelHtml(o.orgUnitName, o.orgUnitName) +
          '</td><td>' + new Date(o.start).toLocaleString('sk-SK') + '</td></tr>';
      }).join('') || '<tr><td colspan="4" class="empty-cell">Žiadne blížiace sa operácie</td></tr>';
      const alerts = (d.criticalAlerts || []).map(function (a) {
        return '<tr class="row-tint" style="--row-accent:' + colorForKey(a.priority || a.typeCode) + '">' +
          '<td>' + statusBadge(a.priority) + '</td><td>' + typeLabelSk(a.typeCode) +
          '</td><td>' + a.title + '</td></tr>';
      }).join('') || '<tr><td colspan="3" class="empty-cell">Žiadne kritické upozornenia</td></tr>';

      const weatherHtml = renderWeatherPanel(weather);
      var primaryHoursPie;
      if (scope === 'unit') {
        primaryHoursPie = renderFlightHoursPanel(
          'Nalietané hodiny podľa pilotov',
          d.totalFlightHours,
          d.flightHoursByPilot || [],
          'Top piloti útvaru podľa uzavretých letov v denníku'
        );
      } else {
        primaryHoursPie = renderFlightHoursPanel(
          'Nalietané hodiny podľa kraja',
          d.totalFlightHours,
          d.flightHoursByOrgUnit || [],
          scope === 'branch'
            ? 'Rozdelenie nalietaných hodín v rámci zložky (denník)'
            : 'Z denníka letov (potvrdené odlety)'
        );
      }
      const uasPie = renderFlightHoursPanel(
        'Nalietané hodiny podľa dronu',
        d.totalFlightHours,
        d.flightHoursByUas || [],
        'Top drony podľa uzavretých letov v denníku'
      );
      const fleetSlices = [
        { key: 'inUse', label: 'Používané', color: '#0d8f7a', hours: (d.fleet && d.fleet.inUse) || 0 },
        { key: 'idle', label: 'Nepoužívané', color: '#e09b2d', hours: (d.fleet && d.fleet.idle) || 0 },
        { key: 'inMaintenance', label: 'V údržbe / poškodené', color: '#c47b1a', hours:
          ((d.fleet && d.fleet.inMaintenance) || 0) + ((d.fleet && d.fleet.damaged) || 0) + ((d.fleet && d.fleet.lost) || 0) },
        { key: 'decommissioned', label: 'Vyradené', color: '#b42318', hours: (d.fleet && d.fleet.decommissioned) || 0 }
      ].filter(function (s) { return Number(s.hours) > 0; });
      const fleetPie = renderFlightHoursPanel(
        'Stav flotily UAS',
        d.fleet.totalUas,
        fleetSlices,
        'Používané = majú nálet v denníku. Nepoužívané = evidované bez letu.',
        'ks'
      );
      const modelsPanel = renderFleetModelsPanel(d.fleetByModel || [], d.fleet.totalUas);
      const activityBars = renderActivityBarsPanel(d);
      const ICONS = {
        clock: '<path d="M12 7v5l3 2"/><circle cx="12" cy="12" r="9"/>',
        plane: '<path d="M10.5 3.5 12 2l1.5 1.5v6l7.5 4.5v2l-7.5-2.5v4.5l2.5 2V21l-4-1-4 1v-1.5l2.5-2V13L3 15.5v-2l7.5-4.5z"/>',
        drone: '<circle cx="5" cy="5" r="2.5"/><circle cx="19" cy="5" r="2.5"/><circle cx="5" cy="19" r="2.5"/><circle cx="19" cy="19" r="2.5"/><path d="M7 7l3 3M17 7l-3 3M7 17l3-3M17 17l-3-3"/><rect x="9.5" y="9.5" width="5" height="5" rx="1.2"/>',
        calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 10h18"/>',
        alert: '<path d="M10.3 3.8 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.8a2 2 0 0 0-3.4 0z"/><path d="M12 9v5M12 17h.01"/>',
        shield: '<path d="M12 3 4 7v5c0 5 3.4 8.4 8 9 4.6-.6 8-4 8-9V7l-8-4z"/><path d="M12 8v5M12 16h.01"/>'
      };
      function kpi(opts) {
        var go = opts.page && isPageAllowedForRole(opts.page);
        var tag = go ? 'button type="button" data-go="' + opts.page + '"' : 'div';
        return '<' + tag + ' class="kpi kpi-' + (opts.tone || 'teal') + '">' +
          '<span class="kpi-ico"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' + ICONS[opts.icon] + '</svg></span>' +
          '<span class="kpi-label">' + opts.label + '</span>' +
          '<span class="kpi-value">' + opts.value + (opts.unit ? '<small>' + opts.unit + '</small>' : '') + '</span>' +
          (opts.sub ? '<span class="kpi-sub">' + opts.sub + '</span>' : '') +
          '</' + (go ? 'button' : 'div') + '>';
      }
      const fleet = d.fleet || {};
      const hours = d.totalFlightHours != null ? Number(d.totalFlightHours).toLocaleString('sk-SK', { maximumFractionDigits: 1 }) : '0';
      const kpis =
        kpi({ icon: 'clock', label: 'Nálet celkom', value: hours, unit: 'h', sub: 'z denníka letov', page: 'flights' }) +
        kpi({ icon: 'plane', label: 'Odletené lety', value: d.totalTakeoffs != null ? d.totalTakeoffs : 0, sub: 'potvrdené odlety', page: 'flights', tone: 'blue' }) +
        kpi({ icon: 'drone', label: 'UAS vo flotile', value: fleet.totalUas || 0, sub: (fleet.inUse || 0) + ' používaných · ' + (fleet.idle || 0) + ' bez letu', page: 'uas', tone: 'violet' }) +
        kpi({ icon: 'calendar', label: 'Operácie dnes', value: d.operationsToday || 0, sub: 'naplánované na dnes', page: 'operations', tone: 'blue' }) +
        kpi({ icon: 'alert', label: 'Otvorené poruchy', value: d.openDefects || 0, sub: d.openDefects ? 'vyžadujú pozornosť' : 'všetko v poriadku', page: 'defects', tone: d.openDefects ? 'amber' : 'green' }) +
        kpi({ icon: 'shield', label: 'Incidenty', value: d.openIncidents || 0, sub: d.openIncidents ? 'otvorené' : 'žiadne otvorené', page: 'incidents', tone: d.openIncidents ? 'red' : 'green' });

      const hour = new Date().getHours();
      const greet = hour < 10 ? 'Dobré ráno' : (hour < 18 ? 'Dobrý deň' : 'Dobrý večer');
      const firstName = String(state.displayName || '').trim().split(/\s+/)[0] || '';
      const today = new Date().toLocaleDateString('sk-SK', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
      const metaBits = [
        statusLabelSk(d.roleHint),
        scopeHint,
        (d.confirmedMonthlyReports || 0) + ' odovzdaných výkazov',
        (d.missingMonthlyReports || 0) + ' chýbajúcich',
        (d.unreadNotifications || 0) + ' neprečítaných notifikácií'
      ];

      content.innerHTML =
        '<div class="stack dash">' +
        '<div class="dash-hero">' +
        '<div class="dash-hero-text">' +
        '<p class="dash-date">' + today + '</p>' +
        '<h1>' + greet + (firstName ? ', ' + firstName : '') + '</h1>' +
        '<p class="dash-meta">' + metaBits.map(function (b) { return '<span>' + b + '</span>'; }).join('') + '</p>' +
        '</div>' +
        '<div class="dash-hero-controls">' +
        '<div class="segmented" role="tablist" aria-label="Rozsah prehľadu">' +
        '<button type="button" class="dash-scope' + (scope === 'central' ? ' is-on' : '') + '" data-scope="central"' + (scope === 'central' ? ' aria-selected="true"' : '') + '>Centrálny</button>' +
        '<button type="button" class="dash-scope' + (scope === 'branch' ? ' is-on' : '') + '" data-scope="branch"' + (scope === 'branch' ? ' aria-selected="true"' : '') + '>Zložka</button>' +
        '<button type="button" class="dash-scope' + (scope === 'unit' ? ' is-on' : '') + '" data-scope="unit"' + (scope === 'unit' ? ' aria-selected="true"' : '') + '>Útvar</button>' +
        '</div>' +
        orgSelectHtml +
        '</div>' +
        '</div>' +
        '<div class="kpi-grid">' + kpis + '</div>' +
        '<div class="dash-row dash-row-map">' +
        '<div class="dash-map-panel">' +
        '<div class="dash-map-chrome">' +
        '<div><h2>Operačný priestor · Slovensko</h2>' +
        '<p>VFR prehľad vzdušných priestorov (LPS SR)</p></div>' +
        '<button type="button" class="secondary" id="dash-open-map">Otvoriť mapu →</button>' +
        '</div>' +
        '<div id="dash-drone-map"></div>' +
        '</div>' +
        weatherHtml +
        '</div>' +
        '<div class="dash-row">' + primaryHoursPie + uasPie + '</div>' +
        '<div class="dash-row">' + fleetPie + activityBars + '</div>' +
        '<div class="dash-row dash-row-full">' + modelsPanel + '</div>' +
        '<div class="dash-row">' +
        '<div class="dash-table-panel"><div class="card-head"><h2>Najbližšie operácie</h2>' +
        (isPageAllowedForRole('operations') ? '<button type="button" class="link-btn" data-go="operations">Všetky →</button>' : '') + '</div>' +
        makeTable(['Názov', 'Stav', 'Útvar', 'Začiatok'], upcoming, true) + '</div>' +
        '<div class="dash-table-panel"><div class="card-head"><h2>Kritické upozornenia</h2></div>' +
        makeTable(['Priorita', 'Typ', 'Názov'], alerts, true) + '</div>' +
        '</div>' +
        '</div>';
      content.querySelectorAll('[data-go]').forEach(function (el) {
        el.addEventListener('click', function () { navigate(el.getAttribute('data-go')); });
      });
      bindChartTooltips(content);
      content.querySelectorAll('.dash-scope').forEach(function (btn) {
        btn.onclick = function () {
          state.dashboardScope = btn.dataset.scope;
          state.dashboardOrgUnitId = '';
          navigate('dashboard');
        };
      });
      var orgEl = document.getElementById('dash-org');
      if (orgEl) {
        orgEl.onchange = function () {
          state.dashboardOrgUnitId = this.value || '';
          navigate('dashboard');
        };
      }
      document.getElementById('dash-open-map').onclick = function () {
        document.querySelectorAll('.nav').forEach(function (b) {
          b.classList.toggle('active', b.dataset.page === 'map');
        });
        navigate('map');
      };
      try {
        await ensureMapLibs();
        if (window.IsSuasDroneMap) {
          window.IsSuasDroneMap.initMap('dash-drone-map', {
            token: state.token,
            preview: true,
            center: [48.7, 19.5],
            zoom: 7
          });
        }
      } catch (_) { /* mapa na prehlade je volitelna */ }
      return;
}
