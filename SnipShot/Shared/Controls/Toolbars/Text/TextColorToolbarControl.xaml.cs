using Microsoft.UI.Xaml.Controls;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Text
{
    /// <summary>
    /// Toolbar control for text color selection
    /// </summary>
    public sealed partial class TextColorToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when a color is selected
        /// </summary>
        public event EventHandler<Color>? ColorSelected;

        public TextColorToolbarControl()
        {
            this.InitializeComponent();
        }

        private void ColorPalette_ColorSelected(object? sender, Color color)
        {
            ColorSelected?.Invoke(this, color);
        }
    }
}
