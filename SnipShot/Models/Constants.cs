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

        /// <summary>
        /// Recorrido mínimo (en píxeles) para considerar que hubo arrastre y no un clic suelto.
        /// </summary>
        /// <remarks>
        /// Es distinto de MIN_SELECTION_SIZE a propósito: aquel es el tamaño mínimo al que se
        /// puede encoger una selección, y usarlo también para filtrar clics hacía que un
        /// arrastre pequeño pero deliberado se descartara en silencio.
        /// </remarks>
        public const double MIN_DRAG_DISTANCE = 5;

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

        #region Build Info

        /// <summary>
        /// Sufijo que identifica la configuración con la que se compiló la app.
        /// En Release queda vacío, así que el usuario final no ve ningún cambio.
        /// </summary>
#if DEBUG
        public const string BUILD_SUFFIX = " (Debug)";
#else
        public const string BUILD_SUFFIX = "";
#endif

        #endregion
    }
}
