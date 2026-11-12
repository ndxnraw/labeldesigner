using System;
using System.IO;
using System.Drawing;
using System.Drawing.Printing;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using EanEtikety;

class PrintMediaTests {
 static int checks;static string dir;
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,Hidden).GetValue(value);}
 static object Call(object value,string name,params object[] args){return value.GetType().GetMethod(name,Hidden).Invoke(value,args);}
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Wait(PrintPanel panel,Action start){int before=panel.PreviewBuilds;start();var timer=Stopwatch.StartNew();while(panel.PreviewBuilds==before){Application.DoEvents();System.Threading.Thread.Sleep(2);if(timer.ElapsedMilliseconds>30000)throw new Exception("Preview timeout: "+Field<Label>(panel,"info").Text);}Check(panel.Ready,"preview remains printable");}
 static LabelSettings Label(){var label=LabelSettings.Default();label.HideBarcode=true;label.Texts.Clear();label.Texts.Add(new TextItem{Text="MMMM",X=35,Y=15,Width=24,Height=15,Size=18,Bold=true});return label;}
 static int Ink(Bitmap bitmap,Rectangle bounds){int count=0;for(int y=bounds.Top;y<bounds.Bottom;y++)for(int x=bounds.Left;x<bounds.Right;x++)if(bitmap.GetPixel(x,y).R<160)count++;return count;}
 static Rectangle InkBounds(Bitmap bitmap){int left=bitmap.Width,top=bitmap.Height,right=-1,bottom=-1;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).R<160){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}return Rectangle.FromLTRB(left,top,right+1,bottom+1);}
 static void Geometry(){var label=Label();using(var doc=new LabelPrintDocument(label)){
  var small=new Rectangle(0,0,80,80);var printable=new RectangleF(0,0,80,80);
  Check(!doc.FitsPaper(small),"small driver media is detected");
  Check(doc.MediaWarnings(small,printable).Count==2,"paper and clipping are warnings");
  foreach(bool compatibility in new[]{false,true})using(var bitmap=new Bitmap(700,500)){
   bitmap.SetResolution(254,254);doc.IgnoreDriverMargins=compatibility;
   using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);doc.RenderPrinterPage(graphics,0,printable,0,0,false);}
   int ink=Ink(bitmap,new Rectangle(300,120,300,200));Check(compatibility?ink>100:ink==0,"driver clipping is optional and never scales content");
   bitmap.Save(Path.Combine(dir,compatibility?"compatibility.png":"driver-clip.png"));
  }
  doc.IgnoreDriverMargins=true;
  Rectangle first=Rectangle.Empty;
  foreach(bool shifted in new[]{false,true})using(var bitmap=new Bitmap(700,500)){bitmap.SetResolution(254,254);doc.OffsetX=shifted?-2:0;doc.OffsetY=shifted?1:0;using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);doc.RenderPrinterPage(graphics,0,printable,10,5,false);}var ink=InkBounds(bitmap);if(!shifted)first=ink;else{Check(Math.Abs(ink.Width-first.Width)<=1&&Math.Abs(ink.Height-first.Height)<=1,"calibration and hard margins preserve rendered scale");Check(Math.Abs(ink.X-first.X+20)<=1&&Math.Abs(ink.Y-first.Y-10)<=1,"calibration moves output by measured millimetres");}}
  doc.OffsetX=doc.OffsetY=0;
  using(var bitmap=new Bitmap(700,500)){bitmap.SetResolution(254,254);using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);graphics.SetClip(new Rectangle(0,0,200,500));doc.RenderPrinterPage(graphics,0,printable,0,0,false);}Check(Ink(bitmap,new Rectangle(300,120,300,200))==0,"compatibility preserves native/device clip");}
  Check(doc.MediaWarnings(small,printable).Count==2,"compatibility still warns about paper and hardware edges");
  using(var image=doc.CreatePreview(0,new SizeF(60,45)))using(var bitmap=new Bitmap(600,450)){using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);graphics.DrawImage(image,new Rectangle(0,0,600,450));}Check(Ink(bitmap,new Rectangle(300,120,290,200))>100,"independent vector preview retains content outside driver area");bitmap.Save(Path.Combine(dir,"full-preview.png"));}
  doc.OffsetX=-2;doc.OffsetY=1;var bounds=doc.LabelBounds(0);Check(bounds.Width==60&&bounds.Height==45&&bounds.X==-2&&bounds.Y==1,"calibration retains physical dimensions");
 }
 // Exercise the real PrintPage override with deliberately false bounds. No job
 // is submitted: a bitmap receives the page instead of a physical printer.
 using(var doc=new TestDocument(label))using(var bitmap=new Bitmap(700,500)){doc.PrinterSettings.PrinterName="Microsoft Print to PDF";Check(doc.PrinterSettings.IsValid,"PDF driver available");doc.DefaultPageSettings.PaperSize=new PaperSize("Tiny",80,80);doc.ValidatePrinter();doc.IgnoreDriverMargins=true;using(var graphics=Graphics.FromImage(bitmap)){var args=new PrintPageEventArgs(graphics,smallBounds,smallBounds,doc.DefaultPageSettings);doc.Draw(args);}Check(true,"PrintPage and ValidatePrinter allow small reported paper");}
 }
 static readonly Rectangle smallBounds=new Rectangle(0,0,80,80);
 class TestDocument:LabelPrintDocument {public TestDocument(LabelSettings s):base(s){}public void Draw(PrintPageEventArgs e){base.OnPrintPage(e);}}
 static void Persistence(){string path=Path.Combine(dir,"legacy-profiles.xml");
  // XML written before named media and compatibility fields existed.
  File.WriteAllText(path,"<ArrayOfPrinterProfile><PrinterProfile><Printer>Printer A</Printer><Paper>A4</Paper><Width>827</Width><Height>1169</Height><Landscape>false</Landscape><X>-1.25</X><Y>2.5</Y></PrinterProfile></ArrayOfPrinterProfile>");
  var store=new PrinterProfileStore(path);var legacy=store.Find("Printer A");Check(legacy!=null&&!legacy.IgnoreDriverMargins&&legacy.X==-1.25,"legacy profiles load with safe defaults");
  store.Save(new PrinterProfile{Printer="Printer A",Name="60 x 40",Paper="Custom",Width=236,Height=157,IgnoreDriverMargins=true,X=1.5});
  store.Save(new PrinterProfile{Printer="Printer A",Name="100 x 150",Paper="Custom",Width=394,Height=591,Landscape=true,Y=-2});
  store.Save(new PrinterProfile{Printer="Printer B",Name="60 x 40",Paper="Custom",Width=236,Height=157});
  store=new PrinterProfileStore(path);Check(store.ForPrinter("printer a").Count==3,"three media coexist on the same printer");Check(store.ForPrinter("Printer B").Count==1,"same media name on another printer is independent");
  Check(store.Find("Printer A","60 x 40").IgnoreDriverMargins&&store.Find("Printer A","100 x 150").Landscape,"media settings persist independently");
  var updated=store.Find("Printer A","60 x 40").Copy();updated.X=-3;store.Save(updated);updated.X=99;Check(store.Profiles.Count==4&&store.Find("Printer A","60 x 40").X==-3,"update one profile without aliases or duplicates");
  store.Save(legacy);Check(store.ForPrinter("Printer A").Count==3,"saving migrated legacy media does not duplicate it");store.Delete("Printer A","100 x 150");Check(new PrinterProfileStore(path).Profiles.Count==3,"delete persists and preserves other media");
 }
 static void Ui(){using(var form=new Form{Size=new Size(1050,760),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new PrintPanel(Label())){
  string path=Path.Combine(dir,"ui-profiles.xml");panel.LoadProfiles(path);form.Controls.Add(panel);Wait(panel,()=>form.Show());Wait(panel,()=>panel.SelectPrinter("Microsoft Print to PDF"));
  Wait(panel,()=>Check(panel.ConfigurePrinter(dialog=>{dialog.PrinterSettings.DefaultPageSettings.PaperSize=new PaperSize("Tiny",80,80);return DialogResult.OK;}),"configure tiny driver media"));
  Check(Field<string[]>(panel,"warnings").Length>=1,"mismatch warns without blocking print button");Check(Field<PrintCanvas>(panel,"preview").Paper.Width>=60,"preview includes full document");Check(Field<Label>(panel,"paperInfo").Text.Contains("100 %"),"physical scale displayed");
  Wait(panel,()=>Field<CheckBox>(panel,"ignoreDriverMargins").Checked=true);Wait(panel,()=>Call(panel,"UseDocumentPaper"));
  Check(Math.Abs(Field<PageSettings>(panel,"selectedPage").Bounds.Width*.254-60)<.3,"custom document media selected");
  Field<TextBox>(panel,"profileName").Text=" Label 60 x 45 ";Call(panel,"SaveMediaProfile");Check(Field<TextBox>(panel,"profileName").Text=="Label 60 x 45","profile names are trimmed");
  Wait(panel,()=>Field<CheckBox>(panel,"landscape").Checked=true);Wait(panel,()=>Field<NumericUpDown>(panel,"offsetX").Value=-1.5m);Wait(panel,()=>Field<CheckBox>(panel,"ignoreDriverMargins").Checked=false);
  Field<TextBox>(panel,"profileName").Text="Rotated label";Call(panel,"SaveMediaProfile");
  var profiles=Field<ModernComboBox>(panel,"mediaProfiles");Check(profiles.Items.Count==2,"two media profiles available in UI");
  Wait(panel,()=>profiles.SelectedIndex=0);Check(!Field<CheckBox>(panel,"landscape").Checked&&Field<CheckBox>(panel,"ignoreDriverMargins").Checked&&Field<NumericUpDown>(panel,"offsetX").Value==0,"first profile restores complete settings");
  Wait(panel,()=>profiles.SelectedIndex=1);Check(Field<CheckBox>(panel,"landscape").Checked&&!Field<CheckBox>(panel,"ignoreDriverMargins").Checked&&Field<NumericUpDown>(panel,"offsetX").Value==-1.5m,"second profile restores complete settings");
  var store=new PrinterProfileStore(path);Check(store.ForPrinter("Microsoft Print to PDF").Count==2,"UI profiles persist on disk");
  Wait(panel,()=>Call(panel,"UseDocumentPaper"));var oriented=Field<PageSettings>(panel,"selectedPage");Check(Math.Abs(oriented.Bounds.Width*.254-60)<.3&&Math.Abs(oriented.Bounds.Height*.254-45)<.3,"document dimensions respect landscape without swapping output");
  using(var screenshot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(screenshot,form.ClientRectangle);screenshot.Save(Path.Combine(dir,"media-profiles.png"));}form.Close();
 }}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Geometry();Persistence();Ui();foreach(string language in Loc.Codes){Loc.Language=language;foreach(string key in new[]{"Papier podľa ovládača","Ignorovať tlačiteľnú oblasť ovládača"})Check(language=="sk"||Loc.T(key)!=key,"media controls translated: "+language);}File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" print-media checks: warnings, clipping, native clip preservation, vector preview, scale, legacy migration, multiple media, UI switching, persistence and localization. No physical printing.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
