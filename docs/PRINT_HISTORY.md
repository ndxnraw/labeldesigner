# História tlače

Otvorte **Nástroje → Kontrola a údržba → História tlače**. Každý riadok zobrazuje čas
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
Do výpočtu vstupujú aj potvrdené množstvá z pokusov, ktoré už boli
z histórie odstránené alebo automaticky prečistené.

### Prehľad dávok a trvalé ID

Otvorte **Prehľad dávok…** v histórii tlače. Pri zvolenej dávke uvidíte
pôvodný pokus, prepojené dotlače, potvrdené množstvá a zostatok.
Z prehľadu môžete otvoriť výsledok jednotlivého pokusu po riadkoch
alebo zobraziť uzávierky dávky.

ID pôvodnej úlohy je zároveň trvalým ID dávky. Ďalšia dotlač ho zdedí
aj vtedy, keď vznikne z predchádzajúcej dotlače. Pri prečistení alebo
ručnom odstránení z histórie sa metadáta pokusu a dostupné počty po
riadkoch uchovajú lokálne v podpriečinku `batches`. Podklady na tlač
a údaje frontu sa naďalej riadia limitmi histórie tlače. Pri starších
záznamoch sa využijú zachované väzby. Údaje odstránené pred zavedením
tejto funkcie sa nedajú obnoviť.

Prenos medzi počítačmi je dostupný cez **Projekt →
Prenosná záloha a obnova… → Dávky a uzávierky**. ZIP prenáša pôvodné
ID a uložené počty aj uzávierky. Náhľad rozlišuje nové dávky, zhodné
údaje a konflikty. Pri konflikte môžete použiť údaje zo zálohy pre
vybranú dávku. Ak sa zostatok po zlúčení líši od uloženej uzávierky,
náhľad zobrazí obe hodnoty. Podklady na samotnú dotlač zostávajú
v miestnej histórii tlače.

### Uzavretie a export dávky

Po ručnom potvrdení pôvodnej tlače vyberte jej záznam a stlačte
**Uzavrieť dávku…**. Skontrolujte súhrn po zdrojových riadkoch. Ak niektoré
dotlače čakajú na potvrdenie, uzavretie treba výslovne odsúhlasiť. Pri
kladnom zostatku zadajte aj dôvod. Uzávierka uloží čas, množstvá po
riadkoch, počet nepotvrdených dotlačí a dôvod. Počas uzavretia sú úpravy
výsledkov a odosielanie prepojených dotlačí zablokované.

V časti **Uzávierky dávok…** zobrazíte uložené stavy a exportujete ich do
CSV. Súbor je v UTF-8, používa čiarku a zachováva čísla zdrojových riadkov
aj rovnaké `batch_id` pre všetky uzávierky jednej dávky.
Pri etikete bez zdrojového riadka je pole prázdne. Otvorenie CSV
v tabuľkovom programe môže vyžadovať výber čiarky ako oddeľovača.

Ak je pôvodný pokus v histórii alebo v prehľade uchovaných dávok, funkcia
**Znova otvoriť dávku…** sprístupní úpravy počtov. Po ďalšom uzavretí vznikne nový záznam. Predchádzajúce
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
