using System;
using System.Text;
using System.Collections.Generic;

namespace EanEtikety {
public static partial class MoreBarcodes {
 public static readonly int[] Kinds={13,8,12,128,39,25,14};
 public static readonly string[] Names={"EAN-13","EAN-8","UPC-A","Code 128","Code 39","ITF (2 z 5)","EAN14 / ITF-14"};
 public static string Name(int kind){int i=Array.IndexOf(Kinds,kind);return i<0?Loc.T("Neznámy kód"):Names[i];}
 public static string Hint(int kind){switch(kind){case 14:return Loc.T("GTIN-14: 13 alebo 14 číslic, kód ITF-14 s ochrannými pásmi.");case 13:return Loc.T("12 alebo 13 číslic; kontrolná číslica sa doplní alebo overí.");case 8:return Loc.T("7 alebo 8 číslic; kontrolná číslica sa doplní alebo overí.");case 12:return Loc.T("11 alebo 12 číslic; kontrolná číslica sa doplní alebo overí.");case 128:return Loc.T("1–80 znakov ASCII: písmená, číslice, medzery a symboly. Bez diakritiky.");case 39:return Loc.T("1–40 znakov: A–Z, 0–9, medzera a - . $ / + %. Bez kontrolnej číslice.");case 25:return Loc.T("Párny počet číslic (2–80). Bez kontrolnej číslice; nejde o ITF-14.");default:return "";}}
 public static string Validate(string value,int kind){
  string s=value??"";int max=kind==39?40:80;
  if(kind!=128&&kind!=39&&kind!=25)throw new Exception(Loc.T("Nepodporovaný typ čiarového kódu."));
  if(s.Length==0||s.Length>max)throw new Exception(Name(kind)+Loc.T(": zadajte 1 až ")+max+" znakov.");
  foreach(char c in s){if(kind==128&&(c<32||c>126))throw new Exception(Hint(kind));if(kind==39&&Alphabet39.IndexOf(c)<0)throw new Exception(Hint(kind));if(kind==25&&(c<'0'||c>'9'))throw new Exception(Hint(kind));}
  if(kind==25&&s.Length%2!=0)throw new Exception(Hint(kind));
  return s;
 }
 static string Widths(string widths){var b=new StringBuilder();bool black=true;foreach(char c in widths){b.Append(black?'1':'0',c-'0');black=!black;}return b.ToString();}
 public static string Bits(string value,int kind){string s=Validate(value,kind);var b=new StringBuilder();
  if(kind==128){bool numeric=s.Length%2==0;foreach(char c in s)numeric&=c>='0'&&c<='9';var codes=new List<int>();codes.Add(numeric?105:104);if(numeric){for(int i=0;i<s.Length;i+=2)codes.Add((s[i]-'0')*10+s[i+1]-'0');}else foreach(char c in s)codes.Add(c-32);int sum=codes[0];for(int i=1;i<codes.Count;i++)sum+=i*codes[i];codes.Add(sum%103);codes.Add(106);foreach(int c in codes)b.Append(Widths(Patterns128[c]));}
  else if(kind==39){string full="*"+s+"*";for(int i=0;i<full.Length;i++){if(i>0)b.Append('0');int index=full[i]=='*'?43:Alphabet39.IndexOf(full[i]);b.Append(Widths(Patterns39[index]));}}
  else {b.Append("1010");for(int i=0;i<s.Length;i+=2){string bars=PatternsItf[s[i]-'0'],spaces=PatternsItf[s[i+1]-'0'];for(int j=0;j<5;j++){b.Append('1',bars[j]-'0');b.Append('0',spaces[j]-'0');}}b.Append("11101");}
  return b.ToString();
 }
}
}
