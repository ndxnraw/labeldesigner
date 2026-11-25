using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  const string WorkspaceBackupTitle="Prenosná záloha a obnova…";
  const string WorkspaceRestoreTitle="Náhľad obnovy pracovných údajov";

  void BuildWorkspaceBackupPage(Form dialog,Panel page) {
   var rows=ScrollRows(page);
   if(workspace==null){
    ManagementLayout.AddRow(rows,Description("Pracovné úložisko nie je dostupné."));
    return;
   }
   var service=new WorkspaceBackup(workspace.DirectoryPath,LocalPrintHistory());
   ManagementLayout.AddRow(rows,Description("Vyberte, čo má obsahovať záloha."));
   var templates=Check(rows,"Šablóny",true);
   var profiles=Check(rows,"Profily médií",true);
   var batches=Check(rows,"Dávky a uzávierky",false);
   ManagementLayout.AddRow(rows,Description("Záloha dávok obsahuje identifikátory, počty a uzávierky. Obsah na dotlač sa uchováva v histórii tlače."));
   var export=Action("Exportovať zálohu do ZIPu…",()=>ExportWorkspace(dialog,service,templates.Checked,profiles.Checked,batches.Checked));
   ManagementLayout.AddRow(rows,export);
   ManagementLayout.AddRow(rows,Action("Otvoriť ZIP na obnovu…",()=>RestoreWorkspace(dialog,service)));
   EventHandler update=delegate{export.Enabled=templates.Checked||profiles.Checked||batches.Checked;};
   templates.CheckedChanged+=update;
   profiles.CheckedChanged+=update;
   batches.CheckedChanged+=update;
  }

  void ExportWorkspace(Form parent,WorkspaceBackup service,bool templates,bool profiles,bool batches) {
   using(var pick=new SaveFileDialog{Title=Loc.T("Exportovať zálohu do ZIPu…"),Filter="ZIP (*.zip)|*.zip",DefaultExt="zip",AddExtension=true,FileName="LabelDesigner-workspace.zip"}){
    if(pick.ShowDialog(parent)!=DialogResult.OK)return;
    string path=pick.FileName;
    try{
     string exported;
     if(RunWork(parent,"workspace-backup","Vytváram zálohu…",()=>{service.Export(path,templates,profiles,batches);return path;},out exported))
      UiMessageBox.Show(parent,Loc.T("Záloha bola uložená:")+Environment.NewLine+exported,Loc.T(WorkspaceBackupTitle));
    }catch(Exception ex){Error(ex);}
   }
  }

  void RestoreWorkspace(Form parent,WorkspaceBackup service) {
   using(var pick=new OpenFileDialog{Title=Loc.T("Otvoriť ZIP na obnovu…"),Filter="ZIP (*.zip)|*.zip"}){
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
    foreach(string heading in new[]{"Typ","Názov","Zo zálohy","Miestne údaje","Akcia"})
     items.Columns.Add(heading,Loc.T(heading));
    items.Columns[0].FillWeight=65;items.Columns[1].FillWeight=145;items.Columns[4].FillWeight=70;
    root.Controls.Add(items,0,3);

    var footer=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,
     AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill};
    root.Controls.Add(footer,0,4);
    var chooseBatch=new CheckBox{AutoSize=true,Name="workspaceUseBatchBackup",Text=Loc.T("Použiť údaje zo zálohy pre vybranú dávku")};
    footer.Controls.Add(chooseBatch);
    var batchDetails=new Label{AutoSize=true,MaximumSize=new Size(560,0),Name="workspaceBatchDetails"};
    footer.Controls.Add(batchDetails);
    var status=new Label{AutoSize=true,UseMnemonic=false,MaximumSize=new Size(590,0)};
    footer.Controls.Add(status);
    var actions=new UiMessageBox.ActionRow();
    var restore=Btn(Loc.T("Obnoviť pracovné údaje"),()=>dialog.DialogResult=DialogResult.OK);
    var cancel=Btn(Loc.T("Zrušiť"),()=>dialog.DialogResult=DialogResult.Cancel);
    cancel.DialogResult=DialogResult.Cancel;
    actions.AddAction(restore);actions.AddAction(cancel);root.Controls.Add(actions,0,5);
    dialog.CancelButton=cancel;

    WorkspaceRestorePlan candidate=null;
    var useBackup=new HashSet<string>(StringComparer.Ordinal);
    Func<WorkspaceRestoreItem> selected=delegate{
     int index=items.CurrentRow==null?-1:items.CurrentRow.Index;
     return candidate==null||index<0||index>=candidate.Items.Count?null:candidate.Items[index];
    };
    bool showingChoice=false;
    Action showChoice=delegate{
     var item=selected();
     bool selectable=item!=null&&item.BatchConflict&&item.Action!=WorkspaceRestoreAction.Invalid;
     showingChoice=true;
     chooseBatch.Visible=selectable;
     chooseBatch.Checked=selectable&&useBackup.Contains(item.BatchId);
     showingChoice=false;
     batchDetails.Text=item==null?"":item.BatchDetails??"";
     batchDetails.Visible=batchDetails.Text.Length>0;
    };
    items.CellValueNeeded+=delegate(object sender,DataGridViewCellValueEventArgs e){
     if(candidate==null||e.RowIndex<0||e.RowIndex>=candidate.Items.Count)return;
     var item=candidate.Items[e.RowIndex];
     switch(e.ColumnIndex){
      case 0:e.Value=Loc.T(item.BatchId!=null?"Dávka":item.SourcePrinter==null?"Šablóna":"Profil média");break;
      case 1:e.Value=item.Name;break;
      case 2:e.Value=item.BackupBatchSummary??item.SourcePrinter;break;
      case 3:e.Value=item.LocalBatchSummary??item.TargetPrinter;break;
      case 4:e.Value=Loc.T(item.BatchId!=null&&item.Action==WorkspaceRestoreAction.Invalid?"Nezlučiteľná dávka":
       item.BatchId!=null&&item.Action==WorkspaceRestoreAction.Keep&&!item.BatchConflict?"Zhodná dávka":RestoreActionText(item.Action));break;
     }
    };
    Action update=delegate{
     var mapping=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
     foreach(DataGridViewRow row in map.Rows){
      var target=row.Cells[1].Value as string;
      if(target!=null)mapping.Add((string)row.Cells[0].Value,target);
     }
     try{
      candidate=review.Preview(replace.Checked,mapping,useBackup);
      int add=0,overwrite=0,keep=0,invalid=0,incompatible=0;
      foreach(var item in candidate.Items){
       if(item.Action==WorkspaceRestoreAction.Add)add++;
       else if(item.Action==WorkspaceRestoreAction.Replace)overwrite++;
       else if(item.Action==WorkspaceRestoreAction.Keep)keep++;
       else if(item.BatchId==null)invalid++;
       else incompatible++;
      }
      status.Text=string.Format(Loc.T("{0} pridať, {1} nahradiť, {2} ponechať."),add,overwrite,keep);
      if(invalid>0)status.Text+=Environment.NewLine+string.Format(Loc.T("Poškodené šablóny sa pri obnove preskočia: {0}"),invalid);
      if(incompatible>0)status.Text+=Environment.NewLine+string.Format(Loc.T("Nezlučiteľné dávky sa pri obnove preskočia: {0}"),incompatible);
      restore.Enabled=add+overwrite>0;
     }catch(InvalidDataException ex){
      candidate=null;status.Text=ex.Message;restore.Enabled=false;
     }
     items.RowCount=candidate==null?0:candidate.Items.Count;
     items.Invalidate();
     showChoice();
    };
    foreach(var grid in new[]{map,items})
     foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
    map.CurrentCellDirtyStateChanged+=delegate{if(map.IsCurrentCellDirty)map.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    map.CellValueChanged+=delegate{update();};
    replace.CheckedChanged+=delegate{update();};
    items.CurrentCellChanged+=delegate{showChoice();};
    chooseBatch.CheckedChanged+=delegate{
     var item=selected();
     if(showingChoice||item==null||!item.BatchConflict||item.Action==WorkspaceRestoreAction.Invalid)return;
     if(chooseBatch.Checked)useBackup.Add(item.BatchId);else useBackup.Remove(item.BatchId);
     update();
    };
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
