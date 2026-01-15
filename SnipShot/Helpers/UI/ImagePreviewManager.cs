using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona la vista previa de imágenes capturadas.
    /// Maneja la conversión, renderizado y visualización de capturas.
    /// </summary>
    public class ImagePreviewManager : IDisposable
    {
        private readonly Image _previewImage;
        private readonly ScrollViewer _previewScrollViewer;
        private readonly FrameworkElement _placeholderPanel;
        private readonly Button _copyButton;
        private readonly Button _saveButton;
        private readonly Button _clearButton;
        private readonly FrameworkElement? _editToolbar;
        private readonly FrameworkElement? _actionSeparator;

        private SoftwareBitmap? _currentCapture;
        private InMemoryRandomAccessStream? _currentPreviewStream;
        private bool _disposed;

        /// <summary>
        /// Obtiene el bitmap de la captura actual.
        /// </summary>
        public SoftwareBitmap? CurrentCapture => _currentCapture;

        /// <summary>
        /// Evento que se dispara cuando se muestra una nueva captura.
        /// </summary>
        public event EventHandler<SoftwareBitmap>? CaptureDisplayed;

        /// <summary>
        /// Evento que se dispara cuando se limpia la captura.
        /// </summary>
        public event EventHandler? CaptureCleared;

        /// <summary>
        /// Evento que se dispara cuando cambia la visibilidad de la barra de herramientas de edición.
        /// </summary>
        public event EventHandler<bool>? EditToolbarVisibilityChanged;

        /// <summary>
        /// Inicializa una nueva instancia de ImagePreviewManager.
        /// </summary>
        /// <param name="previewImage">Control Image para mostrar la vista previa.</param>
        /// <param name="previewScrollViewer">ScrollViewer que contiene la imagen.</param>
        /// <param name="placeholderPanel">Panel placeholder que se muestra cuando no hay captura.</param>
        /// <param name="copyButton">Botón de copiar.</param>
        /// <param name="saveButton">Botón de guardar.</param>
        /// <param name="clearButton">Botón de limpiar.</param>
        /// <param name="editToolbar">Barra de herramientas de edición (opcional).</param>
        /// <param name="actionSeparator">Separador entre botones de acción (opcional).</param>
        public ImagePreviewManager(
            Image previewImage,
            ScrollViewer previewScrollViewer,
            FrameworkElement placeholderPanel,
            Button copyButton,
            Button saveButton,
            Button clearButton,
            FrameworkElement? editToolbar = null,
            FrameworkElement? actionSeparator = null)
        {
            _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
            _previewScrollViewer = previewScrollViewer ?? throw new ArgumentNullException(nameof(previewScrollViewer));
            _placeholderPanel = placeholderPanel ?? throw new ArgumentNullException(nameof(placeholderPanel));
            _copyButton = copyButton ?? throw new ArgumentNullException(nameof(copyButton));
            _saveButton = saveButton ?? throw new ArgumentNullException(nameof(saveButton));
            _clearButton = clearButton ?? throw new ArgumentNullException(nameof(clearButton));
            _editToolbar = editToolbar;
            _actionSeparator = actionSeparator;
        }

        /// <summary>
        /// Muestra una captura en la vista previa.
        /// </summary>
        /// <param name="screenshot">Bitmap de la captura a mostrar.</param>
        /// <returns>True si se mostró correctamente, false en caso contrario.</returns>
        public async Task<bool> ShowCaptureAsync(SoftwareBitmap screenshot)
        {
            if (screenshot == null)
                return false;

            try
            {
                // Limpiar captura anterior
                _currentCapture?.Dispose();
                _currentCapture = screenshot;

                _currentPreviewStream?.Dispose();
                _currentPreviewStream = new InMemoryRandomAccessStream();

                // Convertir a formato compatible si es necesario
                SoftwareBitmap bitmapForEncoding = screenshot;
                if (screenshot.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    screenshot.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    bitmapForEncoding = SoftwareBitmap.Convert(
                        screenshot,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                }

                // Codificar como PNG
                var encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.PngEncoderId,
                    _currentPreviewStream);
                encoder.SetSoftwareBitmap(bitmapForEncoding);
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                encoder.IsThumbnailGenerated = false;
                await encoder.FlushAsync();

                // Limpiar bitmap temporal si fue convertido
                if (!ReferenceEquals(bitmapForEncoding, screenshot))
                {
                    bitmapForEncoding.Dispose();
                }

                // Preparar para mostrar
                _currentPreviewStream.Seek(0);

                var bitmapImage = new BitmapImage
                {
                    CreateOptions = BitmapCreateOptions.IgnoreImageCache
                };
                await bitmapImage.SetSourceAsync(_currentPreviewStream);
                _previewImage.Source = bitmapImage;

                // Configurar dimensiones de la imagen
                _previewImage.Stretch = Stretch.None;
                _previewImage.Width = _currentCapture.PixelWidth;
                _previewImage.Height = _currentCapture.PixelHeight;

                // Actualizar visibilidad de controles
                ShowPreviewControls();

                // Notificar que se mostró una captura
                CaptureDisplayed?.Invoke(this, screenshot);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Carga y muestra una imagen desde un archivo de almacenamiento.
        /// </summary>
        /// <param name="file">Archivo de imagen a cargar.</param>
        /// <returns>True si se cargó correctamente, false en caso contrario.</returns>
        public async Task<bool> LoadImageFromFileAsync(Windows.Storage.StorageFile file)
        {
            if (file == null)
                return false;

            try
            {
                using (var stream = await file.OpenReadAsync())
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);

                    return await ShowCaptureAsync(softwareBitmap);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Limpia la vista previa actual y restaura el estado inicial.
        /// </summary>
        public void Clear()
        {
            // Limpiar recursos
            _previewImage.Source = null;
            _currentCapture?.Dispose();
            _currentCapture = null;
            _currentPreviewStream?.Dispose();
            _currentPreviewStream = null;

            // Ocultar controles de vista previa
            HidePreviewControls();

            // Notificar que se limpió la captura
            CaptureCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Verifica si hay una captura actualmente mostrada.
        /// </summary>
        public bool HasCapture => _currentCapture != null;

        /// <summary>
        /// Muestra los controles de vista previa y oculta el placeholder.
        /// </summary>
        private void ShowPreviewControls()
        {
            _previewImage.Visibility = Visibility.Visible;
            _previewScrollViewer.Visibility = Visibility.Visible;
            _placeholderPanel.Visibility = Visibility.Collapsed;

            _copyButton.Visibility = Visibility.Visible;
            _saveButton.Visibility = Visibility.Visible;
            _clearButton.Visibility = Visibility.Visible;
            
            if (_actionSeparator != null)
            {
                _actionSeparator.Visibility = Visibility.Visible;
            }
            
            if (_editToolbar != null)
            {
                _editToolbar.Visibility = Visibility.Visible;
                EditToolbarVisibilityChanged?.Invoke(this, true);
            }
        }

        /// <summary>
        /// Oculta los controles de vista previa y muestra el placeholder.
        /// </summary>
        private void HidePreviewControls()
        {
            _previewImage.Visibility = Visibility.Collapsed;
            _previewScrollViewer.Visibility = Visibility.Collapsed;
            _placeholderPanel.Visibility = Visibility.Visible;

            _copyButton.Visibility = Visibility.Collapsed;
            _saveButton.Visibility = Visibility.Collapsed;
            _clearButton.Visibility = Visibility.Collapsed;
            
            if (_actionSeparator != null)
            {
                _actionSeparator.Visibility = Visibility.Collapsed;
            }
            
            if (_editToolbar != null)
            {
                _editToolbar.Visibility = Visibility.Collapsed;
                EditToolbarVisibilityChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// Libera los recursos utilizados por ImagePreviewManager.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _currentCapture?.Dispose();
            _currentPreviewStream?.Dispose();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
