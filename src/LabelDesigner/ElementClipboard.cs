using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;
namespace EanEtikety {
public partial class MainForm {
 const string ElementClipboardFormat="ND.LabelDesigner.Element.v1";
 string lastPaste;int pasteCount;
 bool EditingText(){Control c=ActiveControl;while(c is ContainerControl&&((ContainerControl)c).ActiveControl!=null)c=((ContainerControl)c).ActiveControl;return c is TextBoxBase||c is ComboBox||c is UpDownBase||(c!=null&&(c.Parent is UpDownBase||c.Parent is ComboBox))||((c==grid||grid.ContainsFocus)&&grid.IsCurrentCellInEditMode);}
 string CopyElementData(){if(SelectedElements().Count>1)return CopyGroupData();var s=Read();int i=preview.SelectedIndex;var e=new ElementTransfer();if(i>=0&&i<s.Texts.Count)e.Text=s.Texts[i];else if(i<=-3&&-3-i<s.Symbols.Count)e.Symbol=s.Symbols[-3-i];else if(i==-1)e.Symbol=new SymbolItem{X=s.BarX,Y=s.BarY,Width=s.BarWidth,Height=Engine.BarcodeHeight(s),Code=BarcodeContent.From(s),Rotation=s.BarRotation};else return null;return ElementTransfer.Encode(e);}
 void CopyElement(){try{var xml=CopyElementData();if(xml==null)return;var data=new DataObject();data.SetData(ElementClipboardFormat,false,xml);Clipboard.SetDataObject(data,true,3,30);lastPaste=null;pasteCount=0;}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 void PasteElement(){try{var data=Clipboard.GetDataObject();if(data==null||!data.GetDataPresent(ElementClipboardFormat,false))return;PasteElementData(data.GetData(ElementClipboardFormat,false) as string);}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 void PasteElementData(string xml){if(xml!=null&&xml.Contains("<ElementGroup")){PasteGroupData(xml);return;}var e=ElementTransfer.Decode(xml);var s=Read();int count=xml==lastPaste?pasteCount+1:1;double offset=Math.Min(40,count*2);int index;
  if(e.Text!=null){if(s.Texts.Count>=50)throw new InvalidOperationException(Loc.T("Povolených je najviac 50 textových polí."));e.Text.X=Math.Min(500,e.Text.X+offset);e.Text.Y=Math.Min(500,e.Text.Y+offset);index=s.Texts.Count;s.Texts.Add(e.Text);}else{if(s.Symbols.Count>=20)throw new InvalidOperationException(Loc.T("Najviac 20 symbolov."));e.Symbol.X=Math.Min(500,e.Symbol.X+offset);e.Symbol.Y=Math.Min(500,e.Symbol.Y+offset);index=-3-s.Symbols.Count;s.Symbols.Add(e.Symbol);}
  Engine.Validate(s);Apply(s);Changed(this,EventArgs.Empty);timer.Stop();preview.SelectElement(index);preview.Focus();lastPaste=xml;pasteCount=count;
 }
}
}
