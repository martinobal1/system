# Modul 6.4 — Operácie a schvaľovanie

## Tok
`Draft` → `Submitted` → `Approved` | `Returned` | `Rejected`  
(`Returned` → znova `Submitted`)

## API `/api/v1/operations`
- GET/POST list/create
- PATCH update (iba Draft/Returned)
- POST `{id}/resources` — UAS/pilot/batéria/komponent (kontrola spôsobilosti + časový konflikt)
- POST `{id}/submit` — vyžaduje UAS + pilota
- POST `{id}/approve|return|reject` — SoD: navrhovateľ ≠ schvaľovateľ
- GET `/api/v1/work-tasks` — úlohy (neodkladná operácia vytvorí follow-up)

## ACC
- ACC-4 draft→submit→approve
- ACC-5 urgent → WorkTask URGENT_OPERATION_FOLLOWUP
- ACC-14 vlastné schválenie → 403
