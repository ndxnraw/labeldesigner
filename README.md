# ND LabelDesigner

Windows aplikácia na návrh a tlač etikiet. Aktuálna verzia: **2.6.1**.

## Spustenie

Spustite `ND-LabelDesigner.exe` vo Windows s .NET Framework 4.8.
Podrobný návod: [Navod.txt](Navod.txt).

## Oprava tlače 2.6.1

Tlačiť najprv otvorí náhľad so zoomom a prepínaním strán. Cez Tlačiareň
a nastavenia vyberte zariadenie, papier, orientáciu a počet kópií.
Po potvrdení skontrolujte obnovený náhľad. Až Tlačiť v náhľade odošle úlohu.
Náhľad a tlač používajú rovnaký dokument a rozmery v milimetroch.

Systémový dialóg WinForms vlastný náhľad neposkytuje. Dodatočné škálovanie
ovládača nie je súčasťou náhľadu; používajte 100 % a jednu stranu na hárok.

## Zostavenie a testy

- `Zdrojovy-kod/build.cmd`: zostavenie pomocou kompilátora .NET Framework.
- `Testy/run-tests.cmd`: regresné a vstavané testy aplikácie.
- Test tlače používa výhradne Microsoft Print to PDF, ak je nainštalovaný.

Overené: zostavenie, regresné testy, grafický náhľad a trojstranové PDF A4.
Fyzická tlač ani špecifické funkcie jednotlivých ovládačov neboli overené.

Created by Daniel Návojský (ndxnraw) using Codex | 2026
