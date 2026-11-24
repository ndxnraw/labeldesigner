using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  static DataGridView ClosureGrid(string name, params string[] headings) {
   var grid = new DataGridView { Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
    AllowUserToDeleteRows = false, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
   foreach (string label in headings) {
    int column = grid.Columns.Add("column" + grid.Columns.Count, Loc.T(label));
    grid.Columns[column].SortMode = DataGridViewColumnSortMode.NotSortable;
   }
   return grid;
  }

  static DataGridView ClosureRows(PrintBatchClosure snapshot) {
   var grid = ClosureGrid("batchClosureRows", "Zdrojový riadok", "Plánované etikety", "Potvrdené z tejto úlohy",
    "Potvrdené z dotlačí", "Zostávajúce etikety");
   grid.AccessibleName = Loc.T("Výsledok uzavretia dávky");
   grid.VirtualMode = true;
   grid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
    var row = snapshot.Rows[e.RowIndex];
    switch (e.ColumnIndex) {
     case 0: e.Value = row.Record > 0 ? (object)row.Record : "—"; break;
     case 1: e.Value = row.Planned; break;
     case 2: e.Value = row.Original; break;
     case 3: e.Value = row.Reprinted; break;
     case 4: e.Value = row.Remaining; break;
    }
   };
   grid.RowCount = snapshot.Rows.Count;
   return grid;
  }

  static TableLayoutPanel ClosureLayout(Form owner) {
   var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
   layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
   layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   owner.Controls.Add(layout);
   return layout;
  }

  static string ClosureTotals(PrintBatchClosure snapshot) {
   return string.Format(Loc.T("Plánované {0} · Z tejto úlohy {1} · Z dotlačí {2} · Zostávajúce {3}"),
    snapshot.Planned, snapshot.Original, snapshot.Reprinted, snapshot.Remaining);
  }

  void ClosePrintBatch(Form owner, PrintHistoryStore store, PrintHistoryEntry entry, Action refresh) {
   PrintBatchOutcome outcome;
   if (!RunWork(owner, "load-batch-closure", "Načítavam tlačovú úlohu…", () => store.OpenBatchOutcome(entry.Id), out outcome)) return;
   if (outcome.BatchClosed) { ShowBatchClosures(owner, store, refresh, entry.Id); return; }
   if (outcome.Result.ConfirmedTotal < 0) throw new InvalidOperationException(Loc.T("Najprv potvrďte výsledok pôvodnej tlače."));
   var sample = PrintBatchClosure.Capture(outcome, "");
   using (var dialog = FeatureForm("Uzavrieť dávku…", 850, 530)) {
    dialog.MinimumSize = new Size(590, 395);
    var layout = ClosureLayout(dialog);
    layout.Controls.Add(new Label { Text = ClosureTotals(sample), AutoSize = true, MaximumSize = new Size(520, 0) }, 0, 0);
    layout.Controls.Add(ClosureRows(sample), 0, 1);
    var footer = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
     AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
    layout.Controls.Add(footer, 0, 2);
    var acknowledge = new CheckBox { Name = "acceptPendingReprints", AutoSize = true, MaximumSize = new Size(525, 0),
     Text = Loc.T("Potvrďte uzavretie s nepotvrdenými dotlačami.") };
    if (sample.PendingReprints > 0) footer.Controls.Add(new Label { Text = UnconfirmedReprints(sample.PendingReprints), AutoSize = true });
    footer.Controls.Add(acknowledge);
    acknowledge.Visible = sample.PendingReprints > 0;
    var reason = new TextBox { Name = "batchClosureReason", Width = 490, MaxLength = PrintBatchClosure.MaxReasonLength,
     AccessibleName = Loc.T("Dôvod uzavretia so zostatkom") };
    if (sample.Remaining > 0) footer.Controls.Add(new Label { Text = reason.AccessibleName, AutoSize = true });
    footer.Controls.Add(reason);
    reason.Visible = sample.Remaining > 0;
    var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    var commit = Btn(Loc.T("Uzavrieť dávku"), delegate { dialog.DialogResult = DialogResult.OK; });
    commit.Name = "confirmBatchClosure";
    var cancel = Btn(Loc.T("Zrušiť"), dialog.Close);
    actions.Controls.Add(commit);
    actions.Controls.Add(cancel);
    footer.Controls.Add(actions);
    Action enable = () => commit.Enabled = (sample.PendingReprints == 0 || acknowledge.Checked) &&
     (sample.Remaining == 0 || !string.IsNullOrWhiteSpace(reason.Text));
    acknowledge.CheckedChanged += delegate { enable(); };
    reason.TextChanged += delegate { enable(); };
    enable();
    dialog.AcceptButton = commit;
    dialog.CancelButton = cancel;
    ApplyColors(dialog);
    if (dialog.ShowDialog(owner) != DialogResult.OK) return;
    string note = reason.Text;
    bool accept = acknowledge.Checked;
    PrintBatchClosure saved;
    if (!RunWork(owner, "save-batch-closure", "Ukladám uzávierku dávky…",
         () => store.CloseBatch(entry.Id, outcome.Version, note, accept), out saved)) return;
    refresh();
    ShowBatchClosures(owner, store, refresh, saved.EntryId);
   }
  }

  void ShowBatchClosures(Form owner, PrintHistoryStore store, Action refreshHistory, string preferred = null) {
   using (var dialog = FeatureForm("Uzávierky dávok…", 990, 555)) {
    dialog.MinimumSize = new Size(590, 390);
    var layout = ClosureLayout(dialog);
    layout.Controls.Add(new Label { Name = "batchClosureHistorySummary", AutoSize = true, MaximumSize = new Size(550, 0),
     Text = Loc.T("Uložené uzávierky sú dostupné aj po odstránení tlačovej úlohy z histórie.") }, 0, 0);
    var list = ClosureGrid("batchClosureHistory", "Čas uzavretia", "Dávka", "Stav", "Plánované etikety", "Zostávajúce etikety");
    list.AccessibleName = dialog.Text;
    list.Columns[0].FillWeight = 140;
    layout.Controls.Add(list, 0, 1);
    Func<PrintBatchClosure> selected = () => list.CurrentRow == null ? null : list.CurrentRow.Tag as PrintBatchClosure;
    Action update = null;
    Action refresh = delegate {
     var current = selected();
     string currentId = current == null ? null : current.ClosureId;
     DataGridViewRow restore = null;
     list.Rows.Clear();
     foreach (var item in store.ListBatchClosures()) {
      int index = list.Rows.Add(item.ClosedUtc.ToLocalTime().ToString("g"), item.EntryId.Substring(0, 8),
       Loc.T(item.IsOpen ? "Znova otvorená" : "Uzavretá"), item.Planned, item.Remaining);
      var row = list.Rows[index];
      row.Tag = item;
      if (item.ClosureId == currentId || (restore == null && currentId == null && item.EntryId == preferred)) restore = row;
     }
     if (restore != null) list.CurrentCell = restore.Cells[0];
     if (update != null) update();
    };
    var view = Btn(Loc.T("Zobraziť uzávierku…"), delegate {
     var item = selected();
     if (item == null) return;
     try {
      PrintBatchClosure snapshot;
      if (RunWork(dialog, "read-batch-closure", "Načítavam uzávierku dávky…",
           () => store.ReadBatchClosure(item.EntryId, item.ClosureId), out snapshot)) ShowClosureDetails(dialog, snapshot);
     } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    var export = Btn(Loc.T("Exportovať CSV…"), delegate {
     var item = selected();
     if (item == null) return;
     try {
      using (var pick = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", DefaultExt = "csv", AddExtension = true,
       FileName = "LabelDesigner-" + item.ClosedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv" }) {
       if (pick.ShowDialog(dialog) != DialogResult.OK) return;
       string path = pick.FileName;
       bool saved;
       RunWork(dialog, "export-batch-closure", "Exportujem výsledok dávky…", () => {
        store.ExportBatchClosure(item.EntryId, item.ClosureId, path);
        return true;
       }, out saved);
      }
     } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    var reopen = Btn(Loc.T("Znova otvoriť dávku…"), delegate {
     var item = selected();
     if (item == null || !item.CanReopen) return;
     using (var ask = UiMessageBox.Create(dialog, Loc.T("Znova otvoriť dávku na úpravu? Uložený stav uzávierky zostane dostupný."),
       dialog.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question)) {
      ask.AcceptButton = ask.CancelButton;
      if (ask.ShowDialog(dialog) != DialogResult.Yes) return;
     }
     try {
      bool opened;
      if (RunWork(dialog, "reopen-batch-closure", "Otváram dávku na úpravu…", () => {
       store.ReopenBatch(item.EntryId, item.ClosureId);
       return true;
      }, out opened)) { refresh(); refreshHistory(); }
     } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    reopen.Name = "reopenBatchClosure";
    view.Name = "viewBatchClosure";
    export.Name = "exportBatchClosure";
    var close = Btn(Loc.T("Zavrieť"), dialog.Close);
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    foreach (var item in new[] { view, export, reopen, close }) actions.Controls.Add(item);
    layout.Controls.Add(actions, 0, 2);
    update = delegate {
     var item = selected();
     view.Enabled = export.Enabled = item != null;
     reopen.Enabled = item != null && item.CanReopen;
    };
    list.CurrentCellChanged += delegate { update(); };
    dialog.CancelButton = close;
    refresh();
    ApplyColors(dialog);
    dialog.ShowDialog(owner);
   }
  }

  void ShowClosureDetails(Form owner, PrintBatchClosure snapshot) {
   using (var dialog = FeatureForm("Výsledok uzavretia dávky", 850, 505)) {
    var layout = ClosureLayout(dialog);
    string text = ClosureTotals(snapshot) + Environment.NewLine +
     Loc.T("Čas uzavretia") + " · " + snapshot.ClosedUtc.ToLocalTime().ToString("g");
    if (snapshot.PendingReprints > 0) text += Environment.NewLine + UnconfirmedReprints(snapshot.PendingReprints);
    if (!string.IsNullOrEmpty(snapshot.Reason)) text += Environment.NewLine + Loc.T("Dôvod uzavretia so zostatkom") + " · " + snapshot.Reason;
    layout.Controls.Add(new TextBox { Text = text, ReadOnly = true, Multiline = true, WordWrap = true, Dock = DockStyle.Fill,
     ScrollBars = ScrollBars.Vertical, Height = 126, AccessibleName = dialog.Text, Name = "batchClosureDetails" }, 0, 0);
    layout.Controls.Add(ClosureRows(snapshot), 0, 1);
    var close = Btn(Loc.T("Zavrieť"), dialog.Close);
    layout.Controls.Add(close, 0, 2);
    dialog.CancelButton = close;
    ApplyColors(dialog);
    dialog.ShowDialog(owner);
   }
  }
 }
}
