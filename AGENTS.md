# Repository instructions

- Owner: Daniel Návojský / **@ndxnraw**; repository `ndxnraw/LabelDesigner`.
- Before changes, verify GitHub authentication. All GitHub operations and pushes must
  authenticate as **ndxnraw**. Never push using another account.
- Configure Git identity only for this repository: Daniel Návojský and the account's
  verified associated email (preferred `qdann@icloud.com`). Never invent a noreply address.
- Before push inspect Author and Committer with `git log -1 --format=fuller`.
  After push verify GitHub commit `author.login` and `committer.login` are `ndxnraw`.
  Text-only `Daniel Návojský` without actual profile attribution is insufficient.
- The product is **Pre-release**. Do not create numeric product versions or stable releases.
  Keep technical assembly/manifest versions valid; product informational version is Pre-release.
- Canonical production source: `src/LabelDesigner/` (Windows UI) and
  `src/LabelDesigner.Core/` (shared document/rendering/output). Active tests: `tests/regression/`
  and `tests/integration/`; inputs: `tests/fixtures/`; optional tools: `tests/export/`, `tests/tools/`.
- Build: `scripts\build.cmd`. Tests (including build): `scripts\test.cmd`.
  Package: `scripts\package.cmd`. See `docs/BUILD.md` and `docs/TESTING.md` for prerequisites.
- Put assets in `assets/`, documentation in `docs/`, helper scripts in `scripts/`.
  Generated output belongs in ignored `build/`; never commit routine EXE, ZIP, results or caches.
- Preserve Git commit history. The owner consolidated numbered releases and archives into
  `Pre-release` on 2026-11-06. Do not reintroduce numbered product releases.
- Every substantial change must preserve handoff from a clean GitHub clone and update
  relevant documentation. Follow `docs/HANDOFF.md` and verify a clean final git status.
- Preserve functionality when reorganizing. Do not import unrelated local experiments.
  The owner explicitly authorized importing the latest local snapshot on 2026-10-20;
  this code is now canonical. See `docs/LOCAL_SOURCE_IMPORT.md`.
- The .NET 10 SDK builds ZXing from `src/third-party/ZXing/`. Never depend on
  an untracked local DLL. The standard runner contains 32 suites.
- Support .NET 10 for Windows and keep the application usable offline. SDK
  builds use `global.json` and `LabelDesigner.sln`. Publish one self-contained
  Windows x64 ZIP with its checksum and single-offer update index.
