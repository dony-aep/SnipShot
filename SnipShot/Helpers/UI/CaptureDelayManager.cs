using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona el delay antes de las capturas y la actualización de la UI
    /// </summary>
    public class CaptureDelayManager
    {
        private readonly TextBlock _delayOptionText;
        private readonly ButtonBase _delayOptionsButton;
        private int _delaySeconds = 0;

        /// <summary>
        /// Obtiene el delay actual en segundos
        /// </summary>
        public int DelaySeconds => _delaySeconds;

        /// <summary>
        /// Evento que se dispara cuando cambia el delay
        /// </summary>
        public event EventHandler<int>? DelayChanged;

        /// <summary>
        /// Inicializa el gestor de delay de captura
        /// </summary>
        /// <param name="delayOptionText">TextBlock que muestra el delay actual</param>
        /// <param name="delayOptionsButton">Botón que contiene el texto</param>
        public CaptureDelayManager(TextBlock delayOptionText, ButtonBase delayOptionsButton)
        {
            _delayOptionText = delayOptionText ?? throw new ArgumentNullException(nameof(delayOptionText));
            _delayOptionsButton = delayOptionsButton ?? throw new ArgumentNullException(nameof(delayOptionsButton));
        }

        /// <summary>
        /// Establece el delay antes de capturar y actualiza la UI
        /// </summary>
        /// <param name="seconds">Segundos de espera</param>
        public void SetDelay(int seconds)
        {
            string tooltip = seconds == 0
                ? "Sin delay"
                : $"{seconds} segundos de delay";

            _delaySeconds = seconds;
            UpdateDelayButton($"{seconds}s", tooltip);
            DelayChanged?.Invoke(this, seconds);
        }

        /// <summary>
        /// Actualiza el botón de delay con el texto y tooltip correspondientes
        /// </summary>
        private void UpdateDelayButton(string text, string tooltip)
        {
            _delayOptionText.Text = text;
            ToolTipService.SetToolTip(_delayOptionsButton, tooltip);
            // El botón muestra el número sin unidades. Sin esto el lector de pantalla
            // anunciaría el valor inicial del XAML en vez del delay activo.
            AutomationProperties.SetName(_delayOptionsButton, tooltip);
        }
    }
}
