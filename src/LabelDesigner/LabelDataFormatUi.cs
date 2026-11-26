using System;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  Control textDataFormat;

  void EditTextDataFormat(){
   int index=preview.SelectedIndex;
   if(index<0||elementMetadata.Exists(meta=>meta.Index==index&&meta.Locked))return;
   CommitInspectorIfFocused();
   var template=Read();
   var existing=LabelDataFormat.ForField(template,index);
   var values=existing??new LabelDataFormat();
   var dialog=CreateFieldSettingsDialog("Formát údajov…");
   using var window=dialog.Window;
   var kind=Choice(dialog.Fields,"Formát poľa",new[]{"Bez formátovania","Číslo","Dátum"},existing==null?0:(int)values.Kind+1);
   kind.Name="dataFormatKind";
   var numberSettings=Stack();numberSettings.Padding=Padding.Empty;StackAdd(dialog.Fields,numberSettings);
   var separator=Choice(numberSettings,"Desatinný oddeľovač na vstupe",new[]{"Bodka (1.5)","Čiarka (1,5)"},(int)values.DecimalInput);
   separator.Name="dataFormatSeparator";
   var decimals=Number(numberSettings,"Desatinné miesta na etikete",values.Decimals,0,8,0);decimals.Name="dataFormatDecimals";
   var unit=Input(numberSettings,"Jednotka na etikete",values.Unit??"");unit.Name="dataFormatUnit";unit.MaxLength=32;
   var dateSettings=Stack();dateSettings.Padding=Padding.Empty;StackAdd(dialog.Fields,dateSettings);
   var sourceDate=Choice(dateSettings,"Formát dátumu na vstupe",
    new[]{"RRRR-MM-DD","D.M.RRRR","D/M/RRRR","M/D/RRRR"},(int)values.DateInput);
   sourceDate.Name="dataFormatDateInput";
   var outputDate=Choice(dateSettings,"Formát dátumu na etikete",
    new[]{"Podľa jazyka etikety","RRRR-MM-DD","DD. MM. RRRR","DD/MM/RRRR","MM/DD/RRRR"},(int)values.DateOutput);
   outputDate.Name="dataFormatDateOutput";
   var input=Input(dialog.Fields,"Skúšobná vstupná hodnota",existing==null?"1.5":template.Texts[index].Text);
   input.Name="dataFormatSample";input.MaxLength=240;
   var result=Description("");result.Name="dataFormatResult";StackAdd(dialog.Fields,result);
   LabelDataFormat Settings()=>kind.SelectedIndex==0?null:new LabelDataFormat{
    Kind=(TextDataKind)(kind.SelectedIndex-1),DecimalInput=(TextDecimalInput)separator.SelectedIndex,
    Decimals=(int)decimals.Value,Unit=unit.Text.Trim(),DateInput=(TextDateOrder)sourceDate.SelectedIndex,
    DateOutput=(TextDateDisplay)outputDate.SelectedIndex
   };
   var culture=LabelDataFormat.CultureFor(template);
   void Refresh(){
    numberSettings.Visible=kind.SelectedIndex==1;
    dateSettings.Visible=kind.SelectedIndex==2;
    try{
     var format=Settings();
     string formatted=input.Text,error=null;
     bool valid=format==null||format.TryFormat(input.Text,TextInputKind.Text,culture,out formatted,out error);
     result.Text="→ "+(valid?formatted:error);
     dialog.Apply.Enabled=true;
    }catch(Exception ex){result.Text=ex.Message;dialog.Apply.Enabled=false;}
   }
   kind.SelectedIndexChanged+=delegate{
    if(kind.SelectedIndex==2&&input.Text=="1.5")input.Text="2026-11-26";
    else if(kind.SelectedIndex==1&&input.Text=="2026-11-26")input.Text="1.5";
    Refresh();
   };
   separator.SelectedIndexChanged+=delegate{Refresh();};
   decimals.ValueChanged+=delegate{Refresh();};
   sourceDate.SelectedIndexChanged+=delegate{Refresh();};
   outputDate.SelectedIndexChanged+=delegate{Refresh();};
   foreach(var editor in new[]{unit,input})editor.TextChanged+=delegate{Refresh();};
   Refresh();
   ApplyColors(window);
   if(window.ShowDialog(this)==DialogResult.OK){
    ChangeDocument(label=>Elements.Meta(label,index).DataFormat=Settings());
    preview.SelectElement(index);
   }
  }
 }
}
