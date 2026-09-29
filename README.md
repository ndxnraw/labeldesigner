# ND LabelDesigner

ND LabelDesigner je desktopová aplikácia pre Windows na návrh a tlač produktových etikiet. Texty a údaje čiarových kódov zadávate ručne a rozloženie upravujete priamo v grafickom editore. Program pracuje lokálne a nevyžaduje internetové pripojenie.

## Čo program ponúka

- **Grafický editor etikiet:** presúvanie, zmena veľkosti a ľubovoľné otáčanie textov, kódov a symbolov. Zarovnávacie vodiace čiary pomáhajú s umiestnením; rotácia sa prichytáva k pravým uhlom alebo s klávesom Shift po 15°.
- **Textové polia:** úprava dvojklikom, výber fontu, veľkosti, tučného písma a zarovnania. Prvky možno kopírovať a vkladať cez Ctrl+C a Ctrl+V.
- **Čiarové kódy:** EAN-13, EAN-8, UPC-A, ITF-14, Code 128, Code 39 a Interleaved 2 of 5. GS1-128 umožňuje zvoliť aplikačné identifikátory, ich poradie a dáta.
- **Presné rozmery:** nastaviteľná etiketa, poloha a veľkosť prvkov v milimetroch alebo centimetroch; bežné aj vlastné formáty papiera a okraje.
- **Symbol Zálohované:** vektorový symbol s možnosťou presunu, škálovania a rotácie.
- **Priama tlač:** samostatné okno s náhľadom, výberom tlačiarne, papiera, orientácie a počtu kópií. Priblíženie náhľadu nemení fyzickú mierku tlače.
- **Vzhľad a jazyk:** svetlá a tmavá téma, zladené titulky vlastných okien, slovenčina, angličtina, čeština a maďarčina. Nastavenia spustenia si možno uložiť.

## Projekty, import a export

Rozpracované etikety sa ukladajú do `.ean.xml`. Projekt uchováva obsah, rozmery, fonty, polohy a rotácie prvkov.

| Formát | Použitie |
| --- | --- |
| PDF | Vektorový export pre tlač a zdieľanie. |
| SVG | Vektorová etiketa s transparentným pozadím. |
| Word / DOCX | Samostatné transparentné SVG objekty s PNG alternatívami pre staršie čítačky. |
| ZPL | Grafický výstup pre kompatibilné tlačiarne pri zvolenom DPI. |
| ZPL / PRN – import | Vlastný ZPL s projektovými údajmi sa otvorí na úpravu; podporovaný cudzí ZPL sa zobrazí v náhľade iba na čítanie. |

Import cudzieho ZPL nepokrýva všetky príkazy a natívne fonty tlačiarní sa môžu zobraziť približne. Súbory `.nlbl` a QR kódy nie sú podporované.

## Stiahnutie a spustenie

1. Otvorte [najnovšie vydanie](https://github.com/ndxnraw/LabelDesigner/releases/latest) a stiahnite Windows ZIP.
2. Rozbaľte celý balík do priečinka.
3. Spustite `ND-LabelDesigner.exe`. Súbor `ND-LabelDesigner.exe.config` ponechajte vedľa programu.

Potrebný je Windows s **.NET Framework 4.8 alebo 4.8.1**. Balík obsahuje program, návod, zdrojový kód a regresné testy. Podrobný návod je v [Navod.txt](Navod.txt); staršie vydania nájdete v [Releases](https://github.com/ndxnraw/LabelDesigner/releases) a [archíve](Archive/README.md).

## Zdrojový kód

Aplikácia je napísaná v C# a používa Windows Forms. Zostavenie spustíte cez `Zdrojovy-kod/build.cmd`, regresné testy cez `Testy/run-tests.ps1`. Použité písmo Source Sans 3 je pribalené lokálne vrátane licencie v `Zdrojovy-kod/Fonts/OFL.txt`.

Created by Daniel Návojský (ndxnraw) using Codex | 2026
