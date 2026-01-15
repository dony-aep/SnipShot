using System;
using Windows.Foundation;
using Windows.Graphics;

namespace SnipShot.Helpers.Utils
{
    /// <summary>
    /// Helper para convertir coordenadas entre UI y píxeles de pantalla
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Convierte dos puntos de UI a un rectángulo de pantalla en píxeles
        /// </summary>
        /// <param name="startPoint">Punto de inicio en coordenadas UI</param>
        /// <param name="endPoint">Punto final en coordenadas UI</param>
        /// <param name="virtualBounds">Límites del escritorio virtual</param>
        /// <param name="rasterizationScale">Escala de rasterización (DPI)</param>
        /// <returns>RectInt32 en coordenadas de pantalla</returns>
        public static RectInt32 ConvertToScreenRect(
            Point startPoint,
            Point endPoint,
            RectInt32 virtualBounds,
            double rasterizationScale)
        {
            // Normalizar a esquina superior izquierda y dimensiones positivas
            double x = Math.Min(startPoint.X, endPoint.X);
            double y = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            // Convertir a píxeles de pantalla
            int pixelLeft = virtualBounds.X + (int)Math.Round(x * rasterizationScale);
            int pixelTop = virtualBounds.Y + (int)Math.Round(y * rasterizationScale);
            int pixelWidth = (int)Math.Round(width * rasterizationScale);
            int pixelHeight = (int)Math.Round(height * rasterizationScale);

            return new RectInt32(pixelLeft, pixelTop, pixelWidth, pixelHeight);
        }

        /// <summary>
        /// Obtiene un rectángulo normalizado (esquina superior izquierda + dimensiones)
        /// </summary>
        /// <param name="startPoint">Punto de inicio</param>
        /// <param name="endPoint">Punto final</param>
        /// <returns>Rect normalizado</returns>
        public static Rect GetNormalizedRect(Point startPoint, Point endPoint)
        {
            double x = Math.Min(startPoint.X, endPoint.X);
            double y = Math.Min(startPoint.Y, endPoint.Y);
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// Convierte un punto en coordenadas de UI a un punto en píxeles de pantalla.
        /// </summary>
        public static PointInt32 ConvertToScreenPoint(
            Point uiPoint,
            RectInt32 virtualBounds,
            double rasterizationScale)
        {
            int pixelX = virtualBounds.X + (int)Math.Round(uiPoint.X * rasterizationScale);
            int pixelY = virtualBounds.Y + (int)Math.Round(uiPoint.Y * rasterizationScale);
            return new PointInt32(pixelX, pixelY);
        }

        /// <summary>
        /// Convierte un rectángulo de pantalla en píxeles a coordenadas UI relativas al overlay.
        /// </summary>
        public static Rect ConvertToUiRect(
            RectInt32 screenRect,
            RectInt32 virtualBounds,
            double rasterizationScale)
        {
            double x = (screenRect.X - virtualBounds.X) / rasterizationScale;
            double y = (screenRect.Y - virtualBounds.Y) / rasterizationScale;
            double width = screenRect.Width / rasterizationScale;
            double height = screenRect.Height / rasterizationScale;

            return new Rect(x, y, width, height);
        }
    }
}
