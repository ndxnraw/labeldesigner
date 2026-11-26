using System;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
public class ElementGroup {public List<ElementTransfer> Items=new List<ElementTransfer>();public List<string> Groups=new List<string>();}
public partial class MainForm {
 List<int> SelectedElements(){var result=new List<int>();var s=Read();foreach(int i in preview.Selection)if(Elements.Indices(s).Contains(i))result.Add(i);if(result.Count==0&&Elements.Indices(s).Contains(preview.SelectedIndex))result.Add(preview.SelectedIndex);return result;}
 void RemoveSelected(){var selected=SelectedElements();var s=Read();selected.RemoveAll(i=>Elements.Locked(s,i));if(selected.Count==0)return;ChangeDocument(x=>Elements.Remove(x,selected));preview.SelectElement(-2);}
 void Arrange(string command){var selected=SelectedElements();if(selected.Count==0)return;ChangeDocument(delegate(LabelSettings s){
  if(command=="unlock"){foreach(int i in selected)Elements.Meta(s,i).Locked=false;return;}
  if(command=="lock"){foreach(int i in selected)Elements.Meta(s,i).Locked=true;return;}
  selected.RemoveAll(i=>Elements.Locked(s,i));if(selected.Count==0)return;
  if(command=="group"||command=="ungroup"){string group=command=="group"?Guid.NewGuid().ToString("N"):"";foreach(int i in selected){var meta=Elements.Meta(s,i);meta.Group=group;meta.GroupVisibility=null;}return;}
  if(command=="front"||command=="back"){int layer=command=="front"?int.MinValue:int.MaxValue;foreach(int i in Elements.Indices(s))layer=command=="front"?Math.Max(layer,Elements.Layer(s,i)):Math.Min(layer,Elements.Layer(s,i));foreach(int i in selected)Elements.Meta(s,i).Layer=command=="front"?++layer:--layer;return;}
  var items=new List<ElementEdit>();RectangleF bounds=RectangleF.Empty;foreach(int i in selected){var e=ElementTransform.Get(s,i);items.Add(e);var b=ElementTransform.Bounds(e);bounds=bounds.IsEmpty?b:RectangleF.Union(bounds,b);}
  if(command=="spaceX"||command=="spaceY"){
   if(items.Count<3)throw new InvalidOperationException(Loc.T("Vyberte aspoň tri prvky"));bool horizontal=command=="spaceX";items.Sort((a,b)=>(horizontal?ElementTransform.Bounds(a).Left:ElementTransform.Bounds(a).Top).CompareTo(horizontal?ElementTransform.Bounds(b).Left:ElementTransform.Bounds(b).Top));double occupied=0;foreach(var e in items)occupied+=horizontal?ElementTransform.Bounds(e).Width:ElementTransform.Bounds(e).Height;double gap=((horizontal?bounds.Width:bounds.Height)-occupied)/(items.Count-1),pos=horizontal?bounds.Left:bounds.Top;foreach(var e in items){var b=ElementTransform.Bounds(e);if(horizontal)e.X+=pos-b.Left;else e.Y+=pos-b.Top;pos+=(horizontal?b.Width:b.Height)+gap;Elements.Set(s,e);}return;
  }
  foreach(var e in items){var b=ElementTransform.Bounds(e);switch(command){case "left":e.X+=bounds.Left-b.Left;break;case "right":e.X+=bounds.Right-b.Right;break;case "top":e.Y+=bounds.Top-b.Top;break;case "bottom":e.Y+=bounds.Bottom-b.Bottom;break;case "centerX":e.X+=bounds.Left+bounds.Width/2-b.Left-b.Width/2;break;case "centerY":e.Y+=bounds.Top+bounds.Height/2-b.Top-b.Height/2;break;case "labelX":e.X+=s.Width/2-bounds.Left-bounds.Width/2;break;case "labelY":e.Y+=s.Height/2-bounds.Top-bounds.Height/2;break;}Elements.Set(s,e);}
 });preview.SelectMany(selected,true);}
 void ShowArrange(){using(var f=FeatureForm("Rozloženie a výber",480,650)){var p=FeatureStack(f);StackAdd(p,new Label{Text=Loc.T("Ctrl/Shift: výber viacerých prvkov. Ťahanie na prázdnej ploche: výberový obdĺžnik."),AutoSize=true,MaximumSize=new Size(410,0)});
  string[] names={"Zoskupiť","Rozoskupiť","Zamknúť","Odomknúť","Zarovnať vľavo","Zarovnať vpravo","Zarovnať hore","Zarovnať dole","Vodorovné stredy","Zvislé stredy","Vodorovné rozostupy","Zvislé rozostupy","Na stred etikety vodorovne","Na stred etikety zvisle","Do popredia","Do pozadia"};string[] keys={"group","ungroup","lock","unlock","left","right","top","bottom","centerX","centerY","spaceX","spaceY","labelX","labelY","front","back"};for(int i=0;i<keys.Length;i++){string key=keys[i];StackAdd(p,Btn(Loc.T(names[i]),delegate{try{Arrange(key);}catch(Exception ex){Error(ex);}}));}ApplyColors(f);f.ShowDialog(this);}}
 string CopyGroupData(){var s=Read();var group=new ElementGroup();foreach(int index in SelectedElements()){group.Groups.Add(Elements.Meta(s,index).Group);if(index>=0)group.Items.Add(new ElementTransfer{Text=s.Texts[index]});else if(index==-1)group.Items.Add(new ElementTransfer{Symbol=new SymbolItem{X=s.BarX,Y=s.BarY,Width=s.BarWidth,Height=Engine.BarcodeHeight(s),Rotation=s.BarRotation,Code=BarcodeContent.From(s)}});else group.Items.Add(new ElementTransfer{Symbol=s.Symbols[-3-index]});var item=group.Items[group.Items.Count-1];var meta=Elements.Meta(s,index);item.SourceIndex=index;item.Visibility=meta.Visibility;item.GroupVisibility=meta.GroupVisibility;}return ProjectXml.Encode(group);}
 void PasteGroupData(string xml){if(xml.Length>64000000)throw new InvalidOperationException(Loc.T("Príliš veľa údajov"));var group=ProjectXml.Decode<ElementGroup>(xml);if(group.Items.Count>70)throw new InvalidOperationException(Loc.T("Príliš veľa prvkov"));var selected=new List<int>();ChangeDocument(delegate(LabelSettings s){foreach(var item in group.Items){if(item.Text!=null){item.Text.X+=2;item.Text.Y+=2;selected.Add(s.Texts.Count);s.Texts.Add(item.Text);}else if(item.Symbol!=null){item.Symbol.X+=2;item.Symbol.Y+=2;selected.Add(-3-s.Symbols.Count);s.Symbols.Add(item.Symbol);}else throw new InvalidOperationException();}var groups=new Dictionary<string,string>();for(int i=0;i<selected.Count&&i<group.Groups.Count;i++){string key=group.Groups[i];if(string.IsNullOrEmpty(key))continue;if(!groups.ContainsKey(key))groups[key]=Guid.NewGuid().ToString("N");Elements.Meta(s,selected[i]).Group=groups[key];}
  var sourceIndices=new Dictionary<int,int>();
  for(int i=0;i<selected.Count;i++)if(group.Items[i].SourceIndex!=-2)sourceIndices.TryAdd(group.Items[i].SourceIndex,selected[i]);
  for(int i=0;i<selected.Count;i++){
   var item=group.Items[i];
   RemapPastedImage(item.Symbol,sourceIndices);
   if(item.Visibility==null&&item.GroupVisibility==null)continue;
   var meta=Elements.Meta(s,selected[i]);
   meta.Visibility=RemapPastedVisibility(item.Visibility,sourceIndices);
   if(!string.IsNullOrEmpty(meta.Group))meta.GroupVisibility=RemapPastedVisibility(item.GroupVisibility,sourceIndices);
  }
  ValidatePaste(s);});preview.SelectMany(selected,true);}
}
}
