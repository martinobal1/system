# Modul 6.8 — Dokumenty, checklisty, posúdenie rizika

## API
- `GET/POST /api/v1/documents` (+ `POST /refresh-expirations`)
- `GET/POST /api/v1/form-templates`
- `GET/POST /api/v1/form-submissions` (+ `/{id}/answers`)
- `GET/POST /api/v1/risk-assessments`
- `GET /api/v1/operations/{id}/document-block`

## Pravidlá
- Dokument: typ, verzia, platnosť, vlastník, citlivosť, checksum; nahradenie = nová verzia (stará `Superseded`)
- Max 1 aktuálna verzia v rámci `DocumentFamilyId`
- Exspirácia → stav `Expired` + notifikácia `DOCUMENT_EXPIRED`
- Šablóny verziované; submission drží `TemplateVersion` z času vyplnenia
- Checklist: blokujúce nevyplnené položky bránia dokončeniu a môžu blokovať submit operácie
- Riziko: `score = likelihood × severity` (1–5), klasifikácia Low/Medium/High/Critical
