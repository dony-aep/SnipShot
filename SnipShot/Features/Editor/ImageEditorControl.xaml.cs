using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SnipShot.Features.Capture.Annotations.Managers;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Features.Capture.Annotations.Tools;
using SnipShot.Features.Capture.Toolbars;
using SnipShot.Helpers.UI;
using SnipShot.Helpers.Utils;
using SnipShot.Models;
using SnipShot.Shared.Controls.Toolbars.Shapes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Windows.Media.Ocr;
using Windows.ApplicationModel.DataTransfer;

namespace SnipShot.Features.Editor
{
    /// <summary>
    /// Control de edición de imágenes con soporte para anotaciones.
    /// Reutiliza la arquitectura de anotaciones de RectangularCaptureWindow.
    /// </summary>
    public sealed partial class ImageEditorControl : UserControl
    {
        // Managers de anotaciones
        private AnnotationManager? _annotationManager;
        private AnnotationHistoryManager? _historyManager;
        private ShapeManipulationManager? _shapeManipulation;
        private TextManipulationManager? _textManipulation;
        private EmojiManipulationManager? _emojiManipulation;

        // Toolbar manager (shared with capture windows)
        private FloatingToolbarManager? _toolbarManager;
        private ZoomManager? _zoomManager;
        private ResizeHandleManager.HandleSet? _cropHandleSet;
        private Rect _cropBounds;
        private Rect _cropBoundsBeforeDrag;
        private Point _cropDragStart;
        private string? _activeCropHandle;
        private bool _isCropping;
        private bool _isCropHandleDragging;
        private bool _isCropDragging;
        
        // Padding para el modo recorte (espacio para handles visibles en los bordes)
        private const double CROP_MODE_PADDING = 24.0;

        // Estado de la imagen
        private SoftwareBitmap? _currentBitmap;
        private bool _hasImage;

        // Estado de dibujo
        private bool _isDrawing;
        private Point _lastPoint;
        private bool _isErasing;

        // Estado de edición de texto
        private Grid? _activeTextElement;
        private bool _isEditingText;
        private bool _isNewTextElement;

        // Estado de panning (arrastre con Ctrl)
        private bool _isPanning;
        private bool _isCtrlPressed;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;
        private readonly InputCursor _grabCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        private readonly InputCursor _grabbingCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);

