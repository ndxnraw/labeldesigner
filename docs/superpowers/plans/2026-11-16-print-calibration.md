# Print Calibration Implementation Plan

> Execute inline with superpowers:executing-plans; finish with independent review.

**Goal:** Measured per-media print calibration and useful privacy-preserving diagnostics.
**Architecture:** Shared calibration math/target in Core; printer profile, driver
rendering, wizard and diagnostic capture in the Windows UI. Existing print review
and STA worker remain the only route to spool submission.
**Tech Stack:** C#, Framework 4.8 offline csc, SDK .NET 10 Windows, WinForms/GDI+.
**Spec:** `docs/superpowers/specs/2026-11-16-print-calibration-design.md`

## Global constraints

- Product Pre-release; keep both targets and source-built ZXing.
- Scale 0.8–1.2 and offsets ±100 mm; old profiles default to unit scale.
- Corrections affect physical printing/preview only, no automatic fit-to-paper.
- No document values, printer/profile names or paths in diagnostic exports/logs.
- No physical printing or GitHub publication without explicit user instruction.

## Review focus

- Repeated calibration must replace the baseline, not compound old corrections.
- Scale and hard margins must not change translation units or device clipping.
- Media/printer switching must restore independent complete corrections.
- Changing only scale must invalidate output approval and recompute preview.
- Small windows/high DPI and long translations must allow access to every action.

### Task 1: Calibration model, persistence and rendered geometry

Files: new `src/LabelDesigner.Core/PrintCalibration.cs`, modify
`src/LabelDesigner/PrinterProfiles.cs`, `src/LabelDesigner/Printing.cs`, new
`tests/integration/PrintCalibrationTests.cs`, `scripts/test-suites.json`.

- [x] Add failing tests: legacy unit defaults, validated scale/offsets, measured
  recovery (anchor 5, span 50, measured span 51, origin 7.1 gives s=50/51,
  x=5-s*7.1), adaptive target and unchanged document/export geometry.
- [x] Implement `PrintCalibration` (X/Y, ScaleX/ScaleY, validation, transform and
  `FromMeasurements`) and `CalibrationTarget.Create(width,height)`.
- [x] Add profile scale defaults/persistence. Apply transform consistently in
  LabelBounds, render, printable-area and media checks; include scales in review.
- [x] Run new and existing print/preflight tests; prove raster scale/translation
  under hard margins, optional driver clip and native clip preservation.

### Task 2: Wizard and complete profile integration

Files: new `src/LabelDesigner/PrintCalibrationUi.cs`, modify
`PrinterProfiles.cs`, `PrintPanel.cs`, `translations.tsv`, shared new suite.

- [x] Add failing UI tests for scale persistence/switching, correction reset on
  another printer, repeat-calibration baseline, no apply without measurement
  confirmation, invalid measurements with no mutations and scaled layout.
- [x] Add direct scale controls and pass scales through STA request/results.
- [x] Implement a scrollable wizard with uncorrected target review, measurements,
  explicit confirmation and atomic profile save before updating active controls.
- [x] Translate all copy to sk/en/cs/hu; test keyboard names, scroll access and
  minimum layout at four scale factors/languages.

### Task 3: Driver facts and bounded diagnostics

Files: new `src/LabelDesigner/PrintDiagnostics.cs`, modify `Diagnostics.cs`,
`PrintPanel.cs`, `translations.tsv`, shared new suite.

- [x] Add failing tests for configured-vs-actual wording, corrected dimensions,
  numeric diagnostic capture, invariant output, excluded private values, bounded
  rotation and failure-safe logging. Test print context/approval invalidation.
- [x] Capture a numeric snapshot on the worker; show/export it from print UI.
- [x] Extend bounded logger with typed print context and allowed status values;
  log actual submission/cancellation/review result without claiming paper output.
- [x] Run focused tests on both runtimes, then whole standard suites and dual build.

### Task 4: Handoff and verified packages

- [x] Update FEATURES, USER_GUIDE, TESTING, HANDOFF and suite count instructions.
- [x] Package both versions; validate contents and execute packaged self-tests.
- [x] Independent review; resolve substantial findings and run affected checks.
- [x] Commit locally with correct owner attribution and verify clean status.

## Execution ledger

- Auth verified as ndxnraw before changes. Existing linked worktree and clean
  branch codex/platform-migration retained; baseline is published 313f3d2.
- Ruling: Continue implements the approved proposed scope inline; no redundant
  plan approval, physical job submission or publication is requested.
- Calibration model and legacy XML tests observed failing before implementation,
  then passing with raster geometry and preflight invalidation tests.
- UI and diagnostics checks observed failing before their implementations; focused
  Framework and .NET 10 suites passed, including measurement confirmation, privacy,
  rotation and synthetic wizard layout in four languages.
- Independent review found target DEVMODE loss and editable target setup. Added
  a regression that failed on lost resolution/source, then cloned active printer/page
  settings and locked target setup; driver fitting must be disabled in the parent.
- SDK restore/build helper processes require elevated execution in this environment;
  same sources build cleanly with 0 warnings/errors once access is granted.

- Final verification: Framework 26/26 at build/test-results/20261116-065259;
  .NET 10 26/26 at build/sdk-test-results/net10.0-windows/20261116-065212.
  The new suite passed 122 checks on each runtime. Both ZIP manifests, checksum
  files and packaged application self-tests passed in Windows PowerShell 5.1.
  All three packaged managed binaries match the compatible test copies and
  every modern suite's copies. No physical job submitted; real printer/monitor
  checks remain manual. Local commit retained for subsequent publication.
