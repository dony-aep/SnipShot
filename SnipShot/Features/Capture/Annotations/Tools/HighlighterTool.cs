using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using SnipShot.Features.Capture.Annotations.Base;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta de resaltador para dibujo libre con trazo semi-transparente.
    /// Similar al bolígrafo pero con mayor grosor y transparencia.
    /// </summary>
    public class HighlighterTool : AnnotationToolBase
    {
        #region Fields

        private PathFigure? _pathFigure;
        private PolyLineSegment? _polyLineSegment;
        private PathGeometry? _pathGeometry;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Highlighter";

        #endregion

        #region Constructor

        public HighlighterTool() : base(AnnotationSettings.DefaultHighlighter)
        {
        }

        public HighlighterTool(AnnotationSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Crea un resaltador con color específico
        /// </summary>
        public HighlighterTool(Color color, double thickness = 16.0) : base()
        {
            _settings = new AnnotationSettings
            {
                StrokeColor = color,
                StrokeOpacity = 0.5, // Semi-transparente por defecto
                StrokeThickness = thickness,
                FillEnabled = false
            };
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override Path StartStroke(Point startPoint)
        {
            _startPoint = startPoint;
            _isDrawing = true;

            // Crear la geometría del path
            _polyLineSegment = new PolyLineSegment();
            _pathFigure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false,
                Segments = { _polyLineSegment }
            };

            _pathGeometry = new PathGeometry();
            _pathGeometry.Figures.Add(_pathFigure);

            _currentPath = new Path
            {
                Stroke = CreateStrokeBrush(),
                StrokeThickness = _settings.StrokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = _pathGeometry,
                Tag = CreateShapeData(startPoint)
            };

            return _currentPath;
        }

        /// <inheritdoc/>
        public override void ContinueStroke(Point currentPoint)
        {
            if (!_isDrawing || _polyLineSegment == null)
            {
                return;
            }

            _polyLineSegment.Points.Add(currentPoint);
            UpdateShapeData(currentPoint);
        }

        /// <inheritdoc/>
        public override Path? EndStroke()
        {
            if (!_isDrawing || _currentPath == null)
            {
                return null;
            }

            _isDrawing = false;

            if (!ValidateStroke())
            {
                CancelStroke();
                return null;
            }

            var completedPath = _currentPath;
            
            // Limpiar referencias
            _currentPath = null;
            _pathFigure = null;
            _polyLineSegment = null;
            _pathGeometry = null;

            return completedPath;
        }

        /// <inheritdoc/>
        public override void CancelStroke()
        {
            base.CancelStroke();
            _pathFigure = null;
            _polyLineSegment = null;
            _pathGeometry = null;
        }

        /// <inheritdoc/>
        protected override Geometry CreateGeometry(Point startPoint)
        {
            // La geometría ya se crea en StartStroke
            return _pathGeometry!;
        }

        /// <inheritdoc/>
        protected override void UpdateGeometry(Point currentPoint)
        {
            // La actualización se hace en ContinueStroke
            _polyLineSegment?.Points.Add(currentPoint);
        }

        /// <inheritdoc/>
        protected override bool ValidateStroke()
        {
            // El trazo es válido si tiene al menos 2 puntos
            return _polyLineSegment != null && _polyLineSegment.Points.Count >= 1;
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc/>
        protected override SolidColorBrush CreateStrokeBrush()
        {
            // El resaltador siempre usa semi-transparencia
            var color = Color.FromArgb(
                (byte)(_settings.StrokeOpacity * 255),
                _settings.StrokeColor.R,
                _settings.StrokeColor.G,
                _settings.StrokeColor.B);

            return BrushCache.GetBrush(color);
        }

        /// <inheritdoc/>
        protected override ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = "Highlighter",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion
    }
}
