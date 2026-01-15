using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;

namespace SnipShot.Features.Capture.Annotations.Managers
{
    /// <summary>
    /// Represents an action that can be undone and redone
    /// </summary>
    public interface IHistoryAction
    {
        /// <summary>
        /// Undoes this action
        /// </summary>
        void Undo();

        /// <summary>
        /// Redoes this action
        /// </summary>
        void Redo();

        /// <summary>
        /// Gets a description of this action
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// History action for adding a path to the canvas
    /// </summary>
    public class AddPathAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly Path _path;

        public string Description => "Add annotation";

        public AddPathAction(Canvas canvas, Path path)
        {
            _canvas = canvas;
            _path = path;
        }

        public void Undo()
        {
            if (_canvas.Children.Contains(_path))
            {
                _canvas.Children.Remove(_path);
            }
        }

        public void Redo()
        {
            if (!_canvas.Children.Contains(_path))
            {
                _canvas.Children.Add(_path);
            }
        }
    }

    /// <summary>
    /// History action for removing a path from the canvas
    /// </summary>
    public class RemovePathAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly Path _path;

        public string Description => "Remove annotation";

        public RemovePathAction(Canvas canvas, Path path)
        {
            _canvas = canvas;
            _path = path;
        }

        public void Undo()
        {
            if (!_canvas.Children.Contains(_path))
            {
                _canvas.Children.Add(_path);
            }
        }

