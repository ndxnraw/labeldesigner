using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EanEtikety {
 public partial class MainForm {
  void RecoverPrintJob(Form owner, PrintHistoryStore store, PrintHistoryEntry entry, Action refresh) {
   ArchivedPrintJob job;
   if (!RunWork(owner, "print-recovery-load", "Načítavam tlačovú úlohu…", () => {
    var archived = store.Open(entry.Id);
    archived.ValidateArchive();
    return archived;
   }, out job)) return;

   OutputPlan plan = null;
   int selectedCopy = 0;
   using (var selection = FeatureForm("Pripraviť dotlač…", 640, 545)) {
    var stack = FeatureStack(selection);
    StackAdd(stack, new Label {
     AutoSize = true, MaximumSize = new Size(470, 0),
     Text = Loc.T("Vyberte rozsah podľa skutočných výtlačkov. Každá dotlač sa týka jednej kópie.")
    });
    var copy = Number(stack, "Kópia pôvodnej úlohy", 1, 1, job.Repetitions, 0);
    copy.Name = "recoveryCopy";
    copy.Enabled = job.Repetitions > 1;
    var mode = Choice(stack, "Vybrať podľa", new[] { "Zvyšné strany", "Vybrané etikety" }, 0);
    mode.Name = "recoveryMode";
    int pageCount = job.Placements[job.Placements.Count - 1].Page + 1;
    var lastPage = Number(stack, "Posledná správne vytlačená strana (0 = žiadna)", 0, 0, pageCount, 0);
    lastPage.Name = "recoveryLastPage";
    var lastPageLabel = stack.GetControlFromPosition(0, stack.GetRow(lastPage) - 1);
    var labels = new CheckedListBox {
     Name = "recoveryLabels", CheckOnClick = true, Height = 190, IntegralHeight = false,
     AccessibleName = Loc.T("Vybrané etikety")
    };
    labels.BeginUpdate();
    try {
     for (int i = 0; i < job.Placements.Count; i++) {
      var item = job.Placements[i];
      string row = string.Format(Loc.T("Etiketa {0} · strana {1}"), i + 1, item.Page + 1);
      if (item.Slot > 0) row += string.Format(Loc.T(" · pozícia {0}"), item.Slot);
      if (item.Record > 0) row += string.Format(Loc.T(" · záznam {0}"), item.Record);
      labels.Items.Add(row);
     }
    } finally { labels.EndUpdate(); }
    StackAdd(stack, labels);
    var actions = new FlowLayoutPanel { AutoSize = true };
    var next = Btn(Loc.T("Otvoriť náhľad"), delegate {
     try {
      selectedCopy = (int)copy.Value;
      plan = mode.SelectedIndex == 0 ? job.ContinueCopy(selectedCopy, (int)lastPage.Value)
       : job.SelectCopyLabels(selectedCopy, labels.CheckedIndices.Cast<int>().Select(index => index + 1).ToArray());
      selection.DialogResult = DialogResult.OK;
     } catch (ArgumentException ex) { UiMessageBox.Show(selection, ex.Message, selection.Text); }
    });
    next.Name = "recoveryPreview";
    var cancel = Btn(Loc.T("Zrušiť"), delegate { selection.DialogResult = DialogResult.Cancel; });
    actions.Controls.Add(next);
    actions.Controls.Add(cancel);
    StackAdd(stack, actions);
    Action updateMode = () => {
     lastPageLabel.Visible = lastPage.Visible = mode.SelectedIndex == 0;
     labels.Visible = mode.SelectedIndex == 1;
    };
    mode.SelectedIndexChanged += delegate { updateMode(); };
    updateMode();
    selection.AcceptButton = next;
    selection.CancelButton = cancel;
    ApplyColors(selection);
    if (selection.ShowDialog(owner) != DialogResult.OK) return;
   }

   using (var preview = CreateJobPrintWindow(job.Format, plan, null, job)) {
    ((PrintPanel)preview.Controls[0]).SetPrintRecovery(entry.Id, selectedCopy, () => {
     try {
      owner.BeginInvoke((Action)(() => {
       if (owner.IsDisposed || !owner.Visible) return;
       try { refresh(); } catch (Exception ex) { Error(ex); }
      }));
     } catch (InvalidOperationException) {
      // The history window may have closed while submission was in progress.
     }
    });
    preview.ShowDialog(owner);
   }
  }
 }
}
