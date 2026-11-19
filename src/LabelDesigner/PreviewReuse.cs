using System;using System.Diagnostics;using System.Drawing;using System.Drawing.Printing;using System.Globalization;
namespace EanEtikety {
public partial class PrintPanel {
 readonly PreviewSessionCache previewCache=new PreviewSessionCache();readonly bool ownsPreparedPlan;int driverRevision;
 Func<PageSettings,PrinterPageGeometry> geometryReader=PrinterPageGeometry.Capture;
 static double PreviewClock{get{return Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;}}
 static string KeyNumber(double n){return n.ToString("R",CultureInfo.InvariantCulture);}
 void InvalidatePreviewCache(){driverRevision++;previewCache.Clear();}
 static PaperSize[] ReadPreviewPapers(PrinterSettings printer,PreviewTrace trace){var sizes=new System.Collections.Generic.List<PaperSize>();if(printer.IsValid){trace.CapabilityReads++;foreach(PaperSize size in printer.PaperSizes)sizes.Add(size);}return sizes.ToArray();}
 string GeometryKey(PrinterSettings printer,PageSettings page,int revision){var size=page.PaperSize;return revision.ToString(CultureInfo.InvariantCulture)+"|"+printer.PrinterName+"|"+size.PaperName+"|"+size.RawKind+"|"+size.Width+"|"+size.Height+"|"+page.Landscape;}
 PrinterPageGeometry PreviewGeometry(string key,PageSettings page,bool fresh,PreviewTrace trace){var geometry=fresh?null:previewCache.GetGeometry(key,PreviewClock);if(geometry==null){geometry=geometryReader(page);trace.DriverGeometryReads+=4;previewCache.StoreGeometry(key,geometry,PreviewClock);}return geometry;}
 Image PreviewImage(LabelPrintDocument document,OutputPlan plan,int index,SizeF paper,PreviewTrace trace){
  // Externally supplied OutputPlans have public mutable labels/placements. Do not
  // cache their artwork; only our internally prepared, frozen job is reusable.
  string key=index.ToString(CultureInfo.InvariantCulture)+"|"+KeyNumber(paper.Width)+"|"+KeyNumber(paper.Height)+"|"+KeyNumber(document.OffsetX)+"|"+KeyNumber(document.OffsetY)+"|"+KeyNumber(document.ScaleX)+"|"+KeyNumber(document.ScaleY);
  if(ownsPreparedPlan){previewCache.SetPlan(plan,Loc.Language);var image=previewCache.GetImage(key);if(image!=null){trace.ArtworkCacheHit=true;return image;}}
  trace.RenderedPages++;var result=document.CreatePreview(index,paper);try{if(ownsPreparedPlan)previewCache.StoreImage(key,result);return result;}catch{result.Dispose();throw;}
 }
}
}
