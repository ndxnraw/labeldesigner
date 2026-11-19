using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace EanEtikety {
 public enum PrintQueueOutcome { Unavailable,Found,Missing,Ambiguous }
 public enum PrintQueueState { Unknown,Waiting,Spooling,Printing,Paused,Offline,PaperOut,Attention,Error,HandedOff,Removed,DriverReported }

 public sealed class PrintQueueObservation {
  public DateTime CheckedUtc;
  public PrintQueueOutcome Outcome;
  public PrintQueueState State;
  public string ProviderText;
 }

 public sealed class PrintQueueHistory {
  public string Token;
  public PrintQueueObservation LastCheck,LastKnown;
 }

 public sealed class PrintQueueJob {
  public readonly string Title;
  public readonly uint Status;
  public readonly string ProviderText;
  public PrintQueueJob(string title,uint status,string providerText=null){Title=title;Status=status;ProviderText=providerText;}
 }

 public interface IPrintQueueSource {
  List<PrintQueueJob> Read(string printer);
 }

 public static class PrintQueue {
  internal const string TitlePrefix="ND LabelDesigner [";
  internal static readonly int TitleLength=TitlePrefix.Length+33;
  internal const int MaxProviderText=256;
  static int activeNativeQuery;

  public static string DocumentName(string token) {
   Guid id;
   if(!Guid.TryParseExact(token,"N",out id))throw new ArgumentException("token");
   return TitlePrefix+id.ToString("N")+"]";
  }

  public static bool CanCheck(PrintHistoryEntry entry) {
   Guid id;
   return entry!=null&&entry.Status==PrintHistoryStatus.Submitted&&!string.IsNullOrWhiteSpace(entry.Printer)&&
    Guid.TryParseExact(entry.QueueToken,"N",out id);
  }

  public static PrintQueueObservation Check(PrintHistoryEntry entry,IPrintQueueSource source) {
   return Check(entry,source,DateTime.UtcNow);
  }

  static PrintQueueObservation Check(PrintHistoryEntry entry,IPrintQueueSource source,DateTime requested) {
   if(!CanCheck(entry))throw new ArgumentException("entry");
   if(source==null)throw new ArgumentNullException("source");
   WorkContext.Check();
   List<PrintQueueJob> jobs;
   try{jobs=source.Read(entry.Printer);}
   catch(IOException){return Observation(requested,PrintQueueOutcome.Unavailable);}
   catch(Win32Exception){return Observation(requested,PrintQueueOutcome.Unavailable);}
   catch(UnauthorizedAccessException){return Observation(requested,PrintQueueOutcome.Unavailable);}
   WorkContext.Check();
   if(jobs==null)return Observation(requested,PrintQueueOutcome.Unavailable);
   string title=DocumentName(entry.QueueToken);
   PrintQueueJob match=null;
   foreach(var job in jobs){
    WorkContext.Check();
    if(job==null||!string.Equals(title,job.Title,StringComparison.Ordinal))continue;
    if(match!=null)return Observation(requested,PrintQueueOutcome.Ambiguous);
    match=job;
   }
   if(match==null)return Observation(requested,PrintQueueOutcome.Missing);
   string provider=SafeProviderText(match.ProviderText);
   var observation=Observation(requested,PrintQueueOutcome.Found,
    provider==null?Translate(match.Status):PrintQueueState.DriverReported);
   observation.ProviderText=provider;
   return observation;
  }

  static string SafeProviderText(string text) {
   if(string.IsNullOrEmpty(text))return null;
   var result=new StringBuilder();
   for(int i=0;i<Math.Min(MaxProviderText,text.Length);i++){
    char c=text[i];
    if(char.IsWhiteSpace(c))result.Append(' ');
    else if(!char.IsControl(c)&&XmlConvert.IsXmlChar(c))result.Append(c);
   }
   string value=result.ToString().Trim();
   return value.Length==0?null:value;
  }

  internal static PrintQueueObservation CheckWindows(PrintHistoryEntry entry) {
   return CheckBounded(entry,new WindowsPrintQueueSource(),TimeSpan.FromSeconds(6));
  }

  // A spooler call or remote print provider can block inside native code.
  // Bound the wait and retain one slot until the actual native call exits.
  internal static PrintQueueObservation CheckBounded(PrintHistoryEntry entry,IPrintQueueSource source,TimeSpan limit) {
   WorkContext.Check();
   var requested=DateTime.UtcNow;
   if(Interlocked.CompareExchange(ref activeNativeQuery,1,0)!=0)
    return Observation(requested,PrintQueueOutcome.Unavailable);
   var watch=Stopwatch.StartNew();
   Task<PrintQueueObservation> work;
   try{
    work=Task.Run(()=>{
     try{return Check(entry,source,requested);}
     finally{Interlocked.Exchange(ref activeNativeQuery,0);}
    });
   }catch{
    Interlocked.Exchange(ref activeNativeQuery,0);
    throw;
   }
   try{
    while(!work.IsCompleted){
     WorkContext.Check();
     if(watch.Elapsed>=limit)return Observation(requested,PrintQueueOutcome.Unavailable);
     Thread.Sleep(25);
    }
    WorkContext.Check();
    return work.GetAwaiter().GetResult();
   }finally{
    // Observe errors even if the waiter has timed out or been cancelled.
    if(!work.IsCompleted||work.IsFaulted)
     work.ContinueWith(task=>{var ignored=task.Exception;},TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously);
   }
  }

  static PrintQueueObservation Observation(DateTime requested,PrintQueueOutcome outcome,PrintQueueState state=PrintQueueState.Unknown) {
   return new PrintQueueObservation{CheckedUtc=requested,Outcome=outcome,State=state};
  }

  static PrintQueueState Translate(uint flags) {
   const uint Paused=1,Error=2,Deleting=4,Spooling=8,Printing=0x10,Offline=0x20,PaperOut=0x40,
    Printed=0x80,Deleted=0x100,Blocked=0x200,Intervention=0x400,Complete=0x1000;
   if((flags&(Deleting|Deleted))!=0)return PrintQueueState.Removed;
   if((flags&PaperOut)!=0)return PrintQueueState.PaperOut;
   if((flags&Offline)!=0)return PrintQueueState.Offline;
   if((flags&(Blocked|Intervention))!=0)return PrintQueueState.Attention;
   if((flags&Error)!=0)return PrintQueueState.Error;
   if((flags&Paused)!=0)return PrintQueueState.Paused;
   if((flags&(Printed|Complete))!=0)return PrintQueueState.HandedOff;
   if((flags&Printing)!=0)return PrintQueueState.Printing;
   if((flags&Spooling)!=0)return PrintQueueState.Spooling;
   return flags==0?PrintQueueState.Waiting:PrintQueueState.Unknown;
  }
 }

 sealed class WindowsPrintQueueSource:IPrintQueueSource {
  const uint MaxJobs=1024,MaxBytes=4*1024*1024,InsufficientBuffer=122;

  [StructLayout(LayoutKind.Sequential)]
  struct SystemTime {public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Millisecond;}
  [StructLayout(LayoutKind.Sequential)]
  struct PrinterOptions {public uint Size,Flags;}
  [StructLayout(LayoutKind.Sequential)]
  struct JobInfo {
   public uint Id;
   public IntPtr Printer,Machine,User,Document,DataType,StatusText;
   public uint Status,Priority,Position,TotalPages,PagesPrinted;
   public SystemTime Submitted;
  }

  [DllImport("winspool.drv",EntryPoint="OpenPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool OpenPrinter(string name,out IntPtr handle,IntPtr defaults);
  [DllImport("winspool.drv",EntryPoint="OpenPrinter2W",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern bool OpenPrinterFresh(string name,out IntPtr handle,IntPtr defaults,ref PrinterOptions options);
  [DllImport("winspool.drv",EntryPoint="EnumJobsW",SetLastError=true)]
  static extern bool EnumJobs(IntPtr handle,uint first,uint count,uint level,IntPtr buffer,uint size,out uint required,out uint returned);
  [DllImport("winspool.drv",SetLastError=true)]
  static extern bool ClosePrinter(IntPtr handle);

  public List<PrintQueueJob> Read(string printer) {
   IntPtr handle;
   bool opened;
   if(printer.StartsWith(@"\\",StringComparison.Ordinal)){
    const uint noCache=1;
    var options=new PrinterOptions{Size=(uint)Marshal.SizeOf(typeof(PrinterOptions)),Flags=noCache};
    opened=OpenPrinterFresh(printer,out handle,IntPtr.Zero,ref options);
   }else opened=OpenPrinter(printer,out handle,IntPtr.Zero);
   if(!opened)throw new Win32Exception(Marshal.GetLastWin32Error());
   try{
    uint size=0;
    for(int attempt=0;attempt<3;attempt++){
     var buffer=size==0?IntPtr.Zero:Marshal.AllocHGlobal((int)size);
     try{
      uint needed,returned;
      if(EnumJobs(handle,0,MaxJobs+1,1,buffer,size,out needed,out returned))return ReadJobs(buffer,size,returned);
      int code=Marshal.GetLastWin32Error();
      if(code!=InsufficientBuffer)throw new Win32Exception(code);
      if(needed<=size||needed>MaxBytes)break;
      size=needed;
     }finally{if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);}
    }
    throw new IOException("Print queue snapshot exceeds the lookup bounds.");
   }finally{ClosePrinter(handle);}
  }

  static List<PrintQueueJob> ReadJobs(IntPtr buffer,uint size,uint returned) {
   int stride=Marshal.SizeOf(typeof(JobInfo));
   if(returned>MaxJobs||(long)stride*returned>size)throw new IOException("Incomplete print queue snapshot.");
   var jobs=new List<PrintQueueJob>();
   for(int i=0;i<returned;i++){
    var job=(JobInfo)Marshal.PtrToStructure(IntPtr.Add(buffer,i*stride),typeof(JobInfo));
    long offset=job.Document.ToInt64()-buffer.ToInt64();
    int length=PrintQueue.TitleLength;
    if(job.Document==IntPtr.Zero||offset<(long)stride*returned||offset+(length+1)*2>size)continue;
    if(Marshal.ReadInt16(job.Document,length*2)!=0)continue;
    string title=Marshal.PtrToStringUni(job.Document,length);
    // Only this application's opaque identifiers can match a history entry.
    if(!title.StartsWith(PrintQueue.TitlePrefix,StringComparison.Ordinal))continue;
    jobs.Add(new PrintQueueJob(title,job.Status,ReadProviderText(job.StatusText,buffer,size,(long)stride*returned)));
   }
   return jobs;
  }

  static string ReadProviderText(IntPtr address,IntPtr buffer,uint size,long recordsSize) {
   long offset=address.ToInt64()-buffer.ToInt64();
   if(address==IntPtr.Zero||offset<recordsSize||offset>=size)return null;
   int max=(int)Math.Min(PrintQueue.MaxProviderText,(size-offset)/2);
   int length=0;
   while(length<max&&Marshal.ReadInt16(address,length*2)!=0)length++;
   return length==0?null:Marshal.PtrToStringUni(address,length);
  }
 }
}
