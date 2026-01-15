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
using Windows.System;
using Windows.UI;
using WinUICore = Windows.UI.Core;

namespace SnipShot.Features.Capture.Annotations.Managers
{
    /// <summary>
    /// Handle types for shape resizing
    /// </summary>
    public enum ShapeHandle
    {
        None,
        Start,      // For lines/arrows: start point
        End,        // For lines/arrows: end point
        NorthWest,  // Top-left corner
        NorthEast,  // Top-right corner
        SouthWest,  // Bottom-left corner
        SouthEast,  // Bottom-right corner
        Rotation    // Rotation handle
    }

    /// <summary>
    /// Manages selection, dragging, and resizing of shapes
    /// </summary>
    public class ShapeManipulationManager
    {
        private readonly Canvas _shapesCanvas;
        private readonly Canvas _handlesCanvas;
        private readonly AnnotationHistoryManager? _historyManager;
        private readonly FrameworkElement _rootElement;

        // Selection state
        private Path? _selectedShape;
        private ShapeData? _originalShapeData;
        private bool _isDragging;
        private bool _isResizing;
        private bool _isRotating;
        private Point _dragStart;
        private ShapeHandle _activeHandle = ShapeHandle.None;
        private Rect _boundsBeforeResize;
        private Point _startPointBeforeResize;
        private Point _endPointBeforeResize;
        private double _rotationAngleBeforeRotation;
        private Point _rotationCenter;
        private Point _fixedCornerRotated; // Punto fijo en coordenadas de pantalla (después de rotación)
        private double _rotationAngleBeforeResize; // Ángulo de rotación al iniciar resize

        // Handle elements
        private readonly Rectangle _handleNW;
        private readonly Rectangle _handleNE;
        private readonly Rectangle _handleSW;
        private readonly Rectangle _handleSE;
        private readonly Ellipse _handleStart;
        private readonly Ellipse _handleEnd;
        private readonly Rectangle _selectionBorder;
        private readonly Border _rotationHandle;

        // Handle design constants
        private const double HANDLE_SIZE = 12;
        private const double HANDLE_STROKE_WIDTH = 2;
        
        // Colors for handles - usando blanco con borde gris oscuro para mejor visibilidad
        private static readonly Color HandleFillColor = Colors.White;
        private static readonly Color HandleStrokeColor = Color.FromArgb(255, 80, 80, 80); // Gris oscuro
        private static readonly Color HandleHoverFillColor = Color.FromArgb(255, 220, 220, 220); // Gris claro on hover
        private static readonly Color SelectionBorderColor = Color.FromArgb(255, 100, 100, 100); // Gris para borde de selección

        /// <summary>
        /// Event raised when the selected shape changes
        /// </summary>
        public event EventHandler<Path?>? SelectionChanged;

        /// <summary>
        /// Event raised when a shape is moved or resized
        /// </summary>
        public event EventHandler<Path>? ShapeModified;

        /// <summary>
        /// Gets the currently selected shape
        /// </summary>
        public Path? SelectedShape => _selectedShape;

        /// <summary>
        /// Gets whether a shape is currently selected
        /// </summary>
        public bool HasSelection => _selectedShape != null;

        /// <summary>
        /// Gets whether the user is currently dragging a shape
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Gets whether the user is currently resizing a shape
        /// </summary>
        public bool IsResizing => _isResizing;

        /// <summary>
        /// Gets whether the user is currently rotating a shape
        /// </summary>
        public bool IsRotating => _isRotating;

        /// <summary>
        /// Gets whether the user is manipulating a shape (dragging or resizing)
        /// </summary>
        public bool IsManipulating => _isDragging || _isResizing || _isRotating;

        /// <summary>
        /// Creates a new ShapeManipulationManager
        /// </summary>
        /// <param name="shapesCanvas">The canvas containing the shapes</param>
        /// <param name="handlesCanvas">The canvas for drawing selection handles</param>
        /// <param name="rootElement">The root element for coordinate transforms</param>
        /// <param name="historyManager">Optional history manager for undo/redo</param>
        public ShapeManipulationManager(
            Canvas shapesCanvas,
            Canvas handlesCanvas,
            FrameworkElement rootElement,
            AnnotationHistoryManager? historyManager = null)
        {
            _shapesCanvas = shapesCanvas ?? throw new ArgumentNullException(nameof(shapesCanvas));
            _handlesCanvas = handlesCanvas ?? throw new ArgumentNullException(nameof(handlesCanvas));
            _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            _historyManager = historyManager;

            // Create handle elements
            _handleNW = CreateCornerHandle("NW");
            _handleNE = CreateCornerHandle("NE");
            _handleSW = CreateCornerHandle("SW");
            _handleSE = CreateCornerHandle("SE");
            _handleStart = CreatePointHandle("Start");
            _handleEnd = CreatePointHandle("End");
            _selectionBorder = CreateSelectionBorder();
            _rotationHandle = CreateRotationHandle();

            // Add handles to canvas (initially hidden)
            _handlesCanvas.Children.Add(_selectionBorder);
            _handlesCanvas.Children.Add(_handleNW);
            _handlesCanvas.Children.Add(_handleNE);
            _handlesCanvas.Children.Add(_handleSW);
            _handlesCanvas.Children.Add(_handleSE);
            _handlesCanvas.Children.Add(_handleStart);
            _handlesCanvas.Children.Add(_handleEnd);
            _handlesCanvas.Children.Add(_rotationHandle);

            HideHandles();
        }

