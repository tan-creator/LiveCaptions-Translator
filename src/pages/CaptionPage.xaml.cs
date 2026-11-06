using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public partial class CaptionPage : Page
    {
        public const int CARD_HEIGHT = 110;

        private static CaptionPage instance;
        public static CaptionPage Instance => instance;

        public CaptionPage()
        {
            InitializeComponent();
            DataContext = Translator.Caption;
            instance = this;

            Loaded += (s, e) =>
            {
                AutoHeight();
                CaptionScroll.ScrollToEnd();
                (App.Current.MainWindow as MainWindow).CaptionLogButton.Visibility = Visibility.Visible;
            };
            Unloaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow).CaptionLogButton.Visibility = Visibility.Collapsed;
            };

            CollapseTranslatedCaption(Translator.Setting.MainWindow.CaptionLogEnabled);
            ApplyFontSizes();
            if (Translator.Caption.LogEntries.Count == 0)
                _ = Translator.LoadPreviousLog();
        }

        private void ApplyFontSizes()
        {
            // Resources, so the log rows and the current row resize together.
            Resources["OriginalFontSize"] = (double)Translator.Setting.MainWindow.OriginalFontSize;
            Resources["TranslatedFontSize"] = (double)Translator.Setting.MainWindow.TranslatedFontSize;
        }

        private void OriginalCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                ForwardWheel(e);
                return;
            }
            Translator.Setting.MainWindow.OriginalFontSize =
                AdjustFontSize(Translator.Setting.MainWindow.OriginalFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private void TranslatedCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                ForwardWheel(e);
                return;
            }
            Translator.Setting.MainWindow.TranslatedFontSize =
                AdjustFontSize(Translator.Setting.MainWindow.TranslatedFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private static int AdjustFontSize(int current, int wheelDelta)
        {
            int next = current + (wheelDelta > 0 ? StyleConsts.DELTA_FONT_SIZE : -StyleConsts.DELTA_FONT_SIZE);
            return Math.Clamp(next, StyleConsts.MIN_FONT_SIZE, StyleConsts.MAX_FONT_SIZE);
        }

        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            LogCards.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        // TextBox and RichTextBox swallow the wheel even when they have nothing to scroll.
        private void ForwardWheel(MouseWheelEventArgs e)
        {
            CaptionScroll.ScrollToVerticalOffset(CaptionScroll.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        // Follow the newest sentence while the view is at the bottom; leave it alone while the user reads older ones.
        private bool followNewest = true;

        private void CaptionScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0)
                followNewest = CaptionScroll.VerticalOffset >= CaptionScroll.ScrollableHeight - 1;
            else if (followNewest)
                CaptionScroll.ScrollToEnd();
        }

        public void AutoHeight()
        {
            if (Translator.Setting.MainWindow.CaptionLogEnabled)
                (App.Current.MainWindow as MainWindow).AutoHeightAdjust(
                    minHeight: CARD_HEIGHT * (Translator.Setting.DisplaySentences + 1),
                    maxHeight: CARD_HEIGHT * (Translator.Setting.DisplaySentences + 1));
            else
                (App.Current.MainWindow as MainWindow).AutoHeightAdjust(
                    minHeight: (int)App.Current.MainWindow.MinHeight,
                    maxHeight: (int)App.Current.MainWindow.MinHeight);
        }
    }
}
