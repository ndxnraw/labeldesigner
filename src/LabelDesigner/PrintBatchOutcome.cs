using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EanEtikety {
 public sealed class PrintBatchOutcome {
  readonly Dictionary<int,long> reprinted = new Dictionary<int,long>();
  readonly string reprintVersion;
  public PrintBatchResult Result { get; internal set; }
  public bool BatchClosed { get; internal set; }
  internal readonly string[] LinkedEntries;
  public int PendingReprints { get; private set; }
  public long ReprintedTotal { get { return reprinted.Values.Sum(); } }
  public int RemainingTotal { get { return Result.Rows.Sum(row => Remaining(row)); } }
  public string Version { get { return Result.Revision + "|" + reprintVersion; } }

  public long Reprinted(int record) {
   long count;
   reprinted.TryGetValue(record, out count);
   return count;
  }

  public int Remaining(PrintBatchResultRow row) {
   return (int)Math.Max(0L, row.Remaining - Reprinted(row.Record));
  }

  internal PrintBatchOutcome(PrintBatchResult result, List<LinkedPrintResult> attempts) {
   Result = result;
   LinkedEntries = attempts.Select(attempt => attempt.Id).ToArray();
   reprintVersion = LinkedPrintResult.Version(attempts);
   foreach (var row in result.Rows) reprinted.Add(row.Record, 0);
   foreach (var attempt in attempts) {
    if (attempt.Pending) { PendingReprints++; continue; }
    foreach (var row in attempt.Result.Rows)
     if (reprinted.ContainsKey(row.Record)) reprinted[row.Record] += row.Confirmed;
   }
  }

  public PrintBatchResult ForProposal() {
   if (Result.ConfirmedTotal < 0) throw PrintBatchResult.Invalid();
   var effective = new PrintBatchResult { EntryId = Result.EntryId, Revision = Result.Revision };
   foreach (var row in Result.Rows)
    effective.Rows.Add(new PrintBatchResultRow { Record = row.Record, Planned = row.Planned, Confirmed = row.Planned - Remaining(row) });
   effective.ConfirmedTotal = effective.Rows.Sum(row => row.Confirmed);
   return effective;
  }
 }

 internal sealed class LinkedPrintResult {
  public string Id;
  public PrintBatchResult Result;
  public bool Pending { get { return Result == null || Result.ConfirmedTotal < 0; } }
  public static string Version(IEnumerable<LinkedPrintResult> attempts) {
   return string.Join("|", attempts.Select(item => item.Id + ":" + (item.Result == null ? null : item.Result.Revision)));
  }
 }

 internal sealed class PrintRecoveryAuthorization {
  public string RootId, FamilyVersion, ProposalVersion;
  public int PendingReprints;
  public bool Matches(PrintRecoveryAuthorization other) {
   return other != null && RootId == other.RootId && FamilyVersion == other.FamilyVersion && ProposalVersion == other.ProposalVersion;
  }
 }

 public sealed partial class PrintHistoryStore {
  public PrintBatchOutcome OpenBatchOutcome(string id) {
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    return ReadBatchOutcome(entry, ReadBatchResult(entry));
   }
  }

  public PrintBatchOutcome SaveBatchOutcome(string id, PrintBatchResult proposed) {
   if (proposed == null) throw new ArgumentNullException("proposed");
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    var stored = ReadBatchResult(entry);
    var outcome = ReadBatchOutcome(entry, stored);
    outcome.Result = SaveResult(entry, stored, proposed);
    return outcome.Result == null ? null : outcome;
   }
  }

  PrintBatchOutcome ReadBatchOutcome(PrintHistoryEntry entry, PrintBatchResult result) {
   var entries = ReadHistoryEntries();
   return new PrintBatchOutcome(result, ReadReprints(entry, entries)) { BatchClosed = IsBatchClosed(entry, RecoveryRoot(entry, entries).Id) };
  }

  List<PrintHistoryEntry> ReadHistoryEntries() {
   return EntryFiles().Take(MaxEntries).Select(TryReadRelatedEntry).Where(item => item != null).ToList();
  }

  List<LinkedPrintResult> ReadReprints(PrintHistoryEntry entry, IEnumerable<PrintHistoryEntry> entries) {
   var attempts = new List<LinkedPrintResult>();
   var children = entries.Where(item => !string.IsNullOrEmpty(item.RecoverySourceId))
    .ToLookup(item => item.RecoverySourceId, StringComparer.Ordinal);
   var visited = new HashSet<string>(StringComparer.Ordinal) { entry.Id };
   var parents = new Queue<PrintHistoryEntry>();
   parents.Enqueue(entry);
   while (parents.Count > 0) {
    WorkContext.Check();
    var parent = parents.Dequeue();
    foreach (var child in children[parent.Id].OrderBy(item => item.Id, StringComparer.Ordinal)) {
     if (child.RecoveredCopy < 1 || child.RecoveredCopy > parent.Repetitions || !visited.Add(child.Id)) continue;
     attempts.Add(new LinkedPrintResult { Id = child.Id, Result = TryReadReprintResult(child) });
     parents.Enqueue(child);
    }
   }
   return attempts;
  }

  internal PrintRecoveryAuthorization ReadRecoveryAuthorization(string id, bool proposal) {
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    var entries = ReadHistoryEntries();
    var root = RecoveryRoot(entry, entries);
    EnsureBatchOpen(entry, root.Id);
    var attempts = ReadReprints(root, entries);
    string proposalVersion = null;
    if (proposal) {
     var related = root.Id == id ? attempts : ReadReprints(entry, entries);
     proposalVersion = ReadBatchResult(entry).Revision + "|" + LinkedPrintResult.Version(related);
    }
    return new PrintRecoveryAuthorization { RootId = root.Id, FamilyVersion = LinkedPrintResult.Version(attempts),
     ProposalVersion = proposalVersion, PendingReprints = attempts.Count(item => item.Pending) };
   }
  }

  static PrintHistoryEntry RecoveryRoot(PrintHistoryEntry entry, IEnumerable<PrintHistoryEntry> entries) {
   if (string.IsNullOrEmpty(entry.RecoverySourceId)) return entry;
   var byId = entries.ToDictionary(item => item.Id, StringComparer.Ordinal);
   var root = entry;
   var visited = new HashSet<string>(StringComparer.Ordinal) { entry.Id };
   PrintHistoryEntry parent;
   while (!string.IsNullOrEmpty(root.RecoverySourceId) && byId.TryGetValue(root.RecoverySourceId, out parent) &&
          root.RecoveredCopy > 0 && root.RecoveredCopy <= parent.Repetitions && visited.Add(parent.Id))
    root = parent;
   return root;
  }

  internal IDisposable BeginRecoverySend(string id, PrintRecoveryAuthorization approved) {
   if (approved == null) throw new InvalidOperationException(Loc.T("Potvrďte dotlač v náhľade."));
   // The batch lock spans dispatch and recording. History can still be read during printing.
   var lease = new HistoryLease(mutexName + ".Reprint." + approved.RootId);
   try {
    if (!approved.Matches(ReadRecoveryAuthorization(id, approved.ProposalVersion != null)))
     throw new InvalidOperationException(Loc.T("Výsledok sa medzitým zmenil. Zatvorte okno a otvorte ho znova."));
    return lease;
   } catch { lease.Dispose(); throw; }
  }

  PrintHistoryEntry TryReadRelatedEntry(FileInfo file) {
   try {
    var entry = Read<PrintHistoryEntry>(file.FullName, 64 * 1024);
    return entry != null && file.FullName.Equals(PathFor(entry.Id, ".entry.xml"), StringComparison.OrdinalIgnoreCase) ? entry : null;
   } catch (InvalidOperationException) { return null; }
     catch (InvalidDataException) { return null; }
     catch (ArgumentException) { return null; }
     catch (IOException) { return null; }
     catch (UnauthorizedAccessException) { return null; }
  }

  PrintBatchResult TryReadReprintResult(PrintHistoryEntry entry) {
   // Legacy jobs without a result file have never been confirmed.
   try { return File.Exists(PathFor(entry.Id, ResultSuffix)) ? ReadBatchResult(entry) : null; }
   catch (InvalidOperationException) { return null; }
   catch (InvalidDataException) { return null; }
   catch (IOException) { return null; }
   catch (UnauthorizedAccessException) { return null; }
  }
 }
}
