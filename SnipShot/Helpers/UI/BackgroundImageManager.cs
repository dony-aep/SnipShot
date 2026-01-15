using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Helper para gestionar la imagen de fondo del overlay
    /// </summary>
    public static class BackgroundImageManager
    {
        /// <summary>
        /// Prepara y configura el bitmap de fondo para mostrarlo en el Image control
        /// </summary>
        /// <param name="backgroundBitmap">Bitmap original capturado</param>
        /// <param name="backgroundImage">Control Image donde se mostrará</param>
        /// <param name="backgroundSource">Referencia al SoftwareBitmapSource (puede ser null)</param>
        /// <returns>SoftwareBitmapSource configurado</returns>
        public static async Task<SoftwareBitmapSource?> PrepareBackgroundAsync(
            SoftwareBitmap? backgroundBitmap,
            Image backgroundImage,
            SoftwareBitmapSource? backgroundSource)
        {
            if (backgroundBitmap == null)
            {
                return null;
            }

            try
            {
                SoftwareBitmap bitmapForDisplay = backgroundBitmap;

                // Convertir el formato si es necesario
                if (backgroundBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    backgroundBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmapForDisplay = SoftwareBitmap.Convert(
                        backgroundBitmap,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                }

                // Crear o reutilizar el SoftwareBitmapSource
                backgroundSource ??= new SoftwareBitmapSource();
                await backgroundSource.SetBitmapAsync(bitmapForDisplay);
                backgroundImage.Source = backgroundSource;

                // Liberar el bitmap temporal si se creó uno nuevo
                if (!ReferenceEquals(bitmapForDisplay, backgroundBitmap))
                {
                    bitmapForDisplay.Dispose();
                }

                return backgroundSource;
            }
            catch
            {
                // Ignore background rendering issues; overlay will still work with the shade.
                return null;
            }
        }

        /// <summary>
        /// Limpia los recursos del bitmap de fondo
        /// </summary>
        /// <param name="backgroundImage">Control Image a limpiar</param>
        public static void CleanupBackground(Image backgroundImage)
        {
            if (backgroundImage != null)
            {
                backgroundImage.Source = null;
            }
        }
    }
}
