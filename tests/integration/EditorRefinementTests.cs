using System;using System.IO;using System.Drawing;using System.Windows.Forms;using System.Reflection;using EanEtikety;
class EditorRefinementTests {
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
  Check(insert.Items.Count==6&&insert.Items[2].Text==Loc.T("Pridať kód…"),"one general code chooser alongside other elements");
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
 [STAThread]static void Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);ConditionalOutput(args[0]);EditorShell(args[0]);PdfWorkspace(args[0]);PanelPersistence(args[0]);GuidedGs1(args[0]);Gs1Theme(args[0]);GuidedGs1Locales(args[0]);WheelEditing(args[0]);WheelViewport();NativeWheelEditing();ComboWheels();SettingExplanations();ComboCaptions();ComboRows();PrintTextLayout(args[0]);TabCaptions();Sizing();MoreSizing();FixedText(args[0]);StyledField(args[0]);FixedTextEditor(args[0]);Editor(args[0]);PrintSections(args[0]);File.WriteAllText(Path.Combine(args[0],"passed.txt"),"PASS "+count+" editor refinement checks");}catch(Exception ex){File.WriteAllText(Path.Combine(args[0],"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
