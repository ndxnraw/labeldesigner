# Repository structure

| Path | Purpose |
| --- | --- |
| `src/LabelDesigner/` | Canonical application C#, translations, framework config and manifest |
| `tests/regression/` | Active regression suites |
| `tests/integration/` | Import and export integration suites |
| `tests/fixtures/` | Versioned input data, never generated results |
| `tests/export/` | Optional independent raster comparisons |
| `tests/tools/` | Benchmark and capture utilities |
| `assets/app.ico` | Actual embedded application icon |
| `assets/branding/` | Original logo sources; distinct historic icon retained |
| `assets/fonts/` | Embedded Source Sans 3 font binaries and license |
| `assets/examples/` | Curated original visual and document examples |
| `scripts/` | Build, test and package entry points |
| `build/` | Ignored generated files; only README is tracked |
| `docs/` | Current development, build, testing, handoff and user guides |
| `archive/prelease/` | Historical snapshots; never build inputs |
| `.github/workflows/prelease.yml` | Read-only Windows CI and artifacts |

The original tracked-file inventory is [ORIGINAL_TREE.txt](ORIGINAL_TREE.txt).
Historical directory moves are recorded in [archive/README.md](../archive/README.md).
Old source/test directories have been replaced, not duplicated as production code.
Historical ZIPs are retained because they contain earlier source and development
evidence. Old numerical strings inside archive documents/assets are historical only.
