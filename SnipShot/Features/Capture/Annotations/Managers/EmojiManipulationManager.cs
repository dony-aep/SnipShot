using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Features.Capture.Annotations.Tools;
using SnipShot.Helpers.Utils;
using System;
using Windows.Foundation;
using Windows.UI;

namespace SnipShot.Features.Capture.Annotations.Managers
{
    /// <summary>
    /// Handle types for emoji element resizing
    /// </summary>
    public enum EmojiHandle
    {
        None,
        NorthWest,  // Top-left corner
        NorthEast,  // Top-right corner
        SouthWest,  // Bottom-left corner
        SouthEast,  // Bottom-right corner
        Rotation    // Rotation handle above the emoji
    }

    /// <summary>
    /// Manages selection, dragging, and resizing of emoji elements.
    /// Similar to TextManipulationManager but with proportional scaling.
    /// </summary>
    public class EmojiManipulationManager
    {
        private readonly Canvas _emojiCanvas;
        private readonly Canvas _handlesCanvas;
        private readonly AnnotationHistoryManager? _historyManager;

        // Selection state
        private Grid? _selectedEmoji;
        private EmojiData? _originalEmojiData;
        private bool _isDragging;
        private bool _isResizing;
        private Point _dragStart;
        private EmojiHandle _activeHandle = EmojiHandle.None;
        private Rect _boundsBeforeResize;
        private Point _fixedCornerRotated; // Punto fijo en coordenadas de pantalla (después de rotación)
        private double _rotationAngleBeforeResize; // Ángulo de rotación al iniciar resize

        // Handle elements
        private readonly Rectangle _handleNW;
        private readonly Rectangle _handleNE;
        private readonly Rectangle _handleSW;
        private readonly Rectangle _handleSE;
        private readonly Border _handleRotation;
        private readonly Line _rotationLine;
        private readonly Rectangle _selectionBorder;
        private bool _isRotating;
        private double _rotationStartAngle;
        private Point _rotationCenter;

        // Handle design constants
        private const double HANDLE_SIZE = 12;
        private const double HANDLE_STROKE_WIDTH = 2;
        private const double MIN_EMOJI_SIZE = 24;
        private const double MAX_EMOJI_SIZE = 400;
        private const double ROTATION_HANDLE_DISTANCE = 30; // Distance above the emoji
        private const double ROTATION_HANDLE_SIZE = 24; // Same as shapes

        // Colors for handles - usando blanco con borde gris oscuro para mejor visibilidad
        private static readonly Color HandleFillColor = Colors.White;
        private static readonly Color HandleStrokeColor = Color.FromArgb(255, 80, 80, 80); // Gris oscuro
        private static readonly Color HandleHoverFillColor = Color.FromArgb(255, 220, 220, 220); // Gris claro on hover
        private static readonly Color SelectionBorderColor = Color.FromArgb(255, 100, 100, 100); // Gris para borde de selección

        /// <summary>
        /// Event raised when the selected emoji changes
        /// </summary>
        public event EventHandler<Grid?>? SelectionChanged;

        /// <summary>
        /// Event raised when an emoji is moved or resized
        /// </summary>
        public event EventHandler<Grid>? EmojiModified;

        /// <summary>
        /// Gets the currently selected emoji
        /// </summary>
        public Grid? SelectedEmoji => _selectedEmoji;

        /// <summary>
        /// Gets whether an emoji is currently selected
        /// </summary>
        public bool HasSelection => _selectedEmoji != null;

        /// <summary>
        /// Gets whether the user is currently dragging an emoji
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Gets whether the user is currently resizing an emoji
        /// </summary>
        public bool IsResizing => _isResizing;

        /// <summary>
        /// Gets whether the user is rotating an emoji
        /// </summary>
        public bool IsRotating => _isRotating;

        /// <summary>
        /// Gets whether the user is manipulating an emoji
        /// </summary>
        public bool IsManipulating => _isDragging || _isResizing || _isRotating;

