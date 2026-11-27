using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public enum TextDataKind { Number, Date }
 public enum TextDecimalInput { Dot, Comma }
 public enum TextDateOrder { Iso, DayMonthDot, DayMonthSlash, MonthDaySlash }
 public enum TextDateDisplay { Localized, Iso, DayMonthDot, DayMonthSlash, MonthDaySlash }
 public enum TextInputKind : byte { Text, WorksheetNumber, WorksheetDate }

 public sealed class LabelDataFormat {
  internal const string IsoDate="yyyy-MM-dd";
  static readonly NumberFormatInfo CommaDecimal=new NumberFormatInfo{NumberDecimalSeparator=","};
  public TextDataKind Kind;
  public TextDecimalInput DecimalInput;
  public int Decimals=2;
  public string Unit;
  public TextDateOrder DateInput;
  public TextDateDisplay DateOutput;

  internal void Validate(){
   if(!Enum.IsDefined(Kind)||!Enum.IsDefined(DecimalInput)||!Enum.IsDefined(DateInput)||!Enum.IsDefined(DateOutput)||
      Decimals<0||Decimals>8||Unit?.Length>32||Unit?.IndexOfAny(new[]{'\r','\n','\t'})>=0)
    throw new InvalidDataException(Loc.T("Neplatný formát údajov."));
   if(Unit!=null)XmlConvert.VerifyXmlChars(Unit);
  }

  public static LabelDataFormat ForField(LabelSettings label,int index)=>
   index>=0?label.Metadata.Find(meta=>meta.Index==index)?.DataFormat:null;
  public static LabelDataFormat ForTarget(LabelSettings label,string target)=>
   Elements.TryFieldIndex(target,out int index)?ForField(label,index):null;
  internal static bool HasFields(LabelSettings label)=>label.Metadata.Exists(meta=>meta?.DataFormat!=null);

  public static CultureInfo CultureFor(LabelSettings label,string requestedLanguage=null){
   var languages=label.Languages;
   string code=languages?.Codes.Find(value=>LabelLanguages.SameCode(value,requestedLanguage))??
    (requestedLanguage==null?languages?.Selected:languages?.Fallback??languages?.Selected);
   code=code?.ToLowerInvariant() switch {
    null or "" or "sk"=>"sk-SK",
    "cz" or "cs"=>"cs-CZ",
    "en"=>"en-GB",
    "hu"=>"hu-HU",
    _=>code
   };
   try{
    var culture=CultureInfo.GetCultureInfo(code);
    return culture.IsNeutralCulture?CultureInfo.GetCultureInfo(CultureInfo.CreateSpecificCulture(code).Name):culture;
   }
   catch(CultureNotFoundException){return CultureInfo.GetCultureInfo("sk-SK");}
  }

  public bool TryFormat(string input,TextInputKind inputKind,CultureInfo culture,out string output,out string error){
   Validate();
   output="";error=null;
   try{XmlConvert.VerifyXmlChars(input??"");}
   catch(XmlException){error=Loc.T("Hodnota obsahuje nepovolený znak.");return false;}
   if(string.IsNullOrWhiteSpace(input))return true;
   string value=input.Trim();
   if(value.StartsWith("!ERROR:",StringComparison.Ordinal)){
    error=value;return false;
   }
   if(Kind==TextDataKind.Number){
    if(!TryNumber(value,inputKind,out decimal number)){
     error=NumberError(inputKind);
     return false;
    }
    output=number.ToString("F"+Decimals,culture);
    if(!string.IsNullOrWhiteSpace(Unit))output+=" "+Unit.Trim();
    return true;
   }
   if(TryDate(value,inputKind,out DateOnly date,out string pattern)){
    string display=DateOutput switch {
     TextDateDisplay.Iso=>IsoDate,
     TextDateDisplay.DayMonthDot=>"dd. MM. yyyy",
     TextDateDisplay.DayMonthSlash=>"dd/MM/yyyy",
     TextDateDisplay.MonthDaySlash=>"MM/dd/yyyy",
     _=>"d"
    };
    try{
     output=date.ToString(display,DateOutput==TextDateDisplay.Localized?culture:CultureInfo.InvariantCulture);
     return true;
    }catch(ArgumentOutOfRangeException){
     error=Loc.T("Dátum je mimo rozsahu zvoleného kalendára.");
     return false;
    }
   }
   error=string.Format(Loc.T("Zadajte platný dátum vo formáte {0}."),pattern);
   return false;
  }

  bool UsesComma(TextInputKind kind)=>DecimalInput==TextDecimalInput.Comma&&kind!=TextInputKind.WorksheetNumber;
  internal string NumberError(TextInputKind kind)=>Loc.T(UsesComma(kind)?
   "Zadajte číslo s desatinnou čiarkou.":"Zadajte číslo s desatinnou bodkou.");
  internal bool TryNumber(string value,TextInputKind kind,out decimal number)=>
   decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,
    UsesComma(kind)?CommaDecimal:CultureInfo.InvariantCulture.NumberFormat,out number);
  internal bool TryDate(string value,TextInputKind kind,out DateOnly date,out string pattern){
   pattern=kind==TextInputKind.WorksheetDate?IsoDate:DateInput switch {
    TextDateOrder.DayMonthDot=>"d.M.yyyy",TextDateOrder.DayMonthSlash=>"d/M/yyyy",
    TextDateOrder.MonthDaySlash=>"M/d/yyyy",_=>IsoDate
   };
   return DateOnly.TryParseExact(value,pattern,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out date);
  }

  // Apply after data-driven artwork is selected. Rules and image keys continue
  // to use expanded source values, while renderers receive formatted text once.
  internal static void ApplyFields(LabelSettings label,CultureInfo culture,Func<int,TextInputKind> inputKind,Action<int,string> report){
   foreach(var meta in label.Metadata){
    var format=meta.DataFormat;
    if(format==null)continue;
    if(meta.Index<0||meta.Index>=label.Texts.Count)throw new InvalidDataException(Loc.T("Neplatný formát údajov."));
    meta.DataFormat=null;
    var text=label.Texts[meta.Index];
    if(!format.TryFormat(text.Text,inputKind(meta.Index),culture,out string output,out string error))
     report(meta.Index,error);
    if(text.Text!=output)StyledText.ReplaceField(text,output);
   }
  }
 }
}
