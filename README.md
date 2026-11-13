# ND LabelDesigner

**Pre-release**

Program pre Windows na návrh a tlač produktových etikiet. V grafickom editore môžete pridávať texty, obrázky, čiarové a QR kódy, upravovať rozmery a rozloženie etikety. Podporuje údaje z CSV/XLSX, dávkovú tlač a export do PDF, SVG, DOCX a ZPL. Pracuje lokálne.

[Stiahnuť Pre-release](https://github.com/ndxnraw/LabelDesigner/releases/tag/Pre-release) · [Návod](docs/USER_GUIDE.txt)

Rozbaľte ZIP a spustite `ND-LabelDesigner.exe`. Potrebný je Windows s .NET Framework 4.8 alebo 4.8.1.

Zdrojový kód je v C# / Windows Forms. Zostavenie: `scripts\build.cmd`, testy: `scripts\test.cmd`. Podrobnosti sú v [dokumentácii](docs/BUILD.md).

Zdroj podporuje aj paralelné zostavenie pre .NET 10 LTS so spoločným jadrom.
Moderný balík `Windows-NET10-x64` obsahuje potrebný runtime; kompatibilný balík
naďalej používa .NET Framework 4.8. Oba používajú rovnaké projekty etikiet.
