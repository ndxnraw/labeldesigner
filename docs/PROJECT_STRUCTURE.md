# Repository structure

| Path | Purpose |
| --- | --- |
| `LabelDesigner.sln`, `Directory.Build.*`, `global.json` | Standard dual-target SDK solution and pinned SDK |
| `src/LabelDesigner/` | Windows Forms UI, print-driver integration, translations, config and manifest |
| `src/LabelDesigner.Core/` | Shared document, persistence, validation, fonts, rendering and output |
| `src/Shared/` | SDK platform metadata shared by build targets |
| `src/third-party/ZXing/` | Vendored offline-buildable barcode library source, checksums and license |
| `tests/regression/` | Active regression suites |
| `tests/integration/` | Import and export integration suites |
| `tests/fixtures/` | Versioned input data, never generated results |
| `tests/export/` | Optional independent raster comparisons |
| `tests/tools/` | Shared test startup/path helpers, benchmark and capture utilities |
| `assets/app.ico` | Actual embedded application icon |
| `assets/branding/` | Original logo sources; distinct historic icon retained |
| `assets/fonts/` | Embedded Source Sans 3 font binaries and license |
| `assets/examples/` | Curated original visual and document examples |
| `scripts/` | Legacy and SDK build/test/package entry points, common suite list |
| `build/` | Ignored generated files; only README is tracked |
| `docs/` | Current development, build, testing, handoff and user guides |
| `.github/workflows/pre-release.yml` | Read-only Windows CI and artifacts |

Earlier source snapshots remain in Git history.
