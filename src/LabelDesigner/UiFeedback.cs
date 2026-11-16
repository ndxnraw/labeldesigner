using System;using System.IO;using System.Xml;
namespace EanEtikety {
 public static class UiFailure {
  public static string Describe(Exception error,string diagnosticId){
   if(error==null)throw new ArgumentNullException("error");
   var cause=error is BackgroundFailure&&error.InnerException!=null?error.InnerException:error;
   string summary,action;
   if(cause is UnauthorizedAccessException){summary="Nemáte oprávnenie na prístup k súboru alebo priečinku.";action="Vyberte priečinok, do ktorého môžete zapisovať, alebo skontrolujte prístupové práva.";}
   else if(cause is FormatException||cause is ArgumentException||cause is InvalidDataException||cause is XmlException){summary="Údaje sa nepodarilo spracovať.";action="Skontrolujte zadané hodnoty a formát súboru. Rozpracovaný projekt môžete ďalej upravovať.";}
   else if(cause is IOException){summary="Súbor alebo zariadenie nie je dostupné.";action="Skontrolujte umiestnenie, pripojenie a či súbor nepoužíva iný program. Potom skúste operáciu znova.";}
   else{summary="Operáciu sa nepodarilo dokončiť.";action="Skúste operáciu zopakovať. Ak problém pretrváva, použite podrobnosti a diagnostický záznam.";}
   return Loc.T(summary)+"\r\n"+Loc.T(action)+"\r\n\r\n"+Loc.T("Podrobnosti")+":\r\n"+error.Message+(string.IsNullOrEmpty(diagnosticId)?"":"\r\n\r\n"+Loc.T("Diagnostický záznam")+": "+diagnosticId);
  }
 }
}
