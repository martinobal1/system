# IS ŠUAS

Informačný systém štátnych bezpilotných leteckých systémov (MV SR).

## Rýchly štart (DEV)

### Backend
```bash
cd backend
dotnet run --project src/IsSuas.Api
```
API: http://localhost:5211 · Swagger: http://localhost:5211/swagger

Demo login: `gestor` / `Heslo123!`

### Frontend
```bash
cd frontend
npm install
npm run dev
```
UI: http://localhost:5173

### Infra
```bash
cd infra
docker compose up -d
```

Dokumentácia: [docs/README.md](docs/README.md)
