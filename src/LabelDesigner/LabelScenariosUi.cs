using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  List<LabelScenario> labelScenarios=new();
  List<LabelIssue> scenarioPreviewIssues;
  string activeScenarioId;
  ModernComboBox scenarioPicker;
  bool refreshingScenarios;

  void AddScenarioControls(FlowLayoutPanel controls){
   scenarioPicker=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=170,
    Name="canvasScenario",AccessibleName=Loc.T("Scenár náhľadu"),Margin=new Padding(8,8,3,3)};
   ComboStyle(scenarioPicker);
   scenarioPicker.SelectedIndexChanged+=delegate{
    if(!refreshingScenarios)SelectScenario((scenarioPicker.SelectedItem as LabelScenario)?.Id);
   };
   controls.Controls.Add(scenarioPicker);
   controls.Controls.Add(NamedEditorButton("manageScenarios","Testovacie scenáre…",ManageScenarios,HeaderIcon.Data));
   RefreshScenarioPicker();
  }

  void RefreshScenarioPicker(){
   if(scenarioPicker==null)return;
   refreshingScenarios=true;
   scenarioPicker.BeginUpdate();
   try{
    scenarioPicker.Items.Clear();
    scenarioPicker.Items.Add(Loc.T("Bežný náhľad"));
    scenarioPicker.Items.AddRange(labelScenarios.ToArray());
    int index=labelScenarios.FindIndex(item=>item.Id==activeScenarioId);
    if(index<0)activeScenarioId=null;
    scenarioPicker.SelectedIndex=index+1;
    scenarioPicker.Enabled=labelScenarios.Count>0;
   }finally{scenarioPicker.EndUpdate();refreshingScenarios=false;}
  }

  void SelectScenario(string id){
   CommitInspectorIfFocused();
   activeScenarioId=id;
   int selected=labelScenarios.FindIndex(item=>item.Id==id)+1;
   if(scenarioPicker.SelectedIndex!=selected)RefreshScenarioPicker();
   preview.HighlightedSafetyZone=null;
   timer.Stop();
   UpdatePreview();
  }

  void ManageScenarios(){
   CommitInspectorIfFocused();
   var template=Read();
   string chosen=null;
   bool show=false,changed=false;
   using(var window=FeatureForm("Testovacie scenáre…",680,440)){
    var root=Rows();window.Controls.Add(root);
    ManagementLayout.AddRow(root,Description("Scenáre použijete na skúšanie návrhu v editore. Tlač a export používajú údaje šablóny."));
    var list=new ListBox{Dock=DockStyle.Fill,Name="templateScenarios",AccessibleName=Loc.T("Testovacie scenáre…"),
     IntegralHeight=false,HorizontalScrollbar=true};
    ManagementLayout.AddRow(root,list,SizeType.Percent);
    var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
    ManagementLayout.AddRow(root,actions);
    Button add=null,edit=null,remove=null,showButton=null,check=null;
    add=Btn(Loc.T("Pridať"),()=>SaveEdit(new LabelScenario(),false));
    edit=Btn(Loc.T("Upraviť"),()=>{if(list.SelectedItem is LabelScenario item)SaveEdit(item,true);});
    remove=Btn(Loc.T("Odstrániť"),delegate{
     if(list.SelectedItem is not LabelScenario item)return;
     template.Scenarios.Remove(item);changed=true;RefreshList(null);
    });
    showButton=Btn(Loc.T("Zobraziť na plátne"),delegate{
     if(list.SelectedItem is not LabelScenario item)return;
     chosen=item.Id;show=true;window.DialogResult=DialogResult.OK;
    });
    ScenarioFinding finding=null;
    check=Btn(Loc.T("Skontrolovať všetky scenáre"),delegate{
     try{
      var instant=currentTime();
      if(!RunWork(window,"template-scenarios","Kontrolujem scenáre…",()=>InspectScenarios(template,instant),out var results))return;
      finding=ShowScenarioFindings(window,template,results);
      if(finding==null)return;
      chosen=finding.Scenario.Id;show=true;window.DialogResult=DialogResult.OK;
     }catch(Exception ex){Error(ex);}
    });
    check.Name="checkScenarios";
    actions.Controls.AddRange(new Control[]{add,edit,remove,showButton,check});
    var applied=Btn(Loc.T("Použiť"),()=>window.DialogResult=DialogResult.OK);
    var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
    var footer=new UiMessageBox.ActionRow();
    footer.AddAction(applied);footer.AddAction(cancel);
    ManagementLayout.AddRow(root,footer);
    window.AcceptButton=applied;window.CancelButton=cancel;
    list.SelectedIndexChanged+=delegate{UpdateActions();};
    list.DoubleClick+=delegate{edit.PerformClick();};
    RefreshList(activeScenarioId);
    ApplyColors(window);
    if(window.ShowDialog(this)!=DialogResult.OK)return;
    if(changed)ChangeDocument(label=>label.Scenarios=template.Scenarios);
    if(show)SelectScenario(chosen);
    if(finding!=null){
     // Keep the original element selected even if the sample suppressed its artwork.
     SelectEditorIssue(finding.Issue);
     if(finding.Issue.Element!=-2)FocusInspector();
     else FocusProblems();
    }

    void UpdateActions(){
     add.Enabled=template.Scenarios.Count<LabelScenario.MaximumCount;
     edit.Enabled=remove.Enabled=showButton.Enabled=list.SelectedIndex>=0;
     check.Enabled=template.Scenarios.Count>0;
    }
    void RefreshList(string id){
     list.Items.Clear();
     list.Items.AddRange(template.Scenarios.ToArray());
     int index=template.Scenarios.FindIndex(item=>item.Id==id);
     if(list.Items.Count>0)list.SelectedIndex=Math.Max(0,index);
     UpdateActions();
    }
    void SaveEdit(LabelScenario item,bool existing){
     var result=EditScenario(window,template,item);
     if(result==null)return;
     if(existing)template.Scenarios[template.Scenarios.IndexOf(item)]=result;
     else template.Scenarios.Add(result);
     changed=true;RefreshList(result.Id);
    }
   }
  }

  sealed record ScenarioFinding(LabelScenario Scenario,LabelIssue Issue,string ElementName=null){
   public override string ToString()=>Scenario.Name+" · "+
    (ElementName==null?"":ElementName+" · ")+(Issue?.ToString()??Loc.T("Bez problémov"));
  }

  static List<ScenarioFinding> InspectScenarios(LabelSettings template,DateTime instant){
   var results=new List<ScenarioFinding>();
   for(int index=0;index<template.Scenarios.Count;index++){
    WorkContext.Report("Kontrolujem scenáre…",index,template.Scenarios.Count);
    var scenario=template.Scenarios[index];
    try{
     var draft=QuickPrint.PrepareScenario(template,scenario,instant);
     var issues=draft.SourceIssues();
     if(draft.CanPrint){
      var artwork=draft.Label;
      var paper=new RectangleF(0,0,(float)artwork.Width,(float)artwork.Height);
      var plan=new OutputPlan{Width=artwork.Width,Height=artwork.Height,PreparedAt=instant,RecordCount=1};
      plan.Placements.Add(new OutputPlacement{Label=artwork,Bounds=paper,Slot=1,Record=1});
      var readability=BarcodeReadability.Inspect(plan,new BarcodePrintGeometry{
       DpiX=artwork.Dpi,DpiY=artwork.Dpi,Paper=paper
      },true,100,maxPixels:16000000);
      var report=new PreflightReport(plan);
      readability.AddWarnings(report);
      foreach(var issue in report.Issues)issues.Add(new LabelIssue{Element=issue.Element,Message=issue.Message});
     }
     if(issues.Count==0)results.Add(new ScenarioFinding(scenario,null));
     else foreach(var issue in issues){
      string element=issue.Element==-2?null:
       issue.Element<=-3&&template.Symbols[-3-issue.Element].Code==null&&template.Symbols[-3-issue.Element].Matrix==null
        ?template.Symbols[-3-issue.Element].Name+" "+(-2-issue.Element)
        :QuickPrint.Name(template,issue.Element);
      results.Add(new ScenarioFinding(scenario,issue,element));
     }
    }catch(OperationCanceledException){throw;}
    catch(Exception ex){results.Add(new ScenarioFinding(scenario,new LabelIssue{Blocking=true,Message=ex.Message}));}
   }
   return results;
  }

  ScenarioFinding ShowScenarioFindings(Form owner,LabelSettings template,List<ScenarioFinding> findings){
   using var window=FeatureForm("Kontrola testovacích scenárov",840,480);
   var root=Rows();window.Controls.Add(root);
   int problems=findings.FindAll(item=>item.Issue!=null).Count;
   ManagementLayout.AddRow(root,Description(string.Format(Loc.T("Scenáre: {0} · Problémy: {1}"),template.Scenarios.Count,problems)));
   var list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true,
    Name="scenarioFindings",AccessibleName=Loc.T("Kontrola testovacích scenárov")};
   list.Items.AddRange(findings.ToArray());
   ManagementLayout.AddRow(root,list,SizeType.Percent);
   ScenarioFinding selected=null;
   var go=Btn(Loc.T("Prejsť v editore"),delegate{
    if(list.SelectedItem is not ScenarioFinding item||item.Issue==null)return;
    selected=item;window.DialogResult=DialogResult.OK;
   });
   var close=Btn(Loc.T("Zavrieť"),()=>window.DialogResult=DialogResult.Cancel);
   var footer=new UiMessageBox.ActionRow();footer.AddAction(go);footer.AddAction(close);
   ManagementLayout.AddRow(root,footer);
   window.AcceptButton=go;window.CancelButton=close;
   list.SelectedIndexChanged+=delegate{go.Enabled=(list.SelectedItem as ScenarioFinding)?.Issue!=null;};
   list.DoubleClick+=delegate{go.PerformClick();};
   list.SelectedIndex=findings.FindIndex(item=>item.Issue!=null);
   go.Enabled=list.SelectedIndex>=0;
   ApplyColors(window);
   return window.ShowDialog(owner)==DialogResult.OK?selected:null;
  }
 }
}
