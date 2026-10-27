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

const removedPages = { documents: true, reports: true };

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
  // Stránky odstránené z menu (napr. Dokumenty, Reporty) sa nedajú otvoriť ani priamo.
  if (removedPages[page]) page = defaultPageForRole();
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
  syncSectionCrumb(page);
  closeUserMenu();
  if (window.IsSuasDroneMap) window.IsSuasDroneMap.destroyMap();
  content.classList.toggle('content-map', page === 'map' || page === 'geozones');
  content.innerHTML = loadingSkeleton();
  try {
    var loadPage = pageModules[page];
    if (!loadPage) throw new Error('Neznama stranka: ' + page);
    var render = await loadPage();
    await render();
    enhanceLayout(content);
  } catch (err) {
    content.innerHTML = '<div class="error">' + err.message + '</div>';
  }
}

function loadingSkeleton() {
  return '<div class="stack skeleton" aria-busy="true" aria-label="Načítavam">' +
    '<div class="sk sk-title"></div><div class="sk sk-sub"></div>' +
    '<div class="sk-grid"><div class="sk sk-card"></div><div class="sk sk-card"></div><div class="sk sk-card"></div></div>' +
    '<div class="sk sk-block"></div></div>';
}

// Úpravy rozloženia spoločné pre všetky stránky (bez zásahu do logiky stránok):
// panel s tlačidlami hneď pod hlavičkou presunieme do pravej časti hlavičky.
function enhanceLayout(root) {
  root.querySelectorAll('.page-head').forEach(function (head) {
    var next = head.nextElementSibling;
    while (next && next.id && /-msg$/.test(next.id) && !next.innerHTML.trim()) next = next.nextElementSibling;
    if (!next || !next.classList.contains('toolbar')) return;
    if (next.querySelector('input, select, textarea')) return;
    var buttons = next.querySelectorAll('button');
    if (!buttons.length || buttons.length > 4) return;
    var actions = document.createElement('div');
    actions.className = 'page-actions';
    while (next.firstChild) actions.appendChild(next.firstChild);
    next.remove();
    var chips = head.querySelector(':scope > .chip-row');
    var side = document.createElement('div');
    side.className = 'page-head-side';
    if (chips) side.appendChild(chips);
    side.appendChild(actions);
    head.appendChild(side);
  });
}

// Drobčeková navigácia: názov sekcie menu, do ktorej stránka patrí.
function syncSectionCrumb(page) {
  var el = document.getElementById('page-section');
  if (!el) return;
  var btn = document.querySelector('#app-nav .nav[data-page="' + page + '"]');
  var sec = btn ? btn.previousElementSibling : null;
  while (sec && !sec.classList.contains('nav-section')) sec = sec.previousElementSibling;
  var label = sec ? sec.textContent.trim() : 'IS ŠUAS';
  el.textContent = label;
  el.parentElement.classList.toggle('crumbs-single', label === (titles[page] || page));
}

function setNavCollapsed(collapsed) {
  document.documentElement.classList.toggle('nav-collapsed', !!collapsed);
  var btn = document.getElementById('sidebar-collapse');
  if (btn) {
    var label = collapsed ? 'Rozbaliť menu' : 'Zbaliť menu';
    btn.setAttribute('aria-label', label);
    btn.title = label;
    var span = btn.querySelector('span');
    if (span) span.textContent = label;
  }
  try { localStorage.setItem('issuas.navCollapsed', collapsed ? '1' : '0'); } catch (e) {}
}

export function closeUserMenu() {
  var dd = document.getElementById('user-dropdown');
  var trigger = document.getElementById('user-trigger');
  if (dd) dd.classList.add('hidden');
  if (trigger) trigger.setAttribute('aria-expanded', 'false');
}

function initShellChrome() {
  var collapseBtn = document.getElementById('sidebar-collapse');
  if (collapseBtn) {
    setNavCollapsed(document.documentElement.classList.contains('nav-collapsed'));
    collapseBtn.addEventListener('click', function () {
      setNavCollapsed(!document.documentElement.classList.contains('nav-collapsed'));
    });
  }
  // V zbalenom menu ukáž názov položky ako tooltip.
  document.querySelectorAll('#app-nav .nav').forEach(function (btn) {
    if (!btn.title) btn.title = btn.textContent.trim();
  });

  var trigger = document.getElementById('user-trigger');
  var dd = document.getElementById('user-dropdown');
  if (trigger && dd) {
    trigger.addEventListener('click', function (ev) {
      ev.stopPropagation();
      var open = dd.classList.contains('hidden');
      dd.classList.toggle('hidden', !open);
      trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
    dd.addEventListener('click', function (ev) {
      if (ev.target.closest('button')) closeUserMenu();
    });
    document.addEventListener('click', function (ev) {
      if (!ev.target.closest('#user-menu')) closeUserMenu();
    });
  }

  document.addEventListener('keydown', function (ev) {
    if (ev.key === 'Escape') closeUserMenu();
    if ((ev.ctrlKey || ev.metaKey) && (ev.key === 'k' || ev.key === 'K')) {
      var q = document.getElementById('global-search-q');
      if (q && q.offsetParent !== null) {
        ev.preventDefault();
        q.focus();
        q.select();
      }
    }
  });
}

export function initNav() {
  initShellChrome();
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
  dashboard: 'Prehľad',
  orgs: 'Organizačná štruktúra',
  accounts: 'Prevádzkovatelia',
  delegations: 'Delegácie oprávnení',
  pilots: 'Register PnD',
  certificates: 'Osvedčenia',
  operations: 'Žiadosti o let',
  flights: 'Denník letov',
  defects: 'Poruchy',
  incidents: 'Incidenty',
  documents: 'Dokumenty',
  reports: 'Reporty',
  calendar: 'Kalendár',
  map: 'Mapa — VFR / vzdušné priestory',
  geozones: 'Mapa GEO zón',
  'geozones-admin': 'Správa GEO zón',
  admin: 'Administrácia',
  uas: 'Register UAS',
  components: 'Technická dokumentácia',
  batteries: 'Batérie',
  monthly: 'Mesačné hlásenia o letoch',
  search: 'Výsledky hľadania'
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
