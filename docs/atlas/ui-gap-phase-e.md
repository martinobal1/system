# Fáza E — UI gap (Etapa 5) vs wwwroot

**Primárne UI:** `backend/src/IsSuas.Api/wwwroot/index.html`  
**Katalóg:** UI-001–075 (ATLAS Master v1.1, Etapa 5)

## 1. Chýbajúce / slabé UI patterny (spoločné)

| Pattern | Stav | Akcia |
|---------|------|-------|
| Confirm / badge / history | áno (Wave 12–13) | — |
| Globálne vyhľadávanie (UI-006) | áno (Wave 14) | `GET /api/v1/search` |
| MFA / re-auth (UI-002) | čiastočne (Wave 16) | `POST /auth/reauth`; TOTP neskôr |

## 2. Dashboardy

| Obrazovka | Stav | Plán |
|-----------|------|------|
| UI-003 Centrálny | áno (Wave 12/15) | `scope=central` |
| UI-004 Zložka | áno (Wave 15) | `scope=branch` + seed `ZLOZKA-PZ` |
| UI-005 Útvar | áno (Wave 12/15) | `scope=unit` |

## 3. Ďalšie kroky

1. MFA / OIDC / LPS (Fáza F).
2. Widget config ENT-099.
3. React `frontend/` nechať vedľajší.
