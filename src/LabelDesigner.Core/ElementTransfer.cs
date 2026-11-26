using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;

namespace EanEtikety {
// A copied barcode retains its semantic data and is rendered by the same vector engine.
// Geometry uses the existing movable symbol model; no bitmap or platform clipboard object is stored.
public class BarcodeContent {
 public string Ean; public int Kind; public bool Digits,Gs1Mode;
 public double Width,BarHeight,DigitSize,Dpi,SourceX,SourceY;
 public List<AiValue> ApplicationIdentifiers=new List<AiValue>();
 public static BarcodeContent From(LabelSettings s){var c=new BarcodeContent{Ean=s.Ean,Kind=s.Kind,Digits=s.Digits,Gs1Mode=s.Gs1Mode,Width=s.BarWidth,BarHeight=s.BarHeight,DigitSize=s.DigitSize,Dpi=s.Dpi,SourceX=s.BarX,SourceY=s.BarY};foreach(var ai in s.ApplicationIdentifiers)c.ApplicationIdentifiers.Add(new AiValue{AI=ai.AI,Data=ai.Data});return c;}
 public LabelSettings Settings(){return new LabelSettings{Width=10,Height=10,BarX=SourceX,BarY=SourceY,Ean=Ean,Kind=Kind,Digits=Digits,Gs1Mode=Gs1Mode,BarWidth=Width,BarHeight=BarHeight,DigitSize=DigitSize,Dpi=Dpi,ApplicationIdentifiers=ApplicationIdentifiers};}
 public BarcodeContent Copy(){return From(Settings());}
}
public class ElementTransfer {
 public TextItem Text; public SymbolItem Symbol;
 public int SourceIndex=-2; public VisibilityRule Visibility,GroupVisibility;
 public bool ShouldSerializeSourceIndex()=>Visibility!=null||GroupVisibility!=null||SourceIndex!=-2;
 public static string Encode(ElementTransfer element){using(var w=new StringWriter()){new XmlSerializer(typeof(ElementTransfer)).Serialize(w,element);return w.ToString();}}
 public static ElementTransfer Decode(string xml){if(xml==null||xml.Length>64000000)throw new InvalidDataException(Loc.T("Neplatný prvok v schránke."));using(var r=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64000000})){var e=(ElementTransfer)new XmlSerializer(typeof(ElementTransfer)).Deserialize(r);if((e.Text==null)==(e.Symbol==null))throw new InvalidDataException(Loc.T("Neplatný prvok v schránke."));var s=LabelSettings.Default();s.Texts.Clear();if(e.Text!=null)s.Texts.Add(e.Text);else s.Symbols.Add(e.Symbol);if(e.Visibility!=null||e.Symbol?.ImageMapping!=null)Engine.ValidateDraft(s);else Engine.Validate(s);return e;}}
}

}
