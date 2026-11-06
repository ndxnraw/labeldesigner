# Repository structure

| Path | Purpose |
| --- | --- |
| `src/LabelDesigner/` | Canonical application C#, translations, framework config and manifest |
| `src/third-party/ZXing/` | Vendored offline-buildable barcode library source, checksums and license |
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
| `.github/workflows/pre-release.yml` | Read-only Windows CI and artifacts |

Earlier source snapshots remain in Git history.
