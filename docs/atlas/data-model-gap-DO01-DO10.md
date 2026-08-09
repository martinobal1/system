# Fáza D — Porovnanie dátového modelu DO-01–10

**Zdroj entít:** ATLAS Master v1.1, kapitola 4.3 (ENT-001–124)  
**Kód:** `backend/src/IsSuas.Domain` + EF migrácie  
**Stavy:** `sedí` | `čiastočne` | `chýba` | `naviac` (v kóde bez ENT)

Konvencia: existujúce tabuľky **nepremenúvame** na `TAB-xxx`; mapujeme aliasom.

---

## DO-01 — Organizácie, používatelia a prístup

| ENT | Názov | Trieda / tabuľka | Stav | Poznámka |
|-----|-------|------------------|------|----------|
| ENT-001 | Organizácia | — / `OrgUnit` (root) | čiastočne | Root OrgUnit slúži ako organizácia; chýba samostatná entita |
| ENT-002 | Organizačný útvar | `OrgUnit` | čiastočne | OK základ; doplniť typ/úroveň podľa slovníka |
| ENT-003 | Organizačná väzba | `OrgUnit.ParentId` + `OrgUnitMoveHistory` | čiastočne | Hierarchia áno; nie plná časová väzba ako samostatná ENT |
| ENT-004 | Osoba | `Person` | sedí | |
| ENT-005 | Používateľský účet | `Account` | sedí | |
| ENT-006 | Rola | `Role` | sedí | |
| ENT-007 | Oprávnenie | — | **chýba** | Konstanty v `Permissions.cs` |
| ENT-008 | Oprávnenie roly | — | čiastočne | Seed `RolePermission` + runtime `PermissionCatalog` |
| ENT-009 | Pridelenie roly | `RoleAssignment` | sedí | |
| ENT-010 | Delegované oprávnenie | `DelegatedPermission` | sedí | Wave 11; API + `HasPermission` |
| ENT-011 | Používateľská relácia | — | **chýba** | JWT bez persistovanej relácie |

---

## DO-02 — UAS, komponenty a batérie

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-012 | UAS | `Uas` | čiastočne | Jadro OK |
| ENT-013 | Pridelenie UAS | `UasAssignment` | sedí | |
| ENT-014 | História stavu UAS | `UasStatusHistory` | sedí | |
| ENT-015 | Komponent alebo vybavenie | `Component` | čiastočne | |
| ENT-016 | Pridelenie a montáž | `Installation` | čiastočne | |
| ENT-017 | Kompatibilita komponentu | `ComponentCompatibility` | sedí | Wave 5 |
| ENT-018 | Batéria | `Battery` | čiastočne | |
| ENT-019 | Pridelenie batérie | `BatteryAssignment` | sedí | |
| ENT-020 | Kompatibilita batérie | `BatteryCompatibility` | sedí | Wave 5 |
| ENT-021 | Technický záznam batérie | `BatteryTechnicalRecord` | sedí | Wave 5 |
| ENT-022 | Prevádzkový záznam batérie | `BatteryOperationRecord` | sedí | Wave 5 |
| ENT-023 | História stavu batérie | `BatteryStatusHistory` | sedí | Wave 5 |

---

## DO-03 — Piloti a odborná spôsobilosť

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-024 | Organizačné zaradenie osoby | `Person.HomeOrgUnitId` | čiastočne | Nie história |
| ENT-025 | Elektronická karta pilota | `Pilot` | čiastočne | |
| ENT-026 | História stavu pilota | `PilotStatusHistory` | sedí | |
| ENT-027 | Prevádzková/odborná funkcia | `PersonFunctionAssignment` | sedí | Wave 8 |
| ENT-028 | Obmedzenie pilota | `PilotRestriction` | sedí | Wave 8; blokuje eligibility |
| ENT-029 | Externý doklad | `PilotQualification` | čiastočne | |
| ENT-030 | Overenie externého dokladu | `QualificationDocumentVerification` | sedí | Wave 8 |
| ENT-031 | Osvedčenie gestora | `PilotCertificate` | čiastočne | |
| ENT-032 | Rozsah osvedčenia | `CertificateScope` | sedí | Wave 8 |
| ENT-033 | Katalóg vzdelávacích aktivít | `TrainingCatalogItem` | sedí | Wave 8 |
| ENT-034 | Výcviková aktivita (termín) | `TrainingActivity` + `PilotTraining` | sedí | Wave 8; flat sync na kartu |
| ENT-035 | Účasť na aktivite | `TrainingParticipation` | sedí | Wave 8 |
| ENT-036 | Výsledok aktivity | `TrainingActivityResult` | sedí | Wave 8 |

