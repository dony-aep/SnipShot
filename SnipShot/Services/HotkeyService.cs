using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.UI.Xaml;

namespace SnipShot.Services
{
    /// <summary>
    /// Tipos de hotkey soportados por la aplicación.
    /// </summary>
    public enum HotkeyType
    {
        /// <summary>
        /// Tecla Print Screen (VK_SNAPSHOT = 0x2C)
        /// </summary>
        PrintScreen,

        /// <summary>
        /// Combinación Ctrl + Shift + S
        /// </summary>
        CtrlShiftS
    }

    /// <summary>
    /// Servicio para gestionar hotkeys globales del sistema.
    /// Permite registrar Print Screen o Win+Shift+S para iniciar capturas.
    /// </summary>
    public class HotkeyService : IDisposable
    {
        #region P/Invoke declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modificadores de tecla
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        // Virtual Key Codes
        private const uint VK_SNAPSHOT = 0x2C;  // Print Screen
        private const uint VK_S = 0x53;         // S key

        // Hotkey IDs
        private const int HOTKEY_ID_PRINTSCREEN = 1;
        private const int HOTKEY_ID_CTRLSHIFTS = 2;

        // Window Message
        public const int WM_HOTKEY = 0x0312;

        #endregion

        #region Registry paths

        /// <summary>
        /// Ruta del registro donde Windows 11 guarda la configuración de Print Screen para Snipping Tool.
        /// </summary>
        private const string KEYBOARD_REGISTRY_PATH = @"Control Panel\Keyboard";
        private const string PRINT_SCREEN_SNIPPING_VALUE = "PrintScreenKeyForSnippingEnabled";

        #endregion

        private readonly IntPtr _windowHandle;
        private bool _printScreenRegistered;
        private bool _ctrlShiftSRegistered;
        private bool _disposed;

        /// <summary>
        /// Se dispara cuando se presiona el hotkey registrado.
        /// </summary>
        public event EventHandler<HotkeyType>? HotkeyPressed;

        /// <summary>
        /// Se dispara cuando hay un error al registrar un hotkey.
        /// </summary>
        public event EventHandler<string>? RegistrationError;

        /// <summary>
        /// Indica si el hotkey Print Screen está registrado.
        /// </summary>
        public bool IsPrintScreenRegistered => _printScreenRegistered;

        /// <summary>
        /// Indica si el hotkey Ctrl+Shift+S está registrado.
        /// </summary>
        public bool IsCtrlShiftSRegistered => _ctrlShiftSRegistered;

        /// <summary>
        /// Inicializa el servicio de hotkeys.
        /// </summary>
        /// <param name="window">Ventana principal de la aplicación.</param>
        public HotkeyService(Window window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        }

        #region Public Methods

