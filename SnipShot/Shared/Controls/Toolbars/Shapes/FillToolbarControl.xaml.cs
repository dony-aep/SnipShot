using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Shapes
{
    /// <summary>
    /// Toolbar control for fill options (color, opacity)
    /// </summary>
    public sealed partial class FillToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when the fill color changes
        /// </summary>
        public event EventHandler<Color>? ColorChanged;

        /// <summary>
        /// Event raised when the fill opacity changes
        /// </summary>
        public event EventHandler<double>? OpacityChanged;

        private Color _currentColor = Colors.Transparent;

        public FillToolbarControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the current fill color
        /// </summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                _currentColor = value;
                ColorPalette.SelectedColor = value;
            }
        }

        /// <summary>
        /// Gets or sets the current fill opacity (0-100)
        /// </summary>
        public double FillOpacity
        {
            get => OpacitySlider.Value;
            set => OpacitySlider.Value = value;
        }

        /// <summary>
        /// Sets all fill values at once
        /// </summary>
        public void SetFill(Color color, double opacity)
        {
            _currentColor = color;
            ColorPalette.SelectedColor = color;
            OpacitySlider.Value = opacity;
        }

        private void ColorPalette_ColorSelected(object? sender, Color color)
        {
            _currentColor = color;
            ColorChanged?.Invoke(this, color);
        }

        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            OpacityChanged?.Invoke(this, e.NewValue);
        }
    }
}
