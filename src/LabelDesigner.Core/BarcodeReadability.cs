using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using ZXing;

namespace EanEtikety {
 public enum BarcodeDecodeStatus { NotRun, Matched, Unreadable, Different, TooLarge }

 // Coordinates match the unrotated vector path in millimetres.
 internal sealed class BarcodeMark {
  public BarcodeFormat Format;
  public string Name, Expected;
  public bool Matrix, Gs1;
  public RectangleF Data, Footprint;
  public double StepX, StepY;

  public BarcodeMark Transform(Matrix transform) {
   var copy = (BarcodeMark)MemberwiseClone();
   var axes = new[] { new PointF((float)StepX, 0), new PointF(0, (float)StepY) };
   transform.TransformVectors(axes);
   copy.StepX = Math.Abs(axes[0].X);
   copy.StepY = Math.Abs(axes[1].Y);
   copy.Data = TransformRect(Data, transform);
   copy.Footprint = TransformRect(Footprint, transform);
   return copy;
  }

  static RectangleF TransformRect(RectangleF rectangle, Matrix transform) {
   var corners = new[] { rectangle.Location, new PointF(rectangle.Right, rectangle.Bottom) };
   transform.TransformPoints(corners);
   return RectangleF.FromLTRB(corners[0].X, corners[0].Y, corners[1].X, corners[1].Y);
  }

  public static bool LinearSupported(int kind, bool gs1) {
   return kind == 13 || kind == 14 || kind == 114 || kind == 128 && gs1;
  }

  public static bool MatrixSupported(string type) {
   return type == "QR" || type == "Data Matrix" || type == "GS1 QR" || type == "GS1 Data Matrix";
  }

  public static BarcodeMark Linear(LabelSettings label, RectangleF data, RectangleF footprint, double step) {
   if (!LinearSupported(label.Kind, label.Gs1Mode)) return null;
   bool gs1 = label.Kind == 114 || Engine.IsGs1(label);
   string expected = label.Kind == 114 ? "01" + Engine.Normalize(label.Ean, 14) :
    gs1 ? EanEtikety.Gs1.Payload(label.ApplicationIdentifiers) : Engine.Normalize(label.Ean, label.Kind);
   return new BarcodeMark {
    Format = gs1 ? BarcodeFormat.CODE_128 : label.Kind == 13 ? BarcodeFormat.EAN_13 : BarcodeFormat.ITF,
    Name = gs1 ? "GS1-128" : label.Kind == 13 ? "EAN-13" : "ITF-14",
    Expected = expected, Gs1 = gs1, Data = data, Footprint = footprint, StepX = step, StepY = step
   };
  }
 }

 public sealed class BarcodePrintGeometry {
  public double DpiX, DpiY, X, Y, ScaleX = 1, ScaleY = 1;
  public PointF GridOrigin;
  public RectangleF Paper;
  public RectangleF? Printable;
  public bool EstimatedDpi, Monochrome;

  public string Fingerprint {
   get {
    return string.Format(CultureInfo.InvariantCulture, "{0:R}|{1:R}|{2:R}|{3:R}|{4:R}|{5:R}|{6}|{7}|{8}|{9}|{10}",
     DpiX, DpiY, X, Y, ScaleX, ScaleY, GridOrigin, Paper, Printable, EstimatedDpi, Monochrome);
   }
  }
 }

 public sealed class BarcodeReadabilityItem {
  public int Page, Record, Element, Slot;
  public string Type;
  public double ModuleDots, LeftDots, RightDots, TopDots, BottomDots;
  public RectangleF Bounds;
  public bool InkClipped, QuietClipped, QuietObstructed;
  public BarcodeDecodeStatus Decode;

  public string Advice {
   get {
    var messages = new List<string>();
    if (ModuleDots < 1.999) messages.Add(Loc.T("Modul má menej než dva body tlačiarne. Zväčšite kód alebo použite vyššie DPI."));
    if (InkClipped) messages.Add(Loc.T("Kód môže byť orezaný. Posuňte ho dovnútra etikety alebo overte médium a kalibráciu."));
    if (QuietClipped || QuietObstructed) messages.Add(Loc.T("Voľný okraj kódu je orezaný alebo obsadený. Pridajte voľný okraj alebo posuňte susedný prvok."));
    if (Decode == BarcodeDecodeStatus.Unreadable || Decode == BarcodeDecodeStatus.Different)
     messages.Add(Loc.T("Skúšobné načítanie nepotvrdilo pôvodné údaje. Skontrolujte veľkosť, voľné okraje a prekrytie."));
    if (Decode == BarcodeDecodeStatus.TooLarge) messages.Add(Loc.T("Výrez je príliš veľký na skúšobné načítanie pri tomto DPI."));
    return string.Join(" ", messages.ToArray());
   }
  }
 }

