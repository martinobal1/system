# Modul 6.9 — Dashboard, kalendár, reporty

## API
- `GET /api/v1/dashboard`
- `GET /api/v1/calendar?from=&to=`
- `POST /api/v1/reports/generate`
- `POST /api/v1/reports/export.csv`
- `GET/POST /api/v1/official-reports` (+ `/{id}/approve`)

## Pravidlá
- Dashboard podľa org. rozsahu + role hint
- Reporty: UAS, PILOTS, FLIGHTS, OPERATIONS, BATTERIES, MAINTENANCE, DEFECTS, INCIDENTS
- `operations_count` a `flights_count` sú oddelené ukazovatele (bez dvojitého započítania)
- Oficiálny výkaz = zamrznutý `SnapshotJson` + `RulesJson`; po schválení sa pri zmene dát nemení
- Oprava = nová verzia v rodine; SoD: pripravovateľ ≠ schvaľovateľ
