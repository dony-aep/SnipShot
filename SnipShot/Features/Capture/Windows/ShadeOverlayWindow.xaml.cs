using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Features.Capture.Modes.ColorPicker;
using SnipShot.Features.Capture.Modes.FreeForm;
using SnipShot.Features.Capture.Modes.Rectangular;
using SnipShot.Features.Capture.Modes.WindowCapture;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Models;

namespace SnipShot.Features.Capture.Windows
{
    /// <summary>
    /// Ventana overlay que contiene el shade y carga dinámicamente los modos de captura.
    /// Esta ventana permanece abierta mientras se cambia entre modos.
    /// </summary>
    public sealed partial class ShadeOverlayWindow : Window
    {
        #region Eventos

        /// <summary>
        /// Se dispara cuando una captura se completa exitosamente
        /// </summary>
        public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

        /// <summary>
        /// Se dispara cuando el usuario cancela la captura
        /// </summary>
        public event EventHandler? CaptureCancelled;

        #endregion

        #region Campos privados

        private readonly SoftwareBitmap _backgroundBitmap;
        private readonly RectInt32 _virtualBounds;
        private readonly CaptureMode _initialMode;
        private IReadOnlyList<WindowInfo>? _availableWindows;
        
        private SoftwareBitmapSource? _backgroundSource;
        private bool _backgroundPrepared;
        private bool _overlayClosed;
        
        private ICaptureMode? _currentMode;
        private CaptureMode? _previousMode;
        private readonly TaskCompletionSource<bool> _completionSource = new();

        #endregion

        #region Propiedades públicas

        /// <summary>
        /// Modo de captura actualmente activo
        /// </summary>
        public CaptureMode? CurrentMode => _currentMode?.Mode;

        /// <summary>
        /// Bitmap capturado (si el modo devuelve uno procesado)
        /// </summary>
        public SoftwareBitmap? CapturedBitmap { get; private set; }

        /// <summary>
        /// Bitmap de fondo original (para FullScreen)
        /// </summary>
        public SoftwareBitmap BackgroundBitmap => _backgroundBitmap;

        #region Configuración de bordes (Rectangular)
        
        private bool _borderEnabled;
        private string _borderColorHex = "#FFFFFF";
        private double _borderThickness = 2.0;

        /// <summary>
        /// Configura los ajustes de borde para capturas rectangulares
        /// </summary>
        public void SetBorderSettings(bool enabled, string colorHex, double thickness)
        {
            _borderEnabled = enabled;
            _borderColorHex = colorHex;
            _borderThickness = thickness;
            
            // Si ya hay un modo rectangular activo, actualizar sus configuraciones
            if (_currentMode is RectangularCaptureControl rectangularControl)
            {
                rectangularControl.SetBorderSettings(enabled, colorHex, thickness);
            }
        }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Crea una nueva instancia del overlay
        /// </summary>
        /// <param name="backgroundBitmap">Bitmap de la captura de pantalla completa (debe estar en formato Bgra8/Premultiplied)</param>
        /// <param name="virtualBounds">Límites de la pantalla virtual</param>
        /// <param name="initialMode">Modo inicial a cargar</param>
        /// <param name="availableWindows">Ventanas disponibles (para WindowCapture)</param>
        public ShadeOverlayWindow(
            SoftwareBitmap backgroundBitmap, 
            RectInt32 virtualBounds, 
            CaptureMode initialMode,
            IReadOnlyList<WindowInfo>? availableWindows = null)
        {
            InitializeComponent();
            
            _backgroundBitmap = backgroundBitmap ?? throw new ArgumentNullException(nameof(backgroundBitmap));
            _virtualBounds = virtualBounds;
            _initialMode = initialMode;
            _availableWindows = availableWindows;

            // Configurar la ventana como overlay a pantalla completa
            WindowConfigurationHelper.ConfigureOverlayWindow(this, virtualBounds);

            // Preparar cuando el layout esté listo usando la secuencia anti-parpadeo optimizada
            RootGrid.Loaded += OnRootGridLoaded;
        }

        /// <summary>
        /// Manejador del evento Loaded con secuencia anti-parpadeo optimizada.
        /// Garantiza que el fondo esté completamente renderizado antes de mostrar la ventana.
        /// </summary>
        private async void OnRootGridLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Preparar el bitmap de fondo (ya pre-convertido desde el servicio)
                await PrepareBackgroundAsync();
                
                // 2. Esperar a que la imagen de fondo esté completamente renderizada
                var imageRendered = await WindowConfigurationHelper.WaitForImageRenderAsync(
                    BackgroundImage, 
                    this.DispatcherQueue,
                    timeoutMs: 500);
                
                // 3. Ahora que el fondo está listo, mostrar el shade con animación suave
                await ShowShadeLayerAsync();
                
                // 4. Esperar ciclos adicionales para estabilidad visual
                await WindowConfigurationHelper.WaitForLayoutAsync(this.DispatcherQueue, 2);
                
                // 5. Mostrar la ventana (quitar cloak)
                WindowConfigurationHelper.UncloakWindow(this);
                
