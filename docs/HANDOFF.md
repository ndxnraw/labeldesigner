# Continue on another Windows computer

Project: **ND LabelDesigner — Pre-release**. Owner: **Daniel Návojský / @ndxnraw**.
Canonical repository: https://github.com/ndxnraw/LabelDesigner, branch `main`.

## Install prerequisites

Install Git for Windows and .NET Framework 4.8/4.8.1 on 64-bit Windows 10/11.
Windows PowerShell 5.1 and the framework C# compiler are used by the scripts.
For the complete UI/printing tests, enable Microsoft Print to PDF and Print Spooler
and use a desktop session. No original computer, local cache, ZIP snapshot, secret,
Node.js, Python or private dependency is needed for the standard workflow.

## Clone and validate

Run these commands in PowerShell from a new empty parent directory:

```powershell
git clone --branch main https://github.com/ndxnraw/LabelDesigner.git
cd LabelDesigner
git switch main
.\scripts\build.cmd
.\scripts\test.cmd
.\scripts\package.cmd
git status --short
```

Stop on any nonzero exit code. Expected outputs: application/config in `build/app/`,
21 successful suites and reports in `build/test-results/`, validated
`build/packages/ND-LabelDesigner-Pre-release-Windows.zip`. `git status --short`
must be empty. Work only from this clone when validating the handoff.

## Continue development

Production source is `src/LabelDesigner/`; tests are under `tests/`.
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
21 suites and vendored ZXing sources are in Git. The build recompiles ZXing offline;
no original local DLL is required. Keep the generated `zxing.dll` next to the EXE.
Foreign ZPL support is partial; native printer fonts are approximated. QR and ordinary
Data Matrix are supported; GS1 DataMatrix and `.nlbl` are not. Physical printer and Word rendering need manual checks.
Optional raster comparison dependencies are described in [TESTING](TESTING.md).
Builds are reproducible from source, but timestamped outputs are not byte-identical.
