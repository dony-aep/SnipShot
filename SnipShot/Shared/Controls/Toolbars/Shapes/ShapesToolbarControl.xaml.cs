using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SnipShot.Features.Capture.Annotations.Managers;
using SnipShot.Helpers.Utils;
using System;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Shapes
{
    /// <summary>
    /// Shape type selection enumeration
    /// </summary>
    public enum ShapeType
    {
        None,
        Square,
        Circle,
        Line,
        Arrow,
        Star
    }

    /// <summary>
    /// Toolbar control for selecting shapes and accessing style/fill options
    /// </summary>
    public sealed partial class ShapesToolbarControl : UserControl
    {
        /// <summary>
        /// Event raised when a shape is selected
        /// </summary>
        public event EventHandler<ShapeType>? ShapeSelected;

        /// <summary>
        /// Event raised when the fill button is clicked
        /// </summary>
        public event EventHandler? FillButtonClicked;

        /// <summary>
        /// Event raised when the style button is clicked
        /// </summary>
        public event EventHandler? StyleButtonClicked;

        private ShapeType _selectedShape = ShapeType.None;

        /// <summary>
        /// Gets the currently selected shape type
        /// </summary>
        public ShapeType SelectedShape
        {
            get => _selectedShape;
            set
            {
                _selectedShape = value;
                UpdateButtonStates();
            }
        }

        /// <summary>
        /// Gets or sets whether the fill button is enabled
        /// </summary>
        public bool IsFillEnabled
        {
            get => FillButton.IsEnabled;
            set => FillButton.IsEnabled = value;
        }

        public Button FillAnchorButton => FillButton;

        public Button StyleAnchorButton => StyleButton;

        public ShapesToolbarControl()
        {
            this.InitializeComponent();
        }

        private void SquareButton_Click(object sender, RoutedEventArgs e)
        {
            SelectShape(ShapeType.Square);
        }

        private void CircleButton_Click(object sender, RoutedEventArgs e)
        {
            SelectShape(ShapeType.Circle);
        }

        private void LineButton_Click(object sender, RoutedEventArgs e)
        {
            SelectShape(ShapeType.Line);
        }

        private void ArrowButton_Click(object sender, RoutedEventArgs e)
        {
            SelectShape(ShapeType.Arrow);
        }

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            SelectShape(ShapeType.Star);
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            FillButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void StyleButton_Click(object sender, RoutedEventArgs e)
        {
            StyleButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SelectShape(ShapeType shape)
        {
            // Toggle selection if clicking the same shape
            if (_selectedShape == shape)
            {
                _selectedShape = ShapeType.None;
            }
            else
            {
                _selectedShape = shape;
            }

            // Enable fill for shapes that support it
            IsFillEnabled = _selectedShape == ShapeType.Square || _selectedShape == ShapeType.Circle || _selectedShape == ShapeType.Star;

            UpdateButtonStates();
            ShapeSelected?.Invoke(this, _selectedShape);
        }

        private void UpdateButtonStates()
        {
            var selectedBrush = BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
            var transparentBrush = BrushCache.Transparent;

            SquareButton.Background = _selectedShape == ShapeType.Square ? selectedBrush : transparentBrush;
            CircleButton.Background = _selectedShape == ShapeType.Circle ? selectedBrush : transparentBrush;
            LineButton.Background = _selectedShape == ShapeType.Line ? selectedBrush : transparentBrush;
            ArrowButton.Background = _selectedShape == ShapeType.Arrow ? selectedBrush : transparentBrush;
            StarButton.Background = _selectedShape == ShapeType.Star ? selectedBrush : transparentBrush;
        }

        /// <summary>
        /// Converts ShapeType to AnnotationToolType
        /// </summary>
        public static AnnotationToolType ToAnnotationToolType(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Square => AnnotationToolType.Rectangle,
                ShapeType.Circle => AnnotationToolType.Ellipse,
                ShapeType.Line => AnnotationToolType.Line,
                ShapeType.Arrow => AnnotationToolType.Arrow,
                ShapeType.Star => AnnotationToolType.Star,
                _ => AnnotationToolType.None
            };
        }

        /// <summary>
        /// Clears the current selection
        /// </summary>
        public void ClearSelection()
        {
            _selectedShape = ShapeType.None;
            IsFillEnabled = false;
            UpdateButtonStates();
        }

        /// <summary>
        /// Sets the fill button expanded state (changes background and arrow icon)
        /// </summary>
        public void SetFillExpanded(bool expanded)
        {
            var selectedBrush = BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
            var transparentBrush = BrushCache.Transparent;

            FillButton.Background = expanded ? selectedBrush : transparentBrush;
            FillArrowIcon.Glyph = expanded ? "\uE70E" : "\uE70D"; // ChevronUp : ChevronDown
        }

        /// <summary>
        /// Sets the style button expanded state (changes background and arrow icon)
        /// </summary>
        public void SetStyleExpanded(bool expanded)
        {
            var selectedBrush = BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
            var transparentBrush = BrushCache.Transparent;

            StyleButton.Background = expanded ? selectedBrush : transparentBrush;
            StyleArrowIcon.Glyph = expanded ? "\uE70E" : "\uE70D"; // ChevronUp : ChevronDown
        }

        /// <summary>
        /// Sets the selected shape from AnnotationToolType
        /// </summary>
        public void SetSelectedFromToolType(AnnotationToolType toolType)
        {
            _selectedShape = toolType switch
            {
                AnnotationToolType.Rectangle => ShapeType.Square,
                AnnotationToolType.Ellipse => ShapeType.Circle,
                AnnotationToolType.Line => ShapeType.Line,
                AnnotationToolType.Arrow => ShapeType.Arrow,
                AnnotationToolType.Star => ShapeType.Star,
                _ => ShapeType.None
            };
            
            IsFillEnabled = _selectedShape == ShapeType.Square || _selectedShape == ShapeType.Circle || _selectedShape == ShapeType.Star;
            UpdateButtonStates();
        }

        /// <summary>
        /// Selects a default shape and raises the ShapeSelected event
        /// </summary>
        public void SelectDefaultShape()
        {
            // Si no hay forma seleccionada, seleccionar cuadrado por defecto
            if (_selectedShape == ShapeType.None)
            {
                _selectedShape = ShapeType.Square;
                IsFillEnabled = true;
                UpdateButtonStates();
                ShapeSelected?.Invoke(this, _selectedShape);
            }
        }
    }
}
