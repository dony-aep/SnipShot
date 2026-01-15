using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using System;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Tools
{
    /// <summary>
    /// Herramienta para dibujar flechas.
    /// Crea una línea con punta de flecha en el extremo final.
    /// </summary>
    public class ArrowTool : ShapeTool
    {
        #region Constants

        private const double BaseArrowHeadLength = 15.0;
        private const double ArrowHeadAngle = Math.PI / 6; // 30 grados
        private const double MinArrowHeadLength = 10.0;
        private const double ArrowHeadScaleFactor = 2.5; // Factor de escala relativo al grosor

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override string ToolName => "Arrow";

        /// <inheritdoc/>
        public override bool SupportsFill => false;

        #endregion

        #region Constructor

        public ArrowTool() : base()
        {
        }

        public ArrowTool(AnnotationSettings settings) : base(settings)
        {
        }

        #endregion

        #region Overrides

        /// <inheritdoc/>
        protected override Geometry CreateShapeGeometry(Point startPoint, Point endPoint)
        {
            return CreateArrowGeometry(startPoint, endPoint, Settings.StrokeThickness);
        }

        /// <inheritdoc/>
        protected override void UpdateShapeGeometry(Point startPoint, Point endPoint, bool constrainProportions = false)
        {
            if (_currentPath != null)
            {
                _currentPath.Data = CreateArrowGeometry(startPoint, endPoint, Settings.StrokeThickness);
            }
        }

        /// <inheritdoc/>
        protected override ShapeData CreateShapeData(Point startPoint)
        {
            return new ShapeData
            {
                Type = "Arrow",
                StartPoint = startPoint,
                EndPoint = startPoint
            };
        }

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Creates arrow geometry from start to end point
        /// </summary>
        /// <param name="start">Start point of the arrow</param>
        /// <param name="end">End point of the arrow (where the arrowhead is)</param>
        /// <param name="strokeThickness">Stroke thickness to scale the arrowhead (optional)</param>
        public static PathGeometry CreateArrowGeometry(Point start, Point end, double strokeThickness = 2.0)
        {
            var pathGeometry = new PathGeometry();

            // Calcular el tamaño de la punta proporcional al grosor
            double arrowHeadLength = Math.Max(MinArrowHeadLength, BaseArrowHeadLength + (strokeThickness * ArrowHeadScaleFactor));

            // Calcular el ángulo de la línea
            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);

            // Calcular los puntos de la punta de flecha
            double arrowX1 = end.X - arrowHeadLength * Math.Cos(angle - ArrowHeadAngle);
            double arrowY1 = end.Y - arrowHeadLength * Math.Sin(angle - ArrowHeadAngle);
            double arrowX2 = end.X - arrowHeadLength * Math.Cos(angle + ArrowHeadAngle);
            double arrowY2 = end.Y - arrowHeadLength * Math.Sin(angle + ArrowHeadAngle);

            // Línea principal
            var lineFigure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false
            };
            lineFigure.Segments.Add(new LineSegment { Point = end });
            pathGeometry.Figures.Add(lineFigure);

            // Punta de flecha (lado 1)
            var arrowFigure1 = new PathFigure
            {
                StartPoint = end,
                IsClosed = false
            };
            arrowFigure1.Segments.Add(new LineSegment { Point = new Point(arrowX1, arrowY1) });
            pathGeometry.Figures.Add(arrowFigure1);

            // Punta de flecha (lado 2)
            var arrowFigure2 = new PathFigure
            {
                StartPoint = end,
                IsClosed = false
            };
            arrowFigure2.Segments.Add(new LineSegment { Point = new Point(arrowX2, arrowY2) });
            pathGeometry.Figures.Add(arrowFigure2);

            return pathGeometry;
        }

        #endregion
    }
}
