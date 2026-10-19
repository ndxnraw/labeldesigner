# Testing — prelease

From the checkout root:

```powershell
.\scripts\test.cmd
```

This rebuilds the app, compiles and runs all 17 registered suites. Each must exit
successfully and produce `passed.txt`. Failures return a nonzero status. Test binaries
go to `build/tests/`; timestamped reports, screenshots and generated exports go to
`build/test-results/`. These are ignored, not source fixtures.

`tests/regression/` covers UI, rotation, GS1, symbols, startup preferences, rendering
and compatibility. `tests/integration/` covers import and output formats.
`tests/fixtures/` contains committed legacy input documents; the `work` segment
inside fixture paths is historical fixture organization, not an external workspace.
Revision-number class names identify old regression cases, not product releases.

Windows Forms tests open off-screen windows. Run in a Windows desktop session.
Enable Windows' **Microsoft Print to PDF** printer and the Print Spooler service;
the print-preview suite uses its A5 paper capabilities. No physical printing occurs.
The print-preview stress suite opens many windows and may take several minutes.
Do not run competing test suites in the same output directory.

`tests/tools/` contains the original optional benchmark, screenshot and barcode-data
utilities. They are not pass/fail regression suites and are not run by the standard
runner. They can be compiled against `build/app/ND-LabelDesigner.exe` with the same
framework references as `scripts/test.ps1` and supplied an output-directory argument.

Optional independent raster comparisons in `tests/export/` require Python with
Pillow and numpy, Node.js with sharp, and Poppler's `pdftoppm` on PATH. These tools
are not needed for the standard automated export tests (`OutputTests`, `RotationTests`).
See [export instructions](../tests/export/README.txt).

After a fresh clone, run build, test and package, then `git status --short`.
No tracked or untracked output should appear. Hardware printer behavior, visual
review in Word and optional raster comparisons are separate manual checks.
