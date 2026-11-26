using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  LabelLanguages labelLanguages;
  Control textTranslations;
  bool HasTextTranslations(int index)=>labelLanguages!=null&&elementMetadata.Exists(meta=>meta.Index==index&&meta.Translations?.Count>0);

  (Form Window,TableLayoutPanel Fields,Button Apply) CreateLanguageDialog(string title){
   var window=FeatureForm(title,590,500);
   window.MinimumSize=new Size(450,360);
   window.MaximizeBox=false;window.MinimizeBox=false;
   var layout=Rows();window.Controls.Add(layout);
   var viewport=new Panel();ManagementLayout.AddRow(layout,viewport,SizeType.Percent);
   var fields=ScrollRows(viewport);
   var footer=new UiMessageBox.ActionRow();
   var apply=Btn(Loc.T("Použiť"),()=>window.DialogResult=DialogResult.OK);
   var cancel=Btn(Loc.T("Zrušiť"),()=>window.DialogResult=DialogResult.Cancel);
   cancel.DialogResult=DialogResult.Cancel;
   footer.AddAction(apply);footer.AddAction(cancel);ManagementLayout.AddRow(layout,footer);
   window.AcceptButton=apply;window.CancelButton=cancel;
   return (window,fields,apply);
  }

  void EditLabelLanguages(){
   CommitInspectorIfFocused();
   var existing=labelLanguages;
   var dialog=CreateLanguageDialog("Jazyky etikety…");
   using(var window=dialog.Window){
    var initial=existing?.Codes??new List<string>{"SK","CZ","EN"};
    var codes=Input(dialog.Fields,"Kódy jazykov (jeden na riadok)",string.Join(Environment.NewLine,initial));
    codes.Name="labelLanguageCodes";codes.Multiline=true;codes.AcceptsReturn=true;
    codes.Height=120;codes.MaxLength=600;codes.ScrollBars=ScrollBars.Vertical;
    var selected=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="selectedLabelLanguage"};
    StackField(dialog.Fields,Loc.T("Jazyk etikety"),selected);
    var fallback=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="fallbackLabelLanguage"};
    StackField(dialog.Fields,Loc.T("Náhradný jazyk"),fallback);
    var status=Description("");StackAdd(dialog.Fields,status);
    List<string> available=null;
    string rememberedSelected=existing?.Selected??initial[0],rememberedFallback=existing?.Fallback;
    void RefreshCodes(){
     if(selected.SelectedItem is string code)rememberedSelected=code;
     if(fallback.SelectedIndex>=0)rememberedFallback=fallback.SelectedIndex>0?fallback.SelectedItem as string:null;
     var parsed=new List<string>();
     var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     foreach(string line in codes.Lines){
      string value=line.Trim();
      if(value.Length==0)continue;
      if(!LabelLanguages.IsCode(value)||!seen.Add(value)){
       status.Text=Loc.T("Zadajte jedinečné kódy jazykov, napríklad SK alebo en-GB.");
       dialog.Apply.Enabled=false;return;
      }
      parsed.Add(value);
     }
     if(parsed.Count<1||parsed.Count>LabelLanguages.MaximumCodes){
      status.Text=Loc.T("Zadajte 1 až 16 jazykov etikety.");
      dialog.Apply.Enabled=false;return;
     }
     available=parsed;
     selected.Items.Clear();selected.Items.AddRange(available.ToArray());
     int primary=available.FindIndex(item=>LabelLanguages.SameCode(item,rememberedSelected));
     selected.SelectedIndex=Math.Max(0,primary);
     fallback.Items.Clear();fallback.Items.Add(Loc.T("Bez náhradného jazyka"));fallback.Items.AddRange(available.ToArray());
     fallback.SelectedIndex=1+available.FindIndex(item=>LabelLanguages.SameCode(item,rememberedFallback));
     status.Text="";dialog.Apply.Enabled=true;
    }
    codes.TextChanged+=delegate{RefreshCodes();};
    RefreshCodes();
    ApplyColors(window);
    if(window.ShowDialog(this)==DialogResult.OK)
     ChangeDocument(label=>label.Languages=new LabelLanguages{Codes=available,Selected=(string)selected.SelectedItem,
      Fallback=fallback.SelectedIndex>0?(string)fallback.SelectedItem:null});
   }
  }

  void EditTextTranslations(){
   int index=preview.SelectedIndex;
   if(index<0||elementMetadata.Exists(meta=>meta.Index==index&&meta.Locked))return;
   CommitInspectorIfFocused();
   if(labelLanguages==null){
    EditLabelLanguages();
    if(labelLanguages==null)return;
   }
   var template=Read();
   var previous=template.Metadata.Find(meta=>meta.Index==index)?.Translations;
   var dialog=CreateLanguageDialog("Preklady textu…");
   using(var window=dialog.Window){
    var use=Check(dialog.Fields,"Použiť jazykové varianty",true);
    use.Name="enableTextTranslations";
    var drafts=new List<(string Code,TextBox Input)>();
    foreach(string code in template.Languages.Codes){
     var old=previous?.Find(item=>LabelLanguages.SameCode(item.Language,code));
     string initial=old?.Text??((previous?.Count??0)==0&&LabelLanguages.SameCode(code,template.Languages.Selected)?template.Texts[index].Text:"");
     var input=Input(dialog.Fields,code,initial);
     input.Name="textTranslation-"+code;input.Multiline=true;input.AcceptsReturn=true;
     input.Height=72;input.MaxLength=4000;input.ScrollBars=ScrollBars.Vertical;
     drafts.Add((code,input));
    }
    use.CheckedChanged+=delegate{foreach(var draft in drafts)draft.Input.Enabled=use.Checked;};
    ApplyColors(window);
    if(window.ShowDialog(this)!=DialogResult.OK)return;
    ChangeDocument(label=>{
     var entries=use.Checked?drafts.ConvertAll(draft=>new LanguageTranslation{Language=draft.Code,Text=draft.Input.Text}):null;
     Elements.Meta(label,index).Translations=entries;
     if(entries!=null)label.Texts[index].AutoWidth=false;
    });
    preview.SelectElement(index);
   }
  }
 }
}
