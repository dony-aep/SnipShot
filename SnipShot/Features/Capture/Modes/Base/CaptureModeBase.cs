using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Models;
using WinRT.Interop;

namespace SnipShot.Features.Capture.Modes.Base
{
    /// <summary>
    /// Clase base abstracta para todos los modos de captura.
    /// Proporciona implementación común de ICaptureMode.
    /// </summary>
    public abstract class CaptureModeBase : UserControl, ICaptureMode
    {
        /// <inheritdoc/>
        public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

        /// <inheritdoc/>
        public event EventHandler? CaptureCancelled;

        /// <inheritdoc/>
        public event EventHandler<ModeChangeEventArgs>? ModeChangeRequested;

        /// <inheritdoc/>
        public event EventHandler<LocalShadesVisibilityEventArgs>? LocalShadesVisibilityChanged;

        /// <inheritdoc/>
        public abstract CaptureMode Mode { get; }

        /// <inheritdoc/>
        public bool IsActive { get; protected set; }

        /// <summary>
        /// Bitmap de fondo (captura de pantalla completa)
        /// </summary>
        protected SoftwareBitmap? BackgroundBitmap { get; private set; }

        /// <summary>
        /// Límites de la pantalla virtual (multi-monitor)
        /// </summary>
        protected RectInt32 VirtualBounds { get; private set; }

        /// <summary>
        /// Lista de ventanas disponibles (solo para WindowCapture)
        /// </summary>
        protected IReadOnlyList<WindowInfo>? AvailableWindows { get; private set; }

        /// <summary>
        /// Indica si el modo ha sido inicializado
        /// </summary>
        protected bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public virtual void Initialize(SoftwareBitmap backgroundBitmap, RectInt32 virtualBounds, IReadOnlyList<WindowInfo>? availableWindows = null)
        {
            BackgroundBitmap = backgroundBitmap;
            VirtualBounds = virtualBounds;
            AvailableWindows = availableWindows;
            IsInitialized = true;

            OnInitialized();
        }

        /// <inheritdoc/>
        public virtual void Activate()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("El modo debe ser inicializado antes de activarse.");
            }

            IsActive = true;
            OnActivated();
        }

        /// <inheritdoc/>
        public virtual void Deactivate()
        {
            IsActive = false;
            OnDeactivated();
        }

        /// <inheritdoc/>
        public virtual void Cleanup()
        {
            IsActive = false;
            IsInitialized = false;
            OnCleanup();
        }

        /// <summary>
        /// Se llama cuando el modo ha sido inicializado.
        /// Override en clases derivadas para configuración inicial.
        /// </summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// Se llama cuando el modo se activa.
        /// Override en clases derivadas para iniciar procesamiento de input.
        /// </summary>
        protected virtual void OnActivated() { }

        /// <summary>
        /// Se llama cuando el modo se desactiva.
        /// Override en clases derivadas para pausar procesamiento.
        /// </summary>
        protected virtual void OnDeactivated() { }

        /// <summary>
        /// Se llama cuando el modo se limpia.
        /// Override en clases derivadas para liberar recursos.
        /// </summary>
        protected virtual void OnCleanup() { }

        /// <summary>
        /// Invoca el evento CaptureCompleted
        /// </summary>
        protected void RaiseCaptureCompleted(CaptureCompletedEventArgs args)
        {
            CaptureCompleted?.Invoke(this, args);
        }

        /// <summary>
        /// Invoca el evento CaptureCompleted con un bitmap
        /// </summary>
        protected void RaiseCaptureCompleted(SoftwareBitmap? bitmap, RectInt32? region = null)
        {
            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs
            {
                CapturedBitmap = bitmap,
                SelectedRegion = region
            });
        }

        /// <summary>
        /// Invoca el evento CaptureCancelled
        /// </summary>
        protected void RaiseCaptureCancelled()
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invoca el evento ModeChangeRequested
        /// </summary>
        protected void RaiseModeChangeRequested(CaptureMode newMode, CaptureMode? previousMode = null)
        {
            ModeChangeRequested?.Invoke(this, new ModeChangeEventArgs
            {
                NewMode = newMode,
                PreviousMode = previousMode ?? Mode
            });
        }

        /// <summary>
        /// Invoca el evento LocalShadesVisibilityChanged.
        /// Llamar cuando el modo muestra u oculta sus shades locales.
        /// </summary>
        protected void RaiseLocalShadesVisibilityChanged(bool areVisible)
        {
            LocalShadesVisibilityChanged?.Invoke(this, new LocalShadesVisibilityEventArgs
            {
                AreLocalShadesVisible = areVisible
            });
        }

        /// <summary>
        /// Obtiene el handle de la ventana contenedora.
        /// Útil para diálogos y pickers.
        /// </summary>
        protected IntPtr GetWindowHandle()
        {
            try
            {
                // En WinUI 3, obtenemos el hwnd desde el WindowId del XamlRoot
                if (XamlRoot != null)
                {
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                        Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).Count > 0
                            ? IntPtr.Zero
                            : IntPtr.Zero);
                    
                    // Fallback: usar GetActiveWindow
                    return GetActiveWindow();
                }
            }
            catch
            {
                // Fallback silencioso
            }
            
            return IntPtr.Zero;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
    }
}
