# ND LabelDesigner — Pre-release

Aktuálny Pre-release pre Windows; vychádza z vývojového stavu zo 7. októbra 2026
a obsahuje úpravy tlače, konceptov, dátumových premenných, dávok na pozadí
a zjednoteného ovládania a predvýstupnej kontroly
z 12. novembra 2026. Zachováva Windows Forms,
.NET Framework 4.8, lokálne písmo Source Sans 3 a staršie projekty `.ean.xml`.

## Postupný prechod platformy

Aplikácia má spoločné jadro pre dokumenty, kontrolu, vykresľovanie a exporty.
Kompatibilné zostavenie pre .NET Framework 4.8 zostáva zachované vrátane pôvodného
offline zostavovania. Paralelné zostavenie pre .NET 10 LTS používa rovnaké zdroje
a projekty etikiet. Moderný Windows x64 ZIP obsahuje vlastný runtime; používateľ
ho nemusí inštalovať. K dispozícii sú štandardné projekty a spoločné testy oboch cieľov.
Názov produktu je naďalej Pre-release.

## Nové možnosti

- **Prvky a Kontrola:** trvalé karty pod nastaveniami etikety a papiera.
  Prvky sú zoradené od popredia po pozadie; výber sa synchronizuje s plátnom.
  Zoznam označuje skupiny a zámky a ponúka spoločné príkazy pre vrstvy,
  zamknutie a úpravu. Viacnásobný výber má samostatný panel spoločných akcií.
  F2 upraví vybraný odomknutý prvok zo zoznamu, F4 otvorí vlastnosti,
  F8 kontrolu; Ctrl+PageUp/PageDown prepína karty vo vybranom paneli.
- **Spoločná kontrola výstupu:** tlač aj jednotlivý, hárkový a dávkový export
  používajú rovnaký zoznam chýb a upozornení a súhrn záznamov, etikiet,
  strán, opakovaní celej úlohy a výsledných počtov. Filter nemení platnosť
  kontroly. Chyby nemožno potvrdiť; upozornenia vyžadujú výslovné zaškrtnutie.
  Nesúlad papiera ostáva upozornením. Pred odoslaním tlače sa kontrola opakuje;
  zmena nastavení alebo upozornení vyžiada nové potvrdenie. Tlačí sa v 100 % mierke.
  Problém dávky uvádza pôvodný riadok, prvú dotknutú stranu a zachováva väzbu
  na prvok. Späť na úpravy sa pri vybranej položke vráti na prvok alebo riadok.
  Dávkové okno priebežne ukazuje vybrané záznamy, etikety a chyby;
  prázdny výber alebo zahrnutý chybný riadok vypne obe výstupné tlačidlá.
- **Späť a Znova:** ikony a Ctrl+Z/Ctrl+Y, história obsahu, vlastností a rozmerov;
  súvislé ťahanie je jeden krok. V textovom poli zostáva skratka textovému editoru.
- **Obnova práce:** samostatné obnovovacie kópie, nastaviteľný interval 10–3600 s,
  ponuka obnovy po nečakanom ukončení. Uložený projekt sa neprepisuje.
- **Rozpracované koncepty:** chybný alebo prázdny kód, nedokončené premenné
  a nevyhovujúce rozloženie možno uložiť a znova otvoriť na opravu.
  Chyby naďalej blokujú tlač a export. Štruktúra a limity súboru, geometria
  a bezpečnosť vložených obrázkov sa kontrolujú aj pri ukladaní konceptu.
  Prepísanie čitateľného projektu uchová predchádzajúci obsah v `.ean.xml.bak`.
  Nečitateľný prepisovaný súbor nenahradí existujúcu zálohu.
- **Šablóny:** galéria s náhľadmi, vlastné šablóny, odstránenie a otvorenie ako nový
  projekt. Zoznam posledných 15 projektov upozorní na chýbajúci súbor.
- **Dávky:** CSV a XLSX, výber hárka a hlavičky, mapovanie textov, kódov a počtu
  etikiet; náhľad záznamu, označenie chýb a výber záznamov na tlač alebo export.
  Import, príprava záznamov a tlačového plánu, kontrola výstupu aj export
  bežia na pozadí. Okno priebehu uvádza aktuálny krok a dostupný počet položiek;
  tlačidlo Zrušiť aj zavretie okna požiadajú o bezpečné ukončenie operácie.
  Tabuľka výsledkov používa virtuálne riadky. Náhľad vybraného záznamu
  sa pripravuje samostatne a zastarané požiadavky sa zahadzujú.
