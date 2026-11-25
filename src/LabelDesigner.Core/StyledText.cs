using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace EanEtikety {
 [Flags]
 public enum TextEmphasis { Regular = 0, Bold = 1, Italic = 2 }

 public sealed class TextStyleSpan {
  [XmlAttribute] public int Start { get; set; }
  [XmlAttribute] public int Length { get; set; }
  [XmlAttribute] public TextEmphasis Emphasis { get; set; }
 }

 public static class StyledText {
  internal static readonly Regex Variable = new(@"\{([\p{L}_][\p{L}\p{N}_]*)\}");
  const TextEmphasis All = TextEmphasis.Bold | TextEmphasis.Italic;
  static TextEmphasis DefaultStyle(TextItem text) => text.Bold ? TextEmphasis.Bold : TextEmphasis.Regular;

  public static TextEmphasis At(TextItem text, int index) {
   foreach (var span in text.Styles)
    if (index >= span.Start && index - span.Start < span.Length) return span.Emphasis;
   return DefaultStyle(text);
  }

  static TextEmphasis[] StylesOf(TextItem text) {
   var styles = new TextEmphasis[text.Text.Length];
   Array.Fill(styles, DefaultStyle(text));
   foreach (var span in text.Styles) Array.Fill(styles, span.Emphasis, span.Start, span.Length);
   return styles;
  }

  static void Assign(TextItem text, string value, TextEmphasis[] styles) {
   var spans = new List<TextStyleSpan>();
   var baseline = DefaultStyle(text);
   for (int start = 0; start < styles.Length;) {
    var emphasis = styles[start];
    int end = start + 1;
    while (end < styles.Length && styles[end] == emphasis) end++;
    if (emphasis != baseline) spans.Add(new TextStyleSpan { Start = start, Length = end - start, Emphasis = emphasis });
    start = end;
   }
   text.Text = value;
   text.Styles = spans;
  }

  public static void Toggle(TextItem text, int start, int length, TextEmphasis emphasis) {
   if (length == 0) return;
   var styles = StylesOf(text);
   var selection = styles.AsSpan(start, length);
   bool remove = true;
   foreach (var style in selection) if ((style & emphasis) != emphasis) { remove = false; break; }
   for (int i = 0; i < selection.Length; i++)
    selection[i] = remove ? selection[i] & ~emphasis : selection[i] | emphasis;
   Assign(text, text.Text, styles);
  }

  public static void Edit(TextItem text, string value) {
   if (text.Text == value) return;
   if (text.Styles.Count == 0) { text.Text = value; return; }
   string original = text.Text;
   int first = 0, last = 0;
   while (first < original.Length && first < value.Length && original[first] == value[first]) first++;
   while (last < original.Length - first && last < value.Length - first &&
    original[original.Length - last - 1] == value[value.Length - last - 1]) last++;
   var prior = StylesOf(text);
   var styles = new TextEmphasis[value.Length];
   var inherited = first < prior.Length ? prior[first] : prior[^1];
   Array.Fill(styles, inherited);
   Array.Copy(prior, styles, first);
   Array.Copy(prior, prior.Length - last, styles, styles.Length - last, last);
   Assign(text, value, styles);
  }

  internal static void ReplaceField(TextItem text, string value) {
   if (text.Styles.Count == 0) { text.Text = value; return; }
   var styles = new TextEmphasis[value.Length];
   Array.Fill(styles, At(text, 0));
   Assign(text, value, styles);
  }

  internal static void Expand(TextItem text, MatchEvaluator valueOf) {
   if (text.Styles.Count == 0) { text.Text = Variable.Replace(text.Text ?? "", valueOf); return; }
   var source = StylesOf(text);
   var value = new StringBuilder();
   var styles = new List<TextEmphasis>();
   int position = 0;
   foreach (Match match in Variable.Matches(text.Text)) {
    string replacement = valueOf(match);
    if (value.Length + match.Index - position + replacement.Length > 4000)
     throw new InvalidDataException(Loc.T("Text je príliš dlhý."));
    value.Append(text.Text, position, match.Index - position).Append(replacement);
    for (; position < match.Index; position++) styles.Add(source[position]);
    for (int i = 0; i < replacement.Length; i++) styles.Add(source[match.Index]);
    position = match.Index + match.Length;
   }
   value.Append(text.Text, position, text.Text.Length - position);
   for (; position < source.Length; position++) styles.Add(source[position]);
   Assign(text, value.ToString(), styles.ToArray());
  }

  internal static void Validate(TextItem text) {
   int end = 0;
   if (text.Styles == null || text.Styles.Count > text.Text.Length)
    throw new InvalidDataException(Loc.T("Neplatné formátovanie textu."));
   foreach (var span in text.Styles) {
    if (span == null || span.Start < end || span.Length <= 0 || span.Start > text.Text.Length ||
        span.Length > text.Text.Length - span.Start || (span.Emphasis & ~All) != 0)
     throw new InvalidDataException(Loc.T("Neplatné formátovanie textu."));
    end = span.Start + span.Length;
   }
  }
 }
}
