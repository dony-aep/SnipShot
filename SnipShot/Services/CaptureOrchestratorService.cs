using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Features.Capture.Modes.FreeForm;
using SnipShot.Features.Capture.Modes.Rectangular;
using SnipShot.Features.Capture.Modes.WindowCapture;
using SnipShot.Features.Capture.Windows;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Helpers.UI;
using SnipShot.Models;

namespace SnipShot.Services
{
    /// <summary>
    /// Orquesta los diferentes modos de captura y gestiona los overlays
    /// </summary>
    public class CaptureOrchestratorService
    {
        private readonly ScreenCaptureService _screenCaptureService;
        private readonly Window _mainWindow;
        private bool _borderEnabled;
        private string _borderColorHex = "#FF000000";
        private double _borderThickness = 1.0;
        private bool _hideOnCapture = true;

        /// <summary>
        /// Evento que se dispara cuando una captura se completa exitosamente
        /// </summary>
        public event EventHandler<SoftwareBitmap>? CaptureCompleted;

        /// <summary>
        /// Evento que se dispara cuando se cancela una captura (cierre sin captura)
        /// </summary>
        public event EventHandler? CaptureCancelled;

        /// <summary>
        /// Evento para aplicar el tema actual a un overlay
        /// </summary>
        public event EventHandler<Window>? ApplyThemeToOverlay;

