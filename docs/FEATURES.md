# ND LabelDesigner — Pre-release

Aktuálny Pre-release pre Windows; vychádza z vývojového stavu zo 7. októbra 2026
a obsahuje úpravy tlače, konceptov, dátumových premenných, dávok na pozadí
a zjednoteného ovládania a predvýstupnej kontroly
z 12. novembra 2026, riadených aktualizácií a prístupnejšieho rozhrania
zo 16. novembra 2026 a úprav EAN/Zebra zo 17. novembra 2026. Zachováva Windows Forms,
lokálne písmo Source Sans 3 a staršie projekty `.ean.xml`.

## Platforma

Aplikácia používa .NET 10 a spoločné jadro pre dokumenty, kontrolu,
vykresľovanie a exporty. Samostatný Windows x64 ZIP obsahuje runtime.
Existujúce projekty, šablóny a profily tlače zostávajú použiteľné offline.
Názov produktu je naďalej Pre-release.

## Nové možnosti

- **Formátovanie častí textu** umožňuje použiť tučné písmo a kurzívu v jednom
  poli. Hodnoty premenných preberajú štýl zástupného textu a všetky štýly sa
  spoločne zalamujú aj prispôsobujú pevnej ploche.
- **Uvoľnenie miesta po aktualizáciách** odstráni overené staršie spravované
  zostavy. Aktívna verzia a možnosť návratu zostávajú dostupné.
- **Prispôsobenie textu pevnému poľu** zalomí text a podľa potreby zmenší
  písmo po nastavené minimum. Rovnako funguje pri rýchlej tlači zo šablóny,
  dávkach a exportoch. Ak sa text stále nezmestí, kontrola zobrazí upozornenie.
- **Čiary a obdĺžniky** sa dajú vložiť a upravovať ako samostatné prvky.
  Obdĺžnik má voliteľný čierny obrys a priehľadnú, čiernu alebo bielu
  výplň. Kontrola rozlíši voľný okraj kódu od jeho prekrytia.
- **Presné rozmiestňovanie na plátne** zobrazuje vodiace čiary pri presúvaní
  prvku alebo skupiny, pravítka v mm alebo cm a vzdialenosti od okrajov.
  Prichytávanie sa dá vypnúť v ponuke Panely alebo dočasne klávesom Alt.
- **Stav tlačovej úlohy** sa dá na vyžiadanie načítať z frontu Windows.
- **Ručný výsledok tlače** eviduje použiteľné etikety z pôvodnej tlače aj
  dotlačí a zostávajúce množstvá po zdrojových riadkoch. Z uloženého obsahu
  pripraví návrh podľa zostatku a upozorní na dotlače bez potvrdenia.
- **Uzavretie dávky** uloží výsledok po zdrojových riadkoch, upozorní na
  nepotvrdené dotlače a pri zostatku vyžiada dôvod. Uzávierky možno
  zobraziť a exportovať do CSV aj po prečistení histórie tlače.
- **Prehľad dávok** spája pokusy o tlač, dotlače a uzávierky trvalým ID
  dávky. Potvrdené množstvá ostávajú dostupné aj po prečistení histórie.
- **Rýchle vyplnenie a tlač** zo šablóny otvorí vybrané texty a kódy
  v jednom formulári. Po kontrole údajov a zadaní množstva pripraví tlač
  s rozložením uloženej šablóny.
- **Dotlač prerušenej dávky** pripraví z histórie zvyšné strany jednej kópie
  alebo vybrané etikety v pôvodných pozíciách. Nový pokus odkazuje na zdroj.
  História uchová posledný známy stav aj čas zistenia, vrátane informácie,
  či aktuálna kontrola úlohu ešte našla.

- **Prenosná záloha a obnova** prenesie šablóny s predvoľbami dávok, profily
  médií, potvrdené množstvá a uzávierky cez ZIP. Náhľad umožní vyriešiť
  konflikty pre jednotlivé dávky a priradiť tlačiarne. Pred obnovou vznikne
  lokálna záloha aktuálnych údajov.

- **Predvoľby dávky** ukladajú nastavenia CSV, hárok XLSX a mapovanie v projekte.
  Pri ďalšom importe vyhľadajú stĺpce podľa hlavičiek. Ukážka prvých piatich
  záznamov zobrazí zdroj stĺpca a jeho hodnoty aj pri mapovaní podľa pozície.

- **Kontrola vstupných dát dávky** zobrazí chyby a upozornenia s pôvodným
  riadkom CSV a stĺpcom. Z prehľadu sa dá prejsť na etiketu v náhľade.
  Duplicity sa kontrolujú vo voliteľnom stĺpci s jedinečnými hodnotami.

- **Porovnanie opakovaných dávok** spáruje pripravené riadky s ručne vybraným
  CSV alebo XLSX podľa identifikátora. Zobrazí pridané, odstránené a zmenené
  položky, pôvodné hodnoty a prechod na aktuálnu etiketu. Z pridaných
  a zmenených riadkov sa dá pripraviť výber na tlač s aktuálnymi množstvami.
  Každá predvoľba môže mať uloženú jednu referenčnú dávku. Dá sa zobraziť,
  použiť na porovnanie, nahradiť alebo odstrániť po potvrdení.

