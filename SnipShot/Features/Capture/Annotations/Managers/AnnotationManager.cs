using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SnipShot.Features.Capture.Annotations.Base;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Features.Capture.Annotations.Tools;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace SnipShot.Features.Capture.Annotations.Managers
{
    /// <summary>
    /// Enumeration of available annotation tools
    /// </summary>
    public enum AnnotationToolType
    {
        None,
        Pen,
        Highlighter,
        Arrow,
        Line,
        Rectangle,
        Ellipse,
        Star,
        Text,
        Emoji
    }

    /// <summary>
    /// Manages all annotation tools and their interactions with the canvas.
    /// Coordinates tool selection, stroke creation, and path management.
    /// </summary>
    public class AnnotationManager
    {
        private readonly Canvas _canvas;
        private readonly Dictionary<AnnotationToolType, IAnnotationTool> _tools;
        private IAnnotationTool? _activeTool;
        private AnnotationToolType _activeToolType = AnnotationToolType.None;
        private bool _isDrawing;
        private Path? _currentPath;

        // Settings for different tool types
        private readonly AnnotationSettings _penSettings;
        private readonly AnnotationSettings _highlighterSettings;
        private readonly AnnotationSettings _shapeSettings;
        
        // Text tool (separate from IAnnotationTool since text works differently)
        private readonly TextTool _textTool;
        private readonly TextData _textSettings;

        // Emoji tool (separate from IAnnotationTool since emojis work differently)
        private readonly EmojiTool _emojiTool;
        private readonly EmojiData _emojiSettings;

        /// <summary>
        /// Event raised when an annotation stroke is completed
        /// </summary>
        public event EventHandler<Path>? StrokeCompleted;

        /// <summary>
        /// Event raised when the active tool changes
        /// </summary>
        public event EventHandler<AnnotationToolType>? ActiveToolChanged;

        /// <summary>
        /// Gets the currently active tool type
        /// </summary>
        public AnnotationToolType ActiveToolType => _activeToolType;

        /// <summary>
        /// Gets whether a tool is currently active
        /// </summary>
        public bool HasActiveTool => _activeToolType != AnnotationToolType.None;

        /// <summary>
        /// Gets whether the user is currently drawing
        /// </summary>
        public bool IsDrawing => _isDrawing;

        /// <summary>
        /// Gets the pen settings
        /// </summary>
        public AnnotationSettings PenSettings => _penSettings;

        /// <summary>
        /// Gets the highlighter settings
        /// </summary>
        public AnnotationSettings HighlighterSettings => _highlighterSettings;

        /// <summary>
        /// Gets the shape settings
        /// </summary>
        public AnnotationSettings ShapeSettings => _shapeSettings;

        /// <summary>
        /// Gets the text settings
        /// </summary>
        public TextData TextSettings => _textSettings;

        /// <summary>
        /// Gets the text tool
        /// </summary>
        public TextTool TextTool => _textTool;

        /// <summary>
        /// Gets the emoji tool
        /// </summary>
        public EmojiTool EmojiTool => _emojiTool;

        /// <summary>
        /// Gets the emoji settings
        /// </summary>
        public EmojiData EmojiSettings => _emojiSettings;

        /// <summary>
        /// Gets whether the text tool is currently active
        /// </summary>
        public bool IsTextToolActive => _activeToolType == AnnotationToolType.Text;

        /// <summary>
        /// Gets whether the emoji tool is currently active
        /// </summary>
        public bool IsEmojiToolActive => _activeToolType == AnnotationToolType.Emoji;

        /// <summary>
        /// Creates a new AnnotationManager
        /// </summary>
        /// <param name="canvas">The canvas to draw annotations on</param>
        public AnnotationManager(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            
            // Initialize default settings
            _penSettings = AnnotationSettings.DefaultPen.Clone();
            _highlighterSettings = AnnotationSettings.DefaultHighlighter.Clone();
            _shapeSettings = AnnotationSettings.DefaultShape.Clone();
            _textSettings = new TextData();
            _emojiSettings = EmojiData.Default;

            // Initialize tools
            _tools = new Dictionary<AnnotationToolType, IAnnotationTool>
            {
                { AnnotationToolType.Pen, new PenTool(_penSettings) },
                { AnnotationToolType.Highlighter, new HighlighterTool(_highlighterSettings) },
                { AnnotationToolType.Arrow, new ArrowTool(_shapeSettings) },
                { AnnotationToolType.Line, new LineTool(_shapeSettings) },
                { AnnotationToolType.Rectangle, new RectangleTool(_shapeSettings) },
                { AnnotationToolType.Ellipse, new EllipseTool(_shapeSettings) },
                { AnnotationToolType.Star, new StarTool(_shapeSettings) }
            };

            // Initialize text tool (separate from IAnnotationTool)
            _textTool = new TextTool(_textSettings);

            // Initialize emoji tool (separate from IAnnotationTool)
            _emojiTool = new EmojiTool(_emojiSettings);
        }

        /// <summary>
        /// Sets the active annotation tool
        /// </summary>
        /// <param name="toolType">The type of tool to activate</param>
        public void SetActiveTool(AnnotationToolType toolType)
        {
            if (_isDrawing)
            {
                EndStroke();
            }

            // Deactivate text tool if switching away from it
            if (_activeToolType == AnnotationToolType.Text && toolType != AnnotationToolType.Text)
            {
                _textTool.Deactivate();
            }

            // Deactivate emoji tool if switching away from it
            if (_activeToolType == AnnotationToolType.Emoji && toolType != AnnotationToolType.Emoji)
            {
                _emojiTool.Deactivate();
            }

            _activeToolType = toolType;
            
            if (toolType == AnnotationToolType.None)
            {
                _activeTool = null;
            }
            else if (toolType == AnnotationToolType.Text)
            {
                // Text tool is handled separately
                _activeTool = null;
                _textTool.Activate();
            }
            else if (toolType == AnnotationToolType.Emoji)
            {
                // Emoji tool is handled separately
                _activeTool = null;
                _emojiTool.Activate();
            }
            else if (_tools.TryGetValue(toolType, out var tool))
            {
                _activeTool = tool;
                
                // Apply appropriate settings to the tool
                var settings = GetSettingsForTool(toolType);
                _activeTool.Settings = settings;
                _activeTool.Activate();
            }

            ActiveToolChanged?.Invoke(this, toolType);
        }

        /// <summary>
        /// Deactivates the current tool
        /// </summary>
        public void DeactivateTool()
        {
            SetActiveTool(AnnotationToolType.None);
        }

        /// <summary>
        /// Gets the settings for a specific tool type
        /// </summary>
        private AnnotationSettings GetSettingsForTool(AnnotationToolType toolType)
        {
            return toolType switch
            {
                AnnotationToolType.Pen => _penSettings,
                AnnotationToolType.Highlighter => _highlighterSettings,
                AnnotationToolType.Arrow or 
                AnnotationToolType.Line or 
                AnnotationToolType.Rectangle or 
                AnnotationToolType.Ellipse => _shapeSettings,
                _ => _shapeSettings
            };
        }

        /// <summary>
        /// Starts a stroke at the specified point
        /// </summary>
        /// <param name="point">The starting point</param>
        /// <returns>True if the stroke was started successfully</returns>
        public bool StartStroke(Point point)
        {
            if (_activeTool == null || _isDrawing)
                return false;

            _currentPath = _activeTool.StartStroke(point);
            if (_currentPath != null)
            {
                _canvas.Children.Add(_currentPath);
                _isDrawing = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Continues the current stroke to the specified point
        /// </summary>
        /// <param name="point">The current point</param>
        /// <param name="constrainProportions">If true, constrains shapes to equal proportions (square/circle)</param>
        public void ContinueStroke(Point point, bool constrainProportions = false)
        {
            if (_activeTool == null || !_isDrawing || _currentPath == null)
                return;

            // Apply constraint to shape tools that support it
            if (_activeTool is ShapeTool shapeTool && shapeTool.SupportsConstrainedProportions)
            {
                shapeTool.ConstrainProportions = constrainProportions;
            }

            _activeTool.ContinueStroke(point);
        }

        /// <summary>
        /// Ends the current stroke
        /// </summary>
        /// <returns>The completed path, or null if no stroke was active</returns>
        public Path? EndStroke()
        {
            if (_activeTool == null || !_isDrawing || _currentPath == null)
                return null;

            var currentPath = _currentPath;
            var completedPath = _activeTool.EndStroke();
            
            _isDrawing = false;
            _currentPath = null;

            if (completedPath != null)
            {
                StrokeCompleted?.Invoke(this, completedPath);
            }
            else if (currentPath != null)
            {
                _canvas.Children.Remove(currentPath);
            }
            return completedPath;
        }

        /// <summary>
        /// Cancels the current stroke and removes it from the canvas
        /// </summary>
        public void CancelStroke()
        {
            if (_currentPath != null && _isDrawing)
            {
                _canvas.Children.Remove(_currentPath);
            }

            _isDrawing = false;
            _currentPath = null;
        }

        #region Text Tool Methods

        /// <summary>
        /// Creates a new text element at the specified position.
        /// Only works when the Text tool is active.
        /// </summary>
        /// <param name="position">The position to create the text element.</param>
        /// <returns>The created Grid element, or null if Text tool is not active.</returns>
        public Grid? CreateTextElement(Point position)
        {
            if (_activeToolType != AnnotationToolType.Text)
                return null;

            var textElement = _textTool.CreateTextElement(position);
            _canvas.Children.Add(textElement);
            
            return textElement;
        }

        /// <summary>
        /// Starts editing an existing text element.
        /// </summary>
        public void StartTextEditing(Grid textElement)
        {
            _textTool.StartEditing(textElement);
        }

        /// <summary>
        /// Ends editing a text element.
        /// </summary>
        /// <returns>True if text has content, false if empty (should be removed).</returns>
        public bool EndTextEditing(Grid textElement)
        {
            return _textTool.EndEditing(textElement);
        }

        /// <summary>
        /// Removes a text element from the canvas.
        /// </summary>
        public void RemoveTextElement(Grid textElement)
        {
            _canvas.Children.Remove(textElement);
        }

        #endregion

        #region Emoji Tool Methods

        /// <summary>
        /// Creates a new emoji element at the specified position.
        /// </summary>
        /// <param name="position">The position to create the emoji element.</param>
        /// <param name="emoji">The emoji character to display.</param>
        /// <returns>The created Grid element.</returns>
        public Grid? CreateEmojiElement(Point position, string emoji)
        {
            var emojiElement = _emojiTool.CreateEmoji(position, emoji);
            _canvas.Children.Add(emojiElement);
            
            return emojiElement;
        }

        #endregion

        /// <summary>
        /// Updates the pen color
        /// </summary>
        public void SetPenColor(Color color)
        {
            _penSettings.StrokeColor = color;
            if (_activeToolType == AnnotationToolType.Pen && _activeTool != null)
            {
                _activeTool.Settings = _penSettings;
            }
        }

        /// <summary>
        /// Updates the pen thickness
        /// </summary>
        public void SetPenThickness(double thickness)
        {
            _penSettings.StrokeThickness = thickness;
            if (_activeToolType == AnnotationToolType.Pen && _activeTool != null)
            {
                _activeTool.Settings = _penSettings;
            }
        }

        /// <summary>
        /// Updates the highlighter color
        /// </summary>
        public void SetHighlighterColor(Color color)
        {
            _highlighterSettings.StrokeColor = color;
            if (_activeToolType == AnnotationToolType.Highlighter && _activeTool != null)
            {
                _activeTool.Settings = _highlighterSettings;
            }
        }

        /// <summary>
        /// Updates the highlighter thickness
        /// </summary>
        public void SetHighlighterThickness(double thickness)
        {
            _highlighterSettings.StrokeThickness = thickness;
            if (_activeToolType == AnnotationToolType.Highlighter && _activeTool != null)
            {
                _activeTool.Settings = _highlighterSettings;
            }
        }

        /// <summary>
        /// Updates the shape stroke color
        /// </summary>
        public void SetShapeStrokeColor(Color color)
        {
            _shapeSettings.StrokeColor = color;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Updates the shape stroke opacity
        /// </summary>
        public void SetShapeStrokeOpacity(double opacity)
        {
            _shapeSettings.StrokeOpacity = opacity;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Updates the shape stroke thickness
        /// </summary>
        public void SetShapeStrokeThickness(double thickness)
        {
            _shapeSettings.StrokeThickness = thickness;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Updates the shape fill color
        /// </summary>
        public void SetShapeFillColor(Color color)
        {
            _shapeSettings.FillColor = color;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Updates the shape fill opacity
        /// </summary>
        public void SetShapeFillOpacity(double opacity)
        {
            _shapeSettings.FillOpacity = opacity;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Enables or disables fill for shapes
        /// </summary>
        public void SetShapeFillEnabled(bool enabled)
        {
            _shapeSettings.FillEnabled = enabled;
            UpdateShapeToolSettings();
        }

        /// <summary>
        /// Updates the current active shape tool with new settings
        /// </summary>
        private void UpdateShapeToolSettings()
        {
            if (_activeTool != null && IsShapeTool(_activeToolType))
            {
                _activeTool.Settings = _shapeSettings;
            }
        }

        /// <summary>
        /// Checks if the tool type is a shape tool
        /// </summary>
        public static bool IsShapeTool(AnnotationToolType toolType)
        {
            return toolType switch
            {
                AnnotationToolType.Arrow or 
                AnnotationToolType.Line or 
                AnnotationToolType.Rectangle or 
                AnnotationToolType.Ellipse or
                AnnotationToolType.Star => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if the tool type is a freehand tool (pen or highlighter)
        /// </summary>
        public static bool IsFreehandTool(AnnotationToolType toolType)
        {
            return toolType == AnnotationToolType.Pen || toolType == AnnotationToolType.Highlighter;
        }

        /// <summary>
        /// Removes a path from the canvas
        /// </summary>
        public void RemovePath(Path path)
        {
            if (_canvas.Children.Contains(path))
            {
                _canvas.Children.Remove(path);
            }
        }

        /// <summary>
        /// Adds a path to the canvas
        /// </summary>
        public void AddPath(Path path)
        {
            if (!_canvas.Children.Contains(path))
            {
                _canvas.Children.Add(path);
            }
        }

        /// <summary>
        /// Clears all annotations from the canvas
        /// </summary>
        public void ClearAllAnnotations()
        {
            _canvas.Children.Clear();
        }

        /// <summary>
        /// Gets all paths on the canvas
        /// </summary>
        public IEnumerable<Path> GetAllPaths()
        {
            foreach (var child in _canvas.Children)
            {
                if (child is Path path)
                {
                    yield return path;
                }
            }
        }

        /// <summary>
        /// Gets the path at a specific point, if any
        /// </summary>
        /// <param name="point">The point to check</param>
        /// <param name="hitTestTolerance">The tolerance for hit testing</param>
        /// <returns>The path at the point, or null if none found</returns>
        public Path? GetPathAtPoint(Point point, double hitTestTolerance = 10.0)
        {
            // Iterate in reverse order to get the topmost path first
            for (int i = _canvas.Children.Count - 1; i >= 0; i--)
            {
                if (_canvas.Children[i] is Path path)
                {
                    if (IsPointOnPath(path, point, hitTestTolerance))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a point is on or near a path
        /// </summary>
        private bool IsPointOnPath(Path path, Point point, double tolerance)
        {
            if (path.Data == null)
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

            // For shapes with fill, check if point is inside
            if (path.Tag is ShapeData shapeData && (shapeData.Type == "Square" || shapeData.Type == "Circle" || shapeData.Type == "Star"))
            {
                return bounds.Contains(point);
            }

            // For lines and arrows, check distance to line segment
            if (path.Tag is ShapeData lineData && (lineData.Type == "Line" || lineData.Type == "Arrow"))
            {
                return DistanceToLineSegment(point, lineData.StartPoint, lineData.EndPoint) <= tolerance;
            }

            // For freehand paths (pen/highlighter), use bounding box with tolerance
            var strokeThickness = path.StrokeThickness;
            var adjustedBounds = new Rect(
                bounds.X - strokeThickness / 2 - tolerance,
                bounds.Y - strokeThickness / 2 - tolerance,
                bounds.Width + strokeThickness + tolerance * 2,
                bounds.Height + strokeThickness + tolerance * 2);

            return adjustedBounds.Contains(point);
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
                // Line segment is a point
                double distX = point.X - lineStart.X;
                double distY = point.Y - lineStart.Y;
                return Math.Sqrt(distX * distX + distY * distY);
            }

            // Calculate projection parameter
            double t = Math.Max(0, Math.Min(1, 
                ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared));

            // Calculate closest point on segment
            double closestX = lineStart.X + t * dx;
            double closestY = lineStart.Y + t * dy;

            // Calculate distance
            double deltaX = point.X - closestX;
            double deltaY = point.Y - closestY;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }
    }
}
