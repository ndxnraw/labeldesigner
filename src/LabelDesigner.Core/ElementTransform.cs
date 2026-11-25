using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Globalization;
namespace EanEtikety {
// All element geometry remains in millimetres. Rotation never rewrites document X/Y.
public static class ElementTransform {
 public static double Normalize(double angle){if(double.IsNaN(angle)||double.IsInfinity(angle))throw new ArgumentOutOfRangeException("angle");return (angle%360+360)%360;}
 public const double RightAngleSnapTolerance=3;
 public static double SnapAngle(double angle,bool shift){
  double normalized=Normalize(angle);
  if(shift)return Normalize(Math.Round(angle/15,MidpointRounding.AwayFromZero)*15);
  double nearest=Math.Round(normalized/90,MidpointRounding.AwayFromZero)*90;
  return Math.Abs(normalized-nearest)<=RightAngleSnapTolerance?Normalize(nearest):normalized;
 }
 public static ElementEdit Get(LabelSettings s,int index){if(s==null)return null;if(index==-1&&s.HideBarcode)return null;if(index==-1)return new ElementEdit{Index=index,X=s.BarX,Y=s.BarY,Width=s.BarWidth,Height=EditorBarcodeHeight(s),Font=s.DigitSize,Rotation=s.BarRotation};if(index>=0&&index<s.Texts.Count){var t=s.Texts[index];return new ElementEdit{Index=index,X=t.X,Y=t.Y,Width=t.Width,Height=t.Height,Font=t.Size,Rotation=t.Rotation,AutoWidth=t.AutoWidth};}if(index<=-3&&-3-index<s.Symbols.Count){var t=s.Symbols[-3-index];return new ElementEdit{Index=index,X=t.X,Y=t.Y,Width=t.Width,Height=t.Height,Font=10,Rotation=t.Rotation};}return null;}
 static double EditorBarcodeHeight(LabelSettings s){try{return Engine.BarcodeHeight(s);}catch{return s.BarHeight+(s.Digits?s.DigitSize*25.4/72*1.5:0);}}
 public static PointF Center(ElementEdit e){return new PointF((float)(e.X+e.Width/2),(float)(e.Y+e.Height/2));}
 public static Matrix Matrix(double rotation,PointF center){var m=new Matrix();m.RotateAt((float)rotation,center);return m;}
 public static PointF Rotate(PointF point,PointF center,double angle){double a=angle*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),x=point.X-center.X,y=point.Y-center.Y;return new PointF((float)(center.X+x*c-y*s),(float)(center.Y+x*s+y*c));}
 public static PointF Vector(double x,double y,double angle){return Rotate(new PointF((float)x,(float)y),PointF.Empty,angle);}
 public static RectangleF LocalBounds(ElementEdit e){return new RectangleF((float)e.X,(float)e.Y,(float)e.Width,(float)e.Height);}
 public static PointF[] Corners(ElementEdit e){var b=LocalBounds(e);var a=new[]{new PointF(b.Left,b.Top),new PointF(b.Right,b.Top),new PointF(b.Right,b.Bottom),new PointF(b.Left,b.Bottom)};using(var m=Matrix(e.Rotation,Center(e)))m.TransformPoints(a);return a;}
 public static RectangleF Bounds(ElementEdit e){double a=e.Rotation*Math.PI/180,c=Math.Abs(Math.Cos(a)),s=Math.Abs(Math.Sin(a)),w=e.Width*c+e.Height*s,h=e.Width*s+e.Height*c;return new RectangleF((float)(e.X+(e.Width-w)/2),(float)(e.Y+(e.Height-h)/2),(float)w,(float)h);}
 public static bool Contains(ElementEdit e,PointF canvas){return e!=null&&LocalBounds(e).Contains(Rotate(canvas,Center(e),-e.Rotation));}
 public static bool ContainsShape(ElementEdit element,ShapeContent shape,PointF canvas,float tolerance){
  var bounds=LocalBounds(element);
  var point=Rotate(canvas,Center(element),-element.Rotation);
  if(shape.IsLine||shape.Fill==ShapeFill.Transparent)bounds.Inflate(tolerance,tolerance);
  if(!bounds.Contains(point))return false;
  if(shape.IsLine||shape.Fill!=ShapeFill.Transparent)return true;
  float inset=(float)shape.Stroke+2*tolerance;
  if(inset*2>=Math.Min(bounds.Width,bounds.Height))return true;
  bounds.Inflate(-inset,-inset);
  return !bounds.Contains(point);
 }
 public static bool Outside(LabelSettings s,ElementEdit e){if(e==null)return false;var b=Bounds(e);return Engine.Outside(s,b.X,b.Y,b.Width,b.Height);}
 public static RectangleF Bounds(RectangleF r,double rotation,PointF center){using(var p=new GraphicsPath()){p.AddRectangle(r);using(var m=Matrix(rotation,center))p.Transform(m);return p.GetBounds();}}
 public static string Number(double n){return n.ToString("0.########",CultureInfo.InvariantCulture);}
 public static string Svg(ArtShape shape){return "rotate("+Number(shape.Rotation)+" "+Number(shape.Pivot.X)+" "+Number(shape.Pivot.Y)+")";}
 public static string Pdf(ArtShape shape){using(var m=Matrix(shape.Rotation,shape.Pivot)){var a=m.Elements;return string.Join(" ",Array.ConvertAll(a,x=>Number(x)))+" cm\n";}}
}
}
