# Managed Updates Implementation Plan

> Execute inline with executing-plans and independent requesting-code-review.

**Goal:** User-confirmed, verifiable updates and rollback without losing offline use.
**Architecture:** Strict package/index models and verified side-by-side preparation;
bounded HTTPS GitHub transport; local activation state and explicit update manager.
**Tech stack:** C# / WinForms, Framework 4.8 + .NET 10, standard XML/ZIP/HTTP only.
**Spec:** `docs/superpowers/specs/2026-11-16-managed-updates-design.md`

## Constraints and review focus

- No network before a click; public fixed repository, no embedded credentials.
- Pre-release identity uses commit and payload hash, not numeric product versions.
- Existing install/data stay intact; wrong-family packages never activate.
- Check zip paths, collisions/reparse points, bounds and exact manifest inventory.
- Pin release/asset identity across mutable Pre-release; compare all hashes.
- Activation/rollback failures must preserve old state and unsaved documents.

### 1. Package protocol and local preparation
- [x] Add failing shared tests for identity, family, hashes, path/size limits and cancellation.
- [x] Implement UpdatePackages.cs and package/index generation scripts; include
  package-manifest.xml in both ZIPs and UPDATE-INDEX.xml in CI artifacts.

### 2. Explicit online check/download
- [x] Add failing fake-transport tests: public release/index pinning, offline,
  timeout/cancel, unsafe redirects, truncated downloads and digest mismatch.
- [x] Implement UpdateNetwork.cs with bounded standard HTTP/JSON parsing.

### 3. Confirmed activation and rollback
- [x] Test side-by-side promotion, settings backup, state validation, locked current
  files, rollback, missing/corrupt previous package and launch cancellation.
- [x] Implement UpdateActivation.cs and UpdatesUi.cs; local-only startup redirect
  for authorized roots; no forced exit or file replacement.
- [x] Translate sk/en/cs/hu and verify consent gates and failure paths in real UI.

### 4. Handoff
- [x] Independent review; full standard suites on both targets and dual SDK build.
- [x] Update docs/counts; package both tested versions, index and self-tests.
- [x] Commit locally, verify exact package identity and clean final status.

## Ledger

- Auth verified ndxnraw; clean existing isolated codex/platform-migration at 14c9c07.
- Owner clarified repository is public; verified private=false before HTTP design.
- Ruling: verified side-by-side activation replaces in-place binary swapping;
  preserves locked/unknown current files and makes rollback retain the old install.
- No new design approval requested: user explicitly chose and authorized this scope.

- Implemented package/network/activation/UI layers with 70 focused checks on each runtime.
- Independent review found undeclared installation files, failed-launch backup rotation
  and exhausted authorization history; added reproductions and fixed all three.
  Second review found no remaining material issue.
- Added four-language UI checks, unsaved-editor cancellation, locked-file and
  backup-failure checks; no production installation was activated.
- Both SDK targets build with zero warnings/errors; full 28-suite runs and final
  package protocol verification are recorded below after completion.

- Full compatible run: 28/28, build/test-results/20261116-122448.
- Full modern run: 28/28, build/sdk-test-results/net10.0-windows/20261116-122513.
- Both real ZIPs/index passed production preparation and extracted self-tests:
  build/update-package-results/20261116-123035. Modern binaries match all tested
  app/Core/ZXing assemblies. Final wrappers are refreshed after the local commit
  without rebuilding and rechecked; exact hashes/commit are kept in ignored
  build/phase8-validation.json. No publication is part of this task.