---

## DO-04 — Letové operácie, lety a telemetria

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-037 | Plán letovej operácie | `Operation` | čiastočne | |
| ENT-038 | Pravidlo opakovania | `OperationRecurrenceRule` | sedí | Wave 6 |
| ENT-039 | UAS operácie | `OperationResource` (Uas) | čiastočne | |
| ENT-040 | Vybavenie operácie | `OperationResource` | čiastočne | |
| ENT-041 | Batéria operácie | `OperationResource` | čiastočne | |
| ENT-042 | Personál operácie | `OperationResource` (Pilot) | čiastočne | Bez funkcie |
| ENT-043 | Schvaľovací krok | `OperationApprovalStep` | sedí | Wave 6 |
| ENT-044 | Rozhodnutie o operácii | `OperationDecision` + status history | sedí | Wave 6 |
| ENT-045 | Podmienka schválenia | `OperationApprovalCondition` | sedí | Wave 6 |
| ENT-046 | Letový záznam | `Flight` | čiastočne | |
| ENT-047 | Účastník letu | `Flight.PilotId` | čiastočne | Jeden pilot |
| ENT-048 | Konfigurácia UAS pri lete | — | **chýba** | |
| ENT-049 | Použitie batérie pri lete | `Flight.BatteryId` | čiastočne | |
| ENT-050 | Použitie vybavenia pri lete | — | **chýba** | |
| ENT-051 | Kontrola letu | checklist fields | čiastočne | |
| ENT-052 | Import telemetrie | `TelemetryImport` | sedí | |
| ENT-053 | Telemetrický údaj | `FlightTrackPoint` | čiastočne | Body áno; nie generický measurement |
| ENT-054 | Odchýlka/udalosť letu | `DeviationNotes` | čiastočne | Text, nie entita |
| ENT-122 | Geo objekt operácie | `Operation.LocationGeoJson` | čiastočne | |
| ENT-123 | Geo objekt letu | `TrackGeoJson` | čiastočne | |
| ENT-124 | Mapová kontrola operácie | `OperationMapCheck` | sedí | Wave 10; Offline LPS adapter |

---

## DO-05 — Údržba, poruchy a technická spôsobilosť

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-055 | Plán údržby | `MaintenancePlan` | čiastočne | |
| ENT-056 | Interval údržby | polia na plane | čiastočne | |
| ENT-057 | Servisná požiadavka | `ServiceRequest` | sedí | Wave 7 |
| ENT-058 | Pracovný príkaz | `WorkOrder` | sedí | +ServiceRequestId |
| ENT-059 | Úkon údržby | `MaintenanceRecord` | čiastočne | |
| ENT-060 | Výsledok tech. kontroly | — | **chýba** | |
| ENT-061 | Náhradný diel | `PartsUsed` text | čiastočne | |
| ENT-062 | Externý servis | — | **chýba** | |
| ENT-063 | Porucha | `Defect` | čiastočne | |
| ENT-064 | Technické posúdenie | polia na Defect | čiastočne | |
| ENT-065 | Technické obmedzenie | `TechnicalRestriction` | sedí | Wave 7 |
| ENT-066 | História tech. stavu | `TechnicalStatusHistory` | sedí | Wave 7 |
| ENT-067 | Uvoľnenie do prevádzky | `ReleaseToService` | sedí | Wave 7 |

---

## DO-06 — Incidenty a bezpečnostné udalosti

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-068 | Bezpečnostná udalosť | `SafetyEvent` | čiastočne | |
| ENT-069 | Účastník udalosti | `SafetyEventParticipant` | sedí | Wave 9 |
| ENT-070 | Technický prostriedok udalosti | `SafetyEventAsset` | sedí | Wave 9 |
| ENT-071 | Bezprostredné opatrenie | `SafetyImmediateAction` | sedí | Wave 9 |
| ENT-072 | Dôkaz | `SafetyEvidence` | čiastočne | Nie väzba na DO-07 Document |
| ENT-073 | Preverovanie/vyšetrovanie | `SafetyInvestigation` + polia | sedí | Wave 9 |
| ENT-074 | Príčina udalosti | `SafetyCause` | sedí | Wave 9; text `Causes` ostáva |
| ENT-075 | Oznamovacia povinnosť | `SafetyReportingObligation` | sedí | Wave 9; blokuje close |
| ENT-076 | Nápravné opatrenie | `CorrectiveAction` | sedí | |
| ENT-077 | História klasifikácie | `SafetyClassificationHistory` | sedí | Wave 9 |
| ENT-078 | História stavu | `SafetyStatusHistory` | sedí | Wave 9 |
| ENT-079 | Bezpečnostné upozornenie flotily | `FleetSafetyAlert` | sedí | Wave 9 |

