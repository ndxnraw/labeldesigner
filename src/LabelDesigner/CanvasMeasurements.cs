using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  void AddCanvasViewOptions(ContextMenuStrip menu) {
   menu.Items.Add(new ToolStripSeparator());
   var rulers = new ToolStripMenuItem(Loc.T("Pravítka")) {
    Name = "canvasRulers", Checked = preview.ShowRulers, CheckOnClick = true
   };
   rulers.CheckedChanged += delegate { preview.ShowRulers = rulers.Checked; };
   menu.Items.Add(rulers);
   var snapping = new ToolStripMenuItem(Loc.T("Prichytávať pri presúvaní")) {
    Name = "canvasSnapping", Checked = preview.SnapToGuides, CheckOnClick = true
   };
   snapping.CheckedChanged += delegate { preview.SnapToGuides = snapping.Checked; };
   menu.Items.Add(snapping);
  }

  void LoadCanvasViewOptions(string[] layout) {
   if (layout.Length > 6) ((ToolStripMenuItem)viewMenu.Items["canvasRulers"]).Checked = layout[6] != "0";
   if (layout.Length > 7) ((ToolStripMenuItem)viewMenu.Items["canvasSnapping"]).Checked = layout[7] != "0";
  }
 }

 public partial class PreviewPanel {
  bool showRulers = true;
  [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
  public bool ShowRulers {
   get { return showRulers; }
   set { showRulers = value; Invalidate(); }
  }
  bool snapToGuides = true;
  [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
  public bool SnapToGuides {
   get { return snapToGuides; }
   set { snapToGuides = value; }
  }
  int RulerWidth { get { return (int)Math.Ceiling(38 * DeviceDpi / 96d); } }
  int RulerHeight { get { return (int)Math.Ceiling(24 * DeviceDpi / 96d); } }
  double MeasurementUnit { get { return Settings.Unit == "cm" ? 10 : 1; } }
  string UnitName { get { return Settings.Unit == "cm" ? "cm" : "mm"; } }
  bool OverRuler(Point point) {
   return Compact && ShowRulers && (point.X < RulerWidth || point.Y < RulerHeight);
  }

  bool VisibleRotationHandle(PointF point) {
   const int hitRadius = 8;
   return point.X >= RulerWidth + hitRadius && point.Y >= RulerHeight + hitRadius &&
    point.X < ClientSize.Width - hitRadius && point.Y < ClientSize.Height - hitRadius;
  }

  protected override void WndProc(ref Message message) {
   const int contextMenu = 0x007B;
   if (message.Msg == contextMenu && message.LParam.ToInt64() != -1) {
    long coordinates = message.LParam.ToInt64();
    var screen = new Point(unchecked((short)coordinates), unchecked((short)(coordinates >> 16)));
    if (OverRuler(PointToClient(screen))) return;
   }
   base.WndProc(ref message);
  }

  protected override void OnScroll(ScrollEventArgs e) {
   base.OnScroll(e);
   if (Compact && ShowRulers) Invalidate();
  }

  static double RulerStep(double minimum) {
   double power = Math.Pow(10, Math.Floor(Math.Log10(minimum)));
   double fraction = minimum / power;
   return power * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);
  }

  void DrawRulers(Graphics graphics, Rectangle canvas) {
   if (!Compact || !ShowRulers) return;
   using (var background = new SolidBrush(StudioTheme.Panel(Dark)))
   using (var foreground = new SolidBrush(StudioTheme.Text(Dark)))
   using (var ticks = new Pen(StudioTheme.Text(Dark))) {
    graphics.FillRectangle(background, 0, 0, ClientSize.Width, RulerHeight);
    graphics.FillRectangle(background, 0, RulerHeight, RulerWidth, ClientSize.Height - RulerHeight);
    graphics.DrawLine(ticks, RulerWidth, RulerHeight - 1, ClientSize.Width, RulerHeight - 1);
    graphics.DrawLine(ticks, RulerWidth - 1, RulerHeight, RulerWidth - 1, ClientSize.Height);
    graphics.DrawString(UnitName, Font, foreground, 3, 2);
    DrawRulerTicks(graphics, ticks, foreground, canvas, true);
    DrawRulerTicks(graphics, ticks, foreground, canvas, false);
   }
  }

  void DrawRulerTicks(Graphics graphics, Pen pen, Brush text, Rectangle canvas, bool horizontal) {
   double length = (horizontal ? Settings.Width : Settings.Height) / MeasurementUnit;
   double scale = (horizontal ? canvas.Width : canvas.Height) / length;
   int origin = horizontal ? canvas.Left : canvas.Top;
   int start = horizontal ? RulerWidth : RulerHeight;
   int end = horizontal ? ClientSize.Width : ClientSize.Height;
   if (end <= start) return;
   var state = graphics.Save();
   graphics.SetClip(horizontal ? Rectangle.FromLTRB(start, 0, end, RulerHeight) :
    Rectangle.FromLTRB(0, start, RulerWidth, end), System.Drawing.Drawing2D.CombineMode.Intersect);
   double step = RulerStep(Math.Max(0.001, (horizontal ? 54 : 38) * DeviceDpi / (96d * scale)));
   double minor = step / 5;
   int first = (int)Math.Max(0, Math.Ceiling((start - origin) / (minor * scale)));
   int last = (int)Math.Floor(Math.Min(length / minor, (end - origin) / (minor * scale)) + 1e-8);
   for (int index = first; index <= last; index++) {
    float position = (float)(origin + index * minor * scale);
    bool major = index % 5 == 0;
    float mark = (major ? 7 : 3) * DeviceDpi / 96f;
    if (horizontal) {
     graphics.DrawLine(pen, position, RulerHeight - 1, position, RulerHeight - 1 - mark);
     if (major) graphics.DrawString((index * minor).ToString("0.###"), Font, text, position + 1, 0);
    } else {
     graphics.DrawLine(pen, RulerWidth - 1, position, RulerWidth - 1 - mark, position);
     if (major) graphics.DrawString((index * minor).ToString("0.###"), Font, text, 1, position + 1);
    }
   }
   graphics.Restore(state);
  }

  string FormatDistance(double mm) {
   double value = Math.Round(mm / MeasurementUnit, MeasurementUnit == 10 ? 3 : 2);
   return (value == 0 ? "0" : value.ToString("0.###")) + " " + UnitName;
  }

  string EdgeDistances(RectangleF bounds) {
   return string.Format(Loc.T("Vľavo {0} · Vpravo {1}"), FormatDistance(bounds.Left),
    FormatDistance(Settings.Width - bounds.Right)) + Environment.NewLine +
    string.Format(Loc.T("Hore {0} · Dole {1}"), FormatDistance(bounds.Top),
    FormatDistance(Settings.Height - bounds.Bottom));
  }

  void DrawEdgeDistances(Graphics graphics) {
   if (!dragging || handle >= 0 || snap == null) return;
   string caption = EdgeDistances(dragBounds);
   var size = graphics.MeasureString(caption, Font);
   float left = Compact && ShowRulers ? RulerWidth + 2 : 2;
   float top = Compact ? ShowRulers ? RulerHeight + 2 : 2 : 84;
   float x = Math.Max(left, Math.Min(ClientSize.Width - size.Width - 14, latestMouse.X + 16));
   float y = latestMouse.Y + 18;
   if (y + size.Height + 10 > ClientSize.Height) y = latestMouse.Y - size.Height - 14;
   y = Math.Max(top, y);
   using (var background = new SolidBrush(StudioTheme.Panel(Dark)))
   using (var foreground = new SolidBrush(StudioTheme.Text(Dark)))
   using (var border = new Pen(StudioTheme.Edge(Dark))) {
    var box = new RectangleF(x, y, size.Width + 12, size.Height + 8);
    graphics.FillRectangle(background, box);
    graphics.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
    graphics.DrawString(caption, Font, foreground, x + 6, y + 4);
   }
  }
 }
}
