using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;

namespace SnipShot.Features.Capture.Annotations.Managers
{
    /// <summary>
    /// Representa una acción que se puede deshacer y rehacer
    /// </summary>
    public interface IHistoryAction
    {
        /// <summary>
        /// Deshace esta acción
        /// </summary>
        void Undo();

        /// <summary>
        /// Rehace esta acción
        /// </summary>
        void Redo();

        /// <summary>
        /// Obtiene una descripción de esta acción
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// Reinserción de anotaciones en el canvas conservando su orden Z.
    /// </summary>
    /// <remarks>
    /// El orden de <see cref="Canvas.Children"/> es el orden de pintado: quien está antes
    /// queda debajo. Reinsertar con Add() manda el elemento al final, así que deshacer un
    /// borrado lo devolvería encima de todo en vez de a su sitio. Por eso se guarda el
    /// índice que ocupaba y se reinserta ahí.
    /// </remarks>
    internal static class CanvasOrder
    {
        /// <summary>
        /// Quita el elemento del canvas y devuelve el índice que ocupaba, o -1 si no estaba.
        /// </summary>
        public static int RemoveTracking(Canvas canvas, UIElement element)
        {
            int index = canvas.Children.IndexOf(element);
            if (index >= 0)
            {
                canvas.Children.RemoveAt(index);
            }

            return index;
        }

        /// <summary>
        /// Reinserta el elemento en su índice original. Si ese índice ya no es válido
        /// —hay menos hijos que antes— se añade al final, que es lo mejor disponible.
        /// </summary>
        public static void Restore(Canvas canvas, UIElement element, int index)
        {
            if (canvas.Children.Contains(element))
            {
                return;
            }

            if (index >= 0 && index <= canvas.Children.Count)
            {
                canvas.Children.Insert(index, element);
            }
            else
            {
                canvas.Children.Add(element);
            }
        }
    }

    /// <summary>
    /// Acción de historial para añadir un path al canvas
    /// </summary>
    public class AddPathAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly Path _path;
        private int _index = -1;

        public string Description => "Add annotation";

        public AddPathAction(Canvas canvas, Path path)
        {
            _canvas = canvas;
            _path = path;
        }

        public void Undo()
        {
            _index = CanvasOrder.RemoveTracking(_canvas, _path);
        }

        public void Redo()
        {
            CanvasOrder.Restore(_canvas, _path, _index);
        }
    }

    /// <summary>
    /// Acción de historial para quitar un path del canvas
    /// </summary>
    public class RemovePathAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly Path _path;
        private int _index;

        public string Description => "Remove annotation";

        /// <param name="index">
        /// Índice que ocupaba el path antes de quitarlo. Los llamadores registran la acción
        /// después de haberlo quitado, así que hay que capturarlo antes y pasarlo aquí.
        /// </param>
        public RemovePathAction(Canvas canvas, Path path, int index = -1)
        {
            _canvas = canvas;
            _path = path;
            _index = index;
        }

        public void Undo()
        {
            CanvasOrder.Restore(_canvas, _path, _index);
        }

