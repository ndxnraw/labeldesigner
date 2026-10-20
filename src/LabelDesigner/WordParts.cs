using System;using System.IO;using System.Text;using System.Drawing;using System.Drawing.Imaging;using System.Collections.Generic;using System.Globalization;
namespace EanEtikety {
public class WordPart {public int Number;public string Svg;public byte[] Png;public RectangleF Bounds;}
public static class WordParts {
 static string F(double x){return x.ToString("0.#####",CultureInfo.InvariantCulture);}
 public static List<WordPart> Create(LabelSettings s){using(var scene=VectorArt.Build(s))return CreateScene(scene);}
 public static List<WordPart> CreateScene(VectorScene original){var result=new List<WordPart>();var keys=new List<int>();foreach(var shape in original.Shapes)if(!keys.Contains(shape.Element))keys.Add(shape.Element);
  foreach(int key in keys){using(var part=new VectorScene{Width=original.Width,Height=original.Height}){RectangleF bounds=RectangleF.Empty;foreach(var shape in original.Shapes)if(shape.Element==key&&shape.Path.PointCount>0){var clip=shape.Clip;RectangleF area;using(var transformed=(System.Drawing.Drawing2D.GraphicsPath)shape.Path.Clone()){using(var m=ElementTransform.Matrix(shape.Rotation,shape.Pivot))transformed.Transform(m);area=RectangleF.Intersect(ElementTransform.Bounds(clip,shape.Rotation,shape.Pivot),transformed.GetBounds());}if(shape.CanvasClip.HasValue)area=RectangleF.Intersect(area,shape.CanvasClip.Value);area=RectangleF.Intersect(area,new RectangleF(0,0,(float)original.Width,(float)original.Height));if(area.Width<=0||area.Height<=0)continue;bounds=bounds.IsEmpty?area:RectangleF.Union(bounds,area);part.Shapes.Add(shape.Copy());}if(bounds.IsEmpty)continue;
   string svg=SceneOutput.Svg(part,bounds);
   double dpi=Math.Min(600,Math.Sqrt(4000000*25.4*25.4/(bounds.Width*bounds.Height))),k=dpi/25.4;using(var bmp=new Bitmap(Math.Max(1,(int)Math.Ceiling(bounds.Width*k)),Math.Max(1,(int)Math.Ceiling(bounds.Height*k)),PixelFormat.Format32bppArgb)){bmp.SetResolution((float)dpi,(float)dpi);using(var g=Graphics.FromImage(bmp)){g.Clear(Color.Transparent);part.Draw(g,new RectangleF((float)(-bounds.X*k),(float)(-bounds.Y*k),(float)(original.Width*k),(float)(original.Height*k)),false);}using(var ms=new MemoryStream()){bmp.Save(ms,ImageFormat.Png);result.Add(new WordPart{Number=result.Count+1,Bounds=bounds,Svg=svg.ToString(),Png=ms.ToArray()});}}
  }}
 return result;}
}
}
