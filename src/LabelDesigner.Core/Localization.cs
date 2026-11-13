using System;using System.IO;using System.Collections.Generic;using System.Reflection;
namespace EanEtikety {
public static class Loc {
 public static readonly string[] Codes={"sk","en","cs","hu"};public static string Language="sk";
 static readonly Dictionary<string,string[]> translations=new Dictionary<string,string[]>();
 static readonly Dictionary<string,string> original=new Dictionary<string,string>();
 static Loc(){using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("translations.tsv"),System.Text.Encoding.UTF8)){string row;while((row=reader.ReadLine())!=null){if(row.Length==0)continue;var parts=row.Split('\t');if(parts.Length!=4)throw new InvalidDataException("Translation row must have four columns.");for(int i=0;i<4;i++)parts[i]=parts[i].Replace("\\r\\n","\r\n");translations.Add(parts[0],parts);foreach(var value in parts)if(!original.ContainsKey(value))original.Add(value,parts[0]);}}}
 public static string T(string value){if(value==null)return null;string key;bool cm=value.Contains("(cm)");string normalized=cm?value.Replace("(cm)","(mm)"):value;if(!original.TryGetValue(normalized,out key))key=normalized;string[] row;int index=Array.IndexOf(Codes,Language);string result=translations.TryGetValue(key,out row)?row[Math.Max(0,index)]:normalized;return cm?result.Replace("(mm)","(cm)"):result;}
 public static string AiName(string value){string result=T(value);if(result!=value)return result;foreach(string code in new[]{" (RRMMDD)"})if(value.EndsWith(code))return T(value.Substring(0,value.Length-code.Length))+(Language=="en"?" (YYMMDD)":Language=="hu"?" (ÉÉHHNN)":" (RRMMDD)");foreach(string unit in new[]{"kg","lb"}){string prefix="Čistá hmotnosť "+unit+" – ";if(value.StartsWith(prefix))return T(prefix)+value.Substring(prefix.Length).Replace(" desatinných miest",T(" desatinných miest"));}return result;}
}
}
