using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
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