        public void Redo()
        {
            _index = CanvasOrder.RemoveTracking(_canvas, _path);
        }
    }

    /// <summary>
    /// Acción de historial para añadir un UIElement genérico (por ejemplo, texto) al canvas
    /// </summary>
    public class AddElementAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;
        private int _index = -1;

        public string Description => "Add element";

        public AddElementAction(Canvas canvas, UIElement element)
        {
            _canvas = canvas;
            _element = element;
        }

        public void Undo()
        {
            _index = CanvasOrder.RemoveTracking(_canvas, _element);
        }

        public void Redo()
        {
            CanvasOrder.Restore(_canvas, _element, _index);
        }
    }

    /// <summary>
    /// Acción de historial para quitar un UIElement genérico del canvas
    /// </summary>
    public class RemoveElementAction : IHistoryAction
    {
        private readonly Canvas _canvas;
        private readonly UIElement _element;
        private int _index;

        public string Description => "Remove element";

        /// <param name="index">Índice que ocupaba el elemento antes de quitarlo.</param>
        public RemoveElementAction(Canvas canvas, UIElement element, int index = -1)
        {
            _canvas = canvas;
            _element = element;
            _index = index;
        }

        public void Undo()
        {
            CanvasOrder.Restore(_canvas, _element, _index);
        }

        public void Redo()
        {
            _index = CanvasOrder.RemoveTracking(_canvas, _element);
        }
    }

    /// <summary>
    /// Acción de historial para mover un path
    /// </summary>
    public class MovePathAction : IHistoryAction
    {
        private readonly Path _path;
        private readonly Models.ShapeData? _originalData;
        private readonly Models.ShapeData? _newData;
        private readonly Action<Path, Models.ShapeData>? _updateGeometry;

        public string Description => "Move annotation";

        /// <param name="updateGeometry">
        /// Redibuja el path a partir de sus datos. Sin esto, deshacer solo cambia el
        /// ShapeData y la forma se queda pintada donde estaba.
        /// </param>
        public MovePathAction(
            Path path,
            Models.ShapeData? originalData,
            Models.ShapeData? newData,
            Action<Path, Models.ShapeData>? updateGeometry = null)
        {
            _path = path;
            _originalData = originalData != null ? originalData.Clone() : null;
            _newData = newData != null ? newData.Clone() : null;
            _updateGeometry = updateGeometry;
        }

        public void Undo() => Apply(_originalData);

        public void Redo() => Apply(_newData);

        private void Apply(Models.ShapeData? data)
        {
            if (data == null || _path.Tag is not Models.ShapeData currentData)
            {
                return;
            }

            currentData.StartPoint = data.StartPoint;
            currentData.EndPoint = data.EndPoint;
            _updateGeometry?.Invoke(_path, currentData);
        }
    }

    /// <summary>
    /// Acción de historial para redimensionar un path
    /// </summary>
    public class ResizePathAction : IHistoryAction
    {
        private readonly Path _path;
        private readonly Models.ShapeData? _originalData;
        private readonly Models.ShapeData? _newData;
        private readonly Action<Path, Models.ShapeData>? _updateGeometry;

        public string Description => "Resize annotation";

        /// <param name="updateGeometry">Redibuja el path a partir de sus datos.</param>
        public ResizePathAction(
            Path path,
            Models.ShapeData? originalData,
            Models.ShapeData? newData,
            Action<Path, Models.ShapeData>? updateGeometry = null)
        {
            _path = path;
            _originalData = originalData != null ? originalData.Clone() : null;
            _newData = newData != null ? newData.Clone() : null;
            _updateGeometry = updateGeometry;
        }

        public void Undo() => Apply(_originalData);

        public void Redo() => Apply(_newData);

        private void Apply(Models.ShapeData? data)
        {
            if (data == null || _path.Tag is not Models.ShapeData currentData)
            {
                return;
            }

            currentData.StartPoint = data.StartPoint;
            currentData.EndPoint = data.EndPoint;
            _updateGeometry?.Invoke(_path, currentData);
        }
    }

    /// <summary>
    /// Acción de historial para modificar un texto (mover, redimensionar o cambiar estilo)
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

                Canvas.SetLeft(_textElement, _newData.Position.X);
                Canvas.SetTop(_textElement, _newData.Position.Y);
                _textElement.Width = _newData.Width;
                _textElement.Height = _newData.Height;
            }
        }
    }

    /// <summary>
    /// Acción de historial para modificar un emoji (mover o redimensionar)
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
                currentData.Emoji = _originalData.Emoji;
                currentData.FontSize = _originalData.FontSize;
                currentData.Position = _originalData.Position;
                currentData.Width = _originalData.Width;
                currentData.Height = _originalData.Height;
                currentData.RotationAngle = _originalData.RotationAngle;

                Canvas.SetLeft(_emojiElement, _originalData.Position.X);
                Canvas.SetTop(_emojiElement, _originalData.Position.Y);
                _emojiElement.Width = _originalData.Width;
                _emojiElement.Height = _originalData.Height;

                ApplyRotation(_emojiElement, _originalData.RotationAngle);

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
                currentData.Emoji = _newData.Emoji;
                currentData.FontSize = _newData.FontSize;
                currentData.Position = _newData.Position;
                currentData.Width = _newData.Width;
                currentData.Height = _newData.Height;
                currentData.RotationAngle = _newData.RotationAngle;

                Canvas.SetLeft(_emojiElement, _newData.Position.X);
                Canvas.SetTop(_emojiElement, _newData.Position.Y);
                _emojiElement.Width = _newData.Width;
                _emojiElement.Height = _newData.Height;

                ApplyRotation(_emojiElement, _newData.RotationAngle);

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
    /// Gestiona el historial de deshacer y rehacer de las anotaciones
    /// </summary>
    public class AnnotationHistoryManager
    {
        private readonly Stack<IHistoryAction> _undoStack;
        private readonly Stack<IHistoryAction> _redoStack;
        private readonly Canvas _canvas;
        private readonly int _maxHistorySize;

        /// <summary>
        /// Evento que se dispara cuando cambia el estado del historial
        /// </summary>
        public event EventHandler? HistoryChanged;

        /// <summary>
        /// Obtiene si hay acciones que deshacer
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Obtiene si hay acciones que rehacer
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Obtiene el número de acciones en la pila de deshacer
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Obtiene el número de acciones en la pila de rehacer
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Crea un nuevo AnnotationHistoryManager
        /// </summary>
        /// <param name="canvas">Canvas que contiene las anotaciones</param>
        /// <param name="maxHistorySize">Número máximo de acciones que se guardan en el historial</param>
        public AnnotationHistoryManager(Canvas canvas, int maxHistorySize = 100)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _maxHistorySize = maxHistorySize;
            _undoStack = new Stack<IHistoryAction>();
            _redoStack = new Stack<IHistoryAction>();
        }

        /// <summary>
        /// Registra una acción en el historial
        /// </summary>
        /// <param name="action">Acción que se registra</param>
        public void RecordAction(IHistoryAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear(); // Registrar algo nuevo invalida la rama que hubiera para rehacer

            if (_undoStack.Count > _maxHistorySize)
            {
                TrimHistory();
            }

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Registra que se añadió un path al canvas
        /// </summary>
        public void RecordPathAdded(Path path)
        {
            RecordAction(new AddPathAction(_canvas, path));
        }

        /// <summary>
        /// Registra que se añadió un UIElement genérico (por ejemplo, texto) al canvas
        /// </summary>
        public void RecordElementAdded(UIElement element)
        {
            RecordAction(new AddElementAction(_canvas, element));
        }

        /// <summary>
        /// Registra que se quitó un path del canvas
        /// </summary>
        /// <param name="index">
        /// Índice que ocupaba en el canvas antes de quitarlo, para poder devolverlo a su
        /// orden Z al deshacer. Omitirlo hace que reaparezca encima del resto.
        /// </param>
        public void RecordPathRemoved(Path path, int index = -1)
        {
            RecordAction(new RemovePathAction(_canvas, path, index));
        }

        /// <summary>
        /// Registra que se quitó un UIElement genérico del canvas
        /// </summary>
        /// <param name="index">Índice que ocupaba en el canvas antes de quitarlo.</param>
        public void RecordElementRemoved(UIElement element, int index = -1)
        {
            RecordAction(new RemoveElementAction(_canvas, element, index));
        }

        /// <summary>
        /// Registra que se movió un path
        /// </summary>
        /// <param name="updateGeometry">Redibuja el path tras restaurar sus datos.</param>
        public void RecordPathMoved(
            Path path,
            Models.ShapeData? originalData,
            Models.ShapeData? newData,
            Action<Path, Models.ShapeData>? updateGeometry = null)
        {
            RecordAction(new MovePathAction(path, originalData, newData, updateGeometry));
        }

        /// <summary>
        /// Registra que se redimensionó un path
        /// </summary>
        /// <param name="updateGeometry">Redibuja el path tras restaurar sus datos.</param>
        public void RecordPathResized(
            Path path,
            Models.ShapeData? originalData,
            Models.ShapeData? newData,
            Action<Path, Models.ShapeData>? updateGeometry = null)
        {
            RecordAction(new ResizePathAction(path, originalData, newData, updateGeometry));
        }

        /// <summary>
        /// Registra que se modificó un texto (mover, redimensionar o cambiar estilo)
        /// </summary>
        public void RecordTextModified(Grid textElement, Models.TextData originalData, Models.TextData newData)
        {
            RecordAction(new ModifyTextAction(textElement, originalData, newData));
        }

        /// <summary>
        /// Registra que se modificó un emoji (mover o redimensionar)
        /// </summary>
        public void RecordEmojiModified(Grid emojiElement, Models.EmojiData originalData, Models.EmojiData newData)
        {
            RecordAction(new ModifyEmojiAction(emojiElement, originalData, newData));
        }

        /// <summary>
        /// Deshace la última acción
        /// </summary>
        /// <returns>True si se deshizo una acción</returns>
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
        /// Rehace la última acción deshecha
        /// </summary>
        /// <returns>True si se rehízo una acción</returns>
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
        /// Vacía todo el historial
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Obtiene la descripción de la siguiente acción que se deshará
        /// </summary>
        public string? GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        /// <summary>
        /// Obtiene la descripción de la siguiente acción que se rehará
        /// </summary>
        public string? GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }

        /// <summary>
        /// Recorta el historial al tamaño máximo
        /// </summary>
        private void TrimHistory()
        {
            // Stack<T> no deja quitar por el fondo, así que hay que reconstruirla. ToArray()
            // devuelve desde la cima hacia abajo, por lo que recorrer el array desde el índice
            // máximo hacia atrás descarta las acciones más antiguas y conserva el orden.
            var actions = _undoStack.ToArray();
            _undoStack.Clear();

            for (int i = Math.Min(_maxHistorySize - 1, actions.Length - 1); i >= 0; i--)
            {
                _undoStack.Push(actions[i]);
            }
        }
    }
}
