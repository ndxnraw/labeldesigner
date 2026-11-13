using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;

namespace EanEtikety {
public class ElementEdit {
 public int Index;
 public double X,Y,Width,Height,Font,Rotation;
 public ElementEdit Copy(){return (ElementEdit)MemberwiseClone();}
}

}
