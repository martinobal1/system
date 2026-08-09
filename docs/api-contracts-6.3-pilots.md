# Modul 6.3 — Piloti

## API `/api/v1/pilots`

| Metóda | Cesta | Popis |
|---|---|---|
| GET | `/pilots` | Zoznam (org scope) |
| GET | `/pilots/{id}` | Detail + kvalifikácie, osvedčenia, výcvik, história |
| POST | `/pilots` | Nový koncept pilota |
| PATCH | `/pilots/{id}/status` | Zmena stavu |
| POST | `/pilots/{id}/qualifications` | Pridať kvalifikáciu |
| POST | `/pilots/{id}/certificates` | Koncept osvedčenia |
| POST | `/pilots/{id}/certificates/{cid}/issue` | Vydanie (nemenná verzia, predchádzajúce → Superseded) |
| POST | `/pilots/{id}/trainings` | Záznam školenia/skúšky |
| GET | `/pilots/{id}/eligibility` | Kontrola spôsobilosti do operácie |

## Pravidlá
- Stavy: Draft, Active, Fit, Expiring, Unfit, Suspended, Inactive
- Do operácie len pri platnom Issued osvedčení + stave Fit/Active/Expiring + platnej kondičnej spôsobilosti
- Seed: PIL-001 spôsobilý, PIL-002 nespôsobilý
