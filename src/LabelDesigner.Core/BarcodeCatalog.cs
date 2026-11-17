// Extended Code 39 mapping adapted from ZXing Code39Writer.
// Copyright 2010 ZXing authors. Licensed under the Apache License, Version 2.0.
// https://www.apache.org/licenses/LICENSE-2.0
// See src/third-party/ZXing/ZXing-LICENSE.txt for the full license and disclaimer.
using System;using System.Collections.Generic;using System.Text;
namespace EanEtikety {
public sealed class BarcodeDefinition {
 public string Name,Category,Sample,MatrixType;public int Kind;public bool Gs1;
 public string Hint {get{return MatrixType==null?(Gs1?"GS1 AI: (01)12345678901231(10)LOT":MoreBarcodes.Hint(Kind)):BarcodeCatalog.MatrixHint(MatrixType);}}
 public override string ToString(){return Loc.T(Name);}
}
public static class BarcodeCatalog {
 public static readonly string[] MatrixTypes={"QR","Data Matrix","Aztec","PDF417","GS1 QR","GS1 Data Matrix"};
 public static bool MatrixSupported(string type){return Array.IndexOf(MatrixTypes,type)>=0;}
 public static bool Square(string type){return MatrixSupported(type)&&type!="PDF417";}
 public static string MatrixHint(string type){return type.StartsWith("GS1 ",StringComparison.Ordinal)?"GS1: (01)12345678901231(10)LOT(21)SERIAL. Použite podporované AI v zátvorkách; FNC1 sa vloží automaticky.":"Text alebo URL (UTF-8). Premenné zapisujte ako {serial}.";}
 public static readonly BarcodeDefinition[] All=Create();
 static BarcodeDefinition[] Create(){var list=new List<BarcodeDefinition>();foreach(int k in MoreBarcodes.Kinds)list.Add(new BarcodeDefinition{Name=MoreBarcodes.Name(k),Category=k==13||k==8||k==12||k==112?"EAN / UPC":k==114?"GS1":"1D",Kind=k,Sample=Sample(k)});list.Add(new BarcodeDefinition{Name="GS1-128",Category="GS1",Kind=128,Gs1=true,Sample="(01)12345678901231(10)LOT"});foreach(string type in MatrixTypes)list.Add(new BarcodeDefinition{Name=type,Category=type.StartsWith("GS1 ",StringComparison.Ordinal)?"GS1":"2D",MatrixType=type,Sample=type.StartsWith("GS1 ",StringComparison.Ordinal)?"(01)12345678901231(10)LOT":"https://example.com"});return list.ToArray();}
 public static string Sample(int kind){switch(kind){case 13:return "8580000000016";case 8:return "1234567";case 12:return "12345678901";case 14:case 114:return "1234567890123";case 112:return "04252614";case 25:case 1001:return "12345678";case 27:return "A123456A";case 139:return "Label + 123";case 1002:return "1234ABCD";default:return "ABC123";}}
 public static BarcodeDefinition[] Search(string text,string category,bool linearOnly){var result=new List<BarcodeDefinition>();foreach(var d in All)if((!linearOnly||d.MatrixType==null)&&(string.IsNullOrEmpty(category)||d.Category==category)&&(string.IsNullOrEmpty(text)||(d.Name+" "+Loc.T(d.Name)+" "+d.Category).IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0))result.Add(d);return result.ToArray();}
 // Printable subset of ISO/IEC 16388 extended encoding; mapping follows the
 // Apache-2.0 vendored ZXing Code39Writer. Always escape shift characters.
 public static string Extended39(string data){var b=new StringBuilder();foreach(char c in data){if(c<32||c>126)throw new ArgumentException("Code 39 Full ASCII: ASCII 32–126");if(c==' '||c=='-'||c=='.'||c>='0'&&c<='9'||c>='A'&&c<='Z')b.Append(c);else if(c=='@')b.Append("%V");else if(c=='`')b.Append("%W");else if(c<=','||c=='/'||c==':')b.Append('/').Append((char)('A'+c-33));else if(c<='?')b.Append('%').Append((char)('F'+c-59));else if(c<='_')b.Append('%').Append((char)('K'+c-91));else if(c<='z')b.Append('+').Append((char)('A'+c-97));else b.Append('%').Append((char)('P'+c-123));}return b.ToString();}
}
}
