using System;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 internal static class ManagementLayout {
  internal static TableLayoutPanel Rows() {
   var rows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(12)};
   rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   return rows;
  }

  internal static TableLayoutPanel ScrollRows(Panel viewport) {
   viewport.AutoScroll=true;
   var rows=Rows();
   rows.AutoSize=true;
   rows.Dock=DockStyle.Top;
   // Reserve the scrollbar width so wrapping cannot cause a horizontal scrollbar.
   void WrapLabels() {
    int width=Math.Max(1,viewport.ClientSize.Width-SystemInformation.VerticalScrollBarWidth);
    rows.MaximumSize=new Size(width,0);
    rows.Width=width;
    foreach(Control item in rows.Controls)
     if(item is Label || item is CheckBox)item.MaximumSize=new Size(Math.Max(1,width-rows.Padding.Horizontal),0);
   }
   viewport.SizeChanged+=delegate{WrapLabels();};
   rows.ControlAdded+=delegate{WrapLabels();};
   viewport.Controls.Add(rows);
   return rows;
  }

  internal static void AddRow(TableLayoutPanel rows,Control control,SizeType height=SizeType.AutoSize) {
   control.Dock=height==SizeType.Percent?DockStyle.Fill:DockStyle.Top;
   control.Margin=new Padding(0,4,0,6);
   control.TabIndex=rows.RowCount;
   rows.RowStyles.Add(new RowStyle(height,100));
   rows.Controls.Add(control,0,rows.RowCount++);
  }

  internal static Label Description(string text) {
   return new Label{Text=Loc.T(text),AutoSize=true,UseMnemonic=false};
  }

  internal static ModernButton Action(string text,Action action) {
   var button=new ModernButton{Text=Loc.T(text),AutoSize=true,FlatStyle=FlatStyle.Flat,MinimumSize=new Size(0,38),UseMnemonic=false};
   button.Click+=delegate{action();};
   return button;
  }
 }
}