- **Bezpečné zrušenie:** výsledok nahradí predchádzajúcu tabuľku alebo dávku
  až po úspešnom dokončení. Export sa zapisuje do dočasného súboru;
  pri chybe alebo zrušení sa cieľový súbor nenahradí. Zrušenie po dokončenom
  nahradení súboru sa nepovažuje za zrušený export.
- **Diagnostika:** Nástroje → Diagnostika zobrazí posledné lokálne záznamy
  a umožní ich uložiť do textového súboru. Chyby dostanú identifikátor,
  typ výnimky, miesto zlyhania a identifikátor zostavy. Správy výnimiek,
  cesty k súborom, názvy tlačiarní ani obsah dokumentov sa do logu nezapisujú.
  Denník rotuje najviac do troch súborov po približne 1 MB.
- **Výber a rozloženie:** Ctrl/Shift, výberový obdĺžnik, spoločný presun,
  kopírovanie, odstránenie, skupiny, zamknutie, zarovnanie, rozostupy a vrstvy.
- **Samolepiace hárky:** riadky, stĺpce, štyri okraje, medzery a prvá voľná pozícia.
  Spoločné rozloženie strán používajú tlač, náhľad aj dávkové exporty.
- **Profily tlačiarní:** formát papiera, orientácia a korekcia X/Y v mm;
  kalibračná etiketa sa otvorí v náhľade a tlačí sa až po stlačení Tlačiť.
  Jedna tlačiareň môže mať viac pomenovaných profilov pre rôzne médiá.
  Profil uchováva aj režim ignorovania tlačiteľnej oblasti ovládača.
- **Nesúlad papiera:** menší formát hlásený ovládačom je upozornenie;
  používateľ môže potvrdiť pokračovanie v mierke 100 %. Náhľad je nezávislý
  od orezania ovládača a zobrazí celý dokument. Rozmery dokumentu a údaj
  ovládača sa zobrazujú samostatne. V rozšírených nastaveniach možno použiť
  rozmery dokumentu ako vlastné médium alebo ignorovať dodatočné orezanie
  podľa ovládača. Fyzické limity zariadenia a orezanie samotného ovládača
  nemožno týmto režimom odstrániť.
- **Kontrola etikety:** chyby a upozornenia, výber dotknutého prvku kliknutím,
  presahy, text, údaje kódov, voľný priestor kódov a rozlíšenie obrázkov.
  Kontrolné rámčeky sa neexportujú.
- **Obrázky:** PNG, JPEG a podporovaná vektorová podmnožina SVG, uloženie priamo
  v projekte, rotácia, presun, veľkosť, pomer strán a percentuálny výrez.
- **Premenné:** číslovanie, prefix/suffix, nulovanie, pevný text a dátum,
  posuny dní/mesiacov. Zápis `{serial}` funguje v textoch a kódoch.
  Jedna úloha vyhodnotí údaje pri príprave a uchová ich pre náhľad aj výstup.
  Zmena tlačiarne, orientácie, počtu kópií alebo priblíženia ich neprepočíta.
  Tlačové okno ukazuje čas prípravy a hodnoty premenných; pri jednej etikete
  ich aktualizuje tlačidlo **Prepočítať údaje**, pri dávke **Pripraviť dávku**.
  Dátum v editore sa po zmene dňa obnoví do 30 sekúnd bez označenia projektu
  za zmenený. Pevné dátumy zostávajú pevné. Projekt uchová definície premenných,
  nie posledné vypočítané hodnoty.
- **QR a Data Matrix:** viac samostatných kódov, vektorová geometria, tiché zóny,
  editovanie dvojklikom a všetky existujúce výstupy. GS1 DataMatrix sa neponúka.

## Ovládanie

Nové funkcie sú v ponuke **Nástroje** na hornej lište. História má dve samostatné
ikony. Dávku pripravte po nastavení mapovania; zmena mapovania vymaže pripravené
výsledky, aby sa omylom nevytlačili staré údaje. Chybné označené riadky blokujú
výstup; možno ich opraviť v zdroji alebo vedome zrušiť ich zahrnutie.

