# Print Preview Performance Implementation Plan

> Execute inline with superpowers:executing-plans and independent final review.

**Goal:** Reduce preview latency with measured, bounded reuse and current print validation.
**Architecture:** Stage metrics and benchmark first; isolate measured optimizations in
Windows print-session helpers, retaining the shared rendering/output core.
**Tech stack:** C#, offline Framework 4.8, SDK .NET 10 Windows, WinForms/GDI+.
**Spec:** `docs/superpowers/specs/2026-11-16-preview-performance-design.md`

## Global constraints

- Product Pre-release; keep both runtimes and source-built ZXing.
- One bounded STA printer worker; no physical print jobs in automated checks.
- Caches must be bounded, invalidated correctly and unable to bypass submit validation.
- No label values, printer names or paths in timing diagnostics.

## Review focus

- Unchanged named paper dimensions can hide changed driver DEVMODE.
- Cached Image/Metafile ownership must survive UI disposal and worker races.
- Recalculated variables/plans must never reuse earlier artwork or checks.
- Preview-page changes must retain full-document preflight warnings.
- Timing comparison must isolate runtimes and document input/environment differences.

### Task 1: Instrument and establish comparable baseline

Files: new `src/LabelDesigner/PreviewPerformance.cs`, instrumentation in
`PrintPanel.cs`, new `tests/tools/PrintPreviewBenchmark.cs` and project,
`scripts/measure-preview.ps1`.
- [x] Record capture/queue/discovery/setup/inspection/render/delivery/apply stages.
- [x] Benchmark published baseline behavior on Framework then .NET 10; retain
  source diff, binaries, scenario timings and driver facts under build/phase7/before.
- [x] Identify measured dominant work and record chosen optimizations in ledger.

### Task 2: Implement measured reuse safely

Files: print session helper(s), `PrintPanel.cs`, `Printing.cs`,
`PrintDiagnostics.cs` as required by measurements; new integration suite.
- [x] Add failing tests for chosen reuse, invalidation, fresh submit validation,
  stale delivery, exact raster equivalence and bounded resource ownership.
- [x] Implement the smallest measured improvements; keep controls/printing semantics.
- [x] Run focused Framework and modern suites and inspect benchmark stage changes.

### Task 3: Complete verification and comparison

- [x] Run full shared suites on both runtimes and dual SDK build.
- [x] Run identical benchmark sequentially, baseline and changed versions; compare
  median/p95 per scenario, stage timings, UI heartbeat and native/memory resources.
- [x] Independent review and fix material findings with regression checks.
- [x] Update docs/suite counts, package both exact tested versions and self-test.
- [x] Save comparison report/proof, commit locally and verify clean status.

## Execution ledger

- Auth verified ndxnraw; clean linked worktree on codex/platform-migration at 7e0b8b2.
- User approved the stated seventh-package approach; implement inline without a
  redundant design approval. No publication or physical printing requested.

- Baseline sequential stage measurements: setup about 2 s on reopen, inspection
  about 0.8 s; native unfrozen PaperSize 193 ms vs frozen 0 ms, PrintableArea
  108 ms, core preflight 7 ms. Preserved both instrumented baseline binaries.
- Ruling: freeze current paper/orientation once; share a five-second, four-key
  numeric geometry snapshot across diagnostics/preflight/preview. Submit always
  reads new facts. Reuse capability enumeration inside a single setup.
- Ruling: cache only internally owned frozen plans (four images / 32 MiB); render
  externally mutable supplied plans fresh. Keep full document checks on every request.
- New reuse test observed failing before implementation, then passed on both
  runtimes; added exact raster, fresh-submit hard-margin, TTL, stale delivery,
  driver-property invalidation, budget and disposal tests.
- Injected clone allocation failure observed failing, then guarded cache operations
  so an optional cache cannot discard a successfully rendered preview.
- Independent review found no material defect; report process resource snapshots
  as whole-process observations including initialization, not leak bounds.

- Final full suites: Framework 27/27 at build/test-results/20261116-080015;
  .NET 10 27/27 at build/sdk-test-results/net10.0-windows/20261116-080041.
  New preview suite passed 50 checks on each runtime. Dual SDK build clean.
- Final sequential comparison contains 132 samples, identical benchmark source
  SHA256 on both sides. First preview 3721→918 ms / 3250→996 ms; unchanged
  preview 671→62 ms / 676→63 ms. UI/global resource caveats recorded in report.
- Both ZIPs include the report; both packaged application self-tests and entry
  manifests passed in Windows PowerShell 5.1. Managed binaries match all test
  copies and the after-benchmark binaries. Baseline source-only patch validated
  against the published original file; it is a reproducibility input fixture.
- Independent final review found no material findings after cache allocation
  fallback and close-race tests. Publication and physical printing remain separate.
