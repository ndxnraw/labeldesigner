using System;
using System.Text;
using System.Collections.Generic;
namespace EanEtikety {
public class AiValue {public string AI {get;set;} public string Data {get;set;} public AiValue(){AI="01";Data="";}}
public class AiDefinition {public string Code,Name;public int Length;public bool Variable,Numeric,Checksum,Date;public override string ToString(){return "("+Code+") "+Loc.AiName(Name);}}
public static class Gs1 {
 public static readonly List<AiDefinition> Catalog=Create();
 static List<AiDefinition> Create(){var list=new List<AiDefinition>();Action<string,string,int,bool,bool,bool,bool> add=delegate(string ai,string name,int len,bool variable,bool numeric,bool checksum,bool date){list.Add(new AiDefinition{Code=ai,Name=name,Length=len,Variable=variable,Numeric=numeric,Checksum=checksum,Date=date});};
  add("00","SSCC – logistická jednotka",18,false,true,true,false);add("01","GTIN – obchodná položka",14,false,true,true,false);add("02","GTIN položiek v logistickej jednotke",14,false,true,true,false);
  add("10","Šarža / dávka",20,true,false,false,false);
  string[] dates={"11","13","15","16","17"},names={"Dátum výroby","Dátum balenia","Minimálna trvanlivosť","Predať do","Dátum exspirácie"};for(int i=0;i<dates.Length;i++)add(dates[i],names[i]+" (RRMMDD)",6,false,true,false,true);
  add("20","Variant produktu",2,false,true,false,false);add("21","Sériové číslo",20,true,false,false,false);add("22","Spotrebiteľský variant",20,true,false,false,false);add("30","Počet – premenlivé množstvo",8,true,true,false,false);add("37","Počet položiek v logistickej jednotke",8,true,true,false,false);
  add("240","Doplnkový identifikátor produktu",30,true,false,false,false);add("241","Číslo produktu zákazníka",30,true,false,false,false);add("250","Doplnkové sériové číslo",30,true,false,false,false);add("251","Odkaz na pôvodnú položku",30,true,false,false,false);add("400","Číslo objednávky zákazníka",30,true,false,false,false);
  add("410","GLN príjemcu",13,false,true,true,false);add("414","GLN fyzického miesta",13,false,true,true,false);
  for(int i=0;i<=5;i++){add("310"+i,"Čistá hmotnosť kg – "+i+" desatinných miest",6,false,true,false,false);add("320"+i,"Čistá hmotnosť lb – "+i+" desatinných miest",6,false,true,false,false);}return list;
 }
 public static AiDefinition Find(string code){foreach(var d in Catalog)if(d.Code==code)return d;throw new Exception(Loc.T("Nepodporovaný GS1 identifikátor: ")+code);}
 public static string Payload(List<AiValue> values){if(values==null||values.Count==0||values.Count>20)throw new Exception(Loc.T("GS1: pridajte 1 až 20 identifikátorov."));var b=new StringBuilder();var seen=new HashSet<string>();int length=0;for(int i=0;i<values.Count;i++){var item=values[i];if(item==null)throw new Exception(Loc.T("Neplatné pole GS1."));var d=Find(item.AI);string s=item.Data??"";if(!seen.Add(item.AI))throw new Exception(Loc.T("GS1: identifikátor (")+item.AI+Loc.T(") sa opakuje."));if(s.Length<1||s.Length>d.Length||(!d.Variable&&s.Length!=d.Length))throw new Exception("GS1 ("+d.Code+"): "+(d.Variable?Loc.T("1 až "):"")+d.Length+" "+(d.Numeric?Loc.T("číslic"):Loc.T("znakov"))+".");
   foreach(char c in s){bool allowed=d.Numeric?c>='0'&&c<='9':c>='0'&&c<='9'||c>='A'&&c<='Z'||c>='a'&&c<='z'||"!\"%&'()*+,-./:;<=>?_".IndexOf(c)>=0;if(!allowed)throw new Exception("GS1 ("+d.Code+Loc.T("): nepovolený znak. Použite ")+(d.Numeric?Loc.T("číslice 0–9."):Loc.T("GS1 znaky bez medzier a diakritiky.")));}
   if(d.Checksum){int sum=0,weight=3;for(int j=s.Length-2;j>=0;j--){sum+=(s[j]-'0')*weight;weight=4-weight;}if(s[s.Length-1]-'0'!=(10-sum%10)%10)throw new Exception("GS1 ("+d.Code+Loc.T("): nesprávna kontrolná číslica."));}
   if(d.Date){int yy=2000+int.Parse(s.Substring(0,2)),mm=int.Parse(s.Substring(2,2)),dd=int.Parse(s.Substring(4,2));if(mm<1||mm>12||dd>DateTime.DaysInMonth(yy,mm))throw new Exception("GS1 ("+d.Code+Loc.T("): neplatný dátum RRMMDD. Deň 00 označuje posledný deň mesiaca."));}
   b.Append(d.Code).Append(s);length+=d.Code.Length+s.Length;if(d.Variable&&i<values.Count-1)b.Append('\x1d');
  }if(length>48)throw new Exception(Loc.T("GS1-128: najviac 48 dátových znakov vrátane AI. Rozdeľte údaje do viacerých etikiet/kódov."));return b.ToString();
 }
 public static string Human(List<AiValue> values){Payload(values);var b=new StringBuilder();foreach(var item in values)b.Append('(').Append(item.AI).Append(')').Append(item.Data);return b.ToString();}
 public static List<string> Warnings(List<AiValue> values){var w=new List<string>();var ids=new HashSet<string>();foreach(var v in values)ids.Add(v.AI);if(ids.Contains("02")&&!ids.Contains("37"))w.Add(Loc.T("GS1 (02) vyžaduje aj (37) na označovanej jednotke."));if(ids.Contains("37")&&!ids.Contains("02"))w.Add(Loc.T("GS1 (37) vyžaduje aj identifikáciu obsahu (02) na označovanej jednotke."));if(ids.Contains("01")&&(ids.Contains("02")||ids.Contains("37")))w.Add(Loc.T("GS1: (01) sa nekombinuje s (02)/(37)."));foreach(var v in values){if((v.AI=="10"||v.AI=="11"||v.AI=="13"||v.AI=="15"||v.AI=="16"||v.AI=="17"||v.AI=="20"||v.AI=="30"||v.AI=="240"||v.AI=="241"||v.AI.StartsWith("310")||v.AI.StartsWith("320"))&&!ids.Contains("01")&&!ids.Contains("02"))w.Add("GS1 ("+v.AI+Loc.T("): skontrolujte súvisiaci GTIN na označovanej jednotke."));if((v.AI=="21"||v.AI=="22"||v.AI=="250"||v.AI=="251")&&!ids.Contains("01"))w.Add("GS1 ("+v.AI+Loc.T("): skontrolujte súvisiaci GTIN (01)."));if(v.AI=="250"&&!ids.Contains("21"))w.Add(Loc.T("GS1 (250) vyžaduje aj (21)."));}return w;}
}
public static partial class MoreBarcodes {
 public static List<int> Gs1Codes(string payload){int n=payload.Length;var cost=new int[n+1,2];var choice=new int[n,2];for(int i=n-1;i>=0;i--)for(int state=0;state<2;state++){if(payload[i]=='\x1d'){cost[i,state]=1+cost[i+1,state];choice[i,state]=2;continue;}bool pair=i+1<n&&char.IsDigit(payload[i])&&char.IsDigit(payload[i+1]);int b=(state==0?1:2)+cost[i+1,0],c=pair?(state==1?1:2)+cost[i+2,1]:100000;cost[i,state]=Math.Min(b,c);choice[i,state]=c<b?1:0;}
  int set=cost[0,1]<=cost[0,0]?1:0;var codes=new List<int>{set==1?105:104,102};for(int i=0;i<n;){int op=choice[i,set];if(op==2){codes.Add(102);i++;continue;}if(op!=set){codes.Add(op==1?99:100);set=op;}if(set==1){codes.Add((payload[i]-'0')*10+payload[i+1]-'0');i+=2;}else codes.Add(payload[i++]-32);}int checksum=codes[0];for(int i=1;i<codes.Count;i++)checksum+=i*codes[i];codes.Add(checksum%103);codes.Add(106);return codes;
 }
 public static string Gs1Bits(string payload){var b=new StringBuilder();foreach(int code in Gs1Codes(payload))b.Append(Widths(Patterns128[code]));return b.ToString();}
}
}
