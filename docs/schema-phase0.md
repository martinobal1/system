# IS ŠUAS — Dátová schéma (Fáza 0 + 6.1 + 6.2)

## Spoločné polia (`AuditableEntity`)

| Pole | Typ | Poznámka |
|---|---|---|
| Id | UUID | Nemenný technický kľúč |
| CreatedAt / CreatedBy | timestamptz / UUID? | UTC |
| UpdatedAt / UpdatedBy | timestamptz / UUID? | UTC |
| RowVersion | uint | Optimistic concurrency |
| IsActive | bool | Soft lifecycle (bez fyzického DELETE z API) |

## 6.1 Identity

```
OrgUnit (ParentId, Path, Level, Code unique)
  └── OrgUnitMoveHistory
Person → Account (UserName unique, Status, PasswordHash)
Role (SystemRole code unique, GrantsBusinessDataAccess)
RoleAssignment (Account, Role, OrgUnit, ValidFrom/ValidTo, Reason)
LookupCode → LookupItem
ConfigRule (Key, Version, EffectiveFrom/To, ValueJson)
AuditEvent (append-only)
```

### Stavy účtu
`Prepared | Active | TemporarilyBlocked | Deactivated`

### Roly
CentralGestor, UnitAdministrator, Approver, RemotePilot, TechnicalWorker, SafetyOfficer, ControllerAuditor, ReadOnlyUser, SystemAdministrator (bez vecného prístupu).

## 6.2 Flotila

```
Uas (SerialNumber unique, RegistrationNumber unique nullable, Status)
  ├── UasAssignment (ValidFrom/ValidTo — max 1 aktívne)
  ├── UasStatusHistory
  └── Installation ← Component
Battery (InternalNumber unique, SerialNumber unique)
  └── BatteryAssignment
```

### UAS lifecycle
`Draft → Registered → InService | InStorage | InMaintenance | Suspended | Damaged | Lost | Decommissioned`

Evidenčné číslo: `SUAS-{YYYY}-{SEQ:5}` pri schválení.

## Pravidlá

- 1 UAS = max 1 aktívne pridelenie útvaru (prekryv = 409)
- 1 komponent = max 1 aktívna montáž
- OrgUnit path bez cyklov pri move
- Audit append-only
