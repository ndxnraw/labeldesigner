using System.Windows.Forms;
namespace EanEtikety {
public static class UiLocalization {
 public static void Refresh(Control control){
  if(!string.IsNullOrEmpty(control.AccessibleName))control.AccessibleName=Loc.T(control.AccessibleName);
  if(!string.IsNullOrEmpty(control.AccessibleDescription))control.AccessibleDescription=Loc.T(control.AccessibleDescription);
  if(control is Label||control is Button||control is CheckBox||control is Form)control.Text=Loc.T(control.Text);
  var grid=control as DataGridView;if(grid!=null){foreach(DataGridViewColumn col in grid.Columns)col.HeaderText=Loc.T(col.HeaderText);return;}
  var combo=control as ComboBox;if(combo!=null){int selected=combo.SelectedIndex;for(int i=0;i<combo.Items.Count;i++)if(combo.Items[i] is string)combo.Items[i]=Loc.T((string)combo.Items[i]);combo.SelectedIndex=selected;combo.Invalidate();return;}
  if(control is TextBoxBase||control is NumericUpDown)return;
  foreach(Control child in control.Controls)Refresh(child);
 }
}
}
