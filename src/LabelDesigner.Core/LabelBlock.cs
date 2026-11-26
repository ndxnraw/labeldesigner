using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EanEtikety {
 public sealed class LabelBlock {
  public int Format=1;
  public string Name;
  public double Width,Height;
  public LabelSettings Artwork;

  public static LabelBlock Capture(LabelSettings source,IEnumerable<int> selection,string name){
   var selected=new HashSet<int>(selection);
   var indices=Elements.Indices(source).FindAll(selected.Contains);
   if(indices.Count==0||indices.Count!=selected.Count)
    throw new InvalidDataException(Loc.T("Vyberte prvky na uloženie do bloku."));
   var copy=ProjectXml.Clone(source);
   var bounds=indices.Select(index=>ElementTransform.Bounds(ElementTransform.Get(copy,index))).Aggregate(RectangleF.Union);
   var block=new LabelBlock{Name=name?.Trim(),Width=bounds.Width,Height=bounds.Height,
    Artwork=new LabelSettings{HideBarcode=true,Width=Math.Max(10,bounds.Width),Height=Math.Max(10,bounds.Height),Dpi=source.Dpi}};
   Append(copy,indices,block.Artwork,-bounds.Left,-bounds.Top);
   var used=VariableNames(block.Artwork);
   block.Artwork.Variables=copy.Variables.FindAll(variable=>used.Contains(variable.Name));
   foreach(string variable in used)
    if(!block.Artwork.Variables.Exists(item=>item.Name==variable))
     throw new InvalidDataException(Loc.T("Neznáma premenná")+": "+variable);
   if(block.Artwork.Metadata.Exists(meta=>meta.Translations?.Count>0))block.Artwork.Languages=copy.Languages;
   block.Validate();
   return block;
  }

  public void Save(string path){Validate();ProjectXml.Save(path,this);}

  public static LabelBlock Load(string path){
   if(new FileInfo(path).Length>64_000_000)throw new InvalidDataException(Loc.T("Príliš veľa údajov"));
   var block=ProjectXml.Decode<LabelBlock>(File.ReadAllText(path));
   block.Validate();
   return block;
  }

  public List<int> Insert(LabelSettings destination){
   Validate();
   var copy=ProjectXml.Clone(Artwork);
   var draft=ProjectXml.Clone(destination);
   if(copy.Languages!=null){
    if(draft.Languages==null)draft.Languages=copy.Languages.Copy();
    else foreach(string code in copy.Languages.Codes)
     if(!draft.Languages.Codes.Exists(item=>LabelLanguages.SameCode(item,code)))draft.Languages.Codes.Add(code);
   }
   draft.Variables.AddRange(copy.Variables.FindAll(variable=>!draft.Variables.Exists(item=>item.Name==variable.Name)));
   var added=Append(copy,Elements.Indices(copy),draft,Math.Max(0,(draft.Width-Width)/2),Math.Max(0,(draft.Height-Height)/2));
   Engine.ValidateDraft(draft);
   destination.Texts=draft.Texts;
   destination.Symbols=draft.Symbols;
   destination.Metadata=draft.Metadata;
   destination.Languages=draft.Languages;
   destination.Variables=draft.Variables;
   return added;
  }

  void Validate(){
   if(Format!=1||string.IsNullOrWhiteSpace(Name)||Name.Length>80||Artwork==null||!Artwork.HideBarcode||
      !double.IsFinite(Width)||!double.IsFinite(Height)||Width<=0||Height<=0||Width>500||Height>500)
    throw new InvalidDataException(Loc.T("Neplatný blok etikety."));
   Engine.ValidateDraft(Artwork);
   var indices=new HashSet<int>(Elements.Indices(Artwork));
   if(indices.Count==0)throw new InvalidDataException(Loc.T("Neplatný blok etikety."));
   foreach(var meta in Artwork.Metadata){
    CheckDependency(meta.Visibility?.Field??-2,meta.Visibility?.Variable,meta.Visibility!=null,indices);
    CheckDependency(meta.GroupVisibility?.Field??-2,meta.GroupVisibility?.Variable,meta.GroupVisibility!=null,indices);
   }
   foreach(var symbol in Artwork.Symbols)
    if(symbol.ImageMapping!=null)CheckDependency(symbol.ImageMapping.Field,symbol.ImageMapping.Variable,true,indices);
  }

  static void CheckDependency(int field,string variable,bool exists,ICollection<int> indices){
   if(exists&&string.IsNullOrEmpty(variable)&&!indices.Contains(field))
    throw new InvalidDataException(Loc.T("Do výberu pridajte aj zdroj údajov pre pravidlo alebo obrázok."));
  }

  static List<int> Append(LabelSettings source,List<int> indices,LabelSettings target,double x,double y){
   var map=new Dictionary<int,int>();
   var groups=new Dictionary<string,string>(StringComparer.Ordinal);
   var groupRules=new Dictionary<string,VisibilityRule>(StringComparer.Ordinal);
   foreach(var meta in source.Metadata)
    if(!string.IsNullOrEmpty(meta.Group)&&meta.GroupVisibility!=null)groupRules.TryAdd(meta.Group,meta.GroupVisibility);
   int layer=Elements.Indices(target).Select(index=>Elements.Layer(target,index)).DefaultIfEmpty(-1).Max();
   foreach(int index in indices.OrderBy(index=>Elements.Layer(source,index))){
    int next;
    if(index>=0){
     var text=source.Texts[index];
     text.X+=x;text.Y+=y;
     next=target.Texts.Count;target.Texts.Add(text);
    }else{
     var symbol=index==-1?new SymbolItem{X=source.BarX,Y=source.BarY,Width=source.BarWidth,
      Height=ElementTransform.Get(source,-1).Height,Rotation=source.BarRotation,Code=BarcodeContent.From(source)}:source.Symbols[-3-index];
     symbol.X+=x;symbol.Y+=y;
     next=-3-target.Symbols.Count;target.Symbols.Add(symbol);
    }
    map.Add(index,next);
    Elements.Meta(target,next).Layer=checked(++layer);
   }
   VisibilityRule Remap(VisibilityRule rule){
    if(rule==null)return null;
    CheckDependency(rule.Field,rule.Variable,true,map.Keys);
    var copy=rule.Copy();
    if(string.IsNullOrEmpty(copy.Variable))copy.Field=map[copy.Field];
    return copy;
   }
   foreach(int index in indices){
    int next=map[index];
    var original=source.Metadata.Find(meta=>meta.Index==index);
    var meta=Elements.Meta(target,next);
    if(original!=null){
     meta.Locked=original.Locked;
     meta.Translations=original.Translations;
     meta.Visibility=Remap(original.Visibility);
     if(!string.IsNullOrEmpty(original.Group)){
      if(!groups.TryGetValue(original.Group,out string group))groups[original.Group]=group=Guid.NewGuid().ToString("N");
      meta.Group=group;
      if(groupRules.TryGetValue(original.Group,out var rule))meta.GroupVisibility=Remap(rule);
     }
    }
    if(next<=-3&&target.Symbols[-3-next].ImageMapping is { } image&&string.IsNullOrEmpty(image.Variable)){
     CheckDependency(image.Field,image.Variable,true,map.Keys);
     image.Field=map[image.Field];
    }
   }
   return indices.ConvertAll(index=>map[index]);
  }

  static HashSet<string> VariableNames(LabelSettings artwork){
   var names=new HashSet<string>(StringComparer.Ordinal);
   void AddName(string name){if(!string.IsNullOrEmpty(name))names.Add(name);}
   void Text(string value){foreach(Match match in StyledText.Variable.Matches(value??""))AddName(match.Groups[1].Value);}
   foreach(var item in artwork.Texts)Text(item.Text);
   foreach(var meta in artwork.Metadata){
    AddName(meta.Visibility?.Variable);
    AddName(meta.GroupVisibility?.Variable);
    if(meta.Translations!=null)foreach(var translation in meta.Translations)Text(translation.Text);
   }
   foreach(var symbol in artwork.Symbols){
    AddName(symbol.ImageMapping?.Variable);
    Text(symbol.Matrix?.Data);
    if(symbol.Code!=null){
     Text(symbol.Code.Ean);
     foreach(var identifier in symbol.Code.ApplicationIdentifiers)Text(identifier.Data);
    }
   }
   return names;
  }
 }
}