- **Čitateľnosť kódov pred tlačou** je v tlačovom náhľade pod **Detaily →
  Čitateľnosť kódov…**. Pre EAN-13, ITF-14, GS1-128, QR a Data Matrix
  kontroluje modul, voľné okraje, orez a skúšobné načítanie pri rozlíšení
  tlačiarne. Pozri [Kontrola čitateľnosti pred tlačou](BARCODE_READABILITY.md).

- **História tlače:** cez **Nástroje → Kontrola a údržba → História tlače** sa zobrazia
  odoslané, zrušené a neúspešné pokusy o tlač. Ak bolo v karte **Detaily**
  zapnuté uloženie obsahu úlohy, z histórie sa dá pripraviť opakovanie celku,
  vybraných strán alebo pôvodných záznamov dávky. Pred tlačou sa otvorí náhľad
  s uloženými nastaveniami a vyžiada sa nové predtlačové potvrdenie. Pozri
  [História tlače](PRINT_HISTORY.md).

- **Pracovný priestor editora:** Projekt, Vložiť, Dávka, Zobrazenie, Nástroje,
  Exportovať a Tlač sú na jednej lište. Vľavo sú karty Prvky, Kontrola, Etiketa
  a Papier. Vlastnosti združujú obsah, typografiu, polohu a rozmery. Tučné písmo
  a kurzívu možno použiť priamo na výber v textovom poli. Mierka pod plátnom mení
  iba jeho zobrazenie. Tabuľku otvoríte v ponuke Zobrazenie a centrovanie je vo
  vlastnostiach hlavného kódu. Bočné panely si uchovávajú šírku.
- **Tlač a export:** náhľad prepína medzi celou stranou a detailom vybranej
  etikety. Súhrn zvýrazní výsledný počet etikiet a strán vrátane opakovaní.
  Kontrola ponúkne prechod na opraviteľný prvok alebo riadok dávky.
  Bezchybný export otvorí priamo uloženie súboru.
- **Dávky a šablóny:** v dávke sú samostatné kroky Zdroj, Mapovanie a Kontrola.
  Pri generovaných záznamoch alebo zhodnej predvoľbe s hlavičkou sa dá dávka
  pripraviť už zo Zdroja. Knižnica má karty Šablóny a Nedávne projekty.
  Vyplniť a tlačiť otvorí rýchlu tlač vybranej šablóny priamo z knižnice.
- **GS1 editor:** pomenované polia, rýchle voľby, vyhľadávanie, formáty a chyby
  pri poli, náhľad, zmena poradia a bezpečné zrušenie.
- **Ochrana ovládania kolieskom:** číselné polia a zatvorené rozbaľovacie ponuky
  vyžadujú kliknutie a zameranie; samotný hover alebo prechod klávesom Tab nemení
  hodnotu. Otvorené zoznamy a klávesnicový výber zostávajú dostupné. Tlačové
  tlačidlá a dlhé popisy sa prispôsobujú písmu a šírke; pri nedostatku miesta je
  obsah posúvateľný. Dôležité korekcie majú preložené vysvetlenia aj prístupné opisy.
- **EAN a Zebra:** väčší predvolený popis nových kódov, samostatný ITF-14 a
  EAN-14 / GS1-128 s `(01)`, voliteľná priama ZPL tlač s DPI a tmavosťou
  uloženými podľa média. Staršie veľkosti a bežná tlač Windows zostávajú zachované.
  Postup a rozsah overenia opisuje pribalený BARCODES_ZEBRA.md.
- **Prístupnejšie rozhranie:** zalamované hlavičky kariet, prístupné názvy a stav
  výberu, viditeľný klávesnicový výber a F6/Shift+F6 medzi oblasťami editora.
  Dlhé hlásenia sú dostupné klávesnicou; nastavenia a úprava textu majú posúvanie
  so samostatnými akciami. Chyby pridávajú ďalší postup a zachovávajú podrobnosti.
  Rozsah a obmedzenia overenia opisuje pribalený ACCESSIBILITY.md.
- **Riadené aktualizácie:** Nástroje → Kontrola a údržba → Aktualizácie, kontrola verejného vydania
  iba na vyžiadanie, správny balík podľa runtime, overenie súčtov a obsahu,
  samostatná príprava a spustenie až po potvrdení. Rozpracovaný dokument možno
  pred prechodom uložiť alebo prechod zrušiť. Nastavenia sa zálohujú; dostupný
  je potvrdený návrat na predchádzajúcu verziu a ručný import ZIPu bez internetu.
  Podrobnosti a obnova spustenia sú v UPDATES.md.
