using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Input;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Helpers.WindowManagement;
using SnipShot.Models;
using SnipShot.Features.Capture.Annotations.Managers;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Features.Capture.Annotations.Tools;
using SnipShot.Features.Capture.Toolbars;
using SnipShot.Features.Capture.Modes.Base;
using SnipShot.Shared.Controls.Toolbars.Shapes;
using Windows.UI;
using Microsoft.UI;
using WinUICore = Windows.UI.Core;
using WinUIText = Windows.UI.Text;

namespace SnipShot.Features.Capture.Modes.Rectangular
{
    /// <summary>
    /// Control de modo de captura rectangular.
    /// Se carga dentro del ShadeOverlayWindow.
    /// </summary>
    public sealed partial class RectangularCaptureControl : CaptureModeBase
    {
        #region Campos privados

        private Point _startPoint;
        private Point _currentPoint;
        private bool _isSelecting;
        private double _rasterizationScale = 1.0;
        private bool _selectionCompleted;

        // Monitor bounds for limiting selection to current monitor
        private RectInt32 _currentMonitorBounds;

        // Selection state management
        private SelectionState _state = SelectionState.None;
        private bool _isDraggingHandle = false;
        private string? _activeHandle = null;
        private Point _handleDragStart;
        private Rect _selectionBeforeResize;

        private ResizeHandleManager.HandleSet? _handleSet;

        // Cursores de resize para handles
        private readonly InputCursor _sizeNSCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        private readonly InputCursor _sizeWECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        private readonly InputCursor _sizeNWSECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
        private readonly InputCursor _sizeNESWCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);
        
        // UI Brushes (usando recursos de tema nativos)
        private static SolidColorBrush SelectedButtonBrush => 
            Application.Current.Resources["ControlFillColorSecondaryBrush"] as SolidColorBrush 
            ?? BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
        private static SolidColorBrush TransparentBrush => BrushCache.Transparent;
        
        // Shape drawing state (start point tracked locally for coordinate limiting)
        private Point _shapeStartPoint;
        
        // Undo/Redo - using AnnotationHistoryManager
        private AnnotationHistoryManager? _historyManager;
        
        // Annotation drawing - using AnnotationManager
        private AnnotationManager? _annotationManager;

        // Shape selection and manipulation - using ShapeManipulationManager
        private ShapeManipulationManager? _shapeManipulation;

        // Text manipulation - using TextManipulationManager
        private TextManipulationManager? _textManipulation;

        // Emoji manipulation - using EmojiManipulationManager
        private EmojiManipulationManager? _emojiManipulation;

        // Toolbar positioning - using FloatingToolbarManager
        private FloatingToolbarManager? _toolbarManager;

        // Text editing state
        private Grid? _activeTextElement;
        private bool _isEditingText;
        private bool _isNewTextElement;
        private bool _isEraserActive;
        private bool _isErasing;

        // Border settings
        private bool _borderEnabled;
        private string _borderColorHex = "#FF000000";
        private double _borderThickness = 1.0;

        #endregion

        #region Propiedades

        /// <inheritdoc/>
        public override CaptureMode Mode => CaptureMode.Rectangular;

        /// <summary>
        /// Bitmap capturado con anotaciones
        /// </summary>
        public SoftwareBitmap? CapturedBitmap { get; private set; }

        #endregion

        #region Constructor

        public RectangularCaptureControl()
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

            // Inicializar el AnnotationHistoryManager para undo/redo
            _historyManager = new AnnotationHistoryManager(ShapesCanvas);
            _historyManager.HistoryChanged += (s, args) => UpdateUndoRedoButtonStates();

            // Inicializar el AnnotationManager para dibujo de anotaciones
            _annotationManager = new AnnotationManager(ShapesCanvas);
            _annotationManager.StrokeCompleted += OnAnnotationStrokeCompleted;

            // Inicializar el ShapeManipulationManager para selección, arrastre y redimensionado
            _shapeManipulation = new ShapeManipulationManager(ShapesCanvas, ShapeHandlesCanvas, RootGrid, _historyManager);
            _shapeManipulation.SelectionChanged += OnShapeSelectionChanged;
            _shapeManipulation.ShapeModified += OnShapeModified;

            // Inicializar el TextManipulationManager para selección y manipulación de texto
            _textManipulation = new TextManipulationManager(ShapesCanvas, ShapeHandlesCanvas, _historyManager);
            _textManipulation.SelectionChanged += OnTextSelectionChanged;
            _textManipulation.TextModified += OnTextModified;

            // Inicializar el EmojiManipulationManager para selección y manipulación de emojis
            _emojiManipulation = new EmojiManipulationManager(ShapesCanvas, ShapeHandlesCanvas, _historyManager);
            _emojiManipulation.SelectionChanged += OnEmojiSelectionChanged;
            _emojiManipulation.EmojiModified += OnEmojiModified;

