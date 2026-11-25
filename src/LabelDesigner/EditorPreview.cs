using System;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace EanEtikety {
public partial class PreviewPanel:Panel {
 public bool Compact;double viewZoom;internal event EventHandler ZoomChanged;
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]public double ViewZoom {get{return viewZoom;}set{if(viewZoom==value)return;StopDrag();viewZoom=value;AutoScrollPosition=Point.Empty;Invalidate();ZoomChanged?.Invoke(this,EventArgs.Empty);}}
 public VectorScene Scene;public bool Dark=true;public event Action<int> TextEditRequested;public Bitmap Image;public string Caption="";public LabelSettings Settings;
 public event Action<ElementEdit> ElementEdited;public event Action EditFinished;
 public event Action<int> ElementSelected;public event Action<int> SymbolEditRequested;
 public int SelectedIndex=-1;
 public readonly List<int> Selection=new List<int>();public readonly List<int> ProblemElements=new List<int>();public bool Restoring;
 readonly List<ElementEdit> groupOriginal=new List<ElementEdit>();bool marquee;Point marqueeStart,marqueeEnd;List<int> beforeMarquee=new List<int>();
 public void SelectMany(IEnumerable<int> indices,bool notify){Selection.Clear();foreach(int index in indices)if(Element(index)!=null&&!Selection.Contains(index))Selection.Add(index);SelectedIndex=Selection.Count==0?-2:Selection[0];if(notify&&ElementSelected!=null)ElementSelected(SelectedIndex);Invalidate();}
 List<int> GroupIndices(int index){var result=new List<int>();if(index==-2)return result;result.Add(index);var meta=Settings.Metadata.Find(m=>m.Index==index);if(meta!=null&&!string.IsNullOrEmpty(meta.Group))foreach(var m in Settings.Metadata)if(m.Group==meta.Group&&!result.Contains(m.Index)&&Element(m.Index)!=null)result.Add(m.Index);return result;}
 void FinishMarquee(){if(!marquee)return;marquee=false;Capture=false;var rect=Rectangle.FromLTRB(Math.Min(marqueeStart.X,marqueeEnd.X),Math.Min(marqueeStart.Y,marqueeEnd.Y),Math.Max(marqueeStart.X,marqueeEnd.X),Math.Max(marqueeStart.Y,marqueeEnd.Y));var selected=new List<int>(beforeMarquee);foreach(int index in Elements.Indices(Settings)){var corners=ElementTransform.Corners(Element(index));for(int i=0;i<4;i++)corners[i]=ScreenPoint(corners[i]);using(var path=new GraphicsPath()){path.AddPolygon(corners);using(var region=new Region(path))if(region.IsVisible(rect))foreach(int member in GroupIndices(index))if(!selected.Contains(member))selected.Add(member);}}SelectMany(selected,true);}

 readonly List<ElementEdit> snapTargets=new List<ElementEdit>();SnapResult snap;RectangleF groupBounds,dragBounds;bool bypassedSnap;
 public bool IsDragging {get{return dragging;}} public event Action DragStarted;readonly Timer dragFrame=new Timer{Interval=16};bool movePending,dragMoved,endingDrag;Point latestMouse;
 bool dragging;int handle=-1;Point mouseStart;ElementEdit original;float scaleX,scaleY;
 public PreviewPanel(){dragFrame.Tick+=delegate{FlushDragMove();};DoubleBuffered=true;BackColor=Color.FromArgb(232,237,244);TabStop=true;SetStyle(ControlStyles.Selectable,true);}
 public Rectangle LabelRectangle(){if(Compact&&Image!=null&&Settings!=null){double dpi=DeviceDpi;int mx=ShowRulers?Math.Max(28,RulerWidth+4):28,my=ShowRulers?Math.Max(28,RulerHeight+4):28;double compactScale=ViewZoom==0?Math.Min((ClientSize.Width-2*mx)/Settings.Width,(ClientSize.Height-2*my)/Settings.Height):ViewZoom*dpi/25.4;int cw=Math.Max(1,(int)(Settings.Width*compactScale)),ch=Math.Max(1,(int)(Settings.Height*compactScale));AutoScroll=true;var extent=ViewZoom==0?Size.Empty:new Size(cw+2*mx,ch+2*my);if(AutoScrollMinSize!=extent)AutoScrollMinSize=extent;return new Rectangle(Math.Max(mx,(ClientSize.Width-cw)/2)+AutoScrollPosition.X,Math.Max(my,(ClientSize.Height-ch)/2)+AutoScrollPosition.Y,cw,ch);}if(Image==null||Settings==null)return Rectangle.Empty;double scale=Math.Min((ClientSize.Width-100)/Settings.Width,(ClientSize.Height-164)/Settings.Height);if(scale<=0)return Rectangle.Empty;int w=(int)(Settings.Width*scale),h=(int)(Settings.Height*scale);return new Rectangle((Width-w)/2,92+(Height-164-h)/2,w,h);}
 public ElementEdit Element(int index){return ElementTransform.Get(Settings,index);}
 public RectangleF ElementRectangle(int index){var r=LabelRectangle();var el=Element(index);if(r.IsEmpty||el==null)return RectangleF.Empty;return new RectangleF(r.X+(float)(el.X/Settings.Width*r.Width),r.Y+(float)(el.Y/Settings.Height*r.Height),(float)(el.Width/Settings.Width*r.Width),(float)(el.Height/Settings.Height*r.Height));}
 public RectangleF BarcodeRectangle(){return ElementRectangle(-1);}
 public PointF CanvasPoint(PointF screen){var r=LabelRectangle();return new PointF((float)((screen.X-r.X)*Settings.Width/r.Width),(float)((screen.Y-r.Y)*Settings.Height/r.Height));}
 public PointF ScreenPoint(PointF canvas){var r=LabelRectangle();return new PointF(r.X+(float)(canvas.X*r.Width/Settings.Width),r.Y+(float)(canvas.Y*r.Height/Settings.Height));}
 public PointF[] Handles(){if(Selection.Count>1||Settings!=null&&Elements.Locked(Settings,SelectedIndex))return new PointF[0];var e=Element(SelectedIndex);if(e==null)return new PointF[0];var c=ElementTransform.Corners(e);var result=new PointF[8];for(int i=0;i<4;i++){result[i*2]=ScreenPoint(c[i]);var next=c[(i+1)%4];result[i*2+1]=ScreenPoint(new PointF((c[i].X+next.X)/2,(c[i].Y+next.Y)/2));}return result;}
 public PointF RotationHandle(){PointF anchor;return RotationHandle(out anchor);}
 PointF RotationHandle(out PointF anchor){
  anchor=PointF.Empty;
  var element=Element(SelectedIndex);
  var handles=Handles();
  if(element==null||handles.Length==0)return PointF.Empty;
  var center=ScreenPoint(ElementTransform.Center(element));
  PointF fallback=PointF.Empty;
  foreach(int side in new[]{1,3,7,5}){
   var edge=handles[side];
   float dx=edge.X-center.X,dy=edge.Y-center.Y;
   double length=Math.Sqrt(dx*dx+dy*dy);
   if(length<.001)continue;
   for(int attempt=0;attempt<12;attempt++){
    float distance=(28+16*attempt)*DeviceDpi/96f;
    var candidate=new PointF(edge.X+(float)(distance*dx/length),edge.Y+(float)(distance*dy/length));
    if(Compact&&ShowRulers&&!VisibleRotationHandle(candidate))continue;
    anchor=edge;
    fallback=candidate;
    bool overlap=false;
    foreach(int index in Elements.Indices(Settings)){
     if(index==SelectedIndex)continue;
     var other=Element(index);
     foreach(var offset in new[]{new PointF(0,0),new PointF(0,8),new PointF(0,-8),new PointF(8,0),new PointF(-8,0)}){
      if(!ElementTransform.Contains(other,CanvasPoint(new PointF(candidate.X+offset.X,candidate.Y+offset.Y))))continue;
      overlap=true;break;
     }
     if(overlap)break;
    }
    if(!overlap)return candidate;
   }
   if(!Compact||!ShowRulers)break;
  }
  return fallback;
 }
 public bool ContainsElement(int index,Point p){
  var element=Element(index);
  if(element==null)return false;
  var point=CanvasPoint(p);
  var shape=index<=-3?Settings.Symbols[-3-index].Shape:null;
  if(shape==null)return ElementTransform.Contains(element,point);
  var canvas=LabelRectangle();
  if(canvas.Width<=0||canvas.Height<=0)return false;
  float tolerance=(float)(3*DeviceDpi/96d*Math.Max(Settings.Width/canvas.Width,Settings.Height/canvas.Height));
  return ElementTransform.ContainsShape(element,shape,point,tolerance);
 }
 int HandleAt(Point p){if(Handles().Length>0){var rot=RotationHandle();if(!rot.IsEmpty&&Math.Abs(rot.X-p.X)<=8&&Math.Abs(rot.Y-p.Y)<=8)return 8;}var a=Handles();for(int i=0;i<a.Length;i++)if(Math.Abs(p.X-a[i].X)<=6&&Math.Abs(p.Y-a[i].Y)<=6)return i;return -1;}
 Cursor ResizeCursor(int h){double angle=(h==0||h==4?45:h==2||h==6?135:h==1||h==5?90:0)+(Element(SelectedIndex)==null?0:Element(SelectedIndex).Rotation);int sector=(int)Math.Round(ElementTransform.Normalize(angle)/45)%4;return sector==0?Cursors.SizeWE:sector==1?Cursors.SizeNWSE:sector==2?Cursors.SizeNS:Cursors.SizeNESW;}
 int Hit(Point p){if(Settings==null)return -2;var indices=Elements.Indices(Settings);indices.Sort((x,y)=>Elements.Layer(Settings,y).CompareTo(Elements.Layer(Settings,x)));foreach(int i in indices)if(ContainsElement(i,p))return i;return -2;}
 public void SelectElement(int index){SelectElement(index,true);}
 // Grid-originated selection must never notify the grid while it is changing CurrentCell.
 public void SelectElement(int index,bool notify){if(dragging)StopDrag();SelectedIndex=index;Selection.Clear();if(Settings!=null)Selection.AddRange(GroupIndices(index));if(notify&&ElementSelected!=null)ElementSelected(index);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;using(var captionBrush=new SolidBrush(Dark?Color.FromArgb(180,201,223):Color.FromArgb(65,84,108))){g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
  if(!Compact){using(var f=UiFonts.Create(11,FontStyle.Bold))g.DrawString(Loc.T("NÁHĽAD ETIKETY"),f,captionBrush,20,12);
  g.DrawString(Loc.T("Kliknite na text alebo kód a presuňte ho myšou."),Font,captionBrush,20,38);
  g.DrawString(Loc.T("Rohy: veľkosť aj písmo. Hrany: šírka/výška. Esc: zrušiť."),Font,captionBrush,20,59);}
  if(Image==null){g.DrawString(Loc.T("Upravte údaje podľa hlásenia nižšie."),Font,captionBrush,20,97);return;}
  var r=LabelRectangle();if(r.IsEmpty)return;
  using(var shadow=new SolidBrush(Color.FromArgb(205,214,226)))g.FillRectangle(shadow,r.X+6,r.Y+6,r.Width,r.Height);
  g.FillRectangle(Brushes.White,r);if(Scene!=null)Scene.Draw(g,r,false);else g.DrawImage(Image,r);g.DrawRectangle(Pens.SlateGray,r.X-1,r.Y-1,r.Width+1,r.Height+1);
  var saved=g.Save();g.SetClip(Compact?ClientRectangle:new Rectangle(0,84,Width,Math.Max(0,Height-125)));
  if(Settings!=null)for(int slot=-1;slot<Settings.Texts.Count+Settings.Symbols.Count;slot++){int i=slot<Settings.Texts.Count?slot:-3-(slot-Settings.Texts.Count);var el=Element(i);if(el==null)continue;bool outside=ElementTransform.Outside(Settings,el);if(!Selection.Contains(i)&&i!=SelectedIndex&&!outside&&!ProblemElements.Contains(i))continue;using(var pen=new Pen(outside||ProblemElements.Contains(i)?Color.Firebrick:Color.FromArgb(35,100,204),i==SelectedIndex?2:1)){pen.DashStyle=DashStyle.Dash;var corners=ElementTransform.Corners(el);for(int c=0;c<corners.Length;c++)corners[c]=ScreenPoint(corners[c]);g.DrawPolygon(pen,corners);}}
  foreach(var pt in Handles()){g.FillRectangle(Brushes.White,pt.X-4,pt.Y-4,8,8);g.DrawRectangle(Pens.RoyalBlue,pt.X-4,pt.Y-4,8,8);}DrawGuides(g,r);if(marquee){var box=Rectangle.FromLTRB(Math.Min(marqueeStart.X,marqueeEnd.X),Math.Min(marqueeStart.Y,marqueeEnd.Y),Math.Max(marqueeStart.X,marqueeEnd.X),Math.Max(marqueeStart.Y,marqueeEnd.Y));g.DrawRectangle(Pens.RoyalBlue,box);}PointF top;var rh=RotationHandle(out top);if(!rh.IsEmpty){g.DrawLine(Pens.RoyalBlue,top,rh);g.FillEllipse(Brushes.White,rh.X-6,rh.Y-6,12,12);g.DrawEllipse(Pens.RoyalBlue,rh.X-6,rh.Y-6,12,12);if(dragging&&handle==8){string angle=Loc.T("Rotácia")+": "+Element(SelectedIndex).Rotation.ToString("0.#")+"°";var size=g.MeasureString(angle,Font);var tip=new RectangleF(Math.Max(0,Math.Min(Width-size.Width-12,latestMouse.X+14)),Math.Max(0,Math.Min(Height-size.Height-8,latestMouse.Y+14)),size.Width+12,size.Height+8);using(var background=new SolidBrush(StudioTheme.Panel(Dark)))using(var foreground=new SolidBrush(StudioTheme.Text(Dark))){g.FillRectangle(background,tip);g.DrawString(angle,Font,foreground,tip.X+6,tip.Y+4);}}}DrawEdgeDistances(g);g.Restore(saved);DrawRulers(g,r);
  if(!Compact)using(var f=UiFonts.Create(10,FontStyle.Regular))using(var fmt=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(Caption,f,captionBrush,new RectangleF(10,Height-37,Width-20,32),fmt);}
 }
 void DrawGuides(Graphics g,Rectangle canvas){if(!dragging||handle>=0||snap==null||Settings==null)return;using(var pen=new Pen(Dark?Color.FromArgb(244,94,205):Color.FromArgb(177,22,135),1)){pen.DashStyle=DashStyle.Dash;DrawGuide(g,pen,canvas,snap.X);DrawGuide(g,pen,canvas,snap.Y);}}
 void DrawGuide(Graphics g,Pen pen,Rectangle canvas,AlignmentGuide guide){if(guide==null)return;float sx=(float)(canvas.Width/Settings.Width),sy=(float)(canvas.Height/Settings.Height);if(guide.Vertical){float x=canvas.X+(float)guide.Position*sx;g.DrawLine(pen,x,canvas.Y+(float)guide.Start*sy-8,x,canvas.Y+(float)guide.End*sy+8);}else{float y=canvas.Y+(float)guide.Position*sy;g.DrawLine(pen,canvas.X+(float)guide.Start*sx-8,y,canvas.X+(float)guide.End*sx+8,y);}}
 protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);StopDrag();if(e.Button==MouseButtons.Left&&!OverRuler(e.Location)){int index=Hit(e.Location);if(Settings!=null&&Elements.Locked(Settings,index))return;if(index>=0&&TextEditRequested!=null)TextEditRequested(index);else if(index<=-3&&SymbolEditRequested!=null)SymbolEditRequested(index);}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(OverRuler(e.Location))return;if(e.Button==MouseButtons.Right&&Settings!=null){SelectElement(Hit(e.Location));Focus();return;}if(e.Button!=MouseButtons.Left||Settings==null||e.Y<(Compact?0:84)||e.Y>Height-(Compact?0:41))return;int h=HandleAt(e.Location);if(h<0){int hit=Hit(e.Location);bool additive=(ModifierKeys&(Keys.Control|Keys.Shift))!=0;if(hit==-2){marquee=true;marqueeStart=marqueeEnd=e.Location;beforeMarquee=additive?new List<int>(Selection):new List<int>();if(!additive)SelectMany(new int[0],true);Capture=true;Focus();return;}if(additive){var next=new List<int>(Selection);foreach(int member in GroupIndices(hit)){if(next.Contains(member))next.Remove(member);else next.Add(member);}SelectMany(next,true);Focus();return;}if(!Selection.Contains(hit))SelectElement(hit);}foreach(int member in Selection)if(Elements.Locked(Settings,member))return;var r=LabelRectangle();if(r.Width<=0||r.Height<=0)return;original=Element(SelectedIndex);if(original==null)return;groupOriginal.Clear();foreach(int member in Selection){var value=Element(member);if(value!=null)groupOriginal.Add(value.Copy());}Focus();dragging=true;handle=h;mouseStart=e.Location;scaleX=r.Width/(float)Settings.Width;scaleY=r.Height/(float)Settings.Height;snap=null;snapTargets.Clear();if(handle<0)BeginMove();movePending=dragMoved=false;if(DragStarted!=null)DragStarted();Capture=true;Invalidate();}
 static double Clamp(double value,double min,double max){return Math.Max(min,Math.Min(max,value));}
 ElementEdit Transform(double dx,double dy){var a=original.Copy();if(handle==8){var center=ElementTransform.Center(original);var first=CanvasPoint(mouseStart);var now=CanvasPoint(latestMouse);double startAngle=Math.Atan2(first.Y-center.Y,first.X-center.X),endAngle=Math.Atan2(now.Y-center.Y,now.X-center.X);a.Rotation=ElementTransform.SnapAngle(original.Rotation+(endAngle-startAngle)*180/Math.PI,(ModifierKeys&Keys.Shift)!=0);return a;}if(handle>=0){var local=ElementTransform.Vector(dx,dy,-original.Rotation);dx=local.X;dy=local.Y;}bool west=handle==0||handle==6||handle==7,north=handle==0||handle==1||handle==2;
  if(handle<0){a.X=Clamp(a.X+dx,-500,500);a.Y=Clamp(a.Y+dy,-500,500);return a;}
  else {
   bool aspect=a.Index<=-3&&(Settings.Symbols[-3-a.Index].Matrix!=null||Settings.Symbols[-3-a.Index].Image!=null&&Settings.Symbols[-3-a.Index].Image.KeepAspect);bool corner=handle%2==0;double minW=original.Index==-1?Math.Max(1,Math.Ceiling(Engine.Modules(Settings)*25.4/Settings.Dpi*100)/100):1;
   double minH=a.Index<=-3&&Settings.Symbols[-3-a.Index].Shape!=null&&Settings.Symbols[-3-a.Index].Shape.IsLine?ShapeContent.MinimumLineThickness:1;
   if(corner){double factor=1+((west?-dx:dx)*a.Width+(north?-dy:dy)*a.Height)/(a.Width*a.Width+a.Height*a.Height);double min=Math.Max(Math.Max(minW/a.Width,minH/a.Height),a.Index<=-3?0:4/a.Font);double max=Math.Min(Math.Min(500/a.Width,500/a.Height),a.Index<=-3?double.MaxValue:(a.Index==-1?72:144)/a.Font);factor=Clamp(factor,min,max);a.Width*=factor;a.Height*=factor;if(a.Index>-3)a.Font*=factor;}
   else if(handle==3||handle==7)a.Width=Clamp(a.Width+(west?-dx:dx),minW,500);
   else a.Height=Clamp(a.Height+(north?-dy:dy),minH,500);
   if(aspect&&!corner){if(handle==3||handle==7)a.Height=a.Width*original.Height/original.Width;else a.Width=a.Height*original.Width/original.Height;}if(a.Index==-1){double extra=Engine.ExtraHeight(Settings,a.Width,a.Font);a.Height=Clamp(a.Height,extra+1,extra+500);}
   if(west)a.X=original.X+original.Width-a.Width;if(north)a.Y=original.Y+original.Height-a.Height;
  }
  if(handle>=0&&original.Rotation!=0){double cx=a.X+a.Width/2-(original.X+original.Width/2),cy=a.Y+a.Height/2-(original.Y+original.Height/2);var shift=ElementTransform.Vector(cx,cy,original.Rotation);a.X=original.X+original.Width/2+shift.X-a.Width/2;a.Y=original.Y+original.Height/2+shift.Y-a.Height/2;}
  a.X=Math.Round(Clamp(a.X,-500,500),2);a.Y=Math.Round(Clamp(a.Y,-500,500),2);a.Width=Math.Round(a.Width,2);a.Height=Math.Round(a.Height,2);a.Font=Math.Round(a.Font,2);return a;
 }
 // Mouse messages only replace a pending position. At most one edit is applied per UI frame.
 void BeginMove(){
  bypassedSnap=BypassSnapping;
  groupBounds=ElementTransform.Bounds(original);
  foreach(var member in groupOriginal)groupBounds=RectangleF.Union(groupBounds,ElementTransform.Bounds(member));
  foreach(int index in Elements.Indices(Settings))
   if(!Selection.Contains(index))snapTargets.Add(Element(index));
 }
 protected virtual bool BypassSnapping {get{return (ModifierKeys&Keys.Alt)!=0;}}
 void FlushDragMove(){
  if(!dragging||Settings==null){dragFrame.Stop();return;}
  bool moving=handle<0;
  bool bypass=BypassSnapping;
  if(!moving||!SnapToGuides)dragFrame.Stop();
  if(!movePending&&(!moving||!SnapToGuides||bypass==bypassedSnap))return;
  if(!dragMoved&&!movePending)return;
  movePending=false;
  bypassedSnap=bypass;
  double dx=(latestMouse.X-mouseStart.X)/scaleX,dy=(latestMouse.Y-mouseStart.Y)/scaleY;
  var edit=Transform(dx,dy);
  if(moving)edit=AlignMove(edit,SnapToGuides&&!bypass);
  if(ElementEdited!=null){
   ElementEdited(edit);
   if(moving&&groupOriginal.Count>1)foreach(var member in groupOriginal){
    if(member.Index==edit.Index)continue;
    var moved=member.Copy();
    moved.X+=edit.X-original.X;
    moved.Y+=edit.Y-original.Y;
    ElementEdited(moved);
   }
  }
  Invalidate();
 }
 ElementEdit AlignMove(ElementEdit edit,bool apply){
  double minX=-500-original.X,maxX=500-original.X,minY=-500-original.Y,maxY=500-original.Y;
  foreach(var member in groupOriginal){
   minX=Math.Max(minX,-500-member.X);maxX=Math.Min(maxX,500-member.X);
   minY=Math.Max(minY,-500-member.Y);maxY=Math.Min(maxY,500-member.Y);
  }
  edit.X=original.X+Clamp(edit.X-original.X,minX,maxX);
  edit.Y=original.Y+Clamp(edit.Y-original.Y,minY,maxY);
  var moving=edit;
  if(groupOriginal.Count>1)moving=new ElementEdit{Index=edit.Index,
   X=groupBounds.X+(edit.X-original.X),Y=groupBounds.Y+(edit.Y-original.Y),Width=groupBounds.Width,Height=groupBounds.Height};
  double rawX=edit.X-original.X,rawY=edit.Y-original.Y;
  snap=SmartGuides.CalculateSnap(moving,snapTargets,Settings.Width,Settings.Height,scaleX,scaleY,apply,SmartGuides.TolerancePixels*DeviceDpi/96d,
   (axisX,correction)=>axisX?rawX+correction>=minX&&rawX+correction<=maxX:rawY+correction>=minY&&rawY+correction<=maxY);
  double nextX=edit.X+(snap.Element.X-moving.X)-original.X,nextY=edit.Y+(snap.Element.Y-moving.Y)-original.Y;
  double boundedX=Clamp(nextX,minX,maxX),boundedY=Clamp(nextY,minY,maxY);
  if(Math.Abs(nextX-boundedX)>1e-8)snap.X=null;
  if(Math.Abs(nextY-boundedY)>1e-8)snap.Y=null;
  if(groupOriginal.Count==1&&nextX==boundedX&&nextY==boundedY)edit=snap.Element;
  else{edit.X=original.X+boundedX;edit.Y=original.Y+boundedY;}
  snap.Element=edit;
  dragBounds=groupBounds;
  dragBounds.Offset((float)(edit.X-original.X),(float)(edit.Y-original.Y));
  return edit;
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(marquee){marqueeEnd=e.Location;Invalidate();return;}if(dragging&&Settings!=null){dragMoved|=e.Location!=mouseStart;if(!dragMoved)return;latestMouse=e.Location;movePending=true;if(!dragFrame.Enabled)dragFrame.Start();}else{if(OverRuler(e.Location)){Cursor=Cursors.Default;return;}int h=HandleAt(e.Location);Cursor=h==8?Cursors.Hand:h>=0?ResizeCursor(h):Hit(e.Location)!=-2?Cursors.SizeAll:Cursors.Default;}}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(marquee&&e.Button==MouseButtons.Left){marqueeEnd=e.Location;FinishMarquee();return;}if(e.Button==MouseButtons.Left&&dragging){if(dragMoved||movePending||e.Location!=mouseStart){latestMouse=e.Location;movePending=true;}StopDrag();}}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&marquee)FinishMarquee();if(!Capture&&dragging)StopDrag();}
 protected override void OnResize(EventArgs e){StopDrag();base.OnResize(e);Invalidate();}
 void StopDrag(){StopDrag(true,true);}
 void StopDrag(bool flush,bool notify){if(endingDrag)return;endingDrag=true;try{bool edited=dragging;if(flush)FlushDragMove();dragFrame.Stop();movePending=false;dragging=false;snap=null;snapTargets.Clear();Capture=false;Cursor=Cursors.Default;Invalidate();if(edited&&notify&&EditFinished!=null)EditFinished();}finally{endingDrag=false;}}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(dragging&&handle<0&&(keyData&Keys.KeyCode)==Keys.Menu)return true;if(marquee&&keyData==Keys.Escape){marquee=false;Capture=false;SelectMany(beforeMarquee,true);return true;}if(dragging&&keyData==Keys.Escape){var saved=original.Copy();Restoring=true;try{StopDrag(false,false);if(ElementEdited!=null){ElementEdited(saved);foreach(var member in groupOriginal)if(member.Index!=saved.Index)ElementEdited(member);}}finally{Restoring=false;}if(EditFinished!=null)EditFinished();return true;}return base.ProcessCmdKey(ref msg,keyData);}
 protected override void Dispose(bool disposing){if(disposing){dragFrame.Dispose();if(Image!=null)Image.Dispose();if(Scene!=null)Scene.Dispose();}base.Dispose(disposing);}
}
}
