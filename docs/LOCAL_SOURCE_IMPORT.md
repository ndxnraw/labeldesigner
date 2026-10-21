# Latest local source import — 2026-10-20

The owner confirmed that the local snapshot historically called `3.1.0.test` is
the newest implementation and requested it on GitHub. The preceding cleanup commit
`6de69c728dfa02b2e25c5727461b2ba488a3b88b` had reorganized the older GitHub source
without importing this local work. This update corrects that omission.

All production C# files, translations, manifest, configuration, application icon,
font inputs and active test sources come from that local snapshot. Changes to these
inputs are limited to product labeling (`pre-release`) and repository paths.
The existing clean layout, CI, package name and attribution requirements remain.
The internal numerical assembly/file/manifest identity is `3.1.0.0`; this is not a
new official product version. The original local workspace is preserved.

The additional functionality is documented in [FEATURES.md](FEATURES.md).
The standard suite contains 20 test programs, including BatchImportTests,
WorkspaceUiTests and Revision310Tests. Historical revision class names identify
regression cases, not marketing versions.

The source snapshot includes ZXing.Net with its Apache license, provenance notice
and source checksums. The upstream commit was not recorded by the original local
development; the committed source bytes and SHA256SUMS.txt identify it instead.
Every application build compiles this source using the Windows framework compiler.
No untracked DLL or files from the old workspace are needed. Generated EXE/DLL/ZIP
files remain ignored. WPF WindowsBase and PresentationCore come from .NET Framework.

Validation uses `scripts/test.cmd` and `scripts/package.cmd`, then repeats from a
fresh GitHub clone after push. Optional physical-printer, real-monitor DPI and Word
checks remain manual, as documented in the feature guide.
