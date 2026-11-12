using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace EanEtikety {
 public static class DiagnosticLog {
  static readonly object gate=new object();public const int MaxBytes=1024*1024;
  public static string DirectoryPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NDLabelMaker","Diagnostics");}}
  public static string Write(string operation,Exception error){return WriteTo(DirectoryPath,operation,error);}
  public static string WriteTo(string directory,string operation,Exception error){
   string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
   // Exception messages, file paths, printer names and document data are omitted.
   var text=new StringBuilder();text.AppendLine("ID="+id);text.AppendLine("UTC="+DateTime.UtcNow.ToString("o"));
   text.AppendLine("Operation="+SafeOperation(operation));text.AppendLine("Assembly="+typeof(MainForm).Assembly.GetName().Version);text.AppendLine("Product=Pre-release");text.AppendLine("Build="+typeof(MainForm).Module.ModuleVersionId);text.AppendLine("Runtime="+Environment.Version);text.AppendLine("Windows="+Environment.OSVersion.Version);
   for(int depth=0;error!=null&&depth<4;depth++,error=error.InnerException){
    text.AppendLine("Exception="+error.GetType().FullName+" HResult="+error.HResult.ToString("X8"));
    var frames=new StackTrace(error,false).GetFrames();if(frames!=null)for(int i=0;i<Math.Min(24,frames.Length);i++){var method=frames[i].GetMethod();text.AppendLine(" at "+(method.DeclaringType==null?"":method.DeclaringType.FullName+".")+method.Name);}
   }
   text.AppendLine();
   try{lock(gate){Directory.CreateDirectory(directory);string path=Path.Combine(directory,"application.log");if(File.Exists(path)&&new FileInfo(path).Length+Encoding.UTF8.GetByteCount(text.ToString())>MaxBytes){string oldest=path+".2",recent=path+".1";if(File.Exists(oldest))File.Delete(oldest);if(File.Exists(recent))File.Move(recent,oldest);File.Move(path,recent);}File.AppendAllText(path,text.ToString(),new UTF8Encoding(false));}return id;}
   catch{ return null; /* Diagnostics must never hide the original failure. */ }
  }
  static string SafeOperation(string value){if(string.IsNullOrEmpty(value)||value.Length>60)return "application";foreach(char c in value)if(!(c>='a'&&c<='z')&&c!='-')return "application";return value;}
  public static string ReadRecent(){lock(gate){string path=Path.Combine(DirectoryPath,"application.log");if(!File.Exists(path))return "";string text=File.ReadAllText(path);return text.Length>100000?text.Substring(text.Length-100000):text;}}
 }
 public partial class MainForm {
  void ShowDiagnostics(){using(var window=FeatureForm("Diagnostika",800,540)){
   var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=DiagnosticLog.ReadRecent()};window.Controls.Add(text);
   var save=Btn(Loc.T("Uložiť diagnostiku…"),delegate{try{using(var dialog=new SaveFileDialog{Filter="Text|*.txt",FileName="ND-LabelDesigner-diagnostics.txt"})if(dialog.ShowDialog(window)==DialogResult.OK)AtomicOutput.Save(dialog.FileName,p=>File.WriteAllText(p,text.Text,new UTF8Encoding(false)));}catch(Exception ex){Error(ex);}});save.Dock=DockStyle.Bottom;window.Controls.Add(save);ApplyColors(window);window.ShowDialog(this);
  }}
 }
}
