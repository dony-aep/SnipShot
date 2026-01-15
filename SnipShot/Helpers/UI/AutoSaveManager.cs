using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Administra el guardado automático de capturas en una carpeta configurada
    /// </summary>
    public class AutoSaveManager
    {
        private const string AUTO_SAVE_FOLDER_NAME = "SnipShot";
        private const string AUTO_SAVE_FOLDER_TOKEN = "SnipShotAutoSaveFolderToken";

        private bool _autoSaveEnabled;
        private string _autoSaveFolderPath = string.Empty;

        /// <summary>
        /// Indica si el guardado automático está habilitado
        /// </summary>
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set => _autoSaveEnabled = value;
        }

        /// <summary>
        /// Obtiene la ruta actual de la carpeta de guardado automático
        /// </summary>
        public string AutoSaveFolderPath => _autoSaveFolderPath;

        /// <summary>
        /// Evento que se dispara cuando cambia la ruta de la carpeta de guardado automático
        /// </summary>
        public event EventHandler<string>? AutoSaveFolderPathChanged;

        /// <summary>
        /// Inicializa el AutoSaveManager con la configuración inicial
        /// </summary>
        /// <param name="autoSaveEnabled">Si el guardado automático está habilitado</param>
        /// <param name="initialPath">Ruta inicial de la carpeta (opcional)</param>
        public AutoSaveManager(bool autoSaveEnabled = false, string initialPath = "")
        {
            _autoSaveEnabled = autoSaveEnabled;
            _autoSaveFolderPath = !string.IsNullOrEmpty(initialPath) ? initialPath : GetDefaultAutoSavePath();
        }

        /// <summary>
        /// Guarda automáticamente una captura si el guardado automático está habilitado
        /// </summary>
        /// <param name="screenshot">El bitmap de la captura a guardar</param>
        /// <returns>True si se guardó exitosamente, false en caso contrario</returns>
        public async Task<bool> AutoSaveIfEnabledAsync(SoftwareBitmap screenshot)
        {
            if (!_autoSaveEnabled)
                return false;

            try
            {
                var targetFolder = await GetAutoSaveFolderAsync();
                if (targetFolder == null)
                {
                    return false;
                }

                string fileName = $"SnipShot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                var file = await targetFolder.CreateFileAsync(
                    fileName,
                    CreationCollisionOption.GenerateUniqueName);

                SoftwareBitmap bitmapForEncoding = screenshot;
                if (screenshot.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    screenshot.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmapForEncoding = SoftwareBitmap.Convert(
                        screenshot,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                }

                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    encoder.SetSoftwareBitmap(bitmapForEncoding);
                    encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                    encoder.IsThumbnailGenerated = false;
                    await encoder.FlushAsync();
                }

                if (!ReferenceEquals(bitmapForEncoding, screenshot))
                {
                    bitmapForEncoding.Dispose();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Auto-save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene la carpeta de guardado automático
        /// </summary>
        /// <returns>La carpeta de guardado automático, o null si no está disponible</returns>
        public async Task<StorageFolder?> GetAutoSaveFolderAsync()
        {
            StorageFolder? folder = null;
            var futureAccessList = StorageApplicationPermissions.FutureAccessList;

            // Intentar obtener carpeta personalizada desde el token
            if (futureAccessList.ContainsItem(AUTO_SAVE_FOLDER_TOKEN))
            {
                try
                {
                    folder = await futureAccessList.GetFolderAsync(AUTO_SAVE_FOLDER_TOKEN);
                    if (folder != null)
                    {
                        UpdateAutoSavePathCache(folder.Path);
                        return folder;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Custom auto-save folder unavailable: {ex.Message}");
                    futureAccessList.Remove(AUTO_SAVE_FOLDER_TOKEN);
                    ResetToDefaultAutoSavePath();
                }
            }

            // Fallback a carpeta por defecto en Pictures
            try
            {
                var picturesFolder = KnownFolders.PicturesLibrary;
                folder = await picturesFolder.CreateFolderAsync(
                    AUTO_SAVE_FOLDER_NAME,
                    CreationCollisionOption.OpenIfExists);

                UpdateAutoSavePathCache(folder.Path);
                return folder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to access default auto-save folder: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cambia la carpeta de guardado automático a una carpeta personalizada
        /// </summary>
        /// <param name="folder">La nueva carpeta seleccionada por el usuario</param>
        public void SetCustomAutoSaveFolder(StorageFolder folder)
        {
            if (folder == null)
                return;

            StorageApplicationPermissions.FutureAccessList.AddOrReplace(
                AUTO_SAVE_FOLDER_TOKEN,
                folder);

            UpdateAutoSavePathCache(folder.Path);
        }

        /// <summary>
        /// Restablece la carpeta de guardado automático a la ruta por defecto
        /// </summary>
        public void ResetToDefaultAutoSavePath()
        {
            UpdateAutoSavePathCache(GetDefaultAutoSavePath());
        }

        /// <summary>
        /// Actualiza la caché de ruta de guardado automático y notifica el cambio
        /// </summary>
        /// <param name="path">La nueva ruta</param>
        private void UpdateAutoSavePathCache(string path)
        {
            if (string.Equals(_autoSaveFolderPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            _autoSaveFolderPath = path;
            AutoSaveFolderPathChanged?.Invoke(this, _autoSaveFolderPath);
        }

        /// <summary>
        /// Obtiene la ruta por defecto para guardado automático
        /// </summary>
        /// <returns>Ruta completa a Pictures\SnipShot</returns>
        private string GetDefaultAutoSavePath()
        {
            var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return System.IO.Path.Combine(picturesPath, AUTO_SAVE_FOLDER_NAME);
        }
    }
}
