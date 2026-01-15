using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.Graphics;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using System.Runtime.InteropServices;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Models;
using SnipShot.Features.Capture.Modes.ColorPicker;
using SnipShot.Features.Capture.Modes.FreeForm;
using SnipShot.Features.Capture.Modes.Rectangular;
using SnipShot.Features.Capture.Modes.WindowCapture;
using SnipShot.Services;
using SnipShot.Features.Editor;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SnipShot
{
    /// <summary>
    /// Ventana principal de SnipShot - Aplicación de captura de pantalla
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Constantes para tamaño mínimo de ventana
        private const int MIN_WINDOW_WIDTH = 600;
        private const int MIN_WINDOW_HEIGHT = 300;

        // P/Invoke para establecer tamaño mínimo de ventana
        private const int WM_GETMINMAXINFO = 0x0024;
        private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WinProc? _newWndProc;
        private IntPtr _oldWndProc = IntPtr.Zero;


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);



        private const int GWLP_WNDPROC = -4;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        // Border settings
        private bool _borderEnabled;
        private string _borderColorHex = "#FF000000";
        private double _borderThickness = 1.0;
        private bool _confirmDeleteCapture = true;

        private DialogService? _dialogService;
        private SettingsService? _settingsService;
        private ScreenCaptureService? _screenCaptureService;
        private ZoomManager? _zoomManager;
        private ImagePreviewManager? _imagePreviewManager;
        private AutoSaveManager? _autoSaveManager;
        private CaptureOrchestratorService? _captureOrchestrator;
        private WindowVisibilityManager? _windowVisibilityManager;
        private CaptureModeManager? _captureModeManager;
        private CaptureDelayManager? _captureDelayManager;
        private UIStateManager? _uiStateManager;
        private HotkeyService? _hotkeyService;
        private NativeSystemTrayService? _systemTrayService;
        private bool _isReallyClosing;
        private bool _minimizeToTray = true;

        // Layout responsivo
        private const double COMPACT_WIDTH_THRESHOLD = 1100;
        private bool _isCompactLayout;
        private DispatcherTimer? _resizeDebounceTimer;

        // Propiedad para acceder al DialogService con inicialización lazy
        private DialogService DialogService
        {
            get
            {
                if (_dialogService == null)
                {
                    _dialogService = new DialogService(this.Content.XamlRoot);
                }
                return _dialogService;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            ImageEditor.SetToolbarAnchors(EditShapesButton, null, null, EditTextButton, EditEmojiButton);
            ImageEditor.ToolbarVisibilityChanged += ImageEditor_ToolbarVisibilityChanged;
            ImageEditor.CropModeChanged += ImageEditor_CropModeChanged;
            ImageEditor.OcrResultsChanged += ImageEditor_OcrResultsChanged;
            
            // Inicializar los colores de los iconos de bolígrafo y resaltador
            InitializeEditPenAndHighlighterIcons();
            
            // Inicializar servicios
            _settingsService = new SettingsService();
            _screenCaptureService = new ScreenCaptureService();
            _zoomManager = new ZoomManager(PreviewImage, PreviewScrollViewer, ZoomLevelMenuItem);
            _imagePreviewManager = new ImagePreviewManager(
                PreviewImage,
                PreviewScrollViewer,
                PlaceholderPanel,
                CopyButton,
                SaveButton,
                ClearButton,
                EditToolbar,
                ActionButtonsSeparator);
            _autoSaveManager = new AutoSaveManager();
            _captureOrchestrator = new CaptureOrchestratorService(_screenCaptureService, this);
            _windowVisibilityManager = new WindowVisibilityManager(this);
            _captureModeManager = new CaptureModeManager(CaptureOptionIcon, CaptureOptionsButton);
            _captureDelayManager = new CaptureDelayManager(DelayOptionText, DelayOptionsButton);
            _uiStateManager = new UIStateManager(
                MainPanel,
                SettingsView,
                ZoomSubmenu,
                WindowCaptureTeachingTip,
                NewCaptureButton);
            
            // Suscribirse a eventos de ImagePreviewManager
            _imagePreviewManager.CaptureDisplayed += (s, bitmap) => _zoomManager?.SetBitmap(bitmap);
            _imagePreviewManager.EditToolbarVisibilityChanged += ImagePreviewManager_EditToolbarVisibilityChanged;
            
            // Suscribirse a eventos de AutoSaveManager
            _autoSaveManager.AutoSaveFolderPathChanged += (s, path) => SettingsView.SetAutoSaveFolderPath(path);
            
            // Suscribirse a eventos de CaptureOrchestrator
            _captureOrchestrator.CaptureCompleted += async (s, bitmap) => await ShowCapturePreview(bitmap, true);
            _captureOrchestrator.ApplyThemeToOverlay += (s, overlay) => ApplyThemeToOverlay(overlay);
            _captureOrchestrator.CaptureCancelled += (s, e) => 
            {
                // Mostrar la ventana solo si está configurada para ocultarse durante captura
                if (_windowVisibilityManager?.HideOnCapture == true)
                {
                    _windowVisibilityManager.ShowWindow();
                }
            };
            
            // Configurar Title Bar personalizada
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            
            // Configurar tamaño mínimo de la ventana
            SetupMinimumWindowSize();
            
            // Configurar tamaño inicial de la ventana (compacto)
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = MIN_WINDOW_WIDTH, Height = MIN_WINDOW_HEIGHT });
            
            // Centrar la ventana (opcional)
            CenterWindow();

            // Inicializar servicio de hotkeys ANTES de cargar preferencias
            // (LoadUserPreferences necesita _hotkeyService para cargar preferencias de hotkeys)
            _hotkeyService = new HotkeyService(this);
            _hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
            _hotkeyService.RegistrationError += HotkeyService_RegistrationError;
            
            // Cargar preferencias guardadas
            LoadUserPreferences();
            
            // Configurar eventos del control de configuración
            SettingsView.BackRequested += SettingsView_BackRequested;
            SettingsView.ThemeChanged += SettingsView_ThemeChanged;
            SettingsView.AutoSavePreferenceChanged += SettingsView_AutoSavePreferenceChanged;
            SettingsView.AutoSaveOpenFolderRequested += SettingsView_AutoSaveOpenFolderRequested;
            SettingsView.AutoSaveChangeFolderRequested += SettingsView_AutoSaveChangeFolderRequested;
            SettingsView.BorderEnabledChanged += SettingsView_BorderEnabledChanged;
            SettingsView.BorderColorChanged += SettingsView_BorderColorChanged;
            SettingsView.BorderThicknessChanged += SettingsView_BorderThicknessChanged;
            SettingsView.HideOnCaptureChanged += SettingsView_HideOnCaptureChanged;
            SettingsView.ConfirmDeleteCaptureChanged += SettingsView_ConfirmDeleteCaptureChanged;
            SettingsView.PrintScreenHotkeyChanged += SettingsView_PrintScreenHotkeyChanged;
            SettingsView.CtrlShiftSHotkeyChanged += SettingsView_CtrlShiftSHotkeyChanged;
            SettingsView.MinimizeToTrayChanged += SettingsView_MinimizeToTrayChanged;
            SettingsView.StartWithWindowsChanged += SettingsView_StartWithWindowsChanged;

            // Inicializar el servicio de System Tray nativo (sin H.NotifyIcon para mejor rendimiento)
            _systemTrayService = new NativeSystemTrayService(this);
            _systemTrayService.ShowWindowRequested += SystemTrayService_ShowWindowRequested;
            _systemTrayService.CaptureRequested += SystemTrayService_CaptureRequested;
            _systemTrayService.ExitRequested += SystemTrayService_ExitRequested;
            _systemTrayService.Initialize();

            // Configurar evento de cierre para minimizar al tray
            this.Closed += MainWindow_Closed;

            WindowCaptureTeachingTip.Target = CaptureOptionsButton;
        }

        private void SetupMinimumWindowSize()
        {
            _newWndProc = new WinProc(NewWindowProc);
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _oldWndProc = SetWindowLongPtr(hWnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Procesar mensajes de hotkey
            if (_hotkeyService?.ProcessHotkeyMessage(msg, wParam) == true)
            {
                return IntPtr.Zero;
            }

            if (msg == WM_GETMINMAXINFO)
            {
                var dpi = GetDpiForWindow(hWnd);
                var scalingFactor = dpi / 96.0f;

                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.X = (int)(MIN_WINDOW_WIDTH * scalingFactor);
                minMaxInfo.ptMinTrackSize.Y = (int)(MIN_WINDOW_HEIGHT * scalingFactor);
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private void CenterWindow()
        {
            var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
            var width = this.AppWindow.Size.Width;
            var height = this.AppWindow.Size.Height;
            
            this.AppWindow.Move(new Windows.Graphics.PointInt32
            {
                X = (displayArea.WorkArea.Width - width) / 2,
                Y = (displayArea.WorkArea.Height - height) / 2
            });
        }

        private void ExpandWindowForImage()
        {
            // Expandir la ventana al tamaño para visualización de imágenes (1200x600)
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1200, Height = 600 });
            CenterWindow();
        }

        /// <summary>
        /// Maneja el cambio de tamaño del panel principal para layout responsivo.
        /// Mueve la barra de herramientas de edición a la parte inferior cuando el ancho es menor a 1100px.
        /// Usa debounce para evitar llamadas excesivas durante el redimensionamiento.
        /// </summary>
        private void MainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Debounce: esperar 100ms de "calma" antes de ejecutar el cambio de layout
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            
            // Limpiar suscripciones anteriores para evitar múltiples handlers
            _resizeDebounceTimer.Tick -= ResizeDebounceTimer_Tick;
            _resizeDebounceTimer.Tick += ResizeDebounceTimer_Tick;
            _resizeDebounceTimer.Start();
        }

        /// <summary>
        /// Handler del timer de debounce para el resize.
        /// </summary>
        private void ResizeDebounceTimer_Tick(object? sender, object e)
        {
            _resizeDebounceTimer?.Stop();
            
            var shouldBeCompact = MainPanel.ActualWidth < COMPACT_WIDTH_THRESHOLD;
            if (shouldBeCompact == _isCompactLayout)
            {
                return; // No hay cambio de layout
            }
            
            _isCompactLayout = shouldBeCompact;
            UpdateToolbarLayout();
        }

        /// <summary>
        /// Actualiza la posición del EditToolbar según el layout actual.
        /// </summary>
        private void UpdateToolbarLayout()
        {
            if (_isCompactLayout)
            {
                // Mover EditToolbar a la barra inferior
                // Primero remover del contenedor actual (Grid.Column="1" de la barra superior)
                var parent = EditToolbar.Parent as Panel;
                if (parent != null)
                {
                    parent.Children.Remove(EditToolbar);
                }
                
                // Agregar al contenedor inferior
                BottomToolbarContainer.Child = EditToolbar;
                
                // Mostrar la barra inferior solo si hay imagen cargada
                BottomToolbarPanel.Visibility = EditToolbar.Visibility;
            }
            else
            {
                // Mover EditToolbar de vuelta a la barra superior centrada
                BottomToolbarContainer.Child = null;
                
                // Encontrar el Grid de la barra superior (primera fila)
                var topBarGrid = MainPanel.Children[0] as Grid;
                if (topBarGrid != null && !topBarGrid.Children.Contains(EditToolbar))
                {
                    // Insertar EditToolbar en la columna central
                    Grid.SetColumn(EditToolbar, 1);
                    topBarGrid.Children.Add(EditToolbar);
                }
                
                // Ocultar la barra inferior
                BottomToolbarPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Maneja el cambio de visibilidad de la barra de herramientas de edición.
        /// Actualiza la visibilidad del panel inferior en modo compacto.
        /// </summary>
        private void ImagePreviewManager_EditToolbarVisibilityChanged(object? sender, bool isVisible)
        {
            if (_isCompactLayout)
            {
                BottomToolbarPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void NewCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // Ejecuta la captura según el modo actualmente seleccionado
            await CaptureScreenAsync(_captureModeManager!.CurrentMode);
        }

        private async void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Crear el selector de archivos
                var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
                
                // Obtener el handle de la ventana para el selector
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

                // Configurar los tipos de archivo permitidos
                openPicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                openPicker.FileTypeFilter.Add(".png");
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".bmp");
                openPicker.FileTypeFilter.Add(".gif");
                openPicker.FileTypeFilter.Add(".tiff");
                openPicker.FileTypeFilter.Add(".ico");

                // Mostrar el selector y obtener el archivo
                var file = await openPicker.PickSingleFileAsync();
                
                if (file != null)
                {
                    await LoadImageFromFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo abrir la imagen:");
            }
        }

        private async Task LoadImageFromFileAsync(StorageFile file)
        {
            try
            {
                var success = await _imagePreviewManager!.LoadImageFromFileAsync(file);
                if (success)
                {
                    // Cerrar el flyout de OCR si está abierto (nueva imagen invalida el OCR anterior)
                    CloseOcrFlyout();
                    
                    // Cargar imagen en el editor
                    if (_imagePreviewManager.CurrentCapture != null)
                    {
                        await ImageEditor.LoadImageAsync(_imagePreviewManager.CurrentCapture);
                        ImageEditor.Visibility = Visibility.Visible;
                        UpdateOcrUiState();
                        
                        // Ocultar el ScrollViewer antiguo para evitar duplicación visual
                        PreviewScrollViewer.Visibility = Visibility.Collapsed;
                        PreviewImage.Visibility = Visibility.Collapsed;
                    }
                    
                    ExpandWindowForImage();
                    ApplyDefaultZoom();
                    _uiStateManager?.ShowZoomControls();
                }
                else
                {
                    await DialogService.ShowErrorAsync("No se pudo cargar la imagen.");
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error al cargar imagen", "No se pudo cargar la imagen:");
            }
        }

        private void RectangularCapture_Click(object sender, RoutedEventArgs e)
        {
            _captureModeManager?.SetRectangularMode();
        }

        private void WindowCapture_Click(object sender, RoutedEventArgs e)
        {
            _captureModeManager?.SetWindowMode();
        }

        private void FullScreenCapture_Click(object sender, RoutedEventArgs e)
        {
            _captureModeManager?.SetFullScreenMode();
        }

        private void FreeFormCapture_Click(object sender, RoutedEventArgs e)
        {
            _captureModeManager?.SetFreeFormMode();
        }

        private void NoDelay_Click(object sender, RoutedEventArgs e)
        {
            _captureDelayManager?.SetNoDelay();
        }

        private void Delay3_Click(object sender, RoutedEventArgs e)
        {
            _captureDelayManager?.SetDelay3Seconds();
        }

        private void Delay5_Click(object sender, RoutedEventArgs e)
        {
            _captureDelayManager?.SetDelay5Seconds();
        }

        private void Delay10_Click(object sender, RoutedEventArgs e)
        {
            _captureDelayManager?.SetDelay10Seconds();
        }

        private void ShowWindowCaptureTip(string message)
        {
            _uiStateManager?.ShowWindowCaptureTip(message);
        }

        private void HideWindowCaptureTip()
        {
            _uiStateManager?.HideWindowCaptureTip();
        }

        private async Task CaptureScreenAsync(string captureMode)
        {
            var appWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

            if (captureMode == "Ventana")
            {
                await CaptureWindowAsync(appWindowHandle);
                return;
            }

            HideWindowCaptureTip();

            await _windowVisibilityManager!.ExecuteWithHiddenWindowAsync(async () =>
            {
                switch (captureMode)
                {
                    case "Pantalla Completa":
                        await CaptureFullScreenAsync();
                        break;
                    case "Rectangular":
                        await CaptureRectangularAsync();
                        break;
                    case "Forma Libre":
                        await CaptureFreeFormAsync();
                        break;
                    default:
                        throw new NotImplementedException($"Modo '{captureMode}' no implementado.");
                }
            }, _captureDelayManager!.DelaySeconds);
        }

        private async Task CaptureFullScreenAsync()
        {
            try
            {
                await _captureOrchestrator!.CaptureFullScreenAsync();
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo capturar la pantalla:");
            }
        }

        private async Task CaptureRectangularAsync()
        {
            try
            {
                await _captureOrchestrator!.CaptureRectangularAsync();
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo capturar el área rectangular:");
            }
        }

        private async Task CaptureWindowAsync(IntPtr appWindowHandle)
        {
            // Enumerar ventanas ANTES de ocultar la aplicación para incluirla en la lista
            var availableWindows = WindowEnumerationHelper.GetCaptureableWindows(appWindowHandle, includeOwnWindow: true);

            if (availableWindows.Count == 0)
            {
                ShowWindowCaptureTip("No hay ventanas disponibles para capturar.");
                return;
            }

            HideWindowCaptureTip();

            await _windowVisibilityManager!.ExecuteWithHiddenWindowAsync(async () =>
            {
                // Pasar las ventanas pre-enumeradas al orchestrator
                await _captureOrchestrator!.CaptureWindowAsync(availableWindows);
            }, _captureDelayManager!.DelaySeconds);
        }

        private async Task CaptureFreeFormAsync()
        {
            try
            {
                await _captureOrchestrator!.CaptureFreeFormAsync();
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo capturar en forma libre:");
            }
        }



        private async Task ShowCapturePreview(SoftwareBitmap screenshot, bool autoSaveEligible = false)
        {
            var success = await _imagePreviewManager!.ShowCaptureAsync(screenshot);
            if (success)
            {
                // Cerrar el flyout de OCR si está abierto (nueva captura invalida el OCR anterior)
                CloseOcrFlyout();
                
                // Cargar imagen en el editor
                await ImageEditor.LoadImageAsync(screenshot);
                ImageEditor.Visibility = Visibility.Visible;
                UpdateOcrUiState();
                
                // Ocultar el ScrollViewer antiguo para evitar duplicación visual
                // (ImagePreviewManager lo hace visible por compatibilidad, pero ahora usamos ImageEditor)
                PreviewScrollViewer.Visibility = Visibility.Collapsed;
                PreviewImage.Visibility = Visibility.Collapsed;
                
                ExpandWindowForImage();
                ApplyDefaultZoom();
                _uiStateManager?.ShowZoomControls();

                if (autoSaveEligible)
                {
                    await _autoSaveManager!.AutoSaveIfEnabledAsync(screenshot);
                }
            }
        }



        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _uiStateManager?.ShowSettingsPanel();
            // Sincronizar toggles cada vez que se abre el panel de configuración
            SettingsView.SyncAllToggles();
        }

        private void SettingsView_BackRequested(object? sender, EventArgs e)
        {
            _uiStateManager?.ShowMainPanel();
        }

        private void SettingsView_ThemeChanged(object? sender, string themeTag)
        {
            ApplyTheme(themeTag);
            SaveThemePreference(themeTag);
        }

        private void SettingsView_AutoSavePreferenceChanged(object? sender, bool enabled)
        {
            _autoSaveManager!.AutoSaveEnabled = enabled;
            SaveAutoSavePreference(enabled);
        }

        private async void SettingsView_AutoSaveOpenFolderRequested(object? sender, EventArgs e)
        {
            try
            {
                var folder = await _autoSaveManager!.GetAutoSaveFolderAsync();
                if (folder != null)
                {
                    await Launcher.LaunchFolderAsync(folder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to open auto-save folder: {ex.Message}");
            }
        }

        private async void OpenCapturesFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = await _autoSaveManager!.GetAutoSaveFolderAsync();
                if (folder != null)
                {
                    await Launcher.LaunchFolderAsync(folder);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to open captures folder: {ex.Message}");
            }
        }

        private async void SettingsView_AutoSaveChangeFolderRequested(object? sender, EventArgs e)
        {
            try
            {
                var picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary
                };
                picker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    _autoSaveManager!.SetCustomAutoSaveFolder(folder);
                    SaveAutoSaveFolderPath(_autoSaveManager.AutoSaveFolderPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to change auto-save folder: {ex.Message}");
            }
        }

        private void SettingsView_BorderEnabledChanged(object? sender, bool enabled)
        {
            _borderEnabled = enabled;
            _captureOrchestrator?.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            SaveBorderPreferences();
        }

        private void SettingsView_BorderColorChanged(object? sender, string colorHex)
        {
            _borderColorHex = colorHex;
            _captureOrchestrator?.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            SaveBorderPreferences();
        }

        private void SettingsView_BorderThicknessChanged(object? sender, double thickness)
        {
            _borderThickness = thickness;
            _captureOrchestrator?.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
            SaveBorderPreferences();
        }

        private void SettingsView_HideOnCaptureChanged(object? sender, bool hideOnCapture)
        {
            _windowVisibilityManager?.SetHideOnCapture(hideOnCapture);
            _captureOrchestrator?.SetHideOnCapture(hideOnCapture);
            _settingsService?.SaveHideOnCapture(hideOnCapture);
        }

        private void SettingsView_ConfirmDeleteCaptureChanged(object? sender, bool enabled)
        {
            _confirmDeleteCapture = enabled;
            _settingsService?.SaveConfirmDeleteCapture(enabled);
        }

        private void SettingsView_PrintScreenHotkeyChanged(object? sender, bool enabled)
        {
            if (enabled)
            {
                // Verificar si Snipping Tool tiene el control ANTES de intentar activar
                if (HotkeyService.IsSnippingToolUsingPrintScreen())
                {
                    // No permitir activar si Snipping Tool está usando la tecla
                    SettingsView.SetPrintScreenHotkeyPreference(false);
                    SettingsView.SetSnippingToolWarningVisible(true);
                    return;
                }

                var success = _hotkeyService?.RegisterPrintScreen() ?? false;
                if (!success)
                {
                    // Si falló el registro, desactivar el toggle
                    SettingsView.SetPrintScreenHotkeyPreference(false);
                    return;
                }

                // Registro exitoso: ocultar advertencia si estaba visible
                SettingsView.SetSnippingToolWarningVisible(false);
            }
            else
            {
                _hotkeyService?.UnregisterPrintScreen();
                SettingsView.SetSnippingToolWarningVisible(false);
            }

            _settingsService?.SavePrintScreenHotkeyEnabled(enabled);
        }

        private void SettingsView_CtrlShiftSHotkeyChanged(object? sender, bool enabled)
        {
            if (enabled)
            {
                var success = _hotkeyService?.RegisterCtrlShiftS() ?? false;
                if (!success)
                {
                    // Si falló el registro, desactivar el toggle
                    SettingsView.SetCtrlShiftSHotkeyPreference(false);
                    return;
                }
            }
            else
            {
                _hotkeyService?.UnregisterCtrlShiftS();
            }

            _settingsService?.SaveCtrlShiftSHotkeyEnabled(enabled);
        }

        private void SettingsView_MinimizeToTrayChanged(object? sender, bool enabled)
        {
            _minimizeToTray = enabled;
            _settingsService?.SaveMinimizeToTrayEnabled(enabled);
        }

        private async void SettingsView_StartWithWindowsChanged(object? sender, bool enabled)
        {
            var success = await StartupService.SetStartupEnabledAsync(enabled);
            if (!success && enabled)
            {
                // Si no se pudo habilitar, revertir el toggle
                SettingsView.SetStartWithWindowsPreference(false);
                await DialogService.ShowErrorAsync(
                    "No se pudo configurar el inicio automático. Es posible que esté deshabilitado por una política de sistema o el usuario.",
                    "Inicio con Windows");
            }
            else
            {
                _settingsService?.SaveStartWithWindowsEnabled(enabled);
            }
        }

        private async void HotkeyService_HotkeyPressed(object? sender, HotkeyType hotkeyType)
        {
            // Ejecutar la captura según el modo actual
            await CaptureScreenAsync(_captureModeManager!.CurrentMode);
        }

        private async void HotkeyService_RegistrationError(object? sender, string errorMessage)
        {
            await DialogService.ShowErrorAsync(errorMessage, "Error de atajo de teclado");
        }

        #region System Tray Event Handlers

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // Si no es un cierre real y la opción está habilitada, minimizar al tray
            if (!_isReallyClosing && _minimizeToTray)
            {
                args.Handled = true;
                HideToTray();
            }
            else
            {
                // Limpieza al cerrar realmente
                _systemTrayService?.Dispose();
                _hotkeyService?.Dispose();
            }
        }

        private void HideToTray()
        {
            // Ocultar la ventana
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ShowWindow(hwnd, SW_HIDE);
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        private void SystemTrayService_ShowWindowRequested(object? sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void ShowFromTray()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            this.Activate();
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private async void SystemTrayService_CaptureRequested(object? sender, EventArgs e)
        {
            // Ejecutar captura desde el tray con el modo actualmente configurado
            await CaptureScreenAsync(_captureModeManager!.CurrentMode);
        }

        private void SystemTrayService_ExitRequested(object? sender, EventArgs e)
        {
            // Marcar que es un cierre real
            _isReallyClosing = true;
            this.Close();
        }

        #endregion


        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            await CopyCurrentImageAsync();
        }

        /// <summary>
        /// Copia la imagen actual al portapapeles
        /// </summary>
        private async Task CopyCurrentImageAsync()
        {
            if (!_imagePreviewManager!.HasCapture)
            {
                await DialogService.ShowErrorAsync("No hay ninguna captura para copiar.");
                return;
            }

            try
            {
                var bitmap = await GetExportBitmapAsync();
                if (bitmap == null)
                {
                    await DialogService.ShowErrorAsync("No hay ninguna captura para copiar.");
                    return;
                }

                await ClipboardHelper.CopyImageToClipboardAsync(bitmap);
                bitmap.Dispose();

                // Animación: cambiar a CheckMark
                CopyButtonIcon.Glyph = "\uE73E"; // CheckMark
                
                // Volver al icono original después de 1.5 segundos
                await Task.Delay(1500);
                CopyButtonIcon.Glyph = "\uE8C8"; // Copy icon
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo copiar al portapapeles:");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveCurrentImageAsync();
        }

        /// <summary>
        /// Guarda la imagen actual en un archivo
        /// </summary>
        private async Task SaveCurrentImageAsync()
        {
            if (!_imagePreviewManager!.HasCapture)
            {
                await DialogService.ShowErrorAsync("No hay ninguna captura para guardar.");
                return;
            }

            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var bitmap = await GetExportBitmapAsync();
                if (bitmap == null)
                {
                    await DialogService.ShowErrorAsync("No hay ninguna captura para guardar.");
                    return;
                }

                var saveResult = await FileHelper.SaveImageAsync(bitmap, hwnd);
                bitmap.Dispose();

                if (saveResult.saved)
                {
                    await DialogService.ShowSuccessAsync($"La imagen se guardó en:\n{saveResult.filePath}", "Guardado Exitoso");
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowErrorAsync(ex, "Error", "No se pudo guardar la imagen:");
            }
        }

        private async Task<SoftwareBitmap?> GetExportBitmapAsync()
        {
            if (ImageEditor.Visibility == Visibility.Visible && ImageEditor.HasImage)
            {
                var rendered = await ImageEditor.RenderWithAnnotationsAsync();
                if (rendered != null)
                {
                    return rendered;
                }
            }

            return _imagePreviewManager?.CurrentCapture;
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_imagePreviewManager?.HasCapture != true)
            {
                return;
            }

            if (_confirmDeleteCapture)
            {
                var confirmed = await DialogService.ShowConfirmationAsync(
                    "¿Deseas eliminar la captura actual?",
                    "Eliminar captura",
                    "Eliminar",
                    "Cancelar");

                if (!confirmed)
                {
                    return;
                }
            }

            _imagePreviewManager?.Clear();
            _zoomManager?.Reset();
            _uiStateManager?.HideZoomControls();
            
            // Cerrar el flyout de OCR si está abierto
            CloseOcrFlyout();
            
            // Limpiar y ocultar el editor de imágenes
            ImageEditor.Clear();
            ImageEditor.Visibility = Visibility.Collapsed;
            UpdateOcrUiState();
        }

        private void ApplyTheme(string theme)
        {
            if (this.Content is FrameworkElement rootElement)
            {
                ThemeHelper.ApplyTheme(rootElement, theme);
                
                // Actualizar colores de los botones de caption (minimizar, maximizar, cerrar)
                UpdateCaptionButtonColors(theme, rootElement);
            }
        }

        private void UpdateCaptionButtonColors(string theme, FrameworkElement rootElement)
        {
            var titleBar = this.AppWindow.TitleBar;
            if (titleBar == null) return;

            // Determinar si estamos en modo claro u oscuro
            bool isLightTheme = theme switch
            {
                "Light" => true,
                "Dark" => false,
                _ => rootElement.ActualTheme == ElementTheme.Light // Para "Default", usar el tema actual del sistema
            };

            if (isLightTheme)
            {
                // Tema claro - iconos oscuros
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 128, 128, 128);
                
                // Fondos para hover/pressed en tema claro
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(25, 0, 0, 0);
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(50, 0, 0, 0);
            }
            else
            {
                // Tema oscuro - iconos claros
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 128, 128, 128);
                
                // Fondos para hover/pressed en tema oscuro
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(25, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(50, 255, 255, 255);
            }

            // Fondo transparente para los botones normales
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }

        private void SaveThemePreference(string theme)
        {
            _settingsService?.SaveTheme(theme);
        }

        private void SaveAutoSavePreference(bool enabled)
        {
            _settingsService?.SaveAutoSaveEnabled(enabled);
        }

        private void SaveAutoSaveFolderPath(string path)
        {
            _settingsService?.SaveAutoSaveFolderPath(path);
        }

        private void SaveBorderPreferences()
        {
            _settingsService?.SaveBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);
        }

        private void LoadUserPreferences()
        {
            if (_settingsService == null)
            {
                return;
            }

            // Cargar tema
            var savedTheme = _settingsService.GetTheme();
            ApplyTheme(savedTheme);
            SettingsView.SetCurrentTheme(savedTheme);

            // Cargar configuración de guardado automático
            _autoSaveManager!.AutoSaveEnabled = _settingsService.GetAutoSaveEnabled();

            // Cargar configuración de bordes
            var borderSettings = _settingsService.GetBorderSettings();
            _borderEnabled = borderSettings.enabled;
            _borderColorHex = borderSettings.colorHex;
            _borderThickness = borderSettings.thickness;
            _captureOrchestrator?.SetBorderSettings(_borderEnabled, _borderColorHex, _borderThickness);

            // Cargar configuración de ocultar al capturar
            var hideOnCapture = _settingsService.GetHideOnCapture();
            _windowVisibilityManager?.SetHideOnCapture(hideOnCapture);
            _captureOrchestrator?.SetHideOnCapture(hideOnCapture);

            _confirmDeleteCapture = _settingsService.GetConfirmDeleteCapture();


            // Cargar ruta de guardado automático
            var savedPath = _settingsService.GetAutoSaveFolderPath();
            if (!string.IsNullOrEmpty(savedPath))
            {
                // Si hay una ruta guardada pero no es la actual, actualizarla en el manager
                // El manager ya tiene la ruta por defecto, solo actualizamos si hay una guardada diferente
            }

            // Actualizar UI de configuración
            SettingsView.SetBorderPreference(_borderEnabled, _borderColorHex, _borderThickness);
            SettingsView.SetAutoSavePreference(_autoSaveManager.AutoSaveEnabled);
            SettingsView.SetAutoSaveFolderPath(_autoSaveManager.AutoSaveFolderPath);
            SettingsView.SetHideOnCapturePreference(hideOnCapture);
            SettingsView.SetConfirmDeleteCapturePreference(_confirmDeleteCapture);

            // Cargar preferencias de hotkeys
            LoadHotkeyPreferences();

            // Cargar preferencias de System Tray
            LoadSystemTrayPreferences();

        }

        /// <summary>
        /// Aplica el tema actual a una ventana de overlay
        /// </summary>
        private void ApplyThemeToOverlay(Window overlay)
        {
            if (this.Content is FrameworkElement rootElement && overlay.Content is FrameworkElement overlayContent)
            {
                overlayContent.RequestedTheme = rootElement.RequestedTheme;
            }
        }

        /// <summary>
        /// Carga y aplica las preferencias de hotkeys guardadas.
        /// </summary>
        private void LoadHotkeyPreferences()
        {
            if (_settingsService == null || _hotkeyService == null)
            {
                return;
            }

            // Verificar si Snipping Tool está usando Print Screen
            bool snippingToolActive = HotkeyService.IsSnippingToolUsingPrintScreen();
            SettingsView.SetSnippingToolWarningVisible(snippingToolActive && _settingsService.GetPrintScreenHotkeyEnabled());

            // Cargar preferencia de Print Screen
            var printScreenEnabled = _settingsService.GetPrintScreenHotkeyEnabled();
            SettingsView.SetPrintScreenHotkeyPreference(printScreenEnabled);
            if (printScreenEnabled)
            {
                _hotkeyService.RegisterPrintScreen();
            }

            // Cargar preferencia de Ctrl+Shift+S
            var ctrlShiftSEnabled = _settingsService.GetCtrlShiftSHotkeyEnabled();
            SettingsView.SetCtrlShiftSHotkeyPreference(ctrlShiftSEnabled);
            if (ctrlShiftSEnabled)
            {
                _hotkeyService.RegisterCtrlShiftS();
            }
        }

        /// <summary>
        /// Carga y aplica las preferencias de System Tray guardadas.
        /// </summary>
        private async void LoadSystemTrayPreferences()
        {
            if (_settingsService == null)
            {
                return;
            }

            // Cargar preferencia de minimizar a la bandeja
            _minimizeToTray = _settingsService.GetMinimizeToTrayEnabled();
            SettingsView.SetMinimizeToTrayPreference(_minimizeToTray);

            // Cargar preferencia de iniciar con Windows
            var startWithWindows = _settingsService.GetStartWithWindowsEnabled();
            
            // Verificar el estado real en el sistema
            var actualState = await StartupService.IsStartupEnabledAsync();
            if (actualState != startWithWindows)
            {
                // Sincronizar con el estado real
                _settingsService.SaveStartWithWindowsEnabled(actualState);
                startWithWindows = actualState;
            }
            
            SettingsView.SetStartWithWindowsPreference(startWithWindows);
        }

        
        #region Zoom Functionality

        private bool UseEditorZoom()
        {
            return ImageEditor.Visibility == Visibility.Visible && ImageEditor.HasImage;
        }

        private void ApplyDefaultZoom()
        {
            if (UseEditorZoom())
            {
                ImageEditor.SetActualSize();
            }
            else
            {
                _zoomManager?.SetActualSize();
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (UseEditorZoom())
            {
                ImageEditor.ZoomIn();
            }
            else
            {
                _zoomManager?.ZoomIn();
            }
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (UseEditorZoom())
            {
                ImageEditor.ZoomOut();
            }
            else
            {
                _zoomManager?.ZoomOut();
            }
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (UseEditorZoom())
            {
                ImageEditor.FitToWindow();
            }
            else
            {
                _zoomManager?.FitToWindow();
            }
        }

        private void ZoomActualSize_Click(object sender, RoutedEventArgs e)
        {
            if (UseEditorZoom())
            {
                ImageEditor.SetActualSize();
            }
            else
            {
                _zoomManager?.SetActualSize();
            }
        }

        private void MainGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && ImageEditor.ExitOcrMode())
            {
                UpdateOcrUiState();
                e.Handled = true;
                return;
            }

            if (UseEditorZoom() && ImageEditor.HandleZoomShortcut(e))
            {
                return;
            }

            _zoomManager?.HandleKeyboardShortcut(e);
        }

        private void MainGrid_EscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (ImageEditor.ExitOcrMode())
            {
                UpdateOcrUiState();
                args.Handled = true;
            }
        }



        private void PreviewScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Este evento es manejado internamente por ZoomManager
            // Solo necesitamos este método stub porque está referenciado en el XAML
        }

        #endregion

        #region Edit Toolbar Handlers

        // Brushes para selección de botones de edición
        private static readonly SolidColorBrush _editSelectedButtonBrush = new(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255));
        private static readonly SolidColorBrush _editTransparentBrush = new(Microsoft.UI.Colors.Transparent);

        private void EditShapesButton_Click(object sender, RoutedEventArgs e)
        {
            ImageEditor.ToggleShapesToolbar();
        }

        private void EditPenButton_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            ShowEditFlyout(EditPenButton);
        }

        private void EditPenButton_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está activo, desactivar (toggle)
            if (ImageEditor.ActiveToolType == EditorToolType.Pen)
            {
                DeactivateEditPen();
                return;
            }
            
            // Desactivar otras herramientas
            DeactivateEditHighlighter();
            ImageEditor.DeactivateTools();
            
            // Activar modo bolígrafo
            ImageEditor.ActivatePenToolOnly();
            
            // Marcar el botón como seleccionado
            EditPenButton.Background = _editSelectedButtonBrush;
        }

        private void DeactivateEditPen()
        {
            ImageEditor.DeactivatePenTool();
            EditPenButton.Background = _editTransparentBrush;
        }

        private void EditPenToolbar_ColorChanged(object? sender, Windows.UI.Color color)
        {
            ImageEditor.SetPenColor(color);
            // Actualizar el color del icono del bolígrafo
            EditPenIcon.Foreground = BrushCache.GetBrush(color);
        }

        private void EditPenToolbar_ThicknessChanged(object? sender, double thickness)
        {
            ImageEditor.SetPenThickness(thickness);
        }

        private void EditHighlighterButton_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            ShowEditFlyout(EditHighlighterButton);
        }

        private void EditHighlighterButton_Click(object sender, RoutedEventArgs e)
        {
            // Si ya está activo, desactivar (toggle)
            if (ImageEditor.ActiveToolType == EditorToolType.Highlighter)
            {
                DeactivateEditHighlighter();
                return;
            }
            
            // Desactivar otras herramientas
            DeactivateEditPen();
            ImageEditor.DeactivateTools();
            
            // Activar modo resaltador
            ImageEditor.ActivateHighlighterToolOnly();
            
            // Marcar el botón como seleccionado
            EditHighlighterButton.Background = _editSelectedButtonBrush;
        }

        private void DeactivateEditHighlighter()
        {
            ImageEditor.DeactivateHighlighterTool();
            EditHighlighterButton.Background = _editTransparentBrush;
        }

        private void EditHighlighterToolbar_ColorChanged(object? sender, Windows.UI.Color color)
        {
            ImageEditor.SetHighlighterColor(color);
            // Actualizar el color del icono del resaltador
            EditHighlighterIcon.Foreground = BrushCache.GetBrush(color);
        }

        private void EditHighlighterToolbar_ThicknessChanged(object? sender, double thickness)
        {
            ImageEditor.SetHighlighterThickness(thickness);
        }

        private void EditEraserButton_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            ShowEditFlyout(EditEraserButton);
        }

        private void EditEraserButton_Click(object sender, RoutedEventArgs e)
        {
            // Desactivar herramientas de dibujo
            DeactivateEditPen();
            DeactivateEditHighlighter();
            ImageEditor.ToggleEraserTool();
        }

        private void EditClearAnnotations_Click(object sender, RoutedEventArgs e)
        {
            ImageEditor.ClearAllAnnotations();
        }

        /// <summary>
        /// Muestra el flyout asociado al botón anclado debajo de él.
        /// Usa Button.Flyout en lugar de ContextFlyout para garantizar posicionamiento fijo.
        /// </summary>
        private static void ShowEditFlyout(Button? button)
        {
            if (button?.Flyout == null)
            {
                return;
            }

            // Button.Flyout se abre automáticamente anclado al botón con el Placement configurado.
            // Solo necesitamos llamar ShowAt para mostrarlo explícitamente.
            button.Flyout.ShowAt(button, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Bottom
            });
        }

        private void EditEmojiButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEditPen();
            DeactivateEditHighlighter();
            ImageEditor.ToggleEmojiToolbar();
        }

        private void EditTextButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEditPen();
            DeactivateEditHighlighter();
            ImageEditor.ToggleTextToolbar();
        }

        private void EditCropButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageEditor.HasImage)
            {
                return;
            }

            if (ImageEditor.IsCropping)
            {
                ImageEditor.CancelCrop();
            }
            else
            {
                ImageEditor.BeginCrop();
            }
        }

        private async void EditOcrButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageEditor.HasImage || ImageEditor.IsOcrRunning)
            {
                return;
            }

            // Si ya tiene resultados OCR, desactivar OCR completamente
            if (ImageEditor.HasOcrResult)
            {
                // Desactivar el modo OCR (ocultar overlay con texto detectado)
                ImageEditor.ExitOcrMode();
                UpdateOcrUiState();
                // El flyout se cierra automáticamente porque el botón lo maneja
                return;
            }

            // Cerrar el flyout antes de iniciar el análisis (por si estaba abierto de un intento anterior)
            EditOcrFlyout.Hide();

            // Mostrar animación de progreso
            EditOcrIcon.Visibility = Visibility.Collapsed;
            EditOcrProgressRing.IsActive = true;
            EditOcrProgressRing.Visibility = Visibility.Visible;
            EditOcrButton.IsEnabled = false;
            EditOcrCopyAllButton.IsEnabled = false;
            ToolTipService.SetToolTip(EditOcrButton, "Analizando texto...");
            
            var success = await ImageEditor.AnalyzeTextAsync();
            
            // Ocultar animación de progreso
            EditOcrProgressRing.IsActive = false;
            EditOcrProgressRing.Visibility = Visibility.Collapsed;
            EditOcrIcon.Visibility = Visibility.Visible;
            
            UpdateOcrUiState();

            if (success)
            {
                // Mostrar el flyout después de detectar texto exitosamente
                EditOcrFlyout.ShowAt(EditOcrButton);
            }
            else
            {
                await DialogService.ShowInfoAsync("No se detectó texto en la imagen.", "Extraer texto");
            }
        }

        private async void EditOcrCopyAll_Click(object sender, RoutedEventArgs e)
        {
            var text = ImageEditor.GetAllOcrText();
            if (string.IsNullOrWhiteSpace(text))
            {
                await DialogService.ShowInfoAsync("No hay texto para copiar.", "Extraer texto");
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);

            // Mostrar feedback visual de copia exitosa
            EditOcrCopyAllIcon.Glyph = "\uE73E"; // Check icon
            
            // Restaurar el icono original después de un breve delay
            await Task.Delay(1500);
            EditOcrCopyAllIcon.Glyph = "\uE8C8"; // Copy icon
        }

        /// <summary>
        /// Cierra el flyout de OCR de manera programática (para limpieza de imagen o nuevas capturas).
        /// </summary>
        private void CloseOcrFlyout()
        {
            EditOcrFlyout.Hide();
        }

        /// <summary>
        /// Aplica focus y fondo al botón de OCR cuando se abre el flyout.
        /// </summary>
        private void EditOcrFlyout_Opened(object sender, object e)
        {
            EditOcrButton.Focus(FocusState.Programmatic);
            
            // Aplicar fondo de selección al botón OCR
            var selectedBrush = Application.Current.Resources["ControlFillColorSecondaryBrush"] as SolidColorBrush 
                ?? BrushCache.GetBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255));
            EditOcrButton.Background = selectedBrush;
        }

        /// <summary>
        /// Actualiza el fondo del botón OCR cuando el flyout se cierra.
        /// Si el OCR sigue activo, mantiene el fondo de selección.
        /// </summary>
        private void EditOcrFlyout_Closed(object sender, object e)
        {
            // Si el OCR sigue activo, mantener el fondo de selección
            if (ImageEditor.HasOcrResult)
            {
                var selectedBrush = Application.Current.Resources["ControlFillColorSecondaryBrush"] as SolidColorBrush 
                    ?? BrushCache.GetBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255));
                EditOcrButton.Background = selectedBrush;
            }
            else
            {
                EditOcrButton.Background = BrushCache.Transparent;
            }
        }

        private async void CropApplyButton_Click(object sender, RoutedEventArgs e)
        {
            await ImageEditor.ApplyCropAsync();
        }

        private void CropCancelButton_Click(object sender, RoutedEventArgs e)
        {
            ImageEditor.CancelCrop();
        }

        private void EditUndoButton_Click(object sender, RoutedEventArgs e)
        {
            ImageEditor.Undo();
        }

        private void EditRedoButton_Click(object sender, RoutedEventArgs e)
        {
            ImageEditor.Redo();
        }

        private void ImageEditor_UndoRedoStateChanged(object? sender, EventArgs e)
        {
            EditUndoButton.IsEnabled = ImageEditor.CanUndo;
            EditRedoButton.IsEnabled = ImageEditor.CanRedo;
        }

        private void ImageEditor_ImageModified(object? sender, EventArgs e)
        {
            // La imagen fue modificada, podemos habilitar el guardado si es necesario
        }

        private async void ImageEditor_SaveImageRequested(object? sender, EventArgs e)
        {
            // Reutilizar la lógica existente de guardado
            await SaveCurrentImageAsync();
        }

        private async void ImageEditor_CopyImageRequested(object? sender, EventArgs e)
        {
            // Reutilizar la lógica existente de copiado
            await CopyCurrentImageAsync();
        }

        /// <summary>
        /// Manejador para búsqueda de imagen en navegador.
        /// Copia la imagen al portapapeles y abre el motor de búsqueda seleccionado.
        /// </summary>
        private async void ImageEditor_SearchImageRequested(object? sender, string searchEngine)
        {
            // 1. Copiar imagen al portapapeles
            await CopyCurrentImageAsync();

            // 2. Determinar URL según motor de búsqueda
            // Nota: No es posible abrir directamente el diálogo de subida por seguridad del navegador
            string url = searchEngine switch
            {
                "google" => "https://www.google.com/imghp?hl=es",
                "bing" => "https://www.bing.com/images?FORM=HDRSC3",
                _ => "https://www.google.com/imghp?hl=es"
            };

            // 3. Abrir navegador
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));

            // 4. Mostrar InfoBar con instrucciones
            ShowSearchImageInfoBar(searchEngine);
        }

        /// <summary>
        /// Muestra el InfoBar con instrucciones para pegar la imagen en el buscador
        /// </summary>
        private void ShowSearchImageInfoBar(string searchEngine)
        {
            string engineName = searchEngine == "google" ? "Google Imágenes" : "Búsqueda Visual de Bing";
            string iconDescription = searchEngine == "google" ? "el icono de cámara 📷" : "el icono de búsqueda visual";
            SearchImageInfoBar.Title = "Imagen copiada al portapapeles";
            SearchImageInfoBar.Message = $"Haz clic en {iconDescription} en {engineName} y pega la imagen con Ctrl+V";
            SearchImageInfoBar.IsOpen = true;
        }

        private void ImageEditor_OcrResultsChanged(object? sender, EventArgs e)
        {
            UpdateOcrUiState();
        }

        private void ImageEditor_CropModeChanged(object? sender, bool isCropping)
        {
            SetCropUiState(isCropping);
            UpdateEditToolbarSelection(isCropping ? EditCropButton : null);
        }

        private void ImageEditor_ToolbarVisibilityChanged(object? sender, EditorToolType toolType)
        {
            UpdateEditToolbarSelection(GetToolbarButtonForTool(toolType));
        }

        private Control? GetToolbarButtonForTool(EditorToolType toolType)
        {
            return toolType switch
            {
                EditorToolType.Shapes => EditShapesButton,
                EditorToolType.Pen => EditPenButton,
                EditorToolType.Highlighter => EditHighlighterButton,
                EditorToolType.Text => EditTextButton,
                EditorToolType.Eraser => EditEraserButton,
                EditorToolType.Emoji => EditEmojiButton,
                _ => null
            };
        }

        private void UpdateEditToolbarSelection(Control? selectedButton)
        {
            // Brush para botón seleccionado (usando recursos de tema nativos)
            var selectedBrush = Application.Current.Resources["ControlFillColorSecondaryBrush"] as SolidColorBrush 
                ?? BrushCache.GetBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255));
            var transparentBrush = BrushCache.Transparent;

            // Reset all edit buttons
            EditShapesButton.Background = transparentBrush;
            EditPenButton.Background = transparentBrush;
            EditHighlighterButton.Background = transparentBrush;
            EditEraserButton.Background = transparentBrush;
            EditEmojiButton.Background = transparentBrush;
            EditTextButton.Background = transparentBrush;
            EditCropButton.Background = transparentBrush;
            EditOcrButton.Background = transparentBrush;

            // Highlight selected
            if (ImageEditor.IsCropping)
            {
                EditCropButton.Background = selectedBrush;
                return;
            }

            if (selectedButton != null)
            {
                selectedButton.Background = selectedBrush;
            }
        }

        private void SetCropUiState(bool isCropping)
        {
            CaptureButtonsPanel.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            ActionButtonsPanel.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;

            EditShapesButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditPenButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditHighlighterButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditEraserButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditEmojiButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditTextButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditCropButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditOcrButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditToolbarSeparator.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditUndoButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;
            EditRedoButton.Visibility = isCropping ? Visibility.Collapsed : Visibility.Visible;

            CropApplyButton.Visibility = isCropping ? Visibility.Visible : Visibility.Collapsed;
            CropCancelButton.Visibility = isCropping ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateOcrUiState()
        {
            if (EditOcrButton == null)
            {
                return;
            }

            var hasImage = ImageEditor.Visibility == Visibility.Visible && ImageEditor.HasImage;
            var isRunning = ImageEditor.IsOcrRunning;
            EditOcrButton.IsEnabled = hasImage && !isRunning;
            EditOcrCopyAllButton.IsEnabled = hasImage && !isRunning && ImageEditor.HasOcrResult;
            var tooltip = isRunning
                ? "Analizando texto..."
                : ImageEditor.HasOcrResult
                    ? "Ocultar texto"
                    : "Extraer texto";
            ToolTipService.SetToolTip(EditOcrButton, tooltip);
        }

        /// <summary>
        /// Inicializa los colores de los iconos de bolígrafo y resaltador según los colores por defecto
        /// </summary>
        private void InitializeEditPenAndHighlighterIcons()
        {
            // Obtener los colores actuales de las herramientas del ImageEditor
            var penColor = ImageEditor.GetPenColor();
            var highlighterColor = ImageEditor.GetHighlighterColor();
            
            EditPenIcon.Foreground = BrushCache.GetBrush(penColor);
            EditHighlighterIcon.Foreground = BrushCache.GetBrush(highlighterColor);
        }

        #endregion
    }
}
