using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  const string BlockExtension=".block.xml";

  void SaveSelectionAsBlock(){
   CommitInspectorIfFocused();
   var selected=SelectedElements();
   var block=LabelBlock.Capture(Read(),selected,Loc.T("Nový blok"));
   using var window=FeatureForm("Uložiť výber ako blok…",430,225);
   var fields=FeatureStack(window);
   var name=Input(fields,"Názov bloku",block.Name);
   name.Name="blockName";name.MaxLength=80;
   AddAccept(window,fields,delegate{
    block.Name=name.Text.Trim();
    if(block.Name.Length==0||block.Name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
     throw new InvalidDataException(Loc.T("Neplatný názov"));
    string fileName=block.Name+BlockExtension;
    try{UpdateFiles.SafeName(fileName);}
    catch(InvalidDataException){throw new InvalidDataException(Loc.T("Neplatný názov"));}
    string path=Path.Combine(workspace.BlocksPath,fileName);
    using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath)){
     if(File.Exists(path))throw new InvalidOperationException(Loc.T("Názov už existuje"));
     block.Save(path);
    }
   });
   window.ShowDialog(this);
  }

  void ShowBlockLibrary(){
   CommitInspectorIfFocused();
   var target=Read();
   using var window=FeatureForm("Knižnica blokov…",800,540);
   window.MinimumSize=new Size(570,410);
   var root=LibraryPage(window);
   var split=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
   split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));
   split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,64));
   root.Controls.Add(split,0,0);
   var list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true,
    Name="blockList",AccessibleName=window.Text};
   split.Controls.Add(list,0,0);
   var details=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12,0,0,0)};
   details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   details.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   split.Controls.Add(details,1,0);
   var description=new Label{AutoSize=true,Dock=DockStyle.Fill};
   details.Controls.Add(description,0,0);
   var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,
    Name="blockPreview",AccessibleName=Loc.T("Náhľad bloku")};
   details.Controls.Add(picture,0,1);
   var footer=new UiMessageBox.ActionRow();
   root.Controls.Add(footer,0,1);
   LabelBlock selected=null;
   var insert=Btn(Loc.T("Vložiť kópiu"),()=>window.DialogResult=DialogResult.OK,HeaderIcon.Library);
   insert.Name="insertBlock";footer.AddAction(insert);
   Button remove=null;
   remove=Btn(Loc.T("Odstrániť blok"),delegate{
    if(list.SelectedItem is BlockLibraryEntry entry&&
     UiMessageBox.Show(window,Loc.T("Odstrániť vybraný blok z knižnice?"),window.Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){
     try{
      using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath))File.Delete(entry.Path);
      RefreshList();
     }catch(Exception ex){Error(ex);}
    }
   });
   footer.AddAction(remove);
   var close=Btn(Loc.T("Zavrieť"),()=>window.DialogResult=DialogResult.Cancel);
   footer.AddAction(close);window.AcceptButton=insert;window.CancelButton=close;

   void RefreshList(){
    list.Items.Clear();
    if(Directory.Exists(workspace.BlocksPath)){
     var paths=Directory.GetFiles(workspace.BlocksPath,"*"+BlockExtension);
     Array.Sort(paths,StringComparer.CurrentCultureIgnoreCase);
     foreach(string path in paths)list.Items.Add(new BlockLibraryEntry(path));
    }
    if(list.Items.Count>0)list.SelectedIndex=0;
    else UpdateSelection();
   }
   void UpdateSelection(){
    selected=null;
    picture.Image?.Dispose();picture.Image=null;
    insert.Enabled=false;remove.Enabled=list.SelectedItem!=null;
    description.Text=Loc.T("Knižnica zatiaľ neobsahuje žiadne bloky.");
    if(list.SelectedItem is not BlockLibraryEntry entry)return;
    try{
     var block=LabelBlock.Load(entry.Path);
     var draft=QuickPrint.PrepareEditor(block.Artwork,currentTime());
     using var scene=PreviewValidation.Build(draft.Label,draft.Issues);
     float width=(float)block.Width,height=(float)block.Height;
     float scale=Math.Min(440/width,310/height);
     // Draw the cropped artwork at its native aspect ratio, even below 10 mm.
     var image=new Bitmap(Math.Max(1,(int)Math.Ceiling(width*scale)),Math.Max(1,(int)Math.Ceiling(height*scale)));
     try{
      using var graphics=Graphics.FromImage(image);
      graphics.Clear(Color.White);
      scene.Draw(graphics,new RectangleF(0,0,(float)scene.Width*scale,(float)scene.Height*scale),false);
      picture.Image=image;
     }catch{image.Dispose();throw;}
     description.Text=block.Name+" · "+string.Format(CultureInfo.CurrentCulture,"{0:0.##} × {1:0.##} mm",block.Width,block.Height);
     if(block.Width>target.Width||block.Height>target.Height)
      description.Text+=Environment.NewLine+Loc.T("Blok presahuje rozmery aktuálnej etikety.");
     if(draft.Issues.Exists(issue=>issue.Blocking))description.Text+=Environment.NewLine+draft.Issues.Find(issue=>issue.Blocking).Message;
     var languages=block.Artwork.Languages?.Codes;
     if(languages!=null){
      var missing=languages.Where(code=>target.Languages?.Codes.Exists(item=>LabelLanguages.SameCode(item,code))!=true).ToArray();
      if(missing.Length>0)description.Text+=Environment.NewLine+Loc.T("Pri vložení sa doplnia jazyky")+": "+string.Join(", ",missing);
     }
     var definitions=block.Artwork.Variables.Where(variable=>!target.Variables.Exists(item=>item.Name==variable.Name)).Select(variable=>variable.Name).ToArray();
     if(definitions.Length>0)description.Text+=Environment.NewLine+Loc.T("Pri vložení sa doplnia premenné")+": "+string.Join(", ",definitions);
     selected=block;insert.Enabled=true;
    }catch(Exception ex){description.Text=Loc.T("Chyba")+": "+ex.Message;}
   }
   list.SelectedIndexChanged+=delegate{UpdateSelection();};
   list.DoubleClick+=delegate{if(insert.Enabled)insert.PerformClick();};
   try{
    RefreshList();ApplyColors(window);
    if(window.ShowDialog(this)==DialogResult.OK&&selected!=null){
     System.Collections.Generic.List<int> indices=null;
     ChangeDocument(label=>indices=selected.Insert(label));
     preview.SelectMany(indices,true);
     preview.Focus();
    }
   }finally{picture.Image?.Dispose();}
  }

  sealed class BlockLibraryEntry(string path){
   public string Path{get;}=path;
   public override string ToString()=>System.IO.Path.GetFileName(Path)[..^BlockExtension.Length];
  }
 }
}
