using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static EanEtikety.ManagementLayout;

namespace EanEtikety {
 public partial class MainForm {
  const string VisibilityTitle="Pravidlo zobrazenia…";

  string SelectedVisibilityGroup(){
   string group=elementMetadata.Find(meta=>meta.Index==preview.SelectedIndex)?.Group;
   return !string.IsNullOrEmpty(group)&&preview.Selection.TrueForAll(index=>elementMetadata.Exists(meta=>meta.Index==index&&meta.Group==group))?group:null;
  }

  bool CanEditVisibility()=>preview.SelectedIndex!=-2&&
   (preview.Selection.Count==1||SelectedVisibilityGroup()!=null)&&
   !preview.Selection.Exists(index=>elementMetadata.Exists(meta=>meta.Index==index&&meta.Locked));

  Button VisibilityAction()=>Btn(Loc.T(VisibilityTitle),EditVisibility,HeaderIcon.Visibility);

  sealed class ArtworkDataSource {
   public readonly string Name,Variable;
   public readonly int Field;
   public ArtworkDataSource(string name,int field=-2,string variable=null){Name=name;Field=field;Variable=variable;}
   public override string ToString()=>Name;
  }

  static void AddArtworkSources(ComboBox choices,LabelSettings template){
   foreach(int field in QuickPrint.Available(template))
    choices.Items.Add(new ArtworkDataSource(QuickPrint.Name(template,field),field));
   foreach(var variable in template.Variables)
    choices.Items.Add(new ArtworkDataSource(Loc.T("Premenná")+" · "+variable.Name,variable:variable.Name));
  }

  void EditVisibility(){
   if(!CanEditVisibility())return;
   CommitInspectorIfFocused();
   var template=Read();
   int index=preview.SelectedIndex;
   string group=SelectedVisibilityGroup();
   var meta=template.Metadata.Find(item=>item.Index==index);
   var groupRule=group==null?null:template.Metadata.Find(item=>item.Group==group&&item.GroupVisibility!=null)?.GroupVisibility;
   using(var dialog=FeatureForm(VisibilityTitle,590,430)){
    dialog.MaximizeBox=false;dialog.MinimizeBox=false;
    var layout=Rows();
    dialog.Controls.Add(layout);
    var scroll=new Panel();
    ManagementLayout.AddRow(layout,scroll,SizeType.Percent);
    var rows=ScrollRows(scroll);
    var scope=Choice(rows,"Použiť na",group==null?new[]{"Prvok"}:new[]{"Prvok","Skupina"},group==null?0:1);
    scope.Name="visibilityScope";
    var mode=Choice(rows,"Zobraziť",new[]{"Vždy","Ak je pole vyplnené","Ak sa hodnota rovná"},0);
    mode.Name="visibilityMode";
    var source=new ModernComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="visibilitySource"};
    StackField(rows,Loc.T("Zdroj údajov"),source);
    AddArtworkSources(source,template);
    var expected=Input(rows,"Porovnávaná hodnota","");
    expected.Name="visibilityExpected";expected.MaxLength=4000;
    var footer=new UiMessageBox.ActionRow();
    var save=(ModernButton)Btn(Loc.T("Použiť"),()=>dialog.DialogResult=DialogResult.OK);
    var cancel=(ModernButton)Btn(Loc.T("Zrušiť"),()=>dialog.DialogResult=DialogResult.Cancel);
    cancel.DialogResult=DialogResult.Cancel;
    footer.AddAction(save);footer.AddAction(cancel);
    ManagementLayout.AddRow(layout,footer);
    footer.Dock=DockStyle.Top;
    dialog.AcceptButton=save;dialog.CancelButton=cancel;

    void RefreshActions(){
     source.Enabled=mode.SelectedIndex>0;
     expected.Enabled=mode.SelectedIndex==2;
     save.Enabled=mode.SelectedIndex==0||source.SelectedIndex>=0;
    }
    void LoadRule(){
     var rule=scope.SelectedIndex==1?groupRule:meta?.Visibility;
     mode.SelectedIndex=rule==null?0:rule.Mode==VisibilityMode.Filled?1:2;
     source.SelectedIndex=-1;
     for(int i=0;rule!=null&&i<source.Items.Count;i++){
      var item=(ArtworkDataSource)source.Items[i];
      if(item.Field==rule.Field&&item.Variable==rule.Variable){source.SelectedIndex=i;break;}
     }
     expected.Text=rule?.Value??"";
     RefreshActions();
    }
    scope.SelectedIndexChanged+=delegate{LoadRule();};
    mode.SelectedIndexChanged+=delegate{RefreshActions();};
    source.SelectedIndexChanged+=delegate{RefreshActions();};
    LoadRule();
    ApplyColors(dialog);
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    var data=(ArtworkDataSource)source.SelectedItem;
    var result=mode.SelectedIndex==0?null:new VisibilityRule{
     Field=data.Field,Variable=data.Variable,Mode=mode.SelectedIndex==1?VisibilityMode.Filled:VisibilityMode.EqualsValue,
     Value=mode.SelectedIndex==2?expected.Text:null
    };
    var selection=new List<int>(preview.Selection);
    ChangeDocument(label=>{
     if(scope.SelectedIndex==0)Elements.Meta(label,index).Visibility=result;
     else foreach(var member in label.Metadata)if(member.Group==group)member.GroupVisibility=result?.Copy();
    });
    preview.SelectMany(selection,true);
   }
  }
 }
}
