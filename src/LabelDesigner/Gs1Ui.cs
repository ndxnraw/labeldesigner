using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace EanEtikety {
public partial class MainForm {
 CheckBox gs1Mode=new CheckBox{Text=Loc.T("Použiť GS1 AI"),AutoSize=true};Button gs1Button;List<AiValue> aiValues=new List<AiValue>();
 void Gs1State(){bool code128=kind.SelectedIndex>=0&&MoreBarcodes.Kinds[kind.SelectedIndex]==128;if(codeInspector!=null){var host=gs1Mode.Parent;host.Visible=code128;var caption=codeInspector.GetControlFromPosition(0,codeInspector.GetRow(host)-1);if(caption!=null)caption.Visible=code128;}gs1Mode.Enabled=code128;gs1Button.Enabled=code128;ean.Enabled=!(code128&&gs1Mode.Checked);if(code128&&gs1Mode.Checked){try{formatHint.Text=Gs1.Human(aiValues);}catch{formatHint.Text=Loc.T("Nastavte identifikátory a ich dáta tlačidlom Identifikátory…");}}else if(kind.SelectedIndex>=0)formatHint.Text=MoreBarcodes.Hint(MoreBarcodes.Kinds[kind.SelectedIndex]);}
 static List<AiValue> CopyAi(List<AiValue> items){var result=new List<AiValue>();if(items!=null)foreach(var v in items)result.Add(new AiValue{AI=v.AI,Data=v.Data});return result;}
 void EditGs1(){using(var dialog=new Gs1EditorDialog(aiValues){Font=Font}){dialog.UseTheme(ApplyColors);if(dialog.ShowDialog(this)==DialogResult.OK){var items=dialog.Values;Gs1.Payload(items);aiValues=CopyAi(items);gs1Mode.Checked=true;Gs1State();Changed(this,EventArgs.Empty);timer.Stop();UpdatePreview();}}}
}
}
