using System;
using Windows.Graphics;

namespace SnipShot.Models
{
    /// <summary>
    /// Representa información relevante de una ventana capturable.
    /// </summary>
    public readonly struct WindowInfo
    {
        public WindowInfo(IntPtr handle, RectInt32 bounds, string title)
        {
            Handle = handle;
            Bounds = bounds;
            Title = title;
        }

        /// <summary>
        /// Handle de la ventana (HWND).
        /// </summary>
        public IntPtr Handle { get; }

        /// <summary>
        /// Rectángulo en coordenadas de pantalla.
        /// </summary>
        public RectInt32 Bounds { get; }

        /// <summary>
        /// Título visible de la ventana.
        /// </summary>
        public string Title { get; }
    }
}
