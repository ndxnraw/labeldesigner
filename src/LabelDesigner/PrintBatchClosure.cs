using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace EanEtikety {
 public sealed class PrintBatchClosureRow {
  [XmlAttribute] public int Record, Planned, Original, Remaining;
  [XmlAttribute] public long Reprinted;
 }

 public sealed class PrintBatchClosure {
  public const int MaxReasonLength = 500;
  [XmlAttribute] public string EntryId, ClosureId, Reason, LinkedEntries;
  [XmlAttribute] public DateTime ClosedUtc, ReopenedUtc;
  [XmlAttribute] public int Planned, Original, Remaining, PendingReprints;
  [XmlAttribute] public long Reprinted;
  [XmlElement("Row")] public List<PrintBatchClosureRow> Rows = new List<PrintBatchClosureRow>();
  [XmlIgnore] public bool CanReopen;
  [XmlIgnore] public bool IsOpen { get { return ReopenedUtc != default(DateTime); } }
  public bool ShouldSerializeReopenedUtc() { return IsOpen; }

  internal static PrintBatchClosure Capture(PrintBatchOutcome outcome, string reason) {
   var snapshot = new PrintBatchClosure {
    EntryId = outcome.Result.EntryId, ClosureId = Guid.NewGuid().ToString("N"), ClosedUtc = DateTime.UtcNow,
    Reason = reason, PendingReprints = outcome.PendingReprints, CanReopen = true,
    LinkedEntries = string.Join(",", outcome.LinkedEntries)
   };
   foreach (var row in outcome.Result.Rows) {
    WorkContext.Check();
    var saved = new PrintBatchClosureRow { Record = row.Record, Planned = row.Planned, Original = row.Confirmed,
     Reprinted = outcome.Reprinted(row.Record), Remaining = outcome.Remaining(row) };
    snapshot.Rows.Add(saved);
    snapshot.Planned += saved.Planned;
    snapshot.Original += saved.Original;
    snapshot.Reprinted += saved.Reprinted;
    snapshot.Remaining += saved.Remaining;
   }
   return snapshot;
  }

  internal void ValidateHeader() {
   Guid id;
   if (!Guid.TryParseExact(EntryId, "N", out id) || !Guid.TryParseExact(ClosureId, "N", out id) ||
       ClosedUtc.Kind != DateTimeKind.Utc || (IsOpen && (ReopenedUtc.Kind != DateTimeKind.Utc || ReopenedUtc < ClosedUtc)) ||
       Planned < 1 || Original < 0 || Original > Planned || Remaining < 0 || Remaining > Planned - Original ||
       Reprinted < 0 || PendingReprints < 0 || (Reason != null && Reason.Length > MaxReasonLength) ||
       (Remaining > 0 && string.IsNullOrWhiteSpace(Reason))) throw Invalid();
   if (!string.IsNullOrEmpty(LinkedEntries)) {
    var linked = LinkedEntries.Split(',');
    if (linked.Any(value => value == EntryId || !Guid.TryParseExact(value, "N", out id)) ||
        linked.Distinct(StringComparer.Ordinal).Count() != linked.Length) throw Invalid();
   }
  }

  internal bool Includes(string id) {
   return !string.IsNullOrEmpty(LinkedEntries) && LinkedEntries.Split(',').Contains(id, StringComparer.Ordinal);
  }

  internal void Validate() {
   ValidateHeader();
   if (Rows == null || Rows.Count == 0 || Rows.Count > OutputPlan.MaxLabels) throw Invalid();
   long planned = 0, original = 0, reprinted = 0, remaining = 0;
   int last = -1;
   foreach (var row in Rows) {
    WorkContext.Check();
    if (row == null || row.Record <= last || row.Planned < 1 || row.Original < 0 || row.Original > row.Planned ||
        row.Reprinted < 0 || row.Reprinted > Reprinted - reprinted ||
        row.Remaining != Math.Max(0L, row.Planned - row.Original - row.Reprinted)) throw Invalid();
    last = row.Record;
    planned += row.Planned;
    original += row.Original;
    reprinted += row.Reprinted;
    remaining += row.Remaining;
   }
   if (planned != Planned || original != Original || reprinted != Reprinted || remaining != Remaining) throw Invalid();
  }

  internal static InvalidDataException Invalid() { return new InvalidDataException(Loc.T("Uložená uzávierka dávky je neplatná.")); }
 }

 public sealed partial class PrintHistoryStore {
  const int MaxClosureBytes = 4 * 1024 * 1024;
  string ClosureDirectory { get { return Path.Combine(directory, "batch-closures"); } }

  string ClosurePath(string entryId, string closureId) {
   Guid entry, closure;
   if (!Guid.TryParseExact(entryId, "N", out entry) || !Guid.TryParseExact(closureId, "N", out closure)) throw PrintBatchClosure.Invalid();
   return Path.Combine(ClosureDirectory, entry.ToString("N") + "." + closure.ToString("N") + ".xml");
  }

  IEnumerable<string> ClosureFiles(string pattern) {
   return Directory.Exists(ClosureDirectory) ? Directory.EnumerateFiles(ClosureDirectory, pattern) : Enumerable.Empty<string>();
  }

  PrintBatchClosure ReadClosureHeader(string path) {
   using (var stream = File.OpenRead(path)) {
    if (stream.Length > MaxClosureBytes) throw PrintBatchClosure.Invalid();
    using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
     XmlResolver = null, MaxCharactersInDocument = MaxClosureBytes })) {
     reader.MoveToContent();
     if (reader.Name != "PrintBatchClosure") throw PrintBatchClosure.Invalid();
     string reopened = reader.GetAttribute("ReopenedUtc");
     var header = new PrintBatchClosure {
      EntryId = reader.GetAttribute("EntryId"), ClosureId = reader.GetAttribute("ClosureId"), Reason = reader.GetAttribute("Reason"),
      LinkedEntries = reader.GetAttribute("LinkedEntries"),
      ClosedUtc = XmlConvert.ToDateTime(reader.GetAttribute("ClosedUtc"), XmlDateTimeSerializationMode.RoundtripKind),
      ReopenedUtc = reopened == null ? default(DateTime) : XmlConvert.ToDateTime(reopened, XmlDateTimeSerializationMode.RoundtripKind),
      Planned = XmlConvert.ToInt32(reader.GetAttribute("Planned")), Original = XmlConvert.ToInt32(reader.GetAttribute("Original")),
      Reprinted = XmlConvert.ToInt64(reader.GetAttribute("Reprinted")), Remaining = XmlConvert.ToInt32(reader.GetAttribute("Remaining")),
      PendingReprints = XmlConvert.ToInt32(reader.GetAttribute("PendingReprints"))
     };
     header.ValidateHeader();
     if (!string.Equals(path, ClosurePath(header.EntryId, header.ClosureId), StringComparison.OrdinalIgnoreCase))
      throw PrintBatchClosure.Invalid();
     header.CanReopen = !header.IsOpen && HasBatchOriginal(header.EntryId);
     return header;
    }
   }
  }

  bool IsBatchClosed(string id) {
   Guid parsed;
   if (!Guid.TryParseExact(id, "N", out parsed)) throw PrintBatchClosure.Invalid();
   return ClosureFiles(parsed.ToString("N") + ".*.xml").Any(path => !ReadClosureHeader(path).IsOpen);
  }

  bool IsBatchClosed(PrintHistoryEntry entry, string rootId) {
   if (IsBatchClosed(rootId)) return true;
   // A saved closure retains its members even after one of the parent entries disappears from history.
   return !IsHistoryId(entry.BatchId) && !string.IsNullOrEmpty(entry.RecoverySourceId) &&
    ReadClosureHeaders().Any(item => !item.IsOpen && (item.EntryId == entry.RecoverySourceId ||
     item.Includes(entry.Id) || item.Includes(entry.RecoverySourceId)));
  }

  void EnsureBatchOpen(PrintHistoryEntry entry, string rootId) {
   if (IsBatchClosed(entry, rootId)) throw new InvalidOperationException(Loc.T("Dávka je uzavretá. Pred úpravou ju znova otvorte."));
  }

  public PrintBatchClosure CloseBatch(string id, string expectedVersion, string reason, bool acceptPending) {
   Guid parsed;
   if (!Guid.TryParseExact(id, "N", out parsed)) throw PrintBatchClosure.Invalid();
   using (new HistoryLease(mutexName + ".Reprint." + parsed.ToString("N")))
   using (new HistoryLease(mutexName)) {
    var entry = ReadResultEntry(id);
    if (!string.IsNullOrEmpty(entry.RecoverySourceId)) throw PrintBatchClosure.Invalid();
    EnsureBatchOpen(entry, id);
    var result = ReadBatchResult(entry);
    if (result.ConfirmedTotal < 0) throw new InvalidOperationException(Loc.T("Najprv potvrďte výsledok pôvodnej tlače."));
    var outcome = ReadBatchOutcome(entry, result);
    if (outcome.Version != expectedVersion)
     throw new InvalidOperationException(Loc.T("Výsledok sa medzitým zmenil. Zatvorte okno a otvorte ho znova."));
    if (outcome.PendingReprints > 0 && !acceptPending)
     throw new InvalidOperationException(Loc.T("Potvrďte uzavretie s nepotvrdenými dotlačami."));
    reason = (reason ?? "").Trim();
    if (reason.Length > PrintBatchClosure.MaxReasonLength || (outcome.RemainingTotal > 0 && reason.Length == 0))
     throw new InvalidOperationException(Loc.T("Zadajte dôvod uzavretia so zostatkom."));
    var snapshot = PrintBatchClosure.Capture(outcome, reason);
    snapshot.Validate();
    Directory.CreateDirectory(ClosureDirectory);
    SaveClosure(snapshot);
    return snapshot;
   }
  }

  public List<PrintBatchClosure> ListBatchClosures() {
   using (new HistoryLease(mutexName)) return ReadClosureHeaders().OrderByDescending(item => item.ClosedUtc).ToList();
  }

  IEnumerable<PrintBatchClosure> ReadClosureHeaders() {
   foreach (string path in ClosureFiles("*.xml")) {
    WorkContext.Check();
    var header = TryReadClosureHeader(path);
    if (header != null) yield return header;
   }
  }

  PrintBatchClosure TryReadClosureHeader(string path) {
   try { return ReadClosureHeader(path); }
   catch (InvalidDataException) { return null; }
   catch (IOException) { return null; }
   catch (UnauthorizedAccessException) { return null; }
   catch (XmlException) { return null; }
   catch (FormatException) { return null; }
   catch (OverflowException) { return null; }
   catch (ArgumentException) { return null; }
  }

  void SaveClosure(PrintBatchClosure snapshot) {
   AtomicOutput.Save(ClosurePath(snapshot.EntryId, snapshot.ClosureId), temp => {
    using (var stream = File.Create(temp)) new XmlSerializer(typeof(PrintBatchClosure)).Serialize(stream, snapshot);
   });
  }

  public PrintBatchClosure ReadBatchClosure(string entryId, string closureId) {
   using (new HistoryLease(mutexName)) return LoadClosure(entryId, closureId);
  }

  PrintBatchClosure LoadClosure(string entryId, string closureId) {
   var snapshot = Read<PrintBatchClosure>(ClosurePath(entryId, closureId), MaxClosureBytes);
   if (snapshot == null || snapshot.EntryId != entryId || snapshot.ClosureId != closureId) throw PrintBatchClosure.Invalid();
   snapshot.Validate();
   snapshot.CanReopen = !snapshot.IsOpen && HasBatchOriginal(entryId);
   return snapshot;
  }

  bool HasBatchOriginal(string id) {
   return File.Exists(PathFor(id, ".entry.xml")) || File.Exists(RetainedPath(id, id));
  }

  public void ReopenBatch(string entryId, string closureId) {
   using (new HistoryLease(mutexName)) {
    var snapshot = LoadClosure(entryId, closureId);
    if (snapshot.IsOpen) return;
    if (!snapshot.CanReopen) throw new InvalidOperationException(Loc.T("Pôvodná úloha ani jej uchované údaje nie sú dostupné."));
    var original = ReadResultEntry(entryId);
    if (original.Retained && original.RetainedResult == null) throw PrintBatchResult.Invalid();
    var now = DateTime.UtcNow;
    snapshot.ReopenedUtc = now < snapshot.ClosedUtc ? snapshot.ClosedUtc : now;
    SaveClosure(snapshot);
   }
  }

  public void ExportBatchClosure(string entryId, string closureId, string path) {
   var snapshot = ReadBatchClosure(entryId, closureId);
   string target = Path.GetFullPath(path);
   if (!string.Equals(Path.GetExtension(target), ".csv", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException(Loc.T("Výsledok uložte do súboru CSV."));
   string reason = snapshot.Reason ?? "";
   string leading = reason.TrimStart();
   if (leading.Length > 0 && "=+-@".IndexOf(leading[0]) >= 0) reason = "'" + reason;
   reason = "\"" + reason.Replace("\"", "\"\"") + "\"";
   AtomicOutput.Save(target, temp => {
    using (var writer = new StreamWriter(temp, false, new UTF8Encoding(true))) {
     writer.WriteLine("source_record,planned,confirmed_original,confirmed_reprints,remaining,batch_id,closure_id,closed_utc,pending_reprints,reason");
     foreach (var row in snapshot.Rows) {
      WorkContext.Check();
      writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7:O},{8},{9}",
       row.Record == 0 ? "" : row.Record.ToString(CultureInfo.InvariantCulture), row.Planned, row.Original, row.Reprinted,
       row.Remaining, snapshot.EntryId, snapshot.ClosureId, snapshot.ClosedUtc, snapshot.PendingReprints, reason));
     }
    }
   });
  }
 }
}
