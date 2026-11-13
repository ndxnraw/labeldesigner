using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
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
