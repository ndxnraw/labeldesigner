# Gradual platform migration — Pre-release

The owner selected the recommended gradual migration with a compatible build on
2026-11-12 and requested implementation and tests. Preserve Windows Forms and the
existing product behavior; this is the fifth modernization package.

## Goals and constraints

- Retain the .NET Framework 4.8 build, including its offline framework-compiler path.
- Add a parallel .NET 10 LTS Windows x64 build from the same production sources.
- Introduce standard SDK projects and a solution. Vendored ZXing remains source-built.
- Extract document, persistence, validation, rendering, job preparation and export
  into a shared Core library with no reference to the application or Windows Forms.
  The core remains Windows-specific because rendering uses GDI+ and WPF geometry.
- Keep forms, control localization, print-driver interaction and progress dialogs
  in the Windows application, using the same Core for both targets.
- Preserve project XML, backups, imports, output dimensions, frozen job data,
  cancellation, paper warning overrides, resources, fonts and localization.
- Keep product informational version `Pre-release` and technical versions `3.1.0.0`.
- Produce separate clearly named portable ZIPs. The compatible ZIP keeps its name;
  the modern ZIP includes its runtime so users do not need a .NET installation.
- Do not push or modify GitHub releases during this implementation request.

## Approaches

1. **Selected: parallel targets and incremental boundaries.** Shared code builds for
   .NET Framework 4.8 and .NET 10, with separate outputs and tests. Preserves a working
   fallback and makes compatibility differences explicit.
2. Replace Framework outright: less build maintenance, but removes the fallback and
   changes deployment in one step. The owner chose to retain compatibility.

## Structure

`src/LabelDesigner.Core` owns the document and rendering/output pipeline;
`src/LabelDesigner` owns Windows interaction. Public domain names and XML names stay
unchanged. Mixed files are split along existing class boundaries rather than rewritten.
Localization text and rendering fonts live in Core; control traversal lives in the UI.
The print document is still in the Windows application and consumes shared output plans.

Legacy scripts build the new Core DLL before the EXE. SDK projects target both
frameworks. Test sources remain shared and are compiled/run for each target in
separate directories. Modern startup explicitly preserves legacy DPI and text/font
behavior. Windows-1250 CSV remains supported. Tests must use a compatible encoding
provider on modern .NET and account for actual runtime differences, not weaken rules.

## Validation and delivery

Add migration coverage for architecture boundaries, legacy XML, barcode output,
fixed-time jobs, Windows-1250 and exported dimensions/pages. Run the existing full
24-suite baseline, then the expanded suite on both targets. Verify clean SDK builds
and legacy builds. Validate each ZIP against its tested inputs and test the modern
self-contained executable with the SDK/runtime hidden from discovery.

Hardware printing and physical multi-monitor DPI remain manual. No redesign of
the editor, new label features or rewrite of the renderer is included.
