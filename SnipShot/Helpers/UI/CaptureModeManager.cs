using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona la selección del modo de captura y la actualización de la UI
    /// </summary>
    public class CaptureModeManager
    {
        private readonly FontIcon _captureOptionIcon;
        private readonly ButtonBase _captureOptionsButton;
        private string _currentMode = "Rectangular";

        /// <summary>
        /// Obtiene el modo de captura actual
        /// </summary>
        public string CurrentMode => _currentMode;

        /// <summary>
        /// Evento que se dispara cuando cambia el modo de captura
        /// </summary>
        public event EventHandler<string>? CaptureModeChanged;

        /// <summary>
        /// Inicializa el gestor de modo de captura
        /// </summary>
        /// <param name="captureOptionIcon">Icono que muestra el modo actual</param>
        /// <param name="captureOptionsButton">Botón que contiene el icono</param>
        public CaptureModeManager(FontIcon captureOptionIcon, ButtonBase captureOptionsButton)
        {
            _captureOptionIcon = captureOptionIcon ?? throw new ArgumentNullException(nameof(captureOptionIcon));
            _captureOptionsButton = captureOptionsButton ?? throw new ArgumentNullException(nameof(captureOptionsButton));
        }

        /// <summary>
        /// Establece el modo de captura activo y actualiza la UI.
        /// Los glifos deben coincidir con los de la lista en MainWindow.xaml.
        /// </summary>
        /// <param name="mode">Modo tal cual lo espera CaptureScreenAsync</param>
        public void SetMode(string mode)
        {
            (string glyph, string tooltip) = mode switch
            {
                "Rectangular" => ("\uF407", "Captura Rectangular"),
                "Ventana" => ("\uF7ED", "Captura de Ventana"),
                "Pantalla Completa" => ("\uE9A6", "Pantalla Completa"),
                "Forma Libre" => ("\uF408", "Forma Libre"),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Modo '{mode}' desconocido.")
            };

            _currentMode = mode;
            UpdateCaptureOptionButton(glyph, tooltip);
            CaptureModeChanged?.Invoke(this, mode);
        }

        /// <summary>
        /// Actualiza el botón de opciones de captura con el icono y tooltip correspondientes
        /// </summary>
        private void UpdateCaptureOptionButton(string iconGlyph, string tooltip)
        {
            _captureOptionIcon.Glyph = iconGlyph;
            ToolTipService.SetToolTip(_captureOptionsButton, tooltip);
            // El botón es solo icono. Sin esto el lector de pantalla anunciaría
            // el valor inicial del XAML en vez del modo de captura activo.
            AutomationProperties.SetName(_captureOptionsButton, tooltip);
        }
    }
}
