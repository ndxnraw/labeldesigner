using System;using System.Collections.Generic;using System.Drawing;using System.Windows.Forms;
namespace EanEtikety {
 public partial class MainForm {
  ModernTabs workspaceTabs;IssueView problemView;TableLayoutPanel selectionInspector;Label selectionOverview;readonly List<Button> selectionActions=new List<Button>();
  void InitializeEditorWorkspace(TableLayoutPanel stack,Panel left,ModernTabs documentTabs){
   workspaceTabs=new ModernTabs{Dock=DockStyle.Fill};var layers=workspaceTabs.AddPage(Loc.T("Prvky"));var problems=workspaceTabs.AddPage(Loc.T("Kontrola"));
   var layerLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,AutoScroll=true};layerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layers.Controls.Add(layerLayout);
   elementList=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,IntegralHeight=false,SelectionMode=SelectionMode.MultiExtended,HorizontalScrollbar=true,AccessibleName=Loc.T("Prvky")};layerLayout.Controls.Add(elementList,0,0);
   var actions=new TableLayoutPanel{Name="layerActions",Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0)};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));layerLayout.Controls.Add(actions,0,1);
   AddLayerAction(actions,"Do popredia",()=>Arrange("front"));AddLayerAction(actions,"Do pozadia",()=>Arrange("back"));AddLayerAction(actions,"Zamknúť / odomknúť",ToggleSelectionLock);AddLayerAction(actions,"Upraviť prvok",EditSelection);
   FitLayerActions(layerLayout,actions);
   elementList.SelectedIndexChanged+=delegate{if(!studioSync)SelectFromLayerList();};elementList.DoubleClick+=delegate{EditSelection();};
   problemView=new IssueView{Dock=DockStyle.Fill};problemView.SelectedIssueChanged+=SelectEditorIssue;problems.Controls.Add(problemView);
   StackAdd(stack,workspaceTabs);workspaceTabs.Dock=DockStyle.Fill;left.AutoScroll=false;stack.AutoSize=false;stack.Dock=DockStyle.Fill;stack.RowStyles[0]=new RowStyle(SizeType.Percent,55);stack.RowStyles[1]=new RowStyle(SizeType.Percent,45);documentTabs.Dock=DockStyle.Fill;
  }
  void AddLayerAction(TableLayoutPanel panel,string text,Action action){var button=Btn(Loc.T(text),delegate{try{action();}catch(Exception ex){Error(ex);}});button.AutoSize=false;button.Dock=DockStyle.Fill;button.Margin=new Padding(2);button.MinimumSize=Size.Empty;button.AccessibleName=Loc.T(text);int index=panel.Controls.Count;panel.Controls.Add(button,index%2,index/2);}
  void InitializeSelectionInspector(){selectionInspector=Stack();inspectorHost.Controls.Add(selectionInspector);selectionOverview=new FocusText{AutoSize=true,UseMnemonic=false,Font=new Font(Font,FontStyle.Bold)};StackAdd(selectionInspector,selectionOverview);
   string[] names={"Zoskupiť","Rozoskupiť","Rozloženie a výber","Odstrániť prvok"};
   for(int i=0;i<names.Length;i++){int command=i;var button=Btn(Loc.T(names[i]),delegate{try{if(command==0)Arrange("group");else if(command==1)Arrange("ungroup");else if(command==2)ShowArrange();else RemoveSelected();}catch(Exception ex){Error(ex);}});selectionActions.Add(button);StackAdd(selectionInspector,button);}
  }
  void SyncElementList(){
   var items=new List<StudioItem>();if(!hideBarcode)items.Add(new StudioItem{Index=-1,Name=MoreBarcodes.Name(MoreBarcodes.Kinds[Math.Max(0,kind.SelectedIndex)])});for(int i=0;i<grid.Rows.Count;i++)items.Add(new StudioItem{Index=i,Name=Convert.ToString(grid.Rows[i].Cells[0].Value)});for(int i=0;i<symbols.Count;i++)items.Add(new StudioItem{Index=-3-i,Name=symbols[i].Name+" "+(i+1)});
   Func<int,int> layer=index=>{var meta=elementMetadata.Find(m=>m.Index==index);return meta==null?(index==-1?50:index>=0?index:100-index):meta.Layer;};
   Func<int,int> paintOrder=index=>index==-1?grid.Rows.Count:index>=0?index:grid.Rows.Count+1+(-3-index);
   items.Sort((a,b)=>{int order=layer(b.Index).CompareTo(layer(a.Index));return order==0?paintOrder(b.Index).CompareTo(paintOrder(a.Index)):order;});
   foreach(var item in items){var meta=elementMetadata.Find(m=>m.Index==item.Index);if(meta!=null&&meta.Locked)item.Name="["+Loc.T("Zamknuté")+"] "+item.Name;if(meta!=null&&!string.IsNullOrEmpty(meta.Group))item.Name="["+Loc.T("Skupina")+"] "+item.Name;}
   bool rebuild=items.Count!=elementList.Items.Count;
   if(!rebuild)for(int i=0;i<items.Count;i++){var old=(StudioItem)elementList.Items[i];if(old.Index!=items[i].Index||old.Name!=items[i].Name){rebuild=true;break;}}
   // Keep the native range-selection anchor and scroll position on selection-only updates.
   int top=elementList.TopIndex;elementList.BeginUpdate();try{if(rebuild){elementList.Items.Clear();elementList.Items.AddRange(items.ToArray());}for(int i=0;i<items.Count;i++){bool selected=preview.Selection.Contains(items[i].Index);if(elementList.GetSelected(i)!=selected)elementList.SetSelected(i,selected);}if(rebuild&&items.Count>0)elementList.TopIndex=Math.Max(0,Math.Min(top,items.Count-1));}finally{elementList.EndUpdate();}
   elementList.AccessibleDescription=Loc.T("Vybrané prvky")+": "+preview.Selection.Count;
  }
  void SelectFromLayerList(){
   var selected=new List<int>();foreach(StudioItem item in elementList.SelectedItems)selected.Add(item.Index);
   if((Control.ModifierKeys&(Keys.Control|Keys.Shift))==0&&selected.Count==1){preview.SelectElement(selected[0]);return;}
   var removedGroups=new HashSet<string>();if((Control.ModifierKeys&(Keys.Control|Keys.Shift))!=0)foreach(int previous in preview.Selection)if(!selected.Contains(previous)){var meta=elementMetadata.Find(m=>m.Index==previous);if(meta!=null&&!string.IsNullOrEmpty(meta.Group))removedGroups.Add(meta.Group);}
   selected.RemoveAll(index=>{var meta=elementMetadata.Find(m=>m.Index==index);return meta!=null&&removedGroups.Contains(meta.Group??"");});
   var groups=new HashSet<string>();foreach(int index in selected){var meta=elementMetadata.Find(m=>m.Index==index);if(meta!=null&&!string.IsNullOrEmpty(meta.Group))groups.Add(meta.Group);}foreach(var meta in elementMetadata)if(groups.Contains(meta.Group??"")&&!selected.Contains(meta.Index))selected.Add(meta.Index);
   int primary=selected.Find(i=>!preview.Selection.Contains(i));if(selected.Contains(preview.SelectedIndex))primary=preview.SelectedIndex;if(selected.Contains(primary)){selected.Remove(primary);selected.Insert(0,primary);}preview.SelectMany(selected,true);
  }
  bool ShowSelectionInspector(){int count=preview.Selection.Count;bool show=count!=1||preview.SelectedIndex==-2;selectionInspector.Visible=show;if(!show)return false;selectionOverview.Text=count==0?Loc.T("Nie je vybraný prvok"):Loc.T("Vybrané prvky")+": "+count;foreach(var button in selectionActions)button.Enabled=count>0;codeInspector.Visible=textInspector.Visible=false;return true;}
  void ToggleSelectionLock(){var selected=SelectedElements();bool unlock=selected.Count>0&&selected.TrueForAll(index=>elementMetadata.Exists(m=>m.Index==index&&m.Locked));Arrange(unlock?"unlock":"lock");}
  void EditSelection(){if(preview.Selection.Count!=1)return;int index=preview.SelectedIndex;if(elementMetadata.Exists(m=>m.Index==index&&m.Locked))return;if(index==-1){FocusInspector();return;}if(index>=0)BeginInlineText(index);else if(index<=-3)EditSymbol(index);}
  void SelectEditorIssue(LabelIssue issue){if(issue==null||issue.Element==-2)return;preview.SelectElement(issue.Element);preview.ProblemElements.Clear();preview.ProblemElements.Add(issue.Element);preview.Invalidate();}
  void SyncIssueViews(){if(problemView==null)return;problemView.SetIssues(labelIssues);workspaceTabs.SetTitle(1,Loc.T("Kontrola")+" ("+labelIssues.Count+")");}
  void FocusProblems(){if(workspaceTabs==null)return;if(studioBody.ColumnStyles[0].Width==0)SetStudioPanel(0,true);workspaceTabs.SelectedIndex=1;problemView.FocusList();}
  void FocusInspector(){if(studioBody.ColumnStyles[2].Width==0)SetStudioPanel(2,true);if(preview.Selection.Count!=1){selectionOverview.Focus();return;}if(preview.SelectedIndex==-1)ean.Focus();else if(preview.SelectedIndex>=0)inspectText.Focus();else inspectNumbers[0].Focus();}
  void CycleEditorSection(bool backwards){
   if(studioBody==null)return;var left=studioBody.GetControlFromPosition(0,0);Control[] sections={left,preview,inspectorHost};int current=preview.ContainsFocus?1:inspectorHost.ContainsFocus?2:left.ContainsFocus?0:backwards?0:2;
   for(int step=1;step<=sections.Length;step++){int next=(current+(backwards?sections.Length-step:step))%sections.Length;if(next==0&&studioBody.ColumnStyles[0].Width==0||next==2&&studioBody.ColumnStyles[2].Width==0)continue;var target=sections[next];if(next==1){if(preview.Focus())return;}else if(target.SelectNextControl(null,true,true,true,false))return;}
  }
 }
}
