using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EanEtikety {
 public sealed class PrintBatchBackupCollection {
  public List<PrintBatchBackup> Batches = new List<PrintBatchBackup>();

  internal void Validate(bool encodedSizes = false) {
   if (Batches == null) throw PrintBatchBackup.Invalid();
   var batches = new HashSet<string>(StringComparer.Ordinal);
   var closures = new HashSet<string>(StringComparer.Ordinal);
   foreach (var batch in Batches) {
    WorkContext.Check();
    if (batch == null || !batches.Add(batch.BatchId)) throw PrintBatchBackup.Invalid();
    batch.Validate(batch.BatchId);
    if (encodedSizes) batch.ValidateEncodedSizes();
    foreach (var closure in batch.Closures)
     if (!closures.Add(closure.ClosureId)) throw PrintBatchBackup.Invalid();
   }
   var attempts = WorkspaceBatchRestore.Owners(Batches);
   foreach (var batch in Batches)
    if (WorkspaceBatchRestore.ForeignLinks(batch, attempts)) throw PrintBatchBackup.Invalid();
  }
 }

 internal static class WorkspaceBatchRestore {
  internal static Dictionary<string,string> Owners(IEnumerable<PrintBatchBackup> batches) {
   var owners = new Dictionary<string,string>(StringComparer.Ordinal);
   foreach (var batch in batches) {
    RegisterOwner(owners, batch.BatchId, batch.BatchId);
    foreach (var attempt in batch.Attempts) RegisterOwner(owners, attempt.Entry.Id, batch.BatchId);
   }
   return owners;
  }

  static void RegisterOwner(Dictionary<string,string> owners, string id, string batchId) {
   string owner;
   if (!owners.TryGetValue(id, out owner)) owners.Add(id,batchId);
   else if (owner != batchId) throw PrintBatchBackup.Invalid();
  }

  internal static bool ForeignLinks(PrintBatchBackup batch, Dictionary<string,string> attempts) {
   string known;
   if (attempts.TryGetValue(batch.BatchId, out known) && known != batch.BatchId) return true;
   foreach (var item in batch.Attempts) {
    string owner;
    if (attempts.TryGetValue(item.Entry.Id, out owner) && owner != batch.BatchId) return true;
    string source = item.Entry.RecoverySourceId;
    if (!string.IsNullOrEmpty(source) && attempts.TryGetValue(source, out owner) && owner != batch.BatchId) return true;
   }
   foreach (var closure in batch.Closures)
    if (!string.IsNullOrEmpty(closure.LinkedEntries) &&
        closure.LinkedEntries.Split(',').Any(id => attempts.TryGetValue(id, out known) && known != batch.BatchId)) return true;
   return false;
  }

  internal static void Preview(List<PrintBatchBackup> local, List<PrintBatchBackup> incoming,
   ISet<string> useBackup, WorkspaceRestorePlan plan) {
   var batches = local.ToDictionary(item => item.BatchId, StringComparer.Ordinal);
   var attemptOwners = Owners(local);
   var closureOwners = local.SelectMany(item => item.Closures).ToDictionary(item => item.ClosureId, item => item.EntryId, StringComparer.Ordinal);
   foreach (var candidate in incoming.OrderBy(item => item.BatchId, StringComparer.Ordinal)) {
    WorkContext.Check();
    PrintBatchBackup previous;
    batches.TryGetValue(candidate.BatchId, out previous);
    if (ForeignLinks(candidate, attemptOwners) || candidate.Closures.Any(item => {
     string owner;
     return closureOwners.TryGetValue(item.ClosureId, out owner) && owner != candidate.BatchId;
    })) {
     plan.Items.Add(Item(candidate, previous, WorkspaceRestoreAction.Invalid, true, Loc.T("Nezlučiteľné identifikátory dávky.")));
     continue;
    }
    if (previous == null) {
     plan.BatchWrites.Add(candidate);
     plan.Items.Add(Item(candidate, null, WorkspaceRestoreAction.Add, false, ClosureBalanceNote(candidate.Closures.Find(item => !item.IsOpen),
      candidate.Attempts)));
     continue;
    }
    Merge(previous, candidate, useBackup != null && useBackup.Contains(candidate.BatchId), plan);
   }
   // An incoming attempt can also become the parent of a local orphan.
   var merged = new PrintBatchBackupCollection {
    Batches = local.Where(item => !plan.BatchWrites.Any(next => next.BatchId == item.BatchId)).Concat(plan.BatchWrites).ToList()
   };
   merged.Validate();
  }

  static void Merge(PrintBatchBackup previous, PrintBatchBackup incoming, bool replace, WorkspaceRestorePlan plan) {
   var attempts = previous.Attempts.ToDictionary(item => item.Entry.Id, StringComparer.Ordinal);
   var closures = previous.Closures.ToDictionary(item => item.ClosureId, StringComparer.Ordinal);
   var merged = new PrintBatchBackup { BatchId = previous.BatchId };
   bool changed = false, attemptsChanged = false;
   int results = 0, closureChanges = 0;
   var differences = new List<string>();
   foreach (var candidate in incoming.Attempts) {
    RetainedPrintAttempt old;
    if (!attempts.TryGetValue(candidate.Entry.Id, out old)) {
     attempts.Add(candidate.Entry.Id, candidate);
     changed = attemptsChanged = true;
     continue;
    }
    if (!PrintBatchBackup.Compatible(old, candidate)) {
     plan.Items.Add(Item(incoming, previous, WorkspaceRestoreAction.Invalid, true, Loc.T("Nezlučiteľné identifikátory dávky.")));
     return;
    }
    if (candidate.Result == null || PrintBatchBackup.SameResult(old.Result, candidate.Result) ||
        (old.Result != null && old.Result.ConfirmedTotal >= 0 && candidate.Result.ConfirmedTotal < 0)) continue;
    if (old.Result != null && old.Result.ConfirmedTotal >= 0) {
     results++;
     for (int index = 0; index < old.Result.Rows.Count && differences.Count < 3; index++) {
      var localRow = old.Result.Rows[index];
      var backupRow = candidate.Result.Rows[index];
      if (localRow.Confirmed != backupRow.Confirmed)
       differences.Add(string.Format(Loc.T("Riadok {0}: miestne {1}, zo zálohy {2}"), localRow.Record > 0 ? localRow.Record.ToString() : "—",
        old.Result.ConfirmedTotal < 0 ? "—" : localRow.Confirmed.ToString(),
        candidate.Result.ConfirmedTotal < 0 ? "—" : backupRow.Confirmed.ToString()));
     }
    }
    attempts[candidate.Entry.Id] = candidate;
    changed = attemptsChanged = true;
   }
   foreach (var candidate in incoming.Closures) {
    PrintBatchClosure old;
    if (closures.TryGetValue(candidate.ClosureId, out old)) {
     if (PrintBatchBackup.SameClosure(old, candidate)) continue;
     closureChanges++;
    }
    closures[candidate.ClosureId] = candidate;
    changed = true;
   }
   var active = incoming.Closures.Find(item => !item.IsOpen);
   var localActive = previous.Closures.Find(item => !item.IsOpen);
   bool closureStateConflict = (localActive == null && active != null) || (localActive != null && attemptsChanged);
   if (active != null) {
    foreach (var other in closures.Values.Where(item => !item.IsOpen && item.ClosureId != active.ClosureId).ToList()) {
     closures.Remove(other.ClosureId);
     closureChanges++;
    }
   }
   if (localActive != null && active == null && attemptsChanged && closures.Values.Any(item => !item.IsOpen)) {
    closures.Remove(localActive.ClosureId);
    closureChanges++;
   }
   bool conflict = results > 0 || closureChanges > 0 || closureStateConflict;
   bool apply = changed && (!conflict || replace);
   if (apply) {
    merged.Attempts = attempts.Values.OrderBy(item => item.Entry.Id, StringComparer.Ordinal).ToList();
    merged.Closures = closures.Values.OrderBy(item => item.ClosureId, StringComparer.Ordinal).ToList();
    merged.Validate(merged.BatchId);
    plan.BatchWrites.Add(merged);
   }
   var details = conflict ? string.Format(Loc.T("Rozdielne výsledky pokusov: {0}. Rozdielne uzávierky: {1}."), results, closureChanges) : null;
   if (differences.Count > 0) details += Environment.NewLine + string.Join(" · ", differences);
   if (closureChanges > 0 || closureStateConflict)
    details += Environment.NewLine + Loc.T("Pri nahradení zostane miestna verzia uzávierky v zálohe pred obnovou.");
   string balanceNote = ClosureBalanceNote(active, attempts.Values);
   if (balanceNote != null) details = string.IsNullOrEmpty(details) ? balanceNote : details + Environment.NewLine + balanceNote;
   plan.Items.Add(Item(incoming, previous, apply ? WorkspaceRestoreAction.Replace : WorkspaceRestoreAction.Keep, conflict, details));
  }

  static string ClosureBalanceNote(PrintBatchClosure closure, IEnumerable<RetainedPrintAttempt> attempts) {
   if (closure == null) return null;
   var original = attempts.FirstOrDefault(item => item.Entry.Id == closure.EntryId);
   if (original == null || original.Result == null || original.Result.ConfirmedTotal < 0)
    return Loc.T("Uložená uzávierka je dostupná. Súhrn dávky nemožno z prítomných pokusov overiť.");
   var reprints = attempts.Where(item => item.Entry.Id != closure.EntryId)
    .Select(item => new LinkedPrintResult {Id=item.Entry.Id,Result=item.Result}).ToList();
   var outcome = new PrintBatchOutcome(original.Result, reprints);
   var rows = original.Result.Rows;
   if (rows.Count == closure.Rows.Count && outcome.PendingReprints == closure.PendingReprints &&
       rows.Select((row,index) => {
        var saved = closure.Rows[index];
        return row.Record == saved.Record && row.Planned == saved.Planned && row.Confirmed == saved.Original &&
         outcome.Reprinted(row.Record) == saved.Reprinted && outcome.Remaining(row) == saved.Remaining;
       }).All(equal => equal)) return null;
   return string.Format(Loc.T("Zostatok po zlúčení: {0}. Zostatok v uloženej uzávierke: {1}. Údaje sa líšia."),
    outcome.RemainingTotal, closure.Remaining);
  }

  static WorkspaceRestoreItem Item(PrintBatchBackup incoming, PrintBatchBackup local, WorkspaceRestoreAction action,
   bool conflict, string details) {
   return new WorkspaceRestoreItem(incoming.BatchId, null, null, action) {
    BatchId = incoming.BatchId, BatchConflict = conflict, LocalBatchSummary = Summary(local), BackupBatchSummary = Summary(incoming),
    BatchDetails = details
   };
  }

  static string Summary(PrintBatchBackup batch) {
   if (batch == null) return "—";
   return string.Format(Loc.T("Pokusy {0} · Potvrdené {1} · Uzávierky {2}"), batch.Attempts.Count,
    batch.Attempts.Sum(item => item.Result == null ? 0L : Math.Max(0L, item.Result.ConfirmedTotal)), batch.Closures.Count);
  }
 }

 internal sealed class WorkspaceRestoreWrite {
  readonly string root, name;
  readonly byte[] before, after;
  readonly string beforeHash, afterHash;

  WorkspaceRestoreWrite(string root, string name, byte[] before, byte[] after) {
   this.root = root; this.name = name; this.before = before; this.after = after;
   beforeHash = before == null ? null : UpdateFiles.Digest(before);
   afterHash = after == null ? null : UpdateFiles.Digest(after);
  }

  internal static WorkspaceRestoreWrite ForWorkspace(string root, WorkspaceBackupFile before, WorkspaceBackupFile after) {
   return new WorkspaceRestoreWrite(root, after.Name, before == null ? null : before.Bytes, after.Bytes);
  }

  internal static WorkspaceRestoreWrite Capture(string root, string name, byte[] bytes) {
   string path = CheckedPath(root, name);
   if (Directory.Exists(path)) throw WorkspaceBackup.Changed();
   if (File.Exists(path) && new FileInfo(path).Length > 8 * 1024 * 1024) throw PrintBatchBackup.Invalid();
   return new WorkspaceRestoreWrite(root, name, File.Exists(path) ? File.ReadAllBytes(path) : null, bytes);
  }

  static string CheckedPath(string root, string name) {
   string path = UpdateFiles.Child(root, name);
   string current = path;
   while (current != null && current.Length >= root.Length) {
    if (Directory.Exists(current) || File.Exists(current)) UpdateFiles.NoReparse(current);
    if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
    current = Path.GetDirectoryName(current);
   }
   return path;
  }

  internal void Apply() { Replace(before, beforeHash, after); }
  internal void RollBack() { Replace(after, afterHash, before); }

  void Replace(byte[] expected, string hash, byte[] payload) {
   string path = CheckedPath(root, name);
   if (Directory.Exists(path) || File.Exists(path) != (expected != null) ||
       (expected != null && (new FileInfo(path).Length != expected.LongLength || UpdateFiles.Hash(path) != hash)))
    throw WorkspaceBackup.Changed();
   if (payload == null) { File.Delete(path); return; }
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   UpdateFiles.NoReparseParents(root, path);
   AtomicOutput.Save(path, temp => File.WriteAllBytes(temp, payload));
  }
 }
}
