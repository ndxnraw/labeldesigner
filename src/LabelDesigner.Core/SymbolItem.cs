using System;
using System.Collections.Generic;
using System.Drawing;

namespace EanEtikety {
public class SymbolItem {public double Rotation {get;set;} public BarcodeContent Code {get;set;} public ImageContent Image {get;set;} public ImageMapping ImageMapping {get;set;} public MatrixContent Matrix {get;set;} public ShapeContent Shape {get;set;} public string Name {get{return ImageMapping!=null?Loc.T("Obrázok podľa údajov"):Image!=null?Loc.T("Obrázok"):Matrix!=null?Matrix.Type:Shape!=null?Loc.T(Shape.IsLine?"Čiara":"Obdĺžnik"):Code==null?Loc.T("Symbol Zálohované"):MoreBarcodes.Name(Code.Kind);}} public double X {get;set;} public double Y {get;set;} public double Width {get;set;} public double Height {get;set;} public SymbolItem(){X=3;Y=3;Width=16;Height=16*DepositArt.Aspect;}internal SymbolItem WithoutDataDrivenImages(){var copy=(SymbolItem)MemberwiseClone();copy.ImageMapping=null;if(Image?.FitFrame==true)copy.Image=null;return copy;}public SymbolItem Copy(){var copy=(SymbolItem)MemberwiseClone();if(Code!=null)copy.Code=Code.Copy();if(Image!=null)copy.Image=Image.Copy();if(ImageMapping!=null)copy.ImageMapping=ImageMapping.Copy();if(Matrix!=null)copy.Matrix=ProjectXml.Decode<MatrixContent>(ProjectXml.Encode(Matrix));if(Shape!=null)copy.Shape=Shape.Copy();return copy;}}

}
