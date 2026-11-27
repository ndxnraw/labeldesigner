using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EanEtikety {
 public static class FixedTextLayout {
  const double PointsPerStep = .1;

  internal static double RequiredHeight(TextItem text,double width,double dpi){
   if(string.IsNullOrWhiteSpace(text.Text))return 0;
   if(text.Styles.Count>0)return RichFieldLayout.WrappedHeight(text,width)+.05;
   using var path=new GraphicsPath();
   using var family=VectorArt.Family(text.FontName);
   using var format=new StringFormat(StringFormat.GenericTypographic){Trimming=StringTrimming.None};
   format.FormatFlags|=StringFormatFlags.NoClip;
   string value=text.Text.Replace("\r\n","\n").Replace('\r','\n');
   path.AddString(value,family,(int)VectorArt.Style(family,text.Bold),(float)(text.Size*25.4/72),
    new RectangleF(0,0,(float)width,100000),format);
   var ink=path.GetBounds();
   double inkHeight=ink.Bottom+Math.Max(0,-ink.Top);
   double lineHeight=text.MinimumFitSize>0?0:LabelChecks.MeasurePlainText(text,dpi,width).Height*25.4/dpi;
   return Math.Max(inkHeight,lineHeight)+.05;
  }

  public static double Resolve(TextItem text, out bool fits) {
   using var path = Build(text, out double size, out fits);
   return size;
  }

  internal static GraphicsPath Build(TextItem text, out double size, out bool fits) {
   size = text.Size;
   fits = true;
   string value = text.Text;
   if (string.IsNullOrWhiteSpace(value)) return new GraphicsPath();
   value = value.Replace("\r\n", "\n").Replace('\r', '\n');
   using var family = VectorArt.Family(text.FontName);
   using var format = new StringFormat(StringFormat.GenericTypographic);
   format.Alignment = text.Center ? StringAlignment.Center : StringAlignment.Near;
   format.Trimming = StringTrimming.None;
   format.FormatFlags |= StringFormatFlags.NoClip;
   var box = new RectangleF(0, 0, (float)text.Width, 100000);
   var style = (int)VectorArt.Style(family, text.Bold);

   GraphicsPath MakePath(double points) {
    WorkContext.Check();
    if (text.Styles.Count > 0) return RichFieldLayout.Outline(text, points);
    var path = new GraphicsPath();
    try {
     path.AddString(value, family, style, (float)(points * 25.4 / 72), box, format);
     return path;
    } catch { path.Dispose(); throw; }
   }
   bool WithinField(GraphicsPath path) {
    if (path.PointCount == 0) return false;
    var ink = path.GetBounds();
    return ink.Width <= text.Width + .005 && ink.Bottom + Math.Max(0, -ink.Top) <= text.Height + .005;
   }

   double minimum = text.MinimumFitSize > 0 ? Math.Min(text.Size, text.MinimumFitSize) : text.Size;
   var best = MakePath(size);
   try {
    fits = WithinField(best);
    if (!fits && minimum < size) {
     using var smallest = MakePath(minimum);
     fits = WithinField(smallest);
     best.Dispose();
     best = (GraphicsPath)smallest.Clone();
     size = minimum;
     if (fits) {
      int low = 1, high = (int)Math.Floor((text.Size - minimum) / PointsPerStep);
      while (low <= high) {
       int step = low + (high - low) / 2;
       double points = Math.Min(text.Size, minimum + step * PointsPerStep);
       using var candidate = MakePath(points);
       if (WithinField(candidate)) {
        best.Dispose();
        best = (GraphicsPath)candidate.Clone();
        size = points;
        low = step + 1;
       } else high = step - 1;
      }
     }
    }
    using var offset = new Matrix();
    var ink = best.PointCount == 0 ? RectangleF.Empty : best.GetBounds();
    // Some glyphs extend past their layout origin. Keep the whole outline in the field.
    double dx = fits ? Math.Max(-ink.Left, Math.Min(0, text.Width - ink.Right)) : 0;
    double dy = fits ? Math.Max(0, -ink.Top) : 0;
    offset.Translate((float)(text.X + dx), (float)(text.Y + dy));
    best.Transform(offset);
    return best;
   } catch { best.Dispose(); throw; }
  }
 }
}
