# Modul 6.10 — Administrácia, číselníky, integrácie, audit

## API
- `GET/POST /api/v1/lookups` (+ `/{code}/items`, `items/{id}/deactivate`)
- `GET/POST /api/v1/config-rules` (+ `effective/{key}`)
- `GET/POST /api/v1/integrations` (+ `/{id}/runs`)
- `GET /api/v1/audit-events` (+ `export.csv`)
- `GET /api/v1/system/info`

## Pravidlá
- Položka číselníka sa iba deaktivuje, nikdy nemaže
- Config pravidlá sú verziované; účinnosť od času; neovplyvňujú minulé uzavreté záznamy
- Integrácie ukazujú len `SecretRef`, nie tajomstvo
- Audit je append-only; export je sám auditovaný (`Audit.Export`)
