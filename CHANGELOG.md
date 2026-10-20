# Pre-release development history

## 2026-10-20 — Synchronize the latest local development snapshot

- Import the owner's latest application and tests into the established repository layout.
- Include undo/redo, recovery, templates, batch data, layout tools, images, variables,
  printer profiles, QR and Data Matrix already implemented in the local snapshot.
- Build ZXing offline from vendored sources; ship its DLL and license in the package.
- Expand standard validation to 20 suites; retain the product designation `prelease`.

## 2026-10-19 — Reproducible development handoff

- Canonical source, tests, assets, scripts and current documentation have explicit homes.
- Build/test/package commands use only clone contents and Windows framework dependencies.
- UI and package names consistently use `prelease`; technical assembly metadata is retained.
- Windows CI builds, tests and validates a package without publishing a stable release.
- Historical snapshots and tags remain identifiable; no Git history was rewritten.

## Earlier development

Historical development notes and snapshots are preserved in [archive](archive/README.md).
Their old numerical identifiers are not official product releases. Git history retains
the original development chronology.
