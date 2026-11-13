using System.Text;

namespace EanEtikety {
 // Each legacy encoding consumer initializes its own prerequisite. Importing
 // ZPL first must behave exactly like importing a CSV before it.
 internal static class TextEncodings {
  static TextEncodings(){
#if NETCOREAPP
   Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
  }
  public static Encoding Get(int codePage){return Encoding.GetEncoding(codePage);}
 }
}
