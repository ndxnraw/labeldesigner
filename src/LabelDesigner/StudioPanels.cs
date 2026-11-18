using System;using System.Drawing;using System.Globalization;using System.Windows.Forms;
namespace EanEtikety {
public partial class MainForm {
 void FitLayerActions(TableLayoutPanel layout,TableLayoutPanel actions){bool fitting=false;EventHandler fit=delegate{if(fitting||actions.ClientSize.Width<40)return;fitting=true;try{var heights=new int[2];foreach(Control button in actions.Controls){int row=actions.GetRow(button),width=Math.Max(1,actions.ClientSize.Width/2-button.Margin.Horizontal-16);heights[row]=Math.Max(heights[row],TextRenderer.MeasureText(button.Text,button.Font,new Size(width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+12+button.Margin.Vertical);}int total=heights[0]+heights[1];for(int i=0;i<2;i++){actions.RowStyles[i].SizeType=SizeType.Absolute;actions.RowStyles[i].Height=heights[i];}layout.RowStyles[1].Height=total;layout.AutoScrollMinSize=new Size(0,total+Math.Max(60,layout.Font.Height*4));}finally{fitting=false;}};actions.SizeChanged+=fit;foreach(Control button in actions.Controls){button.FontChanged+=fit;button.TextChanged+=fit;}fit(null,EventArgs.Empty);}
 float leftPanelWidth=240,inspectorPanelWidth=300;bool studioPanelsReady;
 float PanelScale{get{return IsHandleCreated?Math.Max(1,DeviceDpi/96f):1;}}
 void LoadStudioPanels(string[] lines){leftPanelWidth=ReadPanelWidth(lines,4,240);inspectorPanelWidth=ReadPanelWidth(lines,5,300);bool left=lines[1]=="1",right=lines[2]=="1";studioBody.ColumnStyles[0].Width=left?leftPanelWidth:0;studioBody.ColumnStyles[2].Width=right?inspectorPanelWidth:0;studioBody.GetControlFromPosition(0,0).Visible=left;studioBody.GetControlFromPosition(2,0).Visible=right;Shown+=delegate{SetStudioPanel(0,left);SetStudioPanel(2,right);};}
 void SetStudioPanel(int column,bool visible){
  if(studioBody==null)return;var style=studioBody.ColumnStyles[column];var host=studioBody.GetControlFromPosition(column,0);
  if(studioPanelsReady&&!visible&&style.Width>0){if(column==0)leftPanelWidth=style.Width/PanelScale;else inspectorPanelWidth=style.Width/PanelScale;}
  studioBody.SuspendLayout();try{if(!visible)host.Visible=false;style.SizeType=SizeType.Absolute;style.Width=visible?(column==0?leftPanelWidth:inspectorPanelWidth)*PanelScale:0;if(visible)host.Visible=true;}finally{studioBody.ResumeLayout(true);}
  if(visible){RelayoutStudioPanel(host);if(IsHandleCreated)BeginInvoke((Action)delegate{if(!IsDisposed&&host.Visible)RelayoutStudioPanel(host);});}
 }
 static void RelayoutStudioPanel(Control host){var scroll=host as ScrollableControl;if(scroll!=null)scroll.AutoScrollPosition=new Point(0,-scroll.AutoScrollPosition.Y);foreach(Control child in host.Controls)RelayoutStudioPanel(child);host.PerformLayout();var tabs=host as ModernTabs;if(tabs!=null)tabs.RefreshTabs();}
 void ResetStudioPanels(){leftPanelWidth=240;inspectorPanelWidth=300;SetStudioPanel(0,true);SetStudioPanel(2,true);tableSplit.Panel2Collapsed=true;}
 static float ReadPanelWidth(string[] lines,int index,float fallback){float value;return lines.Length>index&&float.TryParse(lines[index],NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&value>=160&&value<=600?value:fallback;}
 void RememberStudioPanelWidths(){if(studioBody.ColumnStyles[0].Width>0)leftPanelWidth=studioBody.ColumnStyles[0].Width/PanelScale;if(studioBody.ColumnStyles[2].Width>0)inspectorPanelWidth=studioBody.ColumnStyles[2].Width/PanelScale;}
}
}
