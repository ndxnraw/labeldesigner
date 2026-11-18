using System.Windows.Forms;
namespace EanEtikety {
public partial class PrintPanel {
 static void Explain(TableLayoutPanel fields,string text,params Control[] controls){
  string translated=Loc.T(text);foreach(var control in controls)control.AccessibleDescription=translated;
  Add(fields,new Label{Text=translated,AutoSize=true,UseMnemonic=false,AccessibleName=Loc.T("Vysvetlenie nastavenia")});
 }
}
}
