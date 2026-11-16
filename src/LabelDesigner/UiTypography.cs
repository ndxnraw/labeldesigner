using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace EanEtikety {
public sealed class UiContextMenuStrip:ContextMenuStrip {public UiContextMenuStrip(){Font=UiFonts.Default;}}
public sealed class UiToolTip:ToolTip {
 public UiToolTip(){OwnerDraw=true;Popup+=delegate(object sender,PopupEventArgs e){var size=TextRenderer.MeasureText(GetToolTip(e.AssociatedControl),UiFonts.Default,new Size(520,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);e.ToolTipSize=new Size(size.Width+16,size.Height+12);};Draw+=delegate(object sender,DrawToolTipEventArgs e){e.DrawBackground();e.DrawBorder();TextRenderer.DrawText(e.Graphics,e.ToolTipText,UiFonts.Default,new Rectangle(8,6,e.Bounds.Width-16,e.Bounds.Height-12),ForeColor,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);};}
}
// Application messages are owned dialogs so that their font is independent of
// Windows MessageBox typography. Windows file/driver dialogs remain OS-owned.
public static class UiMessageBox {
 // Equal-width actions retain space for the message at large text sizes. A wrapped
 // flow row can otherwise consume the entire dialog with three vertical buttons.
 sealed class ActionRow:TableLayoutPanel {
  bool arranging;
  public ActionRow(){Dock=DockStyle.Bottom;RowCount=1;RowStyles.Add(new RowStyle(SizeType.Absolute,42));Padding=new Padding(0,8,0,0);Margin=Padding.Empty;}
  public void AddAction(Button button){ColumnCount++;ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));button.Dock=DockStyle.Fill;button.AutoSize=false;button.MinimumSize=Size.Empty;button.Margin=new Padding(3);Controls.Add(button,ColumnCount-1,0);}
  protected override void OnLayout(LayoutEventArgs e){if(!arranging&&Controls.Count>0){arranging=true;try{int width=Math.Max(1,ClientSize.Width/Controls.Count-22),height=0;foreach(Control b in Controls)height=Math.Max(height,Math.Max((int)Math.Ceiling(36*b.Font.Height/(double)UiFonts.Default.Height),TextRenderer.MeasureText(b.Text,b.Font,new Size(width,0),TextFormatFlags.WordBreak).Height+12));RowStyles[0].Height=height+6;Height=height+6+Padding.Vertical;}finally{arranging=false;}}base.OnLayout(e);}
 }
 public static DialogResult Show(IWin32Window owner,string message,string title){return Show(owner,message,title,MessageBoxButtons.OK,MessageBoxIcon.None);}
 public static DialogResult Show(IWin32Window owner,string message,string title,MessageBoxButtons buttons,MessageBoxIcon icon){using(var dialog=Create(owner,message,title,buttons,icon))return dialog.ShowDialog(owner);}
 public static Form Create(IWin32Window owner,string message,string title,MessageBoxButtons buttons,MessageBoxIcon icon){
  var control=owner as Control;bool dark=control!=null&&control.BackColor.GetBrightness()<.5f;int width=620;var screen=control==null?Screen.PrimaryScreen.WorkingArea:Screen.FromControl(control).WorkingArea;width=Math.Min(width,screen.Width-60);int contentHeight=TextRenderer.MeasureText(message,UiFonts.Default,new Size(width-48,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
  var form=new Form{Text=title,Font=UiFonts.Default,ClientSize=new Size(width,Math.Min(Math.Max(160,contentHeight+104),Math.Max(160,screen.Height-120))),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false,AutoScaleMode=AutoScaleMode.Dpi,BackColor=StudioTheme.Panel(dark),ForeColor=StudioTheme.Text(dark),Padding=new Padding(20)};
  var body=new TextBox{Text=message,Font=UiFonts.Default,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,TabStop=true,TabIndex=1,AccessibleName=Loc.T("Text hlásenia"),BackColor=form.BackColor,ForeColor=form.ForeColor};form.Controls.Add(body);
  string severity=icon==MessageBoxIcon.Error?"Chyba":icon==MessageBoxIcon.Warning?"Upozornenie":icon==MessageBoxIcon.Information?"Informácia":icon==MessageBoxIcon.Question?"Otázka":null;
  if(severity!=null)form.Controls.Add(new Label{Text=Loc.T(severity),AccessibleName=Loc.T(severity),Dock=DockStyle.Top,AutoSize=true,Font=UiFonts.Get(11,FontStyle.Bold),Padding=new Padding(0,0,0,8)});
  var row=new ActionRow{TabIndex=0};form.Controls.Add(row);
  DialogResult[] values=buttons==MessageBoxButtons.YesNoCancel?new[]{DialogResult.Cancel,DialogResult.No,DialogResult.Yes}:buttons==MessageBoxButtons.YesNo?new[]{DialogResult.No,DialogResult.Yes}:new[]{DialogResult.OK};
  for(int i=values.Length-1;i>=0;i--){var value=values[i];var button=new ModernButton{Text=Loc.T(value==DialogResult.Yes?"Áno":value==DialogResult.No?"Nie":value==DialogResult.Cancel?"Zrušiť":"OK"),DialogResult=value,TabIndex=value==DialogResult.Yes||value==DialogResult.OK?0:value==DialogResult.No?1:2,Padding=new Padding(8,4,8,4),FlatStyle=FlatStyle.Flat,BackColor=StudioTheme.Panel(dark),ForeColor=form.ForeColor};button.FlatAppearance.BorderColor=StudioTheme.Edge(dark);row.AddAction(button);if(value==DialogResult.Yes||value==DialogResult.OK)form.AcceptButton=button;if(value==DialogResult.Cancel||value==DialogResult.OK||buttons==MessageBoxButtons.YesNo&&value==DialogResult.No)form.CancelButton=button;}
  form.Shown+=delegate{if(form.AcceptButton is Control)((Control)form.AcceptButton).Select();};
  WindowTheme.Apply(form,dark);return form;
 }
}
}
