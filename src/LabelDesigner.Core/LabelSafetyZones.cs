using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;

namespace EanEtikety {
 public sealed class ReservedLabelZone {
  public string Name;
  public double X,Y,Width,Height;
  public RectangleF Bounds()=>new((float)X,(float)Y,(float)Width,(float)Height);
  public ReservedLabelZone Copy()=>(ReservedLabelZone)MemberwiseClone();
 }

 public sealed class LabelSafetyZones {
  public const int MarginZone=-1,MaximumZones=32;
  const double Tolerance=.001;
  public double Left,Top,Right,Bottom;
  public bool Snap;
  public List<ReservedLabelZone> Zones=new();
  public bool HasMargins()=>Left>0||Top>0||Right>0||Bottom>0;
  public bool IsEmpty()=>!HasMargins()&&Zones.Count==0;

  public LabelSafetyZones Copy()=>new(){Left=Left,Top=Top,Right=Right,Bottom=Bottom,Snap=Snap,
   Zones=Zones.ConvertAll(zone=>zone.Copy())};

  static bool InRange(double value,double min)=>value>=min&&value<=500;
  public void Validate(){
   if(!InRange(Left,0)||!InRange(Top,0)||!InRange(Right,0)||!InRange(Bottom,0)||Zones==null||Zones.Count>MaximumZones)
    throw new InvalidDataException(Loc.T("Neplatné nastavenie bezpečných zón."));
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var zone in Zones){
    if(zone==null||string.IsNullOrWhiteSpace(zone.Name)||zone.Name.Length>80||
       zone.Name.IndexOfAny(new[]{'\r','\n','\t'})>=0||!names.Add(zone.Name.Trim())||
       !InRange(zone.X,0)||!InRange(zone.Y,0)||!InRange(zone.Width,.01)||!InRange(zone.Height,.01))
     throw new InvalidDataException(Loc.T("Zóna potrebuje jedinečný názov a platné rozmery."));
    XmlConvert.VerifyXmlChars(zone.Name);
   }
  }

  public RectangleF SafeArea(LabelSettings label)=>new((float)Left,(float)Top,
   (float)Math.Max(0,label.Width-Left-Right),(float)Math.Max(0,label.Height-Top-Bottom));

  public List<RectangleF> SnapBounds(LabelSettings label){
   var result=new List<RectangleF>();
   if(!Snap)return result;
   var safe=SafeArea(label);
   if(HasMargins()&&safe.Width>0&&safe.Height>0)result.Add(safe);
   foreach(var zone in Zones)
    if(InsideLabel(label,zone))result.Add(zone.Bounds());
   return result;
  }

  public static bool InsideLabel(LabelSettings label,ReservedLabelZone zone)=>
   zone.X+zone.Width<=label.Width+Tolerance&&zone.Y+zone.Height<=label.Height+Tolerance;

  internal void Inspect(LabelSettings label,List<LabelIssue> result){
   var safe=SafeArea(label);
   bool margins=HasMargins(),usable=safe.Width>0&&safe.Height>0;
   if(margins&&!usable)result.Add(new LabelIssue{SafetyZone=MarginZone,Message=Loc.T("Bezpečné okraje zaberajú celú etiketu.")});
   for(int zone=0;zone<Zones.Count;zone++)
    if(!InsideLabel(label,Zones[zone]))result.Add(new LabelIssue{SafetyZone=zone,
     Message=string.Format(Loc.T("Voľná zóna „{0}“ presahuje etiketu."),Zones[zone].Name)});
   foreach(int index in Elements.Indices(label)){
    WorkContext.Check();
    if(index>=0&&string.IsNullOrWhiteSpace(label.Texts[index].Text))continue;
    var item=ElementTransform.Get(label,index);
    if(item==null)continue;
    var bounds=ElementTransform.Bounds(item);
    string name=index>=-1?QuickPrint.Name(label,index):label.Symbols[-3-index].Name+" "+(-2-index);
    if(margins&&usable&&(Left>0&&bounds.Left<safe.Left-Tolerance||Top>0&&bounds.Top<safe.Top-Tolerance||
       Right>0&&bounds.Right>safe.Right+Tolerance||Bottom>0&&bounds.Bottom>safe.Bottom+Tolerance))
     result.Add(new LabelIssue{Element=index,SafetyZone=MarginZone,Message=string.Format(Loc.T("{0} presahuje bezpečné okraje."),name)});
    for(int zone=0;zone<Zones.Count;zone++)
     if(Overlaps(item,bounds,Zones[zone].Bounds()))result.Add(new LabelIssue{Element=index,SafetyZone=zone,
      Message=string.Format(Loc.T("{0} zasahuje do voľnej zóny „{1}“."),name,Zones[zone].Name)});
   }
  }

  static bool Overlaps(ElementEdit item,RectangleF bounds,RectangleF area){
   var intersect=RectangleF.Intersect(bounds,area);
   if(intersect.Width<=Tolerance||intersect.Height<=Tolerance)return false;
   if(item.Rotation==0)return true;
   // The axis-aligned rejection above tests the zone axes. Also test the
   // two axes of the rotated element, so its empty corners do not collide.
   double angle=item.Rotation*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle);
   double x=area.X+area.Width/2d-item.X-item.Width/2,y=area.Y+area.Height/2d-item.Y-item.Height/2;
   double spanX=(area.Width*Math.Abs(cos)+area.Height*Math.Abs(sin))/2;
   double spanY=(area.Width*Math.Abs(sin)+area.Height*Math.Abs(cos))/2;
   return Math.Abs(x*cos+y*sin)<item.Width/2+spanX-Tolerance&&Math.Abs(y*cos-x*sin)<item.Height/2+spanY-Tolerance;
  }
 }
}
