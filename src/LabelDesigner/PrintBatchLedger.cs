using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace EanEtikety {
 public sealed class RetainedPrintAttempt {
  public PrintHistoryEntry Entry;
  public PrintBatchResult Result;
 }

 public sealed class PrintBatchAttempt {
  public PrintHistoryEntry Entry { get; internal set; }
  public bool InHistory { get { return !Entry.Retained; } }
 }

 public sealed class PrintBatchOverview {
  public string BatchId { get; internal set; }
  public PrintBatchOutcome Outcome { get; internal set; }
  public List<PrintBatchAttempt> Attempts { get; internal set; }
 }

 public sealed class PrintBatchCatalogItem {
  public string BatchId { get; internal set; }
  public DateTime? StartedUtc { get; internal set; }
  public string Printer { get; internal set; }
 }

 public sealed partial class PrintHistoryStore {
  string BatchDirectory { get { return Path.Combine(directory, "batches"); } }

  static bool IsHistoryId(string id) {
   Guid parsed;
   return Guid.TryParseExact(id, "N", out parsed) && parsed.ToString("N") == id;
  }

  string BatchFolder(string batchId) {
   if (!IsHistoryId(batchId)) throw PrintBatchResult.Invalid();
   return Path.Combine(BatchDirectory, batchId);
  }

  string RetainedPath(string batchId, string id) {
   if (!IsHistoryId(id)) throw PrintBatchResult.Invalid();
   return Path.Combine(BatchFolder(batchId), id + ".xml");
  }

  IEnumerable<string> RetainedBatchIds() {
   return Directory.Exists(BatchDirectory)
    ? Directory.EnumerateDirectories(BatchDirectory).Select(path => Path.GetFileName(path)).Where(IsHistoryId)
    : Enumerable.Empty<string>();
  }

  PrintHistoryEntry ReadRetainedEntry(string batchId, string id) {
   string path = RetainedPath(batchId, id);
   if (!File.Exists(path)) return null;
   var attempt = Read<RetainedPrintAttempt>(path, MaxResultBytes + 64 * 1024);
   if (attempt == null || attempt.Entry == null || attempt.Entry.Id != id || attempt.Entry.BatchId != batchId)
    throw PrintBatchResult.Invalid();
   var entry = attempt.Entry;
   if (attempt.Result != null) {
    if (attempt.Result.EntryId != id) throw PrintBatchResult.Invalid();
    attempt.Result.Validate(PlannedTotal(entry));
    if (attempt.Result.ConfirmedTotal >= 0) entry.ManualConfirmed = attempt.Result.ConfirmedTotal;
   }
   entry.Retained = true;
   entry.RetainedResult = attempt.Result;
   entry.HasResultRows = attempt.Result != null;
   entry.HasArchive = false;
   entry.QueueToken = null;
   return entry;
  }

  PrintHistoryEntry FindRetainedEntry(string id) {
   var original = ReadRetainedForOverview(id, id);
   if (original != null) return original;
   foreach (string batchId in RetainedBatchIds()) {
    WorkContext.Check();
    if (batchId == id) continue;
    var attempt = ReadRetainedForOverview(batchId, id);
    if (attempt != null) return attempt;
   }
   return null;
  }

  PrintHistoryEntry ReadRetainedForOverview(string batchId, string id) {
   try { return ReadRetainedEntry(batchId, id); }
   catch (InvalidDataException) { }
   catch (InvalidOperationException) { }
   catch (IOException) { }
   catch (UnauthorizedAccessException) { }
   return new PrintHistoryEntry { Id = id, BatchId = batchId, Retained = true, ManualResultUnavailable = true,
    RecoverySourceId = id == batchId ? null : batchId };
  }

  PrintHistoryEntry RetainedCopy(PrintHistoryEntry entry) {
   if (IsHistoryId(entry.BatchId)) return ReadRetainedForOverview(entry.BatchId, entry.Id);
   return string.IsNullOrEmpty(entry.RecoverySourceId) ? ReadRetainedForOverview(entry.Id, entry.Id) : FindRetainedEntry(entry.Id);
  }

  PrintHistoryEntry ReadLiveEntry(string id) {
   string path = PathFor(id, ".entry.xml");
   if (!File.Exists(path)) return null;
   var entry = Read<PrintHistoryEntry>(path, 64 * 1024);
   if (entry == null || entry.Id != id) throw PrintBatchResult.Invalid();
   return entry;
  }

  PrintHistoryEntry BatchParent(string id, IDictionary<string,PrintHistoryEntry> live) {
   PrintHistoryEntry parent;
   if (live.TryGetValue(id, out parent)) return parent;
   return IsHistoryId(id) ? FindRetainedEntry(id) : null;
  }

  string ResolveBatchId(PrintHistoryEntry entry, IDictionary<string,PrintHistoryEntry> live) {
   var current = entry;
   var visited = new HashSet<string>(StringComparer.Ordinal) { current.Id };
   while (true) {
    if (IsHistoryId(current.BatchId)) return current.BatchId;
    if (string.IsNullOrEmpty(current.RecoverySourceId)) return current.Id;
    var parent = BatchParent(current.RecoverySourceId, live);
    if (parent == null) {
     // Older releases may already have saved the missing relationship in a closure.
     var closure = ReadClosureHeaders().FirstOrDefault(item => item.Includes(current.Id) ||
      item.Includes(current.RecoverySourceId) || item.EntryId == current.RecoverySourceId);
     return closure != null ? closure.EntryId : IsHistoryId(current.RecoverySourceId) ? current.RecoverySourceId : current.Id;
    }
    if (current.RecoveredCopy < 1 || !visited.Add(parent.Id)) return current.Id;
    if (parent.Retained && parent.ManualResultUnavailable) return parent.BatchId;
    if (current.RecoveredCopy > parent.Repetitions) return current.Id;
    current = parent;
   }
  }

  void AssignBatchId(PrintHistoryEntry entry) {
   if (string.IsNullOrEmpty(entry.RecoverySourceId)) { entry.BatchId = entry.Id; return; }
   var live = ReadHistoryEntries();
   string approved = entry.BatchId;
   entry.BatchId = null;
   string resolved = ResolveBatchId(entry, live);
   if (IsHistoryId(approved) && resolved != entry.Id && approved != resolved) throw PrintBatchResult.Invalid();
   entry.BatchId = IsHistoryId(approved) ? approved : resolved;
   EnsureBatchOpen(entry, entry.BatchId);
  }

  void WriteRetainedEntry(PrintHistoryEntry entry, PrintBatchResult result) {
   string path = RetainedPath(entry.BatchId, entry.Id);
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   AtomicOutput.Save(path, temp => {
    using (var stream = File.Create(temp))
     new XmlSerializer(typeof(RetainedPrintAttempt)).Serialize(stream, new RetainedPrintAttempt { Entry = entry, Result = result });
   });
  }

  void RetainEntry(string id) {
   var entry = ReadLiveEntry(id);
   if (entry == null || PlannedTotal(entry) <= 0) return;
   entry.BatchId = ResolveBatchId(entry, ReadHistoryEntries());
   PrintBatchResult result = null;
   if (File.Exists(PathFor(id, ResultSuffix)) || File.Exists(PathFor(id, ".job.xml"))) {
    try { result = ReadBatchResult(entry); }
    catch (InvalidOperationException ex) { DiagnosticLog.Write("batch-retention", ex); }
    catch (InvalidDataException ex) { DiagnosticLog.Write("batch-retention", ex); }
   }
   // A previous cleanup may have saved the counts before removing only some history files.
   if (result == null) {
    var prior = ReadRetainedEntry(entry.BatchId, entry.Id);
    if (prior != null) result = prior.RetainedResult;
   }
   entry.HasArchive = false;
   entry.QueueToken = null;
   WriteRetainedEntry(entry, result);
  }

  List<PrintHistoryEntry> BatchEntries(string batchId, IDictionary<string,PrintHistoryEntry> live) {
   var entries = live.Values.Where(item => ResolveBatchId(item, live) == batchId).ToList();
   string folder = BatchFolder(batchId);
   if (!Directory.Exists(folder)) return entries;
   foreach (string path in Directory.EnumerateFiles(folder, "*.xml")) {
    WorkContext.Check();
    string id = Path.GetFileNameWithoutExtension(path);
    if (!IsHistoryId(id) || live.ContainsKey(id)) continue;
    var retained = ReadRetainedForOverview(batchId, id);
    if (retained != null) entries.Add(retained);
   }
   return entries;
  }

  public string GetPrintBatchId(string entryId) {
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(entryId);
    return IsHistoryId(entry.BatchId) ? entry.BatchId : ResolveBatchId(entry, ReadHistoryEntries());
   }
  }

  public List<PrintBatchCatalogItem> ListPrintBatches() {
   using (new HistoryLease(mutexName)) {
    var live = ReadHistoryEntries();
    var ids = new HashSet<string>(RetainedBatchIds(), StringComparer.Ordinal);
    foreach (var item in live.Values)
     if (PlannedTotal(item) > 0) ids.Add(ResolveBatchId(item, live));
    foreach (var closure in ReadClosureHeaders()) ids.Add(closure.EntryId);
    var batches = new List<PrintBatchCatalogItem>();
    foreach (string id in ids) {
     WorkContext.Check();
     PrintHistoryEntry original;
     if (!live.TryGetValue(id, out original)) original = ReadRetainedForOverview(id, id);
     batches.Add(new PrintBatchCatalogItem { BatchId = id, StartedUtc = original == null || original.StartedUtc == default(DateTime)
      ? (DateTime?)null : original.StartedUtc,
      Printer = original == null ? null : original.Printer });
    }
    return batches.OrderByDescending(item => item.StartedUtc).ThenBy(item => item.BatchId, StringComparer.Ordinal).ToList();
   }
  }

  public PrintBatchOverview OpenPrintBatch(string batchId) {
   using (new HistoryLease(mutexName)) {
    var entries = BatchEntries(batchId, ReadHistoryEntries());
    var original = entries.Find(item => item.Id == batchId && string.IsNullOrEmpty(item.RecoverySourceId));
    var result = original == null ? null : TryReadAttemptResult(original);
    return new PrintBatchOverview { BatchId = batchId,
     Outcome = result == null ? null : BatchOutcome(original, result, batchId, entries),
     Attempts = entries.OrderBy(item => item.Id != batchId).ThenBy(item => item.StartedUtc).ThenBy(item => item.Id, StringComparer.Ordinal)
      .Select(item => { FillAttemptSummary(item); return new PrintBatchAttempt { Entry = item }; }).ToList() };
   }
  }

  void FillAttemptSummary(PrintHistoryEntry entry) {
   if (entry.Retained) return;
   entry.HasArchive &= File.Exists(PathFor(entry.Id, ".job.xml"));
   LoadResultSummary(entry);
  }
 }
}
