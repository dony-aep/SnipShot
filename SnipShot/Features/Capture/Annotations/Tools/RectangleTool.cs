using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using System;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para dibujar rectángulos.
    /// Soporta relleno opcional.
    /// </summary>
    public class RectangleTool : ShapeTool
    {
        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Square";

        /// <inheritdoc/>
        public override bool SupportsFill => true;
        
        /// <inheritdoc/>
        public override bool SupportsConstrainedProportions => true;

        #endregion

        #region Constructor

        public RectangleTool() : base()
        {
        }

        public RectangleTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override Geometry CreateShapeGeometry(Point startPoint, Point endPoint)
        {
            return CreateRectangleGeometry(startPoint, endPoint);
        }

        /// <inheritdoc/>
        protected override void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false)
        {
            if (_currentPath != null)
            {
                _currentPath.Data = CreateRectangleGeometry(startPoint, endPoint, constrainProportions);
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
                Type = "Square",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Creates rectangle geometry from start to end point
        /// </summary>
        /// <param name="start">Start point</param>
        /// <param name="end">End point</param>
        /// <param name="constrainToSquare">If true, creates a perfect square</param>
        public static RectangleGeometry CreateRectangleGeometry(Point start, Point end, bool constrainToSquare = false)
        {
            Point adjustedEnd = end;

            if (constrainToSquare)
            {
                double width = Math.Abs(end.X - start.X);
                double height = Math.Abs(end.Y - start.Y);
                double size = Math.Min(width, height);

                adjustedEnd = new Point(
                    start.X + (end.X >= start.X ? size : -size),
                    start.Y + (end.Y >= start.Y ? size : -size)
                );
            }

            double x = Math.Min(start.X, adjustedEnd.X);
            double y = Math.Min(start.Y, adjustedEnd.Y);
            double rectWidth = Math.Abs(adjustedEnd.X - start.X);
            double rectHeight = Math.Abs(adjustedEnd.Y - start.Y);

            return new RectangleGeometry
            {
                Rect = new Rect(x, y, rectWidth, rectHeight)
            };
        }

        #endregion
    }
}
