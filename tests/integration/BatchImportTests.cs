using System;using System.IO;using System.IO.Compression;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;using EanEtikety;
class BatchImportTests {
 static void CsvValidation(){
  var source=BatchData.CsvText("Code;Qty;Description;Id\r\n590123412345;1;\"First\r\nlabel\";007\r\n5901234123450;bad;Next;008\n;1;Missing;007\r590123412345;1;;7",';');
  var template=LabelSettings.Default();
  template.Batch.UniqueColumn=3;
  var mappings=new List<ColumnBinding>{new ColumnBinding{Target="code",Column=0},
   new ColumnBinding{Target="text:0",Column=2}};
  var batch=BatchProcessing.Prepare(template,source,true,mappings,1,1,DateTime.Today);
  Check(batch.Count==4&&batch[0].Row==2&&batch[1].Row==4&&batch[2].Row==5&&batch[3].Row==6,"physical CSV line numbers through CRLF, LF, CR and embedded line breaks");
  Check(batch[0].Label.Texts[0].Text=="First\r\nlabel","quoted multiline data preserved");
  Check(batch[1].Issues.Count==2&&batch[1].Issues.Exists(issue=>issue.Column==0)&&batch[1].Issues.Exists(issue=>issue.Column==1),"collect code and quantity errors with source columns");
  Check(batch[1].Error!=null&&batch[1].Label==null&&batch[1].PreviewLabel.HideBarcode&&batch[1].PreviewLabel.Texts[0].Text=="Next","partial preview preserves usable data and omits an invalid code");
  using(var scene=VectorArt.Build(batch[1].PreviewLabel))Check(scene.Shapes.Count>0,"partial preview is renderable");
  Check(batch[2].Error!=null&&batch[2].Issues.Exists(issue=>issue.Blocking&&issue.Column==0),"missing mapped code reported");
  Check(batch[0].Error==null&&batch[0].Issues.Exists(issue=>!issue.Blocking&&issue.Column==3),"explicit duplicate column flags first occurrence without blocking");
  Check(batch[2].Issues.Exists(issue=>!issue.Blocking&&issue.Column==3)&&batch[3].Issues.Count==0,"duplicate warning on later occurrence, optional empty text and leading zeros preserved");
  Check(ProjectXml.Clone(template).Batch.UniqueColumn==3&&ProjectXml.Decode<BatchSetup>("<BatchSetup />").UniqueColumn==-1,"unique column persists and old projects default to disabled");
  var shortRow=BatchProcessing.Prepare(template,BatchData.CsvText("590123412345;1",';'),false,mappings,1,1,DateTime.Today)[0];
  Check(shortRow.Row==1&&shortRow.Error!=null&&shortRow.Issues.Exists(issue=>issue.Column==2)&&!shortRow.PreviewLabel.HideBarcode,"missing field differs from explicitly empty text");
  var plan=OutputPlan.Create(new List<BatchRecord>{batch[0],batch[3]},template);
  Check(plan.Placements[0].Record==2&&plan.Placements[1].Record==6,"excluded invalid rows never renumber printed records");
  bool rejected=false;try{OutputPlan.Create(batch,template);}catch(InvalidDataException){rejected=true;}
  Check(rejected,"print plan rejects selected invalid records");
  template.Batch.UniqueColumn=-1;
  Check(!BatchProcessing.Prepare(template,source,true,mappings,1,1,DateTime.Today)[0].Issues.Exists(issue=>!issue.Blocking),"uniqueness check is opt-in");
  var sparse=new DataTableSource{Rows=new List<string[]>{new[]{"590123412345","1"}}};
  Check(BatchProcessing.Prepare(template,sparse,false,mappings,1,1,DateTime.Today)[0].Error==null,"sparse XLSX-style rows keep existing optional empty text behavior");
  template.Symbols.Add(new SymbolItem{Width=24,Height=24,Matrix=new MatrixContent{Type="QR",Data="original"}});
  template.Symbols.Add(new SymbolItem{Width=24,Height=24,Matrix=new MatrixContent{Type="Data Matrix",Data="kept"}});
  Elements.Meta(template,-3).Layer=80;
  Elements.Meta(template,-4).Layer=3;
  mappings.Add(new ColumnBinding{Target="symbol:0",Column=3});
  var invalidMatrix=BatchProcessing.Prepare(template,BatchData.CsvText("590123412345;1;Valid text;",';'),false,mappings,1,1,DateTime.Today)[0];
  Check(invalidMatrix.Issues.Count==1&&invalidMatrix.Issues[0].Column==3&&invalidMatrix.PreviewLabel.Symbols.Count==1&&
   invalidMatrix.PreviewLabel.Symbols[0].Matrix.Data=="kept"&&!invalidMatrix.PreviewLabel.HideBarcode&&
   Elements.Layer(invalidMatrix.PreviewLabel,-3)==3,"invalid matrix is omitted while valid artwork keeps its layers");
  var invalidText=BatchProcessing.Prepare(template,BatchData.CsvText("590123412345;1;bad\u0001text;QR",';'),false,mappings,1,1,DateTime.Today)[0];
  Check(invalidText.Label==null&&invalidText.Issues.Exists(issue=>issue.Blocking&&issue.Column==2)&&
   invalidText.PreviewLabel.Texts[0].Text=="","unprintable XML text gets a mapped row error and a safe preview");
  try{BatchData.CsvText("Heading\r\n\"Multiline\r\nfield\"\r\n\"unclosed",';');throw new Exception("invalid CSV accepted");}
  catch(InvalidDataException ex){Check(ex.Message.Contains("4"),"CSV syntax error cites the source line");}
 }
 static int count;static void Check(bool b,string message){count++;if(!b)throw new Exception(message);}static void Entry(ZipArchive z,string name,string content){using(var w=new StreamWriter(z.CreateEntry(name).Open()))w.Write(content);}
 [STAThread]static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);try{CsvValidation();string file=Path.Combine(dir,"source.xlsx");using(var fs=File.Create(file))using(var z=new ZipArchive(fs,ZipArchiveMode.Create)){
 Entry(z,"xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Products' r:id='r1'/><sheet name='Other' r:id='r2'/></sheets></workbook>");Entry(z,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/><Relationship Id='r2' Target='worksheets/sheet2.xml'/></Relationships>");Entry(z,"xl/styles.xml","<styleSheet><numFmts><numFmt numFmtId='164' formatCode='000000'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='164'/><xf numFmtId='14'/></cellXfs></styleSheet>");Entry(z,"xl/sharedStrings.xml","<sst><si><t>Žltý čaj</t></si></sst>");Entry(z,"xl/worksheets/sheet1.xml","<worksheet><sheetData><row r='1'><c r='A1' s='1'><v>123</v></c><c r='B1' t='s'><v>0</v></c><c r='C1' s='2'><v>45292</v></c><c r='D1'><f>1+1</f><v>2</v></c><c r='E1'><f>1+1</f></c></row><row r='3'><c r='A3' t='inlineStr'><is><t>000045</t></is></c></row></sheetData></worksheet>");Entry(z,"xl/worksheets/sheet2.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Other sheet</t></is></c></row></sheetData></worksheet>");}
 var tables=BatchData.Xlsx(file);Check(tables.Count==2&&tables[1].Name=="Other","sheet selection");Check(tables[0].Rows[0][0]=="000123"&&tables[0].Rows[2][0]=="000045","formatted and text leading zeros");Check(tables[0].Rows[0][1]=="Žltý čaj","shared strings unicode");Check(tables[0].Rows[0][2]=="2024-01-01","Excel numeric date");Check(tables[0].Rows[0][3]=="2"&&tables[0].Rows[0][4].StartsWith("!ERROR"),"formula cached result versus error");Check(tables[0].Rows[1].Length==0,"sparse row number retained");
 var persisted=LabelSettings.Default();persisted.Texts.Add(new TextItem{Text="Second"});persisted.Batch.Bindings.Add(new ColumnBinding{Target="text:1",Column=3});persisted=ProjectXml.Clone(persisted);Check(persisted.Batch.Bindings[0].Column==3,"batch mapping project roundtrip");Elements.Remove(persisted,new List<int>{0});Check(persisted.Batch.Bindings[0].Target=="text:0","mapping follows surviving element after deletion");var label=LabelSettings.Default();label.Kind=128;var mappings=new List<ColumnBinding>{new ColumnBinding{Target="code",Column=0},new ColumnBinding{Target="text:0",Column=1}};var record=BatchData.Record(label,tables[0].Rows[0],mappings,3,0,1,DateTime.Today);Check(record.Error==null&&record.Count==2&&record.Label.Ean=="000123","mapped record quantity");mappings[1].Column=4;Check(BatchData.Record(label,tables[0].Rows[0],mappings,3,0,1,DateTime.Today).Error!=null,"formula error blocks mapped row");
 var profileFile=Path.Combine(dir,"printers.xml");var profiles=new PrinterProfileStore(profileFile);profiles.Save(new PrinterProfile{Printer="Printer A",Paper="A4",Width=827,Height=1169,X=-1.25,Y=2.5});profiles.Save(new PrinterProfile{Printer="Printer B",Paper="Custom",Width=200,Height=300,Landscape=true});profiles=new PrinterProfileStore(profileFile);Check(profiles.Find("Printer A").X==-1.25&&profiles.Find("Printer B").Landscape,"independent persistent printer profiles");
 label=LabelSettings.Default();label.Symbols.Add(new SymbolItem{X=1,Y=1,Width=24,Height=24,Matrix=new MatrixContent{Type="QR",Data="test"}});label.Texts[0].X=2;label.Texts[0].Y=1;label.Texts[0].Width=20;label.Texts[0].Height=1;Check(LabelChecks.Inspect(label).Exists(i=>i.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu")&&i.Element==0),"top QR quiet zone checked");label.Symbols[0].Rotation=90;Check(LabelChecks.Inspect(label).Exists(i=>i.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu")),"rotated quiet zone checked");
 label=LabelSettings.Default();label.UsePaper=true;label.PageWidth=210;label.PageHeight=297;label.Layout.Enabled=true;label.Layout.Count=18;label.Layout.FirstSlot=14;var plan=OutputPlan.Create(label);JobOutput.Export(Path.Combine(dir,"sheets.zip"),"svg",plan,203);using(var z=ZipFile.OpenRead(Path.Combine(dir,"sheets.zip")))Check(z.Entries.Count==3,"one SVG for every output page");
 File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+count+" XLSX, mapping, printer profile, quiet-zone and multipage export checks.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
