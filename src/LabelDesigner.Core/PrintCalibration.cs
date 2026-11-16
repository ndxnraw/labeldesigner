using System;using System.Drawing;using System.Globalization;using System.Text;
namespace EanEtikety {
// Physical output correction only. Document coordinates and exports stay intact.
public sealed class PrintCalibration {
 public double X,Y,ScaleX=1,ScaleY=1;
 public void Validate(){if(!Finite(X)||!Finite(Y)||Math.Abs(X)>100||Math.Abs(Y)>100||!Finite(ScaleX)||!Finite(ScaleY)||ScaleX<.8||ScaleX>1.2||ScaleY<.8||ScaleY>1.2)throw new ArgumentException(Loc.T("Neplatná kalibrácia: posun ±100 mm, mierka 80–120 %."));}
 static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
 public RectangleF Transform(RectangleF bounds){Validate();return new RectangleF((float)(bounds.X*ScaleX+X),(float)(bounds.Y*ScaleY+Y),(float)(bounds.Width*ScaleX),(float)(bounds.Height*ScaleY));}
 public static PrintCalibration FromMeasurements(CalibrationTarget target,double measuredX,double measuredY,double measuredWidth,double measuredHeight){
  if(target==null)throw new ArgumentNullException("target");if(!Finite(measuredWidth)||!Finite(measuredHeight)||measuredWidth<=0||measuredHeight<=0)throw new ArgumentException(Loc.T("Namerané vzdialenosti musia byť kladné."));
  var c=new PrintCalibration{ScaleX=target.SpanX/measuredWidth,ScaleY=target.SpanY/measuredHeight};c.X=target.AnchorX-c.ScaleX*measuredX;c.Y=target.AnchorY-c.ScaleY*measuredY;c.Validate();return c;
 }
}
public sealed class CalibrationTarget {
 public LabelSettings Label{get;private set;}public double AnchorX{get;private set;}public double AnchorY{get;private set;}public double SpanX{get;private set;}public double SpanY{get;private set;}
 static string N(double n){return n.ToString("0.###",CultureInfo.InvariantCulture);}
 public static CalibrationTarget Create(double width,double height){
  if(double.IsNaN(width)||double.IsNaN(height)||double.IsInfinity(width)||double.IsInfinity(height)||width<12||height<12||width>2000||height>2000)throw new ArgumentException(Loc.T("Kalibračná strana musí mať rozmery 12–2000 mm."));
  var t=new CalibrationTarget{AnchorX=Math.Min(5,width/5),AnchorY=Math.Min(5,height/5)};t.SpanX=Math.Min(100,width-2*t.AnchorX);t.SpanY=Math.Min(100,height-2*t.AnchorY);
  var label=LabelSettings.Default();label.Width=width;label.Height=height;label.HideBarcode=true;label.Texts.Clear();label.Symbols.Clear();
  var path=new StringBuilder();foreach(var p in new[]{new PointF((float)t.AnchorX,(float)t.AnchorY),new PointF((float)(t.AnchorX+t.SpanX),(float)t.AnchorY),new PointF((float)t.AnchorX,(float)(t.AnchorY+t.SpanY))}){path.Append("M"+N(p.X-1)+" "+N(p.Y)+" h2 M"+N(p.X)+" "+N(p.Y-1)+" v2 ");}
  string svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 "+N(width)+" "+N(height)+"'><path d='"+path+"' fill='none' stroke='black' stroke-width='0.2'/></svg>";
  label.Symbols.Add(new SymbolItem{X=0,Y=0,Width=width,Height=height,Image=new ImageContent{Mime="image/svg+xml",Data=Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)),PixelWidth=(int)Math.Ceiling(width),PixelHeight=(int)Math.Ceiling(height)}});
  if(width>=35&&height>=25)label.Texts.Add(new TextItem{Text="X: "+N(t.SpanX)+" mm\nY: "+N(t.SpanY)+" mm",X=t.AnchorX+3,Y=t.AnchorY+3,Width=width-t.AnchorX-5,Height=Math.Min(15,height-t.AnchorY-5),Size=8});
  t.Label=label;return t;
 }
}
}
