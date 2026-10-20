# Repository instructions

- Owner: Daniel Návojský / **@ndxnraw**; repository `ndxnraw/LabelDesigner`.
- Before changes, verify GitHub authentication. All GitHub operations and pushes must
  authenticate as **ndxnraw**. Never push using another account.
- Configure Git identity only for this repository: Daniel Návojský and the account's
  verified associated email (preferred `qdann@icloud.com`). Never invent a noreply address.
- Before push inspect Author and Committer with `git log -1 --format=fuller`.
  After push verify GitHub commit `author.login` and `committer.login` are `ndxnraw`.
  Text-only `Daniel Návojský` without actual profile attribution is insufficient.
- The product is **prelease**. Do not create numeric product versions or stable releases.
  Keep technical assembly/manifest versions valid; product informational version is prelease.
- Canonical production source: `src/LabelDesigner/`. Active tests: `tests/regression/`
  and `tests/integration/`; inputs: `tests/fixtures/`; optional tools: `tests/export/`, `tests/tools/`.
- Build: `scripts\build.cmd`. Tests (including build): `scripts\test.cmd`.
  Package: `scripts\package.cmd`. See `docs/BUILD.md` and `docs/TESTING.md` for prerequisites.
- Put assets in `assets/`, documentation in `docs/`, helper scripts in `scripts/`.
  Generated output belongs in ignored `build/`; never commit routine EXE, ZIP, results or caches.
- Archive data is historical prelease evidence, never an active build dependency.
  Preserve Git history/tags; do not force-push to rename historical releases.
- Every substantial change must preserve handoff from a clean GitHub clone and update
  relevant documentation. Follow `docs/HANDOFF.md` and verify a clean final git status.
- Preserve functionality when reorganizing. Do not import unrelated local experiments.
  The owner explicitly authorized importing the latest local snapshot on 2026-10-20;
  this code is now canonical. See `docs/LOCAL_SOURCE_IMPORT.md`.
- Build ZXing from `src/third-party/ZXing/` via `scripts/build-zxing.ps1`; never depend
  on an untracked local DLL. The standard runner now contains 20 suites.
