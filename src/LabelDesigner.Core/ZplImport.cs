using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Serialization;
namespace EanEtikety {
public static class ZplImport {
 const string Marker="^FXELG26:";
 public static string ReadFile(string path){if(new FileInfo(path).Length>96000000)throw new Exception(Loc.T("ZPL súbor je príliš veľký (limit 96 MB)."));return File.ReadAllText(path,new UTF8Encoding(false,true));}
 static string Hash(string value){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));}
 public static void AddProject(string path,LabelSettings settings){
  if(settings.Batch!=null&&settings.Batch.Presets!=null&&settings.Batch.Presets.Exists(preset=>preset!=null&&preset.Reference!=null)){
   settings=ProjectXml.Clone(settings);
   BatchReferences.Strip(settings);
  }
  string body=File.ReadAllText(path);string xml;using(var writer=new StringWriter(CultureInfo.InvariantCulture)){new XmlSerializer(typeof(LabelSettings)).Serialize(writer,settings);xml=writer.ToString();}string comment=Marker+Hash(body)+":"+Convert.ToBase64String(Encoding.UTF8.GetBytes(xml))+"^FS";File.WriteAllText(path,body.Insert(3,comment),new UTF8Encoding(false));
 }
 public static LabelSettings Project(string source){int start=source.IndexOf(Marker,StringComparison.Ordinal);if(start<0)start=source.IndexOf("^FXELG23:",StringComparison.Ordinal);if(start<0)start=source.IndexOf("^FXELG22:",StringComparison.Ordinal);if(start<0)start=source.IndexOf("^FXELG21:",StringComparison.Ordinal);if(start<0)return null;int end=source.IndexOf("^FS",start,StringComparison.Ordinal);if(end<0)return null;string[] parts=source.Substring(start+Marker.Length,end-start-Marker.Length).Split(':');if(parts.Length!=2)return null;string body=source.Remove(start,end+3-start);if(Hash(body)!=parts[0])return null;try{string xml=Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64000000})){var s=(LabelSettings)new XmlSerializer(typeof(LabelSettings)).Deserialize(reader);Engine.Validate(s);return s;}}catch{return null;}}
 static int Num(string s,int fallback){if(string.IsNullOrWhiteSpace(s))return fallback;int n;if(!int.TryParse(s.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out n)||n<0||n>50000)throw new Exception(Loc.T("Neplatná číselná hodnota ZPL: ")+s);return n;}
 static string P(string[] p,int i,string fallback){return i<p.Length&&!string.IsNullOrEmpty(p[i])?p[i]:fallback;}
 static int N(string[] p,int i,int fallback){return Num(P(p,i,""),fallback);}
 static Exception Unsupported(string code){return new Exception(Loc.T("Náhľad nepodporuje príkaz alebo variant ")+code+Loc.T(". Súbor sa neotvoril ako neúplná etiketa. Pôvodný súbor zostal nezmenený."));}
 public static Bitmap Render(string source,double dpi,double widthMm,double heightMm,out string note){
  if(source.Length>96000000)throw new Exception(Loc.T("ZPL je príliš veľké."));
  var matches=Regex.Matches(source,@"([\^~])([A-Za-z0-9]{2})([^\^~]*)");
  int cursor=0;foreach(Match token in matches){if(source.Substring(cursor,token.Index-cursor).Trim().Length>0)throw Unsupported(Loc.T("neplatná syntax ZPL"));cursor=token.Index+token.Length;}if(source.Substring(cursor).Trim().Length>0)throw Unsupported(Loc.T("neplatná syntax ZPL"));
  int formats=0,ends=0,w=0,h=0;bool inside=false,finished=false;
  foreach(Match cmd in matches){string code=cmd.Groups[2].Value;if(code=="XA"){if(inside||finished)throw Unsupported(Loc.T("viac etikiet ^XA/^XZ v jednom súbore"));inside=true;formats++;}else if(code=="XZ"){if(!inside)throw Unsupported("^XZ bez ^XA");inside=false;finished=true;ends++;}else if(!inside)throw Unsupported(Loc.T("príkaz mimo ^XA/^XZ"));if(code=="PW")w=Num(cmd.Groups[3].Value,0);if(code=="LL")h=Num(cmd.Groups[3].Value,0);}
  if(formats!=1||ends!=1||inside)throw new Exception(Loc.T("ZPL musí obsahovať jednu úplnú etiketu ^XA … ^XZ."));
  bool assumed=w==0||h==0;double k=dpi/25.4;if(w==0)w=(int)Math.Round(widthMm*k);if(h==0)h=(int)Math.Round(heightMm*k);if(w<1||h<1||(long)w*h>40000000)throw new Exception(Loc.T("Rozmery ZPL prekračujú limit 40 miliónov bodov."));
  var bmp=new Bitmap(w,h,PixelFormat.Format24bppRgb);bmp.SetResolution((float)dpi,(float)dpi);bool approximate=false;int fields=0;
  try{using(var g=Graphics.FromImage(bmp)){g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   int x=0,y=0,lx=0,ly=0,module=2,barHeight=100,fontH=30,fontW=30,currentH=30,currentW=30,kind=0,copies=1;bool caption=true,hex=false,utf8=false;char hexChar='_';
   foreach(Match cmd in matches){string code=cmd.Groups[2].Value,arg=cmd.Groups[3].Value.TrimEnd('\r','\n'),prefix=cmd.Groups[1].Value;string[] p=arg.Split(',');if(prefix!="^")throw Unsupported(prefix+code);
    switch(code){
    case "XA":case "XZ":if(arg.Trim().Length!=0)throw Unsupported("^"+code+Loc.T(" dáta"));break;
    case "FX":break;
    case "PW":case "LL":break;
    case "PR":case "MD":case "MT":case "MM":case "MN":break;
    case "PO":case "PM":case "LR":case "FW":if(P(p,0,"N")!="N"||(p.Length>1&&P(p,1,"0")!="0"))throw Unsupported("^"+code);break;
    case "LS":case "LT":if(arg!="0")throw Unsupported("^"+code);break;
    case "LH":lx=N(p,0,0);ly=N(p,1,0);break;
    case "FO":if(P(p,2,"0")!="0")throw Unsupported("^FO zarovnanie");x=N(p,0,0);y=N(p,1,0);break;
    case "FS":kind=0;currentH=fontH;currentW=fontW;hex=false;break;
    case "CI":if(arg!="28"&&arg!="0")throw Unsupported("^CI"+arg);utf8=arg=="28";break;
    case "FH":hex=true;hexChar=arg.Length==0?'_':arg[0];break;
    case "CF":if(P(p,0,"0")!="0")throw Unsupported("^CF font "+p[0]);fontH=N(p,1,30);fontW=N(p,2,fontH);currentH=fontH;currentW=fontW;break;
    case "A0":if(P(p,0,"N")!="N")throw Unsupported(Loc.T("^A0 rotácia"));currentH=N(p,1,fontH);currentW=N(p,2,currentH);break;
    case "BY":module=N(p,0,2);if(module<1||module>10)throw Unsupported(Loc.T("^BY šírka"));if(P(p,1,"3")!="3"&&P(p,1,"3")!="3.0")throw Unsupported(Loc.T("^BY pomer iný než 3"));barHeight=N(p,2,100);break;
    case "BE":case "B8":case "BU":case "BC":case "B3":case "B2":
     if(P(p,0,"N")!="N")throw Unsupported("^"+code+Loc.T(" rotácia"));kind=code=="BE"?13:code=="B8"?8:code=="BU"?12:code=="BC"?128:code=="B3"?39:25;
     int offset=code=="B3"?1:0;if(offset==1&&P(p,1,"N")!="N")throw Unsupported(Loc.T("^B3 kontrolná číslica"));barHeight=N(p,1+offset,barHeight);caption=P(p,2+offset,"Y")=="Y";if(P(p,3+offset,"N")!="N")throw Unsupported(Loc.T("popis nad kódom"));if((code=="BC"||code=="B2")&&P(p,4,"N")!="N")throw Unsupported("^"+code+Loc.T(" kontrolná číslica"));if(code=="BC"&&P(p,5,"N")!="N")throw Unsupported(Loc.T("^BC režim"));break;
    case "FD":
     string value=arg;if(hex){var bytes=new List<byte>();for(int i=0;i<value.Length;i++){if(value[i]==hexChar){if(i+2>=value.Length)throw new Exception(Loc.T("Neúplné ^FH dáta."));byte v;if(!byte.TryParse(value.Substring(i+1,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out v))throw new Exception(Loc.T("Neplatné ^FH dáta."));bytes.Add(v);i+=2;}else {bytes.AddRange(Encoding.UTF8.GetBytes(value[i].ToString()));}}value=(utf8?(Encoding)new UTF8Encoding(false,true):TextEncodings.Get(850)).GetString(bytes.ToArray());}
     if(value.IndexOf('\\')>=0||value.IndexOf('\r')>=0||value.IndexOf('\n')>=0)throw Unsupported(Loc.T("viacriadkové ^FD"));
     if(kind==0){if(currentH<1||currentW<1)throw Unsupported(Loc.T("nulová veľkosť fontu"));var state=g.Save();g.TranslateTransform(x+lx,y+ly);g.ScaleTransform((float)currentW/currentH,1);using(var font=new Font("Arial",currentH*.75f,FontStyle.Regular,GraphicsUnit.Pixel))g.DrawString(value,font,Brushes.Black,0,0,StringFormat.GenericTypographic);g.Restore(state);approximate=true;}
     else{if(value.Contains(">")&&kind==128)throw Unsupported("^BC riadiace sekvencie");var s=new LabelSettings{Width=w/k,Height=h/k,Dpi=dpi,Kind=kind,Ean=value,BarX=(x+lx-Engine.Quiet(kind)*module)/k,BarY=(y+ly)/k,BarHeight=barHeight/k,Digits=caption,DigitSize=8};s.BarWidth=Engine.Modules(s)*module/k;using(var scene=VectorArt.Build(s))scene.Draw(g,new RectangleF(0,0,(float)(s.Width*k),(float)(s.Height*k)),false);approximate|=caption;}fields++;break;
    case "GF":
     if(p.Length<5||p[0]!="A")throw Unsupported(Loc.T("^GF (podporované je ASCII ^GFA)"));int total=N(p,2,0),stride=N(p,3,0);if(total<1||total>5000000||stride<1||total%stride!=0)throw new Exception(Loc.T("Neplatné rozmery ^GFA."));int pos=0;for(int i=0;i<4;i++)pos=arg.IndexOf(',',pos)+1;var data=DecodeGraphic(arg.Substring(pos),total,stride);using(var image=new Bitmap(stride*8,total/stride,PixelFormat.Format24bppRgb)){if((long)image.Width*image.Height>40000000)throw new Exception(Loc.T("Grafika je príliš veľká."));var locked=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);try{var row=new byte[locked.Stride];for(int yy=0;yy<image.Height;yy++){for(int xx=0;xx<image.Width;xx++){byte shade=(data[yy*stride+xx/8]&(128>>(xx%8)))!=0?(byte)0:(byte)255;row[xx*3]=row[xx*3+1]=row[xx*3+2]=shade;}System.Runtime.InteropServices.Marshal.Copy(row,0,IntPtr.Add(locked.Scan0,yy*locked.Stride),row.Length);}}finally{image.UnlockBits(locked);}image.MakeTransparent(Color.White);var gs=g.Save();g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(image,new Rectangle(x+lx,y+ly,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel);g.Restore(gs);}fields++;break;
    case "GB":int bw=N(p,0,1),bh=N(p,1,1),th=N(p,2,1);if(P(p,3,"B")!="B"||P(p,4,"0")!="0")throw Unsupported("^GB farba/rohy");g.FillRectangle(Brushes.Black,x+lx,y+ly,bw,Math.Min(th,bh));g.FillRectangle(Brushes.Black,x+lx,y+ly+Math.Max(0,bh-th),bw,Math.Min(th,bh));g.FillRectangle(Brushes.Black,x+lx,y+ly,Math.Min(th,bw),bh);g.FillRectangle(Brushes.Black,x+lx+Math.Max(0,bw-th),y+ly,Math.Min(th,bw),bh);fields++;break;
    case "PQ":copies=N(p,0,1);if(N(p,1,0)!=0||N(p,2,1)>1||P(p,3,"N")!="N")throw Unsupported("^PQ variant");break;
    default:throw Unsupported("^"+code);
    }
   }
   if(fields==0)throw new Exception(Loc.T("ZPL neobsahuje zobraziteľné polia."));note=w+" × "+h+" bodov | "+(w/k).ToString("0.##")+" × "+(h/k).ToString("0.##")+" mm | "+copies+Loc.T(" kópií. ")+(approximate?Loc.T("Textové fonty a popisy kódov sú približné (Arial). "):"")+(assumed?Loc.T("Chýba ^PW alebo ^LL: rozmer bol prevzatý z aktuálnej etikety. "):"")+Loc.T("Náhľad je iba na čítanie; V tejto verzii sa tento ZPL neprevádza na editovateľné prvky.");
  }return bmp;}catch{bmp.Dispose();throw;}
 }
 static byte[] Decode64(string text,int total){string[] parts=text.Split(':');if(parts.Length!=4)throw new Exception(Loc.T("Neplatná hlavička B64/Z64."));int expected;if(parts[3].Length!=4||!int.TryParse(parts[3],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out expected))throw new Exception(Loc.T("Chýba CRC B64/Z64."));int crc=0;foreach(byte value in Encoding.ASCII.GetBytes(parts[2])){crc^=value<<8;for(int i=0;i<8;i++)crc=((crc&0x8000)!=0?(crc<<1)^0x1021:crc<<1)&0xffff;}if(crc!=expected)throw new Exception(Loc.T("Nesprávny CRC grafiky B64/Z64."));byte[] data=Convert.FromBase64String(parts[2]);if(parts[1]=="B64"){if(data.Length!=total)throw new Exception(Loc.T("Nesprávna dĺžka B64."));return data;}if(data.Length<6||(data[0]&15)!=8||((data[0]<<8)+data[1])%31!=0||(data[1]&32)!=0)throw new Exception(Loc.T("Neplatná zlib hlavička Z64."));var result=new byte[total];using(var input=new MemoryStream(data,2,data.Length-6))using(var inflate=new System.IO.Compression.DeflateStream(input,System.IO.Compression.CompressionMode.Decompress)){int count=0,n;while(count<total&&(n=inflate.Read(result,count,total-count))>0)count+=n;if(count!=total||inflate.ReadByte()!=-1)throw new Exception(Loc.T("Nesprávna dĺžka Z64."));}uint a=1,b=0;foreach(byte value in result){a=(a+value)%65521;b=(b+a)%65521;}uint checksum=((uint)data[data.Length-4]<<24)|((uint)data[data.Length-3]<<16)|((uint)data[data.Length-2]<<8)|data[data.Length-1];if(checksum!=((b<<16)|a))throw new Exception(Loc.T("Nesprávny Adler32 grafiky Z64."));return result;}
 public static byte[] DecodeGraphic(string text,int total,int stride){if(total<1||total>5000000||stride<1||total%stride!=0)throw new Exception(Loc.T("Neplatné rozmery grafiky."));if(text.TrimStart().StartsWith(":Z64:")||text.TrimStart().StartsWith(":B64:"))return Decode64(text.Trim(),total);var result=new byte[total];int nibble=0,run=0,rowSize=stride*2;Action<int,int> put=delegate(int digit,int count){if(count<1||nibble+count>total*2)throw new Exception(Loc.T("Dĺžka ^GFA nezodpovedá hlavičke."));for(int j=0;j<count;j++,nibble++)result[nibble/2]|=(byte)(digit<<(nibble%2==0?4:0));};foreach(char c in text){if(char.IsWhiteSpace(c))continue;if(c>='G'&&c<='Y'){run+=c-'G'+1;}else if(c>='g'&&c<='z'){run+=(c-'g'+1)*20;}else if(c==','||c=='!'){if(run!=0)throw new Exception(Loc.T("Neplatná kompresia ^GFA."));put(c==','?0:15,rowSize-nibble%rowSize);}else if(c==':'){if(run!=0||nibble<rowSize||nibble%rowSize!=0||nibble+rowSize>total*2)throw new Exception(Loc.T("Neplatné opakovanie riadka ^GFA."));Array.Copy(result,nibble/2-stride,result,nibble/2,stride);nibble+=rowSize;}else{int digit=c>='0'&&c<='9'?c-'0':c>='A'&&c<='F'?c-'A'+10:c>='a'&&c<='f'?c-'a'+10:-1;if(digit<0)throw Unsupported("kompresia ^GFA");put(digit,run==0?1:run);run=0;}if(run>total*2)throw new Exception(Loc.T("Neplatná kompresia ^GFA."));}if(run!=0||nibble!=total*2)throw new Exception(Loc.T("Neúplné dáta ^GFA."));return result;}
}
}
