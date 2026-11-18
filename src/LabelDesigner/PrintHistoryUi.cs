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
    return store.Record(entry, archive) ? warning : Loc.T("Obsah úlohy sa do histórie nepodarilo uložiť.");
   } catch (Exception ex) {
    DiagnosticLog.Write("print-history", ex);
    return Loc.T("Záznam do histórie tlače sa nepodarilo uložiť.");
   }
  }
 }

 public partial class MainForm {
  PrintHistoryStore LocalPrintHistory() {
   // Settings backups copy the workspace recursively. Keep optional print content separate.
   return workspace == null ? null : new PrintHistoryStore(startupPath + ".print-history");
  }

  static string PrintHistoryStatusText(PrintHistoryStatus status) {
   if (status == PrintHistoryStatus.Submitted) return Loc.T("Odoslané");
   if (status == PrintHistoryStatus.Cancelled) return Loc.T("Tlač zrušená");
   return Loc.T("Chyba odoslania");
  }

  void ShowPrintHistory() {
   var store = LocalPrintHistory();
   if (store == null) return;
   using (var window = FeatureForm("História tlače", 940, 560)) {
    window.MinimumSize = new Size(620, 380);
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    window.Controls.Add(layout);
    var hint = new Label { Text = Loc.T("Odoslané znamená prijatie úlohy tlačovým systémom."), AutoSize = true };
    layout.Controls.Add(hint, 0, 0);
    var table = new DataGridView {
     Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
     RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
     AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    table.AccessibleName = Loc.T("História tlače");
    foreach (string title in new[] { "Čas", "Tlačiareň", "Profil média", "Strany", "Etikety", "Stav", "Uložený obsah" })
     table.Columns.Add(title, Loc.T(title));
    layout.Controls.Add(table, 0, 1);
    Func<PrintHistoryEntry> selected = () => table.CurrentRow == null ? null : table.CurrentRow.Tag as PrintHistoryEntry;
    Action refresh = delegate {
     table.Rows.Clear();
     foreach (var entry in store.List()) {
      int row = table.Rows.Add(entry.StartedUtc.ToLocalTime().ToString("g"), entry.Printer, entry.Media,
       (long)entry.Pages * entry.Repetitions, (long)entry.Labels * entry.Repetitions,
       PrintHistoryStatusText(entry.Status), Loc.T(entry.HasArchive ? "Áno" : "Nie"));
      table.Rows[row].Tag = entry;
     }
    };
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    var repeat = Btn(Loc.T("Pripraviť opakovanie…"), delegate {
     var entry = selected();
     if (entry == null || !entry.HasArchive) return;
     try { RepeatPrintJob(window, store, entry); refresh(); }
     catch (Exception ex) { Error(ex); }
    });
    var remove = Btn(Loc.T("Odstrániť"), delegate {
     var entry = selected();
     if (entry == null) return;
     if (UiMessageBox.Show(window, Loc.T("Odstrániť tento záznam a jeho uložený obsah?"),
          window.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
     try { store.Remove(entry.Id); refresh(); } catch (Exception ex) { Error(ex); }
    });
    var close = Btn(Loc.T("Zavrieť"), window.Close);
    foreach (var action in new[] { repeat, remove, close }) actions.Controls.Add(action);
    layout.Controls.Add(actions, 0, 2);
    window.CancelButton = close;
    Action update = delegate {
     var entry = selected();
     repeat.Enabled = entry != null && entry.HasArchive;
     remove.Enabled = entry != null;
    };
    table.SelectionChanged += delegate { update(); };
    refresh();
    update();
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
