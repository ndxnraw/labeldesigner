using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow {
   Button compareButton;
   DataTableSource comparisonTable;

   void CompareBatch() {
    if (comparisonTable == null) return;
    if (ActivePreset != null && ActivePreset.Reference != null) { ShowBatchReference(); return; }
    CompareFromFilePicker();
   }

   void CompareFromFilePicker() {
    if (comparisonTable == null) return;
    using (var picker = new OpenFileDialog { Filter = "CSV / Excel|*.csv;*.xlsx", Title = Loc.T("Vybrať referenčný súbor") })
     if (picker.ShowDialog(window) == DialogResult.OK) CompareBatchWithFile(picker.FileName);
   }

   void CompareBatchWithFile(string path) {
    var current = comparisonTable;
    if (current == null) return;
    try {
     bool csv = string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase);
     char split = setup.Separator;
     int codePage = setup.CodePage;
     if (csv && !ReadReferenceCsvOptions(ref split, ref codePage)) return;
     List<DataTableSource> reference;
     if (!ReadSource(path, split, codePage, csv, out reference)) return;
     if (reference.Count == 0) throw new InvalidDataException(Loc.T("Referenčný súbor neobsahuje tabuľku."));
     ConfigureComparison(path, current, reference, csv);
    } catch (Exception ex) { owner.Error(ex); }
   }

   bool ReadReferenceCsvOptions(ref char split, ref int codePage) {
    using (var dialog = owner.FeatureForm("Nastavenia referenčného CSV", 490, 330)) {
     var stack = owner.FeatureStack(dialog);
     var separatorChoice = owner.Choice(stack, "Oddeľovač", new[] { ";", ",", "Tab" }, split == '\t' ? 2 : split == ',' ? 1 : 0);
     separatorChoice.Name = "comparisonSeparator";
     var encodingChoice = owner.Choice(stack, "Kódovanie", new[] { "UTF-8", "Windows-1250" }, codePage == 1250 ? 1 : 0);
     var actions = new FlowLayoutPanel { AutoSize = true };
     var load = owner.Btn(Loc.T("Načítať tabuľku"), () => dialog.DialogResult = DialogResult.OK);
     var cancel = owner.Btn(Loc.T("Zrušiť"), dialog.Close);
     actions.Controls.Add(load);
     actions.Controls.Add(cancel);
     StackAdd(stack, actions);
     dialog.AcceptButton = load;
     dialog.CancelButton = cancel;
     owner.ApplyColors(dialog);
     if (dialog.ShowDialog(window) != DialogResult.OK) return false;
     split = separatorChoice.SelectedIndex == 2 ? '\t' : separatorChoice.SelectedIndex == 1 ? ',' : ';';
     codePage = encodingChoice.SelectedIndex == 1 ? 1250 : 65001;
     return true;
    }
   }

   void ConfigureComparison(string path, DataTableSource current, List<DataTableSource> reference, bool csv) {
    BatchComparisonResult result = null;
    using (var dialog = owner.FeatureForm("Porovnať dávku…", 640, 500)) {
     var stack = owner.FeatureStack(dialog);
     var filename = new TextBox { Text = Path.GetFileName(path), ReadOnly = true };
     owner.StackField(stack, Loc.T("Referenčný súbor"), filename);
     int sheetIndex = reference.FindIndex(item => item.Name == current.Name);
     var refSheet = csv ? null : owner.Choice(stack, "Referenčný hárok", reference.ConvertAll(item => item.Name).ToArray(), sheetIndex);
     if (refSheet != null) refSheet.Name = "comparisonReferenceSheet";
     var refHeader = owner.Check(stack, "Prvý riadok referencie je hlavička", setup.Header);
     var currentId = new ModernComboBox { Name = "comparisonCurrentKey", DropDownStyle = ComboBoxStyle.DropDownList };
     var referenceId = new ModernComboBox { Name = "comparisonReferenceKey", DropDownStyle = ComboBoxStyle.DropDownList };
     owner.StackField(stack, Loc.T("Identifikátor aktuálnej dávky"), currentId);
     owner.StackField(stack, Loc.T("Identifikátor referenčnej dávky"), referenceId);
     FillComparisonColumns(currentId, current, setup.Header);
     if (setup.UniqueColumn >= 0 && setup.UniqueColumn < currentId.Items.Count) currentId.SelectedIndex = setup.UniqueColumn;
     var hint = new Label { AutoSize = true, MaximumSize = new Size(540, 0), UseMnemonic = false };
     StackAdd(stack, hint);
     Func<DataTableSource> selectedTable = () => reference[refSheet == null ? 0 : refSheet.SelectedIndex];
     var actions = new FlowLayoutPanel { AutoSize = true };
     var compare = owner.Btn(Loc.T("Porovnať"), delegate {
      try {
       var prior = selectedTable();
       bool heading = refHeader.Checked;
       int leftKey = currentId.SelectedIndex, rightKey = referenceId.SelectedIndex;
       var mapping = setup;
       if (leftKey < 0 || rightKey < 0) return;
       if (owner.RunWork(dialog, "compare-batches", "Porovnávam dávky…",
           () => BatchComparison.Compare(current, prior, mapping, heading, leftKey, rightKey), out result))
        dialog.DialogResult = DialogResult.OK;
      } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
     });
     compare.Name = "comparisonRun";
     var cancel = owner.Btn(Loc.T("Zrušiť"), dialog.Close);
     actions.Controls.Add(compare);
     actions.Controls.Add(cancel);
     StackAdd(stack, actions);
     Action updateEnabled = () => compare.Enabled = currentId.SelectedIndex >= 0 && referenceId.SelectedIndex >= 0;
     Action chooseReference = delegate {
      var table = selectedTable();
      FillComparisonColumns(referenceId, table, refHeader.Checked);
      bool names = setup.Header && refHeader.Checked;
      hint.Text = Loc.T(names ? "Porovnávajú sa všetky pripravené riadky. Mapované stĺpce sa hľadajú podľa názvu."
       : "Porovnávajú sa všetky pripravené riadky. Mapované stĺpce sa porovnajú podľa pozície.");
      if (currentId.Items.Count == 0 || referenceId.Items.Count == 0)
       hint.Text = Loc.T("Obe tabuľky musia obsahovať stĺpec s identifikátormi.");
      int preferred = currentId.SelectedIndex;
      if (names && preferred >= 0) {
       string heading = BatchData.Value(current.Rows[0], preferred).Trim();
       preferred = -1;
       if (heading.Length != 0 && table.Rows.Count > 0) {
        for (int i = 0; i < table.Rows[0].Length; i++) {
         if (!string.Equals(table.Rows[0][i].Trim(), heading, StringComparison.OrdinalIgnoreCase)) continue;
         if (preferred >= 0) { preferred = -1; break; }
         preferred = i;
        }
       }
      }
      referenceId.SelectedIndex = preferred < referenceId.Items.Count ? preferred : -1;
      updateEnabled();
     };
     currentId.SelectedIndexChanged += delegate { chooseReference(); };
     referenceId.SelectedIndexChanged += delegate { updateEnabled(); };
     refHeader.CheckedChanged += delegate { chooseReference(); };
     if (refSheet != null) refSheet.SelectedIndexChanged += delegate { chooseReference(); };
     chooseReference();
     dialog.AcceptButton = compare;
     dialog.CancelButton = cancel;
     owner.ApplyColors(dialog);
     if (dialog.ShowDialog(window) != DialogResult.OK) return;
    }
    ShowComparison(result);
   }

   static void FillComparisonColumns(ComboBox box, DataTableSource table, bool heading) {
    box.Items.Clear();
    int width = 0;
    foreach (var row in table.Rows) width = Math.Max(width, row.Length);
    for (int i = 0; i < width; i++) {
     string name = heading && table.Rows.Count > 0 ? BatchData.Value(table.Rows[0], i) : "";
     box.Items.Add((i + 1) + " · " + (string.IsNullOrWhiteSpace(name) ? Loc.T("Stĺpec") : SampleText(name)));
    }
   }

   static string DifferenceStatus(BatchDifferenceKind kind) {
    switch (kind) {
     case BatchDifferenceKind.Added: return Loc.T("Pridaná položka");
     case BatchDifferenceKind.Removed: return Loc.T("Odstránená položka");
     default: return Loc.T("Zmenená položka");
    }
   }

   void ShowComparison(BatchComparisonResult result) {
    int targetRow = 0;
    HashSet<int> printRows = null;
    bool hasIssues = result.Issues.Count != 0;
    using (var dialog = owner.FeatureForm("Rozdiely medzi dávkami", 960, 660)) {
     dialog.MinimumSize = new Size(620, 400);
     var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
     layout.RowStyles.Add(new RowStyle(SizeType.Percent, result.Changed > 0 ? 35 : 0));
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     dialog.Controls.Add(layout);
     var summary = new Label { AutoSize = true, MaximumSize = new Size(580, 0), Text = hasIssues
      ? Loc.T("Pred porovnaním opravte identifikátory v uvedených riadkoch.")
      : string.Format(Loc.T("Pridané {0} · Odstránené {1} · Zmenené {2} · Bez zmeny {3}"),
       result.Added, result.Removed, result.Changed, result.Unchanged) };
     layout.Controls.Add(summary, 0, 0);

     var rows = ComparisonGrid("batchComparisonRows", dialog.Text);
     rows.VirtualMode = true;
     rows.Columns.Add("status", Loc.T("Stav"));
     rows.Columns.Add("id", Loc.T("Identifikátor"));
     rows.Columns.Add("now", Loc.T("Aktuálny riadok"));
     rows.Columns.Add("before", Loc.T("Referenčný riadok"));
     rows.Columns["status"].FillWeight = 170;
     foreach (DataGridViewColumn column in rows.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
     rows.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
      if (e.RowIndex < 0 || e.RowIndex >= (hasIssues ? result.Issues.Count : result.Rows.Count)) return;
      if (hasIssues) {
       var issue = result.Issues[e.RowIndex];
       switch (e.ColumnIndex) {
        case 0: e.Value = issue.Message; break;
        case 1: e.Value = SampleText(issue.Id); break;
        case 2: e.Value = RowText(issue.CurrentRow); break;
        case 3: e.Value = RowText(issue.ReferenceRow); break;
       }
      } else {
       var difference = result.Rows[e.RowIndex];
       switch (e.ColumnIndex) {
        case 0: e.Value = DifferenceStatus(difference.Kind); break;
        case 1: e.Value = SampleText(difference.Id); break;
        case 2: e.Value = RowText(difference.CurrentRow); break;
        case 3: e.Value = RowText(difference.ReferenceRow); break;
       }
      }
     };
     rows.RowCount = hasIssues ? result.Issues.Count : result.Rows.Count;
     layout.Controls.Add(rows, 0, 1);

     var details = ComparisonGrid("batchComparisonValues", Loc.T("Zmenené hodnoty"));
     details.Columns.Add("field", Loc.T("Prvok"));
     details.Columns.Add("before", Loc.T("Pôvodná hodnota"));
     details.Columns.Add("after", Loc.T("Nová hodnota"));
     details.Visible = result.Changed > 0;
     layout.Controls.Add(details, 0, 2);
     var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
     Func<int> currentRow = () => rows.CurrentCell == null ? 0 : hasIssues
      ? result.Issues[rows.CurrentCell.RowIndex].CurrentRow : result.Rows[rows.CurrentCell.RowIndex].CurrentRow;
     Action go = delegate {
      targetRow = currentRow();
      if (targetRow != 0) dialog.DialogResult = DialogResult.OK;
     };
     var jump = owner.Btn(Loc.T("Prejsť na etiketu"), go);
     var close = owner.Btn(Loc.T("Zavrieť"), dialog.Close);
     actions.Controls.Add(jump);
     if (!hasIssues && result.Added + result.Changed > 0)
      layout.Controls.Add(ComparisonPrintControls(dialog, rows, result, actions, selection => printRows = selection), 0, 3);
     actions.Controls.Add(close);
     layout.Controls.Add(actions, 0, 4);
     Action update = delegate {
      jump.Enabled = currentRow() != 0;
      details.Rows.Clear();
      if (hasIssues || rows.CurrentCell == null) return;
      var values = result.Rows[rows.CurrentCell.RowIndex].Values;
      if (values == null) return;
      foreach (var value in values) {
       int fieldIndex = targets.IndexOf(value.Target);
       string title = fieldIndex < 0 ? value.Target : Convert.ToString(mapping.Rows[fieldIndex].Cells["target"].Value);
       details.Rows.Add(title, value.Before ?? Loc.T("(chýba hodnota)"), value.After ?? Loc.T("(chýba hodnota)"));
      }
     };
     rows.CurrentCellChanged += delegate { update(); };
     rows.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) {
      if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && rows.Columns[e.ColumnIndex].Name != "print") go();
     };
     update();
     dialog.AcceptButton = jump;
     dialog.CancelButton = close;
     owner.ApplyColors(dialog);
     if (dialog.ShowDialog(window) != DialogResult.OK) return;
    }
    if (printRows != null) SetIncluded(record => printRows.Contains(record.Row));
    else NavigateRecord(targetRow);
   }

   static object RowText(int row) { return row > 0 ? (object)row : "—"; }

   static DataGridView ComparisonGrid(string name, string accessibleName) {
    return new DataGridView { Dock = DockStyle.Fill, Name = name, AccessibleName = accessibleName, ReadOnly = true,
     AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
     MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
     AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
   }
  }
 }
}
