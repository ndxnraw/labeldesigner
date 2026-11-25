using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  void ShowLibrary() {
   if(workspace==null)return;
   using var library=FeatureForm("Šablóny a nedávne projekty",820,620);
   using var images=new ImageList{ImageSize=new Size(160,110),ColorDepth=ColorDepth.Depth32Bit};
   using var management=new UiContextMenuStrip();
   library.MinimumSize=new Size(620,420);
   var tabs=new ModernTabs{Dock=DockStyle.Fill,Padding=new Padding(12),Name="templateLibraryTabs",AccessibleName=library.Text};
   library.Controls.Add(tabs);
   var templates=LibraryPage(tabs.AddPage(Loc.T("Šablóny")));
   var projects=LibraryPage(tabs.AddPage(Loc.T("Nedávne projekty")));
   var list=new ListView{
    Dock=DockStyle.Fill,View=View.LargeIcon,LargeImageList=images,MultiSelect=false,
    HideSelection=false,ShowItemToolTips=true,Name="templateLibraryList",AccessibleName=Loc.T("Šablóny")
   };
   templates.Controls.Add(list,0,0);
   var recent=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true,AccessibleName=Loc.T("Nedávne projekty")};
   projects.Controls.Add(recent,0,0);
   foreach(string path in workspace.Options.Recent)recent.Items.Add(path);

   var thumbnails=new List<Bitmap>();
   string SelectedTemplate()=>list.SelectedItems.Count==1?(string)list.SelectedItems[0].Tag:null;
   void RefreshTemplates() {
    list.BeginUpdate();
    try{
     list.Items.Clear();
     images.Images.Clear();
     foreach(var old in thumbnails)old.Dispose();
     thumbnails.Clear();
     string[] files=Directory.GetFiles(workspace.TemplatesPath,"*.ean.xml");
     Array.Sort(files,StringComparer.CurrentCultureIgnoreCase);
     foreach(string file in files){
      try{
       var settings=Engine.Load(file);
       using var scene=VectorArt.Build(settings);
       var thumbnail=new Bitmap(160,110);
       thumbnails.Add(thumbnail);
       using(var graphics=Graphics.FromImage(thumbnail)){
        graphics.Clear(Color.White);
        float scale=Math.Min(156/(float)settings.Width,106/(float)settings.Height);
        float width=(float)settings.Width*scale,height=(float)settings.Height*scale;
        scene.Draw(graphics,new RectangleF((160-width)/2,(110-height)/2,width,height),false);
       }
       images.Images.Add(thumbnail);
       list.Items.Add(new ListViewItem(Path.GetFileName(file)[..^8],images.Images.Count-1){Tag=file});
      }catch(Exception ex){
       list.Items.Add(new ListViewItem(Path.GetFileName(file)+" — "+Loc.T("Chyba")){Tag=file,ToolTipText=ex.Message});
      }
     }
    }finally{list.EndUpdate();}
   }

   void QuickPrintSelected() {
    string path=SelectedTemplate();
    if(path==null)return;
    try{ShowQuickPrint(library,Engine.Load(path));}catch(Exception ex){Error(ex);}
   }
   void OpenTemplate() {
    string path=SelectedTemplate();
    if(path==null||!CanDiscard())return;
    try{
     Apply(Engine.Load(path));settingsPath=null;ResetHistory(false);Changed(this,EventArgs.Empty);library.Close();
    }catch(Exception ex){Error(ex);}
   }
   void RemoveTemplate() {
    string path=SelectedTemplate();
    if(path==null||UiMessageBox.Show(library,Loc.T("Odstrániť šablónu?"),Loc.T("Šablóny"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    try{
     using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath))File.Delete(path);
     RefreshTemplates();
    }catch(Exception ex){Error(ex);}
   }
   void OpenRecent() {
    if(recent.SelectedItem is not string path)return;
    if(!File.Exists(path)){
     UiMessageBox.Show(library,Loc.T("Súbor sa nenašiel")+": "+path,Loc.T("Nedávne projekty"));
     workspace.Options.Recent.Remove(path);
     workspace.SaveOptions();
     recent.Items.Remove(path);
     return;
    }
    if(!CanDiscard())return;
    try{OpenImportedFile(path);library.Close();}catch(Exception ex){Error(ex);}
   }

   var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true};
   templates.Controls.Add(actions,0,1);
   var quick=Btn(Loc.T("Vyplniť a tlačiť…"),QuickPrintSelected);
   quick.Name="openTemplateQuickPrint";
   quick.MinimumSize=new Size(150,38);
   list.ItemActivate+=delegate{QuickPrintSelected();};
   var edit=Btn(Loc.T("Upraviť šablónu"),OpenTemplate);
   var manage=Btn(Loc.T("Správa šablóny"),()=>{});
   manage.AccessibleRole=AccessibleRole.ButtonMenu;
   manage.Click+=delegate{management.Show(manage,0,manage.Height);};
   management.Renderer=new StudioFeatureRenderer(dark);
   management.ForeColor=StudioTheme.Text(dark);
   management.Items.Add(Loc.T("Upraviteľné polia šablóny…"),null,delegate{
    string path=SelectedTemplate();
    if(path==null)return;
    try{ConfigureLibraryQuickPrint(library,path);}catch(Exception ex){Error(ex);}
   });
   management.Items.Add(Loc.T("Odstrániť šablónu"),null,delegate{RemoveTemplate();});
   void UpdateTemplateActions(){quick.Enabled=edit.Enabled=manage.Enabled=SelectedTemplate()!=null;}
   list.SelectedIndexChanged+=delegate{UpdateTemplateActions();};
   actions.Controls.AddRange(new Control[]{quick,edit,manage,Btn(Loc.T("Uložiť ako šablónu"),()=>SaveLibraryTemplate(library,RefreshTemplates))});

   var openRecent=Btn(Loc.T("Otvoriť nedávny projekt"),OpenRecent);
   openRecent.Enabled=false;
   recent.SelectedIndexChanged+=delegate{openRecent.Enabled=recent.SelectedItem!=null;};
   recent.DoubleClick+=delegate{OpenRecent();};
   projects.Controls.Add(openRecent,0,1);

   try{
    using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath)){
     if(Directory.GetFiles(workspace.TemplatesPath,"*.ean.xml").Length==0){
      foreach(double width in new[]{60d,100d}){
       var settings=StartupLayout.Create(new StartupProfile{Width=width,Height=width==60?45:60});
       ProjectXml.Save(Path.Combine(workspace.TemplatesPath,"Etiketa "+width+".ean.xml"),settings);
      }
     }
    }
    RefreshTemplates();
    UpdateTemplateActions();
    ApplyColors(library);
    if(!SystemInformation.HighContrast){
     quick.BackColor=StudioTheme.Accent;
     quick.ForeColor=Color.White;
     quick.FlatAppearance.BorderColor=StudioTheme.Accent;
    }
    library.ShowDialog(this);
   }catch(Exception ex){Error(ex);}
   finally{foreach(var thumbnail in thumbnails)thumbnail.Dispose();}
  }

  static TableLayoutPanel LibraryPage(Panel page) {
   page.AutoScroll=false;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(5)};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
   page.Controls.Add(layout);
   return layout;
  }

  void SaveLibraryTemplate(Form owner,Action refresh) {
   using var dialog=FeatureForm("Názov šablóny",400,220);
   var stack=FeatureStack(dialog);
   var input=Input(stack,"Názov šablóny",Loc.T("Moja etiketa"));
   AddAccept(dialog,stack,delegate{
    if(string.IsNullOrWhiteSpace(input.Text)||input.Text.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
     throw new InvalidOperationException(Loc.T("Neplatný názov"));
    string path=Path.Combine(workspace.TemplatesPath,input.Text.Trim()+".ean.xml");
    var settings=Read();
    using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath)){
     if(File.Exists(path))throw new InvalidOperationException(Loc.T("Názov už existuje"));
     Engine.Save(path,settings);
    }
    refresh();
   });
   dialog.ShowDialog(owner);
  }
 }
}
