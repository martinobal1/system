# PROMPT PRE CURSOR AI — Informačný systém štátnych bezpilotných leteckých systémov (IS ŠUAS)

> Skopíruj celý tento dokument do Cursor AI (napr. do `.cursor/rules` alebo priamo ako prvý prompt v Agent/Composer režime). Je navrhnutý tak, aby AI agent pracoval **postupne po fázach**, nie aby sa pokúsil vygenerovať celý systém naraz.

---

## 0. Rola a spôsob práce

Si senior full-stack vývojár a softvérový architekt. Tvojou úlohou je postupne navrhnúť a implementovať produkčne pripravený webový informačný systém **IS ŠUAS** podľa špecifikácie nižšie.

Pravidlá práce:
- Pracuj **inkrementálne po moduloch** (viď fázovanie v sekcii 9). Nikdy sa nesnaž vygenerovať všetko v jednom kroku.
- Pred písaním kódu pre modul najprv navrhni: dátové entity → migrácie → API kontrakty → autorizačné pravidlá → UI obrazovky → testy.
- Každý modul musí byť funkčný a otestovaný predtým, než prejdeš na ďalší.
- Nič fyzicky nemaž z databázy — všade používaj stavy, deaktiváciu a históriu (viď sekcia 4 – spoločné pravidlá).
- Každá zmena významného údaja musí byť auditovateľná (kto, kedy, čo, pôvodná a nová hodnota).
- Autorizácia sa vždy overuje na serveri, nikdy iba v UI.
- Ak si nie si istý konkrétnou hodnotou (formát evidenčného čísla, konkrétne limity, retenčné lehoty), implementuj ju ako **konfigurovateľnú položku číselníka**, nie natvrdo v kóde.
- Píš kód v angličtine (názvy premenných, tabuliek, API), ale používateľské rozhranie generuj v **slovenčine**.

---

## 1. Kontext a účel systému

IS ŠUAS je rezortný informačný systém pre Ministerstvo vnútra SR na jednotnú elektronickú správu štátnych bezpilotných leteckých systémov (dronov), personálu (pilotov), letovej činnosti, technickej spôsobilosti, dokumentácie a bezpečnostných udalostí. Nahrádza roztrúsené tabuľky, papierové formuláre a samostatné denníky.

Funkčným benchmarkom (referenciou rozsahu funkcií, **nie** kódu ani dizajnu) sú riešenia typu DroneLogbook / UAV Logbook — správa flotily, pilotov, misií, telemetrie, batérií, údržby, dokumentov, incidentov a reportov. Ide o **vlastnú, samostatnú implementáciu** s vlastným dátovým modelom a zdrojovým kódom.

### 1.1 Ciele
- jednorazové zadanie údaja a jeho opätovné použitie vo všetkých súvisiacich evidenciách,
- úplná evidencia životného cyklu UAS, komponentov, batérií a pilotov,
- plánovanie → schvaľovanie → vykonanie → vyhodnotenie letových operácií,
- import telemetrie, výpočet prevádzkových ukazovateľov a kontrola limitov,
- riadenie údržby, kontrol, porúch, incidentov a nápravných opatrení,
- prístup riadený rolou a organizačným útvarom,
- nemenná auditná stopa, reporty a reprodukovateľné výkazy.

### 1.2 Rozsah prvej verzie (V ROZSAHU vs MIMO ROZSAHU)

**V rozsahu:** webová aplikácia pre interných používateľov MV SR; organizácie/účty/roly a organizačný prístup; register UAS, komponentov, vybavenia a batérií; piloti, kvalifikácie, školenia, spôsobilosť; plány operácií, schvaľovanie, lety a telemetria; údržba, poruchy, incidenty, dokumenty a reporty; API, import a export.

**Mimo rozsahu (neimplementuj):** riadenie UAS/autopilota v reálnom čase; náhrada centrálneho systému identít MV SR (iba integrácia); účtovníctvo a komplexná majetková evidencia; plnohodnotný vzdelávací LMS; riadenie letovej prevádzky alebo vydávanie externých povolení; náhrada centrálneho DMS/registratúry; ovládače pre nezdokumentované proprietárne formáty.

### 1.3 Záväznosť požiadaviek
- **MUST** — povinné pre prvú produkčnú verziu (implementuj vždy).
- **SHOULD** — žiaduce, môže sa odsunúť len na základe explicitného rozhodnutia (implementuj po MUST module, ak je čas/priestor).
- **FUTURE** — architektúra tomu nesmie brániť, ale teraz sa neimplementuje (nechaj rozšíriteľné miesto — napr. voľné pole, event hook, modulárne rozhranie).

---

## 2. Odporúčaný technologický stack

