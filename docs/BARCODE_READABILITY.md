# Kontrola čitateľnosti pred tlačou

V tlačovom náhľade otvorte **Detaily → Čitateľnosť kódov…**. Prehľad ukáže
stranu, pôvodný záznam dávky, pozíciu etikety na hárku, šírku modulu v bodoch
tlačiarne, voľné okraje a výsledok skúšobného načítania. Výber riadka označí
príslušný kód v tlačovom náhľade a ukáže odporúčanie pri zistenom probléme.

Kontrola podporuje EAN-13, ITF-14, GS1-128 vrátane EAN-14 s AI (01), QR a
Data Matrix vrátane ich variantov GS1. Používa kalibráciu, rozmery média a
tlačiteľnú oblasť. Pre Zebra RAW používa nastavené DPI a rovnaký prevod na čiernu
a bielu ako výstup. Pri tlači cez ovládač preberá DPI z Windows a pri chýbajúcom
číselnom rozlíšení označí použité DPI dokumentu ako odhad.

Kontrola najskôr vykreslí príslušnú časť etikety v rozlíšení tlačiarne a lokálne
porovná dekódované údaje s pôvodnou hodnotou kódu. Kontroluje orez, zásah do
voľných okrajov a upozorní pri module menšom než dva body tlačiarne. Výsledky
vstupujú do existujúcej predtlačovej kontroly ako upozornenia, pri ktorých sa dá
pokračovať v tlači.

Automatická kontrola má limit 48 kódov, 24 strán a 6 miliónov vykreslených bodov.
Pre väčšiu úlohu vyberte **Skontrolovať celú úlohu**. Kontrola beží na pozadí,
ukazuje priebeh a dá sa zrušiť. Jednotlivý výrez nad 4 milióny bodov alebo
s hranou nad 12 000 bodov môže dostať výsledok, že skúšobné načítanie preskočilo.
Predtlačová kontrola zobrazí najviac 100 jednotlivých upozornení na čitateľnosť
a počet ďalších. Úplné výsledky zostanú v prehľade čitateľnosti.

Kontrola funguje bez internetu na .NET Framework 4.8 aj .NET 10. Vplyv média,
tmavosti, skenera a mechaniky overte skúšobným výtlačkom na vybranej tlačiarni.
