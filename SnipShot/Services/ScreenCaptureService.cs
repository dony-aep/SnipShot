using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Models;
using static SnipShot.Models.NativeMethods;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio para captura de pantalla utilizando Windows GDI32 API.
    /// Proporciona métodos para capturar el escritorio virtual completo o regiones específicas.
    /// </summary>
    public class ScreenCaptureService
    {
        #region Windows API Constants

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SRCCOPY = 0x00CC0020;

        #endregion

        #region Windows API Imports

        #endregion

        #region Structures


        #endregion

        #region Public Methods

        /// <summary>
        /// Captura todo el escritorio virtual (todas las pantallas conectadas).
        /// </summary>
        /// <returns>SoftwareBitmap con la captura completa, o null si falla.</returns>
        public async Task<SoftwareBitmap?> CaptureFullScreenAsync()
        {
            return await Task.Run(() =>
            {
                IntPtr hdcScreen = IntPtr.Zero;
                IntPtr hdcMemDC = IntPtr.Zero;
                IntPtr hBitmap = IntPtr.Zero;

                try
                {
                    // Obtener dimensiones de TODAS las pantallas (escritorio virtual)
                    int screenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                    int screenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
                    int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                    int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                    // Crear contextos de dispositivo
                    hdcScreen = GetDC(IntPtr.Zero);
                    hdcMemDC = CreateCompatibleDC(hdcScreen);
                    hBitmap = CreateCompatibleBitmap(hdcScreen, screenWidth, screenHeight);

                    SelectObject(hdcMemDC, hBitmap);
                    
                    // Capturar desde la posición virtual (puede ser negativa si hay monitores a la izquierda)
                    BitBlt(hdcMemDC, 0, 0, screenWidth, screenHeight, hdcScreen, screenLeft, screenTop, SRCCOPY);

                    // Convertir a SoftwareBitmap
                    return ConvertToBitmap(hdcScreen, hBitmap, screenWidth, screenHeight);
                }
                finally
                {
                    // Limpiar recursos
                    if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                    if (hdcMemDC != IntPtr.Zero) DeleteDC(hdcMemDC);
                    if (hdcScreen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            });
        }

        /// <summary>
        /// Captura una región específica de la pantalla.
        /// </summary>
        /// <param name="x">Coordenada X de inicio (puede ser negativa en monitores múltiples).</param>
        /// <param name="y">Coordenada Y de inicio (puede ser negativa en monitores múltiples).</param>
        /// <param name="width">Ancho de la región a capturar.</param>
        /// <param name="height">Alto de la región a capturar.</param>
        /// <returns>SoftwareBitmap con la región capturada, o null si falla.</returns>
        public async Task<SoftwareBitmap?> CaptureRegionAsync(int x, int y, int width, int height)
        {
            return await Task.Run(() =>
            {
                IntPtr hdcScreen = IntPtr.Zero;
                IntPtr hdcMemDC = IntPtr.Zero;
                IntPtr hBitmap = IntPtr.Zero;

                try
                {
                    // Crear contextos de dispositivo
                    hdcScreen = GetDC(IntPtr.Zero);
                    hdcMemDC = CreateCompatibleDC(hdcScreen);
                    hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);

                    SelectObject(hdcMemDC, hBitmap);
                    
                    // Capturar la región específica
                    BitBlt(hdcMemDC, 0, 0, width, height, hdcScreen, x, y, SRCCOPY);

                    // Convertir a SoftwareBitmap
                    return ConvertToBitmap(hdcScreen, hBitmap, width, height);
                }
                finally
                {
                    // Limpiar recursos
                    if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                    if (hdcMemDC != IntPtr.Zero) DeleteDC(hdcMemDC);
                    if (hdcScreen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcScreen);
                }
            });
        }

        /// <summary>
        /// Obtiene las dimensiones completas del escritorio virtual.
        /// </summary>
        /// <returns>RectInt32 con las coordenadas y dimensiones del escritorio virtual.</returns>
        public RectInt32 GetVirtualScreenBounds()
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            return new RectInt32(left, top, width, height);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Convierte un bitmap de GDI a SoftwareBitmap.
        /// </summary>
        private SoftwareBitmap? ConvertToBitmap(IntPtr hdcScreen, IntPtr hBitmap, int width, int height)
        {
            try
            {
                // Configurar BITMAPINFO
                BITMAPINFO bmpInfo = new BITMAPINFO
                {
                    biSize = Marshal.SizeOf<BITMAPINFO>(),
                    biWidth = width,
                    biHeight = -height, // Top-down (negativo para invertir)
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                };

                // Obtener datos de píxeles
                int stride = width * 4;
                byte[] pixels = new byte[stride * height];
                GetDIBits(hdcScreen, hBitmap, 0, (uint)height, pixels, ref bmpInfo, 0);

                // Crear SoftwareBitmap
                var softwareBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    width,
                    height,
                    BitmapAlphaMode.Premultiplied);

                softwareBitmap.CopyFromBuffer(pixels.AsBuffer());

                return softwareBitmap;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
