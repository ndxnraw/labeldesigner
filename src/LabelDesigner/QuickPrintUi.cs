using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  List<int> quickPrintFields=new List<int>();
  sealed class QuickPrintPreview:IDisposable {
   public QuickPrintDraft Draft;
   public Bitmap Image;
   public string Failure;
   public void Dispose(){if(Image!=null)Image.Dispose();}
  }

  List<int> ChooseQuickPrintFields(Form owner,LabelSettings template) {
   var available=QuickPrint.Available(template);
   var selected=new HashSet<int>(QuickPrint.Selected(template));
   using(var dialog=FeatureForm("Upraviteľné polia šablóny…",610,455)){
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    dialog.Controls.Add(layout);
    layout.Controls.Add(new Label{AutoSize=true,MaximumSize=new Size(460,0),Text=Loc.T("Označte údaje, ktoré sa budú meniť pri rýchlej tlači.")},0,0);
    var choices=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,Name="quickPrintFieldChoices",
     AccessibleName=dialog.Text,IntegralHeight=false};
    foreach(int index in available){
     string value=(QuickPrint.Value(template,index)??"").Replace("\r"," ").Replace("\n"," ");
     if(value.Length>42)value=value.Substring(0,42)+"…";
     choices.Items.Add(QuickPrint.Name(template,index)+(value.Length>0?" · "+value:""),selected.Contains(index));
    }
    layout.Controls.Add(choices,0,1);
    var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
    var save=Btn(Loc.T("Použiť"),()=>dialog.DialogResult=DialogResult.OK);
    var cancel=Btn(Loc.T("Zrušiť"),dialog.Close);
    actions.Controls.Add(save);actions.Controls.Add(cancel);
    layout.Controls.Add(actions,0,2);
    dialog.AcceptButton=save;dialog.CancelButton=cancel;
    ApplyColors(dialog);
    return dialog.ShowDialog(owner)==DialogResult.OK?choices.CheckedIndices.Cast<int>().Select(position=>available[position]).ToList():null;
   }
  }

  void ConfigureQuickPrintFields() {
   CommitInspectorIfFocused();
   var fields=ChooseQuickPrintFields(this,Read());
   if(fields!=null&&!quickPrintFields.SequenceEqual(fields))ChangeDocument(label=>label.QuickPrintFields=fields);
  }

  void ConfigureLibraryQuickPrint(Form owner,string path) {
   LabelSettings template;
   string hash;
   using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath)){
    hash=UpdateFiles.Hash(path);
    template=Engine.Load(path);
   }
   var fields=ChooseQuickPrintFields(owner,template);
   if(fields==null||QuickPrint.Selected(template).SequenceEqual(fields))return;
   using(WorkspaceBackup.LockDirectory(workspace.DirectoryPath)){
    if(UpdateFiles.Hash(path)!=hash)throw new IOException(Loc.T("Šablóna sa medzitým zmenila. Otvorte ju znova."));
    template.QuickPrintFields=fields;
    Engine.Save(path,template);
   }
  }

  static Bitmap QuickPrintImage(QuickPrintDraft draft) {
   const int dimension=520;
   var label=draft.Label;
   double scale=dimension/Math.Max(label.Width,label.Height);
   var bitmap=new Bitmap(Math.Max(1,(int)Math.Round(label.Width*scale)),Math.Max(1,(int)Math.Round(label.Height*scale)));
   try{
    var issues=draft.Issues.FindAll(issue=>issue.Element<0||issue.Message!=Loc.T("Text sa nezmestí do poľa"));
    using(var graphics=Graphics.FromImage(bitmap))using(var scene=PreviewValidation.Build(label,issues)){
     graphics.Clear(Color.White);
     scene.Draw(graphics,new RectangleF(0,0,bitmap.Width,bitmap.Height),false);
    }
    return bitmap;
   }catch{bitmap.Dispose();throw;}
  }

  void ShowQuickPrint(Form owner,LabelSettings template) {
   var instant=DateTime.Now;
   using(var dialog=FeatureForm("Rýchle vyplnenie a tlač…",945,650))
   using(var changes=new Timer{Interval=180}){
    dialog.MinimumSize=new Size(640,415);
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(12)};
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46));
    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    dialog.Controls.Add(layout);
    var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(0,0,12,0)};
    var fields=Stack();scroll.Controls.Add(fields);layout.Controls.Add(scroll,0,0);
    var editors=new Dictionary<int,TextBox>();
    var errors=new Dictionary<int,Label>();
    foreach(int index in QuickPrint.Selected(template)){
     bool aiLines=QuickPrint.UsesAiLines(template,index);
     bool multiline=aiLines||index>=0;
     var editor=new TextBox{Text=QuickPrint.Value(template,index)??"",Name="quickPrintValue"+index,MaxLength=4000,
      Multiline=multiline,AcceptsReturn=multiline,Height=multiline?54:24,ScrollBars=multiline?ScrollBars.Vertical:ScrollBars.None};
     StackField(fields,QuickPrint.Name(template,index),editor);
     if(aiLines)StackAdd(fields,new Label{AutoSize=true,MaximumSize=new Size(330,0),Text=Loc.T("Zadajte každý identifikátor na samostatný riadok vo formáte (AI)hodnota.")});
     var hint=new Label{AutoSize=true,MaximumSize=new Size(330,0),Visible=false,Name="quickPrintError"+index};
     StackAdd(fields,hint);
     editors.Add(index,editor);errors.Add(index,hint);
    }
    if(editors.Count==0)StackAdd(fields,new Label{AutoSize=true,MaximumSize=new Size(315,0),Text=Loc.T("Šablóna nemá vybrané upraviteľné polia.")});
    var quantity=Input(fields,"Počet etikiet",(template.Layout.Enabled?template.Layout.Count:template.Copies).ToString(CultureInfo.InvariantCulture));
    quantity.Name="quickPrintQuantity";
    var quantityError=new Label{AutoSize=true,Name="quickPrintQuantityError",Visible=false};
    StackAdd(fields,quantityError);
    Func<int> count=delegate{
     int parsed;
     return int.TryParse(quantity.Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out parsed)&&
      parsed>=1&&parsed<=OutputPlan.MaxLabels?parsed:0;
    };
    var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Name="quickPrintPreview",
     AccessibleName=Loc.T("Náhľad etikety")};
    layout.Controls.Add(picture,1,0);
    var footer=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=1};
    layout.Controls.Add(footer,0,1);layout.SetColumnSpan(footer,2);
    var status=new Label{AutoSize=true,MaximumSize=new Size(560,0),Name="quickPrintStatus"};
    footer.Controls.Add(status,0,0);
    QuickPrintDraft draft=null;
    bool previewReady=false;
    var deliveryGate=new object();
    QuickPrintPreview pending=null;
    bool closed=false;
    int revision=0;
    var proceed=Btn(Loc.T("Pokračovať na tlač…"),delegate{
     int requested=count();
     if(!previewReady||draft==null||!draft.CanPrint||requested==0)return;
     try{
      var plan=draft.CreatePlan(requested);
      using(var print=CreateJobPrintWindow(draft.Label,plan,issue=>{
       TextBox field;
       if(editors.TryGetValue(issue.Element,out field))field.Focus();
      })){
       ((PrintPanel)print.Controls[0]).LimitToSingleRun();
       print.ShowDialog(dialog);
      }
     }catch(Exception ex){status.Text=ex.Message;}
    });
    proceed.Name="quickPrintContinue";
    var close=Btn(Loc.T("Zavrieť"),dialog.Close);
    var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
    actions.Controls.Add(proceed);actions.Controls.Add(close);
    footer.Controls.Add(actions,0,1);
    dialog.AcceptButton=proceed;dialog.CancelButton=close;
    Action updateContinue=delegate{
     bool valid=count()>0;
     quantityError.Text=valid?"":Loc.T("Zadajte celý počet etikiet od 1 do 10000.");
     quantityError.Visible=!valid;
     proceed.Enabled=valid&&previewReady&&draft!=null&&draft.CanPrint;
    };
    quantity.TextChanged+=delegate{updateContinue();};
    Action clearImage=delegate{
     var previous=picture.Image;
     picture.Image=null;
     if(previous!=null)previous.Dispose();
    };
    Action<QuickPrintPreview> apply=preview=>{
     clearImage();
     foreach(var error in errors.Values){error.Text="";error.Visible=false;}
      draft=preview.Draft;
      if(draft==null){status.Text=preview.Failure;return;}
      foreach(var issue in draft.Issues){
       Label error;
       if(!errors.TryGetValue(Elements.SourceIndex(draft.Label,issue.Element),out error)||(!issue.Blocking&&error.Text.Length>0))continue;
       error.Text=issue.Message;
       error.Visible=true;
      }
      var general=draft.Issues.Find(issue=>issue.Blocking&&!errors.ContainsKey(Elements.SourceIndex(draft.Label,issue.Element)));
      status.Text=general!=null?general.Message:!draft.CanPrint?Loc.T("Opravte údaje pred tlačou."):
       draft.Issues.Count>0?Loc.T("Upozornenia skontrolujte v tlačovom náhľade."):Loc.T("Náhľad etikety");
      picture.Image=preview.Image;
      preview.Image=null;
      previewReady=picture.Image!=null;
      if(preview.Failure!=null)status.Text=preview.Failure;
      updateContinue();
    };
    Action refresh=delegate{
     changes.Stop();
     previewReady=false;
     updateContinue();
     status.Text=Loc.T("Pripravujem náhľad…");
     var values=editors.ToDictionary(item=>item.Key,item=>item.Value.Text);
     int ticket;
     lock(deliveryGate){ticket=revision;}
     PrintWorker.Queue(dialog,delegate{
      var result=new QuickPrintPreview();
      try{
       result.Draft=QuickPrint.Prepare(template,values,instant);
       result.Image=QuickPrintImage(result.Draft);
      }catch(Exception ex){result.Failure=ex.Message;}
      lock(deliveryGate){
       if(closed||revision!=ticket){result.Dispose();return;}
       if(pending!=null)pending.Dispose();
       pending=result;
      }
      try{
       dialog.BeginInvoke((Action)delegate{
        lock(deliveryGate){
         if(pending!=result)return;
         pending=null;
         if(closed||revision!=ticket){result.Dispose();return;}
        }
        using(result)apply(result);
       });
      }catch(InvalidOperationException){
       lock(deliveryGate){if(pending==result){pending=null;result.Dispose();}}
      }
     });
    };
    changes.Tick+=delegate{refresh();};
    foreach(var editor in editors.Values)editor.TextChanged+=delegate{
     lock(deliveryGate){revision++;}
     previewReady=false;
     updateContinue();
     status.Text=Loc.T("Pripravujem náhľad…");
     changes.Stop();changes.Start();
    };
    ApplyColors(dialog);
    dialog.Shown+=delegate{refresh();};
    try{dialog.ShowDialog(owner);}finally{
     changes.Stop();
     lock(deliveryGate){closed=true;if(pending!=null){pending.Dispose();pending=null;}}
     PrintWorker.Cancel(dialog);
     clearImage();
    }
   }
  }
 }
}
