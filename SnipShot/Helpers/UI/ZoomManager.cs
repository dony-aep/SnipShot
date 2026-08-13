using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Graphics.Imaging;
using Windows.System;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona toda la funcionalidad de zoom para la vista previa de imágenes.
    /// Proporciona métodos para zoom in/out, ajustar a ventana, y tamaño real.
    /// </summary>
    public class ZoomManager
    {
        #region Constants

        private const double ZOOM_INCREMENT = 0.1;

        /// <summary>
        /// Suelo absoluto del zoom. No es un valor elegido: ScrollViewer.MinZoomFactor lanza
        /// "The MinZoomFactor property cannot be set to a value smaller than 0.1" por debajo
        /// de esto. No bajarlo.
        /// </summary>
        /// <remarks>
        /// El mínimo real se calcula por imagen en GetMinimumZoom(); esta constante solo es el
        /// tope que impone el control. Una imagen tan grande que ni al 10% quepa entera en el
        /// viewport se quedará ahí, que es lo máximo que el ScrollViewer permite alejar.
        /// </remarks>
        private const double MIN_ZOOM = 0.1;

        private const double MAX_ZOOM = 10.0;

        #endregion

        #region Fields

        private double _currentZoomLevel = 1.0;
        private ZoomMode _currentZoomMode = ZoomMode.FitToWindow;
        private bool _fitZoomPending;

        /// <summary>
        /// Hay una transición de zoom animada en curso, así que _currentZoomLevel es el
        /// destino y el ZoomFactor del ScrollViewer todavía no ha llegado a él.
        /// </summary>
        private bool _isAnimatingZoom;
        private DispatcherTimer? _sizeChangedDebounceTimer;

        private readonly Image _previewImage;
        private readonly ScrollViewer _scrollViewer;

        private SoftwareBitmap? _currentBitmap;

        #endregion

        #region Enums

        public enum ZoomMode
        {
            FitToWindow,
            ActualSize,
            Custom
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Inicializa una nueva instancia de ZoomManager.
        /// </summary>
        /// <param name="previewImage">Control Image para mostrar la vista previa.</param>
        /// <param name="scrollViewer">ScrollViewer que contiene la imagen.</param>
        public ZoomManager(Image previewImage, ScrollViewer scrollViewer)
        {
            _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
            _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));

            // Suscribirse al evento de cambio de tamaño del ScrollViewer
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;

            // Para saber cuándo termina una transición animada de zoom
            _scrollViewer.ViewChanged += OnScrollViewerViewChanged;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Establece el bitmap actual para el zoom.
        /// </summary>
        /// <param name="bitmap">El bitmap a gestionar.</param>
        public void SetBitmap(SoftwareBitmap? bitmap)
        {
            _currentBitmap = bitmap;
        }

        /// <summary>
        /// Aumenta el nivel de zoom.
        /// </summary>
        public void ZoomIn()
        {
            ApplyZoom(GetEffectiveZoomLevel() + ZOOM_INCREMENT);
        }

        /// <summary>
        /// Disminuye el nivel de zoom.
        /// </summary>
        public void ZoomOut()
        {
            ApplyZoom(GetEffectiveZoomLevel() - ZOOM_INCREMENT);
        }

        /// <summary>
        /// Ajusta la imagen para que quepa completamente en la ventana.
        /// </summary>
        public void FitToWindow()
        {
            if (_currentBitmap == null)
                return;

            _currentZoomMode = ZoomMode.FitToWindow;
            ApplyFitZoom();
        }

        /// <summary>
        /// Establece el zoom al tamaño real de la imagen (100%).
        /// </summary>
        public void SetActualSize()
        {
            if (_currentBitmap == null)
                return;

            ApplyZoom(1.0, ZoomMode.ActualSize);
        }

        /// <summary>
        /// Restablece el estado del zoom.
        /// </summary>
        public void Reset()
        {
            _currentZoomLevel = 1.0;
            _currentZoomMode = ZoomMode.ActualSize;
            _currentBitmap = null;
            _fitZoomPending = false;

            _isAnimatingZoom = false;

            _previewImage.Width = double.NaN;
            _previewImage.Height = double.NaN;

            // Sin animar: al limpiar no hay nada que acompañar visualmente.
            _scrollViewer.ChangeView(null, null, 1.0f, true);
        }

        /// <summary>
        /// Maneja los atajos de teclado para el zoom.
        /// </summary>
        /// <param name="e">Argumentos del evento de teclado.</param>
        /// <returns>True si el evento fue manejado, false en caso contrario.</returns>
        public bool HandleKeyboardShortcut(KeyRoutedEventArgs e)
        {
            // Verificar si Ctrl está presionado
            var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!ctrlPressed || _currentBitmap == null)
                return false;

            // Ctrl + Plus (Zoom In)
            if (e.Key == VirtualKey.Add || e.Key == (VirtualKey)187) // 187 = tecla '+'
            {
                ZoomIn();
                e.Handled = true;
                return true;
            }
            // Ctrl + Minus (Zoom Out)
            else if (e.Key == VirtualKey.Subtract || e.Key == (VirtualKey)189) // 189 = tecla '-'
            {
                ZoomOut();
                e.Handled = true;
                return true;
            }
            // Ctrl + 0 (Tamaño real)
            else if (e.Key == VirtualKey.Number0 || e.Key == VirtualKey.NumberPad0)
            {
                SetActualSize();
                e.Handled = true;
                return true;
            }
            // Ctrl + 9 (Ajustar a ventana). No se usa Ctrl+Shift+0: Ctrl+Shift es el atajo
            // del sistema para cambiar de distribución de teclado, y Windows lo intercepta
            // antes de que llegue a la app cuando hay más de una instalada.
            else if (e.Key == VirtualKey.Number9 || e.Key == VirtualKey.NumberPad9)
            {
                FitToWindow();
                e.Handled = true;
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Devuelve el nivel de zoom que se está viendo de verdad.
        /// </summary>
        /// <remarks>
        /// Los ScrollViewer de la app tienen ZoomMode habilitado, así que el usuario puede
        /// hacer zoom con Ctrl+rueda sin pasar por este manager. Partir de _currentZoomLevel
        /// haría que acercar y alejar saltaran desde un valor obsoleto: estando al 250% por
        /// la rueda, acercar llevaba al 125%. Solo se cae al campo si el ScrollViewer aún no
        /// tiene un factor válido, que ocurre antes del primer layout.
        /// </remarks>
        private double GetEffectiveZoomLevel()
        {
            // Con una animación en marcha el ZoomFactor va a medio camino hacia el destino.
            // Partir de él haría que pulsar acercar dos veces seguidas diera pasos más cortos
            // de lo pedido, así que mientras dura se usa el destino ya fijado.
            if (_isAnimatingZoom)
            {
                return _currentZoomLevel;
            }

            double actualZoom = _scrollViewer.ZoomFactor;
            return actualZoom > 0 ? actualZoom : _currentZoomLevel;
        }

        /// <summary>
        /// La vista deja de moverse cuando llega un ViewChanged no intermedio.
        /// </summary>
        private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (!e.IsIntermediate)
            {
                _isAnimatingZoom = false;
            }
        }

        /// <summary>
        /// Aplica un nivel de zoom específico.
        /// </summary>
        private void ApplyZoom(double newZoomLevel, ZoomMode mode = ZoomMode.Custom)
        {
            if (_currentBitmap == null || _previewImage.Source == null)
                return;

            SyncScrollViewerZoomBounds();

            newZoomLevel = Math.Clamp(newZoomLevel, GetMinimumZoom(), MAX_ZOOM);
            _currentZoomLevel = newZoomLevel;
            _currentZoomMode = mode;

            // Asegurar tamaño base de la imagen
            _previewImage.Stretch = Stretch.None;
            _previewImage.Width = _currentBitmap.PixelWidth;
            _previewImage.Height = _currentBitmap.PixelHeight;

            (double? offsetX, double? offsetY) = GetCenteredOffsets(newZoomLevel);
            // ChangeView devuelve false si no pudo iniciar la transición; tomando la bandera
            // de ahí no se queda encendida para siempre en ese caso.
            _isAnimatingZoom = _scrollViewer.ChangeView(
                offsetX, offsetY, (float)_currentZoomLevel, disableAnimation: false);
        }

        /// <summary>
        /// Calcula los desplazamientos que dejan en el centro del viewport el mismo punto de
        /// la imagen que ya estaba centrado antes de cambiar el zoom.
        /// </summary>
        /// <remarks>
        /// Pasando null a ChangeView, el ScrollViewer conserva los offsets actuales, que están
        /// anclados a la esquina superior izquierda: al ampliar, la imagen parece escaparse
        /// hacia un lado en vez de crecer desde el centro.
        /// </remarks>
        private (double? OffsetX, double? OffsetY) GetCenteredOffsets(double newZoomLevel)
        {
            double currentZoom = _scrollViewer.ZoomFactor;
            double viewportWidth = _scrollViewer.ViewportWidth;
            double viewportHeight = _scrollViewer.ViewportHeight;

            // Antes del primer layout no hay viewport ni zoom con los que calcular nada;
            // se deja que el ScrollViewer decida.
            if (currentZoom <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            {
                return (null, null);
            }

            double centerX = (_scrollViewer.HorizontalOffset + (viewportWidth / 2)) / currentZoom;
            double centerY = (_scrollViewer.VerticalOffset + (viewportHeight / 2)) / currentZoom;

            return (
                (centerX * newZoomLevel) - (viewportWidth / 2),
                (centerY * newZoomLevel) - (viewportHeight / 2));
        }

        /// <summary>
        /// Zoom mínimo útil: aquel con el que la imagen entra entera en el viewport.
        /// </summary>
        /// <remarks>
        /// Alejarse más allá no aporta nada, solo encoge una imagen que ya se ve completa.
        /// En imágenes más pequeñas que el viewport el mínimo es el 100%, porque tampoco
        /// tiene sentido reducirlas todavía más.
        /// </remarks>
        private double GetMinimumZoom()
        {
            if (_currentBitmap == null)
            {
                return MIN_ZOOM;
            }

            double viewportWidth = _scrollViewer.ViewportWidth;
            double viewportHeight = _scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0
                || _currentBitmap.PixelWidth <= 0 || _currentBitmap.PixelHeight <= 0)
            {
                return MIN_ZOOM;
            }

            double fitFactor = Math.Min(
                viewportWidth / _currentBitmap.PixelWidth,
                viewportHeight / _currentBitmap.PixelHeight);

            return Math.Clamp(Math.Min(fitFactor, 1.0), MIN_ZOOM, 1.0);
        }

        /// <summary>
        /// Aplica los límites al propio ScrollViewer para que el zoom con Ctrl+rueda respete
        /// el mismo rango que los botones y los atajos.
        /// </summary>
        private void SyncScrollViewerZoomBounds()
        {
            _scrollViewer.MaxZoomFactor = (float)MAX_ZOOM;
            _scrollViewer.MinZoomFactor = (float)GetMinimumZoom();
        }

        /// <summary>
        /// Aplica el zoom ajustado a la ventana.
        /// </summary>
        private void ApplyFitZoom()
        {
            if (_currentBitmap == null)
                return;

            var viewportWidth = _scrollViewer.ViewportWidth;
            var viewportHeight = _scrollViewer.ViewportHeight;

            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                if (_fitZoomPending)
                    return;

                _fitZoomPending = true;
                _scrollViewer.DispatcherQueue?.TryEnqueue(() =>
                {
                    _fitZoomPending = false;
                    if (_currentZoomMode == ZoomMode.FitToWindow)
                    {
                        ApplyFitZoom();
                    }
                });
                return;
            }

            var imageWidth = _currentBitmap.PixelWidth;
            var imageHeight = _currentBitmap.PixelHeight;

            if (imageWidth <= 0 || imageHeight <= 0)
                return;

            var widthFactor = viewportWidth / imageWidth;
            var heightFactor = viewportHeight / imageHeight;
            var fitFactor = Math.Clamp(Math.Min(widthFactor, heightFactor), MIN_ZOOM, MAX_ZOOM);

            _currentZoomLevel = fitFactor;

            _previewImage.Stretch = Stretch.None;
            _previewImage.Width = imageWidth;
            _previewImage.Height = imageHeight;

            // Antes de mover la vista, para que el nuevo mínimo (que es este mismo factor)
            // ya esté aplicado y el ScrollViewer no rechace el valor.
            SyncScrollViewerZoomBounds();

            _isAnimatingZoom = _scrollViewer.ChangeView(
                null, null, (float)_currentZoomLevel, disableAnimation: false);
        }



        /// <summary>
        /// Maneja el cambio de tamaño del ScrollViewer con debounce.
        /// </summary>
        private void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_currentZoomMode != ZoomMode.FitToWindow || _currentBitmap == null)
            {
                return;
            }

            // Debounce: esperar 50ms antes de aplicar el zoom
            _sizeChangedDebounceTimer?.Stop();
            _sizeChangedDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _sizeChangedDebounceTimer.Tick -= SizeChangedDebounceTimer_Tick;
            _sizeChangedDebounceTimer.Tick += SizeChangedDebounceTimer_Tick;
            _sizeChangedDebounceTimer.Start();
        }

        /// <summary>
        /// Handler del timer de debounce para el cambio de tamaño.
        /// </summary>
        private void SizeChangedDebounceTimer_Tick(object? sender, object e)
        {
            _sizeChangedDebounceTimer?.Stop();
            if (_currentZoomMode == ZoomMode.FitToWindow && _currentBitmap != null)
            {
                ApplyFitZoom();
            }
        }

        #endregion
    }
}
