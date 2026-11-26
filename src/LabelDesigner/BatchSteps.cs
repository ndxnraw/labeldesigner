using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  sealed partial class BatchWindow {
   const int SourceStep=0,MappingStep=1,ReviewStep=2;
   readonly ModernTabs batchSteps=new(){Dock=DockStyle.Fill,Name="batchSteps"};
   readonly Label sourceInfo=new(){AutoSize=true,UseMnemonic=false};
   TableLayoutPanel csvSettings,sheetSettings,generatedSettings;
   Button csvOptions,useGenerated,sourceContinue;
   bool showCsvOptions;

   void BuildBatchSteps(Control previewPane) {
    window.MinimumSize=new Size(760,540);
    batchSteps.Padding=new Padding(12);
    batchSteps.AccessibleName=Loc.T("Kroky dávky");
    window.Controls.Add(batchSteps);
    BuildSourceStep(batchSteps.AddPage(Loc.T("Zdroj")));
    BuildMappingStep(batchSteps.AddPage(Loc.T("Mapovanie")));
    BuildReviewStep(batchSteps.AddPage(Loc.T("Kontrola")),previewPane);
   }

   void BuildSourceStep(Panel page) {
    var fields=Stack();
    fields.MaximumSize=new Size(640,0);
    page.Controls.Add(fields);
    var actions=StepActions();
    actions.Controls.Add(owner.Btn(Loc.T("Import CSV / XLSX"),Import,HeaderIcon.Open));
    useGenerated=owner.Btn(Loc.T("Použiť generované záznamy"),UseGeneratedSource);
    actions.Controls.Add(useGenerated);
    actions.Controls.Add(owner.Btn(Loc.T("Predvoľby dávky…"),ShowPresets,HeaderIcon.Library));
    actions.Controls.Add(owner.Btn(Loc.T("Premenné polia"),delegate{owner.EditVariables();Map();}));
    referenceButton=owner.Btn(Loc.T("Referenčná dávka…"),ShowBatchReference);
    actions.Controls.Add(referenceButton);
    StackAdd(fields,actions);
    StackAdd(fields,sourceInfo);
    fields.SizeChanged+=delegate{sourceInfo.MaximumSize=new Size(Math.Max(1,fields.ClientSize.Width-fields.Padding.Horizontal),0);};

    csvOptions=owner.Btn(Loc.T("Nastavenia CSV"),delegate{showCsvOptions=!showCsvOptions;RefreshSourceStep();});
    StackAdd(fields,csvOptions);
    csvSettings=Stack();
    csvSettings.Padding=Padding.Empty;
    owner.StackField(csvSettings,Loc.T("Oddeľovač"),separator);
    owner.StackField(csvSettings,Loc.T("Kódovanie"),encoding);
    StackAdd(fields,csvSettings);
    sheetSettings=Stack();
    sheetSettings.Padding=Padding.Empty;
    owner.StackField(sheetSettings,Loc.T("Pracovný hárok"),sheet);
    StackAdd(fields,sheetSettings);
    StackAdd(fields,header);

    generatedSettings=Stack();
    generatedSettings.Padding=Padding.Empty;
    owner.StackField(generatedSettings,Loc.T("Počet záznamov"),generated);
    StackAdd(fields,generatedSettings);

    sourceContinue=owner.Btn(Loc.T("Pokračovať na mapovanie"),ContinueFromSource);
    sourceContinue.Name="batchSourceContinue";
    var next=StepActions();
    next.Controls.Add(sourceContinue);
    StackAdd(fields,next);
   }

   void BuildMappingStep(Panel page) {
    page.AutoScroll=false;
    var layout=StepLayout();
    page.Controls.Add(layout);
    mapping.AccessibleName=Loc.T("Mapovanie stĺpcov");
    layout.Controls.Add(mapping,0,1);
    mappingInfo.AutoSize=true;
    mappingInfo.UseMnemonic=false;
    layout.Controls.Add(mappingInfo,0,0);
    layout.SizeChanged+=delegate{mappingInfo.MaximumSize=new Size(Math.Max(1,layout.ClientSize.Width-12),0);};

    var actions=StepActions();
    actions.Controls.Add(owner.Btn(Loc.T("Späť na zdroj"),()=>batchSteps.SelectedIndex=SourceStep));
    sampleButton=owner.Btn(Loc.T("Náhľad prvých záznamov…"),ShowSample,HeaderIcon.Visibility);
    actions.Controls.Add(sampleButton);
    prepareButton=owner.Btn(Loc.T("Pripraviť dávku"),Prepare,HeaderIcon.Check);
    prepareButton.Name="batchPrepare";
    actions.Controls.Add(prepareButton);
    layout.Controls.Add(actions,0,2);
   }

   void BuildReviewStep(Panel page,Control previewPane) {
    page.AutoScroll=false;
    var layout=StepLayout();
    page.Controls.Add(layout);
    preparedInfo.AutoSize=true;
    preparedInfo.UseMnemonic=false;
    layout.Controls.Add(preparedInfo,0,0);
    layout.SizeChanged+=delegate{preparedInfo.MaximumSize=new Size(Math.Max(1,layout.ClientSize.Width-12),0);};
    var contents=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
    contents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,56));
    contents.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,44));
    records.AccessibleName=Loc.T("Pripravené záznamy");
    contents.Controls.Add(records,0,0);
    contents.Controls.Add(previewPane,1,0);
    layout.Controls.Add(contents,0,1);

    var footer=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=2};
    selectionInfo.AutoSize=true;
    selectionInfo.UseMnemonic=false;
    footer.Controls.Add(selectionInfo,0,0);
    footer.SizeChanged+=delegate{selectionInfo.MaximumSize=new Size(Math.Max(1,footer.ClientSize.Width-12),0);};
    var actions=StepActions();
    actions.Controls.Add(owner.Btn(Loc.T("Vybrať všetko"),()=>SelectAll(true)));
    actions.Controls.Add(owner.Btn(Loc.T("Zrušiť výber"),()=>SelectAll(false)));
    issuesButton=owner.Btn(Loc.T("Problémy vstupných dát…"),ShowIssues);
    actions.Controls.Add(issuesButton);
    compareButton=owner.Btn(Loc.T("Porovnať dávku…"),CompareBatch);
    actions.Controls.Add(compareButton);
    exportSelected=owner.Btn(Loc.T("Exportovať vybrané"),Export);
    printSelected=owner.Btn(Loc.T("Tlačiť vybrané"),Print);
    actions.Controls.Add(exportSelected);
    actions.Controls.Add(printSelected);
    footer.Controls.Add(actions,0,1);
    layout.Controls.Add(footer,0,2);
   }

   static TableLayoutPanel StepLayout() {
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(6)};
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    return layout;
   }

   static FlowLayoutPanel StepActions()=>new(){AutoSize=true,Dock=DockStyle.Top,WrapContents=true};

   void StyleBatchPrimaryActions() {
    if(SystemInformation.HighContrast)return;
    foreach(var button in new[]{sourceContinue,prepareButton,printSelected}){
     button.BackColor=StudioTheme.Accent;
     button.ForeColor=Color.White;
     button.FlatAppearance.BorderColor=StudioTheme.Accent;
    }
   }

   bool SourceCanPrepareDirectly()=>CanPrepare()&&(CurrentTable==null||
    (ActivePreset!=null&&header.Checked));

   void ContinueFromSource() {
    if(SourceCanPrepareDirectly()){Prepare();return;}
    if(CurrentTable==null)return;
    batchSteps.SelectedIndex=MappingStep;
    mapping.Focus();
   }

   void UseGeneratedSource() {
    csvPath=tableSourcePath=null;
    setup.ActivePresetName=null;
    showCsvOptions=false;
    InstallTables(new System.Collections.Generic.List<DataTableSource>());
   }

   void RefreshSourceStep() {
    bool imported=tables.Count>0;
    bool csv=csvPath!=null;
    csvOptions.Visible=!imported;
    csvSettings.Visible=csv||!imported&&showCsvOptions;
    sheetSettings.Visible=imported&&!csv;
    header.Visible=imported||showCsvOptions;
    generatedSettings.Visible=!imported&&ActivePreset==null;
    useGenerated.Visible=imported||ActivePreset!=null;
    sourceInfo.Text=imported?Path.GetFileName(tableSourcePath):Loc.T("Generované záznamy alebo import CSV / XLSX.");
    if(mappingInfo.Text.Length>0)sourceInfo.Text+=Environment.NewLine+mappingInfo.Text;
    bool skip=SourceCanPrepareDirectly();
    sourceContinue.Text=Loc.T(skip?"Pripraviť dávku":"Pokračovať na mapovanie");
    sourceContinue.Enabled=skip||CurrentTable!=null;
   }
  }
 }
}
