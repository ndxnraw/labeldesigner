using System;
using System.IO;

static class TestPaths {
 public static string RepositoryRoot {
  get {
   foreach(string start in new[]{Environment.CurrentDirectory,AppDomain.CurrentDomain.BaseDirectory}){
    var directory=new DirectoryInfo(start);
    while(directory!=null){
     if(File.Exists(Path.Combine(directory.FullName,"src","LabelDesigner","App.cs"))&&File.Exists(Path.Combine(directory.FullName,"assets","app.ico")))return directory.FullName;
     directory=directory.Parent;
    }
   }
   throw new DirectoryNotFoundException("Run the regression tests from a LabelDesigner checkout.");
  }
 }
}
