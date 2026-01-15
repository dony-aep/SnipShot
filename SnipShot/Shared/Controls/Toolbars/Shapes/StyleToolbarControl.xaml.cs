using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SnipShot.Helpers.Utils;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Shapes
{
    /// <summary>
    /// Toolbar control for stroke style options (color, opacity, thickness)
    /// </summary>
    public sealed partial class StyleToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when the stroke color changes
        /// </summary>
        public event EventHandler<Color>? ColorChanged;

        /// <summary>
        /// Event raised when the stroke opacity changes
        /// </summary>
        public event EventHandler<double>? OpacityChanged;

        /// <summary>
        /// Event raised when the stroke thickness changes
        /// </summary>
        public event EventHandler<double>? ThicknessChanged;

        private Color _currentColor = Colors.Red;

        public StyleToolbarControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the current stroke color
        /// </summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                _currentColor = value;
                ColorPalette.SelectedColor = value;
                UpdatePreview();
            }
        }

        /// <summary>
        /// Gets or sets the current opacity (0-100)
        /// </summary>
        public double StrokeOpacity
        {
            get => OpacitySlider.Value;
            set => OpacitySlider.Value = value;
        }

        /// <summary>
        /// Gets or sets the current thickness
        /// </summary>
        public double Thickness
        {
            get => ThicknessSlider.Value;
            set => ThicknessSlider.Value = value;
        }

        /// <summary>
        /// Sets all style values at once
        /// </summary>
        public void SetStyle(Color color, double opacity, double thickness)
        {
            _currentColor = color;
            ColorPalette.SelectedColor = color;
            OpacitySlider.Value = opacity;
            ThicknessSlider.Value = thickness;
            UpdatePreview();
        }

        private void ColorPalette_ColorSelected(object? sender, Color color)
        {
            _currentColor = color;
            UpdatePreview();
            ColorChanged?.Invoke(this, color);
        }

        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            OpacityChanged?.Invoke(this, e.NewValue);
        }

        private void ThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ThicknessPreview != null)
            {
                ThicknessPreview.Height = e.NewValue;
            }
            ThicknessChanged?.Invoke(this, e.NewValue);
        }

        private void UpdatePreview()
        {
            if (ThicknessPreview != null)
            {
                ThicknessPreview.Fill = BrushCache.GetBrush(_currentColor);
            }
        }
    }
}
