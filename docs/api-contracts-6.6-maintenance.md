# Modul 6.6 — Údržba a poruchy

## API
- `/api/v1/maintenance-plans`
- `/api/v1/work-orders` (+ `/{id}/complete`)
- `/api/v1/defects` (+ `/{id}/resolve`)
- `/api/v1/uas/{id}/block-status`

## Pravidlá
- Po uzatvorení letu sa prepočítajú plány (flight count / hours / cycles)
- Overdue + `BlockWhenOverdue` → UAS `InMaintenance` a blokovanie do operácie
- Blokujúca porucha pozastaví UAS; pôvodné hlásenie je nemenné
- Obnova vyžaduje technické posúdenie + skúšku + iného overovateľa
