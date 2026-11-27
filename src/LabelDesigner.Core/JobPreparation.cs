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
  public static bool HasDynamicArtwork(LabelSettings label)=>label.Languages!=null||ConditionalVisibility.HasRules(label)||
   HasImageMappings(label)||LabelDataFormat.HasFields(label);
  static bool HasImageMappings(LabelSettings label)=>label.Symbols?.Exists(symbol=>symbol?.ImageMapping!=null)==true;

  internal static LabelSettings CloneForPreparation(LabelSettings template){
   template=template.WithoutEditorReference();
   if(!template.Symbols.Exists(symbol=>symbol?.ImageMapping!=null||symbol?.Image?.FitFrame==true))return ProjectXml.Clone(template);
   // Share immutable encoded bytes across records and output placements.
   var copy=ProjectXml.Clone(template.WithoutDataDrivenImages());
   for(int i=0;i<copy.Symbols.Count;i++){
    var source=template.Symbols[i];
    copy.Symbols[i].ImageMapping=source.ImageMapping;
    if(source.Image?.FitFrame==true)copy.Symbols[i].Image=source.Image.Copy();
   }
   return copy;
  }

  internal static HashSet<int> ProjectArtwork(LabelSettings label,Dictionary<string,string> values,Func<int,bool> invalidSource,Action<int,string> report){
   var hidden=ConditionalVisibility.FindHidden(label,values,invalidSource,message=>report(-2,message));
   if(HasImageMappings(label)){
    var fields=new HashSet<int>(QuickPrint.Available(label));
    for(int i=0;i<label.Symbols.Count;i++){
     int index=-3-i;
     var symbol=label.Symbols[i];
     var mapping=symbol.ImageMapping;
     if(mapping==null||hidden.Contains(index))continue;
     WorkContext.Check();
     if(symbol.Image!=null||symbol.Code!=null||symbol.Matrix!=null||symbol.Shape!=null)
      throw new System.IO.InvalidDataException(Loc.T("Neplatné mapovanie obrázka."));
     mapping.Validate();
     symbol.ImageMapping=null;
     bool variable=!string.IsNullOrEmpty(mapping.Variable);
     int field=variable?-2:mapping.Field;
     string value=null;
     bool available=variable?values.TryGetValue(mapping.Variable,out value):fields.Contains(field);
     string failure=null;
     if(!available)failure=Loc.T("Zdroj obrázka už nie je dostupný.");
     else if(!variable&&invalidSource(field))failure=Loc.T("Zdroj obrázka obsahuje chybu.");
     else{
      if(!variable)value=QuickPrint.Value(label,field,true);
      symbol.Image=mapping.Select(value);
      if(symbol.Image==null&&mapping.Missing==MissingImage.Error)
       failure=Loc.T("Pre hodnotu chýba priradený obrázok.");
      if(symbol.Image!=null)try{Assets.Validate(symbol.Image);}
      catch(OperationCanceledException){throw;}
      catch(Exception ex){failure=ex.Message;symbol.Image=null;}
     }
     if(failure!=null)report(field,string.Format(Loc.T("Obrázok {0}"),i+1)+" — "+failure);
     if(symbol.Image==null)hidden.Add(index);
    }
   }
   if(hidden.Count==0)return hidden;
   // Preserve original layers and issue navigation when indices are compacted.
   foreach(int index in Elements.Indices(label)){
    if(hidden.Contains(index))continue;
    var meta=Elements.Meta(label,index);
    meta.SourceIndex??=index;
   }
   Elements.Remove(label,new List<int>(hidden));
   return hidden;
  }

  public static LabelSettings Resolve(LabelSettings template,DateTime instant){
   template=template?.WithoutEditorReference();
   Engine.ValidateDraft(template);
   if(template.Variables.Count==0&&!HasDynamicArtwork(template)){var copy=CloneForPreparation(template);BatchReferences.Strip(copy);Engine.Validate(copy);return copy;}
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
