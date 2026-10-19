using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using EanEtikety;
class ImportTests {
 static string folder;
 static void Assert(bool b,string message){if(!b)throw new Exception(message);}
 static void Reject(string text){bool rejected=false;try{string n;using(var b=ZplImport.Render(text,300,60,45,out n)){} }catch{rejected=true;}Assert(rejected,"Expected rejection: "+text);}
 static object Call(object o,string n,params object[] p){return o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,p);}
 static void Snap(Form form,string name){using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(folder,name+".png"));}}
 [STAThread]static void Main(string[] args){folder=args[0];Directory.CreateDirectory(folder);try{Run();File.WriteAllText(Path.Combine(folder,"passed.txt"),"PASS: current/legacy ZPL graphics, exact pixel roundtrip, compressed ASCII rows, native text/barcodes/boxes, UTF-8, invalid/unsupported input, metadata validation, editable project reopening, read-only preview and unchanged editor state.");}catch(Exception ex){File.WriteAllText(Path.Combine(folder,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
 static void Run(){
  var s=LabelSettings.Default();s.Unit="cm";s.DarkMode=false;s.Texts[0].FontName="Georgia";s.Texts[0].Text="Žltý kôň";s.Copies=4;string file=Path.Combine(folder,"roundtrip.zpl");Engine.Zpl(file,s);string source=ZplImport.ReadFile(file);var back=ZplImport.Project(source);Assert(back!=null&&back.Unit=="cm"&&back.Texts[0].Text=="Žltý kôň"&&back.Texts[0].FontName=="Georgia"&&!back.DarkMode&&back.Copies==4,"metadata roundtrip");Assert(ZplImport.Project(source.Replace("^PW709","^PW710"))==null,"changed source metadata must not be trusted");string note;
  using(var imported=ZplImport.Render(source,300,60,45,out note))using(var original=VectorArt.Raster(s,300,true)){imported.Save(Path.Combine(folder,"imported-debug.png"));original.Save(Path.Combine(folder,"original-debug.png"));Assert(imported.Size==original.Size,"raster dimensions");for(int y=0;y<original.Height;y++)for(int x=0;x<original.Width;x++)Assert(original.GetPixel(x,y).ToArgb()==imported.GetPixel(x,y).ToArgb(),"raster pixel mismatch "+x+","+y+" "+original.GetPixel(x,y)+" vs "+imported.GetPixel(x,y));imported.Save(Path.Combine(folder,"roundtrip.png"));}
  string legacy=File.ReadAllText("tests/fixtures/work/qa-2.0.1/ean13.zpl");Assert(ZplImport.Project(legacy)==null,"old ZPL has no project");using(var b=ZplImport.Render(legacy,300,60,45,out note)){Assert(b.Width==709&&b.Height==531,"legacy sizes");}
  var data=ZplImport.DecodeGraphic("HFA,:!!",8,2);Assert(data.Length==8&&data[0]==255&&data[1]==160&&data[2]==255&&data[3]==160&&data[4]==255&&data[7]==255,"compressed graphics");
  foreach(string format in new[]{"Z64","B64"}){var decoded=ZplImport.DecodeGraphic(File.ReadAllText(Path.Combine(folder,format+".txt")),80,4);for(int i=0;i<80;i++)Assert(decoded[i]==new byte[]{255,0,170,85}[i%4],format+" independent fixture");}
  string native="^XA^PW600^LL450^CI28^LH10,10^FO20,20^A0N,30,30^FH^FD_C5_BDlt_C3_BD k_C3_B4_C5_88^FS^FO20,60^GB500,5,5^FS^FO40,100^BY3,3,160^BEN,160,Y,N^FD858000000001^FS^PQ2^XZ";
  File.WriteAllText(Path.Combine(folder,"native.zpl"),native);using(var b=ZplImport.Render(native,300,60,45,out note)){Assert(note.Contains("približné"),"native font approximation notice");b.Save(Path.Combine(folder,"native.png"));}
  foreach(var pair in new[]{new[]{"BC","ABC123"},new[]{"B8","9638507"},new[]{"BU","03600029145"},new[]{"B2","123456"},new[]{"B3","ABC123"}}){string z="^XA^PW800^LL500^FO40,40^BY2,3,100^"+pair[0]+(pair[0]=="B3"?"N,N,100,Y,N":"N,100,Y,N")+"^FD"+pair[1]+"^FS^XZ";using(var b=ZplImport.Render(z,300,60,45,out note))Assert(b.Width==800,"barcode "+pair[0]);}
  using(var b=ZplImport.Render("^XA^FO2,2^GFA,2,2,1,FF00^FS^XZ",203.2,60,45,out note)){Assert(b.Size==new Size(480,360)&&note.Contains("Chýba"),"missing dimensions notice");}
  using(var b=ZplImport.Render("^XA^PW100^LL100^FO0,0^GB10,10,10^FS^FO0,0^GFA,1,1,1,00^FS^XZ",300,60,45,out note))Assert(b.GetPixel(0,0).R==0,"graphic white pixels transparent");
  Reject("garbage^XA^XZ");Reject("^XA^FO1,2^FDhi^FS^XZ^");Reject("^XA^FO1,2^FDhi^FS^XZjunk");Reject("^XA^PW50000^LL50000^FDhi^FS^XZ");Reject("^XA^FO2,2^BQN,2,5^FDMA,HELLO^FS^XZ");Reject("^XA^FO2,2^GFA,1,1,1,FFFF^FS^XZ");Reject("^XA^FO2,2^GFA,1,1,1,^FS^XZ");Reject("^XA^FO2,2^GFA,1,1,1,:^FS^XZ");Reject("^XA^FO2,2^FR^FDabc^FS^XZ");Reject(native+native);
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var form=new MainForm(Path.Combine(folder,"preferences.xml"))){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();Call(form,"OpenImportedFile",file);var current=(LabelSettings)Call(form,"Read");Assert(current.Texts[0].FontName=="Georgia"&&current.Unit=="cm","UI editable import");Assert(form.GetType().GetField("settingsPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form)==null,"import save must not overwrite ZPL");Snap(form,"editable-import");bool shown=false;Exception error=null;var timer=new Timer{Interval=100};timer.Tick+=delegate{timer.Stop();try{foreach(Form dialog in Application.OpenForms)if(dialog!=form){shown=true;Snap(dialog,"zpl-viewer");dialog.Close();break;}}catch(Exception e){error=e;}};timer.Start();Call(form,"OpenImportedFile",Path.Combine(folder,"native.zpl"));timer.Dispose();if(error!=null)throw error;Assert(shown,"viewer opened");Assert(((LabelSettings)Call(form,"Read")).Texts[0].Text=="Žltý kôň","preview changed editor");Call(form,"Apply",LabelSettings.Default());form.Close();}
 }
}

