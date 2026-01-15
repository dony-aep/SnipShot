using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Gestiona la visibilidad de paneles y controles de UI en la ventana principal
    /// </summary>
    public class UIStateManager
    {
        private readonly FrameworkElement _mainPanel;
        private readonly FrameworkElement _settingsView;
        private readonly FrameworkElement _zoomSubmenu;
        private readonly TeachingTip _windowCaptureTeachingTip;
        private readonly FrameworkElement _newCaptureButton;

        /// <summary>
        /// Evento que se dispara cuando se muestra el panel de configuración
        /// </summary>
        public event EventHandler? SettingsPanelShown;

        /// <summary>
        /// Evento que se dispara cuando se muestra el panel principal
        /// </summary>
        public event EventHandler? MainPanelShown;

        /// <summary>
        /// Evento que se dispara cuando se muestran los controles de zoom
        /// </summary>
        public event EventHandler? ZoomControlsShown;

        /// <summary>
        /// Evento que se dispara cuando se ocultan los controles de zoom
        /// </summary>
        public event EventHandler? ZoomControlsHidden;

        /// <summary>
        /// Inicializa el gestor de estado de UI
        /// </summary>
        /// <param name="mainPanel">Panel principal de la aplicación</param>
        /// <param name="settingsView">Panel de configuración</param>
        /// <param name="zoomSubmenu">Submenú de controles de zoom</param>
        /// <param name="windowCaptureTeachingTip">TeachingTip para mensajes de captura de ventana</param>
        /// <param name="newCaptureButton">Botón de nueva captura (para target del TeachingTip)</param>
        public UIStateManager(
            FrameworkElement mainPanel,
            FrameworkElement settingsView,
            FrameworkElement zoomSubmenu,
            TeachingTip windowCaptureTeachingTip,
            FrameworkElement newCaptureButton)
        {
            _mainPanel = mainPanel ?? throw new ArgumentNullException(nameof(mainPanel));
            _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
            _zoomSubmenu = zoomSubmenu ?? throw new ArgumentNullException(nameof(zoomSubmenu));
            _windowCaptureTeachingTip = windowCaptureTeachingTip ?? throw new ArgumentNullException(nameof(windowCaptureTeachingTip));
            _newCaptureButton = newCaptureButton ?? throw new ArgumentNullException(nameof(newCaptureButton));
        }

        /// <summary>
        /// Muestra el panel de configuración y oculta el panel principal
        /// </summary>
        public void ShowSettingsPanel()
        {
            _mainPanel.Visibility = Visibility.Collapsed;
            _settingsView.Visibility = Visibility.Visible;
            SettingsPanelShown?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Muestra el panel principal y oculta el panel de configuración
        /// </summary>
        public void ShowMainPanel()
        {
            _settingsView.Visibility = Visibility.Collapsed;
            _mainPanel.Visibility = Visibility.Visible;
            MainPanelShown?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Muestra los controles de zoom
        /// </summary>
        public void ShowZoomControls()
        {
            _zoomSubmenu.Visibility = Visibility.Visible;
            ZoomControlsShown?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Oculta los controles de zoom
        /// </summary>
        public void HideZoomControls()
        {
            _zoomSubmenu.Visibility = Visibility.Collapsed;
            ZoomControlsHidden?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Muestra un mensaje de tip para captura de ventana
        /// </summary>
        /// <param name="message">Mensaje a mostrar</param>
        public void ShowWindowCaptureTip(string message)
        {
            if (_windowCaptureTeachingTip.Target == null)
            {
                _windowCaptureTeachingTip.Target = _newCaptureButton;
            }

            _windowCaptureTeachingTip.Subtitle = message;
            _windowCaptureTeachingTip.IsOpen = true;
        }

        /// <summary>
        /// Oculta el mensaje de tip para captura de ventana
        /// </summary>
        public void HideWindowCaptureTip()
        {
            if (_windowCaptureTeachingTip.IsOpen)
            {
                _windowCaptureTeachingTip.IsOpen = false;
            }
        }

        /// <summary>
        /// Indica si el panel de configuración está visible
        /// </summary>
        public bool IsSettingsPanelVisible => _settingsView.Visibility == Visibility.Visible;

        /// <summary>
        /// Indica si el panel principal está visible
        /// </summary>
        public bool IsMainPanelVisible => _mainPanel.Visibility == Visibility.Visible;

        /// <summary>
        /// Indica si los controles de zoom están visibles
        /// </summary>
        public bool AreZoomControlsVisible => _zoomSubmenu.Visibility == Visibility.Visible;

        /// <summary>
        /// Indica si el tip de captura de ventana está visible
        /// </summary>
        public bool IsWindowCaptureTipVisible => _windowCaptureTeachingTip.IsOpen;
    }
}
