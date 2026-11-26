using System;
using System.Collections.Generic;

namespace EanEtikety {
 public enum VisibilityMode { Filled, EqualsValue }

 public sealed class VisibilityRule {
  public int Field=-2;
  public string Variable;
  public VisibilityMode Mode;
  public string Value;
  public VisibilityRule Copy()=> (VisibilityRule)MemberwiseClone();
 }

 public static class ConditionalVisibility {
  public static bool HasRules(LabelSettings label)=>label.Metadata.Exists(meta=>meta!=null&&(meta.Visibility!=null||meta.GroupVisibility!=null));

  // Apply only to an owned copy after data substitution. Frozen output can then
  // use the ordinary validators and renderers without evaluating rules again.
  internal static HashSet<int> Apply(LabelSettings label,Dictionary<string,string> values,Func<int,bool> invalidSource,Action<string> report) {
   var hidden=new HashSet<int>();
   if(!HasRules(label))return hidden;
   var indices=Elements.Indices(label);
   var available=new HashSet<int>(QuickPrint.Available(label));
   var hiddenGroups=new HashSet<string>(StringComparer.Ordinal);
   var evaluatedGroups=new HashSet<string>(StringComparer.Ordinal);

   bool Matches(VisibilityRule rule) {
    string value;
    if(!string.IsNullOrEmpty(rule.Variable)){
     if(!values.TryGetValue(rule.Variable,out value)){
      report(Loc.T("Zdroj pravidla zobrazenia už nie je dostupný."));
      return true;
     }
    }else if(!available.Contains(rule.Field)){
     report(Loc.T("Zdroj pravidla zobrazenia už nie je dostupný."));
     return true;
    }else if(invalidSource(rule.Field)){
     report(Loc.T("Zdroj pravidla zobrazenia obsahuje chybu."));
     return true;
    }else{
     value=QuickPrint.Value(label,rule.Field,true);
    }
    return rule.Mode==VisibilityMode.Filled?!string.IsNullOrWhiteSpace(value):
     string.Equals(value?.Trim()??"",rule.Value?.Trim()??"",StringComparison.Ordinal);
   }

   foreach(var meta in label.Metadata)
    if(indices.Contains(meta.Index)&&!string.IsNullOrEmpty(meta.Group)&&meta.GroupVisibility!=null&&evaluatedGroups.Add(meta.Group)&&!Matches(meta.GroupVisibility))
     hiddenGroups.Add(meta.Group);

   foreach(var meta in label.Metadata){
    if(indices.Contains(meta.Index)&&(hiddenGroups.Contains(meta.Group??"")||meta.Visibility!=null&&!Matches(meta.Visibility)))hidden.Add(meta.Index);
    meta.Visibility=null;
    meta.GroupVisibility=null;
   }

   if(hidden.Count==0)return hidden;
   // Preserve original layer and diagnostic identities across compacted arrays.
   foreach(int index in indices){
    if(hidden.Contains(index))continue;
    var meta=Elements.Meta(label,index);
    meta.SourceIndex??=index;
   }
   Elements.Remove(label,new List<int>(hidden));
   return hidden;
  }
 }
}
