using System;
using System.Runtime.InteropServices;

namespace SnipShot.Models
{
    /// <summary>
    /// Estructuras nativas y P/Invoke para Shell_NotifyIcon (System Tray).
    /// Implementación sin dependencias externas para máximo rendimiento.
    /// </summary>
    public static class NativeSystemTrayStructures
    {
        #region Constants

        /// <summary>
        /// Mensaje personalizado para callbacks del System Tray.
        /// </summary>
        public const int WM_TRAYICON = 0x8000 + 1; // WM_APP + 1

        /// <summary>
        /// Mensaje de destrucción de ventana.
        /// </summary>
        public const int WM_DESTROY = 0x0002;

        /// <summary>
        /// Mensajes del mouse.
        /// </summary>
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_CONTEXTMENU = 0x007B;

        /// <summary>
        /// Operaciones de Shell_NotifyIcon.
        /// </summary>
        public const int NIM_ADD = 0x00000000;
        public const int NIM_MODIFY = 0x00000001;
        public const int NIM_DELETE = 0x00000002;
        public const int NIM_SETVERSION = 0x00000004;

        /// <summary>
        /// Flags de NOTIFYICONDATA.
        /// </summary>
        public const int NIF_MESSAGE = 0x00000001;
        public const int NIF_ICON = 0x00000002;
        public const int NIF_TIP = 0x00000004;
        public const int NIF_STATE = 0x00000008;
        public const int NIF_INFO = 0x00000010;
        public const int NIF_GUID = 0x00000020;
        public const int NIF_SHOWTIP = 0x00000080;

        /// <summary>
        /// Versión del comportamiento del icono.
        /// </summary>
        public const int NOTIFYICON_VERSION_4 = 4;

        /// <summary>
        /// Comando de tracking del menú.
        /// </summary>
        public const uint TPM_LEFTALIGN = 0x0000;
        public const uint TPM_BOTTOMALIGN = 0x0020;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_NONOTIFY = 0x0080;

        #endregion

        #region Structures

        /// <summary>
        /// Estructura NOTIFYICONDATA para Shell_NotifyIcon.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        /// <summary>
        /// Estructura POINT para coordenadas.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Información de clase de ventana.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        #endregion

        #region P/Invoke

        /// <summary>
        /// Agrega, modifica o elimina un icono de la bandeja del sistema.
        /// </summary>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        /// <summary>
        /// Carga un icono desde un archivo.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImage(
            IntPtr hInstance,
            string lpszName,
            uint uType,
            int cxDesired,
            int cyDesired,
            uint fuLoad);

        /// <summary>
        /// Destruye un icono cargado.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// Obtiene la posición del cursor.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Crea un menú popup.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        /// <summary>
        /// Destruye un menú.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        /// <summary>
        /// Añade un item al menú.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        /// <summary>
        /// Muestra el menú popup y devuelve la selección.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            IntPtr hWnd,
            IntPtr lptpm);

        /// <summary>
        /// Pone la ventana en primer plano.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Registra una clase de ventana.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        /// <summary>
        /// Desregistra una clase de ventana.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        /// <summary>
        /// Crea una ventana.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x, int y,
            int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        /// <summary>
        /// Destruye una ventana.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(IntPtr hWnd);

        /// <summary>
        /// Procedimiento de ventana por defecto.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Obtiene el handle del módulo actual.
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        /// <summary>
        /// Envía un mensaje a la cola de mensajes.
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Constants for Menu and Image

        /// <summary>
        /// Tipo de imagen: icono.
        /// </summary>
        public const uint IMAGE_ICON = 1;

        /// <summary>
        /// Cargar desde archivo.
        /// </summary>
        public const uint LR_LOADFROMFILE = 0x00000010;

        /// <summary>
        /// Tamaño por defecto.
        /// </summary>
        public const uint LR_DEFAULTSIZE = 0x00000040;

        /// <summary>
        /// Flags de menú.
        /// </summary>
        public const uint MF_STRING = 0x00000000;
        public const uint MF_SEPARATOR = 0x00000800;

        #endregion
    }
}
