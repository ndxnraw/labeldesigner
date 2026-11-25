using System;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  const string MinimumFitColumn = "minimumFitSize";
  CheckBox inspectFixedFit;
  NumericUpDown inspectFitMinimum;

  void BuildTextFitInspector() {
   inspectFixedFit = new CheckBox { Name = "fitTextToField", Text = Loc.T("Prispôsobiť text pevnému poľu"), AutoSize = true };
   StackAdd(textInspector, inspectFixedFit);
   inspectFitMinimum = Number(textInspector, "Minimálne písmo (pt)", 6, 4, 144, 2);
   inspectFitMinimum.Name = "fitTextMinimum";
   inspectFitMinimum.Increment = .1m;
   inspectFixedFit.CheckedChanged += delegate { TextSizingChanged(true); };
   inspectFitMinimum.ValueChanged += delegate { if (inspectFixedFit.Checked) CommitInspector(); };
  }

  void TextSizingChanged(bool fixedFitChanged) {
   if (studioSync || loading) return;
   studioSync = true;
   try {
    if (fixedFitChanged && inspectFixedFit.Checked) inspectAutoWidth.Checked = false;
    if (!fixedFitChanged && inspectAutoWidth.Checked) inspectFixedFit.Checked = false;
    InspectorVisible(inspectFitMinimum, inspectFixedFit.Checked);
   } finally { studioSync = false; }
   CommitInspector();
  }

  void SyncTextFitInspector(DataGridViewRow row) {
   double minimum = Convert.ToDouble(row.Cells[MinimumFitColumn].Value ?? 0d);
   inspectFixedFit.Checked = minimum > 0;
   inspectFitMinimum.Maximum = inspectNumbers[4].Value;
   inspectFitMinimum.Value = Math.Min(minimum > 0 ? (decimal)minimum : 6, inspectFitMinimum.Maximum);
   inspectAutoWidth.Checked = !inspectFixedFit.Checked && Convert.ToBoolean(row.Cells[9].Value ?? false);
   InspectorVisible(inspectFitMinimum, inspectFixedFit.Checked);
  }
 }
}
