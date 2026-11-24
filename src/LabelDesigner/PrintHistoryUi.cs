using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class PrintPanel {
  PrintHistoryStore printHistory;
  bool replaying;
  string recoverySourceId;
  int recoveredCopy;
  Action recoveryRecorded;
  Func<PrintRecoveryAuthorization> authorizeRecovery;
  PrintRecoveryAuthorization approvedRecovery;
  readonly CheckBox archivePrint = new CheckBox { AutoSize = true };

  internal void UsePrintHistory(PrintHistoryStore store) {
   printHistory = store;
   archivePrint.Visible = store != null && !calibrationTargetMode;
  }

  internal void RestorePrintSettings(ArchivedPrintJob job) {
   replaying = true;
   printBack.Text = Loc.T("Späť do histórie tlače");
   initialPrinterName = job.Profile.Printer;
   requestedProfile = job.Profile.Copy();
   copies.Value = job.Repetitions;
  }

  internal void SetPrintRecovery(string sourceId, int copy, Action recorded, Func<PrintRecoveryAuthorization> authorize) {
   recoverySourceId = sourceId;
   recoveredCopy = copy;
   recoveryRecorded = recorded;
   authorizeRecovery = authorize;
   copies.Value = 1;
   copies.Maximum = 1;
   copies.Enabled = false;
   UpdateOutputSummary();
  }

  void AddHistoryOption(TableLayoutPanel fields) {
   archivePrint.Text = Loc.T("Uložiť obsah úlohy lokálne na opakovanie");
   archivePrint.AccessibleDescription = Loc.T("Obsah etikiet sa uloží lokálne a bude dostupný v histórii tlače.");
   Add(fields, archivePrint);
   archivePrint.Visible = false;
  }

  static PrinterProfile HistoryProfile(PrinterSettings printer, PageSettings paper,
      string profileName, LabelPrintDocument doc, ZebraSettings zebra) {
   return new PrinterProfile {
    Printer = printer.PrinterName, Name = profileName, Paper = paper.PaperSize.PaperName,
    Width = paper.PaperSize.Width, Height = paper.PaperSize.Height, Landscape = paper.Landscape,
    X = doc.OffsetX, Y = doc.OffsetY, ScaleX = doc.ScaleX, ScaleY = doc.ScaleY,
    IgnoreDriverMargins = doc.IgnoreDriverMargins, ZebraZpl = zebra.Enabled,
    ZplDpi = zebra.Dpi, Darkness = zebra.Darkness
   };
  }

  static string RecordPrintAttempt(PrintHistoryStore store, PrintHistoryEntry entry, bool keepCopy,
      LabelSettings format, OutputPlan plan, PrinterProfile profile, PrintResult result) {
   if (entry == null || result.RequiresReview) return null;
   entry.Status = result.Sent ? PrintHistoryStatus.Submitted :
    result.Cancelled ? PrintHistoryStatus.Cancelled : PrintHistoryStatus.Failed;
   if (!result.Sent) entry.QueueToken = null;
   ArchivedPrintJob archive = null;
   string warning = null;
   if (keepCopy && profile != null) {
    try { archive = ArchivedPrintJob.Capture(format, plan, profile, entry.Repetitions); }
    catch (Exception ex) {
     DiagnosticLog.Write("print-history-archive", ex);
     warning = Loc.T("Obsah úlohy sa do histórie nepodarilo uložiť.");
    }
   }
   if (keepCopy && profile == null) warning = Loc.T("Obsah úlohy sa do histórie nepodarilo uložiť.");
   try {
    if (!store.Record(entry, archive, plan)) warning = Loc.T("Obsah úlohy sa do histórie nepodarilo uložiť.");
    if (plan.Placements.Count > 0 && !entry.HasResultRows)
     warning = (warning == null ? "" : warning + " ") + Loc.T("Počty po riadkoch sa nepodarilo uložiť.");
    return warning;
   } catch (Exception ex) {
    DiagnosticLog.Write("print-history", ex);
    return Loc.T("Záznam do histórie tlače sa nepodarilo uložiť.");
   }
  }
 }

 public partial class MainForm {
  Func<PrintHistoryEntry,PrintQueueObservation> historyQueueQuery = PrintQueue.CheckWindows;
  PrintHistoryStore LocalPrintHistory() {
   // Settings backups copy the workspace recursively. Keep optional print content separate.
   return workspace == null ? null : new PrintHistoryStore(startupPath + ".print-history");
  }

  static string PrintHistoryStatusText(PrintHistoryStatus status) {
   if (status == PrintHistoryStatus.Submitted) return Loc.T("Odoslané");
   if (status == PrintHistoryStatus.Cancelled) return Loc.T("Tlač zrušená");
   return Loc.T("Chyba odoslania");
  }

  static string PrintQueueStateText(PrintQueueState state) {
   switch (state) {
    case PrintQueueState.Waiting: return Loc.T("Vo fronte");
    case PrintQueueState.Spooling: return Loc.T("Zaraďuje sa do frontu");
    case PrintQueueState.Printing: return Loc.T("Prebieha tlač");
    case PrintQueueState.Paused: return Loc.T("Pozastavené");
    case PrintQueueState.Offline: return Loc.T("Tlačiareň offline");
    case PrintQueueState.PaperOut: return Loc.T("Chýba papier");
    case PrintQueueState.Attention: return Loc.T("Vyžaduje zásah");
    case PrintQueueState.Error: return Loc.T("Chyba vo fronte");
    case PrintQueueState.HandedOff: return Loc.T("Odovzdané podľa Windows");
    case PrintQueueState.Removed: return Loc.T("Odstraňuje sa alebo odstránené");
    default: return "—";
   }
  }

  static string PrintQueueCheckText(PrintHistoryEntry entry) {
   if (entry.Status != PrintHistoryStatus.Submitted) return "—";
   if (!PrintQueue.CanCheck(entry)) return Loc.T("Úlohu nemožno priradiť");
   var check = entry.QueueStatus == null ? null : entry.QueueStatus.LastCheck;
   if (check == null) return Loc.T("Front ešte nebol skontrolovaný");
   string status;
   switch (check.Outcome) {
    case PrintQueueOutcome.Found: status = Loc.T("Stav zistený"); break;
    case PrintQueueOutcome.Missing: status = Loc.T("Úloha sa vo fronte nenašla"); break;
    case PrintQueueOutcome.Ambiguous: status = Loc.T("Nejednoznačná úloha vo fronte"); break;
    default: status = Loc.T("Front je nedostupný"); break;
   }
   return status + " · " + check.CheckedUtc.ToLocalTime().ToString("g");
  }

  static string LastQueueStateText(PrintQueueObservation observation) {
   if (observation == null) return "—";
   if (observation.State == PrintQueueState.DriverReported) return observation.ProviderText ?? Loc.T("Neurčený stav frontu");
   if (observation.State == PrintQueueState.Unknown) return Loc.T("Neurčený stav frontu");
   return PrintQueueStateText(observation.State);
  }

  void ShowPrintHistory() {
   var store = LocalPrintHistory();
   if (store == null) return;
   using (var window = FeatureForm("História tlače", 1110, 560)) {
    window.MinimumSize = new Size(620, 380);
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    window.Controls.Add(layout);
    var hint = new Label { Text = Loc.T("História rozlišuje odoslanie úlohy a stav hlásený frontom Windows."),
     AutoSize = true, MaximumSize = new Size(580, 0) };
    layout.Controls.Add(hint, 0, 0);
    var table = new DataGridView {
     Name = "printHistoryGrid", Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
     RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
     AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    table.AccessibleName = Loc.T("História tlače");
    foreach (string title in new[] { "Čas", "Tlačiareň", "Profil média", "Strany", "Etikety", "Stav",
      "Posledný známy stav", "Čas zistenia", "Posledná kontrola frontu", "Uložený obsah", "Dotlač kópie", "Ručný výsledok" })
     table.Columns.Add(title, Loc.T(title));
    layout.Controls.Add(table, 0, 1);
    Func<PrintHistoryEntry> selected = () => table.CurrentRow == null ? null : table.CurrentRow.Tag as PrintHistoryEntry;
    Action update = null;
    Action refresh = delegate {
     var previous = selected();
     DataGridViewRow restoreRow = null;
     table.Rows.Clear();
     foreach (var entry in store.List()) {
      var known = entry.QueueStatus == null ? null : entry.QueueStatus.LastKnown;
      int row = table.Rows.Add(entry.StartedUtc.ToLocalTime().ToString("g"), entry.Printer, entry.Media,
       (long)entry.Pages * entry.Repetitions, (long)entry.Labels * entry.Repetitions,
       PrintHistoryStatusText(entry.Status), LastQueueStateText(known),
       known == null ? "—" : known.CheckedUtc.ToLocalTime().ToString("g"), PrintQueueCheckText(entry),
       Loc.T(entry.HasArchive ? "Áno" : "Nie"), entry.RecoveredCopy > 0 ? entry.RecoveredCopy.ToString() : "—", ManualResultText(entry));
      table.Rows[row].Tag = entry;
      if (previous != null && entry.Id == previous.Id) restoreRow = table.Rows[row];
     }
     if (restoreRow != null) table.CurrentCell = restoreRow.Cells[0];
     if (update != null) update();
    };
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    var repeat = Btn(Loc.T("Pripraviť opakovanie…"), delegate {
     var entry = selected();
     if (entry == null || !entry.HasArchive) return;
     try { RepeatPrintJob(window, store, entry); refresh(); }
     catch (Exception ex) { Error(ex); }
    });
    var recover = Btn(Loc.T("Pripraviť dotlač…"), delegate {
     var entry = selected();
     if (entry == null || !entry.HasArchive) return;
     try { RecoverPrintJob(window, store, entry, refresh); }
     catch (Exception ex) { Error(ex); }
    });
    Func<PrintHistoryEntry,DataGridViewRow> sourceRow = entry => entry == null || string.IsNullOrEmpty(entry.RecoverySourceId)
     ? null : table.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => row.Tag is PrintHistoryEntry
      && ((PrintHistoryEntry)row.Tag).Id == entry.RecoverySourceId);
    var source = Btn(Loc.T("Prejsť na zdrojovú úlohu"), delegate {
     var row = sourceRow(selected());
     if (row != null) { table.CurrentCell = row.Cells[0]; table.Focus(); }
    });
    recover.Name = "recoverPrintJob";
    source.Name = "recoverySourceJob";
    var outcome = Btn(Loc.T("Potvrdiť výsledok…"), delegate {
     var entry = selected();
     if (entry == null || !entry.HasResultRows) return;
     try { EditBatchResult(window, store, entry, refresh); }
     catch (Exception ex) { Error(ex); }
    });
    outcome.Name = "manualPrintResult";
    var closeBatch = Btn(Loc.T("Uzavrieť dávku…"), delegate {
     var entry = selected();
     if (entry == null || !entry.ManualConfirmed.HasValue || !string.IsNullOrEmpty(entry.RecoverySourceId)) return;
     try { ClosePrintBatch(window, store, entry, refresh); }
     catch (Exception ex) { UiMessageBox.Show(window, ex.Message, window.Text); }
    });
    closeBatch.Name = "closePrintBatch";
    var closures = Btn(Loc.T("Uzávierky dávok…"), delegate {
     try { ShowBatchClosures(window, store, refresh); }
     catch (Exception ex) { UiMessageBox.Show(window, ex.Message, window.Text); }
    });
    closures.Name = "listBatchClosures";
    var remove = Btn(Loc.T("Odstrániť"), delegate {
     var entry = selected();
     if (entry == null) return;
     string prompt = Loc.T("Odstrániť tento záznam a jeho uložený obsah?");
     if (!string.IsNullOrEmpty(entry.RecoverySourceId))
      prompt += Environment.NewLine + Loc.T("Odstránenie dotlače môže zmeniť zostatok pôvodnej dávky.");
     if (UiMessageBox.Show(window, prompt,
          window.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
     try { store.Remove(entry.Id); refresh(); } catch (Exception ex) { Error(ex); }
    });
    var close = Btn(Loc.T("Zavrieť"), window.Close);
    var checkQueue = Btn(Loc.T("Skontrolovať front tlače"), delegate {
     var entry = selected();
     if (!PrintQueue.CanCheck(entry)) return;
     try {
      bool saved;
      if (RunWork(window, "print-queue", "Kontrolujem front tlače…", () =>
          store.UpdateQueue(entry.Id, entry.QueueToken, historyQueueQuery(entry)), out saved)) refresh();
     } catch (Exception ex) { Error(ex); }
    });
    foreach (var action in new[] { checkQueue, outcome, closeBatch, closures, recover, repeat, source, remove, close }) actions.Controls.Add(action);
    layout.Controls.Add(actions, 0, 2);
    window.CancelButton = close;
    update = delegate {
     var entry = selected();
     repeat.Enabled = entry != null && entry.HasArchive;
     recover.Enabled = repeat.Enabled;
     outcome.Enabled = entry != null && entry.HasResultRows;
     closeBatch.Enabled = entry != null && entry.ManualConfirmed.HasValue && string.IsNullOrEmpty(entry.RecoverySourceId);
     source.Enabled = sourceRow(entry) != null;
     remove.Enabled = entry != null;
     checkQueue.Enabled = PrintQueue.CanCheck(entry);
    };
    table.CurrentCellChanged += delegate { update(); };
    refresh();
    ApplyColors(window);
    window.ShowDialog(this);
   }
  }

  void RepeatPrintJob(Form owner, PrintHistoryStore store, PrintHistoryEntry entry) {
   ArchivedPrintJob job;
   if (!RunWork(owner, "print-history-load", "Načítavam tlačovú úlohu…", () => store.Open(entry.Id), out job)) return;
   OutputPlan plan = null;
   using (var selection = FeatureForm("Pripraviť opakovanie…", 600, 360)) {
    var stack = FeatureStack(selection);
    var mode = Choice(stack, "Vybrať podľa", new[] { "Strany", "Záznamy dávky" }, 0);
    mode.Enabled = job.Placements.Any(place => place != null && place.Record > 0);
    var available = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 56 };
    StackField(stack, Loc.T("Dostupné čísla"), available);
    var range = Input(stack, "Výber, napríklad 1,3-5. Prázdne znamená všetko.", "");
    range.MaxLength = 4096;
    Action update = delegate {
     var ids = job.Placements.Where(place => place != null)
      .Select(place => mode.SelectedIndex == 1 ? place.Record : place.Page + 1).Distinct().OrderBy(id => id).ToArray();
     available.Text = string.Join(", ", ids);
    };
    mode.SelectedIndexChanged += delegate { update(); };
    update();
    var actions = new FlowLayoutPanel { AutoSize = true };
    var next = Btn(Loc.T("Otvoriť náhľad"), delegate {
     try {
      plan = job.Select(range.Text, mode.SelectedIndex == 1);
      selection.DialogResult = DialogResult.OK;
     } catch (Exception ex) { UiMessageBox.Show(selection, ex.Message, selection.Text); }
    });
    var cancel = Btn(Loc.T("Zrušiť"), delegate { selection.DialogResult = DialogResult.Cancel; });
    actions.Controls.Add(next);
    actions.Controls.Add(cancel);
    StackAdd(stack, actions);
    selection.AcceptButton = next;
    selection.CancelButton = cancel;
    ApplyColors(selection);
    if (selection.ShowDialog(owner) != DialogResult.OK) return;
   }
   using (var previewWindow = CreateJobPrintWindow(job.Format, plan, null, job))
    previewWindow.ShowDialog(owner);
  }
 }
}
