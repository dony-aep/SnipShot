using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipShot.Models;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace SnipShot.Features.Capture.Toolbars
{
    /// <summary>
    /// Represents different types of secondary toolbars
    /// </summary>
    public enum SecondaryToolbarType
    {
        None,
        Shapes,
        Style,
        Fill,
        Pen,
        Highlighter,
        Text,
        TextColor,
        TextHighlight,
        Emoji
    }

    /// <summary>
    /// Manages the positioning and visibility of floating toolbars in the capture window
    /// </summary>
    public class FloatingToolbarManager
    {
        private readonly Canvas _canvas;
        private readonly FrameworkElement _rootElement;
        private readonly FrameworkElement? _mainToolbar;
        private readonly Dictionary<SecondaryToolbarType, FrameworkElement> _secondaryToolbars;
        private readonly Dictionary<SecondaryToolbarType, Button?> _associatedButtons;

        private SecondaryToolbarType _activeSecondaryToolbar = SecondaryToolbarType.None;
        private Rect _selectionBounds;
        private Rect? _monitorBounds; // Límites del monitor actual para limitar el posicionamiento
        private readonly bool _isEditorMode;

        /// <summary>
        /// Event raised when a secondary toolbar is shown or hidden
        /// </summary>
        public event EventHandler<SecondaryToolbarType>? SecondaryToolbarChanged;

        /// <summary>
        /// Gets the currently active secondary toolbar
        /// </summary>
        public SecondaryToolbarType ActiveSecondaryToolbar => _activeSecondaryToolbar;

        /// <summary>
        /// Gets whether the manager is in editor mode (no main toolbar, centered positioning)
        /// </summary>
        public bool IsEditorMode => _isEditorMode;

        /// <summary>
        /// Creates a new FloatingToolbarManager for capture windows (selection-based positioning)
        /// </summary>
        /// <param name="canvas">The canvas containing the toolbars</param>
        /// <param name="rootElement">The root element for size calculations</param>
        /// <param name="mainToolbar">The main floating toolbar element</param>
        public FloatingToolbarManager(Canvas canvas, FrameworkElement rootElement, FrameworkElement mainToolbar)
            : this(canvas, rootElement, mainToolbar, isEditorMode: false)
        {
        }

        /// <summary>
        /// Creates a new FloatingToolbarManager for editor mode (centered positioning)
        /// </summary>
        /// <param name="canvas">The canvas containing the toolbars</param>
        /// <param name="rootElement">The root element for size calculations</param>
        public static FloatingToolbarManager CreateForEditorMode(Canvas canvas, FrameworkElement rootElement)
        {
            return new FloatingToolbarManager(canvas, rootElement, mainToolbar: null, isEditorMode: true);
        }

        /// <summary>
        /// Private constructor for internal initialization
        /// </summary>
        private FloatingToolbarManager(Canvas canvas, FrameworkElement rootElement, FrameworkElement? mainToolbar, bool isEditorMode)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            _mainToolbar = mainToolbar;
            _isEditorMode = isEditorMode;
            _secondaryToolbars = new Dictionary<SecondaryToolbarType, FrameworkElement>();
            _associatedButtons = new Dictionary<SecondaryToolbarType, Button?>();
        }

        /// <summary>
        /// Registers a secondary toolbar
        /// </summary>
        /// <param name="type">The type of toolbar</param>
        /// <param name="toolbar">The toolbar element</param>
        /// <param name="associatedButton">Optional button that toggles this toolbar</param>
        public void RegisterSecondaryToolbar(SecondaryToolbarType type, FrameworkElement toolbar, Button? associatedButton = null)
        {
            _secondaryToolbars[type] = toolbar;
            _associatedButtons[type] = associatedButton;
        }

        public void SetAssociatedButton(SecondaryToolbarType type, Button? button)
        {
            _associatedButtons[type] = button;
        }

        /// <summary>
        /// Updates the selection bounds for toolbar positioning
        /// </summary>
        public void UpdateSelectionBounds(Rect bounds)
        {
            _selectionBounds = bounds;
        }

        /// <summary>
        /// Establece los límites del monitor actual para limitar el posicionamiento del toolbar
        /// </summary>
        /// <param name="monitorBounds">Límites del monitor en coordenadas de UI (no píxeles físicos)</param>
        public void UpdateMonitorBounds(Rect monitorBounds)
        {
            _monitorBounds = monitorBounds;
        }

        /// <summary>
        /// Limpia los límites del monitor (vuelve a usar el contenedor completo)
        /// </summary>
        public void ClearMonitorBounds()
        {
            _monitorBounds = null;
        }

        /// <summary>
        /// Positions the main toolbar based on the current selection
        /// </summary>
        public void PositionMainToolbar()
        {
            if (_mainToolbar == null) return;
            
            PositionMainToolbar(
                _rootElement.ActualWidth,
                _rootElement.ActualHeight,
                _selectionBounds.X,
                _selectionBounds.Y,
                _selectionBounds.Width,
                _selectionBounds.Height);
        }

        /// <summary>
        /// Positions the main toolbar with explicit parameters
        /// </summary>
        public void PositionMainToolbar(double containerWidth, double containerHeight,
            double selectionX, double selectionY, double selectionWidth, double selectionHeight)
        {
            if (_mainToolbar == null) return;
            _mainToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double toolbarWidth = _mainToolbar.DesiredSize.Width > 0 ? _mainToolbar.DesiredSize.Width : 350;
            double toolbarHeight = _mainToolbar.DesiredSize.Height > 0 ? _mainToolbar.DesiredSize.Height : 50;

            // Usar límites del monitor si están establecidos, sino usar el contenedor completo
            double boundsLeft = _monitorBounds?.X ?? 0;
            double boundsTop = _monitorBounds?.Y ?? 0;
            double boundsRight = _monitorBounds?.Right ?? containerWidth;
            double boundsBottom = _monitorBounds?.Bottom ?? containerHeight;
            double boundsWidth = boundsRight - boundsLeft;
            double boundsHeight = boundsBottom - boundsTop;

            double toolbarX = selectionX + (selectionWidth - toolbarWidth) / 2;
            // Limitar al área del monitor
            toolbarX = Math.Max(boundsLeft + Constants.DISPLAY_MARGIN, 
                Math.Min(toolbarX, boundsRight - toolbarWidth - Constants.DISPLAY_MARGIN));

            // Posicionar debajo de la selección preferentemente.
            // Si no hay espacio debajo, posicionar arriba.
            // Si no hay espacio ni arriba ni abajo, posicionar DENTRO del área seleccionada.
            double toolbarY;
            double spaceBelow = boundsBottom - (selectionY + selectionHeight + Constants.DISPLAY_MARGIN);
            double spaceAbove = selectionY - boundsTop - Constants.DISPLAY_MARGIN;
            const double innerPadding = 12.0; // Padding interno para no ocultar handles

            if (spaceBelow >= toolbarHeight + Constants.DISPLAY_MARGIN)
            {
                // Hay espacio debajo: posicionar debajo
                toolbarY = selectionY + selectionHeight + Constants.DISPLAY_MARGIN;
            }
            else if (spaceAbove >= toolbarHeight + Constants.DISPLAY_MARGIN)
            {
                // No hay espacio debajo pero sí arriba: posicionar arriba
                toolbarY = selectionY - toolbarHeight - Constants.DISPLAY_MARGIN;
            }
            else
            {
                // No hay espacio ni arriba ni abajo: posicionar DENTRO del área seleccionada
                // Centrar horizontalmente dentro de la selección (sin importar el espacio disponible)
                toolbarX = selectionX + (selectionWidth - toolbarWidth) / 2;
                
                // Posicionar en la parte inferior del área seleccionada con padding
                toolbarY = selectionY + selectionHeight - toolbarHeight - innerPadding;
                
                // Si no cabe dentro verticalmente, centrar verticalmente
                if (toolbarY < selectionY + innerPadding)
                {
                    toolbarY = selectionY + (selectionHeight - toolbarHeight) / 2;
                }
            }

            // Ensure within monitor bounds and round to avoid subpixel positioning (causes blur)
            toolbarY = Math.Max(boundsTop + Constants.DISPLAY_MARGIN, 
                Math.Min(toolbarY, boundsBottom - toolbarHeight - Constants.DISPLAY_MARGIN));

            // Redondear a píxeles enteros para evitar borrosidad
            Canvas.SetLeft(_mainToolbar, Math.Round(toolbarX));
            Canvas.SetTop(_mainToolbar, Math.Round(toolbarY));

            // Update secondary toolbars
            UpdateSecondaryToolbarPositions();
        }

        /// <summary>
        /// Shows the main toolbar
        /// </summary>
        public void ShowMainToolbar()
        {
            if (_mainToolbar == null) return;
            
            _mainToolbar.Visibility = Visibility.Visible;
            PositionMainToolbar();
        }

        /// <summary>
        /// Hides the main toolbar and all secondary toolbars
        /// </summary>
        public void HideMainToolbar()
        {
            if (_mainToolbar != null)
            {
                _mainToolbar.Visibility = Visibility.Collapsed;
            }
            CollapseAllSecondaryToolbars();
        }

        /// <summary>
        /// Toggles a secondary toolbar
        /// </summary>
        /// <param name="type">The type of toolbar to toggle</param>
        /// <returns>True if the toolbar is now visible</returns>
        public bool ToggleSecondaryToolbar(SecondaryToolbarType type)
        {
            if (!_secondaryToolbars.TryGetValue(type, out var toolbar))
                return false;

            if (toolbar.Visibility == Visibility.Visible)
            {
                CollapseSecondaryToolbar(type);
                return false;
            }
            else
            {
                ShowSecondaryToolbar(type);
                return true;
            }
        }

        /// <summary>
        /// Shows a secondary toolbar
        /// </summary>
        /// <param name="type">The type of toolbar to show</param>
        /// <param name="preserveShapesToolbar">If true, keeps the Shapes toolbar visible</param>
        public void ShowSecondaryToolbar(SecondaryToolbarType type, bool preserveShapesToolbar = false)
        {
            if (!_secondaryToolbars.TryGetValue(type, out var toolbar))
                return;

            // Determine which toolbars to preserve based on context
            var toolbarsToPreserve = new List<SecondaryToolbarType> { type };
            
            // If showing text color or highlight, preserve the main text toolbar
            if (type == SecondaryToolbarType.TextColor || type == SecondaryToolbarType.TextHighlight)
            {
                toolbarsToPreserve.Add(SecondaryToolbarType.Text);
            }
            
            // Optionally preserve Shapes toolbar (for Fill/Style)
            if (preserveShapesToolbar)
            {
                toolbarsToPreserve.Add(SecondaryToolbarType.Shapes);
            }

            // Collapse other secondary toolbars
            CollapseSecondaryToolbarsExcept(toolbarsToPreserve.ToArray());

            _activeSecondaryToolbar = type;
            toolbar.Visibility = Visibility.Visible;
            PositionSecondaryToolbar(type);

            SecondaryToolbarChanged?.Invoke(this, type);
        }

        /// <summary>
        /// Collapses all secondary toolbars except the specified ones
        /// </summary>
        private void CollapseSecondaryToolbarsExcept(params SecondaryToolbarType[] exceptions)
        {
            var exceptSet = new HashSet<SecondaryToolbarType>(exceptions);
            
            foreach (var kvp in _secondaryToolbars)
            {
                if (!exceptSet.Contains(kvp.Key))
                {
                    kvp.Value.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Collapses a specific secondary toolbar
        /// </summary>
        /// <param name="type">The type of toolbar to collapse</param>
        public void CollapseSecondaryToolbar(SecondaryToolbarType type)
        {
            if (!_secondaryToolbars.TryGetValue(type, out var toolbar))
                return;

            toolbar.Visibility = Visibility.Collapsed;
            
            if (_activeSecondaryToolbar == type)
            {
                _activeSecondaryToolbar = SecondaryToolbarType.None;
            }

            SecondaryToolbarChanged?.Invoke(this, SecondaryToolbarType.None);
        }

        /// <summary>
        /// Collapses all secondary toolbars
        /// </summary>
        public void CollapseAllSecondaryToolbars()
        {
            foreach (var kvp in _secondaryToolbars)
            {
                kvp.Value.Visibility = Visibility.Collapsed;
            }

            _activeSecondaryToolbar = SecondaryToolbarType.None;
        }

        /// <summary>
        /// Updates positions of all visible secondary toolbars
        /// </summary>
        public void UpdateSecondaryToolbarPositions()
        {
            foreach (var kvp in _secondaryToolbars)
            {
                if (kvp.Value.Visibility == Visibility.Visible)
                {
                    PositionSecondaryToolbar(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Positions a secondary toolbar relative to its associated button or the main toolbar
        /// </summary>
        private void PositionSecondaryToolbar(SecondaryToolbarType type)
        {
            if (!_secondaryToolbars.TryGetValue(type, out var toolbar))
                return;

            // Measure the toolbar
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double toolbarWidth = toolbar.DesiredSize.Width > 0 ? toolbar.DesiredSize.Width : 200;
            double toolbarHeight = toolbar.DesiredSize.Height > 0 ? toolbar.DesiredSize.Height : 150;

            double left = Constants.DISPLAY_MARGIN;
            double top = Constants.DISPLAY_MARGIN;
            bool positioned = false;

            // Check if there's an associated button
            if (_associatedButtons.TryGetValue(type, out var button) && button != null)
            {
                button.UpdateLayout();
                bool preferBelow = _isEditorMode || IsSubmenuToolbar(type);
                bool centerOnButton = _isEditorMode || IsSubmenuToolbar(type);
                try
                {
                    var position = CalculatePositionRelativeToButton(button, toolbarWidth, toolbarHeight, preferBelow, centerOnButton);
                    left = position.X;
                    top = position.Y;
                    positioned = true;
                }
                catch (ArgumentException)
                {
                    positioned = false;
                }
            }

            if (!positioned && _isEditorMode)
            {
                CenterToolbarHorizontally(toolbar);
                return;
            }

            if (!positioned && _mainToolbar != null)
            {
                // Position relative to main toolbar
                double mainToolbarLeft = Canvas.GetLeft(_mainToolbar);
                double mainToolbarTop = Canvas.GetTop(_mainToolbar);

                _mainToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double mainToolbarWidth = _mainToolbar.DesiredSize.Width;

                left = mainToolbarLeft + (mainToolbarWidth - toolbarWidth) / 2;
                top = mainToolbarTop - toolbarHeight - Constants.DISPLAY_MARGIN;

                // If not enough space above, position below
                if (top < Constants.DISPLAY_MARGIN)
                {
                    top = mainToolbarTop + _mainToolbar.DesiredSize.Height + Constants.DISPLAY_MARGIN;
                }
            }
            if (!positioned && _mainToolbar == null)
            {
                // Fallback: center the toolbar
                left = (_rootElement.ActualWidth - toolbarWidth) / 2;
                top = Constants.DISPLAY_MARGIN;
            }

            // Ensure within bounds
            left = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(left, _rootElement.ActualWidth - toolbarWidth - Constants.DISPLAY_MARGIN));
            top = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(top, _rootElement.ActualHeight - toolbarHeight - Constants.DISPLAY_MARGIN));

            Canvas.SetLeft(toolbar, left);
            Canvas.SetTop(toolbar, top);
        }

        /// <summary>
        /// Calculates position for a toolbar relative to a button
        /// </summary>
        private Point CalculatePositionRelativeToButton(Button button, double toolbarWidth, double toolbarHeight, bool preferBelow, bool centerOnButton)
        {
            var buttonTransform = button.TransformToVisual(_rootElement);
            var buttonPosition = buttonTransform.TransformPoint(new Point(0, 0));
            double buttonHeight = button.ActualHeight;
            double buttonWidth = button.ActualWidth;

            // Calculate space above and below the button
            double spaceAbove = buttonPosition.Y - Constants.DISPLAY_MARGIN;
            double spaceBelow = _rootElement.ActualHeight - (buttonPosition.Y + buttonHeight + Constants.DISPLAY_MARGIN);

            double top;

            if (preferBelow)
            {
                if (spaceBelow >= toolbarHeight || spaceBelow >= spaceAbove)
                {
                    top = buttonPosition.Y + buttonHeight + Constants.DISPLAY_MARGIN;
                }
                else
                {
                    top = buttonPosition.Y - toolbarHeight - Constants.DISPLAY_MARGIN;
                }
            }
            else if (spaceAbove >= toolbarHeight || spaceAbove > spaceBelow)
            {
                top = buttonPosition.Y - toolbarHeight - Constants.DISPLAY_MARGIN;
            }
            else
            {
                top = buttonPosition.Y + buttonHeight + Constants.DISPLAY_MARGIN;
            }

            // Align horizontally with button
            double left = centerOnButton
                ? buttonPosition.X + (buttonWidth - toolbarWidth) / 2
                : buttonPosition.X;

            left = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(left, _rootElement.ActualWidth - toolbarWidth - Constants.DISPLAY_MARGIN));
            top = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(top, _rootElement.ActualHeight - toolbarHeight - Constants.DISPLAY_MARGIN));

            return new Point(left, top);
        }

        private static bool IsSubmenuToolbar(SecondaryToolbarType type)
        {
            return type == SecondaryToolbarType.Fill ||
                   type == SecondaryToolbarType.Style ||
                   type == SecondaryToolbarType.TextColor ||
                   type == SecondaryToolbarType.TextHighlight;
        }

        /// <summary>
        /// Checks if a specific secondary toolbar is visible
        /// </summary>
        public bool IsSecondaryToolbarVisible(SecondaryToolbarType type)
        {
            return _secondaryToolbars.TryGetValue(type, out var toolbar) && toolbar.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Gets a secondary toolbar element by type
        /// </summary>
        public FrameworkElement? GetSecondaryToolbar(SecondaryToolbarType type)
        {
            return _secondaryToolbars.TryGetValue(type, out var toolbar) ? toolbar : null;
        }

        #region Editor Mode (Centered Toolbars)

        /// <summary>
        /// Centers a toolbar horizontally at a fixed top position.
        /// Used for editor mode where there's no selection to position around.
        /// </summary>
        /// <param name="toolbar">The toolbar to center</param>
        /// <param name="topOffset">The distance from the top of the container</param>
        public void CenterToolbarHorizontally(FrameworkElement toolbar, double topOffset = 16)
        {
            toolbar.UpdateLayout();
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            
            double toolbarWidth = toolbar.DesiredSize.Width > 0 ? toolbar.DesiredSize.Width : toolbar.ActualWidth;
            if (toolbarWidth == 0) toolbarWidth = 400; // Default fallback
            
            double containerWidth = _rootElement.ActualWidth;
            double centerX = (containerWidth - toolbarWidth) / 2;
            centerX = Math.Max(Constants.DISPLAY_MARGIN, centerX);
            
            Canvas.SetLeft(toolbar, centerX);
            Canvas.SetTop(toolbar, topOffset);
        }

        /// <summary>
        /// Shows a toolbar centered horizontally (editor mode).
        /// Hides all other secondary toolbars first.
        /// </summary>
        /// <param name="type">The type of toolbar to show</param>
        /// <param name="topOffset">The distance from the top of the container</param>
        public void ShowToolbarCentered(SecondaryToolbarType type, double topOffset = 16)
        {
            if (!_secondaryToolbars.TryGetValue(type, out var toolbar))
                return;

            // Hide all secondary toolbars first
            CollapseAllSecondaryToolbars();
            
            // Show and center the requested toolbar
            toolbar.Visibility = Visibility.Visible;
            CenterToolbarHorizontally(toolbar, topOffset);
            
            _activeSecondaryToolbar = type;
            SecondaryToolbarChanged?.Invoke(this, type);
        }

        /// <summary>
        /// Centers all registered secondary toolbars (used during initialization in editor mode)
        /// </summary>
        /// <param name="topOffset">The distance from the top of the container</param>
        public void CenterAllSecondaryToolbars(double topOffset = 16)
        {
            foreach (var toolbar in _secondaryToolbars.Values)
            {
                CenterToolbarHorizontally(toolbar, topOffset);
            }
        }

        #endregion
    }
}
