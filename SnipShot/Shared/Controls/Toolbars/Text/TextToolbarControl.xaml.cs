using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;
using System;
using Windows.UI;
using Windows.UI.Text;

namespace SnipShot.Shared.Controls.Toolbars.Text
{
    /// <summary>
    /// Toolbar control for text formatting options
    /// </summary>
    public sealed partial class TextToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when font family changes
        /// </summary>
        public event EventHandler<string>? FontFamilyChanged;

        /// <summary>
        /// Event raised when font size changes
        /// </summary>
        public event EventHandler<double>? FontSizeChanged;

        /// <summary>
        /// Event raised when bold style changes
        /// </summary>
        public event EventHandler<bool>? BoldChanged;

        /// <summary>
        /// Event raised when italic style changes
        /// </summary>
        public event EventHandler<bool>? ItalicChanged;

        /// <summary>
        /// Event raised when underline style changes
        /// </summary>
        public event EventHandler<bool>? UnderlineChanged;

        /// <summary>
        /// Event raised when strikethrough style changes
        /// </summary>
        public event EventHandler<bool>? StrikethroughChanged;

        /// <summary>
        /// Event raised when text color button is clicked
        /// </summary>
        public event EventHandler? TextColorButtonClicked;

        /// <summary>
        /// Event raised when text highlight button is clicked
        /// </summary>
        public event EventHandler? TextHighlightButtonClicked;

        /// <summary>
        /// Event raised when any setting changes
        /// </summary>
        public event EventHandler<TextData>? SettingsChanged;

        private TextData _settings = new();

        public TextToolbarControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets the current text format settings
        /// </summary>
        public TextData Settings => _settings;

        /// <summary>
        /// Sets the text color and updates the indicator
        /// </summary>
        public void SetTextColor(Color color)
        {
            _settings.TextColor = color;
            TextColorIndicator.Background = BrushCache.GetBrush(color);
            UpdatePreview();
        }

        /// <summary>
        /// Sets the highlight color and updates the indicator
        /// </summary>
        public void SetHighlightColor(Color color)
        {
            _settings.HighlightColor = color;
            if (color != Colors.Transparent)
            {
                TextHighlightIndicator.Background = BrushCache.GetBrush(color);
                TextHighlightIndicator.BorderThickness = new Thickness(0);
            }
            else
            {
                TextHighlightIndicator.Background = BrushCache.Transparent;
                TextHighlightIndicator.BorderThickness = new Thickness(1);
            }
            UpdatePreview();
        }

        /// <summary>
        /// Applies settings from external source
        /// </summary>
        public void ApplySettings(TextData settings)
        {
            _settings = settings.Clone();

            // Update font family
            for (int i = 0; i < FontFamilyComboBox.Items.Count; i++)
            {
                if (FontFamilyComboBox.Items[i] is ComboBoxItem item &&
                    item.Content?.ToString() == settings.FontFamily)
                {
                    FontFamilyComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Update font size
            FontSizeComboBox.Text = ((int)settings.FontSize).ToString();

            // Update toggles
            BoldToggle.IsChecked = settings.IsBold;
            ItalicToggle.IsChecked = settings.IsItalic;
            UnderlineToggle.IsChecked = settings.IsUnderline;
            StrikethroughToggle.IsChecked = settings.IsStrikethrough;

            // Update colors
            SetTextColor(settings.TextColor);
            SetHighlightColor(settings.HighlightColor);

            UpdatePreview();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                var fontFamily = item.Content?.ToString() ?? "Segoe UI";
                _settings.FontFamily = fontFamily;
                UpdatePreview();
                FontFamilyChanged?.Invoke(this, fontFamily);
                SettingsChanged?.Invoke(this, _settings);
            }
        }

        private void FontSizeComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            if (double.TryParse(args.Text, out double size))
            {
                size = Math.Clamp(size, 8, 200);
                _settings.FontSize = size;
                UpdatePreview();
                FontSizeChanged?.Invoke(this, size);
                SettingsChanged?.Invoke(this, _settings);
                args.Handled = true;
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem item)
            {
                if (double.TryParse(item.Content?.ToString(), out double size))
                {
                    _settings.FontSize = size;
                    UpdatePreview();
                    FontSizeChanged?.Invoke(this, size);
                    SettingsChanged?.Invoke(this, _settings);
                }
            }
        }

        private void BoldToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsBold = BoldToggle.IsChecked == true;
            UpdatePreview();
            BoldChanged?.Invoke(this, _settings.IsBold);
            SettingsChanged?.Invoke(this, _settings);
        }

        private void ItalicToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsItalic = ItalicToggle.IsChecked == true;
            UpdatePreview();
            ItalicChanged?.Invoke(this, _settings.IsItalic);
            SettingsChanged?.Invoke(this, _settings);
        }

        private void UnderlineToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsUnderline = UnderlineToggle.IsChecked == true;
            UpdatePreview();
            UnderlineChanged?.Invoke(this, _settings.IsUnderline);
            SettingsChanged?.Invoke(this, _settings);
        }

        private void StrikethroughToggle_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsStrikethrough = StrikethroughToggle.IsChecked == true;
            UpdatePreview();
            StrikethroughChanged?.Invoke(this, _settings.IsStrikethrough);
            SettingsChanged?.Invoke(this, _settings);
        }

        private void TextColorButton_Click(object sender, RoutedEventArgs e)
        {
            TextColorButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void TextHighlightButton_Click(object sender, RoutedEventArgs e)
        {
            TextHighlightButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void UpdatePreview()
        {
            if (TextPreviewBlock == null) return;

            TextPreviewBlock.FontFamily = new FontFamily(_settings.FontFamily);
            TextPreviewBlock.FontSize = _settings.FontSize;
            TextPreviewBlock.FontWeight = _settings.IsBold ? FontWeights.Bold : FontWeights.Normal;
            TextPreviewBlock.FontStyle = _settings.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            TextPreviewBlock.Foreground = BrushCache.GetBrush(_settings.TextColor);

            // Handle underline and strikethrough via TextDecorations
            var decorations = TextDecorations.None;
            if (_settings.IsUnderline) decorations |= TextDecorations.Underline;
            if (_settings.IsStrikethrough) decorations |= TextDecorations.Strikethrough;
            TextPreviewBlock.TextDecorations = decorations;

            // Handle highlight (Transparent means no highlight)
            TextPreviewHighlightBorder.Background = BrushCache.GetBrush(_settings.HighlightColor);
        }
    }
}
