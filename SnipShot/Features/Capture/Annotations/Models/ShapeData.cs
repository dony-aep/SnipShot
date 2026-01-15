using Windows.Foundation;

namespace SnipShot.Features.Capture.Annotations.Models
{
    /// <summary>
    /// Representa los datos de una forma dibujada en el canvas de anotaciones.
    /// </summary>
    public class ShapeData
    {
        /// <summary>
        /// Tipo de forma: "Arrow", "Line", "Square", "Circle", "Star", "Pen", "Highlighter"
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Punto de inicio de la forma
        /// </summary>
        public Point StartPoint { get; set; }

        /// <summary>
        /// Punto final de la forma
        /// </summary>
        public Point EndPoint { get; set; }

        /// <summary>
        /// Indica si la forma está seleccionada actualmente
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Ángulo de rotación de la forma en grados
        /// </summary>
        public double RotationAngle { get; set; }

        /// <summary>
        /// Crea una copia de los datos de la forma
        /// </summary>
        public ShapeData Clone()
        {
            return new ShapeData
            {
                Type = Type,
                StartPoint = StartPoint,
                EndPoint = EndPoint,
                IsSelected = IsSelected,
                RotationAngle = RotationAngle
            };
        }
    }
}
