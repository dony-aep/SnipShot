using System;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Recorte y mezcla de píxeles BGRA8 premultiplicados sobre arrays de bytes. No depende de
    /// WinUI, así que las pruebas lo compilan directamente.
    /// </summary>
    public static class PixelCompositor
    {
        private const int BytesPerPixel = 4;

        /// <summary>
        /// Recorta un rectángulo de la imagen y, si se da una capa, la pinta encima. La capa está
        /// en las mismas coordenadas que la imagen; lo que cae fuera de ella se ignora.
        /// </summary>
        /// <param name="source">Píxeles de la imagen, fila a fila sin relleno.</param>
        /// <param name="layer">Capa con alfa (p. ej., las anotaciones), o null.</param>
        /// <returns>Píxeles del recorte, de <paramref name="width"/> × <paramref name="height"/>.</returns>
        public static byte[] Crop(
            byte[] source, int sourceWidth, int sourceHeight,
            int x, int y, int width, int height,
            byte[]? layer = null, int layerWidth = 0, int layerHeight = 0)
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
                int sourceY = y + row;
                Buffer.BlockCopy(source, (sourceY * sourceWidth + x) * BytesPerPixel, result, row * rowBytes, rowBytes);

                if (layer is null || sourceY >= layerHeight)
                {
                    continue;
                }

                for (int column = 0; column < width && x + column < layerWidth; column++)
                {
                    int src = (sourceY * layerWidth + x + column) * BytesPerPixel;
                    int alpha = layer[src + 3];
                    if (alpha == 0)
                    {
                        continue;
                    }

                    // Mezcla "over" con alfa premultiplicado: resultado = capa + imagen * (1 - alfa).
                    int dst = row * rowBytes + column * BytesPerPixel;
                    int inverse = 255 - alpha;
                    for (int channel = 0; channel < BytesPerPixel; channel++)
                    {
                        result[dst + channel] = (byte)(layer[src + channel] + (result[dst + channel] * inverse + 127) / 255);
                    }
                }
            }

            return result;
        }
    }
}
