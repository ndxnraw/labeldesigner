using System;using System.IO;using System.Drawing;using System.Windows.Forms;using System.Reflection;using System.Collections.Generic;using EanEtikety;
class AccessibilityUiTests {
 static string dir;static int checks;static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,flags).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,flags).Invoke(o,args);}
 static bool Key(Control c,Keys key){var method=c.GetType().GetMethod("ProcessCmdKey",flags);object[] args={new Message(),key};return (bool)method.Invoke(c,args);}
 static List<T> All<T>(Control c)where T:Control{var result=new List<T>();if(c is T)result.Add((T)c);foreach(Control child in c.Controls)result.AddRange(All<T>(child));return result;}
 static void Show(Form f){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.Show();f.Hide();f.Show();Application.DoEvents();}
 static void Snap(Form f,string name){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(Point.Empty,f.Size));image.Save(Path.Combine(dir,name+".png"));}}
 static void Management(){
  var detection=typeof(StudioTheme).GetField("detectHighContrast",BindingFlags.Static|BindingFlags.NonPublic);
  var previous=detection.GetValue(null);
  string prefs=Path.Combine(dir,"management.xml");
  var service=new UpdateActivation(Path.Combine(dir,"management-updates"),UpdateFiles.CurrentFamily,prefs);
  using(var main=new MainForm(prefs)){
   try{
    foreach(string lang in Loc.Codes)foreach(float scale in new[]{1f,1.5f,2f})foreach(bool contrast in new[]{false,true}){
     Loc.Language=lang;
     detection.SetValue(null,(Func<bool>)(()=>contrast));
     using(var f=(Form)Call(main,"CreateApplicationManagement",service)){
      f.Font=UiFonts.Get(UiFonts.Default.Size*scale);Show(f);f.MinimumSize=Size.Empty;f.ClientSize=new Size(510,350);Application.DoEvents();
      var tabs=All<ModernTabs>(f)[0];
      var headers=All<Button>(tabs).FindAll(b=>b.AccessibleRole==AccessibleRole.PageTab);
      Check(headers.Count==3&&tabs.SelectedIndex==0&&f.Text==Loc.T("Správa aplikácie"),"three localized management pages, updates first");
      foreach(var b in headers){
       int wanted=TextRenderer.MeasureText(b.Text,b.Font,new Size(Math.Max(1,b.Width-16),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
       var location=f.RectangleToClient(b.RectangleToScreen(b.ClientRectangle));
       Check(b.Height>=wanted+8&&b.Parent.ClientRectangle.Contains(b.Bounds)&&f.ClientRectangle.Contains(location),
        "management tab fits "+lang+" @ "+scale+" / "+b.Bounds+" / header "+b.Parent.ClientSize+" / "+string.Join(",",tabs.GetRowHeights()));
      }
      var close=(Button)f.CancelButton;
      var bounds=f.RectangleToClient(close.RectangleToScreen(close.ClientRectangle));
      Check(f.ClientRectangle.Contains(bounds)&&close.DialogResult==DialogResult.Cancel,"Close pinned and available on Escape");
      Check(f.AcceptButton==null,"Enter at the dialog level cannot start an update");
      headers[0].Focus();Check(Key(headers[0],Keys.Right)&&tabs.SelectedIndex==1&&headers[1].Focused,"keyboard selects backups");
      var backup=All<CheckBox>(f).Find(b=>b.Visible);
      Check(f.SelectNextControl(headers[1],true,true,true,false)&&backup!=null&&backup.Focused,"Tab enters backup choices");
      Check(Key(tabs,Keys.Control|Keys.PageDown)&&tabs.SelectedIndex==2&&headers[2].Focused,"keyboard selects diagnostics");
      var log=All<TextBox>(f).Find(b=>b.Visible);
      if(scale==2&&lang=="hu"&&contrast)Snap(f,"management-diagnostics-hu-200-contrast");
      Check(f.SelectNextControl(headers[2],true,true,true,false)&&log!=null&&log.ReadOnly&&log.Focused&&log.Height>=log.Font.Height*2&&!string.IsNullOrEmpty(log.AccessibleName),
       "diagnostics readable and reachable "+lang+" @ "+scale+" / "+log?.Bounds+" / "+log?.Font.Height+" / "+log?.Focused+" / header "+headers[0].Parent.Size);
      if(contrast){
       Check(f.BackColor==SystemColors.Control&&f.ForeColor==SystemColors.ControlText,"system dialog palette");
       Check(log.BackColor==SystemColors.Window&&log.ForeColor==SystemColors.WindowText,"system text palette");
       Check(headers[2].BackColor==SystemColors.Highlight&&headers[2].ForeColor==SystemColors.HighlightText,"system selection palette");
      }
      headers[2].Focus();Check(Key(headers[2],Keys.Home)&&tabs.SelectedIndex==0,"Home returns to updates");
      var primary=Field<Button>(f,"primary");
      Check(f.SelectNextControl(headers[0],true,true,true,false)&&primary.Focused&&primary.Text==Loc.T("Skontrolovať aktualizácie"),"Tab reaches update check");
      var page=(Panel)f.Controls.Find("updatePage",true)[0];
      Check(!page.HorizontalScroll.Visible,"update content wraps within viewport "+lang+" @ "+scale);
      if(lang=="hu"&&scale==2&&contrast)Snap(f,"management-updates-hu-200-contrast");
      if(lang=="sk"&&scale==1&&!contrast)Snap(f,"management-updates-sk-100-dark");
      Check((bool)Call(f,"ProcessDialogKey",Keys.Escape)&&f.DialogResult==DialogResult.Cancel,"Escape cancels from management");
     }
    }
   }finally{detection.SetValue(null,previous);}
  }
 }
 static void Tabs(){foreach(string lang in Loc.Codes)foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
  Loc.Language=lang;using(var font=new Font(UiFonts.Default.FontFamily,UiFonts.Default.Size*scale))using(var f=new Form{AutoScaleMode=AutoScaleMode.None,ClientSize=new Size((int)(230*scale),(int)(260*scale)),Font=font}){
   var tabs=new ModernTabs{Dock=DockStyle.Fill};f.Controls.Add(tabs);var page=tabs.AddPage(Loc.T("Etiketa a kód"));tabs.AddPage(Loc.T("Kontrola")+" (12345)");var input=new TextBox{Text="sample"};page.Controls.Add(input);Show(f);
   var buttons=All<Button>(tabs);foreach(var b in buttons){int wanted=TextRenderer.MeasureText(b.Text,b.Font,new Size(Math.Max(1,b.Width-16),0),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;Check(b.Height>=wanted+8,"tab caption clipped "+lang+" @ "+scale);Check(b.AccessibilityObject.Role==AccessibleRole.PageTab,"tab role");}
   Check((buttons[0].AccessibilityObject.State&AccessibleStates.Selected)!=0,"selected tab exposed");buttons[0].Focus();bool handled=Key(buttons[0],Keys.Right);Check(handled&&tabs.SelectedIndex==1&&buttons[1].Focused,"arrow selects/focuses tab: "+handled+" / "+tabs.SelectedIndex+" / "+buttons[1].Focused+" / "+buttons[1].CanFocus+" / "+lang+" / "+scale);Check(Key(buttons[1],Keys.Home)&&tabs.SelectedIndex==0,"Home selects first tab");
   input.Focus();Key(input,Keys.Right);Check(tabs.SelectedIndex==0,"input arrow does not change tab");Check(Key(tabs,Keys.Control|Keys.PageDown)&&tabs.SelectedIndex==1,"Ctrl+PageDown preserved");
   int height=buttons[0].Height;f.Width+=100;f.Width-=100;Application.DoEvents();Check(buttons[0].Height==height,"tab resize round trip");if(scale==2&&lang=="hu")Snap(f,"tabs-hu-200");f.Close();
  }
 }}
 static void Messages(){foreach(string lang in Loc.Codes)foreach(bool dark in new[]{false,true})foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
  Loc.Language=lang;using(var owner=new Form{BackColor=StudioTheme.Panel(dark)})using(var message=UiMessageBox.Create(owner,new string('x',14000),"Test",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning)){
   Show(message);message.Scale(new SizeF(scale,scale));message.Font=UiFonts.Get(UiFonts.Default.Size*scale);message.MinimumSize=Size.Empty;message.ClientSize=new Size(460,360);var body=All<TextBox>(message)[0];body.Font=UiFonts.Get(UiFonts.Default.Size*scale);foreach(var label in All<Label>(message))label.Font=UiFonts.Get(11*scale,FontStyle.Bold);Application.DoEvents();Check(body.Height>=body.Font.Height*2,"message text keeps readable space @ "+scale);Check(body.TabStop,"message text must be keyboard reachable");Check(!string.IsNullOrEmpty(body.AccessibleName),"message text has accessible name");Check(body.ScrollBars==ScrollBars.Vertical,"long text scrollable");Check(All<Label>(message).Exists(l=>l.Text==Loc.T("Upozornenie")),"severity is text");
   foreach(var b in All<Button>(message)){Rectangle r=message.RectangleToClient(b.RectangleToScreen(b.ClientRectangle));Check(r.Left>=0&&r.Right<=message.ClientSize.Width&&r.Top>=0&&r.Bottom<=message.ClientSize.Height,"message action reachable @ "+scale);}
   Check(((Button)message.CancelButton).DialogResult==DialogResult.Cancel,"Cancel result preserved");Check(((Button)message.AcceptButton).DialogResult==DialogResult.Yes,"Yes result preserved");body.Focus();Check(body.Focused,"message can select/copy detail");if(lang=="sk"&&dark&&scale==2)Snap(message,"long-message-dark-200");message.Close();
  }
 }}
 static void Editor(){Loc.Language="sk";using(var f=new MainForm(Path.Combine(dir,"prefs.xml"))){Show(f);var preview=Field<PreviewPanel>(f,"preview");preview.Focus();Check(Key(f,Keys.F6),"F6 handled");Check(Field<Panel>(f,"inspectorHost").ContainsFocus,"F6 canvas to properties");Check(Key(f,Keys.F6),"F6 again");Check(Field<FlowLayoutPanel>(f,"headerCommands").ContainsFocus,"F6 properties to commands");Check(Key(f,Keys.Shift|Keys.F6)&&Field<Panel>(f,"inspectorHost").ContainsFocus,"reverse section cycle");
  preview.SelectMany(new int[0],true);preview.Focus();Key(f,Keys.F4);Check(Field<Label>(f,"selectionOverview").Focused,"F4 focuses empty selection explanation");var body=Field<TableLayoutPanel>(f,"studioBody");body.ColumnStyles[0].Width=0;body.ColumnStyles[2].Width=0;preview.Focus();Key(f,Keys.F6);Check(Field<FlowLayoutPanel>(f,"headerCommands").ContainsFocus,"F6 skips hidden panels");Key(f,Keys.F4);Check(body.ColumnStyles[2].Width>0&&Field<Panel>(f,"inspectorHost").ContainsFocus,"F4 reveals hidden properties");preview.SelectElement(-1);var ean=Field<TextBox>(f,"ean");string old=ean.AccessibleName;Loc.Language="en";UiLocalization.Refresh(f);Check(ean.AccessibleName==Loc.T(old),"input accessible name changes language");var nums=Field<Dictionary<string,NumericUpDown>>(f,"nums");Check(!string.IsNullOrEmpty(nums["Width"].AccessibleName),"dimension accessible name");Field<ComboBox>(f,"units").SelectedItem="cm";Check(nums["Width"].AccessibleName.Contains("(cm)"),"accessible name follows unit label");
  Call(f,"Apply",LabelSettings.Default());f.Close();
 }}
 static void Dialog(MainForm owner,string method,object[] args,Action<Form> inspect){Exception error=null;bool seen=false;using(var timer=new Timer{Interval=30}){timer.Tick+=delegate{foreach(Form f in Application.OpenForms)if(f!=owner){timer.Stop();seen=true;try{inspect(f);}catch(Exception e){error=e;}f.DialogResult=DialogResult.Cancel;f.Close();break;}};timer.Start();Call(owner,method,args);}Check(seen,"dialog opened "+method);if(error!=null)throw error;}
 static void Dialogs(){foreach(string lang in Loc.Codes){Loc.Language=lang;using(var main=new MainForm(Path.Combine(dir,lang+"-dialog-prefs.xml"))){Show(main);Field<ComboBox>(main,"language").SelectedIndex=Array.IndexOf(Loc.Codes,lang);Check(Loc.Language==lang,"dialog language applied");foreach(bool dark in new[]{true,false})foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
  typeof(MainForm).GetField("dark",flags).SetValue(main,dark);Call(main,"ApplyTheme");foreach(string method in new[]{"StudioPreferences","EditTextDialog"})Dialog(main,method,method=="EditTextDialog"?new object[]{0}:new object[0],f=>{
   Check(f.AutoScaleMode==AutoScaleMode.Dpi,"DPI-aware dialog: "+method);f.Scale(new SizeF(scale,scale));f.Font=UiFonts.Get(UiFonts.Default.Size*scale);f.MinimumSize=Size.Empty;f.ClientSize=new Size(460,280);Application.DoEvents();var close=(Control)f.CancelButton;Check(close!=null,"dialog Escape action "+method);Rectangle r=f.RectangleToClient(close.RectangleToScreen(close.ClientRectangle));Check(r.Left>=0&&r.Right<=f.ClientSize.Width&&r.Top>=0&&r.Bottom<=f.ClientSize.Height,"dialog action outside viewport "+method+" @ "+scale+" / "+r+" / "+f.ClientSize+" / "+close.Parent.Bounds);Check(All<Panel>(f).Exists(p=>p.AutoScroll),"scrollable dialog content "+method);Check(close.Focus(),"dialog action keyboard reachable");if(scale==2&&lang=="hu"&&dark)Snap(f,method+"-hu-200");
  });Check(Field<ComboBox>(main,"language").Parent!=null&&!Field<ComboBox>(main,"language").IsDisposed,"language control restored after dialog");}
  Call(main,"Apply",LabelSettings.Default());main.Close();
 }}}
 static void Failures(){var type=typeof(MainForm).Assembly.GetType("EanEtikety.UiFailure");Check(type!=null,"actionable error guidance exists");var method=type.GetMethod("Describe");foreach(string lang in Loc.Codes){Loc.Language=lang;foreach(Exception error in new Exception[]{new UnauthorizedAccessException("detail-marker"),new IOException("detail-marker"),new FormatException("detail-marker"),new Exception("detail-marker")}){string text=(string)method.Invoke(null,new object[]{error,"diag-marker"});Check(text.Contains("detail-marker")&&text.Contains("diag-marker"),"failure retains detail and reference");Check(text.Contains(Loc.T("Podrobnosti")),"failure labels detail");Check(text.Length>100,"failure includes guidance");Check(lang=="sk"||!text.StartsWith("Nemáte oprávnenie")&&!text.StartsWith("Súbor alebo zariadenie")&&!text.StartsWith("Údaje sa nepodarilo")&&!text.StartsWith("Operáciu sa nepodarilo"),"failure explanation translated");}}}
 static void Resizing(){using(var f=new Form{ClientSize=new Size(240,180)}){var button=new ModernButton{Text="Text & focus"};var combo=new ModernComboBox{Text="Text"};f.Controls.Add(button);f.Controls.Add(combo);Show(f);foreach(int size in new[]{1,3,4,8,12,24,100}){button.Size=new Size(size,size);combo.Size=new Size(size,size);using(var b=new Bitmap(240,180))f.DrawToBitmap(b,new Rectangle(0,0,240,180));Check(true,"tiny control paints safely");}int redraws=0;combo.Invalidated+=delegate{redraws++;};combo.Focus();button.Focus();Check(redraws>=2,"combo focus transition invalidates custom surface");f.Close();}}
 [STAThread]static void Main(string[] args){dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Management();Tabs();Messages();Editor();Dialogs();Failures();Resizing();File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" accessibility/layout/keyboard checks; synthetic scaling and contrast, no physical monitor transition or printing");}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}finally{Loc.Language="sk";}}
}
