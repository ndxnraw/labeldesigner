using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EanEtikety {
 public sealed class PrintBatchBackup {
  public string BatchId;
  public List<RetainedPrintAttempt> Attempts = new List<RetainedPrintAttempt>();
  public List<PrintBatchClosure> Closures = new List<PrintBatchClosure>();

  internal void Validate(string id) {
   if (BatchId != id || !UpdateFiles.Hex(id, 32) || Attempts == null || Closures == null ||
       Attempts.Count + Closures.Count == 0) throw Invalid();
   var attemptIds = new HashSet<string>(StringComparer.Ordinal);
   foreach (var attempt in Attempts) {
    WorkContext.Check();
    var entry = attempt == null ? null : attempt.Entry;
    if (entry == null || !UpdateFiles.Hex(entry.Id, 32) || entry.BatchId != id || !attemptIds.Add(entry.Id) ||
        entry.HasArchive || entry.QueueToken != null || entry.Labels < 1 || entry.Labels > OutputPlan.MaxLabels ||
        entry.Repetitions < 1 || entry.Repetitions > 32767 || entry.Pages < 0 || !Enum.IsDefined(typeof(PrintHistoryStatus), entry.Status))
     throw Invalid();
    if (string.IsNullOrEmpty(entry.RecoverySourceId)) {
     if (entry.Id != id || entry.RecoveredCopy != 0) throw Invalid();
    } else if (!UpdateFiles.Hex(entry.RecoverySourceId, 32) || entry.RecoverySourceId == entry.Id || entry.RecoveredCopy < 1 ||
               entry.RecoveredCopy > 32767) throw Invalid();
    if (attempt.Result == null) continue;
    if (attempt.Result.EntryId != entry.Id) throw Invalid();
    attempt.Result.Validate((long)entry.Labels * entry.Repetitions);
   }
   var attempts = Attempts.ToDictionary(item => item.Entry.Id, item => item.Entry, StringComparer.Ordinal);
   foreach (var entry in attempts.Values) {
    var visited = new HashSet<string>(StringComparer.Ordinal) { entry.Id };
    var current = entry;
    PrintHistoryEntry parent;
    while (!string.IsNullOrEmpty(current.RecoverySourceId) && attempts.TryGetValue(current.RecoverySourceId, out parent)) {
     WorkContext.Check();
     if (current.Id == id || current.RecoveredCopy > parent.Repetitions || !visited.Add(parent.Id)) throw Invalid();
     current = parent;
    }
   }
   var closureIds = new HashSet<string>(StringComparer.Ordinal);
   int active = 0;
   foreach (var closure in Closures) {
    if (closure == null || closure.EntryId != id || !UpdateFiles.Hex(closure.ClosureId, 32) ||
        !closureIds.Add(closure.ClosureId) || (!closure.IsOpen && ++active > 1)) throw Invalid();
    closure.Validate();
   }
  }

  internal static InvalidDataException Invalid() { return new InvalidDataException(Loc.T("Neplatné údaje o dávke v zálohe.")); }

  internal void ValidateEncodedSizes() {
   var encoding = new UTF8Encoding(false, true);
   foreach (var attempt in Attempts) {
    WorkContext.Check();
    if (encoding.GetByteCount(ProjectXml.Encode(attempt.Entry)) > 64 * 1024 ||
        encoding.GetByteCount(ProjectXml.Encode(attempt)) > PrintHistoryStore.MaxResultBytes + 64 * 1024 ||
        (attempt.Result != null && encoding.GetByteCount(ProjectXml.Encode(attempt.Result)) > PrintHistoryStore.MaxResultBytes)) throw Invalid();
   }
   foreach (var closure in Closures) {
    WorkContext.Check();
    if (encoding.GetByteCount(ProjectXml.Encode(closure)) > PrintHistoryStore.MaxClosureBytes) throw Invalid();
   }
  }

  internal static bool SameResult(PrintBatchResult left, PrintBatchResult right) {
   if (left == null || right == null) return left == right;
   return left.ConfirmedTotal == right.ConfirmedTotal && SamePlan(left, right) &&
    left.Rows.Select(row => row.Confirmed).SequenceEqual(right.Rows.Select(row => row.Confirmed));
  }

  static bool SamePlan(PrintBatchResult left, PrintBatchResult right) {
   return left.Rows.Select(row => row.Record).SequenceEqual(right.Rows.Select(row => row.Record)) &&
    left.Rows.Select(row => row.Planned).SequenceEqual(right.Rows.Select(row => row.Planned));
  }

  internal static bool Compatible(RetainedPrintAttempt local, RetainedPrintAttempt incoming) {
   return ProjectXml.Encode(local.Entry) == ProjectXml.Encode(incoming.Entry) &&
    (local.Result == null || incoming.Result == null || SamePlan(local.Result, incoming.Result));
  }

  internal static bool SameClosure(PrintBatchClosure left, PrintBatchClosure right) {
   return ProjectXml.Encode(left) == ProjectXml.Encode(right);
  }
 }

 public sealed partial class PrintHistoryStore {
  internal string BackupDirectory { get { return directory; } }

  sealed class BackupLeases : IDisposable {
   readonly List<IDisposable> leases = new List<IDisposable>();
   public void Add(IDisposable lease) { leases.Add(lease); }
   public void Dispose() {
    for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
   }
  }

  internal IDisposable LockBatchBackup(IEnumerable<string> affected = null) {
   var locks = new BackupLeases();
   try {
    if (affected != null) {
     foreach (string id in affected.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)) {
      WorkContext.Check();
      if (!IsHistoryId(id)) throw PrintBatchBackup.Invalid();
      locks.Add(new HistoryLease(mutexName + ".Reprint." + id));
     }
    }
    locks.Add(new HistoryLease(mutexName));
    return locks;
   } catch { locks.Dispose(); throw; }
  }

  internal List<PrintBatchBackup> CaptureBatchBackup() {
   using (LockBatchBackup()) {
    CheckBatchBackupPaths();
    var live = ReadHistoryEntries();
    var batches = new SortedDictionary<string,PrintBatchBackup>(StringComparer.Ordinal);
    foreach (var id in RetainedBatchIds()) batches.Add(id, new PrintBatchBackup { BatchId = id });
    foreach (var entry in live.Values) {
     if (PlannedTotal(entry) <= 0) continue;
     string id = ResolveBatchId(entry, live);
     if (!batches.ContainsKey(id)) batches.Add(id, new PrintBatchBackup { BatchId = id });
    }
    foreach (string path in ClosureFiles("*.xml")) {
     var header = ReadClosureHeader(path);
     var closure = LoadClosure(header.EntryId, header.ClosureId);
     PrintBatchBackup batch;
     if (!batches.TryGetValue(header.EntryId, out batch)) {
      batch = new PrintBatchBackup { BatchId = header.EntryId };
      batches.Add(header.EntryId, batch);
     }
     batch.Closures.Add(closure);
    }
    var result = new List<PrintBatchBackup>();
    foreach (var batch in batches.Values) {
     WorkContext.Check();
     var entries = BatchEntries(batch.BatchId, live);
     if (entries.Any(item => item.Retained && item.ManualResultUnavailable)) throw PrintBatchBackup.Invalid();
     foreach (var entry in entries.Where(item => PlannedTotal(item) > 0).OrderBy(item => item.Id, StringComparer.Ordinal)) {
      batch.Attempts.Add(new RetainedPrintAttempt { Entry = PortableEntry(entry, batch.BatchId), Result = ReadAvailableBatchResult(entry) });
     }
     if (batch.Attempts.Count == 0 && batch.Closures.Count == 0) continue;
     batch.Closures.Sort((left, right) => StringComparer.Ordinal.Compare(left.ClosureId, right.ClosureId));
     batch.Validate(batch.BatchId);
     result.Add(batch);
    }
    return result;
   }
  }

  static PrintHistoryEntry PortableEntry(PrintHistoryEntry entry, string batchId) {
   return new PrintHistoryEntry { Id = entry.Id, BatchId = batchId, Printer = entry.Printer, Media = entry.Media,
    StartedUtc = entry.StartedUtc, Pages = entry.Pages, Labels = entry.Labels, Repetitions = entry.Repetitions,
    RecoverySourceId = entry.RecoverySourceId, RecoveredCopy = entry.RecoveredCopy, Status = entry.Status };
  }

  void CheckBatchBackupPaths() {
   if (!Directory.Exists(directory)) {
    if (File.Exists(directory)) throw PrintBatchBackup.Invalid();
    return;
   }
   UpdateFiles.NoReparse(directory);
   foreach (string file in Directory.EnumerateFiles(directory, "*.xml")) UpdateFiles.NoReparse(file);
   if (Directory.Exists(ClosureDirectory)) {
    UpdateFiles.NoReparse(ClosureDirectory);
    foreach (string file in ClosureFiles("*.xml")) UpdateFiles.NoReparse(file);
   }
   if (!Directory.Exists(BatchDirectory)) return;
   UpdateFiles.NoReparse(BatchDirectory);
   foreach (string batchId in RetainedBatchIds()) {
    string folder = BatchFolder(batchId);
    UpdateFiles.NoReparse(folder);
    foreach (string file in Directory.EnumerateFiles(folder, "*.xml")) UpdateFiles.NoReparse(file);
   }
  }

  internal void BatchRestoreWrites(PrintBatchBackup before, PrintBatchBackup after, List<WorkspaceRestoreWrite> writes) {
   var attempts = before == null ? new Dictionary<string,RetainedPrintAttempt>(StringComparer.Ordinal) :
    before.Attempts.ToDictionary(item => item.Entry.Id, StringComparer.Ordinal);
   foreach (var item in after.Attempts) {
    RetainedPrintAttempt old;
    attempts.TryGetValue(item.Entry.Id, out old);
    if (old != null && PrintBatchBackup.SameResult(old.Result, item.Result)) continue;
    var result = item.Result == null ? null : ProjectXml.Decode<PrintBatchResult>(ProjectXml.Encode(item.Result));
    if (result != null && result.ConfirmedTotal >= 0) result.Revision = Guid.NewGuid().ToString("N");
    var live = ReadLiveEntry(item.Entry.Id);
    if (live != null) {
     if (result == null || old == null || !PrintBatchBackup.Compatible(old, item)) throw PrintBatchBackup.Invalid();
     QueueBatchWrite(PathFor(live.Id, ResultSuffix), ProjectXml.Encode(result), writes);
    } else QueueBatchWrite(RetainedPath(after.BatchId, item.Entry.Id), ProjectXml.Encode(new RetainedPrintAttempt {Entry=item.Entry,Result=result}), writes);
   }
   var closures = before == null ? new Dictionary<string,PrintBatchClosure>(StringComparer.Ordinal) :
    before.Closures.ToDictionary(item => item.ClosureId, StringComparer.Ordinal);
   var retainedClosures = new HashSet<string>(after.Closures.Select(item => item.ClosureId), StringComparer.Ordinal);
   foreach (var removed in closures.Values.Where(item => !retainedClosures.Contains(item.ClosureId)))
    QueueBatchWrite(ClosurePath(removed.EntryId, removed.ClosureId), null, writes);
   foreach (var item in after.Closures.OrderBy(item => !item.IsOpen)) {
    PrintBatchClosure old;
    if (!closures.TryGetValue(item.ClosureId, out old) || !PrintBatchBackup.SameClosure(old, item))
     QueueBatchWrite(ClosurePath(item.EntryId, item.ClosureId), ProjectXml.Encode(item), writes);
   }
  }

  void QueueBatchWrite(string path, string xml, List<WorkspaceRestoreWrite> writes) {
   string name = path.Substring(directory.TrimEnd('\\', '/').Length + 1).Replace('\\', '/');
   writes.Add(WorkspaceRestoreWrite.Capture(directory, name, xml == null ? null : new UTF8Encoding(false, true).GetBytes(xml)));
  }
 }
}
