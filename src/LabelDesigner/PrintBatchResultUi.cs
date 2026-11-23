using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  static string ManualResultText(PrintHistoryEntry entry) {
   if (entry.ManualResultUnavailable) return Loc.T("Výsledok nedostupný");
   if (!entry.ManualConfirmed.HasValue)
    return entry.HasResultRows ? Loc.T("Nepotvrdené") : "—";
   int count = entry.ManualConfirmed.Value;
   long planned = (long)entry.Labels * entry.Repetitions;
   return ResultStatus(count, planned) + " · " + count + " / " + planned;
  }

  static string ResultStatus(int confirmed, long planned) {
   return Loc.T(confirmed == 0 ? "Bez použiteľných etikiet" : confirmed == planned ? "Dokončené" : "Čiastočne dokončené");
  }

  void EditBatchResult(Form owner, PrintHistoryStore store, PrintHistoryEntry entry, Action refresh) {
   PrintBatchResult result;
   if (!RunWork(owner, "read-print-result", "Načítavam tlačovú úlohu…", () => store.OpenBatchResult(entry.Id), out result)) return;
   PrintBatchResult proposal = null;
   using (var dialog = FeatureForm("Ručný výsledok tlače", 880, 570)) {
    dialog.MinimumSize = new Size(580, 380);
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(12) };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    dialog.Controls.Add(layout);
    layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(530, 0),
     Text = Loc.T("Zadajte počet použiteľných etikiet podľa skutočných výtlačkov zo všetkých kópií tejto úlohy.") }, 0, 0);
    var grid = new DataGridView { Name = "manualPrintRows", Dock = DockStyle.Fill, VirtualMode = true,
     AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, MultiSelect = false,
     AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
     AccessibleName = dialog.Text };
    foreach (string label in new[] { "Zdrojový riadok", "Plánované etikety", "Potvrdené etikety", "Zostávajúce etikety" }) {
     int index = grid.Columns.Add("result" + grid.Columns.Count, Loc.T(label));
     grid.Columns[index].SortMode = DataGridViewColumnSortMode.NotSortable;
     grid.Columns[index].ReadOnly = index != 2;
    }
    var summary = new Label { Name = "manualPrintSummary", AutoSize = true, MaximumSize = new Size(540, 0) };
    layout.Controls.Add(summary, 0, 2);
    bool changed = false, invalid = false;
    int planned = result.Rows.Sum(row => row.Planned);
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    layout.Controls.Add(actions, 0, 3);
    Action update = null;
    var save = Btn(Loc.T("Uložiť výsledok"), delegate {
     if (grid.IsCurrentCellInEditMode && grid.CurrentCell.ColumnIndex == 2) {
      int count;
      invalid = !ParseConfirmed(grid.CurrentCell.EditedFormattedValue, result.Rows[grid.CurrentCell.RowIndex], out count);
      if (invalid) { update(); return; }
     }
     if (!grid.EndEdit() || invalid) return;
     try {
      var candidate = result;
      PrintBatchResult saved;
      if (!RunWork(dialog, "save-print-result", "Ukladám výsledok tlače…", () => {
       var committed = store.SaveBatchResult(entry.Id, candidate);
       if (committed == null)
        throw new InvalidOperationException(Loc.T("Výsledok sa medzitým zmenil. Zatvorte okno a otvorte ho znova."));
       return committed;
      }, out saved)) return;
      result = saved;
      changed = false;
      grid.Invalidate();
      refresh();
      update();
     } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
    });
    var propose = Btn(Loc.T("Navrhnúť dotlač…"), delegate {
     if (!grid.EndEdit() || changed || invalid || result.ConfirmedTotal < 0 || result.ConfirmedTotal >= planned) return;
     proposal = result;
     dialog.Close();
    });
    var close = Btn(Loc.T("Zavrieť"), dialog.Close);
    close.CausesValidation = false;
    Action<bool> fill = all => {
     grid.CancelEdit();
     invalid = false;
     foreach (var row in result.Rows) row.Confirmed = all ? row.Planned : 0;
     changed = true;
     grid.Invalidate();
     update();
    };
    var allButton = Btn(Loc.T("Vyplniť všetky počty"), () => fill(true));
    var zeroButton = Btn(Loc.T("Vynulovať všetky počty"), () => fill(false));
    allButton.CausesValidation = zeroButton.CausesValidation = false;
    foreach (var action in new[] { allButton, zeroButton, save, propose, close }) actions.Controls.Add(action);
    update = delegate {
     int confirmed = result.Rows.Sum(row => row.Confirmed);
     summary.Text = string.Format(Loc.T("Plánované {0} · Potvrdené {1} · Zostávajúce {2}"), planned, confirmed, planned - confirmed);
     if (invalid) summary.Text += Environment.NewLine + Loc.T("Zadajte celé číslo od nuly po plánované množstvo.");
     else if (changed || result.ConfirmedTotal < 0) summary.Text += Environment.NewLine + Loc.T("Výsledok čaká na potvrdenie.");
     else summary.Text += Environment.NewLine + ResultStatus(confirmed, planned);
     save.Enabled = grid.IsCurrentCellDirty || !invalid && (changed || result.ConfirmedTotal < 0);
     propose.Enabled = entry.HasArchive && !grid.IsCurrentCellDirty && !changed && !invalid &&
      result.ConfirmedTotal >= 0 && result.ConfirmedTotal < planned;
    };
    grid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
     if (e.RowIndex < 0 || e.RowIndex >= result.Rows.Count) return;
     var row = result.Rows[e.RowIndex];
     switch (e.ColumnIndex) {
      case 0: e.Value = row.Record > 0 ? (object)row.Record : "—"; break;
      case 1: e.Value = row.Planned; break;
      case 2: e.Value = row.Confirmed; break;
      case 3: e.Value = row.Remaining; break;
     }
    };
    grid.CellValidating += delegate(object sender, DataGridViewCellValidatingEventArgs e) {
     if (e.ColumnIndex != 2 || e.RowIndex < 0) return;
     int value;
     invalid = !ParseConfirmed(e.FormattedValue, result.Rows[e.RowIndex], out value);
     e.Cancel = invalid;
     update();
    };
    grid.CellValuePushed += delegate(object sender, DataGridViewCellValueEventArgs e) {
     if (e.ColumnIndex != 2 || e.RowIndex < 0) return;
     var row = result.Rows[e.RowIndex];
     int value;
     if (!ParseConfirmed(e.Value, row, out value)) return;
     invalid = false;
     changed |= row.Confirmed != value;
     row.Confirmed = value;
     grid.InvalidateCell(3, e.RowIndex);
     update();
    };
    grid.CellEndEdit += delegate { if (invalid) { invalid = false; update(); } };
    grid.CurrentCellDirtyStateChanged += delegate { update(); };
    grid.RowCount = result.Rows.Count;
    layout.Controls.Add(grid, 0, 1);
    dialog.CancelButton = close;
    update();
    ApplyColors(dialog);
    dialog.ShowDialog(owner);
   }
   if (proposal != null) ProposeBatchRecovery(owner, store, entry, proposal, refresh);
  }

  static bool ParseConfirmed(object value, PrintBatchResultRow row, out int count) {
   return int.TryParse(Convert.ToString(value), NumberStyles.Integer, CultureInfo.CurrentCulture, out count) &&
    count >= 0 && count <= row.Planned;
  }

  void ProposeBatchRecovery(Form owner, PrintHistoryStore store, PrintHistoryEntry entry, PrintBatchResult result, Action refresh) {
   ArchivedPrintJob job;
   if (!RunWork(owner, "print-recovery-load", "Načítavam tlačovú úlohu…", () => store.Open(entry.Id), out job)) return;
   int first = job.FirstIncompleteCopy(result);
   if (first == 0) return;
   int copy;
   using (var dialog = FeatureForm("Navrhnúť dotlač…", 615, 315)) {
    var stack = FeatureStack(dialog);
    StackAdd(stack, new Label { AutoSize = true, MaximumSize = new Size(500, 0),
     Text = Loc.T("Návrh priradí potvrdené počty najprv k skorším kópiám a potom k prvým etiketám daného riadka v kópii. Rozsah overte v náhľade podľa výtlačkov.") });
    var number = Number(stack, "Kópia pôvodnej úlohy", first, first, job.Repetitions, 0);
    number.Enabled = first < job.Repetitions;
    var actions = new FlowLayoutPanel { AutoSize = true };
    var open = Btn(Loc.T("Otvoriť náhľad"), () => dialog.DialogResult = DialogResult.OK);
    var cancel = Btn(Loc.T("Zrušiť"), dialog.Close);
    actions.Controls.Add(open);
    actions.Controls.Add(cancel);
    StackAdd(stack, actions);
    dialog.AcceptButton = open;
    dialog.CancelButton = cancel;
    ApplyColors(dialog);
    if (dialog.ShowDialog(owner) != DialogResult.OK) return;
    copy = (int)number.Value;
   }
   OutputPlan plan;
   if (!RunWork(owner, "prepare-print-result-recovery", "Pripravujem tlačovú úlohu…", () => {
    var latest = store.OpenBatchResult(entry.Id);
    if (latest.Revision != result.Revision)
     throw new InvalidOperationException(Loc.T("Výsledok sa medzitým zmenil. Zatvorte okno a otvorte ho znova."));
    return job.ProposeRemainder(copy, latest);
   }, out plan)) return;
   if (plan.Placements.Count > 0) ShowRecoveryPreview(owner, entry, refresh, job, plan, copy);
  }
 }
}