---

## DO-07 — Dokumenty a elektronický archív

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-080 | Dokument | `ManagedDocument` family | čiastočne | FamilyId ≈ dokument |
| ENT-081 | Verzia dokumentu | riadky `ManagedDocument` | čiastočne | |
| ENT-082 | Elektronický súbor | meta na dokumente | čiastočne | Bez blob storage |
| ENT-083 | Väzba na objekt | LinkedEntity* | čiastočne | |
| ENT-084 | Elektronický spis | — | **chýba** | |
| ENT-085 | Schválenie dokumentu | — | **chýba** | |
| ENT-086 | Elektronický podpis | — | **chýba** | |
| ENT-087 | Retenčné pravidlo | — | **chýba** | |
| ENT-088 | Archivačná udalosť | — | **chýba** | |
| ENT-089 | Šablóna dokumentu | `FormTemplate` (forms) | čiastočne | Iný účel |
| ENT-090 | História stavu dokumentu | Status transitions | čiastočne | |

*Naviac:* `FormSubmission`, `FormAnswer`, `RiskAssessment` — podporujú BP checklist/risk, mimo čistého ENT zoznamu dokumentov.

---

## DO-08 — Notifikácie a pracovné úlohy

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-091 | Notifikácia | `AppNotification` | čiastočne | |
| ENT-092 | Príjemca notifikácie | AccountId na notifikácii | čiastočne | |
| ENT-093 | Pokus o doručenie | — | **chýba** | |
| ENT-094 | Šablóna správy | — | **chýba** | |
| ENT-095 | Pracovná úloha | `WorkTask` | čiastočne | |
| ENT-096 | Pridelenie úlohy | Assignee polia | čiastočne | |
| ENT-097 | História úlohy | — | **chýba** | |
| ENT-098 | Pravidlo eskalácie | — | **chýba** | |

---

## DO-09 — Dashboard, reporty a kontrolná činnosť

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-099 | Nastavenie dashboardu | — | **chýba** | |
| ENT-100 | Konfigurácia widgetu | — | **chýba** | |
| ENT-101 | Definícia reportu | hardcoded typy | čiastočne | |
| ENT-102 | Parameter reportu | request body | čiastočne | |
| ENT-103 | Vytvorenie reportu | `OfficialReport` | čiastočne | |
| ENT-104 | Kontrola alebo audit | — | **chýba** | MOD-15 |
| ENT-105 | Člen kontrolnej skupiny | — | **chýba** | |
| ENT-106 | Kontrolný zoznam | — | **chýba** | |
| ENT-107 | Výsledok kontrolného bodu | — | **chýba** | |
| ENT-108 | Kontrolné zistenie | — | **chýba** | |
| ENT-109 | Kontrolné nápravné opatrenie | — | **chýba** | |
| ENT-110 | Plnenie kontrolného opatrenia | — | **chýba** | |
| ENT-111 | Kontrolná/auditná správa | — | **chýba** | |

---

## DO-10 — Číselníky, konfigurácia a auditná stopa

| ENT | Názov | Trieda | Stav | Poznámka |
|-----|-------|---------|------|----------|
| ENT-112 | Číselník | `LookupCode` | sedí | |
| ENT-113 | Položka číselníka | `LookupItem` | sedí | |
| ENT-114 | História položky | — | **chýba** | |
| ENT-115 | Konfiguračný parameter | `ConfigRule` | čiastočne | |
| ENT-116 | Verzia konfigurácie | Version na ConfigRule | čiastočne | |
| ENT-117 | Auditná udalosť | `AuditEvent` | sedí | Append-only enforced |
| ENT-118 | História zmeny objektu | Before/After JSON v audite | čiastočne | Nie samostatná tabuľka |
| ENT-119 | Importná dávka | TelemetryImport / — | čiastočne | Len telemetria |
| ENT-120 | Exportná udalosť | audit pri CSV export | čiastočne | |
| ENT-121 | Integračná udalosť | — | **chýba** | `IntegrationConnector` bez event logu |

*Naviac:* `IntegrationConnector` (config stub).

---

## Súhrn počtov (orientačne)

