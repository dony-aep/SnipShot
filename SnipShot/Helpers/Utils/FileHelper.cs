using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SnipShot.Helpers.Utils
{
    /// <summary>
    /// Helper para operaciones con archivos
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Guarda una imagen en un archivo seleccionado por el usuario
        /// </summary>
        /// <param name="softwareBitmap">Imagen a guardar</param>
        /// <param name="windowHandle">Handle de la ventana para el diálogo</param>
        /// <param name="suggestedName">Nombre sugerido para el archivo (sin timestamp)</param>
        /// <returns>Par indicando si se guardó exitosamente y la ruta del archivo</returns>
        public static async Task<(bool saved, string? filePath)> SaveImageAsync(
            SoftwareBitmap softwareBitmap,
            IntPtr windowHandle,
            string? suggestedName = null)
        {
            if (softwareBitmap == null)
                throw new ArgumentNullException(nameof(softwareBitmap));

            string baseName = string.IsNullOrWhiteSpace(suggestedName) ? "SnipShot" : suggestedName;

            // Crear el FileSavePicker
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = $"{baseName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
            };

            savePicker.FileTypeChoices.Add("PNG", new[] { ".png" });
            savePicker.FileTypeChoices.Add("JPEG", new[] { ".jpg", ".jpeg" });

            // Inicializar con el handle de la ventana
            InitializeWithWindow.Initialize(savePicker, windowHandle);

            // Mostrar el diálogo
            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file == null)
                return (false, null); // Usuario canceló

            // Guardar la imagen
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapEncoder encoder;
                if (file.FileType.ToLower() == ".png")
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                }
                else
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                }

                encoder.SetSoftwareBitmap(softwareBitmap);
                await encoder.FlushAsync();
            }

            return (true, file.Path);
        }
    }
}
