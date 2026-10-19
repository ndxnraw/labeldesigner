using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace EanEtikety {
// Keep the native frame, caption buttons, system menu and DPI handling owned by Windows.
public static class WindowTheme {
 static readonly ConditionalWeakTable<Form,Caption> captions=new ConditionalWeakTable<Form,Caption>();
 static readonly Icon logo=LoadLogo();
 static Icon LoadLogo(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AppLogo.ico")){var result=new Icon(stream);AppDomain.CurrentDomain.ProcessExit+=delegate{result.Dispose();};return result;}}
 public static void Apply(Form form,bool dark){if(form==null||form.IsDisposed)return;Caption caption=captions.GetValue(form,f=>new Caption(f));caption.Dark=dark;form.Icon=logo;form.ShowIcon=true;caption.Refresh();}
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 static int ColorRef(Color color){return color.R|(color.G<<8)|(color.B<<16);}
 sealed class Caption {
  readonly Form form;bool active=true;public bool Dark;
  public Caption(Form value){form=value;form.HandleCreated+=delegate{Refresh();};form.Activated+=delegate{active=true;Refresh();};form.Deactivate+=delegate{active=false;Refresh();};form.SystemColorsChanged+=delegate{Refresh();};}
  public void Refresh(){if(!form.IsHandleCreated||form.IsDisposed)return;
   bool contrast=SystemInformation.HighContrast;int dark=Dark&&!contrast?1:0;
   int background=contrast?-1:ColorRef(StudioTheme.Panel(Dark));
   int foreground=contrast?-1:ColorRef(active?StudioTheme.Text(Dark):Dark?Color.FromArgb(169,183,203):Color.FromArgb(86,99,116));
   try{
    // Older Windows builds can reject individual attributes; leave their native fallback intact.
    if(DwmSetWindowAttribute(form.Handle,20,ref dark,4)<0)DwmSetWindowAttribute(form.Handle,19,ref dark,4);
    DwmSetWindowAttribute(form.Handle,35,ref background,4);
    DwmSetWindowAttribute(form.Handle,36,ref foreground,4);
   }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  }
 }
}
}
