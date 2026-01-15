using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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
        /// Establece delay de 0 segundos (sin delay)
        /// </summary>
        public void SetNoDelay()
        {
            SetDelay(0, "0s", "Sin delay");
        }

        /// <summary>
        /// Establece delay de 3 segundos
        /// </summary>
        public void SetDelay3Seconds()
        {
            SetDelay(3, "3s", "3 segundos de delay");
        }

        /// <summary>
        /// Establece delay de 5 segundos
        /// </summary>
        public void SetDelay5Seconds()
        {
            SetDelay(5, "5s", "5 segundos de delay");
        }

        /// <summary>
        /// Establece delay de 10 segundos
        /// </summary>
        public void SetDelay10Seconds()
        {
            SetDelay(10, "10s", "10 segundos de delay");
        }

        /// <summary>
        /// Establece el delay y actualiza la UI
        /// </summary>
        private void SetDelay(int seconds, string text, string tooltip)
        {
            _delaySeconds = seconds;
            UpdateDelayButton(text, tooltip);
            DelayChanged?.Invoke(this, seconds);
        }

        /// <summary>
        /// Actualiza el botón de delay con el texto y tooltip correspondientes
        /// </summary>
        private void UpdateDelayButton(string text, string tooltip)
        {
            _delayOptionText.Text = text;
            ToolTipService.SetToolTip(_delayOptionsButton, tooltip);
        }
    }
}
