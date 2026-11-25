# Riadené aktualizácie — Pre-release

Nástroje → Kontrola a údržba → Aktualizácie ponúka kontrolu a prípravu nového balíka. Pri štarte,
časovačom ani na pozadí editora sa internet nekontroluje. Verejný repozitár
`ndxnraw/labeldesigner` nevyžaduje účet ani token. Bez internetu zostáva editor,
tlač, export aj spustenie už pripravenej verzie použiteľné.

## Prvý prechod na výhradne .NET 10

Stiahnite si Windows x64 ZIP z Pre-release ručne, rozbaľte ho do nového
priečinka a spustite `ND-LabelDesigner.exe`. Starší aktualizátor vyžaduje
dve položky v indexe, preto je ručný krok potrebný aj pri prechode z jeho
verzie .NET 10. Nasledujúce aktualizácie už používa nový aktualizátor.

Projekty zostávajú v pôvodných priečinkoch. Aplikácia naďalej načítava
nastavenia z `%LOCALAPPDATA%\NDLabelMaker\startup.xml` a šablóny a profily
tlače z existujúceho `startup.xml.workspace`.

## Použitie

1. Skontrolovať aktualizácie načíta údaje jediného vydania Pre-release.
   Názov produktu zostáva rovnaký; nové zostavenie sa rozlišuje podľa obsahu balíka.
2. Stiahnuť a pripraviť overí ZIP a všetky jeho súbory v samostatnom priečinku.
   Pôvodný priečinok aplikácie sa neprepisuje. Prípravu možno zrušiť.
3. Spustiť pripravenú verziu vyžaduje výslovné potvrdenie. Neuložený dokument
   možno uložiť, zahodiť alebo celý prechod zrušiť. Zrušenie ponechá editor otvorený.
4. Pred aktiváciou sa zálohujú nastavenia spustenia/rozloženia a pracovný priečinok
   s profilmi, kalibráciami a šablónami. Aktívne obnovovacie zámky sa nekopírujú.
5. Ďalšie spustenie z už autorizovaného priečinka otvorí vybranú overenú verziu.
   Toto presmerovanie používa len lokálnu históriu .NET 10 v priečinku
   `Updates/net10-x64`.
6. Návrat na predchádzajúcu verziu vyžaduje potvrdenie a overenie pôvodných súborov.
   Súčasné nastavenia sa automaticky neprepisujú starou zálohou.

Pri probléme s novou verziou podržte **Shift počas spustenia pôvodného EXE** alebo
ho spustite s `--no-update-redirect`. Potom použite návrat v ponuke Aktualizácie.
Pôvodné súbory zostávajú na mieste. Zmenená/poškodená predchádzajúca inštalácia
sa nespustí cez správcu. Vždy možno ručne rozbaliť dôveryhodný ZIP do nového priečinka.

Pripravené verzie a história sú v LocalAppData/NDLabelMaker/Updates, podľa runtime.
Zálohy sú v `settings-backups`. Staršie priečinky možno po overení aktívnej verzie
odstrániť ručne; správca neodstraňuje pôvodné inštalácie ani projekty.

## Bez internetu

Použiť ručne stiahnutý ZIP vyžaduje príslušný SHA256SUMS súbor a potvrdenie
dôveryhodného pôvodu. Balík používa `SHA256SUMS-NET10.txt`.
Súčet potvrdzuje integritu; nejde o nezávislý digitálny
podpis vydavateľa. Balíky získavajte z vlastného dôveryhodného zdroja.

## Protokol a integrita

- Produktové označenie je Pre-release. Interný formát metadát je 1.
- Každý ZIP obsahuje package-manifest.xml s commitom, runtime rodinou a presným
  zoznamom veľkostí/SHA256 súborov. PackageId je SHA256 kanonického obsahu tohto
  zoznamu, produktu, rodiny a commitu. Manifest sám nie je súčasťou vlastného hashu.
- `UPDATE-INDEX.xml` obsahuje jedinú ponuku pre `net10-x64` s PackageId,
  názvom ZIPu, veľkosťou a SHA-256. Commit pochádza z manifestu balíka.
- Online kontrola zafixuje ID aktív a SHA256 z GitHub API. Overí index, zhodu
  commitu/rodiny a celý ZIP; nahradenie premenlivého vydania počas sťahovania
  nesmie podsunúť iný balík. Autoritatívny je explicitne vybraný verejný repozitár.
- Sieť používa HTTPS a kontrolované presmerovania len na GitHub a jeho domény
  pre aktíva. Nepridávajú sa cookies, tokeny ani predvolené prihlasovacie údaje.
- Rozbaľovanie odmieta cesty mimo cieľa, ADS, rezervované názvy, kolízie veľkosti
  písmen/súborov/priečinkov, odkazy/reparse záznamy a nedeklarované súbory.
  Limity: ZIP 256 MiB, rozbalené súbory 512 MiB spolu / 128 MiB jednotlivo,
  najviac 512 položiek plus manifest. V spravovaných inštaláciách sa kontroluje
  aj úplnosť inventára. Pôvodný priečinok môže navyše obsahovať bežné dátové súbory.
- Aktivácia používa zámok a atomický zápis histórie. Neúspešné spustenie obnoví
  primárnu aj záložnú históriu. Autorizované korene sa ohraničujú na 64; aktuálny
  a predchádzajúci sa zachovajú. Súbory sa pri ohraničení histórie neodstraňujú.
- Nepoužíva sa zvýšenie práv, násilné ukončenie procesov ani prepis zamknutého EXE.

Použité API: [GitHub release assets](https://docs.github.com/en/rest/releases/assets?apiVersion=2022-11-28)
uvádza SHA256 a stiahnutie podľa ID aktíva. Automatické presmerovania HTTP sú
vypnuté; ciele sa kontrolujú samostatne podľa [správania HttpClientHandler](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.allowautoredirect?view=net-10.0).

## Overenie a distribúcia

UpdateTests simuluje sieť bez živých zmien vydania a nespúšťa testovacie EXE.
Overuje offline stav, presmerovania, časový limit, zrušenie/truncation, súčty,
škodlivé cesty, integritu, aktiváciu, návrat, obnovu histórie, pôvodné dáta
a skutočné zrušenie zavretia editora pri neuloženom dokumente.
`scripts/test-update-packages.ps1` pripraví výsledný ZIP cez produkčný
overovač .NET 10, porovná index a spustí rozbalený program v režime
self-test s vypnutým vyhľadávaním nainštalovaného runtime.

Pri publikovaní nahrajte **tri aktíva** z otestovaného commitu. Vydanie
obsahuje ZIP pre .NET 10 Windows x64, `SHA256SUMS-NET10.txt` a
`UPDATE-INDEX.xml`. `SourceCommit` musí zodpovedať publikovanému commitu.

V metadátach GitHub vydania nastavte `target_commitish` na plný 40-znakový
SHA zo `SourceCommit` v indexe. Staršie aktualizátory odmietnu názov vetvy
`main` aj vtedy, keď značka vydania a ZIPy ukazujú na správny commit.
GitHub Actions po zverejnení alebo úprave verejného Pre-release automaticky
porovná metadáta vydania, značku, index a údaje ZIPu. Pri nezhode kontrola
zlyhá v karte **Actions → Pre-release publication check**. Kontrola je dostupná
aj ručne cez `pwsh -File scripts/test-published-update.ps1`.
