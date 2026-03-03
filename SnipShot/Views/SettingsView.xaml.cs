using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Services;

// Alias para evitar conflictos entre Windows.UI.Color y System.Drawing.Color
using Color = Windows.UI.Color;

namespace SnipShot.Views
{
    /// <summary>
    /// Control de vista de configuración de la aplicación.
    /// Maneja la interfaz y eventos relacionados con las preferencias del usuario.
    /// </summary>
    public sealed partial class SettingsView : UserControl
    {
        /// <summary>
        /// Se dispara cuando el usuario presiona el
        /// </summary>
        public event EventHandler? BackRequested;

        /// <summary>
        /// Se dispara cuando el usuario cambia el tema de la aplicación.
        /// El argumento contiene el tag del tema seleccionado ("Light", "Dark", "Default").
        /// </summary>
        public event EventHandler<string>? ThemeChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de guardado automático.
        /// </summary>
        public event EventHandler<bool>? AutoSavePreferenceChanged;

        /// <summary>
        /// Se dispara cuando el usuario solicita
        /// </summary>
        public event EventHandler? AutoSaveOpenFolderRequested;

        /// <summary>
        /// Se dispara cuando el usuario desea cambiar la ruta de guardado automático.
        /// </summary>
        public event EventHandler? AutoSaveChangeFolderRequested;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de borde.
        /// </summary>
        public event EventHandler<bool>? BorderEnabledChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia el color del borde.
        /// </summary>
        public event EventHandler<string>? BorderColorChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia el grosor del borde.
        /// </summary>
        public event EventHandler<double>? BorderThicknessChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de ocultar al capturar.
        /// </summary>
        public event EventHandler<bool>? HideOnCaptureChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de confirmar al eliminar.
        /// </summary>
        public event EventHandler<bool>? ConfirmDeleteCaptureChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia del hotkey Print Screen.
        /// </summary>
        public event EventHandler<bool>? PrintScreenHotkeyChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia del hotkey Ctrl+Shift+S.
        /// </summary>
        public event EventHandler<bool>? CtrlShiftSHotkeyChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de minimizar a la bandeja.
        /// </summary>
        public event EventHandler<bool>? MinimizeToTrayChanged;

        /// <summary>
        /// Se dispara cuando el usuario cambia la preferencia de iniciar con Windows.
        /// </summary>
        public event EventHandler<bool>? StartWithWindowsChanged;

        private string _currentThemeTag = "Default";
        private bool _autoSaveEnabled;
        private string _autoSaveFolderPathDisplay = string.Empty;
        private bool _borderEnabled;
        private string _borderColorHex = "#FF000000";
        private double _borderThickness = 1.0;
        private bool _hideOnCapture = true;
        private bool _confirmDeleteCapture = true;
        private bool _printScreenHotkeyEnabled;
        private bool _ctrlShiftSHotkeyEnabled;
        private bool _minimizeToTrayEnabled = true;
        private bool _startWithWindowsEnabled;
        private DialogService? _dialogService;

        /// <summary>
        /// Propiedad para acceder al DialogService con inicialización lazy.
        /// </summary>
        private DialogService DialogService
        {
            get
            {
                if (_dialogService == null && this.XamlRoot != null)
                {
                    _dialogService = new DialogService(this.XamlRoot);
                }
                return _dialogService!;
            }
        }

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            SyncAllToggles();
            UpdateAboutSection();
        }

        /// <summary>
        /// Sincroniza todos los toggles con sus valores guardados.
        /// Llamar este método cuando el SettingsView se hace visible.
        /// </summary>
        public void SyncAllToggles()
        {
            SetAutoSaveFolderPath(_autoSaveFolderPathDisplay);
            UpdateAboutSection();
            
            // Aplicar valores guardados a los toggles de hotkeys
            if (PrintScreenHotkeyToggle != null)
            {
                ControlStateManager.SetToggleSilently(PrintScreenHotkeyToggle, _printScreenHotkeyEnabled, PrintScreenHotkeyToggle_Toggled);
            }
            if (CtrlShiftSHotkeyToggle != null)
            {
                ControlStateManager.SetToggleSilently(CtrlShiftSHotkeyToggle, _ctrlShiftSHotkeyEnabled, CtrlShiftSHotkeyToggle_Toggled);
            }
            
            // Aplicar valores guardados a los toggles de System Tray
            if (MinimizeToTrayToggle != null)
            {
                ControlStateManager.SetToggleSilently(MinimizeToTrayToggle, _minimizeToTrayEnabled, MinimizeToTrayToggle_Toggled);
            }
            if (StartWithWindowsToggle != null)
            {
                ControlStateManager.SetToggleSilently(StartWithWindowsToggle, _startWithWindowsEnabled, StartWithWindowsToggle_Toggled);
            }
        }