 public sealed class BarcodeReadabilityResult {
  public readonly List<BarcodeReadabilityItem> Items = new List<BarcodeReadabilityItem>();
  public bool Partial;

  public void AddWarnings(PreflightReport report) {
   const int maxDetails = 100;
   int warnings = 0;
   foreach (var item in Items) {
    string advice = item.Advice;
    if (advice.Length == 0) continue;
    if (++warnings > maxDetails) continue;
    report.Issues.Add(new LabelIssue {
     Page = item.Page, Record = item.Record, Element = item.Element,
     Message = item.Type + " · " + Loc.T("Pozícia etikety") + " " + item.Slot + " · " + advice
    });
   }
   if (warnings > maxDetails)
    report.AddWarning(Loc.T("Ďalšie upozornenia na čitateľnosť nájdete v okne Čitateľnosť kódov.") + " (" + (warnings - maxDetails) + ")");
   if (Partial) report.AddWarning(Loc.T("Automatická kontrola čitateľnosti preverila časť úlohy. Úplnú kontrolu spustíte v okne Čitateľnosť kódov."));
  }
 }

 public static class BarcodeReadability {
  const int MaxCropPixels = 4000000;
  const int MaxCropSide = 12000;

  static bool Supported(LabelSettings label) {
   if (!label.HideBarcode && BarcodeMark.LinearSupported(label.Kind, label.Gs1Mode)) return true;
   return label.Symbols.Exists(s => s.Matrix != null && BarcodeMark.MatrixSupported(s.Matrix.Type)
    || s.Code != null && BarcodeMark.LinearSupported(s.Code.Kind, s.Code.Gs1Mode));
  }

  sealed class Candidate {
   public ArtShape Shape;
   public OutputPlacement Placement;
   public int Element, Slot;
  }

  public static BarcodeReadabilityResult Inspect(OutputPlan plan, BarcodePrintGeometry geometry,
      bool decode, int maxCodes, int maxPages = int.MaxValue, long maxPixels = long.MaxValue) {
   if (!(geometry.DpiX > 0 && geometry.DpiX <= 9600 && geometry.DpiY > 0 && geometry.DpiY <= 9600)
     || !(geometry.ScaleX > 0 && geometry.ScaleY > 0)) throw new ArgumentOutOfRangeException("geometry");
   var result = new BarcodeReadabilityResult();
   var pages = new SortedSet<int>();
   var checkedLabels = new Dictionary<LabelSettings, bool>();
   foreach (var placement in plan.Placements) {
    WorkContext.Check();
    bool supported;
    if (!checkedLabels.TryGetValue(placement.Label, out supported)) {
     supported = Supported(placement.Label);
     checkedLabels.Add(placement.Label, supported);
    }
    if (supported) pages.Add(placement.Page);
   }
   int completed = 0;
   foreach (int page in pages) {
    WorkContext.Report("Kontrolujem čitateľnosť kódov…", completed, pages.Count);
    if (completed >= maxPages || result.Items.Count >= maxCodes) { result.Partial = true; break; }
    var candidates = new List<Candidate>();
    using (var scene = plan.Scene(page, (placement, shape, element, slot) =>
       candidates.Add(new Candidate { Placement = placement, Shape = shape, Element = Elements.SourceIndex(placement.Label,element), Slot = slot }))) {
     foreach (var candidate in candidates) {
      WorkContext.Check();
      if (result.Items.Count >= maxCodes) { result.Partial = true; break; }
      var check = Measure(candidate, scene, geometry);
      if (decode && !Scan(candidate.Shape, scene, geometry, check, ref maxPixels)) {
       result.Partial = true;
       break;
      }
      result.Items.Add(check);
     }
    }
    completed++;
    if (result.Partial) break;
   }
   WorkContext.Report("Kontrolujem čitateľnosť kódov…", completed, pages.Count);
   return result;
  }

