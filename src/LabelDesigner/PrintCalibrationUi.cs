using System;using System.Drawing;using System.Drawing.Printing;using System.Windows.Forms;
namespace EanEtikety {
public sealed class PrintCalibrationDialog:Form {
 readonly CalibrationTarget target;readonly Action<PrintCalibration> save;
 readonly NumericUpDown measuredX=new NumericUpDown(),measuredY=new NumericUpDown(),measuredWidth=new NumericUpDown(),measuredHeight=new NumericUpDown();
 readonly CheckBox confirmed=new CheckBox();readonly Label error=new Label();readonly ModernButton apply=new ModernButton();
 public PrintCalibrationDialog(CalibrationTarget target,Action printTarget,Action<PrintCalibration> save){
  if(target==null)throw new ArgumentNullException("target");this.target=target;this.save=save;
  Text=Loc.T("Sprievodca kalibráciou…");Font=UiFonts.Get(10.5f);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);Size=new Size(620,700);MinimumSize=new Size(480,420);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(16)};Controls.Add(scroll);
  var fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));scroll.Controls.Add(fields);
  Add(fields,TextLabel("1. Vytlačte skúšobnú stranu bez korekcií. Použije sa aktuálne nastavenie tlačiarne a média."));
  var print=new ModernButton{Text=Loc.T("Vytlačiť skúšobnú stranu…"),Height=44};print.Click+=delegate{try{if(printTarget!=null)printTarget();}catch(Exception ex){error.Text=ex.Message;}};Add(fields,print);
  Add(fields,TextLabel("2. Zmerajte vzdialenosti medzi stredmi krížikov a polohu ľavého horného krížika od okrajov papiera."));
  Add(fields,new Label{Text="X: "+target.SpanX.ToString("0.##")+" mm / Y: "+target.SpanY.ToString("0.##")+" mm",AutoSize=true});
  Number(fields,measuredWidth,"Nameraná vodorovná vzdialenosť (mm)",.01m,2000,(decimal)target.SpanX);
  Number(fields,measuredHeight,"Nameraná zvislá vzdialenosť (mm)",.01m,2000,(decimal)target.SpanY);
  Number(fields,measuredX,"Krížik od ľavého okraja papiera (mm)",-100,2000,(decimal)target.AnchorX);
  Number(fields,measuredY,"Krížik od horného okraja papiera (mm)",-100,2000,(decimal)target.AnchorY);
  confirmed.Text=Loc.T("Zmeral som nekorigovaný skúšobný výtlačok.");confirmed.AutoSize=true;Add(fields,confirmed);
  Add(fields,TextLabel("Korekcia sa uloží len pre toto médium. Opakované meranie nahradí predchádzajúcu kalibráciu."));
  error.AutoSize=true;error.ForeColor=Color.Firebrick;error.AccessibleName=Loc.T("Výsledok kalibrácie");Add(fields,error);
  apply.Text=Loc.T("Použiť a uložiť profil");apply.Height=44;apply.Enabled=false;apply.Click+=delegate{TryApply();};confirmed.CheckedChanged+=delegate{apply.Enabled=confirmed.Checked;};Add(fields,apply);
  var cancel=new ModernButton{Text=Loc.T("Zrušiť"),Height=44,DialogResult=DialogResult.Cancel};Add(fields,cancel);AcceptButton=apply;CancelButton=cancel;
  fields.SizeChanged+=delegate{foreach(Control c in fields.Controls)if(c is Label||c is CheckBox)c.MaximumSize=new Size(Math.Max(100,fields.ClientSize.Width-8),0);};
 }
 static Label TextLabel(string text){return new Label{Text=Loc.T(text),AutoSize=true,UseMnemonic=false,MaximumSize=new Size(520,0)};}
 static void Add(TableLayoutPanel fields,Control c){c.Dock=DockStyle.Top;c.TabIndex=fields.Controls.Count;c.Margin=new Padding(0,4,0,8);fields.Controls.Add(c,0,fields.RowCount++);fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));}
 static void Number(TableLayoutPanel fields,NumericUpDown n,string text,decimal min,decimal max,decimal value){n.Minimum=min;n.Maximum=max;n.DecimalPlaces=2;n.Increment=.1m;n.Value=value;n.AccessibleName=Loc.T(text);Add(fields,TextLabel(text));Add(fields,n);}
 bool TryApply(){if(!confirmed.Checked)return false;try{var c=PrintCalibration.FromMeasurements(target,(double)measuredX.Value,(double)measuredY.Value,(double)measuredWidth.Value,(double)measuredHeight.Value);if(save!=null)save(c);DialogResult=DialogResult.OK;Close();return true;}catch(Exception ex){error.Text=ex.Message;return false;}}
}
public partial class PrintPanel {
 PrinterProfile CurrentMediaProfile(){return new PrinterProfile{Printer=selectedPrinter.PrinterName,Name=profileName.Text.Trim(),Paper=selectedPage.PaperSize.PaperName,Width=selectedPage.PaperSize.Width,Height=selectedPage.PaperSize.Height,Landscape=selectedPage.Landscape,X=(double)offsetX.Value,Y=(double)offsetY.Value,ScaleX=(double)scaleX.Value/100,ScaleY=(double)scaleY.Value/100,IgnoreDriverMargins=ignoreDriverMargins.Checked};}
 PrintPanel CreateCalibrationPanel(CalibrationTarget target,PrinterProfile baseline){var raw=baseline.Copy();raw.X=raw.Y=0;raw.ScaleX=raw.ScaleY=1;var panel=new PrintPanel(target.Label);panel.initialPrinterName=baseline.Printer;panel.requestedProfile=raw;panel.calibrationTargetMode=true;panel.advancedFields.Enabled=false;panel.selectedPrinter=(PrinterSettings)selectedPrinter.Clone();panel.selectedPage=(PageSettings)selectedPage.Clone();panel.updating=true;panel.ignoreDriverMargins.Checked=baseline.IgnoreDriverMargins;panel.updating=false;panel.printers.Enabled=panel.papers.Enabled=panel.landscape.Enabled=panel.copies.Enabled=panel.mediaProfiles.Enabled=panel.ignoreDriverMargins.Enabled=panel.offsetX.Enabled=panel.offsetY.Enabled=panel.scaleX.Enabled=panel.scaleY.Enabled=panel.driverProperties.Enabled=panel.calibrationLaunch.Enabled=false;return panel;}
 void ShowCalibration(){if(selectedPrinter==null||selectedPage==null||busy||sending)return;try{
  if(profiles==null)throw new InvalidOperationException(Loc.T("Profily médií nie sú dostupné."));
  var baseline=CurrentMediaProfile();var target=CalibrationTarget.Create(outputPlan.Width,outputPlan.Height);
  using(var wizard=new PrintCalibrationDialog(target,delegate{
   using(var form=new Form{Text=Loc.T("Kalibrácia"),Size=new Size(1050,760),MinimumSize=new Size(900,640),Font=Font,AutoScaleMode=AutoScaleMode.Dpi,StartPosition=FormStartPosition.CenterParent}){
    var panel=CreateCalibrationPanel(target,baseline);form.Controls.Add(panel);panel.BackRequested+=delegate{form.Close();};if(calibrationStyle!=null){calibrationStyle(form);panel.Style(calibrationStyle,calibrationDark);}form.ShowDialog(FindForm());
   }
  },c=>ApplyMeasuredCalibration(baseline,c))){if(calibrationStyle!=null)calibrationStyle(wizard);wizard.ShowDialog(FindForm());}
 }catch(Exception ex){info.Text=ex.Message;}}
 void ApplyMeasuredCalibration(PrinterProfile baseline,PrintCalibration correction){
  correction.Validate();var calibrated=baseline.Copy();calibrated.X=correction.X;calibrated.Y=correction.Y;calibrated.ScaleX=correction.ScaleX;calibrated.ScaleY=correction.ScaleY;
  // Persist first: disk failures must leave active corrections unchanged.
  profiles.Save(calibrated);updating=true;try{offsetX.Value=(decimal)correction.X;offsetY.Value=(decimal)correction.Y;scaleX.Value=(decimal)(correction.ScaleX*100);scaleY.Value=(decimal)(correction.ScaleY*100);profileName.Text=calibrated.DisplayName.Trim();RefreshProfiles(calibrated.Printer,profileName.Text);}finally{updating=false;}Schedule();
 }
}
}
