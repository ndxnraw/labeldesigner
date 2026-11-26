using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  sealed record ResizeElement(int Index,string Caption){
   public override string ToString()=>Caption;
  }

  sealed class ResizeReview(LabelSettings variant,List<ResizeProblem> problems,VectorScene before,VectorScene after):IDisposable {
   public LabelSettings Variant{get;}=variant;
   public List<ResizeProblem> Problems{get;}=problems;
   public VectorScene Before{get;}=before;
   public VectorScene After{get;}=after;
   public void Dispose(){Before.Dispose();After.Dispose();}
  }

  sealed class ResizeCanvas:Control {
   public LabelSettings Geometry;
   public VectorScene Scene;
   public SizeF ComparisonSize;
   public int FocusedElement=-2;
   public ResizeCanvas(){DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;TabStop=false;}
   protected override void OnPaint(PaintEventArgs e){
    base.OnPaint(e);
    if(Scene==null||Geometry==null||ClientSize.Width<=12||ClientSize.Height<=12)return;
    float scale=Math.Min((ClientSize.Width-12)/ComparisonSize.Width,(ClientSize.Height-12)/ComparisonSize.Height);
    float width=(float)Scene.Width*scale,height=(float)Scene.Height*scale;
    var area=new RectangleF((ClientSize.Width-width)/2,(ClientSize.Height-height)/2,width,height);
    e.Graphics.FillRectangle(Brushes.White,area);
    Scene.Draw(e.Graphics,area,false);
    var item=ElementTransform.Get(Geometry,FocusedElement);
    if(item==null)return;
    var corners=ElementTransform.Corners(item);
    for(int i=0;i<corners.Length;i++)corners[i]=new PointF(area.X+corners[i].X*scale,area.Y+corners[i].Y*scale);
    using var pen=new Pen(ElementTransform.Outside(Geometry,item)?Color.Firebrick:SystemColors.Highlight,2){DashStyle=DashStyle.Dash};
    var bounds=ElementTransform.Bounds(item);
    var visibleBounds=new RectangleF(area.X+bounds.X*scale,area.Y+bounds.Y*scale,bounds.Width*scale,bounds.Height*scale);
    if(!visibleBounds.IntersectsWith(ClientRectangle)){
     float x=Math.Clamp(visibleBounds.X+visibleBounds.Width/2,5,ClientSize.Width-5);
     float y=Math.Clamp(visibleBounds.Y+visibleBounds.Height/2,5,ClientSize.Height-5);
     e.Graphics.FillEllipse(Brushes.Firebrick,x-4,y-4,8,8);
     return;
    }
    e.Graphics.DrawPolygon(pen,corners);
   }
  }

  static VectorScene ResizeScene(LabelSettings settings,DateTime instant,string language){
   var draft=QuickPrint.PrepareEditor(settings,instant,language);
   return PreviewValidation.Build(draft.Label,draft.Issues);
  }

  void ShowResizeLabel(){
   CommitInspectorIfFocused();
   var original=Read();
   var instant=currentTime();
   var anchors=new Dictionary<int,ResizeAnchor>();
   using var window=FeatureForm("Zmeniť rozmery etikety…",1070,675);
   using var debounce=new Timer{Interval=180};
   window.MinimumSize=new Size(760,540);
   var root=Rows();window.Controls.Add(root);
   var setup=new TableLayoutPanel{AutoSize=true,ColumnCount=original.Languages==null?4:5,RowCount=2};
   for(int i=0;i<setup.ColumnCount;i++)setup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/setup.ColumnCount));
   int fieldColumn=0;
   void AddField(string title,Control input){
    var label=Description(title);
    label.Margin=new Padding(0,0,8,4);
    input.AccessibleName=label.Text;
    input.Dock=DockStyle.Top;input.Margin=new Padding(0,0,12,4);
    setup.Controls.Add(label,fieldColumn,0);
    setup.Controls.Add(input,fieldColumn++,1);
   }
   ClickWheelNumericUpDown DimensionField(string name,double value)=>
    new(){Name=name,Minimum=10,Maximum=500,DecimalPlaces=3,Value=(decimal)value};
   var width=DimensionField("resizeWidth",original.Width);
   var height=DimensionField("resizeHeight",original.Height);
   AddField("Šírka etikety (mm)",width);
   AddField("Výška etikety (mm)",height);
   using var language=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="resizeLanguage"};
   if(original.Languages!=null){
    language.Items.AddRange(original.Languages.Codes.ToArray());
    language.SelectedIndex=original.Languages.Codes.FindIndex(code=>LabelLanguages.SameCode(code,original.Languages.Selected));
    AddField("Jazyk etikety",language);
   }
   ModernComboBox AnchorField(string name,params string[] labels){
    var input=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name=name};
    input.Items.AddRange(Array.ConvertAll(labels,Loc.T));
    input.SelectedIndex=0;
    return input;
   }
   var horizontal=AnchorField("resizeAnchorX","Vľavo","Stred","Vpravo");
   var vertical=AnchorField("resizeAnchorY","Hore","Stred","Dole");
   AddField("Vodorovná kotva",horizontal);
   AddField("Zvislá kotva",vertical);
   ManagementLayout.AddRow(root,setup);

   var compare=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};
   compare.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   compare.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
   compare.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
   compare.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
   ManagementLayout.AddRow(root,compare,SizeType.Percent);
   var elements=new ListBox{Dock=DockStyle.Fill,Margin=new Padding(0,4,8,6),IntegralHeight=false,
    HorizontalScrollbar=true,Name="resizeElements",AccessibleName=Loc.T("Prvky etikety")};
   foreach(int index in Elements.Indices(original)){
    string caption=index>=-1?QuickPrint.Name(original,index):original.Symbols[-3-index].Name+" "+(-2-index);
    if(Elements.Locked(original,index))caption+=" · "+Loc.T("Zamknuté");
    elements.Items.Add(new ResizeElement(index,caption));
   }
   compare.Controls.Add(elements,0,0);
   (ResizeCanvas Canvas,Label Caption) AddCanvas(int column,string title,string name){
    var panel=Rows();panel.Padding=new Padding(6,0,0,0);
    compare.Controls.Add(panel,column,0);
    var caption=Description(title);ManagementLayout.AddRow(panel,caption);
    var canvas=new ResizeCanvas{Name=name,AccessibleName=Loc.T(title)};
    ManagementLayout.AddRow(panel,canvas,SizeType.Percent);
    return (canvas,caption);
   }
   var (before,beforeCaption)=AddCanvas(1,"Pôvodná etiketa","resizeBefore");
   var (after,afterCaption)=AddCanvas(2,"Nový rozmer","resizeAfter");
   string Dimensions(LabelSettings label)=>string.Format("{0:0.###} × {1:0.###} mm",label.Width,label.Height);
   beforeCaption.Text=Loc.T("Pôvodná etiketa")+" · "+Dimensions(original);
   var status=Description("Kontrolujem etiketu…");status.Name="resizeStatus";ManagementLayout.AddRow(root,status);
   var issues=new ListBox{Height=112,IntegralHeight=false,HorizontalScrollbar=true,Name="resizeIssues",AccessibleName=Loc.T("Problémy rozloženia")};
   ManagementLayout.AddRow(root,issues);
   var footer=new UiMessageBox.ActionRow();ManagementLayout.AddRow(root,footer);
   ResizeReview review=null;
   WorkContext active=null;
   bool closed=false,changing=false;
   var save=Btn(Loc.T("Uložiť ako novú šablónu…"),delegate{
    if(review!=null&&SaveLibraryTemplate(window,null,review.Variant))window.DialogResult=DialogResult.OK;
   },HeaderIcon.Save);
   save.Name="resizeSave";save.Enabled=false;
   var cancel=Btn(Loc.T("Zavrieť"),()=>window.DialogResult=DialogResult.Cancel);
   footer.AddAction(save);footer.AddAction(cancel);window.CancelButton=cancel;

   int SelectedElement()=>elements.SelectedItem is ResizeElement selected?selected.Index:-2;
   void Highlight(){
    before.FocusedElement=after.FocusedElement=SelectedElement();
    before.Invalidate();after.Invalidate();
   }
   void ReadAnchor(){
    int index=SelectedElement();
    changing=true;
    try{
     if(horizontal.Items.Count==4)horizontal.Items.RemoveAt(3);
     if(index>=0&&LabelResize.CanStretch(original,index))horizontal.Items.Add(Loc.T("Oba okraje"));
     anchors.TryGetValue(index,out var value);
     horizontal.SelectedIndex=(int)value.Horizontal;vertical.SelectedIndex=(int)value.Vertical;
     horizontal.Enabled=vertical.Enabled=index!=-2&&!Elements.Locked(original,index);
    }finally{changing=false;}
    Highlight();
   }
   void Schedule(){
    active?.Cancel();
    active=null;save.Enabled=false;
    status.Text=Loc.T("Kontrolujem etiketu…");
    debounce.Stop();debounce.Start();
   }
   void UpdateAnchor(){
    if(changing||SelectedElement()==-2||horizontal.SelectedIndex<0||vertical.SelectedIndex<0)return;
    anchors[SelectedElement()]=new ResizeAnchor((ResizeEdge)horizontal.SelectedIndex,(ResizeEdge)vertical.SelectedIndex);
    Schedule();
   }
   elements.SelectedIndexChanged+=delegate{ReadAnchor();};
   horizontal.SelectedIndexChanged+=delegate{UpdateAnchor();};
   vertical.SelectedIndexChanged+=delegate{UpdateAnchor();};
   width.ValueChanged+=delegate{Schedule();};
   height.ValueChanged+=delegate{Schedule();};
   language.SelectedIndexChanged+=delegate{Schedule();};
   issues.SelectedIndexChanged+=delegate{
    if(changing||issues.SelectedItem is not ResizeProblem problem)return;
    int target=-1;
    for(int i=0;i<elements.Items.Count;i++)
     if(((ResizeElement)elements.Items[i]).Index==problem.Issue.Element){target=i;break;}
    elements.SelectedIndex=target;
    if(problem.Language!=null)language.SelectedItem=problem.Language;
   };

   debounce.Tick+=async delegate{
    debounce.Stop();
    var work=new WorkContext();active=work;
    var snapshot=new Dictionary<int,ResizeAnchor>(anchors);
    double newWidth=(double)width.Value,newHeight=(double)height.Value;
    string selectedLanguage=language.SelectedItem as string;
    ResizeReview result=null;
    try{
     result=await Task.Run(()=>{
      using var scope=work.Enter();
      var variant=LabelResize.Apply(original,newWidth,newHeight,snapshot);
      var problems=LabelResize.Inspect(variant,instant);
      var originalScene=ResizeScene(original,instant,selectedLanguage);
      try{return new ResizeReview(variant,problems,originalScene,ResizeScene(variant,instant,selectedLanguage));}
      catch{originalScene.Dispose();throw;}
     });
     if(closed||active!=work)return;
     var selectedIssue=issues.SelectedItem as ResizeProblem;
     review?.Dispose();
     review=result;result=null;
     before.Geometry=original;before.Scene=review.Before;
     after.Geometry=review.Variant;after.Scene=review.After;
     before.ComparisonSize=after.ComparisonSize=new SizeF((float)Math.Max(original.Width,review.Variant.Width),
      (float)Math.Max(original.Height,review.Variant.Height));
     afterCaption.Text=Loc.T("Nový rozmer")+" · "+Dimensions(review.Variant);
     Highlight();
     changing=true;issues.BeginUpdate();
     try{
      issues.Items.Clear();
      issues.Items.AddRange(review.Problems.ToArray());
      if(selectedIssue!=null)
       issues.SelectedIndex=review.Problems.FindIndex(problem=>problem.Language==selectedIssue.Language&&
        problem.Issue.Element==selectedIssue.Issue.Element&&problem.Issue.Message==selectedIssue.Issue.Message);
     }finally{issues.EndUpdate();changing=false;}
     status.Text=review.Problems.Count==0?Loc.T("Rozloženie je bez problémov."):
      string.Format(Loc.T("Zistené problémy ({0}). Vyberte položku na označenie prvku."),review.Problems.Count);
     save.Enabled=true;
    }catch(OperationCanceledException) when(work.IsCancelled){}
    catch(Exception ex){
     if(!closed&&active==work){
      issues.Items.Clear();
      status.Text=ex.Message;
      after.Scene=null;after.Invalidate();
     }
    }finally{result?.Dispose();}
   };

   if(elements.Items.Count>0)elements.SelectedIndex=0;
   else ReadAnchor();
   window.Shown+=delegate{Schedule();};
   try{ApplyColors(window);window.ShowDialog(this);}
   finally{closed=true;active?.Cancel();review?.Dispose();}
  }

 }
}
