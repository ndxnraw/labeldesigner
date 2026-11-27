using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  LabelScenario EditScenario(Form owner,LabelSettings template,LabelScenario original){
   using var window=FeatureForm("Údaje testovacieho scenára",790,540);
   var root=Rows();window.Controls.Add(root);
   var settings=Stack();settings.Padding=Padding.Empty;
   var name=Input(settings,"Názov scenára",original.Name);
   name.Name="scenarioName";name.MaxLength=100;
   var useLanguage=Check(settings,"Použiť jazyk zo scenára",original.Language!=null);
   var language=Input(settings,"Jazyk etikety",original.Language??template.Languages?.Selected);
   language.MaxLength=100;
   language.AutoCompleteMode=AutoCompleteMode.Suggest;
   language.AutoCompleteSource=AutoCompleteSource.CustomSource;
   if(template.Languages!=null)language.AutoCompleteCustomSource.AddRange(template.Languages.Codes.ToArray());
   useLanguage.Visible=language.Visible=settings.GetControlFromPosition(0,3).Visible=template.Languages!=null;
   language.Enabled=useLanguage.Checked;
   useLanguage.CheckedChanged+=delegate{language.Enabled=useLanguage.Checked;};
   ManagementLayout.AddRow(root,settings);
   ManagementLayout.AddRow(root,Description("Zaškrtnuté polia použijú hodnotu scenára. Prázdna hodnota je tiež testovací údaj."));
   var grid=CreateScenarioGrid("scenarioValues","Údaje testovacieho scenára");
   grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="use",HeaderText=Loc.T("Použiť"),FillWeight=24});
   grid.Columns.Add(new DataGridViewTextBoxColumn{Name="field",HeaderText=Loc.T("Pole etikety"),ReadOnly=true,FillWeight=52});
   grid.Columns.Add(new DataGridViewTextBoxColumn{Name="value",HeaderText=Loc.T("Hodnota"),MaxInputLength=4000,FillWeight=100});
   foreach(int field in QuickPrint.Editable(template)){
    var sample=original.Values.Find(item=>item.Element==field);
    var row=grid.Rows[grid.Rows.Add(sample!=null,QuickPrint.Name(template,field),sample?.Value??"")];
    row.Tag=field;
    InstallSample(row,sample);
   }
   grid.EditingControlShowing+=delegate(object sender,DataGridViewEditingControlShowingEventArgs args){
    if(args.Control is not DataGridViewTextBoxEditingControl box)return;
    box.KeyDown-=ScenarioLineBreak;
    if(grid.CurrentCell.ColumnIndex==2){box.Multiline=true;box.KeyDown+=ScenarioLineBreak;}
   };
   ManagementLayout.AddRow(root,grid,SizeType.Percent);
   var message=Description("");ManagementLayout.AddRow(root,message);
   var import=Btn(Loc.T("Načítať riadok CSV/XLSX…"),delegate{
    try{
     if(!grid.EndEdit())return;
     var data=ImportScenarioRow(window,template);
     if(data==null)return;
     foreach(DataGridViewRow row in grid.Rows)
      if(data.Values.Find(value=>value.Element==(int)row.Tag) is { } sample)InstallSample(row,sample);
     if(data.Language!=null){useLanguage.Checked=true;language.Text=data.Language;}
     message.Text="";
    }catch(Exception ex){message.Text=ex.Message;}
   });
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
   actions.Controls.Add(import);ManagementLayout.AddRow(root,actions);
   LabelScenario result=null;
   var apply=Btn(Loc.T("Použiť"),delegate{
    try{
     if(!grid.EndEdit())return;
     var next=new LabelScenario{Id=original.Id,Name=name.Text.Trim(),Language=useLanguage.Checked?language.Text.Trim():null};
     // Preserve samples for fields currently translated or disabled.
     var editable=new HashSet<int>(QuickPrint.Editable(template));
     next.Values.AddRange(original.Values.FindAll(value=>!editable.Contains(value.Element)));
     foreach(DataGridViewRow row in grid.Rows){
      if(!Equals(row.Cells[0].Value,true))continue;
      string value=Convert.ToString(row.Cells[2].Value)??"";
      var sample=row.Cells[2].Tag as LabelScenarioValue;
      next.Values.Add(sample!=null&&sample.Value==value?sample:
       new LabelScenarioValue{Element=(int)row.Tag,Value=value});
     }
     var label=template.Scenarios.FindAll(item=>item.Id!=next.Id);
     label.Add(next);
     LabelScenario.Validate(label);
     result=next;window.DialogResult=DialogResult.OK;
    }catch(Exception ex){message.Text=ex.Message;}
   });
   var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
   var footer=new UiMessageBox.ActionRow();footer.AddAction(apply);footer.AddAction(cancel);
   ManagementLayout.AddRow(root,footer);
   window.AcceptButton=apply;window.CancelButton=cancel;
   ApplyColors(window);
   return window.ShowDialog(owner)==DialogResult.OK?result:null;
  }

  static void ScenarioLineBreak(object sender,KeyEventArgs args){
   if(args.KeyCode!=Keys.Enter||!args.Shift)return;
   ((TextBox)sender).SelectedText=Environment.NewLine;
   args.SuppressKeyPress=true;
  }

  static void InstallSample(DataGridViewRow row,LabelScenarioValue value){
   if(value!=null){row.Cells[0].Value=true;row.Cells[2].Value=value.Value;}
   row.Cells[2].Tag=value;
   row.Cells[2].ErrorText=value?.SourceError??"";
  }

  static DataGridView CreateScenarioGrid(string name,string title)=>new(){
   Name=name,AccessibleName=Loc.T(title),Dock=DockStyle.Fill,AllowUserToAddRows=false,
   AllowUserToDeleteRows=false,AllowUserToOrderColumns=false,RowHeadersVisible=false,MultiSelect=false,
   AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill
  };

  LabelScenario ImportScenarioRow(Form owner,LabelSettings template){
   using var file=new OpenFileDialog{Filter="CSV / Excel|*.csv;*.xlsx",CheckFileExists=true};
   if(file.ShowDialog(owner)!=DialogResult.OK)return null;
   bool csv=Path.GetExtension(file.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase);
   char separator=template.Batch.Separator;
   int codePage=template.Batch.CodePage;
   if(csv){
    using var options=FeatureForm("Nastavenie CSV",460,280);
    var stack=FeatureStack(options);
    char[] separators={';',',','\t'};
    var split=Choice(stack,"Oddeľovač",new[]{";",",","Tab"},Array.IndexOf(separators,separator));
    var encoding=Choice(stack,"Kódovanie",new[]{"UTF-8","Windows-1250"},codePage==1250?1:0);
    AddAccept(options,stack,delegate{separator=separators[split.SelectedIndex];codePage=encoding.SelectedIndex==1?1250:65001;});
    if(options.ShowDialog(owner)!=DialogResult.OK)return null;
   }
   if(!RunWork(owner,"scenario-import","Načítavam tabuľku…",
       ()=>csv?new List<DataTableSource>{BatchData.Csv(file.FileName,separator,codePage)}:BatchData.Xlsx(file.FileName),
       out var tables))return null;
   return PickScenarioRow(owner,template,tables,csv);
  }

  LabelScenario PickScenarioRow(Form owner,LabelSettings template,List<DataTableSource> tables,bool csv){
   if(tables.Count==0)throw new InvalidDataException(Loc.T("Tabuľka nemá požadované záznamy."));
   using var window=FeatureForm("Výber riadka a mapovanie",830,530);
   var root=Rows();window.Controls.Add(root);
   var source=Stack();source.Padding=Padding.Empty;
   var sheet=Choice(source,"Hárok",tables.ConvertAll(table=>table.Name).ToArray(),0);
   if(csv){sheet.Visible=false;source.GetControlFromPosition(0,0).Visible=false;}
   var header=Check(source,"Prvý riadok obsahuje názvy stĺpcov",template.Batch.Header);
   var record=Number(source,"Poradie dátového záznamu",1,1,10000,0);
   record.Name="scenarioRecord";
   ManagementLayout.AddRow(root,source);
   var info=Description("");ManagementLayout.AddRow(root,info);
   var grid=CreateScenarioGrid("scenarioMapping","Mapovanie scenára");
   grid.Columns.Add(new DataGridViewTextBoxColumn{Name="field",HeaderText=Loc.T("Pole etikety"),ReadOnly=true,FillWeight=60});
   var column=new DataGridViewComboBoxColumn{Name="column",HeaderText=Loc.T("Stĺpec"),FillWeight=75};
   grid.Columns.Add(column);
   grid.Columns.Add(new DataGridViewTextBoxColumn{Name="sample",HeaderText=Loc.T("Hodnota"),ReadOnly=true});
   foreach(int field in QuickPrint.Editable(template))grid.Rows[grid.Rows.Add(QuickPrint.Name(template,field))].Tag=field;
   if(template.Languages!=null)grid.Rows[grid.Rows.Add(Loc.T("Jazyk etikety"))].Tag=-2;
   ManagementLayout.AddRow(root,grid,SizeType.Percent);
   LabelScenario result=null;
   var apply=Btn(Loc.T("Použiť riadok"),delegate{
    if(!grid.EndEdit())return;
    var table=CurrentTable();
    int index=RecordIndex();
    var sample=new LabelScenario();
    foreach(DataGridViewRow row in grid.Rows){
     int item=column.Items.IndexOf(row.Cells[1].Value)-1;
     if(item<0)continue;
     int element=(int)row.Tag;
     var value=LabelScenario.FromCell(table,index,item,element);
     if(element==-2){
      if(value.SourceError!=null){info.Text=value.SourceError;return;}
      sample.Language=value.Value;
     }else sample.Values.Add(value);
    }
    result=sample;window.DialogResult=DialogResult.OK;
   });
   var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
   var footer=new UiMessageBox.ActionRow();footer.AddAction(apply);footer.AddAction(cancel);
   ManagementLayout.AddRow(root,footer);
   window.AcceptButton=apply;window.CancelButton=cancel;
   bool refreshing=false;
   DataTableSource CurrentTable()=>tables[sheet.SelectedIndex];
   int RecordIndex()=>(int)record.Value-1+(header.Checked?1:0);
   void UpdateSample(){
    if(refreshing)return;
    var table=CurrentTable();
    int index=RecordIndex();bool found=index<table.Rows.Count,mapped=false;
    info.Text=found?string.Format(Loc.T("Pôvodný riadok: {0}"),table.SourceRow(index)):Loc.T("Tabuľka nemá požadované záznamy.");
    foreach(DataGridViewRow row in grid.Rows){
     int item=column.Items.IndexOf(row.Cells[1].Value)-1;
     var value=found&&item>=0?LabelScenario.FromCell(table,index,item,(int)row.Tag):null;
     row.Cells[2].Value=value?.SourceError??value?.Value??"";
     mapped|=item>=0;
    }
    apply.Enabled=found&&mapped;
   }
   void ReloadColumns(){
    refreshing=true;
    try{
     var table=CurrentTable();
     grid.CancelEdit();
     foreach(DataGridViewRow row in grid.Rows)row.Cells[1].Value=null;
     column.Items.Clear();
     column.Items.Add(Loc.T("Bez mapovania"));
     int width=0;
     foreach(var row in table.Rows)width=Math.Max(width,row.Length);
     for(int index=0;index<width;index++){
      string title=header.Checked&&table.Rows.Count>0&&index<table.Rows[0].Length?table.Rows[0][index]:"";
      column.Items.Add((index+1).ToString()+(string.IsNullOrWhiteSpace(title)?"":" · "+title));
     }
     foreach(DataGridViewRow row in grid.Rows)row.Cells[1].Value=column.Items[0];
     int count=table.Rows.Count-(header.Checked?1:0);
     record.Maximum=Math.Max(1,count);record.Value=1;record.Enabled=count>1;
    }finally{refreshing=false;}
    UpdateSample();
   }
   grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
   grid.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs args){if(args.RowIndex>=0&&args.ColumnIndex==1)UpdateSample();};
   sheet.SelectedIndexChanged+=delegate{ReloadColumns();};
   header.CheckedChanged+=delegate{ReloadColumns();};
   record.ValueChanged+=delegate{UpdateSample();};
   ReloadColumns();
   ApplyColors(window);
   return window.ShowDialog(owner)==DialogResult.OK?result:null;
  }
 }
}
