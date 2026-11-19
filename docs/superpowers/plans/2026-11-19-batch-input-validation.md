# Batch input validation

1. Extend `BatchImportTests` with quoted CSV fields crossing physical lines,
   missing and invalid fields, duplicate identifiers, and printable row mapping.
2. Preserve input line numbers in `BatchData`. Collect issues per prepared record
   and keep any recoverable invalid-row artwork separate from the printable label.
   Use `BatchProcessing` to compare identifiers only for an explicitly mapped
   column.
3. Add a virtualized issue list to `BatchWindow` and use its existing preview worker
   for the selected row. Exercise the import, issue list, navigation and exclusion
   from a live batch dialog in `BackgroundBatchTests`.
4. Document the behavior and run the regression runners for Framework and .NET 10.