        public void Redo()
        {
            if (_canvas.Children.Contains(_path))
            {
                _canvas.Children.Remove(_path);
            }
        }
    }

    /// <summary>
    /// History action for adding a generic UIElement (like text) to the canvas
    /// </summary>
    public class AddElementAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;

        public string Description => "Add element";

        public AddElementAction(Canvas canvas, UIElement element)
        {
            _canvas = canvas;
            _element = element;
        }

        public void Undo()
        {
            if (_canvas.Children.Contains(_element))
            {
                _canvas.Children.Remove(_element);
            }
        }

        public void Redo()
        {
            if (!_canvas.Children.Contains(_element))
            {
                _canvas.Children.Add(_element);
            }
        }
    }

    /// <summary>
    /// History action for removing a generic UIElement from the canvas
    /// </summary>
    public class RemoveElementAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;

        public string Description => "Remove element";

        public RemoveElementAction(Canvas canvas, UIElement element)
        {
            _canvas = canvas;
            _element = element;
        }

        public void Undo()
        {
            if (!_canvas.Children.Contains(_element))
            {
                _canvas.Children.Add(_element);
            }
        }

        public void Redo()
        {
            if (_canvas.Children.Contains(_element))
            {
                _canvas.Children.Remove(_element);
            }
        }
    }

    /// <summary>
    /// History action for moving a path
    /// </summary>
    public class MovePathAction : IHistoryAction
    {
        private readonly Path _path;
        private readonly Models.ShapeData? _originalData;
        private readonly Models.ShapeData? _newData;

        public string Description => "Move annotation";

        public MovePathAction(Path path, Models.ShapeData? originalData, Models.ShapeData? newData)
        {
            _path = path;
            _originalData = originalData != null ? originalData.Clone() : null;
            _newData = newData != null ? newData.Clone() : null;
        }

        public void Undo()
        {
            if (_originalData != null && _path.Tag is Models.ShapeData currentData)
            {
                currentData.StartPoint = _originalData.StartPoint;
                currentData.EndPoint = _originalData.EndPoint;
                UpdatePathGeometry();
            }
        }

        public void Redo()
        {
            if (_newData != null && _path.Tag is Models.ShapeData currentData)
            {
                currentData.StartPoint = _newData.StartPoint;
                currentData.EndPoint = _newData.EndPoint;
                UpdatePathGeometry();
            }
        }

        private void UpdatePathGeometry()
        {
            // The geometry update will be handled by the ShapeManipulationManager
            // when it receives notification of data changes
        }
    }

    /// <summary>
    /// History action for resizing a path
    /// </summary>
    public class ResizePathAction : IHistoryAction
    {
        private readonly Path _path;
        private readonly Models.ShapeData? _originalData;
        private readonly Models.ShapeData? _newData;

        public string Description => "Resize annotation";

        public ResizePathAction(Path path, Models.ShapeData? originalData, Models.ShapeData? newData)
        {
            _path = path;
            _originalData = originalData != null ? originalData.Clone() : null;
            _newData = newData != null ? newData.Clone() : null;
        }

        public void Undo()
        {
            if (_originalData != null && _path.Tag is Models.ShapeData currentData)
            {
                currentData.StartPoint = _originalData.StartPoint;
                currentData.EndPoint = _originalData.EndPoint;
            }
        }

        public void Redo()
        {
            if (_newData != null && _path.Tag is Models.ShapeData currentData)
            {
                currentData.StartPoint = _newData.StartPoint;
                currentData.EndPoint = _newData.EndPoint;
            }
        }
    }

    /// <summary>
    /// History action for modifying a text element (move/resize/style)
    /// </summary>
    public class ModifyTextAction : IHistoryAction
    {
        private readonly Grid _textElement;
        private readonly Models.TextData _originalData;
        private readonly Models.TextData _newData;

        public string Description => "Modify text";

        public ModifyTextAction(Grid textElement, Models.TextData originalData, Models.TextData newData)
        {
            _textElement = textElement;
            _originalData = originalData.Clone();
            _newData = newData.Clone();
        }

        public void Undo()
        {
            if (_textElement.Tag is Models.TextData currentData)
            {
                // Restore original data
                currentData.Text = _originalData.Text;
                currentData.FontFamily = _originalData.FontFamily;
                currentData.FontSize = _originalData.FontSize;
                currentData.IsBold = _originalData.IsBold;
                currentData.IsItalic = _originalData.IsItalic;
                currentData.IsUnderline = _originalData.IsUnderline;
                currentData.IsStrikethrough = _originalData.IsStrikethrough;
                currentData.TextColor = _originalData.TextColor;
                currentData.HighlightColor = _originalData.HighlightColor;
                currentData.Position = _originalData.Position;
                currentData.Width = _originalData.Width;
                currentData.Height = _originalData.Height;

                // Update visual position
                Canvas.SetLeft(_textElement, _originalData.Position.X);
                Canvas.SetTop(_textElement, _originalData.Position.Y);
                _textElement.Width = _originalData.Width;
                _textElement.Height = _originalData.Height;
            }
        }

        public void Redo()
        {
            if (_textElement.Tag is Models.TextData currentData)
            {
                // Apply new data
                currentData.Text = _newData.Text;
                currentData.FontFamily = _newData.FontFamily;
                currentData.FontSize = _newData.FontSize;
                currentData.IsBold = _newData.IsBold;
                currentData.IsItalic = _newData.IsItalic;
                currentData.IsUnderline = _newData.IsUnderline;
                currentData.IsStrikethrough = _newData.IsStrikethrough;
                currentData.TextColor = _newData.TextColor;
                currentData.HighlightColor = _newData.HighlightColor;
                currentData.Position = _newData.Position;
                currentData.Width = _newData.Width;
                currentData.Height = _newData.Height;

                // Update visual position
                Canvas.SetLeft(_textElement, _newData.Position.X);
                Canvas.SetTop(_textElement, _newData.Position.Y);
                _textElement.Width = _newData.Width;
                _textElement.Height = _newData.Height;
            }
        }
    }

    /// <summary>
    /// History action for modifying an emoji element (move/resize)
    /// </summary>
    public class ModifyEmojiAction : IHistoryAction
    {
        private readonly Grid _emojiElement;
        private readonly Models.EmojiData _originalData;
        private readonly Models.EmojiData _newData;

        public string Description => "Modify emoji";

        public ModifyEmojiAction(Grid emojiElement, Models.EmojiData originalData, Models.EmojiData newData)
        {
            _emojiElement = emojiElement;
            _originalData = originalData.Clone();
            _newData = newData.Clone();
        }

        public void Undo()
        {
            if (_emojiElement.Tag is Models.EmojiData currentData)
            {
                // Restore original data
                currentData.Emoji = _originalData.Emoji;
                currentData.FontSize = _originalData.FontSize;
                currentData.Position = _originalData.Position;
                currentData.Width = _originalData.Width;
                currentData.Height = _originalData.Height;
                currentData.RotationAngle = _originalData.RotationAngle;

                // Update visual position
                Canvas.SetLeft(_emojiElement, _originalData.Position.X);
                Canvas.SetTop(_emojiElement, _originalData.Position.Y);
                _emojiElement.Width = _originalData.Width;
                _emojiElement.Height = _originalData.Height;

                // Update rotation
                ApplyRotation(_emojiElement, _originalData.RotationAngle);

                // Update TextBlock inside
                if (_emojiElement.Children.Count > 0 && _emojiElement.Children[0] is TextBlock textBlock)
                {
                    textBlock.FontSize = _originalData.FontSize;
                    textBlock.Text = _originalData.Emoji;
                }
            }
        }

        public void Redo()
        {
            if (_emojiElement.Tag is Models.EmojiData currentData)
            {
                // Apply new data
                currentData.Emoji = _newData.Emoji;
                currentData.FontSize = _newData.FontSize;
                currentData.Position = _newData.Position;
                currentData.Width = _newData.Width;
                currentData.Height = _newData.Height;
                currentData.RotationAngle = _newData.RotationAngle;

                // Update visual position
                Canvas.SetLeft(_emojiElement, _newData.Position.X);
                Canvas.SetTop(_emojiElement, _newData.Position.Y);
                _emojiElement.Width = _newData.Width;
                _emojiElement.Height = _newData.Height;

                // Update rotation
                ApplyRotation(_emojiElement, _newData.RotationAngle);

                // Update TextBlock inside
                if (_emojiElement.Children.Count > 0 && _emojiElement.Children[0] is TextBlock textBlock)
                {
                    textBlock.FontSize = _newData.FontSize;
                    textBlock.Text = _newData.Emoji;
                }
            }
        }

        private static void ApplyRotation(Grid emojiGrid, double angle)
        {
            var width = double.IsNaN(emojiGrid.Width) ? emojiGrid.ActualWidth : emojiGrid.Width;
            var height = double.IsNaN(emojiGrid.Height) ? emojiGrid.ActualHeight : emojiGrid.Height;

            if (angle == 0)
            {
                emojiGrid.RenderTransform = null;
            }
            else
            {
                emojiGrid.RenderTransform = new RotateTransform
                {
                    Angle = angle,
                    CenterX = width / 2,
                    CenterY = height / 2
                };
            }
        }
    }

    /// <summary>
    /// Manages undo/redo history for annotations
    /// </summary>
    public class AnnotationHistoryManager
    {
        private readonly Stack<IHistoryAction> _undoStack;
        private readonly Stack<IHistoryAction> _redoStack;
        private readonly Canvas _canvas;
        private readonly int _maxHistorySize;

        /// <summary>
        /// Event raised when the history state changes
        /// </summary>
        public event EventHandler? HistoryChanged;

        /// <summary>
        /// Gets whether there are actions to undo
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether there are actions to redo
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets the number of actions in the undo stack
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Gets the number of actions in the redo stack
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Creates a new AnnotationHistoryManager
        /// </summary>
        /// <param name="canvas">The canvas containing annotations</param>
        /// <param name="maxHistorySize">Maximum number of actions to keep in history</param>
        public AnnotationHistoryManager(Canvas canvas, int maxHistorySize = 100)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _maxHistorySize = maxHistorySize;
            _undoStack = new Stack<IHistoryAction>();
            _redoStack = new Stack<IHistoryAction>();
        }

        /// <summary>
        /// Records an action in the history
        /// </summary>
        /// <param name="action">The action to record</param>
        public void RecordAction(IHistoryAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear(); // Clear redo stack when new action is recorded

            // Trim history if exceeds max size
            if (_undoStack.Count > _maxHistorySize)
            {
                TrimHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Records adding a path to the canvas
        /// </summary>
        public void RecordPathAdded(Path path)
        {
            RecordAction(new AddPathAction(_canvas, path));
        }

        /// <summary>
        /// Records adding a generic UIElement (like text) to the canvas
        /// </summary>
        public void RecordElementAdded(UIElement element)
        {
            RecordAction(new AddElementAction(_canvas, element));
        }

        /// <summary>
        /// Records removing a path from the canvas
        /// </summary>
        public void RecordPathRemoved(Path path)
        {
            RecordAction(new RemovePathAction(_canvas, path));
        }

        /// <summary>
        /// Records removing a generic UIElement from the canvas
        /// </summary>
        public void RecordElementRemoved(UIElement element)
        {
            RecordAction(new RemoveElementAction(_canvas, element));
        }

        /// <summary>
        /// Records moving a path
        /// </summary>
        public void RecordPathMoved(Path path, Models.ShapeData? originalData, Models.ShapeData? newData)
        {
            RecordAction(new MovePathAction(path, originalData, newData));
        }

        /// <summary>
        /// Records resizing a path
        /// </summary>
        public void RecordPathResized(Path path, Models.ShapeData? originalData, Models.ShapeData? newData)
        {
            RecordAction(new ResizePathAction(path, originalData, newData));
        }

        /// <summary>
        /// Records modifying a text element (move, resize, or style change)
        /// </summary>
        public void RecordTextModified(Grid textElement, Models.TextData originalData, Models.TextData newData)
        {
            RecordAction(new ModifyTextAction(textElement, originalData, newData));
        }

        /// <summary>
        /// Records modifying an emoji element (move, resize)
        /// </summary>
        public void RecordEmojiModified(Grid emojiElement, Models.EmojiData originalData, Models.EmojiData newData)
        {
            RecordAction(new ModifyEmojiAction(emojiElement, originalData, newData));
        }

        /// <summary>
        /// Undoes the last action
        /// </summary>
        /// <returns>True if an action was undone</returns>
        public bool Undo()
        {
            if (!CanUndo)
                return false;

            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);

            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Redoes the last undone action
        /// </summary>
        /// <returns>True if an action was redone</returns>
        public bool Redo()
        {
            if (!CanRedo)
                return false;

            var action = _redoStack.Pop();
            action.Redo();
            _undoStack.Push(action);

            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Clears all history
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the description of the next action to undo
        /// </summary>
        public string? GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        /// <summary>
        /// Gets the description of the next action to redo
        /// </summary>
        public string? GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }

        /// <summary>
        /// Trims the history to the maximum size
        /// </summary>
        private void TrimHistory()
        {
            // Convert to array, trim, and rebuild stack
            var actions = _undoStack.ToArray();
            _undoStack.Clear();
            
            // Push back in reverse order, skipping oldest actions
            for (int i = Math.Min(_maxHistorySize - 1, actions.Length - 1); i >= 0; i--)
            {
                _undoStack.Push(actions[i]);
            }
        }
    }
}
