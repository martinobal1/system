# Modul 6.5 — Lety a telemetria

## API
- `GET/POST /api/v1/flights` (+ `from-operation`, `standalone`)
- `POST /flights/{id}/ready`, `/close`
- `POST /api/v1/telemetry-imports` — checksum idempotencia
- `POST /telemetry-imports/{id}/pair`

## Pravidlá
- 1 operácia = N letov
- Samostatný let vyžaduje dôvod
- Import rovnakého obsahu → `DuplicateIgnored`
- Uzatvorenie letu aplikuje počítadlá UAS/batérie/pilota **presne raz** (`CountersApplied`)