        /// <summary>
        /// Verifica si Windows tiene habilitada la opción de Print Screen para Snipping Tool.
        /// </summary>
        /// <returns>True si Snipping Tool está usando Print Screen, False en caso contrario.</returns>
        public static bool IsSnippingToolUsingPrintScreen()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KEYBOARD_REGISTRY_PATH);
                if (key != null)
                {
                    var value = key.GetValue(PRINT_SCREEN_SNIPPING_VALUE);
                    if (value is int intValue)
                    {
                        return intValue == 1;
                    }
                }
            }
            catch
            {
                // Si hay un error al leer el registro, asumimos que no está habilitado
            }

            return false;
        }

        /// <summary>
        /// Abre la configuración de accesibilidad de Windows donde se puede desactivar
        /// la opción de Print Screen para Snipping Tool.
        /// </summary>
        public static async void OpenWindowsKeyboardSettings()
        {
            try
            {
                // URI para abrir directamente la configuración de Accesibilidad > Teclado
                var uri = new Uri("ms-settings:easeofaccess-keyboard");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch
            {
                // Si falla, intentar abrir la configuración general
                try
                {
                    var uri = new Uri("ms-settings:");
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
                catch
                {
                    // Ignorar si no se puede abrir
                }
            }
        }

        /// <summary>
        /// Registra el hotkey Print Screen.
        /// </summary>
        /// <returns>True si se registró correctamente, False en caso contrario.</returns>
        public bool RegisterPrintScreen()
        {
            if (_printScreenRegistered)
            {
                return true;
            }

            // Intentar registrar con MOD_NOREPEAT para evitar repeticiones
            bool success = RegisterHotKey(_windowHandle, HOTKEY_ID_PRINTSCREEN, MOD_NOREPEAT, VK_SNAPSHOT);
            
            if (!success)
            {
                // Intentar sin MOD_NOREPEAT como fallback
                success = RegisterHotKey(_windowHandle, HOTKEY_ID_PRINTSCREEN, MOD_NONE, VK_SNAPSHOT);
            }

            if (success)
            {
                _printScreenRegistered = true;
            }
            else
            {
                var errorCode = Marshal.GetLastWin32Error();
                var errorMessage = GetHotkeyErrorMessage("Print Screen", errorCode);
                RegistrationError?.Invoke(this, errorMessage);
            }

            return success;
        }

        /// <summary>
        /// Registra el hotkey Ctrl+Shift+S.
        /// </summary>
        /// <returns>True si se registró correctamente, False en caso contrario.</returns>
        public bool RegisterCtrlShiftS()
        {
            if (_ctrlShiftSRegistered)
            {
                return true;
            }

            // Ctrl + Shift + S con MOD_NOREPEAT
            bool success = RegisterHotKey(_windowHandle, HOTKEY_ID_CTRLSHIFTS, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_S);
            
            if (!success)
            {
                // Intentar sin MOD_NOREPEAT
                success = RegisterHotKey(_windowHandle, HOTKEY_ID_CTRLSHIFTS, MOD_CONTROL | MOD_SHIFT, VK_S);
            }

            if (success)
            {
                _ctrlShiftSRegistered = true;
            }
            else
            {
                var errorCode = Marshal.GetLastWin32Error();
                var errorMessage = GetHotkeyErrorMessage("Ctrl+Shift+S", errorCode);
                RegistrationError?.Invoke(this, errorMessage);
            }

            return success;
        }

        /// <summary>
        /// Genera un mensaje de error descriptivo para fallos de registro de hotkeys.
        /// </summary>
        /// <param name="hotkeyName">Nombre del hotkey que falló.</param>
        /// <param name="errorCode">Código de error de Win32.</param>
        /// <returns>Mensaje descriptivo del error.</returns>
        private static string GetHotkeyErrorMessage(string hotkeyName, int errorCode)
        {
            // ERROR_HOTKEY_ALREADY_REGISTERED = 1409
            if (errorCode == 1409)
            {
                string appInfo = "Otra aplicación ya tiene registrado este atajo de teclado.";

                return $"No se pudo registrar {hotkeyName}.\n\n{appInfo}";
            }

            // ERROR_INVALID_WINDOW_HANDLE = 1400
            if (errorCode == 1400)
            {
                return $"No se pudo registrar {hotkeyName}: la ventana no es válida.";
            }

            // Mensaje genérico para otros errores
            return $"No se pudo registrar {hotkeyName}.\n\n" +
                   $"Código de error: {errorCode}\n" +
                   "Es posible que otra aplicación esté usando esta combinación de teclas.";
        }

        /// <summary>
        /// Desregistra el hotkey Print Screen.
        /// </summary>
        public void UnregisterPrintScreen()
        {
            if (_printScreenRegistered)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID_PRINTSCREEN);
                _printScreenRegistered = false;
            }
        }

        /// <summary>
        /// Desregistra el hotkey Ctrl+Shift+S.
        /// </summary>
        public void UnregisterCtrlShiftS()
        {
            if (_ctrlShiftSRegistered)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID_CTRLSHIFTS);
                _ctrlShiftSRegistered = false;
            }
        }

        /// <summary>
        /// Desregistra todos los hotkeys.
        /// </summary>
        public void UnregisterAll()
        {
            UnregisterPrintScreen();
            UnregisterCtrlShiftS();
        }

        /// <summary>
        /// Procesa un mensaje de Windows para detectar hotkeys.
        /// Debe llamarse desde el WndProc de la ventana principal.
        /// </summary>
        /// <param name="msg">Código del mensaje.</param>
        /// <param name="wParam">Parámetro wParam (contiene el ID del hotkey).</param>
        /// <returns>True si el mensaje fue procesado, False en caso contrario.</returns>
        public bool ProcessHotkeyMessage(uint msg, IntPtr wParam)
        {
            if (msg != WM_HOTKEY)
            {
                return false;
            }

            int hotkeyId = wParam.ToInt32();

            switch (hotkeyId)
            {
                case HOTKEY_ID_PRINTSCREEN:
                    HotkeyPressed?.Invoke(this, HotkeyType.PrintScreen);
                    return true;

                case HOTKEY_ID_CTRLSHIFTS:
                    HotkeyPressed?.Invoke(this, HotkeyType.CtrlShiftS);
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Libera los recursos del servicio, desregistrando todos los hotkeys.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Libera los recursos del servicio.
        /// </summary>
        /// <param name="disposing">True si se llama desde Dispose(), False desde el finalizador.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                UnregisterAll();
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizador.
        /// </summary>
        ~HotkeyService()
        {
            Dispose(false);
        }

        #endregion
    }
}
