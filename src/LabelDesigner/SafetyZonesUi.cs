using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class PreviewPanel {
  internal int? HighlightedSafetyZone;
  Action<RectangleF> newSafetyZone;
  Action zoneCancelled;
  PointF? zoneStart;
  PointF zoneEnd;

  internal void BeginSafetyZone(Action<RectangleF> completed,Action cancelled){
   StopDrag();newSafetyZone=completed;zoneStart=null;
   zoneCancelled=cancelled;
   Cursor=Cursors.Cross;Focus();
  }
  bool CancelSafetyZone(bool notify=true){
   if(newSafetyZone==null)return false;
   var cancelled=zoneCancelled;zoneCancelled=null;
   newSafetyZone=null;zoneStart=null;Capture=false;Cursor=Cursors.Default;Invalidate();
   if(notify)cancelled?.Invoke();
   return true;
  }
  bool SafetyMouseDown(MouseEventArgs e){
   if(newSafetyZone==null)return false;
   if(e.Button==MouseButtons.Right){CancelSafetyZone();return true;}
   if(e.Button==MouseButtons.Left&&!OverRuler(e.Location)&&LabelRectangle().Contains(e.Location)){
    zoneStart=zoneEnd=ZoneCanvasPoint(e.Location);Capture=true;
   }
   return true;
  }
  PointF ZoneCanvasPoint(Point point){
   var mm=CanvasPoint(point);
   return new PointF((float)Math.Clamp(mm.X,0,Settings.Width),(float)Math.Clamp(mm.Y,0,Settings.Height));
  }
  bool SafetyMouseMove(MouseEventArgs e){
   if(newSafetyZone==null)return false;
   if(zoneStart.HasValue){zoneEnd=ZoneCanvasPoint(e.Location);Invalidate();}
   return true;
  }
  RectangleF DraftZone()=>RectangleF.FromLTRB(Math.Min(zoneStart.Value.X,zoneEnd.X),Math.Min(zoneStart.Value.Y,zoneEnd.Y),
   Math.Max(zoneStart.Value.X,zoneEnd.X),Math.Max(zoneStart.Value.Y,zoneEnd.Y));
  bool SafetyMouseUp(MouseEventArgs e){
   if(newSafetyZone==null)return false;
   if(e.Button!=MouseButtons.Left||!zoneStart.HasValue)return true;
   zoneEnd=ZoneCanvasPoint(e.Location);
   var area=DraftZone();var completed=newSafetyZone;
   bool usable=area.Width>=.1f&&area.Height>=.1f;
   CancelSafetyZone(!usable);
   if(usable)completed(area);
   return true;
  }

  void DrawSafetyZones(Graphics graphics,Rectangle label){
   var safety=Settings?.Safety;
   if(safety==null&&!zoneStart.HasValue)return;
   var saved=graphics.Save();
   graphics.SetClip(label,CombineMode.Intersect);
   try{
    float sx=(float)(label.Width/Settings.Width),sy=(float)(label.Height/Settings.Height);
    RectangleF ScreenArea(RectangleF area)=>new(label.X+area.X*sx,label.Y+area.Y*sy,area.Width*sx,area.Height*sy);
    Color marginInk=StudioTheme.HighContrast?SystemColors.Highlight:Color.FromArgb(0,114,128);
    Color zoneInk=StudioTheme.HighContrast?SystemColors.Highlight:Color.FromArgb(164,85,0);
    Color selectedInk=StudioTheme.HighContrast?SystemColors.Highlight:Color.Firebrick;
    void Mark(RectangleF area,Color color,bool selected,string caption,bool shade){
     if(area.Width<=0||area.Height<=0)return;
     var rect=ScreenArea(area);
     if(shade){
      using var fill=new SolidBrush(Color.FromArgb(selected?48:25,color));
      graphics.FillRectangle(fill,rect);
     }
     using var pen=new Pen(selected?selectedInk:color,selected?2:1){DashStyle=selected?DashStyle.Solid:DashStyle.Dash};
     graphics.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);
     if(caption!=null&&rect.Width>=28&&rect.Height>=Font.Height+4){
      var bounds=Rectangle.Inflate(Rectangle.Round(rect),-2,-2);
      TextRenderer.DrawText(graphics,caption,Font,bounds,selected?selectedInk:color,Color.White,
       TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
     }
    }
    if(safety!=null){
     if(safety.HasMargins())Mark(safety.SafeArea(Settings),marginInk,HighlightedSafetyZone==LabelSafetyZones.MarginZone,null,false);
     for(int index=0;index<safety.Zones.Count;index++){
      var zone=safety.Zones[index];
      Mark(zone.Bounds(),zoneInk,HighlightedSafetyZone==index,zone.Name,true);
     }
    }
    if(zoneStart.HasValue)Mark(DraftZone(),marginInk,true,null,true);
   }finally{graphics.Restore(saved);}
  }
 }

 public partial class MainForm {
  LabelSafetyZones labelSafety;

  void AddSafetyZoneMenu(ContextMenuStrip menu){
   menu.Items.Add(FeatureItem("Bezpečné zóny…",HeaderIcon.Panels,()=>EditSafetyZones(null)));
   var draw=FeatureItem("Vyznačiť voľnú zónu…",HeaderIcon.Panels,delegate{
    if(labelSafety?.Zones.Count>=LabelSafetyZones.MaximumZones)
     throw new InvalidDataException(Loc.T("Najviac 32 voľných zón."));
    status.Text=Loc.T("Potiahnutím vyznačte zónu na etikete. Esc zruší výber.");
    preview.BeginSafetyZone(area=>EditSafetyZones(area),UpdatePreview);
   });
   menu.Items.Add(draw);
  }

  void EditSafetyZones(RectangleF? newZone){
   CommitInspectorIfFocused();
   var label=Read();
   var safety=label.Safety??new LabelSafetyZones();
   using var window=FeatureForm("Bezpečné zóny…",690,590);
   window.MinimumSize=new Size(550,470);
   var root=Rows();window.Controls.Add(root);
   var margins=Stack();margins.Padding=Padding.Empty;
   ClickWheelNumericUpDown Margin(string name,double value)=>new(){Name=name,Minimum=0,Maximum=500,DecimalPlaces=2,Value=(decimal)value};
   var left=Margin("safeLeft",safety.Left);var right=Margin("safeRight",safety.Right);
   var top=Margin("safeTop",safety.Top);var bottom=Margin("safeBottom",safety.Bottom);
   InspectorPair(margins,Loc.T("Ľavý bezpečný okraj (mm)"),left,Loc.T("Pravý bezpečný okraj (mm)"),right);
   InspectorPair(margins,Loc.T("Horný bezpečný okraj (mm)"),top,Loc.T("Dolný bezpečný okraj (mm)"),bottom);
   var snap=Check(margins,"Prichytávať k hranám bezpečných zón",safety.Snap);snap.Name="safeSnap";
   ManagementLayout.AddRow(root,margins);
   ManagementLayout.AddRow(root,Description("Voľné zóny (mm)"));
   var grid=new DataGridView{Dock=DockStyle.Fill,Name="safetyZoneGrid",AccessibleName=Loc.T("Voľné zóny (mm)"),
    AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,
    AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
   grid.Columns.Add("zoneName",Loc.T("Názov zóny"));grid.Columns[0].FillWeight=190;
   foreach(string heading in new[]{"X","Y",Loc.T("Šírka"),Loc.T("Výška")}){
    int column=grid.Columns.Add("value"+grid.Columns.Count,heading);
    grid.Columns[column].DefaultCellStyle.Format="0.###";
   }
   foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
   foreach(var zone in safety.Zones)grid.Rows.Add(zone.Name,zone.X,zone.Y,zone.Width,zone.Height);
   ManagementLayout.AddRow(root,grid,SizeType.Percent);
   void AddZone(RectangleF area){
    var index=1;string name;
    bool Taken(string candidate){
     foreach(DataGridViewRow row in grid.Rows)
      if(string.Equals(Convert.ToString(row.Cells[0].Value)?.Trim(),candidate,StringComparison.OrdinalIgnoreCase))return true;
     return false;
    }
    do{name=string.Format(Loc.T("Voľná zóna {0}"),index++);}while(Taken(name));
    double x=Math.Round(area.X,3),y=Math.Round(area.Y,3);
    int added=grid.Rows.Add(name,x,y,Math.Round(area.Right-x,3),Math.Round(area.Bottom-y,3));
    grid.CurrentCell=grid.Rows[added].Cells[0];
   }
   var message=Description("");ManagementLayout.AddRow(root,message);
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
   var add=Btn(Loc.T("Pridať zónu"),delegate{
    if(grid.Rows.Count>=LabelSafetyZones.MaximumZones){message.Text=Loc.T("Najviac 32 voľných zón.");return;}
    AddZone(new RectangleF(0,0,(float)Math.Min(10,label.Width),(float)Math.Min(10,label.Height)));
   });
   var remove=Btn(Loc.T("Odstrániť zónu"),delegate{if(grid.CurrentRow!=null){grid.CancelEdit();grid.Rows.Remove(grid.CurrentRow);}});
   actions.Controls.Add(add);actions.Controls.Add(remove);ManagementLayout.AddRow(root,actions);
   if(newZone.HasValue)AddZone(newZone.Value);
   LabelSafetyZones result=null;
   var footer=new UiMessageBox.ActionRow();
   double NumberAt(DataGridViewRow row,int column){
    if(double.TryParse(Convert.ToString(row.Cells[column].Value),NumberStyles.Float,CultureInfo.CurrentCulture,out double value))return value;
    throw new InvalidDataException(Loc.T("Zóna potrebuje jedinečný názov a platné rozmery."));
   }
   var apply=Btn(Loc.T("Použiť"),delegate{
    try{
     if(!grid.EndEdit())return;
     var candidate=new LabelSafetyZones{Left=(double)left.Value,Top=(double)top.Value,
      Right=(double)right.Value,Bottom=(double)bottom.Value,Snap=snap.Checked};
     foreach(DataGridViewRow row in grid.Rows)candidate.Zones.Add(new ReservedLabelZone{
      Name=Convert.ToString(row.Cells[0].Value)?.Trim(),X=NumberAt(row,1),Y=NumberAt(row,2),Width=NumberAt(row,3),Height=NumberAt(row,4)});
     candidate.Validate();
     if(candidate.Left+candidate.Right>=label.Width||candidate.Top+candidate.Bottom>=label.Height)
      throw new InvalidDataException(Loc.T("Bezpečné okraje zaberajú celú etiketu."));
     foreach(var zone in candidate.Zones)if(!LabelSafetyZones.InsideLabel(label,zone))
      throw new InvalidDataException(string.Format(Loc.T("Voľná zóna „{0}“ presahuje etiketu."),zone.Name));
     result=candidate.IsEmpty()?null:candidate;
     window.DialogResult=DialogResult.OK;
    }catch(Exception ex){message.Text=ex.Message;}
   });
   var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
   footer.AddAction(apply);footer.AddAction(cancel);ManagementLayout.AddRow(root,footer);
   window.AcceptButton=apply;window.CancelButton=cancel;ApplyColors(window);
   if(window.ShowDialog(this)==DialogResult.OK)ChangeDocument(target=>target.Safety=result);
   else UpdatePreview();
  }
 }
}
