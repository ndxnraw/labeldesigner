using System;using System.Drawing;using System.IO;using System.Windows.Forms;using System.Runtime.InteropServices;
namespace EanEtikety {
public partial class MainForm {
 Control headerSurface;FlowLayoutPanel headerCommands,editorTools;
 bool updatingTitle,layingOutHeader,windowActive=true,keyboardHeaderFocus,captionTracking;
 readonly Timer captionDelay=new Timer{Interval=400};
 [StructLayout(LayoutKind.Sequential)]struct MouseTracking {public int Size,Flags;public IntPtr Window;public int Hover;}
 [DllImport("user32.dll")]static extern bool TrackMouseEvent(ref MouseTracking tracking);
 void UpdateWindowTitle(){if(studio==null||updatingTitle)return;updatingTitle=true;try{string document=settingsPath==null?Loc.T("Nová etiketa"):Path.GetFileName(settingsPath);string title="ND LabelDesigner  |  "+(dirty?"• ":"")+document;if(Text!=title)Text=title;AccessibleName=title+(dirty?" — "+Loc.T("ND LabelDesigner • neuložené zmeny").Replace("ND LabelDesigner • ",""):"");}finally{updatingTitle=false;}}
 void InitializeWindowHeader(){
  Shown+=delegate{ActiveControl=preview;preview.Focus();HideHeaderHints();UpdateToolbarLayout();};
  Activated+=delegate{windowActive=true;RefreshHeaderSurface();};Deactivate+=delegate{windowActive=false;HideHeaderHints();RefreshHeaderSurface();};
  DpiChanged+=delegate{BeginInvoke((Action)delegate{RefreshHeaderActions();UpdateToolbarLayout();});};
  captionDelay.Tick+=delegate{captionDelay.Stop();if(windowActive&&WindowState!=FormWindowState.Minimized&&captionTracking){headerTips.SetToolTip(this,Text);headerTips.Show(Text,this,PointToClient(Cursor.Position).X,0,3000);}};
  UpdateWindowTitle();RefreshHeaderSurface();
 }
 void HideHeaderHints(){captionDelay.Stop();captionTracking=false;headerTips.Hide(this);headerTips.SetToolTip(this,null);foreach(var item in headerActions){headerTips.Hide(item.Key);item.Key.ResetInteraction();}}
 void RefreshHeaderSurface(){if(headerSurface==null)return;Color background=StudioTheme.Panel(dark);studio.BackColor=background;headerSurface.BackColor=background;headerCommands.BackColor=background;if(IsHandleCreated)SetTitleTheme();headerSurface.Invalidate(true);}
 // Calculate from child preferred sizes, never from the wrapped panel's current height.
 public static int ToolbarHeight(FlowLayoutPanel panel,int width,int minimum){int x=0,row=0,total=0;foreach(Control c in panel.Controls){if(!c.Visible)continue;Size size=c.AutoSize?c.GetPreferredSize(Size.Empty):c.Size;int w=size.Width+c.Margin.Horizontal,h=Math.Max(size.Height,c.Height)+c.Margin.Vertical;if(x>0&&x+w>width){total+=row;x=0;row=0;}x+=w;row=Math.Max(row,h);}return Math.Max(minimum,total+row+panel.Padding.Vertical);}
 void MergeToolbar(TableLayoutPanel top,FlowLayoutPanel commands,FlowLayoutPanel tools){
  top.ColumnStyles.Clear();top.ColumnCount=1;top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.SetColumn(commands,0);commands.AutoSize=false;commands.WrapContents=true;
  while(tools.Controls.Count>0){var c=tools.Controls[0];c.TabIndex=commands.Controls.Count;commands.Controls.Add(c);}studio.Controls.Remove(tools);editorTools=commands;tools.Dispose();studio.RowStyles[1].Height=0;commands.SizeChanged+=delegate{UpdateToolbarLayout();};
 }
 void UpdateToolbarLayout(){if(studio==null||editorTools==null||layingOutHeader||WindowState==FormWindowState.Minimized||ClientSize.Width<=0||editorTools.Width<=0)return;layingOutHeader=true;try{float dpi=DeviceDpi/96f;studio.RowStyles[1].Height=0;float height=ToolbarHeight(editorTools,editorTools.ClientSize.Width,(int)Math.Ceiling(44*dpi));if(Math.Abs(studio.RowStyles[0].Height-height)>.5)studio.RowStyles[0].Height=height;}finally{layingOutHeader=false;}}
 protected override void OnSystemColorsChanged(EventArgs e){base.OnSystemColorsChanged(e);if(studio!=null)ApplyTheme();}
 protected override void OnResize(EventArgs e){base.OnResize(e);if(WindowState==FormWindowState.Minimized){if(headerSurface!=null)HideHeaderHints();return;}UpdateToolbarLayout();}
 protected override void WndProc(ref Message m){
  // Observe native caption hover only. All non-client hit tests and window commands
  // remain handled by Windows, including Snap, resizing and the system menu.
  if(m.Msg==0xA0&&m.WParam.ToInt32()==2&&headerSurface!=null){if(!captionTracking){captionTracking=true;captionDelay.Start();var tracking=new MouseTracking{Size=Marshal.SizeOf(typeof(MouseTracking)),Flags=0x12,Window=Handle};TrackMouseEvent(ref tracking);}}
  else if(m.Msg==0x2A2||m.Msg==0xA1||m.Msg==0x201||m.Msg==0x232){if(headerSurface!=null)HideHeaderHints();}
  base.WndProc(ref m);
 }
}
}
