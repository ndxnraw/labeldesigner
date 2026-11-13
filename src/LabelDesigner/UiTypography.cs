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
 public static DialogResult Show(IWin32Window owner,string message,string title){return Show(owner,message,title,MessageBoxButtons.OK,MessageBoxIcon.None);}
 public static DialogResult Show(IWin32Window owner,string message,string title,MessageBoxButtons buttons,MessageBoxIcon icon){using(var dialog=Create(owner,message,title,buttons,icon))return dialog.ShowDialog(owner);}
 public static Form Create(IWin32Window owner,string message,string title,MessageBoxButtons buttons,MessageBoxIcon icon){
  var control=owner as Control;bool dark=control!=null&&control.BackColor.GetBrightness()<.5f;int width=620;var screen=control==null?Screen.PrimaryScreen.WorkingArea:Screen.FromControl(control).WorkingArea;width=Math.Min(width,screen.Width-60);int contentHeight=TextRenderer.MeasureText(message,UiFonts.Default,new Size(width-48,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
  var form=new Form{Text=title,Font=UiFonts.Default,ClientSize=new Size(width,Math.Min(Math.Max(160,contentHeight+104),Math.Max(160,screen.Height-120))),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false,AutoScaleMode=AutoScaleMode.Dpi,BackColor=StudioTheme.Panel(dark),ForeColor=StudioTheme.Text(dark),Padding=new Padding(20)};
  var body=new TextBox{Text=message,Font=UiFonts.Default,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=contentHeight>form.ClientSize.Height-88?ScrollBars.Vertical:ScrollBars.None,BorderStyle=BorderStyle.None,TabStop=false,BackColor=form.BackColor,ForeColor=form.ForeColor};form.Controls.Add(body);
  var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};form.Controls.Add(row);
  DialogResult[] values=buttons==MessageBoxButtons.YesNoCancel?new[]{DialogResult.Cancel,DialogResult.No,DialogResult.Yes}:buttons==MessageBoxButtons.YesNo?new[]{DialogResult.No,DialogResult.Yes}:new[]{DialogResult.OK};
  foreach(var value in values){var button=new ModernButton{Text=Loc.T(value==DialogResult.Yes?"Áno":value==DialogResult.No?"Nie":value==DialogResult.Cancel?"Zrušiť":"OK"),DialogResult=value,TabIndex=value==DialogResult.Yes||value==DialogResult.OK?0:value==DialogResult.No?1:2,Size=new Size(100,36),FlatStyle=FlatStyle.Flat,BackColor=StudioTheme.Panel(dark),ForeColor=form.ForeColor};button.FlatAppearance.BorderColor=StudioTheme.Edge(dark);row.Controls.Add(button);if(value==DialogResult.Yes||value==DialogResult.OK)form.AcceptButton=button;if(value==DialogResult.Cancel||value==DialogResult.OK||buttons==MessageBoxButtons.YesNo&&value==DialogResult.No)form.CancelButton=button;}
  form.Shown+=delegate{if(form.AcceptButton is Control)((Control)form.AcceptButton).Select();};
  WindowTheme.Apply(form,dark);return form;
 }
}
}
