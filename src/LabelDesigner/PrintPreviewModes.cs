using System;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class PrintPanel {
  readonly FlowLayoutPanel detailTools=new();
  readonly NumericUpDown detailIndex=new ClickWheelNumericUpDown();
  RectangleF[] pageLabels=Array.Empty<RectangleF>();
  const int LabelDetailMode=6;

  void BuildPreviewModes(FlowLayoutPanel tools) {
   preview.AccessibleName=Loc.T("Náhľad tlače");
   zoom.Width=168;
   zoom.DropDownStyle=ComboBoxStyle.DropDownList;
   zoom.AccessibleName=Loc.T("Zobrazenie náhľadu");
   zoom.Items.AddRange(new object[]{
    Loc.T("Celá strana"),"50 %","100 %","150 %","200 %","400 %",Loc.T("Detail etikety")
   });
   zoom.SelectedIndex=0;
   zoom.SelectedIndexChanged+=delegate{UpdatePreviewMode();};
   tools.Controls.Add(zoom);

   detailTools.AutoSize=true;
   detailTools.Visible=false;
   detailTools.Controls.Add(new Label{Text=Loc.T("Etiketa na strane"),AutoSize=true});
   detailIndex.Minimum=detailIndex.Maximum=1;
   detailIndex.Width=72;
   detailIndex.AccessibleName=Loc.T("Etiketa na strane");
   detailIndex.ValueChanged+=delegate{if(!updating)UpdatePreviewMode();};
   detailTools.Controls.Add(detailIndex);
   tools.Controls.Add(detailTools);
  }

  void SetPreviewLabels(RectangleF[] bounds,int pageIndex) {
   pageLabels=bounds??Array.Empty<RectangleF>();
   detailIndex.Maximum=Math.Max(1,pageLabels.Length);
   if(preview.Page!=pageIndex)detailIndex.Value=1;
   preview.Page=pageIndex;
   UpdatePreviewMode();
  }

  void UpdatePreviewMode() {
   int selected=zoom.SelectedIndex;
   preview.ShowDetail=selected==LabelDetailMode;
   preview.Zoom=selected switch {1=>.5,2=>1,3=>1.5,4=>2,5=>4,_=>0};
   var detail=pageLabels.Length==0?RectangleF.Empty:pageLabels[(int)detailIndex.Value-1];
   preview.DetailBounds=detail;
   preview.AccessibleDescription=preview.ShowDetail&&!detail.IsEmpty&&
    !preview.Printable.IsEmpty&&!preview.Printable.Contains(detail)
    ?Loc.T("Prerušovaná čiara označuje hranicu tlačiteľnej oblasti."):"";
   detailTools.Visible=preview.ShowDetail&&pageLabels.Length>1;
   preview.AutoScrollPosition=Point.Empty;
   preview.Invalidate();
  }
 }
}
