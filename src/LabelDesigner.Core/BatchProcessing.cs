using System;
using System.Collections.Generic;


namespace EanEtikety {
 public static class BatchProcessing {
  public static List<BatchRecord> Prepare(LabelSettings template,DataTableSource table,bool header,List<ColumnBinding> bindings,int quantity,int generated,DateTime instant){
   int start=table!=null&&header&&table.Rows.Count>0?1:0,count=table==null?generated:table.Rows.Count-start;
   if(count<0||count>10000)throw new InvalidOperationException(Loc.T("Najviac 10000 záznamov"));
   var result=new List<BatchRecord>();
   bool csv=table!=null&&table.HasCsvLineNumbers;
   int uniqueColumn=table==null?-1:template.Batch.UniqueColumn;
   var identifiers=uniqueColumn<0?null:new Dictionary<string,BatchRecord>(StringComparer.Ordinal);
   for(int i=0;i<count;i++){
    WorkContext.Report("Pripravujem záznamy…",i,count);
    int rowIndex=start+i;
    var row=table==null?new string[0]:table.Rows[rowIndex];
    var record=BatchData.Record(template,row,bindings,quantity,i,table==null?i+1:table.SourceRow(rowIndex),instant,csv);
    if(record.Label!=null&&record.Label.HideBarcode&&record.Label.Symbols.Count==0&&
        !record.Label.Texts.Exists(text=>!string.IsNullOrWhiteSpace(text.Text))){
     record.AddIssue(-1,null,Loc.T("Dokument neobsahuje nič na tlač."));
     record.PartialPreview=record.Label;record.Label=null;
    }
    result.Add(record);
    if(identifiers!=null)CheckUnique(record,row,uniqueColumn,identifiers);
   }
   WorkContext.Report("Pripravujem záznamy…",count,count);return result;
  }
  static void CheckUnique(BatchRecord record,string[] row,int column,Dictionary<string,BatchRecord> identifiers){
   string key=BatchData.Value(row,column);
   if(string.IsNullOrWhiteSpace(key)||key.StartsWith("!ERROR:",StringComparison.Ordinal))return;
   BatchRecord first;
   if(!identifiers.TryGetValue(key,out first)){identifiers.Add(key,record);return;}
   string format=Loc.T("Rovnaká hodnota je aj v riadku {0}.");
   if(!first.Issues.Exists(issue=>issue.Target=="unique"))
    first.AddIssue(column,"unique",string.Format(format,record.Row),false);
   record.AddIssue(column,"unique",string.Format(format,first.Row),false);
  }
  public static List<string> CheckOutput(OutputPlan plan){
   var report=PreflightReport.Inspect(plan);report.EnsureValid();return report.Messages();
  }
 }

}
