using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace EanEtikety {
 public sealed class QuickPrintDraft {
  public readonly LabelSettings Label;
  public readonly List<LabelIssue> Issues;
  public readonly IReadOnlySet<int> Hidden;
  readonly DateTime instant;
  internal QuickPrintDraft(LabelSettings label, List<LabelIssue> issues, DateTime instant, HashSet<int> hidden) {
   Label = label; Issues = issues; this.instant = instant; Hidden=hidden;
  }
  public bool CanPrint { get { return !Issues.Exists(issue => issue.Blocking); } }

  public OutputPlan CreatePlan(int quantity) {
   if (!CanPrint) throw new InvalidOperationException(Loc.T("Opravte údaje pred tlačou."));
   if (quantity < 1 || quantity > OutputPlan.MaxLabels) throw new ArgumentOutOfRangeException("quantity");
   return OutputPlan.Create(new List<BatchRecord> { new BatchRecord { Label = Label, Count = quantity, PreparedAt = instant } },Label,instant);
  }
 }

 public static class QuickPrint {
  public static List<int> Available(LabelSettings label) {
   return Elements.Indices(label).FindAll(index => index >= -1 || label.Symbols[-3-index].Code != null ||
    label.Symbols[-3-index].Matrix != null);
  }

  public static List<int> Editable(LabelSettings label)=>Available(label).FindAll(index=>!LabelLanguages.Translated(label,index));

  public static List<int> Selected(LabelSettings label) {
   var selected = new HashSet<int>(label.QuickPrintFields ?? new List<int>());
   return Editable(label).FindAll(selected.Contains);
  }

  public static string Name(LabelSettings label, int index) {
   if(index >= 0) return string.Format(Loc.T("Text {0}"),index+1);
   if(index == -1) return Loc.T("Hlavný kód");
   return string.Format(Loc.T("Kód {0} · {1}"),-2-index,label.Symbols[-3-index].Matrix==null ?
    Loc.T("Čiarový kód") : label.Symbols[-3-index].Matrix.Type);
  }

  public static string Value(LabelSettings label, int index)=>Value(label,index,false);
  internal static string Value(LabelSettings label,int index,bool forVisibility) {
   if(index >= 0) return label.Texts[index].Text;
   if(index == -1) return Engine.IsGs1(label) ? AiText(label.ApplicationIdentifiers,forVisibility) : label.Ean;
   var symbol=label.Symbols[-3-index];
   if(symbol.Matrix!=null) return symbol.Matrix.Data;
   return symbol.Code.Gs1Mode && symbol.Code.Kind==128 ? AiText(symbol.Code.ApplicationIdentifiers,forVisibility) : symbol.Code.Ean;
  }

  public static bool UsesAiLines(LabelSettings label,int index) {
   if(index==-1)return Engine.IsGs1(label);
   if(index>=-2)return false;
   var code=label.Symbols[-3-index].Code;
   return code!=null&&code.Gs1Mode&&code.Kind==128;
  }

  static string AiText(IEnumerable<AiValue> values,bool forVisibility) {
   return string.Join(Environment.NewLine,values.Where(ai=>!forVisibility||!string.IsNullOrWhiteSpace(ai.Data)).Select(ai=>"("+ai.AI+")"+ai.Data));
  }

  static List<AiValue> ParseAiLines(string value) {
   var identifiers=new List<AiValue>();
   foreach(string line in value.Split(new[]{"\r\n","\n","\r"},StringSplitOptions.RemoveEmptyEntries)){
    int end=line.IndexOf(')');
    if(!line.StartsWith("(",StringComparison.Ordinal)||end<3||end>5)
     throw new ArgumentException(Loc.T("Zadajte každý identifikátor na samostatný riadok vo formáte (AI)hodnota."));
    identifiers.Add(new AiValue{AI=line.Substring(1,end-1),Data=line.Substring(end+1)});
   }
   return identifiers;
  }

  static void ApplyValue(LabelSettings label,int index,string value,bool fromTemplate,Dictionary<string,string> variables) {
   Func<string,string> content=text=>XmlConvert.VerifyXmlChars(fromTemplate?BatchData.Expand(text,variables):text??"");
   if(index>=0){
    if(fromTemplate)BatchData.Expand(label.Texts[index],variables);
    else StyledText.Edit(label.Texts[index],XmlConvert.VerifyXmlChars(value??""));
    return;
   }
   var symbol=index<=-3?label.Symbols[-3-index]:null;
   if(symbol!=null&&symbol.Matrix!=null){symbol.Matrix.Data=content(value);return;}
   bool gs1=UsesAiLines(label,index);
   if(gs1){
    var ai=symbol==null?label.ApplicationIdentifiers:symbol.Code.ApplicationIdentifiers;
    if(fromTemplate)foreach(var item in ai)item.Data=content(item.Data);
    else if(symbol==null)label.ApplicationIdentifiers=ParseAiLines(content(value));
    else symbol.Code.ApplicationIdentifiers=ParseAiLines(content(value));
   }else if(symbol==null)label.Ean=content(value);
   else symbol.Code.Ean=content(value);
  }

  public static QuickPrintDraft Prepare(LabelSettings template, IDictionary<int,string> values, DateTime instant)=>Prepare(template,values,instant,null,false);
  public static QuickPrintDraft Prepare(LabelSettings template,IDictionary<int,string> values,DateTime instant,string language)=>Prepare(template,values,instant,language,false);
  public static QuickPrintDraft PrepareEditor(LabelSettings template,DateTime instant)=>Prepare(template,null,instant,null,true);

  static QuickPrintDraft Prepare(LabelSettings template,IDictionary<int,string> values,DateTime instant,string language,bool editor) {
   Engine.ValidateDraft(template);
   var label=JobPreparation.CloneForPreparation(template);
   var selected=new HashSet<int>(editor?new List<int>():Selected(template));
   var issues=new List<LabelIssue>();
   var variables=BatchData.Variables(template,0,instant);
   LabelLanguages.Resolve(label,language,(field,message)=>issues.Add(new LabelIssue{Element=field,Blocking=true,Message=message}));
   if(!editor)foreach(var text in label.Texts)text.AutoWidth=false;
   foreach(int index in Available(label)){
    WorkContext.Check();
    string original=Value(label,index), value=null;
    bool edited=selected.Contains(index)&&values!=null&&values.TryGetValue(index,out value)&&value!=original;
    try{ApplyValue(label,index,edited?value:original,!edited,variables);}
    catch(OperationCanceledException){throw;}
    catch(XmlException){issues.Add(new LabelIssue{Element=index,Blocking=true,Message=Loc.T("Hodnota obsahuje nepovolený znak.")});}
    catch(Exception ex){issues.Add(new LabelIssue{Element=index,Blocking=true,Message=ex.Message});}
   }
   var ruleErrors=new HashSet<string>(StringComparer.Ordinal);
   var hidden=JobPreparation.ProjectArtwork(label,variables,index=>issues.Exists(issue=>issue.Blocking&&issue.Element==index),
    (_,message)=>ruleErrors.Add(message));
   if(hidden.Count>0){
    issues.RemoveAll(issue=>hidden.Contains(issue.Element));
    var indices=Elements.Indices(label);
    foreach(var issue in issues)if(issue.Element!=-2)issue.Element=indices.Find(index=>Elements.SourceIndex(label,index)==issue.Element);
   }
   foreach(string message in ruleErrors)issues.Add(new LabelIssue{Blocking=true,Message=message});
   label.Variables.Clear();
   BatchReferences.Strip(label);
   if(editor)TextAutoLayout.Apply(label);
   foreach(var issue in LabelChecks.Inspect(label))
    if(!issues.Exists(item=>item.Blocking&&item.Element==issue.Element))issues.Add(issue);
   for(int index=0;index<label.Texts.Count;index++){
    if(!selected.Contains(Elements.SourceIndex(label,index)))continue;
    if(label.Texts[index].MinimumFitSize>0)continue;
    if(LabelChecks.TextFits(label.Texts[index],label.Dpi,true))continue;
    string message=Loc.T("Text sa nezmestí do poľa");
    var issue=issues.Find(item=>item.Element==index&&item.Message==message);
    if(issue==null)issues.Add(new LabelIssue{Element=index,Blocking=true,Message=message});
    else issue.Blocking=true;
   }
   if(label.HideBarcode&&label.Symbols.Count==0&&!label.Texts.Exists(text=>!string.IsNullOrWhiteSpace(text.Text))){
    string empty=Loc.T("Dokument neobsahuje nič na tlač.");
    if(!issues.Exists(issue=>issue.Blocking&&issue.Message==empty))issues.Add(new LabelIssue{Blocking=true,Message=empty});
   }
   return new QuickPrintDraft(label,issues,instant,hidden);
  }
 }
}
