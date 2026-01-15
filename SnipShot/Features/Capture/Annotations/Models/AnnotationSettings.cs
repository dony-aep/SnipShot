using Windows.UI;
using Microsoft.UI;

namespace SnipShot.Features.Capture.Annotations.Models
{
    /// <summary>
    /// Configuración para herramientas de anotación.
    /// Contiene propiedades de estilo como color, grosor y opacidad.
    /// </summary>
    public class AnnotationSettings
    {
        #region Stroke Settings

        /// <summary>
        /// Color del trazo
        /// </summary>
        public Color StrokeColor { get; set; } = Colors.Red;

        /// <summary>
        /// Opacidad del trazo (0.0 - 1.0)
        /// </summary>
        public double StrokeOpacity { get; set; } = 1.0;

        /// <summary>
        /// Grosor del trazo en píxeles
        /// </summary>
        public double StrokeThickness { get; set; } = 3.0;

        #endregion

        #region Fill Settings

        /// <summary>
        /// Color de relleno para formas cerradas
        /// </summary>
        public Color FillColor { get; set; } = Colors.Transparent;

        /// <summary>
        /// Opacidad del relleno (0.0 - 1.0)
        /// </summary>
        public double FillOpacity { get; set; } = 0.0;

        /// <summary>
        /// Indica si el relleno está habilitado
        /// </summary>
        public bool FillEnabled { get; set; } = false;

        #endregion

        #region Preset Configurations

        /// <summary>
        /// Configuración por defecto para el bolígrafo
        /// </summary>
        public static AnnotationSettings DefaultPen => new()
        {
            StrokeColor = Colors.White,
            StrokeOpacity = 1.0,
            StrokeThickness = 2.0,
            FillEnabled = false
        };

        /// <summary>
        /// Configuración por defecto para el resaltador
        /// </summary>
        public static AnnotationSettings DefaultHighlighter => new()
        {
            StrokeColor = Color.FromArgb(255, 255, 255, 0), // Amarillo
            StrokeOpacity = 0.5, // Semi-transparente
            StrokeThickness = 16.0,
            FillEnabled = false
        };

        /// <summary>
        /// Configuración por defecto para formas
        /// </summary>
        public static AnnotationSettings DefaultShape => new()
        {
            StrokeColor = Colors.Red,
            StrokeOpacity = 1.0,
            StrokeThickness = 3.0,
            FillColor = Colors.Transparent,
            FillOpacity = 0.0,
            FillEnabled = false
        };

        #endregion

        #region Methods

        /// <summary>
        /// Crea una copia de la configuración actual
        /// </summary>
        public AnnotationSettings Clone()
        {
            return new AnnotationSettings
            {
                StrokeColor = StrokeColor,
                StrokeOpacity = StrokeOpacity,
                StrokeThickness = StrokeThickness,
                FillColor = FillColor,
                FillOpacity = FillOpacity,
                FillEnabled = FillEnabled
            };
        }

        /// <summary>
        /// Obtiene el color del trazo con la opacidad aplicada
        /// </summary>
        public Color GetEffectiveStrokeColor()
        {
            return Color.FromArgb(
                (byte)(StrokeOpacity * 255),
                StrokeColor.R,
                StrokeColor.G,
                StrokeColor.B);
        }

        /// <summary>
        /// Obtiene el color de relleno con la opacidad aplicada
        /// </summary>
        public Color GetEffectiveFillColor()
        {
            if (!FillEnabled || FillOpacity <= 0)
            {
                return Colors.Transparent;
            }

            return Color.FromArgb(
                (byte)(FillOpacity * 255),
                FillColor.R,
                FillColor.G,
                FillColor.B);
        }

        #endregion
    }
}
