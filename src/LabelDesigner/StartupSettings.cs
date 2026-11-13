using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing;
using System.Windows.Forms;
namespace EanEtikety {
public partial class MainForm {
 string startupPath;ComboBox language=new ModernComboBox();
 void AddPreferences(TableLayoutPanel root){
  var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
  var heading=root.GetControlFromPosition(0,0);root.Controls.Remove(heading);header.Controls.Add(heading,0,0);
  var options=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};options.Controls.Add(new Label{Text=Loc.T("Jazyk"),AutoSize=true,Margin=new Padding(0,12,8,0)});
  ComboStyle(language);language.Width=135;language.DropDownStyle=ComboBoxStyle.DropDownList;language.Items.AddRange(new object[]{"Slovenčina","English","Čeština","Magyar"});options.Controls.Add(language);
  options.Controls.Add(Btn(Loc.T("Uložiť nastavenia spustenia"),SaveStartup));header.Controls.Add(options,1,0);root.Controls.Add(header,0,0);
  language.SelectedIndexChanged+=delegate{if(loading||language.SelectedIndex<0)return;Loc.Language=Loc.Codes[language.SelectedIndex];Localize();};
 }
 void Localize(){bool old=loading;loading=true;try{UiLocalization.Refresh(this);RefreshStudioMenus();UpdateUnitLabels(this);ApplyTheme();Gs1State();Text=Loc.T(dirty?"ND LabelDesigner — Pre-release • neuložené zmeny":"ND LabelDesigner — Pre-release");}finally{loading=old;}UpdatePreview();Invalidate(true);}
 void LoadStartup(){Loc.Language="sk";StartupProfile profile;string failure=null;try{profile=StartupStore.Load(startupPath);}catch(Exception ex){profile=new StartupProfile();failure=ex.Message;}
  Loc.Language=profile.Language;loading=true;language.SelectedIndex=Array.IndexOf(Loc.Codes,profile.Language);loading=false;
  try{Apply(StartupLayout.Create(profile));}catch(Exception ex){failure=ex.Message;Apply(LabelSettings.Default());}
  Localize();if(failure!=null)status.Text=Loc.T("Nastavenia spustenia sa nepodarilo načítať. Použité sú predvolené hodnoty. ")+failure;
 }
 void SaveStartup(){try{var profile=new StartupProfile{Language=Loc.Language,Width=N("Width"),Height=N("Height"),Unit=unitFactor==10?"cm":"mm",DarkMode=dark};StartupStore.Save(startupPath,profile);status.Text=Loc.T("Nastavenia spustenia uložené: ")+startupPath;}catch(Exception ex){Error(ex);}}
}
}
