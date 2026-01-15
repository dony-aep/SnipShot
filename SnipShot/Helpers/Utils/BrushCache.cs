using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SnipShot.Helpers.Utils
{
    /// <summary>
    /// Cache de SolidColorBrush para evitar crear objetos repetidos.
    /// Reduce la presión sobre el Garbage Collector al reutilizar brushes
    /// para colores que ya han sido utilizados previamente.
    /// </summary>
    public static class BrushCache
    {
        /// <summary>
        /// Diccionario que almacena los brushes por su color (como uint para búsqueda rápida).
        /// </summary>
        private static readonly Dictionary<uint, SolidColorBrush> _cache = new();

        /// <summary>
        /// Objeto de sincronización para acceso thread-safe.
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        /// Obtiene un SolidColorBrush para el color especificado.
        /// Si el brush ya existe en el cache, lo reutiliza.
        /// Si no existe, crea uno nuevo y lo almacena.
        /// </summary>
        /// <param name="color">El color del brush.</param>
        /// <returns>Un SolidColorBrush para el color especificado.</returns>
        public static SolidColorBrush GetBrush(Color color)
        {
            uint key = ColorToKey(color);

            lock (_lock)
            {
                if (!_cache.TryGetValue(key, out var brush))
                {
                    brush = new SolidColorBrush(color);
                    _cache[key] = brush;
                }
                return brush;
            }
        }

        /// <summary>
        /// Obtiene un SolidColorBrush transparente.
        /// </summary>
        public static SolidColorBrush Transparent => GetBrush(Colors.Transparent);

        /// <summary>
        /// Limpia el cache de brushes.
        /// Útil para liberar memoria si hay demasiados colores almacenados.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Obtiene el número de brushes en el cache.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// Convierte un Color a una clave uint para el diccionario.
        /// </summary>
        private static uint ColorToKey(Color color)
        {
            return ((uint)color.A << 24) | ((uint)color.R << 16) | 
                   ((uint)color.G << 8) | color.B;
        }
    }
}
