# Gradual Platform Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Deliver compatible Framework and modern .NET builds with a shared tested core.

**Architecture:** Extract existing non-UI classes into LabelDesigner.Core. Keep the
Windows UI and print-driver integration in the app. Compile the same sources for
both targets using standard SDK projects and retain the offline legacy compiler.

**Tech Stack:** C#, Windows Forms, GDI+/WPF geometry, .NET Framework 4.8,
.NET 10 LTS Windows x64, PowerShell, vendored ZXing.

**Spec:** `docs/superpowers/specs/2026-11-12-platform-migration-design.md`

## Global Constraints

- Product informational version `Pre-release`; technical versions `3.1.0.0`.
- Preserve .NET Framework 4.8 and offline legacy build.
- Modern output: .NET 10 LTS Windows x64, self-contained ZIP.
- Preserve all existing project and output formats and the common test sources.
- No GitHub publication in this implementation task.

## Review Focus

- Legacy project XML and ZPL embedded metadata must survive extraction unchanged.
- .NET runtime differences must not break Windows-1250, fonts, printing or DPI.
- Output plans must retain exact physical dimensions and prepared data across targets.
- ZIPs must contain all required dependencies, never mix Framework and modern files.
- Both builds and tests must work from a clean checkout without local untracked inputs.

## Task 1: Characterization and shared Core boundary

Files: `tests/integration/PlatformMigrationTests.cs`, `src/LabelDesigner.Core/*.cs`,
mixed files in `src/LabelDesigner`, `scripts/build.ps1`, `scripts/test.ps1`.

- [x] Run the unchanged 24-suite baseline and record its result.
- [x] Add a migration suite; prove the no-UI core boundary fails before extraction.
- [x] Move existing domain/rendering classes and split mixed files at class boundaries.
- [x] Build Core before the app in the legacy compiler path; copy/reference it in tests.
- [x] Verify migration, import/export, draft and preflight tests; retain exact XML names.

## Task 2: Standard dual-target projects

Files: `LabelDesigner.sln`, `Directory.Build.props`, `global.json`, `NuGet.Config`,
SDK projects for Core, app, vendored ZXing and shared regression executable.

- [x] Add SDK projects for `net48` and `net10.0-windows`; explicit resources/assembly metadata.
- [x] Build both targets and test migration behavior on each.
- [x] Fix only demonstrated runtime differences (encoding, DPI, obsolete APIs).
- [x] Run all shared suites on both targets in separate output/result directories.

## Task 3: Reproducible delivery and documentation

Files: `scripts/*sdk*.ps1`, package/test scripts, `.github/workflows/pre-release.yml`,
`docs/BUILD.md`, `docs/TESTING.md`, `docs/HANDOFF.md`, `docs/FEATURES.md`, user guide.

- [x] Provide SDK build/test/publish commands and preserve existing compatible commands.
- [x] Produce and verify separate compatible and self-contained modern ZIPs.
- [x] Smoke-test the modern published executable independent of SDK runtime discovery.
- [x] Run a whole-branch review, resolve material findings, document manual limits.
- [x] Commit locally, verify a clean working tree and report actual test/package results.
