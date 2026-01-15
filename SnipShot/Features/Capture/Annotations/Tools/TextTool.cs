using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;
using System;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
// Use Microsoft.UI.Text namespace for RichEditBox document formatting types
using Microsoft.UI.Text;

// Type aliases to resolve conflicts between Microsoft.UI.Text and Windows.UI.Text
using TextSetOptions = Microsoft.UI.Text.TextSetOptions;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;
using FormatEffect = Microsoft.UI.Text.FormatEffect;
using UnderlineType = Microsoft.UI.Text.UnderlineType;
using FontStyle = Windows.UI.Text.FontStyle;
using FontWeights = Microsoft.UI.Text.FontWeights;
using WinUITextDecorations = Windows.UI.Text.TextDecorations;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Tool for creating and managing text annotations on the canvas.
    /// Unlike shape tools, text creates Grid elements with RichEditBox for rich text formatting support.
    /// </summary>
    public class TextTool
    {
        #region Fields

        private bool _isActive;
        private Grid? _currentTextElement;
        private TextData _settings;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the tool name identifier.
        /// </summary>
        public string ToolName => "Text";

        /// <summary>
        /// Gets or sets whether the tool is currently active.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        /// <summary>
        /// Gets the current text element being created/edited.
        /// </summary>
        public Grid? CurrentTextElement => _currentTextElement;

        /// <summary>
        /// Gets or sets the text settings (font, size, colors, styles).
        /// </summary>
        public TextData Settings
        {
            get => _settings;
            set => _settings = value ?? GetDefaultSettings();
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a text element is created and ready for editing.
        /// </summary>
        public event EventHandler<Grid>? TextElementCreated;

        /// <summary>
        /// Event raised when text editing is completed.
        /// </summary>
        public event EventHandler<Grid>? TextEditingCompleted;

        #endregion

        #region Constructor

        public TextTool()
        {
            _settings = GetDefaultSettings();
        }

        public TextTool(TextData settings)
        {
            _settings = settings ?? GetDefaultSettings();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the text tool.
        /// </summary>
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Deactivates the text tool.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _currentTextElement = null;
        }

        /// <summary>
        /// Creates a new text element at the specified position.
        /// </summary>
        /// <param name="position">The position to create the text element.</param>
        /// <returns>The created Grid containing the text element.</returns>
        public Grid CreateTextElement(Point position)
        {
            // Create the text data for this element
            var textData = new TextData
            {
                Text = string.Empty,
                FontFamily = _settings.FontFamily,
                FontSize = _settings.FontSize,
                IsBold = _settings.IsBold,
                IsItalic = _settings.IsItalic,
                IsUnderline = _settings.IsUnderline,
                IsStrikethrough = _settings.IsStrikethrough,
                TextColor = _settings.TextColor,
                HighlightColor = _settings.HighlightColor,
                Position = position,
                Width = 200, // Default width
                Height = double.NaN // Auto height
            };

            // Create the visual element
            var grid = CreateTextGrid(textData);
            
            // Position the element on canvas
            Canvas.SetLeft(grid, position.X);
            Canvas.SetTop(grid, position.Y);

            _currentTextElement = grid;
            
            TextElementCreated?.Invoke(this, grid);
            
            return grid;
        }

        /// <summary>
        /// Creates a Grid containing a RichEditBox with the specified settings.
        /// </summary>
        /// <param name="textData">The text data to apply.</param>
        /// <returns>The created Grid element.</returns>
        public Grid CreateTextGrid(TextData textData)
        {
            var grid = new Grid
            {
                MinWidth = 50,
                MinHeight = 24,
                Tag = textData
            };

            if (!double.IsNaN(textData.Width))
            {
                grid.Width = textData.Width;
            }

            // Create background border for highlight
            var backgroundBorder = new Border
            {
                Name = "BackgroundBorder",
                Background = textData.HighlightColor == Colors.Transparent
                    ? BrushCache.Transparent
                    : BrushCache.GetBrush(textData.HighlightColor),
                CornerRadius = new CornerRadius(2)
            };

            // Create the RichEditBox for proper rich text formatting support
            var richEditBox = new RichEditBox
            {
                Name = "TextContent",
                FontFamily = new FontFamily(textData.FontFamily),
                FontSize = textData.FontSize,
                Foreground = BrushCache.GetBrush(textData.TextColor),
                Background = BrushCache.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                AcceptsReturn = true,
                IsSpellCheckEnabled = false, // Disable spell check to avoid visual clutter
                // Start disabled - will be enabled when editing starts
                // This allows clicks to pass through to parent Grid for selection/dragging
                IsHitTestVisible = false,
                IsReadOnly = true
            };

            // Apply initial formatting to the document
            ApplyInitialFormatting(richEditBox, textData);

            // Set initial text if any
            if (!string.IsNullOrEmpty(textData.Text))
            {
                richEditBox.Document.SetText(TextSetOptions.None, textData.Text);
            }

            // Add elements to grid
            grid.Children.Add(backgroundBorder);
            grid.Children.Add(richEditBox);

            return grid;
        }

        /// <summary>
        /// Applies initial character formatting to a RichEditBox.
        /// </summary>
        private void ApplyInitialFormatting(RichEditBox richEditBox, TextData textData)
        {
            // Get the default character formatting and apply settings
            var charFormat = richEditBox.Document.GetDefaultCharacterFormat();
            
            charFormat.Bold = textData.IsBold ? FormatEffect.On : FormatEffect.Off;
            charFormat.Italic = textData.IsItalic ? FormatEffect.On : FormatEffect.Off;
            charFormat.Underline = textData.IsUnderline ? UnderlineType.Single : UnderlineType.None;
            charFormat.Strikethrough = textData.IsStrikethrough ? FormatEffect.On : FormatEffect.Off;
            charFormat.ForegroundColor = textData.TextColor;
            charFormat.Name = textData.FontFamily;
            charFormat.Size = (float)textData.FontSize;
            
            richEditBox.Document.SetDefaultCharacterFormat(charFormat);
        }

        /// <summary>
        /// Applies current settings to an existing text element.
        /// </summary>
        /// <param name="textElement">The text element to update.</param>
        public void ApplySettingsToElement(Grid textElement)
        {
            if (textElement?.Tag is not TextData textData)
                return;

            var richEditBox = FindRichEditBox(textElement);
            var backgroundBorder = FindBackgroundBorder(textElement);

            if (richEditBox != null)
            {
                richEditBox.FontFamily = new FontFamily(textData.FontFamily);
                richEditBox.FontSize = textData.FontSize;
                
                // Create the character format with all settings
                var charFormat = richEditBox.Document.GetDefaultCharacterFormat();
                charFormat.Bold = textData.IsBold ? FormatEffect.On : FormatEffect.Off;
                charFormat.Italic = textData.IsItalic ? FormatEffect.On : FormatEffect.Off;
                charFormat.Underline = textData.IsUnderline ? UnderlineType.Single : UnderlineType.None;
                charFormat.Strikethrough = textData.IsStrikethrough ? FormatEffect.On : FormatEffect.Off;
                charFormat.ForegroundColor = textData.TextColor;
                charFormat.Name = textData.FontFamily;
                charFormat.Size = (float)textData.FontSize;
                
                // IMPORTANT: Update default format FIRST so new text uses these settings
                richEditBox.Document.SetDefaultCharacterFormat(charFormat);
                
                // Get current text length
                string currentText;
                richEditBox.Document.GetText(TextGetOptions.None, out currentText);
                var textLength = currentText?.TrimEnd('\r', '\n').Length ?? 0;
                
                // If there's existing text, apply formatting to it
                if (textLength > 0)
                {
                    // Save current selection
                    var savedStart = richEditBox.Document.Selection.StartPosition;
                    var savedEnd = richEditBox.Document.Selection.EndPosition;
                    
                    // Select all text and apply format
                    richEditBox.Document.Selection.SetRange(0, textLength);
                    richEditBox.Document.Selection.CharacterFormat = charFormat;
                    
                    // Restore selection
                    richEditBox.Document.Selection.SetRange(savedStart, savedEnd);
                }
            }

            if (backgroundBorder != null)
            {
                backgroundBorder.Background = textData.HighlightColor == Colors.Transparent
                    ? BrushCache.Transparent
                    : BrushCache.GetBrush(textData.HighlightColor);
            }
        }

        /// <summary>
        /// Updates the font family of the current settings and selected element.
        /// </summary>
        public void SetFontFamily(string fontFamily, Grid? selectedElement = null)
        {
            _settings.FontFamily = fontFamily;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.FontFamily = fontFamily;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Updates the font size of the current settings and selected element.
        /// </summary>
        public void SetFontSize(double fontSize, Grid? selectedElement = null)
        {
            _settings.FontSize = fontSize;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.FontSize = fontSize;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Toggles bold style.
        /// </summary>
        public void ToggleBold(Grid? selectedElement = null)
        {
            _settings.IsBold = !_settings.IsBold;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.IsBold = _settings.IsBold;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Toggles italic style.
        /// </summary>
        public void ToggleItalic(Grid? selectedElement = null)
        {
            _settings.IsItalic = !_settings.IsItalic;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.IsItalic = _settings.IsItalic;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Toggles underline style.
        /// </summary>
        public void ToggleUnderline(Grid? selectedElement = null)
        {
            _settings.IsUnderline = !_settings.IsUnderline;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.IsUnderline = _settings.IsUnderline;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Toggles strikethrough style.
        /// </summary>
        public void ToggleStrikethrough(Grid? selectedElement = null)
        {
            _settings.IsStrikethrough = !_settings.IsStrikethrough;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.IsStrikethrough = _settings.IsStrikethrough;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Sets the text color.
        /// </summary>
        public void SetTextColor(Color color, Grid? selectedElement = null)
        {
            _settings.TextColor = color;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.TextColor = color;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Sets the highlight (background) color.
        /// </summary>
        public void SetHighlightColor(Color color, Grid? selectedElement = null)
        {
            _settings.HighlightColor = color;
            if (selectedElement?.Tag is TextData textData)
            {
                textData.HighlightColor = color;
                ApplySettingsToElement(selectedElement);
            }
        }

        /// <summary>
        /// Puts a text element into edit mode.
        /// Enables full native RichEditBox behavior (cursor, selection, typing).
        /// </summary>
        public void StartEditing(Grid textElement)
        {
            var richEditBox = FindRichEditBox(textElement);
            if (richEditBox == null)
                return;

            // Enable the RichEditBox for full native interaction
            richEditBox.IsHitTestVisible = true;
            richEditBox.IsReadOnly = false;
            
            // Give focus to the RichEditBox
            richEditBox.Focus(FocusState.Programmatic);
            
            // Select all text only if there's existing text
            string text;
            richEditBox.Document.GetText(TextGetOptions.None, out text);
            if (!string.IsNullOrEmpty(text?.Trim()))
            {
                richEditBox.Document.Selection.SetRange(0, int.MaxValue);
            }
            
            _currentTextElement = textElement;
        }

        /// <summary>
        /// Checks if a text element is currently in edit mode.
        /// </summary>
        public bool IsEditing(Grid textElement)
        {
            var richEditBox = FindRichEditBox(textElement);
            return richEditBox != null && richEditBox.IsHitTestVisible && !richEditBox.IsReadOnly;
        }

        /// <summary>
        /// Ends editing mode for a text element.
        /// Replaces RichEditBox with a TextBlock for display-only mode - text becomes a static annotation.
        /// </summary>
        /// <returns>True if the text element has content, false if it's empty.</returns>
        public bool EndEditing(Grid textElement)
        {
            var richEditBox = FindRichEditBox(textElement);
            if (richEditBox == null)
            {
                // Already converted to TextBlock, check if it has content
                var existingTextBlock = FindTextBlock(textElement);
                if (existingTextBlock != null)
                {
                    return !string.IsNullOrWhiteSpace(existingTextBlock.Text);
                }
                return false;
            }

            // Get current text content
            string currentText;
            richEditBox.Document.GetText(TextGetOptions.None, out currentText);
            currentText = currentText?.Trim() ?? string.Empty;

            var hasContent = !string.IsNullOrWhiteSpace(currentText);

            // Update the TextData with current text
            if (textElement.Tag is TextData textData)
            {
                textData.Text = currentText;
            }

            if (hasContent)
            {
                // Create TextBlock with the same formatting
                var textBlock = CreateTextBlockFromRichEditBox(richEditBox, textElement.Tag as TextData);
                
                // Remove RichEditBox and add TextBlock
                textElement.Children.Remove(richEditBox);
                textElement.Children.Add(textBlock);
                
                TextEditingCompleted?.Invoke(this, textElement);
            }

            _currentTextElement = null;
            return hasContent;
        }

        /// <summary>
        /// Creates a TextBlock with formatting matching the RichEditBox content.
        /// </summary>
        private TextBlock CreateTextBlockFromRichEditBox(RichEditBox richEditBox, TextData? textData)
        {
            string text;
            richEditBox.Document.GetText(TextGetOptions.None, out text);
            text = text?.Trim() ?? string.Empty;

            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = richEditBox.FontFamily,
                FontSize = richEditBox.FontSize,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(4),
                IsHitTestVisible = false, // Allow clicks to pass through to parent Grid
            };

            // Apply formatting from TextData
            if (textData != null)
            {
                textBlock.Foreground = BrushCache.GetBrush(textData.TextColor);
                textBlock.FontWeight = textData.IsBold ? FontWeights.Bold : FontWeights.Normal;
                textBlock.FontStyle = textData.IsItalic ? FontStyle.Italic : FontStyle.Normal;
                
                // Apply text decorations (underline and/or strikethrough)
                if (textData.IsUnderline && textData.IsStrikethrough)
                {
                    // Both underline and strikethrough
                    textBlock.TextDecorations = WinUITextDecorations.Underline | WinUITextDecorations.Strikethrough;
                }
                else if (textData.IsUnderline)
                {
                    textBlock.TextDecorations = WinUITextDecorations.Underline;
                }
                else if (textData.IsStrikethrough)
                {
                    textBlock.TextDecorations = WinUITextDecorations.Strikethrough;
                }
            }

            return textBlock;
        }

        /// <summary>
        /// Finds the TextBlock inside a text element Grid (used after conversion from RichEditBox).
        /// </summary>
        public static TextBlock? FindTextBlock(Grid textElement)
        {
            foreach (var child in textElement.Children)
            {
                if (child is TextBlock textBlock)
                    return textBlock;
            }
            return null;
        }

        /// <summary>
        /// Gets the text content from a text element.
        /// Works with both RichEditBox (editing mode) and TextBlock (frozen mode).
        /// </summary>
        public string GetText(Grid textElement)
        {
            // Check if there's a RichEditBox (still editing)
            var richEditBox = FindRichEditBox(textElement);
            if (richEditBox != null)
            {
                string text;
                richEditBox.Document.GetText(TextGetOptions.None, out text);
                return text?.Trim() ?? string.Empty;
            }
            
            // Check if there's a TextBlock (already frozen)
            var textBlock = FindTextBlock(textElement);
            if (textBlock != null)
            {
                return textBlock.Text?.Trim() ?? string.Empty;
            }
            
            // Fallback to TextData if available
            if (textElement.Tag is TextData textData)
            {
                return textData.Text?.Trim() ?? string.Empty;
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Sets the text content of a text element.
        /// </summary>
        public void SetText(Grid textElement, string text)
        {
            var richEditBox = FindRichEditBox(textElement);
            if (richEditBox != null)
            {
                richEditBox.Document.SetText(TextSetOptions.None, text);
                if (textElement.Tag is TextData textData)
                {
                    textData.Text = text;
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets the default text settings.
        /// </summary>
        private static TextData GetDefaultSettings()
        {
            return new TextData
            {
                FontFamily = "Segoe UI",
                FontSize = 16,
                IsBold = false,
                IsItalic = false,
                IsUnderline = false,
                IsStrikethrough = false,
                TextColor = Colors.White,
                HighlightColor = Colors.Transparent
            };
        }

        /// <summary>
        /// Finds the RichEditBox inside a text element Grid.
        /// </summary>
        public static RichEditBox? FindRichEditBox(Grid textElement)
        {
            foreach (var child in textElement.Children)
            {
                if (child is RichEditBox richEditBox)
                    return richEditBox;
            }
            return null;
        }

        /// <summary>
        /// Legacy method for backward compatibility - finds RichEditBox.
        /// </summary>
        [Obsolete("Use FindRichEditBox instead")]
        public static TextBox? FindTextBox(Grid textElement)
        {
            // For backward compatibility, return null since we now use RichEditBox
            return null;
        }

        /// <summary>
        /// Finds the background Border inside a text element Grid.
        /// </summary>
        private static Border? FindBackgroundBorder(Grid textElement)
        {
            foreach (var child in textElement.Children)
            {
                if (child is Border border && border.Name == "BackgroundBorder")
                    return border;
            }
            return null;
        }

        #endregion
    }
}
