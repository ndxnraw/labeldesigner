using System;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  Button textCalculation;
  const string CalculationTitle="Výpočet poľa…";

  ElementEdit AuthoredCalculatedEdit(ElementEdit edit){
   if(edit.Index<0||!preview.ProjectedElements.TryGetValue(edit.Index,out var visible)||
      Math.Abs(visible.Width-edit.Width)>.001||Math.Abs(visible.Height-edit.Height)>.001)
    return edit;
   var authored=ElementTransform.Get(preview.Settings,edit.Index);
   var translated=edit.Copy();
   translated.X=authored.X+edit.X-visible.X;
   translated.Y=authored.Y+edit.Y-visible.Y;
   translated.Width=authored.Width;
   translated.Height=authored.Height;
   return translated;
  }

  void EditFieldCalculation(){
   int index=preview.SelectedIndex;
   if(index<0||elementMetadata.Exists(meta=>meta.Index==index&&meta.Locked))return;
   CommitInspectorIfFocused();
   var template=Read();
   var original=FieldCalculation.ForField(template,index);
   var dialog=CreateFieldSettingsDialog(CalculationTitle);
   using var window=dialog.Window;
   var operation=Choice(dialog.Fields,"Výpočet",new[]{
    "Bez výpočtu","Sčítanie (A + B)","Odčítanie (A − B)","Násobenie (A × B)",
    "Delenie (A ÷ B)","Zaokrúhlenie A na B miest","Posun dátumu A o B dní"
   },original==null?0:(int)original.Operation+1);
   operation.Name="calculationOperation";
   var operands=Stack();operands.Padding=Padding.Empty;StackAdd(dialog.Fields,operands);
   var left=Operand("Vstup A","calculationLeft",original?.Left);
   var right=Operand("Vstup B","calculationRight",original?.Right);
   var hint=Description("Konštanty zadávajte s desatinnou bodkou a dátumy vo formáte RRRR-MM-DD.");
   StackAdd(operands,hint);
   var result=Description("");result.Name="calculationResult";StackAdd(dialog.Fields,result);

   (ComboBox Source,TextBox Value) Operand(string title,string name,CalculationOperand value){
    var combo=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name=name};
    combo.Items.Add(new CalculationSource(-1,Loc.T("Konštanta")));
    if(value?.Field==-2)combo.Items.Add(new CalculationSource(-2,Loc.T("Chýba zdrojové pole výpočtu.")));
    for(int field=0;field<template.Texts.Count;field++){
     if(field==index)continue;
     string current=template.Texts[field].Text.Replace('\r',' ').Replace('\n',' ');
     if(current.Length>35)current=current.Substring(0,35)+"…";
     combo.Items.Add(new CalculationSource(field,QuickPrint.Name(template,field)+(current==""?"":" · "+current)));
    }
    foreach(var input in template.Inputs)
     if(input.Kind!=TemplateInputKind.Text||input.Id==value?.InputId)
      combo.Items.Add(new CalculationSource(-1,TemplateInputCaption(input),input.Id));
    if(value?.InputId!=null&&!template.Inputs.Exists(input=>input.Id==value.InputId))
     combo.Items.Add(new CalculationSource(-1,Loc.T("Pomenovaný vstup už nie je dostupný."),value.InputId));
    combo.SelectedIndex=0;
    for(int i=1;i<combo.Items.Count;i++)
     if(combo.Items[i] is CalculationSource item&&item.Field==value?.Field&&item.InputId==value?.InputId){
      combo.SelectedIndex=i;break;
     }
    StackField(operands,Loc.T(title),combo);
    var literal=Input(operands,"Hodnota konštanty",value?.Literal??"");
    literal.Name=name+"Literal";literal.MaxLength=100;
    return (combo,literal);
   }

   static CalculationOperand ReadOperand((ComboBox Source,TextBox Value) input)=>new(){
    Field=((CalculationSource)input.Source.SelectedItem).Field,InputId=((CalculationSource)input.Source.SelectedItem).InputId,
    Literal=input.Value.Text.Trim()
   };
   FieldCalculation Settings()=>operation.SelectedIndex==0?null:new FieldCalculation{
    Operation=(CalculationOperation)(operation.SelectedIndex-1),Left=ReadOperand(left),Right=ReadOperand(right)
   };
   void SetFormula(LabelSettings label,FieldCalculation formula){
    var meta=Elements.Meta(label,index);
    meta.Calculation=formula;
    if(formula!=null&&meta.DataFormat!=null)
     meta.DataFormat.Kind=formula.ReturnsDate?TextDataKind.Date:TextDataKind.Number;
   }
   void Refresh(){
    operands.Visible=operation.SelectedIndex>0;
    left.Value.Enabled=left.Source.SelectedItem is CalculationSource{Field:-1,InputId:null};
    right.Value.Enabled=right.Source.SelectedItem is CalculationSource{Field:-1,InputId:null};
    var formula=Settings();
    if(formula==null){result.Text="";return;}
    try{
     SetFormula(template,formula);
     var draft=QuickPrint.PrepareEditor(template,currentTime());
     var problem=draft.SourceIssues().Find(issue=>issue.Element==index&&issue.Blocking);
     var indices=Elements.Indices(draft.Label);
     int placed=indices.FindIndex(field=>Elements.SourceIndex(draft.Label,field)==index);
     result.Text=problem?.Message??(placed<0?Loc.T("Skryté pravidlom"):
      Loc.T("Výsledok výpočtu")+" · "+draft.Label.Texts[indices[placed]].Text);
    }catch(Exception ex){result.Text=ex.Message;}
   }
   operation.SelectedIndexChanged+=delegate{Refresh();};
   left.Source.SelectedIndexChanged+=delegate{Refresh();};right.Source.SelectedIndexChanged+=delegate{Refresh();};
   left.Value.TextChanged+=delegate{Refresh();};right.Value.TextChanged+=delegate{Refresh();};
   Refresh();
   ApplyColors(window);
   if(window.ShowDialog(this)!=DialogResult.OK)return;
   ChangeDocument(label=>SetFormula(label,Settings()));
   preview.SelectElement(index);
  }

  sealed record CalculationSource(int Field,string Caption,string InputId=null){
   public override string ToString()=>Caption;
  }
 }
}
