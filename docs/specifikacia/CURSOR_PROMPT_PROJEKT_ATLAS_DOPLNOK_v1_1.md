# DOPLNOK K PROMPTU PRE CURSOR — Projekt ATLAS, aktualizácia na Master verziu v1.1

> Tento dokument **dopĺňa**, nenahrádza, predchádzajúci prompt `CURSOR_PROMPT_PROJEKT_ATLAS.md`. Vlož ho do Cursora ako **ďalšiu správu v tej istej konverzácii/agente**, ktorý už pracuje podľa predchádzajúceho promptu (po dokončení gap-auditu z Fázy A). Ak ešte gap-audit z predchádzajúceho promptu nebol spravený, urob ho najprv.

---

## 0. Čo sa zmenilo oproti pôvodnému zdroju

Predchádzajúci referenčný dokument (`PROJEKT_ATLAS_specifikacia_master.md`, z PDF) obsahoval iba 3 kapitoly:
1. Analýza informačného systému
2. Business procesy (BP-01 až BP-22)
3. Funkčná špecifikácia (MOD-01 až MOD-18)

**Nová Master verzia v1.1** (súbor `PROJEKT_ATLAS_specifikacia_master_v1_1.md`, vznikol z `.docx`) obsahuje tie isté 3 kapitoly **plus tri úplne nové etapy**, ktoré predtým chýbali:

4. **Etapa 4 — Informačný a databázový model**
5. **Etapa 5 — Návrh používateľského rozhrania**
6. **Etapa 6 — Technická špecifikácia**

Katalóg modulov (MOD-01–18) aj katalóg business procesov (BP-01–22) ostali nezmenené — over si detaily jednotlivých modulov/procesov, no štrukturálne ide o rovnaký rozsah ako predtým. Kľúčová nová hodnota je v Etapách 4–6.

**Dôležité:** od tejto chvíle je jediným záväzným referenčným súborom:
```
docs/specifikacia/PROJEKT_ATLAS_specifikacia_master_v1_1.md
```
Starý súbor `PROJEKT_ATLAS_specifikacia_master.md` môžeš v repozitári ponechať ako historickú verziu, ale kdekoľvek sa obsah líši, **platí v1.1**.

---

## 1. Fáza D — Zosúladenie s Etapou 4 (Informačný a databázový model)

Etapa 4 prvýkrát presne definuje dátový model, ktorý predtým existoval iba ako voľný zoznam entít v starom prompte. Over existujúcu DB schému oproti nej:

