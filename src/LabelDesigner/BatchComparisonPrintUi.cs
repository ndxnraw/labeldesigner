using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow {
   Control ComparisonPrintControls(Form dialog, DataGridView grid, BatchComparisonResult comparison,
       FlowLayoutPanel actions, Action<HashSet<int>> accept) {
    var current = new Dictionary<int,BatchRecord>();
    foreach (var record in batch) current.Add(record.Row, record);
    var selected = new HashSet<int>();
    Func<int,BatchRecord> candidate = index => {
     if (index < 0 || index >= comparison.Rows.Count) return null;
     var row = comparison.Rows[index];
     BatchRecord record;
     return row.Kind != BatchDifferenceKind.Removed && current.TryGetValue(row.CurrentRow, out record) ? record : null;
    };

    grid.ReadOnly = false;
    grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
    foreach (DataGridViewColumn column in grid.Columns) column.ReadOnly = true;
    var check = new DataGridViewCheckBoxColumn {
     Name = "print", HeaderText = Loc.T("Zahrnúť"), FillWeight = 63, SortMode = DataGridViewColumnSortMode.NotSortable
    };
    grid.Columns.Add(check);
    int quantity = grid.Columns.Add("printQuantity", Loc.T("Aktuálny počet etikiet"));
    grid.Columns[quantity].ReadOnly = true;
    grid.Columns[quantity].SortMode = DataGridViewColumnSortMode.NotSortable;
    grid.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
     if (e.ColumnIndex != check.Index && e.ColumnIndex != quantity) return;
     var record = candidate(e.RowIndex);
     if (record == null) return;
     e.Value = e.ColumnIndex == check.Index ? (object)selected.Contains(record.Row) : record.Count;
    };
    grid.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs e) {
     if (e.ColumnIndex == check.Index && candidate(e.RowIndex) == null) e.Cancel = true;
    };
    grid.CellPainting += delegate(object sender, DataGridViewCellPaintingEventArgs e) {
     if (e.RowIndex < 0 || e.ColumnIndex != check.Index || candidate(e.RowIndex) != null) return;
     e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);
     e.Handled = true;
    };
    grid.CellToolTipTextNeeded += delegate(object sender, DataGridViewCellToolTipTextNeededEventArgs e) {
     if (e.ColumnIndex != check.Index && e.ColumnIndex != quantity) return;
     var record = candidate(e.RowIndex);
     if (record != null && record.Error != null) e.ToolTipText = record.Error;
    };

    var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
    var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
    panel.Controls.Add(options, 0, 0);
    var summary = new Label { Name = "comparisonPrintSummary", AutoSize = true, MaximumSize = new Size(560, 0) };
    panel.Controls.Add(summary, 0, 1);
    var apply = owner.Btn(Loc.T("Použiť výber na tlač"), delegate {
     if (!grid.EndEdit()) return;
     if (selected.Count == 0 || !CanUseSelection(current, selected)) return;
     accept(selected);
     dialog.DialogResult = DialogResult.OK;
    });
    apply.Name = "applyComparisonSelection";
    actions.Controls.Add(apply);

    Action update = delegate {
     long labels;
     int errors;
     CountSelection(current, selected, out labels, out errors);
     string text = string.Format(Loc.T("Výber na tlač · Záznamy {0} · Etikety {1}"), selected.Count, labels);
     if (errors > 0) text += Environment.NewLine + string.Format(Loc.T("Chybné záznamy vo výbere · {0}"), errors);
     if (labels > OutputPlan.MaxLabels) text += Environment.NewLine + Loc.T("Najviac 10000 etikiet");
     summary.Text = text + Environment.NewLine + Loc.T("Použitím výberu nahradíte výber v dávke.");
     apply.Enabled = selected.Count > 0 && errors == 0 && labels <= OutputPlan.MaxLabels;
    };
    grid.CellValuePushed += delegate(object sender, DataGridViewCellValueEventArgs e) {
     if (e.ColumnIndex != check.Index) return;
     var record = candidate(e.RowIndex);
     if (record == null) return;
     if (e.Value is bool && (bool)e.Value) selected.Add(record.Row);
     else selected.Remove(record.Row);
     update();
    };
    grid.CurrentCellDirtyStateChanged += delegate {
     if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
    };
    Action<BatchDifferenceKind> mark = kind => {
     grid.EndEdit();
     for (int i = 0; i < comparison.Rows.Count; i++) {
      if (comparison.Rows[i].Kind != kind) continue;
      var record = candidate(i);
      if (record != null) selected.Add(record.Row);
     }
     grid.InvalidateColumn(check.Index);
     update();
    };
    var added = owner.Btn(Loc.T("Označiť pridané"), () => mark(BatchDifferenceKind.Added));
    var changed = owner.Btn(Loc.T("Označiť zmenené"), () => mark(BatchDifferenceKind.Changed));
    var clear = owner.Btn(Loc.T("Zrušiť výber"), delegate {
     grid.EndEdit();
     selected.Clear();
     grid.InvalidateColumn(check.Index);
     update();
    });
    added.Enabled = comparison.Added > 0;
    changed.Enabled = comparison.Changed > 0;
    options.Controls.Add(added);
    options.Controls.Add(changed);
    options.Controls.Add(clear);
    update();
    return panel;
   }

   static bool CanUseSelection(Dictionary<int,BatchRecord> current, HashSet<int> selected) {
    long labels;
    int errors;
    CountSelection(current, selected, out labels, out errors);
    return errors == 0 && labels <= OutputPlan.MaxLabels;
   }

   static void CountSelection(Dictionary<int,BatchRecord> current, HashSet<int> selected, out long labels, out int errors) {
    labels = 0;
    errors = 0;
    foreach (int row in selected) {
     var record = current[row];
     labels += record.Count;
     if (record.Error != null || record.Count < 1 || record.Count > OutputPlan.MaxLabels) errors++;
    }
   }
  }
 }
}
