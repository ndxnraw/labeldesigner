using System;using System.Drawing;
namespace EanEtikety {
// Opt-in content geometry. Stored/manual fields remain unchanged.
public static class TextAutoLayout {
 public static void Apply(LabelSettings label){foreach(var text in label.Texts)Fit(text,label.Width,label.Height);}
 public static void Fit(TextItem text,double labelWidth,double labelHeight){
  if(text==null||!text.AutoWidth||text.MinimumFitSize>0||(text.Text!=null&&text.Text.Length>4000))return;
  if(double.IsNaN(text.Size)||double.IsInfinity(text.Size)||text.Size<4||text.Size>144||labelWidth<10||labelHeight<10)return;
  double oldWidth=text.Width,anchor=text.X+oldWidth/2;
  double maximum=Math.Max(1,Math.Min(500,labelWidth-Math.Max(0,text.X)-3));
  if(text.Center){double margin=Math.Min(3,labelWidth*.05);maximum=Math.Max(1,2*Math.Min(anchor-margin,labelWidth-margin-anchor));}
  using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap))using(var family=VectorArt.Family(text.FontName))using(var font=new Font(family,(float)text.Size,VectorArt.Style(family,text.Bold)))using(var format=new StringFormat(StringFormat.GenericTypographic)){
   g.PageUnit=GraphicsUnit.Millimeter;format.FormatFlags|=StringFormatFlags.MeasureTrailingSpaces;
   double natural=1;foreach(string line in (text.Text??"").Replace("\r\n","\n").Split('\n'))natural=Math.Max(natural,g.MeasureString(line.Length==0?" ":line,font,10000,format).Width+.7);
   text.Width=Math.Round(Math.Max(1,Math.Min(maximum,natural)),4);
   if(text.Center&&Math.Abs(oldWidth-text.Width)>.0000001)text.X=anchor-text.Width/2;
   bool multiline=(text.Text??"").IndexOf('\n')>=0||natural>maximum;
   double height=multiline?g.MeasureString(string.IsNullOrEmpty(text.Text)?" ":text.Text,font,new SizeF((float)Math.Max(1,text.Width-.1),10000),format).Height+.5:text.Size*25.4/72*1.2;
   text.Height=Math.Round(Math.Max(text.Height,Math.Min(Math.Max(1,labelHeight-Math.Max(0,text.Y)-3),height)),4);
  }
 }
}
}
