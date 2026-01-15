using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SnipShot.Helpers.Utils;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace SnipShot.Shared.Controls.Common
{
    /// <summary>
    /// Types of color palettes available
    /// </summary>
    public enum ColorPaletteType
    {
        /// <summary>Standard 15-color palette (3 rows)</summary>
        Standard,
        /// <summary>Pen 10-color palette (2 rows)</summary>
        Pen,
        /// <summary>Highlighter semi-transparent 10-color palette (2 rows)</summary>
        Highlighter,
        /// <summary>Text 20-color palette (4 rows)</summary>
        Text,
        /// <summary>Text highlight/background 15-color palette (3 rows) with transparent option</summary>
        TextHighlight
    }

    /// <summary>
    /// A reusable color palette control for selecting colors
    /// </summary>
    public sealed partial class ColorPaletteControl : UserControl
    {
        /// <summary>
        /// Event raised when a color is selected
        /// </summary>
        public event EventHandler<Color>? ColorSelected;

        /// <summary>
        /// Gets or sets the currently selected color
        /// </summary>
        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor),
                typeof(Color),
                typeof(ColorPaletteControl),
                new PropertyMetadata(Microsoft.UI.Colors.White, OnSelectedColorChanged));

        /// <summary>
        /// Gets or sets the title displayed above the palette
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ColorPaletteControl),
                new PropertyMetadata(string.Empty, OnTitleChanged));

        /// <summary>
        /// Gets or sets the type of color palette to display
        /// </summary>
        public ColorPaletteType PaletteType
        {
            get => (ColorPaletteType)GetValue(PaletteTypeProperty);
            set => SetValue(PaletteTypeProperty, value);
        }

        public static readonly DependencyProperty PaletteTypeProperty =
            DependencyProperty.Register(
                nameof(PaletteType),
                typeof(ColorPaletteType),
                typeof(ColorPaletteControl),
                new PropertyMetadata(ColorPaletteType.Standard, OnPaletteTypeChanged));

        private readonly List<Button> _colorButtons = new();
        private Button? _pointerOverButton;
        private bool _buttonsRegistered;

        public ColorPaletteControl()
        {
            this.InitializeComponent();
            UpdatePaletteVisibility();
            Loaded += ColorPaletteControl_Loaded;
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPaletteControl control)
            {
                control.UpdateTitleVisibility();
            }
        }

        private static void OnPaletteTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPaletteControl control)
            {
                control.UpdatePaletteVisibility();
                control.UpdateSelectedIndicator();
            }
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPaletteControl control)
            {
                control.UpdateSelectedIndicator();
            }
        }

        private void UpdateTitleVisibility()
        {
            if (TitleText != null)
            {
                TitleText.Text = Title;
                TitleText.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void UpdatePaletteVisibility()
        {
            if (StandardColorGrid == null || HighlighterColorGrid == null || PenColorGrid == null)
                return;

            StandardColorGrid.Visibility = Visibility.Collapsed;
            HighlighterColorGrid.Visibility = Visibility.Collapsed;
            PenColorGrid.Visibility = Visibility.Collapsed;
            
            if (TextColorGrid != null)
                TextColorGrid.Visibility = Visibility.Collapsed;
            if (TextHighlightColorGrid != null)
                TextHighlightColorGrid.Visibility = Visibility.Collapsed;

            switch (PaletteType)
            {
                case ColorPaletteType.Standard:
                    StandardColorGrid.Visibility = Visibility.Visible;
                    break;
                case ColorPaletteType.Pen:
                    PenColorGrid.Visibility = Visibility.Visible;
                    break;
                case ColorPaletteType.Highlighter:
                    HighlighterColorGrid.Visibility = Visibility.Visible;
                    break;
                case ColorPaletteType.Text:
                    if (TextColorGrid != null)
                        TextColorGrid.Visibility = Visibility.Visible;
                    break;
                case ColorPaletteType.TextHighlight:
                    if (TextHighlightColorGrid != null)
                        TextHighlightColorGrid.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ColorPaletteControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_buttonsRegistered)
            {
                UpdateSelectedIndicator();
                return;
            }

            RegisterColorButtons(StandardColorGrid);
            RegisterColorButtons(HighlighterColorGrid);
            RegisterColorButtons(PenColorGrid);
            RegisterColorButtons(TextColorGrid);
            RegisterColorButtons(TextHighlightColorGrid);

            _buttonsRegistered = true;
            UpdateSelectedIndicator();
        }

        private void RegisterColorButtons(Grid? grid)
        {
            if (grid == null)
            {
                return;
            }

            foreach (var child in grid.Children)
            {
                if (child is Button button)
                {
                    button.PointerEntered += ColorButton_PointerEntered;
                    button.PointerExited += ColorButton_PointerExited;
                    _colorButtons.Add(button);
                    UpdateButtonVisual(button, isPointerOver: false);
                }
            }
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

        private void UpdateSelectedIndicator()
        {
            if (_colorButtons.Count == 0)
            {
                return;
            }

            foreach (var button in _colorButtons)
            {
                var isPointerOver = ReferenceEquals(button, _pointerOverButton);
                UpdateButtonVisual(button, isPointerOver);
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

            return color.Equals(SelectedColor);
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

        private static Brush GetThemeBrush(string resourceKey, Brush fallback)
        {
            if (Application.Current?.Resources?.TryGetValue(resourceKey, out var value) == true &&
                value is Brush brush)
            {
                return brush;
            }

            return fallback;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorHex)
            {
                if (ColorConverter.TryParseHexColor(colorHex, out var color))
                {
                    SelectedColor = color;
                    ColorSelected?.Invoke(this, color);
                }
            }
        }
    }
}
