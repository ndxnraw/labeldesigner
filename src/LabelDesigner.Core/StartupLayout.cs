using System;using System.Drawing;
namespace EanEtikety {
public static class StartupLayout {
 public static LabelSettings Create(StartupProfile p){StartupStore.Validate(p);var s=LabelSettings.Default();s.Width=p.Width;s.Height=p.Height;s.Unit=p.Unit;s.DarkMode=p.DarkMode;s.Dpi=s.Width<20?600:300;
  double scale=Math.Min(s.Width/60,s.Height/45),margin=Math.Min(3,Math.Min(s.Width,s.Height)*.05),gap=Math.Min(1,s.Height*.04);
  var t=s.Texts[0];t.X=margin;t.Width=s.Width-2*margin;t.Center=true;t.Size=Math.Round(Math.Max(4,Math.Min(36,11*scale)),2);
  s.DigitSize=Math.Round(Math.Max(10,Math.Min(28,12*scale)),2);
  double maxTitle=Math.Min(s.Height*.5,s.Height-2*margin-gap-1-s.DigitSize*25.4/72*1.5);
  using(var bmp=new Bitmap(1,1))using(var g=Graphics.FromImage(bmp))using(var fmt=new StringFormat(StringFormat.GenericTypographic)){
   g.PageUnit=GraphicsUnit.Millimeter;fmt.Alignment=StringAlignment.Center;
   while(true){using(var font=new Font(t.FontName,(float)t.Size,FontStyle.Bold)){var measured=g.MeasureString(t.Text,font,new SizeF((float)t.Width,1000),fmt);t.Height=Math.Max(t.Size*25.4/72*1.3,measured.Height+.2);}if(t.Height<=maxTitle||t.Size<=4)break;t.Size=Math.Max(4,t.Size-.25);}
  }
  t.Height=Math.Min(maxTitle,t.Height);TextAutoLayout.Fit(t,s.Width,s.Height);t.Height=Math.Min(maxTitle,t.Height);t.X=(s.Width-t.Width)/2;s.DigitSize=Math.Round(Math.Max(10,Math.Min(28,12*scale)),2);
  s.BarWidth=s.Width<30?s.Width-2*margin:Math.Min(s.Width-2*margin,s.Width*2/3);
  double maximumWidth=s.Width-2*Math.Min(margin,.5);while(!Engine.CaptionFits(s)&&s.BarWidth<maximumWidth){double next=(Engine.Module(s)+1)*Engine.Modules(s)*25.4/s.Dpi+.001;s.BarWidth=Math.Min(maximumWidth,Math.Max(s.BarWidth+.001,next));}s.BarX=(s.Width-s.BarWidth)/2;
  double available=s.Height-2*margin-t.Height-gap;
  s.BarHeight=Math.Max(1,Math.Min(23.5*scale,available-Engine.ExtraHeight(s,s.BarWidth,s.DigitSize)));
  double total=t.Height+gap+Engine.BarcodeHeight(s);t.Y=Math.Max(0,(s.Height-total)/2);s.BarY=t.Y+t.Height+gap;
  Engine.Validate(s);return s;
 }
}
}
