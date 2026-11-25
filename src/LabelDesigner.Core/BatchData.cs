using System;using System.IO;using System.IO.Compression;using System.Xml;using System.Collections.Generic;using System.Globalization;using System.Text;using System.Text.RegularExpressions;
namespace EanEtikety {
public class BatchSetup {
 public List<ColumnBinding> Bindings=new List<ColumnBinding>();
 public int QuantityColumn=-1,UniqueColumn=-1,CodePage=65001,GeneratedCount=1;
 public bool Header=true;
 public char Separator=';';
 public string SheetName="",ActivePresetName;
 public List<BatchPreset> Presets=new List<BatchPreset>();
 public BatchSetup Copy(){
  var copy=(BatchSetup)MemberwiseClone();
  copy.Bindings=Bindings==null?null:Bindings.ConvertAll(binding=>binding==null?null:binding.Copy());
  copy.Presets=Presets==null?null:Presets.ConvertAll(preset=>preset==null?null:preset.Copy());
  return copy;
 }
}
public class DataTableSource {
 public string Name;
 public List<string[]> Rows=new List<string[]>();
 internal readonly List<int> CsvLines=new List<int>();
 public bool HasCsvLineNumbers { get { return CsvLines.Count>0; } }
 internal readonly List<int> ReferenceRows=new List<int>();
 public int SourceRow(int index) { return index<ReferenceRows.Count?ReferenceRows[index]:index<CsvLines.Count?CsvLines[index]:index+1; }
}
public class ColumnBinding {
 public string Target,HeaderName;public int Column=-1;
 internal ColumnBinding Copy(){return (ColumnBinding)MemberwiseClone();}
}
public class BatchRecord {
 public int Row,Count=1;
 public LabelSettings Label;
 public string Error,VariableValues;
 public DateTime? PreparedAt;
 public readonly List<BatchInputIssue> Issues=new List<BatchInputIssue>();
 internal LabelSettings PartialPreview;
 public LabelSettings PreviewLabel { get { return Label??PartialPreview; } }
 internal void AddIssue(int column,string target,string message,bool blocking=true) {
  Issues.Add(new BatchInputIssue { Row=Row, Column=column, Target=target, Message=message, Blocking=blocking });
  if(blocking&&Error==null)Error=message;
 }
}
public sealed class BatchInputIssue {
 public int Row,Column;
 public string Message;
 public bool Blocking;
 internal string Target;
}
public static partial class BatchData {
 public static DataTableSource Csv(string path,char separator,int codePage){if(new FileInfo(path).Length>16000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));string data;using(var input=new CheckedReadStream(File.OpenRead(path)))using(var reader=new StreamReader(input,codePage==65001?new UTF8Encoding(false,true):TextEncodings.Get(codePage),true))data=reader.ReadToEnd();return CsvText(data,separator);}
 public static DataTableSource CsvText(string data,char separator) {
  var result=new DataTableSource { Name="CSV" };
  var row=new List<string>();
  var field=new StringBuilder();
  bool quoted=false,afterQuote=false;
  int line=1,rowLine=1;
  Action finishField=delegate {
   row.Add(field.ToString());field.Clear();afterQuote=false;
   if(row.Count>256)throw CsvError(rowLine,"Príliš veľa stĺpcov");
  };
  Action finishRow=delegate {
   finishField();
   if(result.Rows.Count>=10001)throw CsvError(rowLine,"Najviac 10000 záznamov");
   result.Rows.Add(row.ToArray());result.CsvLines.Add(rowLine);row.Clear();
  };
  for(int i=0;i<data.Length;i++) {
   if((i&4095)==0)WorkContext.Report("Načítavam CSV…",i,data.Length);
   char c=data[i];
   if(c=='\r'||c=='\n') {
    bool crlf=c=='\r'&&i+1<data.Length&&data[i+1]=='\n';
    if(quoted){field.Append(c);if(crlf)field.Append('\n');}
    else{finishRow();rowLine=line+1;}
    line++;
    if(crlf)i++;
    continue;
   }
   if(quoted) {
    if(c!='"')field.Append(c);
    else if(i+1<data.Length&&data[i+1]=='"'){field.Append('"');i++;}
    else{quoted=false;afterQuote=true;}
    continue;
   }
   if(c=='"') {
    if(field.Length>0||afterQuote)throw CsvError(rowLine,"Neplatné úvodzovky CSV");
    quoted=true;
   } else if(c==separator)finishField();
   else if(!afterQuote)field.Append(c);
   else if(!char.IsWhiteSpace(c))throw CsvError(rowLine,"Neplatné CSV");
  }
  if(quoted)throw CsvError(rowLine,"Neuzavreté úvodzovky CSV");
  if(field.Length>0||row.Count>0||afterQuote)finishRow();
  WorkContext.Report("Načítavam CSV…",data.Length,data.Length);
  return result;
 }
 static InvalidDataException CsvError(int line,string message) {
  return new InvalidDataException(Loc.T("Riadok CSV")+" "+line+" — "+Loc.T(message));
 }
 static XmlDocument Xml(ZipArchive z,string path){var entry=z.GetEntry(path);if(entry==null)throw new InvalidDataException(Loc.T("Neplatný XLSX")+": "+path);if(entry.Length>32000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));var d=new XmlDocument{XmlResolver=null};using(var s=entry.Open())using(var checkedStream=new CheckedReadStream(s))using(var reader=XmlReader.Create(checkedStream,new XmlReaderSettings{XmlResolver=null,DtdProcessing=DtdProcessing.Prohibit,MaxCharactersInDocument=32000000}))d.Load(reader);return d;}
 static List<XmlElement> All(XmlNode n,string local){var result=new List<XmlElement>();foreach(XmlNode node in n.SelectNodes(".//*[local-name()='"+local+"']"))if(node is XmlElement)result.Add((XmlElement)node);WorkContext.Check();return result;}
 public static List<DataTableSource> Xlsx(string path){if(new FileInfo(path).Length>32000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));using(var z=ZipFile.OpenRead(path)){long total=0;foreach(var entry in z.Entries){WorkContext.Check();total+=entry.Length;if(total>64000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));}var book=Xml(z,"xl/workbook.xml");var rel=Xml(z,"xl/_rels/workbook.xml.rels");var targets=new Dictionary<string,string>();foreach(var e in All(rel,"Relationship")){if(e.GetAttribute("TargetMode")=="External")continue;string target=e.GetAttribute("Target").Replace('\\','/');if(target.Contains(".."))throw new InvalidDataException(Loc.T("Neplatný XLSX"));targets[e.GetAttribute("Id")]=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;}
  var strings=new List<string>();if(z.GetEntry("xl/sharedStrings.xml")!=null)foreach(var e in All(Xml(z,"xl/sharedStrings.xml"),"si")){var text=new StringBuilder();foreach(var t in All(e,"t"))text.Append(t.InnerText);strings.Add(text.ToString());}
  var formats=new Dictionary<int,string>();var styleIds=new List<int>();if(z.GetEntry("xl/styles.xml")!=null){var styles=Xml(z,"xl/styles.xml");foreach(var e in All(styles,"numFmt"))formats[int.Parse(e.GetAttribute("numFmtId"))]=e.GetAttribute("formatCode");foreach(var e in All(styles,"cellXfs"))foreach(XmlNode n in e.ChildNodes)if(n is XmlElement){int id;int.TryParse(((XmlElement)n).GetAttribute("numFmtId"),out id);styleIds.Add(id);}}
  bool date1904=false;foreach(var e in All(book,"workbookPr"))date1904=e.GetAttribute("date1904")=="1"||e.GetAttribute("date1904")=="true";var output=new List<DataTableSource>();foreach(var sheet in All(book,"sheet")){WorkContext.Report("Načítavam XLSX…",output.Count,All(book,"sheet").Count);string id=sheet.GetAttribute("id","http://schemas.openxmlformats.org/officeDocument/2006/relationships");if(!targets.ContainsKey(id))throw new InvalidDataException(Loc.T("Neplatný XLSX"));var data=new DataTableSource{Name=sheet.GetAttribute("name")};var sourceRows=All(Xml(z,targets[id]),"row");int readRows=0;foreach(var row in sourceRows){WorkContext.Report("Načítavam riadky XLSX…",readRows++,sourceRows.Count);int rowIndex;int.TryParse(row.GetAttribute("r"),out rowIndex);if(rowIndex>10001)throw new InvalidDataException(Loc.T("Najviac 10000 záznamov"));while(data.Rows.Count<rowIndex-1)data.Rows.Add(new string[0]);var values=new List<string>();foreach(var cell in All(row,"c")){WorkContext.Check();int column=0;foreach(char c in cell.GetAttribute("r")){if(c<'A'||c>'Z')break;column=column*26+c-'A'+1;}if(column==0)column=values.Count+1;if(column>256)throw new InvalidDataException(Loc.T("Príliš veľa stĺpcov"));while(values.Count<column)values.Add("");string type=cell.GetAttribute("t"),value="";var raw=cell.SelectSingleNode("*[local-name()='v']");if(raw!=null)value=raw.InnerText;
   if(type=="s"){int index;if(!int.TryParse(value,out index)||index<0||index>=strings.Count)throw new InvalidDataException(Loc.T("Neplatný XLSX"));value=strings[index];}else if(type=="inlineStr"){value="";foreach(var t in All(cell,"t"))value+=t.InnerText;}else if(type=="e")value="!ERROR: "+value;else if(cell.SelectSingleNode("*[local-name()='f']")!=null&&raw==null)value="!ERROR: "+Loc.T("Vzorec nemá uložený výsledok");else if(type!="str"&&type!="b"){
    int style;int.TryParse(cell.GetAttribute("s"),out style);int format=style>=0&&style<styleIds.Count?styleIds[style]:0;string code=formats.ContainsKey(format)?formats[format]:"";double number;if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)){
     string clean=Regex.Replace(code,"\"[^\"]*\"|\\\\.","");bool date=format>=14&&format<=22||format>=45&&format<=47||Regex.IsMatch(clean,"[yd]",RegexOptions.IgnoreCase);if(date){try{value=(date1904?new DateTime(1904,1,1).AddDays(number):DateTime.FromOADate(number<60?number+1:number)).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}catch{value="!ERROR: "+Loc.T("Neplatný dátum");}}else if(Regex.IsMatch(code,@"^0+$"))value=number.ToString(code,CultureInfo.InvariantCulture);else if(value.IndexOfAny(new[]{'E','e'})>=0)value=number.ToString("0.############################",CultureInfo.InvariantCulture);
    }
   }values[column-1]=value;}data.Rows.Add(values.ToArray());}output.Add(data);}return output;}}
 public static string Value(string[] row,int column){return column>=0&&column<row.Length?row[column]:"";}
 public static Dictionary<string,string> Variables(LabelSettings template,int index,DateTime instant){var result=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var variable in template.Variables){if(!Regex.IsMatch(variable.Name??"",@"^[\p{L}_][\p{L}\p{N}_]*$")||result.ContainsKey(variable.Name))throw new InvalidDataException(Loc.T("Neplatný alebo duplicitný názov premennej"));string value;if(variable.Type=="Number"){if(variable.Digits<1||variable.Digits>30)throw new InvalidDataException(Loc.T("Neplatný počet číslic"));value=checked(variable.Start+variable.Step*index).ToString("D"+variable.Digits,CultureInfo.InvariantCulture);}else if(variable.Type=="Date")value=(variable.FixedDate?variable.Date:instant.Date).AddMonths(variable.Months).AddDays(variable.Days).ToString(variable.Format,CultureInfo.InvariantCulture);else if(variable.Type=="Fixed")value=variable.FixedValue;else throw new InvalidDataException(Loc.T("Neplatný typ premennej"));result.Add(variable.Name,variable.Prefix+value+variable.Suffix);}WorkContext.Check();return result;}
 internal static string Expand(string text,Dictionary<string,string> values){return StyledText.Variable.Replace(text??"",match=>VariableValue(match,values));}
 internal static void Expand(TextItem text,Dictionary<string,string> values){
  XmlConvert.VerifyXmlChars(text.Text??"");
  StyledText.Expand(text,match=>XmlConvert.VerifyXmlChars(VariableValue(match,values)));
 }
 static string VariableValue(Match match,Dictionary<string,string> values){
  string name=match.Groups[1].Value;
  if(!values.TryGetValue(name,out string value))throw new InvalidDataException(Loc.T("Neznáma premenná")+": "+name);
  return value;
 }
}
}
