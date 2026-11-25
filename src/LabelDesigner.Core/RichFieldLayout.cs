using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Wpf = System.Windows;
using Media = System.Windows.Media;

namespace EanEtikety {
 internal static class RichFieldLayout {
  const double DipsPerMillimetre = 96d / 25.4;

  static Media.FormattedText Format(TextItem text, double points, double width) {
   WorkContext.Check();
   var family = new Media.FontFamily(string.IsNullOrWhiteSpace(text.FontName) ? "Arial" : text.FontName);
   var face = new Media.Typeface(family, Wpf.FontStyles.Normal,
    text.Bold ? Wpf.FontWeights.Bold : Wpf.FontWeights.Normal, Wpf.FontStretches.Normal);
   var formatted = new Media.FormattedText(text.Text, CultureInfo.InvariantCulture, Wpf.FlowDirection.LeftToRight,
    face, points * 96 / 72, Media.Brushes.Black, 1);
   foreach (var span in text.Styles) {
    formatted.SetFontWeight(span.Emphasis.HasFlag(TextEmphasis.Bold) ? Wpf.FontWeights.Bold : Wpf.FontWeights.Normal,
     span.Start, span.Length);
    if (span.Emphasis.HasFlag(TextEmphasis.Italic)) formatted.SetFontStyle(Wpf.FontStyles.Italic, span.Start, span.Length);
   }
   if (width > 0) formatted.MaxTextWidth = width * DipsPerMillimetre;
   return formatted;
  }

  internal static double NaturalWidth(TextItem text) => Format(text, text.Size, 0).WidthIncludingTrailingWhitespace / DipsPerMillimetre;
  internal static double WrappedHeight(TextItem text, double width) => Format(text, text.Size, width).Height / DipsPerMillimetre;

  internal static GraphicsPath Outline(TextItem text, double points) {
   var formatted = Format(text, points, text.Width);
   if (text.Center) formatted.TextAlignment = Wpf.TextAlignment.Center;
   var geometry = formatted.BuildGeometry(new Wpf.Point())
    .GetFlattenedPathGeometry(.035, Media.ToleranceType.Absolute);
   var path = new GraphicsPath(geometry.FillRule == Media.FillRule.EvenOdd ? FillMode.Alternate : FillMode.Winding);
   try {
    foreach (Media.PathFigure figure in geometry.Figures) {
     if (!figure.IsFilled) continue;
     var pointsOfFigure = new System.Collections.Generic.List<PointF> { Point(figure.StartPoint) };
     foreach (var segment in figure.Segments) {
      if (segment is Media.PolyLineSegment poly) foreach (var point in poly.Points) pointsOfFigure.Add(Point(point));
      else if (segment is Media.LineSegment line) pointsOfFigure.Add(Point(line.Point));
     }
     if (pointsOfFigure.Count < 2) continue;
     path.StartFigure();
     path.AddLines(pointsOfFigure.ToArray());
     if (figure.IsClosed) path.CloseFigure();
    }
    return path;
   } catch { path.Dispose(); throw; }
  }

  static PointF Point(Wpf.Point point) => new((float)(point.X / DipsPerMillimetre), (float)(point.Y / DipsPerMillimetre));
 }
}
