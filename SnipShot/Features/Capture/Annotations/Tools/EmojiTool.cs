using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SnipShot.Features.Capture.Annotations.Models;
using System;
using Windows.Foundation;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para crear y gestionar emojis como anotaciones en el canvas.
    /// Los emojis se crean como TextBlocks envueltos en un Grid para permitir manipulación.
    /// </summary>
    public class EmojiTool
    {
        #region Fields

        private bool _isActive;
        private Grid? _currentEmojiElement;
        private EmojiData _settings;

        #endregion

        #region Properties

        /// <summary>
        /// Nombre identificador de la herramienta
        /// </summary>
        public string ToolName => "Emoji";

        /// <summary>
        /// Indica si la herramienta está activa
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        /// <summary>
        /// Elemento emoji actual siendo creado/editado
        /// </summary>
        public Grid? CurrentEmojiElement => _currentEmojiElement;

        /// <summary>
        /// Configuración actual del emoji (tamaño, etc.)
        /// </summary>
        public EmojiData Settings
        {
            get => _settings;
            set => _settings = value ?? EmojiData.Default;
        }

        #endregion

        #region Events

        /// <summary>
        /// Evento cuando un emoji es creado y añadido al canvas
        /// </summary>
        public event EventHandler<Grid>? EmojiCreated;

        #endregion

        #region Constructor

        public EmojiTool()
        {
            _settings = EmojiData.Default;
        }

        public EmojiTool(EmojiData settings)
        {
            _settings = settings ?? EmojiData.Default;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Activa la herramienta de emojis
        /// </summary>
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Desactiva la herramienta de emojis
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _currentEmojiElement = null;
        }

        /// <summary>
        /// Crea un elemento emoji en la posición especificada
        /// </summary>
        /// <param name="position">Posición donde colocar el emoji</param>
        /// <param name="emoji">El emoji a mostrar</param>
        /// <returns>El Grid contenedor del emoji</returns>
        public Grid CreateEmoji(Point position, string emoji)
        {
            // Crear datos del emoji
            var emojiData = new EmojiData
            {
                Emoji = emoji,
                FontSize = _settings.FontSize,
                Position = position,
                Width = _settings.Width,
                Height = _settings.Height,
                RotationAngle = 0
            };

            // Crear el elemento visual
            var grid = CreateEmojiGrid(emojiData);

            // Posicionar en el canvas
            Canvas.SetLeft(grid, position.X - (emojiData.Width / 2));
            Canvas.SetTop(grid, position.Y - (emojiData.Height / 2));

            _currentEmojiElement = grid;

            EmojiCreated?.Invoke(this, grid);

            return grid;
        }

        /// <summary>
        /// Crea un Grid con el emoji configurado
        /// </summary>
        /// <param name="emojiData">Datos de configuración del emoji</param>
        /// <returns>Grid contenedor con el emoji</returns>
        public Grid CreateEmojiGrid(EmojiData emojiData)
        {
            var grid = new Grid
            {
                Width = emojiData.Width,
                Height = emojiData.Height,
                Tag = emojiData,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };

            // Aplicar rotación si existe
            if (emojiData.RotationAngle != 0)
            {
                grid.RenderTransform = new RotateTransform
                {
                    Angle = emojiData.RotationAngle,
                    CenterX = emojiData.Width / 2,
                    CenterY = emojiData.Height / 2
                };
            }

            // Crear el TextBlock con el emoji
            var textBlock = new TextBlock
            {
                Text = emojiData.Emoji,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = emojiData.FontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false // Permitir que los clicks pasen al Grid padre
            };

            grid.Children.Add(textBlock);

            return grid;
        }

        /// <summary>
        /// Actualiza el tamaño de un emoji existente
        /// </summary>
        /// <param name="emojiGrid">Grid contenedor del emoji</param>
        /// <param name="newWidth">Nuevo ancho</param>
        /// <param name="newHeight">Nuevo alto</param>
        public void UpdateEmojiSize(Grid emojiGrid, double newWidth, double newHeight)
        {
            if (emojiGrid == null) return;

            emojiGrid.Width = newWidth;
            emojiGrid.Height = newHeight;

            // Actualizar el tamaño de fuente proporcionalmente
            var textBlock = FindTextBlock(emojiGrid);
            if (textBlock != null)
            {
                // El tamaño de fuente se basa en el menor de ancho/alto
                var minSize = Math.Min(newWidth, newHeight);
                textBlock.FontSize = minSize * 0.8; // 80% del tamaño del contenedor
            }

            // Actualizar los datos en el Tag
            if (emojiGrid.Tag is EmojiData data)
            {
                data.Width = newWidth;
                data.Height = newHeight;
                data.FontSize = textBlock?.FontSize ?? data.FontSize;
            }

            // Actualizar centro de rotación si hay transform
            if (emojiGrid.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.CenterX = newWidth / 2;
                rotateTransform.CenterY = newHeight / 2;
            }
        }

        /// <summary>
        /// Actualiza el emoji mostrado en un elemento existente
        /// </summary>
        /// <param name="emojiGrid">Grid contenedor</param>
        /// <param name="newEmoji">Nuevo emoji a mostrar</param>
        public void UpdateEmoji(Grid emojiGrid, string newEmoji)
        {
            if (emojiGrid == null) return;

            var textBlock = FindTextBlock(emojiGrid);
            if (textBlock != null)
            {
                textBlock.Text = newEmoji;
            }

            if (emojiGrid.Tag is EmojiData data)
            {
                data.Emoji = newEmoji;
            }
        }

        /// <summary>
        /// Obtiene el emoji de un elemento Grid
        /// </summary>
        public string? GetEmoji(Grid emojiGrid)
        {
            return emojiGrid?.Tag is EmojiData data ? data.Emoji : null;
        }

        /// <summary>
        /// Obtiene los datos del emoji de un Grid
        /// </summary>
        public EmojiData? GetEmojiData(Grid emojiGrid)
        {
            return emojiGrid?.Tag as EmojiData;
        }

        #endregion

        #region Private Methods

        private TextBlock? FindTextBlock(Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is TextBlock textBlock)
                {
                    return textBlock;
                }
            }
            return null;
        }

        #endregion
    }
}
