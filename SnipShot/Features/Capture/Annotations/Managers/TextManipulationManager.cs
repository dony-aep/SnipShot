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
    /// Handle types for text element resizing
    /// </summary>
    public enum TextHandle
    {
        None,
        NorthWest,  // Top-left corner
        NorthEast,  // Top-right corner
        SouthWest,  // Bottom-left corner
        SouthEast   // Bottom-right corner
    }

    /// <summary>
    /// Manages selection, dragging, and resizing of text elements
    /// </summary>
    public class TextManipulationManager
    {
        private readonly Canvas _textCanvas;
        private readonly Canvas _handlesCanvas;
        private readonly AnnotationHistoryManager? _historyManager;

        // Selection state
        private Grid? _selectedText;
        private TextData? _originalTextData;
        private bool _isDragging;
        private bool _isResizing;
        private Point _dragStart;
        private TextHandle _activeHandle = TextHandle.None;
        private Rect _boundsBeforeResize;

        // Handle elements
        private readonly Rectangle _handleNW;
        private readonly Rectangle _handleNE;
        private readonly Rectangle _handleSW;
        private readonly Rectangle _handleSE;
        private readonly Rectangle _selectionBorder;

        // Handle design constants
        private const double HANDLE_SIZE = 10;
        private const double HANDLE_STROKE_WIDTH = 2;
        private const double MIN_TEXT_WIDTH = 50;
        private const double MIN_TEXT_HEIGHT = 24;

        // Colors for handles
        private static readonly Color HandleFillColor = Color.FromArgb(255, 0, 120, 215);
        private static readonly Color HandleStrokeColor = Colors.White;
        private static readonly Color HandleHoverFillColor = Color.FromArgb(255, 0, 90, 180);
        private static readonly Color SelectionBorderColor = Color.FromArgb(255, 0, 120, 215);

        /// <summary>
        /// Event raised when the selected text changes
        /// </summary>
        public event EventHandler<Grid?>? SelectionChanged;

        /// <summary>
        /// Event raised when a text element is moved or resized
        /// </summary>
        public event EventHandler<Grid>? TextModified;

        /// <summary>
        /// Gets the currently selected text element
        /// </summary>
        public Grid? SelectedText => _selectedText;

        /// <summary>
        /// Gets whether a text element is currently selected
        /// </summary>
        public bool HasSelection => _selectedText != null;

        /// <summary>
        /// Gets whether the user is currently dragging a text element
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Gets whether the user is currently resizing a text element
        /// </summary>
        public bool IsResizing => _isResizing;

        /// <summary>
        /// Gets whether the user is manipulating a text element
        /// </summary>
        public bool IsManipulating => _isDragging || _isResizing;

        /// <summary>
        /// Creates a new TextManipulationManager
        /// </summary>
        public TextManipulationManager(
            Canvas textCanvas,
            Canvas handlesCanvas,
            AnnotationHistoryManager? historyManager = null)
        {
            _textCanvas = textCanvas ?? throw new ArgumentNullException(nameof(textCanvas));
            _handlesCanvas = handlesCanvas ?? throw new ArgumentNullException(nameof(handlesCanvas));
            _historyManager = historyManager;

            // Create handle elements
            _handleNW = CreateCornerHandle("NW");
            _handleNE = CreateCornerHandle("NE");
            _handleSW = CreateCornerHandle("SW");
            _handleSE = CreateCornerHandle("SE");
            _selectionBorder = CreateSelectionBorder();

            // Add handles to canvas (initially hidden)
            _handlesCanvas.Children.Add(_selectionBorder);
            _handlesCanvas.Children.Add(_handleNW);
            _handlesCanvas.Children.Add(_handleNE);
            _handlesCanvas.Children.Add(_handleSW);
            _handlesCanvas.Children.Add(_handleSE);

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
            // Change cursor to indicate draggable area
            if (_selectedText != null && !_isResizing)
            {
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(20, 0, 120, 215)); // Light highlight
            }
        }

        private void SelectionBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(1, 0, 0, 0)); // Reset to nearly transparent
            }
        }

        private void SelectionBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_selectedText == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Store original state for history
            if (_selectedText.Tag is TextData textData)
            {
                _originalTextData = textData.Clone();
            }

            _isDragging = true;
            _dragStart = point.Position;
            _selectionBorder.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void SelectionBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || _selectedText == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            var delta = new Point(
                point.Position.X - _dragStart.X,
                point.Position.Y - _dragStart.Y
            );

            var currentLeft = Canvas.GetLeft(_selectedText);
            var currentTop = Canvas.GetTop(_selectedText);

            Canvas.SetLeft(_selectedText, currentLeft + delta.X);
            Canvas.SetTop(_selectedText, currentTop + delta.Y);

            // Update TextData
            if (_selectedText.Tag is TextData textData)
            {
                textData.Position = new Point(currentLeft + delta.X, currentTop + delta.Y);
            }

            _dragStart = point.Position;
            UpdateHandles();
            e.Handled = true;
        }

        private void SelectionBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging && _selectedText != null)
            {
                _selectionBorder.ReleasePointerCapture(e.Pointer);
                _selectionBorder.Fill = BrushCache.GetBrush(Color.FromArgb(1, 0, 0, 0)); // Reset to nearly transparent

                // Record history
                if (_originalTextData != null && _selectedText.Tag is TextData newData)
                {
                    _historyManager?.RecordTextModified(_selectedText, _originalTextData, newData);
                }

                TextModified?.Invoke(this, _selectedText);
            }

            _isDragging = false;
            _originalTextData = null;
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
            if (sender is not FrameworkElement handle || _selectedText == null)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Store original state for history
            if (_selectedText.Tag is TextData textData)
            {
                _originalTextData = textData.Clone();
            }

            _activeHandle = handle.Tag?.ToString() switch
            {
                "NW" => TextHandle.NorthWest,
                "NE" => TextHandle.NorthEast,
                "SW" => TextHandle.SouthWest,
                "SE" => TextHandle.SouthEast,
                _ => TextHandle.None
            };

            if (_activeHandle != TextHandle.None)
            {
                _isResizing = true;
                _dragStart = point.Position;

                var left = Canvas.GetLeft(_selectedText);
                var top = Canvas.GetTop(_selectedText);
                var width = double.IsNaN(_selectedText.Width) ? _selectedText.ActualWidth : _selectedText.Width;
                var height = double.IsNaN(_selectedText.Height) ? _selectedText.ActualHeight : _selectedText.Height;
                _boundsBeforeResize = new Rect(left, top, width, height);

                handle.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        }

        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isResizing || _selectedText == null || _activeHandle == TextHandle.None)
                return;

            var point = e.GetCurrentPoint(_handlesCanvas);
            var delta = new Point(
                point.Position.X - _dragStart.X,
                point.Position.Y - _dragStart.Y
            );

            double newLeft = _boundsBeforeResize.Left;
            double newTop = _boundsBeforeResize.Top;
            double newWidth = _boundsBeforeResize.Width;
            double newHeight = _boundsBeforeResize.Height;

            switch (_activeHandle)
            {
                case TextHandle.NorthWest:
                    newLeft = _boundsBeforeResize.Left + delta.X;
                    newTop = _boundsBeforeResize.Top + delta.Y;
                    newWidth = _boundsBeforeResize.Width - delta.X;
                    newHeight = _boundsBeforeResize.Height - delta.Y;
                    break;

                case TextHandle.NorthEast:
                    newTop = _boundsBeforeResize.Top + delta.Y;
                    newWidth = _boundsBeforeResize.Width + delta.X;
                    newHeight = _boundsBeforeResize.Height - delta.Y;
                    break;

                case TextHandle.SouthWest:
                    newLeft = _boundsBeforeResize.Left + delta.X;
                    newWidth = _boundsBeforeResize.Width - delta.X;
                    newHeight = _boundsBeforeResize.Height + delta.Y;
                    break;

                case TextHandle.SouthEast:
                    newWidth = _boundsBeforeResize.Width + delta.X;
                    newHeight = _boundsBeforeResize.Height + delta.Y;
                    break;
            }

            // Apply minimum constraints
            if (newWidth < MIN_TEXT_WIDTH)
            {
                if (_activeHandle == TextHandle.NorthWest || _activeHandle == TextHandle.SouthWest)
                {
                    newLeft = _boundsBeforeResize.Right - MIN_TEXT_WIDTH;
                }
                newWidth = MIN_TEXT_WIDTH;
            }

            if (newHeight < MIN_TEXT_HEIGHT)
            {
                if (_activeHandle == TextHandle.NorthWest || _activeHandle == TextHandle.NorthEast)
                {
                    newTop = _boundsBeforeResize.Bottom - MIN_TEXT_HEIGHT;
                }
                newHeight = MIN_TEXT_HEIGHT;
            }

            // Apply changes
            Canvas.SetLeft(_selectedText, newLeft);
            Canvas.SetTop(_selectedText, newTop);
            _selectedText.Width = newWidth;
            _selectedText.Height = newHeight;

            // Update TextData
            if (_selectedText.Tag is TextData textData)
            {
                textData.Position = new Point(newLeft, newTop);
                textData.Width = newWidth;
                textData.Height = newHeight;
            }

            UpdateHandles();
            e.Handled = true;
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isResizing && _selectedText != null && sender is FrameworkElement handle)
            {
                handle.ReleasePointerCapture(e.Pointer);

                // Record history
                if (_originalTextData != null && _selectedText.Tag is TextData newData)
                {
                    _historyManager?.RecordTextModified(_selectedText, _originalTextData, newData);
                }

                TextModified?.Invoke(this, _selectedText);
            }

            _isResizing = false;
            _activeHandle = TextHandle.None;
            _originalTextData = null;
            e.Handled = true;
        }

        #endregion

        #region Selection

        /// <summary>
        /// Selects a text element
        /// </summary>
        public void SelectText(Grid textElement)
        {
            if (_selectedText == textElement)
                return;

            Deselect();

            _selectedText = textElement;
            _handlesCanvas.Visibility = Visibility.Visible;
            UpdateHandles();

            SelectionChanged?.Invoke(this, textElement);
        }

        /// <summary>
        /// Deselects the current text element
        /// </summary>
        public void Deselect()
        {
            if (_selectedText == null)
                return;

            _selectedText = null;
            HideHandles();
            _handlesCanvas.Visibility = Visibility.Collapsed;

            SelectionChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Gets a text element at the specified point
        /// </summary>
        public Grid? GetTextAtPoint(Point point)
        {
            foreach (var child in _textCanvas.Children)
            {
                if (child is Grid grid && grid.Tag is TextData)
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

        #endregion

        #region Dragging

        /// <summary>
        /// Starts dragging the selected text element
        /// </summary>
        public bool StartDrag(Point position)
        {
            if (_selectedText == null)
                return false;

            // Store original state for history
            if (_selectedText.Tag is TextData textData)
            {
                _originalTextData = textData.Clone();
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
            if (!_isDragging || _selectedText == null)
                return;

            var delta = new Point(
                position.X - _dragStart.X,
                position.Y - _dragStart.Y
            );

            var currentLeft = Canvas.GetLeft(_selectedText);
            var currentTop = Canvas.GetTop(_selectedText);

            Canvas.SetLeft(_selectedText, currentLeft + delta.X);
            Canvas.SetTop(_selectedText, currentTop + delta.Y);

            // Update TextData
            if (_selectedText.Tag is TextData textData)
            {
                textData.Position = new Point(currentLeft + delta.X, currentTop + delta.Y);
            }

            _dragStart = position;
            UpdateHandles();
        }

        /// <summary>
        /// Ends the drag operation
        /// </summary>
        public void EndDrag()
        {
            if (_isDragging && _selectedText != null)
            {
                // Record history
                if (_originalTextData != null && _selectedText.Tag is TextData newData)
                {
                    _historyManager?.RecordTextModified(_selectedText, _originalTextData, newData);
                }

                TextModified?.Invoke(this, _selectedText);
            }

            _isDragging = false;
            _originalTextData = null;
        }

        #endregion

        #region Handle Positioning

        private void HideHandles()
        {
            _handleNW.Visibility = Visibility.Collapsed;
            _handleNE.Visibility = Visibility.Collapsed;
            _handleSW.Visibility = Visibility.Collapsed;
            _handleSE.Visibility = Visibility.Collapsed;
            _selectionBorder.Visibility = Visibility.Collapsed;
        }

        private void UpdateHandles()
        {
            if (_selectedText == null)
            {
                HideHandles();
                return;
            }

            var left = Canvas.GetLeft(_selectedText);
            var top = Canvas.GetTop(_selectedText);
            var width = double.IsNaN(_selectedText.Width) ? _selectedText.ActualWidth : _selectedText.Width;
            var height = double.IsNaN(_selectedText.Height) ? _selectedText.ActualHeight : _selectedText.Height;

            if (width <= 0) width = MIN_TEXT_WIDTH;
            if (height <= 0) height = MIN_TEXT_HEIGHT;

            var bounds = new Rect(left, top, width, height);
            var halfHandle = HANDLE_SIZE / 2;

            // Position corner handles
            PositionHandle(_handleNW, bounds.Left - halfHandle, bounds.Top - halfHandle);
            PositionHandle(_handleNE, bounds.Right - halfHandle, bounds.Top - halfHandle);
            PositionHandle(_handleSW, bounds.Left - halfHandle, bounds.Bottom - halfHandle);
            PositionHandle(_handleSE, bounds.Right - halfHandle, bounds.Bottom - halfHandle);

            // Position selection border
            Canvas.SetLeft(_selectionBorder, bounds.Left - 2);
            Canvas.SetTop(_selectionBorder, bounds.Top - 2);
            _selectionBorder.Width = bounds.Width + 4;
            _selectionBorder.Height = bounds.Height + 4;
            _selectionBorder.Visibility = Visibility.Visible;
        }

        private void PositionHandle(Rectangle handle, double x, double y)
        {
            Canvas.SetLeft(handle, x);
            Canvas.SetTop(handle, y);
            handle.Visibility = Visibility.Visible;
        }

        #endregion

        #region Double-Click Detection

        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPosition;
        private const double DoubleClickTimeMs = 500;
        private const double DoubleClickDistance = 5;

        /// <summary>
        /// Checks if this click is a double-click.
        /// Note: Double-click to edit is DISABLED. Text is a static annotation once created.
        /// This method now always returns false.
        /// </summary>
        public bool HandlePotentialDoubleClick(Point position, Grid textElement)
        {
            // Double-click to edit is DISABLED
            // Text elements are static annotations that cannot be edited after creation
            // They can only be moved or deleted
            
            // Still track clicks for potential future use, but don't trigger edit
            _lastClickTime = DateTime.Now;
            _lastClickPosition = position;
            
            return false; // Never trigger edit mode
        }

        #endregion
    }
}
