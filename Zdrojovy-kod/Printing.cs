using System;using System.Drawing;using System.Drawing.Printing;using System.Windows.Forms;
namespace EanEtikety {
public class LabelPrintDocument:PrintDocument {
 readonly LabelSettings label;readonly VectorScene scene;int page;
 public int PageCount {get{return (label.Copies+Capacity-1)/Capacity;}}
 public int PreviewPageIndex=-1;
 public bool Cancelled {get;private set;}
 public int Capacity {get{return Engine.Columns(label)*Engine.Rows(label);}}
 public double WidthMm {get{return (label.UsePaper||label.Sheet)?label.PageWidth:label.Width;}}
 public double HeightMm {get{return (label.UsePaper||label.Sheet)?label.PageHeight:label.Height;}}
 public LabelPrintDocument(LabelSettings settings){if(settings==null||(string.IsNullOrWhiteSpace(settings.Ean)&&!settings.Gs1Mode&&(settings.Texts==null||!settings.Texts.Exists(t=>t!=null&&!string.IsNullOrWhiteSpace(t.Text)))&&(settings.Symbols==null||settings.Symbols.Count==0)))throw new InvalidOperationException(Loc.T("Dokument neobsahuje nič na tlač."));
  using(var stream=new System.IO.MemoryStream()){var serializer=new System.Xml.Serialization.XmlSerializer(typeof(LabelSettings));serializer.Serialize(stream,settings);stream.Position=0;label=(LabelSettings)serializer.Deserialize(stream);}Engine.Validate(label);scene=VectorArt.Build(label);DocumentName="ND LabelDesigner";OriginAtMargins=false;
  DefaultPageSettings.Margins=new Margins(0,0,0,0);DefaultPageSettings.Landscape=false;
  DefaultPageSettings.PaperSize=new PaperSize("ND LabelDesigner",(int)Math.Round(WidthMm/25.4*100),(int)Math.Round(HeightMm/25.4*100));PrinterSettings.Copies=1;
 }
 public RectangleF LabelBounds(int index){int cols=Engine.Columns(label);return new RectangleF((float)(label.UsePaper?label.MarginX:label.Sheet?label.MarginX+(index%cols)*(label.Width+label.GapX):0),(float)(label.UsePaper?label.MarginY:label.Sheet?label.MarginY+(index/cols)*(label.Height+label.GapY):0),(float)label.Width,(float)label.Height);}
 public int RenderPage(Graphics g,int pageIndex){if(pageIndex<0||pageIndex>=PageCount)throw new ArgumentOutOfRangeException("pageIndex");var saved=g.Save();try{g.PageUnit=GraphicsUnit.Millimeter;g.PageScale=1;int count=Math.Min(Capacity,label.Copies-pageIndex*Capacity);for(int i=0;i<count;i++)scene.Draw(g,LabelBounds(i),false);return count;}finally{g.Restore(saved);}}
 public bool FitsPaper(Rectangle bounds){double right=0,bottom=0;for(int i=0;i<Math.Min(Capacity,label.Copies);i++){var r=LabelBounds(i);right=Math.Max(right,r.Right);bottom=Math.Max(bottom,r.Bottom);}if(label.UsePaper){right+=label.MarginRight;bottom+=label.MarginBottom;}else if(label.Sheet){right+=label.MarginX;bottom+=label.MarginY;}return bounds.Width*.254+.3>=right&&bounds.Height*.254+.3>=bottom;}

 public bool ContentFits(RectangleF printable){var area=new RectangleF(printable.X*.254f,printable.Y*.254f,printable.Width*.254f,printable.Height*.254f);area.Inflate(.15f,.15f);for(int i=0;i<Math.Min(Capacity,label.Copies);i++){var pos=LabelBounds(i);foreach(var shape in scene.Shapes){RectangleF b;using(var path=(System.Drawing.Drawing2D.GraphicsPath)shape.Path.Clone()){using(var m=ElementTransform.Matrix(shape.Rotation,shape.Pivot))path.Transform(m);b=RectangleF.Intersect(path.GetBounds(),ElementTransform.Bounds(shape.Clip,shape.Rotation,shape.Pivot));}b=RectangleF.Intersect(b,new RectangleF(0,0,(float)label.Width,(float)label.Height));if(b.IsEmpty)continue;b.Offset(pos.X,pos.Y);if(!area.Contains(b))return false;}}return true;}
 protected override void OnBeginPrint(PrintEventArgs e){page=PrintController.IsPreview&&PreviewPageIndex>=0?PreviewPageIndex:0;Cancelled=false;base.OnBeginPrint(e);Cancelled=e.Cancel;}
 protected override void OnPrintPage(PrintPageEventArgs e){if(!FitsPaper(e.PageBounds))throw new InvalidOperationException(Loc.T("Papier tlačiarne je menší než nastavený formát. Upravte formát v nastavení tlačiarne."));var graphicsState=e.Graphics.Save();try{e.Graphics.ResetTransform();e.Graphics.PageUnit=GraphicsUnit.Millimeter;e.Graphics.PageScale=1;if(!PrintController.IsPreview)e.Graphics.TranslateTransform(-e.PageSettings.HardMarginX*.254f,-e.PageSettings.HardMarginY*.254f);var area=e.PageSettings.PrintableArea;e.Graphics.SetClip(new RectangleF(area.X*.254f,area.Y*.254f,area.Width*.254f,area.Height*.254f),System.Drawing.Drawing2D.CombineMode.Intersect);RenderPage(e.Graphics,page++);}finally{e.Graphics.Restore(graphicsState);}e.HasMorePages=page<PageCount&&!(PrintController.IsPreview&&PreviewPageIndex>=0);base.OnPrintPage(e);}
 public void ValidatePrinter(){if(!PrinterSettings.IsValid)throw new InvalidOperationException(Loc.T("Vybraná tlačiareň nie je dostupná."));if(!FitsPaper(DefaultPageSettings.Bounds))throw new InvalidOperationException(Loc.T("Papier tlačiarne je menší než nastavený formát. Upravte formát v nastavení tlačiarne."));}
 public System.Collections.Generic.List<string> PrintWarnings(){var warnings=Engine.Warnings(label);if(!ContentFits(DefaultPageSettings.PrintableArea))warnings.Add(Loc.T("Časť obsahu je mimo tlačiteľnej oblasti tlačiarne a môže byť orezaná."));return warnings;}
 protected override void OnEndPrint(PrintEventArgs e){base.OnEndPrint(e);Cancelled=Cancelled||e.Cancel;}
 protected override void Dispose(bool disposing){if(disposing)scene.Dispose();base.Dispose(disposing);}
}
public partial class MainForm {
 Form CreatePrintWindow(LabelSettings settings){var panel=new PrintPanel(settings);
  var window=new Form{Text="ND LabelDesigner – "+Loc.T("Tlačiť"),Size=new Size(1050,760),MinimumSize=new Size(900,640),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,Font=Font,Icon=Icon,AutoScaleMode=AutoScaleMode.Dpi};
  window.Controls.Add(panel);ApplyColors(window);panel.Style(ApplyColors,dark);panel.BackRequested+=delegate{window.Close();};window.HandleCreated+=delegate{try{int value=dark?1:0;DwmSetWindowAttribute(window.Handle,20,ref value,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}};return window;
 }
 void PrintLabel(){try{var settings=Read();timer.Stop();using(var window=CreatePrintWindow(settings))window.ShowDialog(this);}catch(Exception ex){Error(ex);}}
}
}
