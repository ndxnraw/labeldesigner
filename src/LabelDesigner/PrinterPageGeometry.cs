using System;using System.Drawing;using System.Drawing.Printing;
namespace EanEtikety {
// A single read of each expensive native geometry property, on the printer STA.
public sealed class PrinterPageGeometry {
 public readonly Rectangle Bounds;public readonly RectangleF PrintableArea;public readonly float HardMarginX,HardMarginY;public readonly bool Landscape;
 public PrinterPageGeometry(Rectangle bounds,RectangleF printable,float hardX,float hardY,bool landscape){Bounds=bounds;PrintableArea=printable;HardMarginX=hardX;HardMarginY=hardY;Landscape=landscape;}
 internal static void FreezePaper(PageSettings page){var paper=page.PaperSize;bool landscape=page.Landscape;page.PaperSize=paper;page.Landscape=landscape;}
 public static PrinterPageGeometry Capture(PageSettings page){if(page==null)throw new ArgumentNullException("page");FreezePaper(page);return new PrinterPageGeometry(page.Bounds,page.PrintableArea,page.HardMarginX,page.HardMarginY,page.Landscape);}
}
}
