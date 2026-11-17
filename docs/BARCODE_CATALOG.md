# Katalóg kódov — Pre-release

V ponuke **Pridať kód…** vyhľadajte typ alebo zvoľte kategóriu EAN/UPC,
1D, 2D či GS1. Zadajte vlastný obsah; **Vložiť príklad** ho na požiadanie
nahradí platným príkladom. Zmena výberu obsah neprepisuje. **Vybrať typ…**
v inšpektore hlavného kódu vyberá jeho lineárny typ; 2D kódy sú samostatné
prvky. Dvojklik otvorí ich editor. Existujúce rozbaľovacie ponuky zostávajú.

Katalóg má 21 možností (vrátane GS1 variantov):

| Rodina | Možnosti |
| --- | --- |
| EAN / UPC | EAN-13, EAN-8, UPC-A, UPC-E |
| Lineárne | Code 128, Code 39, Code 39 Full ASCII, Code 93, ITF (2 z 5), ITF-14, Codabar, MSI, Plessey |
| GS1 lineárne | EAN-14 / GS1-128 (01), GS1-128 s identifikátormi AI |
| 2D | QR, Data Matrix, Aztec, PDF417 |
| GS1 2D | GS1 QR, GS1 Data Matrix |

## Pravidlá údajov

- UPC-E prijíma 7 číslic bez kontrolnej číslice alebo 8 s ňou, systém 0/1.
  Kontrola sa počíta z rozšíreného UPC-A, nie priamo zo skrátených číslic.
- Code 93 dopĺňa kontrolné znaky C a K. Code 39 Full ASCII prijíma tlačiteľné
  ASCII 32–126, najviac 80 symbolov po rozšírení. Napríklad malé písmeno
  zaberá dva symboly. Oba typy prijímajú najviac 80 vstupných znakov.
- Codabar prijíma číslice a `- $ : / . +`. Začiatočná aj koncová značka
  A–D musia byť zadané spolu; bez značiek sa doplní A na oboch koncoch.
- MSI **nepridáva kontrolnú číslicu**. Túto variantu použite iba tam, kde ju
  prijíma skener a nadväzujúci systém. Plessey prijíma 0–9/A–F a dopĺňa CRC.
  Plessey potrebuje veľkú šírku pri danom DPI; nezmenšujte ho pod kontrolovaný
  limit rozlíšenia. Jeho dekódovanie priamo knižnicou aplikácie nie je dostupné.
- Bežné 2D kódy prijímajú text a URL v UTF-8 vrátane diakritiky. PDF417 má
  obdĺžnikový pomer strán a samostatnú šírku aj výšku. Ostatné 2D typy sú štvorcové.
- GS1 2D zadávajte napríklad ako `(01)12345678901231(10)LOT(21)SERIAL`.
  Používajú existujúci podporovaný zoznam AI v editore GS1 (nie celý štandard
  GS1). AI a kontrolné číslice sa overia; FNC1 aj oddeľovače premenlivých polí
  sa vytvoria automaticky. Zátvorky sú vyhradené na AI, nie na obsah poľa.
  GS1-128 si zachováva limit 48 dátových znakov vrátane AI; 2D tento limit nemá.

Generovanie, náhľad, dávkové premenné, uloženie projektov, tlač a exporty
fungujú bez internetu na .NET Framework 4.8 aj .NET 10. Staršie projekty
zachovávajú typy a údaje; nové typy vyžadujú túto alebo novšiu aplikáciu.
Poštové, zdravotnícke, kompozitné a platobné šablóny tento balík nezavádza.
Výber rodín bol inšpirovaný [katalógom TEC-IT](https://barcode.tec-it.com/en/);
aplikácia nepoužíva jeho službu ani obrázky.

## Overenie a knižnica

`BarcodeCatalogTests` kontroluje checksumy, neplatné vstupy, dekódovanie nových
typov, FNC1 metadáta, percentá a oddeľovače GS1, Unicode, geometriu a predtlačovú
kontrolu PDF417, XML/schránku, SVG a vyhľadávanie/aplikovanie/zrušenie dialógu
vo všetkých štyroch jazykoch. Plessey má známu testovaciu sekvenciu s CRC;
nemá dekódovací test. Celý štandardný runner obsahuje 31 sád pre oba runtime.
Skutočnú kvalitu na konkrétnom médiu a čítačke treba overiť vytlačenou etiketou.

Pribalený ZXing `DataMatrixWriter` má lokálnu úpravu vetvy GS1: používa ECC200
ASCII s číselnými dvojicami a FNC1. Kompaktný encoder v krátkych poliach mohol
vynechať návrat do ASCII a vytvoriť nadbytočný dekódovaný obsah. Bežný Data Matrix
naďalej používa pôvodné kompaktné kódovanie; test overuje zhodnú maticu. GS1 QR
zdvojuje percentá iba v alfanumerickom režime podľa interpretácie FNC1.
Licencia a pôvod ZXing zostávajú v `src/third-party/ZXing/`.

Overenie 2026-11-17: všetkých 31 sád prešlo na Framework 4.8 aj .NET 10.
Po poslednom doplnení hraníc kapacity a okrajov PDF417 prešlo všetkých 281
kontrol `BarcodeCatalogTests` znova na oboch runtime. Výsledky sú v ignorovaných
`build/test-results/20261117-090634`, `build/test-results/20261117-091858`,
`build/sdk-test-results/net10.0-windows/20261117-091213` a
`build/sdk-test-results/net10.0-windows/20261117-092038`.