- **Dátové oblasti DO-01 až DO-10** — každá dátová entita systému musí patriť do práve jednej z nich a mať jasný autoritatívny modul (napr. DO-02 „UAS, komponenty a batérie" je autoritatívne vlastnený MOD-03/MOD-04/MOD-05). Over, či tvoj dátový model rešpektuje toto rozdelenie a že žiadny modul si nevytvára vlastnú kópiu dát, ktoré vlastní iný modul (má na ne iba referenciu/väzbu).
- **Katalóg hlavných dátových entít** (kapitola 4.3 v referenčnom dokumente, `### Entity oblasti DO-01` až `DO-10`) — presný zoznam entít po dátových oblastiach. Skontroluj, či ti v kóde nechýba niektorá entita.
- **Logický katalóg databázových tabuliek** (kapitola 4.5) a **Dátový slovník hlavných tabuliek** (kapitola 4.6) — obsahuje podrobný zoznam tabuliek a atribútov po dátových oblastiach. Toto je najpresnejší zdroj pre názvy a polia — **prednostne z neho vychádzaj pri doplňovaní chýbajúcich stĺpcov**, nie z pôvodného zjednodušeného zoznamu v starom prompte.
- **Spoločné pravidlá logického databázového modelu** (kapitola 4.4) — primárne/cudzie kľúče, spoločné systémové atribúty, logické dátové typy, pravidlá povinnosti/prázdnych hodnôt, stavy/platnosť/história, pravidlá deaktivácie/zneplatnenia/mazania, jedinečnosť a kontrola duplicít. Toto sú záväzné pravidlá naprieč celou schémou — priebežne ich porovnávaj s existujúcimi migráciami.
- **Kontrola úplnosti a konzistencie databázového modelu** (kapitola 4.7) — použi ako checklist pri vlastnom code review: pokrytie funkčného rozsahu, referenčná integrita, historizácia, auditovateľnosť.

**Postup:**
1. Vygeneruj porovnávaciu tabuľku: dátová oblasť → entity podľa referenčného dokumentu → entity/tabuľky v existujúcom kóde → chýba/sedí/naviac.
2. Priprav migračný plán pre chýbajúce tabuľky/stĺpce/väzby (bez straty existujúcich dát).
3. Ukáž mi porovnávaciu tabuľku a migračný plán, počkaj na potvrdenie, potom implementuj.

---

## 2. Fáza E — Zosúladenie s Etapou 5 (Návrh používateľského rozhrania)

Starý prompt mal iba hrubý „Minimálny katalóg obrazoviek (26 obrazoviek)" a všeobecný „Model obrazoviek". Etapa 5 to nahrádza oveľa presnejšou špecifikáciou:

- **Informačná architektúra a hlavná navigácia** — rozloženie aplikácie, štruktúra hlavného menu, dashboard ako vstupný bod, organizačná navigácia, navigácia medzi súvisiacimi objektmi, globálne a lokálne vyhľadávanie, kontextové akcie, zachovanie pracovného kontextu, priame odkazy/záložky. Over, či navigácia vo frontende zodpovedá tomuto modelu.
- **Spoločné prvky a správanie UI** — presné pravidlá pre: zoznamovú obrazovku, tabuľky údajov, vyhľadávanie/filtre, formulár a validácie, ochranu pred súbežnou úpravou (optimistic locking v UI), detail záznamu a jeho záložky, stavové označenia, tlačidlá/akcie, potvrdenie významných operácií, informačné/chybové hlásenia, dokumenty a prílohy, históriu a auditnú stopu v UI, správanie pri načítavaní/prázdnom výsledku/nedostupnosti, export zoznamov. Toto je záväzný „design system" na úrovni správania (nie vizuálu) — implementuj ako zdieľané UI komponenty/patterny, nie opakovane v každej obrazovke.
- **Katalóg obrazoviek IS ŠUAS** — rozšírený a preskupený zoznam obrazoviek podľa oblastí (spoločné/dashboardy, organizácie, UAS/komponenty/batérie, piloti, operácie/lety/telemetria, údržba/poruchy, incidenty, dokumenty, reporty/kontroly/audit, používatelia/oprávnenia/administrácia). Porovnaj s pôvodným zoznamom 26 obrazoviek a doplň chýbajúce.
- **Dashboardy a organizačný prehľad** — rozlišuje **úrovne dashboardov** (napr. centrálny dashboard MV SR — UI-003, dashboard organizačnej zložky — UI-004, dashboard organizačného útvaru — UI-005), spoločné filtre, spôsob započítania údajov podľa organizácie, definíciu základných ukazovateľov. Toto je podstatne detailnejšie než pôvodné „widgety podľa roly" — implementuj viacúrovňový dashboard, nie jeden univerzálny.
- **Obrazovky registrov a detailov evidovaných objektov** — presné rozloženie pre register UAS, komponenty/vybavenie, batérie, organizácie/útvary atď.

**Postup:**
1. Over existujúce obrazovky oproti katalógu obrazoviek a spoločným UI pravidlám z Etapy 5.
2. Zapíš zoznam chýbajúcich obrazoviek/chýbajúcich UI patternov (napr. chýbajúca ochrana pred súbežnou úpravou, chýbajúci viacúrovňový dashboard).
3. Doplň zdieľané UI komponenty (list/detail/form/status/confirm/history patterns) tak, aby ich používali všetky moduly konzistentne.

---

## 3. Fáza F — Zosúladenie s Etapou 6 (Technická špecifikácia)

Toto je najdôležitejšia nová časť — **prvýkrát ide o skutočnú technickú špecifikáciu**, nie iba nefunkčné požiadavky ako v starom prompte. Kľúčové rozdiely oproti starému prompte:

### 3.1 Technologická neutralita — over kompatibilitu zvoleného stacku
Master špecifikácia **neurčuje** konkrétny jazyk/framework/databázu/cloud — necháva to na dodávateľa s ohľadom na štandardy MV SR, bezpečnostnú klasifikáciu, licencie, dlhodobú podporu a prenositeľnosť. To znamená:
- **Nie je dôvod meniť stack**, ktorý už bol zvolený v predchádzajúcej implementácii (Node/NestJS alebo .NET, PostgreSQL, React) — pokiaľ spĺňa architektonické princípy nižšie.
- Over, či zvolený stack a nasadenie **nie sú** naviazané na neschválenú externú cloudovú infraštruktúru (pozri BR/NFR o kontrole MV SR nad dátami).

### 3.2 Logická a aplikačná architektúra
Over/doplň podľa: hlavné logické komponenty, modularita funkčného riešenia, oddelenie prezentačnej/aplikačnej/doménovej vrstvy, procesný komponent, aplikačné rozhrania, **transakčné spracovanie**, **asynchrónne spracovanie**, **idempotentnosť a ochrana pred duplicitou** (dôležité najmä pre import telemetrie — BP-14), dokumentový/súborový komponent, reportovací komponent, organizačný model systému, konfigurácia systému, spracovanie chýb, rozšíriteľnosť.

### 3.3 Dátová, databázová a súborová architektúra
Fyzický databázový model, identifikátory záznamov, referenčná integrita, povinné/jedinečné údaje, časové údaje, historizácia a časová platnosť, súbežná úprava, dátové typy/formáty, číselníkové hodnoty, indexovanie a vyhľadávanie, **telemetrické údaje** (osobitný režim ukladania), reportovacie a odvodené údaje, ochrana databázových údajov, databázové migrácie, súborové úložisko, prijatie súboru, integrita a nemennosť súborov, prístup k súborom, **elektronické podpisy**, súlad databázy a súborového úložiska, retencia a archivácia, kapacitný návrh.

### 3.4 Identita, autentifikácia a riadenie prístupu
Zdroj identity a integrácia s IAM MV SR, autentifikácia používateľov, životný cyklus používateľského účtu, autorizačný model, organizačný rozsah prístupu, **privilegované roly a oddelenie zodpovedností**, delegovanie oprávnení, správa používateľských relácií, **technické a integračné účty**, **núdzový prístup** (break-glass), kontrola a pravidelné preverovanie prístupov.

### 3.5 Kybernetická bezpečnosť a ochrana údajov
Bezpečnostná klasifikácia systému a údajov, základné bezpečnostné princípy, ochrana komunikácie a kryptografické opatrenia, bezpečnosť aplikácie a API, ochrana osobných a citlivých údajov, ochrana dokumentov/súborov/importov/exportov, bezpečnosť infraštruktúry a konfigurácie, riadenie zraniteľností a aktualizácií, bezpečnostné incidenty, bezpečnostné testovanie.

### 3.6 Integrácie a API — vrátane novej integrácie na LPS SR
Toto v starom prompte chýbalo úplne:
- **Integrácia LPS SR** (Letové prevádzkové služby SR) a mapovej služby vzdušného priestoru — musí ísť cez **samostatný integračný adaptér**, nesmie byť napevno previazaná s business logikou plánovania letov; adresa služby/vrstvy/timeouty/autentifikácia/interval aktualizácie musia byť konfigurovateľné; pri každej mapovej kontrole sa loguje zdroj, vrstvy, čas a výsledok; nedostupnosť/neaktuálnosť mapovej služby sa **nesmie** interpretovať ako „bez obmedzenia"; komunikácia s LPS SR nesmie bez osobitného schválenia odovzdávať identitu používateľa/pilota/UAS/útvaru/účelu operácie.
- **Telemetrické zdroje** — každý zdroj má samostatný importný profil/konektor s definovaným mapovaním polí, jednotkami, časovým pásmom a validáciou; pôvodný súbor/správa sa uchováva nezmenená; automatická synchronizácia podporuje evidenciu posledného úspešného prenosu, riadené opakovanie a manuálne spustenie; pridanie nového konektora nesmie vyžadovať zmenu DB štruktúry základného letového záznamu, ak používa už podporované normalizované údaje; priame odosielanie dát do cloudu výrobcu je zakázané bez výslovného schválenia MV SR.

### 3.7 Výkonnosť a kapacitné požiadavky — teraz s konkrétnymi číslami
Starý prompt mal iba všeobecné „95 % do 2s". Nová špecifikácia určuje presnejšie cieľové hodnoty (over si presné znenie v dokumente, orientačne):
- otvorenie bežnej obrazovky/detailu — do 2 s,
- uloženie bežného formulára — do 3 s,
- vyhľadávanie a filtrovanie — do 3 s,
- načítanie dashboardu — do 5 s,
- vytvorenie bežného reportu — do 30 s,
- API požiadavka bez čakania na externý systém — do 2 s,
- uvedené hodnoty platia pre 95 % požiadaviek pri schválenom zaťažení,
- dlhšie importy/exporty/reporty musia bežať na pozadí s viditeľným stavom spracovania,
- čakanie na externý systém musí mať nastavený timeout.

**Postup pre Fázu F:**
1. Priprav si checklist podľa bodov 3.1–3.7 a over každý bod oproti existujúcej implementácii (áno/čiastočne/nie).
2. Kde je „nie" alebo „čiastočne" — navrhni konkrétny krok na doplnenie (napr. chýbajúci LPS SR adaptér, chýbajúci timeout na externé volania, chýbajúce asynchrónne spracovanie importov).
3. Priority urč takto: najprv bezpečnosť a identita (3.4, 3.5) → potom dátová/súborová architektúra (3.3) → potom integrácie (3.6) → potom výkon/kapacita (3.7).
4. Ukáž mi checklist a navrhované kroky pred implementáciou.

---

## 4. Aktualizovaný poriadok práce

1. Ak ešte nebol spravený gap-audit podľa pôvodného promptu (Fáza A), urob ho najprv — ale porovnávaj už oproti **v1.1**, nie oproti pôvodnému PDF.
2. Fáza D (dátový/databázový model) — najskôr, lebo ostatné fázy sa naň odvolávajú.
3. Fáza E (UI) — nadväzuje na hotový/doplnený dátový model.
4. Fáza F (technická špecifikácia) — prierezovo, priebežne pri každej z predošlých fáz, s prioritou na bezpečnosť/identitu.
5. Následne pokračuj pôvodným fázovaním modul po module (sekcia 7 pôvodného promptu), tentoraz už s presnými entitami/tabuľkami z Etapy 4 a presnými obrazovkami z Etapy 5.

---

*Tento doplnok vychádza z dokumentu „Projekt ATLAS – MASTER v1.1" (`PROJEKT_ATLAS_specifikacia_master_v1_1.md`), ktorý oproti pôvodnej PDF verzii pridáva Etapu 4 (informačný a databázový model), Etapu 5 (návrh používateľského rozhrania) a Etapu 6 (technická špecifikácia). Kapitoly 1–3 (analýza, business procesy, funkčná špecifikácia) zostali rozsahovo rovnaké — moduly MOD-01–18 aj procesy BP-01–22 sa nezmenili, over si však prípadné spresnenia priamo v texte.*
