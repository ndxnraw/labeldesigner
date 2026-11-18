using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
// Inactive-window wheel routing must never edit a value merely under the mouse.
public sealed class ClickWheelNumericUpDown:NumericUpDown {
 bool wheelArmed;
 public ClickWheelNumericUpDown(){foreach(Control child in Controls)HookClick(child);}
 void HookClick(Control control){control.MouseDown+=ArmWheel;foreach(Control child in control.Controls)HookClick(child);}
 void ArmWheel(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left&&Enabled&&!ReadOnly)wheelArmed=true;}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);ArmWheel(this,e);}
 protected override void OnLeave(EventArgs e){wheelArmed=false;base.OnLeave(e);}
 protected override void OnMouseWheel(MouseEventArgs e){
  if(wheelArmed&&ContainsFocus&&Enabled&&!ReadOnly){base.OnMouseWheel(e);return;}
  var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
  int lines=SystemInformation.MouseWheelScrollLines;if(lines==0||e.Delta==0)return;
  for(Control parent=Parent;parent!=null;parent=parent.Parent){var scroll=parent as ScrollableControl;if(scroll==null||!scroll.AutoScroll||!scroll.VerticalScroll.Visible)continue;int current=-scroll.AutoScrollPosition.Y,maximum=Math.Max(0,scroll.VerticalScroll.Maximum-scroll.VerticalScroll.LargeChange+1);double distance=lines<0?scroll.ClientSize.Height:Math.Max(1,lines)*Font.Height;int next=Math.Max(0,Math.Min(maximum,current-(int)Math.Round(e.Delta/120d*distance)));if(next==current)continue;scroll.AutoScrollPosition=new Point(-scroll.AutoScrollPosition.X,next);return;}
 }
}
}