        /// <summary>
        /// Actualiza de forma dinamica los datos de la seccion Acerca de.
        /// </summary>
        private void UpdateAboutSection()
        {
            if (VersionText != null)
            {
                VersionText.Text = $"SnipShot v{UpdateService.CurrentVersion}";
            }

            if (CopyrightText != null)
            {
                CopyrightText.Text = $"© {DateTime.Now.Year} SnipShot. Todos los derechos reservados.";
            }
        }

        /// <summary>
        /// Establece el tema actual en la UI.
        /// </summary>
        /// <param name="themeTag">Tag del tema: "Light", "Dark", o "Default"</param>
        public void SetCurrentTheme(string themeTag)
        {
            _currentThemeTag = themeTag;
            UpdateThemeDropdownText(themeTag);
            
            // Deseleccionar todos primero
            foreach (RadioButton radioButton in ThemeRadioButtons.Items)
            {
                radioButton.IsChecked = false;
            }
            
            // Seleccionar el RadioButton correcto
            foreach (RadioButton radioButton in ThemeRadioButtons.Items)
            {
                if (radioButton.Tag?.ToString() == themeTag)
                {
                    radioButton.IsChecked = true;
                    break;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeRadioButtons.SelectedItem is RadioButton selectedButton)
            {
                var themeTag = selectedButton.Tag?.ToString() ?? "Default";
                _currentThemeTag = themeTag;
                UpdateThemeDropdownText(themeTag);
                ThemeChanged?.Invoke(this, themeTag);
            }
        }

        /// <summary>
        /// Establece el valor inicial del guardado automático sin disparar eventos.
        /// </summary>
        public void SetAutoSavePreference(bool enabled)
        {
            _autoSaveEnabled = enabled;
            if (AutoSaveToggle != null)
            {
                ControlStateManager.SetToggleSilently(AutoSaveToggle, enabled, AutoSaveToggle_Toggled);
            }
        }

        /// <summary>
        /// Actualiza la ruta mostrada para el guardado automático.
        /// </summary>
        public void SetAutoSaveFolderPath(string path)
        {
            _autoSaveFolderPathDisplay = path;
            if (AutoSavePathText != null)
            {
                AutoSavePathText.Text = path;
            }
        }

        private void UpdateThemeDropdownText(string theme)
        {
            ThemeSelectionText.Text = ThemeHelper.ThemeTagToDisplayName(theme);
        }

        private void AutoSaveToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (AutoSaveToggle == null)
                return;

            var newValue = AutoSaveToggle.IsOn;
            if (newValue == _autoSaveEnabled)
                return;

            _autoSaveEnabled = newValue;
            AutoSavePreferenceChanged?.Invoke(this, newValue);
        }

        /// <summary>
        /// Establece el valor inicial de ocultar al capturar sin disparar eventos.
        /// </summary>
        public void SetHideOnCapturePreference(bool enabled)
        {
            _hideOnCapture = enabled;
            if (HideOnCaptureToggle != null)
            {
                ControlStateManager.SetToggleSilently(HideOnCaptureToggle, enabled, HideOnCaptureToggle_Toggled);
            }
        }

        /// <summary>
        /// Establece el valor inicial de confirmar al eliminar sin disparar eventos.
        /// </summary>
        public void SetConfirmDeleteCapturePreference(bool enabled)
        {
            _confirmDeleteCapture = enabled;
            if (ConfirmDeleteToggle != null)
            {
                ControlStateManager.SetToggleSilently(ConfirmDeleteToggle, enabled, ConfirmDeleteToggle_Toggled);
            }
        }

        private void HideOnCaptureToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (HideOnCaptureToggle == null)
                return;

            var newValue = HideOnCaptureToggle.IsOn;
            if (newValue == _hideOnCapture)
                return;

