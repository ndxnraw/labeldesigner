using System;
using System.Collections.Generic;

namespace EanEtikety {
 public enum ResizeEdge { Start, Center, End, Stretch }
 public readonly record struct ResizeAnchor(ResizeEdge Horizontal,ResizeEdge Vertical);
 public sealed record ResizeProblem(string Language,LabelIssue Issue) {
  public override string ToString()=>Language==null?Issue.ToString():Language+" · "+Issue;
 }

 public static class LabelResize {
  public static bool CanStretch(LabelSettings source,int index)=>index>=0&&source.Texts[index].Rotation==0;

  public static LabelSettings Apply(LabelSettings source,double width,double height,IReadOnlyDictionary<int,ResizeAnchor> anchors){
   var result=ProjectXml.Clone(source);
   result.Width=width;result.Height=height;
   if(!result.UsePaper&&!result.Sheet&&!result.Layout.Enabled){
    result.PageWidth=width;result.PageHeight=height;
   }
   double dx=width-source.Width,dy=height-source.Height;
   foreach(int index in Elements.Indices(result)){
    WorkContext.Check();
    // Keep the previewed geometry when this variant is reopened or rendered.
    if(index>=0)result.Texts[index].AutoWidth=false;
    if(Elements.Locked(result,index)||!anchors.TryGetValue(index,out var anchor))continue;
    double x=Displacement(dx,anchor.Horizontal),y=Displacement(dy,anchor.Vertical);
    if(index==-1){
     result.BarX+=x;result.BarY+=y;
    }else if(index>=0){
     var text=result.Texts[index];
     text.X+=x;text.Y+=y;
     if(anchor.Horizontal==ResizeEdge.Stretch&&CanStretch(source,index))text.Width=Math.Clamp(text.Width+dx,1,500);
    }else{
     var symbol=result.Symbols[-3-index];
     symbol.X+=x;symbol.Y+=y;
    }
   }
   Engine.ValidateDraft(result);
   return result;
  }

  static double Displacement(double difference,ResizeEdge edge)=>edge switch {
   ResizeEdge.Center=>difference/2,
   ResizeEdge.End=>difference,
   _=>0
  };

  public static List<ResizeProblem> Inspect(LabelSettings variant,DateTime instant){
   var problems=new List<ResizeProblem>();
   var layout=variant.Layout;
   if(layout.Enabled&&(layout.Left+layout.Columns*variant.Width+(layout.Columns-1)*layout.HorizontalGap+layout.Right>variant.PageWidth+.001||
      layout.Top+layout.Rows*variant.Height+(layout.Rows-1)*layout.VerticalGap+layout.Bottom>variant.PageHeight+.001))
    problems.Add(new ResizeProblem(null,new LabelIssue{Blocking=true,Message=Loc.T("Etikety presahujú papier")}));
   var languages=(IEnumerable<string>)variant.Languages?.Codes??new string[]{null};
   foreach(string language in languages){
    WorkContext.Check();
    var prepared=QuickPrint.PrepareEditor(variant,instant,language);
    foreach(var issue in prepared.Issues){
     issue.Element=Elements.SourceIndex(prepared.Label,issue.Element);
     problems.Add(new ResizeProblem(language,issue));
    }
   }
   return problems;
  }
 }
}
