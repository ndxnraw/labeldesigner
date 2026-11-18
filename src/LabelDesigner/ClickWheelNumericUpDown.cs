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
  WheelInput.ScrollParent(this,e);
 }
}
}
