using System;
using System.Diagnostics;
using System.IO;
using EanEtikety;

class UpdatePackageCheck {
 static int Main(string[] args) {
  string results = Path.GetFullPath(args[0]);
  Directory.CreateDirectory(results);
  try {
   string zip = Path.GetFullPath(args[1]);
   var manifest = UpdatePreparation.ReadArchiveManifest(zip);
   string hash = UpdatePreparation.ReadChecksum(args[2], Path.GetFileName(zip));
   var index = UpdateFiles.ReadXml<UpdateIndex>(args[3], 512 * 1024);
   index.Validate();
   var offer = index.Packages[0];
   string installed = UpdatePreparation.Prepare(zip, hash, UpdateFiles.CurrentFamily, Path.GetFullPath(args[4]), offer);
   var smoke = Path.Combine(results, "self-test");
   Directory.CreateDirectory(smoke);
   var info = new ProcessStartInfo {
    FileName = Path.Combine(installed, "ND-LabelDesigner.exe"),
    WorkingDirectory = installed,
    WindowStyle = ProcessWindowStyle.Hidden,
    UseShellExecute = false
   };
   info.ArgumentList.Add("--self-test");
   info.ArgumentList.Add(smoke);
   info.Environment["DOTNET_ROOT"] = Path.Combine(results, "no-runtime");
   info.Environment["DOTNET_ROOT_X64"] = info.Environment["DOTNET_ROOT"];
   info.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
   using (var process = Process.Start(info)) {
    if (!process.WaitForExit(180000)) {
     process.Kill();
     throw new Exception("Extracted package self-test timeout.");
    }
    if (process.ExitCode != 0 || !File.Exists(Path.Combine(smoke, "passed.txt")))
     throw new Exception("Extracted package failed self-test.");
   }
   string message = "PASS managed update package: " + manifest.Family;
   File.WriteAllText(Path.Combine(results, "passed.txt"), message);
   Console.WriteLine(message);
   return 0;
  } catch (Exception error) {
   File.WriteAllText(Path.Combine(results, "failed.txt"), error.ToString());
   Console.Error.WriteLine(error);
   return 1;
  }
 }
}
