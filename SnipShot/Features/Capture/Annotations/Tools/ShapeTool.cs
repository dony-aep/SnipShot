using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using SnipShot.Features.Capture.Annotations.Base;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Clase base abstracta para herramientas de formas geométricas.
    /// Proporciona funcionalidad común para flechas, líneas, rectángulos y elipses.
    /// </summary>
    public abstract class ShapeTool : AnnotationToolBase
    {
        #region Fields

        protected Point _endPoint;
        protected bool _constrainProportions;
        protected const double MinShapeSize = 12.0;

        #endregion

        #region Properties

        /// <summary>
        /// Indica si la forma soporta relleno
        /// </summary>
        public abstract bool SupportsFill { get; }
        
        /// <summary>
        /// Indica si la forma soporta constraint de proporciones (Shift para cuadrado/círculo perfecto)
        /// </summary>
        public virtual bool SupportsConstrainedProportions => false;
        
        /// <summary>
        /// Obtiene o establece si se deben mantener proporciones iguales (cuadrado/círculo)
        /// </summary>
        public bool ConstrainProportions
        {
            get => _constrainProportions;
            set => _constrainProportions = value;
        }

        /// <summary>
        /// Punto final de la forma
        /// </summary>
        public Point EndPoint => _endPoint;

        #endregion

        #region Constructor

        protected ShapeTool() : base(AnnotationSettings.DefaultShape)
        {
        }

        protected ShapeTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Crea la geometría específica de la forma
        /// </summary>
        protected abstract Geometry CreateShapeGeometry(Point startPoint, Point endPoint);

        /// <summary>
        /// Actualiza la geometría de la forma con los nuevos puntos
        /// </summary>
        /// <param name="startPoint">Punto de inicio</param>
        /// <param name="endPoint">Punto final</param>
        /// <param name="constrainProportions">Si es true, mantiene proporciones iguales</param>
        protected abstract void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false);

        #endregion

        #region Overrides

        /// <inheritdoc/>
        public override Path StartStroke(Point startPoint)
        {
            _startPoint = startPoint;
            _endPoint = startPoint;
            _isDrawing = true;

            _currentPath = new Path
            {
                Stroke = CreateStrokeBrush(),
                StrokeThickness = _settings.StrokeThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = CreateShapeGeometry(startPoint, startPoint),
                Tag = CreateShapeData(startPoint)
            };

            // Aplicar relleno si está habilitado y la forma lo soporta
            if (SupportsFill && _settings.FillEnabled)
            {
                _currentPath.Fill = CreateFillBrush();
            }

            return _currentPath;
        }

        /// <inheritdoc/>
        public override void ContinueStroke(Point currentPoint)
        {
            if (!_isDrawing || _currentPath == null)
            {
                return;
            }

            _endPoint = currentPoint;
            UpdateShapeGeometry(_startPoint, currentPoint, _constrainProportions);
            UpdateShapeData(currentPoint);
        }

        /// <inheritdoc/>
        protected override Geometry CreateGeometry(Point startPoint)
        {
            return CreateShapeGeometry(startPoint, startPoint);
        }

        /// <inheritdoc/>
        protected override void UpdateGeometry(Point currentPoint)
        {
            UpdateShapeGeometry(_startPoint, currentPoint, _constrainProportions);
        }

        /// <inheritdoc/>
        protected override bool ValidateStroke()
        {
            // La forma es válida si tiene un tamaño mínimo
            double distance = GetDistance(_startPoint, _endPoint);
            return distance >= MinShapeSize;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Actualiza la geometría de una forma existente
        /// </summary>
        public void UpdateExistingShape(Path path, Point startPoint, Point endPoint)
        {
            if (path == null) return;

            _startPoint = startPoint;
            _endPoint = endPoint;
            _currentPath = path;

            path.Data = CreateShapeGeometry(startPoint, endPoint);

            if (path.Tag is ShapeData data)
            {
                data.StartPoint = startPoint;
                data.EndPoint = endPoint;
            }
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Calcula el rectángulo delimitador entre dos puntos
        /// </summary>
        protected static Rect GetBoundingRect(Point p1, Point p2)
        {
            double minX = System.Math.Min(p1.X, p2.X);
            double minY = System.Math.Min(p1.Y, p2.Y);
            double maxX = System.Math.Max(p1.X, p2.X);
            double maxY = System.Math.Max(p1.Y, p2.Y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Calcula el ángulo entre dos puntos en radianes
        /// </summary>
        protected static double GetAngle(Point from, Point to)
        {
            return System.Math.Atan2(to.Y - from.Y, to.X - from.X);
        }

        #endregion
    }
}