        /// <summary>
        /// Inicializa el servicio de orquestación de capturas
        /// </summary>
        /// <param name="screenCaptureService">Servicio de captura de pantalla</param>
        /// <param name="mainWindow">Ventana principal de la aplicación</param>
        public CaptureOrchestratorService(ScreenCaptureService screenCaptureService, Window mainWindow)
        {
            _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// Configura los ajustes de borde para las capturas rectangulares
        /// </summary>
        public void SetBorderSettings(bool enabled, string colorHex, double thickness)
        {
            _borderEnabled = enabled;
            _borderColorHex = colorHex;
            _borderThickness = thickness;
        }

        /// <summary>
        /// Configura si se oculta la ventana durante la captura.
        /// Cuando está desactivado, la propia aplicación será capturable.
        /// </summary>
        public void SetHideOnCapture(bool hideOnCapture)
        {
            _hideOnCapture = hideOnCapture;
        }

        /// <summary>
        /// Pre-convierte un bitmap al formato óptimo para visualización (Bgra8, Premultiplied).
        /// Esto evita latencia durante la carga del overlay.
        /// </summary>
        /// <param name="bitmap">Bitmap original a convertir</param>
        /// <returns>Bitmap convertido (o el original si ya estaba en el formato correcto)</returns>
        private static SoftwareBitmap EnsureDisplayFormat(SoftwareBitmap bitmap)
        {
            if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
                bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
            {
                return bitmap;
            }

            var converted = SoftwareBitmap.Convert(
                bitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
            
            bitmap.Dispose();
            return converted;
        }

        /// <summary>
        /// Aplica el borde configurado a un bitmap si está habilitado.
        /// </summary>
        /// <param name="bitmap">Bitmap original</param>
        /// <param name="skipBorder">Si es true, no aplica el borde (para modos que ya lo aplican)</param>
        /// <returns>Bitmap con borde aplicado o el original si no está habilitado</returns>
        private async Task<SoftwareBitmap> ApplyBorderIfEnabledAsync(SoftwareBitmap bitmap, bool skipBorder = false)
        {
            if (!_borderEnabled || skipBorder)
            {
                return bitmap;
            }

            var result = await BorderHelper.ApplyBorderAsync(bitmap, _borderColorHex, _borderThickness);
            return result ?? bitmap;
        }

        /// <summary>
        /// Ejecuta una captura de pantalla completa
        /// </summary>
        public async Task<SoftwareBitmap?> CaptureFullScreenAsync()
        {
            var screenshot = await _screenCaptureService.CaptureFullScreenAsync();
            
            if (screenshot != null)
            {
                // Aplicar borde si está habilitado
                screenshot = await ApplyBorderIfEnabledAsync(screenshot);
                CaptureCompleted?.Invoke(this, screenshot);
            }
            return screenshot;
        }

        /// <summary>
        /// Ejecuta una captura rectangular con overlay de selección
        /// </summary>
        public async Task<SoftwareBitmap?> CaptureRectangularAsync()
        {
            // 1. Capturar la pantalla mientras la ventana principal está cloaked
            var fullScreenBitmap = await _screenCaptureService.CaptureFullScreenAsync();
            
            if (fullScreenBitmap == null)
            {
                return null;
            }

            // Pre-convertir al formato de visualización para evitar latencia en el overlay
            fullScreenBitmap = EnsureDisplayFormat(fullScreenBitmap);

            var virtualBounds = _screenCaptureService.GetVirtualScreenBounds();
            
            SoftwareBitmap? resultBitmap = null;
            var completionSource = new TaskCompletionSource<bool>();

            var overlay = new ShadeOverlayWindow(
                fullScreenBitmap,
                virtualBounds,
                CaptureMode.Rectangular);
            
            overlay.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += (sender, args) =>
            {
                if (args.CapturedBitmap != null)
                {
                    resultBitmap = args.CapturedBitmap;
                    overlay.CloseOverlay();
                    CaptureCompleted?.Invoke(this, resultBitmap);
                    completionSource.TrySetResult(true);
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                completionSource.TrySetResult(false);
            };

            try
            {
                await overlay.WaitForCompletionAsync();
                await completionSource.Task;
                return resultBitmap;
            }
            finally
            {
                overlay.CloseOverlay();
                fullScreenBitmap.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta una captura de ventana con overlay de selección usando ventanas pre-enumeradas.
        /// Usar este método cuando las ventanas deben enumerarse antes de ocultar la aplicación.
        /// </summary>
        /// <param name="availableWindows">Lista de ventanas ya enumeradas</param>
        public async Task<SoftwareBitmap?> CaptureWindowAsync(IReadOnlyList<WindowInfo> availableWindows)
        {
            if (availableWindows.Count == 0)
            {
                return null;
            }

            // 1. Capturar la pantalla mientras la ventana principal está cloaked
            var backgroundBitmap = await _screenCaptureService.CaptureFullScreenAsync();
            
            if (backgroundBitmap == null)
            {
                return null;
            }

            // Pre-convertir al formato de visualización para evitar latencia en el overlay
            backgroundBitmap = EnsureDisplayFormat(backgroundBitmap);

            var virtualBounds = _screenCaptureService.GetVirtualScreenBounds();
            
            SoftwareBitmap? resultBitmap = null;
            var completionSource = new TaskCompletionSource<bool>();

            await OpenWindowOverlayWithResult(backgroundBitmap, virtualBounds, availableWindows, 
                (bitmap) => 
                {
                    resultBitmap = bitmap;
                    completionSource.TrySetResult(true);
                },
                () => completionSource.TrySetResult(false));

            await completionSource.Task;
            return resultBitmap;
        }

        /// <summary>
        /// Ejecuta una captura de ventana con overlay de selección
        /// </summary>
        /// <param name="appWindowHandle">Handle de la ventana de la aplicación para excluirla</param>
        public async Task<SoftwareBitmap?> CaptureWindowAsync(IntPtr appWindowHandle)
        {
            // Siempre incluir la propia ventana de la aplicación como opción capturable
            var availableWindows = WindowEnumerationHelper.GetCaptureableWindows(appWindowHandle, includeOwnWindow: true);

            if (availableWindows.Count == 0)
            {
                return null;
            }

            // 1. Capturar la pantalla mientras la ventana principal está cloaked
            var backgroundBitmap = await _screenCaptureService.CaptureFullScreenAsync();
            
            if (backgroundBitmap == null)
            {
                return null;
            }

            // Pre-convertir al formato de visualización para evitar latencia en el overlay
            backgroundBitmap = EnsureDisplayFormat(backgroundBitmap);

            var virtualBounds = _screenCaptureService.GetVirtualScreenBounds();
            
            SoftwareBitmap? resultBitmap = null;
            var completionSource = new TaskCompletionSource<bool>();

            await OpenWindowOverlayWithResult(backgroundBitmap, virtualBounds, availableWindows, 
                (bitmap) => 
                {
                    resultBitmap = bitmap;
                    completionSource.TrySetResult(true);
                },
                () => completionSource.TrySetResult(false));

            await completionSource.Task;
            return resultBitmap;
        }

        /// <summary>
        /// Abre el overlay de ventana y devuelve el resultado via callbacks
        /// </summary>
        private async Task OpenWindowOverlayWithResult(
            SoftwareBitmap backgroundBitmap, 
            RectInt32 virtualBounds, 
            IReadOnlyList<WindowInfo> availableWindows,
            Action<SoftwareBitmap> onCompleted,
            Action onCancelled)
        {
            var overlay = new ShadeOverlayWindow(
                backgroundBitmap, 
                virtualBounds, 
                CaptureMode.Window, 
                availableWindows);
            
            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += async (sender, args) =>
            {
                if (args.CapturedBitmap != null)
                {
                    // Si viene con bitmap procesado (ej. FullScreen), aplicar borde
                    var bitmapWithBorder = await ApplyBorderIfEnabledAsync(args.CapturedBitmap);
                    overlay.CloseOverlay();
                    CaptureCompleted?.Invoke(this, bitmapWithBorder);
                    onCompleted(bitmapWithBorder);
                }
                else if (args.SelectedRegion.HasValue)
                {
                    overlay.CloseOverlay();
                    await Task.Delay(75);

                    var region = args.SelectedRegion.Value;
                    var screenshot = await _screenCaptureService.CaptureRegionAsync(
                        region.X, region.Y, region.Width, region.Height);

                    if (screenshot != null)
                    {
                        // Aplicar borde a captura de ventana
                        var bitmapWithBorder = await ApplyBorderIfEnabledAsync(screenshot);
                        CaptureCompleted?.Invoke(this, bitmapWithBorder);
                        onCompleted(bitmapWithBorder);
                    }
                    else
                    {
                        onCancelled();
                    }
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                onCancelled();
            };

            try
            {
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta una captura de forma libre con overlay de selección
        /// </summary>
        public async Task<SoftwareBitmap?> CaptureFreeFormAsync()
        {
            // 1. Capturar la pantalla mientras la ventana principal está cloaked
            var backgroundBitmap = await _screenCaptureService.CaptureFullScreenAsync();
            
            if (backgroundBitmap == null)
            {
                return null;
            }

            // Pre-convertir al formato de visualización para evitar latencia en el overlay
            backgroundBitmap = EnsureDisplayFormat(backgroundBitmap);

            var virtualBounds = _screenCaptureService.GetVirtualScreenBounds();
            
            SoftwareBitmap? resultBitmap = null;
            var completionSource = new TaskCompletionSource<bool>();

            await OpenFreeFormOverlayWithResult(backgroundBitmap, virtualBounds,
                (bitmap) =>
                {
                    resultBitmap = bitmap;
                    completionSource.TrySetResult(true);
                },
                () => completionSource.TrySetResult(false));

            await completionSource.Task;
            return resultBitmap;
        }

        /// <summary>
        /// Abre el overlay de forma libre y devuelve el resultado via callbacks
        /// </summary>
        private async Task OpenFreeFormOverlayWithResult(
            SoftwareBitmap backgroundBitmap,
            RectInt32 virtualBounds,
            Action<SoftwareBitmap> onCompleted,
            Action onCancelled)
        {
            var overlay = new ShadeOverlayWindow(
                backgroundBitmap,
                virtualBounds,
                CaptureMode.FreeForm);

            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += async (sender, args) =>
            {
                if (args.CapturedBitmap != null)
                {
                    // Aplicar borde a captura de forma libre
                    var bitmapWithBorder = await ApplyBorderIfEnabledAsync(args.CapturedBitmap);
                    CaptureCompleted?.Invoke(this, bitmapWithBorder);
                    onCompleted(bitmapWithBorder);
                }
                else
                {
                    onCancelled();
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                onCancelled();
            };

            try
            {
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap.Dispose();
            }
        }

        /// <summary>
        /// Abre el overlay rectangular usando ShadeOverlayWindow
        /// </summary>
        private async Task OpenRectangularOverlay(SoftwareBitmap? backgroundBitmap, RectInt32 virtualBounds)
        {
            if (backgroundBitmap == null)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            var overlay = new ShadeOverlayWindow(
                backgroundBitmap,
                virtualBounds,
                CaptureMode.Rectangular);
            
            overlay.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += (sender, args) =>
            {
                // El bitmap ya viene con las anotaciones renderizadas desde RectangularCaptureControl
                if (args.CapturedBitmap != null)
                {
                    overlay.CloseOverlay();
                    CaptureCompleted?.Invoke(this, args.CapturedBitmap);
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            };

            try
            {
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap.Dispose();
            }
        }

        /// <summary>
        /// Abre el overlay de selección de ventana usando ShadeOverlayWindow
        /// </summary>
        private async Task OpenWindowOverlay(SoftwareBitmap? backgroundBitmap, RectInt32 virtualBounds, IReadOnlyList<WindowInfo>? availableWindows)
        {
            if (backgroundBitmap == null)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (availableWindows == null || availableWindows.Count == 0)
            {
                var appWindowHandle = Microsoft.UI.Win32Interop.GetWindowFromWindowId(_mainWindow.AppWindow.Id);
                // Siempre incluir la propia ventana de la aplicación como opción capturable
                availableWindows = WindowEnumerationHelper.GetCaptureableWindows(appWindowHandle, includeOwnWindow: true);
            }

            if (availableWindows.Count == 0)
            {
                backgroundBitmap.Dispose();
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            var overlay = new ShadeOverlayWindow(
                backgroundBitmap, 
                virtualBounds, 
                CaptureMode.Window, 
                availableWindows);
            
            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += async (sender, args) =>
            {
                if (args.CapturedBitmap != null)
                {
                    // Si viene con bitmap procesado (ej. FullScreen), aplicar borde
                    var bitmapWithBorder = await ApplyBorderIfEnabledAsync(args.CapturedBitmap);
                    overlay.CloseOverlay();
                    CaptureCompleted?.Invoke(this, bitmapWithBorder);
                }
                else if (args.SelectedRegion.HasValue)
                {
                    var selectedRegion = args.SelectedRegion.Value;
                    overlay.CloseOverlay();
                    await Task.Delay(75);

                    var screenshot = await _screenCaptureService.CaptureRegionAsync(
                        selectedRegion.X, 
                        selectedRegion.Y, 
                        selectedRegion.Width, 
                        selectedRegion.Height);

                    if (screenshot != null)
                    {
                        // Aplicar borde a captura de ventana
                        var bitmapWithBorder = await ApplyBorderIfEnabledAsync(screenshot);
                        CaptureCompleted?.Invoke(this, bitmapWithBorder);
                    }
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            };

            try
            {
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap.Dispose();
            }
        }

        /// <summary>
        /// Abre el overlay de forma libre usando ShadeOverlayWindow
        /// </summary>
        private async Task OpenFreeFormOverlay(SoftwareBitmap? backgroundBitmap, RectInt32 virtualBounds)
        {
            if (backgroundBitmap == null)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            var overlay = new ShadeOverlayWindow(
                backgroundBitmap, 
                virtualBounds, 
                CaptureMode.FreeForm);
            
            ApplyThemeToOverlay?.Invoke(this, overlay);

            overlay.CaptureCompleted += async (sender, args) =>
            {
                // El FreeFormCaptureControl ya crea el bitmap enmascarado
                if (args.CapturedBitmap != null)
                {
                    // Aplicar borde a captura de forma libre
                    var bitmapWithBorder = await ApplyBorderIfEnabledAsync(args.CapturedBitmap);
                    CaptureCompleted?.Invoke(this, bitmapWithBorder);
                }
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            };

            try
            {
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap.Dispose();
            }
        }

        /// <summary>
        /// Abre el overlay de selector de color usando el nuevo ShadeOverlayWindow
        /// </summary>
        private async Task OpenColorPickerOverlay(SoftwareBitmap? backgroundBitmap, RectInt32 virtualBounds)
        {
            if (backgroundBitmap == null)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Usar el nuevo ShadeOverlayWindow con el modo ColorPicker
            var overlay = new ShadeOverlayWindow(
                backgroundBitmap, 
                virtualBounds, 
                CaptureMode.ColorPicker);
            
            ApplyThemeToOverlay?.Invoke(this, overlay);

            // Suscribirse a eventos del nuevo overlay
            overlay.CaptureCompleted += (sender, args) =>
            {
                // ColorPicker no produce bitmap, solo copia color al portapapeles
                // El color ya fue copiado por el control, no hacemos nada adicional
            };

            overlay.CaptureCancelled += (sender, args) =>
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            };

            try
            {
                // Esperar a que el overlay complete o cancele
                await overlay.WaitForCompletionAsync();
            }
            finally
            {
                overlay.CloseOverlay();
                backgroundBitmap?.Dispose();
            }
        }
    }
}
