using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing;

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

}
