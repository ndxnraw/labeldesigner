# ND LabelDesigner 3.0.7

## Zmeny

- Pri otáčaní myšou sa texty, čiarové kódy aj symboly automaticky prichytávajú k 0°, 90°, 180° a 270°. Tolerancia je ±3°, vrátane prechodu cez 360°. Mimo tolerancie je otáčanie plynulé.
- Shift naďalej používa kroky po 15°. Ručne zadané uhly sa neprichytávajú, takže možno nastaviť napríklad 89,25°. Esc obnoví uhol pred začatím ťahania.
- Odstránená ponuka a externé otváranie NiceLabel `.nlbl`. Výber súborov ponúka projekty XML a ZPL/PRN. Nepodporovaná prípona sa odmietne ešte pred čítaním súboru alebo zmenou návrhu.
- Aktualizované slovenské, anglické, české a maďarské texty, návod a číslo verzie. Uložené projekty a nastavenia nemenia formát.

## Implementácia

Prichytenie rozširuje existujúcu funkciu `ElementTransform.SnapAngle`; všetky typy prvkov používajú tú istú geometriu aj existujúce spracovanie pohybu myši. Nepribudli závislosti ani ďalšie vykresľovacie slučky. Ručné hodnoty, exporty a uložené projekty si ponechávajú presný uhol.

Zmenené aplikačné súbory: `ElementTransform.cs`, `ImportUi.cs`, `App.cs`, `translations.tsv`, `StudioLayout.cs`, `AssemblyInfo.cs`, `app.manifest`. Nové testy sú v `Revision307Tests.cs`, existujúci test verzie a testovací spúšťač sú aktualizované.

## Overenie

Podrobné výsledky aktuálneho behu sú v `testy.txt`. Nová sada overuje hranice tolerancie, záporné uhly a prechod cez 360°, Shift, simulované ťahanie rotačného úchytu pre text/kód/symbol pri troch mierkach náhľadu, Esc, zachovanie X/Y, opätovné použitie vykreslenej scény, presný ručný uhol, uloženie/načítanie, odmietnutie NLBL bez zmeny návrhu a zachovanie importu XML/PRN. Existujúce testy rotácie overujú tiež PDF, SVG, Word a ZPL.

Snímka v `Ukazky/right-angle-snap.png` je zachytená cez WinForms DrawToBitmap. Bola vizuálne skontrolovaná poloha a rám textu pri presných 90°; snímka nie je verným záznamom natívneho titulku Windows. Fyzické ovládanie myšou so stlačeným Shift a fyzická tlač neboli overované.

Ručná kontrola: spustite program, vyberte prvok a rotačným úchytom prejdite cez 90°, 180°, 270° a 0°. V blízkosti týchto uhlov má číselné pole ukazovať presný uhol. Odtiahnutím za toleranciu pokračuje voľná rotácia. Skúste Shift, Esc a ručné zadanie 89,25°. V dialógu Otvoriť už nemá byť položka NiceLabel.
