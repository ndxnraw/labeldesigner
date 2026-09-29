# ND LabelDesigner 3.0.8

Titulkové lišty všetkých vlastných okien aplikácie teraz používajú spoločné nastavenie témy a logo ND LabelDesigner. Týka sa to hlavného okna, Nastavení, hlášok vrátane O aplikácii a neuložených zmien, úprav textu, symbolu, rotácie, GS1, náhľadu ZPL aj tlačového okna.

Farba pozadia a textu titulku vychádza z aktuálnej svetlej alebo tmavej témy. Neaktívny titulok má jemne stlmený text. Zmena témy v Nastaveniach obnoví aj titulok otvoreného dialógu. Opätovné vytvorenie natívneho okna zachová farby. Vysoký kontrast používa systémové farby titulku.

Rám, systémové tlačidlá, presun, zmena veľkosti a systémové menu zostávajú natívne. Nepribudli vlastné náhrady titulku, časovače ani nové závislosti. Logo je vložené do programu a načítava sa raz; nie je závislé od pracovného priečinka.

## Rozsah a kompatibilita

Systémové dialógy Otvoriť/Uložiť a okná ovládačov tlačiarní zostávajú spravované Windows. Na starších verziách Windows, ktoré nepodporujú vlastnú farbu titulku cez DWM, sa použije dostupný systémový vzhľad; logo zostáva nastavené. Presná farba titulku je podporovaná najmä Windows 11. Formáty projektov, nastavenia, náhľad a exporty sa nemenia.

## Overenie

Nové testy `Revision308Tests` otvárajú skutočné dialógy v oboch témach a kontrolujú vložené logo, natívny rám, atribúty DWM, živú zmenu témy a obnovenie atribútov po vytvorení nového handle. Hlášky zachovávajú tlačidlá potvrdenia a zrušenia. Výsledky všetkých regresných sád sú v `testy.txt`.

Ručne nebol overovaný vysoký kontrast, všetky staršie zostavenia Windows ani presun medzi monitormi s rôznym DPI. Testy neodosielajú fyzickú tlač.

Ručná kontrola: v svetlej aj tmavej téme otvorte Nastavenia, O aplikácii, dialóg úpravy textu dvojklikom a upozornenie na neuložené zmeny. Titulky majú zobrazovať logo aplikácie a ladiť s jej témou. Skúste prepnutie témy priamo v Nastaveniach, deaktiváciu okna a Alt+Space.

## Zmenené súbory

Spoločná implementácia je v `WindowTheme.cs`. Používajú ju `ModernUi.cs` a `UiTypography.cs`; pôvodná duplicitná obsluha v `WindowHeader.cs` a `Printing.cs` bola odstránená. `build.cmd` zahŕňa nový súbor a vložené logo. Verzia je aktualizovaná v AssemblyInfo, manifeste, rozhraní, prekladoch a návode.