        // Cursores de resize para handles de recorte
        private readonly InputCursor _sizeNSCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        private readonly InputCursor _sizeWECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        private readonly InputCursor _sizeNWSECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
        private readonly InputCursor _sizeNESWCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);

        // OCR state
        private bool _isOcrRunning;
        private OcrResult? _ocrResult;
        private readonly List<Border> _ocrBoxes = new();
        private readonly HashSet<Border> _selectedOcrBoxes = new();
        private Path? _ocrMaskPath;
        private double _ocrScale = 1.0;
        private readonly InputCursor _ocrTextCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
        private static readonly SolidColorBrush _ocrHoverBrush = new(Color.FromArgb(70, 0, 120, 215));
        private static readonly SolidColorBrush _ocrSelectedBrush = new(Color.FromArgb(120, 0, 120, 215));
        private static readonly SolidColorBrush _ocrSoftHighlightBrush = new(Color.FromArgb(30, 255, 255, 255));
        private static readonly SolidColorBrush _ocrMaskBrush = new(Color.FromArgb(170, 60, 60, 60));
        private readonly MenuFlyout _ocrContextFlyout = new();
        private readonly MenuFlyoutItem _ocrCopyMenuItem = new();
        private readonly MenuFlyoutItem _ocrSelectAllMenuItem = new();
        private bool _isOcrAllSelected;
        private bool _isOcrSelecting;
        private int _ocrSelectionAnchorIndex = -1;
        private int _ocrSelectionCurrentIndex = -1;

        // Menú contextual de imagen (clic derecho cuando OCR no está activo)
        private readonly MenuFlyout _imageContextFlyout = new();
        private readonly MenuFlyoutItem _imageSaveMenuItem = new();
        private readonly MenuFlyoutItem _imageCopyMenuItem = new();
        private readonly MenuFlyoutItem _imageSearchMenuItem = new();

        // Herramienta activa
        private EditorToolType _activeToolType = EditorToolType.None;
        
        // Timer para debounce de SizeChanged
        private DispatcherTimer? _sizeChangedDebounceTimer;

        private const double ZOOM_CHANGE_EPSILON = 0.0001;
        private double _lastZoomFactor = 1.0;
        private bool _pendingZoomCommit;
        private bool _isZoomCommitInProgress;
        
        // Flag para lazy loading de managers
        private bool _managersInitialized;
        
        // Almacenar anchor buttons para aplicar después de inicialización lazy
        private Button? _pendingShapesButton;
        private Button? _pendingPenButton;
        private Button? _pendingHighlighterButton;
        private Button? _pendingTextButton;
        private Button? _pendingEmojiButton;

        // Brushes para selección de botones (usando recursos de tema nativos)
        private static SolidColorBrush SelectedButtonBrush => 
            Application.Current.Resources["ControlFillColorSecondaryBrush"] as SolidColorBrush 
            ?? BrushCache.GetBrush(Color.FromArgb(40, 255, 255, 255));
        private static SolidColorBrush TransparentBrush => BrushCache.Transparent;

        /// <summary>
        /// Evento disparado cuando cambia el estado de Undo/Redo
        /// </summary>
        public event EventHandler? UndoRedoStateChanged;

        /// <summary>
        /// Evento disparado cuando se modifica la imagen (para habilitar guardado)
        /// </summary>
        public event EventHandler? ImageModified;

        public event EventHandler<EditorToolType>? ToolbarVisibilityChanged;
        public event EventHandler<bool>? CropModeChanged;
        public event EventHandler? OcrResultsChanged;
        
        /// <summary>
        /// Evento disparado justo antes de mostrar el menú contextual del OCR.
        /// Permite a MainWindow cerrar otros flyouts antes de mostrar el menú.
        /// </summary>
        public event EventHandler? OcrContextMenuOpening;

        /// <summary>
        /// Evento disparado cuando el usuario solicita guardar la imagen desde el menú contextual
        /// </summary>
        public event EventHandler? SaveImageRequested;

        /// <summary>
        /// Evento disparado cuando el usuario solicita copiar la imagen desde el menú contextual
        /// </summary>
        public event EventHandler? CopyImageRequested;

        /// <summary>
        /// Evento disparado cuando el usuario solicita buscar la imagen en el navegador.
        /// El string indica el motor de búsqueda: "google" o "bing"
        /// </summary>
        public event EventHandler<string>? SearchImageRequested;

        /// <summary>
        /// Obtiene si hay una imagen cargada
        /// </summary>
        public bool HasImage => _hasImage;

        /// <summary>
        /// Obtiene si se puede deshacer
        /// </summary>
        public bool CanUndo => _historyManager?.CanUndo ?? false;

        /// <summary>
        /// Obtiene si se puede rehacer
        /// </summary>
        public bool CanRedo => _historyManager?.CanRedo ?? false;

        /// <summary>
        /// Obtiene el bitmap actual con las anotaciones renderizadas
        /// </summary>
        public SoftwareBitmap? CurrentBitmap => _currentBitmap;

        public EditorToolType ActiveToolType => _activeToolType;

        public bool IsOcrRunning => _isOcrRunning;

        public bool HasOcrResult => _ocrBoxes.Count > 0;

        public ImageEditorControl()
        {
            this.InitializeComponent();

            EditorScrollViewer.ViewChanged += EditorScrollViewer_ViewChanged;
            _lastZoomFactor = EditorScrollViewer.ZoomFactor;
            
            // Inicialización mínima - los managers pesados se inicializan cuando se necesitan
            // InitializeManagers();      // Diferido a EnsureManagersInitialized()
            // InitializeToolbarManager(); // Diferido a EnsureManagersInitialized()
            InitializeCropHandles();
            // _zoomManager se inicializa en EnsureManagersInitialized()
            
            // Suscribirse a SizeChanged para re-centrar los toolbars cuando cambie el tamaño
            this.SizeChanged += ImageEditorControl_SizeChanged;

            // Suscribirse a eventos de teclado para detectar Ctrl (panning)
            this.KeyDown += ImageEditorControl_KeyDown;
            this.KeyUp += ImageEditorControl_KeyUp;
            this.IsTabStop = true;
        }
        
        /// <summary>
        /// Asegura que los managers estén inicializados. Se llama de forma lazy antes de usarlos.
        /// </summary>
        private void EnsureManagersInitialized()
        {
            if (_managersInitialized) return;
            
            InitializeManagers();
            InitializeToolbarManager();
            _zoomManager = new ZoomManager(EditorImage, EditorScrollViewer);
            _managersInitialized = true;
            
            // Aplicar anchor buttons pendientes si se establecieron antes de la inicialización
            ApplyToolbarAnchors();
        }

        private void InitializeToolbarManager()
        {
            // Create toolbar manager in editor mode (centered positioning)
            _toolbarManager = FloatingToolbarManager.CreateForEditorMode(FloatingToolbarsCanvas, this);
            
            // Register all secondary toolbars
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Shapes, FloatingShapesToolbar);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Style, FloatingStyleToolbar, FloatingShapesToolbarContent.StyleAnchorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Fill, FloatingFillToolbar, FloatingShapesToolbarContent.FillAnchorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Pen, FloatingPenToolbar);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Highlighter, FloatingHighlighterToolbar);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Text, FloatingTextToolbar);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.TextColor, FloatingTextColorToolbar, TextColorButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.TextHighlight, FloatingTextHighlightToolbar, TextHighlightButton);
            _toolbarManager.RegisterSecondaryToolbar(SecondaryToolbarType.Emoji, FloatingEmojiToolbar);
        }

        public void SetToolbarAnchors(Button? shapesButton, Button? penButton, Button? highlighterButton, Button? textButton, Button? emojiButton = null)
        {
            // Almacenar para aplicar después de inicialización lazy
            _pendingShapesButton = shapesButton;
            _pendingPenButton = penButton;
            _pendingHighlighterButton = highlighterButton;
            _pendingTextButton = textButton;
            _pendingEmojiButton = emojiButton;
            
            // Si ya están inicializados, aplicar inmediatamente
            if (_toolbarManager == null) return;
            
            ApplyToolbarAnchors();
        }
        
        /// <summary>
        /// Aplica los anchor buttons almacenados al toolbar manager.
        /// </summary>
        private void ApplyToolbarAnchors()
        {
            if (_toolbarManager == null) return;
            
            _toolbarManager.SetAssociatedButton(SecondaryToolbarType.Shapes, _pendingShapesButton);
            _toolbarManager.SetAssociatedButton(SecondaryToolbarType.Pen, _pendingPenButton);
            _toolbarManager.SetAssociatedButton(SecondaryToolbarType.Highlighter, _pendingHighlighterButton);
            _toolbarManager.SetAssociatedButton(SecondaryToolbarType.Text, _pendingTextButton);
            _toolbarManager.SetAssociatedButton(SecondaryToolbarType.Emoji, _pendingEmojiButton);
            _toolbarManager.UpdateSecondaryToolbarPositions();
        }

        private void InitializeCropHandles()
        {
            _cropHandleSet = new ResizeHandleManager.HandleSet
            {
                HandleNW = CropHandleNW,
                HandleNE = CropHandleNE,
                HandleSE = CropHandleSE,
                HandleSW = CropHandleSW,
                HandleN = CropHandleN,
                HandleE = CropHandleE,
                HandleS = CropHandleS,
                HandleW = CropHandleW
            };

            ResizeHandleManager.HideHandles(_cropHandleSet);
        }

        private void ImageEditorControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Debounce: esperar 50ms antes de actualizar posiciones de toolbars
            _sizeChangedDebounceTimer?.Stop();
            _sizeChangedDebounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _sizeChangedDebounceTimer.Tick -= SizeChangedDebounceTimer_Tick;
            _sizeChangedDebounceTimer.Tick += SizeChangedDebounceTimer_Tick;
            _sizeChangedDebounceTimer.Start();
        }

        /// <summary>
        /// Handler del timer de debounce para el cambio de tamaño.
        /// </summary>
        private void SizeChangedDebounceTimer_Tick(object? sender, object e)
        {
            _sizeChangedDebounceTimer?.Stop();
            // Re-centrar los toolbars visibles usando el manager
            _toolbarManager?.UpdateSecondaryToolbarPositions();
        }

        private void InitializeManagers()
        {
            // Inicializar AnnotationManager con el canvas de anotaciones
            _annotationManager = new AnnotationManager(AnnotationsCanvas);
            
            // Inicializar HistoryManager
            _historyManager = new AnnotationHistoryManager(AnnotationsCanvas);
            
            // Inicializar ShapeManipulationManager
            _shapeManipulation = new ShapeManipulationManager(
                AnnotationsCanvas, 
                HandlesCanvas,
                ImageContainer,
                _historyManager);
            
            // Inicializar TextManipulationManager
            _textManipulation = new TextManipulationManager(
                AnnotationsCanvas,
                HandlesCanvas,
                _historyManager);

            // Inicializar EmojiManipulationManager
            _emojiManipulation = new EmojiManipulationManager(
                AnnotationsCanvas,
                HandlesCanvas,
                _historyManager);

            // Suscribirse a eventos
            _annotationManager.StrokeCompleted += OnStrokeCompleted;
            _historyManager.HistoryChanged += OnHistoryChanged;
            _shapeManipulation.SelectionChanged += OnShapeSelectionChanged;
            _shapeManipulation.ShapeModified += OnShapeModified;
            _textManipulation.SelectionChanged += OnTextSelectionChanged;
            _textManipulation.TextModified += OnTextModified;
            _emojiManipulation.SelectionChanged += OnEmojiSelectionChanged;
            _emojiManipulation.EmojiModified += OnEmojiModified;

            InitializeStyleControls();
            InitializeOcrContextMenu();
            InitializeOcrOverlayInput();
        }

        private void InitializeOcrContextMenu()
        {
            _ocrCopyMenuItem.Text = "Copiar texto";
            _ocrCopyMenuItem.Click += OcrCopyMenuItem_Click;
            _ocrCopyMenuItem.Icon = new FontIcon { Glyph = "\uE8C8" };
            _ocrCopyMenuItem.KeyboardAcceleratorTextOverride = "Ctrl+C";

            _ocrSelectAllMenuItem.Text = "Seleccionar todo";
            _ocrSelectAllMenuItem.Click += OcrSelectAllMenuItem_Click;
            _ocrSelectAllMenuItem.Icon = new FontIcon { Glyph = "\uE8B3" };
            _ocrSelectAllMenuItem.KeyboardAcceleratorTextOverride = "Ctrl+A";

            _ocrContextFlyout.Items.Clear();
            _ocrContextFlyout.Items.Add(_ocrCopyMenuItem);
            _ocrContextFlyout.Items.Add(new MenuFlyoutSeparator());
            _ocrContextFlyout.Items.Add(_ocrSelectAllMenuItem);

            // Inicializar menú contextual de imagen
            InitializeImageContextMenu();
        }

        private void InitializeImageContextMenu()
        {
            _imageSaveMenuItem.Text = "Guardar imagen";
            _imageSaveMenuItem.Click += ContextSaveImage_Click;
            _imageSaveMenuItem.Icon = new FontIcon { Glyph = "\uE74E" };
            _imageSaveMenuItem.KeyboardAcceleratorTextOverride = "Ctrl+S";

            _imageCopyMenuItem.Text = "Copiar imagen";
            _imageCopyMenuItem.Click += ContextCopyImage_Click;
            _imageCopyMenuItem.Icon = new FontIcon { Glyph = "\uE8C8" };
            _imageCopyMenuItem.KeyboardAcceleratorTextOverride = "Ctrl+C";

            // Submenú de búsqueda de imagen
            var searchSubMenu = new MenuFlyoutSubItem
            {
                Text = "Buscar imagen en...",
                Icon = new FontIcon { Glyph = "\uF6FA" }
            };

            var googleMenuItem = new MenuFlyoutItem
            {
                Text = "Google Imágenes",
                Icon = new FontIcon { Glyph = "\uE774" }
            };
            googleMenuItem.Click += (s, e) => SearchImageRequested?.Invoke(this, "google");

            var bingMenuItem = new MenuFlyoutItem
            {
                Text = "Búsqueda Visual de Bing",
                Icon = new FontIcon { Glyph = "\uE721" }
            };
            bingMenuItem.Click += (s, e) => SearchImageRequested?.Invoke(this, "bing");

            searchSubMenu.Items.Add(googleMenuItem);
            searchSubMenu.Items.Add(bingMenuItem);

            _imageContextFlyout.Items.Clear();
            _imageContextFlyout.Items.Add(_imageSaveMenuItem);
            _imageContextFlyout.Items.Add(_imageCopyMenuItem);
            _imageContextFlyout.Items.Add(new MenuFlyoutSeparator());
            _imageContextFlyout.Items.Add(searchSubMenu);
        }

        private void InitializeOcrOverlayInput()
        {
            OcrOverlayCanvas.PointerPressed += OcrOverlayCanvas_PointerPressed;
            OcrOverlayCanvas.PointerMoved += OcrOverlayCanvas_PointerMoved;
            OcrOverlayCanvas.PointerReleased += OcrOverlayCanvas_PointerReleased;
            OcrOverlayCanvas.PointerCaptureLost += OcrOverlayCanvas_PointerCaptureLost;
        }

        private void InitializeStyleControls()
        {
            if (_annotationManager == null)
            {
                return;
            }

            var shapeSettings = _annotationManager.ShapeSettings;
            FloatingStyleToolbarContent?.SetStyle(
                shapeSettings.StrokeColor,
                shapeSettings.StrokeOpacity * 100,
                shapeSettings.StrokeThickness);
            FloatingFillToolbarContent?.SetFill(
                shapeSettings.FillColor,
                shapeSettings.FillOpacity * 100);

            FloatingPenToolbarContent?.ApplySettings(_annotationManager.PenSettings);
            FloatingHighlighterToolbarContent?.ApplySettings(_annotationManager.HighlighterSettings);
        }

        /// <summary>
        /// Carga una imagen en el editor
        /// </summary>
        public async Task LoadImageAsync(SoftwareBitmap bitmap)
        {
            if (bitmap == null) return;
            
            // Inicializar managers de forma lazy si aún no se han inicializado
            EnsureManagersInitialized();

            if (_isCropping)
            {
                CancelCrop();
            }

            await UpdateImageAsync(bitmap, resetAnnotations: true);
        }

        private async Task UpdateImageAsync(SoftwareBitmap bitmap, bool resetAnnotations)
        {
            if (bitmap == null) return;

            _currentBitmap = bitmap;
            _hasImage = true;

            // Convertir a formato compatible si es necesario
            SoftwareBitmap displayBitmap = bitmap;
            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                displayBitmap = SoftwareBitmap.Convert(
                    bitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
            }

            // Crear BitmapImage para mostrar
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(displayBitmap);

            EditorImage.Source = source;
            EditorImage.Width = bitmap.PixelWidth;
            EditorImage.Height = bitmap.PixelHeight;

            // Ajustar el tama¤o del canvas de anotaciones
            AnnotationsCanvas.Width = bitmap.PixelWidth;
            AnnotationsCanvas.Height = bitmap.PixelHeight;
            OcrOverlayCanvas.Width = bitmap.PixelWidth;
            OcrOverlayCanvas.Height = bitmap.PixelHeight;
            HandlesCanvas.Width = bitmap.PixelWidth;
            HandlesCanvas.Height = bitmap.PixelHeight;
            CropOverlayCanvas.Width = bitmap.PixelWidth;
            CropOverlayCanvas.Height = bitmap.PixelHeight;
            _zoomManager?.SetBitmap(bitmap);

            ClearOcrResults();

            if (resetAnnotations)
            {
                // Limpiar anotaciones anteriores
                ClearAnnotations();
            }

            if (!ReferenceEquals(displayBitmap, bitmap))
            {
                displayBitmap.Dispose();
            }
        }

        /// <summary>
        /// Limpia la imagen y las anotaciones
        /// </summary>
        public void Clear()
        {
            if (_isCropping)
            {
                CancelCrop();
            }

            EditorImage.Source = null;
            _currentBitmap = null;
            _hasImage = false;
            _isErasing = false;
            ClearOcrResults();
            _zoomManager?.Reset();
            ClearAnnotations();
            CollapseAllSecondaryToolbars();
            _activeToolType = EditorToolType.None;
        }

        /// <summary>
        /// Limpia solo las anotaciones
        /// </summary>
        public void ClearAnnotations()
        {
            AnnotationsCanvas.Children.Clear();
            _historyManager?.ClearHistory();
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearAllAnnotations()
        {
            _annotationManager?.CancelStroke();
            _isErasing = false;
            ClearAnnotations();
            ImageModified?.Invoke(this, EventArgs.Empty);
        }

        #region Tool Activation

        /// <summary>
        /// Activa la herramienta de formas
        /// </summary>
        public void ActivateShapesTool()
        {
            SetActiveTool(EditorToolType.Shapes);
            // Por defecto, seleccionar rectángulo
            _annotationManager?.SetActiveTool(AnnotationToolType.Rectangle);
            FloatingShapesToolbarContent.SetSelectedFromToolType(AnnotationToolType.Rectangle);
            _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Shapes);
            NotifyToolbarVisibility(EditorToolType.Shapes);
        }

        /// <summary>
        /// Activa la herramienta de bolígrafo
        /// </summary>
        public void ActivatePenTool()
        {
            // Deseleccionar forma activa (confirma la forma)
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
            
            SetActiveTool(EditorToolType.Pen);
            _annotationManager?.SetActiveTool(AnnotationToolType.Pen);
            _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Pen);
            NotifyToolbarVisibility(EditorToolType.Pen);
        }

        /// <summary>
        /// Activa la herramienta de bolígrafo sin mostrar el toolbar flotante
        /// </summary>
        public void ActivatePenToolOnly()
        {
            SetActiveTool(EditorToolType.Pen);
            _annotationManager?.SetActiveTool(AnnotationToolType.Pen);
        }

        /// <summary>
        /// Desactiva la herramienta de bolígrafo
        /// </summary>
        public void DeactivatePenTool()
        {
            if (_activeToolType == EditorToolType.Pen)
            {
                _annotationManager?.DeactivateTool();
                _activeToolType = EditorToolType.None;
            }
        }

        /// <summary>
        /// Obtiene el color actual del bolígrafo
        /// </summary>
        public Color GetPenColor()
        {
            return _annotationManager?.PenSettings.StrokeColor ?? Colors.White;
        }

        /// <summary>
        /// Establece el color del bolígrafo
        /// </summary>
        public void SetPenColor(Color color)
        {
            _annotationManager?.SetPenColor(color);
        }

        /// <summary>
        /// Establece el grosor del bolígrafo
        /// </summary>
        public void SetPenThickness(double thickness)
        {
            _annotationManager?.SetPenThickness(thickness);
        }

        /// <summary>
        /// Activa la herramienta de resaltador
        /// </summary>
        public void ActivateHighlighterTool()
        {
            // Deseleccionar forma activa (confirma la forma)
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
            
            SetActiveTool(EditorToolType.Highlighter);
            _annotationManager?.SetActiveTool(AnnotationToolType.Highlighter);
            _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Highlighter);
            NotifyToolbarVisibility(EditorToolType.Highlighter);
        }

        /// <summary>
        /// Activa la herramienta de resaltador sin mostrar el toolbar flotante
        /// </summary>
        public void ActivateHighlighterToolOnly()
        {
            SetActiveTool(EditorToolType.Highlighter);
            _annotationManager?.SetActiveTool(AnnotationToolType.Highlighter);
        }

        /// <summary>
        /// Desactiva la herramienta de resaltador
        /// </summary>
        public void DeactivateHighlighterTool()
        {
            if (_activeToolType == EditorToolType.Highlighter)
            {
                _annotationManager?.DeactivateTool();
                _activeToolType = EditorToolType.None;
            }
        }

        /// <summary>
        /// Obtiene el color actual del resaltador
        /// </summary>
        public Color GetHighlighterColor()
        {
            return _annotationManager?.HighlighterSettings.StrokeColor ?? Colors.Yellow;
        }

        /// <summary>
        /// Establece el color del resaltador
        /// </summary>
        public void SetHighlighterColor(Color color)
        {
            _annotationManager?.SetHighlighterColor(color);
        }

        /// <summary>
        /// Establece el grosor del resaltador
        /// </summary>
        public void SetHighlighterThickness(double thickness)
        {
            _annotationManager?.SetHighlighterThickness(thickness);
        }

        /// <summary>
        /// Activa la herramienta de texto
        /// </summary>
        public void ActivateTextTool()
        {
            SetActiveTool(EditorToolType.Text);
            _annotationManager?.SetActiveTool(AnnotationToolType.Text);
            _toolbarManager?.ShowSecondaryToolbar(SecondaryToolbarType.Text);
            NotifyToolbarVisibility(EditorToolType.Text);
        }

        public void ToggleShapesToolbar()
        {
            if (_toolbarManager == null) return;

            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Shapes))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Shapes);
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
                FloatingShapesToolbarContent.SetStyleExpanded(false);
                FloatingShapesToolbarContent.SetFillExpanded(false);
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            SetActiveTool(EditorToolType.Shapes);
            var toolType = _annotationManager?.ActiveToolType ?? AnnotationToolType.Rectangle;
            if (!AnnotationManager.IsShapeTool(toolType))
            {
                toolType = AnnotationToolType.Rectangle;
            }
            _annotationManager?.SetActiveTool(toolType);
            FloatingShapesToolbarContent.SetSelectedFromToolType(toolType);
            _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Shapes);
            NotifyToolbarVisibility(EditorToolType.Shapes);
        }

        public void TogglePenToolbar()
        {
            if (_toolbarManager == null) return;

            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Pen))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Pen);
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            SetActiveTool(EditorToolType.Pen);
            _annotationManager?.SetActiveTool(AnnotationToolType.Pen);
            _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Pen);
            NotifyToolbarVisibility(EditorToolType.Pen);
        }

        public void ToggleHighlighterToolbar()
        {
            if (_toolbarManager == null) return;

            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Highlighter))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Highlighter);
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            SetActiveTool(EditorToolType.Highlighter);
            _annotationManager?.SetActiveTool(AnnotationToolType.Highlighter);
            _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Highlighter);
            NotifyToolbarVisibility(EditorToolType.Highlighter);
        }

        public void ToggleTextToolbar()
        {
            if (_toolbarManager == null) return;

            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Text))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Text);
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            SetActiveTool(EditorToolType.Text);
            _annotationManager?.SetActiveTool(AnnotationToolType.Text);
            _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Text);
            UpdateTextPreview();
            NotifyToolbarVisibility(EditorToolType.Text);
        }

        public void ToggleEmojiToolbar()
        {
            if (_toolbarManager == null) return;

            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Emoji))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Emoji);
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            SetActiveTool(EditorToolType.Emoji);
            _annotationManager?.SetActiveTool(AnnotationToolType.Emoji);
            _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Emoji);
            NotifyToolbarVisibility(EditorToolType.Emoji);
        }

        public void ToggleEraserTool()
        {
            if (_activeToolType == EditorToolType.Eraser)
            {
                _isErasing = false;
                _annotationManager?.DeactivateTool();
                _activeToolType = EditorToolType.None;
                NotifyToolbarVisibility(EditorToolType.None);
                return;
            }

            _annotationManager?.CancelStroke();
            _annotationManager?.DeactivateTool();
            CollapseAllSecondaryToolbars();
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();

            _isDrawing = false;
            _isErasing = false;
            _activeToolType = EditorToolType.Eraser;
            NotifyToolbarVisibility(EditorToolType.Eraser);
        }

        /// <summary>
        /// Desactiva todas las herramientas
        /// </summary>
        public void DeactivateTools()
        {
            if (_isCropping)
            {
                CancelCrop();
            }

            _isErasing = false;
            _activeToolType = EditorToolType.None;
            _annotationManager?.DeactivateTool();
            CollapseAllSecondaryToolbars();
        }

        private void SetActiveTool(EditorToolType toolType)
        {
            if (_isCropping)
            {
                CancelCrop();
            }

            if (toolType != EditorToolType.Eraser)
            {
                _isErasing = false;
            }

            _activeToolType = toolType;
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
        }

        #endregion

        #region Zoom

        public void ZoomIn()
        {
            _zoomManager?.ZoomIn();
        }

        public void ZoomOut()
        {
            _zoomManager?.ZoomOut();
        }

        public void FitToWindow()
        {
            _zoomManager?.FitToWindow();
        }

        public void SetActualSize()
        {
            _zoomManager?.SetActualSize();
        }

        public bool HandleZoomShortcut(KeyRoutedEventArgs e)
        {
            return _zoomManager?.HandleKeyboardShortcut(e) ?? false;
        }

        private async void EditorScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            var zoomFactor = EditorScrollViewer.ZoomFactor;
            if (Math.Abs(zoomFactor - _lastZoomFactor) < ZOOM_CHANGE_EPSILON)
            {
                return;
            }

            _lastZoomFactor = zoomFactor;

            if (_isZoomCommitInProgress)
            {
                return;
            }

            if (_isCropping || !_hasImage)
            {
                _pendingZoomCommit = false;
                return;
            }

            if (!HasZoomCommitTargets())
            {
                _pendingZoomCommit = false;
                return;
            }

            PrepareForZoomCommit();
            _pendingZoomCommit = true;

            if (e.IsIntermediate)
            {
                return;
            }

            await CommitAnnotationsForZoomAsync();
        }

        private bool HasZoomCommitTargets()
        {
            if (_annotationManager == null || _currentBitmap == null || !_hasImage)
            {
                return false;
            }

            if (_isEditingText || _annotationManager.IsDrawing)
            {
                return true;
            }

            foreach (var child in AnnotationsCanvas.Children)
            {
                if (child is Path path && path.Tag is ShapeData)
                {
                    return true;
                }

                if (child is Grid grid && grid.Tag is TextData)
                {
                    return true;
                }
            }

            return false;
        }

        private void PrepareForZoomCommit()
        {
            if (_annotationManager?.IsDrawing == true)
            {
                var completedPath = _annotationManager.EndStroke();
                _isDrawing = false;
                AnnotationsCanvas.ReleasePointerCaptures();

                if (completedPath != null)
                {
                    _historyManager?.RecordPathAdded(completedPath);
                    ImageModified?.Invoke(this, EventArgs.Empty);
                }
            }

            if (_shapeManipulation?.IsDragging == true)
            {
                _shapeManipulation.EndDrag();
            }

            if (_textManipulation?.IsDragging == true)
            {
                _textManipulation.EndDrag();
            }

            if (_emojiManipulation?.IsDragging == true)
            {
                _emojiManipulation.EndDrag();
            }

            FinishTextEditing();
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
        }

        private async Task CommitAnnotationsForZoomAsync()
        {
            if (_isZoomCommitInProgress || !_pendingZoomCommit)
            {
                return;
            }

            if (_currentBitmap == null)
            {
                _pendingZoomCommit = false;
                return;
            }

            _isZoomCommitInProgress = true;

            try
            {
                var rendered = await RenderWithAnnotationsAsync();
                if (rendered == null)
                {
                    return;
                }

                await UpdateImageAsync(rendered, resetAnnotations: true);
                ImageModified?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _pendingZoomCommit = false;
                _isZoomCommitInProgress = false;
            }
        }

        #endregion

        #region Crop

        public bool IsCropping => _isCropping;

        public void BeginCrop()
        {
            if (!_hasImage || _currentBitmap == null) return;
            if (_isCropping) return;

            _isCropping = true;
            _zoomManager?.FitToWindow();

            _annotationManager?.DeactivateTool();
            _activeToolType = EditorToolType.None;
            CollapseAllSecondaryToolbars();
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
            _cropBounds = new Rect(0, 0, _currentBitmap.PixelWidth, _currentBitmap.PixelHeight);

            // Añadir padding al contenedor para que los handles sean visibles en los bordes
            ImageContainer.Margin = new Thickness(CROP_MODE_PADDING);

            // Ocultar overlay de OCR durante el recorte
            OcrOverlayCanvas.Visibility = Visibility.Collapsed;

            CropOverlayCanvas.Visibility = Visibility.Visible;
            UpdateCropOverlay();
            CropModeChanged?.Invoke(this, true);
        }

        public async Task ApplyCropAsync()
        {
            if (!_isCropping || _currentBitmap == null) return;

            var clamped = ClampCropBounds(_cropBounds);
            int x = (int)Math.Round(clamped.X);
            int y = (int)Math.Round(clamped.Y);
            int width = (int)Math.Round(clamped.Width);
            int height = (int)Math.Round(clamped.Height);

            if (width <= 0 || height <= 0) return;

            var cropped = await CropBitmapAsync(_currentBitmap, x, y, width, height);
            if (cropped == null) return;

            ApplyCropToAnnotations(clamped);
            await UpdateImageAsync(cropped, resetAnnotations: false);

            _historyManager?.ClearHistory();
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
            ImageModified?.Invoke(this, EventArgs.Empty);

            CancelCrop();
            
            // Centrar la imagen recortada en el ScrollViewer
            CenterImageInScrollViewer();
        }

        /// <summary>
        /// Centra la imagen en el ScrollViewer reseteando el scroll al centro.
        /// </summary>
        private void CenterImageInScrollViewer()
        {
            // Resetear el scroll para que el contenido se centre
            // Como ImageContainer tiene HorizontalAlignment="Center" y VerticalAlignment="Center",
            // al poner el scroll en 0,0 la imagen se centrará automáticamente
            EditorScrollViewer.ChangeView(0, 0, EditorScrollViewer.ZoomFactor, disableAnimation: false);
        }

        public void CancelCrop()
        {
            if (!_isCropping) return;

            _isCropping = false;
            _isCropHandleDragging = false;
            _isCropDragging = false;
            _activeCropHandle = null;

            // Remover el padding del modo recorte
            ImageContainer.Margin = new Thickness(0);

            CropOverlayCanvas.Visibility = Visibility.Collapsed;
            if (_cropHandleSet != null)
            {
                ResizeHandleManager.HideHandles(_cropHandleSet);
            }

            // Restaurar overlay de OCR si hay resultados
            if (HasOcrResult)
            {
                OcrOverlayCanvas.Visibility = Visibility.Visible;
            }

            CropModeChanged?.Invoke(this, false);
        }

        private Rect ClampCropBounds(Rect bounds)
        {
            double maxWidth = EditorImage.Width;
            double maxHeight = EditorImage.Height;

            double left = Math.Max(0, bounds.Left);
            double top = Math.Max(0, bounds.Top);
            double right = Math.Min(maxWidth, bounds.Right);
            double bottom = Math.Min(maxHeight, bounds.Bottom);

            if (right - left < Constants.MIN_SELECTION_SIZE)
            {
                if (left <= 0)
                {
                    right = Math.Min(maxWidth, left + Constants.MIN_SELECTION_SIZE);
                }
                else
                {
                    left = Math.Max(0, right - Constants.MIN_SELECTION_SIZE);
                }
            }

            if (bottom - top < Constants.MIN_SELECTION_SIZE)
            {
                if (top <= 0)
                {
                    bottom = Math.Min(maxHeight, top + Constants.MIN_SELECTION_SIZE);
                }
                else
                {
                    top = Math.Max(0, bottom - Constants.MIN_SELECTION_SIZE);
                }
            }

            return new Rect(left, top, Math.Max(Constants.MIN_SELECTION_SIZE, right - left), Math.Max(Constants.MIN_SELECTION_SIZE, bottom - top));
        }

        private void UpdateCropOverlay()
        {
            if (_cropHandleSet == null) return;

            var clamped = ClampCropBounds(_cropBounds);
            _cropBounds = clamped;

            // Actualizar el rectángulo de selección
            Canvas.SetLeft(CropSelectionRect, clamped.Left);
            Canvas.SetTop(CropSelectionRect, clamped.Top);
            CropSelectionRect.Width = clamped.Width;
            CropSelectionRect.Height = clamped.Height;

            // Actualizar los 4 rectángulos de overlay oscuro
            double imageWidth = EditorImage.Width;
            double imageHeight = EditorImage.Height;

            // Top: desde arriba hasta el borde superior de la selección (ancho completo)
            Canvas.SetLeft(CropDimTop, 0);
            Canvas.SetTop(CropDimTop, 0);
            CropDimTop.Width = imageWidth;
            CropDimTop.Height = Math.Max(0, clamped.Top);

            // Bottom: desde el borde inferior de la selección hasta abajo (ancho completo)
            Canvas.SetLeft(CropDimBottom, 0);
            Canvas.SetTop(CropDimBottom, clamped.Bottom);
            CropDimBottom.Width = imageWidth;
            CropDimBottom.Height = Math.Max(0, imageHeight - clamped.Bottom);

            // Left: desde el borde izquierdo hasta la selección (solo altura de la selección)
            Canvas.SetLeft(CropDimLeft, 0);
            Canvas.SetTop(CropDimLeft, clamped.Top);
            CropDimLeft.Width = Math.Max(0, clamped.Left);
            CropDimLeft.Height = clamped.Height;

            // Right: desde la selección hasta el borde derecho (solo altura de la selección)
            Canvas.SetLeft(CropDimRight, clamped.Right);
            Canvas.SetTop(CropDimRight, clamped.Top);
            CropDimRight.Width = Math.Max(0, imageWidth - clamped.Right);
            CropDimRight.Height = clamped.Height;

            ResizeHandleManager.ShowHandles(_cropHandleSet, clamped);
        }

        private async Task<SoftwareBitmap?> CropBitmapAsync(SoftwareBitmap source, int x, int y, int width, int height)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(source);
            encoder.BitmapTransform.Bounds = new BitmapBounds
            {
                X = (uint)x,
                Y = (uint)y,
                Width = (uint)width,
                Height = (uint)height
            };
            encoder.IsThumbnailGenerated = false;
            await encoder.FlushAsync();

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
        }

        private void ApplyCropToAnnotations(Rect cropRect)
        {
            double offsetX = cropRect.X;
            double offsetY = cropRect.Y;
            double maxWidth = cropRect.Width;
            double maxHeight = cropRect.Height;

            var toRemove = new List<UIElement>();

            foreach (var child in AnnotationsCanvas.Children)
            {
                if (child is Path path && path.Tag is ShapeData data)
                {
                    data.StartPoint = new Point(data.StartPoint.X - offsetX, data.StartPoint.Y - offsetY);
                    data.EndPoint = new Point(data.EndPoint.X - offsetX, data.EndPoint.Y - offsetY);

                    if (data.Type == "Pen" || data.Type == "Highlighter")
                    {
                        TranslateGeometry(path, -offsetX, -offsetY);
                    }
                    else
                    {
                        _shapeManipulation?.UpdateShapeGeometry(path, data);
                    }

                    var bounds = path.Data?.Bounds ?? Rect.Empty;
                    if (bounds.Right < 0 || bounds.Bottom < 0 || bounds.Left > maxWidth || bounds.Top > maxHeight)
                    {
                        toRemove.Add(path);
                    }
                }
                else if (child is Grid grid && grid.Tag is TextData textData)
                {
                    double left = Canvas.GetLeft(grid);
                    double top = Canvas.GetTop(grid);

                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;

                    left -= offsetX;
                    top -= offsetY;

                    Canvas.SetLeft(grid, left);
                    Canvas.SetTop(grid, top);
                    textData.Position = new Point(left, top);

                    double width = double.IsNaN(grid.Width) ? grid.ActualWidth : grid.Width;
                    double height = double.IsNaN(grid.Height) ? grid.ActualHeight : grid.Height;

                    if (left + width < 0 || top + height < 0 || left > maxWidth || top > maxHeight)
                    {
                        toRemove.Add(grid);
                    }
                }
            }

            foreach (var element in toRemove)
            {
                AnnotationsCanvas.Children.Remove(element);
            }

            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
        }

        private static void TranslateGeometry(Path path, double dx, double dy)
        {
            if (path.Data == null) return;

            var translate = new TranslateTransform { X = dx, Y = dy };
            if (path.Data.Transform == null)
            {
                path.Data.Transform = translate;
                return;
            }

            if (path.Data.Transform is TransformGroup group)
            {
                group.Children.Add(translate);
            }
            else
            {
                var newGroup = new TransformGroup();
                newGroup.Children.Add(path.Data.Transform);
                newGroup.Children.Add(translate);
                path.Data.Transform = newGroup;
            }
        }

        private void CropHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping || sender is not FrameworkElement handle || handle.Tag is not string tag)
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

        private void CropHandle_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping) return;

            // Restaurar cursor por defecto solo si no estamos arrastrando
            if (!_isCropHandleDragging)
            {
                ProtectedCursor = null;
            }
        }

        private void CropHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping || sender is not FrameworkElement handle || handle.Tag is not string tag)
                return;

            _isCropHandleDragging = true;
            _activeCropHandle = tag;
            _cropDragStart = e.GetCurrentPoint(AnnotationsCanvas).Position;
            _cropBoundsBeforeDrag = _cropBounds;
            handle.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void CropHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping || !_isCropHandleDragging || string.IsNullOrEmpty(_activeCropHandle))
                return;

            var currentPoint = e.GetCurrentPoint(AnnotationsCanvas).Position;
            double dx = currentPoint.X - _cropDragStart.X;
            double dy = currentPoint.Y - _cropDragStart.Y;

            var newBounds = ResizeHandleManager.CalculateNewBounds(_activeCropHandle, _cropBoundsBeforeDrag, dx, dy);
            _cropBounds = ClampCropBounds(newBounds);
            UpdateCropOverlay();
            e.Handled = true;
        }

        private void CropHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping) return;

            _isCropHandleDragging = false;
            _activeCropHandle = null;
            ProtectedCursor = null;
            (sender as FrameworkElement)?.ReleasePointerCapture(e.Pointer);
        }

        private void CropHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isCropHandleDragging = false;
            _activeCropHandle = null;
            ProtectedCursor = null;
        }

        private void CropSelection_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping) return;

            _isCropDragging = true;
            _cropDragStart = e.GetCurrentPoint(AnnotationsCanvas).Position;
            _cropBoundsBeforeDrag = _cropBounds;
            CropSelectionRect.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void CropSelection_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isCropping || !_isCropDragging) return;

            var currentPoint = e.GetCurrentPoint(AnnotationsCanvas).Position;
            double dx = currentPoint.X - _cropDragStart.X;
            double dy = currentPoint.Y - _cropDragStart.Y;

            var newBounds = new Rect(
                _cropBoundsBeforeDrag.X + dx,
                _cropBoundsBeforeDrag.Y + dy,
                _cropBoundsBeforeDrag.Width,
                _cropBoundsBeforeDrag.Height);

            _cropBounds = ClampCropBounds(newBounds);
            UpdateCropOverlay();
            e.Handled = true;
        }

        private void CropSelection_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isCropDragging = false;
            CropSelectionRect.ReleasePointerCapture(e.Pointer);
        }

        private void CropSelection_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isCropDragging = false;
        }

        #endregion

        #region Toolbar Management

        private void FloatingMenu_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Evitar que el evento se propague y cierre el menú
            e.Handled = true;
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_toolbarManager == null) return;

            if (IsPointerOverToolbar(e.OriginalSource as DependencyObject))
            {
                return;
            }

            // Al hacer clic fuera de los toolbars, cerrar menús secundarios EXCEPTO el menú de formas
            // El menú de formas solo se cierra al hacer clic en otro botón de la toolbar
            CollapseSecondaryToolbarsExceptShapes();
        }

        private bool IsPointerOverToolbar(DependencyObject? element)
        {
            var current = element;
            while (current != null)
            {
                if (current == FloatingShapesToolbar ||
                    current == FloatingStyleToolbar ||
                    current == FloatingFillToolbar ||
                    current == FloatingPenToolbar ||
                    current == FloatingHighlighterToolbar ||
                    current == FloatingTextToolbar ||
                    current == FloatingTextColorToolbar ||
                    current == FloatingTextHighlightToolbar)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void NotifyToolbarVisibility(EditorToolType toolType)
        {
            ToolbarVisibilityChanged?.Invoke(this, toolType);
        }

        private void CollapseAllSecondaryToolbars()
        {
            _toolbarManager?.CollapseAllSecondaryToolbars();
            FloatingShapesToolbarContent.SetStyleExpanded(false);
            FloatingShapesToolbarContent.SetFillExpanded(false);
            // Notificar la herramienta activa actual para mantener el estado visual del botón
            NotifyToolbarVisibility(_activeToolType);
        }

        /// <summary>
        /// Colapsa todos los menús secundarios EXCEPTO el menú de formas.
        /// Se usa cuando se hace clic fuera de los menús - el menú de formas debe permanecer visible
        /// hasta que se haga clic en otro botón de la toolbar.
        /// </summary>
        private void CollapseSecondaryToolbarsExceptShapes()
        {
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Pen);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Highlighter);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Text);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            
            FloatingShapesToolbarContent.SetStyleExpanded(false);
            FloatingShapesToolbarContent.SetFillExpanded(false);
            // Notificar la herramienta activa actual para mantener el estado visual del botón
            NotifyToolbarVisibility(_activeToolType);
        }

        #endregion

        #region Pointer Events

        private void ImageContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_hasImage || _annotationManager == null) return;
            if (_isCropping) return;

            var point = e.GetCurrentPoint(AnnotationsCanvas);
            if (!point.Properties.IsLeftButtonPressed) return;

            // Manejar panning con Ctrl + clic izquierdo
            var isCtrlDown = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
            if (isCtrlDown)
            {
                _isCtrlPressed = true;
                StartPanning(e);
                ImageContainer.CapturePointer(e.Pointer);
                return;
            }

            var position = point.Position;

            // Verificar si estamos dentro de los límites de la imagen
            if (position.X < 0 || position.Y < 0 || 
                position.X > EditorImage.Width || position.Y > EditorImage.Height)
                return;

            if (_activeToolType == EditorToolType.Eraser)
            {
                _isErasing = true;
                EraseAtPoint(position);
                AnnotationsCanvas.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }
            // Manejar herramienta de texto
            if (_activeToolType == EditorToolType.Text)
            {
                CreateTextAtPosition(position);
                return;
            }

            // Manejar dibujo con pen/highlighter/shapes
            if (_activeToolType == EditorToolType.Pen || 
                _activeToolType == EditorToolType.Highlighter ||
                (_activeToolType == EditorToolType.Shapes && _annotationManager.HasActiveTool))
            {
                if (_annotationManager.StartStroke(position))
                {
                    _isDrawing = true;
                    _lastPoint = position;
                    AnnotationsCanvas.CapturePointer(e.Pointer);
                }
                return;
            }

            // Hit test para selección de formas existentes
            var hitShape = _shapeManipulation?.GetShapeAtPoint(position);
            if (hitShape != null)
            {
                // Finalizar edición de texto si se está editando
                FinishTextEditing();
                
                _shapeManipulation?.SelectShape(hitShape);
                _shapeManipulation?.StartDrag(position);
                return;
            }

            // Hit test para selección de emojis existentes
            var hitEmoji = _emojiManipulation?.GetEmojiAtPoint(position);
            if (hitEmoji != null)
            {
                // Finalizar edición de texto si se está editando
                FinishTextEditing();
                
                _emojiManipulation?.SelectEmoji(hitEmoji);
                _emojiManipulation?.StartDrag(position);
                return;
            }

            // Hit test para selección de texto existente
            var hitText = _textManipulation?.GetTextAtPoint(position);
            if (hitText != null)
            {
                // Si es el mismo texto que se está editando, no hacer nada
                if (_isEditingText && _activeTextElement == hitText)
                {
                    return;
                }
                
                // Finalizar edición de texto anterior si existe
                FinishTextEditing();
                
                // Iniciar edición del texto clickeado
                StartTextEditing(hitText);
                _textManipulation?.SelectText(hitText);
                return;
            }

            // Finalizar edición de texto si se hizo clic fuera
            FinishTextEditing();
            
            // Deseleccionar si no se hizo clic en nada
            _shapeManipulation?.Deselect();
            _textManipulation?.Deselect();
            _emojiManipulation?.Deselect();
        }

        private void ImageContainer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_hasImage || _annotationManager == null) return;
            if (_isCropping) return;

            // Detectar estado de Ctrl para actualizar cursor (más robusto que KeyDown/KeyUp)
            var isCtrlDown = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
            if (isCtrlDown != _isCtrlPressed && !_isPanning)
            {
                _isCtrlPressed = isCtrlDown;
                UpdatePanningCursor();
            }

            // Manejar panning
            if (_isPanning)
            {
                ContinuePanning(e);
                return;
            }

            var point = e.GetCurrentPoint(AnnotationsCanvas);
            var position = point.Position;

            // Limitar posición a los límites de la imagen
            position = ClampToImageBounds(position);
            if (_activeToolType == EditorToolType.Eraser && _isErasing)
            {
                EraseAtPoint(position);
                e.Handled = true;
                return;
            }

            // Manejar dibujo
            if (_isDrawing && _annotationManager.IsDrawing)
            {
                _annotationManager.ContinueStroke(position);
                _lastPoint = position;
                return;
            }

            // Manejar arrastre de forma
            if (_shapeManipulation?.IsDragging == true)
            {
                _shapeManipulation.ContinueDrag(position);
                return;
            }

            // Manejar arrastre de emoji
            if (_emojiManipulation?.IsDragging == true)
            {
                _emojiManipulation.ContinueDrag(position);
                return;
            }

            // Manejar arrastre de texto
            if (_textManipulation?.IsDragging == true)
            {
                _textManipulation.ContinueDrag(position);
                return;
            }
        }

        private void ImageContainer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_hasImage || _annotationManager == null) return;

            // Manejar fin de panning
            if (_isPanning)
            {
                StopPanning();
                _isCtrlPressed = false;
                ImageContainer.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }

            if (_activeToolType == EditorToolType.Eraser && _isErasing)
            {
                _isErasing = false;
                AnnotationsCanvas.ReleasePointerCaptures();
                e.Handled = true;
                return;
            }
            if (_isCropping) return;

            // Finalizar dibujo
            if (_isDrawing && _annotationManager.IsDrawing)
            {
                var completedPath = _annotationManager.EndStroke();
                _isDrawing = false;
                AnnotationsCanvas.ReleasePointerCaptures();
                
                // Registrar en historial
                if (completedPath != null)
                {
                    _historyManager?.RecordPathAdded(completedPath);
                    ImageModified?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            // Finalizar arrastre de forma
            if (_shapeManipulation?.IsDragging == true)
            {
                _shapeManipulation.EndDrag();
                return;
            }

            // Finalizar arrastre de emoji
            if (_emojiManipulation?.IsDragging == true)
            {
                _emojiManipulation.EndDrag();
                return;
            }

            // Finalizar arrastre de texto
            if (_textManipulation?.IsDragging == true)
            {
                _textManipulation.EndDrag();
                return;
            }
        }

        private void ImageContainer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            // Limpiar estado de panning si se pierde la captura del puntero
            if (_isPanning)
            {
                StopPanning();
                _isCtrlPressed = false;
            }
        }

        private Point ClampToImageBounds(Point position)
        {
            return new Point(
                Math.Clamp(position.X, 0, EditorImage.Width),
                Math.Clamp(position.Y, 0, EditorImage.Height));
        }

        private void EraseAtPoint(Point position)
        {
            if (_historyManager == null || _annotationManager == null) return;

            var hitEmoji = _emojiManipulation?.GetEmojiAtPoint(position);
            if (hitEmoji != null)
            {
                _emojiManipulation?.Deselect();
                if (AnnotationsCanvas.Children.Contains(hitEmoji))
                {
                    AnnotationsCanvas.Children.Remove(hitEmoji);
                    _historyManager.RecordElementRemoved(hitEmoji);
                    ImageModified?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            var hitText = _textManipulation?.GetTextAtPoint(position);
            if (hitText != null)
            {
                _textManipulation?.Deselect();
                if (AnnotationsCanvas.Children.Contains(hitText))
                {
                    AnnotationsCanvas.Children.Remove(hitText);
                    _historyManager.RecordElementRemoved(hitText);
                    ImageModified?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            var hitShape = _annotationManager?.GetPathAtPoint(position);
            if (hitShape != null)
            {
                if (_shapeManipulation?.SelectedShape == hitShape)
                {
                    _shapeManipulation.Deselect();
                }

                if (AnnotationsCanvas.Children.Contains(hitShape))
                {
                    AnnotationsCanvas.Children.Remove(hitShape);
                    _historyManager.RecordPathRemoved(hitShape);
                    ImageModified?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        #endregion

        #region Panning (Ctrl + Arrastrar)

        /// <summary>
        /// Maneja el evento KeyDown para detectar cuando se presiona Ctrl y atajos de teclado OCR
        /// </summary>
        private void ImageEditorControl_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Control && !_isCtrlPressed)
            {
                _isCtrlPressed = true;
                UpdatePanningCursor();
            }
            
            // Atajos de teclado para OCR cuando hay resultados
            if (_ocrBoxes.Count > 0)
            {
                var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                
                if (ctrlPressed)
                {
                    switch (e.Key)
                    {
                        case Windows.System.VirtualKey.C:
                            // Copiar texto seleccionado
                            if (_selectedOcrBoxes.Count > 0)
                            {
                                CopySelectedOcrText();
                                e.Handled = true;
                            }
                            break;
                        case Windows.System.VirtualKey.A:
                            // Seleccionar todo el texto
                            SelectAllOcrText();
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Maneja el evento KeyUp para detectar cuando se suelta Ctrl
        /// </summary>
        private void ImageEditorControl_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Control)
            {
                _isCtrlPressed = false;
                if (_isPanning)
                {
                    StopPanning();
                }
                UpdatePanningCursor();
            }
        }

        /// <summary>
        /// Actualiza el cursor según el estado de panning.
        /// Usa Hand para "grab" (listo para arrastrar) y SizeAll para "grabbing" (arrastrando).
        /// Nota: Windows no tiene un cursor "grabbing" nativo (mano cerrada), SizeAll es la convención.
        /// </summary>
        private void UpdatePanningCursor()
        {
            if (!_hasImage)
            {
                ProtectedCursor = null;
                return;
            }

            if (_isPanning)
            {
                ProtectedCursor = _grabbingCursor;
            }
            else if (_isCtrlPressed)
            {
                ProtectedCursor = _grabCursor;
            }
            else
            {
                // null restaura el cursor por defecto del control
                ProtectedCursor = null;
            }
        }

        /// <summary>
        /// Inicia el panning de la imagen
        /// </summary>
        private void StartPanning(PointerRoutedEventArgs e)
        {
            _isPanning = true;
            _panStartPoint = e.GetCurrentPoint(this).Position;
            _panStartHorizontalOffset = EditorScrollViewer.HorizontalOffset;
            _panStartVerticalOffset = EditorScrollViewer.VerticalOffset;
            UpdatePanningCursor();
            e.Handled = true;
        }

        /// <summary>
        /// Continúa el panning de la imagen
        /// </summary>
        private void ContinuePanning(PointerRoutedEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetCurrentPoint(this).Position;
            var deltaX = _panStartPoint.X - currentPoint.X;
            var deltaY = _panStartPoint.Y - currentPoint.Y;

            EditorScrollViewer.ChangeView(
                _panStartHorizontalOffset + deltaX,
                _panStartVerticalOffset + deltaY,
                null,
                disableAnimation: true);

            e.Handled = true;
        }

        /// <summary>
        /// Detiene el panning de la imagen
        /// </summary>
        private void StopPanning()
        {
            _isPanning = false;
            UpdatePanningCursor();
        }

        #endregion

        #region OCR

        public async Task<bool> AnalyzeTextAsync()
        {
            if (_currentBitmap == null || _isOcrRunning)
            {
                return false;
            }

            _isOcrRunning = true;
            ClearOcrResults();

            SoftwareBitmap? converted = null;
            SoftwareBitmap? scaled = null;
            try
            {
                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null)
                {
                    return false;
                }

                var bitmapForOcr = _currentBitmap;
                if (bitmapForOcr.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    bitmapForOcr.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    converted = SoftwareBitmap.Convert(
                        bitmapForOcr,
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);
                    bitmapForOcr = converted;
                }

                _ocrScale = GetOcrScale(bitmapForOcr);
                if (_ocrScale > 1.01)
                {
                    scaled = await ScaleBitmapAsync(bitmapForOcr, _ocrScale);
                    bitmapForOcr = scaled;
                }
                else
                {
                    _ocrScale = 1.0;
                }

                _ocrResult = await engine.RecognizeAsync(bitmapForOcr);
                BuildOcrOverlay(_ocrResult);
                OcrResultsChanged?.Invoke(this, EventArgs.Empty);
                return _ocrBoxes.Count > 0;
            }
            finally
            {
                _isOcrRunning = false;
                if (scaled != null)
                {
                    scaled.Dispose();
                }
                converted?.Dispose();
            }
        }

        public string GetAllOcrText()
        {
            return _ocrResult?.Text ?? string.Empty;
        }

        public bool ExitOcrMode()
        {
            if (_isOcrRunning || _ocrBoxes.Count == 0)
            {
                return false;
            }

            ClearOcrResults();
            return true;
        }

        private void BuildOcrOverlay(OcrResult? result)
        {
            if (result == null)
            {
                return;
            }

            _selectedOcrBoxes.Clear();
            _isOcrAllSelected = false;

            var canvasWidth = OcrOverlayCanvas.Width > 0 ? OcrOverlayCanvas.Width : EditorImage.Width;
            var canvasHeight = OcrOverlayCanvas.Height > 0 ? OcrOverlayCanvas.Height : EditorImage.Height;
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                return;
            }

            var maskGeometry = new PathGeometry
            {
                FillRule = FillRule.EvenOdd
            };
            maskGeometry.Figures.Add(CreateRectangleFigure(new Rect(0, 0, canvasWidth, canvasHeight)));

            var lineBoxes = BuildOcrLineBoxes(result, _ocrScale);
            var paragraphBoxes = BuildOcrParagraphs(lineBoxes);
            var wordBoxes = BuildOcrWordBoxes(result, _ocrScale);
            const double padding = 6.0; // Padding aumentado para mejor visualización

            foreach (var paragraph in paragraphBoxes)
            {
                var paddedRect = ExpandRect(paragraph.Bounds, padding, canvasWidth, canvasHeight);
                if (paddedRect.Width <= 0 || paddedRect.Height <= 0)
                {
                    continue;
                }

                maskGeometry.Figures.Add(CreateRectangleFigure(paddedRect));

                var highlight = new Border
                {
                    Width = paddedRect.Width,
                    Height = paddedRect.Height,
                    Background = _ocrSoftHighlightBrush,
                    CornerRadius = new CornerRadius(6),
                    IsHitTestVisible = false,
                    Padding = new Thickness(padding) // Padding interno
                };

                Canvas.SetLeft(highlight, paddedRect.X);
                Canvas.SetTop(highlight, paddedRect.Y);
                Canvas.SetZIndex(highlight, 1);

                OcrOverlayCanvas.Children.Add(highlight);
            }

            foreach (var word in wordBoxes)
            {
                var clampedRect = ClampRect(word.Bounds, canvasWidth, canvasHeight);
                if (clampedRect.Width <= 0 || clampedRect.Height <= 0)
                {
                    continue;
                }

                var box = new Border
                {
                    Width = clampedRect.Width,
                    Height = clampedRect.Height,
                    BorderThickness = new Thickness(0),
                    BorderBrush = TransparentBrush,
                    Background = TransparentBrush,
                    Tag = new OcrWordInfo(word.Text, word.Index)
                };

                box.PointerEntered += OcrBox_PointerEntered;
                box.PointerExited += OcrBox_PointerExited;
                box.PointerPressed += OcrBox_PointerPressed;
                box.RightTapped += OcrBox_RightTapped;

                Canvas.SetLeft(box, clampedRect.X);
                Canvas.SetTop(box, clampedRect.Y);
                Canvas.SetZIndex(box, 2);

                OcrOverlayCanvas.Children.Add(box);
                _ocrBoxes.Add(box);
            }

            if (_ocrBoxes.Count > 0)
            {
                OcrOverlayCanvas.Background = TransparentBrush;
                _ocrMaskPath = new Path
                {
                    Data = maskGeometry,
                    Fill = _ocrMaskBrush,
                    IsHitTestVisible = false
                };

                Canvas.SetZIndex(_ocrMaskPath, 0);
                OcrOverlayCanvas.Children.Insert(0, _ocrMaskPath);
                OcrOverlayCanvas.Visibility = Visibility.Visible;
            }
            else
            {
                OcrOverlayCanvas.Visibility = Visibility.Collapsed;
            }
        }

        private static PathFigure CreateRectangleFigure(Rect rect)
        {
            var figure = new PathFigure
            {
                StartPoint = new Point(rect.X, rect.Y),
                IsClosed = true,
                IsFilled = true
            };

            figure.Segments.Add(new LineSegment { Point = new Point(rect.X + rect.Width, rect.Y) });
            figure.Segments.Add(new LineSegment { Point = new Point(rect.X + rect.Width, rect.Y + rect.Height) });
            figure.Segments.Add(new LineSegment { Point = new Point(rect.X, rect.Y + rect.Height) });

            return figure;
        }

        private static Rect ScaleRect(Rect rect, double scale)
        {
            if (scale <= 0)
            {
                return rect;
            }

            return new Rect(
                rect.X / scale,
                rect.Y / scale,
                rect.Width / scale,
                rect.Height / scale);
        }

        private static Rect UnionRects(Rect a, Rect b)
        {
            var x1 = Math.Min(a.X, b.X);
            var y1 = Math.Min(a.Y, b.Y);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        private static Rect ExpandRect(Rect rect, double padding, double maxWidth, double maxHeight)
        {
            var x = Math.Max(0, rect.X - padding);
            var y = Math.Max(0, rect.Y - padding);
            var right = Math.Min(maxWidth, rect.X + rect.Width + padding);
            var bottom = Math.Min(maxHeight, rect.Y + rect.Height + padding);
            return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }

        private static Rect ClampRect(Rect rect, double maxWidth, double maxHeight)
        {
            var x = Math.Max(0, rect.X);
            var y = Math.Max(0, rect.Y);
            var right = Math.Min(maxWidth, rect.X + rect.Width);
            var bottom = Math.Min(maxHeight, rect.Y + rect.Height);
            return new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        }

        private static List<OcrWordBox> BuildOcrWordBoxes(OcrResult result, double scale)
        {
            var words = new List<OcrWordBox>();
            var index = 0;
            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    if (string.IsNullOrWhiteSpace(word.Text))
                    {
                        continue;
                    }

                    var rect = ScaleRect(word.BoundingRect, scale);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    words.Add(new OcrWordBox(rect, word.Text, index++));
                }
            }

            return words;
        }

        private static List<OcrLineBox> BuildOcrLineBoxes(OcrResult result, double scale)
        {
            var lines = new List<OcrLineBox>();
            foreach (var line in result.Lines)
            {
                Rect? lineRect = null;
                foreach (var word in line.Words)
                {
                    if (string.IsNullOrWhiteSpace(word.Text))
                    {
                        continue;
                    }

                    var rect = ScaleRect(word.BoundingRect, scale);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    lineRect = lineRect == null ? rect : UnionRects(lineRect.Value, rect);
                }

                if (lineRect.HasValue)
                {
                    var text = string.IsNullOrWhiteSpace(line.Text)
                        ? BuildLineText(line)
                        : line.Text;
                    lines.Add(new OcrLineBox(lineRect.Value, text));
                }
            }

            lines.Sort((left, right) => left.Bounds.Y.CompareTo(right.Bounds.Y));
            return lines;
        }

        private static string BuildLineText(OcrLine line)
        {
            var parts = new List<string>();
            foreach (var word in line.Words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text))
                {
                    parts.Add(word.Text);
                }
            }

            return string.Join(" ", parts);
        }

        private static List<OcrParagraphBox> BuildOcrParagraphs(List<OcrLineBox> lines)
        {
            var paragraphs = new List<OcrParagraphBox>();
            OcrParagraphBox? current = null;

            foreach (var line in lines)
            {
                if (current == null)
                {
                    current = new OcrParagraphBox(line.Bounds, line.Text);
                    paragraphs.Add(current);
                    continue;
                }

                var gap = line.Bounds.Y - current.Bounds.Bottom;
                var lineHeight = Math.Max(1, line.Bounds.Height);
                var gapThreshold = lineHeight * 0.8;
                var leftThreshold = lineHeight * 1.5;
                var leftDelta = Math.Abs(line.Bounds.X - current.Bounds.X);

                if (gap <= gapThreshold && leftDelta <= leftThreshold)
                {
                    current.AddLine(line.Bounds, line.Text);
                }
                else
                {
                    current = new OcrParagraphBox(line.Bounds, line.Text);
                    paragraphs.Add(current);
                }
            }

            return paragraphs;
        }

        private sealed class OcrLineBox
        {
            public Rect Bounds { get; }
            public string Text { get; }

            public OcrLineBox(Rect bounds, string text)
            {
                Bounds = bounds;
                Text = text;
            }
        }

        private sealed class OcrWordBox
        {
            public Rect Bounds { get; }
            public string Text { get; }
            public int Index { get; }

            public OcrWordBox(Rect bounds, string text, int index)
            {
                Bounds = bounds;
                Text = text;
                Index = index;
            }
        }

        private sealed class OcrWordInfo
        {
            public string Text { get; }
            public int Index { get; }

            public OcrWordInfo(string text, int index)
            {
                Text = text;
                Index = index;
            }
        }

        private sealed class OcrParagraphBox
        {
            public Rect Bounds { get; private set; }
            public string Text => string.Join(Environment.NewLine, _lines);

            private readonly List<string> _lines = new();

            public OcrParagraphBox(Rect bounds, string text)
            {
                Bounds = bounds;
                _lines.Add(text);
            }

            public void AddLine(Rect bounds, string text)
            {
                Bounds = UnionRects(Bounds, bounds);
                _lines.Add(text);
            }
        }

        private static double GetOcrScale(SoftwareBitmap bitmap)
        {
            var maxDimension = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
            if (maxDimension <= 0)
            {
                return 1.0;
            }

            var scale = maxDimension < 1200 ? 2.0 : 1.0;
            var maxAllowed = 3000.0;
            var scaledMax = maxDimension * scale;
            if (scaledMax > maxAllowed)
            {
                scale = maxAllowed / maxDimension;
            }

            return scale;
        }

        private static async Task<SoftwareBitmap> ScaleBitmapAsync(SoftwareBitmap source, double scale)
        {
            var targetWidth = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
            if (targetWidth == source.PixelWidth && targetHeight == source.PixelHeight)
            {
                return source;
            }

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
            encoder.SetSoftwareBitmap(source);
            encoder.BitmapTransform.ScaledWidth = (uint)targetWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)targetHeight;
            await encoder.FlushAsync();

            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        private void ClearOcrResults()
        {
            _ocrResult = null;
            _selectedOcrBoxes.Clear();
            _isOcrAllSelected = false;
            _isOcrSelecting = false;
            _ocrSelectionAnchorIndex = -1;
            _ocrSelectionCurrentIndex = -1;
            _ocrScale = 1.0;
            ProtectedCursor = null;
            OcrOverlayCanvas.ReleasePointerCaptures();
            _ocrBoxes.Clear();
            _ocrMaskPath = null;
            OcrOverlayCanvas.Children.Clear();
            OcrOverlayCanvas.Background = null;
            OcrOverlayCanvas.Visibility = Visibility.Collapsed;
            OcrResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OcrBox_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = _ocrTextCursor;
            if (_isOcrAllSelected)
            {
                return;
            }

            if (sender is Border box && !IsOcrBoxSelected(box))
            {
                box.Background = _ocrHoverBrush;
            }
        }

        private void OcrBox_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = null;
            if (_isOcrAllSelected)
            {
                return;
            }

            if (sender is Border box && !IsOcrBoxSelected(box))
            {
                box.Background = TransparentBrush;
            }
        }

        private void OcrBox_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border box)
            {
                return;
            }

            var point = e.GetCurrentPoint(box);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (_isOcrAllSelected)
            {
                _isOcrAllSelected = false;
                ClearSelectedOcrBoxes();
                BeginOcrSelection(box, e.Pointer);
                e.Handled = true;
                return;
            }

            BeginOcrSelection(box, e.Pointer);
            e.Handled = true;
        }

        private void OcrBox_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not Border box)
            {
                return;
            }

            if (!IsOcrBoxSelected(box))
            {
                SelectOcrBox(box, addToSelection: false);
            }

            _ocrSelectionAnchorIndex = GetOcrBoxIndex(box);
            _ocrSelectionCurrentIndex = _ocrSelectionAnchorIndex;
            
            // Notificar que se va a abrir el menú contextual (para cerrar otros flyouts)
            OcrContextMenuOpening?.Invoke(this, EventArgs.Empty);
            
            _ocrContextFlyout.ShowAt(box, e.GetPosition(box));
            e.Handled = true;
        }

        private void OcrOverlayCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_ocrBoxes.Count == 0)
            {
                return;
            }

            var point = e.GetCurrentPoint(OcrOverlayCanvas);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            var hit = GetOcrBoxAtPoint(point.Position);
            if (hit == null)
            {
                ClearSelectedOcrBoxes();
                _ocrSelectionAnchorIndex = -1;
                _ocrSelectionCurrentIndex = -1;
                EndOcrSelection();
                e.Handled = true;
                return;
            }

            BeginOcrSelection(hit, e.Pointer);
            e.Handled = true;
        }

        private void OcrOverlayCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isOcrSelecting)
            {
                return;
            }

            var point = e.GetCurrentPoint(OcrOverlayCanvas);
            if (!point.Properties.IsLeftButtonPressed)
            {
                EndOcrSelection();
                return;
            }

            var hit = GetOcrBoxAtPoint(point.Position);
            if (hit != null)
            {
                var index = GetOcrBoxIndex(hit);
                if (index >= 0)
                {
                    UpdateOcrRangeSelection(index);
                }
            }
        }

        private void OcrOverlayCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            EndOcrSelection();
        }

        private void OcrOverlayCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            EndOcrSelection();
        }

        private void BeginOcrSelection(Border box, Pointer pointer)
        {
            ClearSelectedOcrBoxes();
            _ocrSelectionAnchorIndex = GetOcrBoxIndex(box);
            _ocrSelectionCurrentIndex = _ocrSelectionAnchorIndex;
            UpdateOcrRangeSelection(_ocrSelectionAnchorIndex);
            _isOcrSelecting = true;
            OcrOverlayCanvas.CapturePointer(pointer);
        }

        private void EndOcrSelection()
        {
            if (!_isOcrSelecting)
            {
                return;
            }

            _isOcrSelecting = false;
            OcrOverlayCanvas.ReleasePointerCaptures();
        }

        private void SelectOcrBox(Border box, bool addToSelection)
        {
            if (_isOcrAllSelected)
            {
                _isOcrAllSelected = false;
                ClearSelectedOcrBoxes();
            }

            if (!addToSelection)
            {
                ClearSelectedOcrBoxes();
                _selectedOcrBoxes.Add(box);
                ApplySelectedVisual(box);
                return;
            }

            if (IsOcrBoxSelected(box))
            {
                return;
            }

            _selectedOcrBoxes.Add(box);
            ApplySelectedVisual(box);
        }

        private void ApplySelectedVisual(Border box)
        {
            box.Background = _ocrSelectedBrush;
        }

        private bool IsOcrBoxSelected(Border box)
        {
            return _selectedOcrBoxes.Contains(box);
        }

        private void ClearSelectedOcrBoxes()
        {
            foreach (var selected in _selectedOcrBoxes)
            {
                selected.Background = TransparentBrush;
            }

            _selectedOcrBoxes.Clear();
            _isOcrAllSelected = false;
        }

        private int GetOcrBoxIndex(Border box)
        {
            if (box.Tag is OcrWordInfo info)
            {
                return info.Index;
            }

            return -1;
        }

        private void UpdateOcrRangeSelection(int targetIndex)
        {
            if (_ocrSelectionAnchorIndex < 0 || targetIndex < 0)
            {
                return;
            }

            if (_ocrSelectionCurrentIndex == targetIndex && _selectedOcrBoxes.Count > 0)
            {
                return;
            }

            _ocrSelectionCurrentIndex = targetIndex;
            ClearSelectedOcrBoxes();

            var start = Math.Min(_ocrSelectionAnchorIndex, targetIndex);
            var end = Math.Max(_ocrSelectionAnchorIndex, targetIndex);

            foreach (var box in _ocrBoxes)
            {
                var index = GetOcrBoxIndex(box);
                if (index >= start && index <= end)
                {
                    _selectedOcrBoxes.Add(box);
                    ApplySelectedVisual(box);
                }
            }
        }

        private void ToggleOcrBoxSelection(Border box)
        {
            if (IsOcrBoxSelected(box))
            {
                _selectedOcrBoxes.Remove(box);
                box.Background = TransparentBrush;
                return;
            }

            _selectedOcrBoxes.Add(box);
            ApplySelectedVisual(box);
        }

        private Border? GetOcrBoxAtPoint(Point position)
        {
            for (int i = _ocrBoxes.Count - 1; i >= 0; i--)
            {
                var box = _ocrBoxes[i];
                if (IsPointInsideBox(position, box))
                {
                    return box;
                }
            }

            return null;
        }

        private static bool IsPointInsideBox(Point position, Border box)
        {
            var left = Canvas.GetLeft(box);
            var top = Canvas.GetTop(box);

            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            var right = left + box.Width;
            var bottom = top + box.Height;

            return position.X >= left && position.X <= right &&
                   position.Y >= top && position.Y <= bottom;
        }

        private void OcrCopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedOcrText();
        }
        
        /// <summary>
        /// Copia el texto OCR seleccionado al portapapeles
        /// </summary>
        private void CopySelectedOcrText()
        {
            var text = GetSelectedOcrText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }

        private void OcrSelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectAllOcrText();
        }
        
        /// <summary>
        /// Selecciona todo el texto detectado por OCR
        /// </summary>
        private void SelectAllOcrText()
        {
            SelectAllOcrBoxes();
        }

        private void SelectAllOcrBoxes()
        {
            if (_ocrBoxes.Count == 0)
            {
                return;
            }

            _isOcrAllSelected = true;
            _selectedOcrBoxes.Clear();

            foreach (var box in _ocrBoxes)
            {
                box.Background = _ocrSelectedBrush;
                _selectedOcrBoxes.Add(box);
            }
        }

        private string GetSelectedOcrText()
        {
            if (_ocrResult == null)
            {
                return string.Empty;
            }

            if (_isOcrAllSelected || _selectedOcrBoxes.Count == _ocrBoxes.Count)
            {
                return _ocrResult.Text ?? string.Empty;
            }

            if (_selectedOcrBoxes.Count == 0)
            {
                return string.Empty;
            }

            var ordered = new List<OcrWordInfo>();
            foreach (var box in _selectedOcrBoxes)
            {
                if (box.Tag is OcrWordInfo info)
                {
                    ordered.Add(info);
                }
            }

            ordered.Sort((left, right) => left.Index.CompareTo(right.Index));
            var parts = new List<string>();
            foreach (var info in ordered)
            {
                if (!string.IsNullOrWhiteSpace(info.Text))
                {
                    parts.Add(info.Text);
                }
            }

            return string.Join(" ", parts);
        }

        #endregion

        #region Text Tool

        private void CreateTextAtPosition(Point position)
        {
            if (_annotationManager == null) return;

            // Finalizar edición de texto anterior si existe
            FinishTextEditing();

            var textElement = _annotationManager.CreateTextElement(position);
            
            if (textElement != null)
            {
                // Marcar como nuevo elemento de texto
                _activeTextElement = textElement;
                _isEditingText = true;
                _isNewTextElement = true;
                
                // Iniciar edición inmediatamente para que el usuario pueda escribir
                _annotationManager.StartTextEditing(textElement);
                
                // Seleccionar el elemento de texto para manipulación
                _textManipulation?.SelectText(textElement);
                
                ImageModified?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Finaliza la edición del elemento de texto actual.
        /// </summary>
        private void FinishTextEditing()
        {
            if (_activeTextElement == null || _annotationManager == null)
                return;

            var textElement = _activeTextElement;
            var hasContent = _annotationManager.EndTextEditing(textElement);

            if (!hasContent)
            {
                // Remover elemento de texto vacío
                _annotationManager.RemoveTextElement(textElement);
                _textManipulation?.Deselect();
            }
            else
            {
                if (_isNewTextElement)
                {
                    // Registrar nuevo elemento de texto en historial solo si tiene contenido
                    _historyManager?.RecordElementAdded(textElement);
                }
                
                // Mostrar los handles para el elemento de texto terminado
                _textManipulation?.SelectText(textElement);
            }

            _activeTextElement = null;
            _isEditingText = false;
            _isNewTextElement = false;
        }

        /// <summary>
        /// Inicia la edición de un elemento de texto existente.
        /// </summary>
        private void StartTextEditing(Grid textElement)
        {
            if (_annotationManager == null)
                return;

            // Finalizar edición anterior si existe
            FinishTextEditing();

            _activeTextElement = textElement;
            _isEditingText = true;
            _isNewTextElement = false;
            _annotationManager.StartTextEditing(textElement);
        }

        #endregion

        #region Undo/Redo

        /// <summary>
        /// Deshace la última acción
        /// </summary>
        public void Undo()
        {
            // Guardar referencias antes del Undo
            var previouslySelectedShape = _shapeManipulation?.SelectedShape;
            var previouslySelectedText = _textManipulation?.SelectedText;
            var previouslySelectedEmoji = _emojiManipulation?.SelectedEmoji;
            
            _historyManager?.Undo();
            
            // Si la forma seleccionada fue eliminada del canvas, deseleccionar
            if (previouslySelectedShape != null && !AnnotationsCanvas.Children.Contains(previouslySelectedShape))
            {
                _shapeManipulation?.Deselect();
            }
            if (previouslySelectedText != null && !AnnotationsCanvas.Children.Contains(previouslySelectedText))
            {
                _textManipulation?.Deselect();
            }
            if (previouslySelectedEmoji != null && !AnnotationsCanvas.Children.Contains(previouslySelectedEmoji))
            {
                _emojiManipulation?.Deselect();
            }
        }

        /// <summary>
        /// Rehace la última acción deshecha
        /// </summary>
        public void Redo()
        {
            // Guardar referencias antes del Redo
            var previouslySelectedShape = _shapeManipulation?.SelectedShape;
            var previouslySelectedText = _textManipulation?.SelectedText;
            var previouslySelectedEmoji = _emojiManipulation?.SelectedEmoji;
            
            _historyManager?.Redo();
            
            // Si la forma seleccionada fue eliminada del canvas, deseleccionar
            if (previouslySelectedShape != null && !AnnotationsCanvas.Children.Contains(previouslySelectedShape))
            {
                _shapeManipulation?.Deselect();
            }
            if (previouslySelectedText != null && !AnnotationsCanvas.Children.Contains(previouslySelectedText))
            {
                _textManipulation?.Deselect();
            }
            if (previouslySelectedEmoji != null && !AnnotationsCanvas.Children.Contains(previouslySelectedEmoji))
            {
                _emojiManipulation?.Deselect();
            }
        }

        #endregion

        #region Shape Tool Handlers

        private void FloatingShapesToolbar_ShapeSelected(object? sender, ShapeType shapeType)
        {
            // Convert ShapeType to AnnotationToolType
            var toolType = shapeType switch
            {
                ShapeType.Square => AnnotationToolType.Rectangle,
                ShapeType.Circle => AnnotationToolType.Ellipse,
                ShapeType.Line => AnnotationToolType.Line,
                ShapeType.Arrow => AnnotationToolType.Arrow,
                ShapeType.Star => AnnotationToolType.Star,
                _ => AnnotationToolType.Rectangle
            };
            _annotationManager?.SetActiveTool(toolType);
            // El control ya maneja su propia selección visual
        }

        private void FloatingShapesToolbar_FillButtonClicked(object? sender, EventArgs e)
        {
            if (_toolbarManager == null) return;
            
            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Fill))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
                FloatingShapesToolbarContent.SetFillExpanded(false);
            }
            else
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
                FloatingShapesToolbarContent.SetStyleExpanded(false);
                _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Fill, preserveShapesToolbar: true);
                FloatingShapesToolbarContent.SetFillExpanded(true);
            }
        }

        private void FloatingShapesToolbar_StyleButtonClicked(object? sender, EventArgs e)
        {
            if (_toolbarManager == null) return;
            
            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.Style))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Style);
                FloatingShapesToolbarContent.SetStyleExpanded(false);
            }
            else
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.Fill);
                FloatingShapesToolbarContent.SetFillExpanded(false);
                _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.Style, preserveShapesToolbar: true);
                FloatingShapesToolbarContent.SetStyleExpanded(true);
            }
        }

        #endregion

        #region Pen Tool Handlers

        private void FloatingPenToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetPenColor(color);
        }

        private void FloatingPenToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetPenThickness(thickness);
        }

        #endregion

        #region Highlighter Tool Handlers

        private void FloatingHighlighterToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetHighlighterColor(color);
        }

        private void FloatingHighlighterToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetHighlighterThickness(thickness);
        }

        #endregion

        #region Shape Style Handlers

        private void FloatingStyleToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetShapeStrokeColor(color);
            ApplyShapeSettingsToSelection();
        }

        private void FloatingStyleToolbar_ThicknessChanged(object? sender, double thickness)
        {
            _annotationManager?.SetShapeStrokeThickness(thickness);
            ApplyShapeSettingsToSelection();
        }

        private void FloatingStyleToolbar_OpacityChanged(object? sender, double opacity)
        {
            _annotationManager?.SetShapeStrokeOpacity(opacity / 100.0);
            ApplyShapeSettingsToSelection();
        }

        private void FloatingFillToolbar_ColorChanged(object? sender, Color color)
        {
            _annotationManager?.SetShapeFillColor(color);
            _annotationManager?.SetShapeFillEnabled(true);
            ApplyShapeSettingsToSelection();
        }

        private void FloatingFillToolbar_OpacityChanged(object? sender, double opacity)
        {
            var normalizedOpacity = opacity / 100.0;
            _annotationManager?.SetShapeFillOpacity(normalizedOpacity);
            _annotationManager?.SetShapeFillEnabled(normalizedOpacity > 0);
            ApplyShapeSettingsToSelection();
        }

        #endregion

        private void ApplyShapeSettingsToSelection()
        {
            if (_annotationManager == null)
            {
                return;
            }

            _shapeManipulation?.UpdateSelectedShapeSettings(_annotationManager.ShapeSettings);
        }

        #region Text Tool Handlers

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_annotationManager != null && FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                var fontFamily = item.Content?.ToString() ?? "Segoe UI";
                _annotationManager.TextSettings.FontFamily = fontFamily;
                UpdateTextPreview();
            }
        }

        private void FontSizeComboBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            if (_annotationManager != null && int.TryParse(args.Text, out int size))
            {
                size = Math.Clamp(size, 8, 200);
                _annotationManager.TextSettings.FontSize = size;
                UpdateTextPreview();
                args.Handled = true;
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_annotationManager != null && FontSizeComboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Content?.ToString(), out int size))
                {
                    _annotationManager.TextSettings.FontSize = size;
                    UpdateTextPreview();
                }
            }
        }

        private void BoldToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.IsBold = BoldToggle.IsChecked ?? false;
                UpdateTextPreview();
            }
        }

        private void ItalicToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.IsItalic = ItalicToggle.IsChecked ?? false;
                UpdateTextPreview();
            }
        }

        private void UnderlineToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.IsUnderline = UnderlineToggle.IsChecked ?? false;
                UpdateTextPreview();
            }
        }

        private void StrikethroughToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.IsStrikethrough = StrikethroughToggle.IsChecked ?? false;
                UpdateTextPreview();
            }
        }

        private void TextColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_toolbarManager == null) return;
            
            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.TextColor))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
            }
            else
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
                _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.TextColor);
            }
        }

        private void TextHighlightButton_Click(object sender, RoutedEventArgs e)
        {
            if (_toolbarManager == null) return;
            
            if (_toolbarManager.IsSecondaryToolbarVisible(SecondaryToolbarType.TextHighlight))
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            }
            else
            {
                _toolbarManager.CollapseSecondaryToolbar(SecondaryToolbarType.TextColor);
                _toolbarManager.ShowSecondaryToolbar(SecondaryToolbarType.TextHighlight);
            }
        }

        private void TextColorPalette_ColorSelected(object? sender, Color color)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.TextColor = color;
                TextColorIndicator.Background = BrushCache.GetBrush(color);
                UpdateTextPreview();
            }
        }

        private void TextBackgroundColorPalette_ColorSelected(object? sender, Color color)
        {
            if (_annotationManager != null)
            {
                _annotationManager.TextSettings.HighlightColor = color;
                if (color.A > 0)
                {
                    TextHighlightIndicator.Background = BrushCache.GetBrush(color);
                    TextHighlightIndicator.BorderThickness = new Thickness(0);
                }
                else
                {
                    TextHighlightIndicator.Background = BrushCache.Transparent;
                    TextHighlightIndicator.BorderThickness = new Thickness(1);
                }
                UpdateTextPreview();
            }
        }

        private void UpdateTextPreview()
        {
            if (_annotationManager == null || TextPreviewBlock == null) return;

            var settings = _annotationManager.TextSettings;
            
            TextPreviewBlock.FontFamily = new FontFamily(settings.FontFamily);
            TextPreviewBlock.FontSize = settings.FontSize;
            TextPreviewBlock.FontWeight = settings.IsBold ? FontWeights.Bold : FontWeights.Normal;
            TextPreviewBlock.FontStyle = settings.IsItalic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            TextPreviewBlock.Foreground = BrushCache.GetBrush(settings.TextColor);
            
            // Aplicar subrayado y tachado
            if (settings.IsUnderline || settings.IsStrikethrough)
            {
                var decorations = Windows.UI.Text.TextDecorations.None;
                if (settings.IsUnderline) decorations |= Windows.UI.Text.TextDecorations.Underline;
                if (settings.IsStrikethrough) decorations |= Windows.UI.Text.TextDecorations.Strikethrough;
                TextPreviewBlock.TextDecorations = decorations;
            }
            else
            {
                TextPreviewBlock.TextDecorations = Windows.UI.Text.TextDecorations.None;
            }

            // Aplicar color de fondo/resaltado
            if (settings.HighlightColor.A > 0)
            {
                TextPreviewHighlightBorder.Background = BrushCache.GetBrush(settings.HighlightColor);
            }
            else
            {
                TextPreviewHighlightBorder.Background = BrushCache.Transparent;
            }
        }

        #endregion

        #region Event Handlers

        private void OnStrokeCompleted(object? sender, Path completedPath)
        {
            if (completedPath.Tag is ShapeData)
            {
                _shapeManipulation?.SelectShape(completedPath);
            }

            ImageModified?.Invoke(this, EventArgs.Empty);
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnShapeSelectionChanged(object? sender, Path? selectedShape)
        {
            // Los handles se manejan internamente por ShapeManipulationManager
        }

        private void OnShapeModified(object? sender, Path modifiedShape)
        {
            ImageModified?.Invoke(this, EventArgs.Empty);
        }

        private void OnTextSelectionChanged(object? sender, Grid? selectedText)
        {
            // Los handles se manejan internamente por TextManipulationManager
        }

        private void OnTextModified(object? sender, Grid modifiedText)
        {
            ImageModified?.Invoke(this, EventArgs.Empty);
        }

        private void OnEmojiSelectionChanged(object? sender, Grid? selectedEmoji)
        {
            // Los handles se manejan internamente por EmojiManipulationManager
        }

        private void OnEmojiModified(object? sender, Grid modifiedEmoji)
        {
            ImageModified?.Invoke(this, EventArgs.Empty);
        }

        private void FloatingEmojiToolbar_EmojiSelected(object sender, string emoji)
        {
            PlaceEmoji(emoji);
            // Cerrar el toolbar flotante después de añadir el emoji
            _toolbarManager?.CollapseSecondaryToolbar(SecondaryToolbarType.Emoji);
            NotifyToolbarVisibility(EditorToolType.None);
        }

        /// <summary>
        /// Coloca un emoji en el centro de la imagen
        /// </summary>
        public void PlaceEmoji(string emoji)
        {
            if (_annotationManager == null || !_hasImage) return;

            EnsureManagersInitialized();

            // Calcular la posición central visible
            var scrollableWidth = EditorScrollViewer.ViewportWidth / EditorScrollViewer.ZoomFactor;
            var scrollableHeight = EditorScrollViewer.ViewportHeight / EditorScrollViewer.ZoomFactor;
            var horizontalOffset = EditorScrollViewer.HorizontalOffset / EditorScrollViewer.ZoomFactor;
            var verticalOffset = EditorScrollViewer.VerticalOffset / EditorScrollViewer.ZoomFactor;

            var centerX = horizontalOffset + scrollableWidth / 2;
            var centerY = verticalOffset + scrollableHeight / 2;

            // Crear el emoji en la posición calculada y añadirlo al canvas
            var emojiGrid = _annotationManager.CreateEmojiElement(new Point(centerX, centerY), emoji);
            
            if (emojiGrid != null && _emojiManipulation != null)
            {
                _emojiManipulation.SelectEmoji(emojiGrid);
                _historyManager?.RecordElementAdded(emojiGrid);
                ImageModified?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Context Menu Handlers

        /// <summary>
        /// Handler para el clic derecho en el contenedor de imagen.
        /// Solo muestra el menú de imagen si el OCR no está activo.
        /// </summary>
        private void ImageContainer_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // Si el OCR está activo, no mostrar el menú contextual de imagen
            // El menú de OCR se maneja en OcrBox_RightTapped
            if (OcrOverlayCanvas.Visibility == Visibility.Visible)
            {
                return;
            }

            // Mostrar menú contextual de imagen
            _imageContextFlyout.ShowAt(ImageContainer, e.GetPosition(ImageContainer));
            e.Handled = true;
        }

        /// <summary>
        /// Handler para guardar imagen desde el menú contextual
        /// </summary>
        private void ContextSaveImage_Click(object sender, RoutedEventArgs e)
        {
            SaveImageRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handler para copiar imagen desde el menú contextual
        /// </summary>
        private void ContextCopyImage_Click(object sender, RoutedEventArgs e)
        {
            CopyImageRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Image Export

        /// <summary>
        /// Renderiza la imagen con las anotaciones y devuelve el resultado
        /// </summary>
        public async Task<SoftwareBitmap?> RenderWithAnnotationsAsync()
        {
            if (_currentBitmap == null) return null;

            var handlesVisibility = HandlesCanvas.Visibility;
            var cropVisibility = CropOverlayCanvas.Visibility;

            try
            {
                HandlesCanvas.Visibility = Visibility.Collapsed;
                CropOverlayCanvas.Visibility = Visibility.Collapsed;

                ImageContainer.UpdateLayout();

                var renderTarget = new RenderTargetBitmap();
                await renderTarget.RenderAsync(
                    ImageContainer,
                    _currentBitmap.PixelWidth,
                    _currentBitmap.PixelHeight);

                var pixelBuffer = await renderTarget.GetPixelsAsync();
                var softwareBitmap = new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    renderTarget.PixelWidth,
                    renderTarget.PixelHeight,
                    BitmapAlphaMode.Premultiplied);
                softwareBitmap.CopyFromBuffer(pixelBuffer);

                return softwareBitmap;
            }
            finally
            {
                HandlesCanvas.Visibility = handlesVisibility;
                CropOverlayCanvas.Visibility = cropVisibility;
            }
        }

        #endregion
    }

    /// <summary>
    /// Tipos de herramientas del editor
    /// </summary>
    public enum EditorToolType
    {
        None,
        Shapes,
        Pen,
        Highlighter,
        Text,
        Eraser,
        Emoji
    }
}




