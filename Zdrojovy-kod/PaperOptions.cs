using System;using System.Windows.Forms;
namespace EanEtikety {
public static class PaperOptions {
 public static readonly string[] Ids={"Label","A3","A4","A5","A6","Letter","Legal","Custom"};
 public static readonly double[] Width={0,297,210,148,105,215.9,215.9,0},Height={0,420,297,210,148,279.4,355.6,0};
}
public partial class MainForm {
 ComboBox paperPreset=new ModernComboBox();
 void SetupPaper(TableLayoutPanel page){ComboStyle(paperPreset);paperPreset.DropDownStyle=ComboBoxStyle.DropDownList;paperPreset.Items.AddRange(new object[]{Loc.T("Podľa etikety"),"A3 (297 × 420 mm)","A4 (210 × 297 mm)","A5 (148 × 210 mm)","A6 (105 × 148 mm)","Letter (215.9 × 279.4 mm)","Legal (215.9 × 355.6 mm)",Loc.T("Vlastné rozmery")});AddRow(page,Loc.T("Formát papiera"),paperPreset);
  AddNum(page,Loc.T("Šírka papiera (mm)"),"PageWidth",10,500,210);AddNum(page,Loc.T("Výška papiera (mm)"),"PageHeight",10,500,297);
  AddNum(page,Loc.T("Ľavý okraj (mm)"),"MarginX",0,250,10);AddNum(page,Loc.T("Pravý okraj (mm)"),"MarginRight",0,250,10);AddNum(page,Loc.T("Horný okraj (mm)"),"MarginY",0,250,10);AddNum(page,Loc.T("Dolný okraj (mm)"),"MarginBottom",0,250,10);
  var info=new Label{AutoSize=true,MaximumSize=new System.Drawing.Size(375,0),Text=Loc.T("Jedna etiketa na strane. Počet kópií nastavte v okne tlače. Okraje platia pre papier, export PDF a Word."),Margin=new Padding(3,14,3,3)};page.Controls.Add(info);page.SetColumnSpan(info,2);
  paperPreset.SelectedIndexChanged+=delegate{if(loading)return;SetPaperState(true);Changed(this,EventArgs.Empty);};nums["Width"].ValueChanged+=delegate{if(!loading&&paperPreset.SelectedIndex==0)SetPaperState(true);};nums["Height"].ValueChanged+=delegate{if(!loading&&paperPreset.SelectedIndex==0)SetPaperState(true);};
 }
 void LoadPaper(LabelSettings s){int i=Array.IndexOf(PaperOptions.Ids,s.PaperPreset);if(s.Sheet)i=7;if(i<0)i=s.UsePaper?7:0;if(s.UsePaper&&i==0)i=7;paperPreset.SelectedIndex=i;SetPaperState(true);}
 void SetPaperState(bool dimensions){bool before=loading;loading=true;try{int i=Math.Max(0,paperPreset.SelectedIndex);nums["PageWidth"].Enabled=nums["PageHeight"].Enabled=i==7;if(dimensions&&i!=7){nums["PageWidth"].Value=(decimal)Display(i==0?N("Width"):PaperOptions.Width[i]);nums["PageHeight"].Value=(decimal)Display(i==0?N("Height"):PaperOptions.Height[i]);}foreach(string key in new[]{"MarginX","MarginRight","MarginY","MarginBottom"})nums[key].Enabled=i!=0;}finally{loading=before;}}
}
}
