using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para dibujar líneas rectas.
    /// </summary>
    public class LineTool : ShapeTool
    {
        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Line";

        /// <inheritdoc/>
        public override bool SupportsFill => false;

        #endregion

        #region Constructor

        public LineTool() : base()
        {
        }

        public LineTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override Geometry CreateShapeGeometry(Point startPoint, Point endPoint)
        {
            return CreateLineGeometry(startPoint, endPoint);
        }

        /// <inheritdoc/>
        protected override void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false)
        {
            if (_currentPath != null)
            {
                _currentPath.Data = CreateLineGeometry(startPoint, endPoint);
            }
        }

        /// <inheritdoc/>
        protected override ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = "Line",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Creates line geometry from start to end point
        /// </summary>
        public static LineGeometry CreateLineGeometry(Point start, Point end)
        {
            return new LineGeometry
            {
                StartPoint = start,
                EndPoint = end
            };
        }

        #endregion
    }
}
