using System;
using System.Collections.Generic;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  List<TemplateInput> templateInputs=new();
  const string TemplateInputsTitle="Pomenované vstupy šablóny…";

  static string TemplateInputCaption(TemplateInput input){
   string type=Loc.T(input.Kind switch{TemplateInputKind.Number=>"Číslo",TemplateInputKind.Date=>"Dátum",_=>"Text"});
   return input.Name+" · "+type+(input.Required?" · "+Loc.T("Povinné"):"");
  }

  void EditTemplateInputs(string focusId=null){
   CommitInspectorIfFocused();
   var document=Read();
   using var window=FeatureForm(TemplateInputsTitle,675,440);
   var root=Rows();window.Controls.Add(root);
   ManagementLayout.AddRow(root,Description("Vstupy používajú výpočty, pravidlá zobrazenia a formuláre šablóny."));
   var list=new ListBox{Dock=DockStyle.Fill,Name="templateInputs",
    IntegralHeight=false,HorizontalScrollbar=true,AccessibleName=Loc.T(TemplateInputsTitle)};
   ManagementLayout.AddRow(root,list,SizeType.Percent);
   var status=Description("");ManagementLayout.AddRow(root,status);
   var footer=new UiMessageBox.ActionRow();
   var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
   Button add=null,edit=null,remove=null;
   add=Btn(Loc.T("Pridať"),()=>Edit(new TemplateInput(),false));
   edit=Btn(Loc.T("Upraviť"),()=>{if(list.SelectedItem is InputRow row)Edit(row.Input,true);});
   remove=Btn(Loc.T("Odstrániť"),delegate{
    if(list.SelectedItem is not InputRow row)return;
    if(row.Input.IsReferenced(document)){status.Text=Loc.T("Vstup používa výpočet alebo pravidlo zobrazenia.");return;}
    string target=TemplateInput.Target(row.Input.Id);
    if(document.Batch.Presets.Exists(preset=>preset.Reference?.Fields.Exists(binding=>binding.Target==target)==true)){
     status.Text=Loc.T("Vstup používa uložená referenčná dávka.");
     return;
    }
    document.Inputs.Remove(row.Input);Refresh(null);
   });
   actions.Controls.AddRange(new Control[]{add,edit,remove});
   ManagementLayout.AddRow(root,actions);
   var apply=Btn(Loc.T("Použiť"),()=>window.DialogResult=DialogResult.OK);
   var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
   footer.AddAction(apply);footer.AddAction(cancel);ManagementLayout.AddRow(root,footer);
   window.AcceptButton=apply;window.CancelButton=cancel;
   list.DoubleClick+=delegate{edit.PerformClick();};
   list.SelectedIndexChanged+=delegate{edit.Enabled=remove.Enabled=list.SelectedIndex>=0;};
   Refresh(focusId);
   ApplyColors(window);
   if(window.ShowDialog(this)!=DialogResult.OK)return;
   if(ProjectXml.Encode(document.Inputs)==ProjectXml.Encode(templateInputs))return;
   ChangeDocument(label=>{
    var kept=new HashSet<string>(document.Inputs.ConvertAll(input=>input.Id),StringComparer.Ordinal);
    bool Obsolete(ColumnBinding binding)=>TemplateInput.TryTarget(binding.Target,out string id)&&!kept.Contains(id);
    label.Inputs=document.Inputs;
    label.Batch.Bindings.RemoveAll(Obsolete);
    foreach(var preset in label.Batch.Presets)preset.Bindings.RemoveAll(Obsolete);
    foreach(var scenario in label.Scenarios)scenario.Values.RemoveAll(value=>value.InputId!=null&&!kept.Contains(value.InputId));
   });

   void Refresh(string id){
    list.Items.Clear();
    list.Items.AddRange(document.Inputs.ConvertAll(input=>new InputRow(input,TemplateInputCaption(input))).ToArray());
    if(list.Items.Count>0)list.SelectedIndex=Math.Max(0,document.Inputs.FindIndex(input=>input.Id==id));
    add.Enabled=list.Items.Count<TemplateInput.MaximumCount;
    edit.Enabled=remove.Enabled=list.SelectedIndex>=0;
    status.Text="";
   }
   void Edit(TemplateInput input,bool existing){
    var updated=EditTemplateInput(window,input,document.Inputs);
    if(updated==null)return;
    if(existing)document.Inputs[document.Inputs.IndexOf(input)]=updated;
    else document.Inputs.Add(updated);
    Refresh(updated.Id);
   }
  }

  TemplateInput EditTemplateInput(Form owner,TemplateInput old,List<TemplateInput> current){
   var dialog=CreateFieldSettingsDialog("Vlastnosti vstupu šablóny");
   using var window=dialog.Window;
   var name=Input(dialog.Fields,"Názov vstupu",old.Name);name.Name="templateInputName";name.MaxLength=100;
   var type=Choice(dialog.Fields,"Typ vstupu",new[]{"Text","Číslo","Dátum"},(int)old.Kind);
   type.Name="templateInputType";
   var value=Input(dialog.Fields,"Predvolená hodnota",old.DefaultValue);
   value.Name="templateInputDefault";value.MaxLength=4000;value.Multiline=true;value.AcceptsReturn=true;value.Height=52;
   var required=Check(dialog.Fields,"Povinný vstup",old.Required);required.Name="templateInputRequired";
   var hint=Description("");StackAdd(dialog.Fields,hint);
   var validation=Description("");StackAdd(dialog.Fields,validation);
   TemplateInput candidate=null;
   void Refresh(){
    candidate=new TemplateInput{Id=old.Id,Name=name.Text.Trim(),Kind=(TemplateInputKind)type.SelectedIndex,
     DefaultValue=value.Text,Required=required.Checked};
    hint.Text=candidate.Kind==TemplateInputKind.Date?Loc.T("Zadajte dátum vo formáte RRRR-MM-DD."):
     candidate.Kind==TemplateInputKind.Number?Loc.T("Čísla môžu používať desatinnú čiarku alebo bodku."):"";
    var definitions=current.FindAll(input=>input.Id!=old.Id);definitions.Add(candidate);
    try{TemplateInput.ValidateAll(definitions);validation.Text="";dialog.Apply.Enabled=true;}
    catch(Exception ex){validation.Text=ex.Message;dialog.Apply.Enabled=false;}
   }
   name.TextChanged+=delegate{Refresh();};type.SelectedIndexChanged+=delegate{Refresh();};
   value.TextChanged+=delegate{Refresh();};required.CheckedChanged+=delegate{Refresh();};
   Refresh();
   ApplyColors(window);
   return window.ShowDialog(owner)==DialogResult.OK?candidate:null;
  }

  sealed record InputRow(TemplateInput Input,string Caption){
   public override string ToString()=>Caption;
  }
 }
}
