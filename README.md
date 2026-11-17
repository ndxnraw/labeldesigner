# ND LabelDesigner

**Pre-release**

Program pre Windows na návrh a tlač produktových etikiet. V grafickom editore môžete pridávať texty, obrázky, čiarové a QR kódy, upravovať rozmery a rozloženie etikety. Podporuje údaje z CSV/XLSX, dávkovú tlač a export do PDF, SVG, DOCX a ZPL. Pracuje lokálne.

[Stiahnuť Pre-release](https://github.com/ndxnraw/LabelDesigner/releases/tag/Pre-release) · [Návod](docs/USER_GUIDE.txt)

Riadené aktualizácie sa kontrolujú na požiadanie a spúšťajú až po potvrdení.
Podporujú návrat k predchádzajúcej verzii aj ručný import bez internetu;
podrobnosti sú v [návode na aktualizácie](docs/UPDATES.md).

Klávesnicové ovládanie, čitateľnosť dialógov a rozsah overenia opisuje
[prístupnosť rozhrania](docs/ACCESSIBILITY.md).

Rozbaľte ZIP a spustite `ND-LabelDesigner.exe`. Potrebný je Windows s .NET Framework 4.8 alebo 4.8.1.

Zdrojový kód je v C# / Windows Forms. Zostavenie: `scripts\build.cmd`, testy: `scripts\test.cmd`. Podrobnosti sú v [dokumentácii](docs/BUILD.md).

Zdroj podporuje aj paralelné zostavenie pre .NET 10 LTS so spoločným jadrom.
Moderný balík `Windows-NET10-x64` obsahuje potrebný runtime; kompatibilný balík
naďalej používa .NET Framework 4.8. Oba používajú rovnaké projekty etikiet.

Čitateľnejší popis EAN, samostatný GTIN-14 / GS1-128 a nastavenie tmavosti Zebra
opisuje [návod EAN a Zebra](docs/BARCODES_ZEBRA.md).

Výber 21 možností 1D, 2D a GS1 vrátane vyhľadávania, príkladov a pravidiel
údajov opisuje [katalóg kódov](docs/BARCODE_CATALOG.md).

Priame písanie dvojklikom, automatickú šírku textu, väčšie číslice a usporiadanie
tlačového okna opisuje [návod úprav editora](docs/EDITOR_REFINEMENT.md).
