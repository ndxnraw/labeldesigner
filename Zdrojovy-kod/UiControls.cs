using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
namespace EanEtikety {
public class ModernComboBox:ComboBox {
 public ModernComboBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);using(var pen=new Pen(Color.FromArgb(85,107,128)))e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(9,1,Width-32,Height-2),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);using(var pen=new Pen(ForeColor,1.5f)){int x=Width-17,y=Height/2;e.Graphics.DrawLines(pen,new[]{new Point(x-4,y-2),new Point(x,y+2),new Point(x+4,y-2)});}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-27,Height-6));}
 protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
}
public class ModernButton:Button {
 public ModernButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;var r=new Rectangle(1,1,Width-3,Height-3);using(var path=new GraphicsPath()){int d=10;path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();bool hover=ClientRectangle.Contains(PointToClient(Cursor.Position));using(var b=new SolidBrush(hover&&Enabled?FlatAppearance.MouseOverBackColor:BackColor))g.FillPath(b,path);using(var p=new Pen(FlatAppearance.BorderColor))g.DrawPath(p,path);}using(var brush=new SolidBrush(Enabled?ForeColor:Color.Gray))using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(Text,Font,brush,r,format);if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-10),ForeColor,BackColor);}
}
public class ModernTabs:TableLayoutPanel {
 public bool Dark=true;FlowLayoutPanel header;Panel body;List<Button> buttons=new List<Button>();List<Panel> pages=new List<Panel>();int selected;
 public ModernTabs(){ColumnCount=1;RowCount=2;RowStyles.Add(new RowStyle(SizeType.Absolute,47));RowStyles.Add(new RowStyle(SizeType.Percent,100));header=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new Padding(0)};body=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};Controls.Add(header,0,0);Controls.Add(body,0,1);}
 public Panel AddPage(string title){int i=pages.Count;var page=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var button=new ModernButton{Text=title,Width=203,Height=43,FlatStyle=FlatStyle.Flat,Margin=new Padding(0,0,5,0)};button.Click+=delegate{selected=i;RefreshTabs();};buttons.Add(button);pages.Add(page);header.Controls.Add(button);body.Controls.Add(page);RefreshTabs();return page;}
 public void RefreshTabs(){for(int i=0;i<pages.Count;i++){pages[i].Visible=i==selected;buttons[i].BackColor=i==selected?Color.FromArgb(25,80,131):BackColor;buttons[i].ForeColor=i==selected?Color.White:ForeColor;buttons[i].FlatAppearance.BorderColor=i==selected?Color.FromArgb(0,143,255):Dark?Color.FromArgb(56,75,92):Color.FromArgb(190,204,219);}Invalidate(true);}
}
}
