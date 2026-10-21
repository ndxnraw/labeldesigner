# ND LabelDesigner 3.0.6 — otáčanie prvkov

## Používanie

Vyberte text, hlavný čiarový kód, kópiu kódu alebo symbol. Nad prvkom je kruhový rotačný úchyt so spojnicou. Ťahajte ho ľavým tlačidlom; počas pohybu sa pri kurzore zobrazuje aktuálny uhol. Shift prichytáva po 15°. Pustenie uloží výsledok, Esc obnoví stav pred začiatkom operácie.

Vo vlastnostiach prvku možno zadať uhol v poli Rotácia (°), vrátane desatinných miest. Pravé tlačidlo ponúka Rotácia → 0°, 90° doprava, 90° doľava, 180° a Vlastný uhol. Resetovať rotáciu nastaví 0°. Pri vybranom prvku mimo textového vstupu Ctrl+] otočí o 90° doprava a Ctrl+[ o 90° doľava.

Rotácia je okolo geometrického stredu a nemení uložené X/Y. Výberový rám a úchyty sledujú otočenie. Pri resize zostáva protiľahlý úchyt ukotvený; rohy zachovávajú pomerné škálovanie a zmenu veľkosti písma. Dvojklik na otočený text stále otvorí jeho úpravu. Posúvanie, centrovanie, kopírovanie, zmena jednotiek a Smart Guides zostávajú dostupné. Vodidlá používajú vonkajšie hranice otočeného prvku. Rotačný úchyt sa podľa potreby odsúva od iných prvkov, aby neblokoval ich výber.

## Formáty a kompatibilita

Podporované sú všetky existujúce typy kódov vrátane EAN-13, EAN-8, UPC-A, ITF-14, Code 128/GS1, Code 39 a Interleaved 2 of 5, vrátane číslic a textového popisu. Otočiť možno aj vektorový symbol Zálohované a kopírované kódy. Editovateľné objekty načítané z projektového XML alebo vlastného ZPL s projektovými údajmi majú rovnaké možnosti.

TextItem a SymbolItem ukladajú Rotation, hlavný kód BarRotation. Všetky hodnoty majú pri chýbajúcom údaji predvolené 0°. Staré projekty a preferencie zostávajú kompatibilné. Uhol sa zachová aj pri kopírovaní a v projektových údajoch ZPL. Interný rozsah je 0 ≤ uhol < 360; číselné pole umožňuje dve desatinné miesta. Rotácia myšou má priebežnú desatinnú presnosť.

Cudzí ZPL/PRN zostáva náhľadom iba na čítanie a NLBL sa naďalej otvára externe. Táto revízia nepridáva nový import editovateľných bitmapových objektov.

## Vykresľovanie a export

ElementTransform poskytuje spoločnú geometriu v milimetroch, normalizáciu uhlov, maticu okolo stredu a prevod pre výber, úchyty, resize a export. Vektorová scéna drží pôvodné cesty aj ich rotačnú transformáciu. Počas presúvania a otáčania sa zlučujú pohybové udalosti a používa sa existujúca scéna.

- PDF zachováva vektorové cesty a používa transformačné matice.
- SVG používa rotate(uhol cx cy) a transparentné pozadie.
- Word obsahuje jednotlivé otočené SVG objekty a zodpovedajúce PNG alternatívy, umiestnené podľa výsledných hraníc.
- ZPL vykresľuje tú istú scénu do ^GFA pri zvolenom DPI.
- Náhľad, tlač a export orezávajú obsah na hranici etikety. Presah vyvolá upozornenie, nie zablokovanie náhľadu.

## Overenie

Zostavenie pre .NET Framework 4.8. Celkovo 15 regresných sád vrátane 583 nových kontrol rotácie. Testované texty a EAN pri 0/45/90/180/270°, Code 128 pri 90°, všetky podporované formáty, presun, všetkých osem resize úchytov pri 45/90/137/270°, tri mierky náhľadu, výber mimo pôvodného obdĺžnika, dvojklik, Esc, konečná poloha, izolácia ostatných prvkov, ručný uhol, skratky, kontextové menu, kopírovanie, XML a staré projekty. Matematika Shift prichytenia a prechodu cez 360° má samostatné kontroly.

Nezávislé vykreslenie PDF cez Poppler a SVG cez librsvg bolo porovnané s renderom aplikácie v siedmich scenároch vrátane presahu. Porovnanie povoľuje odchýlku hrany jeden pixel pri 300 DPI kvôli odlišnej rasterizácii; nie je tvrdením o bitovej zhode týchto rendererov. Viac než 99 % čiernych pixelov sa zhoduje v tejto tolerancii. ZPL ^GFA má presnú bitovú zhodu s monochromatickým renderom. Všetkých 21 samostatných SVG častí Wordu bolo porovnaných aj s ich PNG alternatívou.

Úplné výsledky sú v testy.txt a JSON protokoloch. Snímky v Ukazky pochádzajú z WinForms DrawToBitmap; nepredstavujú verné zachytenie moderného natívneho titulku DWM. Overený bol aj virtuálny PDF výstup v regresných testoch. Fyzická tlač ani skenovanie otočených kódov neprebehli. Samotné otvorenie v desktopovom Microsoft Worde, reálne prechody medzi monitormi s rôznym DPI a fyzické držanie Shift pri pohybe myšou zostávajú na ručné overenie.

## Ručná kontrola

1. Rozbaľte celý ZIP; konfiguráciu .exe.config ponechajte vedľa programu.
2. Otočte názov produktu aj kód pomocou kruhového úchytu. Skúste Shift a Esc.
3. Pri 45° presuňte prvok a zmeňte rozmery rohmi aj hranami. Dvojklikom upravte text.
4. Uložte projekt, znovu ho otvorte a skontrolujte uhol. Vyskúšajte kontextové menu a skratky.
5. Porovnajte PDF, SVG, Word a ZPL s náhľadom. Pri tlači používajte 100 % mierku a správne médium; čitateľnosť kódu overte na cieľovej tlačiarni/skeneri.

## Zdrojové zmeny

Nové ElementTransform.cs a RotationUi.cs. Rozšírené Engine.cs, Symbols.cs, ElementClipboard.cs, EditorPreview.cs, FastPreview.cs, SmartGuides.cs, VectorArt.cs, WordParts.cs, Printing.cs, App.cs a StudioLayout.cs. Verzia a preklady sú aktualizované v AssemblyInfo.cs, app.manifest a translations.tsv; build.cmd zahŕňa nové súbory. Názov okna ostáva ND LabelDesigner a verzia je vľavo dole.
