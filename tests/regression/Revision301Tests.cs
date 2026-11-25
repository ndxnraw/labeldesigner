using System;using System.IO;using System.Drawing;using System.Diagnostics;using System.Collections.Generic;using System.Reflection;using System.Runtime.InteropServices;using System.Text;using System.Windows.Forms;using EanEtikety;
class Revision301Tests {
 static string dir;static int assertions;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string text){assertions++;if(!ok)throw new Exception(text);}static void Equal(double a,double b,string text){Check(Math.Abs(a-b)<1e-9,text+": "+a+" != "+b);}static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,flags).GetValue(o);}static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,flags).Invoke(o,args);}
 static void Mouse(PreviewPanel p,string name,Point at){Call(p,name,new MouseEventArgs(MouseButtons.Left,1,at.X,at.Y,0));if(name=="OnMouseMove")Call(p,"FlushDragMove");}static void Snap(Form f,string name){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(dir,name+".png"));}}
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);[DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr h);[DllImport("user32.dll")]static extern int ReleaseDC(IntPtr h,IntPtr dc);[DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);[DllImport("gdi32.dll",CharSet=CharSet.Unicode)]static extern int GetTextFace(IntPtr dc,int count,StringBuilder face);
 static void NativeFont(Control c){c.CreateControl();var dc=GetDC(c.Handle);var font=SendMessage(c.Handle,0x31,IntPtr.Zero,IntPtr.Zero);var old=SelectObject(dc,font);try{var face=new StringBuilder(128);GetTextFace(dc,128,face);Check(face.ToString()=="Source Sans 3","native GDI font: "+c.GetType()+" "+face);}finally{SelectObject(dc,old);ReleaseDC(c.Handle,dc);}}
 static void Fonts(Control c,Control exception){if(c!=exception)Check(c.Font.Name=="Source Sans 3","font: "+c.GetType()+" "+c.Text+" "+c.Font.Name);if(c is ComboBox||c is NumericUpDown||c is DataGridView)return;foreach(Control child in c.Controls)Fonts(child,exception);}
 [STAThread]static void Main(string[] a){dir=Path.GetFullPath(a[0]);Directory.CreateDirectory(dir);try{Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Control.CheckForIllegalCrossThreadCalls=true;Pure();PrecisionCanvas();Ui();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+assertions+" assertions: nearest X/Y/canvas/edges/centres, pixel tolerance, all element kinds, zoom and scroll, mouse-up/Esc/capture clear guides, resize unsnapped, cached scene, model and save exact coordinates, Source Sans 3 in managed and native GDI controls, menus/dialogs/tooltips, preserved header and label fonts, languages/restart.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
 static void Pure(){var target=new ElementEdit{Index=1,X=41.12345,Y=37.98765,Width=12,Height=9};var targets=new List<ElementEdit>{target};foreach(double scale in new[]{.5,1,2,4,8,15})foreach(double anchor in new[]{0d,.5,1}){var move=new ElementEdit{Index=0,Width=7,Height=5};move.X=target.X+anchor*(target.Width-move.Width)+3/scale;move.Y=target.Y+anchor*(target.Height-move.Height)-3/scale;var result=SmartGuides.CalculateSnap(move,targets,200,160,scale,scale); // Verify chosen line matches an exact anchor, even where several fall inside tolerance.
 Check(result.X!=null&&result.Y!=null,"both axes");bool x=false,y=false;foreach(double m in new[]{0d,.5,1})foreach(double t in new[]{0d,.5,1}){x|=Math.Abs(result.Element.X+m*move.Width-target.X-t*target.Width)<1e-9;y|=Math.Abs(result.Element.Y+m*move.Height-target.Y-t*target.Height)<1e-9;}Check(x&&y,"exact target anchors");}
 var empty=new List<ElementEdit>();var c=SmartGuides.CalculateSnap(new ElementEdit{Index=0,X=47.9,Y=37.2,Width=4,Height=6},empty,100,80,10,10);Equal(c.Element.X,48,"canvas centre X");Equal(c.Element.Y,37,"canvas centre Y");
 foreach(double edge in new[]{0d,100}){var m=new ElementEdit{Index=0,X=edge==0?.5:95.5,Y=12.3,Width=5,Height=7};var r=SmartGuides.CalculateSnap(m,empty,100,80,10,10);Equal(r.Element.X,edge==0?0:95,"canvas edge");Check(r.Y==null,"independent Y");}
 var six=new ElementEdit{Index=0,X=.6,Y=13.7,Width=5,Height=7};Check(SmartGuides.CalculateSnap(six,empty,100,80,10,10).X!=null,"6px inclusive");six.X=.601;Check(SmartGuides.CalculateSnap(six,empty,100,80,10,10).X==null,"outside tolerance");
 var self=new List<ElementEdit>{new ElementEdit{Index=0,X=18,Y=19,Width=5,Height=7}};var no=SmartGuides.CalculateSnap(new ElementEdit{Index=0,X=18.1,Y=19.1,Width=5,Height=7},self,100,80,10,10);Check(no.X==null&&no.Y==null,"self excluded");
 var many=new List<ElementEdit>();for(int i=0;i<70;i++)many.Add(new ElementEdit{Index=i+1,X=i*.73,Y=i*.41,Width=12,Height=9});var sw=Stopwatch.StartNew();for(int i=0;i<10000;i++)SmartGuides.CalculateSnap(new ElementEdit{Index=0,X=i%50,Y=i%37,Width=7,Height=5},many,100,80,6,6);sw.Stop();File.WriteAllText(Path.Combine(dir,"snap-performance.txt"),"10000 calculations, 70 targets: "+sw.Elapsed.TotalMilliseconds+" ms");}
 sealed class BypassPreview:PreviewPanel {
  public bool Alt;
  protected override bool BypassSnapping { get { return Alt; } }
  public void Pointer(Point from,Point to){
   OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,from.X,from.Y,0));
   OnMouseMove(new MouseEventArgs(MouseButtons.Left,1,to.X,to.Y,0));
   Frame();
  }
  public void Frame(){typeof(PreviewPanel).GetMethod("FlushDragMove",flags).Invoke(this,null);}
  public void Release(Point to){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,to.X,to.Y,0));}
 }
 static void MomentaryBypass(){
  using(var form=new Form{Size=new Size(850,640),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
  using(var p=new BypassPreview{Compact=true,Dock=DockStyle.Fill,Image=new Bitmap(1,1),Settings=LabelSettings.Default()}){
   p.Settings.HideBarcode=true;p.Settings.Width=100;p.Settings.Height=80;p.Settings.Texts.Clear();
   p.Settings.Texts.Add(new TextItem{X=10,Y=20,Width=8,Height=8});
   p.ElementEdited+=edit=>{p.Settings.Texts[0].X=edit.X;p.Settings.Texts[0].Y=edit.Y;};
   form.Controls.Add(p);form.Show();p.SelectElement(0);
   var from=Point.Round(p.ScreenPoint(new PointF(14,24)));
   float scale=p.LabelRectangle().Width/100f;
   var to=new Point(from.X-(int)Math.Round(10*scale)+2,from.Y);
   p.Pointer(from,to);
   Equal(p.Element(0).X,0,"regular drag snaps to the edge");
   p.Alt=true;p.Frame();
   Equal(p.Element(0).X,10+(double)((to.X-from.X)/scale),"pressing Alt frees the position without another mouse message");
   p.Alt=false;p.Frame();
   Equal(p.Element(0).X,0,"releasing Alt restores snapping");
   p.Alt=true;p.Release(to);
   Equal(p.Element(0).X,10+(double)((to.X-from.X)/scale),"releasing the mouse with Alt preserves the unsnapped position");
  }
 }
 static void PrecisionCanvas(){
  var targets=new List<ElementEdit>{new ElementEdit{Index=1,X=20,Y=30,Width=8,Height=8}};
  var moving=new ElementEdit{Index=0,X=20.25,Y=10,Width=8,Height=8};
  var free=SmartGuides.CalculateSnap(moving,targets,100,80,10,10,false);
  Equal(free.Element.X,moving.X,"disabled snapping leaves pointer position");
  Check(free.X!=null&&free.X.Position==24,"disabled snapping still shows the nearby guide");
  var limitTargets=new List<ElementEdit>{new ElementEdit{Index=1,X=-505,Y=8,Width=6,Height=4},
   new ElementEdit{Index=2,X=-505.5,Y=8,Width=6,Height=4}};
  var outside=new ElementEdit{Index=0,X=-505.1,Y=20,Width=6,Height=4};
  var feasible=SmartGuides.CalculateSnap(outside,limitTargets,100,80,10,10,true,6,(x,step)=>!x||step<=0);
  Equal(feasible.Element.X,-505.5,"guide search skips the nearest unavailable correction of a rotated group");
  var file=Path.Combine(dir,"precision.xml");
  using(var f=new MainForm(file)){
   f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();
   var p=Field<PreviewPanel>(f,"preview");
   var menu=Field<ContextMenuStrip>(f,"viewMenu");
   var rulers=(ToolStripMenuItem)menu.Items["canvasRulers"];
   var snapping=(ToolStripMenuItem)menu.Items["canvasSnapping"];
   Check(rulers.Checked&&snapping.Checked,"canvas preferences initially enabled");
   var s=LabelSettings.Default();s.Width=100;s.Height=80;s.Unit="cm";s.HideBarcode=true;s.Texts.Clear();
   s.Texts.Add(new TextItem{Text="A",X=10,Y=15,Width=8,Height=6,Size=10});
   s.Texts.Add(new TextItem{Text="B",X=30,Y=28,Width=8,Height=6,Size=10,Rotation=90});
   foreach(double zoom in new[]{0d,2d}){
    Call(f,"Apply",s);Call(f,"ResetHistory",true);p.ViewZoom=zoom;
    if(zoom!=0)p.AutoScrollPosition=new Point(22,18);
    p.SelectMany(new[]{0,1},true);
    var from=Point.Round(p.ScreenPoint(new PointF(14,18)));
    var canvas=p.LabelRectangle();
    double sx=canvas.Width/s.Width;
    var to=new Point(from.X+(int)Math.Round(63*sx)-2,from.Y);
    Mouse(p,"OnMouseDown",from);Mouse(p,"OnMouseMove",to);
    Check(p.IsDragging,"selected group is dragged");
    Equal(p.Element(0).X,73,"rotated outer group edge snaps to paper");
    Equal(p.Element(1).X-p.Element(0).X,20,"group spacing stays fixed");
    var snap=Field<SnapResult>(p,"snap");
    Check(snap.X!=null&&snap.X.Position==100,"right guide marks the label edge");
    var caption=(string)Call(p,"EdgeDistances",new RectangleF(73,15,27,20));
    Check(caption.Contains(7.3.ToString("0.###")+" cm")&&caption.Contains("0 cm"),"distances use project units");
    if(zoom==0)Snap(f,"precision-rulers-and-distances");
    Mouse(p,"OnMouseUp",to);
    Call(f,"StepHistory",false);
    Equal(p.Element(0).X,10,"one undo restores group origin");
    Equal(p.Element(1).X,30,"one undo restores other member");
   }
   snapping.PerformClick();
   p.ViewZoom=0;Call(f,"Apply",s);p.SelectMany(new[]{0,1},true);
   var start=Point.Round(p.ScreenPoint(new PointF(14,18)));
   double scale=p.LabelRectangle().Width/s.Width;
   var end=new Point(start.X+(int)Math.Round(63*scale)-2,start.Y);
   Mouse(p,"OnMouseDown",start);Mouse(p,"OnMouseMove",end);
   Equal(p.Element(0).X,10+(double)((end.X-start.X)/(float)scale),"free group movement follows the cursor");
   Check(Field<SnapResult>(p,"snap").X!=null,"free drag retains guide");
   Call(p,"ProcessCmdKey",new Message(),Keys.Escape);
   Equal(p.Element(0).X,10,"Escape restores group");
   p.ViewZoom=2;
   var edge=ProjectXml.Clone(s);edge.Texts[0].Y=0;
   Call(f,"Apply",edge);p.SelectElement(0);
   var rotation=Point.Round(p.RotationHandle());
   Check(!rotation.IsEmpty&&(int)Call(p,"HandleAt",rotation)==8,"rotation handle remains visible at the top ruler");
   Mouse(p,"OnMouseDown",rotation);
   Check(p.IsDragging&&Field<int>(p,"handle")==8,"mouse can still reach the rotation handle");
   Call(p,"ProcessCmdKey",new Message(),Keys.Escape);
   var screen=p.PointToScreen(new Point(p.ClientSize.Width/2,5));
   var coordinates=new IntPtr((screen.X&0xffff)|((screen.Y&0xffff)<<16));
   Call(p,"WndProc",Message.Create(p.Handle,0x007b,IntPtr.Zero,coordinates));
   Check(!p.ContextMenuStrip.Visible&&p.SelectedIndex==0,"right click on a ruler leaves the canvas menu closed");
   rulers.PerformClick();
   Check(!p.ShowRulers&&!p.SnapToGuides,"menu controls preview aids");
   Call(f,"Apply",s);f.Close();
  }
  using(var f=new MainForm(file)){
   var p=Field<PreviewPanel>(f,"preview");
   Check(!p.ShowRulers&&!p.SnapToGuides,"canvas preferences survive restart");
  }
  MomentaryBypass();
 }
 static void Ui(){using(var f=new MainForm(Path.Combine(dir,"preferences.xml"))){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();f.Size=new Size(1280,720);Label header=null;Check(f.FormBorderStyle==FormBorderStyle.Sizable&&f.Text.StartsWith("ND LabelDesigner"),"native system title retained");Fonts(f,header);NativeFont(Field<TextBox>(f,"ean"));NativeFont(Field<ComboBox>(f,"kind"));foreach(Control child in Field<Dictionary<string,NumericUpDown>>(f,"nums")["Width"].Controls)if(child is TextBox)NativeFont(child);var menu=Field<ContextMenuStrip>(f,"exportMenu");Check(menu.Font.Name=="Source Sans 3","menu font");foreach(ToolStripItem item in menu.Items)Check(item.Font.Name=="Source Sans 3","menu item");
 var p=Field<PreviewPanel>(f,"preview");foreach(double zoom in new[]{0d,.5,1,2})foreach(int index in new[]{0,-1,-3}){var s=LabelSettings.Default();s.Width=100;s.Height=80;s.BarX=50;s.BarY=48;s.BarWidth=30;s.Texts[0].X=7;s.Texts[0].Y=9;s.Texts[0].Width=18;s.Texts[0].Height=7;s.Texts.Add(new TextItem{Text="Target",X=31.12345,Y=30.98765,Width=22,Height=12,Size=11});s.Symbols.Add(new SymbolItem{X=9,Y=50,Width=9,Height=12});Call(f,"Apply",s);p.ViewZoom=zoom;if(zoom==2)p.AutoScrollPosition=new Point(25,30);p.SelectElement(index);var element=p.Element(index);var r=p.ElementRectangle(index);var from=new Point((int)(r.X+r.Width*(index<=-3?.5:.25)),(int)(r.Y+r.Height*.5));Mouse(p,"OnMouseDown",from);var canvas=p.LabelRectangle();double sx=canvas.Width/s.Width,sy=canvas.Height/s.Height;var to=new Point(from.X+(int)Math.Round((31.12345-element.X)*sx)+2,from.Y+(int)Math.Round((30.98765-element.Y)*sy)+2);var before=p.Scene;Mouse(p,"OnMouseMove",to);var snap=Field<SnapResult>(p,"snap");Check(snap!=null&&snap.X!=null&&snap.Y!=null,"visible snap guides "+zoom+" "+index);Equal(p.Element(index).X,snap.Element.X,"model exact X");Equal(p.Element(index).Y,snap.Element.Y,"model exact Y");Check(object.ReferenceEquals(before,p.Scene),"move reuses scene");if(zoom==0&&index==0)Snap(f,"guides-both-axes");Mouse(p,"OnMouseUp",to);Check(Field<SnapResult>(p,"snap")==null,"release clears");var settings=(LabelSettings)Call(f,"Read");string path=Path.Combine(dir,"snapped.xml");Engine.Save(path,settings);var loaded=Engine.Load(path);if(index==0)Equal(loaded.Texts[0].X,p.Element(index).X,"save exact snapped coordinate");p.SelectElement(index);r=p.ElementRectangle(index);from=new Point((int)(r.X+r.Width*(index<=-3?.5:.25)),(int)(r.Y+r.Height*.5));var original=p.Element(index);Mouse(p,"OnMouseDown",from);Mouse(p,"OnMouseMove",new Point(from.X+3,from.Y+4));Call(p,"ProcessCmdKey",new Message(),Keys.Escape);Equal(p.Element(index).X,original.X,"Esc exact original X");Check(Field<SnapResult>(p,"snap")==null,"Esc clears");}
 Call(f,"Apply",LabelSettings.Default());p.ViewZoom=0;p.SelectElement(0);var h=p.Handles()[4];Mouse(p,"OnMouseDown",Point.Round(h));Mouse(p,"OnMouseMove",new Point((int)h.X+15,(int)h.Y+8));Check(Field<SnapResult>(p,"snap")==null,"resize does not snap");Mouse(p,"OnMouseUp",Point.Round(h));
 Call(f,"Apply",LabelSettings.Default());var rr=p.ElementRectangle(0);var start=new Point((int)(rr.Left+rr.Width*.3),(int)(rr.Top+rr.Height*.5));Mouse(p,"OnMouseDown",start);Mouse(p,"OnMouseMove",new Point(start.X+1,start.Y+1));p.Capture=false;Check(Field<SnapResult>(p,"snap")==null,"capture loss clears");
 foreach(string lang in Loc.Codes){Field<ComboBox>(f,"language").SelectedIndex=Array.IndexOf(Loc.Codes,lang);Snap(f,"editor-"+lang);Fonts(f,header);}Field<ComboBox>(f,"language").SelectedIndex=0;Call(f,"ToggleTheme");Snap(f,"editor-light");Call(f,"ToggleTheme");
 using(var dialog=UiMessageBox.Create(f,"Skúška hlásenia: ľščťžýáíé Őű", "ND LabelDesigner",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning)){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();Fonts(dialog,null);Snap(dialog,"message");dialog.Close();}
 using(var tip=new UiToolTip()){Check(tip.OwnerDraw,"tooltip uses app font renderer");}
 // Existing text-edit dialog must inherit the UI family without changing label font.
 var close=new Timer{Interval=100};bool checkedDialog=false;close.Tick+=delegate{close.Stop();foreach(Form d in Application.OpenForms)if(d!=f){Fonts(d,null);Snap(d,"text-dialog");checkedDialog=true;d.Close();break;}};close.Start();Call(f,"EditTextDialog",0);close.Dispose();Check(checkedDialog,"text dialog");Check(((LabelSettings)Call(f,"Read")).Texts[0].FontName=="Arial","label font preserved");Call(f,"Apply",LabelSettings.Default());f.Close();}
 using(var restart=new MainForm(Path.Combine(dir,"preferences.xml"))){Check(restart.Font.Name=="Source Sans 3","restart font");}}
}
