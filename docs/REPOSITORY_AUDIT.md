# Repository cleanup audit — 2026-10-19

Baseline: GitHub main `0c4130242fcc57570e091f69bb361ddd48d482b9` (162 tracked files).
The old local workspace had unrelated uncommitted development work and was not
overwritten or imported. This cleanup uses a separate clone of canonical main.

## Original layout and moves

Root mixed application binaries/configuration, guide, README and aggregate checksums.
`Zdrojovy-kod/` held 44 source/build/asset files; `Testy/` held 32 test/fixture/tool files.
`Dokumentacia/` had nine historical reports, `Logo/` three assets, `Ukazky/` eleven
examples, `Archive/` 35 historical files, `Releases/` 18 package/evidence files,
and `.github/workflows/` four workflows publishing prebuilt numbered packages.

- Production C#, manifest, translations and configuration → `src/LabelDesigner/`.
- Tests → `tests/regression/`, `tests/integration/`; fixture inputs → `tests/fixtures/`.
- Optional export verifiers → `tests/export/`; captures/benchmarks → `tests/tools/`.
- Build/test entry points → `scripts/`; generated output → ignored `build/`.
- Fonts, application icon, branding and examples → `assets/`.
- Current user guide → `docs/USER_GUIDE.txt`; historical reports → prelease archive.
- Historical numbered directories → unique `archive/prelease/snapshot-<original-tree-id>/`.
  The complete directory mapping is in `archive/README.md`.

## Removed or replaced

- Root generated EXE and duplicate config: recreated by build from tracked source/config.
- Root aggregate checksum manifest: obsolete after path changes; packages now generate
  and validate their own SHA-256 manifest.
- Two base64 font duplicates: decoded bytes were verified against the retained TTFs.
  The non-identical base64 license representation is preserved as historical evidence.
- Four numbered publishing workflows: replaced by one read-only Windows validation workflow.
- Old source build entry point: replaced by `scripts/build.cmd` and `scripts/build.ps1`.

References were checked in source, tests, scripts, documentation and workflows.
Historical packages, EXE evidence, PDFs, screenshots, notes and reports are preserved;
they may contain source and evidence unavailable from the current application.
Original binary assets and archived files are copied from Git blobs without re-encoding.
The two original icons differ, so both are retained with explicit roles.

## Version and dependency review

The full-text pattern `\bv?[0-9]+\.[0-9]+(\.[0-9]+)?\b` was reviewed in active
source, tests, scripts, workflows, current docs and assets. Product labels in the
title, footer, About text and translations now use `prelease`; the package and
AssemblyInformationalVersion do too. Remaining numerical strings are:

- .NET/PowerShell prerequisites and compiler directory identifiers;
- internal assembly/file/manifest identity `3.0.8.0`, required to be numerical;
- XML/PDF/SVG and font-license format versions;
- physical dimensions, DPI, scaling, tolerances, formatting strings and test data;
- legacy fixture identifiers/regression descriptions and the original-file inventory;
- preserved historical notes, screenshot pixels and old package/asset names in the archive.

These are not current product release labels. No machine-specific absolute path
or external `work/` dependency remains in active build inputs. Fixture paths that
contain `work` point to committed test data inside this repository.

## GitHub history

Four existing Releases are marked prerelease and described as historical development
snapshots. Their assets and tags were not changed. Preserved tags: `v1.0`, `v2.7.1`,
`v3.0.5`, `v3.0.6`, `v3.0.8`. There was no Release object for `v2.7.1` during audit.
No force-push or history rewrite is needed. New validation CI uploads artifacts and
has no release-publishing permission.

GitHub connector and the credential used for command-line operations both identify
`ndxnraw`. The authenticated profile and existing GitHub commit metadata associate
`qdann@icloud.com` with `ndxnraw` for both author and committer. The credential lacks
access to the private emails endpoint; no unverified claim about that endpoint is made.

## Validation scope

Reorganized build and all 17 standard suites passed on Windows. Packaging validates
all five archive entries byte-for-byte with SHA-256. Fresh GitHub-clone acceptance
is performed after pushing; the completion report records the exact resulting SHA
and post-push author/committer mapping. Optional independent raster tools, physical
printer output and Word rendering are not included in this cleanup's automated checks.
