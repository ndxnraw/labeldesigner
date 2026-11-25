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
- Koliesko mení číselnú hodnotu až po kliknutí do jej poľa a len počas jeho
  zamerania. Samotný hover ani prechod klávesom Tab hodnotu nemenia; koliesko
  vtedy posúva nadradený panel. Po opustení poľa treba opäť kliknúť. Písanie,
  klávesy šípok a tlačidlá číselníka zostávajú dostupné.
- Rovnaká ochrana platí pre samostatné rozbaľovacie ponuky v editore, nastaveniach
  a tlači: zatvorený výber sa kolieskom zmení až po kliknutí a počas zamerania.
  Otvorený zoznam možno posúvať aj po otvorení klávesnicou. Po opustení poľa
  ochrana opäť zabráni zmene. Písanie a výber klávesmi zostávajú dostupné.
  Otvorená ponuka prispôsobí šírku položkám v medziach obrazovky; celý aktuálny
  názov je dostupný aj v bubline pri podržaní myši.

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

Samostatné tlačidlo „Kontrola výstupu“ je odstránené; kontrolu otvorí tlačidlo
Tlačiť. Šírka názvov kariet sa riadi prekladom a veľkosťou písma. Pri nedostatku
miesta sa karty rozložia do viacerých riadkov, aby sa názov Kalibrácia neorezával.

Tlačidlá rastú podľa preloženého textu a písma. Dlhé zaškrtávacie popisy a
vysvetlenia sa zalamujú podľa dostupnej šírky. Pri malom okne alebo veľkom písme
majú horné nastavenia tlačiarne a opakovaní vlastné posúvanie, aby zostal priestor
aj pre obsah kariet. Oba spodné príkazy zostávajú v okne; dlhý súhrn možno posúvať.
Rozbaľovacie ponuky poskytujú rodičovskému rozloženiu skutočnú výšku natívneho
poľa, vrátane zmien písma oboma smermi.

Pri korekciách, mierke, ignorovaní okrajov ovládača, DPI a tmavosti Zebra sú
stručné lokálne vysvetlenia v štyroch jazykoch. Tie isté texty sú dostupné ako
prístupný opis ovládacieho prvku. Objasňujú smer posunu, východiskovú mierku,
význam DPI, odlišnosť vypnutej tmavosti od hodnoty 0 a fyzické limity tlačiarne.
Samotné nastavenia tlače ani predtlačové potvrdenie sa tým nemenia.

## Lišta, obnova panelov a editor GS1

Ďalší balík vychádza zo štyroch bodov trojstranového používateľského PDF
„Labeldesigner“ z 18. novembra 2026.

- Ponuka **Nástroje** má skupiny **Projekt**, **Vložiť**, **Dáta**,
  **Rozloženie** a **Kontrola a údržba**. Príkazy aj skupiny používajú rovnaké
  vektorové predlohy ikon ako horná lišta; všetky pôvodné funkcie zostávajú dostupné.
- Z lišty sú odstránené **Vybrať kód**, **Tabuľka** a **Centrovať kód**.
  Hlavný kód vyberiete na plátne alebo v zozname prvkov. Centrovanie je vo
  vlastnostiach vybraného hlavného kódu, rešpektuje zámok. Tabuľka zostáva cez
  **Panely → Tabuľka** a zachováva svoj pôvodný editor aj deliacu priečku.
- Skrytie panela skryje aj jeho obsah. Opätovné zobrazenie používa zapamätanú
  logickú šírku, vyčistí zastaraný vodorovný posun a obnoví rozloženie potomkov.
  Rovnaký postup používa ponuka, F4/F8 aj obnova uloženého stavu. Reset rozloženia
  obnoví základné šírky. Prvé štyri riadky súboru `.layout` zostávajú kompatibilné;
  dva voliteľné riadky pridávajú logické šírky. Starý súbor má pôvodné predvolené šírky.
  Po načítaní sa šírky nastavia po dokončení úvodného DPI škálovania okna, aby
  moderný runtime neprepočítal už prepočítaný rozmer druhýkrát.
  Spodné príkazy zoznamu Prvky majú výšku podľa skutočného textu a šírky stĺpca;
  pri nedostatku priestoru sa obsah posúva, takže sa neoreže ani dlhší preklad.
- **Identifikátory…** pre GS1-128 otvoria pomenované polia, rýchle voľby GTIN,
  šarže, exspirácie a sériového čísla a lokálne vyhľadávanie katalógu. Každé pole
  ukazuje formát a svoju chybu, výsledný zápis a upozornenia sa obnovujú priebežne.
  Opakovaný výber AI presunie zameranie na existujúce pole. Poradie menia šípky,
  odstránenie je pri poli; **Použiť** je dostupné až po platnej kontrole.
  Dátum zostáva presne v zadanom formáte RRMMDD vrátane dňa 00. Kontrolné číslice
  sa nemenia bez súhlasu. **Zrušiť** neprepíše pôvodné údaje. Existujúce limity
  20 AI, 48 dátových znakov GS1-128, oddelenie FNC1, upozornenia a XML ostávajú.

