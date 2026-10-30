# Kôš (mazanie záznamov)

## Ako to funguje

- **Vymazať** (tlačidlo s ikonou koša) presunie záznam do koša. Záznam okamžite zmizne zo všetkých zoznamov, detailov, hľadania aj prehľadov, ale v databáze zostáva.
- **Kôš** (menu Správa → Kôš, adresa `/kos`) vidí iba gestor. Záznamy sú rozdelené do kategórií; každý sa dá **Obnoviť** alebo **Vymazať natrvalo** (potvrdenie napísaním `VYMAZAŤ`).
- Všetko (presun, obnovenie, trvalé vymazanie, aj zamietnuté pokusy) je v audite (`Trash.*`).

## Hromadné mazanie

- Ak sa na záznam niečo viaže (napr. pilot má lety), po kliknutí na **Vymazať** sa ukáže zoznam týchto väzieb so zaškrtávacími políčkami — aj väzby ďalej (napr. poruchy k letom). Všetko je predvolene označené, po skupinách sa dá označiť/odznačiť. Jedno tlačidlo presunie do koša vybrané väzby aj samotný záznam. Ak niečo neoznačíte, záznam sa nevymaže.
- V **Koši** sa dajú záznamy označiť (aj „označiť všetko“) a hromadne **obnoviť** alebo **natrvalo vymazať**.
- **Vymazať natrvalo** pri jednom zázname ukáže rovnaký zoznam väzieb (aj tých, ktoré sú v koši — označené „v koši“) a vymaže ich naraz. Väzby, ktoré sa nemažú (riadky mesačných hlásení a pod.), sú vypísané ako upozornenie a tlačidlo je vtedy neaktívne.

## Kategórie a kto smie mazať

| Kategória | Kde je tlačidlo | Kto smie presunúť do koša |
|---|---|---|
| Piloti | detail pilota | gestor; prevádzkovateľ vo svojom útvare |
| Osvedčenia | riadok osvedčenia (detail pilota, Osvedčenia) | gestor; prevádzkovateľ vo svojom útvare |
| Denník letov | detail letu | gestor; prevádzkovateľ vo svojom útvare; pilot len lety, ktoré sám vytvoril |
| Žiadosti o let | detail žiadosti | gestor; prevádzkovateľ vo svojom útvare (pilot má naďalej vlastné „Vymazať“ pre svoje koncepty) |
| Register UAS | detail UAS | gestor; prevádzkovateľ vo svojom útvare |
| Poruchy | detail poruchy | gestor; prevádzkovateľ vo svojom útvare; autor poruchy |
| Incidenty | riadok v zozname | gestor; prevádzkovateľ vo svojom útvare; autor hlásenia |
| Prevádzkovatelia a používatelia | Prevádzkovatelia → Odstrániť | gestor (vlastný účet nejde vymazať) |

Obnoviť a natrvalo vymazať smie **iba gestor** (oprávnenie `trash.manage`).

## Väzby (záznam sa nedá vymazať, kým sa naň niečo viaže)

| Mažem | Najprv treba vymazať |
|---|---|
| Pilot | jeho lety v denníku |
| UAS | lety s UAS, poruchy UAS, incidenty s UAS |
| Žiadosť o let | lety k žiadosti, súvisiace incidenty |
| Let | poruchy a incidenty k letu |

Pri **trvalom vymazaní** blokujú aj väzby, ktoré sú v koši, a navyše záznamy, ktoré sa nemažú vôbec:
riadky mesačných hlásení (pilot, UAS), priradenia funkcií a incidenty s UAS ako dotknutým prostriedkom (UAS),
importy GEO zón a delegácie (účet). Pilot sa natrvalo vymaže spolu so svojimi osvedčeniami, históriou a oprávneniami na UAS;
UAS spolu so svojou históriou a oprávneniami pilotov.

## Technicky

- Tabuľka `TrashEntries` (migrácia `20261029140000_TrashBin`). Záznam koša sa nikdy nemaže — obnovenie/trvalé vymazanie sa len doplní.
- Skrytie zabezpečuje globálny filter v `IsSuasDbContext` (`AddTrashFilter`) — netreba upravovať jednotlivé zoznamy. Seeder a kôš používajú `IgnoreTrashFilter`.
- Fyzické mazanie je inak zakázané; jedinou výnimkou je `db.AllowPhysicalDelete()` v `TrashService`.
- Služba: `IsSuas.Infrastructure/Services/TrashService.cs`, API: `TrashController` (`/api/v1/trash`, `/batch`, `/dependents`, `/restore-batch`, `/{id}/dependents`, `/purge-batch`), UI: `wwwroot/js/core/trash.js` (tlačidlo), `wwwroot/js/pages/trash.js` (stránka Kôš).
- Nový typ do koša: doplniť kategóriu v `TrashCategories`, `AddTrashFilter<T>` v DbContexte, `LoadTargetAsync`, `FindBlockersAsync` (väzby = automaticky aj zaškrtávací zoznam), `RemoveEntityAsync` a tlačidlo `trashButtonHtml(...)` v UI.

## Známe obmedzenia

- Technická dokumentácia (modely dronov) sa ukladá iba v prehliadači, nie na serveri — tá má vlastné „Odobrať“ a do koša nejde.
- Organizačné útvary majú vlastné odobratie/obnovenie v Organizačnej štruktúre a v koši nie sú.
- Ak v koši leží napr. UAS so sériovým číslom X, nový UAS s tým istým číslom nepôjde uložiť — najprv starý obnovte alebo natrvalo vymažte.
