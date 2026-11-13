using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;


namespace EanEtikety {
 public sealed class OutputSummary {
  public readonly int Records,Labels,Pages,Copies;public readonly long TotalLabels,TotalPages;
  public OutputSummary(OutputPlan plan,int copies){if(plan==null)throw new ArgumentNullException("plan");if(copies<1)throw new ArgumentOutOfRangeException("copies");Records=plan.RecordCount;Labels=plan.Placements.Count;Pages=plan.PageCount;Copies=copies;TotalLabels=checked((long)Labels*copies);TotalPages=checked((long)Pages*copies);}
  public override string ToString(){return Loc.T("Záznamy")+": "+Records+"\r\n"+Loc.T("Etikety v úlohe")+": "+Labels+"\r\n"+Loc.T("Strany v úlohe")+": "+Pages+"\r\n"+Loc.T("Opakovania celej úlohy")+": "+Copies+"\r\n"+Loc.T("Etikety spolu")+": "+TotalLabels+"\r\n"+Loc.T("Strany spolu")+": "+TotalPages;}
 }
 public sealed class PreflightReport {
  public readonly OutputPlan Plan;public readonly List<LabelIssue> Issues=new List<LabelIssue>();public string Context="";
  public PreflightReport(OutputPlan plan){Plan=plan;}
  public bool Blocking{get{return Issues.Exists(i=>i.Blocking);}}
  public bool HasWarnings{get{return Issues.Exists(i=>!i.Blocking);}}
  public string Fingerprint{get{var text=new StringBuilder(Context);foreach(var issue in Issues)text.Append("\n").Append(issue.Blocking).Append('|').Append(issue.Record).Append('|').Append(issue.Page).Append('|').Append(issue.Element).Append('|').Append(issue.Message);return text.ToString();}}
  public bool CanProceed(bool warningsAccepted){return !Blocking&&(!HasWarnings||warningsAccepted);}
  public void EnsureValid(){var issue=Issues.Find(i=>i.Blocking);if(issue!=null)throw new InvalidOperationException(issue.ToString());}
  public void AddWarning(string message){if(!Issues.Exists(i=>!i.Blocking&&i.Element==-2&&i.Message==message))Issues.Add(new LabelIssue{Message=message});}
  public List<string> Messages(){return Issues.ConvertAll(i=>i.ToString());}
  public static PreflightReport Inspect(OutputPlan plan){
   if(plan==null)throw new ArgumentNullException("plan");var result=new PreflightReport(plan);var seen=new HashSet<LabelSettings>();int index=0;
   foreach(var placement in plan.Placements){WorkContext.Report("Kontrolujem výstup…",index++,plan.Placements.Count);if(!seen.Add(placement.Label))continue;foreach(var issue in LabelChecks.Inspect(placement.Label))result.Issues.Add(new LabelIssue{Blocking=issue.Blocking,Element=issue.Element,Message=issue.Message,Record=placement.Record,Page=placement.Page+1});}
   if(plan.Placements.Count==0)result.Issues.Add(new LabelIssue{Blocking=true,Message=Loc.T("Dokument neobsahuje nič na tlač.")});
   WorkContext.Report("Kontrolujem výstup…",plan.Placements.Count,plan.Placements.Count);return result;
  }
 }

}