        /// <summary>
        /// Creates a corner handle rectangle with improved styling
        /// </summary>
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

            // Add hover effects
            handle.PointerEntered += Handle_PointerEntered;
            handle.PointerExited += Handle_PointerExited;
            handle.PointerPressed += Handle_PointerPressed;
            handle.PointerMoved += Handle_PointerMoved;
            handle.PointerReleased += Handle_PointerReleased;

            return handle;
        }

        /// <summary>
        /// Creates a point handle ellipse (for lines/arrows) with improved styling
        /// </summary>
        private Ellipse CreatePointHandle(string tag)
        {
            var handle = new Ellipse
            {
                Width = HANDLE_SIZE,
                Height = HANDLE_SIZE,
                Fill = BrushCache.GetBrush(HandleFillColor),
                Stroke = BrushCache.GetBrush(HandleStrokeColor),
                StrokeThickness = HANDLE_STROKE_WIDTH,
                Tag = tag,
                Visibility = Visibility.Collapsed
            };

            // Add hover effects
            handle.PointerEntered += Handle_PointerEntered;
            handle.PointerExited += Handle_PointerExited;
            handle.PointerPressed += Handle_PointerPressed;
            handle.PointerMoved += Handle_PointerMoved;
            handle.PointerReleased += Handle_PointerReleased;

            return handle;
        }

        /// <summary>
        /// Creates the selection border rectangle with improved styling
        /// </summary>
        private Rectangle CreateSelectionBorder()
        {
            return new Rectangle
            {
                Stroke = BrushCache.GetBrush(SelectionBorderColor),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Fill = BrushCache.Transparent,
                RadiusX = 2,
                RadiusY = 2,
                Visibility = Visibility.Collapsed
            };
        }

        /// <summary>
        /// Creates the rotation handle button with icon
        /// </summary>
        private Border CreateRotationHandle()
        {
            var icon = new FontIcon
            {
                Glyph = "\uE7AD",
                FontSize = 12,
                Foreground = BrushCache.GetBrush(HandleStrokeColor) // Mismo color que el borde
            };

            var handle = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = BrushCache.GetBrush(HandleFillColor),
                BorderBrush = BrushCache.GetBrush(HandleStrokeColor),
                BorderThickness = new Thickness(HANDLE_STROKE_WIDTH),
                Child = icon,
                Tag = "Rotation",
                Visibility = Visibility.Collapsed
            };

            // Add hover effects and event handlers
            handle.PointerEntered += RotationHandle_PointerEntered;
            handle.PointerExited += RotationHandle_PointerExited;
            handle.PointerPressed += Handle_PointerPressed;
            handle.PointerMoved += Handle_PointerMoved;
            handle.PointerReleased += Handle_PointerReleased;

