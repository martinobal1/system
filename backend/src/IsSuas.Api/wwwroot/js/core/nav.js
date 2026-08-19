import { state } from './state.js';
import { appView, content, pageTitle } from './ui.js';

// Modul stranky sa stahuje az pri jej prvom otvoreni, aby uvodne nacitanie nebolo cele UI.
const pageModules = {
  dashboard: () => import('../pages/dashboard.js').then(m => m.renderDashboard),
  orgs: () => import('../pages/orgs.js').then(m => m.renderOrgs),
  accounts: () => import('../pages/accounts.js').then(m => m.renderAccounts),
  delegations: () => import('../pages/delegations.js').then(m => m.renderDelegations),
  monthly: () => import('../pages/monthly.js').then(m => m.renderMonthly),
  pilots: () => import('../pages/pilots.js').then(m => m.renderPilots),
  certificates: () => import('../pages/certificates.js').then(m => m.renderCertificates),
  operations: () => import('../pages/operations.js').then(m => m.renderOperations),
  flights: () => import('../pages/flights.js').then(m => m.renderFlights),
  defects: () => import('../pages/defects.js').then(m => m.renderDefects),
  incidents: () => import('../pages/incidents.js').then(m => m.renderIncidents),
  documents: () => import('../pages/documents.js').then(m => m.renderDocuments),
  reports: () => import('../pages/reports.js').then(m => m.renderReports),
  calendar: () => import('../pages/calendar.js').then(m => m.renderCalendar),
  map: () => import('../pages/map.js').then(m => m.renderMap),
  geozones: () => import('../pages/geozones.js').then(m => m.renderGeozones),
  'geozones-admin': () => import('../pages/geozones-admin.js').then(m => m.renderGeozonesAdmin),
  admin: () => import('../pages/admin.js').then(m => m.renderAdmin),
  uas: () => import('../pages/uas.js').then(m => m.renderUas),
  components: () => import('../pages/components.js').then(m => m.renderComponents),
  search: () => import('../pages/search.js').then(m => m.renderSearch)
};

// Zrusene a rolou nedostupne stranky presmerujeme skor, nez sa zapise URL.
function resolvePage(page) {
  if (page === 'batteries') return 'components';
  if (page === 'map' && state.accessLevel === 3) return 'geozones';
  return page;
}

export function closeNav() {
  setNavOpen(false);
}

export async function navigate(requestedPage, options) {
  var page = resolvePage(requestedPage);
  var opts = options || {};
  if (!opts.fromHistory) {
    var url = urlForPage(page);
    if (url !== window.location.pathname + window.location.search) {
      if (opts.replaceUrl) window.history.replaceState({ page: page }, '', url);
      else window.history.pushState({ page: page }, '', url);
    }
  }
  syncNavActive(page);
  pageTitle.textContent = titles[page] || page;
  if (window.IsSuasDroneMap) window.IsSuasDroneMap.destroyMap();
  content.classList.toggle('content-map', page === 'map' || page === 'geozones');
  content.innerHTML = '<div class="panel">Nacitavam...</div>';
  try {
    var loadPage = pageModules[page];
    if (!loadPage) throw new Error('Neznama stranka: ' + page);
    var render = await loadPage();
    await render();
  } catch (err) {
    content.innerHTML = '<div class="error">' + err.message + '</div>';
  }
}

export function initNav() {
  document.getElementById('menu-toggle').addEventListener('click', function () {
    setNavOpen(!appView.classList.contains('nav-open'));
  });
  document.getElementById('nav-backdrop').addEventListener('click', closeNav);
  window.addEventListener('resize', function () {
    if (window.innerWidth > 900) closeNav();
  });

  document.querySelectorAll('.nav').forEach(function (btn) {
    btn.addEventListener('click', function () {
      closeNav();
      navigate(btn.dataset.page);
    });
  });

  document.getElementById('sidebar-brand-home').addEventListener('click', function () {
    closeNav();
    navigate(defaultPageForRole());
  });

  document.getElementById('global-search-form').addEventListener('submit', function (ev) {
    ev.preventDefault();
    state.searchQuery = document.getElementById('global-search-q').value.trim();
    navigate('search');
  });

  window.addEventListener('popstate', function () {
    if (!state.token) return;
    var page = pageFromPath(window.location.pathname);
    if (page === 'search') {
      state.searchQuery = new URLSearchParams(window.location.search).get('q') || '';
    }
    if (!page || !isPageAllowedForRole(page)) page = defaultPageForRole();
    navigate(page, { fromHistory: true });
  });
}

