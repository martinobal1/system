# Modul 6.7 — Incidenty a bezpečnostné udalosti

## API
- `GET/POST /api/v1/safety-events`
- `GET /api/v1/safety-events/{id}`
- `POST /api/v1/safety-events/{id}/submit`
- `POST /api/v1/safety-events/{id}/investigation`
- `POST /api/v1/safety-events/{id}/evidences`
- `POST /api/v1/safety-events/{id}/actions`
- `POST /api/v1/safety-events/{id}/close`
- `POST /api/v1/corrective-actions/{id}/complete|verify|waive`
- `GET /api/v1/notifications`

## Pravidlá
- Prvotné hlásenie: minimálne Title + InitialReport; po `submit` je `InitialReportLocked`
- Dôkazy majú checksum; nahradenie len novou verziou (`supersedesEvidenceId`)
- Nápravné opatrenie: overovateľ ≠ vlastník
- Kritická závažnosť pri submit → notifikácie (deduplikované) + voliteľné pozastavenie UAS
- Uzatvorenie: všetky povinné opatrenia Verified/Waived, alebo `closureExceptionReason`
