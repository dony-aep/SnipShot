using System;
using System.Runtime.InteropServices;

namespace SnipShot.Models
{
    /// <summary>
    /// Constantes y estructuras propias de Shell_NotifyIcon (System Tray).
    /// Las firmas P/Invoke que las usan están en <see cref="NativeMethods"/>.
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