| Stav | Počet ENT (z ~124) |
|------|-------------------|
| sedí | ~18 |
| čiastočne | ~55 |
| chýba | ~51 |

---

## Migračný plán (návrh — implementácia po potvrdení)

### Wave 0 — spoločné pravidlá (už hotové / overiť)
- `AuditableEntity`: Created/Updated, RowVersion, IsActive
- Soft-delete blok + AuditEvent immutability v `IsSuasDbContext`
- Additive migrácie; žiadne DROP bez explicitného súhlasu

### Wave 1 — DO-01 prístup (priorita)
Nové tabuľky:
1. `Permissions` (Code, NameSk, ModuleCode)
2. `RolePermissions` (RoleId, PermissionId)
3. `UserSessions` (AccountId, IssuedAt, ExpiresAt, IpAddress, UserAgent, RevokedAt)
4. `DelegatedPermissions` (FromAccountId, ToAccountId, PermissionCode, OrgUnitId, ValidFrom, ValidTo, Reason, ApprovedBy)

Seed: naplniť Permissions z existujúcich konštánt; RolePermissions z aktuálnej mapy.  
`AuthorizationPolicyService`: najprv hybrid (DB + fallback mapa), potom DB-only.

### Wave 2 — DO-09 MOD-15 kontrolná činnosť
Nové tabuľky:
1. `ControlAudits` (Title, Type, Status, OrgUnitId, PlannedFrom/To, ScopeJson, …)
2. `ControlTeamMembers` (ControlAuditId, AccountId, RoleInTeam)
3. `ControlChecklists` (+ items alebo JSON/verzia)
4. `ControlCheckpointResults`
5. `ControlFindings`
6. `ControlCorrectiveActions`
7. `ControlActionProgress` (plnenie)
8. `ControlReports` (frozen text/JSON výstup)

API: `/api/v1/control-audits` CRUD + findings/actions.  
Permissions: `controls.read`, `controls.write`, `controls.close`.

### Wave 3 — DO-07 dokumenty + storage (implementované 2026-08-06)

Nové tabuľky:
1. `StoredFiles` (ENT-082) — blob meta + StorageKey + SHA256
2. `RetentionRules` (ENT-087)
3. `DocumentDossiers` + `DocumentDossierItems` (ENT-084)
4. `ManagedDocuments.StoredFileId` / `RetentionRuleId` — väzba verzie na súbor

Úložisko: `IObjectStorage` → predvolene **Local** (`App_Data/storage`). MinIO endpoint v konfigurácii pripravený na neskorší provider.

API:
- `POST /api/v1/documents` s voliteľným `contentBase64` (uloží blob + overí hash)
- `GET /api/v1/documents/{id}/content`
- `GET /api/v1/retention-rules`
- `GET/POST /api/v1/document-dossiers`

### Wave 4 — DO-08 notifikácie (implementované 2026-08-06)

Nové tabuľky:
1. `MessageTemplates` (ENT-094)
2. `NotificationRecipients` (ENT-092)
3. `DeliveryAttempts` (ENT-093)
4. `EscalationRules` (ENT-098)
5. `WorkTaskHistories` (ENT-097)

Publish cez `INotificationPublisher` vytvára AppNotification + recipient + delivery attempt (InApp = Sent, Email/SMS = Skipped).

API:
- `GET/POST /api/v1/notifications`, `GET …/{id}`, `POST …/{id}/mark-read`, `POST …/run-escalations`
- `GET /api/v1/message-templates`
- `GET /api/v1/escalation-rules`
- `GET /api/v1/work-tasks/{id}/history`, `POST …/{id}/status`

Permissions: `notifications.read`, `notifications.write`. Acc24.

### Wave 5 — DO-02 kompatibility + batériové záznamy (implementované 2026-08-06)

Nové tabuľky:
1. `ComponentCompatibilities` (ENT-017)
2. `BatteryCompatibilities` (ENT-020)
3. `BatteryTechnicalRecords` (ENT-021)
4. `BatteryOperationRecords` (ENT-022)
5. `BatteryStatusHistories` (ENT-023)

Pravidlá: ak existujú aktívne pravidlá, montáž komponentu / použitie batérie na lete vyžaduje Compatible/Conditional match (UAS id alebo typ); Incompatible blokuje. Bez pravidiel = spätne kompatibilné (Acc*).

