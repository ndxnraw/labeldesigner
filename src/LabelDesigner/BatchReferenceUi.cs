using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow {
   Button referenceButton;

   void ShowBatchReference() {
    var preset = ActivePreset;
    if (preset == null) return;
    var current = comparisonTable;
    BatchReference compareReference = null;
    bool chooseFile = false;
    using (var dialog = owner.FeatureForm("Referenčná dávka…", 650, 480)) {
     var stack = owner.FeatureStack(dialog);
     var info = new Label { Name = "batchReferenceInfo", AutoSize = true, UseMnemonic = false, MaximumSize = new Size(540, 0) };
     StackAdd(stack, info);
     var warning = new Label { AutoSize = true, UseMnemonic = false, MaximumSize = new Size(540, 0) };
     StackAdd(stack, warning);
     var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
     StackAdd(stack, actions);
     Action refresh = null;
     var view = owner.Btn(Loc.T("Zobraziť referenciu"), delegate {
      if (preset.Reference == null) return;
      try { ViewBatchReference(dialog, preset.Reference); }
      catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
     });
     var save = owner.Btn(Loc.T("Uložiť aktuálnu dávku ako referenciu…"), delegate {
      try {
       if (current == null) return;
       BatchReferences.EnsurePresetMapping(current, setup, preset);
       int key = SelectReferenceIdentifier(dialog, current, preset.Reference);
       if (key < 0) return;
       BatchReference candidate;
       string source = tableSourcePath;
       string sheetName = csvPath == null ? current.Name : null;
       var mapping = setup;
       var instant = DateTime.UtcNow;
       if (!owner.RunWork(dialog, "save-batch-reference", "Ukladám referenčnú dávku…",
          () => BatchReferences.Capture(current, mapping, key, source, sheetName, instant), out candidate)) return;
       string question = preset.Reference == null ? Loc.T("Uložiť túto referenciu do projektu?")
        : Loc.T("Nahradiť uloženú referenciu aktuálnou dávkou?");
       if (!ConfirmBatchReference(dialog, question + Environment.NewLine + ReferenceInfo(candidate))) return;
       SetBatchReference(preset, candidate);
       refresh();
      } catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
     });
     var remove = owner.Btn(Loc.T("Odstrániť referenciu"), delegate {
      if (preset.Reference == null || !ConfirmBatchReference(dialog, Loc.T("Odstrániť uloženú referenčnú dávku?"))) return;
      try { SetBatchReference(preset, null); refresh(); }
      catch (Exception ex) { UiMessageBox.Show(dialog, ex.Message, dialog.Text); }
     });
     var compare = owner.Btn(Loc.T("Porovnať s uloženou referenciou"), delegate {
      compareReference = preset.Reference;
      dialog.Close();
     });
     var manual = owner.Btn(Loc.T("Vybrať referenčný súbor"), delegate { chooseFile = true; dialog.Close(); });
     var close = owner.Btn(Loc.T("Zavrieť"), dialog.Close);
     foreach (var button in new[] { compare, manual, view, save, remove, close }) actions.Controls.Add(button);
     refresh = delegate {
      var reference = preset.Reference;
      info.Text = string.Format(Loc.T("Predvoľba dávky: {0}"), preset.Name) + Environment.NewLine +
       (reference == null ? Loc.T("Predvoľba nemá uloženú referenciu.") : ReferenceInfo(reference));
      warning.Text = Loc.T("Referenčné hodnoty sa ukladajú do projektu. Uložte projekt, aby boli dostupné nabudúce.");
      view.Enabled = remove.Enabled = reference != null;
      manual.Enabled = save.Enabled = current != null;
      compare.Enabled = current != null && reference != null;
      if (compare.Enabled) {
       try { BatchReferences.ResolveIdentifier(current, setup, reference); }
       catch (InvalidDataException ex) { compare.Enabled = false; warning.Text += Environment.NewLine + ex.Message; }
      }
     };
     refresh();
     dialog.CancelButton = close;
     owner.ApplyColors(dialog);
     dialog.ShowDialog(window);
    }
    if (chooseFile) CompareFromFilePicker();
    else if (compareReference != null && current != null) {
     try {
      BatchComparisonResult result;
      var mapping = setup;
      if (owner.RunWork(window, "compare-batches", "Porovnávam dávky…",
           () => BatchReferences.Compare(current, mapping, compareReference), out result)) ShowComparison(result);
     } catch (Exception ex) { owner.Error(ex); }
    }
   }

   static bool ConfirmBatchReference(Form parent, string message) {
    using (var dialog = UiMessageBox.Create(parent, message, Loc.T("Potvrdenie referencie"), MessageBoxButtons.YesNo, MessageBoxIcon.Question)) {
     dialog.AcceptButton = dialog.CancelButton;
     return dialog.ShowDialog(parent) == DialogResult.Yes;
    }
   }

   void SetBatchReference(BatchPreset preset, BatchReference reference) {
    var previous = preset.Reference;
    BatchReferences.Replace(setup, preset, reference);
    try { PersistPresets(); }
    catch { preset.Reference = previous; throw; }
   }

   int SelectReferenceIdentifier(Form parent, DataTableSource current, BatchReference previous) {
    if (setup.UniqueColumn >= 0) return setup.UniqueColumn;
    using (var dialog = owner.FeatureForm("Identifikátor referenčnej dávky", 540, 290)) {
     var stack = owner.FeatureStack(dialog);
     var key = new ModernComboBox { Name = "savedReferenceIdentifier", DropDownStyle = ComboBoxStyle.DropDownList };
     owner.StackField(stack, Loc.T("Identifikátor aktuálnej dávky"), key);
     FillComparisonColumns(key, current, setup.Header);
     if (previous != null) {
      try { key.SelectedIndex = BatchReferences.ResolveIdentifier(current, setup, previous); }
      catch (InvalidDataException) { }
     }
     var actions = new FlowLayoutPanel { AutoSize = true };
     var next = owner.Btn(Loc.T("Pokračovať"), () => dialog.DialogResult = DialogResult.OK);
     var cancel = owner.Btn(Loc.T("Zrušiť"), dialog.Close);
     key.SelectedIndexChanged += delegate { next.Enabled = key.SelectedIndex >= 0; };
     next.Enabled = key.SelectedIndex >= 0;
     actions.Controls.Add(next);
     actions.Controls.Add(cancel);
     StackAdd(stack, actions);
     dialog.AcceptButton = next;
     dialog.CancelButton = cancel;
     owner.ApplyColors(dialog);
     return dialog.ShowDialog(parent) == DialogResult.OK ? key.SelectedIndex : -1;
    }
   }

   static string ReferenceInfo(BatchReference reference) {
    return string.Format(Loc.T("Zdroj {0} · Uložené {1} · Záznamy {2}"),
     reference.SheetName == null ? reference.SourceName : reference.SourceName + " / " + reference.SheetName,
     reference.SavedUtc.ToLocalTime().ToString("g"), reference.RowCount);
   }

   void ViewBatchReference(Form parent, BatchReference reference) {
    DataTableSource table;
    if (!owner.RunWork(parent, "read-batch-reference", "Načítavam referenčnú dávku…", () => BatchReferences.Open(reference), out table)) return;
    using (var dialog = owner.FeatureForm("Uložená referenčná dávka", 900, 560)) {
     var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 3, Padding = new Padding(12), Dock = DockStyle.Fill };
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     dialog.Controls.Add(layout);
     layout.Controls.Add(new Label { Text = ReferenceInfo(reference), AutoSize = true, UseMnemonic = false, MaximumSize = new Size(550, 0) }, 0, 0);
     var rows = ComparisonGrid("savedBatchReferenceRows", dialog.Text);
     rows.VirtualMode = true;
     rows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
     rows.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
     rows.Columns.Add("source", Loc.T("Referenčný riadok"));
     rows.Columns.Add("id", SavedColumnTitle(reference.Identifier, Loc.T("Identifikátor")));
     for (int i = 0; i < reference.Fields.Count; i++) {
      var field = reference.Fields[i];
      rows.Columns.Add("value" + i, SavedColumnTitle(field, field.Target == "quantity" ? Loc.T("Počet etikiet") : Loc.T("Stĺpec")));
     }
     foreach (DataGridViewColumn column in rows.Columns) {
      column.SortMode = DataGridViewColumnSortMode.NotSortable;
      column.Width = column.Index == 0 ? 115 : 160;
     }
     rows.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
      if (e.RowIndex < 0 || e.RowIndex >= table.Rows.Count) return;
      e.Value = e.ColumnIndex == 0 ? (object)table.SourceRow(e.RowIndex) :
       table.Rows[e.RowIndex][e.ColumnIndex - 1] ?? Loc.T("(chýba hodnota)");
     };
     rows.RowCount = table.Rows.Count;
     layout.Controls.Add(rows, 0, 1);
     var close = owner.Btn(Loc.T("Zavrieť"), dialog.Close);
     layout.Controls.Add(close, 0, 2);
     dialog.CancelButton = close;
     owner.ApplyColors(dialog);
     dialog.ShowDialog(parent);
    }
   }

   static string SavedColumnTitle(ColumnBinding field, string label) {
    return (field.Column + 1) + " · " + (field.HeaderName == null ? label : SampleText(field.HeaderName));
   }
  }
 }
}
