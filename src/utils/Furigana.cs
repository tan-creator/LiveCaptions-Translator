using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using NMeCab.Specialized;

namespace LiveCaptionsTranslator.utils
{
    // Attached `Furigana.Text`: shows Japanese text with readings above kanji, using NMeCab with the IPA dictionary.
    public static class Furigana
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Furigana), new PropertyMetadata(string.Empty, OnTextChanged));

        public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
        public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not RichTextBox box)
                return;
            // A new RichTextBox already holds an empty default document, so mark ours with its Tag.
            if (box.Document.Tag is not Paragraph paragraph)
            {
                paragraph = new Paragraph();
                box.Document = new FlowDocument(paragraph) { PagePadding = new Thickness(0), Tag = paragraph };
                // Furigana words are embedded elements, which RichTextBox's own copy leaves out.
                box.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,
                    (_, _) => Clipboard.SetText(GetSelectedText(box))));
            }

            paragraph.Inlines.Clear();
            foreach (var (text, reading) in Annotate(e.NewValue as string ?? string.Empty))
            {
                if (reading == null)
                    paragraph.Inlines.Add(new Run(text));
                else
                    paragraph.Inlines.Add(CreateRuby(text, reading));
            }
        }

        private static InlineUIContainer CreateRuby(string text, string reading)
        {
            // Both TextBlocks inherit font and color from the parent; the reading is scaled down so Ctrl+Scroll still works.
            var panel = new StackPanel { Tag = text };
            panel.Children.Add(new TextBlock
            {
                Text = reading,
                HorizontalAlignment = HorizontalAlignment.Center,
                LayoutTransform = new ScaleTransform(0.5, 0.5)
            });
            panel.Children.Add(new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center });
            return new InlineUIContainer(panel) { BaselineAlignment = BaselineAlignment.Bottom };
        }

        // The selection as plain text, taking each furigana word's base text from its panel's Tag.
        public static string GetSelectedText(RichTextBox box)
        {
            var sb = new StringBuilder();
            var end = box.Selection.End;
            for (var p = box.Selection.Start; p != null && p.CompareTo(end) < 0;
                 p = p.GetNextContextPosition(LogicalDirection.Forward))
            {
                var context = p.GetPointerContext(LogicalDirection.Forward);
                if (context == TextPointerContext.Text)
                {
                    string run = p.GetTextInRun(LogicalDirection.Forward);
                    sb.Append(run, 0, Math.Min(run.Length, p.GetOffsetToPosition(end)));
                }
                else if (context == TextPointerContext.EmbeddedElement &&
                         p.GetAdjacentElement(LogicalDirection.Forward) is FrameworkElement { Tag: string text })
                {
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }

        // Loading the IPA dictionary takes a moment, so it only happens on the first Japanese caption.
        private static readonly Lazy<MeCabIpaDicTagger> tagger = new(() => MeCabIpaDicTagger.Create());

        // Segments of the text; `reading` is null where no furigana is needed.
        public static List<(string text, string? reading)> Annotate(string text)
        {
            var result = new List<(string, string?)>();
            // Kana is what tells Japanese apart from Chinese, which would otherwise get Japanese readings.
            if (!text.Any(IsKana) || !text.Any(IsKanji))
            {
                result.Add((text, null));
                return result;
            }

            try
            {
                int pos = 0;
                foreach (var node in tagger.Value.Parse(text))
                {
                    // MeCab drops whitespace between words; keep it so the caption reads the same.
                    int at = Math.Max(pos, text.IndexOf(node.Surface, pos, StringComparison.Ordinal));
                    if (at > pos)
                        result.Add((text[pos..at], null));
                    result.AddRange(SplitOkurigana(node.Surface, ToHiragana(node.Reading)));
                    pos = at + node.Surface.Length;
                }
                if (pos < text.Length)
                    result.Add((text[pos..], null));
            }
            catch
            {
                result.Clear();
                result.Add((text, null));
            }
            return result;
        }

        // IPA dictionary readings are katakana; unknown words have none.
        private static string ToHiragana(string? reading) =>
            string.IsNullOrEmpty(reading) || reading == "*" ? string.Empty :
            new string(reading.Select(c => c >= 'ァ' && c <= 'ヶ' ? (char)(c - 0x60) : c).ToArray());

        // 食べる/たべる -> 食(た) + べる: the furigana sits only over the kanji, not over the okurigana.
        public static List<(string text, string? reading)> SplitOkurigana(string display, string reading)
        {
            var result = new List<(string, string?)>();
            if (string.IsNullOrEmpty(reading) || !display.Any(IsKanji))
            {
                result.Add((display, null));
                return result;
            }

            int prefix = 0;
            while (prefix < display.Length && prefix < reading.Length &&
                   display[prefix] == reading[prefix] && !IsKanji(display[prefix]))
                prefix++;
            int suffix = 0;
            while (suffix < display.Length - prefix && suffix < reading.Length - prefix &&
                   display[^(suffix + 1)] == reading[^(suffix + 1)] && !IsKanji(display[^(suffix + 1)]))
                suffix++;

            if (prefix > 0)
                result.Add((display[..prefix], null));
            result.Add((display[prefix..^suffix], reading[prefix..^suffix]));
            if (suffix > 0)
                result.Add((display[^suffix..], null));
            return result;
        }

        private static bool IsKana(char c) => c >= '぀' && c <= 'ヿ';

        private static bool IsKanji(char c) =>
            (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || c == '々';
    }
}
