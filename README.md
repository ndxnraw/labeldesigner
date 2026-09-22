# ND LabelDesigner

Windows aplikácia na návrh a tlač etikiet. Aktuálna verzia: **2.7.1**.

[Stiahnuť najnovšie vydanie](https://github.com/ndxnraw/LabelDesigner/releases/latest)

Vyžaduje Windows a .NET Framework 4.8. Rozbaľte ZIP a spustite `ND-LabelDesigner.exe`. [Podrobný návod](Navod.txt).

## Tlač

Samostatné tmavé okno načíta formát papiera a orientáciu tlačiarne. Tlačiareň, papier a počet kópií vyberiete priamo. Náhľad aj výtlačok používajú PrintDocument; zoom nemení fyzické rozmery. Celý názov tlačiarne je uvedený pod zoznamom. Späť do editora alebo Esc zachová návrh.

Papier nastavený v ovládači musí zodpovedať papieru v zásobníku. Používajte mierku 100 % a jednu stranu na hárok.

## Zostavenie a testy

- `Zdrojovy-kod/build.cmd`: zostavenie cez .NET Framework.
- `Testy/run-tests.cmd`: regresné a vstavané testy. Tlačí iba virtuálne do Microsoft Print to PDF, ak je dostupný.
- Overené zostavenie, A4/A5, viac strán, zrušenie nastavení a zhoda náhľadu s PDF. Fyzická tlač nebola overená.

## Archív

[Všetky staršie verzie 1.0–2.7](Archive/README.md) obsahujú pôvodné programy, zdrojové kódy a dostupné návody.

Created by Daniel Návojský (ndxnraw) using Codex | 2026
