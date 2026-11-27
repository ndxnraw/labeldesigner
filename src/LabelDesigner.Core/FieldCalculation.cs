using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public enum CalculationOperation { Add, Subtract, Multiply, Divide, Round, AddDays }

 public sealed class CalculationOperand {
  public int Field=-1; // -1 is a literal, -2 is a removed or unavailable source.
  public string Literal="";
  internal CalculationOperand Copy()=>(CalculationOperand)MemberwiseClone();
 }

 public sealed class FieldCalculation {
  static readonly LabelDataFormat NumberInput=new();
  static readonly LabelDataFormat DateInput=new(){Kind=TextDataKind.Date};
  public CalculationOperation Operation;
  public CalculationOperand Left=new(),Right=new();

  public bool ReturnsDate=>Operation==CalculationOperation.AddDays;
  public FieldCalculation Copy()=>new(){Operation=Operation,Left=Left.Copy(),Right=Right.Copy()};
  public static FieldCalculation ForField(LabelSettings label,int index)=>
   index>=0?label.Metadata.Find(meta=>meta.Index==index)?.Calculation:null;

  internal static bool HasFields(LabelSettings label)=>label.Metadata.Exists(meta=>meta?.Calculation!=null);

  public static HashSet<int> TextInputs(LabelSettings label){
   var inputs=new HashSet<int>();
   foreach(var meta in label.Metadata)
    if(meta.Calculation is { } formula){
     if(formula.Left.Field>=0)inputs.Add(formula.Left.Field);
     if(formula.Right.Field>=0)inputs.Add(formula.Right.Field);
    }
   return inputs;
  }

  internal void Validate(int count){
   if(!Enum.IsDefined(Operation)||!Valid(Left)||!Valid(Right))
    throw new InvalidDataException(Loc.T("Neplatná definícia výpočtu."));
   bool Valid(CalculationOperand operand){
    if(operand==null||operand.Field< -2||operand.Field>=count||operand.Literal==null||operand.Literal.Length>100)return false;
    XmlConvert.VerifyXmlChars(operand.Literal);
    return true;
   }
  }

  public FieldCalculation Remap(IReadOnlyDictionary<int,int> indices){
   var copy=Copy();
   foreach(var operand in new[]{copy.Left,copy.Right})
    if(operand.Field>=0)operand.Field=indices.GetValueOrDefault(operand.Field,-2);
   return copy;
  }

  internal static void Apply(LabelSettings label,Dictionary<int,TextInputKind> kinds,Func<int,bool> invalid,
      Action<int,int,string> report){
   Dictionary<int,FieldCalculation> formulas=null;
   foreach(var meta in label.Metadata)
    if(meta.Calculation!=null){(formulas??=new()).Add(meta.Index,meta.Calculation);meta.Calculation=null;}
   if(formulas==null)return;
   var active=new HashSet<int>();
   var completed=new Dictionary<int,bool>();
   var failedSources=new Dictionary<int,int>();

   bool Evaluate(int field){
    WorkContext.Check();
    if(completed.TryGetValue(field,out bool result))return result;
    if(!formulas.TryGetValue(field,out var formula))return !invalid(field);
    active.Add(field);
    int source=-1;
    try{
     var left=Read(formula.Left,formula.ReturnsDate);
     var right=Read(formula.Right,false);
     source=formula.Right.Field;
     if(formula.ReturnsDate){
      int offset=Whole(right.Number,-3652058,3652058,Loc.T("Posun dátumu musí byť celý počet dní."));
      StyledText.ReplaceField(label.Texts[field],left.Date.AddDays(offset).ToString(LabelDataFormat.IsoDate,CultureInfo.InvariantCulture));
      kinds[field]=TextInputKind.WorksheetDate;
     }else{
      decimal number=formula.Operation switch{
       CalculationOperation.Add=>left.Number+right.Number,
       CalculationOperation.Subtract=>left.Number-right.Number,
       CalculationOperation.Multiply=>left.Number*right.Number,
       CalculationOperation.Divide=>left.Number/right.Number,
       CalculationOperation.Round=>decimal.Round(left.Number,Whole(right.Number,0,8,
        Loc.T("Počet desatinných miest musí byť celé číslo od 0 do 8.")),MidpointRounding.AwayFromZero),
       _=>throw new InvalidDataException(Loc.T("Neplatná definícia výpočtu."))
      };
      StyledText.ReplaceField(label.Texts[field],number.ToString("0.############################",CultureInfo.InvariantCulture));
      kinds[field]=TextInputKind.WorksheetNumber;
     }
     return completed[field]=true;
    }catch(Exception ex) when(ex is InvalidDataException or ArithmeticException or ArgumentOutOfRangeException){
     string message=ex switch{
      DivideByZeroException=>Loc.T("Delenie nulou."),
      OverflowException or ArgumentOutOfRangeException=>Loc.T("Výsledok výpočtu je mimo rozsahu."),
      _=>ex.Message
     };
     if(source>=0)message=string.Format(Loc.T("Vstup z poľa {0}: {1}"),source+1,message);
     StyledText.ReplaceField(label.Texts[field],"");
     failedSources[field]=source;
     report(field,source,string.Format(Loc.T("Výpočet poľa {0}: {1}"),field+1,message));
     return completed[field]=false;
    }finally{active.Remove(field);}

    (decimal Number,DateOnly Date) Read(CalculationOperand operand,bool date){
     source=operand.Field;
     string value=operand.Literal;
     var kind=TextInputKind.Text;
     var format=date?DateInput:NumberInput;
     if(source!=-1){
      int index=source;
      if(index<0||index>=label.Texts.Count)throw new InvalidDataException(Loc.T("Chýba zdrojové pole výpočtu."));
      if(active.Contains(index))throw new InvalidDataException(Loc.T("Polia výpočtu tvoria cyklus."));
      if(!Evaluate(index)){
       source=failedSources.GetValueOrDefault(index,index);
       throw new InvalidDataException(string.Format(Loc.T("Pole {0} nemá platnú vstupnú hodnotu."),index+1));
      }
      value=label.Texts[index].Text;
      kind=kinds.GetValueOrDefault(index);
      var sourceFormat=LabelDataFormat.ForField(label,index);
      if(sourceFormat?.Kind==format.Kind)format=sourceFormat;
     }
     if(string.IsNullOrWhiteSpace(value))throw new InvalidDataException(Loc.T("Chýba vstupná hodnota."));
     value=value.Trim();
     if(date){
      if(format.TryDate(value,kind,out var parsed,out string pattern))return (0,parsed);
      throw new InvalidDataException(string.Format(Loc.T("Zadajte platný dátum vo formáte {0}."),pattern));
     }
     if(format.TryNumber(value,kind,out decimal number))return (number,default);
     throw new InvalidDataException(format.NumberError(kind));
    }
   }
   foreach(int field in formulas.Keys)Evaluate(field);
  }

  static int Whole(decimal number,int min,int max,string message){
   if(number<min||number>max||decimal.Truncate(number)!=number)throw new InvalidDataException(message);
   return (int)number;
  }
 }
}