        /// <summary>
        /// Creates a new EmojiManipulationManager
        /// </summary>
        public EmojiManipulationManager(
            Canvas emojiCanvas,
            Canvas handlesCanvas,
            AnnotationHistoryManager? historyManager = null)
        {
            _emojiCanvas = emojiCanvas ?? throw new ArgumentNullException(nameof(emojiCanvas));
            _handlesCanvas = handlesCanvas ?? throw new ArgumentNullException(nameof(handlesCanvas));
            _historyManager = historyManager;

            // Create handle elements
            _handleNW = CreateCornerHandle("NW");
            _handleNE = CreateCornerHandle("NE");
            _handleSW = CreateCornerHandle("SW");
            _handleSE = CreateCornerHandle("SE");
            _handleRotation = CreateRotationHandle();
            _rotationLine = CreateRotationLine();
            _selectionBorder = CreateSelectionBorder();

            // Add handles to canvas (initially hidden)
            _handlesCanvas.Children.Add(_selectionBorder);
            _handlesCanvas.Children.Add(_rotationLine);
            _handlesCanvas.Children.Add(_handleNW);
            _handlesCanvas.Children.Add(_handleNE);
            _handlesCanvas.Children.Add(_handleSW);
            _handlesCanvas.Children.Add(_handleSE);
            _handlesCanvas.Children.Add(_handleRotation);

            HideHandles();
        }

        #region Handle Creation

        private Rectangle CreateCornerHandle(string tag)
        {
            var handle = new Rectangle
            {
                Width = HANDLE_SIZE,
                Height = HANDLE_SIZE,
                RadiusX = 2,
                RadiusY = 2,
                Fill = BrushCache.GetBrush(HandleFillColor),
                Stroke = BrushCache.GetBrush(HandleStrokeColor),
                StrokeThickness = HANDLE_STROKE_WIDTH,
                Tag = tag,
                Visibility = Visibility.Collapsed
            };

            handle.PointerEntered += Handle_PointerEntered;
            handle.PointerExited += Handle_PointerExited;
            handle.PointerPressed += Handle_PointerPressed;
            handle.PointerMoved += Handle_PointerMoved;
            handle.PointerReleased += Handle_PointerReleased;

            return handle;
        }

        private Border CreateRotationHandle()
        {
            var icon = new FontIcon
            {
                Glyph = "\uE7AD", // Rotation icon
                FontSize = 12,
                Foreground = BrushCache.GetBrush(HandleStrokeColor)
            };

            var handle = new Border
            {
                Width = ROTATION_HANDLE_SIZE,
                Height = ROTATION_HANDLE_SIZE,
                CornerRadius = new CornerRadius(ROTATION_HANDLE_SIZE / 2),
                Background = BrushCache.GetBrush(HandleFillColor),
                BorderBrush = BrushCache.GetBrush(HandleStrokeColor),
                BorderThickness = new Thickness(HANDLE_STROKE_WIDTH),
                Child = icon,
                Tag = "Rotation",
                Visibility = Visibility.Collapsed
            };

            handle.PointerEntered += RotationHandle_PointerEntered;
            handle.PointerExited += RotationHandle_PointerExited;
            handle.PointerPressed += RotationHandle_PointerPressed;
            handle.PointerMoved += RotationHandle_PointerMoved;
            handle.PointerReleased += RotationHandle_PointerReleased;

            return handle;
        }

        private Line CreateRotationLine()
        {
            return new Line
            {
                Stroke = BrushCache.GetBrush(SelectionBorderColor),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Visibility = Visibility.Collapsed
            };
        }

        private Rectangle CreateSelectionBorder()
        {
            var border = new Rectangle
            {
                Stroke = BrushCache.GetBrush(SelectionBorderColor),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Fill = BrushCache.GetBrush(Color.FromArgb(1, 0, 0, 0)), // Nearly transparent but hit-testable
                RadiusX = 2,
                RadiusY = 2,
                Visibility = Visibility.Collapsed
            };

            // Add drag functionality to selection border
            border.PointerPressed += SelectionBorder_PointerPressed;
            border.PointerMoved += SelectionBorder_PointerMoved;
            border.PointerReleased += SelectionBorder_PointerReleased;
            border.PointerEntered += SelectionBorder_PointerEntered;
            border.PointerExited += SelectionBorder_PointerExited;

            return border;
        }

        #endregion

        #region Selection Border Drag Events

