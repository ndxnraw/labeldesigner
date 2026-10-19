# ND LabelDesigner 3.0.5

## Ovládanie

Horný panel obsahuje 15 samostatných ikon: Otvoriť, Uložiť, Uložiť ako, Exportovať, Tlačiť, Pridať text, Symbol Zálohované, Vybrať kód, Centrovať kód, Tabuľka, Panely, Priblíženie, Nastavenia, Návod a O aplikácii. Ponuka Súbor bola odstránená; jej funkcie sú dostupné priamo. Uložiť ako sa už neopakuje v podmenu. Export a priblíženie majú vlastné rozbaľovacie ponuky.

Ikony používajú spoločné vektorové vykresľovanie, klikateľnú plochu najmenej 36 × 36 logických pixelov, lokálne tooltipy, prístupné názvy a existujúce klávesové skratky. Tlač je naďalej modro zvýraznená. Panel sa pri nedostatku miesta zalomí. Popisy údajov v editore, položky rozbaľovacích ponúk a dialógy ostávajú textové, aby bolo možné identifikovať nastavenia a zadávať hodnoty.

## Plynulosť presúvania

Predchádzajúca verzia pri každom pohybe okamžite menila geometriu, opakovane menila natívny titulok Windows a reštartovala časovač celého náhľadu. Nová verzia uchováva iba najnovšiu polohu a spracuje ju v intervale približne 16 ms, pokiaľ používateľ presúva prvok. Pri pustení myši spracuje konečnú súradnicu okamžite. Časovač mimo presúvania nebeží.

Presun ďalej využíva existujúcu vektorovú scénu. Plné zostavenie náhľadu prebehne po ukončení ťahania, nie uprostred pauzy pri držanom tlačidle myši. Titulok sa označí ako zmenený iba raz. Esc obnoví pôvodnú geometriu a zahodí čakajúci pohyb; strata zachytenia myši dokončí posledný pohyb a odstráni vodidlá. Smart Guides, samostatné zarovnávanie osí a fyzické rozmery ostávajú zachované. Zmena veľkosti cez úchyty využíva rovnaké zlúčenie udalostí, ale zachováva potrebné zostavenie geometrie pri zmene rozmerov.

Porovnávací syntetický test 2 000 pohybových udalostí pri 45 textových poliach:

| Verzia | Čas spracovania dávky | Zmeny geometrie | Zmeny titulku |
|---|---:|---:|---:|
| 3.0.4 | 508,9 ms | 2 000 | 4 000 |
| 3.0.5 | 18,7 ms | 1 | 1 |

Ide o dávkový test bez priebežného spracovania správ medzi udalosťami, nie o meranie FPS alebo záruku výkonu na každom počítači. Samostatný integračný test overuje aj reálny časovač WinForms. Surové výsledky sú v benchmark.txt.

## Zmenené súbory

- StudioLayout.cs, WindowHeader.cs: samostatné ikony, ponuka priblíženia, odstránenie Súbor a nepotrebného skrývania akcií.
- HeaderActions.cs: nové vektorové ikony, jednotná štvorcová veľkosť, odstránenie kreslenia textu tlačidiel.
- EditorPreview.cs: spracovanie najnovšieho pohybu, okamžité dokončenie, Esc a zachytenie myši; DPI zo samotného ovládacieho prvku.
- FastPreview.cs, App.cs: obmedzenie zmien titulku a úplných prepočtov počas presunu.
- AssemblyInfo.cs, app.manifest, translations.tsv, build.cmd: verzia, lokalizácia a zostavenie. Nepoužívaný UiText.cs bol odstránený.
- Testy: nová sada Revision305Tests a úprava starších kontrol na nové ikonové ovládanie a snímkové spracovanie pohybu.

## Overenie a obmedzenia

Build pre .NET Framework 4.8, 14 testovacích sád. Nová sada obsahuje 189 kontrol: ikony, zoom, presun kódu/textu/symbolu pri troch mierkach, dávky pohybov, zachovanie scény, konečná poloha, Esc, strata zachytenia a časovač. Regresné testy pokrývajú aj oba režimy, font Source Sans 3, lokalizáciu, rozmery panela, minimalizáciu, ukladanie, importy, exporty a tlačový náhľad. Úplné výsledky sú v testy.txt.

Snímky pochádzajú z WinForms DrawToBitmap mimo pracovnej plochy. Táto metóda nezachytáva verne moderné farby titulku Windows; svetlý režim bol zachytený v samostatnom spustení kvôli obmedzeniam opakovaného snímania skrytého okna. Žiadna fyzická tlač nebola spustená. Reálne rýchle pohyby na používateľovom zariadení, fyzické zmeny DPI medzi monitormi a systémové Snap gestá zostávajú na ručné overenie.

## Krátka ručná kontrola

1. Rozbaľte celý ZIP. ND-LabelDesigner.exe.config musí zostať vedľa EXE.
2. Skontrolujte tooltipy ikon myšou aj Tabom; vyskúšajte nastavenia, návod a informácie o programe. Uložiť ako je dostupné samostatnou ikonou a Ctrl+Shift+S.
3. Rýchlo presúvajte kód, text aj symbol pri prispôsobení a 200 % zoome. Pustite myš počas pohybu; prvok musí zostať na poslednej polohe s prípadným prichytením k vodidlu.
4. Počas presunu stlačte Esc. Skontrolujte návrat, uloženie a znovuotvorenie dokumentu.
5. Overte svetlú/tmavú tému, úzke okno a vlastné DPI monitora.
