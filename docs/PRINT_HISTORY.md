# História tlače

Otvorte **Nástroje → Dáta → História tlače**. Každý riadok zobrazuje čas
pokusu o odoslanie, tlačiareň, profil alebo média, počty vrátane opakovaní
a výsledok. **Odoslané** znamená prijatie úlohy tlačovým systémom. Pri
prerušení zistite vytlačený rozsah podľa hárkov alebo etikiet.

Vyberte odoslanú úlohu a stlačte **Skontrolovať front tlače**. Aplikácia
vyhľadá jedinečný názov úlohy vo fronte zaznamenanej tlačiarne a zobrazí
posledný známy stav, čas jeho zistenia a výsledok poslednej kontroly. Ak sa
úloha vo fronte nenájde alebo sa front nepodarí načítať, posledný známy
stav je naďalej viditeľný. Staršie záznamy sa označia ako nepriraditeľné.
Kontrola sa spúšťa na vyžiadanie a trvá najviac približne šesť sekúnd.
Ak ovládač poskytne vlastný text stavu, história uloží jeho krátku verziu.
Stav **Odovzdané podľa Windows** znamená stav frontu. Pri rozhodovaní
o opakovaní skontrolujte skutočne vytlačené etikety.

Vyberte úlohu a stlačte **Potvrdiť výsledok…**. Pri každom zdrojovom
riadku zadajte počet použiteľných etikiet zo všetkých opakovaní úlohy.
Tabuľka ukáže plánovaný počet, potvrdené množstvá z tejto úlohy a zo
súvisiacich dotlačí a zostávajúci počet. Uložený ručný výsledok aktuálnej
úlohy uvidíte v histórii pri hlásení frontu Windows.

Po kontrole dotlače potvrďte jej výsledok v samostatnom zázname dotlače.
Pôvodná dávka prepočíta zostatok po riadkoch aj po oprave potvrdenia.
Ak výtlačky chýbajú alebo sú nepoužiteľné, potvrďte nulu. Pred odoslaním
ďalšej dotlače sa zobrazí upozornenie na súvisiace pokusy bez potvrdenia.
Ak sa od prípravy návrhu zmenili potvrdené výsledky, pripravte návrh
znova z aktuálneho zostatku.
Pri výpočte sa použijú prepojené záznamy dostupné v histórii. Odstránenie
záznamu dotlače môže zmeniť zostatok.

### Uzavretie a export dávky

Po ručnom potvrdení pôvodnej tlače vyberte jej záznam a stlačte
**Uzavrieť dávku…**. Skontrolujte súhrn po zdrojových riadkoch. Ak niektoré
dotlače čakajú na potvrdenie, uzavretie treba výslovne odsúhlasiť. Pri
kladnom zostatku zadajte aj dôvod. Uzávierka uloží čas, množstvá po
riadkoch, počet nepotvrdených dotlačí a dôvod. Počas uzavretia sú úpravy
výsledkov a odosielanie prepojených dotlačí zablokované.

V časti **Uzávierky dávok…** zobrazíte uložené stavy a exportujete ich do
CSV. Súbor je v UTF-8, používa čiarku a zachováva čísla zdrojových riadkov.
Pri etikete bez zdrojového riadka je pole prázdne. Otvorenie CSV
v tabuľkovom programe môže vyžadovať výber čiarky ako oddeľovača.

Ak pôvodná úloha zostala v histórii, funkcia **Znova otvoriť dávku…**
sprístupní úpravy. Po ďalšom uzavretí vznikne nový záznam. Predchádzajúce
uzávierky zostávajú dostupné aj po odstránení alebo automatickom
prečistení histórie. Ukladajú sa lokálne v podpriečinku
`batch-closures` histórie tlače.

### Návrh dotlače

Nové úlohy ukladajú počty po riadkoch. Pri starších úlohách sa počty
načítajú z uloženého obsahu. Ak je obsah k dispozícii, po uložení
čiastočného alebo nulového výsledku použite **Navrhnúť dotlač…**.
Návrh priradí súčet potvrdených množstiev najprv k skorším kópiám a potom
k prvým etiketám každého riadka v kópii. Pripraví zostávajúce etikety
z jednej kópie.
Pozície na hárku sa zachovajú. Návrh overte podľa skutočných výtlačkov
v náhľade. Ak poznáte presné chýbajúce pozície, vyberiete ich cez
**Pripraviť dotlač…**.

Ak chcete úlohu neskôr zopakovať, na karte **Detaily** v tlačovom okne
začiarknite **Uložiť obsah úlohy lokálne na opakovanie** pred odoslaním.
Obsah môže zahŕňať hodnoty premenných, kódy, texty a obrázky. Voľba je
predvolene vypnutá pre každé nové tlačové okno. V histórii môžete odstrániť
riadok spolu s jeho uloženým obsahom.

Pri zázname s uloženým obsahom vyberte **Pripraviť opakovanie…**. Zadajte
čísla strán alebo pôvodné čísla záznamov dávky, napríklad `1,3-5`. Prázdny
výber pripraví celú úlohu. Pozície vybraných etikiet na hárku zostanú
zachované. Náhľad obnoví tlačiareň, papier, kalibráciu, nastavenia Zebra a
počet opakovaní. Špecifické nastavenia ovládača, ako zásobník, overte v
jeho vlastnostiach. Pred odoslaním potvrďte novú predtlačovú kontrolu.

Na obnovu prerušenej tlače vyberte úlohu s uloženým obsahom a stlačte
**Pripraviť dotlač…**. Vyberte kópiu pôvodnej úlohy a zvoľte rozsah podľa
skutočných výtlačkov.

- **Zvyšné strany** začnú za poslednou správne vytlačenou stranou. Hodnota
  0 pripraví celú vybranú kópiu.
- **Vybrané etikety** umožnia označiť jednotlivé etikety podľa pôvodnej
  strany, pozície a dostupného čísla záznamu. Pozície na hárku sa zachovajú.

Náhľad zobrazí počet strán a etikiet a pripraví jedno opakovanie. Pri
viacerých nedokončených kópiách pripravte každú samostatne. Po potvrdení
tlače pribudne samostatný pokus. Tlačidlo **Prejsť na zdrojovú úlohu**
vyberie zdrojový záznam, kým je dostupný v histórii.

História sa ukladá vedľa miestnych nastavení, mimo ich aktualizačných záloh, a uchováva najviac
50 pokusov. Obsah má limit 16 MiB na úlohu a 128 MiB spolu. Pri prekročení
celkového limitu sa uvoľní najstarší obsah. Pri prekročení limitu jednej
úlohy sa uchová záznam o odoslaní a tlačové okno oznámi, že obsah sa
neuložil. Funkcia je dostupná bez internetu na .NET Framework aj .NET 10.
