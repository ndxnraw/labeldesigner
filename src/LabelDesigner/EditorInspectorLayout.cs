using System;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  Label typographyHeading,fitHeading;
  Control typographyFields;

  Label InspectorHeading(TableLayoutPanel panel,string title) {
   var heading=new Label {
    Text=Loc.T(title),AutoSize=true,UseMnemonic=false,Font=UiFonts.Get(Font.Size,FontStyle.Bold)
   };
   StackAdd(panel,heading);
   heading.Margin=new Padding(0,9,0,2);
   return heading;
  }

  void MoveInspectorPair(TableLayoutPanel source,TableLayoutPanel target,Control first,Control second) {
   string LabelOf(Control field)=>source.GetControlFromPosition(0,source.GetRow(field))?.Text??field.AccessibleName;
   InspectorPair(target,LabelOf(first),first,LabelOf(second),second);
  }

  Control InspectorPair(TableLayoutPanel target,string firstTitle,Control first,string secondTitle,Control second) {
   var row=new TableLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,RowCount=2};
   row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   StackAdd(target,row);
   row.Margin=new Padding(0,0,0,2);
   AddCell(firstTitle,first,0);
   AddCell(secondTitle,second,1);
   return row;

   void AddCell(string title,Control field,int column) {
    int left=column==0?0:5,right=column==0?5:0;
    var label=new Label {
     Text=title,AutoSize=true,UseMnemonic=false,Anchor=AnchorStyles.Left|AnchorStyles.Bottom,
     Margin=new Padding(left,2,right,1)
    };
    field.AccessibleName=title;
    label.TextChanged+=delegate{field.AccessibleName=label.Text;};
    field.Dock=DockStyle.Top;
    field.Margin=new Padding(left,0,right,3);
    field.TabIndex=column;
    row.Controls.Add(label,column,0);
    row.Controls.Add(field,column,1);
    row.SizeChanged+=delegate{
     int width=Math.Max(1,row.ClientSize.Width/2-label.Margin.Horizontal);
     if(label.MaximumSize.Width!=width)label.MaximumSize=new Size(width,0);
    };
   }
  }
 }
}