Použi moderný, dobre podporovaný stack vhodný pre štátnu inštitúciu (dlhodobá udržateľnosť, žiadne exotické závislosti):

- **Backend:** Node.js (TypeScript) s NestJS *alebo* .NET (C#) — vyber jeden a drž sa ho konzistentne. REST API, verziované (`/api/v1/...`).
- **Databáza:** PostgreSQL (podpora geodát cez PostGIS pre lokality/trasy/polygóny).
- **ORM/migrácie:** Prisma alebo TypeORM (Node) / EF Core (.NET) — migrácie musia byť reprodukovateľné a s rollbackom.
- **Frontend:** React + TypeScript, komponentová knižnica (napr. MUI alebo Ant Design) s podporou slovenčiny a WCAG 2.1 AA.
- **Mapy:** Leaflet alebo MapLibre GL (bez viazanosti na platený SaaS), podpora WGS-84, GeoJSON/KML.
- **Auth:** OpenID Connect/SAML klient pripravený na integráciu s identitou MV SR (SSO + MFA), s lokálnym RBAC engine navrchu.
- **Súbory:** objektové úložisko (S3-kompatibilné) pre dokumenty/telemetriu, DB uchováva iba metadáta a hash.
- **Front pre PDF/CSV/XLSX export:** knižnice ako `pdf-lib`/`puppeteer` (PDF), `exceljs` (XLSX).
- **Testy:** unit + integračné testy pre business pravidlá (najmä stavové prechody a limity), aspoň smoke E2E pre kritické scenáre z ACC-* (sekcia 9).
- **CI/CD:** oddelené DEV/TEST/PROD prostredia, automatizované nasadenie, DB migrácie ako súčasť pipeline.

Ak Cursor navrhne iný stack, musí zachovať tieto vlastnosti: silné typovanie, migrácie s históriou, jasné oddelenie API vrstvy od UI, auditovateľnosť na úrovni ORM/DB triggerov.

---

## 3. Používatelia, roly a autorizačný model

### 3.1 Základné roly
1. **Centrálny gestor** — centrálne registre, schvaľovanie, metodika, celorezortné reporty.
2. **Správca útvaru** — údaje vlastného útvaru, lokálne pridelenia, kontrola úplnosti.
3. **Schvaľovateľ** — schvaľuje operácie a určené záznamy v pridelenom rozsahu.
4. **Pilot na diaľku** — vlastný profil, pridelené operácie, lety, checklisty, hlásenia.
5. **Technický pracovník** — údržba, kontroly, opravy, poruchy, technická spôsobilosť.
6. **Bezpečnostný pracovník** — incidenty, nehody, vyšetrovanie, nápravné opatrenia.
7. **Kontrolór/audítor** — čítanie v schválenom rozsahu, bez vecnej editácie.
8. **Používateľ na čítanie** — prehľad povolených údajov bez zmeny.
9. **Systémový administrátor** — technická konfigurácia, **bez** automatického prístupu k vecnému obsahu.

### 3.2 Princípy autorizácie (implementuj ako centrálny policy layer, nie ad-hoc kontroly v každom endpointe)
- **Najmenšie oprávnenie** — iba čo je nutné na úlohu.
- **Organizačný rozsah** — rola platí pre útvar, jeho podútvary alebo celý rezort; každý dotaz sa filtruje podľa `org_unit` hierarchie.
- **Oddelenie zodpovedností (SoD)** — vlastný návrh nemožno schváliť, ak proces vyžaduje nezávislé schválenie (napr. schvaľovateľ ≠ navrhovateľ).
- **Časová platnosť** — rola/poverenie môže mať `valid_from`/`valid_to`.
- **Citlivý obsah** — samostatné oprávnenie obmedzuje detail, polohu, osobné údaje, prílohy.
- **Systémový administrátor nesmie automaticky čítať vecný obsah.**
- Používateľ **nesmie obísť** oprávnenie zmenou URL, ID záznamu ani priamym API volaním — server vždy re-validuje rozsah, nie iba UI.

---

## 4. Spoločné systémové pravidlá (platia naprieč všetkými modulmi)

Implementuj tieto pravidlá ako zdieľanú vrstvu/mixin, nie opakovane v každom module:

| Pravidlo | Popis |
|---|---|
| Nemenný ID | Každý objekt má technický, nemenný interný identifikátor (nie odvodený z používateľského označenia). |
| Bez fyzického mazania | Záznam s historickou väzbou sa nevymazáva — iba deaktivuje/vyradí. |
| Audit zmien | Zmena významného údaja uchová pôvodnú aj novú hodnotu, autora, čas, dôvod. |
| 1 UAS = 1 útvar | UAS môže byť v danom čase pridelený najviac jednému používateľskému útvaru. |
| Platnosť v čase operácie | Pilot, UAS, batéria a povinný dokument musia byť v čase operácie platné a použiteľné. |
| Operácia ≠ let | Operácia môže obsahovať viac letov; let sa vo výkazoch nesmie počítať ako ďalšia operácia. |
| Riadená zmena schváleného | Schválený/uzatvorený záznam sa mení iba riadenou opravou alebo novou verziou, nie prepísaním. |
| Časové pásmo | Dátumy/časy sa ukladajú jednoznačne vrátane časového pásma (UTC v DB), UI zobrazuje miestny čas. |
| Konfigurovateľné limity | Limity, lehoty, stavy, číselníky sú konfigurovateľné oprávnenou rolou bez zásahu do kódu. |
| Zákaz obchádzania | Nemožno obísť oprávnenie cez URL/API/ID. |
| Pôvod automatických údajov | Automaticky získaný údaj (napr. z telemetrie) musí mať zdroj, čas získania, stav spracovania. |
| Nemenné oficiálne reporty | Oficiálny report používa zmrazený dátový stav a verziu výpočtových pravidiel v čase vytvorenia. |

---

## 5. Dátový model (základné entity a vzťahy)

Navrhni schému okolo týchto hlavných entít (názvy sú návrh — Cursor môže doladiť konkrétne stĺpce, ale zachovaj vzťahy a princípy zo sekcie 4):

**Entity:** `OrgUnit` (hierarchia útvarov), `Person`/`Account`/`RoleAssignment` (identita a oprávnenia), `Pilot`/`Qualification` (odborný profil, doklady, osvedčenia, spôsobilosť), `UAS` (register lietadiel), `Component`/`Battery` (inventár a kompatibilita), `Assignment`/`Installation` (časovo ohraničené väzby na útvar/UAS), `Operation` (plán, zdroje, lokalita, schválenie), `Flight` (skutočný let), `TelemetryImport` (zdrojový súbor, spracovanie, GPS stopa), `Maintenance`/`Defect` (plán údržby, pracovný príkaz, poruchy), `SafetyEvent` (incident, vyšetrovanie, dôkazy, opatrenia), `Document`/`FormTemplate`/`FormInstance` (verziované súbory a formuláre), `Notification`/`Task`, `Report`/`Statement` (definícia a nemenná verzia výkazu), `AuditEvent` (append-only).

**Kľúčové vzťahy:**
- OrgUnit 1:N → používatelia, piloti, UAS, batérie, operácie, dokumenty
- UAS 1:N → lety, pridelenia, montáže, údržby, poruchy, dokumenty, udalosti
- Operation 1:N → lety, personál, zdroje, schválenia, checklisty, dokumenty
- Flight N:M → personál (podľa rolí), batérie, komponenty, udalosti
- Pilot 1:N → kvalifikácie, osvedčenia, výcviky, roly v operáciách, lety
- MaintenancePlan 1:N → pracovné príkazy a záznamy
- SafetyEvent 1:N → dôkazy, zistenia, nápravné opatrenia
- FormTemplate 1:N → nemenné verzie šablóny a vyplnené formuláre
- Každá hlavná entita 1:N → dokumenty, upozornenia, auditné udalosti

**Pravidlá databázového návrhu:**
- Primárne kľúče technické a nemenné (nie odvodené od evidenčného označenia).
- Cudzie kľúče vynucujú referenčnú integritu (alebo riadenú "soft" väzbu pri externom zdroji).
- Aktuálny stav a história stavov sú oddelené entity — nikdy sa nemiešajú do jedného stĺpca bez histórie.
- Časovo platné väzby majú `valid_from`/`valid_to`; aktívne intervaly rovnakého typu sa nesmú prekrývať.
- Súbory sa ukladajú v objektovom úložisku, DB drží iba metadáta + kontrolný súčet (hash).
- Citlivé polia sú označené/oddelené pre osobitný prístup a retenčné pravidlo.
- Fyzické mazanie vecných záznamov je dostupné iba riadenému retenčnému procesu (job), nikdy z UI.
- Agregované počítadlá musia byť reprodukovateľné zo zdrojových záznamov, alebo majú auditovaný prepočet.
- Každá tabuľka: `created_at`, `created_by`, `updated_at`, `updated_by`, `row_version` (optimistic locking).
- Migrácie musia byť opakovateľné, testovateľné, s rollbackom, oddelené pre DEV/TEST/PROD.

---

## 6. Funkčné moduly (implementuj v tomto poradí — viď aj fázovanie v sekcii 9)

Pre **každý** modul nižšie implementuj: dátové entity, API endpoints (CRUD + akcie), stavový model, autorizačnú maticu, validácie a aspoň základné akceptačné testy.

### 6.1 Organizácie, používatelia a oprávnenia
- Organizačná hierarchia útvarov (bez cyklov), história presunov.
- Účty, roly, pridelenie roly s organizačným rozsahom, platnosťou od/do a dôvodom.
- Stavy účtu: Pripravený, Aktívny, Dočasne zablokovaný, Deaktivovaný.
- Kombinácia viacerých rolí u jedného používateľa musí dať zjednotený, ale neprekračujúci rozsah oprávnení.
- Zablokovaný účet sa nesmie prihlásiť ani volať chránené API. Odobratie roly okamžite zruší prístup, ale zachová audit.

### 6.2 Flotila: UAS, komponenty, vybavenie a batérie
- Register UAS: koncept → schválenie → nemenné evidenčné označenie (podľa konfigurovateľného formátu) → životný cyklus (Koncept, Evidovaný, V prevádzke, Na sklade, V údržbe, Pozastavený, Poškodený, Stratený, Vyradený).
- Kontrola jedinečnosti výrobného čísla a evidenčného označenia.
- Komponenty/vybavenie ako samostatné položky; 1 komponent = max 1 aktívna montáž na UAS súčasne; montáž/demontáž/výmena vytvára historický záznam.
- Batérie: samostatný záznam, interné číslo, výrobné číslo, kompatibilné typy UAS; priradenie k letu aktualizuje prevádzkové počítadlá (cykly, kapacita, zdravie, teplotné extrémy).
- Limity veku/cyklov/kapacity/kontrol → upozornenie alebo blokovanie podľa konfigurácie.
- Nepoužiteľný objekt (poškodený/pozastavený/technicky nespôsobilý/vyradený) sa nesmie ponúknuť do novej operácie.
- Detail UAS agreguje: lety, operácie, batérie, komponenty, dokumenty, údržbu, poruchy, incidenty.

### 6.3 Piloti, osvedčenia, kvalifikácie a výcvik
- Profil pilota (1 aktívna identita na osobu), organizačné zaradenie, stav (Návrh, Aktívny, Spôsobilý, Platnosť končí, Nespôsobilý, Pozastavený, Neaktívny).
- Externé doklady + interné osvedčenia (verziované, nemenná vydaná verzia, riadené pozastavenie/obnova).
- Školenia/skúšky s výsledkom, inštruktorom, platnosťou.
- **Kondičná spôsobilosť** — automatický prepočet podľa konfigurovaného pravidla (napr. posledné lety/čas) po každom uzatvorenom lete.
- Upozornenia na končiacu platnosť a nesplnenú kondičnú požiadavku v konfigurovateľnom predstihu.
- Pilota bez platného oprávnenia/spôsobilosti nemožno priradiť do danej roly v operácii.

### 6.4 Plánovanie a schvaľovanie letových operácií
- Operácia: koncept → priebežné ukladanie → predloženie (uzamkne predloženú verziu) → schválenie/vrátenie/zamietnutie s odôvodnením.
- Lokalita: adresa, súradnice, bod, trasa alebo polygón (WGS-84).
- Priradenie UAS, komponentov, batérií, pilotov, personálu podľa rolí — s kontrolou stavu, platnosti, kompatibility a časovej dostupnosti (konflikt = blokujúca chyba).
- Checklisty, posúdenie rizika, povinné dokumenty podľa typu operácie.
- Kalendár (deň/týždeň/mesiac) s upozornením na konflikt zdrojov.
- Voliteľná integrácia počasia/vzdušného priestoru (informatívne, nikdy nenahrádza externé povolenie).
- **Neodkladná operácia** — zrýchlený postup s povinným dôvodom + automaticky vytvorená úloha na dodatočné doplnenie.
- Navrhovateľ nesmie schváliť vlastný návrh (SoD pravidlo).
- Zamietnuté/zrušené operácie sa zachovávajú pre audit a štatistiky.

### 6.5 Lety, denníky a telemetria
- Let vytvorený z operácie alebo samostatne (s dôvodom); 1 operácia môže mať N letov.
- Manuálny zápis **aj** import telemetrického súboru/API.
- **Import musí byť idempotentný** — opakovaný import rovnakého súboru nesmie vytvoriť duplicitný let (kontrolný súčet/korelačné ID).
- Uchovanie pôvodného telemetrického súboru, checksum, formát, zdroj, výsledok spracovania.
- Automaticky prevzaté hodnoty (čas, trvanie, vzdialenosť, výšky, rýchlosti, GPS stopa) sa opravujú iba riadenou opravou s dôvodom — originál zostáva zachovaný.
- Nerozpoznaný import je možné manuálne spárovať s UAS/operáciou/pilotom/batériou.
- Pred uzatvorením letu: povinné poletové údaje, checklist, vyhodnotenie odchýlok.
- Uzatvorenie letu aktualizuje počítadlá UAS/batérií/komponentov/pilotov/plánov údržby **presne raz**.
- GPS stopa na mape + export (KML/KMZ, GeoJSON, prípadne IGC).
- Individuálny denník pilota a denník UAS sa generujú bez duplicitného zadávania.

### 6.6 Údržba, technické kontroly, opravy a poruchy
- Plány údržby pre typ UAS / konkrétny UAS / komponent / batériu / vybavenie; spúšťač = dátum, počet letov, letové hodiny, cykly alebo kombinácia.
- Po uzatvorení letu sa automaticky prepočítajú termíny a zostávajúce limity.
- Upozornenie pred termínom, blokovanie po prekročení (konfigurovateľné prahy).
- Pracovné príkazy → záznam údržby (rozsah práce, výsledok, čas, technik, diely, dokumentácia).
- Poruchy: závažnosť, vplyv na použiteľnosť, dočasné opatrenie, zodpovedná osoba; **blokujúca porucha** znemožní výber objektu do operácie.
- Obnovenie spôsobilosti vyžaduje technické posúdenie, vykonané práce, skúšku a oprávnenú osobu — pôvodné hlásenie poruchy zostáva nemenné.
- Report opakovaných porúch rovnakého typu.

### 6.7 Incidenty, letecké nehody a bezpečnostné udalosti
- Nahlásenie z letu/operácie/UAS alebo samostatne; **prvotné hlásenie vyžaduje iba minimálne údaje** (rýchle zaznamenanie).
- Konfigurovateľná závažnosť a režim prístupu (citlivé prílohy/osobné údaje majú samostatné oprávnenie na zobrazenie/export).
- Dôkazy (foto, dokumenty, telemetria) s kontrolným súčtom; po priložení sa nesmú nahradiť bez novej verzie.
- Vyšetrovanie: zistenia, príčiny, súvisiace faktory, odporúčania → nápravné opatrenia (vlastník, termín, dôkaz splnenia, overil iná osoba než vlastník).
- Kritická udalosť → okamžité upozornenie určeným rolám, môže automaticky pozastaviť UAS/batériu/komponent/oprávnenie.
- Prvotný oznamovateľ nesmie potichu zmeniť pôvodné hlásenie po odoslaní.
- Uzatvorenie vyžaduje vyhodnotenie všetkých povinných opatrení alebo zdôvodnenú výnimku.

### 6.8 Dokumenty, formuláre, checklisty a posúdenia rizík
- Dokument pripojiteľný ku každému podporovanému objektu; typ, verzia, platnosť, vlastník, citlivosť, checksum.
- Nahradenie vytvára novú verziu, stará sa zachová ako nahradená; max 1 aktuálna účinná verzia v danom rozsahu.
- Upozornenie na exspiráciu + automatická zmena stavu; povinný neplatný/chýbajúci dokument môže blokovať operáciu.
- Šablóny elektronických formulárov/checklistov/posúdení rizika — **verziované**; vyplnený formulár zostáva viazaný na verziu použitú pri vyplnení.
- Položky formulára: text, číslo, dátum, výber, áno/nie, príloha, podpisové potvrdenie; podpora povinnosti, podmieneného zobrazenia, validácie.
- Checklist zobrazuje stav úplnosti a nesplnené blokujúce položky (blokuje dokončenie procesu).
- Posúdenie rizika — reprodukovateľný výpočet/klasifikácia podľa konfigurovanej metodiky.
- Kontrola typu/veľkosti súboru + bezpečnostná kontrola nahrávky (antivírus/scan).

### 6.9 Dashboard, kalendár, upozornenia a reporty
- Dashboard prispôsobený role a organizačnému rozsahu: stav flotily, dnešné operácie, otvorené úlohy, termíny, poruchy, upozornenia.
- Operačný kalendár (deň/týždeň/mesiac) s filtrami.
- Upozornenia: typ, priorita, príjemca, objekt, čas, termín, stav prečítania/vyriešenia — bez duplicity pri rovnakej udalosti; klik otvorí zdrojový objekt.
- Pracovné úlohy: pridelenie osobe/role, termín, eskalácia pri omeškaní.
- Preddefinované reporty (UAS, piloti, lety, batérie, údržba, poruchy, incidenty) s filtrami a časom dátového stavu.
- Export min. PDF a CSV; XLSX podľa typu reportu.
- **Oficiálny výkaz** = nemenná verzia so schválením a zamrznutými výpočtovými pravidlami; oprava vytvára novú verziu, pôvodná zostáva.
- Zabráň dvojitému započítaniu operácie a jej letov do rovnakého ukazovateľa.
- Naplánovanie automatického generovania reportu nesmie rozšíriť prístup príjemcu nad jeho oprávnenie.

### 6.10 Administrácia, číselníky, integrácie a audit
- Číselníky spravovateľné bez zásahu do kódu; použitá položka sa iba deaktivuje, nikdy nemaže.
- Verziované konfiguračné pravidlá (limity, lehoty, upozornenia, blokovania) s účinnosťou od daného času — **nesmú spätne meniť už uzatvorené záznamy**.
- Integračné konektory: typ, endpoint, formát, harmonogram, stav, posledný beh/chyba — **bez zobrazenia tajomstiev** v UI/exporte/logu.
- **Auditná stopa:** append-only, nemenná (ani systémový administrátor ju nesmie upraviť/zmazať), obsahuje subjekt, čas, typ úkonu, objekt, výsledok, pred/po hodnoty.
- Vyhľadávanie auditu podľa obdobia/používateľa/objektu/úkonu/výsledku; export auditu je samostatne auditovaný a obmedzený oprávnením.
- Technické logy bez únikov citlivých údajov (žiadne heslá, tokeny, telemetrické súbory, nadbytočné osobné údaje).
- Prehľad verzie aplikácie/DB/aktívnych konfigurácií.

---

## 7. Používateľské rozhranie

### 7.1 Všeobecný model obrazoviek (aplikuj konzistentne na všetky moduly)

| Typ obrazovky | Povinné prvky |
|---|---|
| Dashboard | widgety podľa roly, dnešné operácie, stav flotily, úlohy, termíny, kritické upozornenia |
| Zoznam | vyhľadávanie, filtre, radenie, stránkovanie (server-side), výber stĺpcov, uložený pohľad, export |
| Detail | hlavička, stav, hlavné údaje, karty väzieb, časová os, dokumenty, audit (podľa oprávnenia) |
| Formulár | jasne označené povinné polia, skupiny údajov, priebežná validácia, uloženie konceptu, zrušenie bez straty po potvrdení |
| Schválenie | nemenný náhľad predloženej verzie, rozhodnutie, dôvod, podmienky, história |
| Kalendár/mapa | časový rozsah, filtre, legenda, detail položky; mapa s bodom/trasou/polygónom/GPS stopou |

### 7.2 Minimálny katalóg obrazoviek (26 obrazoviek)
Prihlásenie/SSO presmerovanie · Dashboard · Organizačná štruktúra · Používatelia a roly · Zoznam a detail UAS · Komponenty a vybavenie · Batérie a denník batérie · Piloti a profil spôsobilosti · Osvedčenia/kvalifikácie/výcvik · Operačný kalendár · Nová operácia/plán · Schválenie operácie · Mapa lokality a oblasti · Zoznam a detail letov · Import telemetrie a párovanie · GPS stopa a technické údaje letu · Plány údržby · Pracovné príkazy a záznam údržby · Poruchy a technická spôsobilosť · Hlásenie a detail udalosti · Dokumenty a šablóny · Formulár/checklist/posúdenie rizika · Upozornenia a pracovné úlohy · Reporty a výkazy · Číselníky a konfigurácia · Audit a technický stav integrácií.

### 7.3 Použiteľnosť a prístupnosť
- Rozhranie v slovenčine; odborné skratky majú vysvetlenie (tooltip).
- Primárne úlohy použiteľné na bežnom monitore bez horizontálneho scrollu formulára.
- Responzívne minimálne pre desktop a tablet; kritické hlásenie letu/poruchy/incidentu aj cez mobilný prehliadač.
- Validácia označí konkrétne pole a zrozumiteľne vysvetlí opravu.
- Nezvratné/nebezpečné akcie vyžadujú potvrdenie so zobrazením následku.
- Ovládateľné klávesnicou, primeraná úroveň **WCAG 2.1 AA**.
- Stav sa nekomunikuje iba farbou — vždy text alebo ikona s popisom.
- Ochrana rozpracovaného formulára pred stratou pri vypršaní relácie/náhodnom odchode (autosave draftu / potvrdzujúci dialóg).

---

## 8. Integrácie, import/export a nefunkčné požiadavky

### 8.1 Integrácie
| Integrácia | Rozsah | Priorita |
|---|---|---|
| Identita MV SR | SSO, identifikátor osoby, profil, stav účtu | MUST |
| Telemetria | súborový import + konektory (DJI/Pixhawk/Autel/Parrot a i.) | MUST |
| E-mail/notifikácie | odoslanie bez citlivého obsahu, odkaz do systému | MUST |
| Mapové podklady | WGS-84 body/trasy/polygóny/GPS stopy | MUST |
| Počasie/vzdušný priestor | informatívne dáta so zdrojom/časom/platnosťou | SHOULD |
| Registratúra/DMS | odovzdanie dokumentu / externý identifikátor spisu | SHOULD |
| Majetková evidencia | import inventárneho čísla a základných údajov | SHOULD |
| API pre oprávnené systémy | verziované REST API, autentifikácia služby, audit, limity | MUST |

Spoločné pravidlá: každé rozhranie verziované a dokumentované, oddelené DEV/TEST/PROD; import má korelačné ID, stav, počty spracovaných/odmietnutých, protokol chýb; opakované spracovanie nesmie duplicovať; čiastočné zlyhanie nesmie nechať nekonzistentný stav; tajomstvá v schválenom secrets store s rotáciou; API vracia iba dáta v rozsahu identity a oprávnenia volajúceho.

**Podporované formáty:** CSV UTF-8, XLSX (tabuľkové); PDF/A alebo schválený PDF profil (oficiálne výstupy); KML/KMZ, GeoJSON, IGC (trasy); WGS-84 GeoJSON/KML (mapy); PDF/PDF-A/DOCX/XLSX/JPG/PNG (dokumenty); modulárne adaptéry pre telemetriu (pôvodný súbor sa nikdy nemení).

### 8.2 Nefunkčné a bezpečnostné požiadavky (implementuj od začiatku, nie dodatočne)

**Výkon a dostupnosť:** 95 % bežných obrazoviek do 2 s; komplexný report do 10 s alebo asynchrónne; server-side stránkovanie zoznamov; podpora min. 1000 súbežných relácií (alebo dohodnutá hodnota); veľké telemetrické importy asynchrónne s priebehom; cieľová dostupnosť 99,5 %/mesiac mimo servisných okien; výpadok doplnkovej integrácie nesmie zhodiť jadro.

**Bezpečnosť:** šifrovaná komunikácia (TLS) a šifrovanie citlivých dát v úložisku; MFA cez identitu MV SR; autorizácia vždy na serveri; ochrana proti OWASP Top 10/ASVS; kontrola nahrávaných súborov (typ, veľkosť, malware scan); riadenie relácie (idle/max timeout, secure cookies, centrálny logout); žiadne tajomstvá v kóde/configu/logoch; napojenie na SIEM ak dostupné; pred produkciou — dependency scan, statická analýza, penetračný test, odstránenie kritických/vysokých nálezov; produkčné dáta sa nepoužívajú vo vývoji/teste bez anonymizácie.

**Audit, zálohovanie, obnova:** append-only audit s integritou; retencia podľa pravidla + export dôkazov s checksum; pravidelné zálohy DB/súborov/konfigurácie s monitoringom; pravidelne testovaná obnova; **RPO ≤ 24 h, RTO ≤ 8 h** (pokiaľ analýza neurčí prísnejšie); korelačné ID v logoch, jednotný čas, riadená úroveň detailu; monitoring dostupnosti/chybovosti/výkonu/kapacity/integrácií/front.

**Ochrana údajov:** každé osobné/citlivé pole má účel, rozsah prístupu, retenčnú kategóriu; export osobných/citlivých údajov obmedzený a auditovaný; podpora opravy/obmedzenia/anonymizácie/výmazu v medziach zákonnej evidencie; automatizovaná, schválená a auditovaná retencia.

**Prevádzkovateľnosť:** oddelené DEV/TEST/PROD s automatizovaným nasadením; konfigurácia oddelená od kódu a verziovaná; reprodukovateľné a testované DB migrácie; odovzdanie zdrojového kódu, build postupu, schémy DB, API dokumentácie, prevádzkovej príručky; evidencia knižníc a licencií; podpora slovenských znakov, európskeho formátu dátumu, konfigurovateľného časového pásma.

---

## 9. Fázovanie implementácie (postupuj presne v tomto poradí)

### Fáza 0 — Základ
1. Scaffold projektu (backend + frontend + DB), CI/CD kostra, DEV prostredie.
2. Spoločná vrstva: audit log, soft-delete/stavový mixin, `row_version`, autorizačný policy engine, číselníkový modul.
3. Modul 6.1 (Organizácie, používatelia, oprávnenia) vrátane mock/lokálneho prihlásenia (SSO integrácia môže byť stub do doby, kým nie je dostupná).

### Fáza 1 — MVP jadro (MUST)
V poradí: 6.2 Flotila → 6.3 Piloti → 6.4 Operácie → 6.5 Lety a telemetria (manuálny zápis, import môže byť zjednodušený) → 6.6 Údržba/poruchy → 6.7 Incidenty → 6.8 Dokumenty/checklisty → 6.9 základné reporty a dashboard → 6.10 audit a číselníky (dokončenie).

Toto zodpovedá MVP rozsahu: organizácie/roly; register UAS/komponentov/batérií; piloti a doklady; operácie a schválenie; lety a manuálny/importovaný záznam; údržba/poruchy; incidenty; dokumenty/checklisty; základné reporty; audit.

### Fáza 2 — SHOULD rozšírenie
Online počasie/vzdušný priestor, pokročilé automatické telemetrické konektory, plánované/naplánované reporty, DMS/majetkové integrácie, rozšírené mapové analýzy.

### Fáza 3 — FUTURE (iba priprav architektúru, neimplementuj)
Natívna mobilná aplikácia, plnohodnotný offline režim, 3D prehrávanie letu, pokročilá prediktívna údržba. Nechaj pre tieto veci rozšíriteľné API/event hooky, ale nestav ich teraz.

---

## 10. Akceptačné scenáre — over si nimi každú fázu

Po dokončení príslušného modulu over funkčnosť oproti týmto scenárom (napíš ich ako automatizované testy, kde je to možné):

1. Gestor zapíše UAS → systém odhalí duplicitu → po schválení sa pridelí evidenčné označenie.
2. UAS sa pridelí útvaru; druhé súbežné pridelenie je odmietnuté; história zostáva.
3. Pilot s platným osvedčením sa priradí do operácie; nespôsobilý pilot je zablokovaný.
4. Operácia prejde: koncept → validácia → predloženie → schválenie → vytvorenie letu.
5. Neodkladná operácia sa založí zrýchlene a vytvorí povinnú úlohu na doplnenie.
6. Telemetria sa importuje **práve raz**, priradí sa k letu, zobrazí GPS stopu.
7. Uzatvorený let aktualizuje UAS/pilota/batériu/plán údržby **presne raz**.
8. Prekročený limit údržby zablokuje UAS; po overenej údržbe sa spôsobilosť obnoví.
9. Pilot nahlási poruchu; technik ju vyrieši; pôvodné hlásenie zostane nemenné.
10. Kritický incident vyvolá upozornenie, opatrenie, dôkazy a riadené uzatvorenie.
11. Nová verzia dokumentu nahradí starú bez vymazania; exspirácia vytvorí upozornenie.
12. Checklist s nesplnenou blokujúcou položkou neumožní dokončenie operácie.
13. Používateľ útvaru neotvorí záznam cudzieho útvaru — ani cez UI, ani priamym API volaním.
14. Schvaľovateľ nemôže schváliť vlastný návrh pri aktívnom pravidle oddelenia rolí.
15. Oficiálny report sa po zmene zdrojových dát nezmení; oprava vytvorí novú verziu.
16. Deaktivovaný účet stratí prístup, ale jeho autorstvo historických záznamov ostáva viditeľné.
17. Záloha sa obnoví do testovacieho prostredia a preukáže konzistenciu DB a súborov.
18. Bezpečnostné testy nepotvrdia otvorený kritický/vysoký nález pred nasadením.

---

## 11. Otvorené konfiguračné rozhodnutia (implementuj ako nastaviteľné, spýtaj sa ma na hodnoty ak treba defaulty)

- presný formát evidenčného označenia štátneho UAS,
- schvaľovacie roly a presné pravidlá neodkladnej operácie,
- limity kondičnej spôsobilosti, údržby, batérií a predstihy upozornení,
- podporované telemetrické formáty v MVP a max. veľkosť súboru,
- retenčné lehoty, citlivostné kategórie a registratúrne odovzdanie,
- cieľové mapové/meteo/vzdušné informačné zdroje,
- produkčné RPO/RTO a servisné okná (default: RPO 24 h, RTO 8 h, ak nepoviem inak),
- cieľová identitná služba a spôsob MFA (do integrácie použi lokálny mock login).

---

## 12. Ako začať (prvý krok pre Cursor)

1. Navrhni adresárovú štruktúru monorepa (`/backend`, `/frontend`, `/infra`, `/docs`).
2. Vygeneruj dátovú schému pre entity z Fázy 0 + Fázy 1 modul 6.1 a 6.2 (spolu s migráciami).
3. Vygeneruj autorizačný policy layer (rola + organizačný rozsah + SoD) ako znovupoužiteľný middleware/guard.
4. Ukáž mi návrh schémy a API kontraktov modulu 6.1 a 6.2 **predtým**, než začneš generovať UI — počkaj na moje potvrdenie.
5. Až po potvrdení pokračuj s implementáciou a testami, potom prejdi na ďalší modul podľa fázovania v sekcii 9.

---

*Tento prompt vychádza z interného dokumentu „Funkčná a technická špecifikácia — IS ŠUAS", verzia 1.0 (pracovný návrh MV SR). Neobsahuje chránený obsah tretích strán — funkčný rozsah DroneLogbook/UAV Logbook slúži iba ako referenčný benchmark, nie zdroj kódu či dizajnu.*
