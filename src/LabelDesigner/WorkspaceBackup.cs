using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace EanEtikety {
 [XmlRoot("LabelDesignerWorkspaceBackup")]
 public sealed class WorkspaceBackupManifest {
  public int Format;
  public bool Templates,Profiles,Batches;
  public List<UpdateFile> Files=new List<UpdateFile>();
 }

 public enum WorkspaceRestoreAction { Add,Keep,Replace,Invalid }

 public sealed class WorkspaceRestoreItem {
  public string Name {get;private set;}
  public string SourcePrinter {get;private set;}
  public string TargetPrinter {get;private set;}
  public WorkspaceRestoreAction Action {get;private set;}
  public string BatchId {get;internal set;}
  public bool BatchConflict {get;internal set;}
  public string LocalBatchSummary {get;internal set;}
  public string BackupBatchSummary {get;internal set;}
  public string BatchDetails {get;internal set;}
  internal WorkspaceRestoreItem(string name,string sourcePrinter,string targetPrinter,WorkspaceRestoreAction action){
   Name=name;SourcePrinter=sourcePrinter;TargetPrinter=targetPrinter;Action=action;
  }
 }

 public sealed class WorkspaceRestorePlan {
  public readonly List<WorkspaceRestoreItem> Items=new List<WorkspaceRestoreItem>();
  internal readonly WorkspaceBackupState Before;
  internal readonly string Root;
  internal readonly List<WorkspaceBackupFile> Writes=new List<WorkspaceBackupFile>();
  internal readonly List<PrintBatchBackup> BatchWrites=new List<PrintBatchBackup>();
  internal readonly string PrintRoot;
  internal WorkspaceRestorePlan(string root,string printRoot,WorkspaceBackupState before){Root=root;PrintRoot=printRoot;Before=before;}
 }

 public sealed class WorkspaceRestoreResult {
  public readonly int Applied;
  public readonly string BackupPath;
  internal WorkspaceRestoreResult(int applied,string backupPath){Applied=applied;BackupPath=backupPath;}
 }

 internal sealed class WorkspaceBackupFile {
  public readonly string Name,Hash;
  public readonly byte[] Bytes;
  public WorkspaceBackupFile(string name,byte[] bytes){Name=name;Bytes=bytes;Hash=UpdateFiles.Digest(bytes);}
 }

 internal sealed class WorkspaceBackupState {
  public readonly bool Templates,Profiles,Batches;
  public readonly Dictionary<string,WorkspaceBackupFile> Files=new Dictionary<string,WorkspaceBackupFile>(StringComparer.OrdinalIgnoreCase);
  public readonly HashSet<string> InvalidTemplates=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public List<PrinterProfile> Media=new List<PrinterProfile>();
  public List<PrintBatchBackup> PrintBatches=new List<PrintBatchBackup>();
  public WorkspaceBackupState(bool templates,bool profiles,bool batches=false){Templates=templates;Profiles=profiles;Batches=batches;}
 }

 public sealed class WorkspaceRestoreReview {
  readonly string root,printRoot;
  readonly WorkspaceBackupState incoming,current;
  public readonly List<string> SourcePrinters=new List<string>();

  internal WorkspaceRestoreReview(string root,string printRoot,WorkspaceBackupState incoming,WorkspaceBackupState current) {
   this.root=root;this.printRoot=printRoot;this.incoming=incoming;this.current=current;
   var printers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var profile in incoming.Media)if(printers.Add(profile.Printer))SourcePrinters.Add(profile.Printer);
   SourcePrinters.Sort(StringComparer.CurrentCultureIgnoreCase);
  }

  public WorkspaceRestorePlan Preview(bool replace,IDictionary<string,string> printerMap=null,ISet<string> useBatchBackup=null) {
   var plan=new WorkspaceRestorePlan(root,printRoot,current);
   var paths=new List<string>(incoming.Files.Keys);
   paths.Sort(StringComparer.OrdinalIgnoreCase);
   foreach(string path in paths) {
    if(path==WorkspaceBackup.ProfilesFile||path==WorkspaceBackup.BatchesFile)continue;
    if(incoming.InvalidTemplates.Contains(path)){
     plan.Items.Add(new WorkspaceRestoreItem(Path.GetFileName(path),null,null,WorkspaceRestoreAction.Invalid));
     continue;
    }
    WorkspaceBackupFile previous;
    bool exists=current.Files.TryGetValue(path,out previous);
    var action=Decision(exists,replace);
    plan.Items.Add(new WorkspaceRestoreItem(Path.GetFileName(path),null,null,action));
    if(action!=WorkspaceRestoreAction.Keep) {
     var file=incoming.Files[path];
     plan.Writes.Add(exists?new WorkspaceBackupFile(previous.Name,file.Bytes):file);
    }
   }
   var media=new List<PrinterProfile>(current.Media);
   var mapped=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   bool mediaChanges=false;
   foreach(var source in incoming.Media) {
    var profile=source.Copy();
    string printer;
    if(printerMap!=null&&printerMap.TryGetValue(source.Printer,out printer))profile.Printer=printer;
    WorkspaceBackup.ValidatePrinterName(profile.Printer);
    string identity=WorkspaceBackup.ProfileKey(profile);
    if(!mapped.Add(identity))throw new InvalidDataException(Loc.T("Priradenie tlačiarní vytvára dva profily s rovnakým názvom."));
    int index=media.FindIndex(p=>string.Equals(WorkspaceBackup.ProfileKey(p),identity,StringComparison.OrdinalIgnoreCase));
    var action=Decision(index>=0,replace);
    plan.Items.Add(new WorkspaceRestoreItem(profile.DisplayName,source.Printer,profile.Printer,action));
    if(action==WorkspaceRestoreAction.Keep)continue;
    if(index>=0)media[index]=profile;else media.Add(profile);
    mediaChanges=true;
   }
   if(mediaChanges)plan.Writes.Add(WorkspaceBackup.EncodeProfiles(media));
   if(incoming.Batches)WorkspaceBatchRestore.Preview(current.PrintBatches,incoming.PrintBatches,useBatchBackup,plan);
   WorkspaceBackup.CheckMergedSize(current,plan.Writes);
   return plan;
  }

  static WorkspaceRestoreAction Decision(bool exists,bool replace) {
   return !exists?WorkspaceRestoreAction.Add:replace?WorkspaceRestoreAction.Replace:WorkspaceRestoreAction.Keep;
  }
 }

 public sealed class WorkspaceBackup {
  internal const string ProfilesFile="printers.xml";
  internal const string BatchesFile="print-batches.xml";
  const string ManifestFile="workspace-backup.xml";
  const string TemplatesPrefix="Templates/";
  const int MaxFiles=512,MaxProfiles=1024,MaxManifest=512*1024,MaxTemplate=64*1024*1024,MaxProfileBytes=4*1024*1024;
  const int MaxBatchBytes=64000000;
  const long MaxTotal=256L*1024*1024;
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  readonly string root;
  readonly PrintHistoryStore printHistory;
  string PrintRoot { get { return printHistory==null?null:printHistory.BackupDirectory; } }

  public WorkspaceBackup(string directory,PrintHistoryStore printHistory=null){
   root=Path.GetFullPath(directory).TrimEnd('\\','/');
   if(string.Equals(root,Path.GetPathRoot(root).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("directory");
   this.printHistory=printHistory;
  }

  public void Export(string file,bool templates,bool profiles,bool batches=false) {
   if(!templates&&!profiles&&!batches)throw InvalidBackup();
   if(!string.Equals(Path.GetExtension(file),".zip",StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException(Loc.T("Zálohu uložte ako súbor ZIP."));
   using(Lock()){
    var state=Capture(templates,profiles,batches);
    ValidateTemplates(state,false);
    SaveArchive(file,state);
   }
  }

  public WorkspaceRestoreReview Open(string file) {
   var incoming=ReadArchive(file);
   using(Lock()){
    var current=Capture(incoming.Templates,incoming.Profiles,incoming.Batches);
    foreach(string name in incoming.Files.Keys)
     if(name!=BatchesFile&&!incoming.InvalidTemplates.Contains(name)&&Directory.Exists(UpdateFiles.Child(root,name)))throw PathConflict(name);
    return new WorkspaceRestoreReview(root,PrintRoot,incoming,current);
   }
  }

  public WorkspaceRestoreResult Restore(WorkspaceRestorePlan plan) {
   if(plan==null||!string.Equals(root,plan.Root,StringComparison.OrdinalIgnoreCase)||
      !string.Equals(PrintRoot,plan.PrintRoot,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("plan");
   using(Lock())
   using(plan.Before.Batches ? printHistory.LockBatchBackup(plan.BatchWrites.Select(batch=>batch.BatchId)) : null){
    var current=Capture(plan.Before.Templates,plan.Before.Profiles,plan.Before.Batches);
    if(current.Files.Count!=plan.Before.Files.Count)throw Changed();
    foreach(var file in plan.Before.Files.Values)VerifySnapshot(current,file);
    var writes=new List<WorkspaceRestoreWrite>();
    foreach(var file in plan.Writes){
     WorkspaceBackupFile old;
     current.Files.TryGetValue(file.Name,out old);
     writes.Add(WorkspaceRestoreWrite.ForWorkspace(root,old,file));
    }
    if(plan.BatchWrites.Count>0){
     var selected=plan.BatchWrites.ToDictionary(batch=>batch.BatchId,StringComparer.Ordinal);
     var previous=current.PrintBatches.ToDictionary(batch=>batch.BatchId,StringComparer.Ordinal);
     var combined=current.PrintBatches.Where(item=>!selected.ContainsKey(item.BatchId)).Concat(plan.BatchWrites).ToList();
     var limitCheck=new List<WorkspaceBackupFile>(plan.Writes) { EncodeBatches(combined) };
     CheckMergedSize(current,limitCheck);
     foreach(var batch in plan.BatchWrites){
      PrintBatchBackup old;
      previous.TryGetValue(batch.BatchId,out old);
      printHistory.BatchRestoreWrites(old,batch,writes);
     }
    }
    if(writes.Count==0)return new WorkspaceRestoreResult(0,null);

    string backups=root+".backups";
    Directory.CreateDirectory(backups);
    UpdateFiles.NoReparse(backups);
    string checkpoint=Path.Combine(backups,"before-restore-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")+".zip");
    SaveArchive(checkpoint,current);
    WorkContext.Report("Obnovujem pracovné údaje…",0,writes.Count);
    var completed=new List<WorkspaceRestoreWrite>(writes.Count);
    // A cancellation before the commit leaves the destination untouched.
    // Once writes start, finish them or roll them back as a single operation.
    using(new WorkContext().Enter()){
     try{
      foreach(var file in writes){
       file.Apply();
       completed.Add(file);
      }
     }catch(Exception failure){
      var rollback=new List<Exception>();
      for(int i=completed.Count-1;i>=0;i--){
       var file=completed[i];
       try{file.RollBack();}catch(Exception ex){rollback.Add(ex);}
      }
      if(rollback.Count>0){
       rollback.Insert(0,failure);
       throw new IOException(Loc.T("Obnova sa nedokončila. Záloha je uložená tu:")+Environment.NewLine+checkpoint,
        new AggregateException(rollback));
      }
      throw;
     }
    }
    return new WorkspaceRestoreResult(plan.Items.FindAll(item=>item.Action==WorkspaceRestoreAction.Add||item.Action==WorkspaceRestoreAction.Replace).Count,checkpoint);
   }
  }

  WorkspaceBackupState Capture(bool templates,bool profiles,bool batches) {
   if(File.Exists(root))throw PathConflict(root);
   if(Directory.Exists(root))UpdateFiles.NoReparse(root);
   var state=new WorkspaceBackupState(templates,profiles,batches);
   long total=0;
   string library=UpdateFiles.Child(root,"Templates");
   if(templates&&File.Exists(library))throw PathConflict("Templates");
   if(templates&&Directory.Exists(library)){
    UpdateFiles.NoReparse(library);
    var paths=Directory.GetFiles(library,"*.ean.xml");
    if(paths.Length>MaxFiles)throw InvalidBackup();
    for(int i=0;i<paths.Length;i++){
     WorkContext.Report("Čítam pracovné údaje…",i,paths.Length);
     string name=TemplatesPrefix+Path.GetFileName(paths[i]);
     ValidateName(name,state);
     long size=new FileInfo(paths[i]).Length;
     total=CheckTotal(total,size);
     Add(state,ReadFile(paths[i],name,MaxTemplate));
    }
   }
   string profilePath=UpdateFiles.Child(root,ProfilesFile);
   if(profiles&&Directory.Exists(profilePath))throw PathConflict(ProfilesFile);
   if(profiles&&File.Exists(profilePath)){
    if(state.Files.Count>=MaxFiles)throw InvalidBackup();
    total=CheckTotal(total,new FileInfo(profilePath).Length);
    var file=ReadFile(profilePath,ProfilesFile,MaxProfileBytes);
    Add(state,file);
    state.Media=ReadProfiles(file.Bytes);
   }
   if(batches){
    if(printHistory==null)throw new InvalidOperationException(Loc.T("Úložisko dávok nie je dostupné."));
    state.PrintBatches=printHistory.CaptureBatchBackup();
    var batchFile=EncodeBatches(state.PrintBatches);
    CheckTotal(total,batchFile.Bytes.LongLength);
    Add(state,batchFile);
   }
   return state;
  }

  static WorkspaceBackupFile ReadFile(string path,string name,int limit) {
   UpdateFiles.NoReparse(path);
   using(var stream=File.OpenRead(path))return new WorkspaceBackupFile(name,ReadBytes(stream,stream.Length,limit));
  }

  static WorkspaceBackupState ReadArchive(string path) {
   if(new FileInfo(path).Length>MaxTotal)throw InvalidBackup();
   using(var zip=ZipFile.OpenRead(path)){
    if(zip.Entries.Count<1||zip.Entries.Count>MaxFiles+1)throw InvalidBackup();
    var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
    foreach(var entry in zip.Entries){
     UpdateFiles.SafeName(entry.FullName);
     int attributes=entry.ExternalAttributes,mode=(attributes>>16)&0xF000;
     if(entries.ContainsKey(entry.FullName)||(attributes&0x410)!=0||mode!=0&&mode!=0x8000)throw InvalidBackup();
     entries.Add(entry.FullName,entry);
    }
    ZipArchiveEntry metadata;
    if(!entries.TryGetValue(ManifestFile,out metadata)||metadata.FullName!=ManifestFile)throw InvalidBackup();
    WorkspaceBackupManifest manifest;
    using(var stream=metadata.Open())
     manifest=ProjectXml.Decode<WorkspaceBackupManifest>(Utf8.GetString(ReadBytes(stream,metadata.Length,MaxManifest)));
    if((manifest.Format!=1&&manifest.Format!=2)||(manifest.Format==1&&manifest.Batches)||(manifest.Format==2&&!manifest.Batches)||
       (!manifest.Templates&&!manifest.Profiles&&!manifest.Batches)||manifest.Files==null||
       manifest.Files.Count!=entries.Count-1)throw InvalidBackup();
    var state=new WorkspaceBackupState(manifest.Templates,manifest.Profiles,manifest.Batches);
    long total=0;
    for(int i=0;i<manifest.Files.Count;i++){
     WorkContext.Report("Overujem zálohu…",i,manifest.Files.Count);
     var item=manifest.Files[i];
     if(item==null||!UpdateFiles.Hex(item.Sha256,64))throw InvalidBackup();
     ValidateName(item.Path,state);
     ZipArchiveEntry entry;
     if(!entries.TryGetValue(item.Path,out entry)||entry.FullName!=item.Path||entry.Length!=item.Size)throw InvalidBackup();
     total=CheckTotal(total,item.Size);
     using(var input=entry.Open()){
      int limit=item.Path==ProfilesFile?MaxProfileBytes:item.Path==BatchesFile?MaxBatchBytes:MaxTemplate;
      var blob=new WorkspaceBackupFile(item.Path,ReadBytes(input,item.Size,limit));
      if(blob.Hash!=item.Sha256)throw InvalidBackup();
      Add(state,blob);
     }
    }
    WorkspaceBackupFile profiles;
    if(state.Files.TryGetValue(ProfilesFile,out profiles))state.Media=ReadProfiles(profiles.Bytes);
    if(state.Batches){
     WorkspaceBackupFile batches;
     if(!state.Files.TryGetValue(BatchesFile,out batches))throw InvalidBackup();
     var collection=ProjectXml.Decode<PrintBatchBackupCollection>(Utf8.GetString(batches.Bytes));
     if(collection==null)throw InvalidBackup();
     collection.Validate(true);
     state.PrintBatches=collection.Batches;
    }
   ValidateTemplates(state,true);
    return state;
   }
  }

  static void ValidateName(string name,WorkspaceBackupState state) {
   UpdateFiles.SafeName(name);
   if(state.Profiles&&name==ProfilesFile)return;
   if(state.Batches&&name==BatchesFile)return;
   if(!state.Templates||!name.StartsWith(TemplatesPrefix,StringComparison.Ordinal)||
      name.IndexOf('/',TemplatesPrefix.Length)>=0||!name.EndsWith(".ean.xml",StringComparison.OrdinalIgnoreCase))throw InvalidBackup();
  }

  static void Add(WorkspaceBackupState state,WorkspaceBackupFile file) {
   if(state.Files.ContainsKey(file.Name))throw InvalidBackup();
   state.Files.Add(file.Name,file);
  }

  static void ValidateTemplates(WorkspaceBackupState state,bool allowInvalid) {
   foreach(var file in state.Files.Values){
    WorkContext.Check();
    if(file.Name==ProfilesFile||file.Name==BatchesFile)continue;
    try{using(var memory=new MemoryStream(file.Bytes,false))using(var stream=new CheckedReadStream(memory))Engine.Load(stream);}
    catch(OperationCanceledException){throw;}
    catch(Exception ex){
     if(!allowInvalid)throw new InvalidDataException(Loc.T("Poškodená šablóna:")+" "+Path.GetFileName(file.Name),ex);
     state.InvalidTemplates.Add(file.Name);
    }
   }
  }

  static byte[] ReadBytes(Stream input,long size,int max) {
   if(size<=0||size>max)throw InvalidBackup();
   var bytes=new byte[(int)size];
   int offset=0;
   while(offset<bytes.Length){
    WorkContext.Check();
    int count=input.Read(bytes,offset,Math.Min(32768,bytes.Length-offset));
    if(count==0)throw InvalidBackup();
    offset+=count;
   }
   if(input.ReadByte()!=-1)throw InvalidBackup();
   return bytes;
  }

  static long CheckTotal(long total,long size) {
   if(size<=0||size>MaxTotal-total)throw InvalidBackup();
   return total+size;
  }

  internal static void CheckMergedSize(WorkspaceBackupState current,List<WorkspaceBackupFile> writes) {
   long size=0;
   foreach(var file in current.Files.Values)size+=file.Bytes.LongLength;
   int count=current.Files.Count;
   foreach(var file in writes){
    WorkspaceBackupFile old;
    if(current.Files.TryGetValue(file.Name,out old))size-=old.Bytes.LongLength;
    else count++;
    size+=file.Bytes.LongLength;
   }
   if(size>MaxTotal||count>MaxFiles)throw InvalidBackup();
  }

  static void SaveArchive(string path,WorkspaceBackupState state) {
   var manifest=new WorkspaceBackupManifest{Format=state.Batches?2:1,Templates=state.Templates,Profiles=state.Profiles,Batches=state.Batches};
   var names=new List<string>(state.Files.Keys);
   names.Sort(StringComparer.Ordinal);
   long total=0;
   foreach(string name in names){
    var file=state.Files[name];
    total=CheckTotal(total,file.Bytes.LongLength);
    manifest.Files.Add(new UpdateFile{Path=name,Size=file.Bytes.LongLength,Sha256=file.Hash});
   }
   var metadata=Utf8.GetBytes(ProjectXml.Encode(manifest));
   if(names.Count>MaxFiles||metadata.Length>MaxManifest)throw InvalidBackup();
   AtomicOutput.Save(path,temp=>{
    using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){
     WriteEntry(zip,ManifestFile,metadata);
     for(int i=0;i<names.Count;i++){
      WorkContext.Report("Vytváram zálohu…",i,names.Count);
      WriteEntry(zip,names[i],state.Files[names[i]].Bytes);
     }
    }
    if(new FileInfo(temp).Length>MaxTotal)throw InvalidBackup();
   });
  }

  static void WriteEntry(ZipArchive zip,string name,byte[] bytes) {
   using(var stream=zip.CreateEntry(name,CompressionLevel.Optimal).Open())
    for(int offset=0;offset<bytes.Length;offset+=32768){
     WorkContext.Check();
     stream.Write(bytes,offset,Math.Min(32768,bytes.Length-offset));
    }
  }

  static List<PrinterProfile> ReadProfiles(byte[] bytes) {
   List<PrinterProfile> profiles;
   using(var stream=new MemoryStream(bytes,false))
   using(var reader=new StreamReader(stream,Utf8,true))profiles=ProjectXml.Decode<List<PrinterProfile>>(reader.ReadToEnd());
   if(profiles==null||profiles.Count>MaxProfiles)throw InvalidBackup();
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<profiles.Count;i++){
    WorkContext.Check();
    var profile=PrinterProfileStore.ValidatedCopy(profiles[i]);
    ValidatePrinterName(profile.Printer);
    if(!names.Add(ProfileKey(profile)))throw InvalidBackup();
   }
   return profiles;
  }

  internal static WorkspaceBackupFile EncodeProfiles(List<PrinterProfile> profiles) {
   if(profiles.Count>MaxProfiles)throw InvalidBackup();
   var bytes=Utf8.GetBytes(ProjectXml.Encode(profiles));
   if(bytes.Length>MaxProfileBytes)throw InvalidBackup();
   return new WorkspaceBackupFile(ProfilesFile,bytes);
  }

  internal static string ProfileKey(PrinterProfile profile){return profile.Printer+"\0"+profile.DisplayName.Trim();}

  internal static void ValidatePrinterName(string name) {
   if(string.IsNullOrWhiteSpace(name)||name.Length>512||name.IndexOf('\0')>=0)throw InvalidBackup();
  }

  static void VerifySnapshot(WorkspaceBackupState state,WorkspaceBackupFile expected) {
   WorkspaceBackupFile file;
   if(!state.Files.TryGetValue(expected.Name,out file)||file.Hash!=expected.Hash)throw Changed();
  }

  static WorkspaceBackupFile EncodeBatches(List<PrintBatchBackup> batches) {
   var collection=new PrintBatchBackupCollection{Batches=batches};
   collection.Validate(true);
   var bytes=Utf8.GetBytes(ProjectXml.Encode(collection));
   if(bytes.Length>MaxBatchBytes)throw InvalidBackup();
   return new WorkspaceBackupFile(BatchesFile,bytes);
  }

  internal static IDisposable LockDirectory(string directory){return new WorkspaceBackupLease(Path.GetFullPath(directory));}
  IDisposable Lock(){return LockDirectory(root);}
  static InvalidDataException InvalidBackup(){return new InvalidDataException(Loc.T("Neplatná alebo príliš veľká záloha pracovných údajov."));}
  internal static IOException Changed(){return new IOException(Loc.T("Pracovné údaje sa od náhľadu zmenili. Otvorte zálohu znova."));}
  static IOException PathConflict(string path){return new IOException(Loc.T("Cestu pracovných údajov už používa iná položka:")+" "+path);}

  sealed class WorkspaceBackupLease:IDisposable {
   readonly Mutex mutex;
   public WorkspaceBackupLease(string directory){
    var key=UpdateFiles.Digest(Utf8.GetBytes(directory.TrimEnd('\\','/').ToUpperInvariant()));
    mutex=new Mutex(false,"Local\\NDLabelDesigner.WorkspaceBackup."+key);
    try{
     try{
      var timer=System.Diagnostics.Stopwatch.StartNew();
      while(!mutex.WaitOne(100)){
       WorkContext.Check();
       if(timer.ElapsedMilliseconds>5000)throw new IOException(Loc.T("Pracovné údaje sa práve zálohujú alebo obnovujú."));
      }
     }catch(AbandonedMutexException){ }
    }catch{mutex.Dispose();throw;}
   }
   public void Dispose(){mutex.ReleaseMutex();mutex.Dispose();}
  }
 }
}
