import { state } from '../core/state.js';
import { api } from '../core/api.js';
import { navigate } from '../core/nav.js';

export function beginFlightRequestFlow() {
  state.flightRequestMode = true;
  state.mapStartPick = true;
  state.openFlightForm = true;
  var level = Number(state.accessLevel) || 1;
  navigate(level === 3 ? 'geozones' : 'map');
}

export async function submitFlightRequestFromForm() {
  var orgId = document.getElementById('fl-org').value;
  var uasId = document.getElementById('fl-uas').value || null;
  var pilotId = document.getElementById('fl-pilot').value || null;
  var purpose = document.getElementById('fl-purpose').value.trim();
  var latRaw = document.getElementById('fl-lat').value;
  var lonRaw = document.getElementById('fl-lon').value;
  var radiusRaw = document.getElementById('fl-radius').value;
  var lat = latRaw !== '' ? Number(latRaw) : null;
  var lon = lonRaw !== '' ? Number(lonRaw) : null;
  var radiusM = radiusRaw !== '' ? Number(radiusRaw) : null;
  var takeoff = new Date(document.getElementById('fl-takeoff').value);
  var landing = new Date(document.getElementById('fl-landing').value);
  if (!orgId) throw new Error('Vyberte útvar');
  if (!uasId) throw new Error('Vyberte dron (UAS)');
  if (!pilotId) throw new Error('Vyberte pilota');
  if (!purpose) throw new Error('Vyplňte účel letu');
  if (lat == null || lon == null || !isFinite(lat) || !isFinite(lon))
    throw new Error('Najprv vyberte miesto letu na mape');
  if (!(landing > takeoff)) throw new Error('Pristátie musí byť po vzlete');

  var locLabel = (state.pendingFlightLocation && state.pendingFlightLocation.label)
    || (lat.toFixed(5) + ', ' + lon.toFixed(5));
  var created = await api('/api/v1/operations', {
    method: 'POST',
    body: JSON.stringify({
      title: purpose,
      operationTypeCode: 'FLIGHT_REQUEST',
      orgUnitId: orgId,
      plannedStart: takeoff.toISOString(),
      plannedEnd: landing.toISOString(),
      locationAddress: locLabel,
      locationLat: lat,
      locationLon: lon,
      locationGeoJson: null,
      description: radiusM ? ('Polomer oblasti: ' + Math.round(radiusM) + ' m') : null,
      isUrgent: false,
      urgentReason: null
    })
  });
  await api('/api/v1/operations/' + created.id + '/resources', {
    method: 'POST',
    body: JSON.stringify({ resourceType: 0, resourceId: uasId, roleCode: null })
  });
  await api('/api/v1/operations/' + created.id + '/resources', {
    method: 'POST',
    body: JSON.stringify({ resourceType: 1, resourceId: pilotId, roleCode: 'REMOTE_PILOT' })
  });
  await api('/api/v1/operations/' + created.id + '/submit', {
    method: 'POST',
    body: JSON.stringify({ rowVersion: created.rowVersion })
  });
  state.pendingFlightLocation = null;
  state.openFlightForm = false;
  state.flightRequestMode = false;
  document.querySelectorAll('.nav').forEach(function (b) {
    b.classList.toggle('active', b.dataset.page === 'operations');
  });
  navigate('operations');
}
