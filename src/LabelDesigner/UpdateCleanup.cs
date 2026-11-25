using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace EanEtikety {
 public sealed class UpdateCleanupResult {
  public int Count {get;internal set;}
  public long Bytes {get;internal set;}
 }

 public sealed partial class UpdateActivation {
  public UpdateCleanupResult Cleanup(string runningDirectory,string preparedDirectory=null) {
   WorkContext.Check();
   if(!Directory.Exists(PackagesRoot))return new UpdateCleanupResult();
   UpdateFiles.NoReparse(Root);
   using var lease=new FileStream(Path.Combine(Root,"activation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
   return CleanupVerified(State(),runningDirectory,preparedDirectory);
  }

  UpdateCleanupResult CleanupVerified(UpdateState state,string runningDirectory,string preparedDirectory) {
   var result=new UpdateCleanupResult();
   if(state.Current==null||!Directory.Exists(PackagesRoot))return result;
   UpdateFiles.NoReparse(Root);
   UpdateFiles.NoReparse(PackagesRoot);
   var protectedRoots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   void Protect(string path) {
    if(!string.IsNullOrWhiteSpace(path))protectedRoots.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
   }
   Protect(runningDirectory);
   Protect(preparedDirectory);
   Protect(state.Current.Directory);
   Protect(state.Previous?.Directory);
   string backupPath=StatePath+".bak";
   if(File.Exists(backupPath)){
    UpdateFiles.NoReparse(backupPath);
    var backup=UpdateFiles.ReadXml<UpdateState>(backupPath,128*1024);
    ValidateState(backup);
    Protect(backup.Current?.Directory);
    Protect(backup.Previous?.Directory);
   }

   foreach(string recorded in state.AuthorizedRoots) {
    WorkContext.Check();
    string directory=Path.TrimEndingDirectorySeparator(Path.GetFullPath(recorded));
    string id=Path.GetFileName(directory);
    if(protectedRoots.Contains(directory)||!UpdateFiles.Hex(id,64)||
       !SamePath(Path.GetDirectoryName(directory),PackagesRoot)||!Directory.Exists(directory))continue;
    try {
     string manifestPath=Path.Combine(directory,"package-manifest.xml");
     UpdateFiles.NoReparseParents(PackagesRoot,manifestPath);
     var manifest=UpdatePackage.Read(manifestPath);
     if(manifest.Family!=Family||manifest.PackageId!=id)continue;
     manifest.VerifyDirectory(directory);
     long bytes=new FileInfo(manifestPath).Length;
     foreach(var file in manifest.Files)bytes+=file.Size;
     WorkContext.Check();
     if(IsRunningBuild(directory))continue;

     // Detach the verified package from its public identity before removing its files.
     string retired=Path.Combine(PackagesRoot,".pending-cleanup-"+Guid.NewGuid().ToString("N"));
     Directory.Move(directory,retired);
     UpdateFiles.DeletePending(PackagesRoot,retired);
     result.Count++;
     result.Bytes+=bytes;
    } catch(IOException) {
     // A locked or concurrently changed package is not eligible for removal.
    } catch(UnauthorizedAccessException) {
    } catch(InvalidDataException) {
    } catch(InvalidOperationException ex) when(ex.InnerException is System.Xml.XmlException or InvalidDataException or FormatException or OverflowException) {
    }
   }
   return result;
  }

  static bool IsRunningBuild(string directory) {
   var processes=Process.GetProcessesByName("ND-LabelDesigner");
   try {
    foreach(var process in processes) {
     try {
      string path=process.MainModule?.FileName;
      if(path==null||SamePath(Path.GetDirectoryName(path),directory))return true;
     } catch(Win32Exception) {
      if(!process.HasExited)return true;
     } catch(InvalidOperationException) {
      // The process can exit while its executable path is being read.
     }
    }
    return false;
   } finally {foreach(var process in processes)process.Dispose();}
  }
 }
}
