using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics;
using SnipShot.Models;

namespace SnipShot.Helpers.WindowManagement
{
    /// <summary>
    /// Helper para descubrir ventanas que se pueden capturar.
    /// </summary>
    public static class WindowEnumerationHelper
    {
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const uint GA_ROOT = 2;
        private const uint GW_OWNER = 4;
    private const int SW_HIDE = 0;
    private const int SW_SHOWMINIMIZED = 2;
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int Length;
            public int Flags;
            public int ShowCmd;
            public POINT MinPosition;
            public POINT MaxPosition;
            public RECT NormalPosition;
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        /// <summary>
        /// Determina si existe al menos una ventana capturable distinta a la aplicación.
        /// </summary>
        public static bool HasCaptureableWindow(IntPtr appWindowHandle)
        {
            bool found = false;

            EnumWindows((hwnd, lParam) =>
            {
                if (IsCaptureableWindow(hwnd, appWindowHandle))
                {
                    found = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// Obtiene la ventana actualmente en primer plano que puede capturarse.
        /// </summary>
        public static IntPtr GetForegroundCaptureWindow(IntPtr appWindowHandle)
        {
            var foreground = GetForegroundWindow();
            return IsCaptureableWindow(foreground, appWindowHandle) ? foreground : IntPtr.Zero;
        }

        /// <summary>
        /// Obtiene la lista completa de ventanas capturables.
        /// </summary>
        /// <param name="appWindowHandle">Handle de la ventana de la aplicación.</param>
        /// <param name="includeOwnWindow">Si es true, incluye la propia ventana de la aplicación en la lista.</param>
        public static IReadOnlyList<WindowInfo> GetCaptureableWindows(IntPtr appWindowHandle, bool includeOwnWindow = false)
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hwnd, lParam) =>
            {
                if (IsCaptureableWindow(hwnd, appWindowHandle, includeOwnWindow) &&
                    TryGetWindowBounds(hwnd, out var bounds))
                {
                    string title = TryGetWindowTitle(hwnd, out var windowTitle)
                        ? windowTitle
                        : "Ventana sin título";

                    windows.Add(new WindowInfo(hwnd, bounds, title));
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary>
        /// Obtiene los límites de una ventana incluyendo el marco extendido (sin la barra de tareas).
        /// </summary>
        public static bool TryGetWindowBounds(IntPtr hwnd, out RectInt32 bounds)
        {
            bounds = default;

            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT dwmRect, Marshal.SizeOf<RECT>()) == 0)
            {
                bounds = ToRectInt32(dwmRect);
                return bounds.Width > 0 && bounds.Height > 0;
            }

            if (GetWindowRect(hwnd, out RECT rawRect))
            {
                bounds = ToRectInt32(rawRect);
                return bounds.Width > 0 && bounds.Height > 0;
            }

            return false;
        }

        private static bool IsCaptureableWindow(IntPtr hwnd, IntPtr appWindowHandle, bool includeOwnWindow = false)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            // Excluir la propia ventana solo si includeOwnWindow es false
            if (!includeOwnWindow && hwnd == appWindowHandle)
            {
                return false;
            }

            if (!IsWindowVisible(hwnd))
            {
                return false;
            }

            if (IsIconic(hwnd) || IsWindowCloaked(hwnd) || IsEffectivelyMinimized(hwnd))
            {
                return false;
            }

            if (!TryGetWindowBounds(hwnd, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            if (TryGetClassName(hwnd, out string className) && IsIgnoredWindowClass(className))
            {
                return false;
            }

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
            {
                return false;
            }

            IntPtr owner = GetWindow(hwnd, GW_OWNER);
            if (owner != IntPtr.Zero)
            {
                return false;
            }

            // Excluir ventanas del mismo proceso, a menos que includeOwnWindow sea true
            // y sea específicamente la ventana principal de la aplicación
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == (uint)Environment.ProcessId)
            {
                // Si includeOwnWindow es true, permitir la ventana principal de la app
                if (includeOwnWindow && hwnd == appWindowHandle)
                {
                    // Continuar con la validación
                }
                else
                {
                    return false;
                }
            }

            return GetAncestor(hwnd, GA_ROOT) == hwnd;
        }

        private static bool TryGetClassName(IntPtr hwnd, out string className)
        {
            var buffer = new StringBuilder(256);
            int length = GetClassName(hwnd, buffer, buffer.Capacity);

            if (length > 0)
            {
                className = buffer.ToString();
                return true;
            }

            className = string.Empty;
            return false;
        }

        private static bool TryGetWindowTitle(IntPtr hwnd, out string title)
        {
            int length = GetWindowTextLength(hwnd);
            if (length == 0)
            {
                title = string.Empty;
                return false;
            }

            var buffer = new StringBuilder(length + 1);
            int written = GetWindowText(hwnd, buffer, buffer.Capacity);

            if (written > 0)
            {
                title = buffer.ToString();
                return true;
            }

            title = string.Empty;
            return false;
        }

        private static bool IsIgnoredWindowClass(string className)
        {
            return string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase);
        }

        private static RectInt32 ToRectInt32(RECT rect)
        {
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            return new RectInt32(rect.Left, rect.Top, width, height);
        }

        private static bool IsWindowCloaked(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, Marshal.SizeOf<int>()) == 0)
            {
                return cloaked != 0;
            }

            return false;
        }

        private static bool IsEffectivelyMinimized(IntPtr hwnd)
        {
            var placement = new WINDOWPLACEMENT
            {
                Length = Marshal.SizeOf<WINDOWPLACEMENT>()
            };

            if (!GetWindowPlacement(hwnd, ref placement))
            {
                return false;
            }

            return placement.ShowCmd == SW_SHOWMINIMIZED || placement.ShowCmd == SW_HIDE;
        }
    }
}
