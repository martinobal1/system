# Gap-audit IS ŠUAS vs Projekt ATLAS Master v1.1

**Dátum:** 2026-08-06  
**Zdroj:** `docs/specifikacia/PROJEKT_ATLAS_specifikacia_master_v1_1.md`  
**Kód:** monorepo `backend/` (primárne UI `IsSuas.Api/wwwroot`)  
**Stavy:** `pokryté` | `čiastočne` | `chýba`

---

## 1. MOD-01 až MOD-18

| Modul | Názov | Stav | Mapovanie v kóde | Medzera |
|-------|-------|------|------------------|---------|
| MOD-01 | Dashboard a pracovná plocha | čiastočne | `ReportingService` + stránka `dashboard` | Jeden súhrn; chýbajú UI-003/004/005 úrovne, widget config, globálne vyhľadávanie |
| MOD-02 | Organizačná štruktúra | čiastočne | `OrgUnit`, `OrgUnitService`, `OrgUnitsController` | OrgUnit spája organizáciu+útvar; chýba samostatná Organizácia, časová Organizačná väzba ako ENT-003 |
| MOD-03 | Register UAS | čiastočne | `Uas`, `UasAssignment`, `UasStatusHistory`, `FleetService` | Životný cyklus a pridelenie OK; chýbajú niektoré evidenčné polia zo slovníka TAB-012 |
| MOD-04 | Komponenty a vybavenie | čiastočne | `Component`, `Installation` | Chýba kompatibilita (ENT-017), oddelené „vybavenie“ vs komponent |
| MOD-05 | Batérie | čiastočne | `Battery`, `BatteryAssignment` | Chýbajú ENT-020–023 (kompatibilita, technický/prevádzkový záznam, história stavu) |
| MOD-06 | Piloti a personál | čiastočne | `Pilot`, `Person`, `PersonFunctionAssignment`, `PilotRestriction`, status history | Wave 8: funkcie + obmedzenia; chýba história zaradenia osoby |
| MOD-07 | Oprávnenia/osvedčenia/výcvik | čiastočne | `PilotCertificate`+`CertificateScope`, `PilotQualification`+verification, `TrainingCatalogItem`/`TrainingActivity`/`Participation`/`Result` | Wave 8: katalóg, účasť, overenie, rozsah; flat `PilotTraining` ostáva ako sync |
| MOD-08 | Plánovanie a schvaľovanie letov | čiastočne | `Operation`, `OperationResource`, SoD approve | Chýba opakovanie, schvaľovacie kroky, podmienky schválenia, personál s funkciami |
| MOD-09 | Evidencia letov a telemetria | čiastočne | `Flight`, `FlightTrackPoint`, `TelemetryImport` | Track/GeoJSON OK; chýbajú účastníci, konfigurácia, odchýlky, normalizované telemetrické údaje ako ENT-053 |
| MOD-10 | Údržba, kontroly a opravy | čiastočne | `MaintenancePlan`, `WorkOrder`, `MaintenanceRecord` | Chýba servisná požiadavka, interval ako ENT, náhradné diely, externý servis, uvoľnenie |
| MOD-11 | Poruchy a technická spôsobilosť | čiastočne | `Defect` (+ immutable report) | Chýba samostatné technické posúdenie, technické obmedzenie, história tech. stavu |
| MOD-12 | Incidenty a nehody | čiastočne | `SafetyEvent` + participants/assets/causes/obligations/fleet alert/histories (Wave 9) | Dôkaz bez väzby na Document; evidence ešte zjednodušené |
| MOD-13 | Dokumenty a archív | čiastočne | `ManagedDocument` (verzie v jednej tabuľke) | Chýba trilógia Document/Version/File, spis, e-podpis, retencia, object storage |
| MOD-14 | Reporty a výkazy | čiastočne | `OfficialReport`, CSV generate | Chýbajú definície reportov, parametre, behy; PDF/A |
| MOD-15 | Kontrolná a auditná činnosť | **chýba** | — | Celý modul ENT-104–111 / UI-066–069 |
| MOD-16 | Účty, roly, oprávnenia | čiastočne | `Account`, `Role`, `RoleAssignment`, JWT login, SSO stub | Oprávnenia hardcoded; chýba Permission DB, delegovanie, relácie, MFA |
| MOD-17 | Upozornenia a úlohy | čiastočne | `AppNotification`, `WorkTask` | Chýbajú príjemcovia, doručenie, šablóny, eskalácia, história úloh |
| MOD-18 | Číselníky a konfigurácia | čiastočne | `LookupCode`/`LookupItem`, `ConfigRule`, `IntegrationConnector` | Chýba história položky, import/export/integration event entity |

