using System;using System.IO;using System.Drawing;using System.Collections.Generic;using System.Windows.Forms;using System.Reflection;using EanEtikety;using ZXing;using ZXing.Common;
class BarcodeCatalogTests {
 static void Readability(){
  var label=LabelSettings.Default();
  label.Texts.Clear();
  var plan=OutputPlan.Create(label);
  var output=new BarcodePrintGeometry{DpiX=203,DpiY=203,Paper=new RectangleF(0,0,60,45)};
  var result=BarcodeReadability.Inspect(plan,output,true,100);
  Check(result.Items.Count==1&&result.Items[0].Decode==BarcodeDecodeStatus.Matched,"read final EAN output");
  Check(Math.Abs(result.Items[0].ModuleDots-Engine.Module(label)*203/label.Dpi)<.001,"module measured at output DPI");
  output.X=-35;
  result=BarcodeReadability.Inspect(plan,output,true,100);
  Check(result.Items[0].InkClipped&&result.Items[0].Decode!=BarcodeDecodeStatus.Matched,"clipped output cannot pass as intact");
  foreach(int kind in new[]{14,114,128}){
   var code=LabelSettings.Default();code.Texts.Clear();code.Width=110;code.Height=70;
   code.BarWidth=90;code.BarX=8;code.BarY=5;code.BarHeight=26;code.Kind=kind;code.Ean="09506000134352";
   if(kind==128){code.Gs1Mode=true;code.ApplicationIdentifiers=Gs1.Parse("(01)09506000134352(10)ABC(21)XYZ");}
   var geometry=new BarcodePrintGeometry{DpiX=203,DpiY=203,Paper=new RectangleF(0,0,110,70),Monochrome=true};
   var item=BarcodeReadability.Inspect(OutputPlan.Create(code),geometry,true,100).Items[0];
   Check(item.Decode==BarcodeDecodeStatus.Matched&&!item.InkClipped&&!item.QuietObstructed,"monochrome linear round-trip "+kind+" / "+item.Decode);
  }
  foreach(string type in new[]{"QR","Data Matrix","GS1 QR","GS1 Data Matrix"}){
   var code=LabelSettings.Default();code.Texts.Clear();code.HideBarcode=true;
   code.Symbols.Add(new SymbolItem{X=12,Y=9,Width=24,Height=24,Rotation=90,Matrix=new MatrixContent{
    Type=type,Data=type.StartsWith("GS1 ")?"(01)09506000134352(10)ABC(21)XYZ":"Žltý diel ABC 123"}});
   var geometry=new BarcodePrintGeometry{DpiX=300,DpiY=300,Paper=new RectangleF(0,0,60,45),ScaleX=.98,ScaleY=1.02};
   var item=BarcodeReadability.Inspect(OutputPlan.Create(code),geometry,true,100).Items[0];
   Check(item.Decode==BarcodeDecodeStatus.Matched&&item.Element==-3&&item.TopDots>=0,"rotated calibrated matrix "+type+" / "+item.Decode);
  }
  var batch=OutputPlan.Create(new System.Collections.Generic.List<BatchRecord>{
   new BatchRecord{Row=8,Label=label,Count=1},new BatchRecord{Row=19,Label=label,Count=1}
  },label);
  output.X=0;output.ScaleX=.88;
  var limited=BarcodeReadability.Inspect(batch,output,false,1);
  Check(limited.Partial&&limited.Items[0].Record==8&&Math.Abs(limited.Items[0].ModuleDots-Engine.Module(label)*203/label.Dpi*.88)<.001,"budget and calibrated modules");
  var complete=BarcodeReadability.Inspect(batch,output,true,int.MaxValue);
  Check(!complete.Partial&&complete.Items[1].Record==19&&complete.Items[1].Page==2,"complete check attributes later batch records");
  var sheet=ProjectXml.Clone(label);sheet.PageWidth=200;sheet.PageHeight=120;
  sheet.Layout=new SheetLayout{Enabled=true,Columns=2,Rows=2,FirstSlot=2,Count=1};
  var slot=BarcodeReadability.Inspect(OutputPlan.Create(sheet),new BarcodePrintGeometry{
   Paper=new RectangleF(0,0,200,120),DpiX=203,DpiY=203},false,100).Items[0];
  Check(slot.Slot==3,"sheet check uses the physical slot after first-slot offset");
  var warningsOnly=new BarcodeReadabilityResult();
  for(int i=0;i<125;i++)warningsOnly.Items.Add(new BarcodeReadabilityItem{Type="EAN-13",ModuleDots=1,Page=i+1});
  var preflight=new PreflightReport(plan);warningsOnly.AddWarnings(preflight);
  Check(preflight.Issues.Count==101&&preflight.CanProceed(true)&&warningsOnly.Items.Count==125,"bounded preflight still retains the full report");
  var context=new WorkContext();context.Cancel();
  bool stopped=false;
  using(context.Enter())try{BarcodeReadability.Inspect(batch,output,true,100);}catch(OperationCanceledException){stopped=true;}
  Check(stopped,"full readability check honors cancellation");
  var moved=ProjectXml.Clone(label);moved.Width=100;moved.Height=80;moved.BarX=20;moved.BarY=20;moved.BarRotation=32;
  var tilted=BarcodeReadability.Inspect(OutputPlan.Create(moved),new BarcodePrintGeometry{
   Paper=new RectangleF(0,0,100,80),DpiX=300,DpiY=300},true,100).Items[0];
  Check(tilted.Decode==BarcodeDecodeStatus.Matched&&!tilted.InkClipped,"angled EAN reorients the sampled raster / "+tilted.Decode+" clipped="+tilted.InkClipped+" quiet="+tilted.QuietClipped);
  output.ScaleX=1;
  var cropped=new BarcodePrintGeometry{DpiX=203,DpiY=203,Paper=output.Paper,Printable=new RectangleF(12,0,48,45)};
  var strip=BarcodeReadability.Inspect(plan,cropped,true,100).Items[0];
  Check(!strip.InkClipped&&strip.QuietClipped,"printable area clips quiet space before bars");
  var covered=ProjectXml.Clone(label);
  covered.Texts.Add(new TextItem{X=11,Y=12,Text="X",Width=4,Height=10,Size=12});
  var obstructed=BarcodeReadability.Inspect(OutputPlan.Create(covered),output,true,100).Items[0];
  Check(obstructed.QuietObstructed,"a neighboring printed object intrudes into the quiet zone");
 }
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}static void Reject(Action action,string name){try{action();}catch{count++;return;}throw new Exception("Accepted: "+name);}
 static Result Decode(BitMatrix matrix,BarcodeFormat format){using(var bitmap=new Bitmap((matrix.Width+16)*4,(matrix.Height+16)*4))using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);for(int y=0;y<matrix.Height;y++)for(int x=0;x<matrix.Width;x++)if(matrix[x,y])g.FillRectangle(Brushes.Black,(x+8)*4,(y+8)*4,4,4);var reader=new BarcodeReader();reader.Options.PossibleFormats=new[]{format};reader.Options.TryHarder=true;var result=reader.Decode(bitmap);Check(result!=null,"decode "+format);return result;}}
 static void MatrixTests(){foreach(string type in BarcodeCatalog.MatrixTypes){bool gs1=type.StartsWith("GS1 ");string data=gs1?"(01)12345678901231(10)LOT(21)SERIAL":"Žltý štítok 123 / https://example.com";var matrix=Assets.Matrix(new MatrixContent{Type=type,Data=data});var format=type=="QR"||type=="GS1 QR"?BarcodeFormat.QR_CODE:type=="Data Matrix"||type=="GS1 Data Matrix"?BarcodeFormat.DATA_MATRIX:type=="Aztec"?BarcodeFormat.AZTEC:BarcodeFormat.PDF_417;var result=Decode(matrix,format);string expected=gs1?Gs1.Payload(Gs1.Parse(data),2000):data;Check(result.Text==expected||gs1&&result.Text=="\u001d"+expected,"matrix text "+type+": "+result.Text);if(gs1)Check(Convert.ToString(result.ResultMetadata[ResultMetadataType.SYMBOLOGY_IDENTIFIER])==(type=="GS1 QR"?"]Q3":"]d2"),"GS1 FNC1 "+type+": "+Convert.ToString(result.ResultMetadata[ResultMetadataType.SYMBOLOGY_IDENTIFIER]));
   var s=LabelSettings.Default();s.Width=180;s.Height=120;s.HideBarcode=true;s.Texts.Clear();s.Symbols.Add(new SymbolItem{X=3,Y=3,Width=100,Height=BarcodeCatalog.Square(type)?100:35,Matrix=new MatrixContent{Type=type,Data=data}});Engine.Validate(s);Check(!LabelChecks.Inspect(s).Exists(i=>i.Blocking),"preflight "+type);var round=ProjectXml.Clone(s);Check(round.Symbols[0].Matrix.Type==type&&round.Symbols[0].Matrix.Data==data,"XML "+type);var copy=ElementTransfer.Decode(ElementTransfer.Encode(new ElementTransfer{Symbol=s.Symbols[0]}));Check(copy.Symbol.Matrix.Type==type,"clipboard "+type);using(var bitmap=Engine.Render(s)){var reader=new BarcodeReader();reader.Options.PossibleFormats=new[]{format};reader.Options.TryHarder=true;Check(reader.Decode(bitmap)!=null,"scene raster "+type);}Check(VectorArt.Svg(s).Contains("<path"),"vector "+type);
   if(type=="PDF417"){s.Symbols[0].Height=1;Reject(()=>Engine.Validate(s),"PDF417 vertical resolution");Check(LabelChecks.Inspect(s).Exists(i=>i.Blocking),"preflight vertical resolution");}
  }
  foreach(string type in new[]{"GS1 QR","GS1 Data Matrix"})foreach(string data in new[]{"(10)ABC%DEF","(10)ABC%%DEF","(10)lower%case","(10)ABC%DEF(21)XYZ"}){var result=Decode(Assets.Matrix(new MatrixContent{Type=type,Data=data}),type=="GS1 QR"?BarcodeFormat.QR_CODE:BarcodeFormat.DATA_MATRIX);string expected=Gs1.Payload(Gs1.Parse(data),2000);Check(result.Text==expected||result.Text=="\u001d"+expected,"GS1 percent/separator "+type+" "+data+": "+result.Text.Replace("\u001d","<GS>"));}
  foreach(string type in new[]{"GS1 QR","GS1 Data Matrix"})for(int length=1;length<=30;length++){string data="(240)"+new string('A',length)+(length%2==0?"(21)12345678901234567890":"");var result=Decode(Assets.Matrix(new MatrixContent{Type=type,Data=data}),type=="GS1 QR"?BarcodeFormat.QR_CODE:BarcodeFormat.DATA_MATRIX);string expected=Gs1.Payload(Gs1.Parse(data),2000);Check(result.Text==expected||result.Text=="\u001d"+expected,"GS1 capacity boundary "+type+" "+length);}
  foreach(string type in new[]{"QR","Data Matrix"}){var hints=new Dictionary<EncodeHintType,object>{{EncodeHintType.CHARACTER_SET,"UTF-8"},{EncodeHintType.MARGIN,type=="QR"?4:1}};if(type=="Data Matrix"){hints[EncodeHintType.DATA_MATRIX_SHAPE]=ZXing.Datamatrix.Encoder.SymbolShapeHint.FORCE_SQUARE;hints[EncodeHintType.DATA_MATRIX_COMPACT]=true;}var before=new MultiFormatWriter().encode("old žltý project",type=="QR"?BarcodeFormat.QR_CODE:BarcodeFormat.DATA_MATRIX,0,0,hints);Check(before.Equals(Assets.Matrix(new MatrixContent{Type=type,Data="old žltý project"})),"legacy matrix identical "+type);}
 }
 static void LinearTests(){foreach(int kind in new[]{112,93,139,27,1001}){var s=LabelSettings.Default();s.Width=180;s.Height=60;s.Texts.Clear();s.BarX=5;s.BarY=5;s.BarWidth=165;s.Kind=kind;s.Ean=BarcodeCatalog.Sample(kind);s.Digits=true;var format=kind==112?BarcodeFormat.UPC_E:kind==93?BarcodeFormat.CODE_93:kind==139?BarcodeFormat.CODE_39:kind==27?BarcodeFormat.CODABAR:BarcodeFormat.MSI;using(var bitmap=Engine.Render(s)){var reader=new BarcodeReader();reader.Options.PossibleFormats=new[]{format};reader.Options.TryHarder=true;reader.Options.UseCode39ExtendedMode=true;var decoded=reader.Decode(bitmap);Check(decoded!=null,"linear raster "+kind);string expected=Engine.Normalize(s.Ean,kind);if(kind==27)expected=expected.Substring(1,expected.Length-2);Check(decoded.Text==expected,"linear content "+kind+": "+decoded.Text);}Check(ProjectXml.Clone(s).Kind==kind,"linear XML "+kind);Reject(()=>Engine.Normalize("",kind),"empty "+kind);Reject(()=>Engine.Normalize("á",kind),"alphabet "+kind);}
  foreach(string text in new[]{"+$%/","Hello: world!","@`[~]"}){string bits=Engine.Bits(text,139);var s=LabelSettings.Default();s.Width=200;s.Height=60;s.BarWidth=180;s.Kind=139;s.Ean=text;using(var bitmap=Engine.Render(s)){var reader=new BarcodeReader();reader.Options.PossibleFormats=new[]{BarcodeFormat.CODE_39};reader.Options.UseCode39ExtendedMode=true;var decoded=reader.Decode(bitmap);Check(decoded!=null&&decoded.Text==text,"extended characters "+text);}}
  string zero=new string('1',5)+new string('0',20),start=new string('1',14)+new string('0',11)+new string('1',14)+new string('0',11)+new string('1',5)+new string('0',20)+new string('1',14)+new string('0',11),end=new string('0',20)+new string('1',5)+new string('0',20)+new string('1',5)+new string('0',14)+new string('1',11)+new string('0',14)+new string('1',11);string expectedPlessey=new string('0',100)+start;for(int i=0;i<12;i++)expectedPlessey+=zero;expectedPlessey+=new string('1',25)+end+new string('0',100);Check(Engine.Bits("0",1002)==expectedPlessey,"Plessey zero polynomial CRC golden vector");Reject(()=>Engine.Normalize("G",1002),"Plessey alphabet");
 }
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 static Control Find(Control parent,string name){if(parent.Name==name)return parent;foreach(Control child in parent.Controls){var result=Find(child,name);if(result!=null)return result;}return null;}
 static void Dialog(MainForm main,bool primary,Action<Form> action){Exception error=null;bool seen=false;using(var timer=new Timer{Interval=100}){int ticks=0;timer.Tick+=delegate{if(++ticks>50){timer.Stop();error=new Exception("Picker timeout: "+Application.OpenForms.Count);foreach(Form open in Application.OpenForms)if(open!=main)open.Close();return;}for(int i=Application.OpenForms.Count-1;i>=0;i--){var form=Application.OpenForms[i];if(form==main||Find(form,"barcodeFormats")==null)continue;timer.Stop();seen=true;try{action(form);}catch(Exception ex){error=ex;form.DialogResult=DialogResult.Cancel;}break;}};timer.Start();Call(main,"ChooseBarcode",primary);}if(error!=null)throw error;Check(seen,"picker shown");}
 static void Ui(string dir){File.AppendAllText(Path.Combine(dir,"ui-stages.txt"),"construct\n");using(var main=new MainForm(Path.Combine(dir,"catalog-prefs.xml"))){main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();File.AppendAllText(Path.Combine(dir,"ui-stages.txt"),"shown\n");var initial=(LabelSettings)Call(main,"Read");foreach(string lang in Loc.Codes){Loc.Language=lang;File.AppendAllText(Path.Combine(dir,"ui-stages.txt"),lang+"\n");Dialog(main,true,form=>{var list=(ListBox)Find(form,"barcodeFormats");var data=(TextBox)Find(form,"barcodeData");Check(list.Items.Count==15,"linear picker choices");Check(data.Text==initial.Ean,"primary data retained");((TextBox)Find(form,"barcodeSearch")).Text="Full ASCII";Check(list.Items.Count==1&&((BarcodeDefinition)list.SelectedItem).Kind==139,"search type");Check(data.Text==initial.Ean,"search preserves data");((TextBox)Find(form,"barcodeSearch")).Text="unavailable";Check(list.Items.Count==0,"empty search");form.DialogResult=DialogResult.Cancel;});Check(ProjectXml.Encode(initial)==ProjectXml.Encode((LabelSettings)Call(main,"Read")),"cancel leaves document "+lang);}
  Loc.Language="sk";File.AppendAllText(Path.Combine(dir,"ui-stages.txt"),"add PDF\n");Dialog(main,false,form=>{var search=(TextBox)Find(form,"barcodeSearch");search.Text="PDF417";var list=(ListBox)Find(form,"barcodeFormats");Check(list.Items.Count==1,"PDF417 searchable");((TextBox)Find(form,"barcodeData")).Text="sample 123";((Button)form.AcceptButton).PerformClick();});var document=(LabelSettings)Call(main,"Read");Check(document.Symbols.Count==initial.Symbols.Count+1,"picker adds symbol");var symbol=document.Symbols[document.Symbols.Count-1];Check(symbol.Matrix.Type=="PDF417"&&symbol.Width!=symbol.Height,"picker keeps PDF aspect");Check(!LabelChecks.Inspect(document).Exists(i=>i.Blocking),"added PDF passes preflight");
  Dialog(main,true,form=>{((TextBox)Find(form,"barcodeSearch")).Text="Full ASCII";((TextBox)Find(form,"barcodeData")).Text="Label 123";((Button)form.AcceptButton).PerformClick();});document=(LabelSettings)Call(main,"Read");Check(document.Kind==139&&document.Ean=="Label 123","primary selection applied");typeof(MainForm).GetField("dirty",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,false);main.Close();}}
 [STAThread]static void Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Readability();
  Check(BarcodeCatalog.All.Length==21,"catalog choices");
  foreach(var entry in BarcodeCatalog.All){if(entry.MatrixType!=null){var matrix=Assets.Matrix(new MatrixContent{Type=entry.MatrixType,Data=entry.Sample});Check(matrix.Width>0&&matrix.Height>0,entry.Name);}else if(!entry.Gs1){Check(Engine.Bits(entry.Sample,entry.Kind).Length>0,entry.Name);}}
  Check(Engine.Normalize("0425261",112)=="04252614","UPC-E checksum");Reject(()=>Engine.Normalize("04252615",112),"UPC-E wrong checksum");Reject(()=>Engine.Normalize("2425261",112),"UPC-E number system");
  Check(MoreBarcodes.Validate("123",27)=="A123A","Codabar default guards");Reject(()=>MoreBarcodes.Validate("A123",27),"unpaired guard");Reject(()=>MoreBarcodes.Validate("ač",139),"non ASCII");
  Check(Engine.Bits("+$%/",139)!=Engine.Bits("+$%/",39),"extended escape characters");
  var data=Gs1.Parse("(01)12345678901231(10)LOT(21)SERIAL");Check(Gs1.Payload(data,2000)=="011234567890123110LOT\u001d21SERIAL","GS1 separator");Reject(()=>Gs1.Parse("01abc"),"AI syntax");Reject(()=>Assets.Matrix(new MatrixContent{Type="GS1 QR",Data="(01)12345678901232"}),"GS1 checksum");
  Check(!BarcodeCatalog.Square("PDF417")&&BarcodeCatalog.Square("Aztec"),"matrix aspect");
  MatrixTests();LinearTests();Ui(args[0]);
  File.WriteAllText(Path.Combine(args[0],"passed.txt"),"PASS "+count+" barcode catalog checks");
 }catch(Exception ex){File.WriteAllText(Path.Combine(args[0],"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
