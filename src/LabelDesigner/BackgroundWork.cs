using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;

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
   string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{WorkContext.Check();write(temp);WorkContext.Check();if(File.Exists(target))File.Replace(temp,target,null,true);else File.Move(temp,target);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 public sealed class BackgroundFailure:Exception {
  public readonly string DiagnosticId;
  public BackgroundFailure(Exception error,string id):base(error.Message,error){DiagnosticId=id;}
 }
 public static class WorkDialog {
  public static bool Run<T>(IWin32Window owner,string operation,string title,Font font,Icon icon,Action<Control> style,Func<T> work,out T result,string diagnosticDirectory=null){
   var context=new WorkContext();var gate=new object();bool done=false,cancelled=false;T value=default(T);Exception error=null;string diagnostic=null;
   using(var window=new Form{Text=title,Font=font,Icon=icon,ClientSize=new Size(520,170),MinimumSize=new Size(420,220),StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MaximizeBox=false,MinimizeBox=false,AutoScaleMode=AutoScaleMode.Dpi})
   using(var timer=new System.Windows.Forms.Timer{Interval=50}){
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),RowCount=3,ColumnCount=1};window.Controls.Add(layout);
    var message=new Label{Text=title,AutoSize=true,Dock=DockStyle.Fill,UseMnemonic=false};layout.Controls.Add(message);
    var bar=new ProgressBar{Dock=DockStyle.Top,Style=ProgressBarStyle.Marquee,Height=24};layout.Controls.Add(bar);
    var cancel=new ModernButton{Text=Loc.T("Zrušiť"),AutoSize=true,Anchor=AnchorStyles.Right};layout.Controls.Add(cancel);window.CancelButton=cancel;
    Action requestCancel=delegate{context.Cancel();cancel.Enabled=false;message.Text=Loc.T("Dokončujem zrušenie…");};cancel.Click+=delegate{requestCancel();};
    window.FormClosing+=delegate(object sender,FormClosingEventArgs e){lock(gate){if(!done){e.Cancel=true;requestCancel();}}};
    timer.Tick+=delegate{
     var progress=context.Progress;if(progress!=null&&!context.IsCancelled){message.Text=Loc.T(progress.Stage)+(progress.Total>0?"  "+progress.Completed+" / "+progress.Total:"");bar.Style=progress.Total>0?ProgressBarStyle.Continuous:ProgressBarStyle.Marquee;if(progress.Total>0)bar.Value=Math.Max(0,Math.Min(100,(int)(100L*progress.Completed/progress.Total)));}
     lock(gate){if(done){timer.Stop();window.DialogResult=error!=null?DialogResult.Abort:cancelled?DialogResult.Cancel:DialogResult.OK;window.Close();}}
    };
    window.Shown+=delegate{
     var thread=new Thread(delegate(){
      T candidate=default(T);Exception failure=null;bool stopped=false;string id=null;
      try{using(context.Enter()){WorkContext.Check();candidate=work();}}
      catch(OperationCanceledException ex){if(context.IsCancelled)stopped=true;else{failure=ex;id=DiagnosticLog.WriteTo(diagnosticDirectory??DiagnosticLog.DirectoryPath,operation,ex);}}
      catch(Exception ex){failure=ex;id=DiagnosticLog.WriteTo(diagnosticDirectory??DiagnosticLog.DirectoryPath,operation,ex);}
      lock(gate){value=candidate;error=failure;cancelled=stopped;diagnostic=id;done=true;}
     }){IsBackground=true,Name="ND batch operation"};thread.SetApartmentState(ApartmentState.STA);thread.Start();timer.Start();
    };
    style(window);window.ShowDialog(owner);
   }
   if(error!=null)throw new BackgroundFailure(error,diagnostic);
   result=value;return !cancelled;
  }
 }
 public partial class MainForm {
  bool RunWork<T>(IWin32Window owner,string operation,string title,Func<T> work,out T result){return WorkDialog.Run(owner,operation,Loc.T(title),Font,Icon,ApplyColors,work,out result);}
 }
}
