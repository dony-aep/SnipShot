using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SnipShot.Helpers.Utils;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Common
{
    /// <summary>
    /// A reusable thickness slider control with preview
    /// </summary>
    public sealed partial class ThicknessSliderControl : UserControl
    {
        /// <summary>
        /// Event raised when the thickness value changes
        /// </summary>
        public event EventHandler<double>? ThicknessChanged;

        /// <summary>
        /// Gets or sets the current thickness value
        /// </summary>
        public double Thickness
        {
            get => (double)GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        public static readonly DependencyProperty ThicknessProperty =
            DependencyProperty.Register(
                nameof(Thickness),
                typeof(double),
                typeof(ThicknessSliderControl),
                new PropertyMetadata(2.0, OnThicknessPropertyChanged));

        /// <summary>
        /// Gets or sets the minimum thickness value
        /// </summary>
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(ThicknessSliderControl),
                new PropertyMetadata(1.0, OnMinMaxPropertyChanged));

        /// <summary>
        /// Gets or sets the maximum thickness value
        /// </summary>
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(ThicknessSliderControl),
                new PropertyMetadata(12.0, OnMinMaxPropertyChanged));

        /// <summary>
        /// Gets or sets the preview color
        /// </summary>
        public Color PreviewColor
        {
            get => (Color)GetValue(PreviewColorProperty);
            set => SetValue(PreviewColorProperty, value);
        }

        public static readonly DependencyProperty PreviewColorProperty =
            DependencyProperty.Register(
                nameof(PreviewColor),
                typeof(Color),
                typeof(ThicknessSliderControl),
                new PropertyMetadata(Microsoft.UI.Colors.Black, OnPreviewColorChanged));

        /// <summary>
        /// Gets or sets the title text
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
                typeof(ThicknessSliderControl),
                new PropertyMetadata("Tamaño", OnTitleChanged));

        public ThicknessSliderControl()
        {
            this.InitializeComponent();
            this.Loaded += ThicknessSliderControl_Loaded;
        }

        private void ThicknessSliderControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSlider();
            UpdatePreview();
        }

        private static void OnThicknessPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThicknessSliderControl control)
            {
                control.UpdateSlider();
                control.UpdatePreview();
            }
        }

        private static void OnMinMaxPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThicknessSliderControl control)
            {
                control.UpdateSlider();
            }
        }

        private static void OnPreviewColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThicknessSliderControl control)
            {
                control.UpdatePreview();
            }
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ThicknessSliderControl control && e.NewValue is string title)
            {
                control.TitleText.Text = title;
            }
        }

        private void UpdateSlider()
        {
            ThicknessSlider.Minimum = Minimum;
            ThicknessSlider.Maximum = Maximum;
            ThicknessSlider.Value = Thickness;
        }

        private void UpdatePreview()
        {
            ThicknessPreview.Height = Math.Max(1, Thickness);
            ThicknessPreview.Fill = BrushCache.GetBrush(PreviewColor);
        }

        private void ThicknessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Thickness = e.NewValue;
            UpdatePreview();
            ThicknessChanged?.Invoke(this, e.NewValue);
        }
    }
}
