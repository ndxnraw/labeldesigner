using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EanEtikety {
 sealed class StyledEditBox : RichTextBox {
  public Action<TextEmphasis> ToggleStyle;
  protected override bool ProcessCmdKey(ref Message message, Keys key) {
   if (key == (Keys.Control | Keys.B) || key == (Keys.Control | Keys.I)) {
    ToggleStyle?.Invoke((key & Keys.KeyCode) == Keys.B ? TextEmphasis.Bold : TextEmphasis.Italic);
    return true;
   }
   return base.ProcessCmdKey(ref message, key);
  }
 }

 public partial class MainForm {
  const string TextStylesColumn = "textStyles";
  Button styleTextButton;

  static TextItem StyledGridText(DataGridViewRow row) => new() {
   Text = Convert.ToString(row.Cells[0].Value) ?? "",
   Bold = Convert.ToBoolean(row.Cells[6].Value ?? false),
   Styles = row.Cells[TextStylesColumn].Value as List<TextStyleSpan> ?? new List<TextStyleSpan>()
  };

  static void WriteStyledText(DataGridViewRow row, string value) {
   var item = StyledGridText(row);
   StyledText.Edit(item, value);
   row.Cells[0].Value = item.Text;
   row.Cells[TextStylesColumn].Value = item.Styles;
  }

  void ApplySelectionStyle(TextBox edit, TextEmphasis emphasis) {
   int index = preview.SelectedIndex;
   if (index < 0 || index >= grid.Rows.Count || preview.Selection.Count != 1 || edit.SelectionLength == 0 ||
       elementMetadata.Exists(m => m.Index == index && m.Locked)) return;
   BeginTextSession(index);
   var row = grid.Rows[index];
   var item = StyledGridText(row);
   if (edit.SelectionStart + edit.SelectionLength > item.Text.Length) return;
   StyledText.Toggle(item, edit.SelectionStart, edit.SelectionLength, emphasis);
   loading = true;
   try { row.Cells[TextStylesColumn].Value = item.Styles; }
   finally { loading = false; }
   StoreAutoGeometry(index);
   Changed(this, EventArgs.Empty);
   timer.Stop();
   UpdatePreview();
   PositionInlineText();
  }

  void EditTextStyles() {
   int index = preview.SelectedIndex;
   if (index < 0 || index >= grid.Rows.Count || preview.Selection.Count != 1 ||
       elementMetadata.Exists(m => m.Index == index && m.Locked)) return;
   CommitInspectorIfFocused();
   var text = Read().Texts[index];
   using var dialog = FeatureForm("Formátovať časti textu…", 610, 430);
   using var regular = new Font("Segoe UI", Font.Size, FontStyle.Regular);
   using var bold = new Font(regular, FontStyle.Bold);
   using var italic = new Font(regular, FontStyle.Italic);
   using var both = new Font(regular, FontStyle.Bold | FontStyle.Italic);
   Font StyleFont(TextEmphasis emphasis) => emphasis switch {
    TextEmphasis.Bold => bold,
    TextEmphasis.Italic => italic,
    TextEmphasis.Bold | TextEmphasis.Italic => both,
    _ => regular
   };
   var panel = FeatureStack(dialog);
   var edit = new StyledEditBox {
    Name = "styledTextEditor", Text = text.Text, Font = StyleFont(text.Bold ? TextEmphasis.Bold : TextEmphasis.Regular),
    AcceptsTab = false, MaxLength = 4000, DetectUrls = false, HideSelection = false, Height = 170
   };
   foreach (var span in text.Styles) {
    int start = NormalizedLength(text.Text, span.Start);
    int end = NormalizedLength(text.Text, span.Start + span.Length);
    edit.Select(start, end-start);
    edit.SelectionFont = StyleFont(span.Emphasis);
   }
   edit.Select(0, 0);
   void Toggle(TextEmphasis emphasis) {
    int start = edit.SelectionStart, length = edit.SelectionLength;
    if (length == 0) return;
    var styles = new TextEmphasis[length];
    bool clear = true;
    for (int i=0; i<length; i++) {
     edit.Select(start+i, 1);
     styles[i] = SelectionEmphasis(edit);
     if ((styles[i] & emphasis) == 0) clear = false;
    }
    for (int i=0; i<length; i++) {
     edit.Select(start+i, 1);
     edit.SelectionFont = StyleFont(clear ? styles[i] & ~emphasis : styles[i] | emphasis);
    }
    edit.Select(start,length);
    edit.Focus();
   }
   var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
   var b = Btn(Loc.T("Tučné") + " (Ctrl+B)", () => Toggle(TextEmphasis.Bold));
   var i = Btn(Loc.T("Kurzíva") + " (Ctrl+I)", () => Toggle(TextEmphasis.Italic));
   b.Name = "styleBold"; i.Name = "styleItalic";
   actions.Controls.Add(b); actions.Controls.Add(i);
   StackAdd(panel, actions);
   StackField(panel, Loc.T("Text"), edit);
   edit.ToggleStyle = Toggle;
   edit.KeyDown += delegate(object sender, KeyEventArgs e) {
    if (e.KeyData == (Keys.Control | Keys.V) && Clipboard.ContainsText()) {
     e.SuppressKeyPress = true;
     edit.Paste(DataFormats.GetFormat(DataFormats.UnicodeText));
    }
   };
   AddAccept(dialog, panel, delegate {
    string value = edit.Text;
    if (value.Length > 4000) throw new InvalidOperationException(Loc.T("Text je príliš dlhý."));
    var styles = new List<TextStyleSpan>();
    var baseline = text.Bold ? TextEmphasis.Bold : TextEmphasis.Regular;
    for (int indexOfChar=0; indexOfChar<value.Length; indexOfChar++) {
     edit.Select(indexOfChar, 1);
     var emphasis = SelectionEmphasis(edit);
     if (emphasis == baseline) continue;
     var last = styles.Count > 0 ? styles[^1] : null;
     if (last != null && last.Start+last.Length == indexOfChar && last.Emphasis == emphasis) last.Length++;
     else styles.Add(new TextStyleSpan { Start = indexOfChar, Length = 1, Emphasis = emphasis });
    }
    ChangeDocument(label => {
     label.Texts[index].Text = value;
     label.Texts[index].Styles = styles;
    });
    preview.SelectElement(index);
   });
   dialog.ActiveControl = edit;
   dialog.ShowDialog(this);
  }

  static int NormalizedLength(string text, int length) {
   int count = length;
   for (int i=0;i+1<length;i++) if (text[i]=='\r' && text[i+1]=='\n') count--;
   return count;
  }

  static TextEmphasis SelectionEmphasis(RichTextBox edit) {
   using var font = edit.SelectionFont;
   if (font == null) return TextEmphasis.Regular;
   return (font.Bold ? TextEmphasis.Bold : TextEmphasis.Regular) |
          (font.Italic ? TextEmphasis.Italic : TextEmphasis.Regular);
  }

  void TrackStyledTableEdits() {
   DataGridViewRow active = null;
   string original = null;
   grid.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs e) {
    if (e.ColumnIndex != 0) return;
    active = grid.Rows[e.RowIndex];
    original = Convert.ToString(active.Cells[0].Value) ?? "";
   };
   grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) {
    if (loading || active == null || e.RowIndex != active.Index || e.ColumnIndex != 0) return;
    var item = StyledGridText(active);
    var value = item.Text;
    item.Text = original;
    StyledText.Edit(item, value);
    original = value;
    loading = true;
    try { active.Cells[TextStylesColumn].Value = item.Styles; }
    finally { loading = false; }
   };
   grid.CellEndEdit += delegate { active = null; original = null; };
  }
 }
}
