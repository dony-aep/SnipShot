using System;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Models;

namespace SnipShot.Features.Capture.Modes.Base
{
    /// <summary>
    /// Tipos de modo de captura disponibles
    /// </summary>
    public enum CaptureMode
    {
        Rectangular,
        FreeForm,
        Window,
        ColorPicker,
        FullScreen
    }

    /// <summary>
    /// Argumentos para el evento de cambio de modo
    /// </summary>
    public class ModeChangeEventArgs : EventArgs
    {
        /// <summary>
        /// El nuevo modo solicitado
        /// </summary>
        public CaptureMode NewMode { get; set; }

        /// <summary>
        /// Modo anterior (para poder regresar)
        /// </summary>
        public CaptureMode? PreviousMode { get; set; }
    }

    /// <summary>
    /// Argumentos para el evento de captura completada
    /// </summary>
    public class CaptureCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// El bitmap capturado (puede ser null para ColorPicker)
        /// </summary>
        public SoftwareBitmap? CapturedBitmap { get; set; }

        /// <summary>
        /// Región seleccionada en coordenadas de pantalla
        /// </summary>
        public RectInt32? SelectedRegion { get; set; }

        /// <summary>
        /// Información del color capturado (solo para ColorPicker)
        /// </summary>
        public ColorInfo? ColorInfo { get; set; }
    }

    /// <summary>
    /// Argumentos para el evento de visibilidad de shades locales
    /// </summary>
    public class LocalShadesVisibilityEventArgs : EventArgs
    {
        /// <summary>
        /// Indica si los shades locales del modo están visibles
        /// </summary>
        public bool AreLocalShadesVisible { get; set; }
    }

    /// <summary>
    /// Interface que define el contrato para todos los modos de captura.
    /// Los modos se cargan como UserControls dentro del ShadeOverlayWindow.
    /// </summary>
    public interface ICaptureMode
    {
        /// <summary>
        /// Se dispara cuando el usuario completa una captura exitosamente
        /// </summary>
        event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

        /// <summary>
        /// Se dispara cuando el usuario cancela la captura (Escape, clic derecho, etc.)
        /// </summary>
        event EventHandler? CaptureCancelled;

        /// <summary>
        /// Se dispara cuando el usuario solicita cambiar a otro modo desde el toolbar
        /// </summary>
        event EventHandler<ModeChangeEventArgs>? ModeChangeRequested;

        /// <summary>
        /// Se dispara cuando cambia la visibilidad de los shades locales del modo.
        /// El ShadeOverlayWindow debe ocultar su shade global cuando los shades locales están visibles.
        /// </summary>
        event EventHandler<LocalShadesVisibilityEventArgs>? LocalShadesVisibilityChanged;

        /// <summary>
        /// Tipo de modo actual
        /// </summary>
        CaptureMode Mode { get; }

        /// <summary>
        /// Indica si el modo está activo y procesando input
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Inicializa el modo con el bitmap de fondo y los límites virtuales.
        /// Se llama cuando el modo se carga en el ShadeOverlayWindow.
        /// </summary>
        /// <param name="backgroundBitmap">Bitmap de la captura de pantalla completa</param>
        /// <param name="virtualBounds">Límites de la pantalla virtual (multi-monitor)</param>
        /// <param name="availableWindows">Lista de ventanas disponibles (solo para WindowCapture)</param>
        void Initialize(SoftwareBitmap backgroundBitmap, RectInt32 virtualBounds, IReadOnlyList<WindowInfo>? availableWindows = null);

        /// <summary>
        /// Activa el modo para comenzar a procesar input del usuario.
        /// Se llama cuando el modo se vuelve visible.
        /// </summary>
        void Activate();

        /// <summary>
        /// Desactiva el modo temporalmente (cuando se cambia a otro modo).
        /// El estado se preserva para poder regresar.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Limpia los recursos del modo cuando se cierra definitivamente.
        /// </summary>
        void Cleanup();
    }
}
