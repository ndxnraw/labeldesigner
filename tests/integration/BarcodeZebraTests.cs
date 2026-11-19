using System;using System.IO;using System.Drawing;using System.Windows.Forms;using System.Reflection;using System.Diagnostics;using System.Collections.Generic;using EanEtikety;
class BarcodeZebraTests {
 sealed class TestQueue:IPrintQueueSource {
  public List<PrintQueueJob> Jobs=new List<PrintQueueJob>();
  public string Printer;
  public bool Fail;
  public List<PrintQueueJob> Read(string printer){Printer=printer;if(Fail)throw new IOException("Unavailable queue");return Jobs;}
 }
 static void QueueStatus(){
  var first=new PrintHistoryEntry{StartedUtc=DateTime.UtcNow,Printer="Test queue",Status=PrintHistoryStatus.Submitted,QueueToken=Guid.NewGuid().ToString("N")};
  var other=new PrintHistoryEntry{StartedUtc=first.StartedUtc,Printer=first.Printer,Status=first.Status,QueueToken=Guid.NewGuid().ToString("N")};
  var queue=new TestQueue();
  queue.Jobs.Add(new PrintQueueJob(PrintQueue.DocumentName(other.QueueToken),0x10));
  queue.Jobs.Add(new PrintQueueJob(PrintQueue.DocumentName(first.QueueToken),0x40|0x10));
  var seen=PrintQueue.Check(first,queue);
  Check(queue.Printer==first.Printer&&seen.Outcome==PrintQueueOutcome.Found&&seen.State==PrintQueueState.PaperOut,"same printer and exact job title, paper out takes priority");
  Check(PrintQueue.Check(other,queue).State==PrintQueueState.Printing,"concurrent job keeps its own state");
  var store=new PrintHistoryStore(Path.Combine(dir,"queue-history"));
  store.Record(first,null);store.Record(other,null);
  string entryPath=Path.Combine(dir,"queue-history",first.Id+".entry.xml");
  var originalTime=File.GetLastWriteTimeUtc(entryPath);
  seen.CheckedUtc=first.StartedUtc.AddSeconds(1);
  Check(store.UpdateQueue(first.Id,first.QueueToken,seen),"tracked job stores first observation");
  queue.Jobs.RemoveAt(1);
  var missing=PrintQueue.Check(first,queue);
  missing.CheckedUtc=first.StartedUtc.AddSeconds(2);
  Check(missing.Outcome==PrintQueueOutcome.Missing&&store.UpdateQueue(first.Id,first.QueueToken,missing),"vanished job records missing check");
  var saved=store.List().Find(item=>item.Id==first.Id).QueueStatus;
  Check(saved.LastCheck.Outcome==PrintQueueOutcome.Missing&&saved.LastKnown.State==PrintQueueState.PaperOut&&saved.LastKnown.CheckedUtc==seen.CheckedUtc,"missing job preserves last observed spooler state");
  Check(File.GetLastWriteTimeUtc(entryPath)==originalTime,"checking the queue cannot reorder print history");
  Check(!store.UpdateQueue(first.Id,other.QueueToken,missing)&&!store.UpdateQueue(first.Id,first.QueueToken,seen),"different job and stale results cannot overwrite history");
  queue.Fail=true;
  Check(PrintQueue.Check(first,queue).Outcome==PrintQueueOutcome.Unavailable,"inaccessible queue has a separate result");
  queue.Fail=false;
  queue.Jobs.Add(new PrintQueueJob(PrintQueue.DocumentName(other.QueueToken),0x80));
  Check(PrintQueue.Check(other,queue).Outcome==PrintQueueOutcome.Ambiguous,"multiple matches never identify a job");
  queue.Jobs.RemoveAt(0);
  Check(PrintQueue.Check(other,queue).State==PrintQueueState.HandedOff,"spooler printed status is not physical confirmation");
  string name=PrintQueue.DocumentName(other.QueueToken);
  queue.Jobs[0]=new PrintQueueJob(name,1,"  Driver\r\nreply\u0001 ");
  var driver=PrintQueue.Check(other,queue);
  Check(driver.State==PrintQueueState.DriverReported&&driver.ProviderText=="Driver  reply","provider wording takes precedence and is safe for local storage");
  queue.Jobs[0]=new PrintQueueJob(name,0x80000000);
  Check(PrintQueue.Check(other,queue).State==PrintQueueState.Unknown,"unclassified flags are not identified as waiting");
  var recent=new PrintQueueObservation{CheckedUtc=first.StartedUtc.AddSeconds(4),Outcome=PrintQueueOutcome.Unavailable};
  driver.CheckedUtc=first.StartedUtc.AddSeconds(3);
  Check(store.UpdateQueue(other.Id,other.QueueToken,recent)&&store.UpdateQueue(other.Id,other.QueueToken,driver),"late arrival can still fill last known observation");
  var concurrent=store.List().Find(item=>item.Id==other.Id).QueueStatus;
  Check(concurrent.LastCheck.Outcome==PrintQueueOutcome.Unavailable&&concurrent.LastKnown.ProviderText==driver.ProviderText,
   "later failed check and earlier known status both survive concurrent writers");
  Check(!PrintQueue.CanCheck(new PrintHistoryEntry{Status=PrintHistoryStatus.Submitted,Printer="Test queue"}),"legacy history has no correlatable identity");
  File.WriteAllText(Path.Combine(dir,"queue-history",other.Id+".queue.xml"),"<broken");
  Check(store.List().Exists(item=>item.Id==other.Id&&item.QueueStatus==null),"unreadable status cannot hide history entry");
  store.Remove(first.Id);
  Check(!store.UpdateQueue(first.Id,first.QueueToken,missing)&&!File.Exists(Path.Combine(dir,"queue-history",first.Id+".queue.xml")),"deleted entries never reappear and status file is cleaned up");
 }
 sealed class SlowQueue:IPrintQueueSource {
  public readonly System.Threading.ManualResetEventSlim Release=new System.Threading.ManualResetEventSlim();
  public int Calls;
  public List<PrintQueueJob> Read(string printer){System.Threading.Interlocked.Increment(ref Calls);Release.Wait();return new List<PrintQueueJob>();}
 }
 static void BoundedQueue(){
  var method=typeof(PrintQueue).GetMethod("CheckBounded",BindingFlags.Static|BindingFlags.NonPublic);
  var slow=new SlowQueue();
  var job=new PrintHistoryEntry{Printer="Slow",Status=PrintHistoryStatus.Submitted,QueueToken=Guid.NewGuid().ToString("N")};
  try{
   var watch=Stopwatch.StartNew();
   var result=(PrintQueueObservation)method.Invoke(null,new object[]{job,slow,TimeSpan.FromMilliseconds(120)});
   Check(result.Outcome==PrintQueueOutcome.Unavailable&&watch.ElapsedMilliseconds<3000,"stalled provider has a bounded wait");
   result=(PrintQueueObservation)method.Invoke(null,new object[]{job,slow,TimeSpan.FromMilliseconds(120)});
   Check(result.Outcome==PrintQueueOutcome.Unavailable&&slow.Calls<=1,"stalled provider cannot start another native lookup");
  }finally{slow.Release.Set();}
 }
 static Button QueueButton(Control control){
  var button=control as Button;
  if(button!=null&&button.Text==Loc.T("Skontrolovať front tlače"))return button;
  foreach(Control child in control.Controls){var found=QueueButton(child);if(found!=null)return found;}
  return null;
 }
 static void QueueHistoryUi(){
  string prefs=Path.Combine(dir,"queue-ui.xml");
  var store=new PrintHistoryStore(prefs+".print-history");
  var entry=new PrintHistoryEntry{Printer="Simulated",Media="Roll",StartedUtc=DateTime.UtcNow,Status=PrintHistoryStatus.Submitted,QueueToken=Guid.NewGuid().ToString("N")};
  store.Record(entry,null);
  var legacy=new PrintHistoryEntry{Printer="Legacy",StartedUtc=entry.StartedUtc,Status=PrintHistoryStatus.Submitted};
  store.Record(legacy,null);
  var fake=new TestQueue();
  fake.Jobs.Add(new PrintQueueJob(PrintQueue.DocumentName(entry.QueueToken),1));
  int lookups=0;Exception failure=null;bool visited=false;
  using(var owner=new MainForm(prefs))using(var timer=new Timer{Interval=35}){
   owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
   owner.GetType().GetField("historyQueueQuery",hidden).SetValue(owner,
    (Func<PrintHistoryEntry,PrintQueueObservation>)(job=>{lookups++;return PrintQueue.Check(job,fake);}));
   timer.Tick+=delegate{
    foreach(Form dialog in Application.OpenForms){
     if(dialog.Text!=Loc.T("História tlače"))continue;
     timer.Stop();
     try{
      var grid=(DataGridView)dialog.Controls.Find("printHistoryGrid",true)[0];
      var check=QueueButton(dialog);
      foreach(DataGridViewRow row in grid.Rows)if(((PrintHistoryEntry)row.Tag).Id==legacy.Id){grid.CurrentCell=row.Cells[0];break;}
      Check(!check.Enabled&&lookups==0,"opening history makes no lookup and a legacy entry cannot query a guessed job");
      foreach(DataGridViewRow row in grid.Rows)if(((PrintHistoryEntry)row.Tag).Id==entry.Id){grid.CurrentCell=row.Cells[0];break;}
      Check(check.Enabled,"submitted entry can query the queue");
      check.PerformClick();
      Check(lookups==1&&((PrintHistoryEntry)grid.CurrentRow.Tag).Id==entry.Id&&
       (string)grid.CurrentRow.Cells["Posledný známy stav"].Value==Loc.T("Pozastavené"),"on-demand check shows spooler state and retains selection");
      fake.Jobs.Clear();
      System.Threading.Thread.Sleep(20);
      check.PerformClick();
      Check(lookups==2&&((PrintHistoryEntry)grid.CurrentRow.Tag).QueueStatus.LastCheck.Outcome==PrintQueueOutcome.Missing&&
       (string)grid.CurrentRow.Cells["Posledný známy stav"].Value==Loc.T("Pozastavené"),"missing job is shown beside its previous state");
      visited=true;
     }catch(Exception ex){failure=ex;}finally{dialog.Close();}
     break;
    }
   };
   timer.Start();Call(owner,"ShowPrintHistory");owner.Close();
  }
  if(failure!=null)throw failure;
  Check(visited,"real history dialog tested with simulated Windows queue");
 }
 static void ReadabilityPreview(){
  using(var form=new Form{ClientSize=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
  using(var panel=new PrintPanel(LabelSettings.Default())){
   form.Controls.Add(panel);
   Wait(panel,()=>{form.Show();form.Hide();form.Show();panel.SelectPrinter("Microsoft Print to PDF");});
   var report=F<PreflightReport>(panel,"currentReport");
   Check(panel.Ready&&report.Context.Contains("|readability:"),"readability runs through the print preview");
   F<ModernTabs>(panel,"printSections").SelectedIndex=3;
   Check(F<Button>(panel,"readabilityButton").Enabled,"readability report is available in Details");
   bool seen=false;
   using(var timer=new System.Windows.Forms.Timer{Interval=40}){
    timer.Tick+=delegate{foreach(Form popup in Application.OpenForms)if(popup!=form){
     seen=popup.Text==Loc.T("Čitateľnosť kódov…");
     timer.Stop();popup.DialogResult=DialogResult.OK;popup.Close();break;
    }};
    timer.Start();Call(panel,"ShowReadability");
   }
   var canvas=F<PrintCanvas>(panel,"preview");
   Check(seen&&!F<RectangleF>(canvas,"HighlightedCode").IsEmpty&&F<int>(canvas,"HighlightedPage")==0,
    "selecting a report row highlights that barcode in the print preview");
   form.Close();
  }
 }
 static void ReplayPreview(){
  const string printer="Microsoft Print to PDF";
  var label=LabelSettings.Default();label.Copies=2;
  var profile=new PrinterProfile{Printer=printer,Name="Saved media",Paper="Saved paper",Width=590,Height=820,ZebraZpl=true,Darkness=8.5m,X=1.25};
  var archive=ArchivedPrintJob.Capture(label,OutputPlan.Create(label),profile,3);
  var plan=archive.Select("2",false);
  using(var form=new Form{ClientSize=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
  using(var panel=new PrintPanel(archive.Format,plan)){
   form.Controls.Add(panel);
   typeof(PrintPanel).GetMethod("RestorePrintSettings",hidden).Invoke(panel,new object[]{archive});
   int sent=0;
   typeof(PrintPanel).GetField("zebraSender",hidden).SetValue(panel,(Action<LabelPrintDocument,PrinterPageGeometry,ZebraSettings,int>)((d,g,s,c)=>sent++));
   Wait(panel,()=>{form.Show();form.Hide();form.Show();});
   Check(panel.Ready&&sent==0&&F<PreflightReport>(panel,"currentReport").Plan.PageCount==1,"replay creates a new preflight without sending");
   Check(F<NumericUpDown>(panel,"copies").Value==3&&F<NumericUpDown>(panel,"offsetX").Value==1.25m
    &&F<NumericUpDown>(panel,"zebraDarkness").Value==8.5m,"replay restores saved repeats, calibration and darkness");
   bool reviewed=false,editAction=false;
   using(var timer=new System.Windows.Forms.Timer{Interval=40}){
    timer.Tick+=delegate{foreach(Form window in Application.OpenForms)if(window is OutputReviewDialog){
     reviewed=true;foreach(Control root in window.Controls)foreach(Control row in root.Controls)if(row is FlowLayoutPanel)foreach(Control action in row.Controls)editAction|=action.Text==Loc.T("Späť na úpravy");timer.Stop();window.DialogResult=DialogResult.Cancel;window.Close();break;
    }};
    timer.Start();Call(panel,"Submit");
   }
   Check(reviewed&&!editAction&&sent==0,"replay requires confirmation and cancel sends nothing");
   var history=new PrintHistoryStore(Path.Combine(dir,"missing-printer-history"));
   typeof(PrintPanel).GetMethod("UsePrintHistory",hidden).Invoke(panel,new object[]{history});
   var unavailable=new System.Drawing.Printing.PrinterSettings{PrinterName="Unavailable-"+Guid.NewGuid()};
   typeof(PrintPanel).GetField("selectedPrinter",hidden).SetValue(panel,unavailable);
   Wait(panel,()=>Call(panel,"StartRequest",false,true,null));
   Check(sent==0&&history.List().Count==1&&history.List()[0].Status==PrintHistoryStatus.Failed,"printer loss after preview is logged without dispatch");
   form.Close();
  }
 }
 static void HistoryArchive(){
  string path=Path.Combine(dir,"history-archive");
  var store=new PrintHistoryStore(path);
  var label=LabelSettings.Default();var second=ProjectXml.Clone(label);second.Texts[0].Text="Retained value";
  var plan=new OutputPlan{Width=120,Height=70,RecordCount=2};
  var bounds=new RectangleF(60,0,60,45);
  plan.Placements.Add(new OutputPlacement{Page=0,Record=3,Label=label,Bounds=new RectangleF(0,0,60,45)});
  plan.Placements.Add(new OutputPlacement{Page=0,Record=7,Label=second,Bounds=bounds});
  plan.Placements.Add(new OutputPlacement{Page=1,Record=7,Label=second,Bounds=bounds});
  var profile=new PrinterProfile{Printer="Test printer",Name="Media A",Width=472,Height=276,ZebraZpl=true,Darkness=12.5m};
  var entry=new PrintHistoryEntry{StartedUtc=DateTime.UtcNow,Printer=profile.Printer,Media=profile.Name,Status=PrintHistoryStatus.Submitted,Pages=2,Labels=3,Repetitions=2};
  Check(store.Record(entry,null)&&!store.List()[0].HasArchive&&Directory.GetFiles(path,"*.job.xml").Length==0,"metadata-only history keeps no content");
  Check(store.Record(entry,ArchivedPrintJob.Capture(label,plan,profile,2)),"opt-in archive write succeeds");
  string interrupted=Path.Combine(path,entry.Id+".job.xml."+Guid.NewGuid().ToString("N")+".tmp");
  File.WriteAllText(interrupted,"Interrupted snapshot");
  Check(new PrintHistoryStore(path).List().Exists(item=>item.Id==entry.Id&&item.HasArchive)
   &&!File.Exists(interrupted),"interrupted archive scratch removed when history reopens");
  second.Texts[0].Text="Edited later";
  var archived=store.Open(entry.Id);
  Check(archived.Labels.Count==2&&archived.Profile.Darkness==12.5m&&archived.Repetitions==2,"archive deduplicates labels and retains device options");
  var selected=archived.Select("7",true);
  Check(selected.RecordCount==1&&selected.PageCount==2&&selected.Placements.Count==2,"record selection spans original pages");
  Check(selected.Placements[0].Bounds==bounds&&selected.Placements[0].Label.Texts[0].Text=="Retained value","replay freezes contents and sheet positions");
  var page=archived.Select("2",false);
  Check(page.PageCount==1&&page.Placements[0].Page==0&&page.Placements[0].Record==7,"selected sheet remapped to first preview page");
  Reject(()=>archived.Select("4",true),"absent source row rejected");
  Reject(()=>archived.Select("2-1",false),"backward range rejected");
  store.Remove(entry.Id);
  Check(store.List().Count==1&&Directory.GetFiles(path,"*.job.xml").Length==0,"removal deletes saved artwork too");
 }
 static int checks;static string dir;static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
 static void Reject(Action action,string why){try{action();}catch{checks++;return;}throw new Exception("Expected rejection: "+why);}
 static void Barcodes(){Check(LabelSettings.Default().DigitSize==12,"new caption default 12pt");foreach(double w in new[]{10d,20,30,40,60}){var s=StartupLayout.Create(new StartupProfile{Width=w,Height=45});Check(s.DigitSize>=8,"startup must not shrink caption to 4pt");Engine.Validate(s);}var legacy=ProjectXml.Decode<LabelSettings>("<LabelSettings><DigitSize>5.5</DigitSize></LabelSettings>");Check(legacy.DigitSize==5.5,"stored caption size preserved");Check(ProjectXml.Decode<LabelSettings>("<LabelSettings/>").DigitSize==8,"missing legacy caption size preserved");
  Check(MoreBarcodes.Name(14)=="ITF-14","existing kind clearly ITF14");Check(Array.IndexOf(MoreBarcodes.Kinds,114)>=0,"separate GTIN14 kind");string gtin=Engine.Normalize("1234567890123",14);Check(Engine.Normalize("1234567890123",114)==gtin,"GTIN14 checksum completion");Reject(()=>Engine.Normalize(gtin.Substring(0,13)+(gtin[13]=='0'?"1":"0"),114),"wrong GTIN14 check digit");var s14=LabelSettings.Default();s14.Kind=114;s14.Ean=gtin;s14.BarWidth=60;s14.Width=90;Check(Engine.Caption(s14)=="(01)"+gtin,"GS1 human text includes AI");Check(Engine.CodeBits(s14)==MoreBarcodes.Gs1Bits("01"+gtin),"FNC1 and AI encoded");Check(Engine.CodeBits(s14)!=Engine.Bits(gtin,14),"ITF and GS1-128 distinct");var old=ProjectXml.Clone(s14);old.Kind=14;Check(Engine.ExtraHeight(old,60,10)>Engine.ExtraHeight(s14,60,10),"bearer bars only for ITF14");using(var b=Engine.Render(s14)){b.Save(Path.Combine(dir,"gtin14-gs1-128.png"));var reader=new ZXing.BarcodeReader();reader.Options.AssumeGS1=true;var decoded=reader.Decode(b);Check(decoded!=null&&decoded.BarcodeFormat==ZXing.BarcodeFormat.CODE_128&&decoded.Text.StartsWith("]C101"+gtin),"render decodes as GS1 Code128");}Check(ProjectXml.Decode<LabelSettings>(ProjectXml.Encode(s14)).Kind==114,"new kind XML round trip");
 }
 static void Zebra(){var type=typeof(MainForm).Assembly.GetType("EanEtikety.ZebraSettings");Check(type!=null,"Zebra settings exist");var options=Activator.CreateInstance(type);type.GetField("Enabled").SetValue(options,true);type.GetField("Dpi").SetValue(options,203.2);type.GetField("Darkness").SetValue(options,(decimal?)10.5m);var label=LabelSettings.Default();label.Copies=2;var geometry=new PrinterPageGeometry(new Rectangle(0,0,237,178),new RectangleF(0,0,237,178),0,0,false);var render=typeof(MainForm).Assembly.GetType("EanEtikety.ZebraPrint").GetMethod("Render");using(var doc=new LabelPrintDocument(label))using(var output=new MemoryStream()){render.Invoke(null,new object[]{doc,geometry,options,2,output});string text=System.Text.Encoding.ASCII.GetString(output.ToArray());Check(text.StartsWith("~SD10.5\n"),"absolute invariant darkness");Check(text.Contains("^XA\n^MUD,300,300\n^PW"),"dot units and inherited conversion reset before dimensions");Check(text.Contains("^MD0\n"),"reset relative adjustment when absolute darkness requested");Check(text.Contains("^PW482\n^LL362\n"),"ZPL physical dimensions at selected DPI");Check(text.Split(new[]{"^XA"},StringSplitOptions.None).Length-1==4,"whole job repetitions");Check(text.Contains("^GFA,")&&!text.Contains("^PQ2"),"raster pages with explicit repetition order");File.WriteAllText(Path.Combine(dir,"zebra.zpl"),text);}
  type.GetField("Darkness").SetValue(options,(decimal?)30.1m);Reject(()=>type.GetMethod("Validate").Invoke(options,null),"darkness upper bound");type.GetField("Darkness").SetValue(options,(decimal?)(-0.1m));Reject(()=>type.GetMethod("Validate").Invoke(options,null),"darkness lower bound");
 }
 sealed class FakePrinter:IRawPrinter {public FakeJob Job=new FakeJob();public int Opened;public string Title;public IRawPrintJob Open(string printer,string title){Opened++;Title=title;return Job;}}
 sealed class FakeJob:IRawPrintJob {public MemoryStream Bytes=new MemoryStream();public bool Completed,Disposed,Fail,Zero;public Action OnWrite;public int Write(byte[] b,int o,int n){if(OnWrite!=null)OnWrite();if(Fail)throw new IOException("injected");if(Zero)return 0;int count=Math.Min(7,n);Bytes.Write(b,o,count);return count;}public void Complete(){Completed=true;}public void Dispose(){Disposed=true;}}
 static void Spooling(){var repeated=typeof(ZebraPrint).GetMethod("SendFile",new[]{typeof(string),typeof(string),typeof(IRawPrinter),typeof(int)});Check(repeated!=null,"whole-job repetitions stream one prepared file");string path=Path.Combine(dir,"raw.txt");File.WriteAllText(path,"^XA\n^FO1,1^FDtest^FS^XZ\n");var fake=new FakePrinter();ZebraPrint.SendFile("fake",path,fake);Check(fake.Opened==1&&fake.Job.Completed&&fake.Job.Disposed,"one completed/disposed job");Check(System.Text.Encoding.ASCII.GetString(fake.Job.Bytes.ToArray())==File.ReadAllText(path),"partial writes preserve exact data");fake=new FakePrinter();repeated.Invoke(null,new object[]{"fake",path,fake,2});Check(fake.Opened==1&&System.Text.Encoding.ASCII.GetString(fake.Job.Bytes.ToArray())==File.ReadAllText(path)+File.ReadAllText(path),"whole-job repetition order uses one spool job");fake=new FakePrinter();fake.Job.Zero=true;Reject(()=>ZebraPrint.SendFile("fake",path,fake),"zero write");Check(!fake.Job.Completed&&fake.Job.Disposed,"zero write aborted by disposal");fake=new FakePrinter();fake.Job.Fail=true;Reject(()=>ZebraPrint.SendFile("fake",path,fake),"write failure");Check(fake.Job.Disposed&&!fake.Job.Completed,"failure disposes uncommitted job");using(var context=new CancelScope()){fake=new FakePrinter();fake.Job.OnWrite=context.Context.Cancel;Reject(()=>ZebraPrint.SendFile("fake",path,fake),"cancellation");Check(fake.Job.Disposed&&!fake.Job.Completed,"cancel disposes uncommitted job");}}
 sealed class CancelScope:IDisposable {public WorkContext Context=new WorkContext();IDisposable scope;public CancelScope(){scope=Context.Enter();}public void Dispose(){scope.Dispose();}}
 static string JobText(LabelPrintDocument doc,PrinterPageGeometry geometry,ZebraSettings settings){using(var output=new MemoryStream()){ZebraPrint.Render(doc,geometry,settings,1,output);return System.Text.Encoding.ASCII.GetString(output.ToArray());}}
 static void Translations(){foreach(string lang in Loc.Codes){Loc.Language=lang;using(var panel=new PrintPanel(LabelSettings.Default())){Check(F<CheckBox>(panel,"zebraZpl").AccessibleName==Loc.T("Priama tlač Zebra (ZPL)"),"localized mode label "+lang);Check(F<NumericUpDown>(panel,"zebraDpi").AccessibleName==Loc.T("Rozlíšenie ZPL tlačiarne (DPI)"),"localized DPI label "+lang);Check(lang=="sk"||Loc.T("Nastaviť tmavosť (darkness)")!="Nastaviť tmavosť (darkness)","darkness translated "+lang);Check(!F<CheckBox>(panel,"zebraZpl").Checked&&!F<CheckBox>(panel,"zebraDarknessOverride").Checked,"new print panel preserves Windows/default heat");}}Loc.Language="sk";}
 static void MoreZebra(){var label=LabelSettings.Default();var geometry=new PrinterPageGeometry(new Rectangle(0,0,237,178),new RectangleF(0,0,237,178),4,6,false);var settings=new ZebraSettings{Enabled=true,Dpi=203.2};using(var doc=new LabelPrintDocument(label)){string plain=JobText(doc,geometry,settings);Check(!plain.Contains("~SD")&&!plain.Contains("^MD"),"printer darkness unchanged by default");settings.Darkness=0;string zero=JobText(doc,geometry,settings);Check(zero.StartsWith("~SD0.0\n"),"zero is an explicit darkness");settings.Darkness=10.5m;var culture=System.Threading.Thread.CurrentThread.CurrentCulture;try{System.Threading.Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("sk-SK");Check(JobText(doc,geometry,settings).StartsWith("~SD10.5\n"),"comma culture produces ZPL decimal point");}finally{System.Threading.Thread.CurrentThread.CurrentCulture=culture;}
  string before=JobText(doc,geometry,settings);settings.Darkness=20;string after=JobText(doc,geometry,settings);int start=before.IndexOf("^FO"),end=before.IndexOf("^PQ");Check(before.Substring(start,end-start)==after.Substring(after.IndexOf("^FO"),after.IndexOf("^PQ")-after.IndexOf("^FO")),"darkness changes heat setting, not raster pixels");doc.OffsetX=1.5;doc.ScaleX=1.05;Check(JobText(doc,geometry,settings)!=after,"calibration affects raster");var diagnostics=PrintDiagnostics.Capture(doc,geometry,2,settings);Check(diagnostics.Language=="ZPL"&&diagnostics.OutputDpi==203.2&&diagnostics.Darkness==20,"diagnostics retain applied output settings");Check(diagnostics.Format().Contains("Darkness=20.0"),"diagnostics invariant darkness");
  var fake=new FakePrinter();settings.Darkness=31;Reject(()=>ZebraPrint.Send(doc,geometry,settings,1,fake),"invalid settings before printer open");Check(fake.Opened==0,"invalid job never opens printer");settings.Darkness=null;Reject(()=>ZebraPrint.Render(doc,new PrinterPageGeometry(new Rectangle(0,0,100000,100000),geometry.PrintableArea,0,0,false),settings,1,new MemoryStream()),"oversized raster rejected");settings.Dpi=double.NaN;Reject(settings.Validate,"NaN DPI");settings.Dpi=203.2;settings.Darkness=10.55m;Reject(settings.Validate,"unsupported darkness precision");settings.Darkness=null;using(var context=new CancelScope()){context.Context.Cancel();fake=new FakePrinter();Reject(()=>ZebraPrint.Send(doc,geometry,settings,1,fake),"pre-cancelled preparation");Check(fake.Opened==0,"cancelled preparation never opens printer");}
  using(var identified=new LabelPrintDocument(label)){identified.DocumentName=PrintQueue.DocumentName(Guid.NewGuid().ToString("N"));var named=new FakePrinter();ZebraPrint.Send(identified,geometry,new ZebraSettings{Enabled=true},1,named);Check(named.Title==identified.DocumentName,"RAW transport receives the exact document title");}var small=StartupLayout.Create(new StartupProfile{Width=40,Height=45});Check(Engine.CaptionFits(small),"startup widens code to fit readable caption where possible");}
  string file=Path.Combine(dir,"large-raw.tmp");using(var output=File.Create(file))output.SetLength(100L*1024*1024);try{var fake=new FakePrinter();Reject(()=>ZebraPrint.SendFile("fake",file,fake,3),"bounded repeated spool size");Check(fake.Opened==0,"oversized repeated job rejected before spool");}finally{File.Delete(file);}
 }
 static void Profiles(){Check(typeof(PrintDiagnostics).GetField("Language")!=null,"diagnostics identify actual output language");Check(typeof(PrinterProfile).GetField("ZebraZpl")!=null,"profile supports Zebra settings");var old=ProjectXml.Decode<PrinterProfile>("<PrinterProfile><Printer>fake</Printer><Width>237</Width><Height>178</Height></PrinterProfile>");Check(!(bool)typeof(PrinterProfile).GetField("ZebraZpl").GetValue(old),"legacy profile uses Windows");Check(typeof(PrinterProfile).GetField("Darkness").GetValue(old)==null,"legacy profile leaves darkness unchanged");var store=new PrinterProfileStore(Path.Combine(dir,"profiles.xml"));old.Name="A";typeof(PrinterProfile).GetField("ZebraZpl").SetValue(old,true);typeof(PrinterProfile).GetField("Darkness").SetValue(old,(decimal?)14.5m);store.Save(old);var b=old.Copy();b.Name="B";typeof(PrinterProfile).GetField("Darkness").SetValue(b,(decimal?)8m);store.Save(b);store=new PrinterProfileStore(Path.Combine(dir,"profiles.xml"));Check((decimal)typeof(PrinterProfile).GetField("Darkness").GetValue(store.Find("fake","A"))==14.5m,"per-medium darkness A");Check((decimal)typeof(PrinterProfile).GetField("Darkness").GetValue(store.Find("fake","B"))==8m,"per-medium darkness B");typeof(PrinterProfile).GetField("Darkness").SetValue(b,(decimal?)31m);Reject(()=>store.Save(b),"invalid profile darkness");}
 static readonly BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;static T F<T>(object o,string n){return (T)o.GetType().GetField(n,hidden).GetValue(o);}static object Call(object o,string n,params object[] args){foreach(var method in o.GetType().GetMethods(hidden))if(method.Name==n&&method.GetParameters().Length==args.Length)return method.Invoke(o,args);throw new MissingMethodException(n);}
 static void Wait(PrintPanel p,Action action){bool done=false;EventHandler ready=delegate{done=true;};p.PreviewReady+=ready;try{action();var watch=Stopwatch.StartNew();while(!done){Application.DoEvents();System.Threading.Thread.Sleep(2);if(watch.ElapsedMilliseconds>30000)throw new Exception("Preview timeout: "+F<Label>(p,"info").Text);}}finally{p.PreviewReady-=ready;}}
 static void Ui(){using(var form=new Form{ClientSize=new Size(1050,760)})using(var panel=new PrintPanel(LabelSettings.Default())){form.Controls.Add(panel);var history=new PrintHistoryStore(Path.Combine(dir,"sending-history"));typeof(PrintPanel).GetMethod("UsePrintHistory",hidden).Invoke(panel,new object[]{history});panel.LoadProfiles(Path.Combine(dir,"ui-profiles.xml"));form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();form.Hide();form.Show();Wait(panel,()=>panel.SelectPrinter("Microsoft Print to PDF"));Check(panel.Ready,"PDF preview ready");Check(F<System.Drawing.Printing.PrinterSettings>(panel,"selectedPrinter").PrinterName=="Microsoft Print to PDF","requested printer retained before discovery completes");var initial=F<PreflightReport>(panel,"currentReport").Fingerprint;Wait(panel,()=>{F<CheckBox>(panel,"zebraZpl").Checked=true;F<CheckBox>(panel,"zebraDarknessOverride").Checked=true;F<NumericUpDown>(panel,"zebraDarkness").Value=10.5m;});var report=F<PreflightReport>(panel,"currentReport");Check(report.Context.Contains("ZPL|"),"ZPL included in approval context");Check(report.Fingerprint!=initial,"mode/darkness invalidates consent");int submissions=0;ZebraSettings sent=null;string sentTitle=null;typeof(PrintPanel).GetField("zebraSender",hidden).SetValue(panel,(Action<LabelPrintDocument,PrinterPageGeometry,ZebraSettings,int>)((d,g,s,c)=>{submissions++;sent=s.Copy();sentTitle=d.DocumentName;}));typeof(PrintPanel).GetField("approvedFingerprint",hidden).SetValue(panel,initial);Wait(panel,()=>Call(panel,"StartRequest",false,true,null));Check(submissions==0&&history.List().Count==0,"stale approval prevents RAW submission and history");F<CheckBox>(panel,"archivePrint").Checked=true;typeof(PrintPanel).GetField("approvedFingerprint",hidden).SetValue(panel,F<PreflightReport>(panel,"currentReport").Fingerprint);Wait(panel,()=>Call(panel,"StartRequest",false,true,null));Check(submissions==1&&sent.Darkness==10.5m,"approved job uses captured darkness");var entry=history.List()[0];Check(entry.Status==PrintHistoryStatus.Submitted&&entry.HasArchive,"actual approved send recorded and archived");Check(PrintQueue.CanCheck(entry)&&sentTitle==PrintQueue.DocumentName(entry.QueueToken),"approved transport and history share a job title");Check(history.Open(entry.Id).Profile.Darkness==10.5m,"archived settings match the send");F<CheckBox>(panel,"archivePrint").Checked=false;typeof(PrintPanel).GetField("zebraSender",hidden).SetValue(panel,(Action<LabelPrintDocument,PrinterPageGeometry,ZebraSettings,int>)((d,g,s,c)=>{throw new IOException("Injected send failure");}));Wait(panel,()=>Call(panel,"StartRequest",false,true,null));Check(history.List().Exists(item=>item.Status==PrintHistoryStatus.Failed&&!item.HasArchive),"failed send retains metadata only");Wait(panel,panel.RefreshPreview);typeof(PrintPanel).GetField("zebraSender",hidden).SetValue(panel,(Action<LabelPrintDocument,PrinterPageGeometry,ZebraSettings,int>)((d,g,s,c)=>{throw new OperationCanceledException();}));Wait(panel,()=>Call(panel,"StartRequest",false,true,null));Check(history.List().Exists(item=>item.Status==PrintHistoryStatus.Cancelled&&!item.HasArchive),"cancelled send is distinguished from a failed send");F<TextBox>(panel,"profileName").Text="Media A";Call(panel,"SaveMediaProfile");var saved=new PrinterProfileStore(Path.Combine(dir,"ui-profiles.xml")).Find("Microsoft Print to PDF","Media A");Check(saved.ZebraZpl&&saved.Darkness==10.5m,"real profile UI saves darkness");Wait(panel,()=>{F<NumericUpDown>(panel,"zebraDarkness").Value=8m;F<NumericUpDown>(panel,"zebraDpi").Value=203.2m;});F<TextBox>(panel,"profileName").Text="Media B";Call(panel,"SaveMediaProfile");Wait(panel,()=>{var list=F<ModernComboBox>(panel,"mediaProfiles");foreach(PrinterProfile profile in list.Items)if(profile.DisplayName=="Media A"){list.SelectedItem=profile;break;}});Check(F<NumericUpDown>(panel,"zebraDarkness").Value==10.5m&&F<NumericUpDown>(panel,"zebraDpi").Value==300,"profile switch restores independent Zebra settings");var baseline=(PrinterProfile)Call(panel,"CurrentMediaProfile");using(var target=(PrintPanel)Call(panel,"CreateCalibrationPanel",CalibrationTarget.Create(60,45),baseline)){Check(F<CheckBox>(target,"zebraZpl").Checked&&!F<CheckBox>(target,"zebraZpl").Enabled,"calibration target retains and locks transport");Check(F<NumericUpDown>(target,"zebraDarkness").Value==10.5m&&!F<NumericUpDown>(target,"zebraDarkness").Enabled,"calibration target retains and locks darkness");}form.Close();}}
 [STAThread]static void Main(string[] args){dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Barcodes();Zebra();Spooling();Profiles();MoreZebra();Translations();QueueStatus();BoundedQueue();QueueHistoryUi();HistoryArchive();Ui();ReplayPreview();ReadabilityPreview();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" barcode/Zebra checks; no physical print job");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
