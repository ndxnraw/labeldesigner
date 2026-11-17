# Čitateľnejší popis EAN a tlač Zebra — Pre-release

## Veľkosť číslic

Nový dokument používa popis kódu **10 bodov**. Úvodné rozloženie už pri zmenšení
etikety neklesá až na 4 body; používa najmenej **8 bodov**. Ak je na etikete miesto,
rozšíri kód tak, aby sa väčší popis zmestil. Pri príliš úzkej etikete zostáva
upozornenie na orezanie: zväčšite kód alebo médium. Samotné väčšie písmo nedokáže
zabezpečiť čitateľný a správne široký EAN na ľubovoľne malej etikete.

Uložená veľkosť v staršom projekte sa nemení. Na jej zväčšenie vyberte kód a
upravte **Veľkosť popisu (pt)** vo vlastnostiach. Skontrolujte celý popis a upozornenia.

## EAN-14 / GS1-128 a ITF-14

Voľby sú oddelené:

- **ITF-14:** doterajší zápis s ochrannými pásmi. Staršie projekty sa naďalej
  vykresľujú týmto spôsobom; zmenil sa iba zavádzajúci spoločný názov.
- **EAN-14 / GS1-128 (01):** GTIN-14 zapísaný ako GS1-128. Zadajte 13 číslic bez
  kontrolnej číslice alebo celých 14 číslic. Kontrolná číslica sa doplní/overí.
  FNC1 a identifikátor 01 sa vložia automaticky; popis obsahuje `(01)`. Zátvorky
  ani identifikátor nepíšte do vstupného poľa čísla.

GTIN-14 označuje identifikačné číslo, ITF-14 a GS1-128 sú spôsoby jeho zápisu.
Pozri [GS1: nosiče GTIN](https://support.gs1.org/support/solutions/articles/43000734104-which-barcodes-are-used-to-represent-gtins-).
Samostatná voľba nemení všeobecný editor ďalších GS1 identifikátorov pri Code 128.

## Tmavosť tlače Zebra

V tlačovom okne vyberte správnu tlačiareň a médium. V **Rozšírených nastaveniach**:

1. Zapnite **Priama tlač Zebra (ZPL)** iba pre tlačiareň, ktorá rozumie ZPL.
2. Nastavte **Rozlíšenie ZPL tlačiarne (DPI)** podľa skutočnej konfigurácie
   tlačiarne. Rozlíšenie kódu v dokumente zosúlaďte s tlačiarňou; pri rozdiele
   aplikácia upozorní na možné zhoršenie ostrosti.
3. Zapnite **Nastaviť tmavosť (darkness)** a zadajte hodnotu **0–30** po desatinách.
   Bez tejto voľby sa príkaz na zmenu tmavosti neposiela. Nula je platná hodnota.
4. Uložte pomenovaný **profil média**. Iné médium tej istej tlačiarne môže mať
   vlastnú tmavosť, DPI aj kalibračné korekcie.
5. Pred odoslaním potvrďte kontrolu výstupu. Zmena ZPL režimu, DPI alebo tmavosti
   vyžaduje kontrolu aktuálnych hodnôt.

ZPL úloha používa zvolené rozmery média, orientáciu plochy a korekcie aplikácie.
Ostatné vlastnosti grafického ovládača sa pri tejto priamej tlači nepoužijú.
Typ média, režim pásky, rýchlosť a odtrhávanie nastavte podľa potreby na tlačiarni.
Tmavosť je nastavenie zariadenia a môže ovplyvniť aj nasledujúce úlohy, kým ju
znovu nezmení aplikácia, ovládač alebo obsluha. Voľba neposiela samostatnú
nastavovaciu úlohu pred následnou grafickou tlačou; nastavenie ide spolu s etiketou.

Predvolené profily a staršie projekty používajú naďalej bežnú tlač cez Windows.
Export ZPL z editora je samostatný export dokumentu bez tlačového profilu a jeho
tmavosti. Zmena tmavosti nemení pixely etikety v náhľade; mení tepelný parameter
tlačiarne. Doladenie kvality vyžaduje skúšobný výtlačok na konkrétnom médiu.

## Overenie a technické hranice

Čiarové kódy sa kontrolujú dekódovaním a porovnaním GS1 dát. ZPL sa testuje bez
odoslania do zariadenia cez náhradný tlačový prenos: neúplné zápisy, chyby, zrušenie,
opakovania celej úlohy, profily a predtlačové potvrdenie. Fyzická tmavosť, čitateľnosť
na skutočnom médiu a správanie konkrétneho ovládača zostávajú na ručné overenie.

RAW úloha sa najskôr celá pripraví; veľkosť strany je obmedzená na 40 miliónov
pixelov, prenášaná úloha vrátane opakovaní na 256 MiB. Opakovania používajú jednu
pripravenú sadu strán v pôvodnom poradí. Pri chybe sa neukončená vlastná spoolová
úloha preruší; už fyzicky vytlačené strany nemožno odvolať. Úloha nastavuje bodové
jednotky bez prevodu rozlíšenia a vlastný začiatok súradníc. Diagnostika rozlišuje
Windows/ZPL a zaznamenáva nastavené DPI/tmavosť, nie nameraný stav zariadenia.

Zdroje: [Zebra: tmavosť](https://support.zebra.com/article/000016457),
[Zebra ZPL guide: ~SD, ^MD, ^MU](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/zpl-zbi2-pg-en.pdf),
[Microsoft: RAW prenos](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter).
