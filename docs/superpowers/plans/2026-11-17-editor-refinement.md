# Editor refinement implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** Implement all six user PDF proposals while preserving offline Framework
4.8 and .NET 10, saved projects and the existing print/preflight pipeline.

**Architecture:** Shared TextAutoLayout computes opt-in content boxes. The existing
grid remains document state; inline and inspector edits update it without modal
dialogs. Print sections reuse the existing controls and event handlers.

**Tech stack:** C# 5 compatible Core/WinForms; vendored ZXing; no new dependencies.

**Spec:** ../specs/2026-11-17-editor-refinement-design.md

## Global constraints

- Product informational version remains Pre-release.
- Preserve offline .NET Framework 4.8 and .NET 10 Windows targets.
- Legacy fields keep manual bounds and saved caption sizes.
- No physical printing or GitHub publication for this request.

## Review focus

- Text changes while focused must not reset caret or lose undo/cancel state.
- Multiline, trailing spaces, Unicode, variables and cm units must fit consistently.
- Manual resize and rotated/locked objects must retain predictable behavior.
- Collapsed/hidden print sections must not alter settings or disable calibration locks.
- New defaults must not silently rewrite old XML geometry or caption sizes.

### Task 1: Shared sizing and larger captions
- [x] Add EditorRefinementTests, observe failures for caption defaults/auto sizing.
- [x] Add TextAutoLayout and AutoWidth XML field; wire default/startup, grid and
      batch expansion. Preserve manual dimensions and integrate resize opt-out.
- [x] Verify caption readability, growth/shrink, center, wrapping, XML and variables.

### Task 2: Direct text editing and simplified code inspector
- [x] Add failing inspector/live/Enter/Escape/inline/locked object tests.
- [x] Remove duplicate button; wire inspector live edits and inline overlay.
- [x] Verify focus/caret, undo/redo, cancel restoration and no modal dialog.

### Task 3: Print organization
- [x] Add failing tests for named print sections and all retained controls.
- [x] Group media/profiles, calibration, Zebra and diagnostics; larger bounded window.
- [x] Verify existing profile, fake Zebra, preflight consent and calibration suites.

### Task 4: Final verification and handoff
- [x] Review changes; inspect rendered UI screenshots and address findings.
- [x] Run full standard suites on both runtimes and SDK build both targets.
- [x] Update user docs/package docs and suite counts; local commit, clean status.

Ruling: AutoWidth is opt-in for legacy XML, automatic for new/startup text. Manual width or height resizing opts out; cancelled drags restore the flag via ElementEdit. Apply fits before populating the grid to avoid center drift during cached rotation. Prepared variable values moved to the scrollable Details card so the common summary stays compact. UI text commits are live; autosave never terminates the edit session. Original six review findings and minimum-summary clipping are covered by focused regressions.

Outcome: all 32 suites passed on Framework 4.8 and .NET 10; 87 new editor checks per runtime. Both SDK targets built with zero warnings/errors. Compatible and self-contained packages passed production update integrity checks and extracted offline application self-tests. No physical print or GitHub publication.