API:
- `GET/POST /api/v1/components/{id}/compatibility`
- `GET/POST /api/v1/batteries/{id}/compatibility`
- `GET/POST /api/v1/batteries/{id}/technical-records`
- `GET/POST /api/v1/batteries/{id}/operation-records`
- `GET /api/v1/batteries/{id}/status-history`, `POST …/transition`

Acc25.

### Wave 6 — DO-04 schvaľovacia trasa + opakovanie (implementované 2026-08-06)

Nové tabuľky:
1. `OperationApprovalSteps` (ENT-043)
2. `OperationDecisions` (ENT-044)
3. `OperationApprovalConditions` (ENT-045)
4. `OperationRecurrenceRules` (ENT-038)
5. `Operations.RecurrenceSourceOperationId`

Správanie: pri submit bez krokov vznikne predvolený 1-krok (Acc4); viackrokové approve ostáva v `Submitted` do posledného povinného kroku; podmienky blokujú let (`from-operation` / `ready`).

API:
- `POST /api/v1/operations/{id}/approval-steps`
- `POST /api/v1/operations/{id}/recurrence`, `…/recurrence/generate`
- `POST /api/v1/approval-conditions/{id}/verify`
- `GET` detail obsahuje steps/conditions/recurrence

Acc26.

### Wave 7 — DO-05 servis / obmedzenie / uvoľnenie (implementované 2026-08-06)

Nové tabuľky:
1. `ServiceRequests` (ENT-057)
2. `TechnicalRestrictions` (ENT-065)
3. `ReleasesToService` (ENT-067)
4. `TechnicalStatusHistories` (ENT-066)
5. `WorkOrders.ServiceRequestId`

Pravidlo: dokončenie WO ≠ uvoľnenie; `ReleaseToService` je samostatné rozhodnutie. Aktívne blokujúce obmedzenie vstupuje do `GET …/block-status`.

API:
- `GET/POST /api/v1/service-requests`
- `GET/POST /api/v1/technical-restrictions`, `POST …/{id}/end`
- `GET/POST /api/v1/releases-to-service`
- `GET /api/v1/technical-status-history?targetType=&targetId=`

Acc27.

### Wave 8 — DO-03 piloti / funkcie / výcvik (implementované 2026-08-06)

Nové tabuľky:
1. `PersonFunctionAssignments` (ENT-027)
2. `PilotRestrictions` (ENT-028)
3. `QualificationDocumentVerifications` (ENT-030)
4. `CertificateScopes` (ENT-032)
5. `TrainingCatalogItems` (ENT-033)
6. `TrainingActivities` (ENT-034)
7. `TrainingParticipations` (ENT-035)
8. `TrainingActivityResults` (ENT-036)
9. `PilotTrainings.TrainingParticipationId` (sync výsledku na kartu)

Pravidlo: aktívne `PilotRestriction` s `BlocksOperation=true` blokuje eligibility a priradenie do operácie.

API:
- `POST /api/v1/pilots/{id}/functions`
- `POST /api/v1/pilots/{id}/restrictions`, `POST …/restrictions/{id}/end`
- `POST /api/v1/pilots/{id}/qualifications/{qid}/verifications`
- `POST /api/v1/pilots/{id}/certificates/{cid}/scopes`
- `GET/POST /api/v1/training-catalog`
- `POST /api/v1/training-activities`, `GET …/{id}`
- `POST …/participations`, `POST …/participations/{id}/result` (`SyncToPilotCard`)

Acc28.

### Wave 9 — DO-06 safety rozšírenie (implementované 2026-08-06)

Nové tabuľky:
1. `SafetyEventParticipants` (ENT-069)
2. `SafetyEventAssets` (ENT-070)
3. `SafetyImmediateActions` (ENT-071)
4. `SafetyInvestigations` (ENT-073)
5. `SafetyCauses` (ENT-074)
6. `SafetyReportingObligations` (ENT-075)
7. `SafetyClassificationHistories` (ENT-077)
8. `SafetyStatusHistories` (ENT-078)
9. `FleetSafetyAlerts` (ENT-079)

Pravidlá: nesplnená oznamovacia povinnosť (Pending/Preparing) blokuje close bez výnimky; publish fleet alert vytvára notifikácie `FLEET_SAFETY_ALERT`.

API:
- `POST /api/v1/safety-events/{id}/participants`
- `POST …/assets`, `…/immediate-actions`
- `POST …/investigations`, `…/investigations/{id}/causes`
- `POST …/reporting-obligations`, `…/reporting-obligations/{id}/fulfill`
- `POST …/fleet-alerts`
- `POST …/severity`
- Detail obsahuje participants/assets/causes/obligations/alerts/histories

