using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Graphics;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Models;

namespace SnipShot.Features.Capture.Modes.WindowCapture
{
    /// <summary>
    /// Control de modo de captura de ventana.
    /// Se carga dentro del ShadeOverlayWindow.
    /// </summary>
    public sealed partial class WindowCaptureControl : CaptureModeBase
    {
        #region Campos privados

        private double _rasterizationScale = 1.0;
        private WindowInfo? _currentWindow;
        private bool _selectionCompleted;

        #endregion

        #region Propiedades

        /// <inheritdoc/>
        public override CaptureMode Mode => CaptureMode.Window;

        #endregion

        #region Constructor

        public WindowCaptureControl()
        {
            InitializeComponent();
            Loaded += OnControlLoaded;
        }

        #endregion

        #region Métodos de ciclo de vida

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            _rasterizationScale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            RootGrid.Focus(FocusState.Programmatic);
            
            PositionFloatingMenu();
            UpdateCaptureModeIcon("&#xF7ED;");
        }

        /// <inheritdoc/>
        protected override void OnActivated()
        {
            base.OnActivated();
            ClearHighlight();
            _selectionCompleted = false;
            
            // Establecer cursor crosshair por defecto
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
        }

        /// <inheritdoc/>
        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            ClearHighlight();
        }

        /// <inheritdoc/>
        protected override void OnCleanup()
        {
            base.OnCleanup();
            Loaded -= OnControlLoaded;
            ClearHighlight();
        }

        #endregion

        #region Posicionamiento

        private void PositionFloatingMenu()
        {
            var primaryMonitor = WindowHelper.GetPrimaryMonitorBounds();
            
            double centerX = primaryMonitor.X - VirtualBounds.X + (primaryMonitor.Width / 2.0);
            double centerY = primaryMonitor.Y - VirtualBounds.Y + 20;
            
            double menuWidth = 120;
            
            Canvas.SetLeft(CaptureFloatingMenu, centerX - (menuWidth / 2));
            Canvas.SetTop(CaptureFloatingMenu, centerY);
        }

        private void UpdateCaptureModeIcon(string iconGlyph)
        {
            var glyphCode = iconGlyph.Replace("&#x", "").Replace(";", "");
            var glyphChar = (char)Convert.ToInt32(glyphCode, 16);
            CaptureModeIcon.Glyph = glyphChar.ToString();
        }

        #endregion

        #region Pointer Handling

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive || _selectionCompleted) return;
            UpdateHighlight(e.GetCurrentPoint(RootGrid).Position);
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive || _selectionCompleted) return;

            var pointerPoint = e.GetCurrentPoint(RootGrid);

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                if (_currentWindow.HasValue)
                {
                    CompleteSelection(_currentWindow.Value);
                }
            }
            else if (pointerPoint.Properties.IsRightButtonPressed)
            {
                RaiseCaptureCancelled();
            }
        }

        #endregion

        #region Window Detection

        private void UpdateHighlight(Point position)
        {
            if (AvailableWindows == null || AvailableWindows.Count == 0)
            {
                ClearHighlight();
                return;
            }

            var screenPoint = CoordinateConverter.ConvertToScreenPoint(position, VirtualBounds, _rasterizationScale);
            WindowInfo? window = FindWindowAtPoint(screenPoint);

            if (window.HasValue)
            {
                ShowHighlight(window.Value);
            }
            else
            {
                ClearHighlight();
            }
        }

        private WindowInfo? FindWindowAtPoint(PointInt32 screenPoint)
        {
            if (AvailableWindows == null) return null;

            foreach (var window in AvailableWindows)
            {
                if (Contains(window.Bounds, screenPoint))
                {
                    return window;
                }
            }

            return null;
        }

        private static bool Contains(RectInt32 rect, PointInt32 point)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return false;

            return point.X >= rect.X && point.X < rect.X + rect.Width
                && point.Y >= rect.Y && point.Y < rect.Y + rect.Height;
        }

        private void ShowHighlight(WindowInfo window)
        {
            _currentWindow = window;

            var uiRect = CoordinateConverter.ConvertToUiRect(window.Bounds, VirtualBounds, _rasterizationScale);

            Canvas.SetLeft(HighlightBorder, uiRect.X);
            Canvas.SetTop(HighlightBorder, uiRect.Y);
            HighlightBorder.Width = uiRect.Width;
            HighlightBorder.Height = uiRect.Height;
            HighlightBorder.Visibility = Visibility.Visible;

            SetShadeVisibility(true);
            UILayoutManager.UpdateShadeRectangles(
                TopShade,
                BottomShade,
                LeftShade,
                RightShade,
                RootGrid.ActualWidth,
                RootGrid.ActualHeight,
                uiRect.X,
                uiRect.Y,
                uiRect.Width,
                uiRect.Height);

            // Cambiar cursor a normal cuando está sobre una ventana
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        }

        private void ClearHighlight()
        {
            _currentWindow = null;
            HighlightBorder.Visibility = Visibility.Collapsed;
            SetShadeVisibility(false);
            
            // Cambiar cursor a crosshair cuando no hay ventana seleccionada
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Cross);
        }

        private void SetShadeVisibility(bool isVisible)
        {
            UILayoutManager.SetShadeVisibility(TopShade, BottomShade, LeftShade, RightShade, isVisible);
            // Notificar al ShadeOverlayWindow para que oculte/muestre el shade global
            RaiseLocalShadesVisibilityChanged(isVisible);
        }

        #endregion

        #region Actions

        private void CompleteSelection(WindowInfo window)
        {
            if (_selectionCompleted) return;

            _selectionCompleted = true;

            // Devolver la región de la ventana seleccionada
            RaiseCaptureCompleted(null, window.Bounds);
        }

        #endregion

        #region Floating Menu Handlers

        private void FloatingRectangular_Click(object sender, RoutedEventArgs e)
        {
            UpdateCaptureModeIcon("&#xF407;");
            RaiseModeChangeRequested(CaptureMode.Rectangular);
        }

        private void FloatingWindow_Click(object sender, RoutedEventArgs e)
        {
            // Ya estamos en modo ventana
            UpdateCaptureModeIcon("&#xF7ED;");
        }

        private void FloatingFullScreen_Click(object sender, RoutedEventArgs e)
        {
            UpdateCaptureModeIcon("&#xE9A6;");
            RaiseModeChangeRequested(CaptureMode.FullScreen);
        }

        private void FloatingFreeForm_Click(object sender, RoutedEventArgs e)
        {
            UpdateCaptureModeIcon("&#xF408;");
            RaiseModeChangeRequested(CaptureMode.FreeForm);
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseModeChangeRequested(CaptureMode.ColorPicker);
        }

        private void FloatingClose_Click(object sender, RoutedEventArgs e)
        {
            RaiseCaptureCancelled();
        }

        private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            RaiseCaptureCancelled();
        }

        #endregion
    }
}
