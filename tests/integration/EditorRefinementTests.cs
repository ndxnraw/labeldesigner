using System;using System.IO;using System.Drawing;using System.Windows.Forms;using System.Reflection;using EanEtikety;
class EditorRefinementTests {
 static void ReferenceUnderlay(string dir){
  var label=LabelSettings.Default();label.HideBarcode=true;
  string png=Path.Combine(dir,"reference-source.png"),jpg=Path.Combine(dir,"reference-source.jpg");
  using(var image=new Bitmap(120,80))using(var graphics=Graphics.FromImage(image)){
   graphics.Clear(Color.Red);image.Save(png,System.Drawing.Imaging.ImageFormat.Png);
   image.Save(jpg,System.Drawing.Imaging.ImageFormat.Jpeg);
  }
  var reference=EditorReference.Import(png,label.Width,label.Height) with {X=7,Y=15,Width=24,Height=16,Opacity=50,Locked=true};
  Check(EditorReference.Import(jpg,label.Width,label.Height).Width==60,"JPEG keeps its aspect ratio on import");
  label.EditorReference=reference;
  string project=Path.Combine(dir,"reference.ean.xml");
  Engine.Save(project,label);File.Delete(png);File.Delete(jpg);
  label=Engine.Load(project);
  Check(label.EditorReference==reference,"the reference image and settings survive project reopening without source files");
  var plain=ProjectXml.Clone(label);plain.EditorReference=null;
  var plan=OutputPlan.Create(label);
  Check(plan.Placements[0].Label.EditorReference==null&&QuickPrint.PrepareEditor(label,DateTime.Today).Label.EditorReference==null,
   "print and quick print plans do not copy editor-only image bytes");
  Check(PreflightReport.Inspect(plan).Fingerprint==PreflightReport.Inspect(OutputPlan.Create(plain)).Fingerprint,
   "preflight only checks printable artwork");
  Check(VectorArt.Svg(label)==VectorArt.Svg(plain),"SVG does not include editor-only artwork");
  string output=Path.Combine(dir,"reference-output"),expected=Path.Combine(dir,"reference-plain");
  Engine.Zpl(output+".zpl",label);Engine.Zpl(expected+".zpl",plain);
  Check(File.ReadAllText(output+".zpl")==File.ReadAllText(expected+".zpl")&&ZplImport.Project(File.ReadAllText(output+".zpl")).EditorReference==null,
   "ZPL raster and embedded metadata exclude the editor reference");
  Engine.Pdf(output+".pdf",label);Engine.Pdf(expected+".pdf",plain);
  Check(File.ReadAllBytes(output+".pdf").AsSpan().SequenceEqual(File.ReadAllBytes(expected+".pdf")),"PDF artwork excludes the editor reference");
  Engine.Docx(output+".docx",label);Engine.Docx(expected+".docx",plain);
  using(var actual=System.IO.Compression.ZipFile.OpenRead(output+".docx"))
  using(var baseline=System.IO.Compression.ZipFile.OpenRead(expected+".docx")){
   Check(actual.Entries.Count==baseline.Entries.Count,"Word adds no reference image parts");
   foreach(var entry in actual.Entries){
    using var a=entry.Open();
    using var b=baseline.GetEntry(entry.FullName).Open();
    using var left=new MemoryStream();using var right=new MemoryStream();
    a.CopyTo(left);b.CopyTo(right);
    Check(left.ToArray().AsSpan().SequenceEqual(right.ToArray()),"Word part excludes the reference: "+entry.FullName);
   }
  }
  byte[] PrintRaster(LabelSettings settings,bool previewMode){
   using var document=new LabelPrintDocument(settings);
   using var image=new Bitmap(300,240);
   using(var graphics=Graphics.FromImage(image)){
    graphics.Clear(Color.White);document.RenderPrinterPage(graphics,0,new RectangleF(0,0,300,300),0,0,previewMode);
   }
   using var result=new MemoryStream();image.Save(result,System.Drawing.Imaging.ImageFormat.Png);return result.ToArray();
  }
  Check(PrintRaster(label,false).AsSpan().SequenceEqual(PrintRaster(plain,false))&&
   PrintRaster(label,true).AsSpan().SequenceEqual(PrintRaster(plain,true)),"Windows print and print preview render identical artwork with and without the reference");

  using var owner=new MainForm(Path.Combine(dir,"reference-ui.xml"));
  owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
  Call(owner,"Apply",label);
  var preview=F<PreviewPanel>(owner,"preview");
  Color Pixel(float x,float y){
   using var frame=new Bitmap(preview.Width,preview.Height);
   preview.DrawToBitmap(frame,new Rectangle(Point.Empty,frame.Size));
   var point=preview.ScreenPoint(new PointF(x,y));
   return frame.GetPixel((int)point.X,(int)point.Y);
  }
  var blend=Pixel(10,19);
  Check(blend.R>240&&blend.G is >110 and <145&&Pixel(5,19).ToArgb()==Color.White.ToArgb(),"editor draws the reference at its position, scale and opacity");
  bool inspected=false;Exception failure=null;
  using(var timer=new Timer{Interval=75}){
   timer.Tick+=delegate{
    foreach(Form dialog in Application.OpenForms){
     if(dialog==owner)continue;
     timer.Stop();
     try{
      Control Find(string name)=>dialog.Controls.Find(name,true)[0];
      var x=(NumericUpDown)Find("referenceX");
      var locked=(CheckBox)Find("referenceLocked");
      Check(locked.Checked&&!x.Enabled&&!Find("referenceWidth").Enabled,"locked reference prevents changes to position and dimensions");
      Snapshot(dialog,Path.Combine(dir,"reference-settings.png"));
      locked.Checked=false;
      Check(x.Enabled&&Find("referenceWidth").Enabled,"unlocking restores reference geometry controls");
      x.Value=18;
      ((NumericUpDown)Find("referenceOpacity")).Value=60;
      inspected=true;dialog.AcceptButton.PerformClick();
     }catch(Exception ex){failure=ex;dialog.DialogResult=DialogResult.Cancel;}
     break;
    }
   };
   timer.Start();Call(owner,"EditEditorReference");
  }
  if(failure!=null)throw failure;
  Check(inspected&&((LabelSettings)Call(owner,"Read")).EditorReference.X==18&&Pixel(10,19).ToArgb()==Color.White.ToArgb()&&
   Pixel(21,19).G is >86 and <117,"position and opacity changes apply to the canvas");
  var view=F<ContextMenuStrip>(owner,"viewMenu");
  view.Items["editorReferenceVisible"].PerformClick();
  Check(((LabelSettings)Call(owner,"Read")).EditorReference.Hidden&&Pixel(21,19).ToArgb()==Color.White.ToArgb(),"hiding removes the reference from the editor");
  Call(owner,"StepHistory",false);
  Check(!((LabelSettings)Call(owner,"Read")).EditorReference.Hidden&&Pixel(21,19).G<117,"undo restores the reference display");
  Call(owner,"Apply",LabelSettings.Default());owner.Close();
 }
 static void ResizeLabel(string dir){
  var original=LabelSettings.Default();original.Width=100;original.Height=60;original.PageWidth=100;original.PageHeight=60;
  original.BarX=54;original.BarY=25;
  original.Texts.Clear();
  original.Texts.Add(new TextItem{X=5,Y=4,Width=85,Height=8,Text="Original",Size=12,MinimumFitSize=8,AutoWidth=true});
  original.Texts.Add(new TextItem{X=91,Y=4,Width=8,Height=5,Text="Side"});
  original.Symbols.Add(new SymbolItem{X=70,Y=40,Width=12,Height=12,Matrix=new MatrixContent{Type="QR",Data="123456"}});
  original.Symbols.Add(new SymbolItem{X=74,Y=8,Width=14,Height=10,Shape=new ShapeContent()});
  Elements.Meta(original,-4).Locked=true;
  original.Languages=new LabelLanguages{Codes=new(){"SK","EN"},Selected="SK"};
  Elements.Meta(original,0).Translations=new(){new LanguageTranslation{Language="SK",Text="Krátky text"},new LanguageTranslation{Language="EN",Text=new string('W',140)}};
  Elements.Meta(original,-3).Visibility=new VisibilityRule{Field=0,Mode=VisibilityMode.Filled};
  string imagePath=Path.Combine(dir,"resized-logo.svg");
  File.WriteAllText(imagePath,"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='12'><rect width='20' height='12' fill='black'/></svg>");
  var image=Assets.Import(imagePath);
  original.Symbols.Add(new SymbolItem{X=25,Y=16,Width=10,Height=6,Image=image});
  var options=new System.Collections.Generic.Dictionary<int,ResizeAnchor>{
   {0,new(ResizeEdge.Stretch,ResizeEdge.Start)},{-1,new(ResizeEdge.End,ResizeEdge.End)},
   {-3,new(ResizeEdge.Center,ResizeEdge.End)},{-4,new(ResizeEdge.End,ResizeEdge.End)}
  };
  var resized=LabelResize.Apply(original,70,45,options);
  Check(resized.PageWidth==70&&resized.PageHeight==45&&original.Width==100&&original.Texts[0].Width==85,"resizing a single label updates its media and leaves the source intact");
  Check(resized.BarX==24&&resized.BarY==10&&resized.BarWidth==original.BarWidth&&resized.BarHeight==original.BarHeight&&resized.DigitSize==original.DigitSize,
   "right and bottom anchors move the main code while preserving its dimensions");
  Check(resized.Symbols[0].X==55&&resized.Symbols[0].Y==25&&resized.Symbols[0].Width==12&&resized.Symbols[0].Height==12&&
   resized.Symbols[1].X==74,"center offset is preserved and locked objects keep position");
  Check(resized.Texts[0].Width==55&&resized.Texts[0].MinimumFitSize==8&&!resized.Texts[0].AutoWidth,"stretch preserves margins and fixed-text fitting");
  string path=Path.Combine(dir,"resized-check.ean.xml");Engine.Save(path,resized);
  resized=Engine.Load(path);
  var review=LabelResize.Inspect(resized,new DateTime(2026,11,26));
  Check(review.Exists(problem=>problem.Language=="EN"&&problem.Issue.Element==0&&problem.Issue.Message==Loc.T("Text sa nezmestí do poľa"))&&
   review.Exists(problem=>problem.Issue.Element==1&&problem.Issue.Message==Loc.T("Prvok presahuje etiketu")),"captures text overflow in another language and identifies clipped elements");
  Check(review.Exists(problem=>problem.Issue.Element==-5&&problem.Issue.Message==Loc.T("Prvok zasahuje do voľného priestoru kódu")),
   "reports a new quiet-zone overlap after anchoring the code");
  Check(resized.Languages.Codes.Count==2&&Elements.Meta(resized,-3).Visibility.Field==0&&resized.Symbols[2].Image.Data==image.Data,
   "translations, visibility and embedded images survive a resized template round trip");
  var sheet=ProjectXml.Clone(original);sheet.Layout.Enabled=true;sheet.Layout.Columns=2;sheet.Layout.Rows=1;
  sheet.PageWidth=170;sheet.PageHeight=90;
  var wider=LabelResize.Apply(sheet,85,45,options);
  Check(wider.PageWidth==170&&wider.Layout.Columns==2&&LabelResize.Inspect(wider,DateTime.Today).Exists(p=>p.Issue.Message==Loc.T("Etikety presahujú papier")),
   "sheet dimensions stay fixed and an oversized grid is reported");

  string preferences=Path.Combine(dir,"resize-ui.xml");
  using var owner=new MainForm(preferences);
  owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
  Call(owner,"Apply",original);
  int step=0;Exception error=null;Form wizard=null;
  var clock=System.Diagnostics.Stopwatch.StartNew();
  using(var timer=new Timer{Interval=75}){
   timer.Tick+=delegate{
    try{
     if(clock.ElapsedMilliseconds>25000)throw new Exception("Resize wizard verification timed out.");
     if(step<=2){
      foreach(Form window in Application.OpenForms)
       if(window.Controls.Find("resizeWidth",true).Length>0){wizard=window;break;}
      if(wizard==null)return;
      Control Find(string name)=>wizard.Controls.Find(name,true)[0];
      var save=(Button)Find("resizeSave");
      var elements=(ListBox)Find("resizeElements");
      if(step==0){
       ((NumericUpDown)Find("resizeWidth")).Value=70;
       ((NumericUpDown)Find("resizeHeight")).Value=45;
       elements.SelectedIndex=2;
       ((ComboBox)Find("resizeAnchorX")).SelectedIndex=2;
       ((ComboBox)Find("resizeAnchorY")).SelectedIndex=2;
       elements.SelectedIndex=0;
       ((ComboBox)Find("resizeAnchorX")).SelectedIndex=3;
       step=1;
      }else if(step==1&&save.Enabled){
       var problems=(ListBox)Find("resizeIssues");
       int problem=-1;
       for(int i=0;i<problems.Items.Count;i++)
        if(problems.Items[i] is ResizeProblem item&&item.Language=="EN"&&item.Issue.Element==0){problem=i;break;}
       Check(problem>=0,"wizard lists overflow for the English translation");
       problems.SelectedIndex=problem;
       Check(((ComboBox)Find("resizeLanguage")).SelectedItem as string=="EN"&&
        (int)Find("resizeAfter").GetType().GetField("FocusedElement").GetValue(Find("resizeAfter"))==0,"selecting a problem highlights its element and chooses its language");
       step=2;
      }else if(step==2&&save.Enabled){
       Check(Find("resizeBefore").GetType().GetField("Scene").GetValue(Find("resizeBefore")) is VectorScene&&
        Find("resizeAfter").GetType().GetField("Scene").GetValue(Find("resizeAfter")) is VectorScene,"before and after previews are rendered");
       wizard.Size=wizard.MinimumSize;wizard.PerformLayout();
       bool VisibleInParents(Control control){
        var area=control.RectangleToScreen(control.ClientRectangle);
        for(var parent=control.Parent;parent!=null;parent=parent.Parent)
         if(!parent.RectangleToScreen(parent.ClientRectangle).Contains(area))return false;
        return true;
       }
       Check(Find("resizeBefore").Width>=150&&Find("resizeAfter").Height>=100&&
        VisibleInParents(Find("resizeBefore"))&&VisibleInParents(Find("resizeAfter"))&&
        VisibleInParents(Find("resizeAnchorX"))&&VisibleInParents(Find("resizeAnchorY"))&&VisibleInParents(save),
        "both previews, anchor controls and saving remain visible at the minimum size");
       Snapshot(wizard,Path.Combine(dir,"resize-wizard.png"));
       step=3;save.PerformClick();
      }
     }else if(step==3){
      foreach(Form window in Application.OpenForms){
       var fields=window.Controls.Find("libraryTemplateName",true);
       if(fields.Length==0)continue;
       ((TextBox)fields[0]).Text="Resized label";
       step=4;timer.Stop();window.AcceptButton.PerformClick();break;
      }
     }
    }catch(Exception ex){error=ex;timer.Stop();if(wizard!=null)wizard.DialogResult=DialogResult.Cancel;}
   };
   timer.Start();Call(owner,"ShowResizeLabel");
  }
  if(error!=null)throw error;
  var saved=Engine.Load(Path.Combine(preferences+".workspace","Templates","Resized label.ean.xml"));
  Check(step==4&&saved.Width==70&&saved.Height==45&&saved.BarX==24&&saved.BarY==10&&saved.Texts[0].Width==55&&saved.Languages.Selected=="SK"&&
   ((LabelSettings)Call(owner,"Read")).Width==100,"wizard saves the resized copy to the library and leaves the editor on the original");
  Call(owner,"Apply",LabelSettings.Default());owner.Close();
 }
 static void ReusableBlocks(string dir){
  var source=LabelSettings.Default();source.HideBarcode=true;source.Texts.Clear();
  source.Texts.Add(new TextItem{X=13,Y=9,Width=16,Height=7,Text="Lead"});
  source.Texts.Add(new TextItem{X=33,Y=9,Width=18,Height=7,Text="Base"});
  source.Languages=new LabelLanguages{Codes=new(){ "SK","EN" },Selected="SK"};
  source.Variables.Add(new VariableField{Name="lot",Type="Fixed",FixedValue="012"});
  Elements.Meta(source,1).Translations=new(){new LanguageTranslation{Language="SK",Text="Šarža {lot}"},new LanguageTranslation{Language="EN",Text="Lot {lot}"}};
  foreach(int index in new[]{0,1}){
   var meta=Elements.Meta(source,index);meta.Group="source";meta.GroupVisibility=new VisibilityRule{Field=0,Mode=VisibilityMode.Filled};
  }
  bool rejected=false;
  try{LabelBlock.Capture(source,new[]{1},"Partial");}catch(InvalidDataException){rejected=true;}
  Check(rejected,"blocks require field dependencies in the selection");
  var block=LabelBlock.Capture(source,new[]{0,1},"Lot label");
  string path=Path.Combine(dir,"lot-block.xml");block.Save(path);
  block=LabelBlock.Load(path);
  Check(Math.Abs(block.Width-38)<.01&&Math.Abs(block.Height-7)<.01,"saved block uses selection bounds");
  var destination=LabelSettings.Default();destination.HideBarcode=true;
  var first=block.Insert(destination);
  var second=block.Insert(destination);
  Check(first.Count==2&&second.Count==2&&first[0]!=second[0],"block inserts independent instances");
  var a=Elements.Meta(destination,first[1]);var b=Elements.Meta(destination,second[1]);
  Check(a.Group!=b.Group&&a.GroupVisibility.Field==first[0]&&b.GroupVisibility.Field==second[0],"block groups and their field references are local to each copy");
  Check(destination.Languages.Selected=="SK"&&destination.Variables.Exists(v=>v.Name=="lot"),"block brings language and variable defaults");
  var english=QuickPrint.Prepare(destination,null,new DateTime(2026,11,26),"EN");
  Check(english.CanPrint&&english.Label.Texts[first[1]].Text=="Lot 012","inserted block resolves its stored translation and variable");
  a.Translations[1].Text="Edited";
  destination.Texts[first[0]].Text="";
  var preview=QuickPrint.PrepareEditor(destination,new DateTime(2026,11,26));
  Check(b.Translations[1].Text=="Lot {lot}"&&block.Artwork.Metadata.Exists(m=>m.Translations?.Exists(t=>t.Text=="Lot {lot}")==true)&&
   preview.Hidden.Contains(first[1])&&!preview.Hidden.Contains(second[1]),"changes and visibility in one insert leave the library and second copy intact");
  var customized=LabelSettings.Default();customized.HideBarcode=true;
  customized.Languages=new LabelLanguages{Codes=new(){"EN","CZ"},Selected="EN",Fallback="CZ"};
  customized.Variables.Add(new VariableField{Name="lot",Type="Fixed",FixedValue="999"});
  var customizedIndices=block.Insert(customized);
  Check(customized.Languages.Selected=="EN"&&customized.Languages.Fallback=="CZ"&&customized.Languages.Codes.Contains("SK")&&
   QuickPrint.Prepare(customized,null,new DateTime(2026,11,26)).Label.Texts[customizedIndices[1]].Text=="Lot 999",
   "insert reuses existing variable definitions and preserves destination language choices");
  var mapped=LabelSettings.Default();
  var assetPath=Path.Combine(dir,"reusable-logo.svg");
  File.WriteAllText(assetPath,"<svg xmlns='http://www.w3.org/2000/svg' width='30' height='10'><rect width='30' height='10' fill='black'/></svg>");
  var logo=Assets.Import(assetPath);
  mapped.Symbols.Add(new SymbolItem{X=5,Y=37,Width=20,Height=7,ImageMapping=new ImageMapping{Field=-1,Variants=new(){
   new ImageVariant{Key=mapped.Ean,Image=logo}}}});
  var artwork=LabelBlock.Capture(mapped,new[]{-1,-3},"Code and logo");
  var placed=LabelSettings.Default();
  var positions=artwork.Insert(placed);
  Check(placed.Symbols[-3-positions[0]].Code?.Ean==mapped.Ean&&placed.Symbols[-3-positions[1]].ImageMapping.Field==positions[0]&&
   QuickPrint.PrepareEditor(placed,new DateTime(2026,11,26)).Label.Symbols[-3-positions[1]].Image?.Data==logo.Data,
   "captured primary barcode and mapped image become editable symbols and retain the source link");
  string preferences=Path.Combine(dir,"blocks-ui.xml");
  using var owner=new MainForm(preferences);
  owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
  Call(owner,"Apply",source);
  F<PreviewPanel>(owner,"preview").SelectMany(new[]{0,1},true);
  void Open(string command,Action<Form> act){
   Exception failure=null;
   bool opened=false;
   using var timer=new Timer{Interval=75};
   timer.Tick+=delegate{
    foreach(Form window in Application.OpenForms)if(window!=owner){
     timer.Stop();opened=true;
     try{act(window);window.AcceptButton.PerformClick();}
     catch(Exception ex){failure=ex;window.DialogResult=DialogResult.Cancel;}
     break;
    }
   };
   timer.Start();Call(owner,command);
   if(failure!=null)throw failure;
   Check(opened,command+" opens the expected dialog");
  }
  Open("SaveSelectionAsBlock",dialog=>((TextBox)dialog.Controls.Find("blockName",true)[0]).Text="Reusable lot");
  var savedPath=Path.Combine(preferences+".workspace","Blocks","Reusable lot.block.xml");
  Check(File.Exists(savedPath),"selection saves to the offline workspace library");
  Call(owner,"Apply",LabelSettings.Default());
  Open("ShowBlockLibrary",dialog=>{
   var list=(ListBox)dialog.Controls.Find("blockList",true)[0];
   var image=((PictureBox)dialog.Controls.Find("blockPreview",true)[0]).Image;
   var insert=(Button)dialog.Controls.Find("insertBlock",true)[0];
   Check(list.Items.Count==1&&image!=null&&image.Width>image.Height&&insert.Enabled,"block library lists saved block with usable preview");
  });
  var result=(LabelSettings)Call(owner,"Read");
  Check(result.Texts.Count==3&&result.Metadata.Exists(meta=>meta.Translations?.Count==2),"library command inserts editable translated artwork");
  Call(owner,"Apply",LabelSettings.Default());owner.Close();
 }
 static LabelSettings ConditionalTemplate(){
  var label=LabelSettings.Default();
  label.Texts[0].Text="   ";
  label.Texts.Add(new TextItem{X=3,Y=32,Width=30,Height=8,Text="Visible"});
  label.Symbols.Add(new SymbolItem{X=36,Y=24,Width=18,Height=18,Matrix=new MatrixContent{Data=""}});
  label.QuickPrintFields.AddRange(new[]{0,-3});
  var xml=ProjectXml.Encode(label).Replace("<Metadata />",
   "<Metadata><ElementMeta><Index>0</Index><Group>optional</Group><Layer>0</Layer><GroupVisibility><Field>0</Field><Mode>Filled</Mode></GroupVisibility></ElementMeta>"+
   "<ElementMeta><Index>-3</Index><Group>optional</Group><Layer>103</Layer><GroupVisibility><Field>0</Field><Mode>Filled</Mode></GroupVisibility></ElementMeta></Metadata>");
  return ProjectXml.Decode<LabelSettings>(xml);
 }
 static void TextLanguages(string dir){
  var label=LabelSettings.Default();label.HideBarcode=true;
  label.Texts[0].AutoWidth=false;label.Texts[0].Height=14;
  var xml=ProjectXml.Encode(label).Replace("<Metadata />","<Metadata><ElementMeta><Index>0</Index><Translations>"+
   "<LanguageTranslation><Language>SK</Language><Text>Príklad etikety</Text></LanguageTranslation>"+
   "<LanguageTranslation><Language>CZ</Language><Text>Příliš žluťoučký kůň</Text></LanguageTranslation></Translations></ElementMeta></Metadata>")
   .Replace("</LabelSettings>","<Languages><Codes><string>SK</string><string>CZ</string></Codes><Selected>SK</Selected></Languages></LabelSettings>");
  var template=ProjectXml.Decode<LabelSettings>(xml);
  var date=new DateTime(2026,11,26);
  var batch=BatchData.Record(template,new[]{"CZ"},new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Target="language",Column=0}},-1,0,2,date);
  Check(batch.Error==null&&batch.Label.Texts[0].Text=="Příliš žluťoučký kůň","mapped language chooses the saved Czech text");
  template.Languages.Codes.Add("EN");
  template.Texts[0].Width=30;template.Texts[0].Size=14;template.Texts[0].MinimumFitSize=5;
  template.Variables.Add(new VariableField{Name="lot",Type="Fixed",FixedValue="ABC"});
  template.Metadata[0].Translations[1].Text="Minimální trvanlivost do {lot}";
  template.QuickPrintFields.Add(0);
  string project=Path.Combine(dir,"translations.ean.xml");
  Engine.Save(project,template);
  template=Engine.Load(project);
  Check(template.Languages.Codes.Count==3&&template.Metadata[0].Translations.Count==2,"languages and translations survive project round trip");
  var quick=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0,"override"}},date," cz ");
  var field=quick.Label.Texts[0];
  Check(quick.CanPrint&&field.Text=="Minimální trvanlivost do ABC"&&quick.Label.Languages==null,"quick print uses normalized language then expands variables");
  double fitted=FixedTextLayout.Resolve(field,out bool fits);
  Check(field.Width==template.Texts[0].Width&&field.Height==template.Texts[0].Height&&
   field.X==template.Texts[0].X&&fits&&fitted>=field.MinimumFitSize,"long translation keeps the fixed field and fits");

  var source=BatchData.CsvText("Jazyk;Dáta\nSK;a\ncz;b\nEN;c",';');
  var bindings=new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Target=LabelLanguages.BatchTarget,Column=0}};
  var preset=BatchPresets.Capture("Languages",new BatchSetup{Header=true,Separator=';',Bindings=bindings},source,false);
  var reordered=BatchData.CsvText("Dáta;Jazyk\na;CZ",';');
  Check(BatchPresets.Resolve(preset,reordered,2).Columns[LabelLanguages.BatchTarget]==1,"preset restores language by column heading");
  var records=BatchProcessing.Prepare(template,source,true,bindings,-1,0,date);
  Check(records[0].Label.Texts[0].Text=="Príklad etikety"&&records[1].Label.Texts[0].Text==field.Text,"CSV selects each row's language");
  Check(records[2].Row==4&&records[2].Label==null&&records[2].Issues.Exists(issue=>issue.Blocking&&issue.Column==0&&issue.Message.Contains("EN")),"missing translation retains original CSV row and language column");
  Check(!QuickPrint.Prepare(template,null,date,"EN").CanPrint,"missing translation blocks quick print");
  template.Languages.Fallback="SK";
  Check(QuickPrint.Prepare(template,null,date,"EN").Label.Texts[0].Text=="Príklad etikety","explicit fallback fills a missing translation");
  template.Languages.Fallback=null;
  var optional=ProjectXml.Clone(template);
  optional.Texts.Add(new TextItem{Text="Keep",X=4,Y=28});
  Elements.Meta(optional,0).Visibility=new VisibilityRule{Field=1,Mode=VisibilityMode.EqualsValue,Value="show"};
  Check(QuickPrint.Prepare(optional,null,date,"EN").CanPrint,"a hidden field does not require a translation");

  string xlsx=Path.Combine(dir,"languages.xlsx");
  using(var zip=System.IO.Compression.ZipFile.Open(xlsx,System.IO.Compression.ZipArchiveMode.Create)){
   void Entry(string name,string body){using(var writer=new StreamWriter(zip.CreateEntry(name).Open()))writer.Write(body);}
   Entry("xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Labels' r:id='r1'/></sheets></workbook>");
   Entry("xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/></Relationships>");
   Entry("xl/worksheets/sheet1.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Jazyk</t></is></c></row>"+
    "<row r='2'><c r='A2' t='inlineStr'><is><t>CZ</t></is></c></row></sheetData></worksheet>");
  }
  var workbook=BatchData.Xlsx(xlsx)[0];
  Check(BatchProcessing.Prepare(template,workbook,true,bindings,-1,0,date)[0].Label.Texts[0].Text==field.Text,"XLSX language mapping renders the same text");
  var both=OutputPlan.Create(records.GetRange(0,2),template,date);
  Check(!PreflightReport.Inspect(both).Blocking,"both languages pass output inspection");
  var single=JobPreparation.Resolve(template,date);
  Check(single.Texts[0].Text=="Príklad etikety"&&quick.CreatePlan(2).Placements.Count==2,
   "single print uses project language and quick print keeps the requested quantity");
  string export=Path.Combine(dir,"language-export");
  Engine.Pdf(export+".pdf",quick.Label);Engine.Docx(export+".docx",quick.Label);
  Engine.Svg(export+".svg",quick.Label);Engine.Zpl(export+".zpl",quick.Label);
  Engine.Zpl(export+"-selected.zpl",template);Engine.Zpl(export+"-resolved.zpl",single);
  Check(File.ReadAllText(export+"-selected.zpl")==File.ReadAllText(export+"-resolved.zpl"),"direct ZPL export uses the selected project language");
  JobOutput.Export(export+"-batch.zpl","zpl",both,template.Dpi);
  var pages=File.ReadAllText(export+"-batch.zpl").Split(new[]{"^XA"},StringSplitOptions.RemoveEmptyEntries);
  Check(pages.Length==2&&pages[0]!=pages[1]&&pages[0].Contains("^GFA")&&pages[1].Contains("^GFA"),"ZPL raster preserves per-label translated text");
  using(var owner=new MainForm(Path.Combine(dir,"label-language-ui.xml"))){
   owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
   Call(owner,"Apply",template);Application.DoEvents();
   var preview=F<PreviewPanel>(owner,"preview");
   string before=VectorArt.PathData(preview.Scene.Shapes[0].Path,true);
   void EditLanguageDialog(string command,Action<Form> edit){
    Exception error=null;
    bool opened=false;
    using(var timer=new Timer{Interval=75}){
     timer.Tick+=delegate{
      foreach(Form window in Application.OpenForms)if(window!=owner){
       timer.Stop();opened=true;
       try{edit(window);window.AcceptButton.PerformClick();}
       catch(Exception ex){error=ex;window.DialogResult=DialogResult.Cancel;}
       break;
      }
     };
     timer.Start();Call(owner,command);
    }
    if(error!=null)throw error;
    Check(opened,command+" opens the language editor");
   }
   EditLanguageDialog("EditLabelLanguages",dialog=>
    ((ComboBox)dialog.Controls.Find("selectedLabelLanguage",true)[0]).SelectedIndex=1);
   Check(VectorArt.PathData(preview.Scene.Shapes[0].Path,true)!=before,"editor repaints after selecting another label language");
   preview.SelectElement(0);
   EditLanguageDialog("EditTextTranslations",dialog=>
    ((TextBox)dialog.Controls.Find("textTranslation-CZ",true)[0]).Text="Změněný text");
   var edited=(LabelSettings)Call(owner,"Read");
   Check(edited.Languages.Selected=="CZ"&&edited.Metadata[0].Translations[1].Text=="Změněný text","language dialog saves changes to the template");
   int step=0;Exception failure=null;Image first=null;
   var clock=System.Diagnostics.Stopwatch.StartNew();
   using(var timer=new Timer{Interval=75}){
    timer.Tick+=delegate{
     foreach(Form window in Application.OpenForms)if(window!=owner){
      try{
       var picker=(ComboBox)window.Controls.Find("quickPrintLabelLanguage",true)[0];
       var picture=(PictureBox)window.Controls.Find("quickPrintPreview",true)[0];
       var proceed=(Button)window.Controls.Find("quickPrintContinue",true)[0];
       if(clock.ElapsedMilliseconds>18000)throw new Exception("Quick print language preview timeout");
       if(step==0&&picture.Image!=null&&proceed.Enabled){first=picture.Image;step=1;picker.SelectedIndex=1;}
       else if(step==1&&picture.Image!=null&&picture.Image!=first&&proceed.Enabled){
        step=2;timer.Stop();window.DialogResult=DialogResult.Cancel;
       }
      }catch(Exception ex){failure=ex;timer.Stop();window.DialogResult=DialogResult.Cancel;}
      break;
     }
    };
    timer.Start();Call(owner,"ShowQuickPrint",owner,template);
   }
   if(failure!=null)throw failure;
   Check(step==2,"quick print language chooser refreshes the live preview");
   Call(owner,"Apply",template);owner.Close();
  }
 }
 static void MappedArtwork(string dir){
  var label=LabelSettings.Default();label.HideBarcode=true;
  label.Texts[0].Text="North";label.QuickPrintFields.Add(0);
  string svgPath=Path.Combine(dir,"mapped-logo-source.svg");
  File.WriteAllText(svgPath,"<svg xmlns='http://www.w3.org/2000/svg' width='96' height='32'><rect width='96' height='32' fill='#223344'/></svg>");
  var embedded=Assets.Import(svgPath);
  var image=ProjectXml.Encode(embedded);
  image=image.Substring(image.IndexOf("<ImageContent",StringComparison.Ordinal)).Replace("ImageContent","Image");
  var xml=ProjectXml.Encode(label).Replace("<Symbols />","<Symbols><SymbolItem><X>21</X><Y>17</Y><Width>24</Width><Height>18</Height>"+
   "<ImageMapping><Field>0</Field><Missing>Error</Missing><Variants><ImageVariant><Key>North</Key><Name>logo.svg</Name>"+image+
   "</ImageVariant></Variants></ImageMapping></SymbolItem></Symbols>");
  var template=ProjectXml.Decode<LabelSettings>(xml);
  var date=new DateTime(2026,11,26);
  var current=QuickPrint.Prepare(template,null,date);
  Check(current.CanPrint&&current.Label.Symbols[0].Image?.Data==embedded.Data,"quick print selects the embedded image");
  string pngPath=Path.Combine(dir,"mapped-logo-source.png");
  using(var bitmap=new Bitmap(32,96))using(var g=Graphics.FromImage(bitmap)){
   g.Clear(Color.Black);bitmap.Save(pngPath);
  }
  var tall=Assets.Import(pngPath);
  template.Symbols[0].ImageMapping.Variants.Add(new ImageVariant{Key="South",Name="tall.png",Image=tall});
  string project=Path.Combine(dir,"mapped-images.ean.xml");
  Engine.Save(project,template);
  File.Delete(svgPath);File.Delete(pngPath);
  template=Engine.Load(project);
  Check(template.Symbols[0].ImageMapping.Variants.Count==2,"both images round-trip without their source files");
  var south=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0," South "}},date);
  Check(south.CanPrint&&south.Label.Symbols[0].Image.Data==tall.Data&&south.Label.Symbols[0].ImageMapping==null,"quick print uses filled field and freezes the chosen image");
  using(var wideScene=VectorArt.Build(template))
  using(var tallScene=VectorArt.Build(south.Label)){
   var wideShape=wideScene.Shapes.Find(shape=>shape.Element==-3);
   var tallShape=tallScene.Shapes.Find(shape=>shape.Element==-3);
   Check(wideShape.Clip==new RectangleF(21,22,24,8)&&tallShape.ImageBounds==new RectangleF(30,17,6,18),"SVG and PNG are centered proportionally inside the fixed frame");
  }
  var binding=new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Target="text:0",Column=0}};
  var northRow=BatchData.Record(template,new[]{"North"},binding,-1,0,6,date);
  var southRow=BatchData.Record(template,new[]{"South"},binding,-1,1,7,date);
  var missing=BatchData.Record(template,new[]{"south"},binding,-1,2,8,date);
  Check(northRow.Label?.Symbols[0].Image.Data==embedded.Data&&southRow.Label?.Symbols[0].Image.Data==tall.Data,"batch selects independently per row");
  Check(missing.Error!=null&&missing.Row==8&&missing.Issues.Exists(issue=>issue.Column==0&&issue.Blocking),"unmapped value reports its original row and column");
  var optional=ProjectXml.Clone(template);
  optional.Symbols[0].ImageMapping.Missing=MissingImage.Blank;
  Check(BatchData.Record(optional,new[]{"other"},binding,-1,2,8,date).Label.Symbols.Count==0,"optional unknown value leaves the frame blank");
  optional.Symbols[0].ImageMapping.Missing=MissingImage.Fallback;
  optional.Symbols[0].ImageMapping.FallbackKey="South";
  Check(BatchData.Record(optional,new[]{""},binding,-1,2,8,date).Label.Symbols[0].Image.Data==tall.Data,"empty value uses embedded fallback");
  Elements.Remove(optional,new System.Collections.Generic.List<int>{0});
  Check(!QuickPrint.Prepare(optional,null,date).CanPrint,"a removed source is reported even when fallback is selected");
  var byVariable=ProjectXml.Clone(template);
  byVariable.Variables.Add(new VariableField{Name="brand",Type="Fixed",FixedValue="South"});
  byVariable.Symbols[0].ImageMapping.Variable="brand";
  Check(JobPreparation.Resolve(byVariable,date).Symbols[0].Image.Data==tall.Data,"generated data can select a variant");
  var hidden=ProjectXml.Clone(template);
  Elements.Meta(hidden,0).Visibility=new VisibilityRule{Field=0,Mode=VisibilityMode.EqualsValue,Value="never"};
  Check(QuickPrint.Prepare(hidden,null,date).Label.Symbols[0].Image.Data==embedded.Data,"source data survives conditional hiding of its field");
  Elements.Meta(hidden,-3).Visibility=new VisibilityRule{Field=0,Mode=VisibilityMode.EqualsValue,Value="never"};
  Check(BatchData.Record(hidden,new[]{"unknown"},binding,-1,3,9,date).Error==null,"hidden artwork does not require a mapping match");
  var artworkOnly=ProjectXml.Clone(template);
  Elements.Meta(artworkOnly,0).Visibility=new VisibilityRule{Field=0,Mode=VisibilityMode.EqualsValue,Value="never"};
  var batch=OutputPlan.Create(new System.Collections.Generic.List<BatchRecord>{
   BatchData.Record(artworkOnly,new[]{"North"},binding,-1,0,6,date),
   BatchData.Record(artworkOnly,new[]{"South"},binding,-1,1,7,date)},template,date);
  Check(!PreflightReport.Inspect(batch).Blocking,"preflight accepts both resolved images");
  string output=Path.Combine(dir,"mapped-output");
  Engine.Pdf(output+".pdf",template);Engine.Docx(output+".docx",template);Engine.Svg(output+".svg",template);Engine.Zpl(output+".zpl",template);
  Check(File.ReadAllText(output+".svg").Contains("#223344"),"SVG export includes the selected vector image");
  using(var word=System.IO.Compression.ZipFile.OpenRead(output+".docx"))
   Check(System.Linq.Enumerable.Any(word.Entries,entry=>entry.FullName.StartsWith("word/media/",StringComparison.Ordinal)),"Word output contains embedded artwork");
  JobOutput.Export(output+"-batch.zpl","zpl",batch,template.Dpi);
  var pages=File.ReadAllText(output+"-batch.zpl").Split(new[]{"^XA"},StringSplitOptions.RemoveEmptyEntries);
  Check(pages.Length==2&&pages[0]!=pages[1]&&pages[0].Contains("^GFA")&&pages[1].Contains("^GFA"),"ZPL exports distinct raster artwork per label");
  var geometry=new PrinterPageGeometry(new Rectangle(0,0,237,178),new RectangleF(0,0,237,178),0,0,false);
  using(var document=new LabelPrintDocument(template,batch))using(var stream=new MemoryStream()){
   ZebraPrint.Render(document,geometry,new ZebraSettings{Enabled=true,Dpi=203.2},1,stream);
   var printed=System.Text.Encoding.ASCII.GetString(stream.ToArray()).Split(new[]{"^XA"},StringSplitOptions.RemoveEmptyEntries);
   Check(printed.Length==2&&printed[0]!=printed[1],"direct Zebra path prints distinct artwork per record");
  }
  using(var owner=new MainForm(Path.Combine(dir,"mapped-images-ui.xml"))){
   owner.StartPosition=FormStartPosition.Manual;owner.Location=new Point(-20000,-20000);owner.Show();
   Call(owner,"Apply",template);Application.DoEvents();
   var preview=F<PreviewPanel>(owner,"preview");
   Check(preview.Scene.Shapes.Exists(shape=>shape.Element==-3&&shape.Clip==new RectangleF(21,22,24,8)),"editor preview uses the mapped SVG");
   bool opened=false;Exception failure=null;
   using(var timer=new Timer{Interval=75}){
    timer.Tick+=delegate{
     foreach(Form window in Application.OpenForms)if(window!=owner){
      timer.Stop();
      try{
       var options=(DataGridView)window.Controls.Find("imageVariants",true)[0];
       var source=(ComboBox)window.Controls.Find("imageDataSource",true)[0];
       opened=options.Rows.Count==2&&source.SelectedIndex>=0;
       Snapshot(window,Path.Combine(dir,"mapped-image-dialog.png"));
       ((ComboBox)window.Controls.Find("imageMissingPolicy",true)[0]).SelectedIndex=(int)MissingImage.Fallback;
       ((ComboBox)window.Controls.Find("imageFallback",true)[0]).SelectedIndex=1;
       window.DialogResult=DialogResult.OK;
      }catch(Exception ex){failure=ex;window.DialogResult=DialogResult.Cancel;}
      break;
     }
    };
    timer.Start();Call(owner,"EditMappedImage",-3);
   }
   if(failure!=null)throw failure;
   var edited=(LabelSettings)Call(owner,"Read");
   Check(opened&&edited.Symbols[0].ImageMapping.FallbackKey=="South","mapping dialog applies the selected offline fallback");
   Call(owner,"Apply",template);owner.Close();
  }
 }
 static void ConditionalOutput(string dir){
  var template=ConditionalTemplate();var instant=new DateTime(2026,11,26);
  var quick=QuickPrint.Prepare(template,null,instant);
  Check(quick.CanPrint&&quick.Label.Symbols.Count==0&&quick.Label.Texts.Count==1,"empty field hides the group before validation");
  Check(quick.Label.Texts[0].Y==32&&template.Texts.Count==2&&template.Symbols.Count==1,"fixed geometry and template unchanged");
  var shown=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0,"Lot"},{-3,"https://example.com"}},instant);
  Check(shown.CanPrint&&shown.Label.Symbols.Count==1,"quick print restores visible group from entered data");
  var invalid=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0,"Lot"}},instant);
  Check(!invalid.CanPrint,"visible invalid QR still blocks quick print");
  var mapped=new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Target="text:0",Column=0},new ColumnBinding{Target="symbol:0",Column=1}};
  var absent=BatchData.Record(template,new[]{"",""},mapped,-1,0,2,instant);
  var present=BatchData.Record(template,new[]{"Lot","https://example.com"},mapped,-1,1,3,instant);
  Check(absent.Error==null&&absent.Label.Symbols.Count==0&&present.Error==null&&present.Label.Symbols.Count==1,"batch evaluates each mapped record");
  var ignored=BatchData.Record(template,new[]{"","!ERROR: invalid optional content"},mapped,-1,2,4,instant);
  var badSource=BatchData.Record(template,new[]{"!ERROR: invalid source",""},mapped,-1,3,5,instant);
  Check(ignored.Error==null&&badSource.Error!=null,"hidden content can be ignored but a faulty rule source blocks output");
  var exact=ProjectXml.Clone(template);
  exact.Variables.Add(new VariableField{Name="segment",Type="Fixed",FixedValue="  promo  "});
  foreach(var meta in exact.Metadata)meta.GroupVisibility=new VisibilityRule{Variable="segment",Mode=VisibilityMode.EqualsValue,Value="promo"};
  exact.Symbols[0].Matrix.Data="https://example.com";
  Check(JobPreparation.Resolve(exact,instant).Symbols.Count==1,"equals trims whitespace from resolved variable");
  exact.Variables[0].FixedValue="Promo";
  Check(JobPreparation.Resolve(exact,instant).Symbols.Count==0,"equals is deterministic and case-sensitive");
  string project=Path.Combine(dir,"conditional.ean.xml");
  Engine.Save(project,template);
  Check(ConditionalVisibility.HasRules(Engine.Load(project)),"project round trip preserves group rule");
  var removed=Engine.Load(project);
  Elements.Remove(removed,new System.Collections.Generic.List<int>{0});
  Check(QuickPrint.Prepare(removed,null,instant).Issues.Exists(i=>i.Blocking&&i.Message==Loc.T("Zdroj pravidla zobrazenia už nie je dostupný.")),"removed data source is actionable");
  var plan=quick.CreatePlan(1);
  Check(!PreflightReport.Inspect(plan).Blocking,"preflight inspects output only");
  string file=Path.Combine(dir,"conditional");
  Engine.Pdf(file+".pdf",template);Engine.Docx(file+".docx",template);
  Engine.Svg(file+".svg",template);Engine.Zpl(file+".zpl",template);
  var expected=ProjectXml.Clone(quick.Label);
  Check(File.ReadAllText(file+".svg")==VectorArt.Svg(expected),"SVG uses the same visible geometry");
  Engine.Zpl(file+"-visible.zpl",expected);
  Check(File.ReadAllText(file+".zpl").Split(new[]{"^XZ"},StringSplitOptions.None)[0]==
   File.ReadAllText(file+"-visible.zpl").Split(new[]{"^XZ"},StringSplitOptions.None)[0],"Zebra raster contains only visible, fixed-position artwork");
  var batchPlan=OutputPlan.Create(new System.Collections.Generic.List<BatchRecord>{absent,present},template,instant);
  JobOutput.Export(file+"-batch.zpl","zpl",batchPlan,template.Dpi);
  var pages=File.ReadAllText(file+"-batch.zpl").Split(new[]{"^XA"},StringSplitOptions.RemoveEmptyEntries);
  Check(pages.Length==2&&pages[0]!=pages[1],"batch Zebra output resolves visibility per label");
  using(var form=new MainForm(Path.Combine(dir,"conditional-ui.xml"))){
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);
   form.Show();Call(form,"Apply",template);Application.DoEvents();
   var preview=F<PreviewPanel>(form,"preview");
   Check(preview.HiddenElements.SetEquals(new[]{0,-3})&&preview.Scene.Shapes.Exists(s=>s.Element==1)&&!preview.Scene.Shapes.Exists(s=>s.Element==0||s.Element==-3),"editor preserves identities for visible and hidden artwork");
   var list=F<ListBox>(form,"elementList");
   Check(list.Items.Count==4&&Convert.ToString(list.Items[0]).Contains(Loc.T("Skryté pravidlom")),"layers retain conditional elements");
   var print=All<ModernButton>(form).Find(b=>b.Name=="printCommand");
   var glyph=typeof(ModernButton).GetProperty("Glyph",BindingFlags.Instance|BindingFlags.NonPublic);
   Check(print!=null&&(HeaderIcon)glyph.GetValue(print)==HeaderIcon.Print&&!string.IsNullOrEmpty(print.AccessibleName),"toolbar restores print icon and keeps accessible name");
   preview.SelectElement(0);
   bool visited=false;
   using(var timer=new Timer{Interval=75}){
    timer.Tick+=delegate {
     foreach(Form dialog in Application.OpenForms)if(dialog!=form){
      timer.Stop();
      var mode=(ComboBox)dialog.Controls.Find("visibilityMode",true)[0];
      var source=(ComboBox)dialog.Controls.Find("visibilitySource",true)[0];
      visited=mode.SelectedIndex==1&&source.SelectedIndex>=0&&source.AccessibleName==Loc.T("Zdroj údajov");
      dialog.DialogResult=DialogResult.Cancel;
      break;
     }
    };
    timer.Start();Call(form,"EditVisibility");
   }
   Check(visited,"visibility editor opens for selected group with its current rule");
   Snapshot(form,Path.Combine(dir,"conditional-toolbar.png"));
   string copied=(string)Call(form,"CopyElementData");
   Call(form,"PasteElementData",copied);
   Check(preview.HiddenElements.SetEquals(new[]{0,-3,2,-4}),"pasted group keeps its rules and remaps its data source");
   Call(form,"Apply",template);
   form.Close();
  }
 }
 static void Gs1Theme(string dir){using(var owner=new MainForm(Path.Combine(dir,"gs1-theme.xml")))using(var dialog=new Gs1EditorDialog(new System.Collections.Generic.List<AiValue>{new AiValue{AI="01",Data="09506000134352"}})){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();Application.DoEvents();dialog.UseTheme(c=>Call(owner,"ApplyColors",c));var before=All<TextBox>(dialog).Find(c=>c.Name=="gs1-data-01").BackColor;All<Button>(dialog).Find(b=>b.Name=="gs1-quick-10").PerformClick();Check(All<TextBox>(dialog).Find(c=>c.Name=="gs1-data-01").BackColor==before,"GS1 rebuilding preserves current theme");}}
 static void PanelPersistence(string dir){string path=Path.Combine(dir,"panel-layout.xml");using(var form=new MainForm(path)){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();form.Hide();form.Show();var body=F<TableLayoutPanel>(form,"studioBody");body.ColumnStyles[0].Width=280;body.ColumnStyles[2].Width=340;Call(form,"SetStudioPanel",0,false);Call(form,"SaveStudioLayout");form.Close();}using(var form=new MainForm(path)){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var body=F<TableLayoutPanel>(form,"studioBody");Check(body.ColumnStyles[0].Width==0&&!body.GetControlFromPosition(0,0).Visible,"hidden panel restored hidden");Call(form,"SetStudioPanel",0,true);Check(body.ColumnStyles[0].Width==280&&body.ColumnStyles[2].Width==340,"logical widths survive restart: "+body.ColumnStyles[0].Width+" / "+body.ColumnStyles[2].Width);form.Close();}File.WriteAllLines(path+".layout",new[]{"0","1","1","300"});using(var form=new MainForm(path)){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();var body=F<TableLayoutPanel>(form,"studioBody");Check(Math.Abs(body.ColumnStyles[0].Width-240*form.DeviceDpi/96f)<1&&Math.Abs(body.ColumnStyles[2].Width-300*form.DeviceDpi/96f)<1,"legacy four-line layout remains compatible");form.Close();}}
 static void GuidedGs1Locales(string dir){foreach(string lang in Loc.Codes)foreach(float scale in new[]{1f,1.5f,2f}){Loc.Language=lang;using(var dialog=new Gs1EditorDialog(new System.Collections.Generic.List<AiValue>{new AiValue{AI="01",Data="09506000134352"},new AiValue{AI="17",Data="271200"}})){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);var controls=All<Control>(dialog);var sizes=new System.Collections.Generic.List<float>();foreach(var c in controls)sizes.Add(c.Font.Size);for(int i=0;i<controls.Count;i++)controls[i].Font=UiFonts.Get(sizes[i]*scale,controls[i].Font.Style);dialog.Size=dialog.MinimumSize;dialog.Show();Application.DoEvents();Check(F<Panel>(dialog,"viewport").ClientSize.Height>=80,"GS1 field viewport remains usable "+lang+" @ "+scale);Check(((Button)dialog.AcceptButton).Enabled,"GS1 month-end date remains valid "+lang);var date=All<TextBox>(dialog).Find(c=>c.Name=="gs1-data-17");date.Text="270231";Check(!((Button)dialog.AcceptButton).Enabled,"GS1 invalid date blocked "+lang);date.Text="271200";var code=All<TextBox>(dialog).Find(c=>c.Name=="gs1-data-01");code.Text="09506000134353";Check(!((Button)dialog.AcceptButton).Enabled,"GS1 checksum error blocked "+lang);code.Text="09506000134352";Check(((Button)dialog.AcceptButton).Enabled,"GS1 corrected fields re-enable Apply "+lang);var search=All<TextBox>(dialog).Find(c=>c.Name=="gs1-search");search.Text="3103";var catalog=All<ComboBox>(dialog).Find(c=>c.Name=="gs1-catalog");Check(catalog.Items.Count==1&&((AiDefinition)catalog.SelectedItem).Code=="3103","GS1 catalogue search "+lang);foreach(Control action in new[]{(Control)dialog.AcceptButton,(Control)dialog.CancelButton}){var rectangle=dialog.RectangleToClient(action.RectangleToScreen(action.ClientRectangle));Check(dialog.ClientRectangle.Contains(rectangle),"GS1 dialog footer inside minimum viewport "+lang+" @ "+scale);}if(lang=="hu"&&scale==2)Snapshot(dialog,Path.Combine(dir,"guided-gs1-hu-200.png"));dialog.Close();}}Loc.Language="sk";}
 static void PdfWorkspace(string dir){using(var form=new MainForm(Path.Combine(dir,"pdf-workspace.xml"))){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();form.Hide();form.Show();Application.DoEvents();foreach(var b in All<HeaderActionButton>(form))Check(b.AccessibleName!=Loc.T("Vybrať kód")&&b.AccessibleName!=Loc.T("Tabuľka")&&b.AccessibleName!=Loc.T("Centrovať kód"),"PDF removes redundant toolbar actions");var body=F<TableLayoutPanel>(form,"studioBody");var menu=F<ContextMenuStrip>(form,"viewMenu");for(int col=0;col<=2;col+=2){var host=body.GetControlFromPosition(col,0);body.ColumnStyles[col].Width=col==0?280:340;Application.DoEvents();int width=host.Width;for(int i=0;i<4;i++){menu.Items[col==0?0:1].PerformClick();Application.DoEvents();Check(!host.Visible,"collapsed panel host is hidden");menu.Items[col==0?0:1].PerformClick();Application.DoEvents();Check(host.Visible&&host.Width==width,"panel width retained after repeated toggling");Check(host.DisplayRectangle.Width<=host.ClientSize.Width+1,"restored panel has no stale horizontal overflow");}}var preview=F<PreviewPanel>(form,"preview");preview.SelectElement(-1);var center=F<Button>(form,"centerBarcode");Check(center.Parent==F<TableLayoutPanel>(form,"codeInspector")&&center.Visible&&center.Enabled,"centering is contextual in barcode inspector");var locked=LabelSettings.Default();locked.Metadata.Add(new ElementMeta{Index=-1,Locked=true});Call(form,"Apply",locked);preview.SelectElement(-1);Check(!center.Enabled,"locked barcode cannot be centered");Call(form,"Apply",LabelSettings.Default());preview.SelectElement(0);Check(!center.Visible,"centering absent for text selection");Call(form,"BuildFeatureMenu");var features=F<ContextMenuStrip>(form,"featureMenu");Check(features.Items.Count==3,"feature commands grouped by purpose");foreach(ToolStripMenuItem group in features.Items){Check(group.Image!=null&&group.DropDownItems.Count>0,"group has icon and children");foreach(ToolStripItem item in group.DropDownItems){Check(item.Image!=null,"feature action has matching icon");Check(item.ForeColor==features.ForeColor&&group.DropDown.Renderer==features.Renderer,"submenu text and background share the active theme");}}Call(form,"ResetStudioPanels");preview.SelectElement(-1);Snapshot(form,Path.Combine(dir,"pdf-toolbar-panels.png"));features.Size=features.GetPreferredSize(Size.Empty);Snapshot(features,Path.Combine(dir,"pdf-feature-groups.png"));Call(form,"Apply",LabelSettings.Default());form.Close();}}
 static void GuidedGs1(string dir){var type=typeof(MainForm).Assembly.GetType("EanEtikety.Gs1EditorDialog");Check(type!=null,"guided GS1 editor exists");var input=new System.Collections.Generic.List<AiValue>{new AiValue{AI="01",Data="09506000134352"},new AiValue{AI="10",Data="LOT"}};using(var dialog=(Form)Activator.CreateInstance(type,new object[]{input})){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-20000,-20000);dialog.Show();Application.DoEvents();var data=All<TextBox>(dialog).Find(c=>c.Name=="gs1-data-10");Check(data!=null,"GS1 fields have named data inputs");data.Text="A B";Application.DoEvents();Check(!((Button)dialog.AcceptButton).Enabled,"invalid GS1 disables Apply");Check(All<Label>(dialog).Exists(l=>l.Name=="gs1-error-10"&&l.Text.Length>0),"invalid GS1 is explained at its field");data.Text="NEWLOT";Check(((Button)dialog.AcceptButton).Enabled,"valid GS1 enables Apply");All<Button>(dialog).Find(b=>b.Name=="gs1-quick-10").PerformClick();var values=(System.Collections.Generic.List<AiValue>)type.GetProperty("Values").GetValue(dialog,null);Check(values.Count==2,"duplicate shortcut does not duplicate AI");All<Button>(dialog).Find(b=>b.Name=="gs1-up-10").PerformClick();values=(System.Collections.Generic.List<AiValue>)type.GetProperty("Values").GetValue(dialog,null);Check(values[0].AI=="10"&&values[0].Data=="NEWLOT","guided fields retain order and edited data");Snapshot(dialog,Path.Combine(dir,"guided-gs1.png"));((Button)dialog.CancelButton).PerformClick();Check(input[1].Data=="LOT"&&input[0].AI=="01","GS1 cancel leaves original untouched");} }
 static void ComboRows(){using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var row=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(4),Font=UiFonts.Get(21)})using(var combo=new ModernComboBox{Width=140,DropDownStyle=ComboBoxStyle.DropDownList}){row.Controls.Add(combo);form.Controls.Add(row);form.Show();Application.DoEvents();Check(combo.Height>=combo.Font.Height+4,"large combo text has full control height");Check(combo.Bottom+combo.Margin.Bottom+row.Padding.Bottom<=row.ClientSize.Height,"flow row accommodates native combo height");row.Font=UiFonts.Get(10.5f);Application.DoEvents();int small=row.Height;row.Font=UiFonts.Get(21);Application.DoEvents();Check(row.Height>small,"combo row grows after font change");row.Font=UiFonts.Get(10.5f);Application.DoEvents();Check(row.Height==small,"combo row shrinks after font round trip");form.Close();}}
 static void SettingExplanations(){foreach(string lang in Loc.Codes){Loc.Language=lang;using(var panel=new PrintPanel(LabelSettings.Default())){foreach(string field in new[]{"offsetX","offsetY","scaleX","scaleY","ignoreDriverMargins","zebraDpi","zebraDarkness","zebraDarknessOverride"}){var c=F<Control>(panel,field);Check(!string.IsNullOrWhiteSpace(c.AccessibleDescription),"setting has accessible explanation "+field+" / "+lang);Check(All<Label>(panel).Exists(l=>l.Text==c.AccessibleDescription),"setting explanation is visible inline "+field);if(lang!="sk"){string translated=c.AccessibleDescription;Loc.Language="sk";string original=Loc.T(translated);Loc.Language=lang;Check(original!=translated,"setting explanation translated "+field+" / "+lang);}}}}Loc.Language="sk";}
 static void ComboCaptions(){using(var form=new Form{ClientSize=new Size(380,200),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var combo=new ModernComboBox{Width=160,DropDownStyle=ComboBoxStyle.DropDownList}){form.Controls.Add(combo);string text="A long printer or media profile name that must be readable";combo.Items.Add(text);combo.SelectedIndex=0;form.Show();Call(combo,"OnDropDown",EventArgs.Empty);int wanted=TextRenderer.MeasureText(text,combo.Font).Width+SystemInformation.VerticalScrollBarWidth+24;Check(combo.DropDownWidth>=Math.Min(Screen.FromControl(combo).WorkingArea.Width,wanted),"dropdown expands for complete item caption");Call(combo,"OnMouseEnter",EventArgs.Empty);combo.Items.Add("Changed media");combo.SelectedIndex=1;Check(F<ToolTip>(combo,"valueTip").GetToolTip(combo)==combo.Text,"full-value tooltip follows selection");form.Close();}}
 static System.Collections.Generic.List<T> All<T>(Control c)where T:Control{var found=new System.Collections.Generic.List<T>();if(c is T)found.Add((T)c);foreach(Control child in c.Controls)found.AddRange(All<T>(child));return found;}
 static void PrintTextLayout(string dir){foreach(string lang in Loc.Codes)foreach(float scale in new[]{1f,1.5f,2f}){Loc.Language=lang;using(var form=new Form{Size=new Size(980,680),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new PrintPanel(LabelSettings.Default())){panel.GetType().GetField("started",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,true);panel.Font=UiFonts.Get(10.5f*scale);form.Controls.Add(panel);form.Show();form.Hide();form.Show();Application.DoEvents();var tabs=F<ModernTabs>(panel,"printSections");for(int page=0;page<4;page++){tabs.SelectedIndex=page;Application.DoEvents();foreach(var b in All<ModernButton>(panel)){if(!b.Visible)continue;if(b.Parent is FlowLayoutPanel&&b.Parent.Parent is TableLayoutPanel){Rectangle action=panel.RectangleToClient(b.RectangleToScreen(b.ClientRectangle));Check(panel.ClientRectangle.Contains(action),"footer action fully contained "+b.Text+" / "+lang+" @ "+scale);}int h=TextRenderer.MeasureText(b.Text,b.Font,new Size(Math.Max(1,b.Width-16),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;Check(b.Height>=h+10,"print button caption fits "+lang+" @ "+scale+" / "+b.Text+" / "+b.Size+" wanted "+h); }Check(F<Panel>(tabs,"body").Height>=80,"print settings retain reachable viewport "+lang+" @ "+scale+" / "+F<Panel>(tabs,"body").Height);foreach(var label in All<Label>(tabs)){if(!label.Visible||!label.AutoSize||label.Text.Length==0)continue;int needed=TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(1,label.Width-label.Padding.Horizontal),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;Check(label.Height>=needed+label.Padding.Vertical-2,"inline text fully readable "+lang+" @ "+scale+" / "+label.Text);}foreach(var check in All<CheckBox>(tabs)){if(!check.Visible)continue;using(var g=check.CreateGraphics()){int glyph=CheckBoxRenderer.GetGlyphSize(g,System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal).Width;int h=TextRenderer.MeasureText(check.Text,check.Font,new Size(Math.Max(1,check.Width-glyph-8),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;Check(check.Height>=h,"checkbox caption fits "+lang+" @ "+scale+" / "+check.Text+" / "+check.Size+" wanted "+h+" glyph "+glyph);}}if(lang=="hu"&&scale==2)Snapshot(form,Path.Combine(dir,"print-hu-200-section-"+page+".png"));var print=F<ModernButton>(panel,"print");Rectangle r=panel.RectangleToClient(print.RectangleToScreen(print.ClientRectangle));if(r.Bottom>panel.ClientSize.Height||r.Right>panel.ClientSize.Width)Snapshot(form,Path.Combine(dir,"clipped-actions.png"));Check(r.Bottom<=panel.ClientSize.Height&&r.Right<=panel.ClientSize.Width,"print action remains inside window "+lang+" @ "+scale+" / "+r+" / "+panel.ClientSize+" / "+print.Parent.Bounds);}if(lang=="hu"&&scale==2)Snapshot(form,Path.Combine(dir,"print-hu-200.png"));form.Close();}}Loc.Language="sk";}
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint cmd);
 static void NativeClick(Control c,IntPtr handle){c.Focus();SendMessage(handle,0x201,new IntPtr(1),new IntPtr(3|(3<<16)));SendMessage(handle,0x202,IntPtr.Zero,new IntPtr(3|(3<<16)));Application.DoEvents();}
 static void NativeWheelHandle(Control c,IntPtr handle,int delta){var p=c.PointToScreen(new Point(3,3));SendMessage(handle,0x20a,new IntPtr(unchecked(delta<<16)),new IntPtr(unchecked((p.Y<<16)|(p.X&0xffff))));Application.DoEvents();}
 static void ComboWheels(){foreach(var style in new[]{ComboBoxStyle.DropDownList,ComboBoxStyle.DropDown})using(var form=new Form{ClientSize=new Size(400,180),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new Panel{Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(0,800)})using(var combo=new ModernComboBox{Location=new Point(10,10),Width=250,DropDownStyle=style})using(var other=new TextBox{Top=70}){form.Controls.Add(panel);panel.Controls.Add(combo);panel.Controls.Add(other);for(int i=0;i<40;i++)combo.Items.Add("Value "+i);combo.SelectedIndex=20;form.Show();form.Hide();form.Show();Application.DoEvents();other.Focus();NativeWheel(combo,-120);Check(combo.SelectedIndex==20,"combo hover preserves selection "+style);combo.Focus();NativeWheel(combo,-120);Check(combo.SelectedIndex==20,"combo keyboard focus alone preserves selection "+style);Check(panel.AutoScrollPosition.Y<0,"inactive combo scrolls panel "+style);panel.AutoScrollPosition=Point.Empty;IntPtr child=style==ComboBoxStyle.DropDown?GetWindow(combo.Handle,5):combo.Handle;Check(child!=IntPtr.Zero,"native combo input exists");NativeClick(combo,child);combo.DroppedDown=false;combo.SelectedIndex=20;NativeWheelHandle(combo,child,-120);Check(combo.SelectedIndex>20,"clicked combo wheel changes selection "+style);int selected=combo.SelectedIndex;other.Focus();combo.Focus();NativeWheelHandle(combo,child,120);Check(combo.SelectedIndex==selected,"combo focus loss revokes wheel "+style);combo.Focus();SendMessage(combo.Handle,0x100,new IntPtr(0x28),IntPtr.Zero);Check(combo.SelectedIndex>selected,"combo arrow key remains usable "+style);combo.DroppedDown=true;Check(combo.DroppedDown,"keyboard-opened list remains available "+style);NativeWheel(combo,120);Check(combo.DroppedDown,"wheel preserves open list "+style);combo.DroppedDown=false;if(style==ComboBoxStyle.DropDown){combo.Text="Typed text";Check(combo.Text=="Typed text","editable combo retains text input");}form.Close();}}
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 static void NativeWheel(Control c,int delta){var point=c.PointToScreen(new Point(3,3));SendMessage(c.Handle,0x20a,new IntPtr(unchecked(delta<<16)),new IntPtr(unchecked((point.Y<<16)|(point.X&0xffff))));Application.DoEvents();}
 static void NativeWheelEditing(){using(var form=new Form{StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var number=new ClickWheelNumericUpDown{Maximum=100,Value=50})using(var other=new TextBox{Top=70}){form.Controls.Add(number);form.Controls.Add(other);form.Show();form.Hide();form.Show();Application.DoEvents();Control edit=null;foreach(Control child in number.Controls)if(child is TextBoxBase)edit=child;Check(edit!=null,"native numeric edit exists");other.Focus();NativeWheel(edit,120);Check(number.Value==50,"native child hover cannot edit");number.Focus();NativeWheel(edit,120);Check(number.Value==50,"native focus alone cannot edit");SendMessage(edit.Handle,0x201,new IntPtr(1),new IntPtr(3|(3<<16)));SendMessage(edit.Handle,0x202,IntPtr.Zero,new IntPtr(3|(3<<16)));NativeWheel(edit,120);Check(number.Value>50,"native child click arms wheel");decimal value=number.Value;other.Focus();NativeWheel(edit,-120);Check(number.Value==value,"native focus loss disarms wheel");form.Close();}}
 static int count;static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
 static T F<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
 static void Sizing(){Check(LabelSettings.Default().DigitSize==12,"new caption 12pt");Check(LabelSettings.Default().Texts[0].AutoWidth,"startup text opts in");var old=ProjectXml.Decode<TextItem>("<TextItem><Text>Legacy</Text><Width>54</Width></TextItem>");Check(!old.AutoWidth&&old.Width==54,"legacy manual dimensions");var t=new TextItem{Text="Short",AutoWidth=true,X=3,Y=3};TextAutoLayout.Fit(t,60,45);double shortWidth=t.Width;Check(shortWidth<40,"short text content width");t.Text="Longer product name";TextAutoLayout.Fit(t,60,45);Check(t.Width>shortWidth&&t.Width<=54,"typing grows width");t.Text="A";TextAutoLayout.Fit(t,60,45);Check(t.Width<shortWidth,"deleting shrinks width");Check(ProjectXml.Clone(LabelSettings.Default()).Texts[0].AutoWidth,"auto width XML");}
 static void MoreSizing(){var offCenter=new TextItem{AutoWidth=true,Center=true,X=3,Y=3,Width=10,Text="Long off-center product title"};TextAutoLayout.Fit(offCenter,60,45);Check(offCenter.X>=2.999&&offCenter.Width<=10.001,"off-center text grows within available width");var s=LabelSettings.Default();double center=s.Texts[0].X+s.Texts[0].Width/2;s.Texts[0].Text="A";TextAutoLayout.Apply(s);Check(Math.Abs(center-s.Texts[0].X-s.Texts[0].Width/2)<.001,"center anchor retained");s.Texts[0].Text="Žltý čaj\r\nNový riadok";TextAutoLayout.Apply(s);Check(s.Texts[0].Height>6&&s.Texts[0].Width<54,"Unicode multiline fit");double first=s.Texts[0].Width;TextAutoLayout.Apply(s);Check(Math.Abs(first-s.Texts[0].Width)<.0001,"auto layout idempotent");s.Texts[0].Text="{serial}";s.Variables.Add(new VariableField{Name="serial",Type="Fixed",FixedValue="Long batch product name"});var row=BatchData.Record(s,new string[0],new System.Collections.Generic.List<ColumnBinding>(),-1,0,1,DateTime.Today);Check(row.Error==null&&row.Label.Texts[0].Text=="Long batch product name","batch expands automatic text");var expected=ProjectXml.Decode<TextItem>(ProjectXml.Encode(s.Texts[0]));expected.Text="Long batch product name";TextAutoLayout.Fit(expected,60,45);Check(Math.Abs(row.Label.Texts[0].Width-expected.Width)<.001,"batch geometry fits expanded content");var manual=new TextItem{Text="Fixed",X=3,Y=3,Width=40,Height=8};TextAutoLayout.Fit(manual,60,45);Check(manual.Width==40&&manual.Height==8,"manual fields remain untouched");foreach(double width in new[]{10d,20,30,40,60}){var startup=StartupLayout.Create(new StartupProfile{Width=width,Height=45});Check(startup.DigitSize>=10,"small label caption floor "+width);Engine.Validate(startup);}var old=ProjectXml.Decode<LabelSettings>("<LabelSettings><DigitSize>5.5</DigitSize></LabelSettings>");Check(old.DigitSize==5.5,"saved caption size preserved");Check(ProjectXml.Decode<LabelSettings>("<LabelSettings/>").DigitSize==8,"legacy absent caption unchanged");}
 static void StyledField(string dir){
  var label=LabelSettings.Default();label.HideBarcode=true;label.Texts.Clear();
  var field=new TextItem{Text="Šarža: {lot} / {lot}",X=3,Y=5,Width=24,Height=9,Size=16,MinimumFitSize=6};
  label.Texts.Add(field);
  StyledText.Toggle(field,0,6,TextEmphasis.Bold);
  StyledText.Toggle(field,7,5,TextEmphasis.Italic);
  StyledText.Toggle(field,15,5,TextEmphasis.Bold|TextEmphasis.Italic);
  label.Variables.Add(new VariableField{Name="lot",Type="Fixed",FixedValue="ABC 29041"});
  var file=Path.Combine(dir,"styled.ean.xml");Engine.Save(file,label);label=Engine.Load(file);
  label.QuickPrintFields.Add(0);
  var record=BatchData.Record(label,Array.Empty<string>(),new System.Collections.Generic.List<ColumnBinding>(),-1,0,17,DateTime.Today);
  Check(record.Error==null,"styled variables prepare a batch record");
  var resolved=record.Label.Texts[0];
  Check(resolved.Text=="Šarža: ABC 29041 / ABC 29041","styled placeholders expand");
  Check(StyledText.At(resolved,0)==TextEmphasis.Bold&&StyledText.At(resolved,7)==TextEmphasis.Italic&&
   StyledText.At(resolved,resolved.Text.Length-1)==(TextEmphasis.Bold|TextEmphasis.Italic),"each replacement keeps its token style");
  var quick=QuickPrint.Prepare(label,null,DateTime.Today);
  Check(quick.CanPrint&&ProjectXml.Encode(quick.Label.Texts[0])==ProjectXml.Encode(resolved),"quick template uses the same styled variable expansion");
  var mapped=BatchData.Record(label,new[]{"Mapped"},new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Column=0,Target="text:0"}},-1,0,18,DateTime.Today);
  Check(mapped.Error==null&&StyledText.At(mapped.Label.Texts[0],5)==TextEmphasis.Bold,"whole-field mapping extends the first character's style");
  double actual=FixedTextLayout.Resolve(resolved,out bool fits);
  Check(actual>=6&&actual<16&&fits,"mixed styles wrap and shrink within fixed limits");
  var plan=OutputPlan.Create(record.Label);
  using var scene=plan.Scene(0);
  string path=VectorArt.PathData(scene.Shapes[0].Path,true);
  var bare=ProjectXml.Clone(record.Label);bare.Texts[0].Styles.Clear();
  using(var plain=VectorArt.Build(bare))
   Check(path!=VectorArt.PathData(plain.Shapes[0].Path,true),"style changes rendered geometry");
  using(var doc=new LabelPrintDocument(record.Label,plan)){
   using(var preview=doc.CreatePreview(0,new SizeF(60,45)))
    Check(preview.Width>0&&preview.Height>0,"formatted field renders in the print preview");
   using var pixels=new Bitmap(480,360);
   pixels.SetResolution(203.2f,203.2f);
   using(var g=Graphics.FromImage(pixels)){
    g.Clear(Color.White);
    doc.RenderPrinterPage(g,0,new RectangleF(0,0,240,180),0,0,false);
   }
   Check(Array.Exists(Engine.Packed(pixels,true),dot=>dot!=0),"formatted field produces ink through Windows print rendering");
  }
  foreach(string format in new[]{"svg","pdf","docx","zpl"})JobOutput.Export(Path.Combine(dir,"styled."+format),format,plan,203.2);
  Check(File.ReadAllText(Path.Combine(dir,"styled.svg")).Contains(path),"SVG contains the fitted mixed style path");
  Check(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"styled.pdf"))).Contains(VectorArt.PathData(scene.Shapes[0].Path,false)),"PDF contains the fitted mixed style path");
  using(var word=System.IO.Compression.ZipFile.OpenRead(Path.Combine(dir,"styled.docx")))
  using(var reader=new StreamReader(word.GetEntry("word/media/part1.svg").Open()))
   Check(reader.ReadToEnd().Contains(path),"Word contains the fitted mixed style path");
  using(var raster=JobOutput.Raster(scene,203.2))
  using(var zpl=ZplImport.Render(File.ReadAllText(Path.Combine(dir,"styled.zpl")),203.2,60,45,out _))
   Check(Convert.ToBase64String(Engine.Packed(raster,true))==Convert.ToBase64String(Engine.Packed(zpl,true)),"ZPL retains the fitted mixed style dots");
  StyledText.Edit(resolved,"Nové "+resolved.Text);
  Check(StyledText.At(resolved,5)==TextEmphasis.Bold&&StyledText.At(resolved,12)==TextEmphasis.Italic,"ordinary text edits rebase spans");
 }
 static void StyledEditor(string dir){
  using var main=new MainForm(Path.Combine(dir,"styled-editor.xml"));
  main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);
  main.Show();Application.DoEvents();
  var source=LabelSettings.Default();
  source.Texts[0].Bold=false;
  source.Texts[0].Text="Šarža:\r\n{lot}";
  int token=source.Texts[0].Text.IndexOf('{');
  StyledText.Toggle(source.Texts[0],token,5,TextEmphasis.Italic);
  Call(main,"Apply",source);Call(main,"ResetHistory",true);
  F<PreviewPanel>(main,"preview").SelectElement(0);
  Exception error=null;bool opened=false;
  using(var timer=new Timer{Interval=35}){
   timer.Tick+=delegate{
    foreach(Form dialog in Application.OpenForms){
     if(dialog==main||dialog.Text!=Loc.T("Formátovať časti textu…"))continue;
     timer.Stop();opened=true;
     try{
      var edit=(RichTextBox)dialog.Controls.Find("styledTextEditor",true)[0];
      edit.Select(edit.Text.IndexOf('{'),5);
      using(var font=edit.SelectionFont)Check(font!=null&&font.Italic,"styles restored after CRLF in the editor");
      edit.Select(0,6);Key(edit,Keys.Control|Keys.B);
      using(var font=edit.SelectionFont)Check(font!=null&&font.Bold&&!font.Italic,"keyboard bold applies to the selected text");
      ((Button)dialog.AcceptButton).PerformClick();
     }catch(Exception ex){error=ex;dialog.Close();}
     break;
    }
   };
   timer.Start();F<Button>(main,"styleTextButton").PerformClick();
  }
  if(error!=null)throw error;
  Check(opened,"formatting editor opened from inspector");
  var current=((LabelSettings)Call(main,"Read")).Texts[0];
  Check(StyledText.At(current,0)==TextEmphasis.Bold&&StyledText.At(current,current.Text.IndexOf('{'))==TextEmphasis.Italic,
   "formatting editor saves both selected styles");
  Call(main,"StepHistory",false);
  var restored=((LabelSettings)Call(main,"Read")).Texts[0];
  Check(StyledText.At(restored,0)==TextEmphasis.Regular,"one undo restores the original formatting");
  Call(main,"Apply",LabelSettings.Default());main.Close();
 }
 static void FixedText(string dir){
  const string content="Žltý čaj z horskej záhrady";
  var label=LabelSettings.Default();label.HideBarcode=true;label.Texts.Clear();
  var field=new TextItem{Text=content,X=3,Y=6,Width=24,Height=9,Size=16,MinimumFitSize=6,Center=true};
  label.Texts.Add(field);label.QuickPrintFields.Add(0);
  bool fits;
  double effective=FixedTextLayout.Resolve(field,out fits);
  Check(fits&&effective>=6&&effective<16,"wrapped text shrinks to fit a fixed field");
  using(var scene=VectorArt.Build(label)){
   var shape=scene.Shapes[0];var bounds=shape.Path.GetBounds();
   Check(bounds.Height>3&&bounds.Bottom<=15.01&&bounds.Right<=27.01&&shape.Clip==new RectangleF(3,6,24,9),"vector scene keeps the fixed field and all fitted lines");
  }
  Check(field.Size==16&&field.Width==24&&field.Height==9,"layout never rewrites requested geometry or font");
  var saved=Path.Combine(dir,"fixed-fit.ean.xml");Engine.Save(saved,label);
  var template=Engine.Load(saved);
  Check(template.Texts[0].MinimumFitSize==6&&template.Texts[0].Size==16,"saved template retains both font sizes");
  var quick=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0,content}},DateTime.Today);
  var batch=BatchData.Record(template,new[]{content},new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Column=0,Target="text:0"}},-1,0,17,DateTime.Today);
  Check(quick.CanPrint&&batch.Error==null,"fitted template and batch can print");
  var plan=quick.CreatePlan(1);
  using var printScene=plan.Scene(0);
  var fittedPath=printScene.Shapes[0].Path;
  string svgPath=VectorArt.PathData(fittedPath,true);
  using(var batchScene=VectorArt.Build(batch.Label))
   Check(VectorArt.PathData(batchScene.Shapes[0].Path,true)==svgPath,"batch and quick print use identical vector layout");
  using(var doc=new LabelPrintDocument(quick.Label,plan)){
   using(var image=doc.CreatePreview(0,new SizeF(60,45)))
    Check(image.Width>0&&image.Height>0,"fit renders in the print preview");
   using var printed=new Bitmap(480,360);
   printed.SetResolution(203.2f,203.2f);
   using(var graphics=Graphics.FromImage(printed)){
    graphics.Clear(Color.White);
    doc.RenderPrinterPage(graphics,0,new RectangleF(0,0,240,180),0,0,false);
   }
   var ink=Rectangle.Empty;
   for(int y=0;y<printed.Height;y++)for(int x=0;x<printed.Width;x++){
    if(printed.GetPixel(x,y).R>=128)continue;
    var dot=new Rectangle(x,y,1,1);
    ink=ink.IsEmpty?dot:Rectangle.Union(ink,dot);
   }
   var geometry=fittedPath.GetBounds();
   Check(!ink.IsEmpty&&Math.Abs(ink.Left-geometry.Left*8)<=2&&Math.Abs(ink.Bottom-geometry.Bottom*8)<=2,
    "Windows print drawing uses the fitted bounds in physical units");
  }
  string svg=Path.Combine(dir,"fixed-fit.svg");
  foreach(string format in new[]{"svg","pdf","docx","zpl"})JobOutput.Export(Path.Combine(dir,"fixed-fit."+format),format,plan,203.2);
  Check(File.ReadAllText(svg).Contains(" d=\""+svgPath+"\""),"SVG export uses exactly the fitted artwork");
  Check(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir,"fixed-fit.pdf"))).Contains(VectorArt.PathData(fittedPath,false)),"PDF contains the fitted path");
  using(var word=System.IO.Compression.ZipFile.OpenRead(Path.Combine(dir,"fixed-fit.docx")))
  using(var reader=new StreamReader(word.GetEntry("word/media/part1.svg").Open()))
   Check(reader.ReadToEnd().Contains(" d=\""+svgPath+"\""),"Word contains the fitted path");
  using(var raster=JobOutput.Raster(printScene,203.2))
  using(var zpl=ZplImport.Render(File.ReadAllText(Path.Combine(dir,"fixed-fit.zpl")),203.2,60,45,out _))
   Check(Convert.ToBase64String(Engine.Packed(zpl,true))==Convert.ToBase64String(Engine.Packed(raster,true)),"ZPL encodes exactly the fitted raster");
  string tooLong=new string('W',250);
  var overflow=QuickPrint.Prepare(template,new System.Collections.Generic.Dictionary<int,string>{{0,tooLong}},DateTime.Today);
  Check(overflow.CanPrint&&overflow.Issues.Exists(i=>!i.Blocking&&i.Element==0),"quick print displays an opt-in overflow warning");
  Check(FixedTextLayout.Resolve(overflow.Label.Texts[0],out fits)==6&&!fits,"unfitted text stops at the minimum");
  batch=BatchData.Record(template,new[]{tooLong},new System.Collections.Generic.List<ColumnBinding>{new ColumnBinding{Column=0,Target="text:0"}},-1,0,17,DateTime.Today);
  Check(batch.Error==null&&batch.Issues.Exists(i=>!i.Blocking&&i.Column==0&&i.Row==17&&i.Message.StartsWith(string.Format(Loc.T("Text {0}"),1)+" — ")),"batch warning identifies mapped row, column and text field");
  var report=PreflightReport.Inspect(overflow.CreatePlan(1));
  Check(report.HasWarnings&&!report.CanProceed(false)&&report.CanProceed(true),"overflow requires explicit output review");
  var narrow=new TextItem{Text="W",Width=1,Height=40,Size=14,MinimumFitSize=14};
  Check(FixedTextLayout.Resolve(narrow,out fits)==14&&!fits,"single glyph wider than the field is reported");
  var shortText=new TextItem{Text="Čaj",Width=25,Height=12,Size=12,MinimumFitSize=6};
  Check(FixedTextLayout.Resolve(shortText,out fits)==12&&fits,"short text keeps nominal size");
  shortText.Width=40;shortText.Height=20;shortText.Size=16;
  foreach(string value in new[]{"jablko","Ária"}){
   shortText.Text=value;
   Check(FixedTextLayout.Resolve(shortText,out fits)==16&&fits,"normal left glyph bearing does not shrink "+value);
  }
 }
 static void FixedTextEditor(string dir){
  using var main=new MainForm(Path.Combine(dir,"fixed-fit-prefs.xml"));
  main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);
  main.Show();Application.DoEvents();
  var preview=F<PreviewPanel>(main,"preview");
  preview.SelectElement(0);
  var fixedFit=F<CheckBox>(main,"inspectFixedFit");var minimum=F<NumericUpDown>(main,"inspectFitMinimum");
  var autoWidth=F<CheckBox>(main,"inspectAutoWidth");
  var before=((LabelSettings)Call(main,"Read")).Texts[0];
  Check(!fixedFit.Checked&&autoWidth.Checked&&!minimum.Visible,"existing automatic width is preserved until fixed fit is selected");
  fixedFit.Checked=true;minimum.Value=7;
  var saved=((LabelSettings)Call(main,"Read")).Texts[0];
  Check(fixedFit.Checked&&!autoWidth.Checked&&minimum.Visible&&saved.MinimumFitSize==7&&saved.Width==before.Width,"inspector sets fixed fit without resizing");
  F<TextBox>(main,"inspectText").Text=new string('W',250);
  Call(main,"UpdatePreview");
  Check(preview.ProblemElements.Contains(0)&&preview.Settings.Texts[0].Size==before.Size,"editor flags overflow without permanently reducing font size");
  Call(main,"Apply",LabelSettings.Default());main.Close();
 }
 static void Key(Control control,Keys key){var message=new Message();object[] args={message,key};bool handled=(bool)control.GetType().GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,args);Check(handled,"actual Enter/Escape command handled");Application.DoEvents();}
 static bool HasButton(Control parent,string text){foreach(Control child in parent.Controls){if(child is Button&&child.Text==text)return true;if(HasButton(child,text))return true;}return false;}
 static void Snapshot(Control control,string path){using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path);}}
 static void Editor(string dir){using(var main=new MainForm(Path.Combine(dir,"editor-prefs.xml"))){main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();main.Hide();main.Show();Application.DoEvents();var preview=F<PreviewPanel>(main,"preview");preview.SelectElement(0);Check(!HasButton(F<TableLayoutPanel>(main,"codeInspector"),Loc.T("Vybrať typ…")),"no duplicate code chooser");var input=F<TextBox>(main,"inspectText");Check(input.Focus(),"initial inspector focus visible="+input.Visible+" enabled="+input.Enabled+" selected="+preview.SelectedIndex+" count="+preview.Selection.Count+" inspector="+F<TableLayoutPanel>(main,"textInspector").Visible);var before=(LabelSettings)Call(main,"Read");input.Text="Short";Application.DoEvents();Check(((LabelSettings)Call(main,"Read")).Texts[0].Text=="Short"&&preview.Settings.Texts[0].Text=="Short","inspector updates while focused");Check(input.ContainsFocus,"typing retains focus: "+(main.ActiveControl==null?"null":main.ActiveControl.GetType().Name)+" selected="+preview.SelectedIndex);input.SelectionStart=3;input.SelectedText="x";Check(input.SelectionStart==4,"caret retained");Key(input,Keys.Enter);Check(preview.Focused,"Enter confirms inspector");Call(main,"StepHistory",false);Check(((LabelSettings)Call(main,"Read")).Texts[0].Text==before.Texts[0].Text,"one-step undo of typing");Call(main,"StepHistory",true);Check(((LabelSettings)Call(main,"Read")).Texts[0].Text=="Shoxrt","redo typing");preview.SelectElement(0);input.Focus();input.Text="Cancelled inspector";Key(input,Keys.Escape);Check(((LabelSettings)Call(main,"Read")).Texts[0].Text=="Shoxrt","inspector Escape restores after focus transition");
  preview.SelectElement(0);string original=ProjectXml.Encode((LabelSettings)Call(main,"Read"));int forms=Application.OpenForms.Count;var rectangle=preview.ElementRectangle(0);typeof(PreviewPanel).GetMethod("OnMouseDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,new object[]{new MouseEventArgs(MouseButtons.Left,2,(int)(rectangle.X+rectangle.Width/2),(int)(rectangle.Y+rectangle.Height/2),0)});var inline=F<TextBox>(main,"inlineText");Check(inline!=null&&inline.Focused&&Application.OpenForms.Count==forms,"double-click edits on canvas without modal");inline.Text="Live on canvas";Call(main,"AutoSave");Check(F<TextBox>(main,"inlineText")==inline&&inline.ContainsFocus,"autosave does not terminate inline edit");Check(preview.Settings.Texts[0].Text==inline.Text,"inline live preview");Key(inline,Keys.Escape);Check(ProjectXml.Encode((LabelSettings)Call(main,"Read"))==original,"Escape restores entire text field");
  Call(main,"BeginInlineText",0);inline=F<TextBox>(main,"inlineText");inline.Text="Žltý\r\nštítok";Key(inline,Keys.Enter);Check(F<TextBox>(main,"inlineText")==null&&((LabelSettings)Call(main,"Read")).Texts[0].Text.Contains("štítok"),"inline Unicode multiline commit");
  Call(main,"Apply",LabelSettings.Default());Call(main,"ResetHistory",true);preview.SelectElement(0);input.Focus();input.Text="Cancel clean";Key(input,Keys.Escape);Check(!F<bool>(main,"dirty"),"Escape keeps a clean document clean");input.Focus();Key(input,Keys.Enter);Check(!F<bool>(main,"dirty"),"no-op Enter keeps clean document clean");var initialEdit=ElementTransform.Get((LabelSettings)Call(main,"Read"),0);var resized=initialEdit.Copy();resized.Width+=5;Call(main,"EditElement",resized);Check(!((LabelSettings)Call(main,"Read")).Texts[0].AutoWidth,"resize opts out");preview.Restoring=true;try{Call(main,"EditElement",initialEdit);}finally{preview.Restoring=false;}Check(((LabelSettings)Call(main,"Read")).Texts[0].AutoWidth,"cancel resize restores auto width");Call(main,"EditElement",resized);Call(main,"EditElement",initialEdit);preview.Restoring=true;try{Call(main,"EditElement",initialEdit);}finally{preview.Restoring=false;}Check(((LabelSettings)Call(main,"Read")).Texts[0].AutoWidth,"cancel resize after returning to original bounds restores auto width");var taller=initialEdit.Copy();taller.Height+=2;Call(main,"EditElement",taller);Check(!((LabelSettings)Call(main,"Read")).Texts[0].AutoWidth,"height-only resize opts out");
  var grid=F<DataGridView>(main,"grid");grid.Rows[0].Cells[3].Value=25d;var manual=(LabelSettings)Call(main,"Read");Check(!manual.Texts[0].AutoWidth&&manual.Texts[0].Width==25,"manual table width opts out");manual.Metadata.Add(new ElementMeta{Index=0,Locked=true});Call(main,"Apply",manual);Call(main,"BeginInlineText",0);Check(F<TextBox>(main,"inlineText")==null,"locked text cannot edit");
  Call(main,"Apply",LabelSettings.Default());Call(main,"AddText");var added=(LabelSettings)Call(main,"Read");Check(added.Texts[added.Texts.Count-1].AutoWidth&&added.Texts[added.Texts.Count-1].Width<40,"new field uses natural width");Snapshot(main,Path.Combine(dir,"editor-auto-width.png"));
  var variableLabel=LabelSettings.Default();variableLabel.Width=120;variableLabel.Texts[0].X=40;variableLabel.Texts[0].Width=40;variableLabel.Texts[0].Text="{serial}";variableLabel.Variables.Add(new VariableField{Name="serial",Type="Fixed",FixedValue="Long variable product name"});Call(main,"Apply",variableLabel);preview.SelectElement(0);var variableMove=preview.Element(0).Copy();variableMove.X+=2.5;Call(main,"EditElement",variableMove);Call(main,"UpdatePreview");Check(Math.Abs(preview.Element(0).X-variableMove.X)<.000001,"moving expanded auto-width text preserves final position");Call(main,"Apply",LabelSettings.Default());
  foreach(string lang in Loc.Codes){F<ComboBox>(main,"language").SelectedIndex=Array.IndexOf(Loc.Codes,lang);preview.SelectElement(0);Call(main,"BeginInlineText",0);inline=F<TextBox>(main,"inlineText");Check(inline.AccessibleName==Loc.T("Text na etikete"),"inline accessibility "+lang);Key(inline,Keys.Enter);}
  Call(main,"Apply",LabelSettings.Default());main.Close();}}
 static void PrintSections(string dir){Loc.Language="sk";using(var owner=new MainForm(Path.Combine(dir,"print-owner.xml")))using(var form=(Form)Call(owner,"CreatePrintWindow",LabelSettings.Default())){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);var panel=(PrintPanel)form.Controls[0];typeof(PrintPanel).GetField("initialPrinterName",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,"Microsoft Print to PDF");form.Show();form.Hide();form.Show();var watch=System.Diagnostics.Stopwatch.StartNew();while(panel.PreviewBuilds==0){Application.DoEvents();System.Threading.Thread.Sleep(2);if(watch.ElapsedMilliseconds>30000)throw new Exception("Preview timeout");}Check(panel.Ready,"organized print preview ready");Check(!HasButton(panel,Loc.T("Kontrola výstupu")),"no redundant output review button");var sections=F<ModernTabs>(panel,"printSections");Check(sections!=null,"named print sections");foreach(string name in new[]{"printers","copies","papers","mediaProfiles","profileName","offsetX","offsetY","scaleX","scaleY","ignoreDriverMargins","zebraZpl","zebraDpi","zebraDarkness","diagnosticsButton","driverProperties","calibrationLaunch"}){var control=F<Control>(panel,name);Check(control!=null&&control.Parent!=null,"retained control "+name);}for(int i=0;i<4;i++){sections.SelectedIndex=i;Application.DoEvents();Check(F<Control>(panel,"printers").Visible&&F<Control>(panel,"copies").Visible&&F<Control>(panel,"print").Visible,"primary settings always visible "+i);Snapshot(form,Path.Combine(dir,"print-section-"+i+".png"));}sections.SelectedIndex=0;Check(F<Control>(panel,"papers").Visible&&!F<Control>(panel,"offsetX").Visible,"media first, corrections separate");sections.SelectedIndex=1;Check(F<Control>(panel,"offsetX").Visible&&!F<Control>(panel,"zebraZpl").Visible,"calibration isolated");sections.SelectedIndex=2;Check(F<Control>(panel,"zebraDarkness").Visible,"Zebra section available");form.Size=form.MinimumSize;Application.DoEvents();var print=F<Control>(panel,"print");Rectangle bounds=form.RectangleToClient(print.RectangleToScreen(print.ClientRectangle));Check(form.ClientRectangle.Contains(bounds),"print action visible at minimum size");var summary=F<Label>(panel,"jobSummary");int needed=TextRenderer.MeasureText(summary.Text,summary.Font,new Size(summary.Width,0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;Check(summary.Height>=needed,"summary wraps without clipping: "+summary.Height+" / "+needed);Check(summary.Bottom<=summary.Parent.ClientSize.Height,"summary parent shows all lines: "+summary.Bottom+" / "+summary.Parent.ClientSize.Height+" row="+((TableLayoutPanel)summary.Parent.Parent).RowStyles[1].Height+" actual="+string.Join(",",((TableLayoutPanel)summary.Parent.Parent).GetRowHeights())+" dock="+summary.Parent.Dock+" bounds="+summary.Parent.Bounds);Snapshot(form,Path.Combine(dir,"print-minimum.png"));form.Close();}}
 static void Wheel(Control control,int delta){control.GetType().GetMethod("OnMouseWheel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,5,5,delta)});}
 static void ClickValue(NumericUpDown number){number.Focus();foreach(Control child in number.Controls)if(child is TextBoxBase){typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(child,new object[]{new MouseEventArgs(MouseButtons.Left,1,3,3,0)});return;}throw new Exception("numeric input missing");}
 static void WheelEditing(string dir){using(var main=new MainForm(Path.Combine(dir,"wheel-prefs.xml"))){main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();main.Hide();main.Show();Application.DoEvents();var values=F<System.Collections.Generic.Dictionary<string,NumericUpDown>>(main,"nums");var number=values["BarX"];var other=values["BarY"];other.Focus();decimal original=number.Value;Wheel(number,120);Check(number.Value==original,"hover wheel does not change numeric value");number.Focus();Wheel(number,120);Check(number.Value==original,"focus without click does not arm wheel");ClickValue(number);Wheel(number,120);Check(number.Value>original,"clicked numeric field retains wheel editing");other.Focus();number.Focus();decimal previous=number.Value;Wheel(number,-120);Check(number.Value==previous,"leaving numeric field revokes wheel editing");ClickValue(number);Wheel(number,-120);Check(number.Value<previous,"negative wheel edits activated field");foreach(var value in values.Values)Check(value.GetType().Name=="ClickWheelNumericUpDown","all editor numeric fields use wheel guard");Call(main,"Apply",LabelSettings.Default());main.Close();}}
 static void TabCaptions(){foreach(string lang in Loc.Codes)foreach(float size in new[]{10.5f,15.75f,21f}){Loc.Language=lang;using(var form=new Form{ClientSize=new Size(360,550),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var tabs=new ModernTabs{Dock=DockStyle.Fill,Font=UiFonts.Get(size)}){form.Controls.Add(tabs);foreach(string title in new[]{"Médium","Kalibrácia","Zebra","Detaily"})tabs.AddPage(Loc.T(title));form.Show();Application.DoEvents();var header=F<FlowLayoutPanel>(tabs,"header");foreach(Control button in header.Controls){int needed=TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width+12;Check(button.Width>=needed,"complete tab caption "+lang+" / "+button.Text+" @ "+size);Check(button.Right<=header.ClientSize.Width&&button.Bottom<=header.ClientSize.Height,"tab header stays within viewport");}form.Close();}}Loc.Language="sk";}
 static void WheelViewport(){using(var form=new Form{ClientSize=new Size(400,180),StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})using(var panel=new Panel{Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(0,800)})using(var number=new ClickWheelNumericUpDown{Location=new Point(10,10),Minimum=0,Maximum=10,Value=5}){form.Controls.Add(panel);panel.Controls.Add(number);form.Show();form.Hide();form.Show();Application.DoEvents();Wheel(number,-120);Check(number.Value==5,"unarmed scroll preserves value");Check(panel.AutoScrollPosition.Y<0,"unarmed wheel scrolls enclosing viewport");panel.AutoScrollPosition=Point.Empty;ClickValue(number);Wheel(number,120);Check(number.Value>5,"clicked wheel increments value");decimal edited=number.Value;Check(panel.AutoScrollPosition.Y==0,"armed wheel does not scroll viewport");number.ReadOnly=true;Wheel(number,-120);Check(number.Value==edited,"read-only wheel preserves value");number.ReadOnly=false;number.Value=10;Wheel(number,120);Check(number.Value==10,"wheel respects numeric maximum");number.DownButton();Check(number.Value==9,"spinner remains usable");number.UpButton();Check(number.Value==10,"spinner increment remains usable");form.Close();}}
 static void EditorShell(string dir){
  using var form=new MainForm(Path.Combine(dir,"studio-shell.xml"));
  form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Size=form.MinimumSize;form.Show();form.Hide();form.Show();Application.DoEvents();
  var row=F<FlowLayoutPanel>(form,"headerCommands");
  foreach(string name in new[]{"projectCommand","insertCommand","batchCommand","viewCommand","exportCommand","printCommand"})
   Check(row.Controls.ContainsKey(name)&&row.Controls[name].Text.Length>0,"visible named command: "+name);
  var preview=F<PreviewPanel>(form,"preview");var zoom=F<Button>(form,"canvasZoomMode");
  Check(zoom.Parent.Parent==preview.Parent,"zoom belongs to the canvas area");
  preview.Focus();Key(form,Keys.Control|Keys.Oemplus);Check(preview.ViewZoom>0,"keyboard zoom changes only canvas scale");
  Key(form,Keys.Control|Keys.D0);Check(preview.ViewZoom==0,"keyboard resets fit");
  preview.SelectElement(-1);Key(form,Keys.F4);Check(F<TextBox>(form,"ean").Focused,"F4 focuses current content");
  Key(form,Keys.F8);Check(F<ModernTabs>(form,"workspaceTabs").SelectedIndex==1,"F8 opens checks");
  preview.SelectElement(0);var number=F<NumericUpDown[]>(form,"inspectNumbers");
  Check(number[0].PointToScreen(Point.Empty).Y==number[1].PointToScreen(Point.Empty).Y,"X and Y share a row at standard text size");
  var list=F<ListBox>(form,"elementList");Check(list.ContextMenuStrip!=null,"layer operations are available in context");
  Call(form,"BuildInsertMenu");
  var insert=F<ContextMenuStrip>(form,"insertMenu");
  Check(insert.Items.Count==8&&insert.Items[1].Text==Loc.T("Knižnica blokov…")&&
   insert.Items[3].Text==Loc.T("Obrázok podľa údajov…")&&insert.Items[4].Text==Loc.T("Pridať kód…"),
   "block library and mapped image appear next to the general code chooser");
  var input=F<TextBox>(form,"inspectText");
  input.Focus();input.Select(0,2);Key(input,Keys.Control|Keys.I);
  var styled=((LabelSettings)Call(form,"Read")).Texts[0];
  Check(StyledText.At(styled,0).HasFlag(TextEmphasis.Italic)&&!StyledText.At(styled,2).HasFlag(TextEmphasis.Italic),"keyboard styles only the selected text");
  var italic=form.Controls.Find("formatItalic",true)[0];
  Check((italic.AccessibilityObject.State&AccessibleStates.Pressed)!=0,"formatting reports the selected style");
  var tabs=F<ModernTabs>(form,"workspaceTabs");tabs.SelectedIndex=0;list.Focus();
  Key(form,Keys.Shift|Keys.F10);
  Check(list.ContextMenuStrip.Visible,"keyboard opens the contextual layer commands");
  list.ContextMenuStrip.Close();
  list.Focus();Key(form,Keys.Enter);
  Check(F<TextBox>(form,"inlineText")!=null,"Enter edits the selected layer");
  Key(F<TextBox>(form,"inlineText"),Keys.Escape);
  foreach(int size in new[]{100,150,200}){
   var font=UiFonts.Get(10.5f*size/100);
   form.Font=font;foreach(var control in All<Control>(form))control.Font=font;
   form.PerformLayout();Application.DoEvents();Call(form,"UpdateToolbarLayout");Application.DoEvents();
   foreach(Control button in row.Controls)Check(row.ClientRectangle.Contains(button.Bounds),"toolbar command fits at "+size+"%: "+button.Text);
   Check(zoom.Visible&&zoom.Height>=font.Height,"zoom stays reachable at "+size+"%");
   Check(preview.ClientSize.Width>200&&preview.ClientSize.Height>150,"canvas remains usable at "+size+"%");
   var formatting=F<FlowLayoutPanel>(form,"formattingBar");
   foreach(Control button in formatting.Controls)
    Check(formatting.ClientRectangle.Contains(button.Bounds),"formatting command is visible at "+size+"%");
   foreach(var tab in All<Button>(tabs))
    foreach(string word in tab.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries))
     Check(TextRenderer.MeasureText(word,tab.Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix).Width+10<=tab.Width,
      "tab caption remains readable at "+size+"%: "+tab.Text);
   Check(list.Height>100,"element list retains usable space at "+size+"%");
   Snapshot(form,Path.Combine(dir,"editor-shell-"+size+".png"));
  }
  Call(form,"Apply",LabelSettings.Default());form.Close();
 }
 [STAThread]static void Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);ReferenceUnderlay(args[0]);ResizeLabel(args[0]);ReusableBlocks(args[0]);TextLanguages(args[0]);MappedArtwork(args[0]);ConditionalOutput(args[0]);EditorShell(args[0]);PdfWorkspace(args[0]);PanelPersistence(args[0]);GuidedGs1(args[0]);Gs1Theme(args[0]);GuidedGs1Locales(args[0]);WheelEditing(args[0]);WheelViewport();NativeWheelEditing();ComboWheels();SettingExplanations();ComboCaptions();ComboRows();PrintTextLayout(args[0]);TabCaptions();Sizing();MoreSizing();FixedText(args[0]);StyledField(args[0]);FixedTextEditor(args[0]);Editor(args[0]);PrintSections(args[0]);File.WriteAllText(Path.Combine(args[0],"passed.txt"),"PASS "+count+" editor refinement checks");}catch(Exception ex){File.WriteAllText(Path.Combine(args[0],"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
