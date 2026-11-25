# Build and package — Pre-release

Use supported 64-bit Windows, Git, and the .NET 10 SDK selected by
`global.json`. Windows PowerShell 5.1 runs the command wrappers. The underlying
scripts also run in PowerShell 7. A fresh clone supplies the application,
the shared core, the tests and the vendored ZXing sources.

```powershell
.\scripts\build.cmd
.\scripts\test.cmd
.\scripts\package.cmd
.\scripts\test-update-packages.ps1
```

The only target is `net10.0-windows`. Build output is in
`build/sdk-bin/<project>/Release/net10.0-windows/`. The SDK output uses the
installed .NET 10 Desktop runtime. Run the extracted ZIP for standalone use.

Packaging publishes the Windows x64 runtime and application from source.
Microsoft runtime packs must be available during the first publish. A populated
package cache permits builds and packaging offline. The application, including
editing, printing, project and template loading and export, works offline.

The release uses three files in `build/packages/`.

- `ND-LabelDesigner-Pre-release-Windows-NET10-x64.zip`
- `SHA256SUMS-NET10.txt`
- `UPDATE-INDEX.xml`

The ZIP bundles the runtime, application, user guide and license notices.
Packaging validates every ZIP entry, runs the published application with runtime
lookup disabled and writes a manifest with the package ID and the current Git
commit. The index describes exactly one package. The update package test
reads the real ZIP, checksum and index, runs the production verifier and
starts the extracted application. Build and package from the commit intended
for publication. See [Updates](UPDATES.md) for the manual first transition,
offline import and rollback.
