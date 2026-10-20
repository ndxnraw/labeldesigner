# Build and package — prelease

Prerequisites: 64-bit Windows 10/11, Git, Windows PowerShell 5.1 and .NET Framework
4.8/4.8.1 (including `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
Paths containing spaces are supported. A full Visual Studio installation is not required.

From a repository checkout, in PowerShell:

```powershell
.\scripts\build.cmd
.\scripts\package.cmd
```

The application and configuration are written to `build/app/`. Packaging performs
a new build and creates `build/packages/ND-LabelDesigner-prelease-Windows.zip`
and `SHA256SUMS.txt`. It verifies every ZIP entry's SHA-256 against its input.
The runtime package includes the application, configuration, user guide and license
notices and feature documentation. Development sources and tests are obtained by cloning GitHub.
The build first compiles vendored ZXing.Net sources to `build/app/zxing.dll`;
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
