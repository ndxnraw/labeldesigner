using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  void ShowPrintBatchCatalog(Form owner, PrintHistoryStore store, Action refreshHistory, PrintHistoryEntry preferred) {
   string preferredId = null;
   if (preferred != null && !RunWork(owner, "read-print-batch-id", "Načítavam prehľad dávky…",
        () => store.GetPrintBatchId(preferred.Id), out preferredId)) return;
   List<PrintBatchCatalogItem> batches;
   if (!RunWork(owner, "list-print-batches", "Načítavam dávky…", store.ListPrintBatches, out batches)) return;
   using (var dialog = FeatureForm("Prehľad dávok", 890, 500)) {
    dialog.MinimumSize = new Size(560, 365);
    var layout = ClosureLayout(dialog);
    var grid = ClosureGrid("printBatchCatalog", "ID dávky", "Čas pôvodnej tlače", "Tlačiareň");
    grid.AccessibleName = dialog.Text;
    grid.Columns[0].FillWeight = 155;
    layout.Controls.Add(grid, 0, 1);
    DataGridViewRow preferredRow = null;
    foreach (var batch in batches) {
     int index = grid.Rows.Add(batch.BatchId, batch.StartedUtc.HasValue ? batch.StartedUtc.Value.ToLocalTime().ToString("g") : "—",
      batch.Printer ?? "—");
     var row = grid.Rows[index];
     row.Tag = batch;
     if (batch.BatchId == preferredId) preferredRow = row;
    }
    var show = Btn(Loc.T("Zobraziť dávku…"), delegate {
     var selected = grid.CurrentRow == null ? null : grid.CurrentRow.Tag as PrintBatchCatalogItem;
     if (selected == null) return;
     try { ShowPrintBatchOverview(dialog, store, selected.BatchId, refreshHistory); }
     catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    show.Name = "openPrintBatchOverview";
    var close = Btn(Loc.T("Zavrieť"), dialog.Close);
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    actions.Controls.Add(show);
    actions.Controls.Add(close);
    layout.Controls.Add(actions, 0, 2);
    if (preferredRow != null) grid.CurrentCell = preferredRow.Cells[0];
    show.Enabled = grid.RowCount > 0;
    grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) show.PerformClick(); };
    dialog.AcceptButton = show;
    dialog.CancelButton = close;
    ApplyColors(dialog);
    dialog.ShowDialog(owner);
   }
  }

  void ShowPrintBatchOverview(Form owner, PrintHistoryStore store, string batchId, Action refreshHistory) {
   PrintBatchOverview current;
   if (!RunWork(owner, "read-print-batch", "Načítavam prehľad dávky…", () => store.OpenPrintBatch(batchId), out current)) return;
   using (var dialog = FeatureForm("Prehľad dávky", 1080, 570)) {
    dialog.MinimumSize = new Size(620, 390);
    var layout = ClosureLayout(dialog);
    var top = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
     AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
    var identity = new TextBox { ReadOnly = true, Text = batchId, Width = 350, AccessibleName = Loc.T("ID dávky") };
    var summary = new Label { AutoSize = true, MaximumSize = new Size(560, 0), Name = "printBatchOverviewSummary" };
    top.Controls.Add(new Label { Text = Loc.T("ID dávky"), AutoSize = true });
    top.Controls.Add(identity);
    top.Controls.Add(summary);
    layout.Controls.Add(top, 0, 0);
    var grid = ClosureGrid("printBatchAttempts", "Čas", "Typ pokusu", "Stav", "Plánované etikety", "Ručný výsledok", "História tlače");
    grid.AccessibleName = Loc.T("Pokusy o tlač");
    grid.Columns[0].FillWeight = 130;
    grid.Columns[4].FillWeight = 150;
    layout.Controls.Add(grid, 0, 1);
    Func<PrintHistoryEntry> selected = () => grid.CurrentRow == null ? null : grid.CurrentRow.Tag as PrintHistoryEntry;
    Action update = null;
    Action render = delegate {
     var previous = selected();
     DataGridViewRow restore = null;
     grid.Rows.Clear();
     foreach (var attempt in current.Attempts) {
      var entry = attempt.Entry;
      bool unavailable = entry.Retained && entry.StartedUtc == default(DateTime);
      int index = grid.Rows.Add(unavailable ? "—" : entry.StartedUtc.ToLocalTime().ToString("G"),
       Loc.T(string.IsNullOrEmpty(entry.RecoverySourceId) ? "Pôvodná tlač" : "Dotlač"),
       unavailable ? "—" : PrintHistoryStatusText(entry.Status), unavailable ? "—" : (object)((long)entry.Labels * entry.Repetitions), ManualResultText(entry),
       Loc.T(attempt.InHistory ? "V histórii" : "Trvalo uchované"));
      var row = grid.Rows[index];
      row.Tag = entry;
      if (previous != null && previous.Id == entry.Id) restore = row;
     }
     if (restore != null) grid.CurrentCell = restore.Cells[0];
     var outcome = current.Outcome;
     int pending = outcome == null ? current.Attempts.Count(item => !string.IsNullOrEmpty(item.Entry.RecoverySourceId) &&
      !item.Entry.ManualConfirmed.HasValue) : outcome.PendingReprints;
     summary.Text = outcome == null ? Loc.T("Počty pôvodnej tlače nie sú dostupné.") :
      string.Format(Loc.T("Plánované {0} · Z tejto úlohy {1} · Z dotlačí {2} · Zostávajúce {3}"),
       outcome.Result.Rows.Sum(row => row.Planned), Math.Max(0, outcome.Result.ConfirmedTotal), outcome.ReprintedTotal, outcome.RemainingTotal);
     if (outcome != null && outcome.Result.ConfirmedTotal < 0)
      summary.Text += Environment.NewLine + Loc.T("Výsledok čaká na potvrdenie.");
     if (pending > 0) summary.Text += Environment.NewLine + UnconfirmedReprints(pending);
     if (outcome != null && outcome.BatchClosed)
      summary.Text += Environment.NewLine + Loc.T("Dávka je uzavretá. Pred úpravou ju znova otvorte.");
     if (update != null) update();
    };
    Action refresh = delegate {
     current = store.OpenPrintBatch(batchId);
     render();
     refreshHistory();
    };
    var result = Btn(Loc.T("Potvrdiť výsledok…"), delegate {
     var entry = selected();
     if (entry == null || !entry.HasResultRows) return;
     try { EditBatchResult(dialog, store, entry, refresh); }
     catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    result.Name = "overviewPrintResult";
    var closeBatch = Btn(Loc.T("Uzavrieť dávku…"), delegate {
     var original = current.Attempts.Select(item => item.Entry).FirstOrDefault(item => item.Id == batchId &&
      string.IsNullOrEmpty(item.RecoverySourceId));
     if (original == null) return;
     try { ClosePrintBatch(dialog, store, original, refresh, true); }
     catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    var closures = Btn(Loc.T("Uzávierky dávok…"), delegate {
     try { ShowBatchClosures(dialog, store, refresh, batchId, true); }
     catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    closures.Name = "overviewBatchClosures";
    var close = Btn(Loc.T("Zavrieť"), dialog.Close);
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    foreach (var button in new[] { result, closeBatch, closures, close }) actions.Controls.Add(button);
    layout.Controls.Add(actions, 0, 2);
    update = delegate {
     var entry = selected();
     result.Enabled = entry != null && entry.HasResultRows;
     closeBatch.Enabled = current.Outcome != null && !current.Outcome.BatchClosed && current.Outcome.Result.ConfirmedTotal >= 0;
    };
    grid.CurrentCellChanged += delegate { update(); };
    dialog.CancelButton = close;
    render();
    ApplyColors(dialog);
    dialog.ShowDialog(owner);
   }
  }
 }
}
