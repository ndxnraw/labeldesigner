using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
namespace EanEtikety {
// Read-only selection explanations remain reachable when all edit actions are disabled.
public class FocusText:Label {
 public FocusText(){SetStyle(ControlStyles.Selectable,true);TabStop=true;AccessibleRole=AccessibleRole.StaticText;}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(Focused&&Width>4&&Height>4)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(1,1,Width-3,Height-3),ForeColor,BackColor);}
}
public class ModernComboBox:ComboBox {
 IntPtr nativeFont;
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr handle);
 // UserPaint bypasses WinForms' automatic WM_SETFONT. The native dropdown list
 // still needs the inherited family; its HFONT lives as long as the HWND.
 void SyncNativeFont(){if(!IsHandleCreated)return;var next=Font.ToHfont();SendMessage(Handle,0x30,next,new IntPtr(1));var old=nativeFont;nativeFont=next;if(old!=IntPtr.Zero)DeleteObject(old);}
 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);SyncNativeFont();}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SyncNativeFont();}
 protected override void OnHandleDestroyed(EventArgs e){base.OnHandleDestroyed(e);if(nativeFont!=IntPtr.Zero){DeleteObject(nativeFont);nativeFont=IntPtr.Zero;}}

 public ModernComboBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;}
 protected override void OnPaint(PaintEventArgs e){bool hc=SystemInformation.HighContrast;Color back=hc?SystemColors.Window:BackColor,ink=!Enabled?SystemColors.GrayText:hc?SystemColors.WindowText:ForeColor;e.Graphics.Clear(back);if(Width<4||Height<4)return;using(var pen=new Pen(hc?SystemColors.WindowText:Color.FromArgb(85,107,128)))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);if(Width>32)TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(9,1,Width-32,Height-2),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if(Width>24)using(var pen=new Pen(ink,1.5f)){int x=Width-17,y=Height/2;e.Graphics.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}if(Focused&&Width>32&&Height>6)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-27,Height-6),ink,back);}
 protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);Invalidate();}
 protected override void OnSystemColorsChanged(EventArgs e){base.OnSystemColorsChanged(e);Invalidate();}
}
public class ModernButton:Button {
 protected virtual bool SelectedVisual{get{return false;}}
 public ModernButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;bool hc=SystemInformation.HighContrast,hot=Enabled&&ClientRectangle.Contains(PointToClient(Cursor.Position));
  Color back=hc?(hot||SelectedVisual?SystemColors.Highlight:SystemColors.Control):hot?FlatAppearance.MouseOverBackColor:BackColor;
  Color ink=!Enabled?SystemColors.GrayText:hc?(hot||SelectedVisual?SystemColors.HighlightText:SystemColors.ControlText):ForeColor;
  g.Clear(hc?SystemColors.Control:Parent==null?BackColor:Parent.BackColor);if(Width<4||Height<4)return;
  g.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(1,1,Width-3,Height-3);int d=Math.Min(10,Math.Min(r.Width,r.Height));
  using(var path=new GraphicsPath()){path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();using(var b=new SolidBrush(back))g.FillPath(b,path);using(var p=new Pen(hc?SystemColors.WindowText:FlatAppearance.BorderColor))g.DrawPath(p,path);}
  var flags=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.PreserveGraphicsClipping;
  flags|=!UseMnemonic?TextFormatFlags.NoPrefix:ShowKeyboardCues?0:TextFormatFlags.HidePrefix;
  TextRenderer.DrawText(g,Text,Font,Rectangle.Inflate(r,-4,-3),ink,flags);
  if(Focused&&Width>12&&Height>12)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),ink,back);
 }
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnSystemColorsChanged(EventArgs e){base.OnSystemColorsChanged(e);Invalidate();}
}
public class ModernTabs:TableLayoutPanel {
 public bool Dark=true;FlowLayoutPanel header;Panel body;List<Button> buttons=new List<Button>();List<Panel> pages=new List<Panel>();int selected;bool refreshing;
 sealed class TabButton:ModernButton {
  readonly ModernTabs owner;readonly int index;
  public TabButton(ModernTabs owner,int index){this.owner=owner;this.index=index;AccessibleRole=AccessibleRole.PageTab;UseMnemonic=false;}
  protected override bool SelectedVisual{get{return owner!=null&&owner.selected==index;}}
  protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);if(owner!=null)owner.RefreshTabs();}
  protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);if(owner!=null)owner.RefreshTabs();}
  protected override AccessibleObject CreateAccessibilityInstance(){return new TabAccess(this);}
  sealed class TabAccess:Control.ControlAccessibleObject {
   readonly TabButton button;public TabAccess(TabButton value):base(value){button=value;}
   public override AccessibleStates State{get{return base.State|(button.owner.selected==button.index?AccessibleStates.Selected:AccessibleStates.None);}}
   public override string DefaultAction{get{return Loc.T("Vybrať");}}
   public override void DoDefaultAction(){button.PerformClick();}
  }
  public void NotifySelection(){AccessibilityNotifyClients(AccessibleEvents.Selection,-1);AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);}
  protected override bool ProcessCmdKey(ref Message msg,Keys key){int count=owner.pages.Count;if(count>0&&(key==Keys.Left||key==Keys.Right||key==Keys.Home||key==Keys.End)){owner.SelectedIndex=key==Keys.Home?0:key==Keys.End?count-1:(index+(key==Keys.Right?1:count-1))%count;owner.buttons[owner.selected].Focus();return true;}return base.ProcessCmdKey(ref msg,key);}
 }
 public ModernTabs(){ColumnCount=1;RowCount=2;RowStyles.Add(new RowStyle(SizeType.Absolute,47));RowStyles.Add(new RowStyle(SizeType.Percent,100));header=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0),AccessibleRole=AccessibleRole.PageTabList};body=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};Controls.Add(header,0,0);Controls.Add(body,0,1);}
 public Panel AddPage(string title){int i=pages.Count;var page=new Panel{Dock=DockStyle.Fill,AutoScroll=true,AccessibleName=title,AccessibleRole=AccessibleRole.PropertyPage};var button=new TabButton(this,i){Text=title,AccessibleName=title,Width=203,Height=43,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,5,0),TabIndex=i};button.Click+=delegate{SelectedIndex=i;};buttons.Add(button);pages.Add(page);header.Controls.Add(button);body.Controls.Add(page);RefreshTabs();return page;}
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]public int SelectedIndex{get{return selected;}set{if(value<0||value>=pages.Count)throw new ArgumentOutOfRangeException("value");int old=selected;selected=value;RefreshTabs();if(old!=selected){((TabButton)buttons[old]).NotifySelection();((TabButton)buttons[selected]).NotifySelection();}}}
 public void SetTitle(int index,string title){buttons[index].Text=title;buttons[index].AccessibleName=title;pages[index].AccessibleName=title;RefreshTabs();}
 protected override bool ProcessCmdKey(ref Message msg,Keys key){if(pages.Count>0&&(key==(Keys.Control|Keys.PageDown)||key==(Keys.Control|Keys.PageUp))){SelectedIndex=(selected+(key==(Keys.Control|Keys.PageDown)?1:pages.Count-1))%pages.Count;buttons[selected].Focus();return true;}return base.ProcessCmdKey(ref msg,key);}
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(header!=null&&buttons.Count>0)RefreshTabs();}
 protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);RefreshTabs();}
 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);RefreshTabs();}
 protected override void OnSystemColorsChanged(EventArgs e){base.OnSystemColorsChanged(e);RefreshTabs();}
 public void RefreshTabs(){if(refreshing||header==null||buttons==null||buttons.Count==0)return;refreshing=true;try{
  int width=Math.Max(1,header.ClientSize.Width/buttons.Count-5),height=Math.Max(43,(int)Math.Ceiling(43*Font.Height/(double)UiFonts.Default.Height));
  foreach(var b in buttons){b.Width=width;height=Math.Max(height,TextRenderer.MeasureText(b.Text,b.Font,new Size(Math.Max(1,width-16),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+12);}
  RowStyles[0].Height=height+4;bool hc=SystemInformation.HighContrast;
  for(int i=0;i<pages.Count;i++){var b=buttons[i];b.Height=height;pages[i].Visible=i==selected;b.BackColor=i==selected?(hc?SystemColors.Highlight:Color.FromArgb(25,80,131)):BackColor;b.ForeColor=i==selected?(hc?SystemColors.HighlightText:Color.White):ForeColor;b.FlatAppearance.BorderColor=hc?SystemColors.WindowText:i==selected?Color.FromArgb(0,143,255):Dark?Color.FromArgb(56,75,92):Color.FromArgb(190,204,219);}
  Invalidate(true);
 }finally{refreshing=false;}}
}
}
