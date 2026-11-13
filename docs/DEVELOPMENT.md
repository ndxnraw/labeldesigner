# Development — Pre-release

Owner: Daniel Návojský / [@ndxnraw](https://github.com/ndxnraw).
The canonical source is `main` in `ndxnraw/LabelDesigner`.

Use 64-bit Windows 10/11, Git, Windows PowerShell 5.1 and .NET Framework
4.8 or 4.8.1. The build uses the framework's bundled C# compiler, Windows Forms,
System.Drawing and System.IO.Compression. No Visual Studio, NuGet restore,
Node.js, Python, downloaded SDK or files outside the clone are required for
the application and standard tests. Source Sans 3 is committed with its license.

Start with [HANDOFF](HANDOFF.md), then run `scripts\test.cmd`.
Edit Windows UI/driver integration in `src/LabelDesigner/` and shared document,
validation, persistence, rendering and export C# in `src/LabelDesigner.Core/`.
Core must not reference Windows Forms or the app; it remains Windows-specific
through GDI+ and WPF geometry. Control localization belongs to `UiLocalization` in
the app; text localization and embedded rendering fonts live in Core.
Add regression coverage in
`tests/regression/` or integration coverage in `tests/integration/` and register
new suites in `scripts/test.ps1`. Optional export analysis lives in `tests/export/`.
Build scripts resolve paths relative to themselves, not a developer's home folder.

Standard SDK projects and `LabelDesigner.sln` target Framework 4.8 and .NET 10 Windows.
Use `scripts/build-sdk.ps1`, `scripts/test-sdk.ps1` and `scripts/publish-modern.ps1`;
see [BUILD](BUILD.md). Preserve the legacy compiler path as a compatible offline
fallback. Mixed files were split along existing class boundaries; public domain
names and XML schema did not change. Do not introduce dependencies from Core to UI.

The product version is always `Pre-release`. AssemblyVersion, AssemblyFileVersion
and Windows manifest identity retain `3.1.0.0` as internal compatibility metadata.
AssemblyInformationalVersion and Windows ProductVersion are `Pre-release`.
Do not bump numeric product versions or rewrite old commits/tags.

The owner identified the local development snapshot
as the latest source, and authorized its import on 2026-10-20. It is now the canonical
Pre-release implementation. See `docs/LOCAL_SOURCE_IMPORT.md` for provenance.
ZXing.Net source is vendored under `src/third-party/ZXing/` and rebuilt on every build.
WindowsBase and PresentationCore are framework dependencies for vector graphics.
Preserve existing functionality, update relevant docs, and verify a fresh clone
after substantial layout/dependency changes. Licensing has not been changed to
an open-source license; see the root LICENSE and font license.
