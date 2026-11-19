using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Drawing;


namespace EanEtikety {
 public sealed class WorkProgress {
  public readonly string Stage;public readonly int Completed,Total;
  public WorkProgress(string stage,int completed,int total){Stage=stage;Completed=completed;Total=total;}
 }
 public sealed class WorkContext {
  [ThreadStatic]static WorkContext current;
  int cancelled;WorkProgress latest;long lastReport;readonly Action<WorkProgress> observer;
  public WorkContext(Action<WorkProgress> progress=null){observer=progress;}
  public bool IsCancelled{get{return Interlocked.CompareExchange(ref cancelled,0,0)!=0;}}
  public WorkProgress Progress{get{return Interlocked.CompareExchange(ref latest,null,null);}}
  public void Cancel(){Interlocked.Exchange(ref cancelled,1);}
  public IDisposable Enter(){return new Scope(this);}
  sealed class Scope:IDisposable {readonly WorkContext previous;public Scope(WorkContext value){previous=current;current=value;}public void Dispose(){current=previous;}}
  public static void Check(){if(current!=null&&current.IsCancelled)throw new OperationCanceledException();}
  public static void Report(string stage,int completed,int total){
   Check();if(current==null)return;long now=Stopwatch.GetTimestamp();var previous=current.Progress;
   if(completed!=0&&completed!=total&&previous!=null&&previous.Stage==stage&&now-current.lastReport<Stopwatch.Frequency/30)return;
   current.lastReport=now;var update=new WorkProgress(stage,completed,total);Interlocked.Exchange(ref current.latest,update);if(current.observer!=null)current.observer(update);Check();
  }
 }
 public sealed class CheckedReadStream:Stream {
  readonly Stream inner;public CheckedReadStream(Stream value){inner=value;}
  public override bool CanRead{get{return inner.CanRead;}}public override bool CanSeek{get{return inner.CanSeek;}}public override bool CanWrite{get{return false;}}
  public override long Length{get{return inner.Length;}}public override long Position{get{return inner.Position;}set{inner.Position=value;}}
  public override int Read(byte[] buffer,int offset,int count){WorkContext.Check();int result=inner.Read(buffer,offset,count);WorkContext.Check();return result;}
  public override long Seek(long offset,SeekOrigin origin){return inner.Seek(offset,origin);}public override void Flush(){inner.Flush();}
  public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
  protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
 }
 public static class AtomicOutput {
  public static void Save(string target,Action<string> write){
   target=Path.GetFullPath(target);
   string temp=Path.Combine(Path.GetDirectoryName(target),"."+Guid.NewGuid().ToString("N")+".tmp");
   try{WorkContext.Check();write(temp);WorkContext.Check();if(File.Exists(target))File.Replace(temp,target,null,true);else File.Move(temp,target);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }

}
