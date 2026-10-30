# Režim údržby a núdzový vypínač

Kedykoľvek vypne celý IS ŠUAS. **Počas vypnutia nefunguje žiadny účet – ani gestor.** Používatelia vidia obrazovku „Prebieha údržba systému“, API vracia `503` na všetko vrátane prihlásenia.

## Ovládanie

**1. Núdzový vypínač (bežný spôsob, odkiaľkoľvek, bez prihlásenia)**

- Adresa: `http(s)://<server>/<MaintenanceMode__KillSwitchPath>` (hodnota je v `infra/.env`).
- Zadáš tajný kľúč (`MaintenanceMode__KillSwitchKey`) a klikneš **Vypnúť systém** / **Zapnúť systém**. Voliteľne správa pre používateľov a predpokladaný koniec.
- Adresa sa nikde v kóde ani vo verejných súboroch nevyskytuje; neznáma adresa vráti obyčajnú aplikáciu, takže sa nedá uhádnuť, či vypínač existuje.
- Max. 5 pokusov za minútu z jednej IP, kľúč sa porovnáva v konštantnom čase, každý pokus (aj neúspešný) je v audite (`MaintenanceMode.KillSwitch.*`, `MaintenanceMode.KillSwitchDenied`).
- Zmena adresy alebo kľúča: prepíš riadok v `.env` a spusti `docker compose up -d`.
- Ak `KillSwitchPath` alebo `KillSwitchKey` chýba alebo je neplatný, vypínač neexistuje (v logu je varovanie).

**2. Na serveri (keď web nefunguje)**

`MaintenanceMode__ForceEnabled=true` v `infra/.env` → `docker compose up -d`. Vypnúť sa to dá len vrátením na `false`.

## Technicky

| Časť | Súbor |
|---|---|
| Rozhranie, DTO | `IsSuas.Application/SystemMaintenance/MaintenanceModeContracts.cs` |
| Služba, cache, konfigurácia | `IsSuas.Infrastructure/SystemMaintenance/` |
| Middleware (503), tajná stránka a endpoint | `IsSuas.Api/SystemMaintenance/` |
| Verejný stav | `GET /api/v1/system/maintenance` |
| Obrazovka údržby v UI | `wwwroot/js/core/maintenance.js`, `#maintenance-view` v `index.html` |

- Stav je v existujúcej tabuľke `ConfigRules` (kľúč `system.maintenance_mode`), každé prepnutie = nová verzia. Netreba migráciu, funguje na PostgreSQL aj SQLite, zdieľajú ho všetky inštancie (oneskorenie max. `CacheSeconds`, predvolene 5 s).
- Cez Administráciu → konfiguračné pravidlá sa tento kľúč meniť nedá.
- Úlohy na pozadí sa počas údržby pozastavia (`PauseBackgroundJobs`).

## Konfigurácia (`.env` / premenné prostredia)

| Premenná | Význam |
|---|---|
| `MaintenanceMode__KillSwitchPath` | tajná adresa, 16–128 znakov `A-Z a-z 0-9 - _` |
| `MaintenanceMode__KillSwitchKey` | tajný kľúč, min. 32 znakov |
| `MaintenanceMode__ForceEnabled` / `__ForceMessage` | núdzové vypnutie bez webu |
| `MaintenanceMode__CacheSeconds`, `__RetryAfterSeconds`, `__PauseBackgroundJobs` | ladenie |
| `Cors__AllowedOrigins__0` | len ak UI beží na inej adrese ako API |
| `ReverseProxy__Enabled`, `ReverseProxy__KnownProxies__0` | za nginx/Traefik/load balancerom (správna IP pre limit pokusov a https) |

## Migrácia na iný hosting / doménu

Kód sa nemení, upravuje sa len konfigurácia:

1. Presuň PostgreSQL (`pg_dump`/`pg_restore`) – stav údržby a história idú s ňou. `ConnectionStrings__Default` nastav cez premennú prostredia.
2. Tajomstvá (`Jwt__SigningKey`, `MaintenanceMode__KillSwitchPath`, `MaintenanceMode__KillSwitchKey`, heslá) nastav v `.env` / správcovi tajomstiev hostingu – nie v gite. Pri migrácii je vhodné vygenerovať nové.
3. DNS na nový server, HTTPS certifikát na proxy; za proxy `ReverseProxy__Enabled=true` (+ IP proxy, ak nie je na tom istom stroji).
4. SSO (ak sa používa): `Auth__Oidc__RedirectUri=https://<nová-doména>/auth/callback`.
5. Viac inštancií: rovnaká databáza a rovnaký `Jwt__SigningKey`.

**Overenie:** `GET https://<doména>/api/v1/system/maintenance` vráti `"isActive": false` → na tajnej adrese vypni systém → prihlásenie (aj gestor) skončí obrazovkou údržby, API vracia `503` + `X-Maintenance-Mode: on` → zapni systém → do 30 s sa aplikácia načíta → v Administrácii → Audit sú záznamy `MaintenanceMode.KillSwitch.*`.

## Obmedzenia

- Aplikácia musí bežať v koreni domény (platí pre celé UI).
- Statické súbory (UI) sa načítajú aj počas údržby – dáta chráni API.
- Vypínač na webe funguje, len kým beží aplikácia; inak použi `ForceEnabled` alebo zastav kontajner.
- Ak je pred aplikáciou CDN/proxy cache, nesmie cachovať `/api/*` ani tajnú adresu (aplikácia posiela `Cache-Control: no-store`).
