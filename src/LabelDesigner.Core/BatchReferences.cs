using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace EanEtikety {
 public sealed class BatchReference {
  public string SourceName, SheetName;
  public DateTime SavedUtc;
  public int Version = 1, RowCount;
  public bool Header, UsesConfiguredIdentifier;
  public ColumnBinding Identifier;
  public List<ColumnBinding> Fields = new List<ColumnBinding>();
  public byte[] PackedRows;

  internal BatchReference Copy() {
   var copy = (BatchReference)MemberwiseClone();
   copy.Identifier = Identifier == null ? null : Identifier.Copy();
   copy.Fields = Fields == null ? null : Fields.ConvertAll(field => field == null ? null : field.Copy());
   copy.PackedRows = PackedRows == null ? null : (byte[])PackedRows.Clone();
   return copy;
  }
 }

 public static class BatchReferences {
  const int MaxRows = 10000, MaxPackedBytes = 2 * 1024 * 1024, MaxRawBytes = 16 * 1024 * 1024;
  internal const int MaxProjectBytes = 6 * 1024 * 1024;
  static readonly Encoding TextEncoding = new UTF8Encoding(false, true);

  public static void EnsurePresetMapping(DataTableSource table, BatchSetup mapping, BatchPreset preset) {
   var fields = BatchComparison.MappedFields(mapping);
   if (mapping.UniqueColumn >= 0) fields.Add(new ColumnBinding { Target = "unique", Column = mapping.UniqueColumn });
   var resolved = BatchPresets.Resolve(preset, table, Width(table));
   if (mapping.Header == preset.Header && resolved.Problems.Count == 0 && fields.Count == resolved.Columns.Count) {
    if (fields.TrueForAll(field => {
     int column;
     return resolved.Columns.TryGetValue(field.Target, out column) && column == field.Column;
    })) return;
   }
   throw new InvalidDataException(Loc.T("Pred uložením referencie uložte aktuálne mapovanie do predvoľby."));
  }

  public static BatchReference Capture(DataTableSource table, BatchSetup mapping, int keyColumn,
      string sourceName, string sheetName, DateTime savedUtc) {
   if (mapping.UniqueColumn >= 0 && mapping.UniqueColumn != keyColumn) throw MappingChanged();
   BatchComparison.EnsureIdentifiers(table, mapping.Header, keyColumn);
   var reference = new BatchReference { SourceName = Path.GetFileName(sourceName), SheetName = sheetName,
    SavedUtc = savedUtc, Header = mapping.Header, UsesConfiguredIdentifier = mapping.UniqueColumn >= 0,
    Identifier = Describe(table, mapping.Header, "unique", keyColumn) };
   foreach (var field in BatchComparison.MappedFields(mapping))
    reference.Fields.Add(Describe(table, mapping.Header, field.Target, field.Column));
   if (ResolveIdentifier(table, mapping, reference) != keyColumn) throw MappingChanged();

   using (var raw = new MemoryStream()) {
    using (var writer = new BinaryWriter(raw, TextEncoding, true)) {
     for (int i = mapping.Header ? 1 : 0; i < table.Rows.Count; i++) {
      WorkContext.Report("Ukladám referenčnú dávku…", i, table.Rows.Count);
      if (table.Rows[i].Length == 0) continue;
      if (++reference.RowCount > MaxRows) throw TooLarge();
      writer.Write(table.SourceRow(i));
      WriteValue(writer, BatchData.Value(table.Rows[i], keyColumn));
      foreach (var field in reference.Fields) WriteValue(writer, BatchComparison.FieldValue(table, i, field.Column));
     }
    }
    if (reference.RowCount == 0) throw new InvalidDataException(Loc.T("Referenčná dávka musí obsahovať aspoň jeden záznam."));
    using (var packed = new MemoryStream()) {
     using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, true)) {
      raw.Position = 0;
      CopyBounded(raw, gzip);
     }
     reference.PackedRows = packed.ToArray();
    }
   }
   if (reference.PackedRows.Length > MaxPackedBytes) throw TooLarge();
   Validate(reference);
   return reference;
  }

  public static BatchComparisonResult Compare(DataTableSource current, BatchSetup mapping, BatchReference reference) {
   Validate(reference);
   int key = ResolveIdentifier(current, mapping, reference);
   var columns = new Dictionary<string,int>(StringComparer.Ordinal);
   for (int i = 0; i < reference.Fields.Count; i++) columns.Add(reference.Fields[i].Target, i + 1);
   return BatchComparison.CompareSaved(current, Open(reference), mapping, key, columns);
  }

  public static void Replace(BatchSetup setup, BatchPreset preset, BatchReference reference) {
   if (!setup.Presets.Contains(preset)) throw new ArgumentException("preset");
   if (reference != null) Validate(reference);
   long bytes = reference == null ? 0 : reference.PackedRows.Length;
   foreach (var other in setup.Presets)
    if (other != preset && other.Reference != null) bytes += other.Reference.PackedRows.Length;
   if (bytes > MaxProjectBytes) throw new InvalidDataException(Loc.T("Referenčné dávky prekračujú limit projektu."));
   preset.Reference = reference;
  }

  internal static void Strip(LabelSettings settings) {
   foreach (var preset in settings.Batch.Presets)
    if (preset != null) preset.Reference = null;
  }

  public static int ResolveIdentifier(DataTableSource current, BatchSetup mapping, BatchReference reference) {
   if (reference.Header != mapping.Header || reference.UsesConfiguredIdentifier != (mapping.UniqueColumn >= 0))
    throw MappingChanged();
   var fields = BatchComparison.MappedFields(mapping);
   if (fields.Count != reference.Fields.Count) throw MappingChanged();
   var schema = new BatchPreset { Header = reference.Header, Bindings = new List<ColumnBinding>(reference.Fields) };
   schema.Bindings.Add(reference.Identifier);
   var resolved = BatchPresets.Resolve(schema, current, Width(current));
   if (resolved.Problems.Count != 0) throw MappingChanged();
   foreach (var field in fields) {
    int candidate;
    if (!resolved.Columns.TryGetValue(field.Target, out candidate) || field.Column != candidate) throw MappingChanged();
   }
   int key = resolved.Columns["unique"];
   if (reference.UsesConfiguredIdentifier && mapping.UniqueColumn != key) throw MappingChanged();
   return key;
  }

  static int Width(DataTableSource table) {
   int width = 0;
   foreach (var row in table.Rows) width = Math.Max(width, row.Length);
   return width;
  }

  public static DataTableSource Open(BatchReference reference) {
   Validate(reference);
   var table = new DataTableSource { Name = reference.SheetName ?? reference.SourceName };
   try {
    using (var packed = new MemoryStream(reference.PackedRows, false))
    using (var gzip = new GZipStream(packed, CompressionMode.Decompress))
    using (var raw = new MemoryStream()) {
     CopyBounded(gzip, raw);
     raw.Position = 0;
     using (var reader = new BinaryReader(raw, TextEncoding, true)) {
      int previous = 0;
      for (int i = 0; i < reference.RowCount; i++) {
       WorkContext.Check();
       int row = reader.ReadInt32();
       if (row <= previous) throw InvalidReference();
       previous = row;
       var values = new string[reference.Fields.Count + 1];
       for (int n = 0; n < values.Length; n++) values[n] = reader.ReadBoolean() ? reader.ReadString() : null;
       table.Rows.Add(values);
       table.ReferenceRows.Add(row);
      }
      if (raw.Position != raw.Length) throw InvalidReference();
     }
    }
   } catch (EndOfStreamException) { throw InvalidReference(); }
     catch (DecoderFallbackException) { throw InvalidReference(); }
     catch (FormatException) { throw InvalidReference(); }
   BatchComparison.EnsureIdentifiers(table, false, 0);
   return table;
  }

  internal static void Validate(BatchReference reference) {
   if (reference == null || reference.Version != 1 || string.IsNullOrWhiteSpace(reference.SourceName) || reference.SourceName.Length > 260 ||
       reference.SheetName != null && reference.SheetName.Length > 100 || reference.SavedUtc.Kind != DateTimeKind.Utc ||
       reference.RowCount < 1 || reference.RowCount > MaxRows || reference.Fields == null || reference.Fields.Count > 72 ||
       reference.PackedRows == null || reference.PackedRows.Length == 0 || reference.PackedRows.Length > MaxPackedBytes ||
       reference.Identifier == null || reference.Identifier.Target != "unique") throw InvalidReference();
   ValidateBinding(reference.Identifier, reference.Header);
   var fields = new HashSet<string>(StringComparer.Ordinal) { "unique" };
   foreach (var field in reference.Fields) {
    ValidateBinding(field, reference.Header);
    if (!fields.Add(field.Target)) throw InvalidReference();
   }
  }

  static ColumnBinding Describe(DataTableSource table, bool header, string target, int column) {
   var binding = new ColumnBinding { Target = target, Column = column,
    HeaderName = header && table.Rows.Count > 0 ? BatchPresets.Heading(BatchData.Value(table.Rows[0], column)) : null };
   if (header && string.IsNullOrEmpty(binding.HeaderName))
    throw new InvalidDataException(string.Format(Loc.T("Stĺpec {0} nemá názov v hlavičke."), column + 1));
   return binding;
  }

  static void ValidateBinding(ColumnBinding field, bool header) {
   if (field == null || !BatchPresets.ValidTarget(field.Target) || field.Column < 0 || field.Column >= 256 ||
       header && (string.IsNullOrWhiteSpace(field.HeaderName) || field.HeaderName.Length > 512))
    throw InvalidReference();
  }

  static void WriteValue(BinaryWriter writer, string value) {
   if (writer.BaseStream.Length + 6 + (value == null ? 0 : TextEncoding.GetByteCount(value)) > MaxRawBytes)
    throw TooLarge();
   writer.Write(value != null);
   if (value != null) writer.Write(value);
  }

  static void CopyBounded(Stream source, Stream target) {
   var block = new byte[65536];
   long total = 0;
   int count;
   while ((count = source.Read(block, 0, block.Length)) != 0) {
    WorkContext.Check();
    total += count;
    if (total > MaxRawBytes) throw TooLarge();
    target.Write(block, 0, count);
   }
   WorkContext.Check();
  }

  static InvalidDataException MappingChanged() { return new InvalidDataException(Loc.T("Mapovanie alebo identifikátor sa od uloženia referencie zmenili.")); }
  static InvalidDataException InvalidReference() { return new InvalidDataException(Loc.T("Neplatná referenčná dávka.")); }
  static InvalidDataException TooLarge() { return new InvalidDataException(Loc.T("Referenčná dávka prekračuje limit veľkosti.")); }
 }
}
