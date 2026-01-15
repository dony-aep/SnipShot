using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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
        /// Establece el modo de captura rectangular
        /// </summary>
        public void SetRectangularMode()
        {
            SetMode("Rectangular", "&#xF407;", "Captura Rectangular");
        }

        /// <summary>
        /// Establece el modo de captura de ventana
        /// </summary>
        public void SetWindowMode()
        {
            SetMode("Ventana", "&#xF7ED;", "Captura de Ventana");
        }

        /// <summary>
        /// Establece el modo de captura de pantalla completa
        /// </summary>
        public void SetFullScreenMode()
        {
            SetMode("Pantalla Completa", "&#xE9A6;", "Pantalla Completa");
        }

        /// <summary>
        /// Establece el modo de captura de forma libre
        /// </summary>
        public void SetFreeFormMode()
        {
            SetMode("Forma Libre", "&#xF408;", "Forma Libre");
        }

        /// <summary>
        /// Establece el modo de captura y actualiza la UI
        /// </summary>
        private void SetMode(string mode, string iconGlyph, string tooltip)
        {
            _currentMode = mode;
            UpdateCaptureOptionButton(iconGlyph, tooltip);
            CaptureModeChanged?.Invoke(this, mode);
        }

        /// <summary>
        /// Actualiza el botón de opciones de captura con el icono y tooltip correspondientes
        /// </summary>
        private void UpdateCaptureOptionButton(string iconGlyph, string tooltip)
        {
            // Remover los caracteres HTML entity (&#x y ;)
            var glyphCode = iconGlyph.Replace("&#x", "").Replace(";", "");
            var glyphChar = (char)Convert.ToInt32(glyphCode, 16);
            _captureOptionIcon.Glyph = glyphChar.ToString();
            ToolTipService.SetToolTip(_captureOptionsButton, tooltip);
        }
    }
}
