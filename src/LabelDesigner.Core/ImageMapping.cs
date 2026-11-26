using System;
using System.Collections.Generic;
using System.IO;

namespace EanEtikety {
 public enum MissingImage { Error, Blank, Fallback }

 public sealed class ImageVariant {
  public string Key, Name;
  public ImageContent Image;
  public string DisplayKey=>Key;
  internal ImageVariant Copy()=>new ImageVariant{Key=Key,Name=Name,Image=Image.Copy()};
 }

 public sealed class ImageMapping {
  public const int MaximumVariants=100, MaximumKeyLength=4000;
  public int Field=-2;
  public string Variable;
  public MissingImage Missing;
  public string FallbackKey;
  public List<ImageVariant> Variants=new List<ImageVariant>();

  public ImageMapping Copy()=>new ImageMapping{
   Field=Field,Variable=Variable,Missing=Missing,FallbackKey=FallbackKey,
   Variants=Variants.ConvertAll(variant=>variant.Copy())
  };

  internal static string Key(string value)=>value?.Trim()??"";

  // Check the whole inventory cheaply. Decode and validate only the image used
  // for a given record, through the same path as an ordinary embedded image.
  internal long Validate(){
   if(Variants==null||Variants.Count<1||Variants.Count>MaximumVariants||
     Variable?.Length>200||!Enum.IsDefined(Missing))
    throw new InvalidDataException(Loc.T("Neplatné mapovanie obrázka."));
   var keys=new HashSet<string>(StringComparer.Ordinal);
   long bytes=0;
   foreach(var variant in Variants){
    if(string.IsNullOrWhiteSpace(variant?.Key)||variant.Key.Length>MaximumKeyLength||
      variant.Name?.Length>260||!keys.Add(Key(variant.Key))||variant.Image?.Data==null)
     throw new InvalidDataException(Loc.T("Neplatné mapovanie obrázka."));
    if(variant.Image.Data.Length>Assets.MaxBytes*4/3+8)
     throw new InvalidDataException(Loc.T("Obrázok je príliš veľký"));
    bytes+=variant.Image.Data.Length;
   }
   if(Missing==MissingImage.Fallback&&!keys.Contains(Key(FallbackKey)))
    throw new InvalidDataException(Loc.T("Chýba predvolený obrázok."));
   return bytes;
  }

  internal ImageContent Select(string value){
   string key=Key(value);
   var variant=Variants.Find(item=>string.Equals(Key(item.Key),key,StringComparison.Ordinal));
   if(variant==null&&Missing==MissingImage.Fallback)
    variant=Variants.Find(item=>string.Equals(Key(item.Key),Key(FallbackKey),StringComparison.Ordinal));
   if(variant==null)return null;
   var image=variant.Image.Copy();
   image.FitFrame=true;
   return image;
  }
 }
}
