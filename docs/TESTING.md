# Testing — Pre-release

From the checkout root:

```powershell
.\scripts\test.cmd
```

This rebuilds ZXing, shared Core and the app, compiles and runs all 32 registered suites. Each must exit
successfully and produce `passed.txt`. Failures return a nonzero status. Test binaries
go to `build/tests/`; timestamped reports, screenshots and generated exports go to
`build/test-results/`. These are ignored, not source fixtures.

`tests/regression/` covers UI, rotation, GS1, symbols, startup preferences, rendering
and compatibility. `tests/integration/` covers import and output formats.
`BatchImportTests` checks physical CSV line numbers across quoted line breaks,
multiple findings for one record, opt-in duplicates and migration of existing
mappings. `BackgroundBatchTests` opens the issues list, navigates to the
corresponding partial preview and verifies that excluding the erroneous
record enables printing the valid selection.
Batch preset tests reorder columns containing valid codes, check duplicate
and missing headers, preserve quantity and uniqueness mappings, round-trip
saved presets and verify that deleting an element remaps surviving bindings.
The batch dialog test saves multiple presets, checks actual sample values and
verifies positional confirmation, cancellation and invalidation.
Batch comparison tests cover reordered headers and records, exact identifiers,
missing and duplicate keys, sparse sheets, conflicting columns, numeric
quantities and cancellation. `BackgroundBatchTests` compares a prepared CSV
with a reference CSV using a different delimiter and an XLSX workbook with
multiple sheets. It verifies old and new values, navigation to the current
physical CSV line and retention of the current batch and print selection.
The same suite exercises bulk and individual print selection from comparison,
live totals from current quantities, cancellation and output planning after
confirmation. It also verifies that erroneous rows and a selection over the
output limit prevent confirmation.
Saved-reference tests verify projection to mapped columns, project round trips,
mapping and identifier compatibility, preview, comparison, and confirmed or
declined replacement and removal. The ZPL round-trip test preserves preset
mapping while keeping reference values in the project.
The portable workspace tests verify ZIP inventory, offline transfer of a
template with a saved batch preset, multiple media profiles and calibration,
printer reassignment, default conflict handling, checkpoint backup, stale
preview detection and rollback when a later write fails.
`BarcodeZebraTests` checks exact queue matching for concurrent jobs, missing
and inaccessible queues, preservation of the last observation, the timeout
for a stalled provider, and status refresh in the actual history dialog.
The same suite checks recovery of one archived copy, preservation of positions
for individual labels, empty selection, preview and history navigation to the
source attempt. A test transport captures the submitted copy count.
Manual print result checks cover per-row totals across copies, metadata-only
attempts, old archives, concurrent edits, bounds, cleanup and a recovery
proposal through the actual history and print preview.
`EditorRefinementTests` covers the six user PDF proposals: opt-in text dimensions,
legacy geometry and captions, batch expansion, focused live inspector edits,
actual Enter/Escape command routing, grouped undo, cancellation/clean state,
autosave without losing inline focus, resize cancellation and all four print
sections. Screenshots cover the styled print window and minimum window size.
The menu tests check that child items inherit readable colors and the same
renderer as the parent menu. `BarcodeZebraTests` uses a fake sender to check
logging only after approval, optional content retention, persisted printer
settings, range selection with unchanged sheet positions and fresh confirmation
before repeating a saved job.
It also checks the balance across linked partial reprints, corrected and repeated
confirmations, unconfirmed reprint warning cancellation, stale proposals, and
manual reprints from an intact archive with a damaged result file.
Batch closure checks cover pending reprint acknowledgment, the required reason
when labels remain, stale approval, reopening, retained snapshots and CSV
export after the source history entry is removed. The dialog tests exercise
closing, viewing a saved closure and confirming reopening.
It also tests deliberate numeric wheel editing through both managed events and
native child-window messages: hover and keyboard focus, click activation, focus
loss, readonly values, bounds, spinner buttons and enclosing-panel scrolling.
Translated tab captions are checked at 100%, 150% and 200% font sizes; the
standalone review button is absent while `PreflightUiTests` verifies approval.
The same suite covers native wheel routing for list-only and editable dropdowns,
focus-only versus click activation, focus loss, keyboard selection, open lists,
parent scrolling, expanded item lists and current-value tooltips. Combo rows must
grow and shrink with fonts. Print captions, checkbox wrapping, inline explanations
and both footer actions are measured in all four languages at 100/150/200% fonts
using the actual 980x680 outer minimum window size. This synthetic font-size test
does not represent a physical monitor DPI transition. Accessible setting descriptions
must have matching inline text and all four translations.
PDF workspace checks also cover grouped icon menus, removed toolbar actions,
contextual/locked centering, repeated panel hide/show without horizontal overflow,
remembered logical widths across restart and legacy four-line layout files.
Guided GS1 tests cover working-copy cancel, ordering/data, duplicate prevention,
date day 00, invalid dates/checksums, catalogue search, rebuild theming and minimum
viewport/footer containment at 100/150/200% fonts in all four languages.
`Revision22Tests` retains its payload/FNC1/checksum/XML/ZPL/PRN/rendering assertions
and exercises editing/order/cancel through the guided fields instead of the old grid.
`BarcodeCatalogTests` adds 281 checks of the expanded offline catalogue: actual
decoded content and GS1 FNC1 metadata, percent/field separators, capacity boundaries,
checksums, invalid data, Unicode 2D content, PDF417 geometry/preflight, XML and
clipboard round trips, SVG and real modal chooser actions in four languages.
Plessey uses a known zero-data/CRC vector because the vendored reader lacks this
format. See [BARCODE_CATALOG](BARCODE_CATALOG.md) for limits and local ZXing changes.
The same suite verifies offline print-raster decoding for EAN-13, ITF-14,
GS1-128, QR and Data Matrix, including calibrated geometry, clipping,
quiet-zone obstruction, batch record and sheet positions, scan limits and
cancellation. `BarcodeZebraTests` selects a finding and verifies the highlight
on the correct preview page. See [BARCODE_READABILITY](BARCODE_READABILITY.md).
`BarcodeZebraTests` covers larger new captions with legacy XML defaults preserved,
GTIN-14 GS1-128 decoding/FNC1/AI (01) separately from old ITF-14 bearer bars,
bounded ZPL rendering and unit resets, invariant/default/zero darkness, partial
writes, cancellation/failure disposal, whole-job repetitions, profile persistence,
UI approval invalidation, correct early printer selection and locked calibration
settings. Native RAW printer APIs are not invoked: both the transport and UI send
are replaced with test doubles. DPI/darkness diagnostics contain no label content.
Physical darkness/readability still require a user-controlled trial on the Zebra.
`AccessibilityUiTests` covers wrapped accessible tabs, arrow/Home/End and section
navigation, localized field names, focus repaint, safe tiny bounds, scrollable
keyboard-reachable messages, DPI-aware preferences/text dialogs and actionable
errors retaining diagnostic detail. It exercises four languages, both themes and
100/125/150/200% synthetic bounds/font scaling; physical monitor transitions,
screen readers and Windows contrast themes still require manual verification.
See [ACCESSIBILITY](ACCESSIBILITY.md).
`UpdateTests` covers bounded package validation, fake GitHub transport, offline
operation, cancellation, exact inventories, settings backup, activation/rollback,
failed-launch recovery and explicit consent, including unsaved-document cancellation
in the real editor. No production update is activated. After packaging both variants,
run `scripts/test-update-packages.ps1` to validate the actual ZIPs/index through the
production preparer and run both extracted programs' self-tests. Reports are under
`build/update-package-results/`; modern self-tests disable installed-runtime lookup.
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
`PrintPreviewTests` covers bounded session artwork/geometry reuse, five-second
expiry, printer refresh and opaque driver-property invalidation, independent image
ownership, exact cached/uncached raster equivalence, plan recalculation and mutable
external plans, allocation-failure fallback, UI heartbeat under a blocked driver,
stale delivery and closing during driver work. Actual-submit checks bypass cached
driver facts; a changed hard margin invalidates consent before any print job.
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

## Preview performance comparison

`scripts/measure-preview.ps1 -Runtime compatible -Results build/preview-compatible`
and `-Runtime modern -Results build/preview-modern` run the optional benchmark
sequentially against Microsoft Print to PDF; no jobs are submitted. The modern
command requires the SDK. `-Executable` can measure an archived benchmark build
without rebuilding it. Reports include stage timings, UI tick gaps and whole-process
resource observations. Use the same benchmark source, machine and driver for both
sides; do not run other suites concurrently. See [PREVIEW_PERFORMANCE](PREVIEW_PERFORMANCE.md)
for the baseline instrumentation and the measured seventh-package comparison.

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
