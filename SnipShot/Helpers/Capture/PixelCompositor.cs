using System;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Operaciones sobre píxeles BGRA8 en arrays de bytes. No depende de WinUI, así que las
    /// pruebas lo compilan directamente.
    /// </summary>
    public static class PixelCompositor
    {
        private const int BytesPerPixel = 4;

        /// <summary>
        /// Recorta un rectángulo de la imagen.
        /// </summary>
        /// <param name="source">Píxeles de la imagen, fila a fila sin relleno.</param>
        /// <returns>Píxeles del recorte, de <paramref name="width"/> × <paramref name="height"/>.</returns>
        public static byte[] Crop(byte[] source, int sourceWidth, int sourceHeight, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
                x + width > sourceWidth || y + height > sourceHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "El recorte se sale de la imagen.");
            }

            var result = new byte[width * height * BytesPerPixel];
            int rowBytes = width * BytesPerPixel;

            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(source, ((y + row) * sourceWidth + x) * BytesPerPixel, result, row * rowBytes, rowBytes);
            }

            return result;
        }
    }
}
