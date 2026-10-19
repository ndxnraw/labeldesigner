# ND LabelDesigner — prelease

Desktopová aplikácia pre Windows na návrh, export a tlač produktových etikiet.
Podporuje grafické úpravy textov, čiarových kódov a symbolov, import ZPL a export
PDF, SVG, DOCX a ZPL. Projekty ukladá do `.ean.xml` a pracuje lokálne.

- **Owner:** Daniel Návojský / [@ndxnraw](https://github.com/ndxnraw)
- **Repository:** [ndxnraw/LabelDesigner](https://github.com/ndxnraw/LabelDesigner)
- **Project status / Version:** `prelease`
- **Platforma a stack:** 64-bit Windows, C#, Windows Forms, .NET Framework 4.8/4.8.1.

## Požiadavky a rýchly štart

Git for Windows, Windows PowerShell 5.1 a .NET Framework 4.8/4.8.1 s pribaleným C#
kompilátorom. Pre kompletné testy zapnite Microsoft Print to PDF a službu Print Spooler.
Visual Studio, NuGet ani staré ZIP balíky nie sú potrebné.

```powershell
git clone --branch main https://github.com/ndxnraw/LabelDesigner.git
cd LabelDesigner
.\scripts\build.cmd
.\scripts\test.cmd
.\scripts\package.cmd
git status --short
```

Program: `build/app/ND-LabelDesigner.exe`. Balík:
`build/packages/ND-LabelDesigner-prelease-Windows.zip`. Testovacie reporty:
`build/test-results/`. Generované výstupy sa necommitujú; stav Git má zostať čistý.
CI poskytuje balíky ako GitHub Actions artifacts. Projekt nemá stabilné vydanie.

## Štruktúra

| Priečinok | Obsah |
| --- | --- |
| `src/LabelDesigner/` | Aktuálny production source |
| `tests/` | Regresné, integračné a exportné testy, fixtures a pomocné nástroje |
| `assets/` | Ikony, logá, fonty a ukážky |
| `scripts/` | Build, test a package |
| `docs/` | Aktuálna dokumentácia a handoff |
| `build/` | Ignorované generované výstupy |
| `archive/prelease/` | Historické vývojové snapshoty |
| `.github/workflows/` | Windows CI |

## Dokumentácia a handoff

Na novom počítači postupujte podľa [HANDOFF](docs/HANDOFF.md): nainštalujte uvedené
závislosti, naklonujte `main`, zostavte, otestujte a vytvorte balík. Repozitár obsahuje
všetky zdroje a aktíva potrebné na pokračovanie bez pôvodného počítača.

- [Vývoj](docs/DEVELOPMENT.md), [build a balenie](docs/BUILD.md), [testovanie](docs/TESTING.md)
- [Štruktúra projektu](docs/PROJECT_STRUCTURE.md), [návod aplikácie](docs/USER_GUIDE.txt)
- [Pokyny pre agentov](AGENTS.md), [história vývoja](CHANGELOG.md), [archív](archive/README.md)

Jediné aktuálne produktové označenie je `prelease`. Historické tagy a názvy v archíve
sú vývojové identifikátory. Import cudzieho ZPL je čiastočný; QR a `.nlbl` nie sú
podporované. Pozrite [licenčný stav](LICENSE) a licenciu fontu v `assets/fonts/OFL.txt`.