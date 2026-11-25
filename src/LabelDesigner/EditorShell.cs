using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  UiContextMenuStrip projectMenu,insertMenu;
  Button canvasZoomMode,zoomIn,zoomOut;
  static readonly double[] ZoomPresets={0,.5,1,1.5,2,3,4,6,8};
  static readonly double[] ZoomSteps={.125,.25,.5,.75,1,1.5,2,3,4,6,8};

  void BuildEditorCommands(TableLayoutPanel top) {
   top.ColumnStyles.Clear();
   top.ColumnCount=1;
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   headerCommands=editorTools=new FlowLayoutPanel {
    Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0),AccessibleName=Loc.T("Príkazy editora")
   };
   top.Controls.Add(headerCommands,0,0);
   headerCommands.SizeChanged+=delegate{UpdateToolbarLayout();};
   projectMenu=new UiContextMenuStrip();
   insertMenu=new UiContextMenuStrip();
   featureMenu=new UiContextMenuStrip();
   AddEditorMenu("projectCommand","Projekt",projectMenu,BuildProjectMenu);
   undoButton=HeaderAction("Späť",HeaderIcon.Undo,"Ctrl+Z",()=>StepHistory(false));
   redoButton=HeaderAction("Znova",HeaderIcon.Redo,"Ctrl+Y",()=>StepHistory(true));
   AddEditorCommand(undoButton);
   AddEditorCommand(redoButton);
   AddEditorMenu("insertCommand","Vložiť",insertMenu,BuildInsertMenu);
   AddEditorCommand(NamedEditorButton("batchCommand","Dávka",ShowBatch));

   viewMenu=new UiContextMenuStrip();
   viewMenu.Items.Add(Loc.T("Ľavý panel"),null,delegate{SetStudioPanel(0,studioBody.ColumnStyles[0].Width==0);});
   viewMenu.Items.Add(Loc.T("Vlastnosti"),null,delegate{SetStudioPanel(2,studioBody.ColumnStyles[2].Width==0);});
   viewMenu.Items.Add(Loc.T("Obnoviť rozloženie"),null,delegate{ResetStudioPanels();});
   viewMenu.Items.Add(Loc.T("Tabuľka"),null,delegate{tableSplit.Panel2Collapsed=!tableSplit.Panel2Collapsed;});
   AddCanvasViewOptions(viewMenu);
   viewMenu.Opening+=delegate{
    ((ToolStripMenuItem)viewMenu.Items[0]).Checked=studioBody.ColumnStyles[0].Width>0;
    ((ToolStripMenuItem)viewMenu.Items[1]).Checked=studioBody.ColumnStyles[2].Width>0;
    ((ToolStripMenuItem)viewMenu.Items[3]).Checked=!tableSplit.Panel2Collapsed;
   };
   AddEditorMenu("viewCommand","Zobrazenie",viewMenu);
   AddEditorMenu("moreCommand","Nástroje",featureMenu,BuildFeatureMenu);

   exportMenu=new UiContextMenuStrip();
   foreach(string format in new[]{"pdf","docx","zpl","svg"})
    exportMenu.Items.Add(format=="docx"?"Word":format.ToUpperInvariant(),null,delegate{Export(format);});
   AddEditorMenu("exportCommand","Exportovať",exportMenu);
   printPrimary=NamedEditorButton("printCommand","Tlač…",PrintLabel);
   printPrimary.AccessibleDescription="Ctrl+P";
   AddEditorCommand(printPrimary);
  }

  Button NamedEditorButton(string name,string title,Action action) {
   var button=Btn(Loc.T(title),action);
   button.Name=name;
   button.AccessibleName=button.Text;
   button.MinimumSize=new Size(36,36);
   button.UseMnemonic=false;
   button.FontChanged+=delegate{UpdateToolbarLayout();};
   return button;
  }

  void AddEditorCommand(Button button) {
   button.TabIndex=headerCommands.Controls.Count;
   headerCommands.Controls.Add(button);
  }

  void AddEditorMenu(string name,string title,ContextMenuStrip menu,Action prepare=null) {
   Button button=null;
   button=NamedEditorButton(name,title,()=>{prepare?.Invoke();menu.Show(button,new Point(0,button.Height));});
   button.AccessibleRole=AccessibleRole.ButtonMenu;
   AddEditorCommand(button);
  }

  Control BuildCanvasArea() {
   var area=new TableLayoutPanel {
    Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,AccessibleName=Loc.T("Plátno etikety")
   };
   area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   area.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   area.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   preview.Dock=DockStyle.Fill;
   preview.Compact=true;
   preview.Margin=Padding.Empty;
   preview.AccessibleName=Loc.T("Plátno etikety");
   area.Controls.Add(preview,0,0);

   zoomMenu=new UiContextMenuStrip();
   foreach(double value in ZoomPresets) {
    var item=new ToolStripMenuItem(ZoomCaption(value));
    item.Click+=delegate{preview.ViewZoom=value;};
    zoomMenu.Items.Add(item);
   }
   zoomMenu.Opening+=delegate{
    for(int i=0;i<ZoomPresets.Length;i++)((ToolStripMenuItem)zoomMenu.Items[i]).Checked=preview.ViewZoom==ZoomPresets[i];
   };
   var controls=new FlowLayoutPanel {
    Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Margin=Padding.Empty,Padding=new Padding(4,0,0,0),
    AccessibleName=Loc.T("Mierka plátna")
   };
   zoomOut=NamedEditorButton("canvasZoomOut","−",()=>AdjustCanvasZoom(false));
   zoomOut.AccessibleName=Loc.T("Zmenšiť náhľad");
   zoomIn=NamedEditorButton("canvasZoomIn","+",()=>AdjustCanvasZoom(true));
   zoomIn.AccessibleName=Loc.T("Zväčšiť náhľad");
   canvasZoomMode=NamedEditorButton("canvasZoomMode","Prispôsobiť",
    ()=>zoomMenu.Show(canvasZoomMode,new Point(0,0),ToolStripDropDownDirection.AboveRight));
   canvasZoomMode.AccessibleRole=AccessibleRole.ButtonMenu;
   controls.Controls.AddRange(new Control[]{zoomOut,canvasZoomMode,zoomIn});
   area.Controls.Add(controls,0,1);
   preview.ZoomChanged+=delegate{RefreshCanvasZoom();PositionInlineText();};
   RefreshCanvasZoom();
   return area;
  }

  static string ZoomCaption(double value)=>value==0?Loc.T("Prispôsobiť"):(value*100).ToString("0.#",CultureInfo.CurrentCulture)+" %";

  void RefreshCanvasZoom() {
   if(canvasZoomMode==null)return;
   canvasZoomMode.Text=ZoomCaption(preview.ViewZoom);
   canvasZoomMode.AccessibleName=Loc.T("Mierka plátna")+" · "+canvasZoomMode.Text;
  }

  void AdjustCanvasZoom(bool increase) {
   double scale=preview.ViewZoom;
   if(scale==0&&preview.Settings!=null)
    scale=preview.LabelRectangle().Width/preview.Settings.Width*25.4/preview.DeviceDpi;
   if(increase) {
    foreach(double step in ZoomSteps)if(step>scale+.01){preview.ViewZoom=step;return;}
   } else {
    for(int i=ZoomSteps.Length-1;i>=0;i--)if(ZoomSteps[i]<scale-.01){preview.ViewZoom=ZoomSteps[i];return;}
   }
  }

  void BuildEditorStatus() {
   status.Dock=DockStyle.Fill;
   status.AutoEllipsis=true;
   status.TextAlign=ContentAlignment.MiddleLeft;
   status.Padding=new Padding(8,0,0,0);
   var footer=new TableLayoutPanel {
    Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty,AccessibleName=Loc.T("Stav editora")
   };
   footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   footer.Controls.Add(status,0,0);
   var checks=NamedEditorButton("openEditorChecks","Kontrola",FocusProblems);
   footer.Controls.Add(checks,1,0);
   footer.AutoSize=true;
   studio.RowStyles[3].SizeType=SizeType.AutoSize;
   studio.Controls.Add(footer,0,3);
   status.Cursor=Cursors.Hand;
   status.Click+=delegate{FocusProblems();};
  }
 }
}
