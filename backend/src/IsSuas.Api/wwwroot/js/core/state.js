// Klientsky stav aplikacie: prihlaseny uzivatel a rozpracovane volby na jednotlivych strankach.
export const state = {
  token: localStorage.getItem('issuas.token'),
  displayName: localStorage.getItem('issuas.name') || '',
  userName: localStorage.getItem('issuas.userName') || '',
  personId: localStorage.getItem('issuas.personId') || '',
  accountId: localStorage.getItem('issuas.accountId') || '',
  accessLevel: Number(localStorage.getItem('issuas.accessLevel') || '1') || 1,
  licenseStatus: localStorage.getItem('issuas.licenseStatus') || 'NotApplicable',
  /** @type {{ lat:number, lon:number, radiusM:number, label:string }|null} */
  pendingFlightLocation: null,
  openFlightForm: false,
  mapStartPick: false,
  flightRequestMode: false,
  opEditLocationPick: false,
  operationDetailId: null,
  operationEditing: false,
  techDocModelId: null,
  techDocTab: 'specs',
  techDocCreating: false,
  flightDetailId: null,
  uasDetailId: null,
  pilotDetailId: null,
  pilotRegisterTab: 'a1',
  monthlyYear: null,
  monthlyMonth: null,
  monthlyOrgUnitId: null,
  orgBranchFilter: 'all',
  orgCollapsed: {},
  orgUserPick: false,
  orgPendingUserOrgId: null,
  orgPendingUserOrgName: null,
  certSearchQ: '',
  dashboardScope: 'central',
  dashboardOrgUnitId: null
};

export function applyLoginSession(res) {
  state.token = res.accessToken;
  state.displayName = res.displayName || '';
  state.userName = res.userName || '';
  state.personId = res.personId || '';
  state.accountId = res.accountId || '';
  state.accessLevel = Number(res.accessLevel) || 1;
  state.licenseStatus = res.licenseStatus || 'NotApplicable';
  localStorage.setItem('issuas.token', state.token);
  localStorage.setItem('issuas.name', state.displayName);
  localStorage.setItem('issuas.userName', state.userName);
  localStorage.setItem('issuas.personId', state.personId);
  localStorage.setItem('issuas.accountId', state.accountId);
  localStorage.setItem('issuas.accessLevel', String(state.accessLevel));
  localStorage.setItem('issuas.licenseStatus', state.licenseStatus);
}

export function clearSession(clearStorage = true) {
  state.token = null;
  state.displayName = '';
  state.userName = '';
  state.personId = '';
  state.accountId = '';
  state.accessLevel = 1;
  state.licenseStatus = 'NotApplicable';
  if (clearStorage) {
    localStorage.removeItem('issuas.token');
    localStorage.removeItem('issuas.name');
    localStorage.removeItem('issuas.userName');
    localStorage.removeItem('issuas.personId');
    localStorage.removeItem('issuas.accountId');
    localStorage.removeItem('issuas.accessLevel');
    localStorage.removeItem('issuas.licenseStatus');
  }
}
