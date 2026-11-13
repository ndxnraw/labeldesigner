using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 public sealed class IssueView:UserControl {
  readonly ComboBox filter=new ModernComboBox();readonly ListBox list=new ListBox();readonly Label counts=new Label();List<LabelIssue> issues=new List<LabelIssue>();bool refreshing;
  public event Action<LabelIssue> SelectedIssueChanged;
  public LabelIssue SelectedIssue{get{return list.SelectedItem as LabelIssue;}}
  public IssueView(){
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
   counts.AutoSize=true;counts.Dock=DockStyle.Top;counts.UseMnemonic=false;layout.Controls.Add(counts);filter.DropDownStyle=ComboBoxStyle.DropDownList;filter.Items.AddRange(new object[]{Loc.T("Všetky problémy"),Loc.T("Chyby"),Loc.T("Upozornenia")});filter.SelectedIndex=0;filter.Dock=DockStyle.Top;filter.AccessibleName=Loc.T("Filter problémov");layout.Controls.Add(filter);
   list.Dock=DockStyle.Fill;list.HorizontalScrollbar=true;list.IntegralHeight=false;list.AccessibleName=Loc.T("Kontrola etikety");layout.Controls.Add(list);filter.SelectedIndexChanged+=delegate{RefreshItems();};list.SelectedIndexChanged+=delegate{if(!refreshing&&SelectedIssueChanged!=null)SelectedIssueChanged(SelectedIssue);};
  }
  public void SetIssues(List<LabelIssue> values){issues=new List<LabelIssue>(values);RefreshItems();}
  void RefreshItems(){var selected=SelectedIssue;refreshing=true;list.BeginUpdate();try{
   int errors=issues.FindAll(i=>i.Blocking).Count;counts.Text=Loc.T("Chyby")+": "+errors+"  •  "+Loc.T("Upozornenia")+": "+(issues.Count-errors);list.Items.Clear();
   foreach(var issue in issues)if(filter.SelectedIndex==0||filter.SelectedIndex==1&&issue.Blocking||filter.SelectedIndex==2&&!issue.Blocking){list.Items.Add(issue);if(selected!=null&&selected.Element==issue.Element&&selected.Record==issue.Record&&selected.Message==issue.Message)list.SelectedItem=issue;}
   if(list.Items.Count==0)list.Items.Add(Loc.T(issues.Count==0?"Bez problémov":"Žiadne problémy pre tento filter"));
   list.AccessibleDescription=counts.Text;
  }finally{list.EndUpdate();refreshing=false;}}
  public void FocusList(){list.Focus();}
 }
 public sealed class OutputReviewDialog:Form {
  readonly PreflightReport report;readonly CheckBox acknowledge=new CheckBox();readonly ModernButton proceed=new ModernButton();readonly IssueView issues=new IssueView();
  public LabelIssue EditIssue{get;private set;}
  public OutputReviewDialog(PreflightReport value,int copies,string action,string target,Font font,Icon icon){
   report=value;Text=Loc.T("Kontrola výstupu");Font=font;Icon=icon;ClientSize=new Size(760,600);MinimumSize=new Size(560,480);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=5};root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(root);
   root.Controls.Add(new Label{AutoSize=true,UseMnemonic=false,Text=action+"  •  "+target,MaximumSize=new Size(700,0)});
   root.Controls.Add(new Label{AutoSize=true,UseMnemonic=false,Text=new OutputSummary(value.Plan,copies).ToString(),Padding=new Padding(0,8,0,8)});
   issues.Dock=DockStyle.Fill;issues.SetIssues(value.Issues);root.Controls.Add(issues);acknowledge.Text=Loc.T("Upozornenia som skontroloval(a)");acknowledge.AutoSize=true;acknowledge.Visible=value.HasWarnings;acknowledge.CheckedChanged+=delegate{proceed.Enabled=report.CanProceed(acknowledge.Checked);};root.Controls.Add(acknowledge);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};root.Controls.Add(actions);
   proceed.Text=action;proceed.AutoSize=true;proceed.Enabled=value.CanProceed(false);proceed.DialogResult=DialogResult.OK;actions.Controls.Add(proceed);AcceptButton=proceed;
   var cancel=new ModernButton{Text=Loc.T("Zrušiť"),AutoSize=true,DialogResult=DialogResult.Cancel};actions.Controls.Add(cancel);CancelButton=cancel;
   var edit=new ModernButton{Text=Loc.T("Späť na úpravy"),AutoSize=true};edit.Click+=delegate{EditIssue=issues.SelectedIssue;DialogResult=DialogResult.Cancel;Close();};actions.Controls.Add(edit);
  }
  protected override void OnFormClosing(FormClosingEventArgs e){if(DialogResult==DialogResult.OK&&!report.CanProceed(acknowledge.Checked)){e.Cancel=true;DialogResult=DialogResult.None;}base.OnFormClosing(e);}
 }
 public partial class MainForm {
  bool ReviewOutput(IWin32Window owner,PreflightReport report,int copies,string action,string target,Action<LabelIssue> navigate){
   using(var dialog=new OutputReviewDialog(report,copies,Loc.T(action),target,Font,Icon)){ApplyColors(dialog);bool accepted=dialog.ShowDialog(owner)==DialogResult.OK;if(!accepted&&dialog.EditIssue!=null&&navigate!=null)navigate(dialog.EditIssue);return accepted;}
  }
 }

}
