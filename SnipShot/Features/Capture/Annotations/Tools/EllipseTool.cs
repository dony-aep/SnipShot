using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using System;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para dibujar elipses y círculos.
    /// Soporta relleno opcional.
    /// </summary>
    public class EllipseTool : ShapeTool
    {
        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Circle";

        /// <inheritdoc/>
        public override bool SupportsFill => true;
        
        /// <inheritdoc/>
        public override bool SupportsConstrainedProportions => true;

        #endregion

        #region Constructor

        public EllipseTool() : base()
        {
        }

        public EllipseTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override Geometry CreateShapeGeometry(Point startPoint, Point endPoint)
        {
            return CreateEllipseGeometry(startPoint, endPoint);
        }

        /// <inheritdoc/>
        protected override void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false)
        {
            if (_currentPath != null)
            {
                _currentPath.Data = CreateEllipseGeometry(startPoint, endPoint, constrainProportions);
            }
        }

        /// <inheritdoc/>
        protected override bool ValidateStroke()
        {
            double width = Math.Abs(_endPoint.X - _startPoint.X);
            double height = Math.Abs(_endPoint.Y - _startPoint.Y);
            return width >= MinShapeSize && height >= MinShapeSize;
        }

        /// <inheritdoc/>
        protected override ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = "Circle",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Creates ellipse geometry from start to end point
        /// </summary>
        /// <param name="start">Start point</param>
        /// <param name="end">End point</param>
        /// <param name="constrainToCircle">If true, creates a perfect circle</param>
        public static EllipseGeometry CreateEllipseGeometry(Point start, Point end, bool constrainToCircle = false)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);

            if (constrainToCircle)
            {
                double size = Math.Min(width, height);
                width = size;
                height = size;

                // Ajustar la posición para mantener el punto de inicio
                if (end.X < start.X)
                    left = start.X - size;
                else
                    left = start.X;

                if (end.Y < start.Y)
                    top = start.Y - size;
                else
                    top = start.Y;
            }

            return new EllipseGeometry
            {
                Center = new Point(left + width / 2, top + height / 2),
                RadiusX = width / 2,
                RadiusY = height / 2
            };
        }

        #endregion
    }
}