                // 6. Cargar el modo inicial
                await SwitchToModeAsync(_initialMode);
            }
            catch (Exception ex)
            {
                // En caso de error, intentar mostrar la ventana de todas formas
                System.Diagnostics.Debug.WriteLine($"Error en OnRootGridLoaded: {ex.Message}");
                WindowConfigurationHelper.UncloakWindow(this);
                await SwitchToModeAsync(_initialMode);
            }
        }

        /// <summary>
        /// Muestra el ShadeLayer con una transición suave de opacidad.
        /// </summary>
        private async Task ShowShadeLayerAsync()
        {
            // Animar la opacidad del shade de 0 a 1
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            var storyboard = new Storyboard();
            Storyboard.SetTarget(animation, ShadeLayer);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
            
            var tcs = new TaskCompletionSource<bool>();
            storyboard.Completed += (s, e) => tcs.TrySetResult(true);
            storyboard.Begin();
            
            await tcs.Task;
        }

        #endregion

        #region Métodos públicos

        /// <summary>
        /// Activa el overlay y espera hasta que se complete o cancele
        /// </summary>
        public Task<bool> WaitForCompletionAsync()
        {
            Activate();
            return _completionSource.Task;
        }

        /// <summary>
        /// Cambia al modo especificado sin cerrar el overlay
        /// </summary>
        public async Task SwitchToModeAsync(CaptureMode newMode)
        {
            // Desactivar el modo actual si existe
            if (_currentMode != null)
            {
                _previousMode = _currentMode.Mode;
                _currentMode.Deactivate();
                UnsubscribeFromMode(_currentMode);
            }

            // Crear el nuevo modo
            var mode = CreateMode(newMode);
            if (mode == null)
            {
                // Si es FullScreen, completar directamente con una copia del bitmap de fondo
                if (newMode == CaptureMode.FullScreen)
                {
                    SoftwareBitmap? bitmapCopy = null;
                    try
                    {
                        if (_backgroundBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                            _backgroundBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                        {
                            bitmapCopy = SoftwareBitmap.Convert(
                                _backgroundBitmap,
                                BitmapPixelFormat.Bgra8,
                                BitmapAlphaMode.Premultiplied);
                        }
                        else
                        {
                            bitmapCopy = SoftwareBitmap.Copy(_backgroundBitmap);
                        }
                    }
                    catch
                    {
                        bitmapCopy = _backgroundBitmap;
                    }
                    
                    CompleteCapture(bitmapCopy);
                    return;
                }
                return;
            }

            // Si el modo es WindowCapture y no hay ventanas disponibles, escanearlas
            // Nota: en este punto la ventana principal ya está oculta/cloaked, así que no puede incluirse
            if (newMode == CaptureMode.Window && (_availableWindows == null || _availableWindows.Count == 0))
            {
                var overlayHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                _availableWindows = WindowEnumerationHelper.GetCaptureableWindows(overlayHandle, includeOwnWindow: false);
            }

            // Configurar el shade según el modo
            ConfigureShadeForMode(newMode);

            // Suscribirse a los eventos del modo
            SubscribeToMode(mode);

            // Inicializar el modo
            mode.Initialize(_backgroundBitmap, _virtualBounds, _availableWindows);

            // Cargar el modo en el contenedor
            ModeContainer.Content = mode;
            _currentMode = mode;

            // Esperar un ciclo para que el control se cargue
            await WindowConfigurationHelper.WaitForUIThreadAsync(this.DispatcherQueue);

            // Activar el modo
            mode.Activate();
        }

        /// <summary>
        /// Establece la lista de ventanas disponibles (para WindowCapture)
        /// </summary>
        public void SetAvailableWindows(IReadOnlyList<WindowInfo> windows)
        {
            _availableWindows = windows;
        }

        /// <summary>
        /// Cierra el overlay
        /// </summary>
        public void CloseOverlay()
        {
            if (_overlayClosed) return;
            _overlayClosed = true;

            // Limpiar el modo actual
            if (_currentMode != null)
            {
                _currentMode.Cleanup();
                UnsubscribeFromMode(_currentMode);
                _currentMode = null;
            }

            try
            {
                Close();
            }
            catch { }
        }

        #endregion

        #region Métodos privados

        /// <summary>
        /// Prepara el fondo con el bitmap capturado
        /// </summary>
        private async Task PrepareBackgroundAsync()
        {
            if (_backgroundPrepared) return;

            _backgroundSource = await BackgroundImageManager.PrepareBackgroundAsync(
                _backgroundBitmap,
                BackgroundImage,
                _backgroundSource);

            _backgroundPrepared = true;
        }

        /// <summary>
        /// Crea una instancia del modo especificado
        /// </summary>
        private ICaptureMode? CreateMode(CaptureMode mode)
        {
            return mode switch
            {
                CaptureMode.Rectangular => CreateRectangularMode(),
                CaptureMode.FreeForm => CreateFreeFormMode(),
                CaptureMode.Window => CreateWindowMode(),
                CaptureMode.ColorPicker => CreateColorPickerMode(),
                CaptureMode.FullScreen => null, // FullScreen no necesita un modo, usa el bitmap directamente
                _ => null
            };
        }

        /// <summary>
        /// Crea el modo de captura rectangular
        /// </summary>
        private ICaptureMode? CreateRectangularMode()
        {
            var control = new RectangularCaptureControl();
            control.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            return control;
        }

        /// <summary>
        /// Crea el modo de captura de forma libre
        /// </summary>
        private ICaptureMode? CreateFreeFormMode()
        {
            return new FreeFormCaptureControl();
        }

        /// <summary>
        /// Crea el modo de captura de ventana
        /// </summary>
        private ICaptureMode? CreateWindowMode()
        {
            return new WindowCaptureControl();
        }

        /// <summary>
        /// Crea el modo de selector de color
        /// </summary>
        private ICaptureMode? CreateColorPickerMode()
        {
            var control = new ColorPickerCaptureControl();
            if (_previousMode.HasValue)
            {
                control.SetPreviousMode(_previousMode.Value);
            }
            return control;
        }

        /// <summary>
        /// Configura la visibilidad del shade según el modo
        /// </summary>
        private void ConfigureShadeForMode(CaptureMode mode)
        {
            // El ColorPicker no usa shade (muestra la pantalla tal cual)
            ShadeLayer.Visibility = mode == CaptureMode.ColorPicker 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        /// <summary>
        /// Suscribe a los eventos del modo
        /// </summary>
        private void SubscribeToMode(ICaptureMode mode)
        {
            mode.CaptureCompleted += OnModeCaptureCompleted;
            mode.CaptureCancelled += OnModeCaptureCancelled;
            mode.ModeChangeRequested += OnModeModeChangeRequested;
            mode.LocalShadesVisibilityChanged += OnModeLocalShadesVisibilityChanged;
        }

        /// <summary>
        /// Desuscribe de los eventos del modo
        /// </summary>
        private void UnsubscribeFromMode(ICaptureMode mode)
        {
            mode.CaptureCompleted -= OnModeCaptureCompleted;
            mode.CaptureCancelled -= OnModeCaptureCancelled;
            mode.ModeChangeRequested -= OnModeModeChangeRequested;
            mode.LocalShadesVisibilityChanged -= OnModeLocalShadesVisibilityChanged;
        }

        /// <summary>
        /// Maneja la captura completada desde un modo
        /// </summary>
        private void OnModeCaptureCompleted(object? sender, CaptureCompletedEventArgs e)
        {
            CapturedBitmap = e.CapturedBitmap;
            CaptureCompleted?.Invoke(this, e);
            _completionSource.TrySetResult(true);
            CloseOverlay();
        }

        /// <summary>
        /// Maneja la cancelación desde un modo
        /// </summary>
        private void OnModeCaptureCancelled(object? sender, EventArgs e)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            _completionSource.TrySetResult(false);
            CloseOverlay();
        }

        /// <summary>
        /// Maneja el cambio de visibilidad de los shades locales del modo.
        /// Cuando el modo muestra sus shades locales, ocultamos el shade global para evitar superposición.
        /// </summary>
        private void OnModeLocalShadesVisibilityChanged(object? sender, LocalShadesVisibilityEventArgs e)
        {
            // Cuando los shades locales están visibles, ocultamos el shade global
            // Cuando los shades locales se ocultan, mostramos el shade global
            ShadeLayer.Visibility = e.AreLocalShadesVisible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        /// <summary>
        /// Maneja la solicitud de cambio de modo
        /// </summary>
        private async void OnModeModeChangeRequested(object? sender, ModeChangeEventArgs e)
        {
            // Si el modo solicitado es FullScreen, completar con una copia del bitmap de fondo
            if (e.NewMode == CaptureMode.FullScreen)
            {
                // Crear una copia del bitmap porque el original podría ser disposed
                SoftwareBitmap? bitmapCopy = null;
                try
                {
                    if (_backgroundBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                        _backgroundBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                    {
                        bitmapCopy = SoftwareBitmap.Convert(
                            _backgroundBitmap,
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied);
                    }
                    else
                    {
                        bitmapCopy = SoftwareBitmap.Copy(_backgroundBitmap);
                    }
                }
                catch
                {
                    // Si falla la copia, usar el original
                    bitmapCopy = _backgroundBitmap;
                }
                
                CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs
                {
                    CapturedBitmap = bitmapCopy,
                    SelectedRegion = _virtualBounds
                });
                _completionSource.TrySetResult(true);
                CloseOverlay();
                return;
            }

            // Para todos los demás modos, cambiar internamente sin cerrar el overlay
            await SwitchToModeAsync(e.NewMode);
        }

        /// <summary>
        /// Completa la captura con el bitmap especificado
        /// </summary>
        private void CompleteCapture(SoftwareBitmap bitmap)
        {
            CapturedBitmap = bitmap;
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs
            {
                CapturedBitmap = bitmap
            });
            _completionSource.TrySetResult(true);
            CloseOverlay();
        }

        #endregion
    }
}