- **Rýchlejší tlačový náhľad:** aktuálny papier a orientácia sa načítajú len raz
  pri príprave nastavenia. Opakované náhľady používajú krátkodobé údaje ovládača
  a obmedzenú pamäť už vykreslených strán. Zmena nastavení, profilov, dokumentu
  alebo prepočítanie údajov zachovajú správnu väzbu na nový výstup.
  Obnoviť tlačiarne vyprázdni pamäť; pred odoslaním tlače sa údaje ovládača aj
  kontrola celého dokumentu overia znova. Časy a metodika sú v PREVIEW_PERFORMANCE.md.
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
  zmena nastavení alebo upozornení vyžiada nové potvrdenie. Papier sa automaticky
  neprispôsobuje; voliteľná kalibrácia média upraví mierku a posun.
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
- **Diagnostika:** Nástroje → Kontrola a údržba → Diagnostika zobrazí posledné lokálne záznamy
  a umožní ich uložiť do textového súboru. Chyby dostanú identifikátor,
  typ výnimky, miesto zlyhania a identifikátor zostavy. Správy výnimiek,
  cesty k súborom, názvy tlačiarní ani obsah dokumentov sa do logu nezapisujú.
  Denník rotuje najviac do troch súborov po približne 1 MB.
- **Výber a rozloženie:** Ctrl/Shift, výberový obdĺžnik, spoločný presun,
  kopírovanie, odstránenie, skupiny, zamknutie, zarovnanie, rozostupy a vrstvy.
- **Samolepiace hárky:** riadky, stĺpce, štyri okraje, medzery a prvá voľná pozícia.
  Spoločné rozloženie strán používajú tlač, náhľad aj dávkové exporty.
- **Profily tlačiarní:** formát papiera, orientácia, korekcia X/Y v mm a samostatná
  mierka X/Y 80–120 %. Sprievodca vytvorí nekorigovanú skúšobnú stranu s tromi
  krížikmi; tlačí sa až po kontrole a stlačení Tlačiť. Z nameraných vzdialeností
  vypočíta a po potvrdení uloží korekciu len pre vybrané médium.
  Opakované meranie nahradí kalibráciu. Staré profily majú mierku 100 %.
  Jedna tlačiareň môže mať viac pomenovaných profilov pre rôzne médiá.
  Profil uchováva aj režim ignorovania tlačiteľnej oblasti ovládača.
- **Nesúlad papiera:** menší formát hlásený ovládačom je upozornenie;
  používateľ môže potvrdiť pokračovanie bez automatického prispôsobenia. Náhľad je nezávislý
  od orezania ovládača a zobrazí celý dokument. Rozmery dokumentu a údaj
  ovládača sa zobrazujú samostatne. V karte Médium možno použiť
  rozmery dokumentu ako vlastné médium alebo ignorovať dodatočné orezanie
  podľa ovládača. Fyzické limity zariadenia a orezanie samotného ovládača
  nemožno týmto režimom odstrániť.
- **Diagnostika tlače:** samostatný výpis uvádza nastavené médium, tlačiteľnú
  oblasť, hardvérové okraje, korekcie a runtime. Skutočne vložené médium je
  výslovne neoverené. Výpis možno uložiť; neobsahuje názvy tlačiarní/profilov,
  obsah etikiet ani cesty. Odoslanie do Windows sa nepovažuje za potvrdený výtlačok.
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
  editovanie dvojklikom a všetky existujúce výstupy. Katalóg dopĺňa Aztec,
  PDF417, GS1 QR a GS1 Data Matrix; pozri [BARCODE_CATALOG](BARCODE_CATALOG.md).

## Ovládanie

Nové funkcie sú v ponuke **Nástroje** na hornej lište. História má dve samostatné
ikony. Dávku pripravte po nastavení mapovania; zmena mapovania vymaže pripravené
výsledky, aby sa omylom nevytlačili staré údaje. Chybné označené riadky blokujú
výstup; možno ich opraviť v zdroji alebo vedome zrušiť ich zahrnutie.

Počet etikiet určuje položky a strany výstupu. Počet kópií v okne tlače opakuje
celú tlačovú úlohu. Priblíženie náhľadu ani kalibračný posun nemenia mierku;
meniť ju môžu iba výslovne nastavené kalibračné faktory média.
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
   skúšobnú stranu sprievodcu, zadajte namerané vzdialenosti a polohu krížika,
   potvrďte meranie a uložte profil. Overte ďalším skúšobným výtlačkom.
5. Prejdite obe témy, štyri jazyky a 100/125/150/200 % DPI na cieľovom monitore.
   V editore aj nových oknách skontrolujte klávesnicu, čitateľnosť a dostupnosť
   dolných tlačidiel cez posúvanie. Otvorte DOCX v cieľovej verzii Wordu.

## Priama úprava textu

Dvojklik upravuje text na plátne, zmeny sa zobrazujú počas písania. Enter potvrdí, Shift+Enter vloží riadok a Esc obnoví pôvodný stav. Nové texty používajú šírku podľa obsahu; staré ručné rozmery sú zachované. Tlač má karty Médium, Kalibrácia, Zebra a Detaily. Podrobnosti: [EDITOR_REFINEMENT](EDITOR_REFINEMENT.md).
