using System;
using Windows.UI;
using Microsoft.UI.Xaml.Media;

namespace SnipShot.Helpers.Utils
{
    /// <summary>
    /// Utilidades para conversión y manipulación de colores.
    /// </summary>
    public static class ColorConverter
    {
        /// <summary>
        /// Convierte una cadena hexadecimal a un Color.
        /// Lanza excepción si el formato es inválido.
        /// </summary>
        /// <param name="hex">Cadena hex en formato #AARRGGBB o #RRGGBB</param>
        /// <returns>Color correspondiente</returns>
        /// <exception cref="ArgumentException">Si el formato es inválido</exception>
        public static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                throw new ArgumentException("El valor hex no puede ser nulo o vacío.", nameof(hex));
            }

            var clean = hex.Trim();

            // Remover el # inicial si existe
            if (clean.StartsWith("#"))
            {
                clean = clean.Substring(1);
            }

            // Si es formato RGB (6 caracteres), agregar FF para alpha
            if (clean.Length == 6)
            {
                clean = "FF" + clean;
            }

            // Validar longitud
            if (clean.Length != 8)
            {
                throw new ArgumentException($"Formato hex inválido: {hex}. Se esperaba #AARRGGBB o #RRGGBB", nameof(hex));
            }

            try
            {
                byte a = Convert.ToByte(clean.Substring(0, 2), 16);
                byte r = Convert.ToByte(clean.Substring(2, 2), 16);
                byte g = Convert.ToByte(clean.Substring(4, 2), 16);
                byte b = Convert.ToByte(clean.Substring(6, 2), 16);

                return Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Error al parsear el color hex: {hex}", nameof(hex), ex);
            }
        }

        /// <summary>
        /// Intenta convertir una cadena hexadecimal a un Color.
        /// Retorna false si el formato es inválido.
        /// </summary>
        /// <param name="hex">Cadena hex en formato #AARRGGBB o #RRGGBB</param>
        /// <param name="color">Color resultante si la conversión es exitosa</param>
        /// <returns>True si la conversión fue exitosa, false en caso contrario</returns>
        public static bool TryParseHexColor(string hex, out Color color)
        {
            color = default;

            try
            {
                color = ParseHexColor(hex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Convierte un Color a su representación hexadecimal.
        /// </summary>
        /// <param name="color">Color a convertir</param>
        /// <param name="includeAlpha">Si se debe incluir el canal alpha (por defecto true)</param>
        /// <returns>Cadena hex en formato #AARRGGBB o #RRGGBB</returns>
        public static string ColorToHex(Color color, bool includeAlpha = true)
        {
            if (includeAlpha)
            {
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            else
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }

        /// <summary>
        /// Crea un SolidColorBrush a partir de un Color.
        /// Usa BrushCache cuando la opacidad es 1.0 para mejor rendimiento.
        /// </summary>
        /// <param name="color">Color del brush</param>
        /// <param name="opacity">Opacidad del brush (0.0 a 1.0)</param>
        /// <returns>SolidColorBrush con el color y opacidad especificados</returns>
        public static SolidColorBrush CreateBrush(Color color, double opacity = 1.0)
        {
            // Si la opacidad es 1.0, usar el cache para mejor rendimiento
            if (opacity >= 1.0)
            {
                return BrushCache.GetBrush(color);
            }
            
            // Para opacidades diferentes, crear un nuevo brush
            return new SolidColorBrush(color) { Opacity = opacity };
        }

        /// <summary>
        /// Crea un SolidColorBrush a partir de una cadena hexadecimal.
        /// </summary>
        /// <param name="hex">Cadena hex en formato #AARRGGBB o #RRGGBB</param>
        /// <param name="opacity">Opacidad del brush (0.0 a 1.0)</param>
        /// <returns>SolidColorBrush con el color y opacidad especificados</returns>
        /// <exception cref="ArgumentException">Si el formato hex es inválido</exception>
        public static SolidColorBrush CreateBrushFromHex(string hex, double opacity = 1.0)
        {
            var color = ParseHexColor(hex);
            return CreateBrush(color, opacity);
        }

        /// <summary>
        /// Intenta crear un SolidColorBrush a partir de una cadena hexadecimal.
        /// Retorna null si el formato es inválido.
        /// </summary>
        /// <param name="hex">Cadena hex en formato #AARRGGBB o #RRGGBB</param>
        /// <param name="opacity">Opacidad del brush (0.0 a 1.0)</param>
        /// <returns>SolidColorBrush o null si la conversión falla</returns>
        public static SolidColorBrush? TryCreateBrushFromHex(string hex, double opacity = 1.0)
        {
            if (TryParseHexColor(hex, out var color))
            {
                return CreateBrush(color, opacity);
            }
            return null;
        }
    }
}
