# Portable workspace backup

1. Add shared integration tests for template and profile transfer, conflicts,
   printer reassignment, invalid archives and interrupted writes.
2. Implement a bounded ZIP format with a verified inventory. Validate templates
   and media settings using the existing project and profile rules.
3. Build a read-only restore preview, save the current workspace before writing
   and roll back changes if any write fails.
4. Add the export and restore dialogs to the project menu, localize them and
   test the feature on .NET Framework 4.8 and .NET 10.
