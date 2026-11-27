using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace EanEtikety {
 public sealed class VerticalStack {
  public string Group;
  public double X,Y,Width,Height,Gap;
  public List<int> Order=new();

  public VerticalStack Copy(){
   var result=(VerticalStack)MemberwiseClone();
   result.Order=new List<int>(Order);
   return result;
  }

  public RectangleF Bounds()=>new((float)X,(float)Y,(float)Width,(float)Height);

  public static VerticalStack ForElement(LabelSettings label,int index)=>
   label.VerticalStacks.Find(stack=>stack.Order.Contains(index));

  public static void AppendCopies(IEnumerable<VerticalStack> source,LabelSettings target,
      IReadOnlyDictionary<int,int> members,IReadOnlyDictionary<string,string> groups,double dx,double dy){
   foreach(var stack in source){
    if(!groups.TryGetValue(stack.Group,out string group)||!stack.Order.TrueForAll(members.ContainsKey))continue;
    var copy=stack.Copy();
    copy.Group=group;copy.X+=dx;copy.Y+=dy;
    copy.Order=copy.Order.ConvertAll(index=>members[index]);
    target.VerticalStacks.Add(copy);
   }
  }

  internal static void Validate(LabelSettings label){
   if(label.VerticalStacks==null||label.VerticalStacks.Count>71)throw Invalid();
   var groups=new HashSet<string>(StringComparer.Ordinal);
   var members=new HashSet<int>();
   foreach(var stack in label.VerticalStacks){
    if(stack==null||string.IsNullOrWhiteSpace(stack.Group)||!groups.Add(stack.Group)||
       !InRange(stack.X,0,500)||!InRange(stack.Y,0,500)||!InRange(stack.Width,1,500)||
       !InRange(stack.Height,1,500)||!InRange(stack.Gap,0,500)||stack.Order==null||
       stack.Order.Count<1||stack.Order.Count>71)throw Invalid();
    foreach(int index in stack.Order)
     if(!members.Add(index)||!label.Metadata.Exists(item=>item!=null&&item.Group==stack.Group&&item.Index==index)||
        (index>=0&&index>=label.Texts.Count)||(index<=-3&&-3-index>=label.Symbols.Count)||index==-2)throw Invalid();
   }
  }

  static InvalidDataException Invalid()=>new(Loc.T("Neplatná zvislá skupina."));
  static bool InRange(double number,double min,double max)=>double.IsFinite(number)&&number>=min&&number<=max;

  public static void Prune(LabelSettings label){
   foreach(var stack in label.VerticalStacks)
    stack.Order.RemoveAll(index=>!label.Metadata.Exists(meta=>meta.Index==index&&meta.Group==stack.Group));
   label.VerticalStacks.RemoveAll(stack=>stack.Order.Count==0);
  }

  internal static void Remap(LabelSettings label,Dictionary<int,int> mapping){
   foreach(var stack in label.VerticalStacks){
    stack.Order.RemoveAll(index=>!mapping.ContainsKey(index));
    for(int i=0;i<stack.Order.Count;i++)stack.Order[i]=mapping[stack.Order[i]];
   }
   label.VerticalStacks.RemoveAll(stack=>stack.Order.Count==0);
   if(label.StackIssues!=null){
    label.StackIssues.RemoveAll(issue=>!mapping.ContainsKey(issue.Element));
    foreach(var issue in label.StackIssues)issue.Element=mapping[issue.Element];
   }
  }

  // Called on a prepared copy after visibility, substitutions and formatting.
  internal static void Apply(LabelSettings label){
   if(label.VerticalStacks.Count==0)return;
   label.StackIssues=new();
   foreach(var stack in label.VerticalStacks){
    double top=stack.Y;
    if(stack.X+stack.Width>label.Width+.005||stack.Y+stack.Height>label.Height+.005){
     int first=stack.Order.Find(index=>index!=-1||!label.HideBarcode);
     if(stack.Order.Contains(first))label.StackIssues.Add(new LabelIssue{Element=first,
      Message=Loc.T("Rám zvislej skupiny presahuje etiketu.")});
    }
    foreach(int index in stack.Order){
     WorkContext.Check();
     if(index==-1&&label.HideBarcode)continue;
     var item=ElementTransform.Get(label,index);
     TextItem text=index>=0?label.Texts[index]:null;
     bool grow=text!=null&&text.Rotation==0&&text.Text.Length<=4000;
     double heightNeeded=item.Height;
     if(grow){
      item.Width=Math.Min(text.Width,stack.Width);
      heightNeeded=Math.Max(text.Height,FixedTextLayout.RequiredHeight(text,item.Width,label.Dpi));
      item.Height=Math.Min(500,heightNeeded);
     }
     var bounds=ElementTransform.Bounds(item);
     double x=stack.X+item.X-bounds.Left,y=top+item.Y-bounds.Top;
     bool tooFar=!InRange(x,-500,500)||!InRange(y,-500,500);
     if(tooFar||bounds.Width>stack.Width+.005||top+bounds.Height>stack.Y+stack.Height+.005||heightNeeded>500)
      label.StackIssues.Add(new LabelIssue{Element=index,Blocking=tooFar,
       Message=Loc.T("Zvislá skupina presahuje rám.")});
     if(!tooFar){
      // Updating the origin directly keeps barcode size, caption and module width intact.
      if(index==-1){label.BarX=x;label.BarY=y;}
      else if(text!=null){text.X=x;text.Y=y;text.Width=item.Width;text.Height=item.Height;text.AutoWidth=false;}
      else{var symbol=label.Symbols[-3-index];symbol.X=x;symbol.Y=y;}
     }
     top+=bounds.Height+stack.Gap;
    }
   }
   label.VerticalStacks.Clear();
  }
 }
}
