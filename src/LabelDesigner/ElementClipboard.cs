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
 string CopyElementData(){if(SelectedElements().Count>1||SelectedVisibilityGroup()!=null)return CopyGroupData();var s=Read();int i=preview.SelectedIndex;var meta=s.Metadata.Find(m=>m.Index==i);var e=new ElementTransfer{SourceIndex=i,Visibility=meta?.Visibility,Translations=meta?.Translations,DataFormat=meta?.DataFormat,Calculation=meta?.Calculation,Inputs=s.Inputs.FindAll(input=>input.IsReferencedBy(meta))};if(i>=0&&i<s.Texts.Count)e.Text=s.Texts[i];else if(i<=-3&&-3-i<s.Symbols.Count)e.Symbol=s.Symbols[-3-i];else if(i==-1)e.Symbol=new SymbolItem{X=s.BarX,Y=s.BarY,Width=s.BarWidth,Height=Engine.BarcodeHeight(s),Code=BarcodeContent.From(s),Rotation=s.BarRotation};else return null;return ElementTransfer.Encode(e);}
 void CopyElement(){try{var xml=CopyElementData();if(xml==null)return;var data=new DataObject();data.SetData(ElementClipboardFormat,false,xml);Clipboard.SetDataObject(data,true,3,30);lastPaste=null;pasteCount=0;}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 void PasteElement(){try{var data=Clipboard.GetDataObject();if(data==null||!data.GetDataPresent(ElementClipboardFormat,false))return;PasteElementData(data.GetData(ElementClipboardFormat,false) as string);}catch(System.Runtime.InteropServices.ExternalException){Error(new Exception(Loc.T("Schránka je dočasne nedostupná. Skúste to znova.")));}catch(Exception ex){Error(ex);}}
 static VisibilityRule RemapPastedVisibility(VisibilityRule rule,Dictionary<int,int> indices){
  if(rule==null)return null;
  var copy=rule.Copy();
  if(copy.InputId==null&&string.IsNullOrEmpty(copy.Variable))copy.Field=indices.TryGetValue(copy.Field,out int index)?index:-2;
  return copy;
 }
 static void RemapPastedImage(SymbolItem symbol,Dictionary<int,int> indices){
  var mapping=symbol?.ImageMapping;
  if(mapping!=null&&string.IsNullOrEmpty(mapping.Variable))mapping.Field=indices.TryGetValue(mapping.Field,out int index)?index:-2;
 }
 static void ValidatePaste(LabelSettings s){
  if(JobPreparation.HasDynamicArtwork(s))Engine.ValidateDraft(s);else Engine.Validate(s);
 }
 void PasteElementData(string xml){if(xml!=null&&xml.Contains("<ElementGroup")){PasteGroupData(xml);return;}var e=ElementTransfer.Decode(xml);var s=Read();int count=xml==lastPaste?pasteCount+1:1;double offset=Math.Min(40,count*2);int index;
  if(e.Text!=null){if(s.Texts.Count>=50)throw new InvalidOperationException(Loc.T("Povolených je najviac 50 textových polí."));e.Text.X=Math.Min(500,e.Text.X+offset);e.Text.Y=Math.Min(500,e.Text.Y+offset);index=s.Texts.Count;s.Texts.Add(e.Text);}else{if(s.Symbols.Count>=20)throw new InvalidOperationException(Loc.T("Najviac 20 symbolov."));e.Symbol.X=Math.Min(500,e.Symbol.X+offset);e.Symbol.Y=Math.Min(500,e.Symbol.Y+offset);index=-3-s.Symbols.Count;s.Symbols.Add(e.Symbol);}
  if(e.Visibility!=null)Elements.Meta(s,index).Visibility=RemapPastedVisibility(e.Visibility,new Dictionary<int,int>{{e.SourceIndex,index}});
  if(e.Text!=null&&e.Translations?.Count>0)Elements.Meta(s,index).Translations=e.Translations;
  if(e.Text!=null&&e.DataFormat!=null)Elements.Meta(s,index).DataFormat=e.DataFormat;
  if(e.Text!=null&&e.Calculation!=null)Elements.Meta(s,index).Calculation=e.Calculation.Remap(new Dictionary<int,int>{{e.SourceIndex,index}});
  RemapPastedImage(e.Symbol,new Dictionary<int,int>{{e.SourceIndex,index}});
  TemplateInput.MergeReferenced(e.Inputs,s);ValidatePaste(s);Apply(s);Changed(this,EventArgs.Empty);timer.Stop();preview.SelectElement(index);preview.Focus();lastPaste=xml;pasteCount=count;
 }
}
}
