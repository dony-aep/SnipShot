using System.Collections.Generic;
using Windows.Graphics;

namespace SnipShot.Models
{
    /// <summary>
    /// Representa el resultado de una selección en forma libre.
    /// </summary>
    public sealed class FreeFormSelectionResult
    {
        public FreeFormSelectionResult(IReadOnlyList<PointInt32> points, RectInt32 bounds, double rasterizationScale)
        {
            Points = new List<PointInt32>(points).AsReadOnly();
            BoundingRect = bounds;
            RasterizationScale = rasterizationScale;
        }

        /// <summary>
        /// Puntos que definen el contorno en coordenadas de pantalla.
        /// </summary>
        public IReadOnlyList<PointInt32> Points { get; }

        /// <summary>
        /// Rectángulo delimitador en coordenadas de pantalla.
        /// </summary>
        public RectInt32 BoundingRect { get; }

        /// <summary>
        /// Escala de rasterización utilizada al capturar los puntos.
        /// </summary>
        public double RasterizationScale { get; }
    }
}
