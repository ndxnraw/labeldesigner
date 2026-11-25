using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
public partial class PrintPanel {
 ModernButton fixIssue;
 LabelIssue blockedRepair;

 void BuildRepairAction(FlowLayoutPanel actions){
  fixIssue=Button(Loc.T("Prejsť na opravu"),()=>GoToRepair(blockedRepair));
  fixIssue.Name="fixPrintIssue";
  fixIssue.Visible=false;
  actions.Controls.Add(fixIssue);
 }

 void UpdateRepairAction(){
  blockedRepair=busy||sending||replaying||EditIssueRequested==null?null:
   currentReport?.Issues.Find(issue=>issue.Blocking&&OutputReviewDialog.HasRepairTarget(issue));
  fixIssue.Visible=blockedRepair!=null;
  print.Visible=blockedRepair==null;
  fixIssue.AccessibleDescription=blockedRepair?.ToString();
 }

 void GoToRepair(LabelIssue issue){
  var navigate=EditIssueRequested;
  if(navigate==null||issue==null||sending||busy)return;
  BackRequested?.Invoke(this,EventArgs.Empty);
  navigate(issue);
 }

 void FitCommonSettings(TableLayoutPanel side,TableLayoutPanel fields){
  bool fitting=false;System.Action fit=delegate{if(fitting||side.IsDisposed)return;fitting=true;try{
   int settings=(int)printSections.RowStyles[0].Height+System.Math.Max(100,Font.Height*4);
   int height=System.Math.Min(fields.PreferredSize.Height,System.Math.Max(70,side.ClientSize.Height-settings));
   if(side.RowStyles[0].SizeType!=SizeType.Absolute||side.RowStyles[0].Height!=height){side.RowStyles[0].SizeType=SizeType.Absolute;side.RowStyles[0].Height=height;}
  }finally{fitting=false;}};
  side.SizeChanged+=delegate{fit();};fields.SizeChanged+=delegate{fit();};printSections.Layout+=delegate{fit();};FontChanged+=delegate{fit();};
 }
 TableLayoutPanel printDetails;
 readonly ModernTabs printSections=new ModernTabs{Dock=DockStyle.Fill,Name="printSections",AccessibleName=Loc.T("Nastavenia tlače")};
 TableLayoutPanel PrintSection(string name){var page=printSections.AddPage(Loc.T(name));var stack=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(8)};stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));page.Controls.Add(stack);return stack;}
 void OrganizePrintSettings(TableLayoutPanel side){
  advancedFields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=1,Margin=new Padding(0)};advancedFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));advancedFields.RowStyles.Add(new RowStyle(SizeType.Percent,100));advancedFields.Controls.Add(printSections,0,0);side.Controls.Add(advancedFields,0,1);
  TableLayoutPanel media=PrintSection("Médium"),calibration=PrintSection("Kalibrácia"),zebra=PrintSection("Zebra"),diagnostics=PrintSection("Detaily");printDetails=diagnostics;
  ProfileSelector(media);Add(media,new Label{Text=Loc.T("Papier"),AutoSize=true});papers.DropDownStyle=ComboBoxStyle.DropDownList;papers.DisplayMember="PaperName";papers.AccessibleName=Loc.T("Papier");Add(media,papers);landscape.Text=Loc.T("Na šírku");landscape.AutoSize=true;Add(media,landscape);paperInfo.AutoSize=true;paperInfo.MaximumSize=new Size(290,0);paperInfo.UseMnemonic=false;Add(media,paperInfo);
  driverProperties=Button(Loc.T("Vlastnosti tlačiarne…"),delegate{ConfigurePrinter(d=>d.ShowDialog(FindForm()));});Add(media,driverProperties);ProfileControls(calibration,media,zebra);Add(diagnostics,new Label{Text=Loc.T("Diagnostika popisuje aktuálnu tlačovú úlohu a nastavenia výstupu."),AutoSize=true,MaximumSize=new Size(290,0)});AddPrintDiagnostics(diagnostics);AddReadabilityButton(diagnostics);AddHistoryOption(diagnostics);
 }
}
}
