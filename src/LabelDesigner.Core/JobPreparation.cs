using System;
using System.Collections.Generic;
using System.Drawing;


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
   if(template.Variables.Count==0){var copy=ProjectXml.Clone(template);BatchReferences.Strip(copy);Engine.Validate(copy);return copy;}
   var record=BatchData.Record(template,new string[0],new List<ColumnBinding>(),-1,0,1,instant);
   if(record.Error!=null)throw new InvalidOperationException(record.Error);
   BatchReferences.Strip(record.Label);
   return record.Label;
  }
  public static string Describe(LabelSettings template,DateTime instant){return Describe(BatchData.Variables(template,0,instant));}
  public static string Describe(Dictionary<string,string> values){
   var parts=new List<string>();
   foreach(var pair in values)parts.Add(pair.Key+" = "+pair.Value);
   return string.Join("; ",parts.ToArray());
  }
 }

}
