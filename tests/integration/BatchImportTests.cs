using System;using System.IO;using System.IO.Compression;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;using EanEtikety;
class BatchImportTests {
 static void SavedReferences(string dir){
  var source=BatchData.CsvText("SKU;Name;Qty;Private\r\n001;\"First\r\nlabel\";2;unused secret\r\n002;Old;1;other secret",';');
  var mapping=new BatchSetup{Header=true,QuantityColumn=2,UniqueColumn=0,Bindings=new List<ColumnBinding>{
   new ColumnBinding{Target="text:0",Column=1}}};
  var preset=BatchPresets.Capture("Supplier",mapping,source,false);
  BatchReferences.EnsurePresetMapping(source,mapping,preset);
  preset.Reference=BatchReferences.Capture(source,mapping,0,"source.csv",null,DateTime.UtcNow);
  var project=LabelSettings.Default();
  project.Batch.Presets.Add(preset);
  project.Batch.Presets.Add(new BatchPreset{Name="Other"});
  string projectPath=Path.Combine(dir,"saved-reference.ean.xml");
  Engine.Save(projectPath,project);
  var saved=Engine.Load(projectPath).Batch.Presets;
  var reference=saved[0].Reference;
  Check(reference.RowCount==2&&reference.SourceName=="source.csv"&&saved[1].Reference==null,"one optional reference round-trips per preset");
  var table=BatchReferences.Open(reference);
  Check(table.Rows[0].Length==3&&Array.IndexOf(table.Rows[0],"unused secret")<0,"only identifier and mapped values are saved");
  var output=JobPreparation.Resolve(project,DateTime.Today);
  Check(output.Batch.Presets[0].Reference==null&&project.Batch.Presets[0].Reference!=null,"output snapshot excludes reference without changing the project");
  string zpl=Path.Combine(dir,"saved-reference.zpl");
  Engine.Zpl(zpl,project);
  var imported=ZplImport.Project(File.ReadAllText(zpl));
  Check(imported!=null&&imported.Batch.Presets[0].Name=="Supplier"&&imported.Batch.Presets[0].Reference==null,
   "editable ZPL metadata keeps ordinary presets without reference values");
  var current=BatchData.CsvText("Qty;sku;NAME\n3;002;New\n2;001;\"First\nlabel\"",';');
  mapping.QuantityColumn=0;mapping.UniqueColumn=1;mapping.Bindings[0].Column=2;
  var comparison=BatchReferences.Compare(current,mapping,reference);
  Check(comparison.Changed==1&&comparison.Unchanged==1&&comparison.Rows[0].ReferenceRow==4&&comparison.Rows[0].Values.Count==2,
   "stored reference compares reorganized columns and retains original CSV rows");
  BatchReferences.EnsurePresetMapping(current,mapping,saved[0]);
  var setupCopy=project.Batch.Copy();
  Check(setupCopy.Presets[0].Reference.PackedRows!=preset.Reference.PackedRows&&
   setupCopy.Presets[0].Reference.Fields[0]!=preset.Reference.Fields[0],"editor copies keep independent reference state");
  mapping.UniqueColumn=0;
  bool invalid=false;
  try{BatchReferences.Compare(current,mapping,reference);}catch(InvalidDataException){invalid=true;}
  Check(invalid,"changing the configured identifier makes the reference incompatible");
  invalid=false;
  try{BatchReferences.EnsurePresetMapping(current,mapping,saved[0]);}catch(InvalidDataException){invalid=true;}
  Check(invalid,"temporary overrides cannot save a reference under a different preset mapping");
  invalid=false;
  try{BatchReferences.Capture(BatchData.CsvText("ID\n01\n01",';'),new BatchSetup(),0,"bad.csv",null,DateTime.UtcNow);}
  catch(InvalidDataException){invalid=true;}
  Check(invalid,"duplicate identifiers cannot replace a reference");
  var sparse=new DataTableSource{Rows=new List<string[]>{new[]{"ID","Text"},new string[0],new[]{"0007","Alfa"}}};
  var sparseMapping=new BatchSetup{Bindings=new List<ColumnBinding>{new ColumnBinding{Target="text:0",Column=1}}};
  var worksheet=BatchReferences.Capture(sparse,sparseMapping,0,"workbook.xlsx","Products",DateTime.UtcNow);
  var reopened=BatchReferences.Open(worksheet);
  Check(worksheet.SheetName=="Products"&&worksheet.RowCount==1&&reopened.SourceRow(0)==3&&reopened.Rows[0][0]=="0007",
   "saved worksheet skips gaps and preserves row, sheet and leading zeros");
  BatchReferences.Replace(project.Batch,preset,null);
  Engine.Save(projectPath,project);
  Check(Engine.Load(projectPath).Batch.Presets[0].Reference==null,"deletion persists when saving the project");
 }
 static void Comparison(){
  var setup=new BatchSetup{Header=true,QuantityColumn=2,Bindings=new List<ColumnBinding>{
   new ColumnBinding{Target="text:0",Column=1},new ColumnBinding{Target="code",Column=3}}};
  var current=BatchData.CsvText("SKU;Name;Qty;EAN\r\n01;Same; 02 ;0001\r\n02;\"New\r\nlabel\";3;0002\r\n03;Added;1;0003",';');
  var reference=BatchData.CsvText("ean;qty;name;sku\n0002;1;Old;02\n0001;2;Same;01\n0004;1;Removed;04",';');
  var report=BatchComparison.Compare(current,reference,setup,true,0,3);
  Check(report.Issues.Count==0&&report.Unchanged==1&&report.Rows.Count==3,"reordered columns and rows match and numeric counts compare by value");
  var changed=report.Rows[0];
  Check(changed.Kind==BatchDifferenceKind.Changed&&changed.Id=="02"&&changed.CurrentRow==3&&changed.ReferenceRow==2&&
   changed.Values.Count==2&&changed.Values[0].Before=="Old"&&changed.Values[0].After=="New\r\nlabel","changed fields retain values and original rows");
  Check(report.Rows[1].Kind==BatchDifferenceKind.Added&&report.Rows[1].CurrentRow==5&&report.Rows[1].ReferenceRow==0&&
   report.Rows[2].Kind==BatchDifferenceKind.Removed&&report.Rows[2].CurrentRow==0&&report.Rows[2].ReferenceRow==4,"added and removed rows keep their own source location");
  var bad=BatchData.CsvText("SKU;Name;Qty;EAN\n01;A;1;X\n01;B;2;Y\n;C;1;Z",';');
  var blocked=BatchComparison.Compare(current,bad,setup,true,0,0);
  Check(blocked.Rows.Count==0&&blocked.Issues.Count==3&&blocked.Issues[0].ReferenceRow==2&&
   blocked.Issues[1].ReferenceRow==3&&blocked.Issues[2].ReferenceRow==4,"all duplicate and missing keys reported without partial claims");
  var badCurrent=BatchComparison.Compare(bad,reference,setup,true,0,3);
  Check(badCurrent.Rows.Count==0&&badCurrent.Issues[2].CurrentRow==4,"invalid current rows retain a preview link");
  var positional=BatchComparison.Compare(BatchData.CsvText("001;A\n1;B",';'),
   BatchData.CsvText("1;B\n001;A",';'),new BatchSetup{Header=false},false,0,0);
  Check(positional.Unchanged==2&&positional.Rows.Count==0,"leading zeros remain significant for positional identifiers");
  var renamed=BatchComparison.Compare(BatchData.CsvText("SKU;Name\n001;A",';'),BatchData.CsvText("ID;name\n001;A",';'),
   new BatchSetup{Bindings=new List<ColumnBinding>{new ColumnBinding{Target="code",Column=0},new ColumnBinding{Target="text:0",Column=1}}},true,0,0);
  Check(renamed.Unchanged==1,"manually paired identifier columns can differ in name even when used as label data");
  var duplicateHeading=BatchData.CsvText("ean;qty;name;sku;NAME\n0001;2;Same;01;Other",';');
  bool ambiguous=false;
  try{BatchComparison.Compare(current,duplicateHeading,setup,true,0,3);}catch(InvalidDataException){ambiguous=true;}
  Check(ambiguous,"duplicate mapped headings cannot silently choose one field");
  WorkContext stop=null;
  stop=new WorkContext(progress=>stop.Cancel());
  bool cancelled=false;
  using(stop.Enter())try{BatchComparison.Compare(current,reference,setup,true,0,3);}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled,"comparison honours background cancellation");
  var headerless=new BatchSetup{Header=false,Bindings=new List<ColumnBinding>{new ColumnBinding{Target="text:0",Column=1}}};
  ambiguous=false;
  try{
   BatchComparison.Compare(BatchData.CsvText("A;New",';'),BatchData.CsvText("Old;A",';'),headerless,false,0,1);
  }catch(InvalidDataException){ambiguous=true;}
  Check(ambiguous,"positional comparison cannot compare a label field with the chosen reference identifier");
  ambiguous=false;
  try{
   BatchComparison.Compare(BatchData.CsvText("SKU;Name\nA;New",';'),BatchData.CsvText("DisplayName;Name\nOld;A",';'),
    new BatchSetup{Bindings=headerless.Bindings},true,0,1);
  }catch(InvalidDataException){ambiguous=true;}
  Check(ambiguous,"heading-based comparison also rejects a field mapped to the reference identifier");
  var xlsx=new DataTableSource{Rows=new List<string[]>{new[]{"SKU","Name","Qty","EAN"},new string[0],new[]{"02","New\nlabel","3","0002"}}};
  var sparse=BatchComparison.Compare(current,xlsx,setup,true,0,0);
  Check(sparse.Issues.Count==0&&sparse.Unchanged==1&&sparse.Added==2&&sparse.Removed==0,
   "sparse worksheet gaps are ignored and line endings in label text do not create a difference");
 }
 static void PortableBackup(string dir){
  string source=Path.Combine(dir,"backup-source"),target=Path.Combine(dir,"backup-target");
  string library=Path.Combine(source,"Templates");
  Directory.CreateDirectory(library);Directory.CreateDirectory(target);
  var label=LabelSettings.Default();
  label.Batch.Presets.Add(new BatchPreset{Name="Supplier",Bindings=new List<ColumnBinding>{new ColumnBinding{Target="code",Column=0,HeaderName="Item"}}});
  label.Batch.ActivePresetName="Supplier";
  Engine.Save(Path.Combine(library,"Shipping.ean.xml"),label);
  var printers=new PrinterProfileStore(Path.Combine(source,"printers.xml"));
  printers.Save(new PrinterProfile{Printer="Source Zebra",Name="40 × 30",Paper="Roll",Width=157,Height=118,X=-1.25,ScaleY=1.04,ZebraZpl=true,ZplDpi=203,Darkness=16});
  printers.Save(new PrinterProfile{Printer="Source Zebra",Name="100 × 150",Paper="Roll",Width=394,Height=591,X=2,ScaleX=.97});
  File.WriteAllText(Path.Combine(source,"secret.csv"),"not included");
  string zip=Path.Combine(dir,"workspace.zip");
  new WorkspaceBackup(source).Export(zip,true,true);
  var service=new WorkspaceBackup(target);
  var review=service.Open(zip);
  var staleStore=new PrinterProfileStore(Path.Combine(target,"printers.xml"));
  Check(!Directory.Exists(Path.Combine(target,"Templates"))&&review.SourcePrinters.Count==1,"reading portable backup is nonmutating");
  var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"Source Zebra","Local Zebra"}};
  var plan=review.Preview(false,map);
  Check(plan.Items.Count==3&&plan.Items.TrueForAll(item=>item.Action==WorkspaceRestoreAction.Add),"restore preview shows added template and both media");
  var result=service.Restore(plan);
  var loaded=Engine.Load(Path.Combine(target,"Templates","Shipping.ean.xml"));
  var roll=new PrinterProfileStore(Path.Combine(target,"printers.xml"));
  Check(loaded.Batch.ActivePresetName=="Supplier"&&loaded.Batch.Presets[0].Bindings[0].HeaderName=="Item","portable template retains its named batch preset");
  Check(roll.ForPrinter("Local Zebra").Count==2&&roll.Find("Local Zebra","40 × 30").Darkness==16&&roll.Find("Local Zebra","40 × 30").ScaleY==1.04&&roll.Find("Local Zebra","100 × 150").ScaleX==.97,"printer mapping preserves independent calibration and Zebra darkness");
  Check(result.Applied==3&&Path.GetDirectoryName(result.BackupPath)==target+".backups"&&File.Exists(result.BackupPath)&&
   !File.Exists(Path.Combine(target,"secret.csv")),"successful restore saves an independent checkpoint and transfers only allowed data");
  plan=service.Open(zip).Preview(false,map);
  Check(plan.Items.TrueForAll(item=>item.Action==WorkspaceRestoreAction.Keep)&&service.Restore(plan).Applied==0,"name conflicts default to keeping local items");
  string local=Path.Combine(target,"Templates","Shipping.ean.xml");
  label.Texts[0].Text="local edit";Engine.Save(local,label);
  plan=service.Open(zip).Preview(true,map);
  result=service.Restore(plan);
  Check(Engine.Load(local).Texts[0].Text!="local edit"&&service.Open(result.BackupPath).Preview(true).Items.Count==3,"explicit replacement and a readable pre-restore ZIP");
  plan=service.Open(zip).Preview(true,map);
  label.Texts[0].Text="changed after preview";Engine.Save(local,label);
  bool rejected=false;
  try{service.Restore(plan);}catch(IOException){rejected=true;}
  Check(rejected&&Engine.Load(local).Texts[0].Text=="changed after preview","stale preview cannot overwrite recent local edits");
  string corrupt=Path.Combine(dir,"invalid-workspace.zip");File.Copy(zip,corrupt);
  using(var archive=ZipFile.Open(corrupt,ZipArchiveMode.Update)){Entry(archive,"../accidental.txt","extra");}
  rejected=false;try{service.Open(corrupt);}catch(InvalidDataException){rejected=true;}
  Check(rejected&&!File.Exists(Path.Combine(dir,"accidental.txt")),"unexpected entries are rejected without extraction");
  string tampered=Path.Combine(dir,"tampered.zip");File.Copy(zip,tampered);
  using(var archive=ZipFile.Open(tampered,ZipArchiveMode.Update)){
   var entry=archive.GetEntry("Templates/Shipping.ean.xml");
   using(var stream=entry.Open()){stream.Position=0;stream.WriteByte(33);}
  }
  rejected=false;try{service.Open(tampered);}catch(InvalidDataException){rejected=true;}
  Check(rejected,"same-length payload tampering fails the ZIP inventory checksum");
  File.WriteAllText(local,"<broken");
  result=service.Restore(service.Open(zip).Preview(true,map));
  var checkpoint=service.Open(result.BackupPath).Preview(true);
  Check(checkpoint.Items.Exists(item=>item.Name=="Shipping.ean.xml"&&item.Action==WorkspaceRestoreAction.Invalid)&&
   checkpoint.Items.Exists(item=>item.TargetPrinter=="Local Zebra"&&item.Action==WorkspaceRestoreAction.Replace),
   "checkpoint can reopen after replacing a damaged template, with other entries eligible");
  staleStore.Save(new PrinterProfile{Printer="Another",Name="Roll",Width=100,Height=200});
  Check(new PrinterProfileStore(Path.Combine(target,"printers.xml")).ForPrinter("Local Zebra").Count==2,"saving from an earlier profile store retains restored media");
  string rollback=Path.Combine(dir,"rollback-work");
  string blocked=Path.Combine(rollback,"printers.xml");
  Directory.CreateDirectory(blocked);
  var failing=new WorkspaceBackup(rollback);
  rejected=false;try{failing.Open(zip);}catch(IOException){rejected=true;}
  Check(rejected&&!Directory.Exists(rollback+".backups"),"directory blocking profiles is detected in preview");
  Directory.Delete(blocked);
  new PrinterProfileStore(blocked).Save(new PrinterProfile{Printer="Existing",Name="Roll",Width=200,Height=100});
  plan=failing.Open(zip).Preview(false);
  rejected=false;
  using(var hold=File.Open(blocked,FileMode.Open,FileAccess.Read,FileShare.Read)){
   try{failing.Restore(plan);}catch(IOException){rejected=true;}catch(UnauthorizedAccessException){rejected=true;}
  }
  Check(rejected&&!File.Exists(Path.Combine(rollback,"Templates","Shipping.ean.xml"))&&
   Directory.GetFiles(rollback+".backups","*.zip").Length==1,"later write failure rolls back earlier additions and retains checkpoint");
  string templatesOnly=Path.Combine(dir,"templates.zip");
  new WorkspaceBackup(source).Export(templatesOnly,true,false);
  using(var archive=ZipFile.OpenRead(templatesOnly))Check(archive.GetEntry("printers.xml")==null,"template-only export excludes media profiles");
  var stop=new WorkContext();stop.Cancel();
  byte[] stable=File.ReadAllBytes(templatesOnly);
  rejected=false;using(stop.Enter()){
   try{new WorkspaceBackup(source).Export(templatesOnly,true,true);}catch(OperationCanceledException){rejected=true;}
  }
  Check(rejected&&UpdateFiles.Digest(stable)==UpdateFiles.Hash(templatesOnly),"cancelled export preserves an existing ZIP");
 }
 static void Presets(){
  var source=BatchData.CsvText("Item;Case;Qty;Serial\r\n590123412345;858000000001;2;001",';');
  var setup=new BatchSetup{QuantityColumn=2,UniqueColumn=3};
  setup.Bindings.Add(new ColumnBinding{Target="code",Column=0});
  setup.Bindings.Add(new ColumnBinding{Target="text:1",Column=1});
  var preset=BatchPresets.Capture("Supplier",setup,source,false);
  setup.Presets.Add(preset);setup.ActivePresetName=preset.Name;
  setup=ProjectXml.Decode<BatchSetup>(ProjectXml.Encode(setup));
  preset=setup.Presets[0];
  Check(preset.Name==setup.ActivePresetName&&preset.Header&&preset.Bindings.Count==4,"preset, quantity and duplicate column persist");
  var reordered=BatchData.CsvText("Case;Serial;QTY; ITEM \r\n858000000001;008;3;590123412345",';');
  var mapping=BatchPresets.Resolve(preset,reordered,4);
  Check(mapping.Problems.Count==0&&mapping.Columns["code"]==3&&mapping.Columns["quantity"]==2&&mapping.Columns["unique"]==1,"headers match across reordering, case and edge spaces");
  var label=LabelSettings.Default();
  var records=BatchProcessing.Prepare(label,reordered,true,new List<ColumnBinding>{new ColumnBinding{Target="code",Column=mapping.Columns["code"]}},mapping.Columns["quantity"],1,DateTime.Today);
  Check(records[0].Label.Ean=="590123412345"&&records[0].Count==3,"column swap between valid codes uses intended value");
  var changed=BatchData.CsvText("ITEM;item;Case;Serial\n1;2;3;4",';');
  mapping=BatchPresets.Resolve(preset,changed,4);
  Check(mapping.Problems.ContainsKey("code")&&mapping.Problems.ContainsKey("quantity")&&!mapping.Columns.ContainsKey("code")&&!mapping.Columns.ContainsKey("quantity"),"duplicate and missing headers cannot fall back to positions");
  var positional=new BatchSetup{Header=false,Separator='\t',CodePage=1250};
  positional.Bindings.Add(new ColumnBinding{Target="code",Column=1});
  var rows=BatchData.CsvText("\"one\none\"\t00023\nsecond\t00024\nthird\t00025\nfourth\t00026\nfifth\t00027\nsixth\t00028",'\t');
  var positions=BatchPresets.Capture("Positions",positional,rows,false);
  Check(!positions.Header&&positions.Separator=='\t'&&positions.CodePage==1250&&BatchPresets.Resolve(positions,rows,2).Columns["code"]==1,"headerless preset retains CSV settings and position");
  Check(BatchPresets.Resolve(positions,rows,1).Problems.ContainsKey("code"),"out-of-range positions are blocked");
  var multiline=BatchData.CsvText("\"Item\r\nCode\";Case;Qty;Serial\r\n1;2;3;4",';');
  var multilinePreset=BatchPresets.Capture("Multiline",setup,multiline,false);
  multilinePreset=ProjectXml.Decode<BatchPreset>(ProjectXml.Encode(multilinePreset));
  Check(BatchPresets.Resolve(multilinePreset,multiline,4).Columns["code"]==0,"quoted multiline header resolves after project XML round trip");
  var sample=BatchPresets.Sample(rows,false,new[]{1,0});
  Check(sample.Count==5&&sample[0].Row==1&&sample[1].Row==3&&sample[0].Values[0]=="00023"&&sample[0].Values[1]=="one\none","first five samples preserve original physical lines and input values");
  label.Texts.Add(new TextItem{Text="Second"});label.Batch=setup;
  Elements.Remove(label,new List<int>{0});
  Check(label.Batch.Presets[0].Bindings.Exists(b=>b.Target=="text:0")&&label.Batch.Presets[0].Bindings.Exists(b=>b.Target=="unique"),"deletion remaps surviving preset fields and keeps special bindings");
 }
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
 [STAThread]static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);try{SavedReferences(dir);Comparison();PortableBackup(dir);Presets();CsvValidation();string file=Path.Combine(dir,"source.xlsx");using(var fs=File.Create(file))using(var z=new ZipArchive(fs,ZipArchiveMode.Create)){
 Entry(z,"xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Products' r:id='r1'/><sheet name='Other' r:id='r2'/></sheets></workbook>");Entry(z,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/><Relationship Id='r2' Target='worksheets/sheet2.xml'/></Relationships>");Entry(z,"xl/styles.xml","<styleSheet><numFmts><numFmt numFmtId='164' formatCode='000000'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='164'/><xf numFmtId='14'/></cellXfs></styleSheet>");Entry(z,"xl/sharedStrings.xml","<sst><si><t>Žltý čaj</t></si></sst>");Entry(z,"xl/worksheets/sheet1.xml","<worksheet><sheetData><row r='1'><c r='A1' s='1'><v>123</v></c><c r='B1' t='s'><v>0</v></c><c r='C1' s='2'><v>45292</v></c><c r='D1'><f>1+1</f><v>2</v></c><c r='E1'><f>1+1</f></c></row><row r='3'><c r='A3' t='inlineStr'><is><t>000045</t></is></c></row></sheetData></worksheet>");Entry(z,"xl/worksheets/sheet2.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Other sheet</t></is></c></row></sheetData></worksheet>");}
 var tables=BatchData.Xlsx(file);Check(tables.Count==2&&tables[1].Name=="Other","sheet selection");Check(tables[0].Rows[0][0]=="000123"&&tables[0].Rows[2][0]=="000045","formatted and text leading zeros");Check(tables[0].Rows[0][1]=="Žltý čaj","shared strings unicode");Check(tables[0].Rows[0][2]=="2024-01-01","Excel numeric date");Check(tables[0].Rows[0][3]=="2"&&tables[0].Rows[0][4].StartsWith("!ERROR"),"formula cached result versus error");Check(tables[0].Rows[1].Length==0,"sparse row number retained");
 var persisted=LabelSettings.Default();persisted.Texts.Add(new TextItem{Text="Second"});persisted.Batch.Bindings.Add(new ColumnBinding{Target="text:1",Column=3});persisted=ProjectXml.Clone(persisted);Check(persisted.Batch.Bindings[0].Column==3,"batch mapping project roundtrip");Elements.Remove(persisted,new List<int>{0});Check(persisted.Batch.Bindings[0].Target=="text:0","mapping follows surviving element after deletion");var label=LabelSettings.Default();label.Kind=128;var mappings=new List<ColumnBinding>{new ColumnBinding{Target="code",Column=0},new ColumnBinding{Target="text:0",Column=1}};var record=BatchData.Record(label,tables[0].Rows[0],mappings,3,0,1,DateTime.Today);Check(record.Error==null&&record.Count==2&&record.Label.Ean=="000123","mapped record quantity");mappings[1].Column=4;Check(BatchData.Record(label,tables[0].Rows[0],mappings,3,0,1,DateTime.Today).Error!=null,"formula error blocks mapped row");
 var profileFile=Path.Combine(dir,"printers.xml");var profiles=new PrinterProfileStore(profileFile);profiles.Save(new PrinterProfile{Printer="Printer A",Paper="A4",Width=827,Height=1169,X=-1.25,Y=2.5});profiles.Save(new PrinterProfile{Printer="Printer B",Paper="Custom",Width=200,Height=300,Landscape=true});profiles=new PrinterProfileStore(profileFile);Check(profiles.Find("Printer A").X==-1.25&&profiles.Find("Printer B").Landscape,"independent persistent printer profiles");
 label=LabelSettings.Default();label.Symbols.Add(new SymbolItem{X=1,Y=1,Width=24,Height=24,Matrix=new MatrixContent{Type="QR",Data="test"}});label.Texts[0].X=2;label.Texts[0].Y=1;label.Texts[0].Width=20;label.Texts[0].Height=1;Check(LabelChecks.Inspect(label).Exists(i=>i.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu")&&i.Element==0),"top QR quiet zone checked");label.Symbols[0].Rotation=90;Check(LabelChecks.Inspect(label).Exists(i=>i.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu")),"rotated quiet zone checked");
 label=LabelSettings.Default();label.UsePaper=true;label.PageWidth=210;label.PageHeight=297;label.Layout.Enabled=true;label.Layout.Count=18;label.Layout.FirstSlot=14;var plan=OutputPlan.Create(label);JobOutput.Export(Path.Combine(dir,"sheets.zip"),"svg",plan,203);using(var z=ZipFile.OpenRead(Path.Combine(dir,"sheets.zip")))Check(z.Entries.Count==3,"one SVG for every output page");
 File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+count+" XLSX, mapping, printer profile, quiet-zone and multipage export checks.");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
