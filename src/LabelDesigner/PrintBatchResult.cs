using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace EanEtikety {
 public sealed class PrintBatchResultRow {
  [XmlAttribute("record")] public int Record;
  [XmlAttribute("planned")] public int Planned;
  [XmlAttribute("confirmed")] public int Confirmed;
  [XmlIgnore] public int Remaining { get { return Planned - Confirmed; } }
 }

 public sealed class PrintBatchResult {
  [XmlAttribute("entry")] public string EntryId;
  [XmlAttribute("revision")] public string Revision;
  [XmlAttribute("confirmed")] public int ConfirmedTotal = -1;
  [XmlElement("Row")] public List<PrintBatchResultRow> Rows = new List<PrintBatchResultRow>();

  internal static PrintBatchResult FromRecords(IEnumerable<int> records, int repetitions) {
   if (repetitions < 1 || repetitions > 32767) throw Invalid();
   var counts = new SortedDictionary<int,int>();
   int total = 0;
   foreach (int record in records) {
    WorkContext.Check();
    if (record < 0 || ++total > OutputPlan.MaxLabels) throw Invalid();
    int count;
    counts.TryGetValue(record, out count);
    counts[record] = count + repetitions;
   }
   var result = new PrintBatchResult();
   foreach (var pair in counts)
    result.Rows.Add(new PrintBatchResultRow { Record = pair.Key, Planned = pair.Value });
   return result;
  }

  internal void Validate(long expected) {
   Guid ignored;
   if (Rows == null || Rows.Count == 0 || Rows.Count > OutputPlan.MaxLabels || ConfirmedTotal < -1 ||
       (ConfirmedTotal >= 0 && !Guid.TryParseExact(Revision, "N", out ignored)) ||
       (ConfirmedTotal == -1 && Revision != null)) throw Invalid();
   long planned = 0, confirmed = 0;
   int lastRecord = -1;
   foreach (var row in Rows) {
    if (row == null || row.Record <= lastRecord || row.Planned < 1 ||
        row.Confirmed < 0 || row.Confirmed > row.Planned) throw Invalid();
    lastRecord = row.Record;
    planned += row.Planned;
    confirmed += row.Confirmed;
   }
   if (planned != expected || confirmed != Math.Max(0, ConfirmedTotal)) throw Invalid();
  }

  internal static InvalidDataException Invalid() { return new InvalidDataException(Loc.T("Ručný výsledok tlače je neplatný.")); }
 }

 public sealed partial class PrintHistoryStore {
  const int MaxResultBytes = 2 * 1024 * 1024;
  const string ResultSuffix = ".result.xml";

  static long PlannedTotal(PrintHistoryEntry entry) { return (long)entry.Labels * entry.Repetitions; }

  void LoadResultSummary(PrintHistoryEntry entry) {
   var path = PathFor(entry.Id, ResultSuffix);
   entry.HasResultRows = entry.HasArchive || File.Exists(path);
   try {
    if (!File.Exists(path)) {
     var retained = RetainedCopy(entry);
     if (retained == null) return;
     entry.HasResultRows |= retained.HasResultRows;
     entry.ManualConfirmed = retained.ManualConfirmed;
     entry.ManualResultUnavailable = retained.ManualResultUnavailable;
     return;
    }
    using (var reader = XmlReader.Create(path, new XmlReaderSettings {
     DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxResultBytes
    })) {
     reader.MoveToContent();
     int count;
     if (reader.Name != "PrintBatchResult" || reader.GetAttribute("entry") != entry.Id ||
         !int.TryParse(reader.GetAttribute("confirmed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) ||
         count < -1 || count > PlannedTotal(entry)) throw PrintBatchResult.Invalid();
     if (count < 0) return;
    }
    entry.ManualConfirmed = ReadBatchResult(entry).ConfirmedTotal;
   } catch (InvalidOperationException) { entry.ManualResultUnavailable = true; }
     catch (InvalidDataException) { entry.ManualResultUnavailable = true; }
     catch (XmlException) { entry.ManualResultUnavailable = true; }
     catch (IOException) { entry.ManualResultUnavailable = true; }
     catch (UnauthorizedAccessException) { entry.ManualResultUnavailable = true; }
  }

  public PrintBatchResult OpenBatchResult(string id) {
   using (new HistoryLease(mutexName)) return ReadBatchResult(ReadResultEntry(id));
  }

  PrintHistoryEntry ReadResultEntry(string id) {
   var entry = ReadLiveEntry(id) ?? FindRetainedEntry(id);
   if (entry == null) throw PrintBatchResult.Invalid();
   return entry;
  }

  PrintBatchResult ReadBatchResult(PrintHistoryEntry entry) {
   var result = ReadAvailableBatchResult(entry);
   if (result == null) throw PrintBatchResult.Invalid();
   return result;
  }

  PrintBatchResult ReadAvailableBatchResult(PrintHistoryEntry entry) {
   if (entry.Retained) return entry.RetainedResult;
   var path = PathFor(entry.Id, ResultSuffix);
   PrintBatchResult result;
   if (File.Exists(path)) result = Read<PrintBatchResult>(path, MaxResultBytes);
   else {
    var retained = RetainedCopy(entry);
    if (retained != null && retained.RetainedResult != null) return retained.RetainedResult;
    var archive = PathFor(entry.Id, ".job.xml");
    if (!File.Exists(archive)) return null;
    var job = Read<ArchivedPrintJob>(archive, MaxArchiveBytes);
    if (job == null) throw PrintBatchResult.Invalid();
    job.ValidateArchive();
    result = PrintBatchResult.FromRecords(job.Placements.Select(item => item.Record), job.Repetitions);
    result.EntryId = entry.Id;
   }
   if (result == null || result.EntryId != entry.Id) throw PrintBatchResult.Invalid();
   result.Validate(PlannedTotal(entry));
   return result;
  }

  void SavePlannedRows(PrintHistoryEntry entry, OutputPlan plan) {
   if (plan == null || plan.Placements.Count == 0) return;
   try {
    var result = PrintBatchResult.FromRecords(plan.Placements.Select(item => item.Record), entry.Repetitions);
    result.EntryId = entry.Id;
    result.Validate(PlannedTotal(entry));
    ProjectXml.Save(PathFor(entry.Id, ResultSuffix), result);
    entry.HasResultRows = true;
   } catch (InvalidOperationException ex) { DiagnosticLog.Write("print-result-rows", ex); }
     catch (InvalidDataException ex) { DiagnosticLog.Write("print-result-rows", ex); }
     catch (IOException ex) { DiagnosticLog.Write("print-result-rows", ex); }
     catch (UnauthorizedAccessException ex) { DiagnosticLog.Write("print-result-rows", ex); }
  }

  public PrintBatchResult SaveBatchResult(string id, PrintBatchResult proposed) {
   if (proposed == null) throw new ArgumentNullException("proposed");
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    return SaveResult(entry, ReadBatchResult(entry), proposed);
   }
  }

  PrintBatchResult SaveResult(PrintHistoryEntry entry, PrintBatchResult stored, PrintBatchResult proposed) {
   string id = entry.Id;
   string batchId = IsHistoryId(entry.BatchId) ? entry.BatchId : ResolveBatchId(entry, ReadHistoryEntries());
   EnsureBatchOpen(entry, batchId);
   if (proposed.EntryId != id || stored.Revision != proposed.Revision) return null;
   if (proposed.Rows == null || stored.Rows.Count != proposed.Rows.Count) throw PrintBatchResult.Invalid();
   int total = 0;
   bool changed = stored.ConfirmedTotal < 0;
   for (int i = 0; i < stored.Rows.Count; i++) {
    WorkContext.Check();
    var next = proposed.Rows[i];
    var row = stored.Rows[i];
    if (next == null || row.Record != next.Record || row.Planned != next.Planned || next.Confirmed < 0 || next.Confirmed > row.Planned)
     throw PrintBatchResult.Invalid();
    changed |= row.Confirmed != next.Confirmed;
    row.Confirmed = next.Confirmed;
    total = checked(total + next.Confirmed);
   }
   if (!changed) return stored;
   stored.ConfirmedTotal = total;
   stored.Revision = Guid.NewGuid().ToString("N");
   if (entry.Retained) {
    WriteRetainedEntry(entry, stored);
    return stored;
   }
   WriteLiveResult(stored);
   return stored;
  }

  void WriteLiveResult(PrintBatchResult result) {
   AtomicOutput.Save(PathFor(result.EntryId, ResultSuffix), temp => {
    using (var stream = File.Create(temp)) new XmlSerializer(typeof(PrintBatchResult)).Serialize(stream, result);
   });
  }

  void PreserveLegacyRows(string id) {
   if (File.Exists(PathFor(id, ResultSuffix))) return;
   try {
    var entry = ReadLiveEntry(id);
    var result = entry == null ? null : ReadAvailableBatchResult(entry);
    if (result != null) WriteLiveResult(result);
   } catch (InvalidOperationException ex) { DiagnosticLog.Write("batch-retention", ex); }
     catch (InvalidDataException ex) { DiagnosticLog.Write("batch-retention", ex); }
  }
 }
}