export function applyNavByRole() {
  var level = Number(state.accessLevel) || 1;
  document.querySelectorAll('#app-nav .nav').forEach(function (btn) {
    var roles = (btn.getAttribute('data-roles') || '1,2,3').split(',').map(function (x) { return Number(x.trim()); });
    btn.style.display = roles.indexOf(level) >= 0 ? '' : 'none';
  });
  document.querySelectorAll('#app-nav .nav-section').forEach(function (sec) {
    var next = sec.nextElementSibling;
    var any = false;
    while (next && !next.classList.contains('nav-section')) {
      if (next.classList.contains('nav') && next.style.display !== 'none') any = true;
      next = next.nextElementSibling;
    }
    sec.style.display = any ? '' : 'none';
  });
}

export function defaultPageForRole() {
  var level = Number(state.accessLevel) || 1;
  if (level === 3) return 'flights';
  if (level === 2) return 'dashboard';
  return 'dashboard';
}

export function setNavOpen(open) {
  appView.classList.toggle('nav-open', !!open);
  var toggle = document.getElementById('menu-toggle');
  if (toggle) {
    toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    toggle.setAttribute('aria-label', open ? 'Zavrieť menu' : 'Otvoriť menu');
  }
  document.body.style.overflow = open ? 'hidden' : '';
}

export const titles = {
  dashboard: 'Prehlad',
  orgs: 'Organizacna struktura',
  accounts: 'Prevadzkovatelia',
  delegations: 'Delegacie opravneni',
  pilots: 'Register PnD',
  certificates: 'Osvedčenia',
  operations: 'Žiadosti o let',
  flights: 'Dennik letov',
  defects: 'Poruchy',
  incidents: 'Incidenty',
  documents: 'Dokumenty',
  reports: 'Reporty',
  calendar: 'Kalendar',
  map: 'Mapa — VFR / vzdušné priestory',
  geozones: 'Mapa GEO zon',
  'geozones-admin': 'Sprava GEO zon',
  admin: 'Administracia',
  uas: 'Register UAS',
  components: 'Technická dokumentácia',
  batteries: 'Baterie',
  monthly: 'Mesačné hlásenia o letoch',
  search: 'Vysledky hladania'
};

export const routes = {
  dashboard: 'prehlad',
  orgs: 'organizacna-struktura',
  accounts: 'prevadzkovatelia',
  delegations: 'delegacie-opravneni',
  pilots: 'register-pnd',
  certificates: 'osvedcenia',
  operations: 'ziadosti-o-let',
  flights: 'dennik-letov',
  defects: 'poruchy',
  incidents: 'incidenty',
  documents: 'dokumenty',
  reports: 'reporty',
  calendar: 'kalendar',
  map: 'mapa',
  geozones: 'geo-zony',
  'geozones-admin': 'sprava-geo-zon',
  admin: 'administracia',
  uas: 'register-uas',
  components: 'technicka-dokumentacia',
  batteries: 'baterie',
  monthly: 'mesacne-hlasenia',
  search: 'hladanie'
};

export function urlForPage(page) {
  var url = '/' + (routes[page] || page);
  if (page === 'search' && state.searchQuery) url += '?q=' + encodeURIComponent(state.searchQuery);
  return url;
}

export function pageFromPath(path) {
  var raw = String(path || '').split('#')[0].split('?')[0];
  var slug = raw.replace(/^\/+/, '').replace(/\/+$/, '').toLowerCase();
  if (!slug) return null;
  for (var page in routes) {
    if (routes[page] === slug) return page;
  }
  return titles[slug] ? slug : null;
}

export function syncNavActive(page) {
  document.querySelectorAll('.nav').forEach(function (btn) {
    btn.classList.toggle('active', btn.dataset.page === page);
  });
}

// Stranky bez polozky v menu, ktore sa aj tak smie otvorit priamo z URL.
const urlOnlyPages = { search: true };

// Stranka je pristupna, len ak ju rola vidi aj v menu; inak by URL obisla applyNavByRole.
export function isPageAllowedForRole(page) {
  var btn = document.querySelector('#app-nav .nav[data-page="' + page + '"]');
  if (!btn) return !!urlOnlyPages[page];
  var level = Number(state.accessLevel) || 1;
  var roles = (btn.getAttribute('data-roles') || '1,2,3').split(',').map(function (x) { return Number(x.trim()); });
  return roles.indexOf(level) >= 0;
}

export function openSearchHit(entityType, id, title) {
  state.searchHighlight = { entityType: entityType, id: id, title: title };
  var page = 'dashboard';
  if (entityType === 'Uas') page = 'uas';
  else if (entityType === 'Pilot') page = 'pilots';
  else if (entityType === 'Operation') page = 'operations';
  else if (entityType === 'Account') page = 'accounts';
  else if (entityType === 'SafetyEvent') page = 'incidents';
  else if (entityType === 'ControlAudit') page = 'dashboard';
  navigate(page);
}
