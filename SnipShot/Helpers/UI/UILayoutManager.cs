using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using SnipShot.Models;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Helper para gestionar el layout y posicionamiento de elementos UI
    /// </summary>
    public static class UILayoutManager
    {
        /// <summary>
        /// Actualiza las posiciones y dimensiones de los rectángulos de sombra
        /// </summary>
        public static void UpdateShadeRectangles(
            Rectangle topShade,
            Rectangle bottomShade,
            Rectangle leftShade,
            Rectangle rightShade,
            double totalWidth,
            double totalHeight,
            double selectionX,
            double selectionY,
            double selectionWidth,
            double selectionHeight)
        {
            if (totalWidth <= 0 || totalHeight <= 0)
            {
                return;
            }

            // Top shade
            topShade.Width = totalWidth;
            topShade.Height = Math.Max(0, selectionY);
            Canvas.SetLeft(topShade, 0);
            Canvas.SetTop(topShade, 0);

            // Bottom shade
            double bottomHeight = Math.Max(0, totalHeight - (selectionY + selectionHeight));
            bottomShade.Width = totalWidth;
            bottomShade.Height = bottomHeight;
            Canvas.SetLeft(bottomShade, 0);
            Canvas.SetTop(bottomShade, selectionY + selectionHeight);

            // Left shade
            leftShade.Width = Math.Max(0, selectionX);
            leftShade.Height = selectionHeight;
            Canvas.SetLeft(leftShade, 0);
            Canvas.SetTop(leftShade, selectionY);

            // Right shade
            double rightWidth = Math.Max(0, totalWidth - (selectionX + selectionWidth));
            rightShade.Width = rightWidth;
            rightShade.Height = selectionHeight;
            Canvas.SetLeft(rightShade, selectionX + selectionWidth);
            Canvas.SetTop(rightShade, selectionY);
        }

        /// <summary>
        /// Configura la visibilidad de todos los rectángulos de sombra
        /// </summary>
        public static void SetShadeVisibility(
            Rectangle topShade,
            Rectangle bottomShade,
            Rectangle leftShade,
            Rectangle rightShade,
            bool isVisible)
        {
            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            topShade.Visibility = visibility;
            bottomShade.Visibility = visibility;
            leftShade.Visibility = visibility;
            rightShade.Visibility = visibility;
        }

        /// <summary>
        /// Actualiza el display de coordenadas con las dimensiones actuales.
        /// Posiciona el panel por defecto AFUERA del área seleccionada (arriba-izquierda).
        /// Solo se posiciona DENTRO cuando no hay espacio afuera.
        /// </summary>
        public static void UpdateCoordinatesDisplay(
            Border coordinatesDisplay,
            TextBlock coordinatesText,
            double totalWidth,
            double totalHeight,
            double selectionX,
            double selectionY,
            double selectionWidth,
            double selectionHeight,
            double rasterizationScale)
        {
            // Convertir a píxeles reales usando la escala de rasterización
            int pixelWidth = (int)Math.Round(selectionWidth * rasterizationScale);
            int pixelHeight = (int)Math.Round(selectionHeight * rasterizationScale);

            // Actualizar el texto solo con las dimensiones
            coordinatesText.Text = $"{pixelWidth} × {pixelHeight} px";

            // Mostrar el display si hay área seleccionada
            if (selectionWidth > 0 && selectionHeight > 0)
            {
                coordinatesDisplay.Visibility = Visibility.Visible;

                // Medir el tamaño real del badge
                coordinatesDisplay.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double displayWidth = coordinatesDisplay.DesiredSize.Width > 0 
                    ? coordinatesDisplay.DesiredSize.Width 
                    : Constants.COORDINATES_DISPLAY_WIDTH;
                double displayHeight = coordinatesDisplay.DesiredSize.Height > 0 
                    ? coordinatesDisplay.DesiredSize.Height 
                    : Constants.COORDINATES_DISPLAY_HEIGHT;
                
                // Por defecto: posicionar AFUERA del área de selección (arriba-izquierda)
                double displayX = selectionX;
                double displayY = selectionY - displayHeight - Constants.DISPLAY_MARGIN;
                
                // Si no cabe arriba (sale del área visible), intentar posicionar DENTRO
                if (displayY < Constants.DISPLAY_MARGIN)
                {
                    const double innerPadding = 8.0;
                    
                    // Verificar si cabe dentro del área de selección
                    if (displayWidth + (innerPadding * 2) <= selectionWidth && 
                        displayHeight + (innerPadding * 2) <= selectionHeight)
                    {
                        // Cabe dentro: posicionar en la esquina superior izquierda DENTRO
                        displayX = selectionX + innerPadding;
                        displayY = selectionY + innerPadding;
                    }
                    else
                    {
                        // No cabe dentro: posicionar debajo de la selección
                        displayY = selectionY + selectionHeight + Constants.DISPLAY_MARGIN;
                    }
                }

                // Asegurar que no se salga del área visible (redondear a píxeles enteros)
                displayX = Math.Round(Math.Max(Constants.DISPLAY_MARGIN, displayX));
                displayY = Math.Round(Math.Max(Constants.DISPLAY_MARGIN, displayY));
                displayX = Math.Round(Math.Min(displayX, totalWidth - displayWidth - Constants.DISPLAY_MARGIN));
                displayY = Math.Round(Math.Min(displayY, totalHeight - displayHeight - Constants.DISPLAY_MARGIN));

                Canvas.SetLeft(coordinatesDisplay, displayX);
                Canvas.SetTop(coordinatesDisplay, displayY);
            }
            else
            {
                coordinatesDisplay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Posiciona la toolbar flotante relativa a la selección
        /// </summary>
        public static void PositionFloatingToolbar(
            Border floatingToolbar,
            double totalWidth,
            double totalHeight,
            double selectionX,
            double selectionY,
            double selectionWidth,
            double selectionHeight)
        {
            floatingToolbar.Visibility = Visibility.Visible;

            // Measure the toolbar to get its actual size
            floatingToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double toolbarWidth = floatingToolbar.DesiredSize.Width;
            double toolbarHeight = floatingToolbar.DesiredSize.Height;

            // Position at top center of selection, with offset above
            double toolbarX = selectionX + (selectionWidth - toolbarWidth) / 2;
            double toolbarY = selectionY - toolbarHeight - Constants.TOOLBAR_OFFSET;

            // Keep within bounds
            toolbarX = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(toolbarX, totalWidth - toolbarWidth - Constants.DISPLAY_MARGIN));
            toolbarY = Math.Max(Constants.DISPLAY_MARGIN, toolbarY);

            // If toolbar would be above viewport, put it below selection
            if (toolbarY < Constants.DISPLAY_MARGIN)
            {
                toolbarY = selectionY + selectionHeight + Constants.TOOLBAR_OFFSET;
            }

            Canvas.SetLeft(floatingToolbar, toolbarX);
            Canvas.SetTop(floatingToolbar, toolbarY);
        }

        /// <summary>
        /// Posiciona un toolbar secundario relativo a un botón de referencia.
        /// Intenta posicionarlo arriba del botón, o abajo si no hay espacio.
        /// </summary>
        public static void PositionSecondaryToolbar(
            FrameworkElement toolbar,
            FrameworkElement referenceButton,
            FrameworkElement rootContainer,
            double defaultWidth = 280,
            double defaultHeight = 220)
        {
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double toolbarWidth = toolbar.DesiredSize.Width > 0 ? toolbar.DesiredSize.Width : defaultWidth;
            double toolbarHeight = toolbar.DesiredSize.Height > 0 ? toolbar.DesiredSize.Height : defaultHeight;

            // Obtener la posición y tamaño del botón de referencia
            var buttonTransform = referenceButton.TransformToVisual(rootContainer);
            var buttonPosition = buttonTransform.TransformPoint(new Point(0, 0));
            double buttonHeight = referenceButton.ActualHeight;

            double containerWidth = rootContainer.ActualWidth;
            double containerHeight = rootContainer.ActualHeight;

            // Calcular espacio disponible arriba y abajo del botón
            double spaceAbove = buttonPosition.Y - Constants.DISPLAY_MARGIN;
            double spaceBelow = containerHeight - (buttonPosition.Y + buttonHeight + Constants.DISPLAY_MARGIN);

            double toolbarY;
            
            // Decidir si colocar el menú arriba o abajo según el espacio disponible
            if (spaceAbove >= toolbarHeight || spaceAbove > spaceBelow)
            {
                // Colocar arriba del botón
                toolbarY = buttonPosition.Y - toolbarHeight - Constants.DISPLAY_MARGIN;
                toolbarY = Math.Max(Constants.DISPLAY_MARGIN, toolbarY);
            }
            else
            {
                // Colocar abajo del botón
                toolbarY = buttonPosition.Y + buttonHeight + Constants.DISPLAY_MARGIN;
                toolbarY = Math.Min(toolbarY, containerHeight - toolbarHeight - Constants.DISPLAY_MARGIN);
            }

            // Alinear el menú con el botón horizontalmente
            double toolbarX = buttonPosition.X;
            
            // Ajustar si el menú se sale de los límites horizontales
            toolbarX = Math.Max(Constants.DISPLAY_MARGIN, Math.Min(toolbarX, containerWidth - toolbarWidth - Constants.DISPLAY_MARGIN));

            Canvas.SetLeft(toolbar, toolbarX);
            Canvas.SetTop(toolbar, toolbarY);
        }
    }
}
