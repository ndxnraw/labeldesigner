using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class PrintPanel {
  ModernButton readabilityButton;
  ReadabilityRun readability;

  internal sealed class ReadabilityRun {
   public readonly OutputPlan Plan;
   public readonly BarcodePrintGeometry Geometry;
   public readonly BarcodeReadabilityResult Result;
   public ReadabilityRun(OutputPlan plan, BarcodePrintGeometry geometry, BarcodeReadabilityResult result) {
    Plan = plan; Geometry = geometry; Result = result;
   }
   public bool Matches(OutputPlan plan, BarcodePrintGeometry geometry) {
    return ReferenceEquals(Plan, plan) && Geometry.Fingerprint == geometry.Fingerprint;
   }
  }

  void AddReadabilityButton(TableLayoutPanel fields) {
   readabilityButton = Button(Loc.T("Čitateľnosť kódov…"), ShowReadability);
   readabilityButton.Enabled = false;
   Add(fields, readabilityButton);
  }

  static RectangleF Millimetres(RectangleF driver) {
   return new RectangleF(driver.X * .254f, driver.Y * .254f, driver.Width * .254f, driver.Height * .254f);
  }

  static bool UsableDpi(double dpi) { return dpi >= 50 && dpi <= 9600; }

  static BarcodePrintGeometry CaptureReadability(LabelPrintDocument doc,
      PrinterPageGeometry page, ZebraSettings zebra, double documentDpi) {
   double x = documentDpi, y = documentDpi;
   bool estimated = !zebra.Enabled;
   if (zebra.Enabled) x = y = zebra.Dpi;
   else if (UsableDpi(page.ResolutionX) && UsableDpi(page.ResolutionY)) {
    x = page.ResolutionX; y = page.ResolutionY; estimated = false;
   }
   var paper = Millimetres(page.Bounds);
   var origin = new PointF(page.HardMarginX * .254f, page.HardMarginY * .254f);
   if (zebra.Enabled) {
    // Match the actual integer output bitmap used by the Zebra transport.
    paper = new RectangleF(0, 0,
     (float)(Math.Max(1, Math.Round(page.Bounds.Width * x / 100)) * 25.4 / x),
     (float)(Math.Max(1, Math.Round(page.Bounds.Height * y / 100)) * 25.4 / y));
    origin = PointF.Empty;
   }
   return new BarcodePrintGeometry {
    DpiX = x, DpiY = y, EstimatedDpi = estimated, Monochrome = zebra.Enabled,
    X = doc.OffsetX, Y = doc.OffsetY, ScaleX = doc.ScaleX, ScaleY = doc.ScaleY,
    GridOrigin = origin, Paper = paper, Printable = doc.IgnoreDriverMargins ? (RectangleF?)null : Millimetres(page.PrintableArea)
   };
  }

  static ReadabilityRun InspectReadability(PreflightReport report, LabelPrintDocument doc,
      PrinterPageGeometry page, ZebraSettings zebra, double documentDpi,
      ReadabilityRun cached) {
   if (report.Blocking) return null;
   try {
    var geometry = CaptureReadability(doc, page, zebra, documentDpi);
    var run = cached != null && cached.Matches(report.Plan, geometry) ? cached :
     new ReadabilityRun(report.Plan, geometry, BarcodeReadability.Inspect(report.Plan, geometry, true, 48, 24, 6000000));
    if (run.Result.Items.Count > 0 || run.Result.Partial) {
     report.Context += "|readability:" + geometry.Fingerprint;
     run.Result.AddWarnings(report);
    }
    return run;
   } catch (OperationCanceledException) { throw; }
     catch (Exception ex) {
    DiagnosticLog.Write("barcode-readability", ex);
    report.AddWarning(Loc.T("Kontrola čitateľnosti sa nepodarila. Skontrolujte kód na skúšobnom výtlačku."));
    return null;
   }
  }

  void ApplyReadability(ReadabilityRun run) {
   if (run == null || readability == null || !readability.Matches(run.Plan, run.Geometry))
    preview.HighlightedCode = RectangleF.Empty;
   readability = run;
   readabilityButton.Enabled = run != null && (run.Result.Items.Count > 0 || run.Result.Partial);
  }

  void ShowReadability() {
   var run = readability;
   if (run == null || busy || sending || !ReferenceEquals(run.Plan, outputPlan)) return;
   while (true) {
    BarcodeReadabilityItem selected;
    DialogResult choice = ReadabilityDialog(run, out selected);
    if (choice == DialogResult.OK && selected != null) {
     if(preview.ShowDetail)zoom.SelectedIndex=0;
     page.Value = selected.Page;
     preview.HighlightedCode = selected.Bounds;
     preview.HighlightedPage = selected.Page - 1;
     preview.Invalidate();
     return;
    }
    if (choice != DialogResult.Retry) return;
    BarcodeReadabilityResult complete;
    try {
     if (!WorkDialog.Run(this, "barcode-readability", Loc.T("Kontrolujem čitateľnosť kódov…"), Font,
       FindForm() == null ? null : FindForm().Icon, calibrationStyle ?? (control => { }),
       () => BarcodeReadability.Inspect(run.Plan, run.Geometry, true, int.MaxValue), out complete)) return;
    } catch (Exception ex) {
     UiMessageBox.Show(this, ex.Message, Loc.T("Čitateľnosť kódov…"));
     return;
    }
    if (!ReferenceEquals(run.Plan, outputPlan) || readability == null ||
       !readability.Matches(run.Plan, run.Geometry)) return;
    run = new ReadabilityRun(run.Plan, run.Geometry, complete);
    readability = run;
    Schedule();
   }
  }

  static string Dots(double value) { return value.ToString("0.##", CultureInfo.CurrentCulture); }

  static string ReadStatus(BarcodeDecodeStatus value) {
   switch (value) {
    case BarcodeDecodeStatus.Matched: return Loc.T("Údaje sa zhodujú");
    case BarcodeDecodeStatus.Unreadable: return Loc.T("Nenačítané");
    case BarcodeDecodeStatus.Different: return Loc.T("Údaje sa nezhodujú");
    case BarcodeDecodeStatus.TooLarge: return Loc.T("Príliš veľký výrez");
    default: return Loc.T("Nevykonané");
   }
  }

  DialogResult ReadabilityDialog(ReadabilityRun run, out BarcodeReadabilityItem selected) {
   using (var dialog = new Form {
    Text = Loc.T("Čitateľnosť kódov…"), Font = Font, Size = new Size(1000, 590),
    MinimumSize = new Size(620, 420), StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
    AutoScaleMode = AutoScaleMode.Dpi
   }) {
    var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    dialog.Controls.Add(layout);
    string facts = Loc.T(run.Geometry.EstimatedDpi ? "Odhadované DPI" : "DPI výstupu") + " " +
     Dots(run.Geometry.DpiX) + " × " + Dots(run.Geometry.DpiY) + "   ·   " +
     Loc.T("Skontrolované kódy") + " " + run.Result.Items.Count;
    if (run.Result.Partial) facts += "   ·   " + Loc.T("Čiastočná kontrola");
    layout.Controls.Add(new Label { Text = facts, AutoSize = true }, 0, 0);
    var table = new DataGridView {
     Dock = DockStyle.Fill, ReadOnly = true, VirtualMode = true, RowHeadersVisible = false,
     AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
     SelectionMode = DataGridViewSelectionMode.FullRowSelect,
     AccessibleName = Loc.T("Čitateľnosť kódov…")
    };
    string[] columns = { "Strana", "Záznam", "Pozícia etikety", "Typ kódu", "Modul (body)",
     "Navrhnutý okraj Ľ/P/H/D (body)", "Skúšobné načítanie" };
    int[] widths = { 65, 78, 104, 115, 100, 185, 170 };
    for (int i = 0; i < columns.Length; i++) table.Columns.Add(new DataGridViewTextBoxColumn {
     HeaderText = Loc.T(columns[i]), SortMode = DataGridViewColumnSortMode.NotSortable, Width = widths[i]
    });
    table.CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs args) {
     var item = run.Result.Items[args.RowIndex];
     switch (args.ColumnIndex) {
      case 0: args.Value = item.Page; break;
      case 1: args.Value = item.Record > 0 ? item.Record.ToString() : "–"; break;
      case 2: args.Value = item.Slot; break;
      case 3: args.Value = item.Type; break;
      case 4: args.Value = Dots(item.ModuleDots); break;
      case 5: args.Value = Dots(item.LeftDots) + " / " + Dots(item.RightDots) + " / " + Dots(item.TopDots) + " / " + Dots(item.BottomDots); break;
      case 6: args.Value = ReadStatus(item.Decode); break;
     }
    };
    table.RowCount = run.Result.Items.Count;
    layout.Controls.Add(table, 0, 1);
    var detail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
     AccessibleName = Loc.T("Odporúčanie") };
    layout.Controls.Add(detail, 0, 2);
    var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
    var highlight = Button(Loc.T("Označiť v náhľade"), () => { dialog.DialogResult = DialogResult.OK; });
    var all = Button(Loc.T("Skontrolovať celú úlohu"), () => { dialog.DialogResult = DialogResult.Retry; });
    var close = Button(Loc.T("Zavrieť"), () => { dialog.DialogResult = DialogResult.Cancel; });
    foreach (var button in new[] { highlight, all, close }) {
     button.AutoSize = true;
     actions.Controls.Add(button);
    }
    all.Visible = run.Result.Partial;
    dialog.CancelButton = close;
    layout.Controls.Add(actions, 0, 3);
    Func<BarcodeReadabilityItem> current = () => table.CurrentCell == null ? null : run.Result.Items[table.CurrentCell.RowIndex];
    Action update = delegate {
     var item = current();
     highlight.Enabled = item != null;
     detail.Text = (item == null || item.Advice.Length == 0 ? "" : item.Advice + Environment.NewLine) +
      Loc.T("Skúšobné načítanie je digitálna kontrola. Čitateľnosť výtlačku overte skenerom.");
    };
    table.SelectionChanged += delegate { update(); };
    table.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs args) {
     if (args.RowIndex >= 0) dialog.DialogResult = DialogResult.OK;
    };
    update();
    if (calibrationStyle != null) calibrationStyle(dialog);
    DialogResult choice = dialog.ShowDialog(this);
    selected = current();
    return choice;
   }
  }
 }
}
