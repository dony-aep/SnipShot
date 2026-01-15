using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using SnipShot.Models;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Helper para gestionar los handles de redimensionamiento
    /// </summary>
    public static class ResizeHandleManager
    {
        /// <summary>
        /// Estructura que contiene todos los handles
        /// </summary>
        public class HandleSet
        {
            public FrameworkElement HandleNW { get; set; } = null!;
            public FrameworkElement HandleNE { get; set; } = null!;
            public FrameworkElement HandleSE { get; set; } = null!;
            public FrameworkElement HandleSW { get; set; } = null!;
            public FrameworkElement HandleN { get; set; } = null!;
            public FrameworkElement HandleE { get; set; } = null!;
            public FrameworkElement HandleS { get; set; } = null!;
            public FrameworkElement HandleW { get; set; } = null!;
        }

        /// <summary>
        /// Muestra y posiciona todos los handles alrededor de la selección
        /// </summary>
        public static void ShowHandles(HandleSet handles, Rect selection)
        {
            // Grosor del brazo de los handles L (para calcular offset de esquinas)
            const double cornerBranchThickness = 4;
            
            // Posicionar handles de esquinas (forma L)
            // La esquina interior del L debe coincidir con la esquina de la selección
            // NW: esquina superior izquierda - L abre hacia arriba-izquierda
            PositionHandle(handles.HandleNW, selection.Left - cornerBranchThickness, selection.Top - cornerBranchThickness);
            // NE: esquina superior derecha - L abre hacia arriba-derecha
            PositionHandle(handles.HandleNE, selection.Right - handles.HandleNE.Width + cornerBranchThickness, selection.Top - cornerBranchThickness);
            // SE: esquina inferior derecha - L abre hacia abajo-derecha
            PositionHandle(handles.HandleSE, selection.Right - handles.HandleSE.Width + cornerBranchThickness, selection.Bottom - handles.HandleSE.Height + cornerBranchThickness);
            // SW: esquina inferior izquierda - L abre hacia abajo-izquierda
            PositionHandle(handles.HandleSW, selection.Left - cornerBranchThickness, selection.Bottom - handles.HandleSW.Height + cornerBranchThickness);

            // Posicionar handles de bordes (píldoras)
            // N: centro del borde superior
            PositionHandle(handles.HandleN, selection.Left + selection.Width / 2 - handles.HandleN.Width / 2, selection.Top - handles.HandleN.Height / 2);
            // S: centro del borde inferior
            PositionHandle(handles.HandleS, selection.Left + selection.Width / 2 - handles.HandleS.Width / 2, selection.Bottom - handles.HandleS.Height / 2);
            // E: centro del borde derecho
            PositionHandle(handles.HandleE, selection.Right - handles.HandleE.Width / 2, selection.Top + selection.Height / 2 - handles.HandleE.Height / 2);
            // W: centro del borde izquierdo
            PositionHandle(handles.HandleW, selection.Left - handles.HandleW.Width / 2, selection.Top + selection.Height / 2 - handles.HandleW.Height / 2);

            // Show all handles
            handles.HandleNW.Visibility = Visibility.Visible;
            handles.HandleNE.Visibility = Visibility.Visible;
            handles.HandleSE.Visibility = Visibility.Visible;
            handles.HandleSW.Visibility = Visibility.Visible;
            handles.HandleN.Visibility = Visibility.Visible;
            handles.HandleE.Visibility = Visibility.Visible;
            handles.HandleS.Visibility = Visibility.Visible;
            handles.HandleW.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Oculta todos los handles
        /// </summary>
        public static void HideHandles(HandleSet handles)
        {
            handles.HandleNW.Visibility = Visibility.Collapsed;
            handles.HandleNE.Visibility = Visibility.Collapsed;
            handles.HandleSE.Visibility = Visibility.Collapsed;
            handles.HandleSW.Visibility = Visibility.Collapsed;
            handles.HandleN.Visibility = Visibility.Collapsed;
            handles.HandleE.Visibility = Visibility.Collapsed;
            handles.HandleS.Visibility = Visibility.Collapsed;
            handles.HandleW.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Posiciona un handle en las coordenadas especificadas
        /// </summary>
        private static void PositionHandle(FrameworkElement handle, double left, double top)
        {
            Canvas.SetLeft(handle, left);
            Canvas.SetTop(handle, top);
        }

        /// <summary>
        /// Calcula los nuevos límites de la selección basándose en el arrastre de un handle
        /// </summary>
        public static Rect CalculateNewBounds(
            string handleTag,
            Rect originalBounds,
            double deltaX,
            double deltaY)
        {
            double origLeft = originalBounds.X;
            double origTop = originalBounds.Y;
            double origRight = originalBounds.X + originalBounds.Width;
            double origBottom = originalBounds.Y + originalBounds.Height;

            double newLeft = origLeft;
            double newTop = origTop;
            double newRight = origRight;
            double newBottom = origBottom;

            switch (handleTag)
            {
                case "NW": // Top-left corner
                    newLeft = origLeft + deltaX;
                    newTop = origTop + deltaY;
                    // Clamp to minimum size
                    newLeft = Math.Min(newLeft, origRight - Constants.MIN_SELECTION_SIZE);
                    newTop = Math.Min(newTop, origBottom - Constants.MIN_SELECTION_SIZE);
                    break;

                case "NE": // Top-right corner
                    newRight = origRight + deltaX;
                    newTop = origTop + deltaY;
                    // Clamp to minimum size
                    newRight = Math.Max(newRight, origLeft + Constants.MIN_SELECTION_SIZE);
                    newTop = Math.Min(newTop, origBottom - Constants.MIN_SELECTION_SIZE);
                    break;

                case "SE": // Bottom-right corner
                    newRight = origRight + deltaX;
                    newBottom = origBottom + deltaY;
                    // Clamp to minimum size
                    newRight = Math.Max(newRight, origLeft + Constants.MIN_SELECTION_SIZE);
                    newBottom = Math.Max(newBottom, origTop + Constants.MIN_SELECTION_SIZE);
                    break;

                case "SW": // Bottom-left corner
                    newLeft = origLeft + deltaX;
                    newBottom = origBottom + deltaY;
                    // Clamp to minimum size
                    newLeft = Math.Min(newLeft, origRight - Constants.MIN_SELECTION_SIZE);
                    newBottom = Math.Max(newBottom, origTop + Constants.MIN_SELECTION_SIZE);
                    break;

                case "N": // Top edge
                    newTop = origTop + deltaY;
                    // Clamp to minimum size
                    newTop = Math.Min(newTop, origBottom - Constants.MIN_SELECTION_SIZE);
                    break;

                case "E": // Right edge
                    newRight = origRight + deltaX;
                    // Clamp to minimum size
                    newRight = Math.Max(newRight, origLeft + Constants.MIN_SELECTION_SIZE);
                    break;

                case "S": // Bottom edge
                    newBottom = origBottom + deltaY;
                    // Clamp to minimum size
                    newBottom = Math.Max(newBottom, origTop + Constants.MIN_SELECTION_SIZE);
                    break;

                case "W": // Left edge
                    newLeft = origLeft + deltaX;
                    // Clamp to minimum size
                    newLeft = Math.Min(newLeft, origRight - Constants.MIN_SELECTION_SIZE);
                    break;
            }

            return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
        }
    }
}
