using System;
using System.Runtime.InteropServices;
using SnipShot.Models;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Helper para capturar el color de un pixel específico en la pantalla
    /// </summary>
    public static class ColorCaptureHelper
    {
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int x, int y);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        /// <summary>
        /// Captura el color del pixel en las coordenadas de pantalla especificadas
        /// </summary>
        /// <param name="screenX">Coordenada X en pantalla</param>
        /// <param name="screenY">Coordenada Y en pantalla</param>
        /// <returns>Información del color capturado</returns>
        public static ColorInfo GetPixelColor(int screenX, int screenY)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                uint pixel = GetPixel(hdc, screenX, screenY);

                return new ColorInfo
                {
                    R = (byte)(pixel & 0xFF),
                    G = (byte)((pixel >> 8) & 0xFF),
                    B = (byte)((pixel >> 16) & 0xFF)
                };
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }
}
