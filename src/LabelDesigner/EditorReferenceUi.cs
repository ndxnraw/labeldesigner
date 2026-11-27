using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class PreviewPanel {
  EditorReference editorReference;
  Bitmap editorReferenceBitmap;
  internal void SetEditorReference(EditorReference reference){
   if(editorReference?.Raster!=reference?.Raster){
    var bitmap=reference?.OpenBitmap();
    editorReferenceBitmap?.Dispose();
    editorReferenceBitmap=bitmap;
   }
   editorReference=reference;
   Invalidate();
  }

  void DrawEditorReference(Graphics graphics,Rectangle label){
   if(editorReferenceBitmap==null||editorReference.Hidden||editorReference.Opacity==0)return;
   double sx=label.Width/Settings.Width,sy=label.Height/Settings.Height;
   var destination=new Rectangle((int)Math.Round(label.X+editorReference.X*sx),(int)Math.Round(label.Y+editorReference.Y*sy),
    Math.Max(1,(int)Math.Round(editorReference.Width*sx)),Math.Max(1,(int)Math.Round(editorReference.Height*sy)));
   var saved=graphics.Save();
   try{
    graphics.SetClip(label,CombineMode.Intersect);
    graphics.InterpolationMode=InterpolationMode.Bilinear;
    using var opacity=new ImageAttributes();
    opacity.SetColorMatrix(new ColorMatrix{Matrix33=editorReference.Opacity/100f});
    graphics.DrawImage(editorReferenceBitmap,destination,0,0,editorReferenceBitmap.Width,editorReferenceBitmap.Height,GraphicsUnit.Pixel,opacity);
   }finally{graphics.Restore(saved);}
  }
 }

 public partial class MainForm {
  EditorReference editorReference;

  void AddEditorReferenceMenu(ContextMenuStrip menu){
   menu.Items.Add(FeatureItem("Referenčný podklad…",HeaderIcon.Image,EditEditorReference));
   var show=FeatureItem("Zobraziť referenčný podklad",HeaderIcon.Visibility,delegate{
    if(editorReference!=null)ChangeDocument(label=>label.EditorReference=label.EditorReference with {Hidden=!label.EditorReference.Hidden});
   });
   show.Name="editorReferenceVisible";
   menu.Items.Add(show);
   menu.Opening+=delegate{
    show.Enabled=editorReference!=null;
    show.Checked=editorReference is {Hidden:false};
   };
  }

  void EditEditorReference(){
   CommitInspectorIfFocused();
   var current=Read();
   var reference=current.EditorReference;
   var dialog=CreateFieldSettingsDialog("Referenčný podklad…");
   using var window=dialog.Window;
   var status=Description("");StackAdd(dialog.Fields,status);
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};StackAdd(dialog.Fields,actions);
   var locked=Check(dialog.Fields,"Zamknúť polohu a rozmery",false);locked.Name="referenceLocked";
   var hidden=Check(dialog.Fields,"Skryť podklad",false);hidden.Name="referenceHidden";
   ClickWheelNumericUpDown Dimension(string name,decimal minimum)=>
    new(){Name=name,Minimum=minimum,Maximum=500,DecimalPlaces=3,Increment=.1m};
   var x=Dimension("referenceX",-500);var y=Dimension("referenceY",-500);
   var width=Dimension("referenceWidth",.01m);var height=Dimension("referenceHeight",.01m);
   InspectorPair(dialog.Fields,Loc.T("Poloha podkladu X (mm)"),x,Loc.T("Poloha podkladu Y (mm)"),y);
   InspectorPair(dialog.Fields,Loc.T("Šírka podkladu (mm)"),width,Loc.T("Výška podkladu (mm)"),height);
   var opacity=Number(dialog.Fields,"Krytie podkladu (%)",45,0,100,0);opacity.Name="referenceOpacity";
   Button remove=null;
   var import=Btn(Loc.T("Načítať PNG alebo JPEG…"),delegate{
    using var picker=new OpenFileDialog{Filter=Loc.T("Referenčný obrázok (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg")};
    if(picker.ShowDialog(window)!=DialogResult.OK)return;
    try{
     reference=EditorReference.Import(picker.FileName,current.Width,current.Height);
     Populate();
    }catch(Exception ex){status.Text=ex.Message;}
   },HeaderIcon.Open);
   remove=Btn(Loc.T("Odstrániť podklad"),delegate{reference=null;Populate();},HeaderIcon.Delete);
   actions.Controls.Add(import);actions.Controls.Add(remove);
   void SetEnabled(){
    bool available=reference!=null;
    x.Enabled=y.Enabled=width.Enabled=height.Enabled=available&&!locked.Checked;
    opacity.Enabled=locked.Enabled=hidden.Enabled=remove.Enabled=available;
   }
   void Populate(){
    x.Value=(decimal)(reference?.X??0);y.Value=(decimal)(reference?.Y??0);
    width.Value=(decimal)(reference?.Width??1);height.Value=(decimal)(reference?.Height??1);
    opacity.Value=reference?.Opacity??45;
    locked.Checked=reference?.Locked??false;hidden.Checked=reference?.Hidden??false;
    status.Text=Loc.T(reference==null?"Projekt nemá referenčný podklad.":"Podklad sa zobrazuje iba pri úpravách na plátne.");
    SetEnabled();
   }
   locked.CheckedChanged+=delegate{SetEnabled();};
   Populate();ApplyColors(window);
   if(window.ShowDialog(this)!=DialogResult.OK)return;
   var result=reference==null?null:reference with {
    X=(double)x.Value,Y=(double)y.Value,Width=(double)width.Value,Height=(double)height.Value,
    Opacity=(int)opacity.Value,Locked=locked.Checked,Hidden=hidden.Checked
   };
   if(result!=current.EditorReference)ChangeDocument(label=>label.EditorReference=result);
  }
 }
}