Horné voľby aj polia GS1 sú pri malom okne alebo veľkom písme posúvateľné.
Oba motívy zostávajú zachované po pridaní, presune a odstránení poľa; chybové
informácie sú aj v prístupnom opise vstupu. Nové texty majú všetky štyri preklady.
Katalóg a kontrola fungujú lokálne bez nových knižníc alebo internetového pripojenia.

Overenie tohto balíka 18. novembra 2026: všetkých 32 sád prešlo na oboch runtime,
vrátane 1 545 kontrol editora na každom. SDK zostavenie oboch cieľov má 0 chýb
a 0 varovaní. Kompatibilné výsledky sú zhrnuté v
`build/test-results/pdf-workspace-framework-complete` so súborom `run-map.json`,
ktorý zachováva pôvod jednotlivých úspešných behov; cielená kontrola finálnej DPI
obnovy je v `build/test-results/20261118-114356`. Moderné výsledky sú v
`build/sdk-test-results/net10.0-windows/20261118-114523`. Overené sú motívy,
obnova panelov a rozloženia, zámok centrovania, GS1 dáta a poradie, zrušenie,
neplatné údaje a minimálny priestor pri zväčšenom písme. Fyzický presun medzi
monitormi s odlišným DPI ani fyzické tlačenie tým nie sú nahradené.

## Platformy a overenie

Funkcie sú lokálne a nevyžadujú internet ani nové knižnice. Rovnaké projekty
fungujú na .NET 10 pre Windows. Automatické rozmery sa
počítajú aj po dosadení dávkových premenných, takže tlač a export zdieľajú geometriu.

`EditorRefinementTests` overuje predvolené číslice a staré XML, prirodzenú šírku,
Unicode a viac riadkov, centrovanie, dávkové premenné, živý inšpektor, skutočné
spracovanie klávesov Enter/Esc, undo/redo, obnovenie stavu po zrušení, autosave,
zámok, nové texty, prístupnosť vstupu a úplnosť tlačových kariet. Obrázky zachytávajú
skutočné naštýlované tlačové okno aj jeho minimálnu veľkosť. Štandardný runner má
32 sád; fyzické tlačenie na konkrétnom zariadení zostáva manuálnym overením.

Overenie úprav kolieska a tlačových ovládacích prvkov 18. novembra 2026:
všetkých 32 sád prešlo na kompatibilnom aj modernom runtime, vrátane 215 kontrol
`EditorRefinementTests` na každom z nich. Testy používajú aj natívne správy Windows
vnútorného číselného vstupu a overujú preklady kariet pri 100/150/200 % veľkosti
písma. SDK zostavenie oboch cieľov skončilo s 0 chybami a 0 varovaniami.
Výsledky sú v `build/test-results/20261118-065226` a
`build/sdk-test-results/net10.0-windows/20261118-065809`.

Balík ochrany ponúk, čitateľnosti a vysvetlení bol overený 18. novembra 2026:
32/32 sád na oboch runtime vrátane 1 360 kontrol `EditorRefinementTests` na každom.
Overuje aj natívne správy pevných a editovateľných ponúk, zachovanie klávesnice,
šírky otvorených zoznamov, aktuálnu bublinu hodnoty, výšku riadkov po zmene písma
oboma smermi, zalamovanie zaškrtávacích popisov a oba spodné príkazy v skutočnej
minimálnej vonkajšej veľkosti okna 980 × 680. SDK zostavenie oboch cieľov má
0 chýb a 0 varovaní. Výsledky: `build/test-results/20261118-080409` a
`build/sdk-test-results/net10.0-windows/20261118-081654`. Vizuálne bol overený
aj dlhý maďarský zaškrtávací popis pri 200 % písme; nejde o fyzickú zmenu DPI monitora.

Overenie 17. novembra 2026: všetkých 32 sád prešlo na oboch runtime, vrátane
87 kontrol tejto úpravy na každom z nich. Finálne SDK zostavenie oboch cieľov
skončilo s 0 chybami a 0 varovaniami. Výsledky a snímky sú v ignorovaných
`build/test-results/20261117-124207` a
`build/sdk-test-results/net10.0-windows/20261117-124842`.

Oba distribučné ZIPy prešli aj produkčným overovačom aktualizácií a self-testom
rozbalených aplikácií. Moderný self-test mal vypnuté vyhľadávanie nainštalovaného
runtime. Balíky obsahujú túto príručku.
