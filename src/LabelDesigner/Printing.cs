using System;using System.Drawing;using System.Drawing.Printing;using System.Windows.Forms;
namespace EanEtikety {
public class LabelPrintDocument:PrintDocument {
 readonly LabelSettings label;readonly OutputPlan plan;int page;public double OffsetX,OffsetY;
 public bool IgnoreDriverMargins {get;set;}
 public int PageCount {get{return plan.PageCount;}}
 public int PreviewPageIndex=-1;
 public bool Cancelled {get;private set;}
 public int Capacity {get{return label.Layout.Enabled?label.Layout.Columns*label.Layout.Rows:Engine.Columns(label)*Engine.Rows(label);}}
 public double WidthMm {get{return plan.Width;}}
 public double HeightMm {get{return plan.Height;}}
 public LabelPrintDocument(LabelSettings settings):this(settings,null){}
 public LabelPrintDocument(LabelSettings settings,OutputPlan output){if(settings==null)throw new ArgumentNullException("settings");if((settings.HideBarcode||string.IsNullOrWhiteSpace(settings.Ean)&&!settings.Gs1Mode)&&settings.Symbols.Count==0&&!settings.Texts.Exists(t=>!string.IsNullOrWhiteSpace(t.Text)))throw new InvalidOperationException(Loc.T("Dokument neobsahuje nič na tlač."));label=ProjectXml.Clone(settings);plan=output??OutputPlan.Create(label);if(plan.PageCount==0)throw new InvalidOperationException(Loc.T("Dokument neobsahuje nič na tlač."));DocumentName="ND LabelDesigner — Pre-release";OriginAtMargins=false;DefaultPageSettings.Margins=new Margins(0,0,0,0);DefaultPageSettings.Landscape=false;DefaultPageSettings.PaperSize=new PaperSize("ND LabelDesigner — Pre-release",(int)Math.Round(WidthMm/25.4*100),(int)Math.Round(HeightMm/25.4*100));PrinterSettings.Copies=1;}
 public RectangleF LabelBounds(int index){if(index<0||index>=plan.Placements.Count)throw new ArgumentOutOfRangeException("index");var bounds=plan.Placements[index].Bounds;bounds.Offset((float)OffsetX,(float)OffsetY);return bounds;}
 public int RenderPage(Graphics g,int pageIndex){if(pageIndex<0||pageIndex>=PageCount)throw new ArgumentOutOfRangeException("pageIndex");var saved=g.Save();try{g.PageUnit=GraphicsUnit.Millimeter;g.PageScale=1;g.TranslateTransform((float)OffsetX,(float)OffsetY);using(var scene=plan.Scene(pageIndex))scene.Draw(g,new RectangleF(0,0,(float)plan.Width,(float)plan.Height),false);int count=0;foreach(var placement in plan.Placements)if(placement.Page==pageIndex)count++;return count;}finally{g.Restore(saved);}}
 public bool FitsPaper(Rectangle bounds){double right=0,bottom=0;foreach(var p in plan.Placements){right=Math.Max(right,p.Bounds.Right+OffsetX);bottom=Math.Max(bottom,p.Bounds.Bottom+OffsetY);}if(label.Layout.Enabled){right+=label.Layout.Right;bottom+=label.Layout.Bottom;}else if(label.UsePaper){right+=label.MarginRight;bottom+=label.MarginBottom;}else if(label.Sheet){right+=label.MarginX;bottom+=label.MarginY;}return bounds.Width*.254+.3>=right&&bounds.Height*.254+.3>=bottom;}
 public bool ContentFits(RectangleF printable){var area=new RectangleF(printable.X*.254f,printable.Y*.254f,printable.Width*.254f,printable.Height*.254f);area.Inflate(.15f,.15f);for(int i=0;i<PageCount;i++)using(var scene=plan.Scene(i))foreach(var shape in scene.Shapes){RectangleF b;using(var path=(System.Drawing.Drawing2D.GraphicsPath)shape.Path.Clone()){using(var m=ElementTransform.Matrix(shape.Rotation,shape.Pivot))path.Transform(m);b=RectangleF.Intersect(path.GetBounds(),ElementTransform.Bounds(shape.Clip,shape.Rotation,shape.Pivot));}if(shape.CanvasClip.HasValue)b=RectangleF.Intersect(b,shape.CanvasClip.Value);b=RectangleF.Intersect(b,new RectangleF(0,0,(float)plan.Width,(float)plan.Height));if(b.IsEmpty)continue;b.Offset((float)OffsetX,(float)OffsetY);if(!area.Contains(b))return false;}return true;}
 protected override void OnBeginPrint(PrintEventArgs e){page=PrintController.IsPreview&&PreviewPageIndex>=0?PreviewPageIndex:0;Cancelled=false;base.OnBeginPrint(e);Cancelled=e.Cancel;}
 // Keep the device's native clipping; compatibility only removes our additional
 // driver-reported clip. Neither path scales the document or changes its origin.
 public void RenderPrinterPage(Graphics graphics,int pageIndex,RectangleF printable,float hardMarginX,float hardMarginY,bool isPreview){var state=graphics.Save();try{graphics.ResetTransform();graphics.PageUnit=GraphicsUnit.Millimeter;graphics.PageScale=1;if(!isPreview)graphics.TranslateTransform(-hardMarginX*.254f,-hardMarginY*.254f);if(!IgnoreDriverMargins)graphics.SetClip(new RectangleF(printable.X*.254f,printable.Y*.254f,printable.Width*.254f,printable.Height*.254f),System.Drawing.Drawing2D.CombineMode.Intersect);RenderPage(graphics,pageIndex);}finally{graphics.Restore(state);}}
 public Image CreatePreview(int pageIndex,SizeF paper){
  // Record vector artwork independently of a printer DC so bad driver bounds
  // cannot hide document content in the preview.
  System.Drawing.Imaging.Metafile image;
  using(var bitmap=new Bitmap(1,1))using(var reference=Graphics.FromImage(bitmap)){IntPtr hdc=reference.GetHdc();try{image=new System.Drawing.Imaging.Metafile(hdc,new RectangleF(0,0,paper.Width,paper.Height),System.Drawing.Imaging.MetafileFrameUnit.Millimeter,System.Drawing.Imaging.EmfType.EmfPlusDual);}finally{reference.ReleaseHdc(hdc);}}
  try{using(var graphics=Graphics.FromImage(image))RenderPage(graphics,pageIndex);if(image.GetMetafileHeader().MetafileSize>32*1024*1024)throw new InvalidOperationException(Loc.T("Náhľad prekročil limit 32 MB. Zjednodušte dokument."));return image;}catch{image.Dispose();throw;}
 }
 protected override void OnPrintPage(PrintPageEventArgs e){RenderPrinterPage(e.Graphics,page++,e.PageSettings.PrintableArea,e.PageSettings.HardMarginX,e.PageSettings.HardMarginY,PrintController.IsPreview);e.HasMorePages=page<PageCount&&!(PrintController.IsPreview&&PreviewPageIndex>=0);base.OnPrintPage(e);}
 public void ValidatePrinter(){if(!PrinterSettings.IsValid)throw new InvalidOperationException(Loc.T("Vybraná tlačiareň nie je dostupná."));}
 public System.Collections.Generic.List<string> MediaWarnings(Rectangle paper,RectangleF printable){var warnings=new System.Collections.Generic.List<string>();if(!FitsPaper(paper)||paper.Width*.254+.3<WidthMm||paper.Height*.254+.3<HeightMm)warnings.Add(Loc.T("Ovládač hlási menší papier než dokument. Skontrolujte vložené médium; môžete pokračovať v mierke 100 %."));if(IgnoreDriverMargins)warnings.Add(Loc.T("Tlačiteľná oblasť ovládača sa ignoruje. Fyzické okraje tlačiarne môžu obsah orezať."));else if(!ContentFits(printable))warnings.Add(Loc.T("Časť obsahu je mimo tlačiteľnej oblasti tlačiarne a môže byť orezaná."));return warnings;}
 public System.Collections.Generic.List<string> PrintWarnings(){var warnings=new System.Collections.Generic.List<string>();var seen=new System.Collections.Generic.HashSet<LabelSettings>();foreach(var placement in plan.Placements)if(seen.Add(placement.Label))foreach(var issue in LabelChecks.Inspect(placement.Label))if(!warnings.Contains(issue.ToString()))warnings.Add(issue.ToString());warnings.AddRange(MediaWarnings(DefaultPageSettings.Bounds,DefaultPageSettings.PrintableArea));return warnings;}
 protected override void OnEndPrint(PrintEventArgs e){base.OnEndPrint(e);Cancelled=Cancelled||e.Cancel;}
 protected override void Dispose(bool disposing){base.Dispose(disposing);}
}
public partial class MainForm {
 Form CreatePrintWindow(LabelSettings settings){return CreateJobPrintWindow(settings,null);}
 Form CreateJobPrintWindow(LabelSettings settings,OutputPlan plan){var panel=new PrintPanel(settings,plan);if(workspace!=null)panel.LoadProfiles(System.IO.Path.Combine(workspace.DirectoryPath,"printers.xml"));
  var window=new Form{Text="ND LabelDesigner – "+Loc.T("Tlačiť"),Size=new Size(1050,760),MinimumSize=new Size(900,640),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,Font=Font,Icon=Icon,AutoScaleMode=AutoScaleMode.Dpi};
  window.Controls.Add(panel);ApplyColors(window);panel.Style(ApplyColors,dark);panel.BackRequested+=delegate{window.Close();};return window;
 }
 void PrintLabel(){try{var settings=PreparedLabel(Read());timer.Stop();using(var window=CreatePrintWindow(settings))window.ShowDialog(this);}catch(Exception ex){Error(ex);}}
}
}
