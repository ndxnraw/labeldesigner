using System;
using System.Collections.Generic;
using System.Drawing;

namespace EanEtikety {
public class SymbolItem {public double Rotation {get;set;} public BarcodeContent Code {get;set;} public ImageContent Image {get;set;} public MatrixContent Matrix {get;set;} public string Name {get{return Image!=null?Loc.T("Obrázok"):Matrix!=null?Matrix.Type:Code==null?Loc.T("Symbol Zálohované"):MoreBarcodes.Name(Code.Kind);}} public double X {get;set;} public double Y {get;set;} public double Width {get;set;} public double Height {get;set;} public SymbolItem(){X=3;Y=3;Width=16;Height=16*DepositArt.Aspect;}public SymbolItem Copy(){var copy=(SymbolItem)MemberwiseClone();if(Code!=null)copy.Code=Code.Copy();if(Image!=null)copy.Image=ProjectXml.Decode<ImageContent>(ProjectXml.Encode(Image));if(Matrix!=null)copy.Matrix=ProjectXml.Decode<MatrixContent>(ProjectXml.Encode(Matrix));return copy;}}

}
