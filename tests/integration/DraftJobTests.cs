using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using EanEtikety;

class DraftJobTests {
 static string dir;static int checks;
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly DateTime Night=new DateTime(2026,11,12,23,59,59),Morning=new DateTime(2026,11,13,0,0,1);
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,Hidden).GetValue(o);}
 static void Set(object o,string name,object value){o.GetType().GetField(name,Hidden).SetValue(o,value);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,Hidden).Invoke(o,args);}
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){bool failed=false;try{action();}catch{failed=true;}Check(failed,message);}
 static LabelSettings Template(){var s=LabelSettings.Default();s.Kind=128;s.Ean="{serial}";s.Variables.Add(new VariableField{Name="serial",Start=7,Digits=5});s.Variables.Add(new VariableField{Name="date",Type="Date",Days=1});s.Variables.Add(new VariableField{Name="fixed",Type="Date",FixedDate=true,Date=new DateTime(2020,2,29),Months=1,Days=1});s.Texts[0].Text="{date} / {fixed}";return s;}
 static string Svg(OutputPlan plan){using(var scene=plan.Scene(0))return SceneOutput.Svg(scene,new RectangleF(0,0,(float)plan.Width,(float)plan.Height));}
 static void Wait(PrintPanel panel,Action action){int before=panel.PreviewBuilds;action();var timer=Stopwatch.StartNew();while(panel.PreviewBuilds==before){Application.DoEvents();System.Threading.Thread.Sleep(2);if(timer.ElapsedMilliseconds>30000)throw new Exception("Preview timeout: "+Field<Label>(panel,"info").Text);}Check(panel.Ready,"print preview ready");}
 static void Drafts(){
  string path=Path.Combine(dir,"draft.ean.xml");var good=LabelSettings.Default();Engine.Save(path,good);
  var draft=LabelSettings.Default();draft.Ean="4006381333930";draft.Texts[0].Text="Žltý čaj & rozpracované";
  Engine.Save(path,draft);var loaded=Engine.Load(path);
  Check(loaded.Ean==draft.Ean&&loaded.Texts[0].Text==draft.Texts[0].Text,"invalid checksum and Unicode preserved");
  Check(Engine.Load(path+".bak").Ean==good.Ean,"previous readable project backed up");
  Reject(()=>Engine.Validate(loaded),"invalid draft remains invalid for output");Reject(()=>OutputPlan.Create(loaded),"invalid draft cannot become print plan");
  Reject(()=>Engine.Pdf(Path.Combine(dir,"invalid.pdf"),loaded),"PDF export still rejects invalid code");
  draft.Ean="";Engine.Save(path,draft);Check(Engine.Load(path).Ean=="","empty barcode draft roundtrip");Check(Engine.Load(path+".bak").Ean==loaded.Ean,"backup rotates to previous readable draft");
  string backup=File.ReadAllText(path+".bak");File.WriteAllText(path,"broken XML");Engine.Save(path,good);Check(File.ReadAllText(path+".bak")==backup,"damaged original does not replace readable backup");
  string original=File.ReadAllText(path);using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None)){Reject(()=>Engine.Save(path,draft),"locked target fails without replacing it");}Check(File.ReadAllText(path)==original&&File.ReadAllText(path+".bak")==backup,"failed write preserves project and backup");Check(Directory.GetFiles(dir,"*.tmp").Length==0,"failed saves leave no temporary project");
  var malformed=LabelSettings.Default();malformed.Width=double.NaN;Reject(()=>Engine.Save(path,malformed),"unsafe geometry rejected before write");Check(File.ReadAllText(path)==original,"rejected structure preserves file");
  string unsafePath=Path.Combine(dir,"unsafe.xml");File.WriteAllText(unsafePath,"<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///secret'>]><LabelSettings><Ean>&e;</Ean></LabelSettings>");Reject(()=>Engine.Load(unsafePath),"DTD still rejected");
  malformed=LabelSettings.Default();malformed.Texts=null;File.WriteAllText(unsafePath,ProjectXml.Encode(malformed));Check(Engine.Load(unsafePath).Texts!=null,"omitted collection restores safe defaults");
  var variables=Template();variables.Variables[0].Name="";Engine.Save(path,variables);Check(Engine.Load(path).Variables[0].Name=="","unfinished variable definition can be saved");Reject(()=>new PreparedLabelJob(Engine.Load(path),Night),"unfinished variables block output");
  variables=Template();variables.Variables.Add(new VariableField{Name="serial"});Engine.Save(path,variables);Check(Engine.Load(path).Variables.Count==4,"duplicate variable draft retained for repair");Reject(()=>new PreparedLabelJob(variables,Night),"duplicate variables block output");
  var matrix=LabelSettings.Default();matrix.Symbols.Add(new SymbolItem{Matrix=new MatrixContent{Type="QR",Data=""},Width=20,Height=15});Engine.Save(path,matrix);Check(Engine.Load(path).Symbols[0].Matrix.Data=="","unfinished 2D barcode retained");Reject(()=>OutputPlan.Create(matrix),"unfinished 2D barcode cannot print");
  var sheet=LabelSettings.Default();sheet.UsePaper=true;sheet.PageWidth=20;sheet.PageHeight=20;Engine.Save(path,sheet);Check(Engine.Load(path).PageWidth==20,"unfinished paper layout retained");Reject(()=>OutputPlan.Create(sheet),"nonfitting layout still blocks output");
  string recovery=Path.Combine(dir,"draft-recovery");using(var store=new WorkspaceStore(recovery)){store.SaveRecovery(draft,path);var unsafeDraft=ProjectXml.Clone(draft);unsafeDraft.Width=double.NaN;Reject(()=>store.SaveRecovery(unsafeDraft,path),"unsafe recovery cannot replace the previous draft");}
  using(var store=new WorkspaceStore(recovery)){int found=0;store.Recoverable((file,document)=>{found++;Check(document.Label.Ean==draft.Ean&&document.OriginalPath==path,"unfinished draft survives recovery");File.Delete(file);},error=>{throw new Exception(error);});Check(found==1,"previous recovery is retained after failed autosave");store.Clean();}
 }
 static void Jobs(){
  var template=Template();var job=new PreparedLabelJob(template,Night);var next=new PreparedLabelJob(template,Morning);
  Check(job.Label.Texts[0].Text=="2026-11-13 / 2020-03-30","date uses job preparation time with offsets and fixed date");
  Check(next.Label.Texts[0].Text=="2026-11-14 / 2020-03-30","new job uses new day");
  Check(job.PreparedAt==Night&&job.Plan.PreparedAt==Night&&job.Label.Variables.Count==0,"resolved job records its time");Check(job.VariableValues.Contains("serial = 00007")&&job.VariableValues.Contains("date = 2026-11-13"),"resolved values available for review");
  string svg=Svg(job.Plan);template.Texts[0].Text="Changed";template.Variables[0].Start=99;Check(job.Label.Ean=="00007"&&Svg(job.Plan)==svg,"job is isolated from source edits");
  foreach(string format in new[]{"pdf","docx","svg","zpl"}){string path=Path.Combine(dir,"frozen."+format);JobOutput.Export(path,format,job.Plan,203);Check(new FileInfo(path).Length>100,"frozen job exports "+format);}Check(Svg(job.Plan)==svg,"all exports leave prepared content unchanged");
  template=Template();var first=BatchData.Record(template,new string[0],new List<ColumnBinding>(),-1,0,1,Night);var last=BatchData.Record(template,new string[0],new List<ColumnBinding>(),-1,1,2,Night);var plan=OutputPlan.Create(new List<BatchRecord>{first,last},template);
  Check(plan.PreparedAt==Night&&plan.Placements[0].Label.Ean=="00007"&&plan.Placements[1].Label.Ean=="00008","batch shares preparation time and consecutive values");Check(plan.VariableValues.Contains("00007")&&plan.VariableValues.Contains("00008"),"batch variable range available for review");
  Check(plan.Placements[1].Label.Texts[0].Text==job.Label.Texts[0].Text,"batch uses the same date as single job");
  var different=BatchData.Record(template,new string[0],new List<ColumnBinding>(),-1,1,2,Morning);Reject(()=>OutputPlan.Create(new List<BatchRecord>{first,different},template),"mixed preparation times cannot silently form one job");
  var rawPlan=OutputPlan.Create(template);Check(rawPlan.Placements[0].Label.Variables.Count==0,"low-level output plan also freezes variables");
  var persisted=Path.Combine(dir,"variable-template.ean.xml");Engine.Save(persisted,template);var saved=Engine.Load(persisted);Check(saved.Ean=="{serial}"&&saved.Texts[0].Text.Contains("{date}")&&saved.Variables.Count==3,"saving never replaces template with resolved values");
 }
 static void Editor(){using(var form=new MainForm(Path.Combine(dir,"prefs.xml"))){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
  var draft=LabelSettings.Default();draft.Ean="invalid";Call(form,"Apply",draft);Call(form,"Changed",form,EventArgs.Empty);string path=Path.Combine(dir,"ui-draft.ean.xml");Call(form,"SaveDocument",path);Check(!Field<bool>(form,"dirty")&&Field<string>(form,"settingsPath")==path,"UI saves draft and clears dirty state");
  Call(form,"OpenImportedFile",path);Check(((LabelSettings)Call(form,"Read")).Ean=="invalid","invalid saved draft reopens in editor");var preview=Field<PreviewPanel>(form,"preview");Check(preview.Scene.Shapes.Count>0&&preview.ProblemElements.Contains(-1),"valid content visible and invalid barcode marked");Check(!Field<Button>(form,"pdf").Enabled,"invalid reopened draft cannot export");
  Call(form,"Changed",form,EventArgs.Empty);string blocker=Path.Combine(dir,"not-a-directory");File.WriteAllText(blocker,"blocker");Reject(()=>Call(form,"SaveDocument",Path.Combine(blocker,"failure.xml")),"UI save failure surfaces");Check(Field<string>(form,"settingsPath")==path&&Field<bool>(form,"dirty"),"failed Save As retains original path and dirty state");
  Set(form,"currentTime",(Func<DateTime>)(()=>Night));Call(form,"Apply",Template());Check(preview.Settings.Texts[0].Text.StartsWith("2026-11-13"),"editor preview resolves current date");Set(form,"currentTime",(Func<DateTime>)(()=>Morning));Call(form,"RefreshDatePreview");Check(preview.Settings.Texts[0].Text.StartsWith("2026-11-14"),"editor preview is no longer frozen at application startup");Check(!Field<bool>(form,"dirty"),"date preview refresh does not dirty the saved template");
  string datePath=Path.Combine(dir,"ui-variables.ean.xml");Call(form,"SaveDocument",datePath);Check(Engine.Load(datePath).Texts[0].Text.Contains("{date}"),"UI saves unresolved template");
  Call(form,"Apply",LabelSettings.Default());form.Close();
 }}
 static void Printing(){var template=Template();using(var form=new Form{Size=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new PrintPanel(template)){
  form.Controls.Add(panel);Wait(panel,()=>form.Show());Set(panel,"jobClock",(Func<DateTime>)(()=>Night));Wait(panel,()=>Call(panel,"RefreshJobVariables"));
  var plan=Field<OutputPlan>(panel,"outputPlan");Check(plan.PreparedAt==Night&&plan.Placements[0].Label.Texts[0].Text.StartsWith("2026-11-13"),"print window uses prepared date");Check(Field<Label>(panel,"jobInfo").Text.Contains("00007"),"print window exposes resolved serial value");
  Set(panel,"jobClock",(Func<DateTime>)(()=>Morning));Wait(panel,()=>panel.RefreshPreview());Check(object.ReferenceEquals(plan,Field<OutputPlan>(panel,"outputPlan")),"preview refresh keeps job snapshot after midnight");Wait(panel,()=>Field<CheckBox>(panel,"landscape").Checked=true);Check(Field<OutputPlan>(panel,"outputPlan").PreparedAt==Night,"printer setting changes do not recalculate dates");
  Wait(panel,()=>Call(panel,"RefreshJobVariables"));Check(Field<OutputPlan>(panel,"outputPlan").PreparedAt==Morning&&Field<LabelSettings>(panel,"snapshot").Texts[0].Text.StartsWith("2026-11-14"),"explicit recalculation updates preview and output together");Check(template.Variables.Count==3&&template.Ean=="{serial}","recalculation preserves caller template");
  Set(panel,"jobClock",(Func<DateTime>)(()=>DateTime.MaxValue));Call(panel,"RefreshJobVariables");Check(!panel.Ready&&Field<PrintCanvas>(panel,"preview").Paper.IsEmpty,"failed recalculation cannot leave old job printable");panel.RefreshPreview();Check(!panel.Ready,"ordinary refresh cannot bypass recalculation failure");Set(panel,"jobClock",(Func<DateTime>)(()=>Morning));Wait(panel,()=>Call(panel,"RefreshJobVariables"));
  using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save(Path.Combine(dir,"prepared-job.png"));}form.Close();
 }}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Drafts();Jobs();Editor();Printing();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" draft-save, backup, failure preservation, strict output, variable preparation, midnight, fixed-date, batch consistency and UI recalculation checks. No physical printing.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
