using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using System;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para dibujar estrellas de 5 puntas.
    /// Soporta relleno opcional.
    /// </summary>
    public class StarTool : ShapeTool
    {
        #region Constants

        private const int StarPoints = 5;
        private const double InnerRadiusRatio = 0.4; // Radio interior como proporción del exterior

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Star";

        /// <inheritdoc/>
        public override bool SupportsFill => true;

        /// <inheritdoc/>
        public override bool SupportsConstrainedProportions => true;

        #endregion

        #region Constructor

        public StarTool() : base()
        {
        }

        public StarTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override Geometry CreateShapeGeometry(Point startPoint, Point endPoint)
        {
            return CreateStarGeometry(startPoint, endPoint);
        }

        /// <inheritdoc/>
        protected override void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false)
        {
            if (_currentPath != null)
            {
                _currentPath.Data = CreateStarGeometry(startPoint, endPoint, constrainProportions);
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
                Type = "Star",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Creates a 5-pointed star geometry from start to end point
        /// </summary>
        /// <param name="start">Start point (top-left corner of bounding box)</param>
        /// <param name="end">End point (bottom-right corner of bounding box)</param>
        /// <param name="constrainToRegular">If true, creates a regular star (equal width/height)</param>
        public static PathGeometry CreateStarGeometry(Point start, Point end, bool constrainToRegular = false)
        {
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);
            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);

            if (constrainToRegular)
            {
                double size = Math.Min(width, height);
                width = size;
                height = size;

                // Ajustar la posición para mantener el punto de inicio
                if (end.X < start.X)
                    left = start.X - size;
                if (end.Y < start.Y)
                    top = start.Y - size;
            }

            // Centro de la estrella
            double centerX = left + width / 2;
            double centerY = top + height / 2;

            // Radios exterior e interior
            double outerRadiusX = width / 2;
            double outerRadiusY = height / 2;
            double innerRadiusX = outerRadiusX * InnerRadiusRatio;
            double innerRadiusY = outerRadiusY * InnerRadiusRatio;

            // Crear los puntos de la estrella
            var points = new Point[StarPoints * 2];
            double angleOffset = -Math.PI / 2; // Empezar desde arriba

            for (int i = 0; i < StarPoints * 2; i++)
            {
                double angle = angleOffset + (i * Math.PI / StarPoints);
                bool isOuter = i % 2 == 0;

                double radiusX = isOuter ? outerRadiusX : innerRadiusX;
                double radiusY = isOuter ? outerRadiusY : innerRadiusY;

                points[i] = new Point(
                    centerX + radiusX * Math.Cos(angle),
                    centerY + radiusY * Math.Sin(angle)
                );
            }

            // Crear el PathGeometry
            var pathGeometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = true,
                IsFilled = true
            };

            for (int i = 1; i < points.Length; i++)
            {
                figure.Segments.Add(new LineSegment { Point = points[i] });
            }

            pathGeometry.Figures.Add(figure);

            return pathGeometry;
        }

        #endregion
    }
}
