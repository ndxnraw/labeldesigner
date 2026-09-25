using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using EanEtikety;
public static class Revision12Tests {
 static string folder;
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static object Get(object o,string n){return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);}
 static void Mouse(PreviewPanel p,string name,PointF at){Call(p,name,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(at.X),(int)Math.Round(at.Y),0));if(name=="OnMouseMove")Call(p,"FlushDragMove");}
 static PointF Center(RectangleF r){return new PointF(r.X+r.Width/2,r.Y+r.Height/2);}
 static void Drag(PreviewPanel p,PointF from,float dx,float dy,bool release){Mouse(p,"OnMouseDown",from);Mouse(p,"OnMouseMove",new PointF(from.X+dx,from.Y+dy));if(release)Mouse(p,"OnMouseUp",new PointF(from.X+dx,from.Y+dy));}
 static void Escape(PreviewPanel p){Call(p,"ProcessCmdKey",new Message(),Keys.Escape);}
 static LabelSettings State(MainForm f){return (LabelSettings)Call(f,"Read");}
 static void Close(double a,double b,string m){Check(Math.Abs(a-b)<.04,m+" "+a+" != "+b);}
 static void Snap(Form f,string name){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(folder,name+".png"));}}
 static void Exports(LabelSettings s,string name){using(var b=Engine.Render(s))b.Save(Path.Combine(folder,name+".png"));Engine.Pdf(Path.Combine(folder,name+".pdf"),s);Engine.Docx(Path.Combine(folder,name+".docx"),s);Engine.Zpl(Path.Combine(folder,name+".zpl"),s);Engine.Save(Path.Combine(folder,name+".ean.xml"),s);}
 [STAThread]public static void Main(string[] args){folder=args[0];Directory.CreateDirectory(folder);try{Run();File.WriteAllText(Path.Combine(folder,"passed.txt"),"PASS: text and barcode drag, all resize handles, proportional font resize, Escape, overflow previews/exports, GTIN-14, settings compatibility.");}catch(Exception e){File.WriteAllText(Path.Combine(folder,"failed.txt"),e.ToString());Environment.ExitCode=1;}}
 static void Run(){
  Check(Engine.Normalize("1001234500001",14)=="10012345000017","GTIN14 checksum");Check(Engine.Normalize("0001234567890",14)=="00012345678905","GTIN14 leading zeros");bool rejected=false;try{Engine.Normalize("10012345000018",14);}catch{rejected=true;}Check(rejected,"wrong GTIN14 must fail");
  var itf=LabelSettings.Default();itf.Kind=14;itf.Ean="10012345000017";itf.Width=100;itf.Height=65;itf.BarWidth=82;itf.BarX=9;itf.BarY=10;itf.BarHeight=32;itf.Texts.Clear();Exports(itf,"itf14");File.WriteAllText(Path.Combine(folder,"itf14-bits.txt"),Engine.Bits(itf.Ean,14));
  var overflow=LabelSettings.Default();overflow.Texts.Add(new TextItem{Text="Cena",X=3,Y=39,Width=54,Height=5,Size=10});overflow.BarX=45;overflow.BarY=-5;overflow.Texts[0].X=-8;overflow.Texts[1].Y=43;Exports(overflow,"overflow");Check(Engine.Warnings(overflow).Exists(x=>x.Contains("Kód presahuje")),"code warning");Check(Engine.Warnings(overflow).Exists(x=>x.Contains("Text presahuje")),"text warning");Check(Engine.Load(Path.Combine(folder,"overflow.ean.xml")).BarY==-5,"negative coordinates roundtrip");
  Check(Engine.Load("Testy/Fixtures/work/qa/settings.ean.xml").Kind==13,"old settings");Check(Engine.Load("Testy/Fixtures/work/qa-update/format-3.ean.xml").Kind==128,"v1.1 settings");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var f=new MainForm(Path.Combine(folder,"preferences.xml"))){
   f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();Application.DoEvents();var p=(PreviewPanel)Get(f,"preview");var grid=(DataGridView)Get(f,"grid");
   foreach(var size in new[]{new Size(1320,930),new Size(1100,800)}){
    f.Size=size;Application.DoEvents();Call(f,"Apply",LabelSettings.Default());var lr=p.LabelRectangle();double sx=lr.Width/60.0,sy=lr.Height/45.0;
    var candidate=p.Element(0);candidate.X+=20/sx;candidate.Y+=10/sy;var expected=SmartGuides.CalculateSnap(candidate,new List<ElementEdit>{p.Element(-1)},60,45,sx,sy).Element;Drag(p,Center(p.ElementRectangle(0)),20,10,true);var state=State(f);Close(state.Texts[0].X,expected.X,"text drag X including alignment");Close(state.Texts[0].Y,expected.Y,"text drag Y including alignment");Check(p.SelectedIndex==0&&grid.CurrentCell.RowIndex==0,"selection synchronization");Check(p.Image!=null,"text drag preview");
    Call(f,"Apply",LabelSettings.Default());p.SelectElement(0);var before=State(f).Texts[0];Drag(p,p.Handles()[4],30,4,true);state=State(f);Check(state.Texts[0].Size>before.Size&&state.Texts[0].Width>before.Width&&state.Texts[0].Height>before.Height,"text corner scales font and box");Close(state.Texts[0].Size/before.Size,state.Texts[0].Width/before.Width,"text proportional scaling");Check(p.Image!=null,"resized text preview");
    double x=state.Texts[0].X,y=state.Texts[0].Y,w=state.Texts[0].Width,h=state.Texts[0].Height,font=state.Texts[0].Size;Drag(p,p.Handles()[4],-20,-5,false);Escape(p);state=State(f);Close(state.Texts[0].Width,w,"Escape text width");Close(state.Texts[0].Size,font,"Escape text font");Close(state.Texts[0].X,x,"Escape text X");
    // Exercise all eight handles from a consistent starting state.
    for(int i=0;i<8;i++){Call(f,"Apply",LabelSettings.Default());p.SelectElement(0);var r=p.ElementRectangle(0);Drag(p,p.Handles()[i],i==0||i==6||i==7?-10:10,i<=2?-4:4,true);state=State(f);Check(p.Image!=null,"handle "+i+" preview");Check(state.Texts[0].Width>=1&&state.Texts[0].Height>=1,"positive resized dimensions");if(i%2==1)Close(state.Texts[0].Size,11,"edge resize leaves font");}
    Call(f,"Apply",LabelSettings.Default());p.SelectElement(-1);Drag(p,p.Handles()[4],20,15,true);state=State(f);Check(state.BarWidth>40&&state.BarHeight>23.5&&state.DigitSize>8,"barcode corner scale");Check(p.Image!=null,"barcode scale preview");double bw=state.BarWidth,bh=state.BarHeight,bs=state.DigitSize;Drag(p,p.Handles()[0],-10,-5,false);Escape(p);state=State(f);Close(state.BarWidth,bw,"Escape barcode width");Close(state.BarHeight,bh,"Escape barcode height");Close(state.DigitSize,bs,"Escape barcode font");
    Call(f,"Apply",LabelSettings.Default());Drag(p,Center(p.BarcodeRectangle()),(float)(35*sx),0,true);state=State(f);Check(Math.Abs(state.BarX-45)<.3,"barcode may cross right edge");Check(p.Image!=null&&((Button)Get(f,"pdf")).Enabled,"overflow keeps preview and export");Drag(p,Center(p.BarcodeRectangle()),(float)(-35*sx),0,true);state=State(f);Check(Math.Abs(state.BarX-10)<.3,"drag overflow back");
    Call(f,"Apply",overflow);Check(p.Image!=null,"loaded overflow visible");grid.CurrentCell=grid.Rows[1].Cells[0];Check(p.SelectedIndex==1,"table selects off-edge text");Call(f,"Apply",LabelSettings.Default());
   }
   f.Size=new Size(1320,930);Application.DoEvents();Call(f,"Apply",LabelSettings.Default());p.SelectElement(0);Drag(p,p.Handles()[4],28,4,true);Exports(State(f),"resized");Snap(f,"editor-text");
   Call(f,"Apply",overflow);Snap(f,"editor-overflow");Call(f,"Apply",itf);Snap(f,"editor-itf14");
   p.SelectElement(-1);double oldW=State(f).BarWidth;Drag(p,p.Handles()[3],20,0,true);Check(p.Image!=null&&State(f).BarWidth>oldW,"ITF14 resizing");Engine.Save(Path.Combine(folder,"edited.ean.xml"),State(f));Check(Engine.Load(Path.Combine(folder,"edited.ean.xml")).Kind==14,"ITF14 persisted");
   Call(f,"Apply",LabelSettings.Default());f.Close();
  }
 }
}

