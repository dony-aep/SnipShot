using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using SnipShot.Helpers.Utils;

namespace SnipShot.Helpers.Capture
{
    /// <summary>
    /// Helper para aplicar bordes a capturas de pantalla.
    /// </summary>
    public static class BorderHelper
    {
        /// <summary>
        /// Aplica un borde alrededor de un bitmap y devuelve un nuevo bitmap expandido.
        /// </summary>
        /// <param name="sourceBitmap">Bitmap original</param>
        /// <param name="colorHex">Color del borde en formato hex (ej: "#FF000000")</param>
        /// <param name="thickness">Grosor del borde en píxeles</param>
        /// <returns>Nuevo bitmap con el borde aplicado</returns>
        public static async Task<SoftwareBitmap?> ApplyBorderAsync(
            SoftwareBitmap sourceBitmap,
            string colorHex,
            double thickness)
        {
            if (sourceBitmap == null || thickness <= 0)
            {
                return sourceBitmap;
            }

            try
            {
                // Parsear el color
                if (!ColorConverter.TryParseHexColor(colorHex, out var borderColor))
                {
                    borderColor = Colors.Black;
                }

                int borderThickness = (int)Math.Round(thickness);
                int newWidth = sourceBitmap.PixelWidth + (2 * borderThickness);
                int newHeight = sourceBitmap.PixelHeight + (2 * borderThickness);

                // Convertir a formato compatible si es necesario
                SoftwareBitmap bitmapForRendering;
                bool needsDispose = false;
                
                if (sourceBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    sourceBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmapForRendering = SoftwareBitmap.Convert(
                        sourceBitmap,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                    needsDispose = true;
                }
                else
                {
                    bitmapForRendering = sourceBitmap;
                }

                var device = CanvasDevice.GetSharedDevice();
                using var canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, bitmapForRendering);

                // Crear render target con el tamaño expandido
                using var renderTarget = new CanvasRenderTarget(
                    device,
                    newWidth,
                    newHeight,
                    canvasBitmap.Dpi);

                using (var ds = renderTarget.CreateDrawingSession())
                {
                    // Fondo con el color del borde
                    ds.Clear(borderColor);

                    // Dibujar la imagen original centrada (offset por el grosor del borde)
                    ds.DrawImage(canvasBitmap, borderThickness, borderThickness);
                }

                // Convertir a SoftwareBitmap
                using var stream = new InMemoryRandomAccessStream();
                await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                stream.Seek(0);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var resultBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                if (needsDispose)
                {
                    bitmapForRendering.Dispose();
                }

                return resultBitmap;
            }
            catch (Exception)
            {
                // En caso de error, devolver el bitmap original
                return sourceBitmap;
            }
        }
    }
}
