using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EanEtikety;

class PlatformMigrationTests {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void ExistingWorkspace(string dir){
  string preferences=Path.Combine(dir,"existing.xml");
  string templates=Path.Combine(preferences+".workspace","Templates");
  Directory.CreateDirectory(templates);
  string project=Path.Combine(TestPaths.RepositoryRoot,"tests","fixtures","work","qa-2.0.1","settings.ean.xml");
  string template=Path.Combine(templates,"Legacy.ean.xml");
  File.Copy(project,template);
  string profiles=Path.Combine(preferences+".workspace","printers.xml");
  File.WriteAllText(profiles,"<ArrayOfPrinterProfile><PrinterProfile><Printer>Zebra</Printer><Paper>Label</Paper><Name>Stock</Name><Width>236</Width><Height>177</Height><X>0.4</X><Y>-0.2</Y><ScaleX>1.01</ScaleX><ZebraZpl>true</ZebraZpl><ZplDpi>203.2</ZplDpi><Darkness>12.5</Darkness></PrinterProfile></ArrayOfPrinterProfile>");
  string templateHash=UpdateFiles.Hash(template), profilesHash=UpdateFiles.Hash(profiles);
  using(var workspace=new WorkspaceStore(preferences)){
   Check(workspace.TemplatesPath==templates,"Workspace keeps the existing templates location");
   var fromProject=Engine.Load(project);
   var fromTemplate=Engine.Load(Directory.GetFiles(workspace.TemplatesPath,"*.ean.xml")[0]);
   Check(ProjectXml.Encode(fromProject)==ProjectXml.Encode(fromTemplate),"The existing project and library template keep the same contents");
   using(var scene=VectorArt.Build(fromTemplate))Check(scene.Shapes.Count>0,"The existing template renders offline");
   var profile=new PrinterProfileStore(profiles).Find("Zebra","Stock");
   Check(profile!=null&&profile.Width==236&&profile.Height==177&&profile.X==.4&&profile.Y==-.2&&profile.ScaleX==1.01&&
    profile.ZebraZpl&&profile.ZplDpi==203.2&&profile.Darkness==12.5m,"An existing media profile preserves geometry, calibration and Zebra settings");
  }
  Check(UpdateFiles.Hash(template)==templateHash&&UpdateFiles.Hash(profiles)==profilesHash,"Reading the workspace leaves the existing files intact");
 }
 [STAThread]static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);try{
  // This must be the first encoding consumer in this fresh process: opening
  // standalone CP850 ZPL cannot depend on having imported a batch first.
  string note;using(var cp850=ZplImport.Render("^XA^PW200^LL100^FO10,10^A0N,30,30^FH^FD_82^FS^XZ",300,60,45,out note))
  using(var unicode=ZplImport.Render("^XA^PW200^LL100^CI28^FO10,10^A0N,30,30^FDé^FS^XZ",300,60,45,out note)){
   Check(cp850.Size==new Size(200,100),"First-operation CP850 ZPL import preserves size");
   for(int y=0;y<100;y++)for(int x=0;x<200;x++)if(cp850.GetPixel(x,y)!=unicode.GetPixel(x,y))throw new Exception("CP850 hex escape must render as Unicode é");
   checks++;
  }
  var core=typeof(LabelSettings).Assembly;
  Check(Environment.Version.Major==10&&UpdateFiles.CurrentFamily=="net10-x64","Application and tests run on .NET 10");
  ExistingWorkspace(dir);
  Check(core!=typeof(MainForm).Assembly,"Document/rendering core must be separate from the Windows application");
  foreach(var reference in core.GetReferencedAssemblies())Check(reference.Name!="System.Windows.Forms"&&reference.Name!="ND-LabelDesigner","Core must not depend on Windows controls or the application");
  Check(typeof(VectorArt).Assembly==core&&typeof(OutputPlan).Assembly==core&&typeof(ProjectXml).Assembly==core&&typeof(LabelChecks).Assembly==core,"The application must use one document/rendering/validation core");
  var legacy=ProjectXml.Decode<LabelSettings>("<LabelSettings><Ean>4006381333931</Ean><Width>60</Width><Height>45</Height></LabelSettings>");
  Check(legacy.Width==60&&legacy.Height==45&&legacy.Ean=="4006381333931","Legacy XML values survive assembly extraction");
  Check(Engine.Bits("4006381333931",13).Length==95,"EAN-13 retains its 95 modules");
  legacy.Kind=128;legacy.Ean="{serial}";legacy.Texts.Clear();legacy.Variables.Add(new VariableField{Name="serial",Start=7,Digits=5});legacy.Variables.Add(new VariableField{Name="date",Type="Date",Days=1});legacy.Texts.Add(new TextItem{Text="{date}",X=2,Y=2,Width=50,Height=8});
  var job=new PreparedLabelJob(legacy,new DateTime(2026,11,12,23,59,59));
  Check(job.Label.Ean=="00007"&&job.Label.Texts[0].Text=="2026-11-13","Frozen serial and date retain their existing values");
  Check(job.Plan.Width==60&&job.Plan.Height==45&&job.Plan.PageCount==1,"Physical output dimensions and page count preserved");
  string project=Path.Combine(dir,"legacy-roundtrip.ean.xml");Engine.Save(project,legacy);Check(Engine.Load(project).Ean=="{serial}","Saving retains unresolved legacy variables");
  // Literal Windows-1250 bytes, independent of the runtime's encoding provider.
  string csv=Path.Combine(dir,"windows1250.csv");File.WriteAllBytes(csv,new byte[]{0x4e,0x61,0x6d,0x65,0x0d,0x0a,0x8e,0x6c,0x74,0xfd,0x20,0xe8,0x61,0x6a});
  Check(BatchData.Csv(csv,';',1250).Rows[1][0]=="Žltý čaj","Windows-1250 CSV remains readable");
  foreach(string format in new[]{"pdf","docx","svg","zpl"}){string output=Path.Combine(dir,"shared."+format);JobOutput.Export(output,format,job.Plan,203);Check(new FileInfo(output).Length>100,"Shared pipeline exports "+format);}
  string svg=File.ReadAllText(Path.Combine(dir,"shared.svg"));Check(svg.Contains("width=\"60mm\"")&&svg.Contains("height=\"45mm\""),"SVG retains exact millimetre dimensions");
  using(var zip=ZipFile.OpenRead(Path.Combine(dir,"shared.docx")))Check(zip.GetEntry("word/document.xml")!=null,"DOCX contains document part");
  var s=LabelSettings.Default();string zpl=Path.Combine(dir,"editable.zpl");Engine.Zpl(zpl,s);Check(File.ReadAllText(zpl).Contains("^XA"),"Original ZPL output remains available");
  Check(Engine.Load(project).Variables.Count==2,"Output never mutates the saved variable template");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MainForm(Path.Combine(dir,"migration-ui.xml"))){
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
   int minimumWidth=(int)Math.Round(1024*form.DeviceDpi/96d),minimumHeight=(int)Math.Round(640*form.DeviceDpi/96d);
   Check(form.MinimumSize.Width>=minimumWidth&&form.MinimumSize.Height>=minimumHeight,"Editor minimum must retain its runtime's sizing contract after DPI transition: "+form.MinimumSize+" DPI="+form.DeviceDpi);
   form.Size=form.MinimumSize;Application.DoEvents();
   var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
   var inspector=(Control)typeof(MainForm).GetField("inspectorHost",flags).GetValue(form);
   var bounds=form.RectangleToClient(inspector.RectangleToScreen(inspector.ClientRectangle));
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"migration-minimum.png"));} Check(form.ClientRectangle.Contains(bounds),"Properties panel must remain inside the minimum editor window: panel="+bounds+" window="+form.ClientRectangle+" parent="+inspector.Parent.Bounds);
   form.Close();
  }
  File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" shared core, legacy XML, frozen jobs, encoding and output compatibility checks. Runtime: "+Environment.Version);
 }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
