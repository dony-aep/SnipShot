using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using SnipShot.Models;
using static SnipShot.Models.NativeSystemTrayStructures;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio para gestionar el icono en la bandeja del sistema usando P/Invoke nativo.
    /// Implementación directa con Shell_NotifyIcon sin dependencias externas para máximo rendimiento.
    /// </summary>
    public sealed class NativeSystemTrayService : IDisposable
    {
        #region Constants

        /// <summary>
        /// IDs de menú para el context menu.
        /// </summary>
        private const uint MENU_SHOW = 1001;
        private const uint MENU_CAPTURE = 1002;
        private const uint MENU_EXIT = 1003;

        /// <summary>
        /// Nombre único de la clase de ventana para mensajes.
        /// </summary>
        private const string WINDOW_CLASS_NAME = "SnipShotTrayMsgWindow";

        /// <summary>
        /// ID único del icono en la bandeja.
        /// </summary>
        private const int TRAY_ICON_ID = 1;

        #endregion

        #region Fields

        private readonly Window _mainWindow;
        private IntPtr _messageWindowHandle;
        private IntPtr _iconHandle;
        private bool _isCreated;
        private bool _disposed;
        private NOTIFYICONDATA _notifyIconData;
        private WndProcDelegate? _wndProcDelegate;
        private Thread? _messageThread;
        private volatile bool _stopMessageLoop;

        #endregion

        #region Delegates

        /// <summary>
        /// Delegado para el procedimiento de ventana.
        /// </summary>
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Events

        /// <summary>
        /// Se dispara cuando el usuario solicita mostrar la ventana principal.
        /// </summary>
        public event EventHandler? ShowWindowRequested;

        /// <summary>
        /// Se dispara cuando el usuario solicita iniciar una captura desde el tray.
        /// </summary>
        public event EventHandler? CaptureRequested;

        /// <summary>
        /// Se dispara cuando el usuario solicita cerrar la aplicación desde el tray.
        /// </summary>
        public event EventHandler? ExitRequested;

        #endregion

        #region Constructor

        /// <summary>
        /// Crea una nueva instancia del servicio de System Tray nativo.
        /// </summary>
        /// <param name="mainWindow">Ventana principal de la aplicación.</param>
        public NativeSystemTrayService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Crea e inicializa el icono en la bandeja del sistema.
        /// </summary>
        public void Initialize()
        {
            if (_isCreated)
            {
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Iniciando Initialize()");

                // Crear ventana de mensajes en un thread separado para no bloquear UI
                _stopMessageLoop = false;
                _messageThread = new Thread(MessageLoopThread)
                {
                    IsBackground = true,
                    Name = "TrayMessageLoop"
                };
                _messageThread.SetApartmentState(ApartmentState.STA);
                _messageThread.Start();

                // Esperar a que la ventana se cree (máximo 3 segundos)
                var startTime = DateTime.Now;
                while (_messageWindowHandle == IntPtr.Zero && (DateTime.Now - startTime).TotalSeconds < 3)
                {
                    Thread.Sleep(10);
                }

                if (_messageWindowHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Error - No se pudo crear la ventana de mensajes");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Initialize() completado exitosamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en Initialize(): {ex.Message}");
            }
        }

        /// <summary>
        /// Actualiza el tooltip del icono en la bandeja.
        /// </summary>
        /// <param name="tooltip">Nuevo texto del tooltip.</param>
        public void UpdateTooltip(string tooltip)
        {
            if (!_isCreated || _messageWindowHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                _notifyIconData.szTip = tooltip ?? "SnipShot";
                _notifyIconData.uFlags = NIF_TIP | NIF_SHOWTIP;
                Shell_NotifyIcon(NIM_MODIFY, ref _notifyIconData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en UpdateTooltip(): {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina el icono de la bandeja del sistema.
        /// </summary>
        public void Remove()
        {
            if (!_isCreated)
            {
                return;
            }

            try
            {
                // Eliminar el icono
                Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);

                // Destruir el icono
                if (_iconHandle != IntPtr.Zero)
                {
                    DestroyIcon(_iconHandle);
                    _iconHandle = IntPtr.Zero;
                }

                // Detener el loop de mensajes
                _stopMessageLoop = true;
                if (_messageWindowHandle != IntPtr.Zero)
                {
                    PostMessage(_messageWindowHandle, WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
                }

                // Esperar a que termine el thread
                _messageThread?.Join(1000);

                _isCreated = false;
                System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Remove() completado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en Remove(): {ex.Message}");
            }
        }

        /// <summary>
        /// Indica si el icono del System Tray está creado.
        /// </summary>
        public bool IsCreated => _isCreated;

        #endregion

        #region Private Methods

        /// <summary>
        /// Thread que ejecuta el loop de mensajes para el System Tray.
        /// </summary>
        private void MessageLoopThread()
        {
            try
            {
                // Crear la clase de ventana
                _wndProcDelegate = WndProc;

                var wndClass = new WNDCLASSEX
                {
                    cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                    hInstance = GetModuleHandle(null),
                    lpszClassName = WINDOW_CLASS_NAME
                };

                var classAtom = RegisterClassEx(ref wndClass);
                if (classAtom == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    // Si la clase ya existe (error 1410), intentar desregistrar y volver a registrar
                    if (error == 1410)
                    {
                        UnregisterClass(WINDOW_CLASS_NAME, GetModuleHandle(null));
                        classAtom = RegisterClassEx(ref wndClass);
                    }

                    if (classAtom == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error RegisterClassEx: {Marshal.GetLastWin32Error()}");
                        return;
                    }
                }

                // Crear ventana de mensajes (invisible)
                _messageWindowHandle = CreateWindowEx(
                    0,
                    WINDOW_CLASS_NAME,
                    "SnipShotTrayWindow",
                    0,
                    0, 0, 0, 0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero);

                if (_messageWindowHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error CreateWindowEx: {Marshal.GetLastWin32Error()}");
                    return;
                }

                // Crear el icono del tray
                CreateTrayIcon();

                // Loop de mensajes
                while (!_stopMessageLoop)
                {
                    if (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                    else
                    {
                        break;
                    }
                }

                // Limpiar
                if (_messageWindowHandle != IntPtr.Zero)
                {
                    DestroyWindow(_messageWindowHandle);
                    _messageWindowHandle = IntPtr.Zero;
                }

                UnregisterClass(WINDOW_CLASS_NAME, GetModuleHandle(null));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en MessageLoopThread: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea el icono en la bandeja del sistema.
        /// </summary>
        private void CreateTrayIcon()
        {
            try
            {
                // Cargar el icono desde archivo
                var iconPath = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "snipshot.ico");

                if (System.IO.File.Exists(iconPath))
                {
                    _iconHandle = LoadImage(
                        IntPtr.Zero,
                        iconPath,
                        IMAGE_ICON,
                        16, 16,
                        LR_LOADFROMFILE | LR_DEFAULTSIZE);

                    System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Icono cargado = {_iconHandle != IntPtr.Zero}");
                }

                // Configurar NOTIFYICONDATA
                _notifyIconData = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _messageWindowHandle,
                    uID = TRAY_ICON_ID,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = _iconHandle,
                    szTip = "SnipShot - Captura de pantalla",
                    szInfo = string.Empty,
                    szInfoTitle = string.Empty
                };

                // Añadir el icono
                if (Shell_NotifyIcon(NIM_ADD, ref _notifyIconData))
                {
                    // Establecer versión 4 para comportamiento moderno
                    _notifyIconData.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
                    Shell_NotifyIcon(NIM_SETVERSION, ref _notifyIconData);

                    _isCreated = true;
                    System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Icono creado exitosamente");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("NativeSystemTrayService: Error al crear icono");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en CreateTrayIcon: {ex.Message}");
            }
        }

        /// <summary>
        /// Procedimiento de ventana para manejar mensajes del System Tray.
        /// </summary>
        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseMsg = (int)(lParam.ToInt64() & 0xFFFF);

                switch (mouseMsg)
                {
                    case WM_LBUTTONDBLCLK:
                    case WM_LBUTTONUP:
                        // Clic izquierdo: mostrar ventana
                        InvokeOnMainThread(() => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
                        break;

                    case WM_RBUTTONUP:
                        // Clic derecho: mostrar menú contextual
                        ShowContextMenu();
                        break;
                }
            }
            else if (msg == WM_DESTROY)
            {
                PostQuitMessage(0);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Muestra el menú contextual del System Tray.
        /// </summary>
        private void ShowContextMenu()
        {
            try
            {
                // Crear menú popup
                IntPtr hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero)
                {
                    return;
                }

                // Añadir items
                AppendMenu(hMenu, MF_STRING, MENU_SHOW, "Mostrar SnipShot");
                AppendMenu(hMenu, MF_STRING, MENU_CAPTURE, "Nueva captura");
                AppendMenu(hMenu, MF_SEPARATOR, 0, string.Empty);
                AppendMenu(hMenu, MF_STRING, MENU_EXIT, "Salir");

                // Obtener posición del cursor
                GetCursorPos(out POINT pt);

                // Necesario para que el menú se cierre correctamente al hacer clic fuera
                SetForegroundWindow(_messageWindowHandle);

                // Mostrar menú y obtener selección
                int cmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_LEFTALIGN | TPM_BOTTOMALIGN | TPM_RETURNCMD | TPM_NONOTIFY,
                    pt.X, pt.Y,
                    _messageWindowHandle,
                    IntPtr.Zero);

                // Destruir menú
                DestroyMenu(hMenu);

                // Procesar comando
                switch ((uint)cmd)
                {
                    case MENU_SHOW:
                        InvokeOnMainThread(() => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
                        break;
                    case MENU_CAPTURE:
                        InvokeOnMainThread(() => CaptureRequested?.Invoke(this, EventArgs.Empty));
                        break;
                    case MENU_EXIT:
                        InvokeOnMainThread(() => ExitRequested?.Invoke(this, EventArgs.Empty));
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en ShowContextMenu: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoca una acción en el thread principal de la UI.
        /// </summary>
        private void InvokeOnMainThread(Action action)
        {
            if (_mainWindow?.DispatcherQueue != null)
            {
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NativeSystemTrayService: Error en InvokeOnMainThread: {ex.Message}");
                    }
                });
            }
        }

        #endregion

        #region Additional P/Invoke

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Remove();
            _disposed = true;
        }

        #endregion
    }
}
