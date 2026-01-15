using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using SnipShot.Models;

namespace SnipShot.Helpers.WindowManagement
{
    /// <summary>
    /// Helper para configuración de ventanas
    /// </summary>
    public static class WindowHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        private const int WS_CAPTION = 0xC00000;

        #endregion

        /// <summary>
        /// Obtiene las dimensiones del escritorio virtual (incluyendo todos los monitores)
        /// </summary>
        /// <returns>RectInt32 con las coordenadas y dimensiones del escritorio virtual</returns>
        public static RectInt32 GetVirtualScreenBounds()
        {
            int left = GetSystemMetrics(Constants.SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(Constants.SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(Constants.SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(Constants.SM_CYVIRTUALSCREEN);

            return new RectInt32(left, top, width, height);
        }

        /// <summary>
        /// Obtiene las dimensiones de la pantalla principal
        /// </summary>
        /// <returns>RectInt32 con las coordenadas y dimensiones de la pantalla principal</returns>
        public static RectInt32 GetPrimaryMonitorBounds()
        {
            const uint MONITOR_DEFAULTTOPRIMARY = 1;
            POINT pt = new POINT { X = 0, Y = 0 };
            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTOPRIMARY);

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                return new RectInt32(
                    mi.rcMonitor.Left,
                    mi.rcMonitor.Top,
                    mi.rcMonitor.Right - mi.rcMonitor.Left,
                    mi.rcMonitor.Bottom - mi.rcMonitor.Top
                );
            }

            // Fallback a la pantalla principal usando GetSystemMetrics
            int width = GetSystemMetrics(0); // SM_CXSCREEN
            int height = GetSystemMetrics(1); // SM_CYSCREEN
            return new RectInt32(0, 0, width, height);
        }

        /// <summary>
        /// Obtiene los límites del monitor que contiene el punto especificado.
        /// Los valores están en píxeles físicos de pantalla.
        /// </summary>
        /// <param name="x">Coordenada X del punto</param>
        /// <param name="y">Coordenada Y del punto</param>
        /// <returns>Límites del monitor en píxeles físicos</returns>
        public static RectInt32 GetMonitorBoundsAtPoint(int x, int y)
        {
            const uint MONITOR_DEFAULTTONEAREST = 2;
            POINT pt = new POINT { X = x, Y = y };
            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                return new RectInt32(
                    mi.rcMonitor.Left,
                    mi.rcMonitor.Top,
                    mi.rcMonitor.Right - mi.rcMonitor.Left,
                    mi.rcMonitor.Bottom - mi.rcMonitor.Top
                );
            }

            // Fallback a la pantalla principal
            return GetPrimaryMonitorBounds();
        }

        /// <summary>
        /// Remueve los bordes y la barra de título de una ventana
        /// </summary>
        /// <param name="window">Ventana a modificar</param>
        /// <param name="windowHandle">Handle de la ventana</param>
        public static void RemoveWindowBorders(Window window, IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return;

            try
            {
                int style = GetWindowLong(windowHandle, GWL_STYLE);
                style &= ~(WS_CAPTION | WS_SYSMENU);
                SetWindowLong(windowHandle, GWL_STYLE, style);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al remover bordes de ventana: {ex.Message}");
            }
        }
    }
}
