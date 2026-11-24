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
   var live = ReadHistoryEntries();
   string batchId = ResolveBatchId(entry, live);
   return BatchOutcome(entry, result, batchId, BatchEntries(batchId, live));
  }

  PrintBatchOutcome BatchOutcome(PrintHistoryEntry entry, PrintBatchResult result, string batchId, List<PrintHistoryEntry> entries) {
   return new PrintBatchOutcome(result, ReadReprints(entry.Id, batchId, entries)) { BatchClosed = IsBatchClosed(entry, batchId) };
  }

  Dictionary<string,PrintHistoryEntry> ReadHistoryEntries() {
   return EntryFiles().Select(TryReadRelatedEntry).Where(item => item != null)
    .ToDictionary(item => item.Id, StringComparer.Ordinal);
  }

  List<LinkedPrintResult> ReadReprints(string id, string batchId, List<PrintHistoryEntry> entries) {
   var related = entries.Where(item => item.Id != id);
   if (id != batchId) {
    var children = entries.Where(item => !string.IsNullOrEmpty(item.RecoverySourceId))
     .ToLookup(item => item.RecoverySourceId, StringComparer.Ordinal);
    var visited = new HashSet<string>(StringComparer.Ordinal) { id };
    var parents = new Queue<string>();
    parents.Enqueue(id);
    while (parents.Count > 0) {
     WorkContext.Check();
     foreach (var child in children[parents.Dequeue()])
      if (visited.Add(child.Id)) parents.Enqueue(child.Id);
    }
    related = related.Where(item => visited.Contains(item.Id));
   }
   return related.OrderBy(item => item.Id, StringComparer.Ordinal)
    .Select(item => new LinkedPrintResult { Id = item.Id, Result = TryReadAttemptResult(item) }).ToList();
  }

  internal PrintRecoveryAuthorization ReadRecoveryAuthorization(string id, bool proposal) {
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    var live = ReadHistoryEntries();
    string batchId = ResolveBatchId(entry, live);
    EnsureBatchOpen(entry, batchId);
    var entries = BatchEntries(batchId, live);
    var attempts = ReadReprints(batchId, batchId, entries);
    string proposalVersion = null;
    if (proposal) {
     var related = batchId == id ? attempts : ReadReprints(id, batchId, entries);
     proposalVersion = ReadBatchResult(entry).Revision + "|" + LinkedPrintResult.Version(related);
    }
    return new PrintRecoveryAuthorization { RootId = batchId, FamilyVersion = LinkedPrintResult.Version(attempts),
     ProposalVersion = proposalVersion, PendingReprints = attempts.Count(item => item.Pending) };
   }
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

  PrintBatchResult TryReadAttemptResult(PrintHistoryEntry entry) {
   try { return ReadAvailableBatchResult(entry); }
   catch (InvalidOperationException) { return null; }
   catch (InvalidDataException) { return null; }
   catch (IOException) { return null; }
   catch (UnauthorizedAccessException) { return null; }
  }
 }
}
