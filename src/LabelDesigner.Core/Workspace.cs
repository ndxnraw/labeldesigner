using System;using System.IO;using System.Xml;using System.Xml.Serialization;using System.Collections.Generic;using System.Diagnostics;
namespace EanEtikety {
public class Utf8StringWriter:StringWriter {public override System.Text.Encoding Encoding{get{return new System.Text.UTF8Encoding(false);}}}
public static class ProjectXml {
 public static string Encode<T>(T value){using(var w=new Utf8StringWriter()){new XmlSerializer(typeof(T)).Serialize(w,value);return w.ToString();}}
 public static T Decode<T>(string text){using(var r=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64000000}))return (T)new XmlSerializer(typeof(T)).Deserialize(r);}
 public static LabelSettings Clone(LabelSettings value){var copy=Decode<LabelSettings>(Encode(value));if(value.Metadata!=null)for(int i=0;i<value.Metadata.Count;i++)if(value.Metadata[i]!=null)copy.Metadata[i].SourceIndex=value.Metadata[i].SourceIndex;return copy;}
 public static void Save<T>(string path,T value){Save(path,value,null);}
 public static void Save<T>(string path,T value,string backup){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{string xml=Encode(value);if(xml.Length>64000000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));File.WriteAllText(temp,xml,new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,backup,true);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
public sealed class EditHistory {
 readonly List<(string Xml,EditorReference Reference)> states=new();
 readonly Dictionary<string,int> rasters=new(ReferenceEqualityComparer.Instance);
 int position=-1;long bytes;
 public bool CanUndo=>position>0;
 public bool CanRedo=>position>=0&&position<states.Count-1;
 public void Reset(LabelSettings s){states.Clear();rasters.Clear();position=-1;bytes=0;Record(s);}
 public bool Record(LabelSettings s){
  // Keep the raster shared across ordinary editor changes.
  var reference=s.EditorReference;
  string xml=ProjectXml.Encode(s.WithoutEditorReference());
  if(position>=0&&states[position].Xml==xml&&states[position].Reference==reference)return false;
  while(states.Count>position+1)Remove(states.Count-1);
  states.Add((xml,reference==null?null:reference with { }));
  position++;bytes+=xml.Length*2L;
  if(reference?.Raster is string raster){
   if(!rasters.TryGetValue(raster,out int count))bytes+=raster.Length*2L;
   rasters[raster]=count+1;
  }
  while(states.Count>2&&(states.Count>100||bytes>128*1024*1024)){Remove(0);position--;}
  return true;
 }
 void Remove(int index){
  var state=states[index];states.RemoveAt(index);bytes-=state.Xml.Length*2L;
  if(state.Reference?.Raster is string raster){
   int remaining=rasters[raster]-1;
   if(remaining==0){rasters.Remove(raster);bytes-=raster.Length*2L;}
   else rasters[raster]=remaining;
  }
 }
 LabelSettings Restore(){
  var state=states[position];
  var label=ProjectXml.Decode<LabelSettings>(state.Xml);
  label.EditorReference=state.Reference==null?null:state.Reference with { };
  return label;
 }
 public LabelSettings Undo(){if(!CanUndo)return null;position--;return Restore();}
 public LabelSettings Redo(){if(!CanRedo)return null;position++;return Restore();}
}
public class WorkspaceOptions {public bool AutoSave=true;public int IntervalSeconds=60;public List<string> Recent=new List<string>();}
public class RecoveryDocument {public string OriginalPath;public DateTime SavedUtc;public LabelSettings Label;}
public sealed class WorkspaceStore:IDisposable {
 public readonly string DirectoryPath;readonly string recoveryPath;readonly FileStream lease;public WorkspaceOptions Options;
 public string TemplatesPath{get{return Path.Combine(DirectoryPath,"Templates");}}
 public string BlocksPath=>Path.Combine(DirectoryPath,"Blocks");
 public WorkspaceStore(string preferences){DirectoryPath=preferences+".workspace";Directory.CreateDirectory(DirectoryPath);Directory.CreateDirectory(TemplatesPath);string id=Guid.NewGuid().ToString("N");recoveryPath=Path.Combine(DirectoryPath,id+".recovery.xml");string p=Path.Combine(DirectoryPath,"options.xml");Options=File.Exists(p)?ProjectXml.Decode<WorkspaceOptions>(File.ReadAllText(p)):new WorkspaceOptions();Options.IntervalSeconds=Math.Max(10,Math.Min(3600,Options.IntervalSeconds));if(Options.Recent==null)Options.Recent=new List<string>();lease=new FileStream(recoveryPath+".lock",FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);}
 public void SaveOptions(){ProjectXml.Save(Path.Combine(DirectoryPath,"options.xml"),Options);}
 public void Remember(string path){path=Path.GetFullPath(path);Options.Recent.RemoveAll(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));Options.Recent.Insert(0,path);if(Options.Recent.Count>15)Options.Recent.RemoveRange(15,Options.Recent.Count-15);SaveOptions();}
 public void Recoverable(Action<string,RecoveryDocument> found,Action<string> invalid){foreach(string p in Directory.GetFiles(DirectoryPath,"*.recovery.xml")){if(p==recoveryPath)continue;try{using(var l=new FileStream(p+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){RecoveryDocument d;try{d=ProjectXml.Decode<RecoveryDocument>(File.ReadAllText(p));Engine.ValidateDraft(d.Label);d.Label.EditorReference?.ValidateImage();}catch(Exception ex){invalid(Path.GetFileName(p)+": "+ex.Message);continue;}found(p,d);}}catch(IOException){/* A running instance owns the recovery; do not touch it. */}}}
 public void SaveRecovery(LabelSettings s,string original){Engine.ValidateDraft(s);ProjectXml.Save(recoveryPath,new RecoveryDocument{OriginalPath=original,SavedUtc=DateTime.UtcNow,Label=s});}
 public void Clean(){if(File.Exists(recoveryPath))File.Delete(recoveryPath);}
 public void Dispose(){lease.Dispose();try{File.Delete(recoveryPath+".lock");}catch(IOException){}}
}
public class ElementMeta {
 public int Index;public bool Locked;public string Group="";public int Layer;public VisibilityRule Visibility,GroupVisibility;
 public List<LanguageTranslation> Translations;
 public LabelDataFormat DataFormat;
 public bool ShouldSerializeTranslations()=>Translations?.Count>0;
 [XmlIgnore]public int? SourceIndex;
}
public class SheetLayout {public bool Enabled;public int Columns=3,Rows=5,FirstSlot,Count=1;public double Left=10,Top=10,Right=10,Bottom=10,HorizontalGap=2,VerticalGap=2;}
public class VariableField {public string Name="serial",Type="Number",Prefix="",Suffix="",Format="yyyy-MM-dd",FixedValue="";public long Start=1,Step=1;public int Digits=6,Days,Months;public bool FixedDate;public DateTime Date=DateTime.Today;}
public class ImageContent {public bool KeepAspect=true,FitFrame;public bool ShouldSerializeFitFrame()=>FitFrame;public ImageContent Copy()=>(ImageContent)MemberwiseClone();public string Mime,Data;public double CropX,CropY,CropWidth=1,CropHeight=1;public int PixelWidth,PixelHeight;}
public class MatrixContent {public string Type="QR",Data="https://example.com";}
public static class Elements {
 public static List<int> Indices(LabelSettings s){var a=new List<int>();for(int i=0;i<s.Texts.Count;i++)a.Add(i);if(!s.HideBarcode)a.Add(-1);for(int i=0;i<s.Symbols.Count;i++)a.Add(-3-i);return a;}
 public static ElementMeta Meta(LabelSettings s,int index){if(s.Metadata==null)s.Metadata=new List<ElementMeta>();var m=s.Metadata.Find(x=>x.Index==index);if(m==null){m=new ElementMeta{Index=index,Layer=index==-1?50:index>=0?index:100-index};s.Metadata.Add(m);}return m;}
 public static int Layer(LabelSettings s,int index){var m=s.Metadata.Find(x=>x.Index==index);return m==null?(index==-1?50:index>=0?index:100-index):m.Layer;}
 public static int SourceIndex(LabelSettings s,int index)=>s.Metadata.Find(m=>m.Index==index)?.SourceIndex??index;
 internal static string FieldTarget(int index)=>index==-1?"code":index>=0?"text:"+index:"symbol:"+(-3-index);
 internal static bool TryFieldIndex(string target,out int index){
  index=-2;
  if(target=="code"){index=-1;return true;}
  if(target==null)return false;
  if(target.StartsWith("text:",StringComparison.Ordinal)&&int.TryParse(target.AsSpan(5),out int text)&&text>=0){index=text;return true;}
  if(target.StartsWith("symbol:",StringComparison.Ordinal)&&int.TryParse(target.AsSpan(7),out int symbol)&&symbol>=0){index=-3-symbol;return true;}
  return false;
 }
 static void RemapVisibility(VisibilityRule rule,Dictionary<int,int> mapping){
  if(rule!=null&&string.IsNullOrEmpty(rule.Variable))rule.Field=mapping.TryGetValue(rule.Field,out int mapped)?mapped:-2;
 }
 public static bool Locked(LabelSettings s,int index){var m=s.Metadata.Find(x=>x.Index==index);return m!=null&&m.Locked;}
 public static void Set(LabelSettings s,ElementEdit e){if(e.Index==-1){s.BarX=e.X;s.BarY=e.Y;s.BarWidth=e.Width;s.BarRotation=e.Rotation;s.DigitSize=e.Font;s.BarHeight=Math.Max(1,e.Height-Engine.ExtraHeight(s,e.Width,e.Font));}else if(e.Index>=0){var t=s.Texts[e.Index];if(Math.Abs(t.Width-e.Width)>.001||Math.Abs(t.Height-e.Height)>.001)t.AutoWidth=false;t.X=e.X;t.Y=e.Y;t.Width=e.Width;t.Height=e.Height;t.Rotation=e.Rotation;t.Size=e.Font;}else{var t=s.Symbols[-3-e.Index];t.X=e.X;t.Y=e.Y;t.Width=e.Width;t.Height=e.Height;t.Rotation=e.Rotation;}}
 static List<ColumnBinding> RemapBindings(List<ColumnBinding> bindings,Dictionary<int,int> mapping){
  var result=new List<ColumnBinding>();
  foreach(var binding in bindings){
   if(binding.Target=="quantity"||binding.Target=="unique"||binding.Target==LabelLanguages.BatchTarget){result.Add(binding);continue;}
   int key;
   if(binding.Target=="code")key=-1;
   else if(binding.Target.StartsWith("text:",StringComparison.Ordinal))key=int.Parse(binding.Target.Substring(5));
   else if(binding.Target.StartsWith("symbol:",StringComparison.Ordinal))key=-3-int.Parse(binding.Target.Substring(7));
   else continue;
   int next;
   if(!mapping.TryGetValue(key,out next))continue;
   binding.Target=next==-1?"code":next>=0?"text:"+next:"symbol:"+(-3-next);
   result.Add(binding);
  }
  return result;
 }
 public static void Remove(LabelSettings s,List<int> selection){var old=new List<ElementMeta>();foreach(var m in s.Metadata)old.Add(m);var indices=Indices(s);var mapping=new Dictionary<int,int>();int text=0,symbol=0;foreach(int i in indices){if(selection.Contains(i)){if(i==-1)s.HideBarcode=true;continue;}mapping[i]=i>=0?text++:i<=-3?-3-symbol++:-1;}if(s.Batch!=null&&s.Batch.Bindings!=null){s.Batch.Bindings=RemapBindings(s.Batch.Bindings,mapping);foreach(var preset in s.Batch.Presets)preset.Bindings=RemapBindings(preset.Bindings,mapping);}s.QuickPrintFields=(s.QuickPrintFields??new List<int>()).FindAll(mapping.ContainsKey).ConvertAll(index=>mapping[index]);foreach(var scenario in s.Scenarios){scenario.Values.RemoveAll(value=>!mapping.ContainsKey(value.Element));foreach(var value in scenario.Values)value.Element=mapping[value.Element];}s.Metadata.Clear();foreach(var m in old)if(mapping.ContainsKey(m.Index)){m.Index=mapping[m.Index];RemapVisibility(m.Visibility,mapping);RemapVisibility(m.GroupVisibility,mapping);s.Metadata.Add(m);}for(int i=s.Texts.Count-1;i>=0;i--)if(selection.Contains(i))s.Texts.RemoveAt(i);for(int i=s.Symbols.Count-1;i>=0;i--){if(selection.Contains(-3-i)){s.Symbols.RemoveAt(i);continue;}var image=s.Symbols[i].ImageMapping;if(image!=null&&string.IsNullOrEmpty(image.Variable))image.Field=mapping.TryGetValue(image.Field,out int next)?next:-2;}}
}
}
