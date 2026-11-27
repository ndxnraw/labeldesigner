using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public sealed class LabelScenarioValue {
  public int Element;
  public string Value="";
  public TextInputKind InputKind;
  public string SourceError;
  internal LabelScenarioValue Copy()=>(LabelScenarioValue)MemberwiseClone();
 }

 public sealed class LabelScenario {
  public const int MaximumCount=32;
  public string Id=Guid.NewGuid().ToString("N");
  public string Name="";
  public string Language;
  public List<LabelScenarioValue> Values=new();
  public override string ToString()=>Name;

  public LabelScenario Copy()=>new(){
   Id=Id,Name=Name,Language=Language,Values=Values.ConvertAll(value=>value.Copy())
  };

  public static LabelScenarioValue FromCell(DataTableSource table,int row,int column,int element){
   var cells=table.Rows[row];
   var value=new LabelScenarioValue{Element=element,InputKind=table.InputKind(row,column)};
   if(column>=cells.Length)value.SourceError=Loc.T("V riadku chýba namapovaný stĺpec.");
   else if(!table.HasCsvLineNumbers&&cells[column].StartsWith("!ERROR:",StringComparison.Ordinal))value.SourceError=cells[column];
   else value.Value=cells[column];
   return value;
  }

  public static void Validate(List<LabelScenario> scenarios){
   var ids=new HashSet<string>(StringComparer.Ordinal);
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   long characters=0;
   if(scenarios==null||scenarios.Count>MaximumCount)
    throw new InvalidDataException(Loc.T("Príliš veľa testovacích scenárov."));
   foreach(var scenario in scenarios){
    if(scenario==null||!Guid.TryParseExact(scenario.Id,"N",out _)||!ids.Add(scenario.Id)||
       string.IsNullOrWhiteSpace(scenario.Name)||scenario.Name.Length>100||!names.Add(scenario.Name.Trim())||
       scenario.Language?.Length>100||scenario.Values==null||scenario.Values.Count>256)
     throw new InvalidDataException(Loc.T("Každý scenár potrebuje jedinečný názov a platné údaje."));
    XmlConvert.VerifyXmlChars(scenario.Name);
    if(scenario.Language!=null)XmlConvert.VerifyXmlChars(scenario.Language);
    var elements=new HashSet<int>();
    foreach(var value in scenario.Values){
     if(value==null||value.Element==-2||!elements.Add(value.Element)||value.Value==null||
        value.Value.Length>4000||value.SourceError?.Length>4000||!Enum.IsDefined(value.InputKind))
      throw new InvalidDataException(Loc.T("Neplatná hodnota testovacieho scenára."));
     XmlConvert.VerifyXmlChars(value.Value);
     if(value.SourceError!=null)XmlConvert.VerifyXmlChars(value.SourceError);
     characters+=value.Value.Length+(value.SourceError?.Length??0);
    }
   }
   if(characters>2000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));
  }
 }
}
