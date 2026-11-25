using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace EanEtikety {
public static class Engine {
 static readonly string[] L={"0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"};
 static readonly string[] G={"0100111","0110011","0011011","0100001","0011101","0111001","0000101","0010001","0001001","0010111"};
 static readonly string[] Parity={"LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG","LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL"};
 public static string Normalize(string input,int kind){
  if(kind==114)return Normalize(input,14);
  if(kind!=8&&kind!=13&&kind!=12&&kind!=14)return MoreBarcodes.Validate(input,kind);
  string s=(input??"").Trim();
  if(s.Length!=kind&&s.Length!=kind-1)throw new Exception(Loc.T("Číselný kód musí obsahovať ")+(kind-1)+Loc.T(" číslic bez kontrolnej číslice alebo ")+kind+Loc.T(" číslic s kontrolnou číslicou."));
  foreach(char c in s)if(c<'0'||c>'9')throw new Exception(Loc.T("Číselný kód môže obsahovať iba číslice 0 až 9."));
  int sum=0,weight=3;for(int i=kind-2;i>=0;i--){sum+=(s[i]-'0')*weight;weight=4-weight;}
  char check=(char)('0'+(10-sum%10)%10);
  if(s.Length==kind-1)return s+check;
  if(s[kind-1]!=check)throw new Exception(Loc.T("Nesprávna kontrolná číslica. Správna posledná číslica je ")+check+".");
  return s;
 }
 public static string Bits(string value,int kind){
  if(kind==114)return MoreBarcodes.Gs1Bits("01"+Normalize(value,14));
  string s=Normalize(value,kind);if(kind==12)return Bits("0"+s,13);if(kind==14)return MoreBarcodes.Bits(s,25);if(kind!=8&&kind!=13)return MoreBarcodes.Bits(s,kind);var b=new StringBuilder("101");int half=kind==13?6:4,start=kind==13?1:0;
  for(int i=0;i<half;i++){int n=s[start+i]-'0';b.Append(kind==13&&Parity[s[0]-'0'][i]=='G'?G[n]:L[n]);}
  b.Append("01010");for(int i=start+half;i<kind;i++)foreach(char c in L[s[i]-'0'])b.Append(c=='0'?'1':'0');b.Append("101");return b.ToString();
 }
 static void Range(double n,double min,double max,string field){if(double.IsNaN(n)||double.IsInfinity(n)||n<min||n>max)throw new Exception(field+Loc.T(": povolený rozsah ")+min+Loc.T(" až ")+max+".");}
 public static void Validate(LabelSettings s){Validate(s,false);}
 public static void ValidateDraft(LabelSettings s){Validate(s,true);}
 static void Validate(LabelSettings s,bool draft){
  if(s==null||s.Layout==null||s.Variables==null||s.Metadata==null||s.Batch==null||s.Batch.Bindings==null||s.ApplicationIdentifiers==null)throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  if((s.Unit!="mm"&&s.Unit!="cm")||Array.IndexOf(MoreBarcodes.Kinds,s.Kind)<0||!DraftText(s.Ean,4000))throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  if(s.Variables.Count>256||s.Metadata.Count>1000||s.Batch.Bindings.Count>256||s.ApplicationIdentifiers.Count>100)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));
  foreach(var v in s.Variables)if(v==null||!DraftText(v.Name,200)||!DraftText(v.Type,100)||!DraftText(v.Format,4000)||!DraftText(v.Prefix,4000)||!DraftText(v.Suffix,4000)||!DraftText(v.FixedValue,4000))throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  foreach(var m in s.Metadata)if(m==null)throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  foreach(var b in s.Batch.Bindings)if(b==null||!DraftText(b.Target,200))throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  BatchPresets.ValidateAll(s.Batch);
  foreach(var ai in s.ApplicationIdentifiers)if(ai==null||!DraftText(ai.AI,200)||!DraftText(ai.Data,4000))throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));
  Range(s.Layout.Columns,1,100,Loc.T("Stĺpce"));Range(s.Layout.Rows,1,100,Loc.T("Riadky"));Range(s.Layout.Count,1,10000,Loc.T("Počet etikiet"));Range(s.Layout.FirstSlot,0,9999,Loc.T("Prvá voľná pozícia (od 1)"));
  foreach(double value in new[]{s.Layout.Left,s.Layout.Top,s.Layout.Right,s.Layout.Bottom,s.Layout.HorizontalGap,s.Layout.VerticalGap})Range(value,0,1000,Loc.T("Neplatné rozloženie hárka"));
  if(!draft&&s.Variables.Count>0){var resolved=BatchData.Record(s,new string[0],new List<ColumnBinding>(),-1,0,1,DateTime.Today);if(resolved.Error!=null)throw new InvalidDataException(resolved.Error);return;}
 Range(s.BarRotation,0,359.999999999,"Rotácia");if(s.Texts!=null)foreach(var t in s.Texts)if(t!=null)Range(t.Rotation,0,359.999999999,"Rotácia");if(s.Symbols!=null)foreach(var t in s.Symbols)if(t!=null)Range(t.Rotation,0,359.999999999,"Rotácia");
  if(!draft&&!s.HideBarcode){if(s.Gs1Mode&&s.Kind==128)Gs1.Payload(s.ApplicationIdentifiers);else Normalize(s.Ean,s.Kind);}Range(s.Width,10,500,Loc.T("Šírka etikety"));Range(s.Height,10,500,Loc.T("Výška etikety"));Range(s.Dpi,150,610,Loc.T("Rozlíšenie"));Range(s.Copies,1,500,Loc.T("Počet kópií"));
  Range(s.BarX,-500,500,Loc.T("Poloha kódu X"));Range(s.BarY,-500,500,Loc.T("Poloha kódu Y"));Range(s.BarWidth,1,500,Loc.T("Šírka kódu"));Range(s.BarHeight,1,500,Loc.T("Výška čiar"));Range(s.DigitSize,4,72,Loc.T("Písmo kódu"));
  if(!draft&&!s.HideBarcode&&Module(s)<1)throw new Exception(Loc.T("Kód je pre zvolené rozlíšenie príliš úzky."));
  if(s.Width*s.Height*s.Dpi*s.Dpi/(25.4*25.4)>40000000)throw new Exception(Loc.T("Etiketa je pri tomto DPI príliš veľká. Znížte rozmery alebo DPI."));
  long embedded=0;if(s.Symbols!=null)foreach(var item in s.Symbols)if(item!=null&&item.Image!=null&&item.Image.Data!=null)embedded+=item.Image.Data.Length;if(embedded>48000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));if(s.Symbols==null||s.Symbols.Count>20)throw new Exception(Loc.T("Najviac 20 symbolov."));foreach(var symbol in s.Symbols){if(symbol==null)throw new Exception(Loc.T("Neplatný symbol."));Range(symbol.X,-500,500,"Symbol X");Range(symbol.Y,-500,500,"Symbol Y");Range(symbol.Width,1,500,Loc.T("Šírka symbolu"));Range(symbol.Height,symbol.Shape!=null&&symbol.Shape.IsLine?ShapeContent.MinimumLineThickness:1,500,Loc.T("Výška symbolu"));if(symbol.Shape!=null)symbol.Shape.Validate(symbol);if(symbol.Code!=null)Validate(symbol.Code.Settings(),draft);if(symbol.Image!=null)Assets.Validate(symbol.Image);if(symbol.Matrix!=null&&(!DraftText(symbol.Matrix.Data,4000)||!BarcodeCatalog.MatrixSupported(symbol.Matrix.Type)))throw new InvalidDataException(Loc.T("Neplatná štruktúra projektu."));if(symbol.Matrix!=null&&!draft)ValidateMatrix(symbol,s.Dpi);}
  if(s.Texts==null||s.Texts.Count>50)throw new Exception(Loc.T("Povolených je najviac 50 textových polí."));
  foreach(var t in s.Texts){if(t!=null&&(t.FontName==null||t.FontName.Length>200))throw new Exception(Loc.T("Neplatný názov fontu."));if(t==null||t.Text==null||t.Text.Length>4000)throw new Exception(Loc.T("Text je príliš dlhý."));Range(t.X,-500,500,"Text X");Range(t.Y,-500,500,"Text Y");Range(t.Width,1,500,Loc.T("Šírka textového poľa"));Range(t.Height,1,500,Loc.T("Výška textového poľa"));Range(t.Size,4,144,Loc.T("Veľkosť písma"));}
  {Range(s.PageWidth,10,500,Loc.T("Šírka papiera"));Range(s.PageHeight,10,500,Loc.T("Výška papiera"));Range(s.MarginX,0,250,Loc.T("Okraj X"));Range(s.MarginY,0,250,Loc.T("Okraj Y"));Range(s.MarginRight,0,250,"Right margin");Range(s.MarginBottom,0,250,"Bottom margin");Range(s.GapX,0,100,Loc.T("Medzera X"));Range(s.GapY,0,100,Loc.T("Medzera Y"));if(!draft&&((!s.Layout.Enabled&&s.UsePaper&&(s.MarginX+s.MarginRight+s.Width>s.PageWidth+.001||s.MarginY+s.MarginBottom+s.Height>s.PageHeight+.001))||(s.Sheet&&(Columns(s)<1||Rows(s)<1))))throw new Exception(Loc.T("Na papier sa nezmestí ani jedna etiketa. Upravte rozmery alebo okraje."));}
 }
 internal static void ValidateMatrix(SymbolItem symbol,double dpi){
  var matrix=Assets.Matrix(symbol.Matrix);
  if(BarcodeCatalog.Square(symbol.Matrix.Type)&&Math.Abs(symbol.Width-symbol.Height)>.01)throw new Exception(Loc.T("QR a Data Matrix musia byť štvorcové"));
  if(symbol.Width*dpi/25.4<matrix.Width||symbol.Height*dpi/25.4<matrix.Height)throw new Exception(Loc.T("Kód je pre zvolené rozlíšenie príliš úzky."));
 }
 static bool DraftText(string value,int maximum){return value==null||value.Length<=maximum;}
 public static int Quiet(int kind){return kind==13?11:kind==8?7:kind==12||kind==112?9:10;}
 public static double ExtraHeight(LabelSettings s,double width,double font){int m=(int)Math.Floor(width*s.Dpi/25.4/Modules(s)+1e-7);return (s.Digits?font*25.4/72*1.5:0)+(s.Kind==14?10*Math.Max(1,m)*25.4/s.Dpi:0);}
 public static double BarcodeHeight(LabelSettings s){return s.BarHeight+ExtraHeight(s,s.BarWidth,s.DigitSize);}
 public static bool Outside(LabelSettings s,double x,double y,double w,double h){return x<0||y<0||x+w>s.Width+.001||y+h>s.Height+.001;}
 public static bool IsGs1(LabelSettings s){return s.Kind==128&&s.Gs1Mode;}
 public static string Caption(LabelSettings s){return s.HideBarcode?"":s.Kind==114?"(01)"+Normalize(s.Ean,14):IsGs1(s)?Gs1.Human(s.ApplicationIdentifiers):Normalize(s.Ean,s.Kind);}
 public static string CodeBits(LabelSettings s){return IsGs1(s)?MoreBarcodes.Gs1Bits(Gs1.Payload(s.ApplicationIdentifiers)):Bits(s.Ean,s.Kind);}
 public static int Modules(LabelSettings s){return CodeBits(s).Length+Quiet(s.Kind)+(s.Kind==13?7:Quiet(s.Kind));}
 public static int Module(LabelSettings s){return (int)Math.Floor(s.BarWidth*s.Dpi/25.4/Modules(s)+1e-7);}
 public static int Columns(LabelSettings s){return s.UsePaper?1:s.Sheet?(int)Math.Floor((s.PageWidth-2*s.MarginX+s.GapX)/(s.Width+s.GapX)+1e-7):1;}
 public static int Rows(LabelSettings s){return s.UsePaper?1:s.Sheet?(int)Math.Floor((s.PageHeight-2*s.MarginY+s.GapY)/(s.Height+s.GapY)+1e-7):1;}
 public static List<string> Warnings(LabelSettings s){
  var w=new List<string>();if(!s.HideBarcode&&IsGs1(s))w.AddRange(Gs1.Warnings(s.ApplicationIdentifiers));double x=s.HideBarcode?1:Module(s)*25.4/s.Dpi;
  if(ElementTransform.Outside(s,ElementTransform.Get(s,-1)))w.Add(Loc.T("Kód presahuje etiketu. Pri exporte sa časť mimo etikety oreže."));
  foreach(var t in s.Texts)if(t.Text.Length>0&&ElementTransform.Outside(s,new ElementEdit{X=t.X,Y=t.Y,Width=t.Width,Height=t.Height,Rotation=t.Rotation}))w.Add(Loc.T("Text presahuje etiketu a pri exporte sa oreže: ")+t.Text);
  if(!s.HideBarcode&&s.Kind==14&&(x<.495||s.BarHeight<31.75))w.Add(Loc.T("ITF-14: skontrolujte rozmery pre distribučné balenia; odporúčaná úzka čiara aspoň 0,495 mm a výška čiar 31,75 mm."));
  if(x<.264)w.Add(Loc.T("Úzka čiara má iba ")+x.ToString("0.000")+Loc.T(" mm. Zväčšite šírku kódu pre spoľahlivejšie čítanie."));
  if(!s.HideBarcode&&(s.Kind==13||s.Kind==8||s.Kind==12||s.Kind==112)&&s.BarHeight<(s.Kind==8?18.23:22.85)*x/.33)w.Add(Loc.T("Výška čiar je nízka vzhľadom na ich šírku. Pred použitím overte čitateľnosť skenerom."));
  var bar=s.HideBarcode?RectangleF.Empty:new RectangleF((float)s.BarX,(float)s.BarY,(float)s.BarWidth,(float)BarcodeHeight(s));
  foreach(var symbol in s.Symbols){if(ElementTransform.Outside(s,new ElementEdit{X=symbol.X,Y=symbol.Y,Width=symbol.Width,Height=symbol.Height,Rotation=symbol.Rotation}))w.Add(Loc.T("Symbol presahuje etiketu a pri exporte sa oreže."));if(symbol.Shape==null&&symbol.Code==null&&symbol.Image==null&&symbol.Matrix==null&&symbol.Width<10)w.Add(Loc.T("Zálohované: šírka loga s textom je menšia než 10 mm."));if(symbol.Shape==null&&symbol.Code==null&&symbol.Image==null&&symbol.Matrix==null&&Math.Abs(symbol.Height/symbol.Width-DepositArt.Aspect)>.025)w.Add(Loc.T("Zálohované: zmenený pomer strán. Obnovte ho dvojklikom na symbol."));if(symbol.Shape==null&&bar.IntersectsWith(new RectangleF((float)symbol.X,(float)symbol.Y,(float)symbol.Width,(float)symbol.Height)))w.Add(Loc.T("Symbol zasahuje do oblasti kódu vrátane okrajov."));}
  using(var bmp=new Bitmap(1,1))using(var g=Graphics.FromImage(bmp)){
   double k=s.Dpi/25.4;g.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;
   if(!s.HideBarcode&&s.Digits&&!CaptionFits(g,s))w.Add(Loc.T("Popis kódu je príliš široký a bude orezaný. Zmenšite jeho písmo alebo zväčšite kód."));
   foreach(var t in s.Texts){if(t.Text.Length==0)continue;using(var ff=VectorArt.Family(t.FontName))if(!string.Equals(ff.Name,t.FontName,StringComparison.OrdinalIgnoreCase))w.Add("Font "+t.FontName+Loc.T(" nie je dostupný. Použije sa Arial."));
    if(bar.IntersectsWith(new RectangleF((float)t.X,(float)t.Y,(float)t.Width,(float)t.Height)))w.Add(Loc.T("Textové pole zasahuje do oblasti kódu: ")+t.Text);
    using(var family=VectorArt.Family(t.FontName))using(var f=new Font(family,(float)(t.Size*s.Dpi/72),VectorArt.Style(family,t.Bold),GraphicsUnit.Pixel))using(var fmt=TextFormat(t)){
     var z=g.MeasureString(t.Text,f,(int)Math.Max(1,t.Width*k),fmt);if(z.Height>t.Height*k+1)w.Add(Loc.T("Text sa nezmestí do výšky poľa: ")+t.Text);
    }
   }
  }return w;
 }
 public static bool CaptionFits(LabelSettings s){using(var b=new Bitmap(1,1))using(var g=Graphics.FromImage(b))return CaptionFits(g,s);}
 static bool CaptionFits(Graphics g,LabelSettings s){string value=Caption(s);int m=Module(s);string[] parts;int[] widths;
  if(s.Kind==13){parts=new[]{value.Substring(0,1),value.Substring(1,6),value.Substring(7,6)};widths=new[]{10*m,42*m,42*m};}
  else if(s.Kind==8){parts=new[]{value.Substring(0,4),value.Substring(4,4)};widths=new[]{28*m,28*m};}
  else if(s.Kind==112){parts=new[]{value.Substring(0,1),value.Substring(1,6),value.Substring(7,1)};widths=new[]{8*m,42*m,8*m};} else if(s.Kind==12){parts=new[]{value.Substring(0,1),value.Substring(1,5),value.Substring(6,5),value.Substring(11,1)};widths=new[]{8*m,35*m,35*m,8*m};}
  else{parts=new[]{value};widths=new[]{Modules(s)*m};}
  using(var f=new Font("Arial",(float)(s.DigitSize*s.Dpi/72),FontStyle.Regular,GraphicsUnit.Pixel))using(var fmt=new StringFormat(StringFormat.GenericTypographic)){fmt.FormatFlags=StringFormatFlags.NoWrap;for(int i=0;i<parts.Length;i++)if(g.MeasureString(parts[i],f,int.MaxValue,fmt).Width>widths[i]+1)return false;}return true;
 }
 static StringFormat TextFormat(TextItem t){var f=new StringFormat(StringFormat.GenericTypographic);f.Alignment=t.Center?StringAlignment.Center:StringAlignment.Near;f.Trimming=StringTrimming.None;return f;}
 public static Bitmap Render(LabelSettings s){return VectorArt.Raster(s,s.Dpi,false);}
 public static byte[] Packed(Bitmap bmp,bool blackOne){int stride=(bmp.Width+7)/8;var result=new byte[stride*bmp.Height];var bd=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);try{var row=new byte[Math.Abs(bd.Stride)];for(int y=0;y<bmp.Height;y++){WorkContext.Check();Marshal.Copy(IntPtr.Add(bd.Scan0,y*bd.Stride),row,0,row.Length);for(int x=0;x<bmp.Width;x++){bool black=(row[x*3]+row[x*3+1]+row[x*3+2])<384;if(black==blackOne)result[y*stride+x/8]|=(byte)(128>>(x%8));}}}finally{bmp.UnlockBits(bd);}return result;}
 public static void Zpl(string path,LabelSettings s){using(var b=VectorArt.Raster(s,s.Dpi,true)){var data=Packed(b,true);int stride=(b.Width+7)/8,stripRows=Math.Max(1,60000/stride);using(var w=new StreamWriter(path,false,Encoding.ASCII)){w.Write("^XA\n^PW"+b.Width+"\n^LL"+b.Height+"\n^LH0,0\n");for(int y=0;y<b.Height;y+=stripRows){WorkContext.Check();int count=Math.Min(stripRows,b.Height-y)*stride;w.Write("^FO0,"+y+"^GFA,"+count+","+count+","+stride+",");for(int i=0;i<count;i++)w.Write(data[y*stride+i].ToString("X2"));w.Write("^FS\n");}w.Write("^PQ"+s.Copies+",0,1,N\n^XZ\n");}}ZplImport.AddProject(path,s);}
 static string F(double x){return x.ToString("0.#####",CultureInfo.InvariantCulture);}
 public static void Svg(string path,LabelSettings s){File.WriteAllText(path,VectorArt.Svg(s),new UTF8Encoding(false));}
 public static void Pdf(string path,LabelSettings s){VectorArt.Pdf(path,s);}
 static void ZipText(ZipArchive z,string name,string txt){WorkContext.Check();using(var w=new StreamWriter(z.CreateEntry(name).Open(),new UTF8Encoding(false)))w.Write(txt);}
 public static void Docx(string path,LabelSettings s){Validate(s);DocxPlan(path,OutputPlan.Create(s));}
 public static void DocxPlan(string path,OutputPlan plan){int pages=plan.PageCount;var allParts=new List<WordPart>();var body=new StringBuilder();int id=0;
  for(int page=0;page<pages;page++){WorkContext.Report("Exportujem strany…",page,pages);List<WordPart> parts;using(var scene=plan.Scene(page))parts=WordParts.CreateScene(scene);foreach(var part in parts){part.Number=allParts.Count+1;allParts.Add(part);}body.Append("<w:p><w:pPr>"+(page>0?"<w:pageBreakBefore/>":"")+"<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"1\" w:lineRule=\"exact\"/></w:pPr>");foreach(var pos in new[]{PointF.Empty}){foreach(var part in parts){id++;long cx=(long)Math.Round(part.Bounds.Width*36000),cy=(long)Math.Round(part.Bounds.Height*36000);body.Append("<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"0\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/><wp:positionH relativeFrom=\"page\"><wp:posOffset>"+Math.Round((pos.X+part.Bounds.X)*36000)+"</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"page\"><wp:posOffset>"+Math.Round((pos.Y+part.Bounds.Y)*36000)+"</wp:posOffset></wp:positionV><wp:extent cx=\""+cx+"\" cy=\""+cy+"\"/><wp:wrapNone/><wp:docPr id=\""+id+"\" name=\"Etiketa "+id+"\" descr=\"EAN etiketa\"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"0\" name=\"Etiketa\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"rId"+(part.Number*2-1)+"\"><a:extLst><a:ext uri=\"{96DAC541-7B7A-43D3-8B79-37D633B846F1}\"><asvg:svgBlip xmlns:asvg=\"http://schemas.microsoft.com/office/drawing/2016/SVG/main\" r:embed=\"rId"+(part.Number*2)+"\"/></a:ext></a:extLst></a:blip><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\""+cx+"\" cy=\""+cy+"\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>");}}body.Append("</w:p>");}
  body.Append("<w:sectPr><w:pgSz w:w=\""+Math.Round(plan.Width*1440/25.4)+"\" w:h=\""+Math.Round(plan.Height*1440/25.4)+"\"/><w:pgMar w:top=\""+Math.Round(0d*1440/25.4)+"\" w:right=\""+Math.Round(0d*1440/25.4)+"\" w:bottom=\""+Math.Round(0d*1440/25.4)+"\" w:left=\""+Math.Round(0d*1440/25.4)+"\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr>");
  using(var fs=File.Create(path))using(var z=new ZipArchive(fs,ZipArchiveMode.Create)){
   ZipText(z,"[Content_Types].xml","<?xml version=\"1.0\" encoding=\"utf-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"svg\" ContentType=\"image/svg+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
   ZipText(z,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
   var relationships=new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");foreach(var part in allParts){foreach(string ext in new[]{"png","svg"})relationships.Append("<Relationship Id=\"rId"+(part.Number*2-(ext=="png"?1:0))+"\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/part"+part.Number+"."+ext+"\"/>");}relationships.Append("</Relationships>");ZipText(z,"word/_rels/document.xml.rels",relationships.ToString());
   ZipText(z,"word/document.xml","<?xml version=\"1.0\" encoding=\"utf-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body>"+body+"</w:body></w:document>");
   foreach(var part in allParts){WorkContext.Check();ZipText(z,"word/media/part"+part.Number+".svg",part.Svg);using(var stream=z.CreateEntry("word/media/part"+part.Number+".png").Open())stream.Write(part.Png,0,part.Png.Length);}
  }
 }
 public static void Save(string file,LabelSettings s){ValidateDraft(s);string backup=null;if(File.Exists(file)){try{Load(file);backup=file+".bak";}catch(IOException){throw;}catch(UnauthorizedAccessException){throw;}catch{ /* Keep an existing backup when the overwritten file is damaged. */ }}ProjectXml.Save(file,s,backup);}
 public static LabelSettings Load(string file){using(var stream=File.OpenRead(file))return Load(stream);}
 public static LabelSettings Load(Stream stream){using(var r=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64000000})){var s=(LabelSettings)new XmlSerializer(typeof(LabelSettings)).Deserialize(r);ValidateDraft(s);return s;}}
}
}
