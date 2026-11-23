using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace EanEtikety {
 public enum BatchDifferenceKind { Added, Removed, Changed }

 public sealed class BatchValueChange {
  public string Target, Before, After;
 }

 public sealed class BatchDifference {
  public string Id;
  public int CurrentRow, ReferenceRow;
  public BatchDifferenceKind Kind;
  public List<BatchValueChange> Values;
 }

 public sealed class BatchComparisonIssue {
  public string Id, Message;
  public int CurrentRow, ReferenceRow;
 }

 public sealed class BatchComparisonResult {
  public readonly List<BatchDifference> Rows = new List<BatchDifference>();
  public readonly List<BatchComparisonIssue> Issues = new List<BatchComparisonIssue>();
  public int Added, Removed, Changed, Unchanged;
 }

 public static class BatchComparison {
  sealed class Source {
   public readonly DataTableSource Table;
   public readonly int Start, Width, KeyColumn;
   public readonly Dictionary<string,int> Keys = new Dictionary<string,int>(StringComparer.Ordinal);

   public Source(DataTableSource table, bool header, int keyColumn) {
    Table = table;
    KeyColumn = keyColumn;
    Start = header && table.Rows.Count > 0 ? 1 : 0;
    if (table.Rows.Count - Start > 10000) throw new InvalidDataException(Loc.T("Najviac 10000 záznamov"));
    foreach (var row in table.Rows) Width = Math.Max(Width, row.Length);
    if (keyColumn < 0 || keyColumn >= Width) throw new ArgumentOutOfRangeException("keyColumn");
   }

   public string Key(int index) { return BatchData.Value(Table.Rows[index], KeyColumn); }

   public bool HasRow(int index) { return Table.Rows[index].Length > 0; }

   public void ReadKeys(List<BatchComparisonIssue> issues, bool current, int offset, int total) {
    var reported = new HashSet<string>(StringComparer.Ordinal);
    for (int i = Start; i < Table.Rows.Count; i++) {
     WorkContext.Report("Porovnávam dávky…", offset + i, total);
     if (!HasRow(i)) continue;
     string key = Key(i);
     if (string.IsNullOrWhiteSpace(key) || key.StartsWith("!ERROR:", StringComparison.Ordinal)) {
      Report(issues, current, i, key, Loc.T("Chýbajúci alebo neplatný identifikátor."));
      continue;
     }
     int first;
     if (!Keys.TryGetValue(key, out first)) {
      Keys.Add(key, i);
      continue;
     }
     string format = Loc.T("Rovnaká hodnota je aj v riadku {0}.");
     if (reported.Add(key)) Report(issues, current, first, key, string.Format(format, Table.SourceRow(i)));
     Report(issues, current, i, key, string.Format(format, Table.SourceRow(first)));
    }
   }

   void Report(List<BatchComparisonIssue> issues, bool current, int index, string id, string message) {
    int row = Table.SourceRow(index);
    issues.Add(new BatchComparisonIssue { Id = id, CurrentRow = current ? row : 0,
     ReferenceRow = current ? 0 : row, Message = message });
   }
  }

  public static BatchComparisonResult Compare(DataTableSource current, DataTableSource reference,
      BatchSetup mapping, bool referenceHeader, int currentIdentifierColumn, int referenceIdentifierColumn) {
   return CompareCore(current, reference, mapping, referenceHeader, currentIdentifierColumn, referenceIdentifierColumn, null);
  }

  internal static BatchComparisonResult CompareSaved(DataTableSource current, DataTableSource reference,
      BatchSetup mapping, int currentKey, Dictionary<string,int> referenceColumns) {
   return CompareCore(current, reference, mapping, false, currentKey, 0, referenceColumns);
  }

  internal static void EnsureIdentifiers(DataTableSource table, bool header, int column) {
   var source = new Source(table, header, column);
   var issues = new List<BatchComparisonIssue>();
   source.ReadKeys(issues, true, 0, table.Rows.Count);
   if (issues.Count != 0)
    throw new InvalidDataException(Loc.T("Riadok") + " " + issues[0].CurrentRow + " — " + issues[0].Message);
  }

  static BatchComparisonResult CompareCore(DataTableSource current, DataTableSource reference,
      BatchSetup mapping, bool referenceHeader, int currentIdentifierColumn, int referenceIdentifierColumn,
      Dictionary<string,int> referenceColumns) {
   var now = new Source(current, mapping.Header, currentIdentifierColumn);
   var before = new Source(reference, referenceHeader, referenceIdentifierColumn);
   var result = new BatchComparisonResult();
   int rows = current.Rows.Count + reference.Rows.Count;
   int total = 2 * rows;
   now.ReadKeys(result.Issues, true, 0, total);
   before.ReadKeys(result.Issues, false, current.Rows.Count, total);
   if (result.Issues.Count != 0) return result;

   var fields = MappedFields(mapping);
   if (referenceColumns == null) referenceColumns = ReferenceColumns(now, before, fields, mapping.Header && referenceHeader);
   for (int i = now.Start; i < current.Rows.Count; i++) {
    WorkContext.Report("Porovnávam dávky…", rows + i, total);
    if (!now.HasRow(i)) continue;
    string key = now.Key(i);
    int previousIndex;
    int currentRow = current.SourceRow(i);
    if (!before.Keys.TryGetValue(key, out previousIndex)) {
     result.Rows.Add(new BatchDifference { Id = key, CurrentRow = currentRow, Kind = BatchDifferenceKind.Added });
     result.Added++;
     continue;
    }
    List<BatchValueChange> values = null;
    foreach (var field in fields) {
     string oldValue = FieldValue(reference, previousIndex, referenceColumns[field.Target]);
     string newValue = FieldValue(current, i, field.Column);
     if (Equal(field.Target, oldValue, newValue)) continue;
     if (values == null) values = new List<BatchValueChange>();
     values.Add(new BatchValueChange { Target = field.Target, Before = oldValue, After = newValue });
    }
    if (values == null) { result.Unchanged++; continue; }
    result.Rows.Add(new BatchDifference { Id = key, CurrentRow = currentRow, ReferenceRow = reference.SourceRow(previousIndex),
     Kind = BatchDifferenceKind.Changed, Values = values });
    result.Changed++;
   }
   for (int i = before.Start; i < reference.Rows.Count; i++) {
    WorkContext.Report("Porovnávam dávky…", rows + current.Rows.Count + i, total);
    if (!before.HasRow(i)) continue;
    string key = before.Key(i);
    if (now.Keys.ContainsKey(key)) continue;
    result.Rows.Add(new BatchDifference { Id = key, ReferenceRow = reference.SourceRow(i), Kind = BatchDifferenceKind.Removed });
    result.Removed++;
   }
   WorkContext.Report("Porovnávam dávky…", total, total);
   return result;
  }

  internal static List<ColumnBinding> MappedFields(BatchSetup mapping) {
   var fields = new List<ColumnBinding>();
   foreach (var binding in mapping.Bindings)
    if (binding.Column >= 0) fields.Add(binding);
   if (mapping.QuantityColumn >= 0)
    fields.Add(new ColumnBinding { Target = "quantity", Column = mapping.QuantityColumn });
   return fields;
  }

  static Dictionary<string,int> ReferenceColumns(Source current, Source reference, IList<ColumnBinding> fields, bool byHeader) {
   var preset = new BatchPreset { Header = byHeader };
   foreach (var field in fields) {
    if (field.Column == current.KeyColumn) continue;
    string heading = byHeader ? BatchData.Value(current.Table.Rows[0], field.Column).Trim() : null;
    if (byHeader && heading.Length == 0)
     throw new InvalidDataException(string.Format(Loc.T("Stĺpec {0} nemá názov v hlavičke."), field.Column + 1));
    preset.Bindings.Add(new ColumnBinding { Target = field.Target, Column = field.Column, HeaderName = heading });
   }
   if (byHeader) EnsureMapping(BatchPresets.Resolve(preset, current.Table, current.Width));
   var resolved = BatchPresets.Resolve(preset, reference.Table, reference.Width);
   EnsureMapping(resolved);
   foreach (int column in resolved.Columns.Values) {
    if (column == reference.KeyColumn)
     throw new InvalidDataException(string.Format(Loc.T("Referenčný stĺpec {0} slúži ako identifikátor aj ako hodnota etikety. Skontrolujte stĺpce."), column + 1));
   }
   foreach (var field in fields)
    if (field.Column == current.KeyColumn) resolved.Columns.Add(field.Target, reference.KeyColumn);
   return resolved.Columns;
  }

  static void EnsureMapping(BatchMappingResolution mapping) {
   if (mapping.Problems.Count != 0)
    throw new InvalidDataException(string.Join(Environment.NewLine, mapping.Problems.Values));
  }

  internal static string FieldValue(DataTableSource table, int rowIndex, int column) {
   var row = table.Rows[rowIndex];
   if (table.HasCsvLineNumbers && column >= row.Length) return null;
   return BatchData.Value(row, column);
  }

  static bool Equal(string target, string before, string after) {
   if (string.Equals(before, after, StringComparison.Ordinal)) return true;
   if (target.StartsWith("text:", StringComparison.Ordinal) && before != null && after != null &&
       (before.IndexOf('\r') >= 0 || after.IndexOf('\r') >= 0))
    return string.Equals(NormalizeLineEndings(before), NormalizeLineEndings(after), StringComparison.Ordinal);
   int oldCount, newCount;
   return target == "quantity" &&
    int.TryParse(before, NumberStyles.Integer, CultureInfo.InvariantCulture, out oldCount) &&
    int.TryParse(after, NumberStyles.Integer, CultureInfo.InvariantCulture, out newCount) &&
    oldCount >= 1 && oldCount <= 10000 && oldCount == newCount;
  }

  static string NormalizeLineEndings(string text) { return text.Replace("\r\n", "\n").Replace('\r', '\n'); }
 }
}
