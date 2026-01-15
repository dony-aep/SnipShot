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
        private const double MIN_ZOOM = 0.1;
        private const double MAX_ZOOM = 10.0;

        #endregion

        #region Fields

        private double _currentZoomLevel = 1.0;
        private ZoomMode _currentZoomMode = ZoomMode.FitToWindow;
        private bool _fitZoomPending;
        private DispatcherTimer? _sizeChangedDebounceTimer;

        private readonly Image _previewImage;
        private readonly ScrollViewer _scrollViewer;
        private readonly MenuFlyoutItem? _zoomLevelMenuItem;

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
        /// <param name="zoomLevelMenuItem">MenuFlyoutItem opcional para mostrar el nivel de zoom actual.</param>
        public ZoomManager(Image previewImage, ScrollViewer scrollViewer, MenuFlyoutItem? zoomLevelMenuItem = null)
        {
            _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
            _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));
            _zoomLevelMenuItem = zoomLevelMenuItem;

            // Suscribirse al evento de cambio de tamaño del ScrollViewer
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
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
            ApplyZoom(_currentZoomLevel + ZOOM_INCREMENT);
        }

        /// <summary>
        /// Disminuye el nivel de zoom.
        /// </summary>
        public void ZoomOut()
        {
            ApplyZoom(_currentZoomLevel - ZOOM_INCREMENT);
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

            _previewImage.Width = double.NaN;
            _previewImage.Height = double.NaN;
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
            // Ctrl + 0 (Actual Size o Fit to Window con Shift)
            else if (e.Key == VirtualKey.Number0 || e.Key == VirtualKey.NumberPad0)
            {
                var shiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

                if (shiftPressed)
                {
                    FitToWindow();
                }
                else
                {
                    SetActualSize();
                }
                e.Handled = true;
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Aplica un nivel de zoom específico.
        /// </summary>
        private void ApplyZoom(double newZoomLevel, ZoomMode mode = ZoomMode.Custom)
        {
            if (_currentBitmap == null || _previewImage.Source == null)
                return;

            newZoomLevel = Math.Clamp(newZoomLevel, MIN_ZOOM, MAX_ZOOM);
            _currentZoomLevel = newZoomLevel;
            _currentZoomMode = mode;

            // Asegurar tamaño base de la imagen
            _previewImage.Stretch = Stretch.None;
            _previewImage.Width = _currentBitmap.PixelWidth;
            _previewImage.Height = _currentBitmap.PixelHeight;

            _scrollViewer.ChangeView(null, null, (float)_currentZoomLevel, true);
            UpdateZoomDisplay();
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

            _scrollViewer.ChangeView(null, null, (float)_currentZoomLevel, true);
            UpdateZoomDisplay();
        }

        /// <summary>
        /// Actualiza la visualización del nivel de zoom en el menú.
        /// </summary>
        private void UpdateZoomDisplay()
        {
            if (_zoomLevelMenuItem?.Template == null)
                return;

            var textBlock = FindElementInTemplate<TextBlock>(_zoomLevelMenuItem, "ZoomLevelText");
            if (textBlock != null)
            {
                string displayText = _currentZoomMode switch
                {
                    ZoomMode.FitToWindow => "Ajustado",
                    ZoomMode.ActualSize => "100%",
                    ZoomMode.Custom => $"{(_currentZoomLevel * 100):F0}%",
                    _ => "100%"
                };
                textBlock.Text = displayText;
            }
        }

        /// <summary>
        /// Busca un elemento en la plantilla visual.
        /// </summary>
        private T? FindElementInTemplate<T>(FrameworkElement element, string name) where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                if (child != null)
                {
                    if (child is T typedChild && child.Name == name)
                        return typedChild;

                    var result = FindElementInTemplate<T>(child, name);
                    if (result != null)
                        return result;
                }
            }
            return null;
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
