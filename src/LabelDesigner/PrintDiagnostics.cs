using System;using System.Drawing;using System.Drawing.Printing;using System.Globalization;using System.IO;using System.Text;using System.Windows.Forms;
namespace EanEtikety {
// No names, paths or label values are retained in this numeric snapshot.
public sealed class PrintDiagnostics {
 public readonly SizeF Document,ConfiguredMedia,CorrectedDocument,HardMargins;
 public readonly RectangleF Printable;public readonly double X,Y,ScaleX,ScaleY;public readonly int Pages,Repeats;public readonly bool Landscape,IgnoreDriverMargins;
 PrintDiagnostics(LabelPrintDocument d,PrinterPageGeometry page,int repeats){
  Document=new SizeF((float)d.WidthMm,(float)d.HeightMm);ConfiguredMedia=new SizeF(page.Bounds.Width*.254f,page.Bounds.Height*.254f);CorrectedDocument=new SizeF((float)(d.WidthMm*d.ScaleX),(float)(d.HeightMm*d.ScaleY));
  var a=page.PrintableArea;Printable=new RectangleF(a.X*.254f,a.Y*.254f,a.Width*.254f,a.Height*.254f);HardMargins=new SizeF(page.HardMarginX*.254f,page.HardMarginY*.254f);
  X=d.OffsetX;Y=d.OffsetY;ScaleX=d.ScaleX;ScaleY=d.ScaleY;Pages=d.PageCount;Repeats=repeats;Landscape=page.Landscape;IgnoreDriverMargins=d.IgnoreDriverMargins;
 }
 public static PrintDiagnostics Capture(LabelPrintDocument document,PageSettings page,int repeats){if(document==null||page==null)throw new ArgumentNullException();if(repeats<1||repeats>32767)throw new ArgumentOutOfRangeException("repeats");new PrintCalibration{X=document.OffsetX,Y=document.OffsetY,ScaleX=document.ScaleX,ScaleY=document.ScaleY}.Validate();return new PrintDiagnostics(document,PrinterPageGeometry.Capture(page),repeats);}
 public static PrintDiagnostics Capture(LabelPrintDocument document,PrinterPageGeometry geometry,int repeats){if(document==null||geometry==null)throw new ArgumentNullException();if(repeats<1||repeats>32767)throw new ArgumentOutOfRangeException("repeats");new PrintCalibration{X=document.OffsetX,Y=document.OffsetY,ScaleX=document.ScaleX,ScaleY=document.ScaleY}.Validate();return new PrintDiagnostics(document,geometry,repeats);}
 static string N(double n){return n.ToString("0.######",CultureInfo.InvariantCulture);}
 static string Size(SizeF s){return N(s.Width)+" x "+N(s.Height);}
 public string Format(){return "Product=Pre-release\r\nBuild="+typeof(PrintPanel).Module.ModuleVersionId+"\r\nRuntime="+Environment.Version+"\r\nDocumentMm="+Size(Document)+"\r\nConfiguredDriverMediaMm="+Size(ConfiguredMedia)+"\r\nActualLoadedMedia=unverified\r\nCorrectedDocumentMm="+Size(CorrectedDocument)+"\r\nPrintableAreaMm="+N(Printable.X)+","+N(Printable.Y)+","+N(Printable.Width)+","+N(Printable.Height)+"\r\nHardMarginsMm="+Size(HardMargins)+"\r\nOffsetMm="+N(X)+","+N(Y)+"\r\nScale="+N(ScaleX)+","+N(ScaleY)+"\r\nLandscape="+Landscape+"\r\nIgnoreDriverMargins="+IgnoreDriverMargins+"\r\nPages="+Pages.ToString(CultureInfo.InvariantCulture)+"\r\nRepeats="+Repeats.ToString(CultureInfo.InvariantCulture)+"\r\n";}
 public string Summary(){return Loc.T("Dokument")+": "+Size(Document)+" mm\r\n"+Loc.T("Médium nastavené v ovládači")+": "+Size(ConfiguredMedia)+" mm\r\n"+Loc.T("Skutočne vložené médium: neoverené.")+"\r\n"+Loc.T("Mierka X (%)")+": "+N(ScaleX*100)+" % / "+Loc.T("Mierka Y (%)")+": "+N(ScaleY*100)+" %";}
}
public partial class PrintPanel {
 PrintDiagnostics printDiagnostics;ModernButton diagnosticsButton;
 void AddPrintDiagnostics(TableLayoutPanel fields){diagnosticsButton=Button(Loc.T("Diagnostika tlače…"),ShowPrintDiagnostics);diagnosticsButton.Enabled=false;diagnosticsButton.AccessibleName=Loc.T("Diagnostika tlače…");Add(fields,diagnosticsButton);}
 void ShowPrintDiagnostics(){var snapshot=printDiagnostics;if(snapshot==null||busy||sending)return;using(var form=new Form{Text=Loc.T("Diagnostika tlače…"),Size=new Size(720,600),MinimumSize=new Size(480,360),Font=Font,AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent}){
  var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=snapshot.Summary()+"\r\n\r\n"+snapshot.Format(),AccessibleName=Loc.T("Diagnostika tlače…")};form.Controls.Add(text);
  var save=Button(Loc.T("Uložiť diagnostiku…"),delegate{try{using(var dialog=new SaveFileDialog{Filter="Text|*.txt",FileName="ND-LabelDesigner-print-diagnostics.txt"})if(dialog.ShowDialog(form)==DialogResult.OK)AtomicOutput.Save(dialog.FileName,p=>File.WriteAllText(p,text.Text,new UTF8Encoding(false)));}catch(Exception ex){info.Text=ex.Message;}});save.Dock=DockStyle.Bottom;form.Controls.Add(save);if(calibrationStyle!=null)calibrationStyle(form);form.ShowDialog(FindForm());
 }}
}
}
