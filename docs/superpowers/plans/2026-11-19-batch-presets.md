# Reusable batch presets

1. Add tests to `BatchImportTests` for save/reopen, column reorder with two
   valid barcodes, missing and ambiguous names, and positional samples.
2. Extend `BatchSetup` with named presets and store the CSV options, sheet and
   mapped column names or positions. Resolve a preset against each new source.
   Preserve element bindings when unrelated elements are removed.
3. Connect the preset manager, mapping errors and bounded five-record sample
   to `BatchWindow`. Require explicit sample confirmation for headerless
   imports. Exercise import, switching presets and validation in the batch dialog.
4. Update the user guide and run the shared tests on .NET Framework 4.8
   and .NET 10.
