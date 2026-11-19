using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  const string WorkspaceBackupTitle="Prenosná záloha a obnova…";
  const string WorkspaceRestoreTitle="Náhľad obnovy pracovných údajov";

  void ShowWorkspaceBackup() {
   if(workspace==null)return;
   var service=new WorkspaceBackup(workspace.DirectoryPath);
   using(var dialog=FeatureForm(WorkspaceBackupTitle,590,340)){
    var stack=FeatureStack(dialog);
    var templates=Check(stack,"Šablóny",true);
    var profiles=Check(stack,"Profily médií",true);
    var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
    var export=Btn(Loc.T("Exportovať zálohu do ZIPu…"),()=>ExportWorkspace(dialog,service,templates.Checked,profiles.Checked));
    var restore=Btn(Loc.T("Otvoriť ZIP na obnovu…"),()=>RestoreWorkspace(dialog,service));
    var close=Btn(Loc.T("Zavrieť"),dialog.Close);
    actions.Controls.Add(export);actions.Controls.Add(restore);actions.Controls.Add(close);
    StackAdd(stack,actions);
    EventHandler update=delegate{export.Enabled=templates.Checked||profiles.Checked;};
    templates.CheckedChanged+=update;profiles.CheckedChanged+=update;
    dialog.CancelButton=close;
    ApplyColors(dialog);
    dialog.ShowDialog(this);
   }
  }

  void ExportWorkspace(Form parent,WorkspaceBackup service,bool templates,bool profiles) {
   using(var pick=new SaveFileDialog{Filter="ZIP (*.zip)|*.zip",DefaultExt="zip",AddExtension=true,FileName="LabelDesigner-workspace.zip"}){
    if(pick.ShowDialog(parent)!=DialogResult.OK)return;
    string path=pick.FileName;
    try{
     string exported;
     if(RunWork(parent,"workspace-backup","Vytváram zálohu…",()=>{service.Export(path,templates,profiles);return path;},out exported))
      UiMessageBox.Show(parent,Loc.T("Záloha bola uložená:")+Environment.NewLine+exported,Loc.T(WorkspaceBackupTitle));
    }catch(Exception ex){Error(ex);}
   }
  }

  void RestoreWorkspace(Form parent,WorkspaceBackup service) {
   using(var pick=new OpenFileDialog{Filter="ZIP (*.zip)|*.zip"}){
    if(pick.ShowDialog(parent)!=DialogResult.OK)return;
    try{
     WorkspaceRestoreReview review;
     if(!RunWork(parent,"workspace-backup-read","Overujem zálohu…",()=>service.Open(pick.FileName),out review))return;
     var printers=new List<string>();
     if(review.SourcePrinters.Count>0){
      try{foreach(string name in PrinterSettings.InstalledPrinters)if(!string.IsNullOrWhiteSpace(name))printers.Add(name);}
      catch(System.ComponentModel.Win32Exception){ }
      catch(InvalidPrinterException){ }
     }
     var plan=ReviewWorkspaceRestore(parent,review,printers);
     if(plan==null)return;
     WorkspaceRestoreResult result;
     if(RunWork(parent,"workspace-restore","Obnovujem pracovné údaje…",()=>service.Restore(plan),out result)&&result.Applied>0)
      UiMessageBox.Show(parent,string.Format(Loc.T("Obnovené položky: {0}"),result.Applied)+Environment.NewLine+
       Loc.T("Záloha pred obnovou:")+Environment.NewLine+result.BackupPath,Loc.T(WorkspaceBackupTitle));
    }catch(Exception ex){Error(ex);}
   }
  }

  WorkspaceRestorePlan ReviewWorkspaceRestore(Form parent,WorkspaceRestoreReview review,List<string> printers) {
   using(var dialog=FeatureForm(WorkspaceRestoreTitle,900,660)){
    dialog.MinimumSize=new Size(640,475);
    var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(12)};
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent,review.SourcePrinters.Count==0?0:35));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.Percent,65));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    dialog.Controls.Add(root);

    var map=WorkspaceReviewGrid("workspacePrinterMap");
    map.AccessibleName=Loc.T("Priradenie tlačiarní");
    map.Visible=review.SourcePrinters.Count>0;
    if(map.Visible){
     root.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(590,0),Text=Loc.T(
      "Vyberte cieľovú tlačiareň alebo ponechajte pôvodný názov. Pri zmene tlačiarne overte kalibráciu skúšobnou tlačou.")},0,0);
     map.Columns.Add("source",Loc.T("Pôvodná tlačiareň"));
     map.Columns[0].ReadOnly=true;
     map.Columns.Add(new DataGridViewComboBoxColumn{Name="target",HeaderText=Loc.T("Cieľová tlačiareň")});
     var available=new SortedSet<string>(printers,StringComparer.OrdinalIgnoreCase);
     foreach(string source in review.SourcePrinters){
      int index=map.Rows.Add();
      var row=map.Rows[index];
      row.Cells[0].Value=source;
      var target=(DataGridViewComboBoxCell)row.Cells[1];
      target.Items.Add(source);
      foreach(string printer in available)if(!string.Equals(source,printer,StringComparison.OrdinalIgnoreCase))target.Items.Add(printer);
      target.Value=source;
     }
     root.Controls.Add(map,0,1);
    }

    var replace=new CheckBox{AutoSize=true,Name="workspaceReplace",Text=Loc.T("Nahradiť položky s rovnakým názvom")};
    root.Controls.Add(replace,0,2);
    var items=WorkspaceReviewGrid("workspaceRestoreItems");
    items.AccessibleName=dialog.Text;items.ReadOnly=true;items.VirtualMode=true;
    foreach(string heading in new[]{"Typ","Názov","Pôvodná tlačiareň","Cieľová tlačiareň","Akcia"})
     items.Columns.Add(heading,Loc.T(heading));
    items.Columns[0].FillWeight=65;items.Columns[1].FillWeight=145;items.Columns[4].FillWeight=70;
    root.Controls.Add(items,0,3);

    var status=new Label{AutoSize=true,UseMnemonic=false,MaximumSize=new Size(590,0)};
    root.Controls.Add(status,0,4);
    var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
    var restore=Btn(Loc.T("Obnoviť pracovné údaje"),()=>dialog.DialogResult=DialogResult.OK);
    var cancel=Btn(Loc.T("Zrušiť"),dialog.Close);
    actions.Controls.Add(restore);actions.Controls.Add(cancel);root.Controls.Add(actions,0,5);
    dialog.CancelButton=cancel;

    WorkspaceRestorePlan candidate=null;
    items.CellValueNeeded+=delegate(object sender,DataGridViewCellValueEventArgs e){
     if(candidate==null||e.RowIndex<0||e.RowIndex>=candidate.Items.Count)return;
     var item=candidate.Items[e.RowIndex];
     switch(e.ColumnIndex){
      case 0:e.Value=Loc.T(item.SourcePrinter==null?"Šablóna":"Profil média");break;
      case 1:e.Value=item.Name;break;
      case 2:e.Value=item.SourcePrinter;break;
      case 3:e.Value=item.TargetPrinter;break;
      case 4:e.Value=Loc.T(RestoreActionText(item.Action));break;
     }
    };
    Action update=delegate{
     var mapping=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
     foreach(DataGridViewRow row in map.Rows){
      var target=row.Cells[1].Value as string;
      if(target!=null)mapping.Add((string)row.Cells[0].Value,target);
     }
     try{
      candidate=review.Preview(replace.Checked,mapping);
      int add=0,overwrite=0,keep=0,invalid=0;
      foreach(var item in candidate.Items){
       if(item.Action==WorkspaceRestoreAction.Add)add++;
       else if(item.Action==WorkspaceRestoreAction.Replace)overwrite++;
       else if(item.Action==WorkspaceRestoreAction.Keep)keep++;
       else invalid++;
      }
      status.Text=string.Format(Loc.T("{0} pridať, {1} nahradiť, {2} ponechať."),add,overwrite,keep);
      if(invalid>0)status.Text+=Environment.NewLine+string.Format(Loc.T("Poškodené šablóny sa pri obnove preskočia: {0}"),invalid);
      restore.Enabled=add+overwrite>0;
     }catch(InvalidDataException ex){
      candidate=null;status.Text=ex.Message;restore.Enabled=false;
     }
     items.RowCount=candidate==null?0:candidate.Items.Count;
     items.Invalidate();
    };
    foreach(var grid in new[]{map,items})
     foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
    map.CurrentCellDirtyStateChanged+=delegate{if(map.IsCurrentCellDirty)map.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    map.CellValueChanged+=delegate{update();};
    replace.CheckedChanged+=delegate{update();};
    update();
    ApplyColors(dialog);
    return dialog.ShowDialog(parent)==DialogResult.OK?candidate:null;
   }
  }

  static DataGridView WorkspaceReviewGrid(string name) {
   return new DataGridView{Name=name,Dock=DockStyle.Fill,RowHeadersVisible=false,AllowUserToAddRows=false,
    AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,
    SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
  }

  static string RestoreActionText(WorkspaceRestoreAction action) {
   switch(action){
    case WorkspaceRestoreAction.Add:return "Pridať";
    case WorkspaceRestoreAction.Replace:return "Nahradiť";
    case WorkspaceRestoreAction.Invalid:return "Poškodená šablóna";
    default:return "Ponechať";
   }
  }
 }
}
