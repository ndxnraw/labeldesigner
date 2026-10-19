using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing;
using System.Windows.Forms;
namespace EanEtikety {
public class StartupProfile {
 public int Version=2;
 public double Width=60,Height=45;public bool DarkMode=true;public string Unit="mm";
 public string Language="sk";
 public LabelSettings Label; // Read legacy 2.4 profiles only. Never serialize label contents.
 public bool ShouldSerializeLabel(){return false;}
}
public static class StartupStore {
 public static string DefaultPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NDLabelMaker","startup.xml");}}
 public static StartupProfile Load(string path){
  if(!File.Exists(path))return new StartupProfile();
  using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16000000})){
   var profile=(StartupProfile)new XmlSerializer(typeof(StartupProfile)).Deserialize(reader);
   if(profile.Version==1&&profile.Label!=null){profile.Width=profile.Label.Width;profile.Height=profile.Label.Height;profile.DarkMode=profile.Label.DarkMode;profile.Unit=profile.Label.Unit;profile.Version=2;profile.Label=null;}
   Validate(profile);return profile;
  }
 }
 public static void Validate(StartupProfile p){if(p.Version!=2||Array.IndexOf(Loc.Codes,p.Language)<0||(p.Unit!="mm"&&p.Unit!="cm")||double.IsNaN(p.Width)||double.IsNaN(p.Height)||p.Width<10||p.Width>500||p.Height<10||p.Height>500)throw new Exception(Loc.T("Neplatné nastavenia spustenia."));}
 public static void Save(string path,StartupProfile profile){Validate(profile);
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var stream=File.Create(temp))new XmlSerializer(typeof(StartupProfile)).Serialize(stream,profile);if(File.Exists(path))File.Replace(temp,path,null,true);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
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
 void Localize(){bool old=loading;loading=true;try{Loc.Refresh(this);RefreshStudioMenus();UpdateUnitLabels(this);ApplyTheme();Gs1State();Text=Loc.T(dirty?"ND LabelDesigner — prelease • neuložené zmeny":"ND LabelDesigner — prelease");}finally{loading=old;}UpdatePreview();Invalidate(true);}
 void LoadStartup(){Loc.Language="sk";StartupProfile profile;string failure=null;try{profile=StartupStore.Load(startupPath);}catch(Exception ex){profile=new StartupProfile();failure=ex.Message;}
  Loc.Language=profile.Language;loading=true;language.SelectedIndex=Array.IndexOf(Loc.Codes,profile.Language);loading=false;
  try{Apply(StartupLayout.Create(profile));}catch(Exception ex){failure=ex.Message;Apply(LabelSettings.Default());}
  Localize();if(failure!=null)status.Text=Loc.T("Nastavenia spustenia sa nepodarilo načítať. Použité sú predvolené hodnoty. ")+failure;
 }
 void SaveStartup(){try{var profile=new StartupProfile{Language=Loc.Language,Width=N("Width"),Height=N("Height"),Unit=unitFactor==10?"cm":"mm",DarkMode=dark};StartupStore.Save(startupPath,profile);status.Text=Loc.T("Nastavenia spustenia uložené: ")+startupPath;}catch(Exception ex){Error(ex);}}
}
}
