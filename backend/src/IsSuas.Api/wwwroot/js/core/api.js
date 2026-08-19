import { state } from './state.js';

let unauthorizedHandler = null;

// Reakcia na vyprsanu sesiu je vec UI vrstvy, preto si ju app.js zaregistruje sem.
export function setUnauthorizedHandler(handler) {
  unauthorizedHandler = handler;
}

export async function api(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  if (state.token) headers.Authorization = 'Bearer ' + state.token;

  let res;
  try {
    res = await fetch(path, { ...options, headers });
  } catch (e) {
    throw new Error('Nepodarilo sa spojit s API. Spustite: dotnet run --project src/IsSuas.Api a nechajte terminal otvoreny.');
  }

  if (res.status === 401) {
    if (!path.includes('/auth/login') && unauthorizedHandler) unauthorizedHandler();
    let detail = 'Neplatne prihlasovacie udaje';
    try {
      const body = await res.json();
      detail = body.detail || body.title || detail;
    } catch {}
    throw new Error(detail);
  }

  if (!res.ok) {
    let detail = 'Chyba ' + res.status;
    try {
      const body = await res.json();
      detail = body.detail || body.title || detail;
    } catch {}
    throw new Error(detail);
  }

  if (res.status === 204) return null;
  return res.json();
}
