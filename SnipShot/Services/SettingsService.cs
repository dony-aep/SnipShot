using System;
using Windows.Storage;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio centralizado para la gestión de configuraciones de la aplicación.
    /// Proporciona acceso type-safe a ApplicationData.Current.LocalSettings.
    /// </summary>
    public class SettingsService
    {
        private readonly ApplicationDataContainer _localSettings;

        // Claves de configuración
        private const string THEME_KEY = "AppTheme";
        private const string AUTO_SAVE_ENABLED_KEY = "AutoSaveOriginals";
        private const string AUTO_SAVE_FOLDER_PATH_KEY = "AutoSaveFolderPath";
        private const string BORDER_ENABLED_KEY = "BorderEnabled";
        private const string BORDER_COLOR_KEY = "BorderColor";
        private const string BORDER_THICKNESS_KEY = "BorderThickness";
        private const string HIDE_ON_CAPTURE_KEY = "HideOnCapture";
        private const string CONFIRM_DELETE_CAPTURE_KEY = "ConfirmDeleteCapture";
        private const string PRINT_SCREEN_HOTKEY_ENABLED_KEY = "PrintScreenHotkeyEnabled";
        private const string CTRL_SHIFT_S_HOTKEY_ENABLED_KEY = "CtrlShiftSHotkeyEnabled";
        private const string MINIMIZE_TO_TRAY_KEY = "MinimizeToTray";
        private const string START_WITH_WINDOWS_KEY = "StartWithWindows";

        // Valores predeterminados
        private const string DEFAULT_THEME = "Default";
        private const bool DEFAULT_AUTO_SAVE_ENABLED = false;
        private const bool DEFAULT_BORDER_ENABLED = false;
        private const string DEFAULT_BORDER_COLOR = "#FF000000";
        private const double DEFAULT_BORDER_THICKNESS = 1.0;
        private const bool DEFAULT_HIDE_ON_CAPTURE = true;
        private const bool DEFAULT_CONFIRM_DELETE_CAPTURE = true;
        private const bool DEFAULT_PRINT_SCREEN_HOTKEY_ENABLED = false;
        private const bool DEFAULT_CTRL_SHIFT_S_HOTKEY_ENABLED = true;
        private const bool DEFAULT_MINIMIZE_TO_TRAY = true;
        private const bool DEFAULT_START_WITH_WINDOWS = false;

        public SettingsService()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
        }

        #region Generic Methods

        /// <summary>
        /// Guarda un valor de configuración.
        /// </summary>
        /// <typeparam name="T">Tipo del valor a guardar.</typeparam>
        /// <param name="key">Clave de la configuración.</param>
        /// <param name="value">Valor a guardar.</param>
        public void SaveSetting<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("La clave no puede ser nula o vacía.", nameof(key));
            }

            _localSettings.Values[key] = value;
        }

        /// <summary>
        /// Obtiene un valor de configuración con un valor predeterminado.
        /// </summary>
        /// <typeparam name="T">Tipo del valor a obtener.</typeparam>
        /// <param name="key">Clave de la configuración.</param>
        /// <param name="defaultValue">Valor predeterminado si la clave no existe.</param>
        /// <returns>El valor guardado o el valor predeterminado.</returns>
        public T GetSetting<T>(string key, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            if (_localSettings.Values.TryGetValue(key, out object? value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Intenta obtener un valor de configuración.
        /// </summary>
        /// <typeparam name="T">Tipo del valor a obtener.</typeparam>
        /// <param name="key">Clave de la configuración.</param>
        /// <param name="value">Valor obtenido si existe.</param>
        /// <returns>True si se encontró y es del tipo correcto, False en caso contrario.</returns>
        public bool TryGetSetting<T>(string key, out T? value)
        {
            value = default;

            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_localSettings.Values.TryGetValue(key, out object? storedValue) && storedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Elimina una configuración.
        /// </summary>
        /// <param name="key">Clave de la configuración a eliminar.</param>
        /// <returns>True si se eliminó, False si no existía.</returns>
        public bool RemoveSetting(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _localSettings.Values.Remove(key);
        }

        /// <summary>
        /// Verifica si existe una configuración.
        /// </summary>
        /// <param name="key">Clave de la configuración.</param>
        /// <returns>True si existe, False en caso contrario.</returns>
        public bool ContainsSetting(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _localSettings.Values.ContainsKey(key);
        }

        #endregion

        #region Theme Settings

        /// <summary>
        /// Obtiene el tema actual de la aplicación.
        /// </summary>
        /// <returns>El tema guardado ("Light", "Dark", "Default").</returns>
        public string GetTheme()
        {
            return GetSetting(THEME_KEY, DEFAULT_THEME);
        }

        /// <summary>
        /// Guarda el tema de la aplicación.
        /// </summary>
        /// <param name="theme">Tema a guardar ("Light", "Dark", "Default").</param>
        public void SaveTheme(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                theme = DEFAULT_THEME;
            }

            SaveSetting(THEME_KEY, theme);
        }

        #endregion

        #region Auto Save Settings

        /// <summary>
        /// Obtiene si el guardado automático está habilitado.
        /// </summary>
        public bool GetAutoSaveEnabled()
        {
            return GetSetting(AUTO_SAVE_ENABLED_KEY, DEFAULT_AUTO_SAVE_ENABLED);
        }

        /// <summary>
        /// Guarda la preferencia de guardado automático.
        /// </summary>
        /// <param name="enabled">True para habilitar, False para deshabilitar.</param>
        public void SaveAutoSaveEnabled(bool enabled)
        {
            SaveSetting(AUTO_SAVE_ENABLED_KEY, enabled);
        }

        /// <summary>
        /// Obtiene la ruta de la carpeta de guardado automático.
        /// </summary>
        /// <returns>La ruta guardada o una cadena vacía si no existe.</returns>
        public string GetAutoSaveFolderPath()
        {
            return GetSetting(AUTO_SAVE_FOLDER_PATH_KEY, string.Empty);
        }

        /// <summary>
        /// Guarda la ruta de la carpeta de guardado automático.
        /// </summary>
        /// <param name="path">Ruta de la carpeta.</param>
        public void SaveAutoSaveFolderPath(string path)
        {
            SaveSetting(AUTO_SAVE_FOLDER_PATH_KEY, path ?? string.Empty);
        }

        #endregion

        #region Border Settings

        /// <summary>
        /// Obtiene la configuración completa de bordes.
        /// </summary>
        /// <returns>Tupla con (enabled, colorHex, thickness).</returns>
        public (bool enabled, string colorHex, double thickness) GetBorderSettings()
        {
            var enabled = GetSetting(BORDER_ENABLED_KEY, DEFAULT_BORDER_ENABLED);
            var colorHex = GetSetting(BORDER_COLOR_KEY, DEFAULT_BORDER_COLOR);
            var thickness = GetSetting(BORDER_THICKNESS_KEY, DEFAULT_BORDER_THICKNESS);

            return (enabled, colorHex, thickness);
        }

        /// <summary>
        /// Obtiene si el borde está habilitado.
        /// </summary>
        public bool GetBorderEnabled()
        {
            return GetSetting(BORDER_ENABLED_KEY, DEFAULT_BORDER_ENABLED);
        }

        /// <summary>
        /// Obtiene el color del borde en formato hexadecimal.
        /// </summary>
        public string GetBorderColor()
        {
            return GetSetting(BORDER_COLOR_KEY, DEFAULT_BORDER_COLOR);
        }

        /// <summary>
        /// Obtiene el grosor del borde.
        /// </summary>
        public double GetBorderThickness()
        {
            return GetSetting(BORDER_THICKNESS_KEY, DEFAULT_BORDER_THICKNESS);
        }

        /// <summary>
        /// Guarda la configuración completa de bordes.
        /// </summary>
        /// <param name="enabled">Si el borde está habilitado.</param>
        /// <param name="colorHex">Color en formato hexadecimal.</param>
        /// <param name="thickness">Grosor del borde.</param>
        public void SaveBorderSettings(bool enabled, string colorHex, double thickness)
        {
            SaveSetting(BORDER_ENABLED_KEY, enabled);
            SaveSetting(BORDER_COLOR_KEY, colorHex ?? DEFAULT_BORDER_COLOR);
            SaveSetting(BORDER_THICKNESS_KEY, thickness);
        }

        /// <summary>
        /// Guarda solo si el borde está habilitado.
        /// </summary>
        public void SaveBorderEnabled(bool enabled)
        {
            SaveSetting(BORDER_ENABLED_KEY, enabled);
        }

        /// <summary>
        /// Guarda solo el color del borde.
        /// </summary>
        public void SaveBorderColor(string colorHex)
        {
            SaveSetting(BORDER_COLOR_KEY, colorHex ?? DEFAULT_BORDER_COLOR);
        }

        /// <summary>
        /// Guarda solo el grosor del borde.
        /// </summary>
        public void SaveBorderThickness(double thickness)
        {
            SaveSetting(BORDER_THICKNESS_KEY, thickness);
        }

        #endregion

        #region Hide On Capture Settings

        /// <summary>
        /// Obtiene si la aplicación debe ocultarse al capturar.
        /// Por defecto es true (se oculta).
        /// </summary>
        public bool GetHideOnCapture()
        {
            return GetSetting(HIDE_ON_CAPTURE_KEY, DEFAULT_HIDE_ON_CAPTURE);
        }

        /// <summary>
        /// Guarda la preferencia de ocultar al capturar.
        /// </summary>
        /// <param name="hideOnCapture">True para ocultar, False para mantener visible.</param>
        public void SaveHideOnCapture(bool hideOnCapture)
        {
            SaveSetting(HIDE_ON_CAPTURE_KEY, hideOnCapture);
        }

        #endregion

        #region Confirm Delete Capture Settings

        /// <summary>
        /// Obtiene si se debe mostrar confirmaci¢n al eliminar una captura.
        /// </summary>
        public bool GetConfirmDeleteCapture()
        {
            return GetSetting(CONFIRM_DELETE_CAPTURE_KEY, DEFAULT_CONFIRM_DELETE_CAPTURE);
        }

        /// <summary>
        /// Guarda la preferencia de confirmaci¢n al eliminar una captura.
        /// </summary>
        public void SaveConfirmDeleteCapture(bool enabled)
        {
            SaveSetting(CONFIRM_DELETE_CAPTURE_KEY, enabled);
        }

        #endregion

        #region Hotkey Settings

        /// <summary>
        /// Obtiene si el hotkey Print Screen está habilitado.
        /// </summary>
        public bool GetPrintScreenHotkeyEnabled()
        {
            return GetSetting(PRINT_SCREEN_HOTKEY_ENABLED_KEY, DEFAULT_PRINT_SCREEN_HOTKEY_ENABLED);
        }

        /// <summary>
        /// Guarda la preferencia del hotkey Print Screen.
        /// </summary>
        /// <param name="enabled">True para habilitar, False para deshabilitar.</param>
        public void SavePrintScreenHotkeyEnabled(bool enabled)
        {
            SaveSetting(PRINT_SCREEN_HOTKEY_ENABLED_KEY, enabled);
        }

        /// <summary>
        /// Obtiene si el hotkey Ctrl+Shift+S está habilitado.
        /// </summary>
        public bool GetCtrlShiftSHotkeyEnabled()
        {
            return GetSetting(CTRL_SHIFT_S_HOTKEY_ENABLED_KEY, DEFAULT_CTRL_SHIFT_S_HOTKEY_ENABLED);
        }

        /// <summary>
        /// Guarda la preferencia del hotkey Ctrl+Shift+S.
        /// </summary>
        /// <param name="enabled">True para habilitar, False para deshabilitar.</param>
        public void SaveCtrlShiftSHotkeyEnabled(bool enabled)
        {
            SaveSetting(CTRL_SHIFT_S_HOTKEY_ENABLED_KEY, enabled);
        }

        #endregion

        #region System Tray Settings

        /// <summary>
        /// Obtiene si la opción de minimizar a la bandeja está habilitada.
        /// </summary>
        public bool GetMinimizeToTrayEnabled()
        {
            return GetSetting(MINIMIZE_TO_TRAY_KEY, DEFAULT_MINIMIZE_TO_TRAY);
        }

        /// <summary>
        /// Guarda la preferencia de minimizar a la bandeja.
        /// </summary>
        /// <param name="enabled">True para habilitar, False para deshabilitar.</param>
        public void SaveMinimizeToTrayEnabled(bool enabled)
        {
            SaveSetting(MINIMIZE_TO_TRAY_KEY, enabled);
        }

        /// <summary>
        /// Obtiene si la opción de iniciar con Windows está habilitada.
        /// </summary>
        public bool GetStartWithWindowsEnabled()
        {
            return GetSetting(START_WITH_WINDOWS_KEY, DEFAULT_START_WITH_WINDOWS);
        }

        /// <summary>
        /// Guarda la preferencia de iniciar con Windows.
        /// </summary>
        /// <param name="enabled">True para habilitar, False para deshabilitar.</param>
        public void SaveStartWithWindowsEnabled(bool enabled)
        {
            SaveSetting(START_WITH_WINDOWS_KEY, enabled);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Elimina todas las configuraciones guardadas.
        /// </summary>
        public void ClearAllSettings()
        {
            _localSettings.Values.Clear();
        }

        #endregion
    }
}
