using Microsoft.UI.Xaml.Controls;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Text
{
    /// <summary>
    /// Toolbar control for text highlight color selection
    /// </summary>
    public sealed partial class TextHighlightToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when a color is selected
        /// </summary>
        public event EventHandler<Color>? ColorSelected;

        public TextHighlightToolbarControl()
        {
            this.InitializeComponent();
        }

        private void ColorPalette_ColorSelected(object? sender, Color color)
        {
            ColorSelected?.Invoke(this, color);
        }
    }
}
