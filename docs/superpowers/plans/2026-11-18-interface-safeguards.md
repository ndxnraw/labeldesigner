# Interface safeguards — Pre-release

User approved the proposed interface package: protect dropdowns from accidental
wheel changes, fix text clipping and explain print settings. Work continues in
the existing isolated codex/platform-migration worktree. No publication requested.

## Design and tasks

1. Extend EditorRefinementTests with failing native Windows wheel tests for
   ModernComboBox, both list-only and editable styles. Hover and keyboard focus
   do not authorize closed-list wheel changes; a left click plus retained focus
   does. Focus loss resets authorization. An explicitly opened list can scroll.
   Preserve keyboard selection and text input. Blocked wheel scrolls the parent,
   sharing the existing numeric wheel routing rather than duplicating it.
2. Inspect translated print settings at minimum window size and 100/150/200%
   fonts. Replace fixed-height print buttons with wrapped content sizing; bound
   labels/checks by the actual available width. Adapt print actions/summary and
   provide scrolling for constrained content rather than hiding actions.
   Check long dropdown entries are fully readable when the list is opened.
3. Add local, translated inline explanations and accessible descriptions for X/Y
   offsets, X/Y scale, DPI, Zebra darkness and ignoring driver margins. Explain
   units, default/override behavior and limits. Do not alter output settings or
   approval requirements. Verify all four translations and keyboard access.
4. Run focused tests, independent read-only review, both SDK builds and all 32
   suites sequentially on Framework and .NET 10. Preserve offline builds and
   projects, update documentation, commit locally and leave a clean worktree.

Production files: UiControls.cs, ClickWheelNumericUpDown.cs and a focused shared
wheel helper; PrintPanel.cs/PrintOrganization.cs/PrinterProfiles.cs/ZebraUi.cs;
translations.tsv. Tests remain in EditorRefinementTests and existing layout suites.

Completed: all 32 suites pass on both runtimes, including 1360 editor refinement
checks per runtime. Both SDK targets build with zero errors/warnings. Native probes
reproduced the flow-row/native-combo height mismatch before the AutoSize fix;
growth/shrink tests cover the correction. Exact outer-minimum checks exposed and
verified wrapped checkbox captions. Independent read-only review found no actionable
issues. Reports: build/test-results/20261118-080409 and
build/sdk-test-results/net10.0-windows/20261118-081654. Offline operation and output
approval remain unchanged. No GitHub publication performed.