            return handle;
        }

        /// <summary>
        /// Handles pointer entering the rotation handle
        /// </summary>
        private void RotationHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = BrushCache.GetBrush(HandleHoverFillColor);
                border.Width = 26;
                border.Height = 26;
                border.CornerRadius = new CornerRadius(13);
                
                double currentLeft = Canvas.GetLeft(border);
                double currentTop = Canvas.GetTop(border);
                Canvas.SetLeft(border, currentLeft - 1);
                Canvas.SetTop(border, currentTop - 1);
            }
        }

        /// <summary>
        /// Handles pointer leaving the rotation handle
        /// </summary>
        private void RotationHandle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = BrushCache.GetBrush(HandleFillColor);
                border.Width = 24;
                border.Height = 24;
                border.CornerRadius = new CornerRadius(12);
                
                double currentLeft = Canvas.GetLeft(border);
                double currentTop = Canvas.GetTop(border);
                Canvas.SetLeft(border, currentLeft + 1);
                Canvas.SetTop(border, currentTop + 1);
            }
        }

        /// <summary>
        /// Handles pointer entering a handle (hover effect)
        /// </summary>
        private void Handle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Shape shape)
            {
                shape.Fill = BrushCache.GetBrush(HandleHoverFillColor);
                
                // Scale up slightly for visual feedback
                shape.Width = HANDLE_SIZE + 2;
                shape.Height = HANDLE_SIZE + 2;
                
                // Adjust position to keep centered
                double currentLeft = Canvas.GetLeft(shape);
                double currentTop = Canvas.GetTop(shape);
                Canvas.SetLeft(shape, currentLeft - 1);
                Canvas.SetTop(shape, currentTop - 1);
            }
        }

        /// <summary>
        /// Handles pointer leaving a handle (restore normal state)
        /// </summary>
        private void Handle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Shape shape)
            {
                shape.Fill = BrushCache.GetBrush(HandleFillColor);
                
                // Restore original size
                shape.Width = HANDLE_SIZE;
                shape.Height = HANDLE_SIZE;
                
                // Restore position
                double currentLeft = Canvas.GetLeft(shape);
                double currentTop = Canvas.GetTop(shape);
                Canvas.SetLeft(shape, currentLeft + 1);
                Canvas.SetTop(shape, currentTop + 1);
            }
        }

        /// <summary>
        /// Selects a shape
        /// </summary>
        public void SelectShape(Path shape)
        {
            if (_selectedShape == shape)
                return;

            Deselect();

            _selectedShape = shape;
            _handlesCanvas.Visibility = Visibility.Visible;
            UpdateHandles();

            SelectionChanged?.Invoke(this, shape);
        }

        /// <summary>
        /// Deselects the current shape
        /// </summary>
        public void Deselect()
        {
            if (_selectedShape == null)
                return;

            _selectedShape = null;
            HideHandles();
            _handlesCanvas.Visibility = Visibility.Collapsed;

            SelectionChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Hides all selection handles
        /// </summary>
        private void HideHandles()
        {
            _handleNW.Visibility = Visibility.Collapsed;
            _handleNE.Visibility = Visibility.Collapsed;
            _handleSW.Visibility = Visibility.Collapsed;
            _handleSE.Visibility = Visibility.Collapsed;
            _handleStart.Visibility = Visibility.Collapsed;
            _handleEnd.Visibility = Visibility.Collapsed;
            _selectionBorder.Visibility = Visibility.Collapsed;
            _rotationHandle.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Updates the position of selection handles based on the selected shape
        /// </summary>
        public void UpdateHandles()
        {
            if (_selectedShape == null || !(_selectedShape.Tag is ShapeData data))
            {
                HideHandles();
                return;
            }

            // Hide all handles first
            HideHandles();

            if (data.Type == "Square" || data.Type == "Circle" || data.Type == "Star")
            {
                UpdateBoxHandles(data);
            }
            else if (data.Type == "Line" || data.Type == "Arrow")
            {
                UpdateLineHandles(data);
            }
        }

        /// <summary>
        /// Updates handles for box-shaped elements (rectangle, ellipse)
        /// </summary>
        private void UpdateBoxHandles(ShapeData data)
        {
            if (_selectedShape?.Data == null)
                return;

            var bounds = _selectedShape.Data.Bounds;

            // Validate bounds
            if (bounds.IsEmpty || double.IsInfinity(bounds.X) || double.IsInfinity(bounds.Y) ||
                double.IsNaN(bounds.X) || double.IsNaN(bounds.Y) || bounds.Width < 0 || bounds.Height < 0)
            {
                return;
            }

            // Show corner handles
            _handleNW.Visibility = Visibility.Visible;
            _handleNE.Visibility = Visibility.Visible;
            _handleSW.Visibility = Visibility.Visible;
            _handleSE.Visibility = Visibility.Visible;
            _selectionBorder.Visibility = Visibility.Visible;

            // Calculate center for rotation
            double centerX = bounds.X + bounds.Width / 2;
            double centerY = bounds.Y + bounds.Height / 2;
            double rotationAngle = data.RotationAngle;
            double radians = rotationAngle * Math.PI / 180.0;

            // Update selection border with rotation transform
            Canvas.SetLeft(_selectionBorder, bounds.X);
            Canvas.SetTop(_selectionBorder, bounds.Y);
            _selectionBorder.Width = bounds.Width;
            _selectionBorder.Height = bounds.Height;
            
            // Apply rotation to selection border
            if (rotationAngle != 0)
            {
                _selectionBorder.RenderTransform = new RotateTransform
                {
                    Angle = rotationAngle,
                    CenterX = bounds.Width / 2,
                    CenterY = bounds.Height / 2
                };
            }
            else
            {
                _selectionBorder.RenderTransform = null;
            }

            // Update corner handles with rotated positions
            double halfSize = HANDLE_SIZE / 2;

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

            Canvas.SetLeft(_handleNW, nwRotated.X - halfSize);
            Canvas.SetTop(_handleNW, nwRotated.Y - halfSize);

            Canvas.SetLeft(_handleNE, neRotated.X - halfSize);
            Canvas.SetTop(_handleNE, neRotated.Y - halfSize);

            Canvas.SetLeft(_handleSW, swRotated.X - halfSize);
            Canvas.SetTop(_handleSW, swRotated.Y - halfSize);

            Canvas.SetLeft(_handleSE, seRotated.X - halfSize);
            Canvas.SetTop(_handleSE, seRotated.Y - halfSize);

            // Show and position rotation handle (centered above the selection, rotated)
            _rotationHandle.Visibility = Visibility.Visible;
            double rotationHandleOffset = 30; // Distance above the selection
            Point rotationHandleOriginal = new Point(centerX, bounds.Top - rotationHandleOffset);
            Point rotationHandleRotated = RotatePoint(rotationHandleOriginal, centerX, centerY, radians);
            Canvas.SetLeft(_rotationHandle, rotationHandleRotated.X - 12); // Center horizontally (24/2 = 12)
            Canvas.SetTop(_rotationHandle, rotationHandleRotated.Y - 12);
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
        /// Handles resizing when the shape has rotation applied.
        /// The key is to keep the fixed corner (opposite to the dragged handle) in its screen position.
        /// </summary>
        private void ResizeWithRotation(ShapeData data, Point currentMousePosition)
        {
            if (_selectedShape == null) return;
            
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
            double newWidth = Math.Abs(movingLocal.X - fixedLocal.X);
            double newHeight = Math.Abs(movingLocal.Y - fixedLocal.Y);
            
            // Enforce minimum size
            if (newWidth < 1) newWidth = 1;
            if (newHeight < 1) newHeight = 1;
            
            // El nuevo centro en coordenadas locales
            double newCenterLocalX = newLeft + newWidth / 2;
            double newCenterLocalY = newTop + newHeight / 2;
            
            // Ahora necesitamos ajustar la posición para que el punto fijo
            // permanezca exactamente donde estaba en coordenadas de pantalla.
            // El punto fijo local (esquina del nuevo rectángulo) debe rotar al punto fijo de pantalla.
            
            // Determinar cuál esquina del nuevo rectángulo corresponde al punto fijo
            Point newFixedLocal;
            switch (_activeHandle)
            {
                case ShapeHandle.NorthWest:
                    newFixedLocal = new Point(newLeft + newWidth, newTop + newHeight); // SE corner
                    break;
                case ShapeHandle.NorthEast:
                    newFixedLocal = new Point(newLeft, newTop + newHeight); // SW corner
                    break;
                case ShapeHandle.SouthWest:
                    newFixedLocal = new Point(newLeft + newWidth, newTop); // NE corner
                    break;
                case ShapeHandle.SouthEast:
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
            
            // Crear nuevos bounds
            Rect newBounds = new Rect(newLeft, newTop, newWidth, newHeight);
            
            // Actualizar los puntos Start/End basándose en el mapeo original
            double oldLeft = Math.Min(_startPointBeforeResize.X, _endPointBeforeResize.X);
            double oldTop = Math.Min(_startPointBeforeResize.Y, _endPointBeforeResize.Y);

            Point newStart = new Point();
            Point newEnd = new Point();

            // X mapping
            if (Math.Abs(_startPointBeforeResize.X - oldLeft) < 0.01)
                newStart.X = newBounds.Left;
            else
                newStart.X = newBounds.Right;

            if (Math.Abs(_endPointBeforeResize.X - oldLeft) < 0.01)
                newEnd.X = newBounds.Left;
            else
                newEnd.X = newBounds.Right;

            // Y mapping
            if (Math.Abs(_startPointBeforeResize.Y - oldTop) < 0.01)
                newStart.Y = newBounds.Top;
            else
                newStart.Y = newBounds.Bottom;

            if (Math.Abs(_endPointBeforeResize.Y - oldTop) < 0.01)
                newEnd.Y = newBounds.Top;
            else
                newEnd.Y = newBounds.Bottom;

            data.StartPoint = newStart;
            data.EndPoint = newEnd;

            UpdateShapeGeometry(_selectedShape, data);
            UpdateHandles();
            
            ShapeModified?.Invoke(this, _selectedShape);
        }

        /// <summary>
        /// Updates handles for line-based elements (line, arrow)
        /// </summary>
        private void UpdateLineHandles(ShapeData data)
        {
            _handleStart.Visibility = Visibility.Visible;
            _handleEnd.Visibility = Visibility.Visible;

            double halfSize = HANDLE_SIZE / 2;

            Canvas.SetLeft(_handleStart, data.StartPoint.X - halfSize);
            Canvas.SetTop(_handleStart, data.StartPoint.Y - halfSize);

            Canvas.SetLeft(_handleEnd, data.EndPoint.X - halfSize);
            Canvas.SetTop(_handleEnd, data.EndPoint.Y - halfSize);
        }

        /// <summary>
        /// Starts dragging the selected shape
        /// </summary>
        public void StartDrag(Point position)
        {
            if (_selectedShape == null || !(_selectedShape.Tag is ShapeData data))
                return;

            _isDragging = true;
            _dragStart = position;
            _originalShapeData = data.Clone();
            _startPointBeforeResize = data.StartPoint;
            _endPointBeforeResize = data.EndPoint;
        }

        /// <summary>
        /// Continues dragging the selected shape
        /// </summary>
        public void ContinueDrag(Point position)
        {
            if (!_isDragging || _selectedShape == null || !(_selectedShape.Tag is ShapeData data))
                return;

            double dx = position.X - _dragStart.X;
            double dy = position.Y - _dragStart.Y;

            data.StartPoint = new Point(_startPointBeforeResize.X + dx, _startPointBeforeResize.Y + dy);
            data.EndPoint = new Point(_endPointBeforeResize.X + dx, _endPointBeforeResize.Y + dy);

            UpdateShapeGeometry(_selectedShape, data);
            UpdateHandles();

            ShapeModified?.Invoke(this, _selectedShape);
        }

        /// <summary>
        /// Ends dragging the selected shape
        /// </summary>
        public void EndDrag()
        {
            if (!_isDragging)
                return;

            if (_selectedShape != null && _selectedShape.Tag is ShapeData newData && _originalShapeData != null)
            {
                _historyManager?.RecordPathMoved(_selectedShape, _originalShapeData, newData);
            }

            _isDragging = false;
            _originalShapeData = null;
        }

        /// <summary>
        /// Handles pointer pressed on a handle
        /// </summary>
        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement handle || _selectedShape == null || !(_selectedShape.Tag is ShapeData data))
                return;

            _activeHandle = ParseHandleTag(handle.Tag as string);
            _dragStart = e.GetCurrentPoint(_rootElement).Position;
            _originalShapeData = data.Clone();

            if (_activeHandle == ShapeHandle.Rotation)
            {
                // Start rotation
                _isRotating = true;
                _rotationAngleBeforeRotation = data.RotationAngle;
                
                // Calculate center of the shape for rotation
                var bounds = _selectedShape.Data?.Bounds ?? Rect.Empty;
                _rotationCenter = new Point(
                    bounds.X + bounds.Width / 2,
                    bounds.Y + bounds.Height / 2);
            }
            else
            {
                // Start resizing
                _isResizing = true;
                _boundsBeforeResize = _selectedShape.Data?.Bounds ?? Rect.Empty;
                _startPointBeforeResize = data.StartPoint;
                _endPointBeforeResize = data.EndPoint;
                _rotationAngleBeforeResize = data.RotationAngle;
                
                // Calcular el punto fijo (esquina opuesta) en coordenadas de pantalla
                var bounds = _boundsBeforeResize;
                double centerX = bounds.X + bounds.Width / 2;
                double centerY = bounds.Y + bounds.Height / 2;
                double radians = _rotationAngleBeforeResize * Math.PI / 180.0;
                
                // Determinar cuál es la esquina fija basándose en el handle activo
                Point fixedCornerLocal;
                switch (_activeHandle)
                {
                    case ShapeHandle.NorthWest:
                        fixedCornerLocal = new Point(bounds.Right, bounds.Bottom);
                        break;
                    case ShapeHandle.NorthEast:
                        fixedCornerLocal = new Point(bounds.Left, bounds.Bottom);
                        break;
                    case ShapeHandle.SouthWest:
                        fixedCornerLocal = new Point(bounds.Right, bounds.Top);
                        break;
                    case ShapeHandle.SouthEast:
                        fixedCornerLocal = new Point(bounds.Left, bounds.Top);
                        break;
                    default:
                        fixedCornerLocal = new Point(centerX, centerY);
                        break;
                }
                
                // Rotar el punto fijo para obtener su posición en pantalla
                _fixedCornerRotated = RotatePoint(fixedCornerLocal, centerX, centerY, radians);
            }

            handle.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        /// <summary>
        /// Handles pointer moved on a handle
        /// </summary>
        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_selectedShape == null || !(_selectedShape.Tag is ShapeData data))
                return;

            var currentPoint = e.GetCurrentPoint(_rootElement).Position;

            // Handle rotation
            if (_isRotating && _activeHandle == ShapeHandle.Rotation)
            {
                // Calculate angle from center to current point
                double rotDx = currentPoint.X - _rotationCenter.X;
                double rotDy = currentPoint.Y - _rotationCenter.Y;
                double angle = Math.Atan2(rotDy, rotDx) * (180.0 / Math.PI);
                
                // Calculate initial angle from center to drag start
                double startDx = _dragStart.X - _rotationCenter.X;
                double startDy = _dragStart.Y - _rotationCenter.Y;
                double startAngle = Math.Atan2(startDy, startDx) * (180.0 / Math.PI);
                
                // Calculate rotation delta and apply to original rotation
                double rotationDelta = angle - startAngle;
                data.RotationAngle = _rotationAngleBeforeRotation + rotationDelta;
                
                // Normalize to 0-360
                while (data.RotationAngle < 0) data.RotationAngle += 360;
                while (data.RotationAngle >= 360) data.RotationAngle -= 360;
                
                // Apply rotation transform to the shape
                ApplyRotationTransform(_selectedShape, data.RotationAngle, _rotationCenter);
                
                // Update handles to match rotation
                UpdateHandles();
                
                ShapeModified?.Invoke(this, _selectedShape);
                e.Handled = true;
                return;
            }

            if (!_isResizing)
                return;

            // Handle line/arrow endpoints
            if (_activeHandle == ShapeHandle.Start)
            {
                data.StartPoint = currentPoint;
                UpdateShapeGeometry(_selectedShape, data);
                UpdateHandles();
                e.Handled = true;
                return;
            }
            else if (_activeHandle == ShapeHandle.End)
            {
                data.EndPoint = currentPoint;
                UpdateShapeGeometry(_selectedShape, data);
                UpdateHandles();
                e.Handled = true;
                return;
            }

            // Handle corner resizing for boxes
            // Si hay rotación, necesitamos un cálculo especial
            if (Math.Abs(_rotationAngleBeforeResize) > 0.01)
            {
                ResizeWithRotation(data, currentPoint);
                e.Handled = true;
                return;
            }
            
            // Sin rotación: lógica simple
            double dx = currentPoint.X - _dragStart.X;
            double dy = currentPoint.Y - _dragStart.Y;

            Point fixedPoint;
            Point movingPoint;

            double left = _boundsBeforeResize.Left;
            double top = _boundsBeforeResize.Top;
            double right = _boundsBeforeResize.Right;
            double bottom = _boundsBeforeResize.Bottom;

            switch (_activeHandle)
            {
                case ShapeHandle.NorthWest:
                    fixedPoint = new Point(right, bottom);
                    movingPoint = new Point(left + dx, top + dy);
                    break;
                case ShapeHandle.NorthEast:
                    fixedPoint = new Point(left, bottom);
                    movingPoint = new Point(right + dx, top + dy);
                    break;
                case ShapeHandle.SouthWest:
                    fixedPoint = new Point(right, top);
                    movingPoint = new Point(left + dx, bottom + dy);
                    break;
                case ShapeHandle.SouthEast:
                    fixedPoint = new Point(left, top);
                    movingPoint = new Point(right + dx, bottom + dy);
                    break;
                default:
                    return;
            }

            // Calculate new bounds
            double newLeft = Math.Min(fixedPoint.X, movingPoint.X);
            double newTop = Math.Min(fixedPoint.Y, movingPoint.Y);
            double newWidth = Math.Abs(movingPoint.X - fixedPoint.X);
            double newHeight = Math.Abs(movingPoint.Y - fixedPoint.Y);

            // Apply constraint for perfect square/circle when Shift is pressed
            bool constrainProportions = IsShiftPressed() && SupportsConstrainedProportions(data.Type);
            if (constrainProportions)
            {
                double size = Math.Min(newWidth, newHeight);
                newWidth = size;
                newHeight = size;
                
                // Adjust position based on which corner is being dragged
                // The fixed point stays in place, and we adjust dimensions from there
                switch (_activeHandle)
                {
                    case ShapeHandle.NorthWest:
                        newLeft = fixedPoint.X - size;
                        newTop = fixedPoint.Y - size;
                        break;
                    case ShapeHandle.NorthEast:
                        newLeft = fixedPoint.X;
                        newTop = fixedPoint.Y - size;
                        break;
                    case ShapeHandle.SouthWest:
                        newLeft = fixedPoint.X - size;
                        newTop = fixedPoint.Y;
                        break;
                    case ShapeHandle.SouthEast:
                        newLeft = fixedPoint.X;
                        newTop = fixedPoint.Y;
                        break;
                }
            }

            // Enforce minimum size
            if (newWidth < 1) newWidth = 1;
            if (newHeight < 1) newHeight = 1;

            Rect newBounds = new Rect(newLeft, newTop, newWidth, newHeight);

            // Map new bounds to Start/End points
            double oldLeft = Math.Min(_startPointBeforeResize.X, _endPointBeforeResize.X);
            double oldTop = Math.Min(_startPointBeforeResize.Y, _endPointBeforeResize.Y);

            Point newStart = new Point();
            Point newEnd = new Point();

            // X mapping
            if (Math.Abs(_startPointBeforeResize.X - oldLeft) < 0.01)
                newStart.X = newBounds.Left;
            else
                newStart.X = newBounds.Right;

            if (Math.Abs(_endPointBeforeResize.X - oldLeft) < 0.01)
                newEnd.X = newBounds.Left;
            else
                newEnd.X = newBounds.Right;

            // Y mapping
            if (Math.Abs(_startPointBeforeResize.Y - oldTop) < 0.01)
                newStart.Y = newBounds.Top;
            else
                newStart.Y = newBounds.Bottom;

            if (Math.Abs(_endPointBeforeResize.Y - oldTop) < 0.01)
                newEnd.Y = newBounds.Top;
            else
                newEnd.Y = newBounds.Bottom;

            data.StartPoint = newStart;
            data.EndPoint = newEnd;

            UpdateShapeGeometry(_selectedShape, data);
            UpdateHandles();
            e.Handled = true;

            ShapeModified?.Invoke(this, _selectedShape);
        }

        /// <summary>
        /// Handles pointer released on a handle
        /// </summary>
        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing && !_isRotating)
                return;

            if (_selectedShape != null && _selectedShape.Tag is ShapeData newData && _originalShapeData != null)
            {
                if (_isRotating)
                {
                    _historyManager?.RecordPathMoved(_selectedShape, _originalShapeData, newData);
                }
                else
                {
                    _historyManager?.RecordPathResized(_selectedShape, _originalShapeData, newData);
                }
            }

            _isResizing = false;
            _isRotating = false;
            _activeHandle = ShapeHandle.None;
            _originalShapeData = null;

            (sender as UIElement)?.ReleasePointerCaptures();
            e.Handled = true;
        }

        /// <summary>
        /// Parses a handle tag string to ShapeHandle enum
        /// </summary>
        private static ShapeHandle ParseHandleTag(string? tag)
        {
            return tag switch
            {
                "Start" => ShapeHandle.Start,
                "End" => ShapeHandle.End,
                "NW" => ShapeHandle.NorthWest,
                "NE" => ShapeHandle.NorthEast,
                "SW" => ShapeHandle.SouthWest,
                "SE" => ShapeHandle.SouthEast,
                "Rotation" => ShapeHandle.Rotation,
                _ => ShapeHandle.None
            };
        }

        /// <summary>
        /// Checks if the Shift key is currently pressed
        /// </summary>
        private static bool IsShiftPressed()
        {
            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            return (shiftState & WinUICore.CoreVirtualKeyStates.Down) == WinUICore.CoreVirtualKeyStates.Down;
        }

        /// <summary>
        /// Checks if a shape type supports constrained proportions (square/circle)
        /// </summary>
        private static bool SupportsConstrainedProportions(string shapeType)
        {
            return shapeType == "Square" || shapeType == "Circle" || shapeType == "Star";
        }

        /// <summary>
        /// Updates the geometry of a shape based on its data
        /// </summary>
        public void UpdateShapeGeometry(Path shape, ShapeData data)
        {
            // Obtener el grosor del trazo actual de la forma para flechas
            double strokeThickness = shape.StrokeThickness;
            
            shape.Data = data.Type switch
            {
                "Arrow" => ArrowTool.CreateArrowGeometry(data.StartPoint, data.EndPoint, strokeThickness),
                "Line" => LineTool.CreateLineGeometry(data.StartPoint, data.EndPoint),
                "Square" => RectangleTool.CreateRectangleGeometry(data.StartPoint, data.EndPoint, false),
                "Circle" => EllipseTool.CreateEllipseGeometry(data.StartPoint, data.EndPoint, false),
                "Star" => StarTool.CreateStarGeometry(data.StartPoint, data.EndPoint, false),
                _ => shape.Data
            };
            
            // Apply rotation if set
            if (data.RotationAngle != 0 && shape.Data != null)
            {
                var bounds = shape.Data.Bounds;
                var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                ApplyRotationTransform(shape, data.RotationAngle, center);
            }
        }

        /// <summary>
        /// Applies a rotation transform to a shape around a center point
        /// </summary>
        /// <param name="shape">The shape to rotate</param>
        /// <param name="angle">The rotation angle in degrees</param>
        /// <param name="center">The center point for rotation</param>
        public void ApplyRotationTransform(Path shape, double angle, Point center)
        {
            shape.RenderTransform = new RotateTransform
            {
                Angle = angle,
                CenterX = center.X,
                CenterY = center.Y
            };
        }

        /// <summary>
        /// Clears any rotation transform from a shape
        /// </summary>
        /// <param name="shape">The shape to clear rotation from</param>
        public void ClearRotationTransform(Path shape)
        {
            shape.RenderTransform = null;
        }

        /// <summary>
        /// Checks if a point is on a shape and returns the shape if found
        /// </summary>
        public Path? GetShapeAtPoint(Point point, double tolerance = 10.0)
        {
            // Iterate in reverse to get topmost shape first
            for (int i = _shapesCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (_shapesCanvas.Children[i] is Path path && path.Tag is ShapeData)
                {
                    if (IsPointOnShape(path, point, tolerance))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a point is on or near a shape
        /// </summary>
        private bool IsPointOnShape(Path path, Point point, double tolerance)
        {
            if (path.Data == null || !(path.Tag is ShapeData data))
                return false;

            var bounds = path.Data.Bounds;

            // First check bounding box with tolerance
            var expandedBounds = new Rect(
                bounds.X - tolerance,
                bounds.Y - tolerance,
                bounds.Width + tolerance * 2,
                bounds.Height + tolerance * 2);

            if (!expandedBounds.Contains(point))
                return false;

            // For filled shapes, check if inside bounds
            if (data.Type == "Square" || data.Type == "Circle" || data.Type == "Star")
            {
                return bounds.Contains(point);
            }

            // For lines and arrows, check distance to segment
            if (data.Type == "Line" || data.Type == "Arrow")
            {
                return DistanceToLineSegment(point, data.StartPoint, data.EndPoint) <= tolerance;
            }

            return expandedBounds.Contains(point);
        }

        /// <summary>
        /// Calculates the distance from a point to a line segment
        /// </summary>
        private static double DistanceToLineSegment(Point point, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
            {
                double distX = point.X - lineStart.X;
                double distY = point.Y - lineStart.Y;
                return Math.Sqrt(distX * distX + distY * distY);
            }

            double t = Math.Max(0, Math.Min(1,
                ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared));

            double closestX = lineStart.X + t * dx;
            double closestY = lineStart.Y + t * dy;

            double deltaX = point.X - closestX;
            double deltaY = point.Y - closestY;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>
        /// Deletes the currently selected shape
        /// </summary>
        public bool DeleteSelectedShape()
        {
            if (_selectedShape == null)
                return false;

            var shapeToDelete = _selectedShape;
            Deselect();

            if (_shapesCanvas.Children.Contains(shapeToDelete))
            {
                _shapesCanvas.Children.Remove(shapeToDelete);
                _historyManager?.RecordPathRemoved(shapeToDelete);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates settings on the selected shape (color, thickness, etc.)
        /// </summary>
        public void UpdateSelectedShapeSettings(AnnotationSettings settings)
        {
            if (_selectedShape == null)
                return;

            var brush = new SolidColorBrush(settings.StrokeColor)
            {
                Opacity = settings.StrokeOpacity
            };

            _selectedShape.Stroke = brush;
            _selectedShape.StrokeThickness = settings.StrokeThickness;

            if (_selectedShape.Tag is ShapeData data && (data.Type == "Square" || data.Type == "Circle" || data.Type == "Star"))
            {
                if (settings.FillEnabled)
                {
                    _selectedShape.Fill = new SolidColorBrush(settings.FillColor)
                    {
                        Opacity = settings.FillOpacity
                    };
                }
                else
                {
                    _selectedShape.Fill = BrushCache.Transparent;
                }
            }
        }
    }
}
