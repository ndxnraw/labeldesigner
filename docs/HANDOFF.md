# Continue on another Windows computer

Project: **ND LabelDesigner — Pre-release**. Owner: **Daniel Návojský / @ndxnraw**.
Canonical repository: https://github.com/ndxnraw/LabelDesigner, branch `main`.

## Clone and validate

Install Git and the .NET 10 SDK specified in `global.json` on supported
Windows x64. For the complete UI and printing tests, use a desktop session
with Microsoft Print to PDF and Print Spooler enabled.

```powershell
git clone --branch main https://github.com/ndxnraw/LabelDesigner.git
cd LabelDesigner
.\scripts\build.cmd
.\scripts\test.cmd
.\scripts\package.cmd
.\scripts\test-update-packages.ps1
git status --short
```

Expect 32 successful suites in `build/sdk-test-results/net10.0-windows/`
and one self-contained Windows x64 ZIP, `SHA256SUMS-NET10.txt` and
`UPDATE-INDEX.xml` in `build/packages/`. The extracted application must
pass its self-test and Git status must be clean.

Publish these three assets together and verify the full SHA of the release
and tag using `scripts/test-published-update.ps1`. See [BUILD](BUILD.md) for
restore prerequisites and [UPDATES](UPDATES.md) for the initial manual transition
and offline import.

Production sources are `src/LabelDesigner/` and `src/LabelDesigner.Core/`; tests are under `tests/`.
Use a `codex/<description>` branch for ordinary new work unless instructed otherwise.
Read root AGENTS.md and [DEVELOPMENT](DEVELOPMENT.md) before editing.
Update relevant documentation and preserve clean-clone validation with major changes.
Do not commit `build/` outputs, generated EXE/ZIP files, test results, IDE metadata,
logs, credentials, local working directories or caches.

## GitHub authentication and attribution

All GitHub writes and pushes must authenticate as **ndxnraw**. If GitHub CLI is
installed, check `gh auth status` and `gh api user --jq .login` before changing anything.
For Git credential-manager authentication, independently query the authenticated
GitHub user with that credential without printing or saving the token. A connector's
identity alone does not prove the command-line push identity.

Use repository-local settings, never an unnecessary global identity change:

```powershell
git config user.name "Daniel Návojský"
git config user.email "qdann@icloud.com"
```

Before committing, confirm this email belongs to the account and is verified in
GitHub Settings > Emails (or the authenticated emails API). GitHub profile data and
existing commits already associate this address with ndxnraw. If the mapping changes,
use only the official noreply address displayed by GitHub Settings; never invent it.

Before push, inspect `git log -1 --format=fuller` for both Author and Committer.
After push, query `/repos/ndxnraw/LabelDesigner/commits/<sha>` via GitHub API and
confirm both `author.login` and `committer.login` equal `ndxnraw`. Plain author text
is insufficient. Do not push as Codex, Actions, root or another account. If main is
protected, open a PR as ndxnraw and complete acceptance after it reaches main.

## Status and limitations

The only current product version is `Pre-release`. Internal numeric assembly/manifest
metadata remains valid for .NET/Windows; it is not a stable release designation.
The published build and release tag are named `Pre-release`. Earlier source snapshots remain in Git history.

The current Pre-release includes the owner's latest local development snapshot,
imported on 2026-10-20; see `docs/LOCAL_SOURCE_IMPORT.md`. All its production sources,
32 suites and vendored ZXing sources are in Git. The build recompiles ZXing offline;
no original local DLL is required. Keep the generated `zxing.dll` next to the EXE.
Foreign ZPL support is partial; native printer fonts are approximated. QR, Data Matrix, Aztec, PDF417 and GS1 QR/Data Matrix are supported; see
[BARCODE_CATALOG](BARCODE_CATALOG.md). `.nlbl` is not supported. Physical printer and Word rendering need manual checks.
Optional raster comparison dependencies are described in [TESTING](TESTING.md).
Builds are reproducible from source, but timestamped outputs are not byte-identical.