        private void SelectionBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_selectedEmoji != null && !_isResizing)
            {
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(20, 100, 100, 100));
            }
        }

        private void SelectionBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(1, 0, 0, 0));
            }
        }

        private void SelectionBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_selectedEmoji == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Store original state for history
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                _originalEmojiData = emojiData.Clone();
            }

            _isDragging = true;
            _dragStart = point.Position;
            _selectionBorder.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void SelectionBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || _selectedEmoji == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            var delta = new Point(
                point.Position.X - _dragStart.X,
                point.Position.Y - _dragStart.Y
            );

            var currentLeft = Canvas.GetLeft(_selectedEmoji);
            var currentTop = Canvas.GetTop(_selectedEmoji);

            Canvas.SetLeft(_selectedEmoji, currentLeft + delta.X);
            Canvas.SetTop(_selectedEmoji, currentTop + delta.Y);

            // Update EmojiData
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                emojiData.Position = new Point(currentLeft + delta.X, currentTop + delta.Y);
            }

            _dragStart = point.Position;
            UpdateHandles();
            e.Handled = true;
        }

        private void SelectionBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging && _selectedEmoji != null)
            {
                _selectionBorder.ReleasePointerCapture(e.Pointer);
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(1, 0, 0, 0));

                // No se registra en historial - solo el add inicial se registra
                // Ctrl+Z eliminará el emoji completo

                EmojiModified?.Invoke(this, _selectedEmoji);
            }

            _isDragging = false;
            _originalEmojiData = null;
            e.Handled = true;
        }

        #endregion

        #region Handle Events

        private void Handle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Rectangle handle)
            {
                handle.Fill = BrushCache.GetBrush(HandleHoverFillColor);
                handle.Width = HANDLE_SIZE + 2;
                handle.Height = HANDLE_SIZE + 2;

                double currentLeft = Canvas.GetLeft(handle);
                double currentTop = Canvas.GetTop(handle);
                Canvas.SetLeft(handle, currentLeft - 1);
                Canvas.SetTop(handle, currentTop - 1);

                // Set cursor based on handle position
                SetResizeCursor(handle.Tag?.ToString());
            }
        }

        private void Handle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Rectangle handle)
            {
                handle.Fill = BrushCache.GetBrush(HandleFillColor);
                handle.Width = HANDLE_SIZE;
                handle.Height = HANDLE_SIZE;

                double currentLeft = Canvas.GetLeft(handle);
                double currentTop = Canvas.GetTop(handle);
                Canvas.SetLeft(handle, currentLeft + 1);
                Canvas.SetTop(handle, currentTop + 1);
            }
        }

        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement handle || _selectedEmoji == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Store original state for history
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                _originalEmojiData = emojiData.Clone();
            }

            _activeHandle = handle.Tag?.ToString() switch
            {
                "NW" => EmojiHandle.NorthWest,
                "NE" => EmojiHandle.NorthEast,
                "SW" => EmojiHandle.SouthWest,
                "SE" => EmojiHandle.SouthEast,
                _ => EmojiHandle.None
            };

            if (_activeHandle != EmojiHandle.None)
            {
                _isResizing = true;
                _dragStart = point.Position;

                var left = Canvas.GetLeft(_selectedEmoji);
                var top = Canvas.GetTop(_selectedEmoji);
                var width = double.IsNaN(_selectedEmoji.Width) ? _selectedEmoji.ActualWidth : _selectedEmoji.Width;
                var height = double.IsNaN(_selectedEmoji.Height) ? _selectedEmoji.ActualHeight : _selectedEmoji.Height;
                _boundsBeforeResize = new Rect(left, top, width, height);

                // Capturar el ángulo de rotación actual para resize con rotación
                if (_selectedEmoji.Tag is EmojiData data)
                {
                    _rotationAngleBeforeResize = data.RotationAngle;
                }
                else
                {
                    _rotationAngleBeforeResize = 0;
                }

                // Calcular y guardar la posición de la esquina fija (opuesta al handle activo) en coordenadas de pantalla
                double centerX = _boundsBeforeResize.X + _boundsBeforeResize.Width / 2;
                double centerY = _boundsBeforeResize.Y + _boundsBeforeResize.Height / 2;
                double radians = _rotationAngleBeforeResize * Math.PI / 180.0;

                Point fixedCornerLocal;
                switch (_activeHandle)
                {
                    case EmojiHandle.NorthWest:
                        fixedCornerLocal = new Point(_boundsBeforeResize.Right, _boundsBeforeResize.Bottom); // SE
                        break;
                    case EmojiHandle.NorthEast:
                        fixedCornerLocal = new Point(_boundsBeforeResize.Left, _boundsBeforeResize.Bottom); // SW
                        break;
                    case EmojiHandle.SouthWest:
                        fixedCornerLocal = new Point(_boundsBeforeResize.Right, _boundsBeforeResize.Top); // NE
                        break;
                    case EmojiHandle.SouthEast:
                        fixedCornerLocal = new Point(_boundsBeforeResize.Left, _boundsBeforeResize.Top); // NW
                        break;
                    default:
                        fixedCornerLocal = new Point(_boundsBeforeResize.Left, _boundsBeforeResize.Top);
                        break;
                }

                // Rotar la esquina fija para obtener su posición en pantalla
                _fixedCornerRotated = RotatePoint(fixedCornerLocal, centerX, centerY, radians);

                handle.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing || _selectedEmoji == null || _activeHandle == EmojiHandle.None)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);

            // Si hay rotación, usar el método especial de resize
            if (Math.Abs(_rotationAngleBeforeResize) > 0.01)
            {
                ResizeWithRotation(point.Position);
                e.Handled = true;
                return;
            }

            // Sin rotación: lógica simple
            var delta = new Point(
                point.Position.X - _dragStart.X,
                point.Position.Y - _dragStart.Y
            );

            // Calculate new size maintaining aspect ratio (1:1 for emojis)
            // Use the larger delta to determine size change
            double sizeDelta = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            
            // Determine direction based on handle and deltas
            bool expanding = false;
            switch (_activeHandle)
            {
                case EmojiHandle.SouthEast:
                    expanding = delta.X > 0 || delta.Y > 0;
                    break;
                case EmojiHandle.NorthWest:
                    expanding = delta.X < 0 || delta.Y < 0;
                    break;
                case EmojiHandle.NorthEast:
                    expanding = delta.X > 0 || delta.Y < 0;
                    break;
                case EmojiHandle.SouthWest:
                    expanding = delta.X < 0 || delta.Y > 0;
                    break;
            }

            if (!expanding) sizeDelta = -sizeDelta;

            double newSize = _boundsBeforeResize.Width + sizeDelta;
            
            // Apply constraints
            newSize = Math.Max(MIN_EMOJI_SIZE, Math.Min(MAX_EMOJI_SIZE, newSize));

            double newLeft = _boundsBeforeResize.Left;
            double newTop = _boundsBeforeResize.Top;

            // Adjust position based on handle (anchor point is opposite corner)
            switch (_activeHandle)
            {
                case EmojiHandle.NorthWest:
                    newLeft = _boundsBeforeResize.Right - newSize;
                    newTop = _boundsBeforeResize.Bottom - newSize;
                    break;
                case EmojiHandle.NorthEast:
                    newTop = _boundsBeforeResize.Bottom - newSize;
                    break;
                case EmojiHandle.SouthWest:
                    newLeft = _boundsBeforeResize.Right - newSize;
                    break;
                case EmojiHandle.SouthEast:
                    // Position stays the same (anchor is top-left)
                    break;
            }

            // Apply changes
            Canvas.SetLeft(_selectedEmoji, newLeft);
            Canvas.SetTop(_selectedEmoji, newTop);
            _selectedEmoji.Width = newSize;
            _selectedEmoji.Height = newSize;

            // Update font size proportionally
            UpdateEmojiFont(_selectedEmoji, newSize);

            // Update EmojiData
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                emojiData.Position = new Point(newLeft, newTop);
                emojiData.Width = newSize;
                emojiData.Height = newSize;
                emojiData.FontSize = newSize * 0.8;
            }

            UpdateHandles();
            e.Handled = true;
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isResizing && _selectedEmoji != null && sender is FrameworkElement handle)
            {
                handle.ReleasePointerCapture(e.Pointer);

                // No se registra en historial - solo el add inicial se registra
                // Ctrl+Z eliminará el emoji completo

                EmojiModified?.Invoke(this, _selectedEmoji);
            }

            _isResizing = false;
            _activeHandle = EmojiHandle.None;
            _originalEmojiData = null;
            e.Handled = true;
        }

        private void SetResizeCursor(string? handleTag)
        {
            // Cursor setting would go here if needed
            // WinUI 3 cursor management is more limited
        }

        private void UpdateEmojiFont(Grid emojiGrid, double size)
        {
            foreach (var child in emojiGrid.Children)
            {
                if (child is TextBlock textBlock)
                {
                    textBlock.FontSize = size * 0.8; // 80% of container size
                    break;
                }
            }
        }

        #endregion

        #region Rotation Handle Events

        private void RotationHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border handle)
            {
                handle.Background = BrushCache.GetBrush(HandleHoverFillColor);
                handle.Width = ROTATION_HANDLE_SIZE + 2;
                handle.Height = ROTATION_HANDLE_SIZE + 2;
                handle.CornerRadius = new CornerRadius((ROTATION_HANDLE_SIZE + 2) / 2);

                // Adjust position to keep centered
                double currentLeft = Canvas.GetLeft(handle);
                double currentTop = Canvas.GetTop(handle);
                Canvas.SetLeft(handle, currentLeft - 1);
                Canvas.SetTop(handle, currentTop - 1);
            }
        }

        private void RotationHandle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border handle && !_isRotating)
            {
                handle.Background = BrushCache.GetBrush(HandleFillColor);
                handle.Width = ROTATION_HANDLE_SIZE;
                handle.Height = ROTATION_HANDLE_SIZE;
                handle.CornerRadius = new CornerRadius(ROTATION_HANDLE_SIZE / 2);

                // Restore position
                double currentLeft = Canvas.GetLeft(handle);
                double currentTop = Canvas.GetTop(handle);
                Canvas.SetLeft(handle, currentLeft + 1);
                Canvas.SetTop(handle, currentTop + 1);
            }
        }

        private void RotationHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_selectedEmoji == null) return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            if (!point.Properties.IsLeftButtonPressed) return;

            // Guardar datos originales
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                _originalEmojiData = emojiData.Clone();
                _rotationStartAngle = emojiData.RotationAngle;

                // Calcular el centro del emoji
                var left = Canvas.GetLeft(_selectedEmoji);
                var top = Canvas.GetTop(_selectedEmoji);
                var width = double.IsNaN(_selectedEmoji.Width) ? _selectedEmoji.ActualWidth : _selectedEmoji.Width;
                var height = double.IsNaN(_selectedEmoji.Height) ? _selectedEmoji.ActualHeight : _selectedEmoji.Height;
                _rotationCenter = new Point(left + width / 2, top + height / 2);
            }

            _isRotating = true;
            _activeHandle = EmojiHandle.Rotation;

            if (sender is Border handle)
            {
                handle.CapturePointer(e.Pointer);
            }

            e.Handled = true;
        }

        private void RotationHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isRotating || _selectedEmoji == null) return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            var currentPos = point.Position;

            // Calcular el ángulo desde el centro del emoji al punto actual
            var dx = currentPos.X - _rotationCenter.X;
            var dy = currentPos.Y - _rotationCenter.Y;
            var angleRad = Math.Atan2(dy, dx);
            var angleDeg = angleRad * (180.0 / Math.PI);

            // Ajustar para que 0° esté arriba (añadir 90°)
            var newAngle = angleDeg + 90;

            // Aplicar la rotación al emoji
            ApplyRotation(_selectedEmoji, newAngle);

            // Actualizar los datos
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                emojiData.RotationAngle = newAngle;
            }

            UpdateHandles();
            e.Handled = true;
        }

        private void RotationHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isRotating && _selectedEmoji != null)
            {
                if (sender is Border handle)
                {
                    handle.ReleasePointerCapture(e.Pointer);
                    handle.Background = BrushCache.GetBrush(HandleFillColor);
                    handle.Width = ROTATION_HANDLE_SIZE;
                    handle.Height = ROTATION_HANDLE_SIZE;
                    handle.CornerRadius = new CornerRadius(ROTATION_HANDLE_SIZE / 2);
                }

                // No se registra en historial - solo el add inicial se registra
                // Ctrl+Z eliminará el emoji completo

                EmojiModified?.Invoke(this, _selectedEmoji);
            }

            _isRotating = false;
            _activeHandle = EmojiHandle.None;
            _originalEmojiData = null;
            e.Handled = true;
        }

        private void ApplyRotation(Grid emojiGrid, double angle)
        {
            var width = double.IsNaN(emojiGrid.Width) ? emojiGrid.ActualWidth : emojiGrid.Width;
            var height = double.IsNaN(emojiGrid.Height) ? emojiGrid.ActualHeight : emojiGrid.Height;

            emojiGrid.RenderTransform = new RotateTransform
            {
                Angle = angle,
                CenterX = width / 2,
                CenterY = height / 2
            };
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects an emoji element
        /// </summary>
        public void SelectEmoji(Grid emojiElement)
        {
            if (_selectedEmoji == emojiElement)
                return;

            Deselect();

            _selectedEmoji = emojiElement;
            _handlesCanvas.Visibility = Visibility.Visible;
            UpdateHandles();

            SelectionChanged?.Invoke(this, emojiElement);
        }

        /// <summary>
        /// Deselects the current emoji
        /// </summary>
        public void Deselect()
        {
            if (_selectedEmoji == null)
                return;

            _selectedEmoji = null;
            HideHandles();
            _handlesCanvas.Visibility = Visibility.Collapsed;

            SelectionChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Gets an emoji at the specified point
        /// </summary>
        public Grid? GetEmojiAtPoint(Point point)
        {
            foreach (var child in _emojiCanvas.Children)
            {
                if (child is Grid grid && grid.Tag is EmojiData)
                {
                    var left = Canvas.GetLeft(grid);
                    var top = Canvas.GetTop(grid);
                    var width = double.IsNaN(grid.Width) ? grid.ActualWidth : grid.Width;
                    var height = double.IsNaN(grid.Height) ? grid.ActualHeight : grid.Height;

                    var bounds = new Rect(left, top, Math.Max(width, 1), Math.Max(height, 1));
                    if (bounds.Contains(point))
                    {
                        return grid;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a point is on any handle
        /// </summary>
        public bool IsPointOnHandle(Point point)
        {
            if (_selectedEmoji == null) return false;

            return IsPointInElement(_handleNW, point) ||
                   IsPointInElement(_handleNE, point) ||
                   IsPointInElement(_handleSW, point) ||
                   IsPointInElement(_handleSE, point) ||
                   IsPointInElement(_handleRotation, point);
        }

        private bool IsPointInElement(FrameworkElement element, Point point)
        {
            if (element.Visibility != Visibility.Visible) return false;

            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            var bounds = new Rect(left, top, element.Width, element.Height);
            return bounds.Contains(point);
        }

        #endregion

        #region Dragging

        /// <summary>
        /// Starts dragging the selected emoji
        /// </summary>
        public bool StartDrag(Point position)
        {
            if (_selectedEmoji == null)
                return false;

            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                _originalEmojiData = emojiData.Clone();
            }

            _isDragging = true;
            _dragStart = position;
            return true;
        }

        /// <summary>
        /// Continues dragging to a new position
        /// </summary>
        public void ContinueDrag(Point position)
        {
            if (!_isDragging || _selectedEmoji == null)
                return;

            var delta = new Point(
                position.X - _dragStart.X,
                position.Y - _dragStart.Y
            );

            var currentLeft = Canvas.GetLeft(_selectedEmoji);
            var currentTop = Canvas.GetTop(_selectedEmoji);

            Canvas.SetLeft(_selectedEmoji, currentLeft + delta.X);
            Canvas.SetTop(_selectedEmoji, currentTop + delta.Y);

            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                emojiData.Position = new Point(currentLeft + delta.X, currentTop + delta.Y);
            }

            _dragStart = position;
            UpdateHandles();
        }

        /// <summary>
        /// Ends the drag operation
        /// </summary>
        public void EndDrag()
        {
            if (_isDragging && _selectedEmoji != null)
            {
                // No se registra en historial - solo el add inicial se registra
                // Ctrl+Z eliminará el emoji completo

                EmojiModified?.Invoke(this, _selectedEmoji);
            }

            _isDragging = false;
            _originalEmojiData = null;
        }

        #endregion

        #region Handle Positioning

        private void HideHandles()
        {
            _handleNW.Visibility = Visibility.Collapsed;
            _handleNE.Visibility = Visibility.Collapsed;
            _handleSW.Visibility = Visibility.Collapsed;
            _handleSE.Visibility = Visibility.Collapsed;
            _handleRotation.Visibility = Visibility.Collapsed;
            _rotationLine.Visibility = Visibility.Collapsed;
            _selectionBorder.Visibility = Visibility.Collapsed;
        }

        private void UpdateHandles()
        {
            if (_selectedEmoji == null)
            {
                HideHandles();
                return;
            }

            var left = Canvas.GetLeft(_selectedEmoji);
            var top = Canvas.GetTop(_selectedEmoji);
            var width = double.IsNaN(_selectedEmoji.Width) ? _selectedEmoji.ActualWidth : _selectedEmoji.Width;
            var height = double.IsNaN(_selectedEmoji.Height) ? _selectedEmoji.ActualHeight : _selectedEmoji.Height;

            if (width <= 0) width = MIN_EMOJI_SIZE;
            if (height <= 0) height = MIN_EMOJI_SIZE;

            var bounds = new Rect(left, top, width, height);
            var halfHandle = HANDLE_SIZE / 2;
            var halfRotationHandle = ROTATION_HANDLE_SIZE / 2;

            // Get rotation angle from emoji data
            double rotationAngle = 0;
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                rotationAngle = emojiData.RotationAngle;
            }

            // Calculate center for rotation
            double centerX = bounds.X + bounds.Width / 2;
            double centerY = bounds.Y + bounds.Height / 2;
            double radians = rotationAngle * Math.PI / 180.0;

            // Update selection border with rotation transform
            Canvas.SetLeft(_selectionBorder, bounds.Left - 2);
            Canvas.SetTop(_selectionBorder, bounds.Top - 2);
            _selectionBorder.Width = bounds.Width + 4;
            _selectionBorder.Height = bounds.Height + 4;
            _selectionBorder.Visibility = Visibility.Visible;

            // Apply rotation to selection border
            if (rotationAngle != 0)
            {
                _selectionBorder.RenderTransform = new RotateTransform
                {
                    Angle = rotationAngle,
                    CenterX = (bounds.Width + 4) / 2,
                    CenterY = (bounds.Height + 4) / 2
                };
            }
            else
            {
                _selectionBorder.RenderTransform = null;
            }

            // Original corner positions (before rotation)
            Point nwOriginal = new Point(bounds.Left, bounds.Top);
            Point neOriginal = new Point(bounds.Right, bounds.Top);
            Point swOriginal = new Point(bounds.Left, bounds.Bottom);
            Point seOriginal = new Point(bounds.Right, bounds.Bottom);

            // Rotate corners around center
            Point nwRotated = RotatePoint(nwOriginal, centerX, centerY, radians);
            Point neRotated = RotatePoint(neOriginal, centerX, centerY, radians);
            Point swRotated = RotatePoint(swOriginal, centerX, centerY, radians);
            Point seRotated = RotatePoint(seOriginal, centerX, centerY, radians);

            // Position corner handles at rotated positions
            Canvas.SetLeft(_handleNW, nwRotated.X - halfHandle);
            Canvas.SetTop(_handleNW, nwRotated.Y - halfHandle);
            _handleNW.Visibility = Visibility.Visible;

            Canvas.SetLeft(_handleNE, neRotated.X - halfHandle);
            Canvas.SetTop(_handleNE, neRotated.Y - halfHandle);
            _handleNE.Visibility = Visibility.Visible;

            Canvas.SetLeft(_handleSW, swRotated.X - halfHandle);
            Canvas.SetTop(_handleSW, swRotated.Y - halfHandle);
            _handleSW.Visibility = Visibility.Visible;

            Canvas.SetLeft(_handleSE, seRotated.X - halfHandle);
            Canvas.SetTop(_handleSE, seRotated.Y - halfHandle);
            _handleSE.Visibility = Visibility.Visible;

            // Position rotation handle (centered above the selection, rotated)
            Point rotationHandleOriginal = new Point(centerX, bounds.Top - ROTATION_HANDLE_DISTANCE);
            Point rotationHandleRotated = RotatePoint(rotationHandleOriginal, centerX, centerY, radians);
            Canvas.SetLeft(_handleRotation, rotationHandleRotated.X - halfRotationHandle);
            Canvas.SetTop(_handleRotation, rotationHandleRotated.Y - halfRotationHandle);
            _handleRotation.Visibility = Visibility.Visible;

            // Position rotation line (from top center of emoji to rotation handle, both rotated)
            Point lineStartOriginal = new Point(centerX, bounds.Top);
            Point lineEndOriginal = new Point(centerX, bounds.Top - ROTATION_HANDLE_DISTANCE + halfRotationHandle);
            Point lineStartRotated = RotatePoint(lineStartOriginal, centerX, centerY, radians);
            Point lineEndRotated = RotatePoint(lineEndOriginal, centerX, centerY, radians);
            
            _rotationLine.X1 = lineStartRotated.X;
            _rotationLine.Y1 = lineStartRotated.Y;
            _rotationLine.X2 = lineEndRotated.X;
            _rotationLine.Y2 = lineEndRotated.Y;
            _rotationLine.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Rotates a point around a center by the given angle in radians
        /// </summary>
        private Point RotatePoint(Point point, double centerX, double centerY, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            return new Point(
                centerX + dx * cos - dy * sin,
                centerY + dx * sin + dy * cos
            );
        }

        /// <summary>
        /// Rotates a point in the opposite direction (unrotate)
        /// </summary>
        private Point UnrotatePoint(Point point, double centerX, double centerY, double radians)
        {
            return RotatePoint(point, centerX, centerY, -radians);
        }

        /// <summary>
        /// Handles resizing when the emoji has rotation applied.
        /// The key is to keep the fixed corner (opposite to the dragged handle) in its screen position.
        /// </summary>
        private void ResizeWithRotation(Point currentMousePosition)
        {
            if (_selectedEmoji == null) return;

            double radians = _rotationAngleBeforeResize * Math.PI / 180.0;

            // El punto fijo en coordenadas de pantalla es _fixedCornerRotated
            // El punto que se mueve es la posición actual del mouse (también en coordenadas de pantalla)
            Point movingCornerScreen = currentMousePosition;
            Point fixedCornerScreen = _fixedCornerRotated;

            // Para calcular los nuevos bounds, necesitamos "des-rotar" ambos puntos
            // Usamos el centro original como referencia temporal
            double oldCenterX = _boundsBeforeResize.X + _boundsBeforeResize.Width / 2;
            double oldCenterY = _boundsBeforeResize.Y + _boundsBeforeResize.Height / 2;

            // Des-rotar los puntos de pantalla al sistema de coordenadas locales
            Point fixedLocal = UnrotatePoint(fixedCornerScreen, oldCenterX, oldCenterY, radians);
            Point movingLocal = UnrotatePoint(movingCornerScreen, oldCenterX, oldCenterY, radians);

            // Calcular nuevos bounds en coordenadas locales
            double newLeft = Math.Min(fixedLocal.X, movingLocal.X);
            double newTop = Math.Min(fixedLocal.Y, movingLocal.Y);
            double sizeX = Math.Abs(movingLocal.X - fixedLocal.X);
            double sizeY = Math.Abs(movingLocal.Y - fixedLocal.Y);

            // Mantener proporción 1:1 para emojis - usar el mayor de los dos
            double newSize = Math.Max(sizeX, sizeY);

            // Enforce minimum/maximum size
            newSize = Math.Max(MIN_EMOJI_SIZE, Math.Min(MAX_EMOJI_SIZE, newSize));

            // Ajustar newLeft y newTop basándose en cuál esquina es la fija
            switch (_activeHandle)
            {
                case EmojiHandle.NorthWest:
                    // Fija es SE, así que el nuevo left/top se calcula desde SE
                    newLeft = fixedLocal.X - newSize;
                    newTop = fixedLocal.Y - newSize;
                    break;
                case EmojiHandle.NorthEast:
                    // Fija es SW
                    newLeft = fixedLocal.X;
                    newTop = fixedLocal.Y - newSize;
                    break;
                case EmojiHandle.SouthWest:
                    // Fija es NE
                    newLeft = fixedLocal.X - newSize;
                    newTop = fixedLocal.Y;
                    break;
                case EmojiHandle.SouthEast:
                    // Fija es NW
                    newLeft = fixedLocal.X;
                    newTop = fixedLocal.Y;
                    break;
                default:
                    return;
            }

            // El nuevo centro en coordenadas locales
            double newCenterLocalX = newLeft + newSize / 2;
            double newCenterLocalY = newTop + newSize / 2;

            // Determinar cuál esquina del nuevo rectángulo corresponde al punto fijo
            Point newFixedLocal;
            switch (_activeHandle)
            {
                case EmojiHandle.NorthWest:
                    newFixedLocal = new Point(newLeft + newSize, newTop + newSize); // SE corner
                    break;
                case EmojiHandle.NorthEast:
                    newFixedLocal = new Point(newLeft, newTop + newSize); // SW corner
                    break;
                case EmojiHandle.SouthWest:
                    newFixedLocal = new Point(newLeft + newSize, newTop); // NE corner
                    break;
                case EmojiHandle.SouthEast:
                    newFixedLocal = new Point(newLeft, newTop); // NW corner
                    break;
                default:
                    return;
            }

            // Calcular dónde estaría este punto después de rotar alrededor del nuevo centro
            Point newFixedRotated = RotatePoint(newFixedLocal, newCenterLocalX, newCenterLocalY, radians);

            // La diferencia entre donde debería estar y donde está nos da el offset necesario
            double offsetX = fixedCornerScreen.X - newFixedRotated.X;
            double offsetY = fixedCornerScreen.Y - newFixedRotated.Y;

            // Aplicar el offset a los bounds
            newLeft += offsetX;
            newTop += offsetY;

            // Aplicar cambios al emoji
            Canvas.SetLeft(_selectedEmoji, newLeft);
            Canvas.SetTop(_selectedEmoji, newTop);
            _selectedEmoji.Width = newSize;
            _selectedEmoji.Height = newSize;

            // Update font size proportionally
            UpdateEmojiFont(_selectedEmoji, newSize);

            // Update EmojiData
            if (_selectedEmoji.Tag is EmojiData emojiData)
            {
                emojiData.Position = new Point(newLeft, newTop);
                emojiData.Width = newSize;
                emojiData.Height = newSize;
                emojiData.FontSize = newSize * 0.8;

                // IMPORTANTE: Re-aplicar la rotación con el nuevo centro
                // El RenderTransform usa CenterX/CenterY que dependen del tamaño
                ApplyRotation(_selectedEmoji, emojiData.RotationAngle);
            }

            UpdateHandles();
            EmojiModified?.Invoke(this, _selectedEmoji);
        }

        private void PositionHandle(Rectangle handle, double x, double y)
        {
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
            handle.Visibility = Visibility.Visible;
        }

        #endregion

        #region Delete

        /// <summary>
        /// Deletes the selected emoji
        /// </summary>
        public void DeleteSelected()
        {
            if (_selectedEmoji == null) return;

            var emojiToDelete = _selectedEmoji;
            Deselect();

            if (_emojiCanvas.Children.Contains(emojiToDelete))
            {
                _emojiCanvas.Children.Remove(emojiToDelete);
            }
        }

        #endregion
    }
}
