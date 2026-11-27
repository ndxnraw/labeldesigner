using System;using System.Collections.Generic;using System.Drawing;
namespace EanEtikety {
public static class PreviewValidation {
 // Invalid elements keep their editable bounds; only valid artwork is rendered.
 public static VectorScene Build(LabelSettings source,List<LabelIssue> issues){if(!issues.Exists(i=>i.Blocking))return VectorArt.Build(source);var result=new VectorScene{Width=source.Width,Height=source.Height};try{foreach(int index in Elements.Indices(source)){if(issues.Exists(i=>i.Blocking&&i.Element==index))continue;var part=ProjectXml.Clone(source);part.Variables.Clear();part.Texts.Clear();part.Symbols.Clear();part.HideBarcode=index!=-1;part.UsePaper=part.Sheet=false;part.Layout.Enabled=false;part.Metadata.Clear();if(index>=0)part.Texts.Add(source.Texts[index]);if(index<=-3)part.Symbols.Add(source.Symbols[-3-index]);try{using(var scene=VectorArt.Build(part))foreach(var shape in scene.Shapes){var copy=shape.Copy();copy.Element=index;result.Shapes.Add(copy);}}catch{ /* The problem remains listed and the element remains selectable. */ }}var order=new Dictionary<ArtShape,int>();for(int i=0;i<result.Shapes.Count;i++)order[result.Shapes[i]]=i;result.Shapes.Sort((a,b)=>{int layer=Elements.Layer(source,a.Element).CompareTo(Elements.Layer(source,b.Element));return layer==0?order[a].CompareTo(order[b]):layer;});return result;}catch{result.Dispose();throw;}}
}
public partial class MainForm {
 void UpdatePreview(){if(IsDisposed||Disposing)return;TrackHistory();scenarioPreviewIssues=null;try{var raw=Read();LabelSettings s=raw;VectorScene scene=null;
  var scenario=raw.Scenarios.Find(item=>item.Id==activeScenarioId);
  preview.HiddenElements.Clear();
  preview.ProjectedStackElements.Clear();
  if(JobPreparation.HasDynamicArtwork(raw)){
   var instant=currentTime();previewDay=instant.Date;
   var draft=QuickPrint.PrepareEditor(raw,instant);
   if(scenario==null)scene=BuildEditorScene(draft);
   labelIssues=draft.SourceIssues();
   if(scenario==null){preview.HiddenElements.UnionWith(draft.Hidden);UseStackGeometry(raw,draft.Label);}
  }else{
   string variableError=null;try{s=PreparedLabel(raw);}catch(Exception ex){variableError=ex.Message;}
   labelIssues=LabelChecks.Inspect(s);
   if(variableError!=null&&!labelIssues.Exists(i=>i.Message==variableError))labelIssues.Add(new LabelIssue{Blocking=true,Message=variableError});
   if(scenario==null)scene=PreviewValidation.Build(s,labelIssues);
  }
  if(scenario!=null){
   s=raw;
   try{
    var draft=QuickPrint.PrepareScenario(raw,scenario,currentTime());
    scene=BuildEditorScene(draft);
    preview.HiddenElements.UnionWith(draft.Hidden);
    UseStackGeometry(raw,draft.Label);
    scenarioPreviewIssues=draft.SourceIssues();
   }catch(Exception ex){scenarioPreviewIssues=new List<LabelIssue>{new(){Blocking=true,Message=ex.Message}};}
  }
  if(preview.Image!=null)preview.Image.Dispose();preview.Image=new Bitmap(1,1);
  preview.Scene?.Dispose();preview.Scene=scene;preview.Settings=s;
  var visibleIssues=scenarioPreviewIssues??labelIssues;
  preview.ProblemElements.Clear();
  foreach(var issue in visibleIssues)if(issue.Element!=-2&&!preview.ProblemElements.Contains(issue.Element))preview.ProblemElements.Add(issue.Element);
  bool blocked=visibleIssues.Exists(i=>i.Blocking);
  preview.Caption=Display(s.Width)+" × "+Display(s.Height)+" "+s.Unit+" | "+s.Dpi+" DPI";
  status.Text=preview.Caption+(scenario==null?"":" • "+scenario.Name)+" • "+Loc.T(blocked?"Chyba":visibleIssues.Count>0?"Skontrolujte upozornenia":"Pripravené");
  status.ForeColor=blocked?Color.Firebrick:visibleIssues.Count>0?(dark?Color.FromArgb(255,199,111):Color.FromArgb(148,86,0)):(dark?Color.FromArgb(141,218,183):Color.FromArgb(32,84,58));
  studioStatusDetails=string.Join("\r\n",visibleIssues.ConvertAll(i=>i.ToString()));
  pdf.Enabled=docx.Enabled=zpl.Enabled=svg.Enabled=!labelIssues.Exists(i=>i.Blocking);}
  catch(Exception ex){labelIssues=new List<LabelIssue>{new LabelIssue{Blocking=true,Message=ex.Message}};status.Text=ex.Message;status.ForeColor=Color.Firebrick;pdf.Enabled=docx.Enabled=zpl.Enabled=svg.Enabled=false;}SyncIssueViews();preview.Invalidate();SyncStudio();
 }
 static VectorScene BuildEditorScene(QuickPrintDraft draft){
  var scene=PreviewValidation.Build(draft.Label,draft.Issues);
  foreach(var shape in scene.Shapes)shape.Element=Elements.SourceIndex(draft.Label,shape.Element);
  return scene;
 }
}
}
