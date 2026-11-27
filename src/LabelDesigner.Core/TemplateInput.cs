using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public enum TemplateInputKind { Text, Number, Date }
 public readonly record struct TemplateInputValue(string Value,TextInputKind Kind=TextInputKind.Text,string Error=null);

 public sealed class TemplateInput {
  const string TargetPrefix="input:";
  public const int MaximumCount=50;
  public string Id=Guid.NewGuid().ToString("N");
  public string Name="";
  public TemplateInputKind Kind;
  public string DefaultValue="";
  public bool Required;

  public override string ToString()=>Name;
  public TemplateInput Copy()=>(TemplateInput)MemberwiseClone();
  public static string Target(string id)=>TargetPrefix+id;
  public static bool IsId(string id)=>Guid.TryParseExact(id,"N",out _);
  public static bool TryTarget(string target,out string id){
   id=target?.StartsWith(TargetPrefix,StringComparison.Ordinal)==true?target.Substring(TargetPrefix.Length):null;
   return IsId(id);
  }

  public static void ValidateAll(List<TemplateInput> inputs){
   var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(inputs==null||inputs.Count>MaximumCount)throw Invalid();
   foreach(var input in inputs){
    if(input==null||!IsId(input.Id)||!ids.Add(input.Id)||string.IsNullOrWhiteSpace(input.Name)||
       input.Name.Length>100||!names.Add(input.Name.Trim())||!Enum.IsDefined(input.Kind)||input.DefaultValue==null)
     throw Invalid();
    XmlConvert.VerifyXmlChars(input.Name);
    if(!input.TryNormalize(new(input.DefaultValue),false,out _,out string error))
     throw new InvalidDataException(input.NamedError(error));
   }
  }

  static InvalidDataException Invalid()=>new(Loc.T("Pomenované vstupy potrebujú platné a jedinečné názvy."));
  internal string NamedError(string detail)=>Name+" · "+detail;

  internal bool TryNormalize(TemplateInputValue sample,bool require,out string value,out string error){
   value=sample.Value??"";
   error=sample.Error;
   if(error!=null)return false;
   if(value.Length>4000){error=Loc.T("Text je príliš dlhý.");return false;}
   try{XmlConvert.VerifyXmlChars(value);}
   catch(XmlException){error=Loc.T("Hodnota obsahuje nepovolený znak.");return false;}
   if(string.IsNullOrWhiteSpace(value)){
    value="";
    if(require&&Required){error=Loc.T("Povinný vstup nemá hodnotu.");return false;}
    return true;
   }
   if(Kind==TemplateInputKind.Text)return true;
   string source=value.Trim();
   if(Kind==TemplateInputKind.Number){
    if(sample.Kind!=TextInputKind.WorksheetDate&&
       decimal.TryParse(source.Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,
        CultureInfo.InvariantCulture,out decimal number)){
     value=number.ToString("0.############################",CultureInfo.InvariantCulture);
     return true;
    }
    error=Loc.T("Zadajte platné číslo.");
   }else{
    if(sample.Kind!=TextInputKind.WorksheetNumber&&
       DateOnly.TryParseExact(source,LabelDataFormat.IsoDate,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)){
     value=date.ToString(LabelDataFormat.IsoDate,CultureInfo.InvariantCulture);
     return true;
    }
    error=Loc.T("Zadajte dátum vo formáte RRRR-MM-DD.");
   }
   return false;
  }

  public bool IsReferencedBy(ElementMeta meta)=>meta!=null&&(
   meta.Visibility?.InputId==Id||meta.GroupVisibility?.InputId==Id||
   meta.Calculation?.Left.InputId==Id||meta.Calculation?.Right.InputId==Id);

  public bool IsReferenced(LabelSettings document)=>document.Metadata.Exists(IsReferencedBy);

  public static void MergeReferenced(IEnumerable<TemplateInput> source,LabelSettings target){
   foreach(var input in source){
    if(!input.IsReferenced(target)||target.Inputs.Exists(existing=>existing.Id==input.Id))continue;
    var added=input.Copy();
    string name=added.Name.Trim();
    for(int number=2;target.Inputs.Exists(existing=>string.Equals(existing.Name.Trim(),added.Name.Trim(),
        StringComparison.OrdinalIgnoreCase));number++){
     string suffix=" ("+number.ToString(CultureInfo.InvariantCulture)+")";
     added.Name=name.Substring(0,Math.Min(100-suffix.Length,name.Length))+suffix;
    }
    target.Inputs.Add(added);
   }
   ValidateAll(target.Inputs);
  }
 }

 internal sealed record ResolvedInput(TemplateInput Definition,string Value,bool Valid);

 internal static class TemplateInputResolution {
  static readonly IReadOnlyDictionary<string,ResolvedInput> Empty=new Dictionary<string,ResolvedInput>();

  internal static IReadOnlyDictionary<string,ResolvedInput> Resolve(LabelSettings label,
      IReadOnlyDictionary<string,TemplateInputValue> values,Action<string,string> report){
   if(label.Inputs.Count==0)return Empty;
   var resolved=new Dictionary<string,ResolvedInput>(StringComparer.Ordinal);
   foreach(var input in label.Inputs){
    var sample=values!=null&&values.TryGetValue(input.Id,out var supplied)?supplied:new TemplateInputValue(input.DefaultValue);
    bool valid=input.TryNormalize(sample,true,out string value,out string error);
    if(!valid)report(input.Id,input.NamedError(error));
    resolved.Add(input.Id,new ResolvedInput(input,value,valid));
   }
   label.Inputs.Clear();
   label.Batch.Bindings.RemoveAll(binding=>TemplateInput.TryTarget(binding.Target,out _));
   foreach(var preset in label.Batch.Presets)
    preset.Bindings.RemoveAll(binding=>TemplateInput.TryTarget(binding.Target,out _));
   return resolved;
  }
 }
}
