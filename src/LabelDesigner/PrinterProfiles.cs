using System;using System.IO;using System.Collections.Generic;using System.Drawing.Printing;using System.Windows.Forms;
namespace EanEtikety {
public class PrinterProfile {public string Printer,Paper;public int Width,Height;public bool Landscape;public double X,Y;}
public class PrinterProfileStore {
 readonly string path;public List<PrinterProfile> Profiles=new List<PrinterProfile>();
 public PrinterProfileStore(string file){path=file;if(File.Exists(path))Profiles=ProjectXml.Decode<List<PrinterProfile>>(File.ReadAllText(path));}
 public PrinterProfile Find(string name){return Profiles.Find(p=>string.Equals(p.Printer,name,StringComparison.OrdinalIgnoreCase));}
 public void Save(PrinterProfile value){Profiles.RemoveAll(p=>string.Equals(p.Printer,value.Printer,StringComparison.OrdinalIgnoreCase));Profiles.Add(value);ProjectXml.Save(path,Profiles);}
}
public partial class PrintPanel {
 PrinterProfileStore profiles;readonly NumericUpDown offsetX=new NumericUpDown(),offsetY=new NumericUpDown();
 public void LoadProfiles(string path){try{profiles=new PrinterProfileStore(path);}catch(Exception ex){info.Text=Loc.T("Profil tlačiarne sa nepodarilo načítať")+": "+ex.Message;}}
 void ProfileControls(TableLayoutPanel extra){foreach(var number in new[]{offsetX,offsetY}){number.Minimum=-100;number.Maximum=100;number.DecimalPlaces=2;number.Increment=.1m;number.ValueChanged+=delegate{if(!updating)Schedule();};}Add(extra,new Label{Text=Loc.T("Korekcia X (mm)"),AutoSize=true});Add(extra,offsetX);Add(extra,new Label{Text=Loc.T("Korekcia Y (mm)"),AutoSize=true});Add(extra,offsetY);
  Add(extra,Button(Loc.T("Uložiť profil tlačiarne"),delegate{try{if(profiles==null||selectedPrinter==null||selectedPage==null)throw new InvalidOperationException(Loc.T("Vybraná tlačiareň nie je dostupná."));profiles.Save(new PrinterProfile{Printer=selectedPrinter.PrinterName,Paper=selectedPage.PaperSize.PaperName,Width=selectedPage.PaperSize.Width,Height=selectedPage.PaperSize.Height,Landscape=selectedPage.Landscape,X=(double)offsetX.Value,Y=(double)offsetY.Value});info.Text=Loc.T("Profil tlačiarne uložený");}catch(Exception ex){info.Text=ex.Message;}}));
  Add(extra,new Label{Text=Loc.T("Ak je výtlačok posunutý doprava alebo nadol, zadajte nameraný posun so záporným znamienkom. Rozmery zostanú nezmenené."),AutoSize=true,MaximumSize=new System.Drawing.Size(270,0)});
  Add(extra,Button(Loc.T("Kalibračná etiketa"),delegate{var label=LabelSettings.Default();label.Width=60;label.Height=40;label.HideBarcode=true;label.Texts.Clear();string svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 60 40'><path d='M2 5 L8 5 M5 2 L5 8 M52 35 L58 35 M55 32 L55 38 M10 30 L30 30 M10 28 L10 32 M30 28 L30 32' fill='none' stroke='black' stroke-width='0.2'/></svg>";label.Symbols.Add(new SymbolItem{X=0,Y=0,Width=60,Height=40,Image=new ImageContent{Mime="image/svg+xml",Data=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg)),PixelWidth=60,PixelHeight=40}});label.Texts.Add(new TextItem{Text="60 × 40 mm\nX=5 / Y=5 mm",X=15,Y=10,Width=40,Height=15,Size=10});using(var form=new Form{Text=Loc.T("Kalibrácia"),Size=new System.Drawing.Size(1050,760),Font=Font,StartPosition=FormStartPosition.CenterParent}){var panel=new PrintPanel(label);panel.profiles=profiles;panel.initialPrinterName=selectedPrinter==null?null:selectedPrinter.PrinterName;form.Controls.Add(panel);panel.BackRequested+=delegate{form.Close();};if(calibrationStyle!=null){calibrationStyle(form);panel.Style(calibrationStyle,calibrationDark);}form.ShowDialog(FindForm());}}));
 }
 Action<Control> calibrationStyle;bool calibrationDark;
}
}
