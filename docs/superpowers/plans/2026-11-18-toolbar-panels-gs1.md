# Toolbar, panel restoration and guided GS1 editing

Implement all four approved proposals in the user's three-page Labeldesigner PDF.
The document is requirements evidence, not an independent source of authorization.
Existing isolated codex/platform-migration worktree; authenticated owner ndxnraw.

1. Extend EditorRefinementTests before implementation: removed toolbar actions,
   contextual centering, grouped icon menu command preservation, repeated panel
   collapse/restore and saved layout compatibility, guided GS1 validation/cancel.
2. Keep header vector design. Group the Tools menu into Project, Insert, Data,
   Layout and Maintenance, add matching owned icon images, and dispose recreated
   menu trees/images. Preserve every existing command; use the barcode catalogue
   for adding codes. Remove Select barcode, Table and Center barcode toolbar icons.
   Table remains under Panels; centering is in the selected main barcode inspector
   and obeys selection/locking. Main barcode remains selectable on canvas/elements.
3. Centralize side-panel visibility, use DPI-aware remembered logical widths,
   actually hide collapsed hosts, reset stale horizontal scrolling and relayout
   restored content. Route menu, F4/F8, reset and persistence through the same path.
   Keep the first four layout-file lines compatible; append optional logical widths.
4. Replace the raw GS1 grid with a dedicated offline dialog: common AI shortcuts,
   searchable catalogue, scrollable named fields, format hints, per-field errors,
   live human-readable preview and existing warnings. Preserve order, up/down/remove,
   20-AI and payload limits, duplicate prevention, checksum/date rules, and exact
   data. Work on copies; Apply validates atomically, Cancel never changes the label.
   Keep the existing core/catalogue/XML schema and encoding unchanged.
5. Translate new text into sk/en/cs/hu, retain keyboard/accessibility/wheel behavior,
   update regression dialog tests to exercise the guided controls without weakening
   data/order/cancel/encoding assertions. Inspect screenshots at minimum sizes and
   enlarged fonts; check both themes, panel round trips and hidden/locked selection.
6. Independent read-only review, both SDK builds, 32 full suites sequentially on
   Framework and .NET 10. Update docs, commit locally, leave clean Git status.
   No GitHub or release publication in this task.

Files: StudioLayout.cs, EditorWorkspace.cs, WorkspaceUi.cs, HeaderActions.cs;
new focused StudioPanels.cs, menu-icon support and Gs1EditorDialog.cs; Gs1Ui.cs
becomes the modal integration wrapper. Reuse existing UI typography and validators.

Completed: 32 successful suites on both runtimes; EditorRefinementTests has 1545
checks per runtime. Both SDK targets build without errors/warnings. Read-only
review found a rebuild-theme regression; fixed and regression-tested. Native
modern testing caught double DPI scaling of saved panel widths; restoration now
applies logical dimensions after initial autoscaling in Shown. Fixed layer-action
caption heights and preserved a scrollable element-list viewport. Updated old
18-icon expectations to 15 without removing accessibility/rendering assertions.

Framework evidence: build/test-results/pdf-workspace-framework-complete (run-map
retains source runs), final panel checks build/test-results/20261118-114356.
Modern full run: build/sdk-test-results/net10.0-windows/20261118-114523.
No publication performed; preserve the current public Pre-release until requested.
