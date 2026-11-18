using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
// A blocked value-edit event still scrolls the nearest available viewport.
internal static class WheelInput {
 public static void ScrollParent(Control control,MouseEventArgs e){
  var handled=e as HandledMouseEventArgs;if(handled!=null)handled.Handled=true;
  int lines=SystemInformation.MouseWheelScrollLines;if(lines==0||e.Delta==0)return;
  for(Control parent=control.Parent;parent!=null;parent=parent.Parent){
   var scroll=parent as ScrollableControl;
   if(scroll==null||!scroll.AutoScroll||!scroll.VerticalScroll.Visible)continue;
   int current=-scroll.AutoScrollPosition.Y,maximum=Math.Max(0,scroll.VerticalScroll.Maximum-scroll.VerticalScroll.LargeChange+1);
   double distance=lines<0?scroll.ClientSize.Height:Math.Max(1,lines)*control.Font.Height;
   int next=Math.Max(0,Math.Min(maximum,current-(int)Math.Round(e.Delta/120d*distance)));
   if(next==current)continue;
   scroll.AutoScrollPosition=new Point(-scroll.AutoScrollPosition.X,next);return;
  }
 }
}
}
