using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using SnipShot.Features.Capture.Annotations.Base;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta de bolígrafo para dibujo libre.
    /// Crea trazos suaves siguiendo el movimiento del puntero.
    /// </summary>
    public class PenTool : AnnotationToolBase
    {
        #region Fields

        private PathFigure? _pathFigure;
        private PolyLineSegment? _polyLineSegment;
        private PathGeometry? _pathGeometry;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Pen";

        #endregion

        #region Constructor

        public PenTool() : base(AnnotationSettings.DefaultPen)
        {
        }

        public PenTool(AnnotationSettings settings) : base(settings)
        {
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
        protected override ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = "Pen",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion
    }
}
