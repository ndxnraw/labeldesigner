using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using EanEtikety;
class Revision307Tests {
 static int checks; static string dir;
 static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,Private).Invoke(target,args);}
 static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Private).GetValue(target);}
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Near(double actual,double expected,string message){Check(Math.Abs(actual-expected)<.000001,message+": "+actual+" != "+expected);}
 static void Mouse(PreviewPanel preview,string name,PointF p){Call(preview,name,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(p.X),(int)Math.Round(p.Y),0));if(name=="OnMouseMove")Call(preview,"FlushDragMove");}
 static void Maths(){
  foreach(double target in new[]{-720d,-360,-270,-180,-90,0,90,180,270,360,720}){
   foreach(double delta in new[]{-3d,-2,-.01,0,.01,2,3})Near(ElementTransform.SnapAngle(target+delta,false),ElementTransform.Normalize(target),"cardinal snap inclusive boundary/wrap");
   foreach(double delta in new[]{-3.001d,3.001,10,37})Near(ElementTransform.SnapAngle(target+delta,false),ElementTransform.Normalize(target+delta),"free rotation outside tolerance");
  }
  foreach(double angle in new[]{-361d,-92,-7.5,7.5,38,77,89,177,269,359,721})Near(ElementTransform.SnapAngle(angle,true),ElementTransform.Normalize(Math.Round(angle/15,MidpointRounding.AwayFromZero)*15),"Shift retains 15 degree increments");
  foreach(double angle in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}){bool rejected=false;try{ElementTransform.SnapAngle(angle,false);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"invalid angle rejected");}
 }
 static void Ui(){using(var form=new MainForm(Path.Combine(dir,"prefs.xml"))){
  form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Size=new Size(1440,960);form.Show();
  var preview=Field<PreviewPanel>(form,"preview");
  foreach(double zoom in new[]{0d,1,2})foreach(int index in new[]{0,-1,-3})foreach(double target in new[]{0d,90,180,270}){
   var s=LabelSettings.Default();s.Width=110;s.Height=90;s.BarX=48;s.BarY=35;s.BarWidth=45;
   s.Texts[0].X=12;s.Texts[0].Y=18;s.Texts[0].Width=30;s.Texts[0].Height=9;
   s.Symbols.Add(new SymbolItem{X=12,Y=55,Width=15,Height=15*DepositArt.Aspect});
   s.BarRotation=s.Texts[0].Rotation=s.Symbols[0].Rotation=45;
   Call(form,"Apply",s);preview.ViewZoom=zoom;preview.SelectElement(index);
   var original=preview.Element(index).Copy();var handle=preview.RotationHandle();var center=preview.ScreenPoint(ElementTransform.Center(original));
   var end=ElementTransform.Rotate(handle,center,target-45+1.5);var scene=preview.Scene;
   Mouse(preview,"OnMouseDown",handle);Mouse(preview,"OnMouseMove",end);
   Near(preview.Element(index).Rotation,target,"real mouse drag snaps at each zoom/type");
   Check(object.ReferenceEquals(scene,preview.Scene),"cached scene retained during snap");
   Call(preview,"ProcessCmdKey",new Message(),Keys.Escape);Near(preview.Element(index).Rotation,45,"Escape restores original after snap");
   Mouse(preview,"OnMouseDown",handle);Mouse(preview,"OnMouseMove",end);Mouse(preview,"OnMouseUp",end);
   Near(preview.Element(index).Rotation,target,"release retains exact snapped angle");
   Near(preview.Element(index).X,original.X,"snap preserves X");Near(preview.Element(index).Y,original.Y,"snap preserves Y");
   if(zoom==0&&index==0&&target==90)using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,form.ClientRectangle);b.Save(Path.Combine(dir,"right-angle-snap.png"));}
  }
  preview.SelectElement(0);Field<NumericUpDown>(form,"textRotation").Value=89.25m;Near(preview.Element(0).Rotation,89.25,"manual near-cardinal angle remains exact");
  var path=Path.Combine(dir,"saved.ean.xml");Engine.Save(path,(LabelSettings)Call(form,"Read"));Near(Engine.Load(path).Texts[0].Rotation,89.25,"manual angle survives save/load");
  // Even XML content disguised as .nlbl must be rejected before reading or external launching.
  foreach(string extension in new[]{".nlbl",".NLBL",".exe"}){
   var invalid=Path.Combine(dir,"unsupported"+extension);File.Copy(path,invalid,true);bool rejected=false;
   try{Call(form,"OpenImportedFile",invalid);}catch(TargetInvocationException ex){rejected=ex.InnerException is NotSupportedException;}
   Check(rejected,"unsupported extension rejected "+extension);Near(preview.Element(0).Rotation,89.25,"rejected import preserves draft");
  }
  Call(form,"OpenImportedFile",path);Near(preview.Element(0).Rotation,89.25,"XML still opens");
  var zpl=Path.Combine(dir,"editable.prn");Engine.Zpl(zpl,(LabelSettings)Call(form,"Read"));Call(form,"OpenImportedFile",zpl);Near(preview.Element(0).Rotation,89.25,"PRN still opens editable metadata");
  string source=File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot,"src","LabelDesigner","App.cs"));
  Check(source.IndexOf("nlbl",StringComparison.OrdinalIgnoreCase)<0,"open filter and help omit NLBL");
  string key="Podporované súbory (*.ean.xml;*.xml;*.zpl;*.prn)|*.ean.xml;*.xml;*.zpl;*.prn|Nastavenie etikety (*.ean.xml)|*.ean.xml|ZPL / PRN (*.zpl;*.prn)|*.zpl;*.prn|XML (*.xml)|*.xml";
  foreach(string language in Loc.Codes){Loc.Language=language;string filter=Loc.T(key);Check(filter.IndexOf("nlbl",StringComparison.OrdinalIgnoreCase)<0&&filter.Contains("*.prn"),"localized filter formats");if(language!="sk")Check(filter!=key,"filter translation exists");}Loc.Language="sk";
  Call(form,"Apply",LabelSettings.Default());form.Close();
 }}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Maths();Ui();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" checks: cardinal snapping and boundaries, wrap, Shift, actual mouse gestures at three zooms for text/barcode/symbol, Escape, exact manual angle, persistence, NLBL rejection without draft changes, XML/PRN import, localized filters.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
