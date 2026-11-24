using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Threading;
using System.Windows.Forms;
namespace EanEtikety {
// One STA worker for the process. A stalled driver cannot create an unbounded number
// of threads. A new request replaces pending work; running work is never aborted.
internal static class PrintWorker {
 static readonly object gate=new object();static Action pending;static object owner;
 static readonly AutoResetEvent wake=new AutoResetEvent(false);
 static PrintWorker(){var thread=new Thread(Run){IsBackground=true,Name="ND print worker"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
 public static void Queue(object key,Action action){lock(gate){owner=key;pending=action;}wake.Set();}
 public static void Cancel(object key){lock(gate){if(owner==key){pending=null;owner=null;}}}
 static void Run(){while(true){wake.WaitOne();Action action;lock(gate){action=pending;pending=null;owner=null;}if(action!=null)try{action();}catch(Exception ex){System.Diagnostics.Trace.WriteLine(ex);}}}
}
public class PrintCanvas:ScrollableControl {
 Image image;internal RectangleF HighlightedCode;internal int HighlightedPage=-1;public double Zoom;public int Page;[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]public SizeF Paper{get;set;}[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]public RectangleF Printable{get;set;}
 public PrintCanvas(){DoubleBuffered=true;AutoScroll=true;TabStop=true;Resize+=delegate{Invalidate();};}
 public void SetPage(Image next,SizeF paper,RectangleF printable){var old=image;image=next;Paper=paper;Printable=printable;if(old!=null)old.Dispose();Invalidate();}
 public void ClearPages(){SetPage(null,SizeF.Empty,RectangleF.Empty);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(image==null||Paper.Width<=0||Paper.Height<=0)return;
  double scale=Zoom==0?Math.Max(.01,Math.Min((ClientSize.Width-48)/Paper.Width,(ClientSize.Height-48)/Paper.Height)):Zoom*e.Graphics.DpiX/25.4;
  int w=Math.Max(1,(int)Math.Round(Paper.Width*scale)),h=Math.Max(1,(int)Math.Round(Paper.Height*scale));var extent=Zoom==0?Size.Empty:new Size(w+48,h+48);if(AutoScrollMinSize!=extent)AutoScrollMinSize=extent;
  float x=Math.Max(24,(ClientSize.Width-w)/2)+AutoScrollPosition.X,y=Math.Max(24,(ClientSize.Height-h)/2)+AutoScrollPosition.Y;e.Graphics.FillRectangle(Brushes.White,x,y,w,h);e.Graphics.DrawImage(image,new RectangleF(x,y,w,h));
  if(Page==HighlightedPage&&!HighlightedCode.IsEmpty){
   var code=HighlightedCode;
   using(var pen=new Pen(SystemInformation.HighContrast?SystemColors.Highlight:Color.DeepSkyBlue,3))
    e.Graphics.DrawRectangle(pen,x+code.X*w/Paper.Width,y+code.Y*h/Paper.Height,code.Width*w/Paper.Width,code.Height*h/Paper.Height);
  }
  if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,ClientSize.Width-6,ClientSize.Height-6));
 }
 protected override void Dispose(bool disposing){if(disposing)ClearPages();base.Dispose(disposing);}
}
internal sealed class PrintResult:IDisposable {
 public ZebraSettings Zebra;public PreviewTrace Trace;public PrintDiagnostics Diagnostics;public PreflightReport Report;public bool RequiresReview;public string ProfileWarning;public PrinterProfile Profile;public bool IgnoreDriverMargins;public double OffsetX,OffsetY,ScaleX=1,ScaleY=1;public Image Image;public SizeF Paper;public RectangleF Printable;public string[] Printers,Warnings;public PaperSize[] Papers;public PrinterSettings Printer;public PageSettings Settings;public int Count;public string Error;public bool Sent,Cancelled;public string HistoryWarning;public PrintPanel.ReadabilityRun Readability;
 public void Dispose(){if(Image!=null){Image.Dispose();Image=null;}}
}
public partial class PrintPanel:UserControl {
 bool calibrationTargetMode;TableLayoutPanel advancedFields;ModernButton driverProperties;PreflightReport currentReport;string approvedFingerprint;readonly Label jobSummary=new Label();
 string initialPrinterName;OutputPlan outputPlan;LabelSettings snapshot;readonly LabelSettings sourceTemplate;readonly PrintCanvas preview=new PrintCanvas();
 readonly ModernComboBox printers=new ModernComboBox(),zoom=new ModernComboBox(),papers=new ModernComboBox();
 readonly NumericUpDown copies=new ClickWheelNumericUpDown(),page=new ClickWheelNumericUpDown();readonly CheckBox landscape=new CheckBox();
 readonly ModernButton print=new ModernButton();ModernButton printBack;readonly Label info=new Label(),paperInfo=new Label(),printerName=new Label(),pageTotal=new Label();readonly ToolTip printerTip=new UiToolTip();
 readonly FlowLayoutPanel pageTools=new FlowLayoutPanel();readonly System.Windows.Forms.Timer debounce=new System.Windows.Forms.Timer{Interval=200};
 readonly object deliveryGate=new object();PrintResult delivery;int version;bool updating,closed,started,sending,busy;PrinterSettings selectedPrinter;PageSettings selectedPage;string[] warnings=new string[0];PaperSize[] knownPapers;
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]public PreviewPerformance LastPreviewPerformance{get;private set;}
 public bool Ready{get{return print.Enabled;}}public int PreviewBuilds{get;private set;}public event EventHandler PreviewReady;public event EventHandler BackRequested;public event Action<LabelIssue> EditIssueRequested;
 public PrintPanel(LabelSettings settings):this(settings,null){}
 public PrintPanel(LabelSettings settings,OutputPlan plan):this(settings,plan,false){}
 internal PrintPanel(LabelSettings settings,OutputPlan plan,bool takeOwnership){ownsPreparedPlan=plan==null||takeOwnership;sourceTemplate=CloneLabel(settings);if(plan==null){var job=new PreparedLabelJob(sourceTemplate,jobClock());outputPlan=job.Plan;snapshot=CloneLabel(job.Label);}else{outputPlan=plan;snapshot=CloneLabel(settings);}Dock=DockStyle.Fill;Font=UiFonts.Get(10.5f);Padding=new Padding(14);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(layout);
  layout.Controls.Add(new Label{Text="ND LabelDesigner  /  "+Loc.T("Tlačiť"),Font=new Font("Segoe UI",14,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
  var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,400));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.Controls.Add(body,0,1);
  var side=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0,0,12,0)};side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.AutoSize));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(side,0,0);var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(8)};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var commonScroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=new Padding(0)};commonScroll.Controls.Add(fields);side.Controls.Add(commonScroll,0,0);
  Add(fields,new Label{Text=Loc.T("Tlačiareň"),AutoSize=true});printers.DropDownStyle=ComboBoxStyle.DropDownList;printers.AccessibleName=Loc.T("Tlačiareň");Add(fields,printers);printerName.AutoSize=true;printerName.MaximumSize=new Size(270,0);printerName.UseMnemonic=false;Add(fields,printerName);
  printers.DropDown+=delegate{int width=printers.Width;foreach(var item in printers.Items)width=Math.Max(width,TextRenderer.MeasureText(Convert.ToString(item),printers.Font).Width+40);printers.DropDownWidth=Math.Min(width,Screen.FromControl(this).WorkingArea.Width-40);};
  Add(fields,Button(Loc.T("Obnoviť tlačiarne"),delegate{StartRequest(true,false);}));Add(fields,new Label{Text=Loc.T("Opakovania celej úlohy"),AutoSize=true});copies.Minimum=1;copies.Maximum=32767;copies.Value=1;copies.AccessibleName=Loc.T("Opakovania celej úlohy");Add(fields,copies);copies.AccessibleDescription=Loc.T("Opakovania celej úlohy");copies.ValueChanged+=delegate{UpdateOutputSummary();};jobSummary.AutoSize=true;jobSummary.MaximumSize=new Size(270,0);jobSummary.UseMnemonic=false;jobSummary.AccessibleName=Loc.T("Súhrn výstupu");UpdateOutputSummary();
  Add(fields,new Label{Text=Loc.T("Etiketa")+": "+snapshot.Width.ToString("0.##")+" × "+snapshot.Height.ToString("0.##")+" mm",AutoSize=true,Padding=new Padding(0,6,0,6)});
  OrganizePrintSettings(side);FitCommonSettings(side,fields);
  var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(right,1,0);var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(4)};right.Controls.Add(tools,0,0);
  zoom.Width=140;zoom.DropDownStyle=ComboBoxStyle.DropDownList;zoom.Items.AddRange(new object[]{Loc.T("Celá strana"),"50 %","100 %","150 %","200 %","400 %"});zoom.SelectedIndex=0;zoom.SelectedIndexChanged+=delegate{preview.Zoom=zoom.SelectedIndex==0?0:new[]{.5,1,1.5,2,4}[zoom.SelectedIndex-1];preview.AutoScrollPosition=Point.Empty;preview.Invalidate();};tools.Controls.Add(zoom);
  pageTools.AutoSize=true;pageTools.Visible=false;pageTools.Controls.Add(new Label{Text=Loc.T("Strana:"),AutoSize=true});page.Minimum=page.Maximum=1;page.Width=65;pageTools.Controls.Add(page);pageTotal.AutoSize=true;pageTools.Controls.Add(pageTotal);tools.Controls.Add(pageTools);var jobFields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(8,0,8,4),MaximumSize=new Size(0,150),AutoScroll=true};jobFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));jobSummary.MaximumSize=new Size(0,0);Add(jobFields,jobSummary);InitializeJobInfo(jobFields,plan!=null);Add(printDetails,jobInfo);jobFields.SizeChanged+=delegate{LayoutPrintSummary();};jobFields.VisibleChanged+=delegate{LayoutPrintSummary();};jobSummary.FontChanged+=delegate{LayoutPrintSummary();};jobSummary.SizeChanged+=delegate{LayoutPrintSummary();};jobInfo.TextChanged+=delegate{LayoutPrintSummary();};right.Controls.Add(jobFields,0,1);preview.Dock=DockStyle.Fill;right.Controls.Add(preview,0,2);
  var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,AutoSize=true,MinimumSize=new Size(0,60)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));layout.Controls.Add(footer,0,2);info.Dock=DockStyle.Fill;info.AutoEllipsis=true;info.TextAlign=ContentAlignment.MiddleLeft;footer.Controls.Add(info,0,0);var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};footer.Controls.Add(actions,1,0);printBack=Button(Loc.T("Späť do editora"),Back);printBack.Width=160;actions.Controls.Add(printBack);print.Text=Loc.T("Tlačiť");print.AutoSize=true;print.AutoSizeMode=AutoSizeMode.GrowAndShrink;print.MinimumSize=new Size(130,42);print.FlatStyle=FlatStyle.Flat;print.Click+=delegate{Submit();};actions.Controls.Add(print);print.Enabled=false;
  printers.SelectedIndexChanged+=delegate{if(!updating)SelectPrinter(Convert.ToString(printers.SelectedItem));};papers.SelectedIndexChanged+=delegate{if(!updating&&selectedPage!=null&&papers.SelectedItem is PaperSize){selectedPage.PaperSize=(PaperSize)papers.SelectedItem;Schedule();}};landscape.CheckedChanged+=delegate{if(!updating&&selectedPage!=null){selectedPage.Landscape=landscape.Checked;Schedule();}};page.ValueChanged+=delegate{if(!updating)Schedule();};debounce.Tick+=delegate{debounce.Stop();StartRequest(false,false);};info.Text=Loc.T("Načítavam tlačiarne…");
 }
 public static LabelSettings CloneLabel(LabelSettings source){using(var stream=new System.IO.MemoryStream()){var xml=new System.Xml.Serialization.XmlSerializer(typeof(LabelSettings));xml.Serialize(stream,source);stream.Position=0;return (LabelSettings)xml.Deserialize(stream);}}
 protected override void OnLoad(EventArgs e){base.OnLoad(e);if(!started){started=true;BeginInvoke((Action)delegate{StartRequest(true,false,initialPrinterName);});}}
 static void Add(TableLayoutPanel p,Control c){c.Dock=DockStyle.Top;c.Margin=new Padding(0,4,0,6);c.TabIndex=p.Controls.Count;p.Controls.Add(c,0,p.RowCount++);p.RowStyles.Add(new RowStyle(SizeType.AutoSize));if(c.AutoSize&&(c is Label||c is CheckBox)){var check=c as CheckBox;if(check!=null)check.AutoSize=false;EventHandler fit=delegate{int width=Math.Max(40,p.ClientSize.Width-p.Padding.Horizontal-c.Margin.Horizontal);if(c.MaximumSize.Width!=width)c.MaximumSize=new Size(width,0);if(check!=null){using(var g=check.CreateGraphics()){int glyph=CheckBoxRenderer.GetGlyphSize(g,System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal).Width;check.Height=Math.Max(glyph+6,TextRenderer.MeasureText(check.Text,check.Font,new Size(Math.Max(1,width-glyph-8-check.Padding.Horizontal),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+6+check.Padding.Vertical);}}};p.SizeChanged+=fit;c.FontChanged+=fit;c.TextChanged+=fit;fit(null,EventArgs.Empty);}}
 static ModernButton Button(string text,Action action){var b=new ModernButton{Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(0,38),FlatStyle=FlatStyle.Flat};b.Click+=delegate{action();};return b;}
 public void Style(Action<Control> apply,bool dark){calibrationStyle=apply;calibrationDark=dark;apply(this);preview.BackColor=dark?Color.FromArgb(14,21,32):Color.FromArgb(233,237,243);print.BackColor=StudioTheme.Accent;print.ForeColor=Color.White;print.FlatAppearance.BorderColor=StudioTheme.Accent;}
 void Schedule(){if(sending)return;readabilityButton.Enabled=false;printDiagnostics=null;diagnosticsButton.Enabled=false;currentReport=null;version++;print.Enabled=false;preview.ClearPages();info.Text=Loc.T("Pripravujem náhľad…");debounce.Stop();debounce.Start();}
 public void SelectPrinter(string name){if(sending||calibrationTargetMode)return;InvalidatePreviewCache();requestedProfile=null;selectedPrinter=null;selectedPage=null;knownPapers=null;updating=true;try{ApplyZebra(new ZebraSettings{Dpi=snapshot.Dpi});if(!string.IsNullOrEmpty(name)&&!printers.Items.Contains(name))printers.Items.Add(name);printers.SelectedItem=name;profileName.Clear();RefreshProfiles(name,null);}finally{updating=false;}printerName.Text=name;printerTip.SetToolTip(printers,name);printerTip.SetToolTip(printerName,name);StartRequest(false,false,name);}
 public void RefreshPreview(){StartRequest(false,false);}
 void StartRequest(bool discover,bool send){StartRequest(discover,send,null);}
 void StartRequest(bool discover,bool send,string requested){if(closed||sending&&!send)return;if(jobError!=null){print.Enabled=false;info.Text=jobError;return;}var trace=new PreviewTrace();if(discover)InvalidatePreviewCache();int setupRevision=driverRevision;debounce.Stop();int ticket=++version;string name=requested??Convert.ToString(printers.SelectedItem);var printer=selectedPrinter==null?null:(PrinterSettings)selectedPrinter.Clone();var settings=selectedPage==null?null:(PageSettings)selectedPage.Clone();var cachedPapers=discover?null:knownPapers;int index=(int)page.Value-1;short count=(short)copies.Value;double correctionX=(double)offsetX.Value,correctionY=(double)offsetY.Value,correctionScaleX=(double)scaleX.Value/100,correctionScaleY=(double)scaleY.Value/100;bool ignoreMargins=ignoreDriverMargins.Checked;var zebra=ReadZebra();var sendZebra=zebraSender;var profile=requestedProfile;requestedProfile=null;var availableProfiles=profiles==null?null:new List<PrinterProfile>(profiles.Profiles);if(profile==null&&profiles!=null&&!string.IsNullOrEmpty(name))profile=profiles.Find(name);if(profile!=null)profile=profile.Copy();
  if(calibrationTargetMode){correctionX=correctionY=0;correctionScaleX=correctionScaleY=1;count=1;}printDiagnostics=null;diagnosticsButton.Enabled=false;busy=true;print.Enabled=false;if(!send)preview.ClearPages();info.Text=Loc.T(send?"Odosielam do tlačiarne…":discover?"Načítavam tlačiarne…":"Pripravujem náhľad…");if(send){sending=true;UpdateZebraEnabled();printers.Enabled=papers.Enabled=landscape.Enabled=copies.Enabled=mediaProfiles.Enabled=ignoreDriverMargins.Enabled=offsetX.Enabled=offsetY.Enabled=scaleX.Enabled=scaleY.Enabled=false;}
  var renderLabel=CloneLabel(snapshot);var renderPlan=outputPlan;var cachedReadability=ownsPreparedPlan?readability:null;readabilityButton.Enabled=false;
  var history=send&&!calibrationTargetMode?printHistory:null;
  bool keepCopy=archivePrint.Checked;
  string sourceId=recoverySourceId;
  int sourceCopy=recoveredCopy;
  Action recorded=recoveryRecorded;
  var recoveryApproval=approvedRecovery;
  approvedRecovery=null;
  string historyName=profileName.Text.Trim(), historyPaper=papers.Text;
  string accepted=approvedFingerprint;trace.Captured=trace.Now;
  PrintWorker.Queue(this,delegate{
   trace.Started=trace.Now;
   var result=new PrintResult{Trace=trace};
   var historyEntry=history==null?null:new PrintHistoryEntry{
    StartedUtc=DateTime.UtcNow, Printer=name, Media=historyName.Length>0?historyName:historyPaper,
    Pages=renderPlan.PageCount, Labels=renderPlan.Placements.Count, Repetitions=count,
    RecoverySourceId=sourceId, RecoveredCopy=sourceCopy, BatchId=recoveryApproval==null?null:recoveryApproval.RootId
   };
   PrinterProfile historySettings=null;
   IDisposable recoveryLease=null;
   bool recoveryRefused=false;
   try{
   if(discover){var list=new List<string>();foreach(string item in PrinterSettings.InstalledPrinters)list.Add(item);result.Printers=list.ToArray();}
   trace.Discovered=trace.Now;using(var doc=new LabelPrintDocument(renderLabel,renderPlan)){if(printer==null){printer=new PrinterSettings();if(!string.IsNullOrEmpty(name))printer.PrinterName=name;if(profile==null&&availableProfiles!=null){var saved=availableProfiles.FindLast(p=>string.Equals(p.Printer,printer.PrinterName,StringComparison.OrdinalIgnoreCase));if(saved!=null)profile=saved.Copy();}settings=(PageSettings)printer.DefaultPageSettings.Clone();settings.Margins=new Margins(0,0,0,0);PrinterPageGeometry.FreezePaper(settings);correctionX=correctionY=0;correctionScaleX=correctionScaleY=1;ignoreMargins=false;if(profile!=null){new PrintCalibration{X=profile.X,Y=profile.Y,ScaleX=profile.ScaleX,ScaleY=profile.ScaleY}.Validate();result.Profile=profile;zebra=ProfileZebra(profile);correctionX=profile.X;correctionY=profile.Y;correctionScaleX=profile.ScaleX;correctionScaleY=profile.ScaleY;ignoreMargins=profile.IgnoreDriverMargins;settings.Landscape=profile.Landscape;bool found=false;result.Papers=ReadPreviewPapers(printer,trace);foreach(PaperSize size in result.Papers)if(size.PaperName==profile.Paper&&size.Width==profile.Width&&size.Height==profile.Height){settings.PaperSize=size;found=true;break;}if(!found){settings.PaperSize=new PaperSize(profile.Paper??profile.DisplayName,profile.Width,profile.Height);result.ProfileWarning=Loc.T("Profil používa vlastné rozmery média. Overte ich v nastavení ovládača.");}}}doc.OffsetX=correctionX;doc.OffsetY=correctionY;doc.ScaleX=correctionScaleX;doc.ScaleY=correctionScaleY;doc.IgnoreDriverMargins=ignoreMargins;result.Zebra=zebra.Copy();result.IgnoreDriverMargins=ignoreMargins;result.OffsetX=correctionX;result.OffsetY=correctionY;result.ScaleX=correctionScaleX;result.ScaleY=correctionScaleY;printer.Copies=count;doc.PrinterSettings=printer;doc.DefaultPageSettings=settings;var readyPage=(PageSettings)settings.Clone();readyPage.PaperSize=settings.PaperSize;readyPage.Landscape=settings.Landscape;result.Printer=(PrinterSettings)printer.Clone();result.Settings=readyPage;if(cachedPapers!=null)result.Papers=cachedPapers;else if(result.Papers==null)result.Papers=ReadPreviewPapers(printer,trace);if(!Array.Exists(result.Papers,size=>size.Width==settings.PaperSize.Width&&size.Height==settings.PaperSize.Height))result.ProfileWarning=Loc.T("Profil používa vlastné rozmery média. Overte ich v nastavení ovládača.");result.Count=doc.PageCount;trace.Setup=trace.Now;
    doc.ValidatePrinter();var geometry=PreviewGeometry(GeometryKey(printer,settings,setupRevision),settings,send||discover,trace);result.Diagnostics=PrintDiagnostics.Capture(doc,geometry,count,zebra);result.Report=doc.InspectOutput(geometry);result.Readability=InspectReadability(result.Report,doc,geometry,zebra,renderLabel.Dpi,cachedReadability);ReviewZebra(result.Report,zebra,renderLabel.Dpi);if(result.ProfileWarning!=null)result.Report.AddWarning(result.ProfileWarning);result.Warnings=result.Report.Messages().ToArray();trace.Inspected=trace.Now;if(send&&(result.Report.Blocking||result.Report.Fingerprint!=accepted)){result.RequiresReview=true;DiagnosticLog.WritePrintTo(DiagnosticLog.DirectoryPath,result.Diagnostics,"review-required",null);}else if(send){
     if(sourceId!=null) {
      try { recoveryLease=history.BeginRecoverySend(sourceId,recoveryApproval); }
      catch { recoveryRefused=true; throw; }
     }
     if(historyEntry!=null){
      historyEntry.Printer=printer.PrinterName;
      historyEntry.QueueToken=Guid.NewGuid().ToString("N");
      doc.DocumentName=PrintQueue.DocumentName(historyEntry.QueueToken);
      try{
       historySettings=HistoryProfile(printer,settings,historyName,doc,zebra);
       historyEntry.Printer=historySettings.Printer;
       historyEntry.Media=historySettings.DisplayName;
      }catch(Exception ex){DiagnosticLog.Write("print-history-settings",ex);}
     }
     DiagnosticLog.WritePrintTo(DiagnosticLog.DirectoryPath,result.Diagnostics,"submit",null);if(zebra.Enabled){sendZebra(doc,geometry,zebra,count);result.Sent=true;}else{doc.PrintController=new StandardPrintController();doc.Print();result.Cancelled=doc.Cancelled;result.Sent=!doc.Cancelled;}DiagnosticLog.WritePrintTo(DiagnosticLog.DirectoryPath,result.Diagnostics,result.Cancelled?"cancelled":"submitted",null);}else{result.Paper=new SizeF(Math.Max((float)(doc.WidthMm*doc.ScaleX+Math.Max(0,doc.OffsetX)),geometry.Bounds.Width*.254f),Math.Max((float)(doc.HeightMm*doc.ScaleY+Math.Max(0,doc.OffsetY)),geometry.Bounds.Height*.254f));result.Image=PreviewImage(doc,renderPlan,Math.Max(0,Math.Min(doc.PageCount-1,index)),result.Paper,trace);var a=geometry.PrintableArea;result.Printable=new RectangleF(a.X*.254f,a.Y*.254f,a.Width*.254f,a.Height*.254f);}}
  }catch(OperationCanceledException){result.Cancelled=true;}catch(Exception ex){string id=send?DiagnosticLog.WritePrintTo(DiagnosticLog.DirectoryPath,result.Diagnostics,"failed",ex):DiagnosticLog.Write("print-preview",ex);result.Error=ex.Message+(id==null?"":"\r\n"+Loc.T("Diagnostický záznam")+": "+id);}
   try {
    if(!recoveryRefused) {
     result.HistoryWarning=RecordPrintAttempt(history,historyEntry,keepCopy,renderLabel,renderPlan,historySettings,result);
     if(recorded!=null&&historyEntry!=null&&!result.RequiresReview)recorded();
    }
   } finally { if(recoveryLease!=null)recoveryLease.Dispose(); }
   trace.Rendered=trace.Now;Deliver(ticket,result);
  });
 }
 void Deliver(int ticket,PrintResult result){lock(deliveryGate){if(closed||ticket!=version){result.Dispose();return;}if(delivery!=null)delivery.Dispose();delivery=result;}try{BeginInvoke((Action)delegate{PrintResult ready;lock(deliveryGate){if(delivery!=result)return;ready=delivery;delivery=null;}if(ready==null)return;using(ready){if(closed||ticket!=version)return;ready.Trace.Delivered=ready.Trace.Now;ApplyResult(ready);}});}catch(InvalidOperationException){lock(deliveryGate){if(delivery==result){delivery=null;result.Dispose();}}}}
 void ApplyResult(PrintResult result){ApplyReadability(result.Readability);printDiagnostics=result.Diagnostics;diagnosticsButton.Enabled=printDiagnostics!=null;updating=true;try{scaleX.Value=Math.Max(80,Math.Min(120,(decimal)(result.ScaleX*100)));scaleY.Value=Math.Max(80,Math.Min(120,(decimal)(result.ScaleY*100)));offsetX.Value=Math.Max(-100,Math.Min(100,(decimal)result.OffsetX));offsetY.Value=Math.Max(-100,Math.Min(100,(decimal)result.OffsetY));if(result.Printers!=null){printers.Items.Clear();printers.Items.AddRange(result.Printers);}if(result.Printer!=null&&result.Papers!=null){selectedPrinter=result.Printer;selectedPage=result.Settings;knownPapers=result.Papers;printerName.Text=selectedPrinter.PrinterName;printerTip.SetToolTip(printers,printerName.Text);printerTip.SetToolTip(printerName,printerName.Text);if(!printers.Items.Contains(printerName.Text))printers.Items.Add(printerName.Text);printers.SelectedItem=printerName.Text;papers.Items.Clear();PaperSize chosen=null;foreach(var size in result.Papers){papers.Items.Add(size);if(size.RawKind==selectedPage.PaperSize.RawKind&&size.Width==selectedPage.PaperSize.Width&&size.Height==selectedPage.PaperSize.Height)chosen=size;}if(chosen==null){chosen=selectedPage.PaperSize;papers.Items.Add(chosen);}papers.SelectedItem=chosen;landscape.Checked=selectedPage.Landscape;}
   ignoreDriverMargins.Checked=result.IgnoreDriverMargins;if(result.Zebra!=null)ApplyZebra(result.Zebra);if(result.Profile!=null){profileName.Text=result.Profile.DisplayName;RefreshProfiles(result.Profile.Printer,result.Profile.DisplayName);}page.Maximum=Math.Max(1,result.Count);pageTools.Visible=result.Count>1;pageTotal.Text="/ "+result.Count;warnings=result.Warnings??new string[0];currentReport=result.Report;UpdateOutputSummary();
   if(result.Error!=null){info.Text=Loc.T(sending?"Tlač sa nepodarila: ":"Náhľad nie je dostupný: ")+result.Error;print.Enabled=false;}
   else if(result.RequiresReview){info.Text=Loc.T("Nastavenia alebo upozornenia sa zmenili. Skontrolujte výstup znova.");print.Enabled=currentReport!=null&&!currentReport.Blocking;}
   else if(result.Cancelled){info.Text=Loc.T("Tlač bola zrušená.");print.Enabled=true;}else if(result.Sent){info.Text=Loc.T("Úloha bola odoslaná do tlačového systému Windows.");print.Enabled=true;}
   else{preview.SetPage(result.Image,result.Paper,result.Printable);result.Image=null;preview.Page=(int)page.Value-1;paperInfo.Text=printDiagnostics==null?"":printDiagnostics.Summary();info.Text=warnings.Length==0?Loc.T("Pripravené"):Loc.T(currentReport!=null&&currentReport.Blocking?"Výstup blokujú chyby":"Skontrolujte upozornenia")+" ("+warnings.Length+")";info.AccessibleDescription=string.Join("\r\n",warnings);printerTip.SetToolTip(info,info.AccessibleDescription);print.Enabled=currentReport!=null&&!currentReport.Blocking;PreviewBuilds++;}
  }finally{updating=false;sending=false;busy=false;printers.Enabled=papers.Enabled=landscape.Enabled=mediaProfiles.Enabled=ignoreDriverMargins.Enabled=offsetX.Enabled=offsetY.Enabled=scaleX.Enabled=scaleY.Enabled=!calibrationTargetMode;copies.Enabled=!calibrationTargetMode&&copies.Maximum>1;driverProperties.Enabled=!calibrationTargetMode;UpdateZebraEnabled();}if(result.HistoryWarning!=null)info.Text+="  "+result.HistoryWarning;if(result.Trace!=null&&result.Error==null&&!result.Sent&&!result.Cancelled&&!result.RequiresReview)LastPreviewPerformance=new PreviewPerformance(result.Trace);if(PreviewReady!=null)PreviewReady(this,EventArgs.Empty);
 }
 public bool ConfigurePrinter(Func<PrintDialog,DialogResult> show){if(selectedPrinter==null||sending||busy||calibrationTargetMode)return false;try{using(var dialog=new PrintDialog{PrinterSettings=(PrinterSettings)selectedPrinter.Clone(),UseEXDialog=true,AllowSomePages=false,AllowSelection=false,AllowCurrentPage=false,AllowPrintToFile=false}){dialog.PrinterSettings.DefaultPageSettings.PaperSize=selectedPage.PaperSize;dialog.PrinterSettings.DefaultPageSettings.Landscape=selectedPage.Landscape;dialog.PrinterSettings.Copies=(short)copies.Value;if(show(dialog)!=DialogResult.OK)return false;string previousPrinter=selectedPrinter.PrinterName;InvalidatePreviewCache();selectedPrinter=(PrinterSettings)dialog.PrinterSettings.Clone();selectedPage=(PageSettings)dialog.PrinterSettings.DefaultPageSettings.Clone();if(!string.Equals(previousPrinter,selectedPrinter.PrinterName,StringComparison.OrdinalIgnoreCase)){updating=true;try{ApplyZebra(new ZebraSettings{Dpi=snapshot.Dpi});}finally{updating=false;}}knownPapers=null;copies.Value=Math.Min(copies.Maximum,Math.Max(1,(int)selectedPrinter.Copies));StartRequest(false,false);return true;}}catch(Exception ex){info.Text=ex.Message;return false;}}
 void UpdateOutputSummary(){
  var summary=new OutputSummary(outputPlan,(int)copies.Value).ToString().Replace("\r\n"," · ").Replace("\n"," · ");
  jobSummary.Text=recoveredCopy>0?string.Format(Loc.T("Dotlač kópie {0}"),recoveredCopy)+" · "+summary:summary;
  LayoutPrintSummary();
 }
 void Review(bool submit){
  if(currentReport==null||sending||busy)return;
  var report=currentReport;
  var owner=FindForm();
  using(var dialog=new OutputReviewDialog(report,(int)copies.Value,Loc.T("Tlačiť"),printerName.Text,Font,owner==null?null:owner.Icon,!replaying)){
   if(calibrationStyle!=null)calibrationStyle(dialog);
   if(dialog.ShowDialog(this)!=DialogResult.OK){
    if(dialog.EditIssue!=null&&EditIssueRequested!=null){
     if(BackRequested!=null)BackRequested(this,EventArgs.Empty);
     EditIssueRequested(dialog.EditIssue);
    }
    return;
   }
  }
  if(!submit||!Ready||!ReferenceEquals(report,currentReport))return;
  var recovery=authorizeRecovery==null?null:authorizeRecovery();
  if(authorizeRecovery!=null&&recovery==null)return;
  if(!Ready||!ReferenceEquals(report,currentReport))return;
  approvedRecovery=recovery;
  approvedFingerprint=report.Fingerprint;
  StartRequest(false,true);
 }
 internal void LimitToSingleRun(){
  copies.Value=1;
  copies.Maximum=1;
  copies.Enabled=false;
  UpdateOutputSummary();
 }
 void Submit(){if(!Ready||sending)return;Review(true);}
 void Back(){if(sending){info.Text=Loc.T("Odosielanie už prebieha. Úlohu môžete zrušiť vo fronte Windows.");}if(BackRequested!=null)BackRequested(this,EventArgs.Empty);}
 protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==Keys.Escape){Back();return true;}return base.ProcessCmdKey(ref msg,key);}
 protected override void Dispose(bool disposing){if(disposing){lock(deliveryGate){closed=true;version++;if(delivery!=null){delivery.Dispose();delivery=null;}}PrintWorker.Cancel(this);previewCache.Dispose();debounce.Dispose();printerTip.Dispose();preview.ClearPages();}base.Dispose(disposing);}
}
}
