using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
sealed class StudioFeatureRenderer:ToolStripProfessionalRenderer {
 public StudioFeatureRenderer(bool dark):base(new StudioMenuColors(dark)){}
 protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){
  if(e.Item.Enabled)e.ArrowColor=e.Item.ForeColor;
  base.OnRenderArrow(e);
 }
}
sealed class StudioIconMenuItem:ToolStripMenuItem {
 public StudioIconMenuItem(string text,HeaderIcon icon,Color color,int size):base(text){ForeColor=color;Image=HeaderActionButton.MenuImage(icon,color,size);}
 protected override void Dispose(bool disposing){if(disposing&&Image!=null){var old=Image;Image=null;old.Dispose();}base.Dispose(disposing);}
}
public partial class MainForm {
 ToolStripMenuItem FeatureGroup(string text,HeaderIcon icon){var item=FeatureItem(text,icon,null);item.DropDown.Renderer=featureMenu.Renderer;item.DropDown.ForeColor=featureMenu.ForeColor;featureMenu.Items.Add(item);return item;}
 ToolStripMenuItem FeatureItem(string text,HeaderIcon icon,Action action){var item=new StudioIconMenuItem(Loc.T(text),icon,StudioTheme.Text(dark),(int)Math.Round(20*PanelScale));if(action!=null)item.Click+=delegate{try{action();}catch(Exception ex){Error(ex);}};return item;}
 void GroupedFeatureMenu(){while(featureMenu.Items.Count>0){var old=featureMenu.Items[0];featureMenu.Items.RemoveAt(0);old.Dispose();}featureMenu.Renderer=new StudioFeatureRenderer(dark);featureMenu.ForeColor=StudioTheme.Text(dark);featureMenu.ImageScalingSize=new Size((int)(20*PanelScale),(int)(20*PanelScale));
  var project=FeatureGroup("Projekt",HeaderIcon.Library);project.DropDownItems.Add(FeatureItem("Šablóny a nedávne projekty",HeaderIcon.Library,ShowLibrary));project.DropDownItems.Add(FeatureItem("Automatické ukladanie",HeaderIcon.AutoSave,RecoveryPreferences));
  project.DropDownItems.Add(FeatureItem(WorkspaceBackupTitle,HeaderIcon.Library,ShowWorkspaceBackup));
  var insert=FeatureGroup("Vložiť",HeaderIcon.AddText);insert.DropDownItems.Add(FeatureItem("Pridať text",HeaderIcon.AddText,AddText));insert.DropDownItems.Add(FeatureItem("Vložiť obrázok",HeaderIcon.Image,AddImage));insert.DropDownItems.Add(FeatureItem("Pridať kód…",HeaderIcon.Barcode,delegate{ChooseBarcode(false);}));insert.DropDownItems.Add(FeatureItem("Pridať 2D kód",HeaderIcon.Barcode,AddMatrix));insert.DropDownItems.Add(FeatureItem("Pridať čiarový kód",HeaderIcon.Barcode,AddBarcodeObject));
  var data=FeatureGroup("Dáta",HeaderIcon.Data);data.DropDownItems.Add(FeatureItem("Dávka a premenné",HeaderIcon.Data,ShowBatch));data.DropDownItems.Add(FeatureItem("História tlače",HeaderIcon.Print,ShowPrintHistory));
  var layout=FeatureGroup("Rozloženie",HeaderIcon.Panels);layout.DropDownItems.Add(FeatureItem("Rozloženie a výber",HeaderIcon.Panels,ShowArrange));layout.DropDownItems.Add(FeatureItem("Samolepiace hárky",HeaderIcon.Table,EditSheet));
  var maintenance=FeatureGroup("Kontrola a údržba",HeaderIcon.Tools);maintenance.DropDownItems.Add(FeatureItem("Kontrola etikety",HeaderIcon.Check,ShowChecks));maintenance.DropDownItems.Add(FeatureItem("Diagnostika",HeaderIcon.Diagnostics,ShowDiagnostics));maintenance.DropDownItems.Add(FeatureItem("Aktualizácie",HeaderIcon.Update,ShowUpdates));
 }
}
}
