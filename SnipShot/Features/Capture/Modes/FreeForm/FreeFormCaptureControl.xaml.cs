using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Helpers.WindowManagement;
using VirtualKey = Windows.System.VirtualKey;

namespace SnipShot.Features.Capture.Modes.FreeForm
{
    /// <summary>
    /// Control de modo de captura de forma libre.
    /// Se carga dentro del ShadeOverlayWindow.
    /// </summary>
    public sealed partial class FreeFormCaptureControl : CaptureModeBase
    {
        #region Campos privados

        private readonly List<Point> _uiPoints = new();
        private readonly List<PointInt32> _screenPoints = new();

        private bool _isDrawing;
        private bool _hasSelection;
        private bool _selectionCompleted;

        private double _rasterizationScale = 1.0;

        private PathGeometry? _pathGeometry;
        private PathFigure? _pathFigure;
        private PolyLineSegment? _polyLineSegment;
        private Rect _currentBounds = Rect.Empty;

        private const double MIN_DISTANCE_BETWEEN_POINTS = 2.0;
        private const int MIN_POINTS_THRESHOLD = 12;
        private const double MIN_BOUNDS_SIZE = 12.0;

        #endregion

        #region Propiedades

        /// <inheritdoc/>
        public override CaptureMode Mode => CaptureMode.FreeForm;

        #endregion

        #region Constructor

        public FreeFormCaptureControl()
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
            PositionInfoPanel();
            UpdateCaptureModeIcon("&#xF408;");
        }

        /// <inheritdoc/>
        protected override void OnActivated()
        {
            base.OnActivated();
            ResetDrawingState();
        }

