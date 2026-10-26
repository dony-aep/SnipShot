using System;
using Windows.Graphics;

namespace SnipShot.Models
{
    /// <summary>
    /// Representa información relevante de una ventana capturable.
    /// </summary>
    public readonly struct WindowInfo
    {
        public WindowInfo(IntPtr handle, RectInt32 bounds, string title, int processId = 0, string processName = "")
        {
            Handle = handle;
            Bounds = bounds;
            Title = title;
            ProcessId = processId;
            ProcessName = processName;
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

        /// <summary>
        /// Proceso dueño de la ventana. El comando snipshot lo usa para elegir ventana por nombre
        /// de programa, que es más estable que el título.
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// Nombre del proceso sin extensión (p. ej., "Notepad"), o vacío si no se pudo leer.
        /// </summary>
        public string ProcessName { get; }
    }
}
