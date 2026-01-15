using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona la visibilidad de la ventana principal durante las operaciones de captura
    /// </summary>
    public class WindowVisibilityManager
    {
        private readonly Window _window;
        private const int HIDE_DELAY_MS = 300;
        private bool _hideOnCapture = true;

        /// <summary>
        /// Inicializa el gestor de visibilidad de ventana
        /// </summary>
        /// <param name="window">La ventana a gestionar</param>
        public WindowVisibilityManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>
        /// Establece si la ventana debe ocultarse durante la captura
        /// </summary>
        /// <param name="hideOnCapture">True para ocultar, False para mantener visible</param>
        public void SetHideOnCapture(bool hideOnCapture)
        {
            _hideOnCapture = hideOnCapture;
        }

        /// <summary>
        /// Obtiene si la ventana debe ocultarse durante la captura
        /// </summary>
        public bool HideOnCapture => _hideOnCapture;

        /// <summary>
        /// Ejecuta una operación de captura, ocultando la ventana si está configurado
        /// </summary>
        /// <param name="captureAction">La acción de captura a ejecutar</param>
        /// <param name="userDelaySeconds">Delay adicional solicitado por el usuario (en segundos)</param>
        /// <returns>Task que completa cuando la captura y restauración de ventana finalizan</returns>
        public async Task ExecuteWithHiddenWindowAsync(Func<Task> captureAction, int userDelaySeconds = 0)
        {
            if (captureAction == null)
                throw new ArgumentNullException(nameof(captureAction));

            // Si no debe ocultarse, ejecutar directamente sin ocultar
            if (!_hideOnCapture)
            {
                // Aplicar el delay configurado por el usuario si es mayor a 0
                if (userDelaySeconds > 0)
                {
                    await Task.Delay(userDelaySeconds * 1000);
                }

                await captureAction();
                return;
            }

            try
            {
                // Ocultar la ventana
                _window.AppWindow.Hide();

                // Dar tiempo para que la ventana se oculte completamente
                await Task.Delay(HIDE_DELAY_MS);

                // Aplicar el delay configurado por el usuario si es mayor a 0
                if (userDelaySeconds > 0)
                {
                    await Task.Delay(userDelaySeconds * 1000);
                }

                // Ejecutar la acción de captura
                await captureAction();
            }
            finally
            {
                // Asegurarse de mostrar la ventana incluso si hay error
                _window.AppWindow.Show();
            }
        }

        /// <summary>
        /// Oculta la ventana con el delay estándar
        /// </summary>
        public async Task HideWindowAsync()
        {
            _window.AppWindow.Hide();
            await Task.Delay(HIDE_DELAY_MS);
        }

        /// <summary>
        /// Muestra la ventana inmediatamente
        /// </summary>
        public void ShowWindow()
        {
            _window.AppWindow.Show();
        }

        /// <summary>
        /// Oculta la ventana sin delay
        /// </summary>
        public void HideWindowImmediate()
        {
            _window.AppWindow.Hide();
        }
    }
}
