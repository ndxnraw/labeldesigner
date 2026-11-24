using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace EanEtikety {
 public enum PrintHistoryStatus { Failed, Cancelled, Submitted }

 public sealed class PrintHistoryEntry {
  public string Id, Printer, Media, QueueToken, RecoverySourceId;
  public DateTime StartedUtc;
  public int Pages, Labels, Repetitions, RecoveredCopy;
  public PrintHistoryStatus Status;
  public bool HasArchive;
  [XmlIgnore] public PrintQueueHistory QueueStatus;
  [XmlIgnore] public bool HasResultRows, ManualResultUnavailable;
  [XmlIgnore] public int? ManualConfirmed;
 }

 public sealed class ArchivedPlacement {
  public int Page, Record, LabelIndex, Slot;
  public RectangleF Bounds;
 }

 public sealed class ArchivedPrintJob {
  public LabelSettings Format;
  public PrinterProfile Profile;
  public int Repetitions;
  public double Width, Height;
  public DateTime? PreparedAt;
  public List<LabelSettings> Labels = new List<LabelSettings>();
  public List<ArchivedPlacement> Placements = new List<ArchivedPlacement>();

  public static ArchivedPrintJob Capture(LabelSettings format, OutputPlan plan, PrinterProfile profile, int repetitions) {
   var job = new ArchivedPrintJob {
    // The prepared artwork is in the placements. Keep only sheet geometry and DPI here.
    Format = new LabelSettings {
     Width = format.Width, Height = format.Height, Dpi = format.Dpi, UsePaper = format.UsePaper,
     Sheet = format.Sheet, PageWidth = format.PageWidth, PageHeight = format.PageHeight,
     MarginX = format.MarginX, MarginY = format.MarginY, MarginRight = format.MarginRight,
     MarginBottom = format.MarginBottom, Layout = format.Layout
    },
    Profile = profile, Repetitions = repetitions, Width = plan.Width, Height = plan.Height,
    PreparedAt = plan.PreparedAt
   };
   var indices = new Dictionary<LabelSettings,int>();
   foreach (var placement in plan.Placements) {
    int index;
    if (!indices.TryGetValue(placement.Label, out index)) {
     index = job.Labels.Count;
     indices.Add(placement.Label, index);
     job.Labels.Add(placement.Label);
    }
    job.Placements.Add(new ArchivedPlacement {
     Page = placement.Page, Record = placement.Record, Slot = placement.Slot, LabelIndex = index, Bounds = placement.Bounds
    });
   }
   return job;
  }

  static InvalidDataException InvalidArchive() {
   return new InvalidDataException(Loc.T("Uložená tlačová úloha je neplatná."));
  }

  static bool ValidSize(double value) { return value > 0 && value <= 10000; }

  public OutputPlan Select(string ranges, bool byRecord) {
   ValidateArchive();
   var available = new HashSet<int>();
   foreach (var item in Placements) available.Add(byRecord ? item.Record : item.Page + 1);
   var selected = ParseRanges(ranges, available);
   return BuildPlan((item, index) => selected.Contains(byRecord ? item.Record : item.Page + 1));
  }

  public OutputPlan ContinueCopy(int copy, int lastPrintedPage) {
   ValidateArchive();
   ValidateCopy(copy);
   if (lastPrintedPage < 0 || lastPrintedPage > Placements[Placements.Count - 1].Page)
    throw new ArgumentException(Loc.T("Na dotlač nezostala žiadna strana. Skontrolujte poslednú vytlačenú stranu."));
   return BuildPlan((item, index) => item.Page >= lastPrintedPage);
  }

  public OutputPlan SelectCopyLabels(int copy, ICollection<int> labelNumbers) {
   ValidateArchive();
   ValidateCopy(copy);
   if (labelNumbers == null || labelNumbers.Count == 0)
    throw new ArgumentException(Loc.T("Označte aspoň jednu etiketu na dotlač."));
   var selected = new HashSet<int>();
   foreach (int number in labelNumbers) {
    if (number < 1 || number > Placements.Count) throw new ArgumentOutOfRangeException("labelNumbers");
    selected.Add(number);
   }
   return BuildPlan((item, index) => selected.Contains(index + 1));
  }

  public int FirstIncompleteCopy(PrintBatchResult result) {
   var rows = RecoveryCounts(result);
   return rows.Values.Where(row => row.Remaining > 0)
    .Select(row => row.Confirmed / (row.Planned / Repetitions) + 1).DefaultIfEmpty(0).Min();
  }

  public OutputPlan ProposeRemainder(int copy, PrintBatchResult result) {
   ValidateCopy(copy);
   var rows = RecoveryCounts(result);
   var firstOccurrences = new Dictionary<int,int>();
   foreach (var row in rows.Values)
    firstOccurrences.Add(row.Record, Math.Max(0, row.Confirmed - (copy - 1) * (row.Planned / Repetitions)));
   return BuildPlan((item, index) => {
    int remaining = firstOccurrences[item.Record];
    if (remaining == 0) return true;
    firstOccurrences[item.Record] = remaining - 1;
    return false;
   });
  }

  Dictionary<int,PrintBatchResultRow> RecoveryCounts(PrintBatchResult result) {
   ValidateArchive();
   if (result == null || result.ConfirmedTotal < 0) throw PrintBatchResult.Invalid();
   result.Validate((long)Placements.Count * Repetitions);
   var archived = PrintBatchResult.FromRecords(Placements.Select(item => item.Record), Repetitions);
   if (archived.Rows.Count != result.Rows.Count) throw PrintBatchResult.Invalid();
   for (int i = 0; i < archived.Rows.Count; i++)
    if (archived.Rows[i].Record != result.Rows[i].Record || archived.Rows[i].Planned != result.Rows[i].Planned)
     throw PrintBatchResult.Invalid();
   return result.Rows.ToDictionary(row => row.Record);
  }

  void ValidateCopy(int copy) {
   if (copy < 1 || copy > Repetitions) throw new ArgumentOutOfRangeException("copy");
  }

  internal void ValidateArchive() {
   if (Format == null || Profile == null || Format.Layout == null || !ValidSize(Width) || !ValidSize(Height)
     || Repetitions < 1 || Repetitions > 32767 || Placements == null || Placements.Count < 1 || Placements.Count > 10000
     || Labels == null || Labels.Count < 1 || Labels.Count > Placements.Count || string.IsNullOrWhiteSpace(Profile.Printer)
     || Profile.Width < 1 || Profile.Width > 20000 || Profile.Height < 1 || Profile.Height > 20000)
    throw InvalidArchive();
   int previous = -1;
   foreach (var item in Placements) {
    WorkContext.Check();
    if (item == null || item.Page < previous || item.Page < 0 || item.Page >= 10000
      || item.LabelIndex < 0 || item.LabelIndex >= Labels.Count || Labels[item.LabelIndex] == null
      || !ValidSize(item.Bounds.Width) || !ValidSize(item.Bounds.Height)
      || !(item.Bounds.X >= 0 && item.Bounds.X <= 100000)
      || !(item.Bounds.Y >= 0 && item.Bounds.Y <= 100000)) throw InvalidArchive();
    previous = item.Page;
   }
  }

  OutputPlan BuildPlan(Func<ArchivedPlacement,int,bool> include) {
   var plan = new OutputPlan { Width = Width, Height = Height, PreparedAt = PreparedAt };
   var records = new HashSet<int>();
   int originalPage = -1, outputPage = -1;
   for (int i = 0; i < Placements.Count; i++) {
    WorkContext.Check();
    var item = Placements[i];
    if (!include(item, i)) continue;
    if (item.Page != originalPage) { originalPage = item.Page; outputPage++; }
    plan.Placements.Add(new OutputPlacement {
     Page = outputPage, Record = item.Record, Slot = item.Slot, Bounds = item.Bounds, Label = Labels[item.LabelIndex]
    });
    records.Add(item.Record);
   }
   plan.RecordCount = records.Count;
   return plan;
  }

  static HashSet<int> ParseRanges(string ranges, HashSet<int> available) {
   if (string.IsNullOrWhiteSpace(ranges)) return available;
   var selected = new HashSet<int>();
   var error = Loc.T("Zadajte dostupné čísla alebo rozsahy, napríklad 1,3-5.");
   if (ranges.Length > 4096) throw new ArgumentException(error);
   foreach (string part in ranges.Split(',')) {
    var ends = part.Split('-');
    int first, last;
    if (ends.Length > 2 || !int.TryParse(ends[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out first)
      || first < 1 || !available.Contains(first)) throw new ArgumentException(error);
    last = first;
    if (ends.Length == 2 && (!int.TryParse(ends[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out last)
      || last < first || !available.Contains(last))) throw new ArgumentException(error);
    foreach (int number in available) if (number >= first && number <= last) selected.Add(number);
   }
   return selected;
  }
 }

 public sealed partial class PrintHistoryStore {
  const int MaxArchiveBytes = 16 * 1024 * 1024;
  const int MaxEntries = 50;
  const long MaxTotalArchiveBytes = 128L * 1024 * 1024;
  readonly string directory;
  readonly string mutexName;

  public PrintHistoryStore(string directory) {
   this.directory = Path.GetFullPath(directory);
   string canonical = this.directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
   using (var sha = SHA256.Create())
    mutexName = "Local\\NDLabelDesigner.PrintHistory." + Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
     .Replace('/', '_').Replace('+', '-');
  }

  sealed class HistoryLease : IDisposable {
   readonly Mutex mutex;
   public HistoryLease(string name) {
    mutex = new Mutex(false, name);
    try {
     try {
      if (!mutex.WaitOne(TimeSpan.FromSeconds(3)))
       throw new IOException(Loc.T("Históriu tlače práve používa iná inštancia. Skúste to znova."));
     } catch (AbandonedMutexException) { /* Ownership has transferred to this process. */ }
    } catch { mutex.Dispose(); throw; }
   }
   public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
  }

  string PathFor(string id, string suffix) {
   Guid parsed;
   if (!Guid.TryParseExact(id, "N", out parsed)) throw new ArgumentException("Invalid history ID.");
   return Path.Combine(directory, parsed.ToString("N") + suffix);
  }

  public List<PrintHistoryEntry> List() {
   using (new HistoryLease(mutexName)) {
    CleanInterruptedWrites();
    var entries = new List<PrintHistoryEntry>();
    foreach (var file in EntryFiles().Take(MaxEntries)) {
     try {
      var entry = Read<PrintHistoryEntry>(file.FullName, 64 * 1024);
      if (entry == null || !string.Equals(file.FullName, PathFor(entry.Id, ".entry.xml"), StringComparison.OrdinalIgnoreCase)) continue;
      entry.HasArchive &= File.Exists(PathFor(entry.Id, ".job.xml"));
      if (PrintQueue.CanCheck(entry)) entry.QueueStatus = ReadQueue(entry);
      LoadResultSummary(entry);
      entries.Add(entry);
     } catch (InvalidOperationException) { }
       catch (InvalidDataException) { }
       catch (ArgumentException) { }
       catch (IOException) { }
    }
    return entries;
   }
  }

  IEnumerable<FileInfo> EntryFiles() {
   return Directory.Exists(directory)
    ? new DirectoryInfo(directory).EnumerateFiles("*.entry.xml").OrderByDescending(file => file.LastWriteTimeUtc)
    : Enumerable.Empty<FileInfo>();
  }

  static T Read<T>(string path, int limit) {
   using (var stream = File.OpenRead(path)) {
    if (stream.Length > limit) throw new InvalidDataException(Loc.T("Uložená tlačová úloha je neplatná."));
    using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
     DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limit
    })) return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
   }
  }

  public ArchivedPrintJob Open(string id) {
   using (new HistoryLease(mutexName)) {
    var job = Read<ArchivedPrintJob>(PathFor(id, ".job.xml"), MaxArchiveBytes);
    if (job == null || job.Placements == null)
     throw new InvalidDataException(Loc.T("Uložená tlačová úloha je neplatná."));
    return job;
   }
  }

  PrintQueueHistory ReadQueue(PrintHistoryEntry entry) {
   try {
    var path = PathFor(entry.Id, ".queue.xml");
    if (!File.Exists(path)) return null;
    var value = Read<PrintQueueHistory>(path, 16 * 1024);
    return value != null && value.Token == entry.QueueToken && value.LastCheck != null &&
     value.LastCheck.CheckedUtc.Kind == DateTimeKind.Utc &&
     (value.LastKnown == null || value.LastKnown.Outcome == PrintQueueOutcome.Found &&
      value.LastKnown.CheckedUtc.Kind == DateTimeKind.Utc && value.LastKnown.CheckedUtc <= value.LastCheck.CheckedUtc) ? value : null;
   } catch (InvalidOperationException) { return null; }
     catch (InvalidDataException) { return null; }
     catch (IOException) { return null; }
     catch (UnauthorizedAccessException) { return null; }
  }

  public bool UpdateQueue(string id, string token, PrintQueueObservation observation) {
   if (observation == null || observation.CheckedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("observation");
   using (new HistoryLease(mutexName)) {
    WorkContext.Check();
    var path = PathFor(id, ".entry.xml");
    if (!File.Exists(path)) return false;
    var entry = Read<PrintHistoryEntry>(path, 64 * 1024);
    if (!PrintQueue.CanCheck(entry) || entry.Id != id || entry.QueueToken != token) return false;
    var status = ReadQueue(entry) ?? new PrintQueueHistory { Token = token };
    bool latest = status.LastCheck == null || status.LastCheck.CheckedUtc < observation.CheckedUtc;
    bool known = observation.Outcome == PrintQueueOutcome.Found &&
     (status.LastKnown == null || status.LastKnown.CheckedUtc < observation.CheckedUtc);
    if (!latest && !known) return false;
    if (latest) status.LastCheck = observation;
    if (known) status.LastKnown = observation;
    WorkContext.Check();
    ProjectXml.Save(PathFor(id, ".queue.xml"), status);
    return true;
   }
  }

  public void Remove(string id) {
   using (new HistoryLease(mutexName)) DeleteEntry(id);
  }

  void DeleteEntry(string id) {
   File.Delete(PathFor(id, ".job.xml"));
   File.Delete(PathFor(id, ".queue.xml"));
   File.Delete(PathFor(id, ResultSuffix));
   File.Delete(PathFor(id, ".entry.xml"));
  }

  void CleanInterruptedWrites() {
   if (!Directory.Exists(directory)) return;
   foreach (string file in Directory.EnumerateFiles(directory, "*.tmp")) {
    string[] parts = Path.GetFileName(file).Split('.');
    Guid entry, operation;
    if (parts.Length != 5 || (parts[1] != "job" && parts[1] != "entry" && parts[1] != "queue" && parts[1] != "result") || parts[2] != "xml"
      || parts[4] != "tmp" || !Guid.TryParseExact(parts[0], "N", out entry)
      || !Guid.TryParseExact(parts[3], "N", out operation)) continue;
    try { File.Delete(file); }
    catch (IOException ex) { DiagnosticLog.Write("print-history-cleanup", ex); }
    catch (UnauthorizedAccessException ex) { DiagnosticLog.Write("print-history-cleanup", ex); }
   }
  }

  // Returns false if the optional content could not be saved. Metadata is still retained.
  public bool Record(PrintHistoryEntry entry, ArchivedPrintJob archive, OutputPlan rows = null) {
   using (new HistoryLease(mutexName)) {
    Directory.CreateDirectory(directory);
    CleanInterruptedWrites();
    entry.Id = Guid.NewGuid().ToString("N");
    if (!string.IsNullOrEmpty(entry.RecoverySourceId)) EnsureBatchOpen(entry, RecoveryRoot(entry, ReadHistoryEntries()).Id);
    string archivePath = PathFor(entry.Id, ".job.xml");
    // Publish metadata first. A crash before the atomic archive rename leaves
    // a visible metadata-only entry, and never an unlisted archive.
    entry.HasArchive = archive != null;
    ProjectXml.Save(PathFor(entry.Id, ".entry.xml"), entry);
    bool saved = false;
    if (archive != null) {
     try {
      AtomicOutput.Save(archivePath, temp => {
       using (var stream = new LimitedArchiveStream(temp))
        new XmlSerializer(typeof(ArchivedPrintJob)).Serialize(stream, archive);
      });
      saved = true;
     } catch (InvalidOperationException ex) { DiagnosticLog.Write("print-history-archive", ex); }
       catch (IOException ex) { DiagnosticLog.Write("print-history-archive", ex); }
       catch (UnauthorizedAccessException ex) { DiagnosticLog.Write("print-history-archive", ex); }
    }
    entry.HasArchive = saved;
    entry.HasResultRows = saved;
    Trim();
    if (File.Exists(PathFor(entry.Id, ".entry.xml"))) SavePlannedRows(entry, rows);
    return archive == null || saved;
   }
  }

  void Trim() {
   long bytes = 0;
   int count = 0;
   foreach (var file in EntryFiles().ToList()) {
    string id = file.Name.Substring(0, file.Name.Length - ".entry.xml".Length);
    Guid parsed;
    if (!Guid.TryParseExact(id, "N", out parsed)) continue;
    if (++count > MaxEntries) { DeleteEntry(id); continue; }
    var archive = new FileInfo(PathFor(id, ".job.xml"));
    if (!archive.Exists) continue;
    if (bytes + archive.Length > MaxTotalArchiveBytes) archive.Delete();
    else bytes += archive.Length;
   }
  }

  sealed class LimitedArchiveStream : FileStream {
   public LimitedArchiveStream(string path) : base(path, FileMode.CreateNew, FileAccess.Write) { }
   public override void Write(byte[] buffer, int offset, int count) {
    if (Position + count > MaxArchiveBytes) throw new IOException("Print archive size limit reached.");
    base.Write(buffer, offset, count);
   }
  }
 }
}
