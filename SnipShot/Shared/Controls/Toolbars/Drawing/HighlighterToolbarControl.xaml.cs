using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Drawing
{
    /// <summary>
    /// Toolbar control for highlighter settings (color and thickness)
    /// </summary>
    public sealed partial class HighlighterToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when highlighter settings change
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

        private Color _currentColor = Color.FromArgb(128, 255, 255, 0); // Semi-transparent yellow
        private double _currentThickness = 16.0;
        private readonly List<Button> _colorButtons = new();
        private Button? _pointerOverButton;
        private bool _buttonsRegistered;

        /// <summary>
        /// Gets or sets the current highlighter color
        /// </summary>
        public Color CurrentColor
        {
            get => _currentColor;
            set
            {
                _currentColor = value;
                ThicknessControl.PreviewColor = value;
                UpdateColorButtonVisuals();
            }
        }

        /// <summary>
        /// Gets or sets the current highlighter thickness
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
            StrokeColor = Color.FromArgb(255, _currentColor.R, _currentColor.G, _currentColor.B),
            StrokeThickness = _currentThickness,
            StrokeOpacity = _currentColor.A / 255.0,
            FillEnabled = false
        };

        public HighlighterToolbarControl()
        {
            this.InitializeComponent();
            this.Loaded += HighlighterToolbarControl_Loaded;
        }

        private void HighlighterToolbarControl_Loaded(object sender, RoutedEventArgs e)
        {
            ThicknessControl.PreviewColor = _currentColor;
            RegisterColorButtons();
            UpdateColorButtonVisuals();
        }

        private void HighlighterColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorHex)
            {
                if (ColorConverter.TryParseHexColor(colorHex, out var color))
                {
                    _currentColor = color;
                    ThicknessControl.PreviewColor = color;
                    ColorChanged?.Invoke(this, color);
                    SettingsChanged?.Invoke(this, CurrentSettings);
                    UpdateColorButtonVisuals();
                }
            }
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
            var color = Color.FromArgb(
                (byte)(settings.StrokeOpacity * 255),
                settings.StrokeColor.R,
                settings.StrokeColor.G,
                settings.StrokeColor.B);
            CurrentColor = color;
            CurrentThickness = settings.StrokeThickness;
        }

        private void RegisterColorButtons()
        {
            if (_buttonsRegistered || HighlighterColorGrid == null)
            {
                return;
            }

            foreach (var child in HighlighterColorGrid.Children)
            {
                if (child is Button button)
                {
                    button.PointerEntered += ColorButton_PointerEntered;
                    button.PointerExited += ColorButton_PointerExited;
                    _colorButtons.Add(button);
                    UpdateButtonVisual(button, isPointerOver: false);
                }
            }

            _buttonsRegistered = true;
        }

        private void ColorButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                _pointerOverButton = button;
                UpdateButtonVisual(button, isPointerOver: true);
            }
        }

        private void ColorButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ReferenceEquals(_pointerOverButton, button))
                {
                    _pointerOverButton = null;
                }
                UpdateButtonVisual(button, isPointerOver: false);
            }
        }

        private void UpdateColorButtonVisuals()
        {
            foreach (var button in _colorButtons)
            {
                var isPointerOver = ReferenceEquals(button, _pointerOverButton);
                UpdateButtonVisual(button, isPointerOver);
            }
        }

        private void UpdateButtonVisual(Button button, bool isPointerOver)
        {
            if (button.Content is not Border border)
            {
                return;
            }

            var isSelected = IsButtonSelected(button);
            var defaultBrush = GetThemeBrush("CardStrokeColorDefaultBrush", BrushCache.GetBrush(Microsoft.UI.Colors.Gray));
            var accentBrush = GetThemeBrush("SystemControlForegroundAccentBrush", defaultBrush);
            var hoverBrush = GetThemeBrush("SystemControlHighlightAccentBrush", accentBrush);

            if (isSelected)
            {
                border.BorderThickness = new Thickness(2);
                border.BorderBrush = accentBrush;
            }
            else if (isPointerOver)
            {
                border.BorderThickness = new Thickness(2);
                border.BorderBrush = hoverBrush;
            }
            else
            {
                border.BorderThickness = new Thickness(1);
                border.BorderBrush = defaultBrush;
            }
        }

        private bool IsButtonSelected(Button button)
        {
            if (button.Tag is not string colorHex)
            {
                return false;
            }

            if (!ColorConverter.TryParseHexColor(colorHex, out var color))
            {
                return false;
            }

            return color.Equals(_currentColor);
        }

        private static Brush GetThemeBrush(string resourceKey, Brush fallback)
        {
            if (Application.Current?.Resources?.TryGetValue(resourceKey, out var value) == true &&
                value is Brush brush)
            {
                return brush;
            }

            return fallback;
        }
    }
}
