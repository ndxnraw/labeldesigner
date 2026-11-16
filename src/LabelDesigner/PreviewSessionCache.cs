using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Imaging;
namespace EanEtikety {
// Session-local: short-lived driver facts and independently owned artwork copies.
// No lock spans driver access, rendering, preflight or UI delivery.
internal sealed class PreviewSessionCache:IDisposable {
 internal const int MaxImages=4;internal const long MaxImageBytes=32L*1024*1024;internal const double GeometryLifetimeMs=5000;
 sealed class Artwork {public string Key;public Image Image;public long Bytes;}
 sealed class Geometry {public string Key;public PrinterPageGeometry Value;public double At;}
 Func<Image,Image> cloneImage=image=>(Image)image.Clone();readonly object gate=new object();readonly List<Artwork> images=new List<Artwork>();readonly List<Geometry> geometries=new List<Geometry>();object plan;string language;long bytes;bool disposed;
 public int ImageCount{get{lock(gate)return images.Count;}}public long ImageBytes{get{lock(gate)return bytes;}}
 internal void SetPlan(object current,string currentLanguage){lock(gate){if(disposed)return;if(!ReferenceEquals(plan,current)||language!=currentLanguage){ClearImages();plan=current;language=currentLanguage;}}}
 internal Image GetImage(string key){lock(gate){
  if(disposed)return null;int i=images.FindIndex(a=>a.Key==key);if(i<0)return null;var item=images[i];
  try{var copy=cloneImage(item.Image);images.RemoveAt(i);images.Add(item);return copy;}
  catch(Exception error){if(!ResourceFailure(error))throw;RemoveImage(i);return null;}
 }}
 internal void StoreImage(string key,Image source){
  lock(gate){if(disposed)return;}Image copy=null;
  try{
   long size=source is Metafile?((Metafile)source).GetMetafileHeader().MetafileSize:(long)source.Width*source.Height*4;if(size<=0||size>MaxImageBytes)return;
   copy=cloneImage(source);lock(gate){if(disposed)return;int i=images.FindIndex(a=>a.Key==key);if(i>=0)RemoveImage(i);while(images.Count>=MaxImages||bytes+size>MaxImageBytes)RemoveImage(0);images.Add(new Artwork{Key=key,Image=copy,Bytes=size});bytes+=size;copy=null;}
  }catch(Exception error){if(!ResourceFailure(error))throw;/* Reuse is optional; keep the rendered source usable. */}
  finally{if(copy!=null)copy.Dispose();}
 }
 static bool ResourceFailure(Exception error){return error is OutOfMemoryException||error is System.Runtime.InteropServices.ExternalException||error is ArgumentException||error is ObjectDisposedException;}
 internal PrinterPageGeometry GetGeometry(string key,double now){lock(gate){if(disposed)return null;var found=geometries.Find(g=>g.Key==key);return found!=null&&now>=found.At&&now-found.At<GeometryLifetimeMs?found.Value:null;}}
 internal void StoreGeometry(string key,PrinterPageGeometry value,double now){lock(gate){if(disposed)return;geometries.RemoveAll(g=>g.Key==key);if(geometries.Count>=4)geometries.RemoveAt(0);geometries.Add(new Geometry{Key=key,Value=value,At=now});}}
 internal void Clear(){lock(gate){ClearImages();geometries.Clear();}}
 void RemoveImage(int i){var item=images[i];images.RemoveAt(i);bytes-=item.Bytes;item.Image.Dispose();}
 void ClearImages(){while(images.Count>0)RemoveImage(0);}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;ClearImages();geometries.Clear();plan=null;}}
}
}
