# Testing — Pre-release

From the checkout root:

```powershell
.\scripts\test.cmd
```

This rebuilds ZXing, shared Core and the app, compiles and runs all 26 registered suites. Each must exit
successfully and produce `passed.txt`. Failures return a nonzero status. Test binaries
go to `build/tests/`; timestamped reports, screenshots and generated exports go to
`build/test-results/`. These are ignored, not source fixtures.

`tests/regression/` covers UI, rotation, GS1, symbols, startup preferences, rendering
and compatibility. `tests/integration/` covers import and output formats.
The additional suites cover CSV/XLSX batch data, workspace dialogs, undo/redo,
recovery, embedded images, QR/Data Matrix decoding and multipage export.
`PrintMediaTests` covers nonblocking media mismatch, independent vector previews,
optional application clipping (preserving device clipping), physical dimensions,
legacy profile migration, multiple media per printer, and real UI profile switching.
It uses Microsoft Print to PDF capabilities and bitmap rendering; no print job is submitted.
`PrintCalibrationTests` covers measured baseline recovery, adaptive crosshair targets,
legacy scale defaults, validation and independent profiles, corrected raster geometry
and hard-margin order, output-approval invalidation, real profile UI integration,
wizard confirmation/invalid measurement behavior, scrolling at 100/125/150/200%
synthetic layout scales in all four languages, diagnostic privacy, invariant numeric
output, bounded rotation and failure-safe logging. Synthetic layout scaling is not a
physical monitor DPI transition. No physical print job is submitted.
`DraftJobTests` covers editable invalid projects, atomic saving and backup rotation,
retention of a readable backup after corruption, failed-save preservation, recovery,
strict output validation, frozen job dates and serial ranges, midnight transitions,
fixed-date offsets, variable template persistence, and print-window recalculation.
Dates are controlled in tests; the computer clock is never changed.
`BackgroundBatchTests` exercises cooperative cancellation of CSV/XLSX and record
preparation, cancellation of PDF/DOCX/SVG/ZPL export with atomic target preservation,
progress delivery, STA execution and UI heartbeat, close and commit races, a 2,000-record
virtual grid and cancelled re-preparation, and bounded diagnostics that exclude exception
messages and paths. Synthetic diagnostic failures use a test-local directory.
`PreflightUiTests` covers shared print/export rules, explicit warning consent,
blocking errors hidden by filters, stale print approval rejected before submission,
source row/page navigation, counts across partially filled sheets and whole-job
repetitions, synchronized layer selection, group locks, protected multi-selection
properties, layer undo, keyboard panel access, export cancellation, and batch gates.
It captures the minimum-size editor, both themes and the review in all four languages.
No print job is submitted; preview/media capabilities use Microsoft Print to PDF.
`PlatformMigrationTests` verifies a core without dependencies on the app or Windows
Forms, old XML and variable-template round trips, frozen job values, Windows-1250
CSV, fresh-process CP850 ZPL import, shared PDF/DOCX/SVG/ZPL outputs with exact page
dimensions, minimum editor size and a contained properties panel. Framework retains
its original physical minimum; modern WinForms preserves the logical minimum across
DPI transitions. The same suite
sources are compiled on both runtimes; `scripts/test-suites.json` is their single list.

Run `scripts/test-sdk.ps1` for the complete modern .NET 10 suite and
`scripts/test-sdk.ps1 -Framework net48` for SDK-compiled Framework tests. Both accept
`-Suites` like the legacy runner and reject unknown names. Results are separated under
`build/sdk-test-results/<target>/<timestamp>/`. SDK tests use a small STA host with the
product's DPI mode and locate fixture/source files independently of binary-directory
depth. A failing suite is never silently skipped. No test requires a physical print job.
`tests/fixtures/` contains committed legacy input documents; the `work` segment
inside fixture paths is historical fixture organization, not an external workspace.
Revision-number class names identify old regression cases, not product releases.

Windows Forms tests open off-screen windows. Run in a Windows desktop session.
Enable Windows' **Microsoft Print to PDF** printer and the Print Spooler service;
the print-preview suite uses its A5 paper capabilities. No physical printing occurs.
The print-preview stress suite opens many windows and may take several minutes.
Do not run competing test suites in the same output directory.

`tests/tools/` contains the original optional benchmark, screenshot and barcode-data
utilities. They are not pass/fail regression suites and are not run by the standard
runner. They can be compiled against `build/app/ND-LabelDesigner.exe` with the same
framework references as `scripts/test.ps1` and supplied an output-directory argument.

Optional independent raster comparisons in `tests/export/` require Python with
Pillow and numpy, Node.js with sharp, and Poppler's `pdftoppm` on PATH. These tools
are not needed for the standard automated export tests (`OutputTests`, `RotationTests`).
See [export instructions](../tests/export/README.txt).

After a fresh clone, run build, test and package, then `git status --short`.
No tracked or untracked output should appear. Hardware printer behavior, visual
review in Word and optional raster comparisons are separate manual checks.
