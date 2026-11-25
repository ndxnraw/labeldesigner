using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Run unchanged regression entry points with the same startup DPI as the product.
static class MigrationTestHost {
 [STAThread]static int Main(string[] args){
  try{
   Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
   var assembly=Assembly.GetExecutingAssembly();
   var suite=assembly.GetType(assembly.GetName().Name,true);
   suite.GetMethod("Main",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{args});
   return Environment.ExitCode;
  }catch(Exception ex){if(ex is TargetInvocationException&&ex.InnerException!=null)ex=ex.InnerException;if(args.Length>0){Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"failed.txt"),ex.ToString());}return 1;}
 }
}
