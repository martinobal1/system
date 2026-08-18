# IS ŠUAS — API kontrakty `/api/v1` (moduly 6.1 a 6.2)

Base URL: `http://localhost:5xxx/api/v1`  
Auth: `Authorization: Bearer <JWT>` (okrem login)

## Auth

### `POST /auth/login`
```json
{ "userName": "gestor", "password": "huginko123" }
```
Response 200:
```json
{
  "accessToken": "...",
  "expiresAt": "2026-08-03T20:00:00+00:00",
  "accountId": "...",
  "userName": "gestor",
  "displayName": "Ján Gestor"
}
```
401 — neplatné údaje / neaktívny účet.

## Organizácie

| Metóda | Cesta | Oprávnenie |
|---|---|---|
| GET | `/org-units` | orgunits.read |
| POST | `/org-units` | orgunits.write |
| PATCH | `/org-units/{id}` | orgunits.write |
| POST | `/org-units/{id}/move` | orgunits.write |

Create body: `{ "code", "name", "parentId?" }`  
Move body: `{ "newParentId?", "reason", "rowVersion" }`

## Účty a roly

| Metóda | Cesta | Oprávnenie |
|---|---|---|
| GET | `/accounts` | accounts.read |
| POST | `/accounts` | accounts.write |
| PATCH | `/accounts/{id}/status` | accounts.write |
| GET | `/accounts/{id}/role-assignments` | accounts.read |
| POST | `/accounts/{id}/role-assignments` | roles.assign |
| POST | `/accounts/{id}/role-assignments/{assignmentId}/deactivate` | roles.assign |

## Číselníky a audit

| Metóda | Cesta | Oprávnenie |
|---|---|---|
| GET | `/lookups/{code}` | lookups.read |
| GET | `/audit-events?from&to&entityType&entityId&action` | audit.read |

## Flotila UAS

| Metóda | Cesta | Oprávnenie |
|---|---|---|
| GET | `/uas?page&pageSize&search` | uas.read |
| GET | `/uas/{id}` | uas.read (+ org scope) |
| POST | `/uas` | uas.write |
| POST | `/uas/{id}/submit` | uas.write |
| POST | `/uas/{id}/approve` | uas.approve + SoD |
| POST | `/uas/{id}/transition` | uas.write |
| POST | `/uas/{id}/assignments` | uas.write |

Approve: prideľuje `registrationNumber`, status → `Registered`.  
SoD: schvaľovateľ ≠ `submittedByAccountId`.  
Assignments: druhé aktívne pridelenie → 409.

## Komponenty a batérie

| Metóda | Cesta | Oprávnenie |
|---|---|---|
| GET/POST | `/components` | components.read / write |
| POST | `/components/{id}/installations` | components.write |
| POST | `/components/{id}/installations/{installationId}/remove` | components.write |
| GET/POST | `/batteries` | batteries.read / write |

## Chybové kódy

| HTTP | Význam |
|---|---|
| 400 | Validácia / nepovolený stavový prechod |
| 401 | Neautorizovaný / deaktivovaný účet |
| 403 | Chýba oprávnenie / org scope / SoD / sysadmin bez vecného prístupu |
| 404 | Nenájdené |
| 409 | Konflikt (duplicita SN, prekryv pridelenia, rowVersion) |

## Demo účty (seed)

| User | Rola | Heslo |
|---|---|---|
| gestor | CentralGestor | huginko123 |
| schvalovatel | Approver | Heslo123! |
| spravca.a | UnitAdministrator (Útvar A) | Heslo123! |
| spravca.b | UnitAdministrator (Útvar B) | Heslo123! |
| sysadmin | SystemAdministrator | Heslo123! |
| neaktivny | Deactivated | Heslo123! |
