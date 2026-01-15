using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using SnipShot.Models;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Helper para captura de pantalla usando GDI32
    /// </summary>
    public static class ScreenCaptureHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        private const int SRCCOPY = 0x00CC0020;
        private const int DIB_RGB_COLORS = 0;

        #endregion

        /// <summary>
        /// Captura una región específica de la pantalla
        /// </summary>
        /// <param name="x">Coordenada X de la región</param>
        /// <param name="y">Coordenada Y de la región</param>
        /// <param name="width">Ancho de la región</param>
        /// <param name="height">Alto de la región</param>
        /// <returns>SoftwareBitmap con la imagen capturada</returns>
        public static SoftwareBitmap CaptureRegion(int x, int y, int width, int height)
        {
            IntPtr screenDC = IntPtr.Zero;
            IntPtr memDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                // Obtener el DC de la pantalla
                screenDC = GetDC(IntPtr.Zero);
                if (screenDC == IntPtr.Zero)
                    throw new InvalidOperationException("No se pudo obtener el DC de la pantalla");

                // Crear un DC compatible
                memDC = CreateCompatibleDC(screenDC);
                if (memDC == IntPtr.Zero)
                    throw new InvalidOperationException("No se pudo crear un DC compatible");

                // Crear un bitmap compatible
                hBitmap = CreateCompatibleBitmap(screenDC, width, height);
                if (hBitmap == IntPtr.Zero)
                    throw new InvalidOperationException("No se pudo crear un bitmap compatible");

                // Seleccionar el bitmap en el DC
                hOld = SelectObject(memDC, hBitmap);

                // Copiar la región de la pantalla al bitmap
                if (!BitBlt(memDC, 0, 0, width, height, screenDC, x, y, SRCCOPY))
                    throw new InvalidOperationException("No se pudo copiar la región de la pantalla");

                // Preparar la información del bitmap
                BITMAPINFO bmi = new BITMAPINFO
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFO)),
                    biWidth = width,
                    biHeight = -height, // Top-down bitmap
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                };

                // Calcular el tamaño del buffer
                int stride = ((width * 32 + 31) / 32) * 4;
                byte[] pixelData = new byte[stride * height];

                // Obtener los bits del bitmap
                if (GetDIBits(memDC, hBitmap, 0, (uint)height, pixelData, ref bmi, DIB_RGB_COLORS) == 0)
                    throw new InvalidOperationException("No se pudieron obtener los bits del bitmap");

                // Convertir BGRA a RGBA
                for (int i = 0; i < pixelData.Length; i += 4)
                {
                    byte temp = pixelData[i];
                    pixelData[i] = pixelData[i + 2];
                    pixelData[i + 2] = temp;
                }

                // Crear el SoftwareBitmap
                var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, width, height, BitmapAlphaMode.Premultiplied);
                softwareBitmap.CopyFromBuffer(pixelData.AsBuffer());

                return softwareBitmap;
            }
            finally
            {
                // Limpiar recursos
                if (hOld != IntPtr.Zero && memDC != IntPtr.Zero)
                    SelectObject(memDC, hOld);
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                if (memDC != IntPtr.Zero)
                    DeleteDC(memDC);
                if (screenDC != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
    }
}
