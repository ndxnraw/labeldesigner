using System;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  void EditShape(int index, bool line) {
   CommitInspectorIfFocused();
   bool existing = index <= -3;
   if (!existing && symbols.Count >= 20) throw new InvalidOperationException(Loc.T("Najviac 20 symbolov."));
   var shape = existing ? symbols[-3-index].Copy() : new SymbolItem {
    Width = Math.Min(25, N("Width")-6), Height = line ? .3 : Math.Min(12, N("Height")-6),
    Shape = new ShapeContent { IsLine = line }
   };
   line = shape.Shape.IsLine;
   using (var dialog = FeatureForm(line ? "Čiara" : "Obdĺžnik", 470, 550)) {
    var fields = FeatureStack(dialog);
    decimal factor = (decimal)unitFactor;
    int decimals = unitFactor == 10 ? 3 : 2;
    string unit = unitFactor == 10 ? " (cm)" : " (mm)";
    Func<string,double,double,NumericUpDown> dimensions = (key,value,min) =>
     Number(fields, Loc.T(key)+unit, (decimal)value/factor, (decimal)min/factor, 500/factor, decimals);
    var x = dimensions("Poloha X", shape.X, -500); x.Name = "shapeX";
    var y = dimensions("Poloha Y", shape.Y, -500); y.Name = "shapeY";
    var width = dimensions(line ? "Dĺžka" : "Šírka", shape.Width, 1); width.Name = "shapeWidth";
    var height = dimensions(line ? "Hrúbka čiary" : "Výška", shape.Height, line ? ShapeContent.MinimumLineThickness : 1);
    height.Name = "shapeHeight";
    NumericUpDown stroke = null;
    ComboBox fill = null;
    if (!line) {
     stroke = dimensions("Hrúbka obrysu", shape.Shape.Stroke, 0); stroke.Name = "shapeStroke";
     fill = Choice(fields, "Výplň tvaru", new[]{"Priehľadná","Čierna","Biela"}, (int)shape.Shape.Fill);
     fill.Name = "shapeFill";
    }
    AddAccept(dialog, fields, delegate {
     shape.X = (double)(x.Value*factor);
     shape.Y = (double)(y.Value*factor);
     shape.Width = (double)(width.Value*factor);
     shape.Height = (double)(height.Value*factor);
     if (!line) {
      shape.Shape.Stroke = (double)(stroke.Value*factor);
      shape.Shape.Fill = (ShapeFill)fill.SelectedIndex;
      if (shape.Shape.Stroke == 0 && shape.Shape.Fill == ShapeFill.Transparent)
       throw new InvalidOperationException(Loc.T("Neplatný obrys alebo výplň tvaru."));
     }
     ChangeDocument(label => {
      if (existing) label.Symbols[-3-index] = shape;
      else label.Symbols.Add(shape);
     });
     preview.SelectElement(existing ? index : -2-symbols.Count);
    });
    dialog.ShowDialog(this);
   }
  }
 }
}