---

## 2. BP-01 až BP-22

| BP | Proces | Stav | Poznámka |
|----|--------|------|----------|
| BP-01 | Zápis UAS | čiastočne | Create → submit → approve + registration; nie všetky stavy/vrátenia zo špecifikácie |
| BP-02 | Zmena údajov UAS | čiastočne | Update/transition; chýba formalizovaný návrh zmeny s klasifikáciou |
| BP-03 | Pozastavenie/obnovenie UAS | čiastočne | Status transitions + auto zo safety/defect |
| BP-04 | Vyradenie UAS | čiastočne | Lifecycle stavy; nie plný výmazový proces |
| BP-05 | Pridelenie/presun UAS | čiastočne | `UasAssignment` s exclusive check |
| BP-06 | Zápis pilota | čiastočne | Create pilot |
| BP-07 | Zmena stavu pilota | čiastočne | Status history |
| BP-08 | Interné osvedčenie | čiastočne | `PilotCertificate` + `CertificateScope` (Wave 8) |
| BP-09 | Školenia/výcvik | čiastočne | Katalóg → aktivita → účasť → výsledok (+ sync na kartu) |
| BP-10 | Plánovanie operácie | čiastočne | Draft + resources |
| BP-11 | Schválenie operácie | čiastočne | Approve/return/reject + SoD |
| BP-12 | Neodkladná operácia | čiastočne | Urgent → WorkTask |
| BP-13 | Vykonanie letu | čiastočne | Create/ready/close |
| BP-14 | Import telemetrie | čiastočne | SHA256 idempotent + pair + GeoJSON |
| BP-15 | Životný cyklus batérií | čiastočne | Assignment + counters pri close |
| BP-16 | Údržba | čiastočne | Plans + WO + complete |
| BP-17 | Porucha | čiastočne | Report immutable + resolve |
| BP-18 | Incident | čiastočne | Wave 9: účastníci, príčiny, oznamovacie povinnosti, fleet alert |
| BP-19 | Dokumenty/verzie | čiastočne | Version supersede + expiration notify |
| BP-20 | Účty/roly | čiastočne | CRUD account, assign role, deactivate |
| BP-21 | Reporty/výkazy | čiastočne | Generate + frozen official |
| BP-22 | Kontrolná činnosť | **chýba** | MOD-15 neexistuje (audit trail ≠ BP-22) |

---

## 3. UI-001 až UI-075 vs `wwwroot`

Primárne UI: `backend/src/IsSuas.Api/wwwroot/index.html` (stránky: dashboard, orgs, accounts, pilots, operations, flights, maintenance, defects, incidents, documents, reports, calendar, admin, tasks, uas, components, batteries).

