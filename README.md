# ND LabelDesigner

**Pre-release**

Program pre Windows na návrh a tlač produktových etikiet. V grafickom editore môžete pridávať texty, obrázky, čiarové a QR kódy, upravovať rozmery a rozloženie etikety. Podporuje údaje z CSV/XLSX, dávkovú tlač a export do PDF, SVG, DOCX a ZPL. Pracuje lokálne.

[Stiahnuť Pre-release](https://github.com/ndxnraw/LabelDesigner/releases/tag/Pre-release) · [Návod](docs/USER_GUIDE.txt)

Riadené aktualizácie sa kontrolujú na požiadanie a spúšťajú až po potvrdení.
Podporujú návrat k predchádzajúcej verzii aj ručný import bez internetu;
podrobnosti sú v [návode na aktualizácie](docs/UPDATES.md).

Klávesnicové ovládanie, čitateľnosť dialógov a rozsah overenia opisuje
[prístupnosť rozhrania](docs/ACCESSIBILITY.md).

Rozbaľte ZIP pre Windows x64 do samostatného priečinka a spustite
`ND-LabelDesigner.exe`. Balík obsahuje .NET 10 a aplikácia pracuje offline.

Zdrojový kód je v C# / Windows Forms. Zostavenie: `scripts\build.cmd`, testy: `scripts\test.cmd`. Podrobnosti sú v [dokumentácii](docs/BUILD.md).

Pri prechode zo staršieho Pre-release stiahnite prvý balík s jediným
variantom ručne. Existujúce projekty, šablóny a profily tlače sa naďalej
používajú v pôvodných formátoch a umiestneniach.

Čitateľnejší popis EAN, samostatný GTIN-14 / GS1-128 a nastavenie tmavosti Zebra
opisuje [návod EAN a Zebra](docs/BARCODES_ZEBRA.md).

Výber 21 možností 1D, 2D a GS1 vrátane vyhľadávania, príkladov a pravidiel
údajov opisuje [katalóg kódov](docs/BARCODE_CATALOG.md).

Priame písanie dvojklikom, automatickú šírku textu, väčšie číslice a usporiadanie
tlačového okna opisuje [návod úprav editora](docs/EDITOR_REFINEMENT.md).
