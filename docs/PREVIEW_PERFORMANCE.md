# Výkon tlačového náhľadu — siedmy balík, Pre-release

Meranie z 16. novembra 2026. Zachovaná kompatibilná verzia pre .NET Framework 4.8
aj moderná Windows verzia pre .NET 10. Výsledky platia pre uvedené testovacie
prostredie; čas odpovede iných ovládačov môže byť odlišný.

## Metodika

- Pred úpravami: commit `7e0b8b265ea2edfcdefedda4d58e4e7965e7b9f7` doplnený
  iba o časovanie jednotlivých krokov. Jeho zostavenia boli zachované pred
  implementáciou zrýchlenia. Zdrojový vstup merania je
  `tests/fixtures/preview/phase6-timing.patch` a `PreviewPerformance.cs`.
- Po úpravách: siedmy balík v tomto checkoute. Oba varianty používajú rovnaký
  benchmark `tests/tools/PrintPreviewBenchmark.cs`, SHA-256
  `89d07e0024a8116d83a54bd87bddb572656e087fdb738f78bb2d3a78ba6cbcea`.
- Rovnaký počítač s 12 logickými procesormi, Windows build 26300,
  Microsoft Print to PDF a nezmenené predvolené médium. Framework používal
  CLR `4.0.30319.42000`, moderný variant runtime `10.0.15` / SDK `10.0.404`.
- Najprv pôvodný a upravený Framework, potom pôvodný a upravený .NET 10.
  Beh bol postupný, bez súbežných testovacích sád. Žiadna úloha nebola odoslaná
  na fyzickú tlač. Staršie súbežné testy sa nepoužili na výpočet zrýchlenia.
- Každý variant má 33 vzoriek: prvé tlačové okno nového procesu (1), ďalšie
  otvorenia (3), nezmenený náhľad (8), orientácia (8), posun X (4) a navigácia
  po troch stranách (9). Spolu 132 vzoriek v záverečnom porovnaní.
- Čas prvého okna zahŕňa jeho vytvorenie a čakanie na použiteľný náhľad.
  Systém a tlačový subsystém už boli spustené; nejde o meranie po reštarte Windows.
  Zmena nastavení/strany zahŕňa nezmenený 200 ms odklad zlučujúci rýchle vstupy.
  Medián pri párnom počte je priemer dvoch stredných hodnôt, p95 je nearest-rank.
  Malé počty vzoriek, hlavne jedno prvé otvorenie, obmedzujú všeobecné závery.

## Výsledky

Časy v milisekundách, **pred → po**. V zátvorke je p95.

| Operácia | .NET Framework 4.8 | .NET 10 | Pokles mediánu Framework / moderný |
|---|---:|---:|---:|
| Prvé okno a náhľad | 3721,0 → 918,3 (3721,0 → 918,3) | 3250,1 → 995,6 (3250,1 → 995,6) | 75,3 % / 69,4 % |
| Ďalšie otvorenie | 2859,0 → 651,2 (3151,6 → 670,1) | 2934,6 → 778,3 (2935,9 → 802,4) | 77,2 % / 73,5 % |
| Nezmenený náhľad | 671,0 → 61,9 (739,9 → 141,3) | 675,8 → 63,3 (765,6 → 73,6) | 90,8 % / 90,6 % |
| Zmena orientácie | 906,7 → 279,8 (1025,9 → 624,9) | 922,8 → 289,7 (999,0 → 627,4) | 69,1 % / 68,6 % |
| Zmena posunu X | 852,2 → 297,3 (937,6 → 360,8) | 863,2 → 296,2 (999,2 → 296,4) | 65,1 % / 65,7 % |
| Zmena strany | 865,2 → 281,0 (1010,2 → 373,3) | 890,3 → 282,1 (968,3 → 363,7) | 67,5 % / 68,3 % |

Typický interval UI časovača zostal na Frameworku 16,1 ms; na .NET 10 klesol
z 30,7 na 21,4 ms. Celkové p95 časovača bolo 33,0 → 39,2 ms a 42,9 → 54,4 ms;
maximum 952,9 → 897,2 ms a 1348,1 → 1324,3 ms. Tento časovač zachytáva aj
inicializáciu editora a vytváranie okien. Kratší beh má menej vzoriek a väčší
podiel inicializácie; globálne p95 sa v tomto meraní nezlepšilo. Samostatný
regresný test overuje pokračujúci UI časovač pri zámerne zablokovanom čítaní
ovládača a dokazuje, že čítanie prebieha mimo UI vlákna.

Snímka PrivateBytes po celom behu bola približne 48,9 → 50,9 MiB na Frameworku
a 41,4 → 44,2 MiB na .NET 10. Počet GDI objektov po uvoľnení zostal 100 → 100
a 99 → 99. Pamäťové snímky zahŕňajú inicializáciu a runtime warmup; predstavujú
pozorovanie celého procesu. Hranice pomocnej pamäte a uvoľnenie zdrojov overujú
samostatné testy. Limit je štyri záznamy a 32 MiB uložených obrazových dát,
nie limit celkovej pamäte aplikácie.

