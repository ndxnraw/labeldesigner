using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;


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

}