        /// <inheritdoc/>
        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            ResetDrawingState();
        }

        /// <inheritdoc/>
        protected override void OnCleanup()
        {
            base.OnCleanup();
            Loaded -= OnControlLoaded;
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

        private void PositionInfoPanel()
        {
            var primaryMonitor = WindowHelper.GetPrimaryMonitorBounds();
            
            double posX = primaryMonitor.X - VirtualBounds.X + 32;
            double posY = primaryMonitor.Y - VirtualBounds.Y + 32;
            
            Canvas.SetLeft(InfoPanel, posX);
            Canvas.SetTop(InfoPanel, posY);
        }

        private void UpdateCaptureModeIcon(string iconGlyph)
        {
            var glyphCode = iconGlyph.Replace("&#x", "").Replace(";", "");
            var glyphChar = (char)Convert.ToInt32(glyphCode, 16);
            CaptureModeIcon.Glyph = glyphChar.ToString();
        }

        #endregion

        #region Pointer Handling

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive || _selectionCompleted) return;

            var pointerPoint = e.GetCurrentPoint(RootGrid);

            if (pointerPoint.Properties.IsRightButtonPressed)
            {
                RaiseCaptureCancelled();
                return;
            }

            if (!pointerPoint.Properties.IsLeftButtonPressed) return;

            StartNewSelection(pointerPoint.Position);
            RootGrid.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive || !_isDrawing || _selectionCompleted) return;

            var pointerPoint = e.GetCurrentPoint(RootGrid);
            if (pointerPoint.IsInContact)
            {
                AddPoint(pointerPoint.Position);
                e.Handled = true;
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!IsActive || !_isDrawing || _selectionCompleted) return;

            var pointerPoint = e.GetCurrentPoint(RootGrid);
            AddPoint(pointerPoint.Position);
            FinishDrawing();
            RootGrid.ReleasePointerCaptures();
            e.Handled = true;
        }

        private void RootGrid_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_isDrawing)
            {
                ResetDrawingState();
            }
        }

        #endregion

        #region Drawing Helpers

        private void StartNewSelection(Point startPoint)
        {
            ResetDrawingState();

            _isDrawing = true;
            _hasSelection = false;

            EnsureGeometry();
            AddPoint(startPoint, force: true);

            SelectionPath.Visibility = Visibility.Visible;
            CaptureFloatingMenu.Visibility = Visibility.Collapsed;
            InfoTitleText.Text = "Trazando...";
            InfoSubtitleText.Text = "Suelta para completar la forma.";
            
            // Inicializar el shade invertido y notificar que los shades locales están activos
            InitializeInvertedShade();
            RaiseLocalShadesVisibilityChanged(true);
        }

        private void ResetDrawingState()
        {
            _isDrawing = false;
            _hasSelection = false;
            _selectionCompleted = false;

            _uiPoints.Clear();
            _screenPoints.Clear();

            _pathGeometry = null;
            _pathFigure = null;
            _polyLineSegment = null;

            SelectionPath.Data = null;
            SelectionPath.Visibility = Visibility.Collapsed;
            
            // Limpiar campos del shade invertido
            _invertedShadeGeometry = null;
            _screenRectGeometry = null;
            _holeGeometry = null;
            _holeFigure = null;
            _holeSegment = null;
            
            // Ocultar shade invertido y notificar que no hay shades locales
            InvertedShadePath.Data = null;
            InvertedShadePath.Visibility = Visibility.Collapsed;
            RaiseLocalShadesVisibilityChanged(false);

            FloatingToolbar.Visibility = Visibility.Collapsed;
            CaptureFloatingMenu.Visibility = Visibility.Visible;

            _currentBounds = Rect.Empty;

            InfoTitleText.Text = "Dibuja el área a capturar";
            InfoSubtitleText.Text = "Mantén presionado el botón izquierdo y traza la forma libre.";
            InfoPanel.Visibility = Visibility.Visible;

            RootGrid.ReleasePointerCaptures();
        }

        private void EnsureGeometry()
        {
            if (_pathGeometry != null && _pathFigure != null && _polyLineSegment != null)
            {
                return;
            }

            _polyLineSegment = new PolyLineSegment();
            _pathFigure = new PathFigure
            {
                IsClosed = true,
                Segments = { _polyLineSegment }
            };

            _pathGeometry = new PathGeometry();
            _pathGeometry.Figures.Add(_pathFigure);
            SelectionPath.Data = _pathGeometry;
        }

        private void AddPoint(Point point, bool force = false)
        {
            if (_uiPoints.Count > 0 && !force)
            {
                Point lastPoint = _uiPoints[^1];
                double dx = point.X - lastPoint.X;
                double dy = point.Y - lastPoint.Y;
                if ((dx * dx + dy * dy) < (MIN_DISTANCE_BETWEEN_POINTS * MIN_DISTANCE_BETWEEN_POINTS))
                {
                    return;
                }
            }

            _uiPoints.Add(point);

            var screenPoint = CoordinateConverter.ConvertToScreenPoint(point, VirtualBounds, _rasterizationScale);
            _screenPoints.Add(screenPoint);

            EnsureGeometry();

            if (_uiPoints.Count == 1)
            {
                _pathFigure!.StartPoint = point;
            }
            else
            {
                _polyLineSegment!.Points.Add(point);
            }

            UpdateBounds(point);
            UpdateInfoPanel();
            
            // Actualizar el shade invertido en tiempo real mientras se dibuja
            if (_isDrawing)
            {
                UpdateInvertedShade();
            }
        }

        private void UpdateBounds(Point point)
        {
            if (_currentBounds == Rect.Empty)
            {
                _currentBounds = new Rect(point.X, point.Y, 0, 0);
                return;
            }

            double minX = Math.Min(_currentBounds.X, point.X);
            double minY = Math.Min(_currentBounds.Y, point.Y);
            double maxX = Math.Max(_currentBounds.X + _currentBounds.Width, point.X);
            double maxY = Math.Max(_currentBounds.Y + _currentBounds.Height, point.Y);

            _currentBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void FinishDrawing()
        {
            _isDrawing = false;

            if (_uiPoints.Count < MIN_POINTS_THRESHOLD ||
                _currentBounds.Width < MIN_BOUNDS_SIZE ||
                _currentBounds.Height < MIN_BOUNDS_SIZE)
            {
                ResetDrawingState();
                return;
            }

            _hasSelection = true;
            InfoTitleText.Text = GetDimensionText();
            InfoSubtitleText.Text = $"{_screenPoints.Count} puntos trazados";
            
            // El shade invertido ya se actualizó en tiempo real, solo asegurar la actualización final
            UpdateInvertedShade();
            
            ShowFloatingToolbar();
        }

        // Campos para el shade invertido reutilizable
        private GeometryGroup? _invertedShadeGeometry;
        private RectangleGeometry? _screenRectGeometry;
        private PathGeometry? _holeGeometry;
        private PathFigure? _holeFigure;
        private PolyLineSegment? _holeSegment;

        /// <summary>
        /// Inicializa la estructura del shade invertido al comenzar el dibujo.
        /// </summary>
        private void InitializeInvertedShade()
        {
            // Crear rectángulo que cubre toda la pantalla
            _screenRectGeometry = new RectangleGeometry
            {
                Rect = new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight)
            };

            // Crear la figura del agujero
            _holeSegment = new PolyLineSegment();
            _holeFigure = new PathFigure
            {
                IsClosed = true,
                Segments = { _holeSegment }
            };

            _holeGeometry = new PathGeometry();
            _holeGeometry.Figures.Add(_holeFigure);

            // Combinar con FillRule.EvenOdd para crear el efecto de agujero
            _invertedShadeGeometry = new GeometryGroup
            {
                FillRule = FillRule.EvenOdd
            };
            _invertedShadeGeometry.Children.Add(_screenRectGeometry);
            _invertedShadeGeometry.Children.Add(_holeGeometry);

            InvertedShadePath.Data = _invertedShadeGeometry;
            InvertedShadePath.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Actualiza el shade invertido con los puntos actuales del path de selección.
        /// Se llama en tiempo real mientras se dibuja.
        /// </summary>
        private void UpdateInvertedShade()
        {
            if (_holeFigure == null || _holeSegment == null || _pathFigure == null || _polyLineSegment == null)
                return;

            // Actualizar el punto de inicio
            _holeFigure.StartPoint = _pathFigure.StartPoint;

            // Sincronizar los puntos del segmento
            _holeSegment.Points.Clear();
            foreach (var point in _polyLineSegment.Points)
            {
                _holeSegment.Points.Add(point);
            }
        }

        /// <summary>
        /// Crea un shade que cubre toda la pantalla excepto el área del path de selección.
        /// Usa un GeometryGroup con FillRule.EvenOdd para crear el efecto de "agujero".
        /// </summary>
        [Obsolete("Use InitializeInvertedShade y UpdateInvertedShade para actualización en tiempo real")]
        private void CreateInvertedShade()
        {
            if (_pathGeometry == null) return;

            // Crear rectángulo que cubre toda la pantalla
            var screenRect = new RectangleGeometry
            {
                Rect = new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight)
            };

            // Crear una copia del path de selección para el agujero
            var holeFigure = new PathFigure
            {
                StartPoint = _pathFigure?.StartPoint ?? new Point(0, 0),
                IsClosed = true
            };
            
            if (_polyLineSegment != null)
            {
                var holeSegment = new PolyLineSegment();
                foreach (var point in _polyLineSegment.Points)
                {
                    holeSegment.Points.Add(point);
                }
                holeFigure.Segments.Add(holeSegment);
            }

            var holeGeometry = new PathGeometry();
            holeGeometry.Figures.Add(holeFigure);

            // Combinar con FillRule.EvenOdd para crear el efecto de agujero
            var combinedGeometry = new GeometryGroup
            {
                FillRule = FillRule.EvenOdd
            };
            combinedGeometry.Children.Add(screenRect);
            combinedGeometry.Children.Add(holeGeometry);

            InvertedShadePath.Data = combinedGeometry;
            InvertedShadePath.Visibility = Visibility.Visible;
            
            // Notificar que los shades locales están activos para ocultar el global
            RaiseLocalShadesVisibilityChanged(true);
        }

        private string GetDimensionText()
        {
            int width = (int)Math.Round(_currentBounds.Width * _rasterizationScale);
            int height = (int)Math.Round(_currentBounds.Height * _rasterizationScale);
            return $"{width} × {height} px";
        }

        private void UpdateInfoPanel()
        {
            if (_uiPoints.Count <= 1)
            {
                InfoPanel.Visibility = Visibility.Visible;
                return;
            }

            InfoTitleText.Text = GetDimensionText();
            InfoSubtitleText.Text = $"{_screenPoints.Count} puntos trazados";
        }

        private void ShowFloatingToolbar()
        {
            if (_currentBounds == Rect.Empty) return;

            UILayoutManager.PositionFloatingToolbar(
                FloatingToolbar,
                RootGrid.ActualWidth,
                RootGrid.ActualHeight,
                _currentBounds.X,
                _currentBounds.Y,
                _currentBounds.Width,
                _currentBounds.Height);
        }

        #endregion

        #region Actions

        private async void CompleteSelection()
        {
            if (_selectionCompleted || !_hasSelection) return;

            _selectionCompleted = true;
            RootGrid.IsHitTestVisible = false;

            var bounds = FreeFormCaptureHelper.CalculateBoundingRect(_screenPoints);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                RaiseCaptureCancelled();
                return;
            }

            // Crear bitmap con la selección
            var bitmap = await FreeFormCaptureHelper.CreateMaskedBitmapFromBackgroundAsync(
                BackgroundBitmap,
                VirtualBounds,
                _screenPoints);

            if (bitmap != null)
            {
                RaiseCaptureCompleted(bitmap, bounds);
            }
            else
            {
                RaiseCaptureCancelled();
            }
        }

        private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await HandleImmediateActionAsync(async () =>
            {
                var bitmap = await CaptureSelectionBitmapAsync();
                var hwnd = GetWindowHandle();
                return await ToolbarActionHandler.HandleSaveAction(bitmap, hwnd);
            });
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await HandleImmediateActionAsync(async () =>
            {
                var bitmap = await CaptureSelectionBitmapAsync();
                return await ToolbarActionHandler.HandleCopyAction(bitmap);
            });
        }

        private void CaptureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CompleteSelection();
        }

        private void ConfirmButton_Click(SplitButton sender, SplitButtonClickEventArgs e)
        {
            CompleteSelection();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseCaptureCancelled();
        }

        private async Task HandleImmediateActionAsync(Func<Task<ToolbarActionHandler.ActionResult>> action)
        {
            if (_selectionCompleted || !_hasSelection) return;

            _selectionCompleted = true;
            await PrepareUIForActionAsync();

            var result = await action();

            // Cerrar después de la acción
            RaiseCaptureCancelled();
        }

        private async Task PrepareUIForActionAsync()
        {
            RootGrid.IsHitTestVisible = false;
            FloatingToolbar.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Collapsed;
            SelectionPath.Visibility = Visibility.Collapsed;
            await Task.Yield();
        }

        private async Task<SoftwareBitmap?> CaptureSelectionBitmapAsync()
        {
            if (!_hasSelection || _screenPoints.Count < 3) return null;

            return await FreeFormCaptureHelper.CreateMaskedBitmapFromBackgroundAsync(
                BackgroundBitmap,
                VirtualBounds,
                _screenPoints);
        }

        #endregion

        #region Keyboard Handling

        private void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (!IsActive) return;

            if (e.Key == VirtualKey.Escape)
            {
                // Cancelar la captura
                RaiseCaptureCancelled();
                e.Handled = true;
            }
        }

        private void EscapeAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsActive) return;
            
            RaiseCaptureCancelled();
            args.Handled = true;
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
            UpdateCaptureModeIcon("&#xF7ED;");
            RaiseModeChangeRequested(CaptureMode.Window);
        }

        private void FloatingFullScreen_Click(object sender, RoutedEventArgs e)
        {
            UpdateCaptureModeIcon("&#xE9A6;");
            RaiseModeChangeRequested(CaptureMode.FullScreen);
        }

        private void FloatingFreeForm_Click(object sender, RoutedEventArgs e)
        {
            // Ya estamos en modo forma libre
            UpdateCaptureModeIcon("&#xF408;");
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseModeChangeRequested(CaptureMode.ColorPicker);
        }

        private void FloatingClose_Click(object sender, RoutedEventArgs e)
        {
            RaiseCaptureCancelled();
        }

        #endregion
    }
}
