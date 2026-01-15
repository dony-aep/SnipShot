using Microsoft.UI.Xaml;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Utilidad para manejar temas de la aplicación.
    /// Proporciona conversiones entre tags de tema, nombres de visualización y ElementTheme.
    /// </summary>
    public static class ThemeHelper
    {
        // Tags de tema internos
        public const string LightTag = "Light";
        public const string DarkTag = "Dark";
        public const string DefaultTag = "Default";

        // Nombres de visualización
        public const string LightDisplayName = "Claro";
        public const string DarkDisplayName = "Oscuro";
        public const string DefaultDisplayName = "Sistema";

        /// <summary>
        /// Convierte un tag de tema a su nombre de visualización.
        /// </summary>
        /// <param name="themeTag">Tag del tema ("Light", "Dark", "Default").</param>
        /// <returns>Nombre de visualización ("Claro", "Oscuro", "Sistema").</returns>
        public static string ThemeTagToDisplayName(string themeTag)
        {
            return themeTag switch
            {
                LightTag => LightDisplayName,
                DarkTag => DarkDisplayName,
                DefaultTag => DefaultDisplayName,
                _ => DefaultDisplayName
            };
        }

        /// <summary>
        /// Convierte un nombre de visualización a su tag de tema.
        /// </summary>
        /// <param name="displayName">Nombre de visualización ("Claro", "Oscuro", "Sistema").</param>
        /// <returns>Tag del tema ("Light", "Dark", "Default").</returns>
        public static string DisplayNameToThemeTag(string displayName)
        {
            return displayName switch
            {
                LightDisplayName => LightTag,
                DarkDisplayName => DarkTag,
                DefaultDisplayName => DefaultTag,
                _ => DefaultTag
            };
        }

        /// <summary>
        /// Convierte un tag de tema a ElementTheme.
        /// </summary>
        /// <param name="themeTag">Tag del tema ("Light", "Dark", "Default").</param>
        /// <returns>El ElementTheme correspondiente.</returns>
        public static ElementTheme ThemeTagToElementTheme(string themeTag)
        {
            return themeTag switch
            {
                LightTag => ElementTheme.Light,
                DarkTag => ElementTheme.Dark,
                DefaultTag => ElementTheme.Default,
                _ => ElementTheme.Default
            };
        }

        /// <summary>
        /// Convierte un ElementTheme a tag de tema.
        /// </summary>
        /// <param name="elementTheme">El ElementTheme a convertir.</param>
        /// <returns>Tag del tema correspondiente.</returns>
        public static string ElementThemeToThemeTag(ElementTheme elementTheme)
        {
            return elementTheme switch
            {
                ElementTheme.Light => LightTag,
                ElementTheme.Dark => DarkTag,
                ElementTheme.Default => DefaultTag,
                _ => DefaultTag
            };
        }

        /// <summary>
        /// Aplica un tema a un FrameworkElement.
        /// </summary>
        /// <param name="element">El elemento al que aplicar el tema.</param>
        /// <param name="themeTag">Tag del tema a aplicar.</param>
        public static void ApplyTheme(FrameworkElement element, string themeTag)
        {
            if (element == null)
            {
                return;
            }

            element.RequestedTheme = ThemeTagToElementTheme(themeTag);
        }

        /// <summary>
        /// Valida si un tag de tema es válido.
        /// </summary>
        /// <param name="themeTag">Tag del tema a validar.</param>
        /// <returns>True si es válido, False en caso contrario.</returns>
        public static bool IsValidThemeTag(string themeTag)
        {
            return themeTag == LightTag || themeTag == DarkTag || themeTag == DefaultTag;
        }

        /// <summary>
        /// Obtiene el tag de tema actual de un FrameworkElement.
        /// </summary>
        /// <param name="element">El elemento del que obtener el tema.</param>
        /// <returns>Tag del tema actual.</returns>
        public static string GetCurrentThemeTag(FrameworkElement element)
        {
            if (element == null)
            {
                return DefaultTag;
            }

            return ElementThemeToThemeTag(element.RequestedTheme);
        }
    }
}
