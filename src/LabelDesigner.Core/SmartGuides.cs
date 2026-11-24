using System;
using System.Collections.Generic;
namespace EanEtikety {
// Document-space geometry; only the tolerance is converted from screen pixels.
// No dependency on controls, rendering, the printer, or the zoom implementation.
public sealed class AlignmentGuide {
 public bool Vertical;public double Position,Start,End;
}
public sealed class SnapResult {
 public ElementEdit Element;public AlignmentGuide X,Y;
}
public static class SmartGuides {
 public const double TolerancePixels=6;
 static readonly double[] Anchors={.5,0,1};
 public static SnapResult CalculateSnap(ElementEdit moving,IList<ElementEdit> targets,double canvasWidth,double canvasHeight,double pixelsPerMmX,double pixelsPerMmY){
  return CalculateSnap(moving,targets,canvasWidth,canvasHeight,pixelsPerMmX,pixelsPerMmY,true);
 }
 public static SnapResult CalculateSnap(ElementEdit moving,IList<ElementEdit> targets,double canvasWidth,double canvasHeight,double pixelsPerMmX,double pixelsPerMmY,bool apply,double tolerancePixels=TolerancePixels,Func<bool,double,bool> allowsCorrection=null){
  if(moving==null)throw new ArgumentNullException("moving");
  var result=new SnapResult{Element=moving.Copy()};
  if(pixelsPerMmX<=0||pixelsPerMmY<=0||double.IsNaN(pixelsPerMmX)||double.IsNaN(pixelsPerMmY))return result;
  var canvas=new ElementEdit{Index=-2,Width=canvasWidth,Height=canvasHeight};
  double dx,dy;result.X=Find(moving,targets,canvas,true,tolerancePixels/pixelsPerMmX,allowsCorrection,out dx);result.Y=Find(moving,targets,canvas,false,tolerancePixels/pixelsPerMmY,allowsCorrection,out dy);
  if(apply){result.Element.X+=dx;result.Element.Y+=dy;}
  Extend(result.X,result.Element,targets,canvas);Extend(result.Y,result.Element,targets,canvas);return result;
 }
 static double Start(ElementEdit e,bool x){if(e.Rotation==0)return x?e.X:e.Y;var b=ElementTransform.Bounds(e);return x?b.X:b.Y;}
 static double Size(ElementEdit e,bool x){if(e.Rotation==0)return x?e.Width:e.Height;var b=ElementTransform.Bounds(e);return x?b.Width:b.Height;}
 static AlignmentGuide Find(ElementEdit moving,IList<ElementEdit> targets,ElementEdit canvas,bool x,double tolerance,Func<bool,double,bool> allowsCorrection,out double delta){
  delta=0;AlignmentGuide best=null;double distance=tolerance+1e-10;
  double movingStart=Start(moving,x),movingSize=Size(moving,x),movingOrigin=x?moving.X:moving.Y;
  for(int i=-1;i<targets.Count;i++){var target=i<0?canvas:targets[i];if(target==null||target.Index==moving.Index)continue;
   double targetStart=Start(target,x),targetSize=Size(target,x);
   foreach(double ta in Anchors)foreach(double ma in Anchors){double axis=targetStart+ta*targetSize,change=axis-(movingStart+ma*movingSize);double absolute=Math.Abs(change),position=movingOrigin+change;
    if(absolute>tolerance+1e-10||absolute>=distance)continue;
    if(allowsCorrection==null?position < -500||position > 500:!allowsCorrection(x,change))continue;
    distance=absolute;delta=change;best=new AlignmentGuide{Vertical=x,Position=axis,Start=Start(target,!x),End=Start(target,!x)+Size(target,!x)};
   }
  }return best;
 }
 static void Extend(AlignmentGuide guide,ElementEdit moving,IList<ElementEdit> targets,ElementEdit canvas){if(guide==null)return;bool x=guide.Vertical;guide.Start=Math.Min(guide.Start,Start(moving,!x));guide.End=Math.Max(guide.End,Start(moving,!x)+Size(moving,!x));
  for(int i=-1;i<targets.Count;i++){var target=i<0?canvas:targets[i];if(target==null||target.Index==moving.Index)continue;foreach(double a in Anchors)if(Math.Abs(Start(target,x)+a*Size(target,x)-guide.Position)<1e-8){guide.Start=Math.Min(guide.Start,Start(target,!x));guide.End=Math.Max(guide.End,Start(target,!x)+Size(target,!x));break;}}
 }
}
}
