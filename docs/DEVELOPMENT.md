# Development — pre-release

Owner: Daniel Návojský / [@ndxnraw](https://github.com/ndxnraw).
The canonical source is `main` in `ndxnraw/LabelDesigner`.

Use 64-bit Windows 10/11, Git, Windows PowerShell 5.1 and .NET Framework
4.8 or 4.8.1. The build uses the framework's bundled C# compiler, Windows Forms,
System.Drawing and System.IO.Compression. No Visual Studio, NuGet restore,
Node.js, Python, downloaded SDK or files outside the clone are required for
the application and standard tests. Source Sans 3 is committed with its license.

Start with [HANDOFF](HANDOFF.md), then run `scripts\test.cmd`.
Edit production C# in `src/LabelDesigner/`; add regression coverage in
`tests/regression/` or integration coverage in `tests/integration/` and register
new suites in `scripts/test.ps1`. Optional export analysis lives in `tests/export/`.
Build scripts resolve paths relative to themselves, not a developer's home folder.

The product version is always `pre-release`. AssemblyVersion, AssemblyFileVersion
and Windows manifest identity retain `3.1.0.0` as internal compatibility metadata.
AssemblyInformationalVersion and Windows ProductVersion are `pre-release`.
Do not bump numeric product versions or rewrite old commits/tags.

The owner identified the local development snapshot formerly called `3.1.0.test`
as the latest source, and authorized its import on 2026-10-20. It is now the canonical
pre-release implementation. See `docs/LOCAL_SOURCE_IMPORT.md` for provenance.
ZXing.Net source is vendored under `src/third-party/ZXing/` and rebuilt on every build.
WindowsBase and PresentationCore are framework dependencies for vector graphics.
Preserve existing functionality, update relevant docs, and verify a fresh clone
after substantial layout/dependency changes. Licensing has not been changed to
an open-source license; see the root LICENSE and font license.
