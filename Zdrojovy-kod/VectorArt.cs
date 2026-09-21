using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Globalization;

namespace EanEtikety {
public class ArtShape:IDisposable {public GraphicsPath Path;public RectangleF Clip;public bool Bars;public int Element=-1;public void Dispose(){Path.Dispose();}}
public class VectorScene:IDisposable {
 public double Width,Height;public List<ArtShape> Shapes=new List<ArtShape>();
 public void Draw(Graphics g,RectangleF destination,bool monochrome){var saved=g.Save();g.SetClip(destination,CombineMode.Intersect);g.TranslateTransform(destination.X,destination.Y);g.ScaleTransform(destination.Width/(float)Width,destination.Height/(float)Height);foreach(var item in Shapes){var clip=g.Save();g.SetClip(item.Clip,CombineMode.Intersect);g.SmoothingMode=monochrome||item.Bars?SmoothingMode.None:SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.Default;g.FillPath(Brushes.Black,item.Path);g.Restore(clip);}g.Restore(saved);}
 public void TranslateElement(int index,float dx,float dy){using(var matrix=new Matrix()){matrix.Translate(dx,dy);foreach(var shape in Shapes)if(shape.Element==index){shape.Path.Transform(matrix);if(!shape.Bars){var clip=shape.Clip;clip.Offset(dx,dy);shape.Clip=clip;}}}}
 public void Dispose(){foreach(var item in Shapes)item.Dispose();Shapes.Clear();}
}
public static class VectorArt {
 public static FontFamily Family(string name){try{return new FontFamily(string.IsNullOrWhiteSpace(name)?"Arial":name);}catch{return new FontFamily("Arial");}}
 public static FontStyle Style(FontFamily family,bool bold){var desired=bold?FontStyle.Bold:FontStyle.Regular;if(family.IsStyleAvailable(desired))return desired;foreach(var fallback in new[]{FontStyle.Regular,FontStyle.Bold,FontStyle.Italic,FontStyle.Bold|FontStyle.Italic})if(family.IsStyleAvailable(fallback))return fallback;return FontStyle.Regular;}
 static RectangleF Rect(double x,double y,double w,double h){return new RectangleF((float)x,(float)y,(float)w,(float)h);}
 static void Text(VectorScene scene,string text,string family,double size,bool bold,bool center,RectangleF bounds,bool nowrap,int element=-1){if(string.IsNullOrEmpty(text))return;var path=new GraphicsPath();try{using(var ff=Family(family))using(var fmt=new StringFormat(StringFormat.GenericTypographic)){fmt.Alignment=center?StringAlignment.Center:StringAlignment.Near;fmt.Trimming=StringTrimming.None;if(nowrap)fmt.FormatFlags|=StringFormatFlags.NoWrap;path.AddString(text,ff,(int)Style(ff,bold),(float)(size*25.4/72),bounds,fmt);}scene.Shapes.Add(new ArtShape{Path=path,Clip=bounds,Element=element});}catch{path.Dispose();throw;}}
 public static VectorScene Build(LabelSettings s){Engine.Validate(s);var scene=new VectorScene{Width=s.Width,Height=s.Height};try{
  for(int i=0;i<s.Texts.Count;i++){var t=s.Texts[i];Text(scene,t.Text,t.FontName,t.Size,t.Bold,t.Center,Rect(t.X,t.Y,t.Width,t.Height),false,i);}
  double k=s.Dpi/25.4;int m=Engine.Module(s),total=Engine.Modules(s),quiet=Engine.Quiet(s.Kind);string bits=Engine.CodeBits(s),value=Engine.Caption(s);
  int x=(int)Math.Round(s.BarX*k)+(int)Math.Round((s.BarWidth*k-total*m)/2),y=(int)Math.Round(s.BarY*k),h=(int)Math.Round(s.BarHeight*k),bearer=s.Kind==14?5*m:0;
  var bars=new GraphicsPath();scene.Shapes.Add(new ArtShape{Path=bars,Clip=Rect(0,0,s.Width,s.Height),Bars=true});
  if(bearer>0){bars.AddRectangle(Rect(x/k,y/k,total*m/k,bearer/k));bars.AddRectangle(Rect(x/k,(y+bearer+h)/k,total*m/k,bearer/k));y+=bearer;}
  int guard=s.Digits?Math.Min(5*m,(int)(s.DigitSize*s.Dpi/72*.45)):0;
  for(int i=0;i<bits.Length;i++)if(bits[i]=='1'){bool retail=s.Kind==13||s.Kind==8||s.Kind==12;bool special=retail&&(i<3||i>=bits.Length-3||(i>=(s.Kind==8?31:45)&&i<(s.Kind==8?36:50))||(s.Kind==12&&(i<10||i>=85)));bars.AddRectangle(Rect((x+(quiet+i)*m)/k,y/k,m/k,(h+(special?guard:0))/k));}
  y+=bearer;double captionY=(y+h)/k,captionH=s.DigitSize*25.4/72*1.5;
  Action<string,int,int> digits=delegate(string text,int xx,int width){Text(scene,text,"Arial",s.DigitSize,false,true,Rect(xx/k,captionY,width/k,captionH),true);};
  if(s.Digits){if(s.Kind==13){digits(value.Substring(0,1),x,10*m);digits(value.Substring(1,6),x+14*m,42*m);digits(value.Substring(7,6),x+61*m,42*m);}else if(s.Kind==8){digits(value.Substring(0,4),x+10*m,28*m);digits(value.Substring(4,4),x+43*m,28*m);}else if(s.Kind==12){digits(value.Substring(0,1),x,8*m);digits(value.Substring(1,5),x+19*m,35*m);digits(value.Substring(6,5),x+59*m,35*m);digits(value.Substring(11,1),x+105*m,8*m);}else digits(value,x,total*m);}
  for(int i=0;i<s.Symbols.Count;i++){var symbol=s.Symbols[i];var path=DepositArt.Create();var bounds=path.GetBounds();using(var matrix=new Matrix((float)(symbol.Width/bounds.Width),0,0,(float)(symbol.Height/bounds.Height),(float)symbol.X,(float)symbol.Y))path.Transform(matrix);scene.Shapes.Add(new ArtShape{Path=path,Clip=Rect(symbol.X,symbol.Y,symbol.Width,symbol.Height),Element=-3-i});}
  return scene;
 }catch{scene.Dispose();throw;}}
 public static Bitmap Raster(LabelSettings s,double dpi,bool mono){Engine.Validate(s);double pixels=s.Width*s.Height*dpi*dpi/(25.4*25.4);if(pixels>40000000)dpi=Math.Sqrt(40000000*25.4*25.4/(s.Width*s.Height));int w=Math.Max(1,(int)Math.Round(s.Width*dpi/25.4)),h=Math.Max(1,(int)Math.Round(s.Height*dpi/25.4));var bitmap=new Bitmap(w,h,PixelFormat.Format24bppRgb);bitmap.SetResolution((float)dpi,(float)dpi);try{using(var g=Graphics.FromImage(bitmap))using(var scene=Build(s)){g.Clear(Color.White);scene.Draw(g,new RectangleF(0,0,(float)(s.Width*dpi/25.4),(float)(s.Height*dpi/25.4)),mono);}return bitmap;}catch{bitmap.Dispose();throw;}}
 static string F(double v){return v.ToString("0.#####",CultureInfo.InvariantCulture);}
 public static string PathData(GraphicsPath path,bool svg){var b=new StringBuilder();var pts=path.PathPoints;var types=path.PathTypes;for(int i=0;i<pts.Length;i++){int type=types[i]&7;if(type==0)b.Append(svg?"M":"").Append(F(pts[i].X)).Append(' ').Append(F(pts[i].Y)).Append(svg?" ":" m\n");else if(type==1)b.Append(svg?"L":"").Append(F(pts[i].X)).Append(' ').Append(F(pts[i].Y)).Append(svg?" ":" l\n");else if(type==3&&i+2<pts.Length){b.Append(svg?"C":"");for(int j=0;j<3;j++)b.Append(F(pts[i+j].X)).Append(' ').Append(F(pts[i+j].Y)).Append(' ');b.Append(svg?"":"c\n");i+=2;}if((types[i]&128)!=0)b.Append(svg?"Z ":"h\n");}return b.ToString();}
 public static string Svg(LabelSettings s){using(var scene=Build(s)){var b=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\""+F(s.Width)+"mm\" height=\""+F(s.Height)+"mm\" viewBox=\"0 0 "+F(s.Width)+" "+F(s.Height)+"\"><defs><clipPath id=\"label\"><rect width=\""+F(s.Width)+"\" height=\""+F(s.Height)+"\"/></clipPath>");for(int i=0;i<scene.Shapes.Count;i++){var c=scene.Shapes[i].Clip;b.Append("<clipPath id=\"c"+i+"\"><rect x=\""+F(c.X)+"\" y=\""+F(c.Y)+"\" width=\""+F(c.Width)+"\" height=\""+F(c.Height)+"\"/></clipPath>");}b.Append("</defs><g clip-path=\"url(#label)\" fill=\"black\" fill-rule=\"evenodd\">");for(int i=0;i<scene.Shapes.Count;i++)if(scene.Shapes[i].Path.PointCount>0)b.Append("<path clip-path=\"url(#c"+i+")\" d=\""+PathData(scene.Shapes[i].Path,true)+"\"/>");return b.Append("</g></svg>").ToString();}}
 static byte[] Bytes(string s){return Encoding.ASCII.GetBytes(s);}
 static byte[] Stream(string dict,string content){return Bytes("<< "+dict+" /Length "+Encoding.ASCII.GetByteCount(content)+" >>\nstream\n"+content+"\nendstream");}
 public static void Pdf(string path,LabelSettings s){Engine.Validate(s);int cap=Engine.Columns(s)*Engine.Rows(s),pages=(s.Copies+cap-1)/cap;var objects=new List<byte[]>();objects.Add(Bytes("<< /Type /Catalog /Pages 2 0 R >>"));var kids=new StringBuilder();for(int p=0;p<pages;p++)kids.Append((4+2*p)+" 0 R ");objects.Add(Bytes("<< /Type /Pages /Count "+pages+" /Kids ["+kids+"] >>"));
  var form=new StringBuilder("q 0 0 "+F(s.Width)+" "+F(s.Height)+" re W n 1 0 0 -1 0 "+F(s.Height)+" cm 0 g\n");using(var scene=Build(s))foreach(var item in scene.Shapes){if(item.Path.PointCount==0)continue;var c=item.Clip;form.Append("q "+F(c.X)+" "+F(c.Y)+" "+F(c.Width)+" "+F(c.Height)+" re W n\n"+PathData(item.Path,false)+"f* Q\n");}form.Append("Q");objects.Add(Stream("/Type /XObject /Subtype /Form /BBox [0 0 "+F(s.Width)+" "+F(s.Height)+"] /Resources << >>",form.ToString()));
  double unit=72/25.4,pw=((s.Sheet||s.UsePaper)?s.PageWidth:s.Width)*unit,ph=((s.Sheet||s.UsePaper)?s.PageHeight:s.Height)*unit;
  for(int page=0;page<pages;page++){objects.Add(Bytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 "+F(pw)+" "+F(ph)+"] /Resources << /XObject << /Label 3 0 R >> >> /Contents "+(5+page*2)+" 0 R >>"));var content=new StringBuilder();int count=Math.Min(cap,s.Copies-page*cap),cols=Engine.Columns(s);for(int i=0;i<count;i++){double x=s.UsePaper?s.MarginX:s.Sheet?s.MarginX+i%cols*(s.Width+s.GapX):0,y=s.UsePaper?s.MarginY:s.Sheet?s.MarginY+i/cols*(s.Height+s.GapY):0;content.Append("q "+F(unit)+" 0 0 "+F(unit)+" "+F(x*unit)+" "+F(ph-(y+s.Height)*unit)+" cm /Label Do Q\n");}objects.Add(Stream("",content.ToString()));}
  using(var fs=File.Create(path)){Action<byte[]> write=delegate(byte[] bytes){fs.Write(bytes,0,bytes.Length);};write(Bytes("%PDF-1.4\n"));var offsets=new List<long>();for(int i=0;i<objects.Count;i++){offsets.Add(fs.Position);write(Bytes((i+1)+" 0 obj\n"));write(objects[i]);write(Bytes("\nendobj\n"));}long xref=fs.Position;write(Bytes("xref\n0 "+(objects.Count+1)+"\n0000000000 65535 f \n"));foreach(long offset in offsets)write(Bytes(offset.ToString("D10")+" 00000 n \n"));write(Bytes("trailer\n<< /Size "+(objects.Count+1)+" /Root 1 0 R >>\nstartxref\n"+xref+"\n%%EOF\n"));}
 }
}
}
