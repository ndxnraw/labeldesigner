using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
using EanEtikety;
class SymbolTests {
 static string dir;static void Check(bool b,string m){if(!b)throw new Exception(m);}static object Get(object o,string n){return o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 static void Mouse(PreviewPanel p,string name,PointF pt){Call(p,name,new MouseEventArgs(MouseButtons.Left,1,(int)pt.X,(int)pt.Y,0));if(name=="OnMouseMove")Call(p,"FlushDragMove");}static void Drag(PreviewPanel p,PointF pt,float dx,float dy){Mouse(p,"OnMouseDown",pt);Mouse(p,"OnMouseMove",new PointF(pt.X+dx,pt.Y+dy));Mouse(p,"OnMouseUp",new PointF(pt.X+dx,pt.Y+dy));}
 static void Export(LabelSettings s,string n){Engine.Svg(Path.Combine(dir,n+".svg"),s);Engine.Pdf(Path.Combine(dir,n+".pdf"),s);Engine.Docx(Path.Combine(dir,n+".docx"),s);Engine.Zpl(Path.Combine(dir,n+".zpl"),s);Engine.Save(Path.Combine(dir,n+".ean.xml"),s);using(var b=Engine.Render(s))b.Save(Path.Combine(dir,n+".png"));}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{GraphicShapes();Run();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS: vector symbols and editable shapes, SVG/PDF/DOCX/ZPL/XML, print drawing, quiet zones, symbol movement and eight handles, Escape, cached drag, mm/cm, delete, old project compatibility.");}catch(Exception e){File.WriteAllText(Path.Combine(dir,"failed.txt"),e.ToString());Environment.ExitCode=1;}}
 static bool Black(Bitmap bitmap,double x,double y,double dots){return bitmap.GetPixel((int)(x*dots),(int)(y*dots)).R<100;}
 static void CheckArtwork(Bitmap b,double dots,string source){
  Check(Black(b,10.5,13,dots)&&!Black(b,15,15,dots),source+" preserves hollow outline");
  Check(Black(b,33,15,dots)&&!Black(b,31.5,19,dots),source+" preserves rotated line thickness");
  Check(Black(b,48,14,dots),source+" preserves solid black fill");
  Check(!Black(b,48,26,dots)&&Black(b,44.25,26,dots),source+" preserves a white knockout inside a black outline");
 }
 static bool QuietWarning(LabelSettings label,int index){
  return PreflightReport.Inspect(OutputPlan.Create(label)).Issues.Exists(i=>i.Element==index&&!i.Blocking&&
   i.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu"));
 }
 static void GraphicShapes(){
  var label=LabelSettings.Default();label.HideBarcode=true;label.Texts.Clear();label.Dpi=203.2;
  label.Symbols.Add(new SymbolItem{X=10,Y=10,Width=12,Height=12,Shape=new ShapeContent{Stroke=1}});
  label.Symbols.Add(new SymbolItem{X=27,Y=14,Width=12,Height=2,Rotation=90,Shape=new ShapeContent{IsLine=true}});
  label.Symbols.Add(new SymbolItem{X=44,Y=10,Width=8,Height=20,Shape=new ShapeContent{Fill=ShapeFill.Black}});
  label.Symbols.Add(new SymbolItem{X=44,Y=22,Width=8,Height=8,Shape=new ShapeContent{Stroke=1,Fill=ShapeFill.White}});
  Export(label,"graphic");
  var saved=Engine.Load(Path.Combine(dir,"graphic.ean.xml"));
  Check(saved.Symbols[1].Shape.IsLine&&saved.Symbols[3].Shape.Fill==ShapeFill.White,"editable shape survives XML");
  var copied=saved.Symbols[0].Copy();copied.Shape.Stroke=2;
  Check(saved.Symbols[0].Shape.Stroke==1,"copies have independent styles");
  using(var picture=new Bitmap(Path.Combine(dir,"graphic.png")))CheckArtwork(picture,8,"vector raster");
  string note;
  var zpl=File.ReadAllText(Path.Combine(dir,"graphic.zpl"));
  using(var bitmap=ZplImport.Render(zpl,203.2,60,45,out note))CheckArtwork(bitmap,8,"standalone Zebra ZPL");
  Check(ZplImport.Project(zpl).Symbols[1].Shape.IsLine,"ZPL preserves LabelDesigner source metadata");
  Check(File.ReadAllText(Path.Combine(dir,"graphic.svg")).Contains("fill-rule=\"evenodd\""),"SVG supports the hollow vector ring");
  Check(Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"graphic.pdf"))).Contains("f*"),"PDF supports the hollow vector ring");
  using(var word=ZipFile.OpenRead(Path.Combine(dir,"graphic.docx")))
  using(var reader=new StreamReader(word.GetEntry("word/media/part1.svg").Open()))
   Check(reader.ReadToEnd().Contains("fill-rule=\"evenodd\""),"Word embeds the vector shape");
  using(var doc=new LabelPrintDocument(label))
  using(var pixels=new Bitmap(480,360,PixelFormat.Format24bppRgb)){
   pixels.SetResolution(203.2f,203.2f);
   using(var g=Graphics.FromImage(pixels)){
    g.Clear(Color.White);
    doc.RenderPrinterPage(g,0,new RectangleF(0,0,240,180),0,0,false);
   }
   CheckArtwork(pixels,8,"Windows print path");
   using(var preview=doc.CreatePreview(0,new SizeF(60,45)))
   using(var display=new Bitmap(480,360)){
    using(var g=Graphics.FromImage(display)){
     g.Clear(Color.White);
     g.DrawImage(preview,new RectangleF(0,0,480,360));
    }
    display.Save(Path.Combine(dir,"graphic-print-preview.png"));
    CheckArtwork(display,8,"vector print preview");
   }
   var geometry=new PrinterPageGeometry(new Rectangle(0,0,236,177),new RectangleF(0,0,236,177),0,0,false,203,203);
   using(var stream=new MemoryStream()){
    ZebraPrint.Render(doc,geometry,new ZebraSettings{Enabled=true,Dpi=203.2},1,stream);
    using(var zebra=ZplImport.Render(Encoding.ASCII.GetString(stream.ToArray()).Replace("^MUD,300,300\n",""),203.2,60,45,out note))
     CheckArtwork(zebra,8,"direct Zebra print path");
   }
  }
  var ean=LabelSettings.Default();ean.Texts.Clear();
  ean.Symbols.Add(new SymbolItem{X=4,Y=4,Width=52,Height=36,Shape=new ShapeContent{Stroke=.2}});
  Check(!QuietWarning(ean,-3),"hollow frame around EAN does not occupy quiet zone");
  ean.Symbols[0].Shape.Fill=ShapeFill.Black;
  Check(QuietWarning(ean,-3),"filled EAN frame occupies quiet zone");
  ean.Symbols[0]=new SymbolItem{X=24,Y=14,Width=6,Height=5,Shape=new ShapeContent{Stroke=0,Fill=ShapeFill.White}};
  Check(!QuietWarning(ean,-3)&&LabelChecks.Inspect(ean).Exists(i=>i.Element==-3&&i.Message==Loc.T("Čiara alebo obdĺžnik prekrýva kód.")),
   "foreground white fill warns about erasing bars even without touching the quiet zone");
  ean.Metadata.Add(new ElementMeta{Index=-3,Layer=-10});
  Check(!LabelChecks.Inspect(ean).Exists(i=>i.Element==-3),"white fill below the code is harmless");
  ean.Metadata.Clear();
  ean.Symbols[0]=new SymbolItem{X=10.5,Y=18,Width=4,Height=.15,Rotation=90,Shape=new ShapeContent{IsLine=true}};
  Check(QuietWarning(ean,-3),"thin rotated line in linear quiet zone");
  var qr=LabelSettings.Default();qr.HideBarcode=true;qr.Texts.Clear();
  qr.Symbols.Add(new SymbolItem{X=20,Y=10,Width=20,Height=20,Matrix=new MatrixContent{Type="QR",Data="Label"}});
  qr.Symbols.Add(new SymbolItem{X=26,Y=11,Width=8,Height=.1,Shape=new ShapeContent{IsLine=true}});
  Check(QuietWarning(qr,-4),"thin line in QR top quiet zone");
  qr.Symbols[1].Y=5;
  Check(!QuietWarning(qr,-4),"line outside QR quiet zone");
 }
 static void ShapeDialog(string title,Action show,Action<Form> edit){
  bool opened=false;Exception failure=null;
  using(var timer=new Timer{Interval=40}){
   timer.Tick+=delegate{
    foreach(Form dialog in Application.OpenForms){
     if(dialog.Text!=Loc.T(title))continue;
     timer.Stop();opened=true;
     try{edit(dialog);}catch(Exception ex){failure=ex;dialog.Close();}
     break;
    }
   };
   timer.Start();show();
  }
  if(failure!=null)throw failure;
  Check(opened,"insert or edit dialog opens for "+title);
 }
 static Control ShapeField(Form form,string name){return form.Controls.Find(name,true)[0];}
 static void ShapeEditor(MainForm form,PreviewPanel preview){
  var baseline=LabelSettings.Default();
  baseline.Unit="cm";
  Call(form,"Apply",baseline);
  Call(form,"ResetHistory",true);
  Call(form,"BuildInsertMenu");
  var insert=(ContextMenuStrip)Get(form,"insertMenu");
  var line=insert.Items.OfType<ToolStripMenuItem>().Single(item=>item.Text==Loc.T("Pridať čiaru…"));
  var rectangle=insert.Items.OfType<ToolStripMenuItem>().Single(item=>item.Text==Loc.T("Pridať obdĺžnik…"));
  Check(line.Text==Loc.T("Pridať čiaru…")&&rectangle.Text==Loc.T("Pridať obdĺžnik…"),"shape commands in Insert menu");
  ShapeDialog("Čiara",line.PerformClick,dialog=>{
   ((NumericUpDown)ShapeField(dialog,"shapeWidth")).Value=2.4m;
   ((NumericUpDown)ShapeField(dialog,"shapeHeight")).Value=.025m;
   ((Button)dialog.AcceptButton).PerformClick();
  });
  var label=(LabelSettings)Call(form,"Read");
  Check(label.Symbols.Count==1&&label.Symbols[0].Shape.IsLine&&label.Symbols[0].Width==24&&label.Symbols[0].Height==.25,
   "line inserted with exact cm dimensions");
  Call(form,"StepHistory",false);
  Check(((LabelSettings)Call(form,"Read")).Symbols.Count==0,"one undo removes inserted line");
  ShapeDialog("Obdĺžnik",rectangle.PerformClick,dialog=>{
   ((NumericUpDown)ShapeField(dialog,"shapeX")).Value=.6m;
   ((NumericUpDown)ShapeField(dialog,"shapeY")).Value=.7m;
   ((NumericUpDown)ShapeField(dialog,"shapeWidth")).Value=4.8m;
   ((NumericUpDown)ShapeField(dialog,"shapeHeight")).Value=3.4m;
   ((NumericUpDown)ShapeField(dialog,"shapeStroke")).Value=.05m;
   ((Button)dialog.AcceptButton).PerformClick();
  });
  Check(preview.Scene.Shapes.Exists(shape=>shape.Element==-3),"editor preview contains rectangle artwork");
  preview.SelectElement(-2);
  var center=Point.Round(preview.ScreenPoint(new PointF(30,24)));
  Check((int)Call(preview,"Hit",center)==-1,"hollow center allows selecting the barcode underneath");
  var stroke=Point.Round(preview.ScreenPoint(new PointF(6.25f,23)));
  ShapeDialog("Obdĺžnik",()=>Mouse(preview,"OnMouseDoubleClick",stroke),dialog=>{
   ((ComboBox)ShapeField(dialog,"shapeFill")).SelectedIndex=(int)ShapeFill.White;
   ((Button)dialog.AcceptButton).PerformClick();
  });
  Check(((LabelSettings)Call(form,"Read")).Symbols[0].Shape.Fill==ShapeFill.White,"double-click edits shape fill");
  Check((int)Call(preview,"Hit",center)==-3,"filled center is selectable as shape");
  Call(form,"Apply",baseline);
 }
 static void Run(){var s=LabelSettings.Default();s.Width=90;s.Symbols.Add(new SymbolItem{X=65,Y=12,Width=16,Height=16*DepositArt.Aspect});Export(s,"symbol");Check(Engine.Load(Path.Combine(dir,"symbol.ean.xml")).Symbols.Count==1,"XML symbol");Check(ZplImport.Project(File.ReadAllText(Path.Combine(dir,"symbol.zpl"))).Symbols.Count==1,"ZPL symbol");Check(Engine.Load("tests/fixtures/work/qa-2.0.1/settings.ean.xml").Symbols.Count==0,"legacy XML");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var f=new MainForm(Path.Combine(dir,"preferences.xml"))){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();Call(f,"Apply",s);var p=(PreviewPanel)Get(f,"preview");p.SelectElement(-3);var r=p.ElementRectangle(-3);var old=p.Scene;Mouse(p,"OnMouseDown",new PointF(r.Left+r.Width/2,r.Top+r.Height/2));Mouse(p,"OnMouseMove",new PointF(r.Left+r.Width/2+10,r.Top+r.Height/2+10));Check(object.ReferenceEquals(old,p.Scene),"symbol move cache");var moved=(LabelSettings)Call(f,"Read");Check(moved.Symbols[0].X>s.Symbols[0].X,"symbol moved");Call(p,"ProcessCmdKey",new Message(),Keys.Escape);var back=(LabelSettings)Call(f,"Read");Check(Math.Abs(back.Symbols[0].X-s.Symbols[0].X)<.02,"Escape symbol");
   for(int i=0;i<8;i++){Call(f,"Apply",s);p.SelectElement(-3);Drag(p,p.Handles()[i],i==0||i==6||i==7?-12:12,i<=2?-10:10);var changed=(LabelSettings)Call(f,"Read");Check(p.Scene!=null&&changed.Symbols[0].Width>=1&&changed.Symbols[0].Height>=1,"resize symbol "+i);if(i%2==0)Check(Math.Abs(changed.Symbols[0].Height/changed.Symbols[0].Width-DepositArt.Aspect)<.01,"corner aspect");}
   Call(f,"Apply",s);((ComboBox)Get(f,"units")).SelectedItem="cm";Call(f,"EditElement",new ElementEdit{Index=-3,X=60,Y=15,Width=20,Height=20*DepositArt.Aspect,Font=10});var cm=(LabelSettings)Call(f,"Read");Check(cm.Symbols[0].X==60&&cm.Symbols[0].Width==20,"cm symbol units");Call(f,"UpdatePreview");p.SelectElement(-3);using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(dir,"app.png"));}Call(f,"RemoveText");Check(((LabelSettings)Call(f,"Read")).Symbols.Count==0&&((LabelSettings)Call(f,"Read")).Texts.Count==1,"remove symbol only");
   ShapeEditor(f,p);Call(f,"Apply",LabelSettings.Default());f.Close();
  }
 }
}
