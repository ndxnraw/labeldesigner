using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 // The clock is read by the caller once. All subsequent rendering and output
 // use resolved copies, never the current date or the live editor document.
 public sealed class PreparedLabelJob {
  public readonly DateTime PreparedAt;
  public readonly LabelSettings Label;
  public readonly OutputPlan Plan;
  public readonly string VariableValues;
  public PreparedLabelJob(LabelSettings template,DateTime instant){
   PreparedAt=instant;
   Label=JobPreparation.Resolve(template,instant);
   VariableValues=JobPreparation.Describe(template,instant);
   Plan=OutputPlan.Create(new List<BatchRecord>{new BatchRecord{Label=Label,Count=Label.Layout.Enabled?Label.Layout.Count:Label.Copies,PreparedAt=instant,VariableValues=VariableValues}},Label,instant);
  }
 }
 public static class JobPreparation {
  public static LabelSettings Resolve(LabelSettings template,DateTime instant){
   Engine.ValidateDraft(template);
   if(template.Variables.Count==0){var copy=ProjectXml.Clone(template);Engine.Validate(copy);return copy;}
   var record=BatchData.Record(template,new string[0],new List<ColumnBinding>(),-1,0,1,instant);
   if(record.Error!=null)throw new InvalidOperationException(record.Error);
   return record.Label;
  }
  public static string Describe(LabelSettings template,DateTime instant){return Describe(BatchData.Variables(template,0,instant));}
  public static string Describe(Dictionary<string,string> values){
   var parts=new List<string>();
   foreach(var pair in values)parts.Add(pair.Key+" = "+pair.Value);
   return string.Join("; ",parts.ToArray());
  }
 }
 public partial class PrintPanel {
  Func<DateTime> jobClock=delegate{return DateTime.Now;};
  readonly Label jobInfo=new Label{AutoSize=true,UseMnemonic=false,MaximumSize=new Size(270,0)};
  string jobError;
  void InitializeJobInfo(TableLayoutPanel fields,bool batch){
   jobInfo.Visible=sourceTemplate.Variables.Count>0||!string.IsNullOrEmpty(outputPlan.VariableValues);
   Add(fields,jobInfo);UpdateJobInfo();
   var refresh=Button(Loc.T("Prepočítať údaje"),RefreshJobVariables);
   refresh.Visible=!batch&&sourceTemplate.Variables.Count>0;
   Add(fields,refresh);
  }
  void UpdateJobInfo(){
   jobInfo.Text=Loc.T("Údaje pripravené")+": "+outputPlan.PreparedAt.GetValueOrDefault().ToString("g");
   if(!string.IsNullOrEmpty(outputPlan.VariableValues))jobInfo.Text+="\r\n"+outputPlan.VariableValues;
   jobInfo.AccessibleDescription=jobInfo.Text;
  }
  void RefreshJobVariables(){
   if(sending||busy)return;
   try{
    var job=new PreparedLabelJob(sourceTemplate,jobClock());
    snapshot=CloneLabel(job.Label);outputPlan=job.Plan;jobError=null;UpdateJobInfo();UpdateOutputSummary();
    updating=true;try{page.Value=1;}finally{updating=false;}
    Schedule();
   }catch(Exception ex){jobError=ex.Message;currentReport=null;review.Enabled=false;version++;debounce.Stop();print.Enabled=false;preview.ClearPages();info.Text=jobError;}
  }
 }
}
