using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class PreviewPanel {
  internal readonly Dictionary<int,ElementEdit> ProjectedStackElements=new();
  internal bool InVerticalStack(int index)=>Settings!=null&&VerticalStack.ForElement(Settings,index)!=null;

  void DrawVerticalStack(Graphics graphics,Rectangle label){
   var stack=Settings==null?null:VerticalStack.ForElement(Settings,SelectedIndex);
   if(stack==null)return;
   var first=ScreenPoint(new PointF((float)stack.X,(float)stack.Y));
   var last=ScreenPoint(new PointF((float)(stack.X+stack.Width),(float)(stack.Y+stack.Height)));
   using var pen=new Pen(StudioTheme.HighContrast?SystemColors.Highlight:Color.FromArgb(118,62,152),2){
    DashStyle=DashStyle.Dash
   };
   var state=graphics.Save();
   try{
    graphics.SetClip(label,CombineMode.Intersect);
    graphics.DrawRectangle(pen,first.X,first.Y,last.X-first.X,last.Y-first.Y);
   }finally{graphics.Restore(state);}
  }
 }

 public partial class MainForm {
  const string StackTitle="Zvislá skupina…";
  List<VerticalStack> verticalStacks=new();
  Button selectionStack,textStack,codeStack;

  Button StackAction()=>Btn(Loc.T(StackTitle),delegate{try{ConfigureStack();}catch(Exception ex){Error(ex);}},HeaderIcon.Panels);

  bool CanConfigureStack(){
   string group=SelectedVisibilityGroup();
   if(group!=null)return verticalStacks.Exists(stack=>stack.Group==group)||preview.Selection.Count>=2;
   return preview.Selection.Count>=2&&!preview.Selection.Exists(index=>
    elementMetadata.Exists(meta=>meta.Index==index&&!string.IsNullOrEmpty(meta.Group)));
  }

  void UseStackGeometry(LabelSettings source,LabelSettings placed){
   if(source.VerticalStacks.Count==0)return;
   foreach(int index in Elements.Indices(placed)){
    int original=Elements.SourceIndex(placed,index);
    if(VerticalStack.ForElement(source,original)==null)continue;
    var item=ElementTransform.Get(placed,index);
    item.Index=original;
    preview.ProjectedStackElements[original]=item;
   }
  }

  void ConfigureStack(){
   if(!CanConfigureStack())return;
   CommitInspectorIfFocused();
   var label=Read();
   string group=SelectedVisibilityGroup()??Guid.NewGuid().ToString("N");
   var existing=label.VerticalStacks.Find(stack=>stack.Group==group);
   var stack=existing?.Copy()??new VerticalStack{Group=group,Gap=1};
   if(existing==null){
    stack.Order=SelectedElements();
    stack.Order.Sort((a,b)=>ElementTransform.Bounds(ElementTransform.Get(label,a)).Top
     .CompareTo(ElementTransform.Bounds(ElementTransform.Get(label,b)).Top));
    var bounds=ElementTransform.Bounds(ElementTransform.Get(label,stack.Order[0]));
    foreach(int index in stack.Order)bounds=RectangleF.Union(bounds,ElementTransform.Bounds(ElementTransform.Get(label,index)));
    stack.X=Math.Round(Math.Clamp(bounds.Left,0,label.Width-1),3);
    stack.Y=Math.Round(Math.Clamp(bounds.Top,0,label.Height-1),3);
    stack.Width=Math.Round(Math.Max(1,Math.Min(bounds.Width,label.Width-stack.X)),3);
    stack.Height=Math.Round(Math.Max(1,label.Height-stack.Y),3);
   }

   using var dialog=FeatureForm(StackTitle,660,510);
   dialog.MinimumSize=new Size(540,435);
   var root=Rows();dialog.Controls.Add(root);
   var settings=Stack();settings.Padding=Padding.Empty;
   ClickWheelNumericUpDown Metric(string name,double value,decimal minimum=0)=>new(){
    Name=name,Minimum=minimum,Maximum=500,DecimalPlaces=3,Increment=.5m,Value=(decimal)value
   };
   var x=Metric("stackX",stack.X);var y=Metric("stackY",stack.Y);
   var width=Metric("stackWidth",stack.Width,1);var height=Metric("stackHeight",stack.Height,1);
   var gap=Metric("stackGap",stack.Gap);
   InspectorPair(settings,Loc.T("X (mm)"),x,Loc.T("Y (mm)"),y);
   InspectorPair(settings,Loc.T("Šírka (mm)"),width,Loc.T("Výška (mm)"),height);
   StackField(settings,Loc.T("Zvislý rozostup (mm)"),gap);
   ManagementLayout.AddRow(root,settings);
   ManagementLayout.AddRow(root,Description("Prvky sa zoradia zhora nadol. Skryté prvky uvoľnia miesto."));
   var list=new ListBox{Dock=DockStyle.Fill,Name="stackOrder",AccessibleName=Loc.T("Poradie prvkov"),
    IntegralHeight=false,HorizontalScrollbar=true};
   foreach(int index in stack.Order){
    string name=index>=0?QuickPrint.Name(label,index)+" · "+label.Texts[index].Text:
     index==-1?Loc.T("Hlavný kód"):label.Symbols[-3-index].Name+" "+(-2-index);
    list.Items.Add(new StackMember(index,name));
   }
   ManagementLayout.AddRow(root,list,SizeType.Percent);
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
   var up=Btn(Loc.T("Vyššie"),()=>Move(-1));var down=Btn(Loc.T("Nižšie"),()=>Move(1));
   actions.Controls.AddRange(new Control[]{up,down});
   bool disable=false;
   if(existing!=null)actions.Controls.Add(Btn(Loc.T("Vypnúť pružné rozloženie"),delegate{
    disable=true;dialog.DialogResult=DialogResult.OK;
   }));
   ManagementLayout.AddRow(root,actions);
   list.SelectedIndexChanged+=delegate{up.Enabled=list.SelectedIndex>0;down.Enabled=list.SelectedIndex>=0&&list.SelectedIndex<list.Items.Count-1;};
   list.SelectedIndex=0;
   var apply=Btn(Loc.T("Použiť"),()=>dialog.DialogResult=DialogResult.OK);
   var cancel=Btn(Loc.T("Zrušiť"),()=>dialog.DialogResult=DialogResult.Cancel);
   var footer=new UiMessageBox.ActionRow();footer.AddAction(apply);footer.AddAction(cancel);
   ManagementLayout.AddRow(root,footer);
   dialog.AcceptButton=apply;dialog.CancelButton=cancel;
   ApplyColors(dialog);
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   stack.X=(double)x.Value;stack.Y=(double)y.Value;stack.Width=(double)width.Value;
   stack.Height=(double)height.Value;stack.Gap=(double)gap.Value;
   stack.Order.Clear();
   foreach(StackMember member in list.Items)stack.Order.Add(member.Index);
   int selected=preview.SelectedIndex;
   ChangeDocument(doc=>{
    doc.VerticalStacks.RemoveAll(item=>item.Group==group);
    if(disable)return;
    foreach(int index in stack.Order)Elements.Meta(doc,index).Group=group;
    doc.VerticalStacks.Add(stack);
    Engine.ValidateDraft(doc);
   });
   preview.SelectElement(selected);

   void Move(int delta){
    int index=list.SelectedIndex,target=index+delta;
    if(index<0||target<0||target>=list.Items.Count)return;
    object item=list.Items[index];
    list.Items.RemoveAt(index);list.Items.Insert(target,item);list.SelectedIndex=target;
   }
  }

  sealed record StackMember(int Index,string Name) {
   public override string ToString()=>Name;
  }
 }
}