Acc29.

### Wave 10 — Fáza F integrácie (implementované 2026-08-06)

1. **Storage:** `Storage:Provider=Local|MinIO` — `MinioObjectStorage` (S3 SigV4); predvolene Local (Acc23 zelené).
2. **E-mail:** `IEmailSender` — `Null` (DEV sink → Sent) / `Smtp`; Acc24 ostáva InApp.
3. **LPS / ENT-124:** `OperationMapCheck` + `OfflineAirspaceMapAdapter` (fail-closed); `POST …/operations/{id}/map-check`; `Airspace:RequireMapCheckOnSubmit` default false.
4. **OIDC:** `Auth:Oidc:RedirectUri` v SSO start URL; bez Authority stále `stub`.
5. Seed konektorov: `LPS_SR`, `MINIO_STORAGE`, `EMAIL_SMTP`.
6. `GET /api/v1/system/info` — Storage/Email/Airspace provider + `OidcConfigured`.

Acc30.

### Wave 11 — Delegation API ENT-010 (implementované 2026-08-06)

1. **Runtime:** `AuthorizationPolicyService.HasPermission` zahŕňa aktívne `DelegatedPermissions` pre `ToAccountId` (platné obdobie, `IsActive`) cez `IDelegatedPermissionChecker`.
2. **Service:** `DelegationService` — list / create / revoke; delegovať len oprávnenie z vlastnej role mapy; `FromAccount` = aktuálny používateľ.
3. **API:** `GET/POST /api/v1/delegations`, `POST /api/v1/delegations/{id}/revoke`.
4. **Katalóg:** `PermissionCatalog` (spoločná role→permission mapa).

Acc31.

### Wave 12 — Fáza E UI (implementované 2026-08-06)

1. **UI helpery:** `confirmAction`, `statusBadge`, `showHistory` v `wwwroot/index.html`.
2. **Delegácie:** stránka `delegations` (list/create/revoke + história) nad Wave 11 API.
3. **Dashboard:** `GET /api/v1/dashboard?orgUnitId=` filtruje fleet/ops/upcoming podľa path útvaru; osobné počty ostávajú globálne.

Acc32.

### Wave 13 — Detail kontrol UI (implementované 2026-08-06)

1. **UI `controls`:** zoznam s Otvor/Spustiť; create ostáva `Planned` (bez auto-start).
2. **Detail:** checklist výsledky, findings + corrective actions, vydanie reportu, close; `showHistory('ControlAudit')`.
3. API bez zmien — existujúci `ControlAuditsController` (Acc22 + Acc33).

Acc33.

### Wave 14 — Globálne vyhľadávanie UI-006 (implementované 2026-08-06)

1. **API:** `GET /api/v1/search?q=&limit=` — Uas, Pilot, Operation, Account, SafetyEvent, ControlAudit; bez oprávnenia sa modul nehľadá; org filter cez `CanAccessOrgUnit`.
2. **UI:** topbar Hľadať + stránka výsledkov; navigácia do modulu / detail kontroly.
3. Acc34.

### Wave 15 — Dashboard zložky UI-004 (implementované 2026-08-06)

1. **Seed:** `ZLOZKA-PZ` (L1) pod MVSR; `UTVAR-A/B` (L2) pod zložkou.
2. **API:** `GET /api/v1/dashboard?scope=central|branch|unit&orgUnitId=` + polia Scope/ScopeOrgUnit* v DTO.
3. **UI:** prepínač Centrálny / Zložka / Útvar na Prehľade.

Acc35.

### Wave 16 — Auth / security / background (implementované 2026-08-06)

1. **SSO:** `GET /api/v1/auth/sso/callback` — DEV `dev-sso:{user}`; pri nastavenom Authority → 501.
2. **Reauth:** `POST /api/v1/auth/reauth` — krátkodobý JWT s claim `reauth`.
3. **Security headers** middleware; **IsSuasMaintenanceWorker** (Background:Enabled).
4. Acc36.

### Pravidlá migrácie
1. Iba additive (CREATE TABLE / ADD COLUMN / nullable FK).
2. Backfill seed permissions bez zmeny existujúcich RoleAssignment.
3. Existujúce Acc* testy musia prejsť.
4. Alias dokumentácia ENT↔tabuľka ostáva v tomto súbore.

---

## Odporúčanie na potvrdenie

**Wave 0–16 sú implementované.** Ďalšie: live IdP token exchange, LPS, Hangfire/PDF/A.
