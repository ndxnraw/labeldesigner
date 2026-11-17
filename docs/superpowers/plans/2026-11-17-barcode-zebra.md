# Barcode readability and Zebra output plan

> Execute inline; one independent review at completion.

**Spec:** docs/superpowers/specs/2026-11-17-barcode-zebra-design.md
**Goal:** Three owner-requested corrections with legacy documents preserved.

### 1. Captions and GTIN-14
- [x] Add regression suite and demonstrate failing default/readability and distinct
  GS1-128 GTIN-14 tests, including decoding and old Kind14 bearer output.
- [x] Update Default/StartupLayout and MoreBarcodes/Engine; fit startup caption
  width where possible, retain clipping warnings, add localized hints/docs.

### 2. Zebra job construction and spool lifecycle
- [x] Add failing tests for bounded calibrated bitmap/ZPL, darkness validation,
  invariant tenths, repetitions, partial writes, failure/cancel cleanup; no real jobs.
- [x] Add ZebraPrint.cs with renderer and injectable RAW spooler. Prepare the whole
  bounded job before opening printer; deterministic owned temp-file cleanup.

### 3. Profiles and UI
- [x] Add failing legacy-profile/default/migration, per-medium persistence and UI
  consent invalidation checks. Add ZPL mode, device DPI and optional darkness.
- [x] Integrate background print submission with the existing preflight gate and
  correct profile switching; suppress incompatible calibration-target ZPL controls.
- [x] Four-language labels, documentation and independent review.

### 4. Verification / handoff
- [x] Full suites on both runtimes, SDK builds, tested compatible/modern ZIPs/index.
- [x] Local commit, metadata refresh without rebuilding, exact tested binary proof
  and clean status. Do not publish; do not send a physical print job.

## Ledger

- GitHub auth verified ndxnraw; clean existing isolated worktree at 64136a4.
- Confirmed installed ZDesigner ZT410-203dpi and ZT411-300dpi queues read-only.
- Owner answered that EAN-14 means GS1-128 with (01); no carrier ambiguity remains.
- Ruling: explicit RAW ZPL is needed for reliable app-controlled darkness, rather
  than a separate device command that the subsequent GDI job could overwrite.

- Focused suite: 87 checks on Framework; SDK focused suites also pass on .NET10.
- Fixed early SelectPrinter requested-name loss found by injected RAW UI tests;
  previously a selection before discovery could silently resolve to the default.
- Review found inherited ZPL units/conversion; primary Zebra docs confirmed it.
  Added failing header regression, then ^MUD,300,300 before dimensions. Re-review
  found no remaining actionable issue.
- Full legacy run initially found RotationTests supplying ABC123 to newly added
  numeric GTIN14 kind. Added correct numeric sample; focused rotation passes.
- Final full runs: compatible 20261117-075350; modern 20261117-075102.
  Tests replace both RAW transport and UI send; no printer job was submitted.

- Final complete runs passed 30/30 on both runtimes; BarcodeZebraTests 87 checks
  each. SDK build passed both targets with zero warnings/errors.
- Actual compatible/modern ZIPs and index passed production preparation and
  extracted self-tests: build/update-package-results/20261117-080535. Modern test
  ran with installed-runtime lookup disabled; managed binaries match all tested bins.
- Final wrappers are refreshed after the local commit without rebuilding. Final
  hashes, source commit and report locations are in ignored build/phase10-validation.json.
  No publication and no native printer submission are part of this task.
