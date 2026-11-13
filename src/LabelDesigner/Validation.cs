using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace EanEtikety {
public partial class MainForm {
 List<LabelIssue> labelIssues=new List<LabelIssue>();Func<DateTime> currentTime=delegate{return DateTime.Now;};DateTime previewDay;
 readonly System.Windows.Forms.Timer datePreviewTimer=new System.Windows.Forms.Timer{Interval=30000};
 LabelSettings PreparedLabel(LabelSettings s){var instant=currentTime();previewDay=instant.Date;return s.Variables.Count==0?s:JobPreparation.Resolve(s,instant);}
 void RefreshDatePreview(){if(!loading&&!preview.IsDragging&&currentTime().Date!=previewDay&&variables.Exists(v=>v.Type=="Date"&&!v.FixedDate))UpdatePreview();}
 void ShowChecks(){UpdatePreview();using(var window=FeatureForm("Kontrola etikety",760,480)){var view=new IssueView{Dock=DockStyle.Fill};view.SetIssues(labelIssues);view.SelectedIssueChanged+=SelectEditorIssue;window.Controls.Add(view);window.FormClosed+=delegate{UpdatePreview();};ApplyColors(window);window.ShowDialog(this);}}
}
}
