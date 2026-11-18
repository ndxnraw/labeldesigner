using System;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
 public partial class PrintPanel {
  readonly CheckBox zebraZpl=new CheckBox(),zebraDarknessOverride=new CheckBox();readonly NumericUpDown zebraDpi=new ClickWheelNumericUpDown(),zebraDarkness=new ClickWheelNumericUpDown();
  Action<LabelPrintDocument,PrinterPageGeometry,ZebraSettings,int> zebraSender=(doc,geometry,settings,copies)=>ZebraPrint.Send(doc,geometry,settings,copies);
  ZebraSettings ReadZebra(){var value=new ZebraSettings{Enabled=zebraZpl.Checked,Dpi=(double)zebraDpi.Value,Darkness=zebraZpl.Checked&&zebraDarknessOverride.Checked?(decimal?)zebraDarkness.Value:null};value.Validate();return value;}
  static ZebraSettings ProfileZebra(PrinterProfile profile){var value=new ZebraSettings{Enabled=profile.ZebraZpl,Dpi=profile.ZplDpi,Darkness=profile.Darkness};value.Validate();return value;}
  void ApplyZebra(ZebraSettings value){value.Validate();zebraZpl.Checked=value.Enabled;zebraDpi.Value=(decimal)value.Dpi;zebraDarknessOverride.Checked=value.Darkness.HasValue;if(value.Darkness.HasValue)zebraDarkness.Value=value.Darkness.Value;UpdateZebraEnabled();}
  void UpdateZebraEnabled(){bool editable=!sending&&!busy&&!calibrationTargetMode;zebraZpl.Enabled=editable;zebraDpi.Enabled=editable&&zebraZpl.Checked;zebraDarknessOverride.Enabled=editable&&zebraZpl.Checked;zebraDarkness.Enabled=editable&&zebraZpl.Checked&&zebraDarknessOverride.Checked;}
  void ZebraControls(TableLayoutPanel fields){
   zebraZpl.Text=Loc.T("Priama tlač Zebra (ZPL)");zebraZpl.AutoSize=true;zebraZpl.MaximumSize=new Size(270,0);zebraZpl.AccessibleName=zebraZpl.Text;Add(fields,zebraZpl);
   zebraDpi.Minimum=50;zebraDpi.Maximum=1200;zebraDpi.DecimalPlaces=1;zebraDpi.Increment=.2m;zebraDpi.Value=(decimal)snapshot.Dpi;zebraDpi.AccessibleName=Loc.T("Rozlíšenie ZPL tlačiarne (DPI)");Add(fields,new Label{Text=zebraDpi.AccessibleName,AutoSize=true,MaximumSize=new Size(270,0)});Add(fields,zebraDpi);
   zebraDarknessOverride.Text=Loc.T("Nastaviť tmavosť (darkness)");zebraDarknessOverride.AccessibleName=zebraDarknessOverride.Text;zebraDarknessOverride.AutoSize=true;zebraDarknessOverride.MaximumSize=new Size(270,0);Add(fields,zebraDarknessOverride);
   zebraDarkness.Minimum=0;zebraDarkness.Maximum=30;zebraDarkness.DecimalPlaces=1;zebraDarkness.Increment=.1m;zebraDarkness.Value=10;zebraDarkness.AccessibleName=Loc.T("Tmavosť Zebra (0–30)");Add(fields,zebraDarkness);
   Add(fields,new Label{Text=Loc.T("Použite iba na tlačiarni so ZPL. Rozlíšenie musí zodpovedať tlačiarni. Použijú sa rozmery média a korekcie aplikácie; ostatné vlastnosti ovládača sa nepoužijú. Nastavená tmavosť môže ovplyvniť aj ďalšie úlohy."),AutoSize=true,MaximumSize=new Size(270,0)});
   EventHandler changed=delegate{UpdateZebraEnabled();if(!updating&&!sending)Schedule();};zebraZpl.CheckedChanged+=changed;zebraDarknessOverride.CheckedChanged+=changed;zebraDpi.ValueChanged+=changed;zebraDarkness.ValueChanged+=changed;UpdateZebraEnabled();
  }
  static void ReviewZebra(PreflightReport report,ZebraSettings settings,double documentDpi){settings.Validate();if(settings.Enabled){report.Context+="|"+settings.Context();if(Math.Abs(settings.Dpi-documentDpi)>.05)report.AddWarning(Loc.T("DPI dokumentu a ZPL tlačiarne sa líšia. Pre najlepšiu ostrosť zosúlaďte rozlíšenie kódu s tlačiarňou."));report.AddWarning(Loc.T("Priama tlač ZPL: overte kompatibilitu tlačiarne a zadané DPI. Ostatné nastavenia ovládača sa nepoužijú."));if(settings.Darkness.HasValue)report.AddWarning(Loc.T("Tmavosť ZPL sa nastaví na tlačiarni a môže platiť aj pre ďalšie úlohy.")+" "+settings.Darkness.Value.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));}}
 }
}
