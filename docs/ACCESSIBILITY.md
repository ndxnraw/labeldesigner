# Prístupnosť a spoľahlivosť rozhrania — Pre-release

Úpravy rozhrania v zostave .NET 10 fungujú offline a bez účtu.

## Klávesnica a orientácia

- **Tab / Shift+Tab:** postup medzi dostupnými ovládacími prvkami. Vlastné tlačidlá
  a rozbaľovacie polia prekresľujú označenie aktívneho prvku pri zmene výberu.
- **F6 / Shift+F6:** presun medzi príkazovou lištou, ľavými kartami, plátnom
  a vlastnosťami. Skryté panely sa preskočia.
- **F4:** vlastnosti výberu; v prípade potreby sa panel zobrazí. Pri prázdnom výbere
  možno klávesnicou prečítať vysvetlenie, prečo sa neponúkajú úpravy.
- **F8** otvorí kontrolu dokumentu. V zozname prvkov **F2** alebo **Enter** upraví
  výber a **Shift+F10** otvorí jeho ponuku akcií.
- **Ctrl+plus / Ctrl+mínus** mení mierku plátna a **Ctrl+0** ju prispôsobí oknu.
  Rovnaké akcie sú pod plátnom.
- **Ctrl+PageUp / Ctrl+PageDown:** prepínanie kariet. Keď má výber hlavička karty,
  fungujú aj šípky doľava/doprava a Home/End. Šípky v textovom poli nemenia kartu.

Hlavičky kariet sa prispôsobujú šírke a písmu; dlhý názov sa zalomí. Karty majú
prístupný názov, rolu a stav výberu. Polia aktualizujú prístupný názov pri zmene
jazyka a popisu jednotiek. Podporované sú slovenčina, angličtina, čeština a maďarčina.
Vlastné tlačidlá a rozbaľovacie polia rešpektujú systémové kontrastné farby;
význam výberu a závažnosť hlásenia sa neodovzdávajú iba farbou.

## Dialógy a chyby

Text spoločných hlásení je dostupný cez Tab, možno ho označiť, kopírovať a posúvať
klávesnicou. Závažnosť je uvedená slovom. Tlačidlá Áno/Nie/Zrušiť si zachovávajú
pôvodný význam aj potvrdenia; sú oddelené od posúvateľného obsahu.

Nastavenia a úprava textového poľa majú posúvateľný obsah a samostatný pás akcií.
Pri zmenšení zostáva dostupné zavretie alebo potvrdenie. Escape ruší/zatvára dialóg;
v úprave textu zostáva Enter v samotnom viacriadkovom texte novým riadkom.

V **Nástroje → Kontrola a údržba → Správa aplikácie…** sú karty Aktualizácie,
Zálohy a Diagnostika. Karty rešpektujú systémové kontrastné farby a spoločné
tlačidlo Zavrieť ostáva viditeľné aj pri posúvaní obsahu. Záznamy diagnostiky
sa dajú označiť a kopírovať klávesnicou. Pri potvrdeniach aktualizácií,
návratu a čistenia je predvolená voľba Nie.

Chyby aplikácie obsahujú vysvetlenie a odporúčaný postup pre chýbajúce oprávnenie,
nedostupný súbor/zariadenie alebo neplatné údaje. Pôvodné podrobnosti a existujúci
diagnostický odkaz zostávajú zachované. Správa nemení údaje ani automaticky
neopakuje zlyhanú operáciu.

## Rozsah overenia

`AccessibilityUiTests` pokrýva klávesnicové prepínanie, prístupné názvy/stavy,
malé rozmery, návrat po zmene veľkosti, dlhé hlásenia a skutočné dialógy. Karty
sa skúšajú s písmom pri 100/125/150/200 %; dialógy so synteticky zväčšenými
rozmermi aj písmom v obmedzenom priestore. Skúšajú sa oba motívy a štyri jazyky.
Správa aplikácie sa skúša aj pri vynútenej vetve vysokého kontrastu so
systémovými farbami a písmom do 200 %.
Staršie sady ďalej overujú tlačový náhľad, aktualizačné potvrdenia, koncepty,
export a ochranu neuloženého dokumentu.

Ide o automatické skúšky WinForms a jeho prístupnostných objektov. Nenahrádzajú
ručné overenie čítačkou obrazovky, zapnutého kontrastného motívu Windows,
presunu medzi fyzickými monitormi s rôznym DPI ani fyzickej tlače. Tieto kroky
zostávajú súčasťou overenia na konkrétnom pracovisku.
