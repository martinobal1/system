import { api } from './api.js';

// Rezim udrzby: pocas neho server vracia na vsetky API volania 503 (aj na prihlasenie, aj gestorovi).
// Tu je len zobrazenie obrazovky udrzby — o pristupe rozhoduje vzdy server.

var pollTimer = null;

export async function fetchMaintenanceStatus() {
  try {
    return await api('/api/v1/system/maintenance');
  } catch (_) {
    return null;
  }
}

export function initMaintenance() {
  document.getElementById('maint-retry').addEventListener('click', retryMaintenance);
}

export function showMaintenanceScreen(status) {
  document.getElementById('maint-message').textContent =
    (status && status.message) || 'Prebieha plánovaná údržba systému. Skúste to prosím neskôr.';
  var endEl = document.getElementById('maint-end');
  endEl.textContent = status && status.plannedEndAt
    ? 'Predpokladaný koniec: ' + new Date(status.plannedEndAt).toLocaleString('sk-SK')
    : '';
  endEl.classList.toggle('hidden', !endEl.textContent);
  document.getElementById('app-view').classList.add('hidden');
  document.getElementById('login-view').classList.add('hidden');
  document.getElementById('maintenance-view').classList.remove('hidden');
  if (!pollTimer) pollTimer = setInterval(retryMaintenance, 30000);
}

// Pri starte aplikacie: ak je udrzba zapnuta, rovno ukaz obrazovku udrzby.
export async function checkMaintenanceOnStart() {
  var status = await fetchMaintenanceStatus();
  if (status && status.isActive) showMaintenanceScreen(status);
}

async function retryMaintenance() {
  var status = await fetchMaintenanceStatus();
  if (!status) return;
  if (!status.isActive) {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = null;
    window.location.reload();
    return;
  }
  showMaintenanceScreen(status);
}
