using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EanEtikety {
// .NET Framework has no Application.SetDefaultFont. Forms provide the inherited
// font; independent surfaces (ToolStrip and ToolTip) use the same font service.
public static class UiFonts {
 static readonly PrivateFontCollection collection=new PrivateFontCollection();
 static readonly List<IntPtr> memory=new List<IntPtr>(),registrations=new List<IntPtr>();
 static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();static readonly FontFamily family;
 [DllImport("gdi32.dll")]static extern IntPtr AddFontMemResourceEx(IntPtr data,uint size,IntPtr reserved,ref uint count);
 [DllImport("gdi32.dll")]static extern bool RemoveFontMemResourceEx(IntPtr handle);
 static UiFonts(){foreach(string face in new[]{"Regular","Bold"}){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Fonts.SourceSans3-"+face+".ttf")){if(stream==null)throw new InvalidDataException("Missing embedded Source Sans 3 font.");var bytes=new byte[stream.Length];int offset=0;while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}var data=Marshal.AllocCoTaskMem(bytes.Length);memory.Add(data);Marshal.Copy(bytes,0,data,bytes.Length);collection.AddMemoryFont(data,bytes.Length);uint count=0;var handle=AddFontMemResourceEx(data,(uint)bytes.Length,IntPtr.Zero,ref count);if(handle==IntPtr.Zero)throw new InvalidOperationException("Source Sans 3 could not be registered for this process.");registrations.Add(handle);}}
  foreach(var item in collection.Families)if(item.Name=="Source Sans 3"){family=item;break;}if(family==null)throw new InvalidDataException("Source Sans 3 font family is missing.");AppDomain.CurrentDomain.ProcessExit+=delegate{foreach(var f in fonts.Values)f.Dispose();family.Dispose();collection.Dispose();foreach(var h in registrations)RemoveFontMemResourceEx(h);foreach(var p in memory)Marshal.FreeCoTaskMem(p);};
 }
 public static Font Default{get{return Get(10,FontStyle.Regular);}}
 public static Font Get(float size){return Get(size,FontStyle.Regular);}
 public static Font Get(float size,FontStyle style){string key=size.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+(int)style;lock(fonts){Font f;if(!fonts.TryGetValue(key,out f)){f=Create(size,style);fonts.Add(key,f);}return f;}}
 public static Font Create(float size,FontStyle style){return new Font(family,size,style,GraphicsUnit.Point);}
}

}
