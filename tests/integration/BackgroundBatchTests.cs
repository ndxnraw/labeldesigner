using System;using System.IO;using System.IO.Compression;using System.Drawing;using System.Reflection;using System.Text;using System.Threading;using System.Diagnostics;using System.Collections.Generic;using System.Windows.Forms;using EanEtikety;
class BackgroundBatchTests {
 static string dir;static int checks;static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Hidden).GetValue(value);}static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Hidden).Invoke(value,args);}
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}static void Cancelled(Action action,string message){bool cancelled=false;try{action();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,message);}
 static Button Button(Control parent,string text){if(parent is Button&&parent.Text==text)return (Button)parent;foreach(Control child in parent.Controls){var found=Button(child,text);if(found!=null)return found;}return null;}
 static void ZipText(ZipArchive zip,string path,string text){using(var writer=new StreamWriter(zip.CreateEntry(path).Open()))writer.Write(text);}
 static LabelSettings Label(){var label=LabelSettings.Default();label.Kind=128;label.Ean="{serial}";label.Variables.Add(new VariableField{Name="serial",Digits=6});return label;}
 static void Core(){
  WorkContext.Check();var updates=new List<WorkProgress>();using(new WorkContext(p=>updates.Add(p)).Enter()){WorkContext.Report("stage",0,10);WorkContext.Report("stage",10,10);}Check(updates.Count==2&&updates[1].Completed==10,"progress reports start and exact finish");
  var stop=new WorkContext();stop.Cancel();using(stop.Enter())Cancelled(()=>WorkContext.Check(),"pre-cancellation honoured");WorkContext.Check();Check(true,"ambient context restored after scope");
  string csv=Path.Combine(dir,"table.csv");File.WriteAllText(csv,"Code;Name\n0001;First\n0002;Second\n");WorkContext context=null;context=new WorkContext(p=>context.Cancel());using(context.Enter())Cancelled(()=>BatchData.Csv(csv,';',65001),"CSV cancellation propagated");using(var stream=File.Open(csv,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(stream.Length>0,"CSV handle closed after cancellation");
  string xlsx=Path.Combine(dir,"table.xlsx");using(var file=File.Create(xlsx))using(var zip=new ZipArchive(file,ZipArchiveMode.Create)){
   ZipText(zip,"xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Data' r:id='r1'/></sheets></workbook>");ZipText(zip,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/></Relationships>");ZipText(zip,"xl/worksheets/sheet1.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>0001</t></is></c></row></sheetData></worksheet>");
  }
  context=null;context=new WorkContext(p=>context.Cancel());using(context.Enter())Cancelled(()=>BatchData.Xlsx(xlsx),"XLSX cancellation propagated");Check(BatchData.Xlsx(xlsx)[0].Rows[0][0]=="0001","XLSX remains readable after cancellation");
  var template=Label();string before=ProjectXml.Encode(template);context=null;context=new WorkContext(p=>{if(p.Completed>0)context.Cancel();});using(context.Enter())Cancelled(()=>BatchProcessing.Prepare(template,null,false,new List<ColumnBinding>(),-1,2000,new DateTime(2026,11,12)),"record preparation cancellation does not become a bad row");Check(ProjectXml.Encode(template)==before,"preparation never edits input template");
  var prepared=BatchProcessing.Prepare(template,null,false,new List<ColumnBinding>(),-1,3,new DateTime(2026,11,12));Check(prepared.Count==3&&prepared[2].Label.Ean=="000003","successful preparation preserves serial values");var plan=OutputPlan.Create(prepared,template);Check(plan.PlacementsForPage(0).Count==1&&plan.PlacementsForPage(2).Count==1,"page index preserves individual placements");
  foreach(string format in new[]{"pdf","docx","svg","zpl"}){string target=Path.Combine(dir,"cancelled."+format);File.WriteAllText(target,"ORIGINAL");context=null;context=new WorkContext(p=>{if(p.Stage=="Exportujem strany…")context.Cancel();});using(context.Enter())Cancelled(()=>AtomicOutput.Save(target,p=>JobOutput.Export(p,format,plan,203)),"cancel "+format+" export");Check(File.ReadAllText(target)=="ORIGINAL","cancelled "+format+" preserves existing target");Check(Directory.GetFiles(dir,"*.tmp").Length==0,"cancelled "+format+" removes temporary file");}
  string atomic=Path.Combine(dir,"atomic.txt");File.WriteAllText(atomic,"OLD");context=new WorkContext();using(context.Enter())Cancelled(()=>AtomicOutput.Save(atomic,p=>{File.WriteAllText(p,"NEW");context.Cancel();}),"cancellation immediately before commit");Check(File.ReadAllText(atomic)=="OLD","commit cancellation preserves target");AtomicOutput.Save(atomic,p=>File.WriteAllText(p,"COMPLETE"));Check(File.ReadAllText(atomic)=="COMPLETE","completed atomic output replaces target");
 }
 static void Logs(){string folder=Path.Combine(dir,"logs"),secret="PRIVATE-LABEL-SECRET-"+Guid.NewGuid().ToString("N");Exception error;try{throw new InvalidOperationException(secret);}catch(Exception ex){error=ex;}string id=DiagnosticLog.WriteTo(folder,"export-pdf",error);string log=File.ReadAllText(Path.Combine(folder,"application.log"));Check(id!=null&&log.Contains(id)&&log.Contains("InvalidOperationException"),"diagnostic has reference and error type");Check(!log.Contains(secret)&&!log.Contains(dir),"diagnostics omit messages and filesystem paths");
  for(int i=0;i<3;i++){File.WriteAllText(Path.Combine(folder,"application.log"),new string('x',DiagnosticLog.MaxBytes));DiagnosticLog.WriteTo(folder,"prepare-batch",error);}Check(Directory.GetFiles(folder).Length==3&&new FileInfo(Path.Combine(folder,"application.log")).Length<DiagnosticLog.MaxBytes,"diagnostic rotation bounded to three files");string blocker=Path.Combine(dir,"blocked-log");File.WriteAllText(blocker,"file");Check(DiagnosticLog.WriteTo(blocker,"application",error)==null,"logging failure cannot mask original error");
 }
 static void Dialogs(){using(var owner=new Form{Font=UiFonts.Default,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)}){
  owner.Show();int ui=Thread.CurrentThread.ManagedThreadId,worker=0,pulses=0;bool sta=false;int value;
  using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=delegate{pulses++;if(pulses>=3){foreach(Form form in Application.OpenForms)if(form!=owner&&form.Text=="Cancellation"){timer.Stop();Button(form,Loc.T("Zrušiť")).PerformClick();break;}}};timer.Start();bool finished=WorkDialog.Run(owner,"prepare-batch","Cancellation",owner.Font,owner.Icon,c=>{},delegate{worker=Thread.CurrentThread.ManagedThreadId;sta=Thread.CurrentThread.GetApartmentState()==ApartmentState.STA;var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<4000){WorkContext.Report("Pripravujem dávku…",0,0);Thread.Sleep(5);}return 7;},out value,Path.Combine(dir,"ui-logs"));Check(!finished&&pulses>=3&&worker!=ui&&sta,"UI remains responsive; cancel executes on STA worker");}
  using(var timer=new System.Windows.Forms.Timer{Interval=40}){timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form!=owner&&form.Text=="Close cancellation"){timer.Stop();form.Close();break;}};timer.Start();bool finished=WorkDialog.Run(owner,"prepare-batch","Close cancellation",owner.Font,owner.Icon,c=>{},delegate{var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<4000){WorkContext.Check();Thread.Sleep(5);}return 1;},out value);Check(!finished,"closing progress dialog requests cooperative cancellation");}
  bool failed=false;try{WorkDialog.Run<int>(owner,"prepare-batch","Failure",owner.Font,owner.Icon,c=>{},delegate{throw new InvalidOperationException("SECRET-DATA");},out value,Path.Combine(dir,"ui-logs"));}catch(BackgroundFailure ex){failed=ex.DiagnosticId!=null;}Check(failed,"background failure carries a diagnostic reference");
  using(var committed=new ManualResetEvent(false))using(var clicked=new ManualResetEvent(false))using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=delegate{if(committed.WaitOne(0))foreach(Form form in Application.OpenForms)if(form!=owner&&form.Text=="Commit race"){timer.Stop();Button(form,Loc.T("Zrušiť")).PerformClick();clicked.Set();break;}};timer.Start();bool finished=WorkDialog.Run(owner,"export-svg","Commit race",owner.Font,owner.Icon,c=>{},delegate{AtomicOutput.Save(Path.Combine(dir,"committed.txt"),p=>File.WriteAllText(p,"DONE"));committed.Set();if(!clicked.WaitOne(4000))throw new Exception("UI did not respond");return 5;},out value);Check(finished&&value==5,"cancel after completed commit is reported as success");}
  owner.Close();
 }}
 static void BatchUi(){using(var owner=new MainForm(Path.Combine(dir,"prefs.xml"))){owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();object controller=null;Exception failure=null;bool completed=false;
  using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form!=owner&&form.Text==Loc.T("Dávka a premenné")){timer.Stop();try{
   controller=form.Tag;Field<NumericUpDown>(controller,"generated").Value=2000;Call(controller,"Prepare");var old=Field<List<BatchRecord>>(controller,"batch");Check(old.Count==2000,"large batch preparation installed successful result");Check(Field<DataGridView>(controller,"records").VirtualMode,"batch grid uses virtual rows");
   string original=ProjectXml.Encode((LabelSettings)Call(owner,"Read"));using(var cancelTimer=new System.Windows.Forms.Timer{Interval=5}){cancelTimer.Tick+=delegate{foreach(Form progress in Application.OpenForms)if(progress.Text==Loc.T("Pripravujem dávku…")){cancelTimer.Stop();Button(progress,Loc.T("Zrušiť")).PerformClick();break;}};cancelTimer.Start();Call(controller,"Prepare");}
   // A cancelled re-preparation must keep the complete previous snapshot.
   // No partial result or candidate mapping is committed to the editor.
   Check(object.ReferenceEquals(old,Field<List<BatchRecord>>(controller,"batch"))&&ProjectXml.Encode((LabelSettings)Call(owner,"Read"))==original,"cancel/completion race preserves consistent batch and project");
   var grid=Field<DataGridView>(controller,"records");grid.Rows[0].Cells[0].Value=false;grid.EndEdit();Check(!Field<List<bool>>(controller,"included")[0],"virtual checkbox edits update selected records");Call(controller,"SelectAll",false);Check(!Field<List<bool>>(controller,"included")[0],"virtual selection clears");Call(controller,"SelectAll",true);Check(Field<List<bool>>(controller,"included")[0],"virtual selection restores");
   CsvIssuesUi(controller,form);
   ComparisonUi(controller,form);
   PresetUi(controller,form,owner);
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,form.ClientRectangle);image.Save(Path.Combine(dir,"batch-background.png"));}completed=true;
  }catch(Exception ex){failure=ex;}finally{form.Close();}break;}};timer.Start();Call(owner,"ShowBatch");}
  if(failure!=null)throw failure;Check(completed,"batch window exercised with background work");WorkspaceBackupUi(owner);WorkspaceBatchBackupUi(owner);QuickPrintUi(owner);Call(owner,"Apply",LabelSettings.Default());owner.Close();
 }}
 static void WorkspaceBackupUi(MainForm owner){
  string source=Path.Combine(dir,"profile-export"),target=Path.Combine(dir,"profile-import"),zip=Path.Combine(dir,"profiles.zip");
  string targetFile=Path.Combine(target,"printers.xml");
  new PrinterProfileStore(Path.Combine(source,"printers.xml")).Save(new PrinterProfile{Printer="Old",Name="Roll",Width=200,Height=100,X=2});
  new PrinterProfileStore(targetFile).Save(new PrinterProfile{Printer="Local",Name="Roll",Width=200,Height=100,X=-3});
  new WorkspaceBackup(source).Export(zip,false,true);
  var service=new WorkspaceBackup(target);
  var review=service.Open(zip);
  WorkspaceRestorePlan selected=null;
  Action show=()=>selected=(WorkspaceRestorePlan)Call(owner,"ReviewWorkspaceRestore",owner,review,new List<string>{"Local"});
  VisitDialog("Náhľad obnovy pracovných údajov",show,dialog=>{
   var map=(DataGridView)dialog.Controls.Find("workspacePrinterMap",true)[0];
   var grid=(DataGridView)dialog.Controls.Find("workspaceRestoreItems",true)[0];
   var restore=Button(dialog,Loc.T("Obnoviť pracovné údaje"));
   Check(grid.RowCount==1&&Convert.ToString(grid.Rows[0].Cells[4].Value)==Loc.T("Pridať"),"review initially preserves the original printer");
   map.Rows[0].Cells[1].Value="Local";
   Check(Convert.ToString(grid.Rows[0].Cells[3].Value)=="Local"&&Convert.ToString(grid.Rows[0].Cells[4].Value)==Loc.T("Ponechať")&&!restore.Enabled,"remapping recalculates the conflict and defaults to keeping the destination");
   ((CheckBox)dialog.Controls.Find("workspaceReplace",true)[0]).Checked=true;
   Check(Convert.ToString(grid.Rows[0].Cells[4].Value)==Loc.T("Nahradiť")&&restore.Enabled,"explicit replacement appears in review");
   Button(dialog,Loc.T("Zrušiť")).PerformClick();
  });
  Check(selected==null&&new PrinterProfileStore(targetFile).Find("Local","Roll").X==-3,"cancel writes nothing");
  VisitDialog("Náhľad obnovy pracovných údajov",show,dialog=>{
   var map=(DataGridView)dialog.Controls.Find("workspacePrinterMap",true)[0];
   map.Rows[0].Cells[1].Value="Local";
   ((CheckBox)dialog.Controls.Find("workspaceReplace",true)[0]).Checked=true;
   Button(dialog,Loc.T("Obnoviť pracovné údaje")).PerformClick();
  });
  Check(selected!=null&&selected.Items[0].Action==WorkspaceRestoreAction.Replace&&selected.Items[0].TargetPrinter=="Local","confirmed dialog returns the reviewed replace plan");
 }
 static void WorkspaceBatchBackupUi(MainForm owner){
  var source=new PrintHistoryStore(Path.Combine(dir,"print-from"));
  var target=new PrintHistoryStore(Path.Combine(dir,"print-to"));
  var export=new WorkspaceBackup(Path.Combine(dir,"work-from"),source);
  var restore=new WorkspaceBackup(Path.Combine(dir,"work-to"),target);
  var label=LabelSettings.Default();
  var rows=OutputPlan.Create(new List<BatchRecord>{new BatchRecord{Row=7,Count=2,Label=label}},label);
  var attempt=new PrintHistoryEntry{StartedUtc=DateTime.UtcNow,Labels=2,Pages=1,Repetitions=1,Status=PrintHistoryStatus.Submitted};
  source.Record(attempt,null,rows);
  var result=source.OpenBatchResult(attempt.Id);
  result.Rows[0].Confirmed=1;
  source.SaveBatchResult(attempt.Id,result);
  string zip=Path.Combine(dir,"backup-ui-print.zip");
  export.Export(zip,false,false,true);
  restore.Restore(restore.Open(zip).Preview(false));
  result=target.OpenBatchResult(attempt.Id);
  result.Rows[0].Confirmed=0;
  target.SaveBatchResult(attempt.Id,result);
  var review=restore.Open(zip);
  WorkspaceRestorePlan selected=null;
  Action show=()=>selected=(WorkspaceRestorePlan)Call(owner,"ReviewWorkspaceRestore",owner,review,new List<string>());
  VisitDialog("Náhľad obnovy pracovných údajov",show,dialog=>{
   var grid=(DataGridView)dialog.Controls.Find("workspaceRestoreItems",true)[0];
   var choice=(CheckBox)dialog.Controls.Find("workspaceUseBatchBackup",true)[0];
   var confirm=Button(dialog,Loc.T("Obnoviť pracovné údaje"));
   grid.CurrentCell=grid.Rows[0].Cells[0];
   Check(choice.Visible&&!choice.Checked&&!confirm.Enabled&&Convert.ToString(grid.Rows[0].Cells[4].Value)==Loc.T("Ponechať"),
    "batch restore UI defaults to keeping conflicting local data");
   Check(dialog.Controls.Find("workspaceBatchDetails",true)[0].Text.Contains(string.Format(Loc.T("Riadok {0}: miestne {1}, zo zálohy {2}"),7,0,1)),
    "batch restore UI shows the changed row and both quantities");
   choice.Checked=true;
   Check(confirm.Enabled&&Convert.ToString(grid.Rows[0].Cells[4].Value)==Loc.T("Nahradiť"),
    "batch choice changes the preview for the same ID");
   confirm.PerformClick();
  });
  Check(selected!=null&&selected.Items[0].BatchConflict&&target.OpenBatchResult(attempt.Id).ConfirmedTotal==0,
   "confirming batch preview returns the selected plan without writing yet");
  Check(restore.Restore(selected).Applied==1&&target.OpenBatchResult(attempt.Id).ConfirmedTotal==1,
   "applying the preview restores the chosen batch count once");
 }
 static void QuickPrintUi(MainForm owner){
  string path=Path.Combine(Field<WorkspaceStore>(owner,"workspace").TemplatesPath,"Quick proof.ean.xml");
  var settings=LabelSettings.Default();
  settings.Texts[0].AutoWidth=false;
  settings.Texts[0].MinimumFitSize=6;
  Engine.Save(path,settings);
  VisitDialog("Upraviteľné polia šablóny…",()=>Call(owner,"ConfigureLibraryQuickPrint",owner,path),window=>{
   var fields=(CheckedListBox)window.Controls.Find("quickPrintFieldChoices",true)[0];
   fields.SetItemChecked(0,true);fields.SetItemChecked(1,true);
   Button(window,Loc.T("Použiť")).PerformClick();
  });
  var template=Engine.Load(path);
  Check(template.QuickPrintFields.Count==2,"library saves editable field selection");
  string before=File.ReadAllText(path);
  VisitDialog("Rýchle vyplnenie a tlač…",()=>Call(owner,"ShowQuickPrint",owner,template),window=>{
   var text=(TextBox)window.Controls.Find("quickPrintValue0",true)[0];
   var barcode=(TextBox)window.Controls.Find("quickPrintValue-1",true)[0];
   var error=(Label)window.Controls.Find("quickPrintError-1",true)[0];
   var preview=(PictureBox)window.Controls.Find("quickPrintPreview",true)[0];
   var next=(Button)window.Controls.Find("quickPrintContinue",true)[0];
   var timer=Stopwatch.StartNew();
   while(!next.Enabled&&timer.ElapsedMilliseconds<5000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(next.Enabled&&preview.Image!=null,"form opens with rendered label");
   var fitWarning=(Label)window.Controls.Find("quickPrintError0",true)[0];
   text.Text=new string('W',250);
   timer.Restart();
   while((!next.Enabled||!fitWarning.Visible)&&timer.ElapsedMilliseconds<5000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(next.Enabled&&fitWarning.Visible&&fitWarning.Text==Loc.T("Text sa nezmestí do poľa")&&preview.Image!=null,
    "fixed text overflow is shown at the editable field and can proceed to review");
   text.Text=template.Texts[0].Text;
   barcode.Text="8580000000010";
   timer.Restart();
   while(!error.Visible&&timer.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(error.Visible&&!next.Enabled&&preview.Image!=null,"invalid barcode prevents print while valid artwork remains visible");
   barcode.Text="8580000000016";
   text.Text="LOT-24";
   var count=(TextBox)window.Controls.Find("quickPrintQuantity",true)[0];
   count.Text="20";
   timer.Restart();
   while(!next.Enabled&&timer.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
   Check(next.Enabled&&!error.Visible&&preview.Image!=null,"correcting data refreshes the preview");
   count.Text="20.5";
   Check(!next.Enabled&&window.Controls.Find("quickPrintQuantityError",true)[0].Visible,"fractional input cannot silently change the label quantity");
   count.Text="20";
   VisitDialog("ND LabelDesigner – "+Loc.T("Tlačiť"),()=>next.PerformClick(),printing=>{
    var panel=(PrintPanel)printing.Controls[0];
    var output=Field<OutputPlan>(panel,"outputPlan");
    var repeats=Field<NumericUpDown>(panel,"copies");
    Check(output.Placements.Count==20&&output.Placements[0].Label.Texts[0].Text=="LOT-24"&&repeats.Maximum==1,
     "printing opens with exactly the requested labels and one run");
    printing.Close();
   });
   window.Close();
  });
  Check(File.ReadAllText(path)==before,"printing does not save the edited values into the template");
 }
 static void VisitDialog(string title,Action show,Action<Form> inspect){
  Exception failure=null;bool visited=false;
  using(var timer=new System.Windows.Forms.Timer{Interval=40}){
   timer.Tick+=delegate{
    foreach(Form form in Application.OpenForms){
     if(form.Text!=Loc.T(title))continue;
     timer.Stop();visited=true;
     try{inspect(form);}catch(Exception ex){failure=ex;form.Close();}
     break;
    }
   };
   timer.Start();show();
  }
  if(failure!=null)throw failure;
  Check(visited,"visited dialog "+title);
 }
 static void PresetUi(object controller,Form window,MainForm owner){
  string path=Path.Combine(dir,"preset.csv");
  var mapping=Field<DataGridView>(controller,"mapping");
  var targets=Field<List<string>>(controller,"targets");
  var options=(DataGridViewComboBoxColumn)mapping.Columns["column"];
  foreach(DataGridViewColumn field in mapping.Columns)Check(field.SortMode==DataGridViewColumnSortMode.NotSortable,"mapping row order cannot be changed by sorting");
  var prepare=Field<Button>(controller,"prepareButton");
  var sample=Field<Button>(controller,"sampleButton");
  var presets=Button(window,Loc.T("Predvoľby dávky…"));
  Action<string,int> map=(key,column)=>mapping.Rows[targets.IndexOf(key)].Cells["column"].Value=options.Items[column+1];
  Func<string,int> mapped=key=>options.Items.IndexOf(mapping.Rows[targets.IndexOf(key)].Cells["column"].Value)-1;
  File.WriteAllText(path,"Item;Case;Qty;Serial\r\n590123412345;858000000001;2;007");
  Check((bool)Call(controller,"LoadFile",path,';',65001),"open original supplier table");
  map("code",0);map("text:0",1);map("quantity",2);map("unique",3);
  VisitDialog("Predvoľby dávky…",()=>presets.PerformClick(),dialog=>{
   ((TextBox)dialog.Controls.Find("batchPresetName",true)[0]).Text="Supplier A";
   Button(dialog,Loc.T("Uložiť aktuálne mapovanie")).PerformClick();
  });
  var setup=((LabelSettings)Call(owner,"Read")).Batch;
  Check(setup.Presets.Count==1&&setup.ActivePresetName=="Supplier A","saving a named preset updates project state");
  File.WriteAllText(path,"Case;Serial;Qty;Item\n858000000001;007;3;590123412345");
  Check((bool)Call(controller,"LoadFile",path,';',65001)&&mapped("code")==3&&mapped("text:0")==0&&mapped("unique")==1,
   "active preset uses header names on next import");
  VisitDialog("Náhľad prvých záznamov…",()=>sample.PerformClick(),dialog=>{
   var data=(DataGridView)dialog.Controls.Find("batchSample",true)[0];
   Check(data.RowCount==1&&Convert.ToInt32(data.Rows[0].Cells[0].Value)==2&&
    Convert.ToString(data.Rows[0].Cells[1].Value)=="590123412345"&&data.Columns[1].HeaderText.Contains("Item"),
    "sample gives original row, mapped code and source header");
   Button(dialog,Loc.T("Zavrieť")).PerformClick();
  });
  prepare.PerformClick();
  var batch=Field<List<BatchRecord>>(controller,"batch");
  Check(batch.Count==1&&batch[0].Label.Ean=="590123412345"&&batch[0].Count==3,"prepared batch uses the exact preview mapping");
  SavedReferenceUi(controller,window,owner,path);
  File.WriteAllText(path,"Item;item;Case;Serial\n590123412345;858000000001;Case;3");
  Check((bool)Call(controller,"LoadFile",path,';',65001)&&!prepare.Enabled&&mapping.Columns["schema"].Visible&&
   mapped("code")==-1&&mapped("quantity")==-1,"duplicate and missing headings visibly block preparation");
  map("code",0);map("quantity",3);
  Check(prepare.Enabled&&!mapping.Columns["schema"].Visible,"explicit replacement mapping resolves schema errors");
  map("quantity",-1);
  Check(!prepare.Enabled,"removing the replacement leaves the original schema error unresolved");
  map("quantity",3);
  VisitDialog("Predvoľby dávky…",()=>presets.PerformClick(),dialog=>Button(dialog,Loc.T("Ručné mapovanie")).PerformClick());
  var header=Field<CheckBox>(controller,"header");
  header.Checked=false;
  Field<ModernComboBox>(controller,"separator").SelectedIndex=1;
  File.WriteAllText(path,"590123412345,\"A\",2\n858000000001,B,3");
  Check((bool)Call(controller,"LoadFile",path,',',65001),"headerless CSV imported");
  map("code",0);map("text:0",1);map("quantity",2);map("unique",-1);
  VisitDialog("Predvoľby dávky…",()=>presets.PerformClick(),dialog=>{
   ((TextBox)dialog.Controls.Find("batchPresetName",true)[0]).Text="Positions";
   Button(dialog,Loc.T("Uložiť aktuálne mapovanie")).PerformClick();
  });
  setup=((LabelSettings)Call(owner,"Read")).Batch;
  Check(setup.Presets.Count==2&&setup.ActivePresetName=="Positions","multiple saved presets and positional mode persisted");
  VisitDialog("Náhľad prvých záznamov…",()=>prepare.PerformClick(),dialog=>Button(dialog,Loc.T("Zrušiť")).PerformClick());
  Check(Field<List<BatchRecord>>(controller,"batch").Count==0,"cancelling positional confirmation leaves batch unprepared");
  VisitDialog("Náhľad prvých záznamov…",()=>prepare.PerformClick(),dialog=>Button(dialog,Loc.T("Potvrdiť mapovanie")).PerformClick());
  Check(Field<List<BatchRecord>>(controller,"batch").Count==2,"confirming positional sample continues preparation");
  map("code",1);
  VisitDialog("Náhľad prvých záznamov…",()=>prepare.PerformClick(),dialog=>Button(dialog,Loc.T("Zrušiť")).PerformClick());
  Check(Field<List<BatchRecord>>(controller,"batch").Count==0,"changing a positional binding requires new confirmation");
  VisitDialog("Predvoľby dávky…",()=>presets.PerformClick(),dialog=>{
   var list=(ListBox)dialog.Controls.Find("batchPresetList",true)[0];
   list.SelectedItem="Supplier A";
   Button(dialog,Loc.T("Použiť predvoľbu")).PerformClick();
  });
  Check(((LabelSettings)Call(owner,"Read")).Batch.ActivePresetName=="Supplier A"&&header.Checked&&
   Field<ModernComboBox>(controller,"separator").SelectedIndex==0&&!prepare.Enabled,
   "a preset using another delimiter activates before importing a replacement");
  File.WriteAllText(path,"Case;Serial;Qty;Item\n858000000001;007;3;590123412345");
  Check((bool)Call(controller,"LoadFile",path,';',65001)&&prepare.Enabled&&mapped("code")==3,"new import follows newly selected preset");
 }
 static void SavedReferenceUi(object controller,Form batchWindow,MainForm owner,string path){
  var referenceButton=Button(batchWindow,Loc.T("Referenčná dávka…"));
  const string managerTitle="Referenčná dávka…";
  Func<BatchReference> read=()=>((LabelSettings)Call(owner,"Read")).Batch.Presets.Find(preset=>preset.Name=="Supplier A").Reference;
  Action<Form,string,bool> confirm=(manager,action,yes)=>VisitDialog("Potvrdenie referencie",
   ()=>Button(manager,Loc.T(action)).PerformClick(),dialog=>{
    Check(dialog.AcceptButton==dialog.CancelButton&&dialog.CancelButton.DialogResult==DialogResult.No,
     "reference modification requires an explicit affirmative choice");
    Button(dialog,Loc.T(yes?"Áno":"Nie")).PerformClick();
   });
  const string save="Uložiť aktuálnu dávku ako referenciu…";
  VisitDialog(managerTitle,()=>referenceButton.PerformClick(),manager=>{
   confirm(manager,save,true);
   Check(read()!=null&&read().RowCount==1,"saving the reference attaches it to the active project preset");
   VisitDialog("Uložená referenčná dávka",()=>Button(manager,Loc.T("Zobraziť referenciu")).PerformClick(),dialog=>{
    var grid=(DataGridView)dialog.Controls.Find("savedBatchReferenceRows",true)[0];
    Check(grid.VirtualMode&&grid.RowCount==1&&Convert.ToString(grid.Rows[0].Cells["id"].Value)=="007",
     "saved reference displays original identifier and rows");
    Button(dialog,Loc.T("Zavrieť")).PerformClick();
   });
   Button(manager,Loc.T("Zavrieť")).PerformClick();
  });
  File.WriteAllText(path,"Case;Serial;Qty;Item\n858000000001;007;4;590123412345");
  Check((bool)Call(controller,"LoadFile",path,';',65001),"replacement fixture loaded with existing preset");
  Call(controller,"Prepare");
  var previous=Convert.ToBase64String(read().PackedRows);
  VisitDialog("Rozdiely medzi dávkami",()=>VisitDialog(managerTitle,
   ()=>Button(batchWindow,Loc.T("Porovnať dávku…")).PerformClick(),
   manager=>Button(manager,Loc.T("Porovnať s uloženou referenciou")).PerformClick()),dialog=>{
    var rows=(DataGridView)dialog.Controls.Find("batchComparisonRows",true)[0];
    rows.CurrentCell=rows.Rows[0].Cells["id"];
    var details=(DataGridView)dialog.Controls.Find("batchComparisonValues",true)[0];
    Check(details.RowCount==1&&Convert.ToString(details.Rows[0].Cells["before"].Value)=="3"&&
     Convert.ToString(details.Rows[0].Cells["after"].Value)=="4","saved reference feeds the existing comparison review");
    Button(dialog,Loc.T("Zavrieť")).PerformClick();
   });
  VisitDialog(managerTitle,()=>referenceButton.PerformClick(),manager=>{
   confirm(manager,save,false);
   Check(Convert.ToBase64String(read().PackedRows)==previous,"declining replacement keeps the original snapshot");
   confirm(manager,save,true);
   Check(Convert.ToBase64String(read().PackedRows)!=previous,"confirming replacement updates only this preset");
   confirm(manager,"Odstrániť referenciu",false);
   Check(read()!=null,"declining deletion keeps the reference");
   confirm(manager,"Odstrániť referenciu",true);
   Check(read()==null&&!Button(manager,Loc.T("Zobraziť referenciu")).Enabled,"confirmed removal clears the stored reference");
   Button(manager,Loc.T("Zavrieť")).PerformClick();
  });
 }
 static void ComparisonUi(object controller,Form batchWindow){
  var current=Path.Combine(dir,"current-comparison.csv");
  File.WriteAllText(current,"SKU;Code;Name;Qty\n01;590123412345;\"First\nlabel\";1\n02;590123412345;New;2");
  var compare=Button(batchWindow,Loc.T("Porovnať dávku…"));
  Check((bool)Call(controller,"LoadFile",current,';',65001)&&!compare.Enabled,"comparison requires prepared imported rows");
  var map=Field<DataGridView>(controller,"mapping");
  var columns=(DataGridViewComboBoxColumn)map.Columns["column"];
  var targets=Field<List<string>>(controller,"targets");
  map.Rows[targets.IndexOf("code")].Cells["column"].Value=columns.Items[2];
  map.Rows[targets.IndexOf("text:0")].Cells["column"].Value=columns.Items[3];
  map.Rows[targets.IndexOf("quantity")].Cells["column"].Value=columns.Items[4];
  Call(controller,"Prepare");
  Check(compare.Enabled,"prepared imported batch enables comparison");
  var prepared=Field<List<BatchRecord>>(controller,"batch");
  var grid=Field<DataGridView>(controller,"records");
  grid.Rows[1].Cells[0].Value=false;grid.EndEdit();
  var csv=Path.Combine(dir,"prior-comparison.csv");
  File.WriteAllText(csv,"Qty,Name,Code,SKU\n1,Old,590123412345,02\n1,Removed,590123412345,03");
  var xlsx=Path.Combine(dir,"prior-comparison.xlsx");
  using(var file=File.Create(xlsx))using(var zip=new ZipArchive(file,ZipArchiveMode.Create)){
   ZipText(zip,"xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Other' r:id='r1'/><sheet name='Prior' r:id='r2'/></sheets></workbook>");
   ZipText(zip,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/><Relationship Id='r2' Target='worksheets/sheet2.xml'/></Relationships>");
   ZipText(zip,"xl/worksheets/sheet1.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Other</t></is></c></row></sheetData></worksheet>");
   string xml="<worksheet><sheetData>";
   var values=new[]{new[]{"Qty","Name","Code","SKU"},new[]{"1","Old","590123412345","02"},new[]{"1","Removed","590123412345","03"}};
   for(int i=0;i<values.Length;i++){
    int row=i==0?1:i+2;
    xml+="<row r='"+row+"'>";
    for(int col=0;col<values[i].Length;col++)xml+="<c r='"+(char)('A'+col)+row+"' t='inlineStr'><is><t>"+values[i][col]+"</t></is></c>";
    xml+="</row>";
   }
   ZipText(zip,"xl/worksheets/sheet2.xml",xml+"</sheetData></worksheet>");
  }
  VisitDialog("Nastavenia referenčného CSV",()=>Call(controller,"CompareBatchWithFile",csv),
   dialog=>Button(dialog,Loc.T("Zrušiť")).PerformClick());
  Check(object.ReferenceEquals(prepared,Field<List<BatchRecord>>(controller,"batch")),"cancelled reference import keeps prepared batch");
  ExerciseComparison(controller,csv,true);
  ExerciseComparison(controller,xlsx,false);
  Check(object.ReferenceEquals(prepared,Field<List<BatchRecord>>(controller,"batch"))&&!Field<List<bool>>(controller,"included")[1]
   &&grid.CurrentCell.RowIndex==1,"comparison selects real source row without changing the prepared data or print selection");
  ExerciseComparison(controller,csv,true,1);
  Check(Field<List<bool>>(controller,"included")[0]&&!Field<List<bool>>(controller,"included")[1],
   "closing comparison discards the pending print selection");
  ExerciseComparison(controller,xlsx,false,2);
  Check(!Field<List<bool>>(controller,"included")[0]&&Field<List<bool>>(controller,"included")[1],
   "confirmation replaces the selection with the explicitly chosen source row");
  var args=new object[]{null,null};
  Check((bool)Call(controller,"Plan",args),"existing output planner accepts the comparison selection");
  var plan=(OutputPlan)args[0];
  Check(plan.RecordCount==1&&plan.Placements.Count==2&&plan.Placements.TrueForAll(place=>place.Record==4),
   "output uses the current quantity of two for the selected row");
  int firstCount=prepared[0].Count,secondCount=prepared[1].Count;
  try{
   prepared[1].Error="Invalid prepared row";
   ExerciseComparison(controller,xlsx,false,3);
   prepared[1].Error=null;
   prepared[0].Count=6000;prepared[1].Count=6000;
   ExerciseComparison(controller,xlsx,false,4);
  }finally{prepared[1].Error=null;prepared[0].Count=firstCount;prepared[1].Count=secondCount;}
  Check(!Field<List<bool>>(controller,"included")[0]&&Field<List<bool>>(controller,"included")[1],
   "an invalid or oversized selection leaves the previous batch selection in place");
 }
 static void ExerciseComparison(object controller,string path,bool csv,int selection=0){
  int step=csv?0:1;
  Exception failure=null;
  using(var timer=new System.Windows.Forms.Timer{Interval=40}){
   var watch=Stopwatch.StartNew();
   timer.Tick+=delegate{
    string title=Loc.T(step==0?"Nastavenia referenčného CSV":step==1?"Porovnať dávku…":"Rozdiely medzi dávkami");
    Form dialog=null;
    foreach(Form candidate in Application.OpenForms)if(candidate.Text==title){dialog=candidate;break;}
    if(watch.ElapsedMilliseconds>30000){
     failure=new Exception("Comparison dialog stalled at "+step);
     timer.Stop();
     var open=new List<Form>();
     foreach(Form candidate in Application.OpenForms)
      if(candidate.Modal&&candidate.Text!=Loc.T("Dávka a premenné"))open.Add(candidate);
     for(int i=open.Count-1;i>=0;i--)open[i].Close();
     return;
    }
    if(dialog==null)return;
    try{
     if(step==0){
      ((ComboBox)dialog.Controls.Find("comparisonSeparator",true)[0]).SelectedIndex=1;step=1;
      Button(dialog,Loc.T("Načítať tabuľku")).PerformClick();
     }else if(step==1){
      if(!csv)((ComboBox)dialog.Controls.Find("comparisonReferenceSheet",true)[0]).SelectedIndex=1;
      ((ComboBox)dialog.Controls.Find("comparisonCurrentKey",true)[0]).SelectedIndex=0;
      Check(((ComboBox)dialog.Controls.Find("comparisonReferenceKey",true)[0]).SelectedIndex==3,
       "key inferred by heading after reference column reordering");
      step=2;
      ((Button)dialog.Controls.Find("comparisonRun",true)[0]).PerformClick();
     }else if(step==2){
      var rows=(DataGridView)dialog.Controls.Find("batchComparisonRows",true)[0];
      var details=(DataGridView)dialog.Controls.Find("batchComparisonValues",true)[0];
      Check(rows.VirtualMode&&rows.RowCount==3&&Convert.ToInt32(rows.Rows[1].Cells["now"].Value)==4,"report uses physical CSV line for changed row");
      Check(Convert.ToInt32(rows.Rows[1].Cells["before"].Value)==(csv?2:3),"reference row number remains accurate after sparse worksheet rows");
      rows.CurrentCell=rows.Rows[2].Cells["id"];
      var jump=Button(dialog,Loc.T("Prejsť na etiketu"));
      Check(!jump.Enabled,"reference-only removal has no current preview target");
      rows.CurrentCell=rows.Rows[1].Cells["id"];
      Check(details.RowCount==2&&Convert.ToString(details.Rows[0].Cells["before"].Value)=="Old"&&
       Convert.ToString(details.Rows[0].Cells["after"].Value)=="New","report displays previous and current values");
      step=3;timer.Stop();
      if(selection==0){jump.PerformClick();return;}
      var applyControls=dialog.Controls.Find("applyComparisonSelection",true);
      var summaryControls=dialog.Controls.Find("comparisonPrintSummary",true);
      Check(applyControls.Length==1&&summaryControls.Length==1,"comparison provides a confirmation action and summary");
      var apply=(Button)applyControls[0];
      var summary=(Label)summaryControls[0];
      Check(!apply.Enabled,"empty comparison selection cannot be confirmed");
      Button(dialog,Loc.T("Označiť pridané")).PerformClick();
      Button(dialog,Loc.T("Označiť zmenené")).PerformClick();
      if(selection>=3){
       string warning=Loc.T(selection==3?"Chybné záznamy vo výbere · {0}":"Najviac 10000 etikiet");
       Check(!apply.Enabled&&summary.Text.Contains(selection==3?string.Format(warning,1):warning),
        "summary blocks invalid prepared rows and selections over the output limit");
       ((Button)dialog.CancelButton).PerformClick();return;
      }
      Check(apply.Enabled&&summary.Text.Contains(string.Format(Loc.T("Výber na tlač · Záznamy {0} · Etikety {1}"),2,3)),
       "bulk selection counts current quantities for both categories");
      Check(rows.Columns["id"].ReadOnly&&!rows.Columns["print"].ReadOnly,"only selection checkboxes are editable");
      rows.CurrentCell=rows.Rows[2].Cells["print"];
      Check(!rows.BeginEdit(false),"removed row checkbox cannot be edited");
      rows.Rows[2].Cells["print"].Value=true;
      Check(summary.Text.Contains(string.Format(Loc.T("Výber na tlač · Záznamy {0} · Etikety {1}"),2,3)),
       "removed reference row never enters the print selection");
      if(selection==1){((Button)dialog.CancelButton).PerformClick();return;}
      var cell=(DataGridViewCheckBoxCell)rows.Rows[0].Cells["print"];
      rows.CurrentCell=cell;
      Check(rows.BeginEdit(false),"a current row checkbox can be edited");
      typeof(DataGridViewCheckBoxCell).GetMethod("OnContentClick",Hidden).Invoke(cell,new object[]{new DataGridViewCellEventArgs(cell.ColumnIndex,0)});
      Check(summary.Text.Contains(string.Format(Loc.T("Výber na tlač · Záznamy {0} · Etikety {1}"),1,2)),
       "individual checkbox edits immediately update the total before leaving the cell");
      apply.PerformClick();
     }
    }catch(Exception ex){failure=ex;timer.Stop();dialog.Close();}
   };
   timer.Start();Call(controller,"CompareBatchWithFile",path);
  }
  if(failure!=null)throw failure;
  Check(step==3,"reference file workflow completed");
 }
 static void CsvIssuesUi(object controller,Form batchWindow){
  string csv=Path.Combine(dir,"issues.csv");
  File.WriteAllText(csv,"Code;Name\r\n590123412345;\"Good\r\nlabel\"\r\nnot a barcode;Bad code\r\n590123412345;Other");
  Check((bool)Call(controller,"LoadFile",csv,';',65001),"batch window imports CSV for data check");
  var mapping=Field<DataGridView>(controller,"mapping");
  var targets=Field<List<string>>(controller,"targets");
  var source=(DataGridViewComboBoxColumn)mapping.Columns[1];
  mapping.Rows[targets.IndexOf("code")].Cells[1].Value=source.Items[1];
  mapping.Rows[targets.IndexOf("text:0")].Cells[1].Value=source.Items[2];
  Call(controller,"Prepare");
  var grid=Field<DataGridView>(controller,"records");
  var print=Field<Button>(controller,"printSelected");
  var open=Button(batchWindow,Loc.T("Problémy vstupných dát…"));
  Check(open!=null&&open.Enabled&&!print.Enabled,"imported error enables review and blocks printing");
  Exception dialogFailure=null;bool visited=false;
  using(var timer=new System.Windows.Forms.Timer{Interval=40}){
   timer.Tick+=delegate{
    foreach(Form dialog in Application.OpenForms){
     if(dialog.Text!=Loc.T("Problémy vstupných dát…"))continue;
     timer.Stop();
     try{
      var issues=(DataGridView)dialog.Controls.Find("batchIssues",true)[0];
      Check(issues.VirtualMode&&issues.RowCount==1&&Convert.ToInt32(issues.Rows[0].Cells[0].Value)==4,"issue list displays physical CSV line");
      Button(dialog,Loc.T("Prejsť na etiketu")).PerformClick();visited=true;
     }catch(Exception ex){dialogFailure=ex;dialog.Close();}
     break;
    }
   };
   timer.Start();open.PerformClick();
  }
  if(dialogFailure!=null)throw dialogFailure;
  Check(visited&&grid.CurrentCell.RowIndex==1,"issue action selects matching record in the batch window");
  var picture=Field<PictureBox>(controller,"view");
  var watch=Stopwatch.StartNew();
  while(picture.Image==null&&watch.ElapsedMilliseconds<5000){Application.DoEvents();Thread.Sleep(5);}
  Check(picture.Image!=null,"row with invalid code still displays its valid artwork");
  grid.Rows[1].Cells[0].Value=false;grid.EndEdit();
  Check(print.Enabled,"excluding just the erroneous row enables the other labels");
 }
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Control.CheckForIllegalCrossThreadCalls=true;Core();Logs();Dialogs();BatchUi();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" cancellation, atomic output, CSV/XLSX, batch snapshots, progress, UI heartbeat, close/commit races, virtual grid and private bounded diagnostics checks. No physical printing.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
