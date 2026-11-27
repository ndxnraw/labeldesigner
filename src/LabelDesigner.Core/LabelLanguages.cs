using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public sealed class LanguageTranslation {
  public string Language,Text;
 }

 public sealed class LabelLanguages {
  public const string BatchTarget="language";
  public const int MaximumCodes=16;
  public List<string> Codes=new List<string>();
  public string Selected,Fallback;
  public LabelLanguages Copy()=>new LabelLanguages{Codes=new List<string>(Codes),Selected=Selected,Fallback=Fallback};

  public static bool IsCode(string code){
   if(string.IsNullOrEmpty(code)||code.Length>24)return false;
   var parts=code.Split('-');
   foreach(string part in parts){
    if(part.Length==0)return false;
    foreach(char c in part)if(!char.IsAsciiLetterOrDigit(c))return false;
   }
   return char.IsAsciiLetter(code[0]);
  }
  public static bool SameCode(string a,string b)=>string.Equals(a?.Trim(),b?.Trim(),StringComparison.OrdinalIgnoreCase);

  internal void Validate(){
   if(Codes==null||Codes.Count<1||Codes.Count>MaximumCodes||!Codes.TrueForAll(IsCode)||
     new HashSet<string>(Codes,StringComparer.OrdinalIgnoreCase).Count!=Codes.Count||
     !Codes.Exists(code=>SameCode(code,Selected))||
     !string.IsNullOrEmpty(Fallback)&&!Codes.Exists(code=>SameCode(code,Fallback)))
    throw new InvalidDataException(Loc.T("Neplatné nastavenie jazykov etikety."));
  }

  internal static void ValidateTranslations(List<LanguageTranslation> translations){
   if(translations==null)return;
   var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   if(translations.Count>MaximumCodes||translations.Exists(entry=>entry==null||!IsCode(entry.Language)||
     !keys.Add(entry.Language)||entry.Text==null||entry.Text.Length>4000))
    throw new InvalidDataException(Loc.T("Neplatný preklad textu."));
   foreach(var entry in translations)XmlConvert.VerifyXmlChars(entry.Text);
  }

  public static bool Translated(LabelSettings label,int index)=>index>=0&&label.Languages!=null&&
   label.Metadata.Exists(meta=>meta.Index==index&&meta.Translations?.Count>0);

  internal static void Resolve(LabelSettings label,string requested,Action<int,string> report){
   var languages=label.Languages;
   if(languages==null)return;
   languages.Validate();
   string wanted=(requested??languages.Selected).Trim();
   string code=languages.Codes.Find(item=>SameCode(item,wanted));
   foreach(var meta in label.Metadata){
    var entries=meta.Translations;
    if(meta.Calculation==null&&entries?.Count>0&&meta.Index>=0&&meta.Index<label.Texts.Count){
     string Translation(string language)=>string.IsNullOrEmpty(language)?null:
      entries.Find(entry=>SameCode(entry.Language,language))?.Text;
     string text=Translation(code);
     if(string.IsNullOrWhiteSpace(text))text=Translation(languages.Fallback);
     if(string.IsNullOrWhiteSpace(text)){
      string name=string.Format(Loc.T("Text {0}"),meta.Index+1);
      string message=wanted.Length==0?Loc.T("Jazyk etikety nie je zadaný."):
       string.Format(Loc.T("Chýba preklad pre jazyk {0}."),wanted.Length>24?wanted.Substring(0,24)+"…":wanted);
      report(meta.Index,name+" — "+message);
      text="";
     }
     var field=label.Texts[meta.Index];
     field.AutoWidth=false;
     StyledText.ReplaceField(field,text);
    }
    meta.Translations=null;
   }
   label.Languages=null;
  }
 }
}
