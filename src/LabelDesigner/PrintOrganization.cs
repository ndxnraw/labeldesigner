using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
public partial class PrintPanel {
 void FitCommonSettings(TableLayoutPanel side,TableLayoutPanel fields){
  bool fitting=false;System.Action fit=delegate{if(fitting||side.IsDisposed)return;fitting=true;try{
   int settings=(int)printSections.RowStyles[0].Height+System.Math.Max(100,Font.Height*4);
   int height=System.Math.Min(fields.PreferredSize.Height,System.Math.Max(70,side.ClientSize.Height-settings));
   if(side.RowStyles[0].SizeType!=SizeType.Absolute||side.RowStyles[0].Height!=height){side.RowStyles[0].SizeType=SizeType.Absolute;side.RowStyles[0].Height=height;}
  }finally{fitting=false;}};
  side.SizeChanged+=delegate{fit();};fields.SizeChanged+=delegate{fit();};printSections.Layout+=delegate{fit();};FontChanged+=delegate{fit();};
 }
 bool summaryLayout,summaryLayoutPending;TableLayoutPanel printDetails;
 void LayoutPrintSummary(){if(summaryLayout||jobSummary.Parent==null)return;summaryLayout=true;try{var container=(TableLayoutPanel)jobSummary.Parent;int width=System.Math.Max(100,container.ClientSize.Width-container.Padding.Horizontal-12);jobSummary.MaximumSize=new Size(width,0);jobInfo.MaximumSize=new Size(290,0);jobSummary.AutoSize=false;jobSummary.Height=TextRenderer.MeasureText(jobSummary.Text,jobSummary.Font,new Size(width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+4;int height=container.Padding.Vertical+jobSummary.Height+jobSummary.Margin.Vertical;foreach(Control child in container.Controls)if(child!=jobSummary&&child.Visible)height+=child.GetPreferredSize(new Size(width,0)).Height+child.Margin.Vertical;var parent=container.Parent as TableLayoutPanel;if(parent!=null){int row=parent.GetRow(container);if(row<0||row>=parent.RowStyles.Count)return;parent.RowStyles[row].SizeType=SizeType.Absolute;parent.RowStyles[row].Height=System.Math.Min(150,height);container.AutoSize=false;container.Dock=DockStyle.Fill;if(parent.IsHandleCreated&&!summaryLayoutPending&&parent.GetRowHeights()[row]<(int)parent.RowStyles[row].Height){summaryLayoutPending=true;parent.BeginInvoke((System.Action)delegate{summaryLayoutPending=false;if(!parent.IsDisposed)parent.PerformLayout(container,"Height");});}}}finally{summaryLayout=false;}}
 readonly ModernTabs printSections=new ModernTabs{Dock=DockStyle.Fill,Name="printSections",AccessibleName=Loc.T("Nastavenia tlače")};
 TableLayoutPanel PrintSection(string name){var page=printSections.AddPage(Loc.T(name));var stack=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Padding=new Padding(8)};stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));page.Controls.Add(stack);return stack;}
 void OrganizePrintSettings(TableLayoutPanel side){
  advancedFields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=1,Margin=new Padding(0)};advancedFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));advancedFields.RowStyles.Add(new RowStyle(SizeType.Percent,100));advancedFields.Controls.Add(printSections,0,0);side.Controls.Add(advancedFields,0,1);
  TableLayoutPanel media=PrintSection("Médium"),calibration=PrintSection("Kalibrácia"),zebra=PrintSection("Zebra"),diagnostics=PrintSection("Detaily");printDetails=diagnostics;
  ProfileSelector(media);Add(media,new Label{Text=Loc.T("Papier"),AutoSize=true});papers.DropDownStyle=ComboBoxStyle.DropDownList;papers.DisplayMember="PaperName";papers.AccessibleName=Loc.T("Papier");Add(media,papers);landscape.Text=Loc.T("Na šírku");landscape.AutoSize=true;Add(media,landscape);paperInfo.AutoSize=true;paperInfo.MaximumSize=new Size(290,0);paperInfo.UseMnemonic=false;Add(media,paperInfo);
  driverProperties=Button(Loc.T("Vlastnosti tlačiarne…"),delegate{ConfigurePrinter(d=>d.ShowDialog(FindForm()));});Add(media,driverProperties);ProfileControls(calibration,media,zebra);Add(diagnostics,new Label{Text=Loc.T("Diagnostika popisuje aktuálnu tlačovú úlohu a nastavenia výstupu."),AutoSize=true,MaximumSize=new Size(290,0)});AddPrintDiagnostics(diagnostics);AddHistoryOption(diagnostics);
 }
}
}
