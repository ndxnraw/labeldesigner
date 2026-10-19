using System;using System.IO;using System.Drawing;using System.Reflection;using System.Runtime.InteropServices;using System.Windows.Forms;using EanEtikety;
class Revision308Tests {
 static string dir;static int checks,nativeChecks;static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
 static object Call(object o,string name,params object[] a){return o.GetType().GetMethod(name,flags).Invoke(o,a);}
 static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static string IconData(Icon icon){using(var stream=new MemoryStream()){icon.Save(stream);return Convert.ToBase64String(stream.ToArray());}}
 static int ColorRef(Color color){return color.R|(color.G<<8)|(color.B<<16);}
 static void Verify(Form form,bool dark,string name){
  Check(form.ShowIcon&&IconData(form.Icon)==Logo,"application logo: "+name);
  Check(form.FormBorderStyle!=FormBorderStyle.None&&form.ControlBox,"native frame preserved: "+name);
  int value;if(DwmGetWindowAttribute(form.Handle,35,out value,4)==0){nativeChecks++;Check(value==(SystemInformation.HighContrast?-1:ColorRef(StudioTheme.Panel(dark))),"native caption color: "+name);}
  if(DwmGetWindowAttribute(form.Handle,20,out value,4)==0){nativeChecks++;Check(value==(dark&&!SystemInformation.HighContrast?1:0),"native dark mode: "+name);}
 }
 static string Logo;
 static void Dialog(MainForm owner,bool dark,string method,params object[] args){Exception failure=null;bool seen=false;using(var timer=new Timer{Interval=60}){
  timer.Tick+=delegate{Form dialog=null;foreach(Form f in Application.OpenForms)if(f!=owner&&f.Visible){dialog=f;break;}if(dialog==null)return;timer.Stop();seen=true;try{
   Verify(dialog,dark,method);Call(owner,"ToggleTheme");Call(owner,"ApplyColors",dialog);Verify(dialog,!dark,method+" theme switch");Call(owner,"ToggleTheme");Call(owner,"ApplyColors",dialog);
   typeof(Form).GetMethod("RecreateHandle",flags).Invoke(dialog,null);Verify(dialog,dark,method+" recreated handle");
  }catch(Exception ex){failure=ex;}finally{dialog.DialogResult=DialogResult.Cancel;dialog.Close();}};
  timer.Start();Call(owner,method,args);
 }Check(seen,"dialog opened: "+method);if(failure!=null)throw failure;}
 [STAThread]static void Main(string[] args){dir=args[0];Directory.CreateDirectory(dir);try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var stream=typeof(MainForm).Assembly.GetManifestResourceStream("AppLogo.ico"))using(var icon=new Icon(stream))Logo=IconData(icon);
  using(var main=new MainForm(Path.Combine(dir,"prefs.xml"))){main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.Show();
   foreach(bool dark in new[]{true,false}){
    typeof(MainForm).GetField("dark",flags).SetValue(main,dark);Call(main,"ApplyTheme");Verify(main,dark,"main");
    foreach(MessageBoxIcon icon in new[]{MessageBoxIcon.None,MessageBoxIcon.Information,MessageBoxIcon.Warning,MessageBoxIcon.Error})using(var message=UiMessageBox.Create(main,"Test","O aplikácii",MessageBoxButtons.YesNoCancel,icon)){
     message.StartPosition=FormStartPosition.Manual;message.Location=new Point(-20000,-20000);message.Show(main);Verify(message,dark,"message "+icon);Check(message.AcceptButton!=null&&message.CancelButton!=null,"message keyboard actions retained");message.Close();
    }
    Dialog(main,dark,"StudioPreferences");Dialog(main,dark,"EditTextDialog",0);Dialog(main,dark,"EditGs1");Dialog(main,dark,"EditSymbol",-2);
    var preview=(PreviewPanel)typeof(MainForm).GetField("preview",flags).GetValue(main);preview.SelectElement(0);Dialog(main,dark,"CustomRotation");
    Dialog(main,dark,"ShowZpl","test.zpl","^XA^PW400^LL300^FO10,10^GB50,50,2^FS^XZ");
    using(var print=(Form)Call(main,"CreatePrintWindow",LabelSettings.Default())){Verify(print,dark,"print");}
   }
   Call(main,"Apply",LabelSettings.Default());main.Close();
  }
  File.WriteAllText(Path.Combine(dir,"passed.txt"),"PASS "+checks+" checks; "+nativeChecks+" native DWM attributes: logo, native frame, dark/light title bars, messages, preferences, text, GS1, symbols, custom rotation, ZPL, print, live theme change and recreated handles. No physical printing.");
 }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"failed.txt"),ex.ToString());Environment.ExitCode=1;}}
}
