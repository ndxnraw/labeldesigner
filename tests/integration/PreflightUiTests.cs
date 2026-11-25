using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Reflection;
using System.Windows.Forms;
using EanEtikety;

class PreflightUiTests {
 static string dir;static int checks;
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,Hidden).GetValue(o);}
 static void Set(object o,string name,object value){o.GetType().GetField(name,Hidden).SetValue(o,value);}
 static object Call(object o,string name,params object[] args){foreach(var method in o.GetType().GetMethods(Hidden))if(method.Name==name&&method.GetParameters().Length==args.Length)return method.Invoke(o,args);throw new MissingMethodException(name);}
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Reject(Action action,string message){bool failed=false;try{action();}catch{failed=true;}Check(failed,message);}
 static LabelSettings Label(){var s=LabelSettings.Default();s.HideBarcode=true;s.Texts.Clear();s.Texts.Add(new TextItem{Text="First",X=5,Y=5,Width=30,Height=12,Size=12});s.Texts.Add(new TextItem{Text="Second",X=5,Y=22,Width=30,Height=12,Size=12});return s;}
 static void Pump(Func<bool> ready){var watch=Stopwatch.StartNew();while(!ready()){Application.DoEvents();System.Threading.Thread.Sleep(2);if(watch.ElapsedMilliseconds>30000)throw new Exception("UI timeout");}}
 static void Snapshot(Form form,string name){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(dir,name+".png"));}}
 static Button Button(Control control,string text){if(control is Button&&control.Text==text)return (Button)control;foreach(Control child in control.Controls){var found=Button(child,text);if(found!=null)return found;}return null;}
 static void CancelReview(Action start){bool seen=false;using(var timer=new Timer{Interval=50}){timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form is OutputReviewDialog){timer.Stop();seen=true;form.DialogResult=DialogResult.Cancel;form.Close();break;}};timer.Start();start();}Check(seen,"export uses shared cancellable review");}
 static void CleanExports(){
  using var form=new MainForm(Path.Combine(dir,"clean.xml"));
  form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
  int saves=0;
  Set(form,"showExportSave",(Func<SaveFileDialog,IWin32Window,DialogResult>)((dialog,owner)=>{
   saves++;
   dialog.FileName=Path.Combine(dir,saves==1?"direct.svg":"direct.pdf");
   return DialogResult.OK;
  }));
  Call(form,"Apply",Label());
  Call(form,"Export","svg");
  Check(saves==1&&File.Exists(Path.Combine(dir,"direct.svg")),"clean single export saves directly");
  var sheet=Label();sheet.UsePaper=true;sheet.PageWidth=210;sheet.PageHeight=297;sheet.Layout.Enabled=true;
  Call(form,"Apply",sheet);
  Call(form,"Export","pdf");
  Check(saves==2&&File.Exists(Path.Combine(dir,"direct.pdf")),"clean sheet export saves directly");
  var warning=Label();warning.Texts[0].X=55;Call(form,"Apply",warning);
  CancelReview(()=>Call(form,"Export","svg"));
  Check(saves==2,"cancelling warning confirmation prevents export");
 }
 static void Domain(){
  var s=Label();s.UsePaper=true;s.PageWidth=160;s.PageHeight=130;s.Layout=new SheetLayout{Enabled=true,Columns=2,Rows=2,FirstSlot=3};
  var first=ProjectXml.Clone(s);first.Texts[0].X=55;
  var plan=OutputPlan.Create(new List<BatchRecord>{new BatchRecord{Row=7,Count=2,Label=first},new BatchRecord{Row=11,Count=3,Label=s}},s);
  var summary=new OutputSummary(plan,4);Check(summary.Records==2&&summary.Labels==5&&summary.Pages==2,"source records, labels and partially filled pages counted independently");Check(summary.TotalLabels==20&&summary.TotalPages==8,"whole-job repetitions multiply totals");Reject(()=>new OutputSummary(plan,0),"zero repetitions rejected");
  var report=PreflightReport.Inspect(plan);Check(!report.Blocking&&report.HasWarnings,"geometry overflow remains overridable warning");Check(report.Issues.Exists(i=>i.Record==7&&i.Page==1&&i.Element==0),"warning identifies original source row, first affected page and element");Check(!report.CanProceed(false)&&report.CanProceed(true),"warnings require explicit acknowledgement");
  string svg=Path.Combine(dir,"summary-pages.zip");JobOutput.Export(svg,"svg",plan,203);using(var zip=ZipFile.OpenRead(svg))Check(zip.Entries.Count==summary.Pages,"exported page count matches reviewed partially filled sheet");
  string fingerprint=report.Fingerprint;report.Context="changed media";Check(report.Fingerprint!=fingerprint,"driver context participates in consent fingerprint");
  plan.Placements[0].Label.HideBarcode=false;plan.Placements[0].Label.Ean="invalid";report=PreflightReport.Inspect(plan);Check(report.Blocking&&!report.CanProceed(true),"confirmed warnings cannot override invalid barcode");Reject(report.EnsureValid,"blocking report refuses output");
  Check(PreflightReport.Inspect(new OutputPlan()).Blocking,"empty output blocked");
  using(var document=new LabelPrintDocument(Label())){document.PrinterSettings.PrinterName="Microsoft Print to PDF";document.DefaultPageSettings.PaperSize=new PaperSize("Tiny",80,80);document.ValidatePrinter();report=document.InspectOutput();Check(!report.Blocking&&report.HasWarnings&&report.CanProceed(true),"false driver media mismatch can still print after acknowledgement");fingerprint=report.Fingerprint;document.OffsetX=1;Check(document.InspectOutput().Fingerprint!=fingerprint,"calibration change requires renewed consent");}
 }
 static void Review(){
  var plan=OutputPlan.Create(Label());var report=PreflightReport.Inspect(plan);report.AddWarning("Review warning");
  using(var dialog=new OutputReviewDialog(report,3,"Export","SVG",SystemFonts.MessageBoxFont,null)){
   dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();var proceed=Field<Button>(dialog,"proceed");Check(!proceed.Enabled,"warning action initially disabled");Field<CheckBox>(dialog,"acknowledge").Checked=true;Check(proceed.Enabled,"acknowledgement enables nonblocking action");
   report.Issues.Add(new LabelIssue{Blocking=true,Message="Blocking error",Element=0});var view=Field<IssueView>(dialog,"issues");view.SetIssues(report.Issues);Field<ComboBox>(view,"filter").SelectedIndex=2;
   dialog.DialogResult=DialogResult.OK;dialog.Close();Check(!dialog.IsDisposed&&dialog.Visible,"closing guard prevents bypass even with errors hidden by warning filter");dialog.DialogResult=DialogResult.Cancel;dialog.Close();
  }
  foreach(string language in Loc.Codes){Loc.Language=language;using(var dialog=new OutputReviewDialog(PreflightReport.Inspect(plan),1,Loc.T("Exportovať"),"PDF",SystemFonts.MessageBoxFont,null)){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.ClientSize=new Size(560,480);dialog.Show();Check(Field<Button>(dialog,"proceed").Enabled,"clean output enabled: "+language);Check(Loc.T("Etikety spolu")!="Etikety spolu"||language=="sk","summary translated: "+language);Snapshot(dialog,language+"-review");dialog.Close();}}
  Loc.Language="sk";
  var blocked=new PreflightReport(plan);
  blocked.AddWarning("Printer driver warning");
  var target=new LabelIssue{Blocking=true,Record=17,Element=0,Message="Fix this row"};
  blocked.Issues.Add(target);
  using(var dialog=new OutputReviewDialog(blocked,1,"Export","PDF",SystemFonts.MessageBoxFont,null)){
   dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();
   var edit=(Button)dialog.Controls.Find("fixOutputIssue",true)[0];
   Check(edit.Enabled&&!Field<Button>(dialog,"proceed").Enabled,"blocking row offers repair without allowing output");
   edit.PerformClick();Check(ReferenceEquals(dialog.EditIssue,target),"repair returns the identified source row");
  }
 }
 static void Editor(){using(var form=new MainForm(Path.Combine(dir,"editor.xml"))){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Call(form,"Apply",Label());var preview=Field<PreviewPanel>(form,"preview");var list=Field<ListBox>(form,"elementList");
  object layerItem=list.Items[0];preview.SelectMany(new[]{0,1},true);Check(object.ReferenceEquals(layerItem,list.Items[0]),"selection-only update preserves native list items and selection anchor");Check(list.SelectedItems.Count==2,"canvas multiple selection mirrored in layer list");Check(Field<TableLayoutPanel>(form,"selectionInspector").Visible&&!Field<TableLayoutPanel>(form,"textInspector").Visible,"multiple selection has explicit inspector");string before=ProjectXml.Encode((LabelSettings)Call(form,"Read"));Field<TextBox>(form,"inspectText").Text="must not leak";Call(form,"CommitInspector");Check(ProjectXml.Encode((LabelSettings)Call(form,"Read"))==before,"multiple selection cannot silently edit its first member");
  Call(form,"Arrange","group");Call(form,"ToggleSelectionLock");Check(list.SelectedItems.Count==2&&list.Items[0].ToString().Contains(Loc.T("Zamknuté")),"layer list displays group lock state");Call(form,"ToggleSelectionLock");Check(!Elements.Locked((LabelSettings)Call(form,"Read"),0),"same command unlocks selection");Call(form,"Arrange","ungroup");preview.SelectElement(0);Call(form,"Arrange","front");Check(list.Items[0].ToString()=="First","front command updates visible layer order");Call(form,"StepHistory",false);Check(list.Items[0].ToString()=="Second","layer reorder is undoable");
  preview.SelectMany(new int[0],true);Check(Field<Label>(form,"selectionOverview").Text==Loc.T("Nie je vybraný prvok"),"empty selection explicit");
  var bad=Label();bad.HideBarcode=false;bad.Ean="bad";Call(form,"Apply",bad);Call(form,"PrintLabel");Check(Field<ModernTabs>(form,"workspaceTabs").SelectedIndex==1,"invalid print shortcut opens persistent problems instead of output");var view=Field<IssueView>(form,"problemView");var issues=Field<ListBox>(view,"list");for(int i=0;i<issues.Items.Count;i++)if(issues.Items[i] is LabelIssue&&((LabelIssue)issues.Items[i]).Element==-1){issues.SelectedIndex=i;break;}Check(preview.SelectedIndex==-1,"problem selection navigates to source element");
  form.Size=form.MinimumSize;Application.DoEvents();Check(issues.Height>60,"persistent issue list remains readable at minimum size");Snapshot(form,"editor-problems-minimum");
  Call(issues,"OnKeyDown",new KeyEventArgs(Keys.Enter));
  Check(Field<TextBox>(form,"ean").ContainsFocus,"Enter on the issue focuses the repair field");
  Call(form,"Apply",Label());
  Check(form.Controls.Find("openEditorChecks",true).Length==0,"no redundant review button beside the editor status");
  var body=Field<TableLayoutPanel>(form,"studioBody");body.ColumnStyles[0].Width=0;Call(form,"ProcessCmdKey",new Message(),Keys.F8);Check(body.ColumnStyles[0].Width>0&&Field<ModernTabs>(form,"workspaceTabs").SelectedIndex==1,"F8 restores hidden problems panel");body.ColumnStyles[2].Width=0;Call(form,"ProcessCmdKey",new Message(),Keys.F4);Check(body.ColumnStyles[2].Width>0,"F4 restores properties panel");var tabs=Field<ModernTabs>(form,"workspaceTabs");Call(tabs,"ProcessCmdKey",new Message(),Keys.Control|Keys.PageDown);Check(tabs.SelectedIndex==2,"keyboard cycles to label settings");tabs.SelectedIndex=0;
  list.SelectedIndex=0;Check(preview.SelectedIndex==1,"layer selection navigates to canvas");Check(list.ContextMenuStrip!=null&&list.Height>=35,"context actions and list fit minimum window");preview.SelectMany(new[]{0,1},true);Call(form,"Arrange","lock");Check(Field<DataGridView>(form,"grid").Rows[0].ReadOnly&&Field<DataGridView>(form,"grid").Rows[1].ReadOnly,"multi-selection locking also protects data grid");Call(form,"ToggleTheme");Snapshot(form,"editor-layers-light");Set(form,"dirty",false);form.Close();
 }}
 static void SheetDetail(){
  var sheet=Label();sheet.UsePaper=true;sheet.PageWidth=160;sheet.PageHeight=130;
  sheet.Layout=new SheetLayout{Enabled=true,Columns=2,Rows=2,FirstSlot=2,Count=5};
  using var form=new Form{Size=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)};
  using var panel=new PrintPanel(sheet);
  LabelIssue repair=null;int backs=0;
  panel.EditIssueRequested+=issue=>repair=issue;panel.BackRequested+=delegate{backs++;};
  Set(panel,"initialPrinterName","Microsoft Print to PDF");
  form.Controls.Add(panel);form.Show();Pump(()=>panel.PreviewBuilds>0);
  var zoom=Field<ComboBox>(panel,"zoom");zoom.SelectedItem=Loc.T("Detail etikety");
  var labels=Field<NumericUpDown>(panel,"detailIndex");
  var canvas=Field<PrintCanvas>(panel,"preview");
  Check(labels.Visible&&labels.Maximum==2,"detail selects occupied labels on a partial sheet");
  var first=(RectangleF)Call(canvas,"ViewBounds");int builds=panel.PreviewBuilds;
  labels.Value=2;var second=(RectangleF)Call(canvas,"ViewBounds");
  Check(second.X>first.X&&second.Y==first.Y&&panel.PreviewBuilds==builds,"another slot reuses page geometry and artwork");
  Field<NumericUpDown>(panel,"page").Value=2;Pump(()=>panel.PreviewBuilds>builds);
  Check(labels.Value==1&&labels.Maximum==3,"changing page selects its first occupied label");
  zoom.SelectedIndex=0;
  Check(!labels.Visible&&((RectangleF)Call(canvas,"ViewBounds")).Width>second.Width,"full-page mode restores sheet");
  var report=new PreflightReport(OutputPlan.Create(sheet));
  var error=new LabelIssue{Blocking=true,Element=0,Record=7,Message="Invalid source"};
  report.Issues.Add(error);Set(panel,"currentReport",report);Call(panel,"UpdateRepairAction");
  var action=(Button)panel.Controls.Find("fixPrintIssue",true)[0];
  Check(action.Visible&&!Field<Button>(panel,"print").Visible,"blocking error offers repair as the only primary action");
  action.PerformClick();Check(backs==1&&ReferenceEquals(repair,error),"print repair returns exact problem once");
 }
 static void Printing(){using(var form=new Form{Size=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new PrintPanel(Label())){
  Set(panel,"initialPrinterName","Microsoft Print to PDF");
  form.Controls.Add(panel);form.Show();Pump(()=>panel.PreviewBuilds>0);Check(panel.Ready,"print output ready");int builds=panel.PreviewBuilds;Field<NumericUpDown>(panel,"copies").Value=3;Application.DoEvents();Check(panel.PreviewBuilds==builds&&Field<Label>(panel,"jobSummary").Text.Contains("Etikety spolu: 3"),"repetition summary updates without rerendering preview");
  var summary=Field<Label>(panel,"jobSummary");
  Check(summary.Height>=summary.Font.Height&&summary.Parent.ClientRectangle.Contains(summary.Bounds),"label total is visible above the preview");
  var canvas=Field<PrintCanvas>(panel,"preview");
  var zoom=Field<ComboBox>(panel,"zoom");
  zoom.SelectedItem=Loc.T("Detail etikety");
  var detail=(RectangleF)Call(canvas,"ViewBounds");
  Check(detail.Width<=61&&detail.Height<=46&&detail.Width>55,"detail fits the label, not the entire sheet");
  Check(panel.PreviewBuilds==builds&&Field<Button>(panel,"print").Text.Contains("3"),"detail does not rebuild or change job totals");
  Snapshot(form,"print-label-detail");zoom.SelectedIndex=0;
  bool reviewed=false;using(var timer=new Timer{Interval=50}){timer.Tick+=delegate{foreach(Form dialog in Application.OpenForms)if(dialog is OutputReviewDialog){timer.Stop();reviewed=true;dialog.DialogResult=DialogResult.Cancel;dialog.Close();break;}};timer.Start();Call(panel,"Submit");}Check(reviewed&&!Field<bool>(panel,"sending"),"shared print review can be cancelled before submission");
  // Deliberately stale consent: the worker must return for review without calling Print.
  Set(panel,"approvedFingerprint","unapproved-context");Call(panel,"StartRequest",false,true,null);Pump(()=>!Field<bool>(panel,"busy"));Check(panel.Ready&&Field<Label>(panel,"info").Text.Contains("znova"),"worker rejects stale approval before submitting any job");Check(panel.PreviewBuilds==builds,"revalidation preserves preview");form.Close();
 }}
 static void Batch(){using(var owner=new MainForm(Path.Combine(dir,"batch.xml"))){owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();Call(owner,"Apply",Label());Exception failure=null;bool checkedBatch=false;
  string path=Path.Combine(dir,"batch-direct.pdf");
  Set(owner,"showExportSave",(Func<SaveFileDialog,IWin32Window,DialogResult>)((dialog,parent)=>{dialog.FileName=path;return DialogResult.OK;}));
  using(var timer=new Timer{Interval=50}){timer.Tick+=delegate{foreach(Form form in Application.OpenForms)if(form.Tag!=null&&form.Text==Loc.T("Dávka a premenné")){timer.Stop();try{object window=form.Tag;Field<NumericUpDown>(window,"generated").Value=3;Call(window,"Prepare");Check(Field<Button>(window,"printSelected").Enabled,"valid selected batch enables output");Call(window,"SelectAll",false);Check(!Field<Button>(window,"exportSelected").Enabled,"empty selection disables output");Call(window,"SelectAll",true);var batch=Field<List<BatchRecord>>(window,"batch");batch[1].Error="invalid record";Call(window,"UpdateSelectionSummary");Check(!Field<Button>(window,"printSelected").Enabled&&!Field<Button>(window,"exportSelected").Enabled,"included invalid record blocks both actions");var grid=Field<DataGridView>(window,"records");grid.Rows[1].Cells[0].Value=false;grid.EndEdit();Check(Field<Button>(window,"printSelected").Enabled&&Field<Label>(window,"selectionInfo").Text.Contains(": 2"),"excluding bad record restores output and selected count");Call(window,"Export");Check(File.Exists(path),"clean batch exports selected records directly");Call(window,"NavigateIssue",new LabelIssue{Record=batch[2].Row});Check(grid.CurrentCell.RowIndex==2,"preflight return navigates to original source row");Snapshot(form,"batch-selection");checkedBatch=true;}catch(Exception ex){failure=ex;}finally{form.Close();}break;}};timer.Start();Call(owner,"ShowBatch");}
  if(failure!=null)throw failure;Check(checkedBatch,"batch exercised");Set(owner,"dirty",false);owner.Close();
 }}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);CleanExports();Domain();Review();Editor();Printing();SheetDetail();Batch();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" shared preflight, consent, source navigation, layer selection, undo, summaries and batch checks. No print job submitted. Physical monitor DPI remains manual.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
