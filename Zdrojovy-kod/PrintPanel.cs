using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
namespace EanEtikety {
// Zoom belongs to the screen canvas only; RenderPage always uses millimetres.
public class PrintCanvas:ScrollableControl {
 public LabelPrintDocument Document; PreviewPageInfo[] pages; public double Zoom; public int Page;
 public SizeF Paper {get;set;} public RectangleF Printable {get;set;}
 public PrintCanvas(){DoubleBuffered=true;AutoScroll=true;TabStop=true;Resize+=delegate{Invalidate();};}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Document==null||Paper.Width<=0||Paper.Height<=0)return;
  double scale=Zoom==0?Math.Max(.1,Math.Min((ClientSize.Width-48)/Paper.Width,(ClientSize.Height-48)/Paper.Height)):Zoom*e.Graphics.DpiX/25.4;
  int w=(int)Math.Round(Paper.Width*scale),h=(int)Math.Round(Paper.Height*scale);
  var extent=Zoom==0?Size.Empty:new Size(w+48,h+48);if(AutoScrollMinSize!=extent)AutoScrollMinSize=extent;
  float x=Math.Max(24,(ClientSize.Width-w)/2)+AutoScrollPosition.X,y=Math.Max(24,(ClientSize.Height-h)/2)+AutoScrollPosition.Y;
  e.Graphics.FillRectangle(Brushes.White,x,y,w,h);
  if(pages!=null&&Page<pages.Length)e.Graphics.DrawImage(pages[Page].Image,new RectangleF(x,y,w,h));
  if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,ClientSize.Width-6,ClientSize.Height-6));
 } 
 public void ClearPages(){if(pages!=null)foreach(var p in pages)p.Image.Dispose();pages=null;Invalidate();}
 public void GeneratePages(){ClearPages();var original=Document.PrintController;var controller=new PreviewPrintController{UseAntiAlias=true};try{Document.PrintController=controller;Document.Print();pages=controller.GetPreviewPageInfo();if(pages.Length==0)throw new InvalidOperationException(Loc.T("Dokument neobsahuje nič na tlač."));var size=pages[0].PhysicalSize;Paper=new SizeF(size.Width*.254f,size.Height*.254f);Page=Math.Min(Page,pages.Length-1);}finally{Document.PrintController=original;}Invalidate();}
 protected override void Dispose(bool disposing){if(disposing)ClearPages();base.Dispose(disposing);}

}
public class PrintPanel:UserControl {
 readonly LabelPrintDocument document;
 readonly PrintCanvas preview=new PrintCanvas();
 readonly ModernComboBox printers=new ModernComboBox(),zoom=new ModernComboBox(),papers=new ModernComboBox();
 readonly NumericUpDown copies=new NumericUpDown(),page=new NumericUpDown();
 readonly ModernButton print=new ModernButton();readonly Label info=new Label(),paperInfo=new Label(),printerName=new Label();readonly ToolTip printerTip=new ToolTip();
 readonly FlowLayoutPanel pageTools=new FlowLayoutPanel();bool updating;
 public event EventHandler BackRequested;
 public PrintPanel(LabelPrintDocument doc){document=doc;Dock=DockStyle.Fill;Font=new Font("Segoe UI",10);Padding=new Padding(14);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
  layout.Controls.Add(new Label{Text="ND LabelDesigner  /  "+Loc.T("Tlačiť"),Font=new Font("Segoe UI",21,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
  var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,320));layout.Controls.Add(body,0,1);
  var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=new Padding(0,0,14,0)};left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(left,0,0);
  var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Padding=new Padding(8)};left.Controls.Add(tools,0,0);tools.Controls.Add(new Label{Text=Loc.T("Náhľad etikety"),AutoSize=true,Margin=new Padding(0,7,16,0)});
  zoom.Width=145;zoom.DropDownStyle=ComboBoxStyle.DropDownList;zoom.Items.AddRange(new object[]{Loc.T("Celá strana"),"50 %","100 %","150 %","200 %","400 %"});zoom.SelectedIndex=0;zoom.SelectedIndexChanged+=delegate{preview.Zoom=zoom.SelectedIndex==0?0:new[]{.5,1,1.5,2,4}[zoom.SelectedIndex-1];preview.AutoScrollPosition=Point.Empty;preview.Invalidate();};tools.Controls.Add(zoom);
  pageTools.AutoSize=true;pageTools.Visible=doc.PageCount>1;pageTools.Controls.Add(new Label{Text=Loc.T("Strana:"),AutoSize=true,Margin=new Padding(8,7,0,0)});page.Minimum=1;page.Maximum=doc.PageCount;page.Width=55;page.ValueChanged+=delegate{preview.Page=(int)page.Value-1;preview.Invalidate();};pageTools.Controls.Add(page);pageTools.Controls.Add(new Label{Text="/ "+doc.PageCount,AutoSize=true,Margin=new Padding(0,7,0,0)});tools.Controls.Add(pageTools);
  preview.Dock=DockStyle.Fill;preview.Document=doc;left.Controls.Add(preview,0,1);
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(12)};body.Controls.Add(scroll,1,0);
  var settings=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(2)};settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(settings);
  Add(settings,new Label{Text=Loc.T("Tlačiareň"),AutoSize=true});printers.DropDownStyle=ComboBoxStyle.DropDownList;printers.Width=265;printers.AccessibleName=Loc.T("Tlačiareň");Add(settings,printers);printerName.AutoSize=true;printerName.MaximumSize=new Size(265,0);printerName.UseMnemonic=false;Add(settings,printerName);printers.DropDown+=delegate{int width=printers.Width;foreach(var item in printers.Items)width=Math.Max(width,TextRenderer.MeasureText(Convert.ToString(item),printers.Font).Width+40);printers.DropDownWidth=Math.Min(width,Screen.FromControl(this).WorkingArea.Width-40);};
  Add(settings,new Label{Text=Loc.T("Počet kópií"),AutoSize=true});copies.Minimum=1;copies.Maximum=32767;copies.Value=1;copies.AccessibleName=Loc.T("Počet kópií");Add(settings,copies);
  var label=doc.LabelBounds(0);Add(settings,new Label{Text=Loc.T("Etiketa")+": "+label.Width.ToString("0.##")+" × "+label.Height.ToString("0.##")+" mm",AutoSize=true,Padding=new Padding(0,10,0,10)});
  Add(settings,new Label{Text=Loc.T("Papier"),AutoSize=true});papers.DropDownStyle=ComboBoxStyle.DropDownList;papers.DisplayMember="PaperName";Add(settings,papers);Add(settings,paperInfo);papers.SelectedIndexChanged+=delegate{if(!updating&&papers.SelectedItem is PaperSize){document.DefaultPageSettings.PaperSize=(PaperSize)papers.SelectedItem;RefreshPreview();}};
  var advanced=new CheckBox{Text=Loc.T("Rozšírené nastavenia"),AutoSize=true};Add(settings,advanced);
  var extra=new TableLayoutPanel{AutoSize=true,ColumnCount=1,Visible=false,Dock=DockStyle.Top};extra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paperInfo.AutoSize=true;paperInfo.MaximumSize=new Size(265,0);Add(extra,Button(Loc.T("Vlastnosti tlačiarne…"),delegate{ConfigurePrinter(d=>d.ShowDialog(FindForm()));}));Add(settings,extra);advanced.CheckedChanged+=delegate{extra.Visible=advanced.Checked;};
  print.Text=Loc.T("Tlačiť");print.Height=46;print.FlatStyle=FlatStyle.Flat;print.Click+=delegate{Submit();};Add(settings,print);Add(settings,Button(Loc.T("Späť do editora"),Back));info.AutoSize=true;info.MaximumSize=new Size(265,0);info.Padding=new Padding(0,8,0,0);Add(settings,info);
  try{foreach(string name in PrinterSettings.InstalledPrinters)printers.Items.Add(name);printers.SelectedItem=doc.PrinterSettings.PrinterName;}catch(Exception ex){info.Text=ex.Message;}
  printers.SelectedIndexChanged+=delegate{if(!updating)SelectPrinter(Convert.ToString(printers.SelectedItem));};copies.ValueChanged+=delegate{if(!updating)document.PrinterSettings.Copies=(short)copies.Value;};SelectPrinter(doc.PrinterSettings.PrinterName);
 }
 static void Add(TableLayoutPanel panel,Control c){c.Dock=DockStyle.Top;c.Margin=new Padding(0,5,0,7);c.TabIndex=panel.Controls.Count;panel.Controls.Add(c,0,panel.RowCount++);panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));}
 static ModernButton Button(string text,Action action){var b=new ModernButton{Text=text,Height=40,FlatStyle=FlatStyle.Flat};b.Click+=delegate{action();};return b;}
 public void Style(Action<Control> apply,bool dark){apply(this);preview.BackColor=dark?Color.FromArgb(15,24,33):Color.FromArgb(220,230,240);print.BackColor=Color.FromArgb(0,123,245);print.ForeColor=Color.White;print.FlatAppearance.BorderColor=Color.FromArgb(48,158,255);}
 public void SelectPrinter(string name){try{var next=new PrinterSettings{PrinterName=name,Copies=(short)copies.Value};var selectedPage=(PageSettings)next.DefaultPageSettings.Clone();selectedPage.Margins=new Margins(0,0,0,0);document.PrinterSettings=next;document.DefaultPageSettings=selectedPage;RefreshPreview();}catch(Exception ex){ShowFailure(ex.Message);}}
 void ShowFailure(string text){preview.ClearPages();info.Text=text;info.Visible=true;print.Enabled=false;}
 void UpdatePapers(){updating=true;try{papers.Items.Clear();if(document.PrinterSettings.IsValid){var current=document.DefaultPageSettings.PaperSize;PaperSize selected=null;foreach(PaperSize size in document.PrinterSettings.PaperSizes){papers.Items.Add(size);if(size.RawKind==current.RawKind&&size.Width==current.Width&&size.Height==current.Height)selected=size;}if(selected==null){selected=current;papers.Items.Add(selected);}papers.SelectedItem=selected;}}finally{updating=false;}}
 public void RefreshPreview(){paperInfo.Text="";UpdatePapers();printerName.Text=document.PrinterSettings.PrinterName;printerTip.SetToolTip(printers,printerName.Text);printerTip.SetToolTip(printerName,printerName.Text);preview.Paper=new SizeF((float)document.WidthMm,(float)document.HeightMm);preview.Printable=RectangleF.Empty;
  try{var p=document.DefaultPageSettings;preview.Paper=new SizeF(p.Bounds.Width*.254f,p.Bounds.Height*.254f);var a=p.PrintableArea;preview.Printable=new RectangleF(a.X*.254f,a.Y*.254f,a.Width*.254f,a.Height*.254f);paperInfo.Text=Loc.T("Papier")+": "+preview.Paper.Width.ToString("0.##")+" × "+preview.Paper.Height.ToString("0.##")+" mm\r\n"+Loc.T("Mierka tlače: 100 %");document.ValidatePrinter();preview.GeneratePages();info.Text=string.Join("\r\n",document.PrintWarnings().ToArray());info.Visible=info.Text.Length>0;print.Enabled=true;
  }catch(Exception ex){ShowFailure(printers.Items.Count==0?Loc.T("Vo Windows nie je nainštalovaná tlačiareň."):ex.Message);}preview.Invalidate();
 }
 public bool ConfigurePrinter(Func<PrintDialog,DialogResult> show){var oldPrinter=(PrinterSettings)document.PrinterSettings.Clone();var oldPage=(PageSettings)document.DefaultPageSettings.Clone();bool accepted=false;
  try{using(var dialog=new PrintDialog{Document=document,UseEXDialog=true,AllowSelection=false,AllowSomePages=false,AllowCurrentPage=false,AllowPrintToFile=false}){if(show(dialog)!=DialogResult.OK)return false;document.ValidatePrinter();accepted=true;return true;}}
  catch(Exception ex){MessageBox.Show(this,ex.Message,Loc.T("Tlačiť"),MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
  finally{if(!accepted){document.PrinterSettings=oldPrinter;document.DefaultPageSettings=oldPage;}updating=true;printers.SelectedItem=document.PrinterSettings.PrinterName;copies.Value=Math.Max(copies.Minimum,Math.Min(copies.Maximum,document.PrinterSettings.Copies));updating=false;RefreshPreview();}
 }
 void Submit(){try{var warnings=document.PrintWarnings();if(warnings.Count>0&&MessageBox.Show(this,string.Join("\r\n",warnings.ToArray())+"\r\n"+Loc.T("Pokračovať v tlači?"),Loc.T("Tlačiť"),MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;SendToPrinter(delegate(LabelPrintDocument d){d.PrintController=new PrintControllerWithStatusDialog(new StandardPrintController(),Loc.T("Tlačiť"));d.Print();});}catch(Exception ex){ShowFailure(Loc.T("Tlač sa nepodarila: ")+ex.Message);}}
 public void SendToPrinter(Action<LabelPrintDocument> send){try{document.ValidatePrinter();print.Enabled=false;send(document);info.Text=document.Cancelled?Loc.T("Tlač bola zrušená."):Loc.T("Úloha bola odoslaná do tlačového systému Windows.");info.Visible=true;}catch(Exception ex){info.Text=Loc.T("Tlač sa nepodarila: ")+ex.Message;info.Visible=true;}finally{print.Enabled=document.PrinterSettings.IsValid;}} void Back(){if(BackRequested!=null)BackRequested(this,EventArgs.Empty);}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){Back();return true;}return base.ProcessCmdKey(ref msg,keyData);}
 protected override void Dispose(bool disposing){if(disposing){preview.ClearPages();preview.Document=null;printerTip.Dispose();document.Dispose();}base.Dispose(disposing);}
}
}