| UI | Názov | Stav | wwwroot / API |
|----|-------|------|---------------|
| UI-001 | Prihlásenie | čiastočne | Lokálny login + DEV SSO callback (Wave 16) |
| UI-002 | MFA / re-auth | čiastočne | Wave 16: `POST /api/v1/auth/reauth` (nie TOTP) |
| UI-003 | Centrálny dashboard | čiastočne | Wave 12/15: `scope=central` |
| UI-004 | Dashboard zložky | čiastočne | Wave 15: `scope=branch` + `ZLOZKA-PZ` |
| UI-005 | Dashboard útvaru | čiastočne | Wave 12/15: `scope=unit` |
| UI-006 | Globálne vyhľadávanie | čiastočne | Wave 14: `GET /api/v1/search` + topbar; deep-link detailov len kde existujú |
| UI-007 | Centrum notifikácií | čiastočne | Vnorené / API notifications |
| UI-008 | Pracovné úlohy | čiastočne | `tasks` |
| UI-009–011 | Org strom/detail/správa | čiastočne | `orgs` |
| UI-012–015 | UAS | čiastočne | `uas` |
| UI-016–018 | Komponenty | čiastočne | `components` (bez kompatibility UI) |
| UI-019–022 | Batérie | čiastočne | `batteries` (bez tech histórie UI) |
| UI-023–031 | Piloti/spôsobilosť | čiastočne | `pilots` (zjednodušené) |
| UI-032–037 | Operácie/schválenie | čiastočne | `operations` (+ calendar) |
| UI-038–043 | Lety/telemetria | čiastočne | `flights` (mapa chýba) |
| UI-044–051 | Údržba/poruchy | čiastočne | `maintenance`, `defects` |
| UI-052–058 | Incidenty | čiastočne | `incidents` |
| UI-059–063 | Dokumenty | čiastočne | `documents` |
| UI-064–065 | Reporty | čiastočne | `reports` |
| UI-066–069 | Kontroly MOD-15 | **chýba** | — |
| UI-070–072 | Účty/roly/relácie | čiastočne | `accounts`; relácie/delegácie chýbajú |
| UI-073 | Auditná stopa | čiastočne | `admin` audit search |
| UI-074 | Číselníky/config | čiastočne | `admin` |
| UI-075 | Importy/exporty/integrácie | čiastočne | Connectors v admin; nie event monitoring |

**Súhrn UI:** ~45 čiastočne, ~30 chýba, 0 plne podľa Etapy 5 (chýbajú zdieľané UI patterny: confirm, history panel, optimistic lock UX konzistentne).

---

## 4. Etapa 6 — technický checklist (3.1–3.7)

| Bod | Téma | Stav | Stav kódu / infra |
|-----|------|------|-------------------|
| 3.1 | Technologická neutralita / stack | áno | .NET 10, EF Core, Postgres, JWT; nie viazané na neschválený cloud |
| 3.2 | Logická architektúra | čiastočne | Domain/Application/Infrastructure/Api; chýba asynchrónny job pipeline, procesný orchestrátor |
| 3.2 | Idempotentnosť | čiastočne | Telemetry SHA256; nie všeobecný pattern |
| 3.3 | Dátová / súborová architektúra | čiastočne | Soft-delete + audit immutability; MinIO v docker-compose **bez** C# klienta; dokumenty = meta+hash |
| 3.3 | Elektronické podpisy | **nie** | — |
| 3.4 | Identita / IAM | čiastočne | Lokálny login + SSO stub; chýba OIDC, MFA, break-glass, tech accounts |
| 3.4 | Autorizácia | čiastočne | Role+org path+SoD; permissions nie v DB |
| 3.5 | Kybernetická bezpečnosť | čiastočne | JWT, BCrypt, sensitive doc/safety gates; chýba systematické security testing v CI |
| 3.6 | LPS SR adaptér | **nie** | — |
| 3.6 | Telemetrické konektory | čiastočne | `IntegrationConnector` + file import; nie plné importné profily |
| 3.7 | Výkonnostné SLA | **nie** | Žiadne meranie/timeout policy pre externé systémy |

---

## 5. Priority (pre Fázu D/F)

1. **MOD-15** + BP-22 — úplne chýba  
2. **DO-01** Permission/RolePermission, Session, Delegation  
3. **DO-07** document trilógia + object storage  
4. **DO-08** doručovanie / eskalácia  
5. Spresnenie DO-02–06 (kompatibility, schvaľovacia trasa, servisná požiadavka, …)  
6. LPS SR, e-mail, PDF, OIDC (Fáza F)

---

## 6. Čo už spĺňa IS ŠUAS dobre (ponechať)

- Soft-delete + append-only audit v `IsSuasDbContext`
- Org-scope path prefix + SoD pri schvaľovaní
- UAS lifecycle, exclusive assignment
- Telemetry import idempotency
- Defect original report immutability
- Official report freeze
- Acc* integračné testy pre jadro MVP
