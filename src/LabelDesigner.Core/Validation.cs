using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;
namespace EanEtikety {
public class LabelIssue {public int Element=-2,Record,Page;public int? SafetyZone;internal int? SourceElement;public bool Blocking;public string Message,InputId;public override string ToString(){return (Blocking?Loc.T("Chyba"):Loc.T("Upozornenie"))+": "+(Record>0?Loc.T("Záznam")+" "+Record+", ":"")+(Page>0?Loc.T("Strana")+" "+Page+" — ":"")+Message;}}
public static class LabelChecks {
 static bool ShapeOverlaps(LabelSettings label,SymbolItem symbol,ElementEdit code,IList<RectangleF> areas,bool includeWhite){
  using(var ink=GraphicShape.Ink(symbol,includeWhite))
  using(var scale=new Matrix(64,0,0,64,0,0)){
   if(ink.PointCount==0)return false;
   var pivot=new PointF((float)(symbol.X+symbol.Width/2),(float)(symbol.Y+symbol.Height/2));
   using(var rotation=ElementTransform.Matrix(symbol.Rotation,pivot))ink.Transform(rotation);
   // Work in submillimetre coordinates so a thin line remains visible to GDI regions.
   ink.Transform(scale);
   using(var visibleInk=new Region(ink))
   using(var rotateCode=ElementTransform.Matrix(code.Rotation,ElementTransform.Center(code)))
   using(var identity=new Matrix()){
    visibleInk.Intersect(new RectangleF(0,0,(float)label.Width*64,(float)label.Height*64));
    foreach(var area in areas)using(var target=new GraphicsPath()){
     if(area.Width<=0||area.Height<=0)continue;
     target.AddRectangle(area);
     target.Transform(rotateCode);target.Transform(scale);
     using(var overlap=visibleInk.Clone()){
      overlap.Intersect(target);
      if(overlap.GetRegionScans(identity).Length!=0)return true;
     }
    }
   }
  }
  return false;
 }
 static void CheckShapeCodeOverlaps(LabelSettings label,ElementEdit code,List<int> shapeIndices,Action<int,bool,string> add){
  if(shapeIndices.Count==0)return;
  RectangleF data, footprint;
  var symbol=code.Index<=-3?label.Symbols[-3-code.Index]:null;
  if(symbol!=null&&symbol.Matrix!=null){
   var matrix=Assets.Matrix(symbol.Matrix);
   double step=Math.Floor(Math.Min(symbol.Width/matrix.Width,symbol.Height/matrix.Height)*label.Dpi/25.4)*25.4/label.Dpi;
   if(step<=0)return;
   double x=symbol.X+(symbol.Width-step*matrix.Width)/2,y=symbol.Y+(symbol.Height-step*matrix.Height)/2;
   var ink=matrix.getEnclosingRectangle();
   if(ink==null)return;
   footprint=new RectangleF((float)x,(float)y,(float)(step*matrix.Width),(float)(step*matrix.Height));
   data=new RectangleF((float)(x+ink[0]*step),(float)(y+ink[1]*step),(float)(ink[2]*step),(float)(ink[3]*step));
  }else{
   var settings=symbol==null?label:symbol.Code.Settings();
   double dpi=settings.Dpi/25.4;
   int step=Engine.Module(settings),total=Engine.Modules(settings),quiet=Engine.Quiet(settings.Kind);
   if(step<=0)return;
   int x,y,height,bearer;
   VectorArt.LinearOrigin(settings,step,total,out x,out y,out height,out bearer);
   // Match the scan region of the rendered bars, excluding the caption and ITF bearer bars.
   int right=settings.Kind==13?7:quiet;
   footprint=new RectangleF((float)(x/dpi),(float)((y+bearer)/dpi),(float)(total*step/dpi),(float)(height/dpi));
   data=new RectangleF((float)((x+quiet*step)/dpi),footprint.Y,(float)((total-quiet-right)*step/dpi),footprint.Height);
   if(symbol!=null){
    double sx=symbol.Width/symbol.Code.Width,sy=symbol.Height/Engine.BarcodeHeight(settings);
    Func<RectangleF,RectangleF> placed=r=>new RectangleF((float)(symbol.X+(r.X-symbol.Code.SourceX)*sx),
     (float)(symbol.Y+(r.Y-symbol.Code.SourceY)*sy),(float)(r.Width*sx),(float)(r.Height*sy));
    footprint=placed(footprint);data=placed(data);
   }
  }
  var strips=new[]{
   RectangleF.FromLTRB(footprint.Left,footprint.Top,data.Left,footprint.Bottom),
   RectangleF.FromLTRB(data.Right,footprint.Top,footprint.Right,footprint.Bottom),
   RectangleF.FromLTRB(data.Left,footprint.Top,data.Right,data.Top),
   RectangleF.FromLTRB(data.Left,data.Bottom,data.Right,footprint.Bottom)
  };
  int codeLayer=Elements.Layer(label,code.Index);
  var body=new[]{data};
  foreach(int index in shapeIndices){
   var shape=label.Symbols[-3-index];
   if(ShapeOverlaps(label,shape,code,strips,false))
    add(index,false,Loc.T("Prvok zasahuje do voľného priestoru kódu"));
   int layer=Elements.Layer(label,index);
   bool above=layer>codeLayer||layer==codeLayer&&index<code.Index;
   if(ShapeOverlaps(label,shape,code,body,above))
    add(index,false,Loc.T("Čiara alebo obdĺžnik prekrýva kód."));
  }
 }
 internal static bool TextFits(TextItem text,double dpi,bool checkWidth=false){
  if(text.MinimumFitSize>0||text.Styles.Count>0){FixedTextLayout.Resolve(text,out bool fits);return fits;}
  var size=MeasurePlainText(text,dpi,text.Width);
  return size.Height<=text.Height*dpi/25.4+1&&(!checkWidth||size.Width<=text.Width*dpi/25.4+1);
 }
 internal static SizeF MeasurePlainText(TextItem text,double dpi,double width){
  using(var bitmap=new Bitmap(1,1))using(var graphics=Graphics.FromImage(bitmap))
  using(var family=VectorArt.Family(text.FontName))
  using(var font=new Font(family,(float)(text.Size*dpi/72),VectorArt.Style(family,text.Bold),GraphicsUnit.Pixel))
  using(var format=new StringFormat(StringFormat.GenericTypographic)){
   return graphics.MeasureString(text.Text,font,new SizeF((float)(width*dpi/25.4),100000),format);
  }
 }
 public static List<LabelIssue> Inspect(LabelSettings s){
  if(JobPreparation.HasDynamicArtwork(s)){
   try{
    var prepared=QuickPrint.PrepareEditor(s,DateTime.Today);
    foreach(var issue in prepared.Issues)issue.Element=Elements.SourceIndex(prepared.Label,issue.Element);
    return prepared.Issues;
   }catch(OperationCanceledException){throw;}
   catch(Exception ex){return new List<LabelIssue>{new LabelIssue{Blocking=true,Message=ex.Message}};}
  }
  var result=s.StackIssues==null?new List<LabelIssue>():s.StackIssues.ConvertAll(issue=>new LabelIssue{Element=issue.Element,Blocking=issue.Blocking,Message=issue.Message});Action<int,bool,string> add=(i,b,m)=>result.Add(new LabelIssue{Element=i,Blocking=b,Message=m});var codes=new List<int>();var shapes=new List<int>();if(!s.HideBarcode){codes.Add(-1);try{if(Engine.IsGs1(s))Gs1.Payload(s.ApplicationIdentifiers);else Engine.Normalize(s.Ean,s.Kind);if(Engine.Module(s)<1)throw new Exception(Loc.T("Kód je pre zvolené rozlíšenie príliš úzky."));}catch(Exception ex){add(-1,true,ex.Message);}}
  foreach(int index in Elements.Indices(s)){ElementEdit el;try{el=ElementTransform.Get(s,index);}catch{continue;}if(ElementTransform.Outside(s,el))add(index,false,Loc.T("Prvok presahuje etiketu"));if(index<=-3){var symbol=s.Symbols[-3-index];try{if(symbol.Shape!=null){symbol.Shape.Validate(symbol);shapes.Add(index);}if(symbol.Code!=null){codes.Add(index);Engine.Validate(symbol.Code.Settings());}if(symbol.Matrix!=null){codes.Add(index);var matrix=Assets.Matrix(symbol.Matrix);if(symbol.Width*s.Dpi/25.4<matrix.Width||symbol.Height*s.Dpi/25.4<matrix.Height||BarcodeCatalog.Square(symbol.Matrix.Type)&&Math.Abs(symbol.Width-symbol.Height)>.01)throw new Exception(Loc.T("Neplatné rozmery 2D kódu"));}if(symbol.Image!=null){Assets.Validate(symbol.Image);if(symbol.Image.Mime!="image/svg+xml"&&Assets.ImageDpi(symbol)<s.Dpi*.75)add(index,false,Loc.T("Nízke rozlíšenie obrázka pri tlači"));}}catch(Exception ex){add(index,true,ex.Message);}}
   if(index>=0&&!TextFits(s.Texts[index],s.Dpi))add(index,false,Loc.T("Text sa nezmestí do poľa"));
  }
  foreach(int code in codes){ElementEdit e;try{e=ElementTransform.Get(s,code);}catch{continue;}double left=.08*e.Width,right=left,vertical=0;try{CheckShapeCodeOverlaps(s,e,shapes,add);if(code<=-3&&s.Symbols[-3-code].Matrix!=null){var content=s.Symbols[-3-code].Matrix;var matrix=Assets.Matrix(content);double step=Math.Floor(Math.Min(e.Width/matrix.Width,e.Height/matrix.Height)*s.Dpi/25.4)*25.4/s.Dpi;double margin=content.Type=="QR"||content.Type=="GS1 QR"?4:content.Type=="Aztec"||content.Type=="PDF417"?2:1;left=right=(e.Width-step*matrix.Width)/2+margin*step;vertical=(e.Height-step*matrix.Height)/2+margin*step;}else{var data=code==-1?s:s.Symbols[-3-code].Code.Settings();double scale=e.Width/data.BarWidth,step=Engine.Module(data)*25.4/data.Dpi*scale,padding=(e.Width-Engine.Modules(data)*step)/2;left=padding+Engine.Quiet(data.Kind)*step;right=padding+(data.Kind==13?7:Engine.Quiet(data.Kind))*step;}}catch{continue;}var strips=new List<RectangleF>{new RectangleF((float)e.X,(float)e.Y,(float)left,(float)e.Height),new RectangleF((float)(e.X+e.Width-right),(float)e.Y,(float)right,(float)e.Height)};if(vertical>0){strips.Add(new RectangleF((float)e.X,(float)e.Y,(float)e.Width,(float)vertical));strips.Add(new RectangleF((float)e.X,(float)(e.Y+e.Height-vertical),(float)e.Width,(float)vertical));}foreach(int other in Elements.Indices(s)){if(other==code)continue;try{var symbol=other<=-3?s.Symbols[-3-other]:null;if(symbol!=null&&symbol.Shape!=null)continue;using(var objectPath=new GraphicsPath()){objectPath.AddPolygon(ElementTransform.Corners(ElementTransform.Get(s,other)));foreach(var strip in strips)using(var quietPath=new GraphicsPath()){quietPath.AddRectangle(strip);using(var m=ElementTransform.Matrix(e.Rotation,ElementTransform.Center(e)))quietPath.Transform(m);using(var region=new Region(quietPath)){region.Intersect(objectPath);using(var identity=new Matrix())if(region.GetRegionScans(identity).Length>0){add(other,false,Loc.T("Prvok zasahuje do voľného priestoru kódu"));break;}}}}}catch(ArgumentException){}}
  }if(Elements.Indices(s).Count==0)add(-2,true,Loc.T("Dokument neobsahuje nič na tlač."));try{s.Safety?.Validate();s.Safety?.Inspect(s,result);Engine.Validate(s);}catch(OperationCanceledException){throw;}catch(Exception ex){if(!result.Exists(i=>i.Message==ex.Message))add(-2,true,ex.Message);}return result;
 }
}

}
