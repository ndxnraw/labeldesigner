using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public sealed class UpdatesDialog:Form {
  readonly UpdateActivation service;
  readonly string running;
  readonly Action<Control> style;
  readonly TableLayoutPanel layout;
  readonly FlowLayoutPanel footer;
  readonly Panel updatePage;
  readonly Label information;
  readonly ModernButton primary, onlineCheck, restartPrepared, rollback;
  readonly string installedId;
  UpdateOffer offer;
  string prepared,preparedId;
  Func<UpdateNetwork> networkFactory=()=>new UpdateNetwork();
  Func<string,bool> confirm;

  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  public string RequestedDirectory{get;private set;}
  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  public bool RollbackRequested{get;private set;}

  public UpdatesDialog(UpdateActivation service,string runningDirectory,Action<Control> style) {
   this.service=service;
   running=runningDirectory;
   this.style=style??delegate{};
   Text=Loc.T("Aktualizácie");
   Font=UiFonts.Get(10.5f);
   Size=new Size(700,570);
   MinimumSize=new Size(500,385);
   StartPosition=FormStartPosition.CenterParent;
   AutoScaleMode=AutoScaleMode.Dpi;
   ShowInTaskbar=false;
   MinimizeBox=false;
   MaximizeBox=false;

   layout=Rows();
   layout.Padding=new Padding(12,8,12,8);
   Controls.Add(layout);
   updatePage=new Panel{Name="updatePage",Dock=DockStyle.Fill,Margin=Padding.Empty};
   AddRow(layout,updatePage,SizeType.Percent);
   footer=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.RightToLeft};
   AddRow(layout,footer);
   footer.Margin=new Padding(0,4,0,0);
   var close=Action("Zavrieť",()=>DialogResult=DialogResult.Cancel);
   close.MinimumSize=new Size(110,38);
   close.DialogResult=DialogResult.Cancel;
   close.Margin=Padding.Empty;
   close.TabIndex=1;
   footer.Controls.Add(close);
   CancelButton=close;

   var rows=ScrollRows(updatePage);
   var installed=Description("Vývojová alebo staršia inštalácia bez identifikácie balíka.");
   try{
    var package=UpdatePackage.Read(Path.Combine(running,"package-manifest.xml"));
    installedId=package.PackageId;
    installed.Text=Loc.T("Nainštalovaná zostava")+": "+installedId.Substring(0,12)+" / "+package.Family;
   }catch{ /* Developer builds need no installed manifest. */ }
   AddRow(rows,installed);
   information=Description("Kontrola aktualizácií sa spustí na vyžiadanie.");
   information.LiveSetting=System.Windows.Forms.Automation.AutomationLiveSetting.Polite;
   AddRow(rows,information);

   primary=Action("Skontrolovať aktualizácie",RunPrimaryAction);
   primary.Name="updatePrimaryAction";
   AddRow(rows,primary);
   onlineCheck=Action("Skontrolovať aktualizácie",CheckOnline);
   AddRow(rows,onlineCheck);
   restartPrepared=Action("Spustiť pripravenú verziu…",RequestActivation);
   AddRow(rows,restartPrepared);
   AddRow(rows,Description("Ručne stiahnutý ZIP môžete overiť a pripraviť aj bez internetu."));
   AddRow(rows,Action("Použiť ručne stiahnutý ZIP…",Import));
   rollback=Action("Návrat na predchádzajúcu verziu…",RequestRollback);
   AddRow(rows,rollback);
   AddRow(rows,Action("Uvoľniť miesto po aktualizáciách…",Cleanup));

   confirm=message=>{
    using(var dialog=UiMessageBox.Create(this,message,Loc.T("Aktualizácie"),MessageBoxButtons.YesNo,MessageBoxIcon.Question,true))
     return dialog.ShowDialog(this)==DialogResult.Yes;
   };
   rollback.Enabled=false;
   try{
    var state=service.State();
    rollback.Enabled=state.Previous!=null&&state.AuthorizedRoots.Exists(r=>UpdateActivation.SamePath(r,running));
    if(state.Current!=null&&!UpdateActivation.SamePath(state.Current.Directory,running))Ready(state.Current.Directory);
   }catch(Exception ex){SetFailure(ex);}
   rollback.Visible=rollback.Enabled;
   RefreshActions();
   RefreshPalette();
  }

  internal void AddManagementPages(Action<Panel> buildBackup,Func<Panel,Button> buildDiagnostics) {
   Text=Loc.T("Správa aplikácie");
   var tabs=new ModernTabs{Dock=DockStyle.Fill,Margin=Padding.Empty,AccessibleName=Text,CompactColumns=3,SingleTabStop=true};
   var updates=tabs.AddPage(Loc.T("Aktualizácie"));
   updates.AutoScroll=false;
   layout.Controls.Remove(updatePage);
   updates.Controls.Add(updatePage);
   buildBackup(tabs.AddPage(Loc.T("Zálohy")));
   var save=buildDiagnostics(tabs.AddPage(Loc.T("Diagnostika")));
   save.Margin=new Padding(0,0,6,0);
   footer.Controls.Add(save);
   tabs.TabIndex=0;
   layout.Controls.Add(tabs,0,0);
   RefreshPalette();
  }

  protected override void OnSystemColorsChanged(EventArgs e) {
   base.OnSystemColorsChanged(e);
   if(primary!=null)RefreshPalette();
  }

  void RefreshPalette() {
   style(this);
   primary.BackColor=StudioTheme.HighContrast?SystemColors.Control:StudioTheme.Accent;
   primary.ForeColor=StudioTheme.HighContrast?SystemColors.ControlText:Color.White;
  }

  void SetStatus(string text) {
   information.Text=text;
  }

  void SetFailure(Exception error,string explanation=null) {
   string message=explanation==null?error.Message:Loc.T(explanation)+Environment.NewLine+error.Message;
   if(error is BackgroundFailure failure && failure.DiagnosticId!=null)
    message+=Environment.NewLine+Loc.T("Diagnostický záznam")+": "+failure.DiagnosticId;
   SetStatus(message);
  }

  void RefreshActions() {
   primary.Text=Loc.T(offer!=null?"Stiahnuť a pripraviť":prepared!=null?"Spustiť pripravenú verziu…":"Skontrolovať aktualizácie");
   primary.AccessibleName=primary.Text;
   bool canRecheck=prepared!=null||offer!=null;
   if(!canRecheck&&onlineCheck.Focused)primary.Focus();
   onlineCheck.Visible=canRecheck;
   restartPrepared.Visible=prepared!=null&&offer!=null;
  }

  void RunPrimaryAction() {
   if(offer!=null)Download();
   else if(prepared!=null)RequestActivation();
   else CheckOnline();
  }

  bool Work<T>(string title,Func<T> work,out T result) {
   return WorkDialog.Run(this,"update",Loc.T(title),Font,Icon,style,work,out result);
  }

  void CheckOnline() {
   try{
    if(!Work("Kontrolujem aktualizácie…",()=>{
     using(var client=networkFactory())return client.Check(service.Family);
    },out UpdateOffer found))return;
    offer=found.PackageId==installedId||found.PackageId==preparedId?null:found;
    if(found.PackageId==preparedId)SetPreparedStatus();
    else if(offer==null)SetStatus(Loc.T("Používate aktuálnu zostavu."));
    else SetStatus(Loc.T("Dostupná nová zostava")+": "+offer.PackageId.Substring(0,12)+" / "+offer.Family);
   }catch(Exception ex){
    offer=null;
    SetFailure(ex,"Aktualizáciu sa nepodarilo overiť. Program môžete ďalej používať bez internetu.");
   }finally{RefreshActions();}
  }

  void Download() {
   var selected=offer;
   if(selected==null)return;
   try{
    if(Work("Pripravujem aktualizáciu…",()=>{
     using(var client=networkFactory()){
      string zip=client.Download(selected,Path.Combine(service.Root,"downloads"));
      try{return UpdatePreparation.Prepare(zip,selected.Sha256,service.Family,service.PackagesRoot,selected);}
      finally{if(File.Exists(zip))File.Delete(zip);}
     }
    },out string path))Ready(path);
   }catch(Exception ex){SetFailure(ex);}
  }

  void Import() {
   using(var zip=new OpenFileDialog{Filter="ZIP|*.zip",Title=Loc.T("Použiť ručne stiahnutý ZIP…")}){
    if(zip.ShowDialog(this)!=DialogResult.OK)return;
    using(var sum=new OpenFileDialog{Filter="SHA256SUMS|*.txt",InitialDirectory=Path.GetDirectoryName(zip.FileName),Title=Loc.T("Vyberte kontrolný súčet SHA-256")}){
     if(sum.ShowDialog(this)!=DialogResult.OK)return;
     if(confirm(Loc.T("Použiť tento ZIP z dôveryhodného zdroja? Kontrolný súčet overí integritu, nie nezávislý podpis vydavateľa.")))
      PrepareLocal(zip.FileName,sum.FileName,true);
    }
   }
  }

  public bool PrepareLocal(string zip,string checksum,bool sourceConfirmed) {
   if(!sourceConfirmed)return false;
   try{
    if(!Work("Overujem aktualizáciu…",()=>UpdatePreparation.Prepare(zip,UpdatePreparation.ReadChecksum(checksum,Path.GetFileName(zip)),service.Family,service.PackagesRoot),out string path))return false;
    Ready(path);
    return true;
   }catch(Exception ex){SetFailure(ex);return false;}
  }

  void Ready(string path) {
   var candidate=UpdatePackage.Read(Path.Combine(path,"package-manifest.xml"));
   if(installedId==candidate.PackageId){
    SetStatus(Loc.T("Používate aktuálnu zostavu."));
   }else{
    prepared=path;
    preparedId=candidate.PackageId;
    offer=null;
    SetPreparedStatus();
   }
   RefreshActions();
  }

  void SetPreparedStatus() {
   SetStatus(Loc.T("Balík je overený a pripravený. Spustenie vyžaduje vaše potvrdenie."));
  }

  void RequestActivation() {
   if(prepared==null||!confirm(Loc.T("Spustiť novú verziu a zavrieť toto okno programu? Neuložený dokument môžete uložiť alebo aktualizáciu zrušiť.")))return;
   RequestedDirectory=prepared;
   RollbackRequested=false;
   DialogResult=DialogResult.OK;
   Close();
  }

  void RequestRollback() {
   if(!rollback.Enabled||!confirm(Loc.T("Vrátiť sa na predchádzajúcu verziu? Súčasné nastavenia zostanú zachované.")))return;
   RollbackRequested=true;
   DialogResult=DialogResult.OK;
   Close();
  }

  void Cleanup() {
   if(!confirm(Loc.T("Odstrániť overené staršie zostavy z úložiska aktualizácií? Aktívna verzia a možnosť návratu zostanú dostupné.")))return;
   try{
    if(Work("Čistím úložisko aktualizácií…",()=>service.Cleanup(running,prepared),out UpdateCleanupResult result))
     SetStatus(result.Count==0?Loc.T("Žiadne zostavy na odstránenie."):string.Format(Loc.T("Odstránené zostavy: {0}. Uvoľnené miesto: {1:N1} MiB."),result.Count,result.Bytes/1048576d));
   }catch(Exception ex){SetFailure(ex);}
  }
 }

 public partial class MainForm {
  Func<UpdateActivation> updateActivationFactory=UpdateActivation.Default;
  Func<string,bool> updateLauncher=UpdateActivation.Launch;
  string updateRunningDirectory=AppDomain.CurrentDomain.BaseDirectory;

  UpdatesDialog CreateApplicationManagement(UpdateActivation service) {
   var dialog=new UpdatesDialog(service,updateRunningDirectory,ApplyColors){Font=Font,Icon=Icon};
   dialog.AddManagementPages(backup=>BuildWorkspaceBackupPage(dialog,backup),diagnostics=>BuildDiagnosticsPage(dialog,diagnostics));
   return dialog;
  }

  void ShowApplicationManagement() {
   var service=updateActivationFactory();
   using(var dialog=CreateApplicationManagement(service))
    if(dialog.ShowDialog(this)==DialogResult.OK)CompleteUpdateSwitch(service,dialog.RequestedDirectory,dialog.RollbackRequested);
  }

  void CompleteUpdateSwitch(UpdateActivation service,string target,bool rollback){FormClosingEventHandler handler=delegate(object sender,FormClosingEventArgs e){if(e.Cancel)return;try{if(rollback)service.Rollback(updateRunningDirectory,updateLauncher);else service.Activate(updateRunningDirectory,target,updateLauncher);}catch(Exception ex){e.Cancel=true;Error(ex);}};FormClosing+=handler;try{Close();}finally{FormClosing-=handler;}}
 }
}