Počet etikiet určuje položky a strany výstupu. Počet kópií v okne tlače opakuje
celú tlačovú úlohu. Priblíženie náhľadu ani kalibračný posun nemenia mierku.
Korekcia profilu sa aplikuje na tlač a jej náhľad, nie na exportované súbory.
Mapovanie stĺpcov, hlavička, oddeľovač a voľby dávky sa pri príprave dávky
uložia do projektu. Samotný CSV/XLSX zdroj znova vyberte pri ďalšom otvorení
dávkového okna; jeho údaje sa automaticky neukladajú do projektu.

Viacstranový SVG export vytvorí ZIP so samostatnými transparentnými SVG stranami.
PDF aj DOCX obsahujú všetky strany. Dávkový ZPL obsahuje samostatnú grafickú úlohu
pre každú stranu. Jednotlivý pôvodný ZPL export si zachováva projektové metadáta.

## Formáty a obmedzenia testovacej verzie

- CSV: UTF-8 alebo Windows-1250, bodkočiarka, čiarka alebo tabulátor; podporované
  úvodzovky, diakritika, viacriadkové polia a úvodné nuly.
- XLSX: uložené hodnoty, zdieľané texty, číselné formáty s nulami a dátumy.
  Vzorce sa nepočítajú; chýbajúci uložený výsledok je chyba. Číselné dátumy sa
  normalizujú na `yyyy-MM-dd`; časová časť sa nepreberá. Starý formát XLS nie je podporovaný.
- Najviac 10 000 záznamov/etikiet, 256 stĺpcov, 50 textov a 20 symbolových prvkov
  vrátane obrázkov a dodatočných kódov. Veľké dávky môžu vytvárať veľké exporty.
- Obrázok: do 8 MB a 16 miliónov pixelov. SVG je lokálna vektorová podmnožina:
  cesty, jednoduché tvary, skupiny, transformácie, farby a základný text.
  Externé zdroje, skripty, DTD, filtre, masky a nepodporované konštrukcie sa odmietnu.
  Zložité logo pred importom preveďte na jednoduché vektorové cesty alebo PNG.
- História je obmedzená na 100 stavov / približne 128 MB; veľké obrázky môžu
  znížiť počet uchovaných krokov. História samotná sa neukladá do projektu.
- Obnovovacie kópie, šablóny, profily tlačiarní a nedávne projekty sa ukladajú
  lokálne vedľa súboru nastavení do priečinka s príponou `.workspace`.
- Staršie verzie programu nepoznajú nové typy objektov. Nové projekty upravujte
  v aktuálnom Pre-release; pred otvorením v staršej verzii si ponechajte zálohu.

## Overenie

Regresné testy spustíte cez `scripts/test.cmd`; postup je v `docs/TESTING.md`.
Fyzická tlač, reálne prepínanie monitorov s rozdielnym DPI a správanie všetkých
ovládačov tlačiarní vyžadujú kontrolu na cieľovom počítači.

### Krátka manuálna kontrola

1. Otvorte starý projekt, pridajte PNG a QR; presuňte, otočte a vráťte zmenu
   cez Ctrl+Z. Uložte a znovu otvorte projekt.
2. Zapnite obnovu na 10 sekúnd, zmeňte návrh a počkajte na uloženie kópie.
   Iba na skúšobnom dokumente ukončite proces nečakane; po spustení overte obnovu.
3. Vytvorte skúšobný projekt dávky, importujte vlastný skúšobný CSV a pripravte dávku.
   Skontrolujte názvy, kódy, počty a strany. Chybný kód musí blokovať výstup
   označeného riadka.
4. Vyberte Microsoft Print to PDF; overte A4/A5, orientáciu a prvú voľnú
   pozíciu hárka. PDF tlačte v 100 % mierke. Pri fyzickej tlačiarni odmerajte
   kalibračnú etiketu a nastavte korekciu X/Y.
5. Prejdite obe témy, štyri jazyky a 100/125/150/200 % DPI na cieľovom monitore.
   V editore aj nových oknách skontrolujte klávesnicu, čitateľnosť a dostupnosť
   dolných tlačidiel cez posúvanie. Otvorte DOCX v cieľovej verzii Wordu.