  static Matrix Calibration(BarcodePrintGeometry g) {
   return new Matrix((float)g.ScaleX, 0, 0, (float)g.ScaleY, (float)g.X, (float)g.Y);
  }

  static RectangleF Calibrated(RectangleF area, BarcodePrintGeometry g) {
   return new RectangleF((float)(area.X * g.ScaleX + g.X), (float)(area.Y * g.ScaleY + g.Y),
    (float)(area.Width * g.ScaleX), (float)(area.Height * g.ScaleY));
  }

  static RectangleF Project(RectangleF rectangle, ArtShape shape, BarcodePrintGeometry g) {
   return Calibrated(ElementTransform.Bounds(rectangle, shape.Rotation, shape.Pivot), g);
  }

  static bool Contains(RectangleF outer, RectangleF inner, float tolerance) {
   if (outer.Width <= 0 || outer.Height <= 0) return false;
   outer.Inflate(tolerance, tolerance);
   return outer.Contains(inner);
  }

  static RectangleF VisibleArea(VectorScene scene, ArtShape shape, BarcodePrintGeometry geometry) {
   var canvas = new RectangleF(0, 0, (float)scene.Width, (float)scene.Height);
   if (shape.CanvasClip.HasValue) canvas = RectangleF.Intersect(canvas, shape.CanvasClip.Value);
   var area = RectangleF.Intersect(Calibrated(canvas, geometry), geometry.Paper);
   if (geometry.Printable.HasValue) area = RectangleF.Intersect(area, geometry.Printable.Value);
   return area;
  }

  static BarcodeReadabilityItem Measure(Candidate candidate, VectorScene scene, BarcodePrintGeometry g) {
   var shape = candidate.Shape;
   var mark = shape.Barcode;
   double angle = shape.Rotation * Math.PI / 180;
   double c = Math.Cos(angle), s = Math.Sin(angle);
   double x = g.ScaleX * g.DpiX / 25.4, y = g.ScaleY * g.DpiY / 25.4;
   double across = x * y / Math.Sqrt(x * x * s * s + y * y * c * c);
   double down = x * y / Math.Sqrt(x * x * c * c + y * y * s * s);
   var ink = shape.Path.GetBounds();
   var outer = Project(mark.Footprint, shape, g);
   var area = VisibleArea(scene, shape, g);
   // Subpixel rounding of the vector position is not a missing printed dot.
   float tolerance = (float)(.5 * 25.4 / Math.Min(g.DpiX, g.DpiY));
   float localTolerance = tolerance / (float)Math.Min(g.ScaleX, g.ScaleY);
   return new BarcodeReadabilityItem {
    Page = candidate.Placement.Page + 1, Record = candidate.Placement.Record, Element = candidate.Element, Slot = candidate.Slot,
    Type = mark.Name, Bounds = RectangleF.Union(outer, Project(ink, shape, g)),
    ModuleDots = mark.Matrix ? Math.Min(mark.StepX * across, mark.StepY * down) : mark.StepX * across,
    LeftDots = (mark.Data.Left - mark.Footprint.Left) * across,
    RightDots = (mark.Footprint.Right - mark.Data.Right) * across,
    TopDots = (mark.Data.Top - mark.Footprint.Top) * down,
    BottomDots = (mark.Footprint.Bottom - mark.Data.Bottom) * down,
    InkClipped = !Contains(area, Project(ink, shape, g), tolerance) || !Contains(shape.Clip, ink, localTolerance),
    QuietClipped = !Contains(area, outer, tolerance)
   };
  }

