using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public static partial class BatchData {
  public static BatchRecord Record(LabelSettings template,string[] row,List<ColumnBinding> mappings,
      int quantityColumn,int index,int rowNumber,DateTime instant) {
   return Record(template,row,mappings,quantityColumn,index,rowNumber,instant,false);
  }

  internal static BatchRecord Record(LabelSettings template,string[] row,List<ColumnBinding> mappings,
      int quantityColumn,int index,int rowNumber,DateTime instant,bool csv,TextInputKind[] inputKinds=null) {
   var record=new BatchRecord { Row=rowNumber, PreparedAt=instant };
   try {
    WorkContext.Check();
    var label=JobPreparation.CloneForPreparation(template);
    var columns=new Dictionary<string,int>(StringComparer.Ordinal);
    string requestedLanguage=null;
    foreach(var binding in mappings) {
     if(binding.Column<0)continue;
     bool language=binding.Target==LabelLanguages.BatchTarget;
     if(language&&label.Languages==null)continue;
     if(Elements.TryFieldIndex(binding.Target,out int field)&&LabelLanguages.Translated(label,field))continue;
     string value=Value(row,binding.Column);
     string issue=csv&&binding.Column>=row.Length?Loc.T("V riadku chýba namapovaný stĺpec."):
      value.StartsWith("!ERROR:",StringComparison.Ordinal)?value:null;
     if(issue!=null)record.AddIssue(binding.Column,binding.Target,issue);
     columns[binding.Target]=binding.Column;
     if(language)requestedLanguage=issue==null?value:"";
     else SetBoundValue(label,binding.Target,issue==null?value:"");
    }
    var outputCulture=LabelDataFormat.HasFields(label)?LabelDataFormat.CultureFor(template,requestedLanguage):null;
    LabelLanguages.Resolve(label,requestedLanguage,(field,message)=>record.AddIssue(
     columns.TryGetValue(LabelLanguages.BatchTarget,out int column)?column:-1,Elements.FieldTarget(field),message));

    if(quantityColumn>=0) {
     int count;
     if(!int.TryParse(Value(row,quantityColumn),NumberStyles.Integer,CultureInfo.InvariantCulture,out count)
         ||count<1||count>10000) {
      record.AddIssue(quantityColumn,"quantity",Loc.T("Neplatný počet etikiet"));
      record.Count=0;
     }else record.Count=count;
    }

    var values=Variables(template,index,instant);
    record.VariableValues=JobPreparation.Describe(values);
    Func<string,string,string> resolve=(target,value)=>{
     try{return XmlConvert.VerifyXmlChars(Expand(value,values));}
     catch(InvalidDataException ex){AddFieldIssue(record,columns,target,ex.Message);return "";}
     catch(XmlException){AddFieldIssue(record,columns,target,Loc.T("Hodnota obsahuje nepovolený znak."));return "";}
    };
    label.Ean=resolve("code",label.Ean);
    foreach(var ai in label.ApplicationIdentifiers)ai.Data=resolve("code",ai.Data);
    for(int i=0;i<label.Texts.Count;i++) {
     var text=label.Texts[i];
     try {
      // A formatted source cell is a literal typed value.
      if(outputCulture!=null&&columns.ContainsKey(Elements.FieldTarget(i))&&LabelDataFormat.ForField(label,i)!=null)XmlConvert.VerifyXmlChars(text.Text);
      else Expand(text,values);
     }
     catch(InvalidDataException ex){AddFieldIssue(record,columns,"text:"+i,ex.Message);StyledText.ReplaceField(text,"");}
     catch(XmlException){AddFieldIssue(record,columns,"text:"+i,Loc.T("Hodnota obsahuje nepovolený znak."));StyledText.ReplaceField(text,"");}
    }
    for(int i=0;i<label.Symbols.Count;i++) {
     var symbol=label.Symbols[i];
     string target="symbol:"+i;
     if(symbol.Matrix!=null)symbol.Matrix.Data=resolve(target,symbol.Matrix.Data);
     if(symbol.Code!=null) {
      symbol.Code.Ean=resolve(target,symbol.Code.Ean);
      foreach(var ai in symbol.Code.ApplicationIdentifiers)ai.Data=resolve(target,ai.Data);
     }
    }
    if(JobPreparation.HasDynamicArtwork(label)){
     var ruleErrors=new HashSet<(int Column,string Message)>();
     var hidden=JobPreparation.ProjectArtwork(label,values,index=>record.Issues.Exists(issue=>issue.Blocking&&issue.Target==Elements.FieldTarget(index)),
      (index,message)=>ruleErrors.Add((index!=-2&&columns.TryGetValue(Elements.FieldTarget(index),out int column)?column:-1,message)));
     if(hidden.Count>0){
      record.Issues.RemoveAll(issue=>Elements.TryFieldIndex(issue.Target,out int index)&&hidden.Contains(index));
      record.Error=record.Issues.Find(issue=>issue.Blocking)?.Message;
      var remap=new Dictionary<string,string>(StringComparer.Ordinal);
      foreach(int element in Elements.Indices(label))remap[Elements.FieldTarget(Elements.SourceIndex(label,element))]=Elements.FieldTarget(element);
      var filteredColumns=new Dictionary<string,int>(StringComparer.Ordinal);
      foreach(var pair in columns)if(remap.TryGetValue(pair.Key,out string target))filteredColumns[target]=pair.Value;
      columns=filteredColumns;
      foreach(var issue in record.Issues)if(issue.Target!=null&&remap.TryGetValue(issue.Target,out string target))issue.Target=target;
     }
     foreach(var error in ruleErrors)record.AddIssue(error.Column,null,error.Message);
    }
    if(outputCulture!=null)LabelDataFormat.ApplyFields(label,outputCulture,field=>
     inputKinds!=null&&columns.TryGetValue(Elements.FieldTarget(field),out int column)&&column<inputKinds.Length?inputKinds[column]:TextInputKind.Text,
     (field,message)=>AddFieldIssue(record,columns,Elements.FieldTarget(field),message));
    label.Variables.Clear();
    TextAutoLayout.Apply(label);
    VerticalStack.Apply(label);
    if(label.StackIssues!=null)foreach(var issue in label.StackIssues){
     string target=Elements.FieldTarget(issue.Element);
     record.AddIssue(columns.GetValueOrDefault(target,-1),target,issue.Message,issue.Blocking);
    }
    ValidatePreparedLabel(record,label,columns);
   }catch(OperationCanceledException){throw;}
    catch(Exception ex){record.AddIssue(-1,null,ex.Message);}
   WorkContext.Check();
   return record;
  }

  static void SetBoundValue(LabelSettings label,string target,string value) {
   if(target=="code")label.Ean=value;
   else if(target.StartsWith("text:",StringComparison.Ordinal))
    StyledText.ReplaceField(label.Texts[int.Parse(target.Substring(5),CultureInfo.InvariantCulture)],value);
   else if(target.StartsWith("symbol:",StringComparison.Ordinal)) {
    var symbol=label.Symbols[int.Parse(target.Substring(7),CultureInfo.InvariantCulture)];
    if(symbol.Matrix!=null)symbol.Matrix.Data=value;
    else if(symbol.Code!=null)symbol.Code.Ean=value;
   }
  }

  static void AddFieldIssue(BatchRecord record,Dictionary<string,int> columns,string target,string message) {
   if(record.Issues.Exists(issue=>issue.Target==target&&issue.Blocking))return;
   int column;
   record.AddIssue(columns.TryGetValue(target,out column)?column:-1,target,message);
  }

  static void ValidatePreparedLabel(BatchRecord record,LabelSettings label,Dictionary<string,int> columns) {
   try { Engine.Validate(label); }
   catch(OperationCanceledException){throw;}
   catch(Exception) {
    // Keep the usable artwork for the row inspector. The sanitized label
    // can only be assigned to the preview, never to the printable record.
    var invalidCodes=new List<int>();
    if(!label.HideBarcode&&!ValidField(record,columns,"code",()=>{
     if(Engine.Module(label)<1)throw new InvalidDataException(Loc.T("Kód je pre zvolené rozlíšenie príliš úzky."));
    })) {
     invalidCodes.Add(-1);
     label.Ean="";
    }
    for(int i=0;i<label.Symbols.Count;i++){
     WorkContext.Check();
     var symbol=label.Symbols[i];
     if((symbol.Code!=null||symbol.Matrix!=null)&&!ValidField(record,columns,"symbol:"+i,()=>{
      if(symbol.Code!=null)Engine.Validate(symbol.Code.Settings());
      if(symbol.Matrix!=null)Engine.ValidateMatrix(symbol,label.Dpi);
     }))invalidCodes.Add(-3-i);
    }
    if(invalidCodes.Count>0)Elements.Remove(label,invalidCodes);
    for(int i=0;i<label.Texts.Count;i++) {
     if(label.Texts[i].Text.Length<=4000)continue;
     AddFieldIssue(record,columns,"text:"+i,Loc.T("Text je príliš dlhý."));
     StyledText.ReplaceField(label.Texts[i],"");
    }
    Engine.Validate(label);
   }
   for(int i=0;i<label.Texts.Count;i++) {
    var text=label.Texts[i];
    if((text.MinimumFitSize<=0&&text.Styles.Count==0)||LabelChecks.TextFits(text,label.Dpi))continue;
    string target="text:"+i;
    string field=string.Format(Loc.T("Text {0}"),i+1);
    record.AddIssue(columns.TryGetValue(target,out int column)?column:-1,target,field+" — "+Loc.T("Text sa nezmestí do poľa"),false);
   }
   if(record.Error==null)record.Label=label;
   else record.PartialPreview=label;
  }

  static bool ValidField(BatchRecord record,Dictionary<string,int> columns,string target,Action validate) {
   try{validate();return true;}
   catch(OperationCanceledException){throw;}
   catch(Exception ex){AddFieldIssue(record,columns,target,ex.Message);return false;}
  }
 }
}
