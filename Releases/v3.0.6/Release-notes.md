# ND LabelDesigner 3.0.6 — otáčanie prvkov

Stiahnite **ND-LabelDesigner-3.0.6-Windows.zip**, rozbaľte celý obsah a spustite `ND-LabelDesigner.exe`. Konfiguráciu `.exe.config` ponechajte vedľa EXE. Vyžaduje Windows a .NET Framework 4.8/4.8.1.

- Plynulé otáčanie textov, všetkých podporovaných čiarových kódov a symbolov pomocou rotačného úchytu.
- Číselné zadanie uhla, Shift prichytenie po 15°, Esc na obnovenie pôvodného stavu.
- Otočené výberové rámy, hit-testing, resize úchyty a kurzory; zachované presúvanie a dvojklik na text.
- Kontextové menu rotácie a skratky Ctrl+] / Ctrl+[ pre otočenie o ±90°.
- Uloženie rotácie do projektov a zachovanie vo všetkých exportoch: vektorové PDF/SVG/Word a ZPL ^GFA pri zvolenom DPI.
- Staré projekty bez uhla sa načítajú s rotáciou 0°. Presah mimo etikety sa oreže bez blokovania náhľadu.

Balík obsahuje zdrojový kód, návod, testy, dokumentáciu a ukážkové exporty. Zostavenie a 15 testovacích sád prešlo vrátane 583 kontrol rotácie. Nezávisle overená zhoda PDF/SVG, grafických dát ZPL a SVG/PNG častí Wordu.

Fyzická tlač, skenovanie kódov, otvorenie v desktopovom Worde a reálne prechody medzi monitormi s rôznym DPI neboli overené. Cudzí ZPL/PRN zostáva náhľadom iba na čítanie; NLBL sa otvára externe.
