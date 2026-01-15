using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using Windows.Foundation;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Models;
using WinUIColor = Windows.UI.Color;

namespace SnipShot.Features.Capture.Modes.ColorPicker
{
    /// <summary>
    /// Control de modo de captura para seleccionar colores de la pantalla.
    /// Se carga dentro del ShadeOverlayWindow.
    /// </summary>
    public sealed partial class ColorPickerCaptureControl : CaptureModeBase
    {
        #region Campos privados

        private ColorInfo? _currentColor;
        private ColorFormat _currentFormat = ColorFormat.HEX;
        private bool _isDraggingMenu;
        private Point _dragStartPoint;
        private Point _menuStartPosition;
        private CaptureMode? _previousMode;

        #endregion

        #region Propiedades

        /// <inheritdoc/>
        public override CaptureMode Mode => CaptureMode.ColorPicker;

        #endregion

        #region Constructor

        public ColorPickerCaptureControl()
        {
            InitializeComponent();
            
            // Inicializar con un color por defecto
            _currentColor = new ColorInfo(255, 0, 0);
        }

        #endregion

        #region Métodos de ciclo de vida

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            
            // Posicionar el menú cuando el control esté cargado
            Loaded += OnControlLoaded;
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            PositionColorPickerMenu();
            UpdateColorDisplay();
        }

        /// <inheritdoc/>
        protected override void OnActivated()
        {
            base.OnActivated();
            // El tooltip se mostrará cuando el usuario mueva el mouse
            ColorTooltip.Visibility = Visibility.Collapsed;
        }

        /// <inheritdoc/>
        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            ColorTooltip.Visibility = Visibility.Collapsed;
        }

        /// <inheritdoc/>
        protected override void OnCleanup()
        {
            base.OnCleanup();
            Loaded -= OnControlLoaded;
        }

        #endregion

        #region Métodos públicos

        /// <summary>
        /// Establece el modo anterior para poder volver
        /// </summary>
        public void SetPreviousMode(CaptureMode previousMode)
        {
            _previousMode = previousMode;
        }

        #endregion

        #region Posicionamiento del menú

        /// <summary>
        /// Posiciona el menú del color picker en la parte superior centrada del monitor principal
        /// </summary>
        private void PositionColorPickerMenu()
        {
            // Obtener los límites de la pantalla principal
            var primaryMonitor = WindowHelper.GetPrimaryMonitorBounds();
            var virtualBounds = WindowHelper.GetVirtualScreenBounds();
            
            // Calcular la posición del centro de la pantalla principal en coordenadas del overlay
            double centerX = primaryMonitor.X - virtualBounds.X + (primaryMonitor.Width / 2.0);
            double topMargin = primaryMonitor.Y - virtualBounds.Y + 32; // 32px desde arriba
            
            double menuWidth = 320; // Ancho real aproximado del menú
            
            // Posicionar el menú
            Canvas.SetLeft(ColorPickerMenu, centerX - (menuWidth / 2.0));
            Canvas.SetTop(ColorPickerMenu, topMargin);
        }

        #endregion

        #region Captura de color

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive) return;

            var point = e.GetCurrentPoint(RootGrid);
            
            // Mostrar tooltip cuando el usuario mueva el mouse
            if (ColorTooltip.Visibility == Visibility.Collapsed)
            {
                ColorTooltip.Visibility = Visibility.Visible;
            }
            
            // Convertir a coordenadas de pantalla
            int screenX = VirtualBounds.X + (int)point.Position.X;
            int screenY = VirtualBounds.Y + (int)point.Position.Y;
            
            // Capturar el color del pixel
            _currentColor = ColorCaptureHelper.GetPixelColor(screenX, screenY);
            
            // Actualizar UI
            UpdateColorDisplay();
            PositionTooltip(point.Position);
        }

        private async void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive) return;
            
            // No copiar si se está arrastrando el menú
            if (_isDraggingMenu)
                return;
                
            if (_currentColor != null)
            {
                // Copiar al portapapeles
                await ClipboardHelper.CopyTextToClipboard(_currentColor.GetFormatted(_currentFormat));
                
                // Mostrar notificación de copiado
                ShowCopiedNotification();
                
                // Completar la captura con el color
                RaiseCaptureCompleted(new CaptureCompletedEventArgs
                {
                    ColorInfo = _currentColor
                });
            }
        }

        /// <summary>
        /// Muestra una notificación indicando que el color fue copiado
        /// </summary>
        private void ShowCopiedNotification()
        {
            var notification = new AppNotificationBuilder()
                .AddText("Color copiado al portapapeles")
                .AddText($"Se copió el valor: {_currentColor?.GetFormatted(_currentFormat) ?? ""}")
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }

        /// <summary>
        /// Actualiza la visualización del color capturado
        /// </summary>
        private void UpdateColorDisplay()
        {
            if (_currentColor == null) return;

            // Actualizar el display grande del color
            ColorDisplayBrush.Color = 
                WinUIColor.FromArgb(255, _currentColor.R, _currentColor.G, _currentColor.B);

            // Actualizar el texto del valor
            ColorValueText.Text = _currentColor.GetFormatted(_currentFormat);
            
            // Actualizar el tooltip
            ColorTooltipText.Text = _currentColor.GetFormatted(_currentFormat);
        }

        /// <summary>
        /// Posiciona el tooltip cerca del cursor
        /// </summary>
        private void PositionTooltip(Point cursorPosition)
        {
            double offsetX = 20;
            double offsetY = 20;
            
            Canvas.SetLeft(ColorTooltip, cursorPosition.X + offsetX);
            Canvas.SetTop(ColorTooltip, cursorPosition.Y + offsetY);
        }

        #endregion

        #region Event Handlers

        private void Format_Changed(object sender, SelectionChangedEventArgs e)
        {
            _currentFormat = FormatComboBox.SelectedIndex switch
            {
                0 => ColorFormat.HEX,
                1 => ColorFormat.RGB,
                2 => ColorFormat.HSL,
                _ => ColorFormat.HEX
            };

            UpdateColorDisplay();
        }

        private void ColorPickerMenu_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingMenu = true;
            _dragStartPoint = e.GetCurrentPoint(RootGrid).Position;
            _menuStartPosition = new Point(
                Canvas.GetLeft(ColorPickerMenu),
                Canvas.GetTop(ColorPickerMenu)
            );
            
            (sender as Border)?.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void ColorPickerMenu_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isDraggingMenu)
            {
                var currentPoint = e.GetCurrentPoint(RootGrid).Position;
                var deltaX = currentPoint.X - _dragStartPoint.X;
                var deltaY = currentPoint.Y - _dragStartPoint.Y;
                
                Canvas.SetLeft(ColorPickerMenu, _menuStartPosition.X + deltaX);
                Canvas.SetTop(ColorPickerMenu, _menuStartPosition.Y + deltaY);
                
                e.Handled = true;
            }
        }

        private void ColorPickerMenu_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDraggingMenu)
            {
                _isDraggingMenu = false;
                (sender as Border)?.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Volver al modo anterior (por defecto Rectangular)
            var targetMode = _previousMode ?? CaptureMode.Rectangular;
            RaiseModeChangeRequested(targetMode);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RaiseCaptureCancelled();
        }

        private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            RaiseCaptureCancelled();
        }

        #endregion
    }
}
