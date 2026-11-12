# Pre-release

Spracovanie dávok na pozadí:
- Import, príprava záznamov a tlačových plánov, kontrola a export používajú
  pracovné vlákno s priebehom a bezpečným zrušením.
- Virtuálna tabuľka a samostatný náhľad záznamu s rušením zastaraných požiadaviek.
- Atómové výstupy zachovajú pôvodný súbor pri chybe alebo zrušení.
- Index strán obmedzuje opakované prechádzanie celej dávky pri vykresľovaní.
- Lokálna diagnostika s identifikátorom zostavy a chýb, rotáciou a exportom;
  obsah dokumentov, správy výnimiek a cesty sa do logu neukladajú.

Ukladanie konceptov a príprava údajov:
- Rozpracované kódy, premenné a rozloženia možno uložiť a znova otvoriť;
  overenie výstupu zostáva prísne.
- Atómové ukladanie s jednou zálohou predchádzajúceho čitateľného projektu.
- Dátumy a číslovanie sa vyhodnotia pri príprave úlohy a zostávajú rovnaké
  pri náhľade, tlači aj exporte; tlačové okno umožňuje výslovný prepočet.
- Čas prípravy a hodnoty premenných sú viditeľné, dátum editora sa obnovuje po polnoci.
- Testy obnovy, zlyhaní ukladania, zmien dátumu a zachovania definícií premenných.

Úpravy tlače a médií:
- Nesúlad rozmerov papiera je upozornenie s potvrdením pokračovania.
- Nezávislý vektorový náhľad zobrazí celý dokument aj pri chybných údajoch ovládača.
- Voliteľné ignorovanie tlačiteľnej oblasti ovládača a vlastné médium podľa dokumentu.
- Viac pomenovaných profilov médií pre jednu tlačiareň vrátane kalibrácie a režimu orezania.
- Spätné načítanie starších profilov a regresné testy tlače bez fyzického výstupu.

Program je vo vývoji. Obsahuje grafický editor etikiet, čiarové a QR kódy,
import údajov z CSV/XLSX, dávkovú tlač a export do PDF, SVG, DOCX a ZPL.

Podporované funkcie a obmedzenia opisuje [prehľad funkcií](docs/FEATURES.md).
Podrobné zmeny sú v histórii commitov.
