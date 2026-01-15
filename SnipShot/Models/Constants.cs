namespace SnipShot.Models
{
    /// <summary>
    /// Constantes utilizadas en toda la aplicación
    /// </summary>
    public static class Constants
    {
        #region System Metrics

        /// <summary>
        /// Coordenada X del escritorio virtual (multi-monitor)
        /// </summary>
        public const int SM_XVIRTUALSCREEN = 76;

        /// <summary>
        /// Coordenada Y del escritorio virtual (multi-monitor)
        /// </summary>
        public const int SM_YVIRTUALSCREEN = 77;

        /// <summary>
        /// Ancho del escritorio virtual (multi-monitor)
        /// </summary>
        public const int SM_CXVIRTUALSCREEN = 78;

        /// <summary>
        /// Alto del escritorio virtual (multi-monitor)
        /// </summary>
        public const int SM_CYVIRTUALSCREEN = 79;

        #endregion

        #region Selection Constraints

        /// <summary>
        /// Tamaño mínimo (en píxeles) para una selección válida
        /// </summary>
        public const double MIN_SELECTION_SIZE = 25;

        #endregion

        #region UI Layout

        /// <summary>
        /// Tamaño de los handles de redimensionamiento
        /// </summary>
        public const double HANDLE_SIZE = 12;

        /// <summary>
        /// Offset vertical entre la selección y la toolbar flotante
        /// </summary>
        public const double TOOLBAR_OFFSET = 15;

        /// <summary>
        /// Margen mínimo desde los bordes de la pantalla
        /// </summary>
        public const double DISPLAY_MARGIN = 10;

        /// <summary>
        /// Ancho estimado del display de coordenadas
        /// </summary>
        public const double COORDINATES_DISPLAY_WIDTH = 200;

        /// <summary>
        /// Alto estimado del display de coordenadas
        /// </summary>
        public const double COORDINATES_DISPLAY_HEIGHT = 40;

        #endregion
    }
}
