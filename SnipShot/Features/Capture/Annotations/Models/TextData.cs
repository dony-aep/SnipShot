using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace SnipShot.Features.Capture.Annotations.Models
{
    /// <summary>
    /// Represents the data for a text annotation element.
    /// </summary>
    public class TextData
    {
        /// <summary>
        /// The text content.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Font family name.
        /// </summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Font size in points.
        /// </summary>
        public double FontSize { get; set; } = 16;

        /// <summary>
        /// Whether the text is bold.
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// Whether the text is italic.
        /// </summary>
        public bool IsItalic { get; set; }

        /// <summary>
        /// Whether the text is underlined.
        /// </summary>
        public bool IsUnderline { get; set; }

        /// <summary>
        /// Whether the text has strikethrough.
        /// </summary>
        public bool IsStrikethrough { get; set; }

        /// <summary>
        /// The color of the text.
        /// </summary>
        public Color TextColor { get; set; } = Colors.White;

        /// <summary>
        /// The highlight (background) color of the text. Transparent means no highlight.
        /// </summary>
        public Color HighlightColor { get; set; } = Colors.Transparent;

        /// <summary>
        /// Position of the text element on the canvas.
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Width of the text container.
        /// </summary>
        public double Width { get; set; } = double.NaN;

        /// <summary>
        /// Height of the text container.
        /// </summary>
        public double Height { get; set; } = double.NaN;

        /// <summary>
        /// Whether the text element is currently selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Creates a deep copy of this TextData.
        /// </summary>
        public TextData Clone()
        {
            return new TextData
            {
                Text = Text,
                FontFamily = FontFamily,
                FontSize = FontSize,
                IsBold = IsBold,
                IsItalic = IsItalic,
                IsUnderline = IsUnderline,
                IsStrikethrough = IsStrikethrough,
                TextColor = TextColor,
                HighlightColor = HighlightColor,
                Position = Position,
                Width = Width,
                Height = Height,
                IsSelected = IsSelected
            };
        }
    }
}