## Zistená príčina a úpravy

Samostatná sonda namerala približne 193 ms na čítanie nematerializovaného
`PaperSize`, 108 ms na `PrintableArea`, 73–76 ms na hardvérový okraj a 95 ms
na zoznam médií. Samotná kontrola obsahu trvala približne 7 ms a kontrola
geometrie obsahu 5 ms. Pôvodné vyhľadávanie rozmerov média opakovane načítavalo
rovnaký predvolený papier z ovládača v predikáte zoznamu.

- Aktuálny papier/orientácia sa raz načítajú a explicitne nastavia v pracovnej
  kópii nastavení. Celé nastavenie ovládača a jeho DEVMODE zostávajú zachované.
  Počas jednej prípravy sa zoznam médií načíta najviac raz.
- Tlačiteľná oblasť a hardvérové okraje tvoria jeden nemenný číselný snímok.
  Diagnostika, predtlačová kontrola a náhľad používajú tú istú hodnotu.
  Snímok je lokálny oknu, platí najviac päť sekúnd a má najviac štyri kľúče.
- Vlastnosti ovládača, zmena tlačiarne/profilu a Obnoviť tlačiarne vyprázdnia
  pomocnú pamäť. Revízia nastavení rozlišuje aj zmenený DEVMODE s rovnakými
  pomenovanými rozmermi. Nové okno vždy načítava svoje vlastné aktuálne údaje.
- Nezmenené vektorové náhľady interne pripraveného, zmrazeného dokumentu sa
  ukladajú s limitom štyroch obrázkov / 32 MiB dát. Každé odovzdanie má vlastnú
  kópiu. Externe dodané verejne meniteľné plány sa vždy vykresľujú nanovo.
  Prepočítanie údajov mení plán a vyprázdni jeho obrázky.
- Pomocná pamäť je voliteľná: zlyhanie klonovania/uloženia obrázka nevyhodí
  úspešne vykreslený náhľad. Zavretie okna odmietne oneskorený výsledok a
  uvoľní vlastnené obrázky bez čakania na dokončenie natívneho ovládača.
- Pred skutočným odoslaním sa geometria ovládača načíta nanovo a skontroluje
  sa celý dokument. Zmenený hardvérový okraj zneplatní predchádzajúce potvrdenie.
  Kontrola obsahu zostáva čerstvá pri každom náhľade aj tlači.

Pri opakovanom náhľade klesol medián fázy kontroly z 480 na 25 ms (Framework)
a 486 na 24 ms (.NET 10). Pri ďalšom otvorení klesla príprava nastavení
z 1435 na 297 ms a z 1519 na 336 ms. Fázy zahŕňajú pôvodné rozdelenie práce;
časť čítania geometrie bola presunutá z vykresľovacej fázy do spoločného snímku.
Celkový čas operácie je preto hlavný porovnávací údaj. Prevádzkové počítadlá
v pôvodných CSV boli iba rezervované; ich nuly sa nepovažujú za namerané čítania.

## Testy a zopakovanie

Na oboch platformách prešlo 27/27 spoločných sád. Nová sada má 50 kontrol:
presná rasterizácia s pamäťou/bez pamäte, zneplatnenie nastavení a prepočítaných
údajov, meniteľné externé plány, limit počtu/bajtov, päťsekundová platnosť,
nezávislé vlastníctvo, simulované zlyhanie alokácie, UI pri zablokovanom ovládači,
oneskorené výsledky a zavretie počas práce. Skutočná tlač sa nespúšťala.

Aktuálny variant sa zmeria z koreňa checkoutu:

```powershell
.\scripts\measure-preview.ps1 -Results build/perf/modern
```

Na zopakovanie pôvodného variantu použite samostatný checkout commitu `7e0b8b2`.
Zo siedmeho balíka doň skopírujte `PreviewPerformance.cs`, benchmark a jeho projekt,
zachovaný `MigrationTestHost.cs` a `measure-preview.ps1` z toho istého balíka. Aplikujte zdrojový vstup
`tests/fixtures/preview/phase6-timing.patch`. Zostavte a odmerajte obidva varianty
rovnakými príkazmi, postupne bez ďalších testov. Tým sa doplnia len časovače;
optimalizované helpery sa do pôvodného checkoutu nekopírujú.

Zachovaný benchmark možno spustiť bez zostavovania cez `-Executable`.
`compare-preview.ps1 -Before <výsledky-pred> -After <výsledky-po> -Output <výstup>`
skontroluje zhodu benchmarkového zdroja a počtov vzoriek a vypočíta mediány/p95.
Surové CSV, snímky prostredia, hashe zostavení a JSON porovnania sú v ignorovanom
`build/phase7/comparison/`; bežné merania ani binárne súbory sa necommitujú.

Fyzická tlač, iné ovládače a prechody medzi skutočnými monitormi zostávajú
na manuálne overenie. Prípadné zaseknuté natívne volanie stále zaberá jedno
existujúce tlačové STA vlákno; násilné ukončovanie ovládača sa nepridáva.
