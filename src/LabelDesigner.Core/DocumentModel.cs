using System;using System.Collections.Generic;
namespace EanEtikety {
public class TextItem {
 public bool AutoWidth {get;set;}
 public double MinimumFitSize {get;set;}
 public bool ShouldSerializeMinimumFitSize(){return MinimumFitSize!=0;}
 public double Rotation {get;set;}
 public string Text {get;set;}
 public string FontName {get;set;}
 public double X {get;set;} public double Y {get;set;}
 public double Width {get;set;} public double Height {get;set;}
 public double Size {get;set;} public bool Bold {get;set;} public bool Center {get;set;}
 public TextItem(){FontName="Arial";Text="";Width=54;Height=6;Size=11;}
}
public class LabelSettings {
 public BatchSetup Batch=new BatchSetup();public int ProjectVersion=310;public bool HideBarcode;public List<ElementMeta> Metadata=new List<ElementMeta>();public SheetLayout Layout=new SheetLayout();public List<VariableField> Variables=new List<VariableField>();
 public List<int> QuickPrintFields=new List<int>();
 public bool ShouldSerializeQuickPrintFields(){return QuickPrintFields!=null&&QuickPrintFields.Count>0;}
 public double BarRotation;
 public string Ean="8580000000016";
 public string Unit="mm";public bool DarkMode=true;
 public int Kind=13, Copies=1;
 public bool Gs1Mode=false;public List<AiValue> ApplicationIdentifiers=new List<AiValue>();
 public double Width=60, Height=45, Dpi=300;
 public double BarX=10, BarY=10, BarWidth=40, BarHeight=23.5, DigitSize=8;
 public bool Digits=true, Sheet=false;
 public bool UsePaper=false;public string PaperPreset="Label";public double MarginRight=10,MarginBottom=10;
 public double PageWidth=210, PageHeight=297, MarginX=10, MarginY=10, GapX=2, GapY=2;
 public List<TextItem> Texts=new List<TextItem>();
 public List<SymbolItem> Symbols=new List<SymbolItem>();
 // Keep the constructor's missing-field XML default; only new documents opt in.
 public static LabelSettings Default(){var s=new LabelSettings{DigitSize=12};s.Texts.Add(new TextItem{Text=Loc.T("Názov produktu"),X=3,Y=3,Bold=true,Center=true,AutoWidth=true});TextAutoLayout.Apply(s);return s;}
}
}
