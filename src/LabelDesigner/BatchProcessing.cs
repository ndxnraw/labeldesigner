using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace EanEtikety {
 public static class BatchProcessing {
  public static List<BatchRecord> Prepare(LabelSettings template,DataTableSource table,bool header,List<ColumnBinding> bindings,int quantity,int generated,DateTime instant){
   int start=table!=null&&header?1:0,count=table==null?generated:table.Rows.Count-start;
   if(count<0||count>10000)throw new InvalidOperationException(Loc.T("Najviac 10000 záznamov"));
   var result=new List<BatchRecord>();
   for(int i=0;i<count;i++){WorkContext.Report("Pripravujem záznamy…",i,count);result.Add(BatchData.Record(template,table==null?new string[0]:table.Rows[start+i],bindings,quantity,i,start+i+1,instant));}
   WorkContext.Report("Pripravujem záznamy…",count,count);return result;
  }
  public static List<string> CheckOutput(OutputPlan plan){
   var report=PreflightReport.Inspect(plan);report.EnsureValid();return report.Messages();
  }
 }
 // One bounded STA worker per batch window. Rapid selection replaces pending
 // work and cancels obsolete rendering; no controls are read by the worker.
 public sealed class BatchPreview:IDisposable {
  readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);
  readonly PictureBox view;readonly Action<string> showError;readonly System.Windows.Forms.Timer poll=new System.Windows.Forms.Timer{Interval=50};
  LabelSettings pending;WorkContext active;Bitmap completed;string error;int version,completedVersion;bool closed;
  public BatchPreview(PictureBox target,Action<string> failure){view=target;showError=failure;poll.Tick+=delegate{Deliver();};poll.Start();var thread=new Thread(Run){IsBackground=true,Name="ND batch preview"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
  public void Request(LabelSettings label){lock(gate){if(closed)return;version++;pending=label;if(active!=null)active.Cancel();if(completed!=null){completed.Dispose();completed=null;}error=null;wake.Set();}var old=view.Image;view.Image=null;if(old!=null)old.Dispose();}
  void Run(){try{while(true){wake.WaitOne();LabelSettings label;int ticket;WorkContext context;lock(gate){if(closed)return;label=pending;pending=null;ticket=version;active=context=new WorkContext();}if(label==null)continue;Bitmap image=null;string message=null;
   try{using(context.Enter())using(var scene=VectorArt.Build(label))image=JobOutput.Raster(scene,Math.Min(150,label.Dpi));}
   catch(OperationCanceledException){ }
   catch(Exception ex){string id=DiagnosticLog.Write("batch-preview",ex);message=ex.Message+(id==null?"":"\r\n"+Loc.T("Diagnostický záznam")+": "+id);}
   lock(gate){if(active==context)active=null;if(closed||ticket!=version){if(image!=null)image.Dispose();}else{if(completed!=null)completed.Dispose();completed=image;error=message;completedVersion=ticket;}}
  }}finally{wake.Dispose();}}
  void Deliver(){Bitmap image;string message;lock(gate){if(closed||completedVersion!=version)return;image=completed;message=error;completed=null;error=null;}if(image!=null){var old=view.Image;view.Image=image;if(old!=null)old.Dispose();}if(message!=null)showError(message);}
  public void Dispose(){lock(gate){if(closed)return;closed=true;pending=null;if(active!=null)active.Cancel();if(completed!=null){completed.Dispose();completed=null;}wake.Set();}poll.Dispose();var old=view.Image;view.Image=null;if(old!=null)old.Dispose();}
 }
}
