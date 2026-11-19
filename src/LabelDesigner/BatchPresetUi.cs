using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow {
   readonly Label mappingInfo=new Label();
   Button prepareButton,sampleButton;
   BatchMappingResolution schema;
   bool positionsConfirmed;

   DataTableSource CurrentTable {
    get{return sheet.SelectedIndex>=0&&sheet.SelectedIndex<tables.Count?tables[sheet.SelectedIndex]:null;}
   }
   BatchPreset ActivePreset { get{return FindPreset(setup.ActivePresetName);} }

   BatchPreset FindPreset(string name) {
    return setup.Presets.Find(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));
   }

   void SetSourceControls(bool hasHeader,char split,int codePage) {
    header.Checked=hasHeader;
    separator.SelectedIndex=split=='\t'?2:split==','?1:0;
    encoding.SelectedIndex=codePage==1250?1:0;
   }

   bool ReadSource(string path,char split,int codePage,bool csv,out List<DataTableSource> source) {
    return owner.RunWork(window,csv?"import-csv":"import-xlsx","Načítavam tabuľku…",
     ()=>csv?new List<DataTableSource>{BatchData.Csv(path,split,codePage)}:BatchData.Xlsx(path),out source);
   }

   void InstallTables(List<DataTableSource> source) {
    tables.Clear();tables.AddRange(source);
    updating=true;
    try {
     sheet.Items.Clear();
     foreach(var table in tables)sheet.Items.Add(table.Name);
     SelectSourceSheet();
    }finally{updating=false;}
    Map();
   }

   void SelectSourceSheet() {
    if(tables.Count==0)return;
    var preset=ActivePreset;
    string wanted=preset==null?setup.SheetName:preset.SheetName;
    int index=tables.FindIndex(table=>table.Name==wanted);
    bool requireSheet=csvPath==null&&preset!=null&&!string.IsNullOrEmpty(wanted);
    sheet.SelectedIndex=requireSheet?index:Math.Max(0,index);
   }

   int ColumnFor(int row) {
    var column=(DataGridViewComboBoxColumn)mapping.Columns["column"];
    return column.Items.IndexOf(mapping.Rows[row].Cells["column"].Value)-1;
   }

   BatchSetup ReadMapping() {
    mapping.EndEdit();
    var candidate=ProjectXml.Decode<BatchSetup>(ProjectXml.Encode(setup));
    candidate.Header=header.Checked;candidate.Separator=Separator;candidate.CodePage=CodePage;
    candidate.GeneratedCount=(int)generated.Value;candidate.SheetName=sheet.Text;
    candidate.Bindings.Clear();candidate.QuantityColumn=-1;candidate.UniqueColumn=-1;
    for(int i=0;i<mapping.Rows.Count;i++) {
     int col=ColumnFor(i);
     switch(targets[i]) {
      case "quantity":candidate.QuantityColumn=col;break;
      case "unique":candidate.UniqueColumn=col;break;
      default:candidate.Bindings.Add(new ColumnBinding { Target=targets[i],Column=col });break;
     }
    }
    return candidate;
   }

   bool CanPrepare() {
    var preset=ActivePreset;
    if(CurrentTable==null)return preset==null&&tables.Count==0;
    if(preset!=null&&preset.Bindings.Exists(binding=>!targets.Contains(binding.Target)))return false;
    for(int i=0;i<targets.Count;i++)
     if(schema!=null&&schema.Problems.ContainsKey(targets[i])&&ColumnFor(i)<0)return false;
    return true;
   }

   void UpdateMappingState() {
    bool blocked=!CanPrepare(),showIssues=false;
    for(int i=0;i<targets.Count;i++) {
     string message="";
     bool problem=schema!=null&&schema.Problems.TryGetValue(targets[i],out message)&&ColumnFor(i)<0;
     if(!problem)message="";
     mapping.Rows[i].Cells["schema"].Value=message;
     mapping.Rows[i].Cells["schema"].ToolTipText=message;
     mapping.Rows[i].Cells["column"].ToolTipText=message;
     showIssues|=problem;
    }
    mapping.Columns["schema"].Visible=showIssues;
    var preset=ActivePreset;
    string selection=preset==null?"":string.Format(Loc.T("Predvoľba dávky: {0}"),preset.Name);
    string issue=CurrentTable==null&&preset!=null?(tables.Count==0?Loc.T("Najprv importujte tabuľku."):
     string.Format(Loc.T("Hárok „{0}“ chýba. Vyberte hárok."),preset.SheetName)):
     blocked?Loc.T("Pred prípravou skontrolujte mapovanie stĺpcov."):
     CurrentTable!=null&&!header.Checked&&!positionsConfirmed?Loc.T("Skontrolujte poradie stĺpcov v náhľade."):
     "";
    mappingInfo.Text=selection.Length>0&&issue.Length>0?selection+Environment.NewLine+issue:selection+issue;
    prepareButton.Enabled=!blocked;
    sampleButton.Enabled=!blocked&&CurrentTable!=null;
   }

   void ActivatePreset(BatchPreset preset) {
    bool needsImport=preset!=null&&csvPath!=null&&(preset.Separator!=loadedSeparator||preset.CodePage!=loadedCodePage);
    if(preset==null&&CurrentTable!=null)setup=ReadMapping();
    setup.ActivePresetName=preset==null?null:preset.Name;
    updating=true;
    try {
     if(preset!=null)SetSourceControls(preset.Header,preset.Separator,preset.CodePage);
     if(!needsImport&&preset!=null)SelectSourceSheet();
    }finally{updating=false;}
    if(needsImport){
     csvPath=null;
     InstallTables(new List<DataTableSource>());
    }else Map();
   }

   void PersistPresets() {
    owner.ChangeDocument(s=>s.Batch=setup);
   }

   void ShowPresets() {
    BatchPreset selected=null;
    bool apply=false;
    using(var dialog=owner.FeatureForm("Predvoľby dávky…",600,470)) {
     var panel=owner.FeatureStack(dialog);
     var list=new ListBox{Height=160,IntegralHeight=false,AccessibleName=dialog.Text,Name="batchPresetList"};
     StackAdd(panel,list);
     var name=owner.Input(panel,"Názov predvoľby","");
     name.Name="batchPresetName";name.MaxLength=80;
     Func<BatchPreset> current=()=>FindPreset(list.SelectedItem as string);
     var use=owner.Btn(Loc.T("Použiť predvoľbu"),delegate{selected=current();apply=selected!=null;dialog.Close();});
     var manual=owner.Btn(Loc.T("Ručné mapovanie"),delegate{apply=true;dialog.Close();});
     Action<string> refresh=chosen=>{
      list.Items.Clear();
      foreach(var preset in setup.Presets)list.Items.Add(preset.Name);
      list.SelectedItem=chosen;
     };
     var save=owner.Btn(Loc.T("Uložiť aktuálne mapovanie"),delegate{
      try {
       if(CurrentTable==null||!CanPrepare())return;
       var candidate=BatchPresets.Capture(name.Text,ReadMapping(),CurrentTable,csvPath==null);
       var existing=FindPreset(candidate.Name);
       if(existing==null&&setup.Presets.Count>=BatchPresets.MaxPresets)
        throw new InvalidOperationException(Loc.T("Najviac 24 predvolieb dávky."));
       if(existing!=null&&UiMessageBox.Show(dialog,Loc.T("Nahradiť uloženú predvoľbu?"),dialog.Text,
          MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       if(existing==null)setup.Presets.Add(candidate);
       else setup.Presets[setup.Presets.IndexOf(existing)]=candidate;
       selected=candidate;apply=true;dialog.Close();
      }catch(Exception ex){UiMessageBox.Show(dialog,ex.Message,dialog.Text);}
     });
     var remove=owner.Btn(Loc.T("Odstrániť"),delegate{
      var preset=current();if(preset==null)return;
      if(UiMessageBox.Show(dialog,Loc.T("Odstrániť vybranú predvoľbu dávky?"),dialog.Text,
         MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      if(preset==ActivePreset)ActivatePreset(null);
      setup.Presets.Remove(FindPreset(preset.Name));
      PersistPresets();
      refresh(setup.ActivePresetName);
     });
     var close=owner.Btn(Loc.T("Zavrieť"),dialog.Close);
     var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
     foreach(var button in new[]{use,manual,save,remove,close})actions.Controls.Add(button);
     StackAdd(panel,actions);
     dialog.CancelButton=close;
     list.SelectedIndexChanged+=delegate{
      var item=current();
      if(item!=null)name.Text=item.Name;
      use.Enabled=remove.Enabled=item!=null;
     };
     use.Enabled=remove.Enabled=false;
     save.Enabled=CurrentTable!=null&&CanPrepare();
     refresh(setup.ActivePresetName);
     owner.ApplyColors(dialog);dialog.ShowDialog(window);
    }
    if(!apply)return;
    try{ActivatePreset(selected);PersistPresets();}
    catch(Exception ex){owner.Error(ex);}
   }

   void ShowSample() {
    mapping.EndEdit();
    if(CurrentTable!=null&&CanPrepare())ReviewSample(CurrentTable,!header.Checked&&!positionsConfirmed);
   }

   bool ReviewSample(DataTableSource table,bool confirmPositions) {
    var columns=new List<int>();
    var titles=new List<string>();
    for(int i=0;i<targets.Count;i++) {
     int col=ColumnFor(i);
     if(col<0)continue;
     columns.Add(col);
     titles.Add(Convert.ToString(mapping.Rows[i].Cells["target"].Value)+Environment.NewLine+
      Convert.ToString(mapping.Rows[i].Cells["column"].Value));
    }
    var sample=BatchPresets.Sample(table,header.Checked,columns);
    string title="Náhľad prvých záznamov…";
    using(var dialog=owner.FeatureForm(title,850,450)) {
     dialog.MinimumSize=new Size(580,330);
     var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     dialog.Controls.Add(layout);
     var message=new Label{AutoSize=true,MaximumSize=new Size(525,0),Text=Loc.T(confirmPositions?
      "Tabuľka nemá hlavičku. Pred prípravou dávky potvrďte poradie namapovaných stĺpcov.":
      "Ukážka najviac piatich záznamov s hodnotami zo zdrojovej tabuľky.")};
     layout.Controls.Add(message,0,0);
     var grid=new DataGridView{Dock=DockStyle.Fill,Name="batchSample",AccessibleName=dialog.Text,ReadOnly=true,
      RowHeadersVisible=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
      ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
     grid.Columns.Add("source",Loc.T(table.HasCsvLineNumbers?"Riadok CSV":"Riadok"));
     for(int i=0;i<titles.Count;i++) {
      int index=grid.Columns.Add("value"+i,SampleText(titles[i]));
      grid.Columns[index].Width=185;
     }
     foreach(DataGridViewColumn col in grid.Columns)col.SortMode=DataGridViewColumnSortMode.NotSortable;
     foreach(var row in sample) {
      var values=new object[row.Values.Length+1];
      values[0]=row.Row;
      for(int i=0;i<row.Values.Length;i++)values[i+1]=SampleText(row.Values[i]);
      grid.Rows.Add(values);
     }
     layout.Controls.Add(grid,0,1);
     var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
     if(confirmPositions){
      var accept=owner.Btn(Loc.T("Potvrdiť mapovanie"),()=>dialog.DialogResult=DialogResult.OK);
      actions.Controls.Add(accept);
     }
     var close=owner.Btn(Loc.T(confirmPositions?"Zrušiť":"Zavrieť"),dialog.Close);
     actions.Controls.Add(close);dialog.CancelButton=close;
     layout.Controls.Add(actions,0,2);
     owner.ApplyColors(dialog);
     if(dialog.ShowDialog(window)!=DialogResult.OK)return false;
    }
    positionsConfirmed=true;UpdateMappingState();
    return true;
   }

   static string SampleText(string value){return value.Length<=240?value:value.Substring(0,239)+"…";}
  }
 }
}
