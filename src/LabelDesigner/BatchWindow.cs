using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow:IDisposable {
   readonly MainForm owner;readonly Form window;BatchSetup setup;
   readonly ModernComboBox sheet=new ModernComboBox(),separator=new ModernComboBox(),encoding=new ModernComboBox();
   readonly CheckBox header=new CheckBox();readonly NumericUpDown generated=new ClickWheelNumericUpDown();
   readonly DataGridView mapping=new DataGridView(),records=new DataGridView();readonly PictureBox view=new PictureBox();
   readonly Label selectionInfo=new Label(),previewCaption=new Label();
   Button exportSelected,printSelected,issuesButton;readonly Label preparedInfo=new Label();readonly BatchPreview preview;
   readonly List<DataTableSource> tables=new List<DataTableSource>();readonly List<string> targets=new List<string>();
   List<BatchRecord> batch=new List<BatchRecord>();readonly List<bool> included=new List<bool>();
   string csvPath;char loadedSeparator;int loadedCodePage;bool updating,csvLineNumbers;
   public BatchWindow(MainForm main){
    owner=main;setup=ProjectXml.Decode<BatchSetup>(ProjectXml.Encode(main.batchSetup));window=main.FeatureForm("Dávka a premenné",1100,750);window.Tag=this;
    var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));window.Controls.Add(root);
    var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};root.Controls.Add(toolbar,0,0);root.SetColumnSpan(toolbar,2);
    sheet.DropDownStyle=ComboBoxStyle.DropDownList;sheet.Width=160;
    header.Text=Loc.T("Prvý riadok je hlavička");header.AutoSize=true;
    separator.DropDownStyle=ComboBoxStyle.DropDownList;separator.Width=80;separator.Items.AddRange(new object[]{";",",","Tab"});
    encoding.DropDownStyle=ComboBoxStyle.DropDownList;encoding.Width=100;encoding.Items.AddRange(new object[]{"UTF-8","Windows-1250"});
    var preset=ActivePreset;
    SetSourceControls(preset==null?setup.Header:preset.Header,preset==null?setup.Separator:preset.Separator,preset==null?setup.CodePage:preset.CodePage);
    generated.Minimum=1;generated.Maximum=10000;generated.Value=Math.Min(10000,Math.Max(1,setup.GeneratedCount));generated.Width=75;
    var left=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=200};root.Controls.Add(left,0,1);
    mapping.Dock=DockStyle.Fill;mapping.AllowUserToAddRows=mapping.AllowUserToDeleteRows=false;mapping.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;mapping.Columns.Add("target",Loc.T("Prvok"));mapping.Columns[0].ReadOnly=true;mapping.Columns.Add(new DataGridViewComboBoxColumn{Name="column",HeaderText=Loc.T("Stĺpec")});
    mapping.Columns.Add("schema",Loc.T("Kontrola hlavičky"));mapping.Columns["schema"].ReadOnly=true;mapping.Columns["schema"].Visible=false;
    foreach(DataGridViewColumn field in mapping.Columns)field.SortMode=DataGridViewColumnSortMode.NotSortable;
    left.Panel1.Controls.Add(mapping);
    records.Dock=DockStyle.Fill;records.VirtualMode=true;records.AllowUserToAddRows=records.AllowUserToDeleteRows=false;records.SelectionMode=DataGridViewSelectionMode.FullRowSelect;records.MultiSelect=false;records.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
    records.Columns.Add(new DataGridViewCheckBoxColumn{Name="include",HeaderText=Loc.T("Zahrnúť")});records.Columns.Add("row",Loc.T("Riadok"));records.Columns.Add("count",Loc.T("Počet etikiet"));records.Columns.Add("error",Loc.T("Chyba"));for(int i=1;i<4;i++)records.Columns[i].ReadOnly=true;left.Panel2.Controls.Add(records);
    records.CellValueNeeded+=delegate(object sender,DataGridViewCellValueEventArgs e){if(e.RowIndex<0||e.RowIndex>=batch.Count)return;var record=batch[e.RowIndex];e.Value=e.ColumnIndex==0?(object)included[e.RowIndex]:e.ColumnIndex==1?(object)record.Row:e.ColumnIndex==2?(object)record.Count:record.Error??(record.Issues.Count>0?Loc.T("Upozornenie"):Loc.T("Pripravené"));};
    records.CellValuePushed+=delegate(object sender,DataGridViewCellValueEventArgs e){if(e.ColumnIndex==0&&e.RowIndex>=0&&e.RowIndex<included.Count){included[e.RowIndex]=Convert.ToBoolean(e.Value);UpdateSelectionSummary();}};
    records.CurrentCellDirtyStateChanged+=delegate{if(records.IsCurrentCellDirty)records.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    var previewPane=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};
    previewPane.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    previewPane.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    previewCaption.AutoSize=true;previewCaption.UseMnemonic=false;
    previewPane.SizeChanged+=delegate{previewCaption.MaximumSize=new Size(Math.Max(1,previewPane.ClientSize.Width-12),0);};
    previewPane.Controls.Add(previewCaption,0,0);
    view.Dock=DockStyle.Fill;view.SizeMode=PictureBoxSizeMode.Zoom;view.BackColor=Color.White;
    view.AccessibleName=Loc.T("Náhľad etikety");previewPane.Controls.Add(view,0,1);root.Controls.Add(previewPane,1,1);
    records.CurrentCellChanged+=delegate{ShowRecordPreview();};
    toolbar.Controls.Add(owner.Btn(Loc.T("Import CSV / XLSX"),Import));toolbar.Controls.Add(separator);toolbar.Controls.Add(encoding);toolbar.Controls.Add(sheet);toolbar.Controls.Add(header);
    toolbar.Controls.Add(owner.Btn(Loc.T("Premenné polia"),delegate{owner.EditVariables();Map();}));toolbar.Controls.Add(new Label{Text=Loc.T("Počet záznamov"),AutoSize=true,Margin=new Padding(3,8,3,0)});toolbar.Controls.Add(generated);
    prepareButton=owner.Btn(Loc.T("Pripraviť dávku"),Prepare);toolbar.Controls.Add(prepareButton);
    toolbar.Controls.Add(owner.Btn(Loc.T("Predvoľby dávky…"),ShowPresets));
    sampleButton=owner.Btn(Loc.T("Náhľad prvých záznamov…"),ShowSample);toolbar.Controls.Add(sampleButton);
    mappingInfo.AutoSize=true;mappingInfo.MaximumSize=new Size(435,0);mappingInfo.UseMnemonic=false;toolbar.Controls.Add(mappingInfo);
    preparedInfo.AutoSize=true;preparedInfo.MaximumSize=new Size(400,0);preparedInfo.UseMnemonic=false;toolbar.Controls.Add(preparedInfo);
    var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};root.Controls.Add(actions,0,2);root.SetColumnSpan(actions,2);
    actions.Controls.Add(owner.Btn(Loc.T("Vybrať všetko"),delegate{SelectAll(true);}));actions.Controls.Add(owner.Btn(Loc.T("Zrušiť výber"),delegate{SelectAll(false);}));exportSelected=owner.Btn(Loc.T("Exportovať vybrané"),Export);printSelected=owner.Btn(Loc.T("Tlačiť vybrané"),Print);actions.Controls.Add(exportSelected);actions.Controls.Add(printSelected);selectionInfo.AutoSize=true;selectionInfo.UseMnemonic=false;selectionInfo.MaximumSize=new Size(360,0);actions.Controls.Add(selectionInfo);
    issuesButton=owner.Btn(Loc.T("Problémy vstupných dát…"),ShowIssues);actions.Controls.Add(issuesButton);
    sheet.SelectedIndexChanged+=delegate{if(!updating)Map();};header.CheckedChanged+=delegate{if(!updating)Map();};
    mapping.CurrentCellDirtyStateChanged+=delegate{if(!updating&&mapping.IsCurrentCellDirty)mapping.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    mapping.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs args){
     if(!updating&&args.RowIndex>=0&&args.ColumnIndex==1){positionsConfirmed=false;Clear();UpdateMappingState();}
    };
    generated.ValueChanged+=delegate{if(!updating)Clear();};separator.SelectedIndexChanged+=Reload;encoding.SelectedIndexChanged+=Reload;
    owner.ApplyColors(window);Map();preview=new BatchPreview(view,message=>preparedInfo.Text=message);
   }
   char Separator{get{return separator.SelectedIndex==2?'\t':separator.Text[0];}}int CodePage{get{return encoding.SelectedIndex==0?65001:1250;}}
   void Clear(){records.RowCount=0;batch=new List<BatchRecord>();included.Clear();preparedInfo.Text="";ShowRecordPreview();UpdateSelectionSummary();}
   void Map(){
    Clear();positionsConfirmed=false;updating=true;try{
     mapping.Rows.Clear();targets.Clear();var table=CurrentTable;
     var preset=ActivePreset;header.Enabled=preset==null;generated.Enabled=tables.Count==0&&preset==null;
     var choices=new List<string>{Loc.T("Neprepojené")};int columns=0;if(table!=null)foreach(var row in table.Rows)columns=Math.Max(columns,row.Length);
     for(int i=0;i<columns;i++)choices.Add((i+1)+": "+(header.Checked&&table.Rows.Count>0?BatchData.Value(table.Rows[0],i):Loc.T("Stĺpec")));
     var column=(DataGridViewComboBoxColumn)mapping.Columns[1];column.Items.Clear();column.Items.AddRange(choices.ToArray());
     schema=preset!=null&&table!=null?BatchPresets.Resolve(preset,table,columns):null;
     Action<string,string,int> add=(key,name,index)=>{
      int found;
      if(preset!=null)index=schema!=null&&schema.Columns.TryGetValue(key,out found)?found:-1;
      targets.Add(key);mapping.Rows.Add(name,index>=0&&index<columns?choices[index+1]:choices[0]);
     };
     Action<string,string> bind=(key,name)=>{var saved=setup.Bindings.Find(b=>b.Target==key);add(key,name,saved==null?-1:saved.Column);};
     var model=owner.Read();if(!model.HideBarcode)bind("code",Loc.T("Čiarový kód"));for(int i=0;i<model.Texts.Count;i++)bind("text:"+i,model.Texts[i].Text);for(int i=0;i<model.Symbols.Count;i++)if(model.Symbols[i].Code!=null||model.Symbols[i].Matrix!=null)bind("symbol:"+i,model.Symbols[i].Name+" "+(i+1));add("quantity",Loc.T("Počet etikiet"),setup.QuantityColumn);
     if(table!=null)add("unique",Loc.T("Stĺpec s jedinečnými hodnotami"),setup.UniqueColumn<columns?setup.UniqueColumn:-1);
     UpdateMappingState();
    }finally{updating=false;}
   }
   void Import(){using(var dialog=new OpenFileDialog{Filter="CSV / Excel|*.csv;*.xlsx"})if(dialog.ShowDialog(window)==DialogResult.OK)LoadFile(dialog.FileName,Separator,CodePage);}
   bool LoadFile(string path,char split,int codePage){
    try{
     bool csv=Path.GetExtension(path).Equals(".csv",StringComparison.OrdinalIgnoreCase);List<DataTableSource> candidate;
     if(!ReadSource(path,split,codePage,csv,out candidate))return false;
     csvPath=csv?path:null;loadedSeparator=split;loadedCodePage=codePage;
     InstallTables(candidate);return true;
    }catch(Exception ex){owner.Error(ex);return false;}
   }
   void Reload(object sender,EventArgs args){if(updating||csvPath==null)return;if(!LoadFile(csvPath,Separator,CodePage)){updating=true;try{separator.SelectedIndex=loadedSeparator=='\t'?2:loadedSeparator==','?1:0;encoding.SelectedIndex=loadedCodePage==1250?1:0;}finally{updating=false;}}}
   void Prepare(){try{
    var candidate=ReadMapping();var table=CurrentTable;
    if(!CanPrepare())return;
    if(table!=null&&!candidate.Header&&!positionsConfirmed&&!ReviewSample(table,true))return;
    var model=owner.Read();model.Batch=candidate;DateTime instant=owner.currentTime();List<BatchRecord> prepared;
    if(!owner.RunWork(window,"prepare-batch","Pripravujem dávku…",()=>BatchProcessing.Prepare(model,table,candidate.Header,candidate.Bindings,candidate.QuantityColumn,candidate.GeneratedCount,instant),out prepared))return;
    owner.ChangeDocument(s=>s.Batch=candidate);setup=candidate;Clear();batch=prepared;
    csvLineNumbers=table!=null&&table.HasCsvLineNumbers;records.Columns["row"].HeaderText=SourceRowHeading;
    foreach(var item in batch)included.Add(true);records.RowCount=batch.Count;UpdateSelectionSummary();preparedInfo.Text=Loc.T("Údaje pripravené")+": "+instant.ToString("g");if(batch.Count>0){records.CurrentCell=records.Rows[0].Cells[0];ShowRecordPreview();}
   }catch(Exception ex){owner.Error(ex);}}
   void SelectAll(bool selected){records.EndEdit();for(int i=0;i<included.Count;i++)included[i]=selected;records.Invalidate();UpdateSelectionSummary();}
   bool Plan(out OutputPlan result,out LabelSettings model){records.EndEdit();var selected=new List<BatchRecord>();for(int i=0;i<batch.Count;i++)if(included[i])selected.Add(batch[i]);var format=owner.Read();model=format;return owner.RunWork(window,"plan-batch","Pripravujem tlačovú úlohu…",()=>OutputPlan.Create(selected,format),out result);}
   string SourceRowHeading{get{return Loc.T(csvLineNumbers?"Riadok CSV":"Riadok");}}
   void NavigateIssue(LabelIssue issue){NavigateRecord(issue.Record);}
   void NavigateRecord(int sourceRow){
    int index=batch.FindIndex(record=>record.Row==sourceRow);
    if(index<0)return;
    records.CurrentCell=records.Rows[index].Cells["row"];
    records.Focus();
   }
   void ShowRecordPreview(){
    if(preview==null)return;
    int index=records.CurrentCell==null?-1:records.CurrentCell.RowIndex;
    var record=index>=0&&index<batch.Count?batch[index]:null;
    previewCaption.Text=record==null?"":SourceRowHeading+" "+record.Row+(record.Error==null?"":
     Environment.NewLine+Loc.T(record.PreviewLabel==null?"Náhľad tohto riadka nie je dostupný.":"Náhľad použiteľných prvkov. Záznam má chyby."));
    preview.Request(record==null?null:record.PreviewLabel);
   }
   void ShowIssues(){
    var findings=new List<BatchInputIssue>();
    foreach(var record in batch)findings.AddRange(record.Issues);
    if(findings.Count==0)return;
    int targetRow=0;
    using(var dialog=owner.FeatureForm("Problémy vstupných dát…",850,530)){
     dialog.MinimumSize=new Size(580,350);
     var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
     layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
     dialog.Controls.Add(layout);
     layout.Controls.Add(new Label{Text=Loc.T("Opravte a znova importujte súbor alebo vynechajte chybné záznamy."),AutoSize=true},0,0);
     var grid=new DataGridView{Dock=DockStyle.Fill,Name="batchIssues",AccessibleName=dialog.Text,
      VirtualMode=true,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,
      MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
     grid.Columns.Add("source",SourceRowHeading);grid.Columns.Add("column",Loc.T("Stĺpec"));
     grid.Columns.Add("severity",Loc.T("Stav"));grid.Columns.Add("issue",Loc.T("Podrobnosti"));
     grid.Columns[0].FillWeight=18;grid.Columns[1].FillWeight=15;grid.Columns[2].FillWeight=24;
     grid.Columns[3].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
     grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
     foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
     grid.CellValueNeeded+=delegate(object sender,DataGridViewCellValueEventArgs args){
      var issue=findings[args.RowIndex];
      switch(args.ColumnIndex){
       case 0:args.Value=issue.Row;break;
       case 1:args.Value=issue.Column<0?(object)"–":issue.Column+1;break;
       case 2:args.Value=Loc.T(issue.Blocking?"Chyba":"Upozornenie");break;
       case 3:args.Value=issue.Message;break;
      }
     };
     grid.RowCount=findings.Count;
     layout.Controls.Add(grid,0,1);
     Action go=delegate{
      if(grid.CurrentCell==null)return;
      targetRow=findings[grid.CurrentCell.RowIndex].Row;
      dialog.DialogResult=DialogResult.OK;
     };
     grid.CellDoubleClick+=delegate(object sender,DataGridViewCellEventArgs args){if(args.RowIndex>=0)go();};
     var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
     var jump=owner.Btn(Loc.T("Prejsť na etiketu"),go);
     var close=owner.Btn(Loc.T("Zavrieť"),dialog.Close);
     actions.Controls.Add(jump);actions.Controls.Add(close);
     layout.Controls.Add(actions,0,2);
     dialog.AcceptButton=jump;dialog.CancelButton=close;
     owner.ApplyColors(dialog);
     if(dialog.ShowDialog(window)!=DialogResult.OK)return;
    }
    NavigateRecord(targetRow);
   }
   void UpdateSelectionSummary(){int selected=0,errors=0;long labels=0;for(int i=0;i<batch.Count;i++)if(included[i]){selected++;labels+=batch[i].Count;if(batch[i].Error!=null)errors++;}selectionInfo.Text=Loc.T("Vybrané záznamy")+": "+selected+"  •  "+Loc.T("Etikety spolu")+": "+labels+"  •  "+Loc.T("Chyby")+": "+errors;exportSelected.Enabled=printSelected.Enabled=selected>0&&errors==0;issuesButton.Enabled=batch.Exists(record=>record.Issues.Count>0);}
   void Export(){try{OutputPlan plan;LabelSettings model;if(Plan(out plan,out model))owner.ExportJob(plan,model.Dpi,null,window,NavigateIssue);}catch(Exception ex){owner.Error(ex);}}
   void Print(){try{OutputPlan plan;LabelSettings model;if(Plan(out plan,out model))using(var form=owner.CreateJobPrintWindow(model,plan,NavigateIssue))form.ShowDialog(window);}catch(Exception ex){owner.Error(ex);}}
   public void Show(){window.ShowDialog(owner);}
   public void Dispose(){preview.Dispose();window.Dispose();}
  }
 }
}
