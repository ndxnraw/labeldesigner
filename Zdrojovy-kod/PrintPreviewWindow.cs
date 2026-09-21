using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
namespace EanEtikety {
// Preview is deliberately opened before PrintDialog: the Win32 dialog does not
// consume WinForms PrintDocument preview pages. Both paths use OnPrintPage.
public class PrintPreviewWindow:Form {
 readonly LabelPrintDocument document;
 readonly PrintPreviewControl preview=new PrintPreviewControl();
 readonly Button print=new Button();readonly Label info=new Label();
 readonly NumericUpDown page=new NumericUpDown();readonly ComboBox zoom=new ComboBox();
 bool configured;
 public bool Submitted {get;private set;}
 public PrintPreviewWindow(LabelPrintDocument doc){document=doc;Text=Loc.T("Náhľad pred tlačou");Size=new Size(1080,850);MinimumSize=new Size(800,600);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);ShowInTaskbar=false;
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,80));Controls.Add(layout);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(8)};layout.Controls.Add(bar,0,0);
  var setup=new Button{Text=Loc.T("Tlačiareň a nastavenia…"),AutoSize=true};setup.Click+=delegate{ConfigurePrinter();};bar.Controls.Add(setup);
  print.Text=Loc.T("Tlačiť");print.AutoSize=true;print.Enabled=false;print.Click+=delegate{Submit();};bar.Controls.Add(print);
  bar.Controls.Add(new Label{Text=Loc.T("Strana:"),AutoSize=true,Margin=new Padding(12,9,3,0)});page.Minimum=1;page.Maximum=doc.PageCount;page.Value=1;page.Width=55;page.ValueChanged+=delegate{preview.StartPage=(int)page.Value-1;};bar.Controls.Add(page);bar.Controls.Add(new Label{Text="/ "+doc.PageCount,AutoSize=true,Margin=new Padding(0,9,8,0)});
  zoom.DropDownStyle=ComboBoxStyle.DropDownList;zoom.Width=125;zoom.Items.AddRange(new object[]{Loc.T("Celá strana"),"50 %","100 %","150 %","200 %","400 %"});zoom.SelectedIndex=0;zoom.SelectedIndexChanged+=delegate{preview.AutoZoom=zoom.SelectedIndex==0;if(zoom.SelectedIndex>0)preview.Zoom=new[]{.5,1,1.5,2,4}[zoom.SelectedIndex-1];};bar.Controls.Add(zoom);
  var close=new Button{Text=Loc.T("Zrušiť"),AutoSize=true,DialogResult=DialogResult.Cancel};bar.Controls.Add(close);CancelButton=close;
  preview.Dock=DockStyle.Fill;preview.Document=doc;preview.AutoZoom=true;preview.UseAntiAlias=true;layout.Controls.Add(preview,0,1);
  info.Dock=DockStyle.Fill;info.Padding=new Padding(10,5,10,5);layout.Controls.Add(info,0,2);RefreshInfo();
 }
 void RefreshInfo(){info.Text=Loc.T("Náhľad aplikácie používa rovnaký dokument ako tlač. Systémový dialóg WinForms tento náhľad nezobrazuje.")+"\r\n"+(configured?document.PrinterSettings.PrinterName+" | "+Loc.T("Kópie:")+" "+document.PrinterSettings.Copies+" | "+(document.DefaultPageSettings.Bounds.Width*.254).ToString("0.##")+" × "+(document.DefaultPageSettings.Bounds.Height*.254).ToString("0.##")+" mm":Loc.T("Vyberte tlačiareň. Potom skontrolujte obnovený náhľad a stlačte Tlačiť."));}
 public bool ConfigurePrinter(){return ConfigurePrinter(dialog=>dialog.ShowDialog(this));}
 public bool ConfigurePrinter(Func<PrintDialog,DialogResult> show){var oldPrinter=(PrinterSettings)document.PrinterSettings.Clone();var oldPage=(PageSettings)document.DefaultPageSettings.Clone();bool accepted=false;
  try{using(var dialog=new PrintDialog{Document=document,UseEXDialog=true,AllowSelection=false,AllowSomePages=false,AllowCurrentPage=false,AllowPrintToFile=false}){
    if(show(dialog)!=DialogResult.OK)return false;
    document.ValidatePrinter();document.DefaultPageSettings.Margins=new Margins(0,0,0,0);accepted=true;configured=true;print.Enabled=true;return true;
  }}catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
  finally{if(!accepted){document.PrinterSettings=oldPrinter;document.DefaultPageSettings=oldPage;}preview.InvalidatePreview();RefreshInfo();}
 }
 void Submit(){if(!configured)return;try{document.ValidatePrinter();var warnings=document.PrintWarnings();if(warnings.Count>0&&MessageBox.Show(this,string.Join("\r\n",warnings.ToArray())+"\r\n\r\n"+Loc.T("Pokračovať v tlači?"),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   document.PrintController=new PrintControllerWithStatusDialog(new StandardPrintController());document.Print();Submitted=!document.Cancelled;if(Submitted){DialogResult=DialogResult.OK;Close();}
  }catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
 protected override void Dispose(bool disposing){if(disposing)preview.Document=null;base.Dispose(disposing);}
}
}