            _hideOnCapture = newValue;
            HideOnCaptureChanged?.Invoke(this, newValue);
        }

        private void ConfirmDeleteToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (ConfirmDeleteToggle == null)
            {
                return;
            }

            var newValue = ConfirmDeleteToggle.IsOn;
            if (newValue == _confirmDeleteCapture)
            {
                return;
            }

            _confirmDeleteCapture = newValue;
            ConfirmDeleteCaptureChanged?.Invoke(this, newValue);
        }

        private void OpenAutoSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            AutoSaveOpenFolderRequested?.Invoke(this, EventArgs.Empty);
            // Force text refresh after folder operation
            if (AutoSavePathText != null)
            {
                AutoSavePathText.Text = _autoSaveFolderPathDisplay;
            }
        }

        private void ChangeAutoSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            AutoSaveChangeFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Establece las preferencias de borde iniciales.
        /// </summary>
        public void SetBorderPreference(bool enabled, string colorHex, double thickness)
        {
            _borderEnabled = enabled;
            _borderColorHex = colorHex;
            _borderThickness = thickness;

            if (BorderToggle != null)
            {
                ControlStateManager.SetToggleSilently(BorderToggle, enabled, BorderToggle_Toggled);
            }

            if (BorderThicknessSlider != null)
            {
                ControlStateManager.SetSliderValueSilently(BorderThicknessSlider, thickness, BorderThicknessSlider_ValueChanged);
            }

            if (ColorConverter.TryParseHexColor(colorHex, out var color))
            {
                if (BorderColorPreview != null)
                {
                    BorderColorPreview.Background = ColorConverter.CreateBrush(color);
                }
                
                if (BorderPreviewElement != null)
                {
                    BorderPreviewElement.BorderBrush = ColorConverter.CreateBrush(color);
                    BorderPreviewElement.BorderThickness = new Thickness(thickness);
                }
            }
        }

        private void BorderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (BorderToggle == null) return;

            var newValue = BorderToggle.IsOn;
            if (newValue == _borderEnabled) return;

            _borderEnabled = newValue;
            BorderEnabledChanged?.Invoke(this, newValue);
        }

        private async void BorderColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Obtener color inicial
            if (!ColorConverter.TryParseHexColor(_borderColorHex, out var initialColor))
            {
                initialColor = Color.FromArgb(255, 0, 0, 0); // Negro
            }

            // Mostrar selector de color
            var selectedColor = await DialogService.ShowColorPickerAsync(
                initialColor,
                "Seleccionar color del borde",
                enableAlpha: true);

            if (selectedColor.HasValue)
            {
                var newColor = selectedColor.Value;
                var hex = ColorConverter.ColorToHex(newColor);
                
                if (BorderColorPreview != null)
                {
                    BorderColorPreview.Background = ColorConverter.CreateBrush(newColor);
                }

                if (BorderPreviewElement != null)
                {
                    BorderPreviewElement.BorderBrush = ColorConverter.CreateBrush(newColor);
                }

                if (hex != _borderColorHex)
                {
                    _borderColorHex = hex;
                    BorderColorChanged?.Invoke(this, _borderColorHex);
                }
            }
        }

        private void BorderThicknessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (Math.Abs(e.NewValue - _borderThickness) > 0.01)
            {
                _borderThickness = e.NewValue;
                BorderThicknessChanged?.Invoke(this, _borderThickness);
                
                if (BorderPreviewElement != null)
                {
                    BorderPreviewElement.BorderThickness = new Thickness(_borderThickness);
                }
            }
        }

        private void AutoSavePathText_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                tb.Text = _autoSaveFolderPathDisplay;
            }
        }

        #region Hotkey Settings

        /// <summary>
        /// Establece el valor inicial del hotkey Print Screen sin disparar eventos.
        /// </summary>
        public void SetPrintScreenHotkeyPreference(bool enabled)
        {
            _printScreenHotkeyEnabled = enabled;
            if (PrintScreenHotkeyToggle != null)
            {
                ControlStateManager.SetToggleSilently(PrintScreenHotkeyToggle, enabled, PrintScreenHotkeyToggle_Toggled);
            }
        }

        /// <summary>
        /// Establece el valor inicial del hotkey Ctrl+Shift+S sin disparar eventos.
        /// </summary>
        public void SetCtrlShiftSHotkeyPreference(bool enabled)
        {
            _ctrlShiftSHotkeyEnabled = enabled;
            if (CtrlShiftSHotkeyToggle != null)
            {
                ControlStateManager.SetToggleSilently(CtrlShiftSHotkeyToggle, enabled, CtrlShiftSHotkeyToggle_Toggled);
            }
        }

        /// <summary>
        /// Muestra u oculta la advertencia de Snipping Tool.
        /// </summary>
        /// <param name="show">True para mostrar, False para ocultar.</param>
        public void SetSnippingToolWarningVisible(bool show)
        {
            if (SnippingToolWarningInfoBar != null)
            {
                SnippingToolWarningInfoBar.IsOpen = show;
            }
        }

        private void PrintScreenHotkeyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (PrintScreenHotkeyToggle == null)
            {
                return;
            }

            var newValue = PrintScreenHotkeyToggle.IsOn;
            if (newValue == _printScreenHotkeyEnabled)
            {
                return;
            }

            _printScreenHotkeyEnabled = newValue;
            PrintScreenHotkeyChanged?.Invoke(this, newValue);
        }

        private void CtrlShiftSHotkeyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (CtrlShiftSHotkeyToggle == null)
            {
                return;
            }

            var newValue = CtrlShiftSHotkeyToggle.IsOn;
            if (newValue == _ctrlShiftSHotkeyEnabled)
            {
                return;
            }

            _ctrlShiftSHotkeyEnabled = newValue;
            CtrlShiftSHotkeyChanged?.Invoke(this, newValue);
        }

        private void OpenWindowsKeyboardSettings_Click(object sender, RoutedEventArgs e)
        {
            HotkeyService.OpenWindowsKeyboardSettings();
        }

        #endregion

        #region System Tray Settings

        /// <summary>
        /// Establece el valor inicial de la preferencia de minimizar a la bandeja.
        /// </summary>
        public void SetMinimizeToTrayPreference(bool enabled)
        {
            _minimizeToTrayEnabled = enabled;
            if (MinimizeToTrayToggle != null)
            {
                ControlStateManager.SetToggleSilently(MinimizeToTrayToggle, enabled, MinimizeToTrayToggle_Toggled);
            }
        }

        /// <summary>
        /// Establece el valor inicial de la preferencia de iniciar con Windows.
        /// </summary>
        public void SetStartWithWindowsPreference(bool enabled)
        {
            _startWithWindowsEnabled = enabled;
            if (StartWithWindowsToggle != null)
            {
                ControlStateManager.SetToggleSilently(StartWithWindowsToggle, enabled, StartWithWindowsToggle_Toggled);
            }
        }

        private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (MinimizeToTrayToggle == null)
            {
                return;
            }

            var newValue = MinimizeToTrayToggle.IsOn;
            if (newValue == _minimizeToTrayEnabled)
            {
                return;
            }

            _minimizeToTrayEnabled = newValue;
            MinimizeToTrayChanged?.Invoke(this, newValue);
        }

        private void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (StartWithWindowsToggle == null)
            {
                return;
            }

            var newValue = StartWithWindowsToggle.IsOn;
            if (newValue == _startWithWindowsEnabled)
            {
                return;
            }

            _startWithWindowsEnabled = newValue;
            StartWithWindowsChanged?.Invoke(this, newValue);
        }

        #endregion

        #region Actualizaciones

        private string? _pendingDownloadUrl;

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar estado de carga
            UpdateProgressRing.Visibility = Visibility.Visible;
            UpdateProgressRing.IsActive = true;
            CheckUpdatesButton.IsEnabled = false;
            DownloadUpdateButton.Visibility = Visibility.Collapsed;
            UpdateStatusText.Text = "Buscando actualizaciones...";

            try
            {
                var result = await UpdateService.CheckForUpdatesAsync();

                if (!result.Success)
                {
                    UpdateStatusText.Text = result.ErrorMessage;
                    return;
                }

                if (result.IsUpdateAvailable)
                {
                    UpdateStatusText.Text = $"¡Nueva versión disponible! v{result.LatestVersion}";
                    _pendingDownloadUrl = result.ReleasePageUrl;
                    DownloadUpdateButton.Visibility = Visibility.Visible;
                }
                else
                {
                    UpdateStatusText.Text = $"Estás usando la versión más reciente (v{UpdateService.CurrentVersion})";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                UpdateProgressRing.IsActive = false;
                UpdateProgressRing.Visibility = Visibility.Collapsed;
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private async void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingDownloadUrl))
            {
                await UpdateService.OpenDownloadPageAsync(_pendingDownloadUrl);
            }
            else
            {
                // Fallback: abrir página de releases
                await UpdateService.OpenDownloadPageAsync(UpdateService.ReleasesPageUrl);
            }
        }

        #endregion
    }
}
