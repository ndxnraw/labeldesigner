# Build and package — Pre-release

Prerequisites: 64-bit Windows 10/11, Git, Windows PowerShell 5.1 and .NET Framework
4.8/4.8.1 (including `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
Paths containing spaces are supported. A full Visual Studio installation is not required.

From a repository checkout, in PowerShell:

```powershell
.\scripts\build.cmd
.\scripts\package.cmd
```

The application and configuration are written to `build/app/`. Packaging performs
a new build and creates `build/packages/ND-LabelDesigner-Pre-release-Windows.zip`
and `SHA256SUMS.txt`. It verifies every ZIP entry's SHA-256 against its input.
The runtime package includes the application, configuration, user guide and license
notices and feature documentation. Development sources and tests are obtained by cloning GitHub.
The build first compiles vendored ZXing.Net sources to `build/app/zxing.dll`;
then compiles `LabelDesigner.Core.dll` and the Windows application. Both DLLs must
remain beside the compatible EXE. This legacy path needs no SDK or NuGet restore.
the runtime package includes this DLL and Apache license/provenance notices.
There is no download or dependency on a prebuilt DLL. Two upstream obsolete-API
compiler warnings are expected. WPF framework assemblies WindowsBase and
PresentationCore are used by the application and supplied by .NET Framework.

Run `build/app/ND-LabelDesigner.exe` or extract the whole ZIP. Keep the config next
to the EXE, together with `zxing.dll`. No installation, external package download, historical ZIP extraction,
old output, absolute machine-specific path, or `work/` directory is needed.
All build dependencies are Windows framework components or committed source/assets.
Reproducible here means a complete source build from a clean clone; legacy csc/ZIP
timestamps do not promise byte-identical packages across builds.

CI on Windows runs the same build, tests and package validation and uploads Actions
artifacts. It cannot publish a stable release or write repository contents.

## Parallel SDK / modern Windows build

Install the official .NET 10 SDK matching `global.json` (10.0.404, latest patch
roll-forward permitted). Visual Studio is optional. The solution contains shared
Core, the Windows application, source-built ZXing and shared regression projects.
It targets `net48` and `net10.0-windows` from the same source files. Initial SDK
restore downloads Microsoft's Framework reference assemblies from NuGet; application
dependencies remain vendored source/framework libraries. SDK caches and outputs
stay under ignored `build/`. The legacy commands above remain fully offline.

```powershell
.\scripts\build-sdk.ps1
.\scripts\test-sdk.ps1                         # all suites on modern Windows .NET
.\scripts\test-sdk.ps1 -Framework net48        # optional SDK-compiled Framework suite
.\scripts\publish-modern.ps1                  # self-contained Windows x64 ZIP
```

`build-sdk.ps1 -Framework net48` or `-Framework net10.0-windows` selects one target.
SDK binaries are in `build/sdk-bin/<project>/Release/<target>/`; regression binaries
also include their suite name. The modern EXE requires the .NET 10 Desktop runtime
when run from SDK build output. Use the published folder or ZIP for standalone use.

`publish-modern.ps1` produces `build/packages/ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip`
and `SHA256SUMS-NET10.txt`. It bundles the Windows x64 runtime, validates every ZIP
entry against its source and includes Microsoft runtime notices. Trimming and
single-file bundling are disabled to preserve reflection, XML and embedded resources.
Packaging supports Windows PowerShell 5.1 and PowerShell 7; both compression
assemblies are loaded explicitly so ZIP creation also works in a fresh 5.1 process.
The compatible ZIP and its `SHA256SUMS.txt` keep their original names.

Do not mix files between the two builds. Extract either entire ZIP into a separate
directory and launch its `ND-LabelDesigner.exe`. The modern ZIP does not require
installing .NET; the compatible ZIP still requires Framework 4.8/4.8.1. Both require
supported 64-bit Windows 10/11 and the Windows print system for printer features.

Both ZIPs include `package-manifest.xml` with exact payload hashes and the current
Git commit. Packaging generates `UPDATE-INDEX.xml` only when both manifests have
the same source commit. For managed updates publish five assets together: both ZIPs,
both checksum files and the index. See [UPDATES](UPDATES.md) for offline import,
consent, rollback and the initial manual adoption of the update manager.

After testing and committing, refresh package metadata without rebuilding tested
binaries: run `scripts/package.ps1` directly (the `.cmd` wrapper rebuilds), then
`scripts/publish-modern.ps1 -SkipPublish`. Run `scripts/test-update-packages.ps1`
against these final files. Package only binaries built from that committed source;
the source-commit field alone cannot prove a binary matches its source.
