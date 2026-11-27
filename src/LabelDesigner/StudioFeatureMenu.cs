using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
sealed class StudioFeatureRenderer:ToolStripProfessionalRenderer {
 public StudioFeatureRenderer(bool dark):base(new StudioMenuColors(dark)){}
 protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){
  if(e.Item.Enabled)e.ArrowColor=StudioTheme.HighContrast&&e.Item.Selected?SystemColors.HighlightText:e.Item.ForeColor;
  base.OnRenderArrow(e);
 }
 protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
  if(StudioTheme.HighContrast&&e.Item.Enabled&&e.Item.Selected)e.TextColor=SystemColors.HighlightText;
  base.OnRenderItemText(e);
 }
}
sealed class StudioIconMenuItem:ToolStripMenuItem {
 readonly HeaderIcon icon;
 public StudioIconMenuItem(string text,HeaderIcon icon,Color color,int size):base(text){this.icon=icon;RefreshIcon(color,size);}
 public void RefreshIcon(Color color,int size){var old=Image;ForeColor=color;Image=HeaderActionButton.MenuImage(icon,color,size);old?.Dispose();}
 protected override void Dispose(bool disposing){if(disposing&&Image!=null){var old=Image;Image=null;old.Dispose();}base.Dispose(disposing);}
}
public partial class MainForm {
 ToolStripMenuItem FeatureGroup(string text,HeaderIcon icon){var item=FeatureItem(text,icon,null);item.DropDown.Renderer=featureMenu.Renderer;item.DropDown.ForeColor=featureMenu.ForeColor;featureMenu.Items.Add(item);return item;}
 ToolStripMenuItem FeatureItem(string text,HeaderIcon icon,Action action){var item=new StudioIconMenuItem(Loc.T(text),icon,StudioTheme.Text(dark),(int)Math.Round(20*PanelScale));if(action!=null)item.Click+=delegate{try{action();}catch(Exception ex){Error(ex);}};return item;}
 void PrepareFeatureMenu(ContextMenuStrip menu) {
  while(menu.Items.Count>0){var old=menu.Items[0];menu.Items.RemoveAt(0);old.Dispose();}
  menu.Renderer=new StudioFeatureRenderer(dark);
  menu.ForeColor=StudioTheme.Text(dark);
  menu.ImageScalingSize=new Size((int)(20*PanelScale),(int)(20*PanelScale));
 }
 void BuildProjectMenu() {
  PrepareFeatureMenu(projectMenu);
  var open=FeatureItem("Otvoriť",HeaderIcon.Open,Open);open.ShortcutKeyDisplayString="Ctrl+O";
  var save=FeatureItem("Uložiť",HeaderIcon.Save,Save);save.ShortcutKeyDisplayString="Ctrl+S";
  var saveAs=FeatureItem("Uložiť ako",HeaderIcon.SaveAs,SaveAs);saveAs.ShortcutKeyDisplayString="Ctrl+Shift+S";
  projectMenu.Items.AddRange(new ToolStripItem[]{open,save,saveAs,new ToolStripSeparator(),
   FeatureItem("Šablóny a nedávne projekty",HeaderIcon.Library,ShowLibrary),
   FeatureItem("Upraviteľné polia šablóny…",HeaderIcon.AddText,ConfigureQuickPrintFields),
   FeatureItem(TemplateInputsTitle,HeaderIcon.Data,()=>EditTemplateInputs()),
   FeatureItem("Jazyky etikety…",HeaderIcon.Data,EditLabelLanguages),
   FeatureItem("Automatické ukladanie",HeaderIcon.AutoSave,RecoveryPreferences)});
 }
 void BuildInsertMenu() {
  PrepareFeatureMenu(insertMenu);
  insertMenu.Items.AddRange(new ToolStripItem[]{
   FeatureItem("Pridať text",HeaderIcon.AddText,AddText),
   FeatureItem("Knižnica blokov…",HeaderIcon.Library,ShowBlockLibrary),
   FeatureItem("Vložiť obrázok",HeaderIcon.Image,AddImage),
   FeatureItem(MappedImageTitle,HeaderIcon.Image,()=>EditMappedImage(-2)),
   FeatureItem("Pridať kód…",HeaderIcon.Barcode,()=>ChooseBarcode(false)),
   FeatureItem("Pridať čiaru…",HeaderIcon.Image,()=>EditShape(-2,true)),
   FeatureItem("Pridať obdĺžnik…",HeaderIcon.Image,()=>EditShape(-2,false)),
   FeatureItem("Symbol Zálohované",HeaderIcon.Deposit,()=>EditSymbol(-2))});
  if(hideBarcode)insertMenu.Items.Insert(5,FeatureItem("Obnoviť hlavný kód",HeaderIcon.Barcode,AddBarcodeObject));
 }
 void GroupedFeatureMenu() {
  PrepareFeatureMenu(featureMenu);
  var layout=FeatureGroup("Rozloženie",HeaderIcon.Panels);
  layout.DropDownItems.Add(FeatureItem("Rozloženie a výber",HeaderIcon.Panels,ShowArrange));
  var stack=FeatureItem(StackTitle,HeaderIcon.Panels,ConfigureStack);
  stack.Enabled=CanConfigureStack();layout.DropDownItems.Add(stack);
  layout.DropDownItems.Add(FeatureItem("Zmeniť rozmery etikety…",HeaderIcon.Panels,ShowResizeLabel));
  layout.DropDownItems.Add(FeatureItem("Uložiť výber ako blok…",HeaderIcon.Save,SaveSelectionAsBlock));
  layout.DropDownItems.Add(FeatureItem("Samolepiace hárky",HeaderIcon.Table,EditSheet));
  var maintenance=FeatureGroup("Kontrola a údržba",HeaderIcon.Tools);
  maintenance.DropDownItems.Add(FeatureItem("História tlače",HeaderIcon.Print,ShowPrintHistory));
  maintenance.DropDownItems.Add(FeatureItem("Správa aplikácie…",HeaderIcon.Tools,ShowApplicationManagement));
  var help=FeatureGroup("Nastavenia a pomoc",HeaderIcon.Settings);
  help.DropDownItems.Add(FeatureItem("Nastavenia",HeaderIcon.Settings,StudioPreferences));
  help.DropDownItems.Add(FeatureItem("Návod",HeaderIcon.Help,Help));
  help.DropDownItems.Add(FeatureItem("O aplikácii",HeaderIcon.About,()=>UiMessageBox.Show(this,
   "ND LabelDesigner — Pre-release\r\n\r\nCreated by Daniel Návojský (ndxnraw) using Codex | 2026",Loc.T("O aplikácii"))));
 }
}
}
