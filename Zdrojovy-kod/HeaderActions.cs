using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
namespace EanEtikety {
public enum HeaderIcon {Open,Save,SaveAs,Print,Export,AddText,Deposit,SelectCode,CenterCode,Table,Panels,Zoom,Settings,Help,About}
// Logical vector paths are created once per button. Pens, brushes and scaled paths
// are cached outside OnPaint and disposed with their owner.
public sealed class HeaderActionButton:Button {
 readonly HeaderIcon icon; GraphicsPath source,scaled,outline;
 SolidBrush normal,hover,pressed,disabled; Pen border,ink,disabledInk,focus;
 bool hot,down;
 public HeaderIcon Icon {get{return icon;}}

 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);RebuildGeometry();}
 public HeaderActionButton(HeaderIcon value){icon=value;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AutoSize=false;FlatStyle=FlatStyle.Flat;Size=new Size(36,36);MinimumSize=new Size(36,36);Margin=new Padding(0,4,8,2);AccessibleRole=AccessibleRole.PushButton;source=CreateIcon(value);RebuildGeometry();RefreshPalette();}
 static GraphicsPath CreateIcon(HeaderIcon kind){var p=new GraphicsPath();if(kind>=HeaderIcon.Export){switch(kind){

 case HeaderIcon.Export:p.AddLines(new[]{new PointF(3,10),new PointF(3,17),new PointF(17,17),new PointF(17,10)});p.StartFigure();p.AddLine(10,13,10,2);p.StartFigure();p.AddLines(new[]{new PointF(6,6),new PointF(10,2),new PointF(14,6)});break;
 case HeaderIcon.AddText:p.AddLine(2,3,14,3);p.StartFigure();p.AddLine(8,3,8,17);p.StartFigure();p.AddLine(5,17,11,17);p.StartFigure();p.AddLine(16,10,16,16);p.StartFigure();p.AddLine(13,13,19,13);break;
 case HeaderIcon.Deposit:p.AddArc(3,3,14,14,40,260);p.StartFigure();p.AddLines(new[]{new PointF(15,2),new PointF(16,7),new PointF(11,6)});p.StartFigure();p.AddLines(new[]{new PointF(6,7),new PointF(13,7),new PointF(7,13),new PointF(14,13)});break;
 case HeaderIcon.SelectCode:for(int i=0;i<5;i++){p.StartFigure();p.AddLine(3+i*3,4,3+i*3,16);}break;
 case HeaderIcon.CenterCode:p.AddLine(10,1,10,19);p.StartFigure();p.AddRectangle(new RectangleF(4,4,12,4));p.StartFigure();p.AddRectangle(new RectangleF(6,12,8,4));break;
 case HeaderIcon.Table:p.AddRectangle(new RectangleF(2,3,16,14));p.StartFigure();p.AddLine(2,8,18,8);p.StartFigure();p.AddLine(2,12,18,12);p.StartFigure();p.AddLine(8,3,8,17);break;
 case HeaderIcon.Panels:p.AddRectangle(new RectangleF(2,3,16,14));p.StartFigure();p.AddLine(6,3,6,17);p.StartFigure();p.AddLine(14,3,14,17);break;
 case HeaderIcon.Zoom:p.AddEllipse(2,2,11,11);p.StartFigure();p.AddLine(12,12,18,18);p.StartFigure();p.AddLine(5,7.5f,10,7.5f);p.StartFigure();p.AddLine(7.5f,5,7.5f,10);break;
 case HeaderIcon.Settings:var gear=new PointF[24];for(int i=0;i<gear.Length;i++){double angle=i*Math.PI/12;double radius=i%3==1?6.5:9;gear[i]=new PointF(10+(float)(radius*Math.Cos(angle)),10+(float)(radius*Math.Sin(angle)));}p.AddPolygon(gear);p.StartFigure();p.AddEllipse(7,7,6,6);break;
 case HeaderIcon.Help:p.AddEllipse(2,2,16,16);p.StartFigure();p.AddBezier(7,7,7,3,15,5,12,9);p.AddLine(12,9,10,11);p.StartFigure();p.AddEllipse(9.5f,14,1,1);break;
 case HeaderIcon.About:p.AddEllipse(2,2,16,16);p.StartFigure();p.AddLine(10,9,10,15);p.StartFigure();p.AddEllipse(9.5f,5,1,1);break;
 }return p;}if(kind==HeaderIcon.Open){p.AddLines(new[]{new PointF(2,15),new PointF(2,4),new PointF(8,4),new PointF(10,6),new PointF(18,6),new PointF(18,8)});p.StartFigure();p.AddPolygon(new[]{new PointF(2,16),new PointF(5,9),new PointF(19,9),new PointF(16,16)});}else if(kind==HeaderIcon.Print){p.AddRectangle(new RectangleF(5,2,10,5));p.StartFigure();p.AddLines(new[]{new PointF(5,15),new PointF(2,15),new PointF(2,7),new PointF(18,7),new PointF(18,15),new PointF(15,15)});p.StartFigure();p.AddRectangle(new RectangleF(5,12,10,6));p.StartFigure();p.AddLine(14,10,16,10);}else{p.AddPolygon(new[]{new PointF(3,2),new PointF(15,2),new PointF(18,5),new PointF(18,18),new PointF(3,18)});p.StartFigure();p.AddRectangle(new RectangleF(6,2,7,5));p.StartFigure();p.AddRectangle(new RectangleF(6,11,8,7));if(kind==HeaderIcon.SaveAs){p.Reset();p.AddLines(new[]{new PointF(9,18),new PointF(2,18),new PointF(2,2),new PointF(14,2),new PointF(17,5),new PointF(17,8)});p.StartFigure();p.AddRectangle(new RectangleF(5,2,7,5));p.StartFigure();p.AddLines(new[]{new PointF(5,18),new PointF(5,11),new PointF(10,11)});p.StartFigure();p.AddPolygon(new[]{new PointF(10,18),new PointF(11,14),new PointF(17,8),new PointF(20,11),new PointF(14,17)});p.StartFigure();p.AddLine(15,10,18,13);}}return p;}
 float ScaleFactor {get{return Math.Max(1,Height/36f);}}
 void RebuildGeometry(){if(source==null)return;if(scaled!=null)scaled.Dispose();if(outline!=null)outline.Dispose();float s=ScaleFactor;scaled=(GraphicsPath)source.Clone();float x=(Width-20*s)/2;using(var m=new Matrix(s,0,0,s,x,(Height-20*s)/2))scaled.Transform(m);outline=new GraphicsPath();float d=8*s,r=Width-2,b=Height-2;outline.AddArc(1,1,d,d,180,90);outline.AddArc(r-d,1,d,d,270,90);outline.AddArc(r-d,b-d,d,d,0,90);outline.AddArc(1,b-d,d,d,90,90);outline.CloseFigure();RefreshPalette();}
 void DisposePalette(){if(normal!=null){normal.Dispose();hover.Dispose();pressed.Dispose();disabled.Dispose();border.Dispose();ink.Dispose();disabledInk.Dispose();focus.Dispose();normal=null;}}
 public void RefreshPalette(){DisposePalette();bool hc=SystemInformation.HighContrast;Color bg=hc?SystemColors.Control:BackColor,fg=hc?SystemColors.ControlText:ForeColor;normal=new SolidBrush(bg);hover=new SolidBrush(hc?SystemColors.Highlight:ControlPaint.Light(bg,.15f));pressed=new SolidBrush(hc?SystemColors.Highlight:ControlPaint.Dark(bg,.15f));disabled=new SolidBrush(bg);border=new Pen(hc?SystemColors.WindowText:FlatAppearance.BorderColor);ink=new Pen(fg,1.6f*ScaleFactor){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round};disabledInk=new Pen(SystemColors.GrayText,1.6f*ScaleFactor){LineJoin=LineJoin.Round};focus=new Pen(hc?SystemColors.Highlight:StudioTheme.Accent,2*ScaleFactor);Invalidate();}
 protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);RebuildGeometry();}
 protected override void OnSystemColorsChanged(EventArgs e){base.OnSystemColorsChanged(e);RefreshPalette();}
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hot=true;Invalidate();}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hot=false;Invalidate();}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left)down=true;Invalidate();}
 protected override void OnMouseUp(MouseEventArgs e){down=false;base.OnMouseUp(e);Invalidate();}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)down=false;Invalidate();}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space)down=true;base.OnKeyDown(e);Invalidate();}
 protected override void OnKeyUp(KeyEventArgs e){down=false;base.OnKeyUp(e);Invalidate();}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 public void ResetInteraction(){hot=false;down=false;Invalidate();}
 protected override void OnLostFocus(EventArgs e){down=false;base.OnLostFocus(e);Invalidate();}
 protected override void OnEnabledChanged(EventArgs e){base.OnEnabledChanged(e);down=false;Invalidate();}
 protected override void OnPaint(PaintEventArgs e){if(outline==null||normal==null)return;var g=e.Graphics;g.Clear(Parent==null?BackColor:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.FillPath(!Enabled?disabled:down?pressed:hot?hover:normal,outline);g.DrawPath(border,outline);bool high=SystemInformation.HighContrast&&Enabled&&(hot||down);ink.Color=high?SystemColors.HighlightText:SystemInformation.HighContrast?SystemColors.ControlText:ForeColor;g.DrawPath(Enabled?ink:disabledInk,scaled);if(Focused&&ShowFocusCues)g.DrawRectangle(focus,3,3,Width-7,Height-7);}
 protected override void Dispose(bool disposing){if(disposing){DisposePalette();if(source!=null)source.Dispose();if(scaled!=null)scaled.Dispose();if(outline!=null)outline.Dispose();}base.Dispose(disposing);}
}
public partial class MainForm {
 readonly UiToolTip headerTips=new UiToolTip{InitialDelay=400,ReshowDelay=100,AutoPopDelay=3000,ShowAlways=false};
 readonly Dictionary<HeaderActionButton,string[]> headerActions=new Dictionary<HeaderActionButton,string[]>();
 HeaderActionButton HeaderAction(string name,HeaderIcon icon,string shortcut,Action action){var b=new HeaderActionButton(icon){Font=Font,TabIndex=headerActions.Count};headerActions.Add(b,new[]{name,shortcut});b.Click+=delegate{headerTips.Hide(b);action();};b.GotFocus+=delegate{if(keyboardHeaderFocus)headerTips.Show(headerTips.GetToolTip(b),b,0,b.Height+2,3000);};b.MouseDown+=delegate{keyboardHeaderFocus=false;headerTips.Hide(b);};b.MouseLeave+=delegate{headerTips.Hide(b);};b.LostFocus+=delegate{headerTips.Hide(b);};SetHeaderText(b,name,shortcut);return b;}
 void SetHeaderText(HeaderActionButton b,string name,string shortcut){b.AccessibleName=Loc.T(name);string hint=Loc.T(name)+(string.IsNullOrEmpty(shortcut)?"":" ("+shortcut+")");headerTips.SetToolTip(b,hint);b.AccessibleDescription=hint;b.Text="";b.Width=b.Height;}
 void RefreshHeaderActions(){foreach(var item in headerActions)SetHeaderText(item.Key,item.Value[0],item.Value[1]);}
}
}
