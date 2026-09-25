# ND LabelDesigner

ND LabelDesigner je desktopová aplikácia pre Windows na návrh, export a tlač etikiet. Texty aj údaje čiarových kódov sa zadávajú ručne. Editor umožňuje pripraviť produktové, skladové či logistické etikety s presnými rozmermi.

## Stiahnutie a spustenie

- [Najnovšie vydanie pre Windows](https://github.com/ndxnraw/LabelDesigner/releases/latest)
- [Úplne prvá verzia — EAN Etikety 1.0](https://github.com/ndxnraw/LabelDesigner/releases/tag/v1.0)
- [Všetky vydania](https://github.com/ndxnraw/LabelDesigner/releases) · [Archív starších verzií](Archive/README.md)

Stiahnite ZIP, rozbaľte celý obsah a spustite `ND-LabelDesigner.exe`. Súbor `ND-LabelDesigner.exe.config` ponechajte vedľa programu. Aplikácia vyžaduje Windows s .NET Framework 4.8 alebo 4.8.1. Inštalácia programu nie je potrebná.

## Čo program umožňuje

- Vizuálny návrh etikety: presúvanie a zmena veľkosti prvkov myšou, automatické prichytávanie k okrajom a stredom, priblíženie a kopírovanie prvkov.
- Textové polia s výberom fontu, veľkosti, tučného písma a zarovnania; úprava textu aj dvojklikom v náhľade.
- EAN-13, EAN-8, UPC-A, Code 128, Code 39, Interleaved 2 of 5 a ITF-14. Code 128 podporuje GS1 aplikačné identifikátory, ich poradie a dáta.
- Vektorový slovenský symbol Zálohované s možnosťou presúvania a zmeny rozmerov.
- Rozmery v milimetroch alebo centimetroch, bežné aj vlastné formáty papiera a nastavenie okrajov.
- Export do PDF, Wordu, ZPL a transparentného SVG. Word obsahuje samostatné grafické časti etikety.
- Priama tlač zo samostatného okna s výberom tlačiarne, papiera, orientácie, počtu kópií a náhľadom. Priblíženie náhľadu nemení fyzickú mierku tlače.
- Svetlá a tmavá téma, slovenčina, angličtina, čeština a maďarčina; uloženie preferencií spustenia.

## Súbory a kompatibilita

Rozpracované návrhy sa ukladajú do projektových XML súborov. Program otvára aj ZPL a PRN s podporovaným ZPL obsahom. Vlastné ZPL s vloženými projektovými údajmi možno ďalej upravovať; podporované cudzie ZPL sa otvára v samostatnom náhľade. Súbory NiceLabel `.nlbl` sa otvárajú cez priradený externý program.

Podrobný postup práce je v [návode](Navod.txt). Pri tlači musí papier zvolený v ovládači zodpovedať vloženému médiu; používajte skutočnú veľkosť 100 %.

## Zdrojový kód

Aplikácia používa C# a Windows Forms na .NET Framework 4.8. Zdrojový kód, lokálne fonty a zostavovací skript nájdete v [Zdrojovy-kod](Zdrojovy-kod). Na zostavenie spustite `Zdrojovy-kod/build.cmd`; regresné testy spúšťa `Testy/run-tests.ps1` v PowerShelli.

Zmeny jednotlivých vydaní sú uvedené v [Releases](https://github.com/ndxnraw/LabelDesigner/releases) a [dokumentácii](Dokumentacia).

Created by Daniel Návojský (ndxnraw) using Codex | 2026
