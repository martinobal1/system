# Fáza F — Technický checklist (Etapa 6)

Priorita podľa doplnku v1.1: **3.4/3.5 → 3.3 → 3.6 → 3.7**.

| ID | Požiadavka | Stav | Navrhovaný krok |
|----|------------|------|-----------------|
| F-3.1 | Stack kompatibilný, nie neschválený cloud | áno | Ponechať .NET/Postgres; MinIO self-hosted |
| F-3.2a | Vrstvy prezentačná/aplikačná/doménová | áno | — |
| F-3.2b | Asynchrónne spracovanie | čiastočne (Wave 16) | `IsSuasMaintenanceWorker` (nie Hangfire) |
| F-3.2c | Idempotentnosť | čiastočne | Zovšeobecniť ContentHash pattern |
| F-3.3a | Soft-delete / historizácia | áno | Wave 0 hotové |
| F-3.3b | Object storage + hash | áno | Local + MinIO provider (Wave 3+10) |
| F-3.3c | Elektronický podpis | nie | Stub Entity + overenie neskôr |
| F-3.3d | Retencia / archivácia | čiastočne | RetentionRule (Wave 3) |
| F-3.4a | OIDC / IAM MV SR | čiastočne (Wave 16) | DEV callback `dev-sso:{user}`; live token exchange 501 |
| F-3.4b | Permission v DB | áno (Wave 1) | Katalóg + RolePermission seed |
| F-3.4c | User sessions | áno (Wave 1) | `UserSessions` pri logine |
| F-3.4d | Delegovanie | áno (Wave 11) | API + runtime |
| F-3.4e | Break-glass / tech accounts | nie | Config + auditovaná elevácia |
| F-3.5 | Security testing v CI | čiastočne (Wave 16) | Security headers middleware; ZAP neskôr |
| F-3.6a | LPS SR adaptér | čiastočne | Offline map-check (Wave 10); live LPS neskôr |
| F-3.6b | Telemetry connectors | čiastočne | IntegrationConnector + file import |
| F-3.7 | SLA meranie / timeouty | nie | Activity + HttpClient Timeout + dashboard metrics |

## Hotové v tejto vlne (súvis F)

- Wave 10–11: MinIO/Smtp/LPS offline/OIDC redirect, Delegation
- Wave 16: SSO DEV callback, reauth, security headers, BackgroundService, Acc36