  static bool Scan(ArtShape shape, VectorScene scene, BarcodePrintGeometry g, BarcodeReadabilityItem check, ref long budget) {
   double x = g.DpiX / 25.4, y = g.DpiY / 25.4;
   int left = (int)Math.Floor((check.Bounds.Left - g.GridOrigin.X) * x);
   int top = (int)Math.Floor((check.Bounds.Top - g.GridOrigin.Y) * y);
   int width = (int)Math.Ceiling((check.Bounds.Right - g.GridOrigin.X) * x) - left;
   int height = (int)Math.Ceiling((check.Bounds.Bottom - g.GridOrigin.Y) * y) - top;
   long pixels = (long)width * height;
   if (width < 1 || height < 1 || width > MaxCropSide || height > MaxCropSide || pixels > MaxCropPixels) {
    check.Decode = BarcodeDecodeStatus.TooLarge;
    return true;
   }
   if (pixels > budget) return false;
   budget -= pixels;
   float originX = (float)(g.GridOrigin.X + left / x), originY = (float)(g.GridOrigin.Y + top / y);
   using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb)) {
    using (var graphics = Graphics.FromImage(bitmap))
    using (var toPixels = new Matrix((float)x, 0, 0, (float)y, (float)(-originX * x), (float)(-originY * y)))
    using (var calibration = Calibration(g)) {
     graphics.Clear(Color.White);
     graphics.Transform = toPixels;
     graphics.SetClip(g.Paper);
     if (g.Printable.HasValue) graphics.IntersectClip(g.Printable.Value);
     graphics.MultiplyTransform(calibration);
     var sourceCrop = new RectangleF((float)((originX - g.X) / g.ScaleX), (float)((originY - g.Y) / g.ScaleY),
      (float)(width / x / g.ScaleX), (float)(height / y / g.ScaleY));
     scene.Draw(graphics, new RectangleF(0, 0, (float)scene.Width, (float)scene.Height), false, sourceCrop);
    }
    var packed = Engine.Packed(bitmap, true);
    check.QuietObstructed = Obstructed(shape, g, packed, width, height, originX, originY);
    var reader = new BarcodeReader { AutoRotate = true };
    reader.Options.PossibleFormats = new[] { shape.Barcode.Format };
    reader.Options.AssumeGS1 = shape.Barcode.Gs1;
    reader.Options.TryHarder = true;
    if (shape.Barcode.Format == BarcodeFormat.ITF) reader.Options.AllowedLengths = new[] { 14 };
    WorkContext.Check();
    var decoded = Decode(reader, bitmap, g.Monochrome ? packed : null);
    double angle = shape.Rotation * Math.PI / 180;
    if (decoded == null && !shape.Barcode.Matrix && Math.Abs(shape.Rotation % 90) > .001) {
     // A linear reader needs scan lines along the barcode's axis. Reorient the
     // already sampled output at the same pixel resolution before retrying.
     double direction = Math.Atan2(g.ScaleY * y * Math.Sin(angle), g.ScaleX * x * Math.Cos(angle));
     bool budgetExceeded;
     using (var rotated = Deskew(bitmap, direction, ref budget, out budgetExceeded)) {
      if (budgetExceeded) return false;
      if (rotated == null) { check.Decode = BarcodeDecodeStatus.TooLarge; return true; }
      decoded = Decode(reader, rotated, g.Monochrome ? Engine.Packed(rotated, true) : null);
     }
    }
    WorkContext.Check();
    if (decoded != null && !SpanMatches(decoded, shape.Barcode, g, angle)) decoded = null;
    check.Decode = decoded == null ? BarcodeDecodeStatus.Unreadable :
     Matches(decoded, shape.Barcode) ? BarcodeDecodeStatus.Matched : BarcodeDecodeStatus.Different;
   }
   return true;
  }

  static bool SpanMatches(Result decoded, BarcodeMark mark, BarcodePrintGeometry g, double angle) {
   // A smaller neighboring code can have the same payload. Reject a clearly
   // undersized detection using distances, which survive decoder auto-rotation.
   var points = decoded.ResultPoints;
   if (points == null || points.Length < 2) return true;
   double cosine = Math.Cos(angle), sine = Math.Sin(angle);
   double x = g.ScaleX * g.DpiX / 25.4, y = g.ScaleY * g.DpiY / 25.4;
   double span = mark.Data.Width * Math.Sqrt(x * x * cosine * cosine + y * y * sine * sine);
   if (mark.Matrix) span = Math.Min(span, mark.Data.Height * Math.Sqrt(x * x * sine * sine + y * y * cosine * cosine));
   double minimumSquared = .60 * span * .60 * span;
   for (int i = 0; i < points.Length; i++) for (int j = i + 1; j < points.Length; j++) {
    if (points[i] == null || points[j] == null) continue;
    double dx = points[i].X - points[j].X, dy = points[i].Y - points[j].Y;
    if (dx * dx + dy * dy >= minimumSquared) return true;
   }
   return false;
  }

  static Result Decode(BarcodeReader reader, Bitmap bitmap, byte[] packed) {
   if (packed == null) return reader.Decode(bitmap);
   int width = bitmap.Width, height = bitmap.Height, stride = (width + 7) / 8;
   var gray = new byte[width * height];
   for (int row = 0; row < height; row++) {
    WorkContext.Check();
    for (int col = 0; col < width; col++)
     gray[row * width + col] = (packed[row * stride + col / 8] & (128 >> (col % 8))) != 0 ? (byte)0 : (byte)255;
   }
   return reader.Decode(new RGBLuminanceSource(gray, width, height, RGBLuminanceSource.BitmapFormat.Gray8));
  }

  static Bitmap Deskew(Bitmap source, double angle, ref long budget, out bool budgetExceeded) {
   double cosine = Math.Abs(Math.Cos(angle)), sine = Math.Abs(Math.Sin(angle));
   int width = (int)Math.Ceiling(source.Width * cosine + source.Height * sine);
   int height = (int)Math.Ceiling(source.Width * sine + source.Height * cosine);
   long pixels = (long)width * height;
   budgetExceeded = false;
   if (pixels > MaxCropPixels || width > MaxCropSide || height > MaxCropSide) return null;
   if (pixels > budget) { budgetExceeded = true; return null; }
   budget -= pixels;
   var surface = new Bitmap(width, height, PixelFormat.Format24bppRgb);
   try {
    using (var graphics = Graphics.FromImage(surface)) {
     graphics.Clear(Color.White);
     graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
     graphics.PixelOffsetMode = PixelOffsetMode.Half;
     graphics.TranslateTransform(width / 2f, height / 2f);
     graphics.RotateTransform((float)(-angle * 180 / Math.PI));
     graphics.DrawImage(source, new RectangleF(-source.Width / 2f, -source.Height / 2f, source.Width, source.Height),
      new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
    }
    return surface;
   } catch { surface.Dispose(); throw; }
  }

  static bool Matches(Result decoded, BarcodeMark mark) {
   if (decoded.BarcodeFormat != mark.Format) return false;
   if (!mark.Gs1) return decoded.Text == mark.Expected;
   if (mark.Format == BarcodeFormat.CODE_128) return decoded.Text == "]C1" + mark.Expected;
   object identifier;
   return decoded.ResultMetadata != null
    && decoded.ResultMetadata.TryGetValue(ResultMetadataType.SYMBOLOGY_IDENTIFIER, out identifier)
    && (string)identifier == (mark.Format == BarcodeFormat.QR_CODE ? "]Q3" : "]d2")
    && (decoded.Text == mark.Expected || decoded.Text == "\u001d" + mark.Expected);
  }

  static bool Obstructed(ArtShape shape, BarcodePrintGeometry g, byte[] packed,
      int width, int height, float originX, float originY) {
   var mark = shape.Barcode;
   int stride = (width + 7) / 8;
   double angle = shape.Rotation * Math.PI / 180;
   double cosine = Math.Cos(angle), sine = Math.Sin(angle);
   double stepX = 25.4 / g.DpiX, stepY = 25.4 / g.DpiY;
   // Ignore edge pixels where antialiasing or grid quantization crosses the nominal boundary.
   float guard = (float)(Math.Max(stepX, stepY) / Math.Min(g.ScaleX, g.ScaleY) * .75);
   var inside = mark.Footprint;
   inside.Inflate(-guard, -guard);
   var data = mark.Data;
   data.Inflate(guard, guard);
   for (int row = 0; row < height; row++) {
    WorkContext.Check();
    double dy = (originY + (row + .5) * stepY - g.Y) / g.ScaleY - shape.Pivot.Y;
    for (int col = 0; col < width; col++) {
     if ((packed[row * stride + col / 8] & (128 >> (col % 8))) == 0) continue;
     double dx = (originX + (col + .5) * stepX - g.X) / g.ScaleX - shape.Pivot.X;
     float px = (float)(shape.Pivot.X + dx * cosine + dy * sine);
     float py = (float)(shape.Pivot.Y - dx * sine + dy * cosine);
     if (inside.Contains(px, py) && !data.Contains(px, py)) return true;
    }
   }
   return false;
  }
 }
}
