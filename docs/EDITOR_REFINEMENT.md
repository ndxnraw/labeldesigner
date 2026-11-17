# Priama úprava textu a prehľadnejšia tlač — Pre-release

Úpravy vychádzajú zo šiestich požiadaviek v používateľovom dokumente
„Labeldesigner zmeny“. Obidve strany PDF boli prečítané aj vizuálne overené.

## Ovládanie

- Typ hlavného kódu vyberáte priamo v rozbaľovacej ponuke. Duplicitné tlačidlo
  „Vybrať typ…“ je odstránené. Katalóg na pridávanie ďalších kódov zostáva.
- Nová etiketa používa číslice kódu **12 bodov**. Úvodné rozloženie neklesá pod
  **10 bodov**; podľa potreby rozšíri kód aj do voľného vonkajšieho okraja etikety.
  Povinné tiché zóny kódu zostávajú v jeho rozmeroch. Veľmi úzke etikety stále
  vyžadujú väčší priestor alebo inú veľkosť písma; upozornenie na nezmestiteľný
  popis sa nestráca. Uložené veľkosti aj starý XML predvolený údaj 8 bodov sa nemenia.
- Text vo vlastnostiach sa mení aj na etikete priebežne počas písania.
  **Enter** potvrdí, **Shift+Enter** vloží nový riadok, **Esc** obnoví pôvodný text.
- Dvojklik na text na etikete otvorí vstup priamo na plátne. Formátovanie meníte
  vo vlastnostiach. Enter alebo opustenie vstupu potvrdí úpravu; Esc obnoví celý
  pôvodný rámček aj obsah. Pri otočenom texte je vstup čitateľný vodorovne,
  výsledný text zachováva pôvodnú rotáciu. Zamknuté prvky nemožno upravovať.
- **Šírka podľa textu** je predvolená pre nové texty a úvodný názov produktu.
  Rámček rastie aj sa zužuje podľa obsahu; výška podľa potreby narastie pre nové
  riadky alebo zalomenie. Rozmery sú obmedzené priestorom etikety. Ručná zmena
  šírky či výšky automatiku vypne; zaškrtnutím ju môžete obnoviť. Esc pri ťahaní
  obnoví aj toto nastavenie. Staré projekty bez `AutoWidth` majú ručné rozmery.
- Písanie tvorí jeden krok Späť/Znova po potvrdení. Automatické uloženie uchová
  rozpracovaný obsah bez zatvorenia vstupu a bez straty kurzora.

## Tlačové okno

Väčšie okno ponecháva tlačiareň, opakovania, súhrn, náhľad a akcie tlače prístupné.
Vedľajšie nastavenia sú v kartách:

| Karta | Obsah |
| --- | --- |
| Médium | Papier, orientácia, profily, vlastnosti ovládača, rozmery dokumentu, ignorovanie hlásenej tlačiteľnej oblasti |
| Kalibrácia | Korekcie X/Y, mierky a sprievodca skúšobným výtlačkom |
| Zebra | Režim ZPL, DPI a voliteľná tmavosť |
| Detaily | Diagnostika, čas prípravy a úplné hodnoty premenných aktuálnej tlačovej úlohy |

Obsah kariet je posúvateľný. Kontrola výstupu, upozornenia na papier, potvrdenie
používateľa, profily, zrušenie a obmedzenia kalibračného režimu zostávajú zachované.
Prepnutie karty nemení nastavenia ani nevytvára nový náhľad. Neodosiela sa žiadna
úloha bez pôvodnej predtlačovej kontroly.

## Platformy a overenie

Funkcie sú lokálne a nevyžadujú internet ani nové knižnice. Rovnaké projekty
fungujú na .NET Framework 4.8/4.8.1 aj .NET 10 Windows. Automatické rozmery sa
počítajú aj po dosadení dávkových premenných, takže tlač a export zdieľajú geometriu.

`EditorRefinementTests` overuje predvolené číslice a staré XML, prirodzenú šírku,
Unicode a viac riadkov, centrovanie, dávkové premenné, živý inšpektor, skutočné
spracovanie klávesov Enter/Esc, undo/redo, obnovenie stavu po zrušení, autosave,
zámok, nové texty, prístupnosť vstupu a úplnosť tlačových kariet. Obrázky zachytávajú
skutočné naštýlované tlačové okno aj jeho minimálnu veľkosť. Štandardný runner má
32 sád; fyzické tlačenie na konkrétnom zariadení zostáva manuálnym overením.

Overenie 17. novembra 2026: všetkých 32 sád prešlo na oboch runtime, vrátane
87 kontrol tejto úpravy na každom z nich. Finálne SDK zostavenie oboch cieľov
skončilo s 0 chybami a 0 varovaniami. Výsledky a snímky sú v ignorovaných
`build/test-results/20261117-124207` a
`build/sdk-test-results/net10.0-windows/20261117-124842`.

Oba distribučné ZIPy prešli aj produkčným overovačom aktualizácií a self-testom
rozbalených aplikácií. Moderný self-test mal vypnuté vyhľadávanie nainštalovaného
runtime. Balíky obsahujú túto príručku.
