using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace EanEtikety {
 public sealed record EditorReference {
  public string Raster;
  public double X,Y,Width,Height;
  public int Opacity=45;
  public bool Locked,Hidden;

  public static EditorReference Import(string path,double labelWidth,double labelHeight){
   if(new FileInfo(path).Length>Assets.MaxBytes)throw new InvalidDataException(Loc.T("Obrázok je príliš veľký"));
   var data=File.ReadAllBytes(path);
   using var stream=new MemoryStream(data);
   using var image=ReadImage(stream);
   double scale=Math.Min(labelWidth/image.Width,labelHeight/image.Height);
   return new EditorReference{Raster=Convert.ToBase64String(data),Width=Math.Clamp(image.Width*scale,.01,500),Height=Math.Clamp(image.Height*scale,.01,500)};
  }

  internal void ValidateSettings(){
   if(!InRange(X,-500,500)||!InRange(Y,-500,500)||!InRange(Width,.01,500)||
      !InRange(Height,.01,500)||Opacity<0||Opacity>100)
    throw new InvalidDataException(Loc.T("Neplatné nastavenie referenčného podkladu."));
   if(string.IsNullOrEmpty(Raster)||Raster.Length>(Assets.MaxBytes+2)/3*4)
    throw new InvalidDataException(Loc.T("Neplatný referenčný obrázok PNG alebo JPEG."));
  }

  internal void ValidateImage(){
   using var stream=new MemoryStream(Convert.FromBase64String(Raster));
   using var image=ReadImage(stream);
  }

  public Bitmap OpenBitmap(){
   ValidateSettings();
   using var stream=new MemoryStream(Convert.FromBase64String(Raster));
   using var image=ReadImage(stream);
   return new Bitmap(image);
  }

  static bool InRange(double value,double minimum,double maximum)=>value>=minimum&&value<=maximum;

  static Image ReadImage(Stream stream){
   if(stream.Length>Assets.MaxBytes)throw new InvalidDataException(Loc.T("Obrázok je príliš veľký"));
   var image=Image.FromStream(stream,true,true);
   try{
    if(image.RawFormat.Guid!=ImageFormat.Png.Guid&&image.RawFormat.Guid!=ImageFormat.Jpeg.Guid)
     throw new InvalidDataException(Loc.T("Neplatný referenčný obrázok PNG alebo JPEG."));
    if((long)image.Width*image.Height>Assets.MaxPixels)throw new InvalidDataException(Loc.T("Obrázok je príliš veľký"));
    return image;
   }catch{image.Dispose();throw;}
  }
 }
}
