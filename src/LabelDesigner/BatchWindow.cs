using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed class BatchWindow:IDisposable {
   readonly MainForm owner;readonly Form window;BatchSetup setup;
   readonly ModernComboBox sheet=new ModernComboBox(),separator=new ModernComboBox(),encoding=new ModernComboBox();
   readonly CheckBox header=new CheckBox();readonly NumericUpDown generated=new NumericUpDown();
   readonly DataGridView mapping=new DataGridView(),records=new DataGridView();readonly PictureBox view=new PictureBox();
   readonly Label preparedInfo=new Label();readonly BatchPreview preview;
   readonly List<DataTableSource> tables=new List<DataTableSource>();readonly List<string> targets=new List<string>();
   List<BatchRecord> batch=new List<BatchRecord>();readonly List<bool> included=new List<bool>();
   string csvPath;char loadedSeparator;int loadedCodePage;bool updating;
   public BatchWindow(MainForm main){
    owner=main;setup=ProjectXml.Decode<BatchSetup>(ProjectXml.Encode(main.batchSetup));window=main.FeatureForm("Dávka a premenné",1100,750);window.Tag=this;
    var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));window.Controls.Add(root);
    var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};root.Controls.Add(toolbar,0,0);root.SetColumnSpan(toolbar,2);
    sheet.DropDownStyle=ComboBoxStyle.DropDownList;sheet.Width=160;
    header.Text=Loc.T("Prvý riadok je hlavička");header.Checked=setup.Header;header.AutoSize=true;
    separator.DropDownStyle=ComboBoxStyle.DropDownList;separator.Width=80;separator.Items.AddRange(new object[]{";",",","Tab"});separator.SelectedIndex=setup.Separator=='\t'?2:setup.Separator==','?1:0;
    encoding.DropDownStyle=ComboBoxStyle.DropDownList;encoding.Width=100;encoding.Items.AddRange(new object[]{"UTF-8","Windows-1250"});encoding.SelectedIndex=setup.CodePage==1250?1:0;
    generated.Minimum=1;generated.Maximum=10000;generated.Value=Math.Min(10000,Math.Max(1,setup.GeneratedCount));generated.Width=75;
    var left=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=200};root.Controls.Add(left,0,1);
    mapping.Dock=DockStyle.Fill;mapping.AllowUserToAddRows=mapping.AllowUserToDeleteRows=false;mapping.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;mapping.Columns.Add("target",Loc.T("Prvok"));mapping.Columns[0].ReadOnly=true;mapping.Columns.Add(new DataGridViewComboBoxColumn{Name="column",HeaderText=Loc.T("Stĺpec")});left.Panel1.Controls.Add(mapping);
    records.Dock=DockStyle.Fill;records.VirtualMode=true;records.AllowUserToAddRows=records.AllowUserToDeleteRows=false;records.SelectionMode=DataGridViewSelectionMode.FullRowSelect;records.MultiSelect=false;records.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
    records.Columns.Add(new DataGridViewCheckBoxColumn{Name="include",HeaderText=Loc.T("Zahrnúť")});records.Columns.Add("row",Loc.T("Riadok"));records.Columns.Add("count",Loc.T("Počet etikiet"));records.Columns.Add("error",Loc.T("Chyba"));for(int i=1;i<4;i++)records.Columns[i].ReadOnly=true;left.Panel2.Controls.Add(records);
    records.CellValueNeeded+=delegate(object sender,DataGridViewCellValueEventArgs e){if(e.RowIndex<0||e.RowIndex>=batch.Count)return;var record=batch[e.RowIndex];e.Value=e.ColumnIndex==0?(object)included[e.RowIndex]:e.ColumnIndex==1?(object)record.Row:e.ColumnIndex==2?(object)record.Count:record.Error??Loc.T("Pripravené");};
    records.CellValuePushed+=delegate(object sender,DataGridViewCellValueEventArgs e){if(e.ColumnIndex==0&&e.RowIndex>=0&&e.RowIndex<included.Count)included[e.RowIndex]=Convert.ToBoolean(e.Value);};
    records.CurrentCellDirtyStateChanged+=delegate{if(records.IsCurrentCellDirty)records.CommitEdit(DataGridViewDataErrorContexts.Commit);};
    view.Dock=DockStyle.Fill;view.SizeMode=PictureBoxSizeMode.Zoom;view.BackColor=Color.White;root.Controls.Add(view,1,1);

    records.SelectionChanged+=delegate{if(preview==null)return;int row=records.CurrentCell==null?-1:records.CurrentCell.RowIndex;preview.Request(row>=0&&row<batch.Count?batch[row].Label:null);};
    toolbar.Controls.Add(owner.Btn(Loc.T("Import CSV / XLSX"),Import));toolbar.Controls.Add(separator);toolbar.Controls.Add(encoding);toolbar.Controls.Add(sheet);toolbar.Controls.Add(header);
    toolbar.Controls.Add(owner.Btn(Loc.T("Premenné polia"),delegate{owner.EditVariables();Map();}));toolbar.Controls.Add(new Label{Text=Loc.T("Počet záznamov"),AutoSize=true,Margin=new Padding(3,8,3,0)});toolbar.Controls.Add(generated);toolbar.Controls.Add(owner.Btn(Loc.T("Pripraviť dávku"),Prepare));
    preparedInfo.AutoSize=true;preparedInfo.MaximumSize=new Size(400,0);preparedInfo.UseMnemonic=false;toolbar.Controls.Add(preparedInfo);
    var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};root.Controls.Add(actions,0,2);root.SetColumnSpan(actions,2);
    actions.Controls.Add(owner.Btn(Loc.T("Vybrať všetko"),delegate{SelectAll(true);}));actions.Controls.Add(owner.Btn(Loc.T("Zrušiť výber"),delegate{SelectAll(false);}));actions.Controls.Add(owner.Btn(Loc.T("Exportovať vybrané"),Export));actions.Controls.Add(owner.Btn(Loc.T("Tlačiť vybrané"),Print));
    sheet.SelectedIndexChanged+=delegate{if(!updating)Map();};header.CheckedChanged+=delegate{if(!updating)Map();};mapping.CellValueChanged+=delegate{if(!updating)Clear();};generated.ValueChanged+=delegate{if(!updating)Clear();};separator.SelectedIndexChanged+=Reload;encoding.SelectedIndexChanged+=Reload;
    owner.ApplyColors(window);Map();preview=new BatchPreview(view,message=>preparedInfo.Text=message);
   }
   char Separator{get{return separator.SelectedIndex==2?'\t':separator.Text[0];}}int CodePage{get{return encoding.SelectedIndex==0?65001:1250;}}
   void Clear(){if(preview!=null)preview.Request(null);records.RowCount=0;batch=new List<BatchRecord>();included.Clear();preparedInfo.Text="";}
   void Map(){
    Clear();updating=true;try{
     mapping.Rows.Clear();targets.Clear();generated.Enabled=tables.Count==0;var table=sheet.SelectedIndex>=0&&sheet.SelectedIndex<tables.Count?tables[sheet.SelectedIndex]:null;
     var choices=new List<string>{Loc.T("Neprepojené")};int columns=0;if(table!=null)foreach(var row in table.Rows)columns=Math.Max(columns,row.Length);
     for(int i=0;i<columns;i++)choices.Add((i+1)+": "+(header.Checked&&table.Rows.Count>0?BatchData.Value(table.Rows[0],i):Loc.T("Stĺpec")));
     var column=(DataGridViewComboBoxColumn)mapping.Columns[1];column.Items.Clear();column.Items.AddRange(choices.ToArray());
     Action<string,string> add=(key,name)=>{targets.Add(key);var saved=setup.Bindings.Find(b=>b.Target==key);int index=key=="quantity"?setup.QuantityColumn:saved==null?-1:saved.Column;mapping.Rows.Add(name,choices[Math.Max(0,Math.Min(choices.Count-1,index+1))]);};
     var model=owner.Read();if(!model.HideBarcode)add("code",Loc.T("Čiarový kód"));for(int i=0;i<model.Texts.Count;i++)add("text:"+i,model.Texts[i].Text);for(int i=0;i<model.Symbols.Count;i++)if(model.Symbols[i].Code!=null||model.Symbols[i].Matrix!=null)add("symbol:"+i,model.Symbols[i].Name+" "+(i+1));add("quantity",Loc.T("Počet etikiet"));
    }finally{updating=false;}
   }
   void Import(){using(var dialog=new OpenFileDialog{Filter="CSV / Excel|*.csv;*.xlsx"})if(dialog.ShowDialog(window)==DialogResult.OK)LoadFile(dialog.FileName,Separator,CodePage);}
   bool LoadFile(string path,char split,int codePage){
    try{
     bool csv=Path.GetExtension(path).Equals(".csv",StringComparison.OrdinalIgnoreCase);List<DataTableSource> candidate;
     if(!owner.RunWork(window,csv?"import-csv":"import-xlsx","Načítavam tabuľku…",delegate{return csv?new List<DataTableSource>{BatchData.Csv(path,split,codePage)}:BatchData.Xlsx(path);},out candidate))return false;
     tables.Clear();tables.AddRange(candidate);csvPath=csv?path:null;loadedSeparator=split;loadedCodePage=codePage;
     updating=true;try{sheet.Items.Clear();foreach(var table in tables)sheet.Items.Add(table.Name);if(tables.Count>0)sheet.SelectedIndex=Math.Max(0,tables.FindIndex(t=>t.Name==setup.SheetName));}finally{updating=false;}Map();return true;
    }catch(Exception ex){owner.Error(ex);return false;}
   }
   void Reload(object sender,EventArgs args){if(updating||csvPath==null)return;if(!LoadFile(csvPath,Separator,CodePage)){updating=true;try{separator.SelectedIndex=loadedSeparator=='\t'?2:loadedSeparator==','?1:0;encoding.SelectedIndex=loadedCodePage==1250?1:0;}finally{updating=false;}}}
   void Prepare(){try{
    mapping.EndEdit();var candidate=ProjectXml.Decode<BatchSetup>(ProjectXml.Encode(setup));candidate.Bindings.Clear();candidate.QuantityColumn=-1;
    for(int i=0;i<mapping.Rows.Count;i++){var column=(DataGridViewComboBoxColumn)mapping.Columns[1];int index=column.Items.IndexOf(mapping.Rows[i].Cells[1].Value)-1;if(targets[i]=="quantity")candidate.QuantityColumn=index;else candidate.Bindings.Add(new ColumnBinding{Target=targets[i],Column=index});}
    candidate.Header=header.Checked;candidate.Separator=Separator;candidate.CodePage=CodePage;candidate.GeneratedCount=(int)generated.Value;candidate.SheetName=sheet.Text;
    var model=owner.Read();model.Batch=candidate;var table=sheet.SelectedIndex>=0&&sheet.SelectedIndex<tables.Count?tables[sheet.SelectedIndex]:null;DateTime instant=owner.currentTime();List<BatchRecord> prepared;
    if(!owner.RunWork(window,"prepare-batch","Pripravujem dávku…",()=>BatchProcessing.Prepare(model,table,candidate.Header,candidate.Bindings,candidate.QuantityColumn,candidate.GeneratedCount,instant),out prepared))return;
    owner.ChangeDocument(s=>s.Batch=candidate);setup=candidate;Clear();batch=prepared;foreach(var item in batch)included.Add(true);records.RowCount=batch.Count;preparedInfo.Text=Loc.T("Údaje pripravené")+": "+instant.ToString("g");if(batch.Count>0){records.CurrentCell=records.Rows[0].Cells[0];preview.Request(batch[0].Label);}
   }catch(Exception ex){owner.Error(ex);}}
   void SelectAll(bool selected){records.EndEdit();for(int i=0;i<included.Count;i++)included[i]=selected;records.Invalidate();}
   bool Plan(out OutputPlan result,out LabelSettings model){records.EndEdit();var selected=new List<BatchRecord>();for(int i=0;i<batch.Count;i++)if(included[i])selected.Add(batch[i]);var format=owner.Read();model=format;return owner.RunWork(window,"plan-batch","Pripravujem tlačovú úlohu…",()=>OutputPlan.Create(selected,format),out result);}
   void Export(){try{OutputPlan plan;LabelSettings model;if(Plan(out plan,out model))owner.ExportJob(plan,model.Dpi,null,window);}catch(Exception ex){owner.Error(ex);}}
   void Print(){try{OutputPlan plan;LabelSettings model;if(Plan(out plan,out model))using(var form=owner.CreateJobPrintWindow(model,plan))form.ShowDialog(window);}catch(Exception ex){owner.Error(ex);}}
   public void Show(){window.ShowDialog(owner);}
   public void Dispose(){preview.Dispose();window.Dispose();}
  }
 }
}
