using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using SnipShot.Features.Capture.Annotations.Models;
using SnipShot.Helpers.Utils;

namespace SnipShot.Features.Capture.Annotations.Base
{
    /// <summary>
    /// Clase base abstracta para herramientas de anotación.
    /// Proporciona implementación común para todas las herramientas de dibujo.
    /// </summary>
    public abstract class AnnotationToolBase : IAnnotationTool
    {
        #region Fields

        protected Path? _currentPath;
        protected Point _startPoint;
        protected bool _isDrawing;
        protected bool _isActive;
        protected AnnotationSettings _settings;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public abstract string ToolName { get; }

        /// <inheritdoc/>
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        /// <inheritdoc/>
        public bool IsDrawing => _isDrawing;

        /// <inheritdoc/>
        public AnnotationSettings Settings
        {
            get => _settings;
            set => _settings = value ?? AnnotationSettings.DefaultShape;
        }

        /// <inheritdoc/>
        public Path? CurrentPath => _currentPath;

        #endregion

        #region Constructor

        protected AnnotationToolBase()
        {
            _settings = AnnotationSettings.DefaultShape;
        }

        protected AnnotationToolBase(AnnotationSettings settings)
        {
            _settings = settings ?? AnnotationSettings.DefaultShape;
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Crea la geometría específica de la herramienta
        /// </summary>
        protected abstract Geometry CreateGeometry(Point startPoint);

        /// <summary>
        /// Actualiza la geometría con el punto actual
        /// </summary>
        protected abstract void UpdateGeometry(Point currentPoint);

        /// <summary>
        /// Valida si el trazo es válido para ser guardado
        /// </summary>
        protected abstract bool ValidateStroke();

        #endregion

        #region IAnnotationTool Implementation

        /// <inheritdoc/>
        public virtual Path StartStroke(Point startPoint)
        {
            _startPoint = startPoint;
            _isDrawing = true;

            _currentPath = new Path
            {
                Stroke = CreateStrokeBrush(),
                StrokeThickness = _settings.StrokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = CreateGeometry(startPoint),
                Tag = CreateShapeData(startPoint)
            };

            return _currentPath;
        }

        /// <inheritdoc/>
        public virtual void ContinueStroke(Point currentPoint)
        {
            if (!_isDrawing || _currentPath == null)
            {
                return;
            }

            UpdateGeometry(currentPoint);
            UpdateShapeData(currentPoint);
        }

        /// <inheritdoc/>
        public virtual Path? EndStroke()
        {
            if (!_isDrawing || _currentPath == null)
            {
                return null;
            }

            _isDrawing = false;

            if (!ValidateStroke())
            {
                var invalidPath = _currentPath;
                _currentPath = null;
                return null;
            }

            var completedPath = _currentPath;
            _currentPath = null;
            return completedPath;
        }

        /// <inheritdoc/>
        public virtual void CancelStroke()
        {
            _isDrawing = false;
            _currentPath = null;
        }

        /// <inheritdoc/>
        public virtual bool IsPointValid(Point point, Rect bounds)
        {
            return bounds.Contains(point);
        }

        /// <inheritdoc/>
        public virtual Point ClampPoint(Point point, Rect bounds)
        {
            double x = point.X;
            double y = point.Y;

            if (x < bounds.Left) x = bounds.Left;
            if (x > bounds.Right) x = bounds.Right;
            if (y < bounds.Top) y = bounds.Top;
            if (y > bounds.Bottom) y = bounds.Bottom;

            return new Point(x, y);
        }

        /// <inheritdoc/>
        public virtual void Activate()
        {
            _isActive = true;
        }

        /// <inheritdoc/>
        public virtual void Deactivate()
        {
            _isActive = false;
            CancelStroke();
        }

        /// <inheritdoc/>
        public virtual void Reset()
        {
            CancelStroke();
            _settings = AnnotationSettings.DefaultShape;
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Crea el pincel para el trazo basado en la configuración actual
        /// </summary>
        protected virtual SolidColorBrush CreateStrokeBrush()
        {
            return BrushCache.GetBrush(_settings.GetEffectiveStrokeColor());
        }

        /// <summary>
        /// Crea el pincel para el relleno basado en la configuración actual
        /// </summary>
        protected virtual SolidColorBrush? CreateFillBrush()
        {
            if (!_settings.FillEnabled || _settings.FillOpacity <= 0)
            {
                return null;
            }

            return BrushCache.GetBrush(_settings.GetEffectiveFillColor());
        }

        /// <summary>
        /// Crea los datos de la forma para el Path.Tag
        /// </summary>
        protected virtual ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = ToolName,
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        /// <summary>
        /// Actualiza los datos de la forma con el punto actual
        /// </summary>
        protected virtual void UpdateShapeData(Point currentPoint)
        {
            if (_currentPath?.Tag is ShapeData data)
            {
                data.EndPoint = currentPoint;
            }
        }

        /// <summary>
        /// Calcula la distancia entre dos puntos
        /// </summary>
        protected static double GetDistance(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        #endregion
    }
}
