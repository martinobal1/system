# IS ŠUAS — README

Informačný systém štátnych bezpilotných leteckých systémov (Fáza 0 + MVP jadro 6.1/6.2).

## Stack

- Backend: .NET 10 / ASP.NET Core + EF Core + PostgreSQL (DEV default: InMemory)
- Frontend: React + TypeScript + Vite + MUI (SK)
- Infra: Docker Compose (Postgres 16, MinIO), GitHub Actions CI

## Spustenie backendu

```bash
cd backend
dotnet run --project src/IsSuas.Api
```

Swagger: `/swagger`  
API: `/api/v1`

## Spustenie frontendu

```bash
cd frontend
npm install
npm run dev
```

## Dokumentácia

- [Schéma](schema-phase0.md)
- [API kontrakty 6.1/6.2](api-contracts-6.1-6.2.md)
- [6.3 Piloti](api-contracts-6.3-pilots.md)
- [6.4 Operácie](api-contracts-6.4-operations.md)
- [6.5 Lety](api-contracts-6.5-flights.md)
- [6.6 Údržba](api-contracts-6.6-maintenance.md)
- [6.7 Incidenty](api-contracts-6.7-safety.md)
- [6.8 Dokumenty](api-contracts-6.8-documents.md)
- [6.9 Reporty](api-contracts-6.9-reports.md)
- [6.10 Administrácia](api-contracts-6.10-admin.md)

- [Režim údržby a núdzový vypínač](maintenance-mode.md)
- [Kôš (mazanie záznamov)](kos.md)
