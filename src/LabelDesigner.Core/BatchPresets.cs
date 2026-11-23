using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public sealed class BatchPreset {
  public string Name,SheetName;
  public bool Header=true;
  public char Separator=';';
  public int CodePage=65001;
  public List<ColumnBinding> Bindings=new List<ColumnBinding>();
  public BatchReference Reference;

  internal BatchPreset Copy() {
   var copy=(BatchPreset)MemberwiseClone();
   copy.Bindings=Bindings==null?null:Bindings.ConvertAll(binding=>binding==null?null:binding.Copy());
   copy.Reference=Reference==null?null:Reference.Copy();
   return copy;
  }
 }

 public sealed class BatchMappingResolution {
  public readonly Dictionary<string,int> Columns=new Dictionary<string,int>(StringComparer.Ordinal);
  public readonly Dictionary<string,string> Problems=new Dictionary<string,string>(StringComparer.Ordinal);
 }

 public sealed class BatchSampleRow {
  public int Row;
  public string[] Values;
 }

 public static class BatchPresets {
  public const int MaxPresets=24;
  public const int SampleLimit=5;

  public static BatchPreset Capture(string name,BatchSetup setup,DataTableSource table,bool workbook) {
   if(table==null||table.Rows.Count==0)throw new InvalidDataException(Loc.T("Najprv importujte tabuľku."));
   var preset=new BatchPreset {
    Name=(name??"").Trim(), Header=setup.Header, Separator=setup.Separator, CodePage=setup.CodePage,
    SheetName=workbook?table.Name:null
   };
   var bindings=new List<ColumnBinding>(setup.Bindings) {
    new ColumnBinding { Target="quantity",Column=setup.QuantityColumn },
    new ColumnBinding { Target="unique",Column=setup.UniqueColumn }
   };
   int width=0;
   foreach(var row in table.Rows)width=Math.Max(width,row.Length);
   foreach(var binding in bindings) {
    if(binding.Column<0)continue;
    string heading=preset.Header?Heading(BatchData.Value(table.Rows[0],binding.Column)):null;
    if(preset.Header&&heading.Length==0)
     throw new InvalidDataException(string.Format(Loc.T("Stĺpec {0} nemá názov v hlavičke."),binding.Column+1));
    preset.Bindings.Add(new ColumnBinding { Target=binding.Target,Column=binding.Column,HeaderName=heading });
   }
   if(preset.Bindings.Count==0)throw new InvalidDataException(Loc.T("Pre predvoľbu priraďte aspoň jeden stĺpec."));
   Validate(preset);
   var resolution=Resolve(preset,table,width);
   foreach(var problem in resolution.Problems.Values)throw new InvalidDataException(problem);
   return preset;
  }

  public static BatchMappingResolution Resolve(BatchPreset preset,DataTableSource table,int width) {
   var result=new BatchMappingResolution();
   var headings=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
   if(preset.Header&&table.Rows.Count>0) {
    var row=table.Rows[0];
    for(int i=0;i<row.Length;i++) {
     string heading=Heading(row[i]);
     if(heading.Length>0)headings[heading]=headings.ContainsKey(heading)?-1:i;
    }
   }
   foreach(var binding in preset.Bindings) {
    int column=binding.Column;
    string problem=null;
    if(preset.Header) {
     if(!headings.TryGetValue(Heading(binding.HeaderName),out column))
      problem=string.Format(Loc.T("Chýba stĺpec „{0}“."),binding.HeaderName);
     else if(column<0)problem=string.Format(Loc.T("Názov stĺpca „{0}“ je v hlavičke viackrát."),binding.HeaderName);
    } else if(column<0||column>=width) {
     problem=string.Format(Loc.T("Stĺpec na pozícii {0} chýba."),column+1);
    }
    if(problem==null)result.Columns.Add(binding.Target,column);
    else result.Problems.Add(binding.Target,problem);
   }
   return result;
  }

  public static List<BatchSampleRow> Sample(DataTableSource table,bool header,IList<int> columns) {
   var result=new List<BatchSampleRow>();
   for(int index=header?1:0;index<table.Rows.Count&&result.Count<SampleLimit;index++) {
    var values=new string[columns.Count];
    for(int col=0;col<columns.Count;col++)values[col]=BatchData.Value(table.Rows[index],columns[col]);
    result.Add(new BatchSampleRow { Row=table.SourceRow(index),Values=values });
   }
   return result;
  }

  internal static void ValidateAll(BatchSetup setup) {
   if(setup.Presets==null||setup.Presets.Count>MaxPresets)throw InvalidPreset();
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   long referenceBytes=0;
   foreach(var preset in setup.Presets) {
    Validate(preset);
    if(preset.Reference!=null) {
     BatchReferences.Validate(preset.Reference);
     referenceBytes+=preset.Reference.PackedRows.Length;
     if(referenceBytes>BatchReferences.MaxProjectBytes)throw new InvalidDataException(Loc.T("Referenčné dávky prekračujú limit projektu."));
    }
    if(!names.Add(preset.Name))throw InvalidPreset();
   }
   if(!string.IsNullOrEmpty(setup.ActivePresetName)&&!names.Contains(setup.ActivePresetName))throw InvalidPreset();
  }

  static void Validate(BatchPreset preset) {
   if(preset==null||string.IsNullOrWhiteSpace(preset.Name)||preset.Name.Length>80||preset.Name!=preset.Name.Trim()||
      preset.SheetName!=null&&preset.SheetName.Length>100||preset.CodePage!=65001&&preset.CodePage!=1250||
      preset.Separator!=';'&&preset.Separator!=','&&preset.Separator!='\t'||preset.Bindings==null||
      preset.Bindings.Count>73)throw InvalidPreset();
   var targets=new HashSet<string>(StringComparer.Ordinal);
   foreach(var binding in preset.Bindings) {
    if(binding==null||!ValidTarget(binding.Target)||!targets.Add(binding.Target)||
       binding.Column<0||binding.Column>=256||
       preset.Header&&(string.IsNullOrWhiteSpace(binding.HeaderName)||binding.HeaderName.Length>512||
        binding.HeaderName!=binding.HeaderName.Trim()))throw InvalidPreset();
    if(preset.Header)CheckText(binding.HeaderName);
   }
   CheckText(preset.Name);
  }

  internal static bool ValidTarget(string target) {
   if(target=="code"||target=="quantity"||target=="unique")return true;
   if(string.IsNullOrEmpty(target))return false;
   int index;
   if(target.StartsWith("text:",StringComparison.Ordinal))
    return int.TryParse(target.Substring(5),NumberStyles.None,CultureInfo.InvariantCulture,out index)&&index<50;
   return target.StartsWith("symbol:",StringComparison.Ordinal)&&
    int.TryParse(target.Substring(7),NumberStyles.None,CultureInfo.InvariantCulture,out index)&&index<20;
  }

  internal static string Heading(string value){return (value??"").Trim().Replace("\r\n","\n").Replace('\r','\n');}

  static void CheckText(string value) {
   try{XmlConvert.VerifyXmlChars(value);}
   catch(XmlException){throw InvalidPreset();}
  }

  static InvalidDataException InvalidPreset(){return new InvalidDataException(Loc.T("Neplatná predvoľba dávky."));}
 }
}