            // Inicializar el FloatingToolbarManager para posicionamiento de toolbars
            _toolbarManager = new FloatingToolbarManager(OverlayCanvas, RootGrid, FloatingToolbar);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Shapes, FloatingShapesToolbar, ShapesButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Style, FloatingStyleToolbar, FloatingShapesToolbarContent.StyleAnchorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Fill, FloatingFillToolbar, FloatingShapesToolbarContent.FillAnchorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Pen, FloatingPenToolbar, BallpointPenButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Highlighter, FloatingHighlighterToolbar, HighlighterButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Text, FloatingTextToolbar, TextButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.TextColor, FloatingTextColorToolbar, TextColorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.TextHighlight, FloatingTextHighlightToolbar, TextHighlightButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Emoji, FloatingEmojiToolbar, EmojiButton);

            // Inicializar el conjunto de handles
            _handleSet = new ResizeHandleManager.HandleSet
            {
                HandleNW = HandleNW,
                HandleNE = HandleNE,
                HandleSE = HandleSE,
                HandleSW = HandleSW,
                HandleN = HandleN,
                HandleE = HandleE,
                HandleS = HandleS,
                HandleW = HandleW
            };
            
            // Posicionar el menú flotante en el centro de la pantalla principal
            PositionFloatingMenu();
            
            // Actualizar el ícono según el modo actual
            UpdateCaptureModeIcon("&#xF407;"); // Rectangular por defecto

            // Preparar los valores iniciales del menú de estilo
            InitializeStyleControls();
            
            // Inicializar los colores de los iconos de bolígrafo y resaltador
            InitializePenAndHighlighterIcons();
        }

        /// <inheritdoc/>
        protected override void OnActivated()
        {
            base.OnActivated();
            _selectionCompleted = false;
            _state = SelectionState.None;
            
            // Cambiar el cursor a crosshair para indicar modo de selección
            SetCrosshairCursor();
        }

        /// <inheritdoc/>
        protected override void OnDeactivated()
        {
            base.OnDeactivated();
            ResetSelectionState();
            
            // Restaurar el cursor normal
            RestoreDefaultCursor();
        }

        /// <inheritdoc/>
        protected override void OnCleanup()
        {
            base.OnCleanup();
            Loaded -= OnControlLoaded;
            ResetSelectionState();
        }

        private void ResetSelectionState()
        {
            _isSelecting = false;
            _isDraggingHandle = false;
            _state = SelectionState.None;
            UILayoutManager.SetShadeVisibility(TopShade, BottomShade, LeftShade, RightShade, false);
            SelectionBorder.Visibility = Visibility.Collapsed;
            CoordinatesDisplay.Visibility = Visibility.Collapsed;
            FloatingToolbar.Visibility = Visibility.Collapsed;
            CollapseShapesToolbar();
            CollapseFillToolbar();
            CollapseStyleToolbar();
            if (_handleSet != null)
            {
                ResizeHandleManager.HideHandles(_handleSet);
            }
        }

        #endregion

        #region Cursor Management

        /// <summary>
        /// Establece el cursor crosshair para indicar modo de selección
        /// </summary>
        private void SetCrosshairCursor()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
        }

        /// <summary>
        /// Restaura el cursor al valor por defecto (flecha)
        /// </summary>
        private void RestoreDefaultCursor()
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        #endregion

        #region Posicionamiento

        private void PositionFloatingMenu()
        {
            // Obtener los límites de la pantalla principal
            var primaryMonitor = WindowHelper.GetPrimaryMonitorBounds();
            
            // Calcular la posición del centro de la pantalla principal en coordenadas del overlay
            double centerX = primaryMonitor.X - VirtualBounds.X + (primaryMonitor.Width / 2.0);
            double centerY = primaryMonitor.Y - VirtualBounds.Y + 20; // 20px desde arriba
            
            // Obtener el ancho del menú (aproximado)
            double menuWidth = 120; // Ancho aproximado del menú
            
            // Posicionar el menú
            Canvas.SetLeft(CaptureFloatingMenu, centerX - (menuWidth / 2));
            Canvas.SetTop(CaptureFloatingMenu, centerY);
        }

        private void UpdateCaptureModeIcon(string iconGlyph)
        {
            var glyphCode = iconGlyph.Replace("&#x", "").Replace(";", "");
            var glyphChar = (char)Convert.ToInt32(glyphCode, 16);
            CaptureModeIcon.Glyph = glyphChar.ToString();
        }

        private void InitializeStyleControls()
        {
            // Los valores iniciales se toman del AnnotationManager que ya está inicializado
            // con los defaults de AnnotationSettings.DefaultShape
            if (_annotationManager != null)
            {
                var settings = _annotationManager.ShapeSettings;
                
                // Inicializar StyleToolbarControl
                if (FloatingStyleToolbarContent != null)
                {
                    FloatingStyleToolbarContent.SetStyle(
                        settings.StrokeColor,
                        settings.StrokeOpacity * 100,
                        settings.StrokeThickness);
                }

                // Inicializar FillToolbarControl
                if (FloatingFillToolbarContent != null)
                {
                    FloatingFillToolbarContent.SetFill(
                        settings.FillColor,
                        settings.FillOpacity * 100);
                }
            }
        }

        private SolidColorBrush CreateShapeStrokeBrush()
        {
            if (_annotationManager == null)
                return BrushCache.GetBrush(Colors.Red);
                
            var settings = _annotationManager.ShapeSettings;
            return new SolidColorBrush(settings.StrokeColor)
            {
                Opacity = settings.StrokeOpacity
            };
        }

        private void RefreshCurrentShapeVisual()
        {
            if (_annotationManager == null) return;
            
            var settings = _annotationManager.ShapeSettings;
            var activeShapeType = GetActiveShapeTypeString();
            
            // Actualizar la forma seleccionada si existe
            var selectedShape = _shapeManipulation?.SelectedShape;
            if (selectedShape != null && selectedShape.Tag is ShapeData data)
            {
                selectedShape.Stroke = CreateShapeStrokeBrush();
                selectedShape.StrokeThickness = settings.StrokeThickness;

                if ((data.Type == "Square" || data.Type == "Circle" || data.Type == "Star") && 
                    settings.FillEnabled && settings.FillOpacity > 0)
                {
                    selectedShape.Fill = new SolidColorBrush(settings.FillColor)
                    {
                        Opacity = settings.FillOpacity
                    };
                }
                else
                {
                    selectedShape.Fill = TransparentBrush;
                }
            }
        }

        public void SetBorderSettings(bool enabled, string colorHex, double thickness)
        {
            _borderEnabled = enabled;
            _borderColorHex = colorHex;
            _borderThickness = thickness;
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_selectionCompleted)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(RootGrid);
            if (!pointerPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Al hacer clic fuera de los toolbars, cerrar menús secundarios EXCEPTO el menú de formas
            // El menú de formas solo se cierra al hacer clic en otro botón
            if (_toolbarManager != null &&
                !IsPointerOverToolbar(e.OriginalSource as DependencyObject))
            {
                CollapseSecondaryToolbarsExceptShapes();
            }
            if (_isEraserActive && _state == SelectionState.Selected)
            {
                Rect selectionRect = GetCurrentSelectionRect();
                if (selectionRect.Contains(pointerPoint.Position))
                {
                    _isErasing = true;
                    EraseAtPoint(pointerPoint.Position);
                    RootGrid.CapturePointer(e.Pointer);
                    e.Handled = true;
                    return;
                }
            }

            // Si estamos en modo bolígrafo, iniciar trazo
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Pen && _state == SelectionState.Selected)
            {
                Rect selectionRect = GetCurrentSelectionRect();
                if (selectionRect.Contains(pointerPoint.Position))
                {
                    _annotationManager.StartStroke(pointerPoint.Position);
                    RootGrid.CapturePointer(e.Pointer);
                    e.Handled = true;
                    return;
                }
            }

            // Si estamos en modo resaltador, iniciar trazo
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Highlighter && _state == SelectionState.Selected)
            {
                Rect selectionRect = GetCurrentSelectionRect();
                if (selectionRect.Contains(pointerPoint.Position))
                {
                    _annotationManager.StartStroke(pointerPoint.Position);
                    RootGrid.CapturePointer(e.Pointer);
                    e.Handled = true;
                    return;
                }
            }

            // Si estamos en modo texto, crear elemento de texto o finalizar edición actual
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Text && _state == SelectionState.Selected)
            {
                Rect selectionRect = GetCurrentSelectionRect();
                if (selectionRect.Contains(pointerPoint.Position))
                {
                    HandleTextToolClick(pointerPoint.Position);
                    e.Handled = true;
                    return;
                }
            }
            
            // Si estamos en modo de dibujo de forma, iniciar el dibujo
            if (_annotationManager != null && AnnotationManager.IsShapeTool(_annotationManager.ActiveToolType))
            {
                // Verificar que el clic esté dentro del área de selección
                if (_state == SelectionState.Selected)
                {
                    Rect selectionRect = GetCurrentSelectionRect();
                    if (!selectionRect.Contains(pointerPoint.Position))
                    {
                        e.Handled = true;
                        return;
                    }
                }
                
                DeselectShape(); // Deseleccionar cualquier forma activa
                _shapeStartPoint = pointerPoint.Position;
                
                // Iniciar el trazo de forma usando el manager
                _annotationManager.StartStroke(_shapeStartPoint);
                
                e.Handled = true;
                return;
            }

            // Check if we clicked on a shape handle (handled by ShapeManipulationManager internally)
            if (_shapeManipulation?.IsResizing == true)
            {
                e.Handled = true;
                return;
            }

            // Check if we clicked on a shape
            if (_state == SelectionState.Selected)
            {
                // Use ShapeManipulationManager for hit testing
                var hitShape = _shapeManipulation?.GetShapeAtPoint(pointerPoint.Position);
                
                if (hitShape != null)
                {
                    // If already selected, start dragging
                    if (_shapeManipulation?.SelectedShape == hitShape)
                    {
                        _shapeManipulation.StartDrag(pointerPoint.Position);
                        RootGrid.CapturePointer(e.Pointer);
                        e.Handled = true;
                        return;
                    }
                    
                    SelectShape(hitShape);
                    e.Handled = true;
                    return;
                }
                
                // Check if we clicked on a text element (also works when not in Text mode)
                var hitText = _textManipulation?.GetTextAtPoint(pointerPoint.Position) ?? GetTextElementAtPoint(pointerPoint.Position);
                if (hitText != null)
                {
                    // Deselect any selected shape first
                    DeselectShape();
                    
                    // Check for double-click to edit
                    if (_textManipulation?.HandlePotentialDoubleClick(pointerPoint.Position, hitText) == true)
                    {
                        e.Handled = true;
                        return; // Edit mode will be triggered by event
                    }

                    // If already selected, start dragging
                    if (_textManipulation?.SelectedText == hitText)
                    {
                        _textManipulation.StartDrag(pointerPoint.Position);
                        RootGrid.CapturePointer(e.Pointer);
                        e.Handled = true;
                        return;
                    }

                    // Select the text element
                    _textManipulation?.SelectText(hitText);
                    e.Handled = true;
                    return;
                }

                // Check if we clicked on an emoji element (also works when not in Emoji mode)
                var hitEmoji = _emojiManipulation?.GetEmojiAtPoint(pointerPoint.Position);
                if (hitEmoji != null)
                {
                    // Deselect any selected shape or text first
                    DeselectShape();
                    _textManipulation?.Deselect();

                    // If already selected, start dragging
                    if (_emojiManipulation?.SelectedEmoji == hitEmoji)
                    {
                        _emojiManipulation.StartDrag(pointerPoint.Position);
                        RootGrid.CapturePointer(e.Pointer);
                        e.Handled = true;
                        return;
                    }

                    // Select the emoji element
                    _emojiManipulation?.SelectEmoji(hitEmoji);
                    e.Handled = true;
                    return;
                }
                
                // If we clicked outside any shape, deselect
                if (_shapeManipulation?.HasSelection == true)
                {
                    DeselectShape();
                    // Don't return, allow starting a new selection or interacting with main handles
                }
                
                // Deselect any text as well when clicking outside
                if (_textManipulation?.HasSelection == true)
                {
                    _textManipulation.Deselect();
                }

                // Deselect any emoji as well when clicking outside
                if (_emojiManipulation?.HasSelection == true)
                {
                    _emojiManipulation.Deselect();
                }
            }

            // If we're in Selected state, ignore clicks (only handles can modify selection)
            if (_state == SelectionState.Selected)
            {
                e.Handled = true;
                return;
            }

            // Start a new selection
            _state = SelectionState.Selecting;
            _isSelecting = true;
            _startPoint = pointerPoint.Position;
            _currentPoint = _startPoint;

            // Capturar los límites del monitor donde se inició la selección
            // Convertir coordenadas de UI a píxeles físicos usando VirtualBounds offset
            int physicalX = (int)(_startPoint.X * _rasterizationScale) + VirtualBounds.X;
            int physicalY = (int)(_startPoint.Y * _rasterizationScale) + VirtualBounds.Y;
            _currentMonitorBounds = WindowHelper.GetMonitorBoundsAtPoint(physicalX, physicalY);

            SelectionBorder.Visibility = Visibility.Visible;
            SelectionBorder.Width = 0;
            SelectionBorder.Height = 0;
            CoordinatesDisplay.Visibility = Visibility.Collapsed;
            FloatingToolbar.Visibility = Visibility.Collapsed;
            CaptureFloatingMenu.Visibility = Visibility.Collapsed; // Ocultar menú flotante
            CollapseShapesToolbar();
            CollapseFillToolbar();
            CollapseStyleToolbar();
            HideHandles();
            SetShadeVisibility(true);
            UILayoutManager.UpdateShadeRectangles(
                TopShade, BottomShade, LeftShade, RightShade,
                RootGrid.ActualWidth, RootGrid.ActualHeight,
                _startPoint.X, _startPoint.Y, 0, 0);

            RootGrid.CapturePointer(e.Pointer);
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_selectionCompleted)
            {
                return;
            }
            
            var point = e.GetCurrentPoint(RootGrid);
            if (_isEraserActive && _isErasing)
            {
                Point currentPoint = point.Position;

                if (_state == SelectionState.Selected)
                {
                    Rect selectionRect = GetCurrentSelectionRect();
                    currentPoint = new Point(
                        Math.Max(selectionRect.Left, Math.Min(selectionRect.Right, currentPoint.X)),
                        Math.Max(selectionRect.Top, Math.Min(selectionRect.Bottom, currentPoint.Y))
                    );
                }

                EraseAtPoint(currentPoint);
                e.Handled = true;
                return;
            }

            // Si estamos dibujando con el AnnotationManager (pen, highlighter o forma)
            if (_annotationManager?.IsDrawing == true)
            {
                Point currentPoint = point.Position;
                
                // Limitar el punto al área de selección
                if (_state == SelectionState.Selected)
                {
                    Rect selectionRect = GetCurrentSelectionRect();
                    currentPoint = new Point(
                        Math.Max(selectionRect.Left, Math.Min(selectionRect.Right, currentPoint.X)),
                        Math.Max(selectionRect.Top, Math.Min(selectionRect.Bottom, currentPoint.Y))
                    );
                }
                
                // Pass Shift key state for constrained shapes (square/circle)
                _annotationManager.ContinueStroke(currentPoint, IsShiftPressed());
                e.Handled = true;
                return;
            }

            // Delegar arrastre de formas al ShapeManipulationManager
            if (_shapeManipulation?.IsDragging == true)
            {
                _shapeManipulation.ContinueDrag(point.Position);
                e.Handled = true;
                return;
            }

            // Delegar arrastre de emojis al EmojiManipulationManager
            if (_emojiManipulation?.IsDragging == true)
            {
                _emojiManipulation.ContinueDrag(point.Position);
                e.Handled = true;
                return;
            }

            if (_state == SelectionState.Selecting && _isSelecting)
            {
                Point rawPoint = e.GetCurrentPoint(RootGrid).Position;
                
                // Limitar el punto a los bounds del monitor donde se inició la selección
                // Convertir bounds físicos a coordenadas de UI
                double monitorLeft = (_currentMonitorBounds.X - VirtualBounds.X) / _rasterizationScale;
                double monitorTop = (_currentMonitorBounds.Y - VirtualBounds.Y) / _rasterizationScale;
                double monitorRight = ((_currentMonitorBounds.X + _currentMonitorBounds.Width) - VirtualBounds.X) / _rasterizationScale;
                double monitorBottom = ((_currentMonitorBounds.Y + _currentMonitorBounds.Height) - VirtualBounds.Y) / _rasterizationScale;
                
                _currentPoint = new Point(
                    Math.Max(monitorLeft, Math.Min(monitorRight, rawPoint.X)),
                    Math.Max(monitorTop, Math.Min(monitorBottom, rawPoint.Y))
                );
                
                UpdateSelectionVisual();
            }
        }

        private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_selectionCompleted)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(RootGrid);
            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                return;
            }
            if (_isEraserActive && _isErasing)
            {
                _isErasing = false;
                RootGrid.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }

            // Si estamos dibujando con el AnnotationManager, finalizar el trazo
            if (_annotationManager?.IsDrawing == true)
            {
                _annotationManager.EndStroke();
                RootGrid.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }

            // Delegar fin de arrastre al ShapeManipulationManager
            if (_shapeManipulation?.IsDragging == true)
            {
                _shapeManipulation.EndDrag();
                RootGrid.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }

            // Delegar fin de arrastre al EmojiManipulationManager
            if (_emojiManipulation?.IsDragging == true)
            {
                _emojiManipulation.EndDrag();
                RootGrid.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }

            if (_state == SelectionState.Selecting && _isSelecting)
            {
                _currentPoint = pointerPoint.Position;
                _isSelecting = false;
                RootGrid.ReleasePointerCaptures();

                // Check if selection is valid (minimum size from Constants)
                double width = Math.Abs(_currentPoint.X - _startPoint.X);
                double height = Math.Abs(_currentPoint.Y - _startPoint.Y);
                
                if (width >= Constants.MIN_SELECTION_SIZE && height >= Constants.MIN_SELECTION_SIZE)
                {
                    // Transition to Selected state
                    _state = SelectionState.Selected;
                    ShowHandles();
                    ShowFloatingToolbar();
                }
                else
                {
                    // Too small, reset to initial state
                    _state = SelectionState.None;
                    _isSelecting = false;
                    SelectionBorder.Visibility = Visibility.Collapsed;
                    CoordinatesDisplay.Visibility = Visibility.Collapsed;
                    SetShadeVisibility(false);
                }
            }
        }

        private void EraseAtPoint(Point point)
        {
            if (_annotationManager == null || _historyManager == null) return;

            var hitText = _textManipulation?.GetTextAtPoint(point) ?? GetTextElementAtPoint(point);
            if (hitText != null)
            {
                _textManipulation?.Deselect();
                if (ShapesCanvas.Children.Contains(hitText))
                {
                    ShapesCanvas.Children.Remove(hitText);
                    _historyManager.RecordElementRemoved(hitText);
                }
                return;
            }

            var hitPath = _annotationManager.GetPathAtPoint(point);
            if (hitPath != null)
            {
                if (_shapeManipulation?.SelectedShape == hitPath)
                {
                    DeselectShape();
                }

                if (ShapesCanvas.Children.Contains(hitPath))
                {
                    ShapesCanvas.Children.Remove(hitPath);
                    _historyManager.RecordPathRemoved(hitPath);
                }
            }
        }

        private void RootGrid_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            // Cancelar cualquier trazo en progreso usando el manager
            _annotationManager?.CancelStroke();
            _isErasing = false;

            // Solo cancelar si estamos en proceso de selección
            if (_state == SelectionState.Selecting && _isSelecting)
            {
                CancelSelection();
            }
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_selectionCompleted)
            {
                return;
            }

            // Check for Ctrl key combinations
            var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(WinUICore.CoreVirtualKeyStates.Down);

            if (e.Key == VirtualKey.Escape)
            {
                CancelSelection();
                e.Handled = true;
            }
            else if (ctrlPressed && e.Key == VirtualKey.Enter && _state == SelectionState.Selected)
            {
                // Ctrl+Enter: Capturar
                _ = CompleteSelectionAsync();
                e.Handled = true;
            }
            else if (ctrlPressed && e.Key == VirtualKey.S && _state == SelectionState.Selected)
            {
                // Ctrl+S: Guardar directamente
                SaveMenuItem_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrlPressed && e.Key == VirtualKey.C && _state == SelectionState.Selected)
            {
                // Ctrl+C: Copiar al portapapeles
                CopyMenuItem_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrlPressed && e.Key == VirtualKey.Z && UndoButton.IsEnabled)
            {
                UndoButton_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrlPressed && e.Key == VirtualKey.Y && RedoButton.IsEnabled)
            {
                RedoButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted) return;
            
            CancelSelection();
            args.Handled = true;
        }

        private void CaptureAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted || _state != SelectionState.Selected) return;
            
            _ = CompleteSelectionAsync();
            args.Handled = true;
        }

        private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted || _state != SelectionState.Selected) return;
            
            SaveMenuItem_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void CopyAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted || _state != SelectionState.Selected) return;
            
            CopyMenuItem_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void UndoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted || !UndoButton.IsEnabled) return;
            
            UndoButton_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void RedoAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_selectionCompleted || !RedoButton.IsEnabled) return;
            
            RedoButton_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private async Task CompleteSelectionAsync()
        {
            if (_selectionCompleted)
            {
                return;
            }

            _isSelecting = false;
            _isDraggingHandle = false;
            RootGrid.ReleasePointerCaptures();

            var rect = GetSelectionRect();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                CancelSelection();
                return;
            }

            _selectionCompleted = true;
            _state = SelectionState.None;
            
            // Prepare UI for capture
            await PrepareUIForActionAsync();

            // Capture the bitmap
            CapturedBitmap = await CaptureSelectionBitmapAsync(rect);
            
            // Notificar captura completada
            RaiseCaptureCompleted(CapturedBitmap, rect);
        }

        private void CancelSelection()
        {
            if (_selectionCompleted) return;

            _isSelecting = false;
            _isDraggingHandle = false;
            _state = SelectionState.None;
            RootGrid.ReleasePointerCaptures();
            RaiseCaptureCancelled();
        }

        private void UpdateSelectionVisual()
        {
            double x = Math.Min(_startPoint.X, _currentPoint.X);
            double y = Math.Min(_startPoint.Y, _currentPoint.Y);
            double width = Math.Abs(_currentPoint.X - _startPoint.X);
            double height = Math.Abs(_currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionBorder, x);
            Canvas.SetTop(SelectionBorder, y);
            SelectionBorder.Width = width;
            SelectionBorder.Height = height;

            UILayoutManager.UpdateShadeRectangles(
                TopShade, BottomShade, LeftShade, RightShade,
                RootGrid.ActualWidth, RootGrid.ActualHeight,
                x, y, width, height);

            UILayoutManager.UpdateCoordinatesDisplay(
                CoordinatesDisplay, CoordinatesText,
                RootGrid.ActualWidth, RootGrid.ActualHeight,
                x, y, width, height,
                _rasterizationScale);
            
            // Update handles and toolbar if in appropriate state
            if (_state == SelectionState.Adjusting)
            {
                UpdateHandlePositions();
            }
            if (_state == SelectionState.Selected || _state == SelectionState.Adjusting)
            {
                ShowFloatingToolbar();
            }
        }

        private void SetShadeVisibility(bool isVisible)
        {
            UILayoutManager.SetShadeVisibility(TopShade, BottomShade, LeftShade, RightShade, isVisible);
            // Notificar al ShadeOverlayWindow para que oculte/muestre el shade global
            RaiseLocalShadesVisibilityChanged(isVisible);
        }

        private RectInt32 GetSelectionRect()
        {
            return CoordinateConverter.ConvertToScreenRect(_startPoint, _currentPoint, VirtualBounds, _rasterizationScale);
        }

        private Rect GetCurrentSelectionRect()
        {
            return CoordinateConverter.GetNormalizedRect(_startPoint, _currentPoint);
        }

        #endregion

        #region Resize Handles

        private void Handle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_state != SelectionState.Selected || sender is not FrameworkElement handle || handle.Tag is not string tag)
                return;

            // Cambiar cursor según el tipo de handle
            ProtectedCursor = tag switch
            {
                "N" or "S" => _sizeNSCursor,           // Lados horizontales: flecha vertical
                "E" or "W" => _sizeWECursor,           // Lados verticales: flecha horizontal
                "NW" or "SE" => _sizeNWSECursor,       // Diagonal NW-SE
                "NE" or "SW" => _sizeNESWCursor,       // Diagonal NE-SW
                _ => null
            };
        }

        private void Handle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_state != SelectionState.Selected) return;

            // Restaurar cursor por defecto solo si no estamos arrastrando
            if (!_isDraggingHandle)
            {
                ProtectedCursor = null;
            }
        }

        private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_state != SelectionState.Selected || sender is not FrameworkElement handle)
            {
                return;
            }

            _state = SelectionState.Adjusting;
            _isDraggingHandle = true;
            _activeHandle = handle.Tag as string;
            _handleDragStart = e.GetCurrentPoint(RootGrid).Position;
            _selectionBeforeResize = GetCurrentSelectionRect();

            handle.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingHandle || _activeHandle == null || sender is not FrameworkElement handle || _handleSet == null)
            {
                return;
            }

            Point currentPos = e.GetCurrentPoint(RootGrid).Position;
            double deltaX = currentPos.X - _handleDragStart.X;
            double deltaY = currentPos.Y - _handleDragStart.Y;

            // Calcular los nuevos límites usando ResizeHandleManager
            Rect newBounds = ResizeHandleManager.CalculateNewBounds(
                _activeHandle,
                _selectionBeforeResize,
                deltaX,
                deltaY);

            // Limitar los nuevos bounds al monitor actual
            double monitorLeft = (_currentMonitorBounds.X - VirtualBounds.X) / _rasterizationScale;
            double monitorTop = (_currentMonitorBounds.Y - VirtualBounds.Y) / _rasterizationScale;
            double monitorRight = ((_currentMonitorBounds.X + _currentMonitorBounds.Width) - VirtualBounds.X) / _rasterizationScale;
            double monitorBottom = ((_currentMonitorBounds.Y + _currentMonitorBounds.Height) - VirtualBounds.Y) / _rasterizationScale;
            
            // Clamp the bounds to the current monitor
            double clampedLeft = Math.Max(monitorLeft, Math.Min(monitorRight, newBounds.Left));
            double clampedTop = Math.Max(monitorTop, Math.Min(monitorBottom, newBounds.Top));
            double clampedRight = Math.Max(monitorLeft, Math.Min(monitorRight, newBounds.Right));
            double clampedBottom = Math.Max(monitorTop, Math.Min(monitorBottom, newBounds.Bottom));

            // Update selection points
            _startPoint = new Point(clampedLeft, clampedTop);
            _currentPoint = new Point(clampedRight, clampedBottom);

            UpdateSelectionVisual();
            e.Handled = true;
        }

        private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingHandle || sender is not FrameworkElement handle)
            {
                return;
            }

            _isDraggingHandle = false;
            _activeHandle = null;
            _state = SelectionState.Selected;
            ProtectedCursor = null;
            handle.ReleasePointerCaptures();
            e.Handled = true;
        }

        private void Handle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_isDraggingHandle)
            {
                _isDraggingHandle = false;
                _activeHandle = null;
                _state = SelectionState.Selected;
                ProtectedCursor = null;
            }
        }

        private void ShowHandles()
        {
            if (_handleSet == null) return;
            ResizeHandleManager.ShowHandles(_handleSet, GetCurrentSelectionRect());
        }

        private void HideHandles()
        {
            if (_handleSet == null) return;
            ResizeHandleManager.HideHandles(_handleSet);
        }

        private void UpdateHandlePositions()
        {
            if (_state == SelectionState.Selected || _state == SelectionState.Adjusting)
            {
                ShowHandles();
            }
        }

        #endregion

        #region Floating Toolbar

        private void ShowFloatingToolbar()
        {
            if (_toolbarManager == null) return;
            
            var selectionRect = GetCurrentSelectionRect();
            _toolbarManager.UpdateSelectionBounds(selectionRect);
            
            // Actualizar los bounds del monitor para limitar el posicionamiento del toolbar
            double monitorLeft = (_currentMonitorBounds.X - VirtualBounds.X) / _rasterizationScale;
            double monitorTop = (_currentMonitorBounds.Y - VirtualBounds.Y) / _rasterizationScale;
            double monitorWidth = _currentMonitorBounds.Width / _rasterizationScale;
            double monitorHeight = _currentMonitorBounds.Height / _rasterizationScale;
            _toolbarManager.UpdateMonitorBounds(new Rect(monitorLeft, monitorTop, monitorWidth, monitorHeight));
            
            _toolbarManager.ShowMainToolbar();
        }

        private void CollapseShapesToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Shapes);
            ShapesButton.Background = TransparentBrush;
            CollapseStyleToolbar();
            CollapseFillToolbar();
        }

        private void CollapseStyleToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
            FloatingShapesToolbarContent.SetStyleExpanded(false);
        }

        private void CollapseFillToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
            FloatingShapesToolbarContent.SetFillExpanded(false);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelSelection();
        }

        private void ConfirmButton_Click(SplitButton sender, SplitButtonClickEventArgs e)
        {
            _ = CompleteSelectionAsync();
        }

        private void CaptureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Opción 1: Capturar y mostrar en la aplicación
            _ = CompleteSelectionAsync();
        }

        private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Opción 2: Guardar directamente sin mostrar en la aplicación
            if (_selectionCompleted)
            {
                return;
            }

            var rect = GetSelectionRect();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                CancelSelection();
                return;
            }

            // Marcar como completado antes de mostrar el diálogo
            _selectionCompleted = true;

            // Ocultar elementos visuales del overlay antes de capturar
            await PrepareUIForActionAsync();

            // Preparar el bitmap a partir del fondo capturado
            var selectionBitmap = await CaptureSelectionBitmapAsync(rect);

            // Usar el handler para guardar
            var hwnd = GetWindowHandle();
            var result = await ToolbarActionHandler.HandleSaveAction(selectionBitmap, hwnd);

            if (result == ToolbarActionHandler.ActionResult.Failed)
            {
                RaiseCaptureCancelled();
                return;
            }

            // Notificar que se guardó exitosamente (no se envía a MainWindow)
            RaiseCaptureCancelled();
        }

        private async void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Opción 3: Copiar al portapapeles sin mostrar en la aplicación
            if (_selectionCompleted)
            {
                return;
            }

            var rect = GetSelectionRect();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                CancelSelection();
                return;
            }

            // Marcar como completado inmediatamente para evitar múltiples clicks
            _selectionCompleted = true;

            // Ocultar la UI inmediatamente
            await PrepareUIForActionAsync();

            // Preparar el bitmap a partir del fondo capturado
            var selectionBitmap = await CaptureSelectionBitmapAsync(rect);

            // Usar el handler para copiar
            var result = await ToolbarActionHandler.HandleCopyAction(selectionBitmap);

            if (result == ToolbarActionHandler.ActionResult.Failed)
            {
                RaiseCaptureCancelled();
                return;
            }

            // Notificar que se copió exitosamente (no se envía a MainWindow)
            RaiseCaptureCancelled();
        }

        private async Task PrepareUIForActionAsync()
        {
            RootGrid.IsHitTestVisible = false;
            HideHandles();
            DeselectShape();
            FloatingToolbar.Visibility = Visibility.Collapsed;
            CollapseShapesToolbar();
            CollapseFillToolbar();
            CollapseStyleToolbar();
            CoordinatesDisplay.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Collapsed;
            SetShadeVisibility(false);

            await Task.Yield();
        }

        private async Task<SoftwareBitmap?> CaptureSelectionBitmapAsync(RectInt32 rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0 || BackgroundBitmap == null)
            {
                return null;
            }

            try
            {
                // Define capture area (defaults to selection)
                int captureX = rect.X;
                int captureY = rect.Y;
                int captureWidth = rect.Width;
                int captureHeight = rect.Height;

                // Add border if enabled
                Border? captureBorder = null;
                if (_borderEnabled)
                {
                    if (!ColorConverter.TryParseHexColor(_borderColorHex, out var color))
                    {
                        color = Colors.Black; // Fallback color
                    }
                    
                    double expandedWidth = rect.Width + (2 * _borderThickness);
                    double expandedHeight = rect.Height + (2 * _borderThickness);

                    captureBorder = new Border
                    {
                        BorderBrush = ColorConverter.CreateBrush(color),
                        BorderThickness = new Thickness(_borderThickness),
                        Width = expandedWidth,
                        Height = expandedHeight,
                        IsHitTestVisible = false
                    };
                    
                    Canvas.SetLeft(captureBorder, (rect.X - VirtualBounds.X) - _borderThickness);
                    Canvas.SetTop(captureBorder, (rect.Y - VirtualBounds.Y) - _borderThickness);
                    
                    ShapesCanvas.Children.Add(captureBorder);

                    captureX = (int)(rect.X - _borderThickness);
                    captureY = (int)(rect.Y - _borderThickness);
                    captureWidth = (int)(rect.Width + (2 * _borderThickness));
                    captureHeight = (int)(rect.Height + (2 * _borderThickness));
                }

                // Crear imagen de fondo temporal para renderizar
                var backgroundImage = new Image
                {
                    Stretch = Stretch.None
                };
                
                var source = new SoftwareBitmapSource();
                SoftwareBitmap displayBitmap;
                if (BackgroundBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    BackgroundBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    displayBitmap = SoftwareBitmap.Convert(BackgroundBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
                else
                {
                    displayBitmap = BackgroundBitmap;
                }
                
                await source.SetBitmapAsync(displayBitmap);
                backgroundImage.Source = source;
                
                // Insertar al principio del RootGrid (detrás de todo)
                RootGrid.Children.Insert(0, backgroundImage);
                await Task.Yield(); // Permitir que se renderice

                // Renderizar el RootGrid completo para capturar fondo + formas
                var renderTargetBitmap = new RenderTargetBitmap();
                await renderTargetBitmap.RenderAsync(RootGrid);
                
                // Quitar la imagen de fondo temporal
                RootGrid.Children.Remove(backgroundImage);
                
                // Remove the border after rendering
                if (captureBorder != null)
                {
                    ShapesCanvas.Children.Remove(captureBorder);
                }
                
                // Limpiar el SoftwareBitmap de conversión si fue creado
                if (displayBitmap != BackgroundBitmap)
                {
                    displayBitmap.Dispose();
                }
                
                var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
                
                using var fullScreenBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                    pixelBuffer,
                    BitmapPixelFormat.Bgra8,
                    renderTargetBitmap.PixelWidth,
                    renderTargetBitmap.PixelHeight,
                    BitmapAlphaMode.Premultiplied);

                int relativeX = captureX - (int)VirtualBounds.X;
                int relativeY = captureY - (int)VirtualBounds.Y;

                int cropX = Math.Clamp(relativeX, 0, Math.Max(0, fullScreenBitmap.PixelWidth - 1));
                int cropY = Math.Clamp(relativeY, 0, Math.Max(0, fullScreenBitmap.PixelHeight - 1));
                int cropWidth = Math.Clamp(captureWidth, 0, fullScreenBitmap.PixelWidth - cropX);
                int cropHeight = Math.Clamp(captureHeight, 0, fullScreenBitmap.PixelHeight - cropY);

                if (cropWidth <= 0 || cropHeight <= 0)
                {
                    return null;
                }

                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(fullScreenBitmap);
                encoder.BitmapTransform.Bounds = new BitmapBounds
                {
                    X = (uint)cropX,
                    Y = (uint)cropY,
                    Width = (uint)cropWidth,
                    Height = (uint)cropHeight
                };
                encoder.IsThumbnailGenerated = false;
                await encoder.FlushAsync();

                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                return await decoder.GetSoftwareBitmapAsync(
                    fullScreenBitmap.BitmapPixelFormat,
                    fullScreenBitmap.BitmapAlphaMode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing selection bitmap: {ex.Message}");
                return null;
            }
        }

        private void HideSelection()
        {
            _state = SelectionState.None;
            _isSelecting = false;
            _isDraggingHandle = false;
            _activeHandle = null;
            
            SelectionBorder.Visibility = Visibility.Collapsed;
            CoordinatesDisplay.Visibility = Visibility.Collapsed;
            FloatingToolbar.Visibility = Visibility.Collapsed;
            CollapseShapesToolbar();
            CollapseFillToolbar();
            CollapseStyleToolbar();
            CaptureFloatingMenu.Visibility = Visibility.Visible; // Mostrar menú flotante nuevamente
            HideHandles();
            SetShadeVisibility(false);
            
            RootGrid.ReleasePointerCaptures();
        }

        #endregion

        #region Floating Menu Handlers

        private void FloatingRectangular_Click(object sender, RoutedEventArgs e)
        {
            // Ya estamos en modo rectangular, solo actualizar ícono
            UpdateCaptureModeIcon("&#xF407;");
        }

        private void FloatingWindow_Click(object sender, RoutedEventArgs e)
        {
            // Solicitar cambio a modo ventana
            UpdateCaptureModeIcon("&#xF7ED;");
            RaiseModeChangeRequested(CaptureMode.Window);
        }

        private void FloatingFullScreen_Click(object sender, RoutedEventArgs e)
        {
            // Solicitar cambio a pantalla completa
            UpdateCaptureModeIcon("&#xE9A6;");
            RaiseModeChangeRequested(CaptureMode.FullScreen);
        }

        private void FloatingFreeForm_Click(object sender, RoutedEventArgs e)
        {
            // Solicitar cambio a forma libre
            UpdateCaptureModeIcon("&#xF408;");
            RaiseModeChangeRequested(CaptureMode.FreeForm);
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            // Solicitar cambio a modo selector de color
            RaiseModeChangeRequested(CaptureMode.ColorPicker);
        }

        private void ShapesButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEraser();
            DeactivatePen();
            DeactivateHighlighter();
            
            // Si ya está visible, no hacer nada (mantener focus)
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Shapes) == true)
            {
                return;
            }
            
            // Desactivar modos de bolígrafo y resaltador usando el manager
            _annotationManager?.DeactivateTool();
            
            // Mostrar el menú de formas usando el manager
            _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Shapes);
            ShapesButton.Background = SelectedButtonBrush;
            
            // Seleccionar forma por defecto (cuadrado) si no hay ninguna seleccionada
            FloatingShapesToolbarContent.SelectDefaultShape();
        }

        #endregion

        #region ShapesToolbarControl Event Handlers

        private void FloatingShapesToolbar_ShapeSelected(object sender, ShapeType shapeType)
        {
            DeactivateEraser();
            DeactivatePen();
            DeactivateHighlighter();
            
            // Convertir el tipo de forma a AnnotationToolType y activar
            var toolType = ShapesToolbarControl.ToAnnotationToolType(shapeType);
            _annotationManager?.SetActiveTool(toolType);
            
            // El control ya maneja su propio estado de botones y FillEnabled
            // Solo cerrar el toolbar de relleno si la forma no soporta relleno
            if (shapeType != ShapeType.Square && shapeType != ShapeType.Circle && shapeType != ShapeType.Star)
            {
                CollapseFillToolbar();
            }
        }

        private void FloatingShapesToolbar_FillButtonClicked(object? sender, EventArgs e)
        {
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Fill) == true)
            {
                CollapseFillToolbar();
            }
            else
            {
                // Cerrar el menú de estilo si está abierto, pero mantener Shapes visible
                CollapseStyleToolbar();
                
                // Mostrar usando el manager, preservando el menú de Shapes
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Fill, preserveShapesToolbar: true);
                FloatingShapesToolbarContent.SetFillExpanded(true);
            }
        }

        private void FloatingShapesToolbar_StyleButtonClicked(object? sender, EventArgs e)
        {
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Style) == true)
            {
                CollapseStyleToolbar();
            }
            else
            {
                // Cerrar el menú de relleno si está abierto, pero mantener Shapes visible
                CollapseFillToolbar();
                
                // Mostrar usando el manager, preservando el menú de Shapes
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Style, preserveShapesToolbar: true);
                FloatingShapesToolbarContent.SetStyleExpanded(true);
            }
        }

        #endregion

        /// <summary>
        /// Activa un modo de dibujo de forma específico (usado internamente)
        /// </summary>
        private void ActivateShapeMode(AnnotationToolType toolType)
        {
            // Cerrar toolbars de otras herramientas
            CollapsePenToolbar();
            CollapseHighlighterToolbar();
            
            // Activar la herramienta
            _annotationManager?.SetActiveTool(toolType);
            
            // Actualizar el control
            FloatingShapesToolbarContent.SetSelectedFromToolType(toolType);
            
            bool supportsFill = toolType == AnnotationToolType.Rectangle || toolType == AnnotationToolType.Ellipse || toolType == AnnotationToolType.Star;
            if (!supportsFill)
            {
                CollapseFillToolbar();
            }
        }

        /// <summary>
        /// Convierte un string de tipo de forma al enum AnnotationToolType
        /// </summary>
        private static AnnotationToolType ConvertToAnnotationToolType(string? shapeType)
        {
            return shapeType switch
            {
                "Square" => AnnotationToolType.Rectangle,
                "Circle" => AnnotationToolType.Ellipse,
                "Line" => AnnotationToolType.Line,
                "Arrow" => AnnotationToolType.Arrow,
                "Star" => AnnotationToolType.Star,
                _ => AnnotationToolType.None
            };
        }

        /// <summary>
        /// Obtiene el string de tipo de forma desde el AnnotationToolType actual
        /// </summary>
        private string? GetActiveShapeTypeString()
        {
            return _annotationManager?.ActiveToolType switch
            {
                AnnotationToolType.Rectangle => "Square",
                AnnotationToolType.Ellipse => "Circle",
                AnnotationToolType.Line => "Line",
                AnnotationToolType.Arrow => "Arrow",
                AnnotationToolType.Star => "Star",
                _ => null
            };
        }

        private void ClearCanvasButton_Click(object sender, RoutedEventArgs e)
        {
            // Deseleccionar cualquier forma seleccionada
            if (_shapeManipulation?.HasSelection == true)
            {
                DeselectShape();
            }
            
            // Cancelar cualquier trazo en progreso
            _annotationManager?.CancelStroke();
            
            // Limpiar todas las formas del canvas
            _annotationManager?.ClearAllAnnotations();
            
            // Limpiar los historiales de undo/redo usando el manager
            _historyManager?.ClearHistory();
        }

        private void UpdateShapeButtonSelection()
        {
            // El control ShapesToolbarControl ahora maneja su propio estado
            if (_annotationManager != null)
            {
                FloatingShapesToolbarContent.SetSelectedFromToolType(_annotationManager.ActiveToolType);
            }
        }

        #region Ballpoint Pen

        /// <summary>
        /// Inicializa los colores de los iconos de bolígrafo y resaltador según los colores por defecto
        /// </summary>
        private void InitializePenAndHighlighterIcons()
        {
            // Obtener los colores actuales de las herramientas
            if (_annotationManager != null)
            {
                var penColor = _annotationManager.PenSettings.StrokeColor;
                var highlighterColor = _annotationManager.HighlighterSettings.StrokeColor;
                
                BallpointPenIcon.Foreground = BrushCache.GetBrush(penColor);
                HighlighterIcon.Foreground = BrushCache.GetBrush(highlighterColor);
            }
        }

        private void BallpointPenButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEraser();
            
            // Deseleccionar forma activa (confirma la forma)
            DeselectShape();
            
            // Si ya está activo, desactivar (toggle)
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Pen)
            {
                DeactivatePen();
                return;
            }
            
            // Cerrar otros menús flotantes y desactivar otras herramientas
            CollapseAllSecondaryToolbars();
            DeactivateHighlighter();
            
            // Activar modo bolígrafo usando el manager
            _annotationManager?.SetActiveTool(AnnotationToolType.Pen);
            
            // Marcar el botón como seleccionado
            BallpointPenButton.Background = SelectedButtonBrush;
        }

        private void DeactivatePen()
        {
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Pen)
            {
                _annotationManager?.DeactivateTool();
            }
            BallpointPenButton.Background = TransparentBrush;
        }

        private void CollapsePenToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Pen);
            BallpointPenButton.Background = TransparentBrush;
        }

        private void BallpointPenButton_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            
            // Toggle del menú del bolígrafo
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Pen) == true)
            {
                CollapsePenToolbar();
            }
            else
            {
                CollapseAllSecondaryToolbars();
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Pen);
            }
        }

        private void FloatingPenToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetPenColor(color);
            // Actualizar el color del icono del bolígrafo
            BallpointPenIcon.Foreground = BrushCache.GetBrush(color);
        }

        private void FloatingPenToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetPenThickness(thickness);
        }

        #endregion

        #region Highlighter

        private void HighlighterButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEraser();
            
            // Deseleccionar forma activa (confirma la forma)
            DeselectShape();
            
            // Si ya está activo, desactivar (toggle)
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Highlighter)
            {
                DeactivateHighlighter();
                return;
            }
            
            // Cerrar otros menús flotantes y desactivar otras herramientas
            CollapseAllSecondaryToolbars();
            DeactivatePen();
            
            // Activar modo resaltador usando el manager
            _annotationManager?.SetActiveTool(AnnotationToolType.Highlighter);
            
            // Marcar el botón como seleccionado
            HighlighterButton.Background = SelectedButtonBrush;
        }

        private void DeactivateHighlighter()
        {
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Highlighter)
            {
                _annotationManager?.DeactivateTool();
            }
            HighlighterButton.Background = TransparentBrush;
        }

        private void CollapseHighlighterToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Highlighter);
            HighlighterButton.Background = TransparentBrush;
        }

        private void HighlighterButton_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            
            // Toggle del menú del resaltador
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Highlighter) == true)
            {
                CollapseHighlighterToolbar();
            }
            else
            {
                CollapseAllSecondaryToolbars();
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Highlighter);
            }
        }

        private void FloatingHighlighterToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetHighlighterColor(color);
            // Actualizar el color del icono del resaltador
            HighlighterIcon.Foreground = BrushCache.GetBrush(color);
        }

        private void FloatingHighlighterToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetHighlighterThickness(thickness);
        }

        #endregion

        #region Eraser

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEraserActive)
            {
                DeactivateEraser();
                return;
            }

            if (_isEditingText && _activeTextElement != null)
            {
                FinishTextEditing();
            }

            _annotationManager?.CancelStroke();
            _annotationManager?.DeactivateTool();
            CollapseAllSecondaryToolbars();
            DeselectShape();
            _textManipulation?.Deselect();
            
            // Desactivar pen y highlighter
            DeactivatePen();
            DeactivateHighlighter();

            _isEraserActive = true;
            _isErasing = false;
            EraserButton.Background = SelectedButtonBrush;
        }

        private void ClearAnnotationsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearCanvasButton_Click(sender, e);
        }

        private void EraserButton_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
            
            // Crear y mostrar menú contextual para el borrador
            var menuFlyout = new MenuFlyout();
            var clearItem = new MenuFlyoutItem
            {
                Text = "Limpiar todo",
                Icon = new FontIcon { Glyph = "\uE74D" }
            };
            clearItem.Click += ClearAnnotationsMenuItem_Click;
            menuFlyout.Items.Add(clearItem);
            
            menuFlyout.ShowAt(EraserButton, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
            {
                Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
            });
        }

        private void DeactivateEraser()
        {
            _isEraserActive = false;
            _isErasing = false;
            EraserButton.Background = TransparentBrush;
        }

        #endregion

        #region Text Tool Events

        private void TextButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEraser();
            DeactivatePen();
            DeactivateHighlighter();
            
            // Toggle el menú de texto
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Text) == true)
            {
                CollapseTextToolbar();
                // Desactivar herramienta de texto
                _annotationManager?.DeactivateTool();
            }
            else
            {
                // Finish any active text editing first
                if (_isEditingText && _activeTextElement != null)
                {
                    FinishTextEditing();
                }

                // Cerrar otros menús flotantes y deseleccionar formas
                CollapseAllSecondaryToolbars();
                DeselectShape();
                
                // Mostrar usando el manager
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Text);
                TextButton.Background = SelectedButtonBrush;
                
                // Update the text preview to show current settings
                UpdateTextPreview();
                
                // Activar modo texto
                _annotationManager?.SetActiveTool(AnnotationToolType.Text);
            }
        }

        private void CollapseTextToolbar()
        {
            // Finish any active text editing
            if (_isEditingText && _activeTextElement != null)
            {
                FinishTextEditing();
            }

            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Text);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            TextButton.Background = TransparentBrush;
            
            // Deactivate text tool if it was active
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Text)
            {
                _annotationManager.DeactivateTool();
            }
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFontFamilyToText();
        }

        private void FontSizeComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            // Validar que el texto ingresado sea un número válido
            if (int.TryParse(args.Text, out int size))
            {
                // Limitar el tamaño entre 8 y 200
                size = Math.Max(8, Math.Min(200, size));
                sender.Text = size.ToString();
            }
            else
            {
                // Si no es válido, restaurar al valor anterior
                args.Handled = true;
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFontSizeToText();
        }

        private void BoldToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyStyleToText(style => style.IsBold = BoldToggle.IsChecked == true);
        }

        private void ItalicToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyStyleToText(style => style.IsItalic = ItalicToggle.IsChecked == true);
        }

        private void UnderlineToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyStyleToText(style => style.IsUnderline = UnderlineToggle.IsChecked == true);
        }

        private void StrikethroughToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyStyleToText(style => style.IsStrikethrough = StrikethroughToggle.IsChecked == true);
        }

        private void TextColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle del menú de color de texto
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.TextColor) == true)
            {
                _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
            }
            else
            {
                // Cerrar el menú de resaltado si está abierto
                _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
                
                // Mostrar el menú de color de texto
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.TextColor, preserveShapesToolbar: false);
            }
        }

        private void TextHighlightButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle del menú de color de resaltado
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.TextHighlight) == true)
            {
                _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            }
            else
            {
                // Cerrar el menú de color de texto si está abierto
                _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
                
                // Mostrar el menú de resaltado
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.TextHighlight, preserveShapesToolbar: false);
            }
        }

        private void TextColorPalette_ColorSelected(object? sender, Color color)
        {
            // Actualizar el indicador de color
            if (TextColorIndicator != null)
            {
                TextColorIndicator.Background = BrushCache.GetBrush(color);
            }
            
            // Aplicar color al texto
            ApplyTextColorToText(color);
            
            // Cerrar el menú después de seleccionar
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
        }

        private void TextBackgroundColorPalette_ColorSelected(object? sender, Color color)
        {
            // Actualizar el indicador de resaltado
            if (TextHighlightIndicator != null)
            {
                if (color.A == 0) // Transparente
                {
                    TextHighlightIndicator.Background = BrushCache.Transparent;
                    TextHighlightIndicator.BorderThickness = new Thickness(1);
                }
                else
                {
                    TextHighlightIndicator.Background = BrushCache.GetBrush(color);
                    TextHighlightIndicator.BorderThickness = new Thickness(0);
                }
            }
            
            // Aplicar color de resaltado al texto
            ApplyTextHighlightToText(color);
            
            // Cerrar el menú después de seleccionar
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
        }

        #endregion

        #region Toolbar Utility Methods

        private void CollapseAllSecondaryToolbars()
        {
            // Usar el manager para colapsar todas las toolbars
            _toolbarManager?.CollapseAllSecondaryToolbars();
            
            // Resetear estados visuales de todos los botones, pero mantener el estado si la herramienta está activa
            ShapesButton.Background = AnnotationManager.IsShapeTool(_annotationManager?.ActiveToolType ?? AnnotationToolType.None) ? SelectedButtonBrush : TransparentBrush;
            FloatingShapesToolbarContent.SetStyleExpanded(false);
            FloatingShapesToolbarContent.SetFillExpanded(false);
            BallpointPenButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Pen ? SelectedButtonBrush : TransparentBrush;
            HighlighterButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Highlighter ? SelectedButtonBrush : TransparentBrush;
            TextButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Text ? SelectedButtonBrush : TransparentBrush;
            EmojiButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Emoji ? SelectedButtonBrush : TransparentBrush;
            EraserButton.Background = _isEraserActive ? SelectedButtonBrush : TransparentBrush;
        }

        /// <summary>
        /// Colapsa todos los menús secundarios EXCEPTO el menú de formas.
        /// Se usa cuando se hace clic fuera de los menús - el menú de formas debe permanecer visible
        /// hasta que se haga clic en otro botón de la toolbar.
        /// </summary>
        private void CollapseSecondaryToolbarsExceptShapes()
        {
            // Colapsar todos los menús excepto Shapes
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Pen);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Highlighter);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Text);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Emoji);
            
            // Resetear estados visuales de botones (excepto Shapes que mantiene su estado)
            FloatingShapesToolbarContent.SetStyleExpanded(false);
            FloatingShapesToolbarContent.SetFillExpanded(false);
            BallpointPenButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Pen ? SelectedButtonBrush : TransparentBrush;
            HighlighterButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Highlighter ? SelectedButtonBrush : TransparentBrush;
            TextButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Text ? SelectedButtonBrush : TransparentBrush;
            EmojiButton.Background = _annotationManager?.ActiveToolType == AnnotationToolType.Emoji ? SelectedButtonBrush : TransparentBrush;
            EraserButton.Background = _isEraserActive ? SelectedButtonBrush : TransparentBrush;
        }

        // Style Toolbar handlers
        private void FloatingStyleToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetShapeStrokeColor(color);
            RefreshCurrentShapeVisual();
        }

        private void FloatingStyleToolbar_OpacityChanged(object? sender, double opacity)
        {
            _annotationManager?.SetShapeStrokeOpacity(opacity / 100.0);
            RefreshCurrentShapeVisual();
        }

        private void FloatingStyleToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetShapeStrokeThickness(thickness);
            RefreshCurrentShapeVisual();
        }

        // Fill Toolbar handlers
        private void FloatingFillToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetShapeFillColor(color);
            _annotationManager?.SetShapeFillEnabled(true);
            RefreshCurrentShapeVisual();
        }

        private void FloatingFillToolbar_OpacityChanged(object? sender, double opacity)
        {
            var normalizedOpacity = opacity / 100.0;
            _annotationManager?.SetShapeFillOpacity(normalizedOpacity);
            _annotationManager?.SetShapeFillEnabled(normalizedOpacity > 0);
            RefreshCurrentShapeVisual();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // Guardar referencia a la forma seleccionada antes del Undo
            var previouslySelectedShape = _shapeManipulation?.SelectedShape;
            
            _historyManager?.Undo();
            
            // Si la forma seleccionada fue eliminada del canvas, deseleccionar
            if (previouslySelectedShape != null && !ShapesCanvas.Children.Contains(previouslySelectedShape))
            {
                DeselectShape();
            }
        }

        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            // Guardar referencia a la forma seleccionada antes del Redo
            var previouslySelectedShape = _shapeManipulation?.SelectedShape;
            
            _historyManager?.Redo();
            
            // Si la forma seleccionada fue eliminada del canvas, deseleccionar
            if (previouslySelectedShape != null && !ShapesCanvas.Children.Contains(previouslySelectedShape))
            {
                DeselectShape();
            }
        }

        private void UpdateUndoRedoButtonStates()
        {
            UndoButton.IsEnabled = _historyManager?.CanUndo ?? false;
            RedoButton.IsEnabled = _historyManager?.CanRedo ?? false;
        }

        /// <summary>
        /// Handler para cuando se completa un trazo de anotación
        /// </summary>
        private void OnAnnotationStrokeCompleted(object? sender, Path completedPath)
        {
            _historyManager?.RecordPathAdded(completedPath);
            
            // Auto-seleccionar formas (no pen/highlighter) para edición inmediata
            if (completedPath.Tag is ShapeData)
            {
                _shapeManipulation?.SelectShape(completedPath);
            }
        }

        /// <summary>
        /// Handler para cuando cambia la selección de forma
        /// </summary>
        private void OnShapeSelectionChanged(object? sender, Path? selectedShape)
        {
            // Actualizar estado de UI si es necesario
            // Por ejemplo, habilitar/deshabilitar botones de edición
        }

        /// <summary>
        /// Handler para cuando una forma es modificada (movida o redimensionada)
        /// </summary>
        private void OnShapeModified(object? sender, Path modifiedShape)
        {
            // El historial ya es manejado por ShapeManipulationManager internamente
            // Aquí podemos agregar lógica adicional si es necesario
        }

        /// <summary>
        /// Handler para cuando cambia la selección de texto
        /// </summary>
        private void OnTextSelectionChanged(object? sender, Grid? selectedText)
        {
            if (selectedText != null)
            {
                // Deseleccionar cualquier forma seleccionada
                DeselectShape();
                
                // Mostrar toolbar de texto si no está visible
                if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Text) != true)
                {
                    CollapseAllSecondaryToolbars();
                    _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Text);
                    TextButton.Background = SelectedButtonBrush;
                }

                // Sincronizar toolbar con el texto seleccionado
                SyncTextToolbarWithSelection(selectedText);
            }
        }

        /// <summary>
        /// Handler para cuando un texto es modificado (movido o redimensionado)
        /// </summary>
        private void OnTextModified(object? sender, Grid modifiedText)
        {
            // El historial ya es manejado por TextManipulationManager internamente
        }

        #endregion

        #region Emoji Tool Events

        /// <summary>
        /// Handler para cuando cambia la selección de emoji
        /// </summary>
        private void OnEmojiSelectionChanged(object? sender, Grid? selectedEmoji)
        {
            if (selectedEmoji != null)
            {
                // Deseleccionar cualquier forma o texto seleccionado
                DeselectShape();
                _textManipulation?.Deselect();
            }
        }

        /// <summary>
        /// Handler para cuando un emoji es modificado (movido, redimensionado o rotado)
        /// </summary>
        private void OnEmojiModified(object? sender, Grid modifiedEmoji)
        {
            // El historial ya es manejado por EmojiManipulationManager internamente
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            DeactivateEraser();
            DeactivatePen();
            DeactivateHighlighter();

            // Toggle el menú de emoji
            if (_toolbarManager?.IsSecondaryToolbarVisible(SecondaryToolbarType.Emoji) == true)
            {
                CollapseEmojiToolbar();
                _annotationManager?.DeactivateTool();
            }
            else
            {
                // Finish any active text editing first
                if (_isEditingText && _activeTextElement != null)
                {
                    FinishTextEditing();
                }

                // Cerrar otros menús flotantes y deseleccionar formas/texto
                CollapseAllSecondaryToolbars();
                DeselectShape();
                _textManipulation?.Deselect();

                // Mostrar usando el manager
                _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Emoji);
                EmojiButton.Background = SelectedButtonBrush;

                // Activar modo emoji
                _annotationManager?.SetActiveTool(AnnotationToolType.Emoji);
            }
        }

        private void CollapseEmojiToolbar()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Emoji);
            EmojiButton.Background = TransparentBrush;

            // Deactivate emoji tool if it was active
            if (_annotationManager?.ActiveToolType == AnnotationToolType.Emoji)
            {
                _annotationManager.DeactivateTool();
            }
        }

        private void FloatingEmojiToolbar_EmojiSelected(object sender, string emoji)
        {
            PlaceEmoji(emoji);
            // Cerrar el toolbar flotante después de añadir el emoji
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Emoji);
            EmojiButton.Background = TransparentBrush;
        }

        /// <summary>
        /// Coloca un emoji en el centro del área de selección
        /// </summary>
        private void PlaceEmoji(string emoji)
        {
            if (_annotationManager == null || _state != SelectionState.Selected) return;

            // Calcular la posición central del área de selección (coordenadas UI)
            var selectionRect = GetCurrentSelectionRect();
            var centerX = selectionRect.X + selectionRect.Width / 2;
            var centerY = selectionRect.Y + selectionRect.Height / 2;

            // Crear el emoji en la posición calculada y añadirlo al canvas
            var emojiGrid = _annotationManager.CreateEmojiElement(new Point(centerX, centerY), emoji);

            if (emojiGrid != null && _emojiManipulation != null)
            {
                _emojiManipulation.SelectEmoji(emojiGrid);
                _historyManager?.RecordElementAdded(emojiGrid);
            }
        }

        #endregion

        // Note: OnEditTextRequested removed - text editing after creation is disabled
        // Text elements are static annotations that can only be moved or deleted

        /// <summary>
        /// Sincroniza los controles de la toolbar con el texto seleccionado
        /// </summary>
        private void SyncTextToolbarWithSelection(Grid textElement)
        {
            if (textElement.Tag is not TextData textData)
                return;

            // Sincronizar fuente
            for (int i = 0; i < FontFamilyComboBox.Items.Count; i++)
            {
                if (FontFamilyComboBox.Items[i] is ComboBoxItem item &&
                    item.Content?.ToString() == textData.FontFamily)
                {
                    FontFamilyComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Sincronizar tamaño
            FontSizeComboBox.Text = textData.FontSize.ToString();

            // Sincronizar estilos
            BoldToggle.IsChecked = textData.IsBold;
            ItalicToggle.IsChecked = textData.IsItalic;
            UnderlineToggle.IsChecked = textData.IsUnderline;
            StrikethroughToggle.IsChecked = textData.IsStrikethrough;
        }

        /// <summary>
        /// Gets the current text element being edited or selected
        /// </summary>
        private Grid? GetCurrentTextElement()
        {
            // Prioritize active editing element
            if (_isEditingText && _activeTextElement != null)
                return _activeTextElement;

            // Fall back to selected text
            return _textManipulation?.SelectedText;
        }

        /// <summary>
        /// Applies font family from toolbar to current text (only during initial creation)
        /// </summary>
        private void ApplyFontFamilyToText()
        {
            if (_annotationManager == null)
                return;

            if (FontFamilyComboBox.SelectedItem is not ComboBoxItem item || 
                item.Content?.ToString() is not string fontFamily)
                return;

            // Always update default settings for new text
            _annotationManager.TextSettings.FontFamily = fontFamily;

            // Update the preview
            UpdateTextPreview();

            // Only apply to text element if currently editing (during creation)
            // Once text is created, it cannot be modified
            if (_isEditingText && _activeTextElement != null)
            {
                _annotationManager.TextTool.SetFontFamily(fontFamily, _activeTextElement);
                
                var richEditBox = TextTool.FindRichEditBox(_activeTextElement);
                richEditBox?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Applies font size from toolbar to current text (only during initial creation)
        /// </summary>
        private void ApplyFontSizeToText()
        {
            if (_annotationManager == null)
                return;

            if (!double.TryParse(FontSizeComboBox.Text, out double fontSize) || fontSize <= 0)
                return;

            // Always update default settings for new text
            _annotationManager.TextSettings.FontSize = fontSize;

            // Update the preview
            UpdateTextPreview();

            // Only apply to text element if currently editing (during creation)
            // Once text is created, it cannot be modified
            if (_isEditingText && _activeTextElement != null)
            {
                _annotationManager.TextTool.SetFontSize(fontSize, _activeTextElement);
                
                var richEditBox = TextTool.FindRichEditBox(_activeTextElement);
                richEditBox?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Applies a style change to the current text (only during initial creation)
        /// </summary>
        private void ApplyStyleToText(Action<TextData> styleAction)
        {
            // Always update the default settings for new text
            if (_annotationManager != null)
            {
                styleAction(_annotationManager.TextSettings);
            }

            // Update the preview
            UpdateTextPreview();

            // Only apply to text element if currently editing (during creation)
            // Once text is created, it cannot be modified
            if (_isEditingText && _activeTextElement?.Tag is TextData textData && _annotationManager != null)
            {
                // Apply style change to TextData
                styleAction(textData);
                
                // Update visual immediately
                _annotationManager.TextTool.ApplySettingsToElement(_activeTextElement);
                
                var richEditBox = TextTool.FindRichEditBox(_activeTextElement);
                richEditBox?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Applies text color to current text (only during initial creation)
        /// </summary>
        private void ApplyTextColorToText(Color color)
        {
            if (_annotationManager == null)
                return;

            // Always update default settings for new text
            _annotationManager.TextSettings.TextColor = color;

            // Update the preview
            UpdateTextPreview();

            // Only apply to text element if currently editing (during creation)
            // Once text is created, it cannot be modified
            if (_isEditingText && _activeTextElement != null)
            {
                _annotationManager.TextTool.SetTextColor(color, _activeTextElement);
                
                var richEditBox = TextTool.FindRichEditBox(_activeTextElement);
                richEditBox?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Applies highlight color to current text (only during initial creation)
        /// </summary>
        private void ApplyTextHighlightToText(Color color)
        {
            if (_annotationManager == null)
                return;

            // Always update default settings for new text
            _annotationManager.TextSettings.HighlightColor = color;

            // Update the preview
            UpdateTextPreview();

            // Only apply to text element if currently editing (during creation)
            // Once text is created, it cannot be modified
            if (_isEditingText && _activeTextElement != null)
            {
                _annotationManager.TextTool.SetHighlightColor(color, _activeTextElement);
                
                var richEditBox = TextTool.FindRichEditBox(_activeTextElement);
                richEditBox?.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Updates the text preview in the text toolbar to reflect current settings.
        /// </summary>
        private void UpdateTextPreview()
        {
            if (TextPreviewBlock == null || TextPreviewHighlightBorder == null || _annotationManager == null)
                return;

            var settings = _annotationManager.TextSettings;

            // Update font family
            TextPreviewBlock.FontFamily = new FontFamily(settings.FontFamily);

            // Update font size - show actual size up to 72px (the maximum selectable)
            TextPreviewBlock.FontSize = settings.FontSize;

            // Update text color
            TextPreviewBlock.Foreground = BrushCache.GetBrush(settings.TextColor);

            // Update font weight (bold)
            TextPreviewBlock.FontWeight = settings.IsBold 
                ? Microsoft.UI.Text.FontWeights.Bold 
                : Microsoft.UI.Text.FontWeights.Normal;

            // Update font style (italic)
            TextPreviewBlock.FontStyle = settings.IsItalic 
                ? WinUIText.FontStyle.Italic 
                : WinUIText.FontStyle.Normal;

            // Update text decorations (underline and strikethrough)
            if (settings.IsUnderline && settings.IsStrikethrough)
            {
                TextPreviewBlock.TextDecorations = WinUIText.TextDecorations.Underline | WinUIText.TextDecorations.Strikethrough;
            }
            else if (settings.IsUnderline)
            {
                TextPreviewBlock.TextDecorations = WinUIText.TextDecorations.Underline;
            }
            else if (settings.IsStrikethrough)
            {
                TextPreviewBlock.TextDecorations = WinUIText.TextDecorations.Strikethrough;
            }
            else
            {
                TextPreviewBlock.TextDecorations = WinUIText.TextDecorations.None;
            }

            // Update highlight/background color
            if (settings.HighlightColor.A == 0) // Transparent
            {
                TextPreviewHighlightBorder.Background = BrushCache.Transparent;
            }
            else
            {
                TextPreviewHighlightBorder.Background = BrushCache.GetBrush(settings.HighlightColor);
            }
        }

        #region Shape Geometry Creation (delegating to Tools)

        private static bool IsShiftPressed()
        {
            var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(global::Windows.System.VirtualKey.Shift);
            return (shiftState & WinUICore.CoreVirtualKeyStates.Down) == WinUICore.CoreVirtualKeyStates.Down;
        }

        private static Geometry CreateArrowGeometry(Point start, Point end)
        {
            return ArrowTool.CreateArrowGeometry(start, end);
        }

        private static Geometry CreateSquareGeometry(Point start, Point end)
        {
            return RectangleTool.CreateRectangleGeometry(start, end, IsShiftPressed());
        }

        private static Geometry CreateCircleGeometry(Point start, Point end)
        {
            return EllipseTool.CreateEllipseGeometry(start, end, IsShiftPressed());
        }

        private static Geometry CreateLineGeometry(Point start, Point end)
        {
            return LineTool.CreateLineGeometry(start, end);
        }

        #endregion

        #region Event Handlers

        private void FloatingClose_Click(object sender, RoutedEventArgs e)
        {
            CancelSelection();
        }

        /// <summary>
        /// Previene que los clics en los menús flotantes se propaguen al RootGrid
        /// </summary>
        private void FloatingMenu_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Marcar el evento como manejado para evitar que se propague al RootGrid
            e.Handled = true;
        }

        private bool IsPointerOverToolbar(DependencyObject? element)
        {
            var current = element;
            while (current != null)
            {
                if (current == FloatingToolbar ||
                    current == FloatingShapesToolbar ||
                    current == FloatingStyleToolbar ||
                    current == FloatingFillToolbar ||
                    current == FloatingPenToolbar ||
                    current == FloatingHighlighterToolbar ||
                    current == FloatingTextToolbar ||
                    current == FloatingTextColorToolbar ||
                    current == FloatingTextHighlightToolbar ||
                    current == CaptureFloatingMenu ||
                    current == BallpointPenButton ||
                    current == HighlighterButton)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        #endregion

        #region P/Invoke for System Metrics
        
        // No longer needed - moved to WindowHelper

        #endregion

        #region Shape Manipulation

        #region Text Tool Methods

        /// <summary>
        /// Handles a click when the Text tool is active
        /// </summary>
        private void HandleTextToolClick(Point position)
        {
            // If we're currently editing text, finish editing first
            if (_isEditingText && _activeTextElement != null)
            {
                FinishTextEditing();
            }

            // Check if we clicked on an existing text element
            var existingText = _textManipulation?.GetTextAtPoint(position) ?? GetTextElementAtPoint(position);
            if (existingText != null)
            {
                // Check for double-click to edit
                if (_textManipulation?.HandlePotentialDoubleClick(position, existingText) == true)
                {
                    return; // Edit mode will be triggered by event
                }

                // Select the text element
                _textManipulation?.SelectText(existingText);
                return;
            }

            // Deselect any selected text
            _textManipulation?.Deselect();

            // Create a new text element
            CreateNewTextElement(position);
        }

        /// <summary>
        /// Creates a new text element at the specified position
        /// </summary>
        private void CreateNewTextElement(Point position)
        {
            if (_annotationManager == null)
                return;

            var textElement = _annotationManager.CreateTextElement(position);
            if (textElement == null)
                return;

            // Register event handlers for the RichEditBox
            var richEditBox = TextTool.FindRichEditBox(textElement);
            if (richEditBox != null)
            {
                richEditBox.LostFocus += RichEditBox_LostFocus;
                richEditBox.KeyDown += RichEditBox_KeyDown;
            }

            _activeTextElement = textElement;
            _isEditingText = true;
            _isNewTextElement = true;

            // Don't show handles while creating/editing new text
            // Handles will appear after editing is finished
            _textManipulation?.Deselect();

            // Start editing (focus the RichEditBox)
            _annotationManager.StartTextEditing(textElement);

            // Record in history (will be recorded when editing is complete if text has content)
        }

        /// <summary>
        /// Starts editing an existing text element
        /// </summary>
        private void StartTextEditing(Grid textElement)
        {
            if (_annotationManager == null)
                return;

            _activeTextElement = textElement;
            _isEditingText = true;
            _isNewTextElement = false;
            _annotationManager.StartTextEditing(textElement);
        }

        /// <summary>
        /// Finishes editing the current text element
        /// </summary>
        private void FinishTextEditing()
        {
            if (_activeTextElement == null || _annotationManager == null)
                return;

            var textElement = _activeTextElement;
            var hasContent = _annotationManager.EndTextEditing(textElement);

            if (!hasContent)
            {
                // Remove empty text element (don't record in history if it was new and empty)
                _annotationManager.RemoveTextElement(textElement);
                _textManipulation?.Deselect();
            }
            else
            {
                if (_isNewTextElement)
                {
                    // Record new text element in history
                    _historyManager?.RecordElementAdded(textElement);
                }
                
                // Now show the handles for the finished text element
                _textManipulation?.SelectText(textElement);
            }

            _activeTextElement = null;
            _isEditingText = false;
            _isNewTextElement = false;
        }

        /// <summary>
        /// Gets a text element at the specified point, if any
        /// </summary>
        private Grid? GetTextElementAtPoint(Point point)
        {
            if (ShapesCanvas == null)
                return null;

            foreach (var child in ShapesCanvas.Children)
            {
                if (child is Grid grid && grid.Tag is TextData)
                {
                    var left = Canvas.GetLeft(grid);
                    var top = Canvas.GetTop(grid);
                    var width = double.IsNaN(grid.Width) ? grid.ActualWidth : grid.Width;
                    var height = double.IsNaN(grid.Height) ? grid.ActualHeight : grid.Height;

                    var bounds = new Rect(left, top, Math.Max(width, 1), Math.Max(height, 1));
                    if (bounds.Contains(point))
                    {
                        return grid;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Handles when a RichEditBox loses focus
        /// </summary>
        private void RichEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Small delay to allow focus to settle
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_isEditingText || _activeTextElement == null)
                    return;

                // Check if focus moved to a toolbar control
                var focusedElement = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
                
                // Check if the focused element is within the text toolbar or its submenus
                if (IsElementInTextToolbar(focusedElement as DependencyObject))
                {
                    return; // Keep editing - user is using the toolbar
                }

                // Focus went somewhere else - finish editing
                FinishTextEditing();
            });
        }

        /// <summary>
        /// Checks if an element is part of the text toolbar or its secondary toolbars
        /// </summary>
        private bool IsElementInTextToolbar(DependencyObject? element)
        {
            if (element == null)
                return false;

            // Walk up the visual tree to check if it's in the text toolbar
            var current = element;
            while (current != null)
            {
                // Check if it's the text toolbar or any of its related toolbars
                if (current == FloatingTextToolbar ||
                    current == FloatingTextColorToolbar ||
                    current == FloatingTextHighlightToolbar ||
                    current == FloatingToolbar)
                {
                    return true;
                }

                // Get parent in visual tree
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        /// <summary>
        /// Handles key down events in the RichEditBox
        /// </summary>
        private void RichEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                FinishTextEditing();
                e.Handled = true;
            }
            // Note: Enter key is allowed for multi-line text (AcceptsReturn = true)
            // Use Escape to finish editing or click outside
        }

        #endregion

        /// <summary>
        /// Selecciona una forma para edición
        /// </summary>
        private void SelectShape(Path shape)
        {
            _shapeManipulation?.SelectShape(shape);
        }

        /// <summary>
        /// Deselecciona la forma actual
        /// </summary>
        private void DeselectShape()
        {
            _shapeManipulation?.Deselect();
        }

        #endregion
    }
}
