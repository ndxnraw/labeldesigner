using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;
namespace EanEtikety {
// A copied barcode retains its semantic data and is rendered by the same vector engine.
// Geometry uses the existing movable symbol model; no bitmap or platform clipboard object is stored.
public class BarcodeContent {
 public string Ean; public int Kind; public bool Digits,Gs1Mode;
 public double Width,BarHeight,DigitSize,Dpi,SourceX,SourceY;
 public List<AiValue> ApplicationIdentifiers=new List<AiValue>();
 public static BarcodeContent From(LabelSettings s){var c=new BarcodeContent{Ean=s.Ean,Kind=s.Kind,Digits=s.Digits,Gs1Mode=s.Gs1Mode,Width=s.BarWidth,BarHeight=s.BarHeight,DigitSize=s.DigitSize,Dpi=s.Dpi,SourceX=s.BarX,SourceY=s.BarY};foreach(var ai in s.ApplicationIdentifiers)c.ApplicationIdentifiers.Add(new AiValue{AI=ai.AI,Data=ai.Data});return c;}
 public LabelSettings Settings(){return new LabelSettings{Width=10,Height=10,BarX=SourceX,BarY=SourceY,Ean=Ean,Kind=Kind,Digits=Digits,Gs1Mode=Gs1Mode,BarWidth=Width,BarHeight=BarHeight,DigitSize=DigitSize,Dpi=Dpi,ApplicationIdentifiers=ApplicationIdentifiers};}
 public BarcodeContent Copy(){return From(Settings());}
}
public class ElementTransfer {
 public TextItem Text; public SymbolItem Symbol;
 public static string Encode(ElementTransfer element){using(var w=new StringWriter()){new XmlSerializer(typeof(ElementTransfer)).Serialize(w,element);return w.ToString();}}
 public static ElementTransfer Decode(string xml){if(xml==null||xml.Length>200000)throw new InvalidDataException(Loc.T("Neplatný prvok v schránke."));using(var r=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=200000})){var e=(ElementTransfer)new XmlSerializer(typeof(ElementTransfer)).Deserialize(r);if((e.Text==null)==(e.Symbol==null))throw new InvalidDataException(Loc.T("Neplatný prvok v schránke."));var s=LabelSettings.Default();s.Texts.Clear();if(e.Text!=null)s.Texts.Add(e.Text);else s.Symbols.Add(e.Symbol);Engine.Validate(s);return e;}}
}
public partial class MainForm {
 const string ElementClipboardFormat="ND.LabelDesigner.Element.v1";
 string lastPaste;int pasteCount;
 bool EditingText(){Control c=ActiveControl;while(c is ContainerControl&&((ContainerControl)c).ActiveControl!=null)c=((ContainerControl)c).ActiveControl;return c is TextBoxBase||c is ComboBox||c is UpDownBase||(c!=null&&(c.Parent is UpDownBase||c.Parent is ComboBox))||((c==grid||grid.ContainsFocus)&&grid.IsCurrentCellInEditMode);}
 string CopyElementData(){var s=Read();int i=preview.SelectedIndex;var e=new ElementTransfer();if(i>=0&&i<s.Texts.Count)e.Text=s.Texts[i];else if(i<=-3&&-3-i<s.Symbols.Count)e.Symbol=s.Symbols[-3-i];else if(i==-1)e.Symbol=new SymbolItem{X=s.BarX,Y=s.BarY,Width=s.BarWidth,Height=Engine.BarcodeHeight(s),Code=BarcodeContent.From(s),Rotation=s.BarRotation};else return null;return ElementTransfer.Encode(e);}
 void CopyElement(){try{var xml=CopyElementData();if(xml==null)return;var data=new DataObject();data.SetData(ElementClipboardFormat,false,xml);Clipboard.SetDataObject(data,true,3,30);lastPaste=null;pasteCount=0;}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 void PasteElement(){try{var data=Clipboard.GetDataObject();if(data==null||!data.GetDataPresent(ElementClipboardFormat,false))return;PasteElementData(data.GetData(ElementClipboardFormat,false) as string);}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 void PasteElementData(string xml){var e=ElementTransfer.Decode(xml);var s=Read();int count=xml==lastPaste?pasteCount+1:1;double offset=Math.Min(40,count*2);int index;
  if(e.Text!=null){if(s.Texts.Count>=50)throw new InvalidOperationException(Loc.T("Povolených je najviac 50 textových polí."));e.Text.X=Math.Min(500,e.Text.X+offset);e.Text.Y=Math.Min(500,e.Text.Y+offset);index=s.Texts.Count;s.Texts.Add(e.Text);}else{if(s.Symbols.Count>=20)throw new InvalidOperationException(Loc.T("Najviac 20 symbolov."));e.Symbol.X=Math.Min(500,e.Symbol.X+offset);e.Symbol.Y=Math.Min(500,e.Symbol.Y+offset);index=-3-s.Symbols.Count;s.Symbols.Add(e.Symbol);}
  Engine.Validate(s);Apply(s);Changed(this,EventArgs.Empty);timer.Stop();preview.SelectElement(index);preview.Focus();lastPaste=xml;pasteCount=count;
 }
}
}
