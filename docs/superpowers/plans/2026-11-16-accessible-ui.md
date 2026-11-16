# Accessible UI Implementation Plan

> Execute inline with executing-plans; one independent review at completion.

**Goal:** Reachable, readable and keyboard-accessible shared UI on both runtimes.
**Architecture:** Improve native WinForms controls and shared dialog construction;
add bounded keyboard navigation and localized error guidance at the UI boundary.
**Tech stack:** C# / WinForms, Framework 4.8 and .NET 10; no new dependency.
**Spec:** docs/superpowers/specs/2026-11-16-accessible-ui-design.md

## Constraints / review focus

- Keep Pre-release, existing files/confirmations, both runtimes and offline behavior.
- Narrow cards and long Hungarian/Czech captions must stay readable at 200% scale.
- Focus/selection must be visible and semantic; no swallowing text-editor keys.
- Long error detail must be reachable without hiding confirmation buttons.
- Repeated resize/font/theme changes must not grow handles or accumulate scaling.
- Tests cover synthetic scaling only; do not claim physical monitor/reader testing.

### 1. Shared controls and labels
- [x] Add AccessibilityUiTests and register suite; demonstrate failures for wrapped
  tab height, keyboard tab selection, focus invalidation and accessible field names.
- [x] Fix UiControls.cs, Localization.cs and label builders. Verify four languages,
  themes, tiny bounds and scaled resize round trips.

### 2. Navigation and dialogs
- [x] Add failing F6/Shift+F6, long-message keyboard/severity and preferences/text
  dialog reachability tests; verify unchanged consent results and edited values.
- [x] Implement section navigation, scrollable action-safe dialogs and localizable
  error guidance; add sk/en/cs/hu text. Run focused existing UI/update suites.

### 3. Handoff and verification
- [x] Independent review of the whole change; fix findings with regression checks.
- [x] Update docs and suite count; build both SDK targets and run full suites on
  compatible Framework and modern .NET 10.
- [x] Package and test both actual ZIPs/index; commit locally, refresh metadata
  without rebuilding, verify exact tested binaries and clean final git status.

## Ledger

- Auth verified as ndxnraw; clean isolated codex/platform-migration at fb6d472.
- User's explicit instruction authorizes the previously recommended scope;
  no additional design confirmation is needed.
- Observed fixed 47px tab header, message body TabStop=false, ignored message icon,
  accessible names not refreshed before localization returns for input controls.
- Ruling: improve shared controls and key dialogs rather than blanket rescaling
  every form; preserve existing native DPI ownership and avoid double scaling.

- Reproduced missing tab roles, unreachable message text and non-DPI preferences;
  focused suite passes after fixes. Added F4 empty-selection regression before fix.
- Initial focus test exposed Windows' hidden-process first ShowWindow behavior;
  tests explicitly hide/show off-screen forms before asserting native focus.
- Independent review found no code defects; strengthened message action bounds
  per review. Visual QA then exposed a footer consuming all text space at 200%.
  Added a failing readable-body-space check and replaced the wrapping flow footer
  with equal-width measured actions. Follow-up review found no actionable issue.
- SDK build of both targets passed with zero warnings/errors. Focused legacy UI,
  preflight, update and native-caption suites passed. Final full runs: 20261116-134543
  (compatible), 20261116-134607 (modern). Evidence is recorded below when complete.

- Full final compatible run: 29/29, build/test-results/20261116-134543.
- Full final modern run: 29/29, build/sdk-test-results/net10.0-windows/20261116-134607.
  AccessibilityUiTests reports 1,016 checks on each runtime. Both SDK targets build
  with zero warnings/errors. Legacy emits only the two existing vendored warnings.
- Actual ZIP/index preparation and extracted self-tests passed for both families
  in build/update-package-results/20261116-135209; modern binaries equal tested IL.
- Final wrappers are refreshed after the local commit without rebuilding; exact
  binary/package/index hashes and final source commit are kept in ignored
  build/phase9-validation.json. No publication or production update activation.
