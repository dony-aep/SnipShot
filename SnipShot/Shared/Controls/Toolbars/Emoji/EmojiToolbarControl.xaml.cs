using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace SnipShot.Shared.Controls.Toolbars.Emoji
{
    /// <summary>
    /// Control de toolbar para seleccionar emojis.
    /// Muestra categorías y un grid de emojis seleccionables.
    /// </summary>
    public sealed partial class EmojiToolbarControl : UserControl
    {
        private string _currentCategory = "smileys";
        private readonly Dictionary<string, Button> _categoryButtons;

        /// <summary>
        /// Evento cuando se selecciona un emoji
        /// </summary>
        public event EventHandler<string>? EmojiSelected;

        /// <summary>
        /// Obtiene la categoría actual
        /// </summary>
        public string CurrentCategory => _currentCategory;

        public EmojiToolbarControl()
        {
            this.InitializeComponent();

            // Mapear botones de categoría
            _categoryButtons = new Dictionary<string, Button>
            {
                { "smileys", SmileysCategory },
                { "gestures", GesturesCategory },
                { "symbols", SymbolsCategory },
                { "celebration", CelebrationCategory },
                { "nature", NatureCategory },
                { "arrows", ArrowsCategory }
            };

            // Cargar categoría inicial
            LoadCategory("smileys");
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string category)
            {
                LoadCategory(category);
            }
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string emoji)
            {
                EmojiSelected?.Invoke(this, emoji);
            }
        }

        /// <summary>
        /// Carga una categoría de emojis
        /// </summary>
        /// <param name="category">Nombre de la categoría</param>
        public void LoadCategory(string category)
        {
            if (!EmojiData.Categories.TryGetValue(category, out var emojis))
            {
                return;
            }

            _currentCategory = category;

            // Actualizar estados visuales de botones de categoría
            UpdateCategoryButtonStates();

            // Cargar emojis en el grid
            EmojiGrid.ItemsSource = emojis;
        }

        private void UpdateCategoryButtonStates()
        {
            var selectedBrush = BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
            var transparentBrush = BrushCache.Transparent;

            foreach (var kvp in _categoryButtons)
            {
                kvp.Value.Background = kvp.Key == _currentCategory ? selectedBrush : transparentBrush;
            }
        }

        /// <summary>
        /// Resetea la selección al estado inicial
        /// </summary>
        public void Reset()
        {
            LoadCategory("smileys");
            EmojiScrollViewer.ChangeView(null, 0, null);
        }
    }
}
