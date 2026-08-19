import { api } from '../core/api.js';
import { statusLabelSk } from '../core/ui.js';

export async function loadUasOptions(pageSize) {
  try {
    const size = pageSize || 500;
    const uasData = await api('/api/v1/uas?page=1&pageSize=' + size);
    return uasData.items || uasData.Items || [];
  } catch (err) {
    console.warn('UAS list', err);
    return [];
  }
}

export function uasSelectHtml(uasList, emptyLabel) {
  if (!uasList.length) {
    return '<option value="">' + (emptyLabel || '(ziadne UAS v databaze)') + '</option>';
  }
  return uasList.map(function (u) {
    return '<option value="' + u.id + '">' + (u.registrationNumber || u.serialNumber) +
      ' — ' + u.manufacturer + ' ' + u.model + ' (' + statusLabelSk(u.status) + ')</option>';
  }).join('');
}

export async function createDemoUas() {
  const orgs = await api('/api/v1/org-units');
  const unit = orgs.find(function (o) { return o.code === 'UTVAR-A'; }) || orgs[0];
  const sn = 'UI-' + Date.now().toString().slice(-8);
  return api('/api/v1/uas', {
    method: 'POST',
    body: JSON.stringify({
      serialNumber: sn,
      manufacturer: 'DJI',
      model: 'M300',
      uasTypeCode: 'MULTI_ROTOR',
      owningOrgUnitId: unit.id,
      notes: 'Vytvorene z UI'
    })
  });
}
