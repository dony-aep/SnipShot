using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Apertura y cierre de los desplegables de selección (modo de captura, delay).
    /// </summary>
    public static class FlyoutHelper
    {
        /// <summary>
        /// Padding (4) más borde (1) del FlyoutPresenter: separa su esquina superior
        /// izquierda del primer item de la lista.
        /// </summary>
        private const double PresenterInset = 5;

        /// <summary>
        /// Valor de ListViewItemMinHeight del tema. El contenido de cada fila
        /// (icono de 16 más texto) no lo supera, así que la fila mide exactamente esto.
        /// </summary>
        private const double RowHeight = 40;

        /// <summary>
        /// Muestra el AttachedFlyout de un botón alineando la fila seleccionada sobre él,
        /// como hace un ComboBox con su lista.
        /// </summary>
        /// <remarks>
        /// El Placement del Flyout debe ser BottomEdgeAlignedLeft: con una Position explícita
        /// el Placement no se ignora, sino que decide hacia dónde se ancla el flyout respecto
        /// al punto. Con el valor por defecto (Top) la lista sale centrada y por encima.
        /// La Position se interpreta en coordenadas del propio target.
        /// </remarks>
        /// <param name="button">Botón que actúa de placement target</param>
        /// <param name="list">Lista contenida en el flyout</param>
        public static void ShowOverSelectedItem(FrameworkElement button, ListView list)
        {
            FlyoutBase.GetAttachedFlyout(button)?.ShowAt(button, new FlyoutShowOptions
            {
                Position = new Point(0, -GetSelectedItemOffset(list))
            });
        }

        /// <summary>
        /// Cierra el AttachedFlyout de un botón.
        /// </summary>
        /// <param name="button">Botón que actúa de placement target</param>
        public static void HideAttached(FrameworkElement button)
        {
            FlyoutBase.GetAttachedFlyout(button)?.Hide();
        }

        /// <summary>
        /// Distancia desde el borde superior del flyout hasta el de la fila seleccionada.
        /// </summary>
        /// <remarks>
        /// Tras la primera apertura los contenedores del ListView ya existen y se mide exacto.
        /// En la primera aún no están realizados —el flyout no ha entrado en el árbol visual—
        /// y no hay nada que medir, así que se estima con el alto de fila del tema.
        /// </remarks>
        private static double GetSelectedItemOffset(ListView list)
        {
            if (list.SelectedIndex < 0)
            {
                return PresenterInset;
            }

            if (list.ContainerFromIndex(list.SelectedIndex) is FrameworkElement container
                && container.ActualHeight > 0)
            {
                double offsetInList = container
                    .TransformToVisual(list)
                    .TransformPoint(new Point(0, 0))
                    .Y;

                return PresenterInset + offsetInList;
            }

            return PresenterInset + (list.SelectedIndex * RowHeight);
        }
    }
}
