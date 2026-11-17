using System;using System.IO;using System.Text;using System.Drawing;using System.Drawing.Imaging;using System.Globalization;using System.ComponentModel;using System.Runtime.InteropServices;
namespace EanEtikety {
 public sealed class ZebraSettings {
  public bool Enabled;public double Dpi=300;public decimal? Darkness;
  public ZebraSettings Copy(){return (ZebraSettings)MemberwiseClone();}
  public void Validate(){if(double.IsNaN(Dpi)||double.IsInfinity(Dpi)||Dpi<50||Dpi>1200||Darkness.HasValue&&(!Enabled||Darkness.Value<0||Darkness.Value>30||decimal.Round(Darkness.Value,1)!=Darkness.Value))throw new ArgumentException(Loc.T("Neplatné nastavenie tlače Zebra ZPL."));}
  public string Context(){Validate();return Enabled?"ZPL|"+Dpi.ToString("R",CultureInfo.InvariantCulture)+"|"+(Darkness.HasValue?Darkness.Value.ToString("0.0",CultureInfo.InvariantCulture):"printer"):"Windows";}
 }
 public interface IRawPrintJob:IDisposable {int Write(byte[] bytes,int offset,int count);void Complete();}
 public interface IRawPrinter {IRawPrintJob Open(string printer,string title);}
 public static class ZebraPrint {
  const long Limit=256L*1024*1024;
  sealed class BoundedOutput:Stream {
   readonly Stream output;long written;public BoundedOutput(Stream value){output=value;}
   public override void Write(byte[] b,int o,int n){if(n>Limit-written)throw new IOException(Loc.T("Tlačová úloha ZPL prekročila limit 256 MB."));output.Write(b,o,n);written+=n;}
   public override void Flush(){output.Flush();}public override bool CanRead{get{return false;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return true;}}public override long Length{get{return written;}}public override long Position{get{return written;}set{throw new NotSupportedException();}}
   public override int Read(byte[] b,int o,int n){throw new NotSupportedException();}public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}public override void SetLength(long n){throw new NotSupportedException();}
  }
  public static void Render(LabelPrintDocument document,PrinterPageGeometry geometry,ZebraSettings settings,int repetitions,Stream output){
   if(document==null||geometry==null||settings==null||output==null)throw new ArgumentNullException();settings.Validate();if(!settings.Enabled||repetitions<1||repetitions>32767||(long)document.PageCount*repetitions>1000000||geometry.Bounds.Width<=0||geometry.Bounds.Height<=0)throw new ArgumentException(Loc.T("Neplatné nastavenie tlače Zebra ZPL."));
   double w=geometry.Bounds.Width*settings.Dpi/100,h=geometry.Bounds.Height*settings.Dpi/100;if(w>24000||h>24000||w*h>40000000)throw new InvalidOperationException(Loc.T("Strana pre tlač Zebra ZPL je príliš veľká."));int width=Math.Max(1,(int)Math.Round(w)),height=Math.Max(1,(int)Math.Round(h));
   using(var limited=new BoundedOutput(output))using(var writer=new StreamWriter(limited,Encoding.ASCII,32768,true)){
    if(settings.Darkness.HasValue)writer.Write("~SD"+settings.Darkness.Value.ToString("0.0",CultureInfo.InvariantCulture)+"\n");
    for(int copy=0;copy<repetitions;copy++)for(int page=0;page<document.PageCount;page++){
     WorkContext.Check();using(var image=new Bitmap(width,height,PixelFormat.Format24bppRgb)){image.SetResolution((float)settings.Dpi,(float)settings.Dpi);using(var g=Graphics.FromImage(image)){g.Clear(Color.White);document.RenderPrinterPage(g,page,geometry.PrintableArea,0,0,false);}var data=Engine.Packed(image,true);int stride=(width+7)/8,rows=Math.Max(1,60000/stride);
      writer.Write("^XA\n^MUD,300,300\n^PW"+width+"\n^LL"+height+"\n^LH0,0\n^LS0\n^LT0\n^PON\n^PMN\n^LRN\n");if(settings.Darkness.HasValue)writer.Write("^MD0\n");
      for(int y=0;y<height;y+=rows){WorkContext.Check();int count=Math.Min(rows,height-y)*stride;writer.Write("^FO0,"+y+"^GFA,"+count+","+count+","+stride+",");var hex=new char[count*2];const string digits="0123456789ABCDEF";for(int i=0;i<count;i++){byte b=data[y*stride+i];hex[i*2]=digits[b>>4];hex[i*2+1]=digits[b&15];}writer.Write(hex);writer.Write("^FS\n");}
      writer.Write("^PQ1,0,1,N\n^XZ\n");
     }
    }
   }
  }
  public static void SendFile(string printer,string path,IRawPrinter transport){SendFile(printer,path,transport,1);}
  public static void SendFile(string printer,string path,IRawPrinter transport,int repetitions){if(transport==null)throw new ArgumentNullException("transport");using(var input=File.OpenRead(path)){if(repetitions<1||repetitions>32767||input.Length==0||input.Length*repetitions>Limit)throw new InvalidDataException(Loc.T("Tlačová úloha ZPL prekročila limit 256 MB."));WorkContext.Check();using(var job=transport.Open(printer,"ND LabelDesigner — Pre-release")){var buffer=new byte[65536];for(int copy=0;copy<repetitions;copy++){input.Position=0;int count;while((count=input.Read(buffer,0,buffer.Length))>0){int offset=0;while(offset<count){WorkContext.Check();int written=job.Write(buffer,offset,count-offset);if(written<=0||written>count-offset)throw new IOException(Loc.T("Tlačiareň neprijala všetky dáta úlohy."));offset+=written;}}}WorkContext.Check();job.Complete();}}}
  public static void Send(LabelPrintDocument document,PrinterPageGeometry geometry,ZebraSettings settings,int repetitions,IRawPrinter transport=null){if(document==null||repetitions<1||repetitions>32767||(long)document.PageCount*repetitions>1000000)throw new ArgumentException("repetitions");string path=Path.Combine(Path.GetTempPath(),"NDLabelDesigner-ZPL-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))Render(document,geometry,settings,1,output);SendFile(document.PrinterSettings.PrinterName,path,transport??new WindowsRawPrinter(),repetitions);}finally{try{if(File.Exists(path))File.Delete(path);}catch(IOException e){DiagnosticLog.Write("zebra-temp-cleanup",e);}catch(UnauthorizedAccessException e){DiagnosticLog.Write("zebra-temp-cleanup",e);}}}
 }
 public sealed class WindowsRawPrinter:IRawPrinter {
  public IRawPrintJob Open(string printer,string title){return new Job(printer,title);}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct DocInfo{[MarshalAs(UnmanagedType.LPWStr)]public string Name;public IntPtr Output;[MarshalAs(UnmanagedType.LPWStr)]public string Type;}
  [DllImport("winspool.drv",EntryPoint="OpenPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool OpenPrinter(string name,out IntPtr handle,IntPtr defaults);
  [DllImport("winspool.drv",EntryPoint="StartDocPrinterW",CharSet=CharSet.Unicode,SetLastError=true)]static extern int StartDocPrinter(IntPtr handle,int level,ref DocInfo info);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool StartPagePrinter(IntPtr handle);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool WritePrinter(IntPtr handle,IntPtr bytes,int count,out int written);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool EndPagePrinter(IntPtr handle);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool EndDocPrinter(IntPtr handle);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool AbortPrinter(IntPtr handle);
  [DllImport("winspool.drv",SetLastError=true)]static extern bool ClosePrinter(IntPtr handle);
  sealed class Job:IRawPrintJob {
   IntPtr handle;bool started,completed;
   static void Required(bool value){if(!value)throw new Win32Exception(Marshal.GetLastWin32Error());}
   public Job(string printer,string title){try{Required(OpenPrinter(printer,out handle,IntPtr.Zero));var info=new DocInfo{Name=title,Type="RAW"};Required(StartDocPrinter(handle,1,ref info)!=0);started=true;Required(StartPagePrinter(handle));}catch{Dispose();throw;}}
   public int Write(byte[] bytes,int offset,int count){if(handle==IntPtr.Zero||completed)throw new ObjectDisposedException("print job");if(bytes==null||offset<0||count<0||offset>bytes.Length-count)throw new ArgumentOutOfRangeException();var pinned=GCHandle.Alloc(bytes,GCHandleType.Pinned);try{int written;Required(WritePrinter(handle,IntPtr.Add(pinned.AddrOfPinnedObject(),offset),count,out written));return written;}finally{pinned.Free();}}
   public void Complete(){if(handle==IntPtr.Zero||completed)throw new ObjectDisposedException("print job");Required(EndPagePrinter(handle));Required(EndDocPrinter(handle));completed=true;}
   public void Dispose(){if(handle==IntPtr.Zero)return;if(started&&!completed)AbortPrinter(handle);ClosePrinter(handle);handle=IntPtr.Zero;}
  }
 }
}
