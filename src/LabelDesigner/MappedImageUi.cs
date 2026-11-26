using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  const string MappedImageTitle="Obrázok podľa údajov…";

  void EditMappedImage(int index){
   CommitInspectorIfFocused();
   var template=Read();
   bool existing=index<=-3;
   if(!existing&&template.Symbols.Count>=20){Error(new InvalidOperationException(Loc.T("Najviac 20 symbolov.")));return;}
   var symbol=existing?template.Symbols[-3-index]:new SymbolItem{Width=Math.Min(24,template.Width-6),Height=Math.Min(18,template.Height-6)};
   var mapping=symbol.ImageMapping??new ImageMapping();
   using(var dialog=FeatureForm(MappedImageTitle,685,600)){
    dialog.MinimumSize=new Size(510,400);
    dialog.MaximizeBox=false;dialog.MinimizeBox=false;
    var layout=Rows();dialog.Controls.Add(layout);
    var viewport=new Panel();ManagementLayout.AddRow(layout,viewport,SizeType.Percent);
    var rows=ScrollRows(viewport);

    var source=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="imageDataSource"};
    StackField(rows,Loc.T("Zdroj údajov"),source);
    AddArtworkSources(source,template);
    if(!existing&&source.Items.Count>0)source.SelectedIndex=0;
    for(int i=0;existing&&i<source.Items.Count;i++){
     var option=(ArtworkDataSource)source.Items[i];
     if(option.Field==mapping.Field&&option.Variable==mapping.Variable){source.SelectedIndex=i;break;}
    }
    var grid=new DataGridView{Name="imageVariants",AccessibleName=Loc.T("Priradené obrázky"),Height=180,
     AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,AllowUserToAddRows=false,
     AllowUserToDeleteRows=false,MultiSelect=false,StandardTab=true};
    grid.Columns.Add("imageKey",Loc.T("Porovnávaná hodnota"));
    grid.Columns.Add("imageFile",Loc.T("Obrázok"));
    grid.Columns[1].ReadOnly=true;
    foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
    StackAdd(rows,grid);

    var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};
    StackAdd(rows,actions);
    var missing=Choice(rows,"Ak chýba priradenie",new[]{"Zastaviť výstup","Nechať rám prázdny","Použiť predvolený obrázok"},(int)mapping.Missing);
    missing.Name="imageMissingPolicy";
    var fallback=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="imageFallback"};
    fallback.DisplayMember=nameof(ImageVariant.DisplayKey);
    StackField(rows,Loc.T("Predvolený obrázok"),fallback);
    var validation=Description("");
    StackAdd(rows,validation);

    var footer=new UiMessageBox.ActionRow();
    var save=Btn(Loc.T("Použiť"),()=>dialog.DialogResult=DialogResult.OK);
    var cancel=Btn(Loc.T("Zrušiť"),()=>dialog.DialogResult=DialogResult.Cancel);
    cancel.DialogResult=DialogResult.Cancel;
    footer.AddAction(save);footer.AddAction(cancel);ManagementLayout.AddRow(layout,footer);
    dialog.AcceptButton=save;dialog.CancelButton=cancel;

    ImageVariant Current()=>grid.CurrentRow?.Tag as ImageVariant;
    void Refresh(){
     bool fallbackRequired=missing.SelectedIndex==(int)MissingImage.Fallback;
     fallback.Enabled=fallbackRequired;
     var keys=new HashSet<string>(StringComparer.Ordinal);
     string error=null;
     if(source.SelectedIndex<0)error=Loc.T("Vyberte zdroj obrázka.");
     else if(mapping.Variants.Count==0)error=Loc.T("Pridajte aspoň jeden obrázok.");
     else if(mapping.Variants.Exists(item=>string.IsNullOrWhiteSpace(item.Key)||
      item.Key.Length>ImageMapping.MaximumKeyLength||!keys.Add(item.Key.Trim())))error=Loc.T("Hodnoty obrázkov musia byť vyplnené a jedinečné.");
     else if(fallbackRequired&&fallback.SelectedIndex<0)error=Loc.T("Chýba predvolený obrázok.");
     validation.Text=error??"";
     validation.Visible=error!=null;
     save.Enabled=error==null;
    }
    void RefreshFallback(ImageVariant selected){
     fallback.BeginUpdate();
     try{
      fallback.Items.Clear();
      foreach(var variant in mapping.Variants)fallback.Items.Add(variant);
      fallback.SelectedItem=selected;
     }finally{fallback.EndUpdate();}
     Refresh();
    }
    void AddRowFor(ImageVariant variant){grid.Rows[grid.Rows.Add(variant.Key,variant.Name)].Tag=variant;}
    void ChooseImage(bool replace){
     grid.EndEdit();
     var target=replace?Current():null;
     if(replace&&target==null)return;
     if(!replace&&mapping.Variants.Count>=ImageMapping.MaximumVariants)return;
     using(var file=new OpenFileDialog{Title=Loc.T("Vložiť obrázok"),Filter="PNG / JPEG / SVG|*.png;*.jpg;*.jpeg;*.svg"}){
      if(file.ShowDialog(dialog)!=DialogResult.OK)return;
      try{
       var image=Assets.Import(file.FileName);
       string name=Path.GetFileName(file.FileName);
       if(target!=null){
        target.Name=name;target.Image=image;grid.CurrentRow.Cells[1].Value=name;
       }else{
        string stem=Path.GetFileNameWithoutExtension(name).Trim(),key=stem;
        for(int suffix=2;mapping.Variants.Exists(item=>string.Equals(item.Key?.Trim(),key,StringComparison.Ordinal));suffix++)key=stem+" ("+suffix+")";
        target=new ImageVariant{Key=key,Name=name,Image=image};mapping.Variants.Add(target);AddRowFor(target);
        grid.CurrentCell=grid.Rows[grid.Rows.Count-1].Cells[0];
       }
       RefreshFallback(fallback.SelectedItem as ImageVariant);
      }catch(Exception ex){Error(ex);}
     }
    }
    var add=Btn(Loc.T("Vložiť obrázok"),()=>ChooseImage(false),HeaderIcon.Image);
    var replaceImage=Btn(Loc.T("Nahradiť obrázok"),()=>ChooseImage(true),HeaderIcon.Open);
    var remove=Btn(Loc.T("Odstrániť"),()=>{
     grid.EndEdit();
     var victim=Current();if(victim==null)return;
     var selected=fallback.SelectedItem as ImageVariant;
     mapping.Variants.Remove(victim);grid.Rows.Remove(grid.CurrentRow);
     RefreshFallback(selected==victim?null:selected);
    },HeaderIcon.Delete);
    actions.Controls.AddRange(new Control[]{add,replaceImage,remove});
    void RefreshButtons(){
     add.Enabled=mapping.Variants.Count<ImageMapping.MaximumVariants;
     replaceImage.Enabled=remove.Enabled=Current()!=null;
    }
    foreach(var variant in mapping.Variants)AddRowFor(variant);
    RefreshFallback(mapping.Variants.Find(item=>item.Key.Trim()==mapping.FallbackKey?.Trim()));
    RefreshButtons();
    grid.SelectionChanged+=delegate{RefreshButtons();};
    grid.RowsRemoved+=delegate{RefreshButtons();};
    grid.CellEndEdit+=delegate(object sender,DataGridViewCellEventArgs e){
     if(e.ColumnIndex!=0||e.RowIndex<0||grid.Rows[e.RowIndex].Tag is not ImageVariant variant)return;
     variant.Key=Convert.ToString(grid.Rows[e.RowIndex].Cells[0].Value)?.Trim()??"";
     RefreshFallback(fallback.SelectedItem as ImageVariant);
    };
    source.SelectedIndexChanged+=delegate{Refresh();};
    missing.SelectedIndexChanged+=delegate{Refresh();};
    fallback.SelectedIndexChanged+=delegate{Refresh();};
    dialog.FormClosing+=delegate(object sender,FormClosingEventArgs e){
     if(dialog.DialogResult!=DialogResult.OK)return;
     grid.EndEdit();Refresh();
     if(!save.Enabled){e.Cancel=true;return;}
     var data=(ArtworkDataSource)source.SelectedItem;
     mapping.Field=data.Field;mapping.Variable=data.Variable;mapping.Missing=(MissingImage)missing.SelectedIndex;
     mapping.FallbackKey=mapping.Missing==MissingImage.Fallback?((ImageVariant)fallback.SelectedItem).Key:null;
     symbol.ImageMapping=mapping;
     if(existing)template.Symbols[-3-index]=symbol;else template.Symbols.Add(symbol);
     try{Engine.ValidateDraft(template);}
     catch(Exception ex){
      if(!existing)template.Symbols.RemoveAt(template.Symbols.Count-1);
      e.Cancel=true;Error(ex);
     }
    };
    ApplyColors(dialog);
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    ChangeDocument(label=>{if(existing)label.Symbols[-3-index]=symbol;else label.Symbols.Add(symbol);});
    preview.SelectElement(existing?index:-3-symbols.Count+1);
   }
  }
 }
}
