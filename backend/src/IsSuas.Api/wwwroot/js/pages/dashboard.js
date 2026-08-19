import { api } from '../core/api.js';
import { bindChartTooltips, colorForKey, entityLabelHtml, renderActivityBarsPanel, renderFleetModelsPanel, renderFlightHoursPanel, renderWeatherPanel, swatchHtml } from '../core/charts.js';
import { ensureMapLibs } from '../core/maps.js';
import { navigate } from '../core/nav.js';
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

      const scopeLabel = scope === 'branch' ? 'Zlozka' : (scope === 'unit' ? 'Utvar' : 'Centralny');
      const scopeHint = d.scopeOrgUnitName
        ? scopeLabel + ': ' + d.scopeOrgUnitName
        : scopeLabel;

      var orgSelectHtml = '';
      if (scope === 'branch') {
        const opts = branchOrgs.map(function (o) {
          return '<option value="' + o.id + '"' + (o.id === selectedOrg ? ' selected' : '') + '>' +
            o.code + ' — ' + o.name + '</option>';
        }).join('');
        orgSelectHtml = '<label style="display:flex;gap:0.5rem;align-items:center">Zlozka ' +
          '<select id="dash-org" style="width:auto;min-width:220px">' + opts + '</select></label>';
      } else if (scope === 'unit') {
        const opts = unitOrgs.map(function (o) {
          return '<option value="' + o.id + '"' + (o.id === selectedOrg ? ' selected' : '') + '>' +
            o.code + ' — ' + o.name + '</option>';
        }).join('');
        orgSelectHtml = '<label style="display:flex;gap:0.5rem;align-items:center">Utvar ' +
          '<select id="dash-org" style="width:auto;min-width:220px">' + opts + '</select></label>';
      }

      const upcoming = (d.upcomingOperations || []).map(function (o) {
        return '<tr class="row-tint" style="--row-accent:' + colorForKey(o.orgUnitName || o.title) + '">' +
          '<td><span class="entity-label">' + swatchHtml(colorForKey(o.title)) +
          '<span>' + o.title + '</span></span></td><td>' + statusBadge(o.status) + '</td><td>' +
          entityLabelHtml(o.orgUnitName, o.orgUnitName) +
          '</td><td>' + new Date(o.start).toLocaleString('sk-SK') + '</td></tr>';
      }).join('') || '<tr><td colspan="4">Ziadne bliziace sa operacie</td></tr>';
      const alerts = (d.criticalAlerts || []).map(function (a) {
        return '<tr class="row-tint" style="--row-accent:' + colorForKey(a.priority || a.typeCode) + '">' +
          '<td>' + statusBadge(a.priority) + '</td><td>' + typeLabelSk(a.typeCode) +
          '</td><td>' + a.title + '</td></tr>';
      }).join('') || '<tr><td colspan="3">Ziadne kriticke upozornenia</td></tr>';

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
      const fleetMeta =
        '<div class="meta">' +
        '<span><i class="dot dot-ok"></i>' + ((d.fleet && d.fleet.inUse) || 0) + ' používané</span>' +
        '<span><i class="dot" style="background:#8a97a6"></i>' + ((d.fleet && d.fleet.idle) || 0) + ' nepoužívané</span>' +
        '<span><i class="dot dot-danger"></i>' + ((d.fleet && d.fleet.decommissioned) || 0) + ' vyradené</span>' +
        '</div>';

      content.innerHTML =
        '<div class="stack">' +
        '<div class="msg">Vitajte, ' + state.displayName + ' (rola: ' + statusLabelSk(d.roleHint) + ') — ' + scopeHint + '</div>' +
        '<div class="toolbar">' +
        '<button type="button" class="secondary dash-scope" data-scope="central"' + (scope === 'central' ? ' disabled' : '') + '>Centrálny</button>' +
        '<button type="button" class="secondary dash-scope" data-scope="branch"' + (scope === 'branch' ? ' disabled' : '') + '>Zložka</button>' +
        '<button type="button" class="secondary dash-scope" data-scope="unit"' + (scope === 'unit' ? ' disabled' : '') + '>Útvar</button>' +
        orgSelectHtml +
        '</div>' +
        '<div class="dash-grid">' + primaryHoursPie + uasPie + '</div>' +
        '<div class="dash-grid">' + fleetPie + activityBars + '</div>' +
        '<div class="dash-grid dash-grid-full">' + modelsPanel + '</div>' +
        '<div class="dash-grid">' +
        weatherHtml +
        '<div class="stat-grid dash-stats">' +
        '<div class="stat"><div class="n">' + d.fleet.totalUas + '</div><div class="l">UAS celkom</div>' + fleetMeta + '</div>' +
        '<div class="stat"><div class="n">' + (d.totalFlightHours != null ? Number(d.totalFlightHours).toFixed(1) : '0') + '</div><div class="l">Nálet (denník)</div></div>' +
        '<div class="stat"><div class="n">' + (d.totalTakeoffs != null ? d.totalTakeoffs : 0) + '</div><div class="l">Odletené lety</div></div>' +
        '<div class="stat"><div class="n">' + (d.confirmedMonthlyReports != null ? d.confirmedMonthlyReports : 0) + '</div><div class="l">Odovzdane vykazy</div></div>' +
        '<div class="stat"><div class="n">' + (d.missingMonthlyReports != null ? d.missingMonthlyReports : 0) + '</div><div class="l">Chybajuce vykazy</div></div>' +
        '<div class="stat"><div class="n">' + d.operationsToday + '</div><div class="l">Operacie dnes</div></div>' +
        '<div class="stat"><div class="n">' + d.openDefects + '</div><div class="l">Poruchy</div></div>' +
        '<div class="stat"><div class="n">' + d.openIncidents + '</div><div class="l">Incidenty</div></div>' +
        '<div class="stat"><div class="n">' + d.unreadNotifications + '</div><div class="l">Neprecitane</div></div>' +
        '</div>' +
        '</div>' +
        '<div class="dash-map-panel">' +
        '<div class="dash-map-chrome">' +
        '<div><h2>Operačný priestor · Slovensko</h2>' +
        '<p>VFR prehľad vzdušných priestorov (LPS SR)</p></div>' +
        '<button type="button" class="secondary" id="dash-open-map">Otvorit mapu</button>' +
        '</div>' +
        '<div id="dash-drone-map"></div>' +
        '</div>' +
        '<div class="dash-grid">' +
        '<div class="dash-table-panel"><h2>Najblizsie operacie</h2>' +
        makeTable(['Nazov', 'Stav', 'Utvar', 'Zaciatok'], upcoming, true) + '</div>' +
        '<div class="dash-table-panel"><h2>Kriticke upozornenia</h2>' +
        makeTable(['Priorita', 'Typ', 'Nazov'], alerts, true) + '</div>' +
        '</div>' +
        '</div>';
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
