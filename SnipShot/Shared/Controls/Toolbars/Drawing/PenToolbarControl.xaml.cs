using Microsoft.UI.Xaml.Controls;
using SnipShot.Features.Capture.Annotations.Models;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Drawing
{
    /// <summary>
    /// Toolbar control for pen settings (color and thickness)
    /// </summary>
    public sealed partial class PenToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when pen settings change
        /// </summary>
        public event EventHandler<AnnotationSettings>? SettingsChanged;

        /// <summary>
        /// Event raised when the color changes
        /// </summary>
        public event EventHandler<Color>? ColorChanged;

        /// <summary>
        /// Event raised when the thickness changes
        /// </summary>
        public event EventHandler<double>? ThicknessChanged;

        private Color _currentColor = Microsoft.UI.Colors.White;
        private double _currentThickness = 2.0;

        /// <summary>
        /// Gets or sets the current pen color
        /// </summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                _currentColor = value;
                ColorPalette.SelectedColor = value;
                ThicknessControl.PreviewColor = value;
            }
        }

        /// <summary>
        /// Gets or sets the current pen thickness
        /// </summary>
        public double CurrentThickness
        {
            get => _currentThickness;
            set
            {
                _currentThickness = value;
                ThicknessControl.Thickness = value;
            }
        }

        /// <summary>
        /// Gets the current settings as an AnnotationSettings object
        /// </summary>
        public AnnotationSettings CurrentSettings => new AnnotationSettings
        {
            StrokeColor = _currentColor,
            StrokeThickness = _currentThickness,
            StrokeOpacity = 1.0,
            FillEnabled = false
        };

        public PenToolbarControl()
        {
            this.InitializeComponent();
        }

        private void ColorPalette_ColorSelected(object sender, Color e)
        {
            _currentColor = e;
            ThicknessControl.PreviewColor = e;
            ColorChanged?.Invoke(this, e);
            SettingsChanged?.Invoke(this, CurrentSettings);
        }

        private void ThicknessControl_ThicknessChanged(object sender, double e)
        {
            _currentThickness = e;
            ThicknessChanged?.Invoke(this, e);
            SettingsChanged?.Invoke(this, CurrentSettings);
        }

        /// <summary>
        /// Updates the control with the provided settings
        /// </summary>
        public void ApplySettings(AnnotationSettings settings)
        {
            CurrentColor = settings.StrokeColor;
            CurrentThickness = settings.StrokeThickness;
        }
    }
}
