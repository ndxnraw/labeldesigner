using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace EanEtikety {
 public enum ShapeFill { Transparent, Black, White }

 public sealed class ShapeContent {
  public const double MinimumLineThickness = .05;
  public bool IsLine;
  public double Stroke = .3;
  public ShapeFill Fill;

  public ShapeContent Copy() { return (ShapeContent)MemberwiseClone(); }

  internal void Validate(SymbolItem symbol) {
   if (symbol.Image != null || symbol.Matrix != null || symbol.Code != null ||
       !Enum.IsDefined(typeof(ShapeFill), Fill) || double.IsNaN(Stroke) || Stroke < 0 || Stroke > 500 ||
       (IsLine ? Fill != ShapeFill.Transparent : Stroke == 0 && Fill == ShapeFill.Transparent))
    throw new InvalidDataException(Loc.T("Neplatný obrys alebo výplň tvaru."));
  }
 }

 internal static class GraphicShape {
  static RectangleF Bounds(SymbolItem symbol) {
   return new RectangleF((float)symbol.X, (float)symbol.Y, (float)symbol.Width, (float)symbol.Height);
  }

  internal static GraphicsPath Ink(SymbolItem symbol, bool includeWhite = false) {
   var path = new GraphicsPath(FillMode.Alternate);
   var bounds = Bounds(symbol);
   var shape = symbol.Shape;
   if (shape.IsLine || shape.Fill == ShapeFill.Black || includeWhite && shape.Fill == ShapeFill.White) {
    path.AddRectangle(bounds);
   } else if (shape.Stroke > 0) {
    path.AddRectangle(bounds);
    float stroke = (float)shape.Stroke;
    if (stroke * 2 < Math.Min(bounds.Width, bounds.Height)) {
     bounds.Inflate(-stroke, -stroke);
     path.AddRectangle(bounds);
    }
   }
   return path;
  }

  internal static void Add(VectorScene scene, SymbolItem symbol, int index) {
   var bounds = Bounds(symbol);
   float stroke = (float)symbol.Shape.Stroke;
   if (!symbol.Shape.IsLine && symbol.Shape.Fill == ShapeFill.White && stroke * 2 < Math.Min(bounds.Width, bounds.Height)) {
    var interior = bounds;
    interior.Inflate(-stroke, -stroke);
    var white = new GraphicsPath();
    white.AddRectangle(interior);
    scene.Shapes.Add(new ArtShape { Path = white, Clip = bounds, Fill = Color.White, Element = index });
   }
   var ink = Ink(symbol);
   if (ink.PointCount > 0) scene.Shapes.Add(new ArtShape { Path = ink, Clip = bounds, Element = index });
   else ink.Dispose();
  }
 }
}
