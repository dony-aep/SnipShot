using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using SnipShot.Features.Capture.Annotations.Models;

namespace SnipShot.Features.Capture.Annotations.Base
{
    /// <summary>
    /// Interfaz para herramientas de anotación.
    /// Define el contrato para todas las herramientas de dibujo (bolígrafo, resaltador, formas).
    /// </summary>
    public interface IAnnotationTool
    {
        #region Properties

        /// <summary>
        /// Nombre identificador de la herramienta
        /// </summary>
        string ToolName { get; }

        /// <summary>
        /// Indica si la herramienta está actualmente activa
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// Indica si la herramienta está actualmente dibujando
        /// </summary>
        bool IsDrawing { get; }

        /// <summary>
        /// Configuración actual de la herramienta
        /// </summary>
        AnnotationSettings Settings { get; set; }

        /// <summary>
        /// El Path actualmente siendo dibujado (null si no hay dibujo activo)
        /// </summary>
        Path? CurrentPath { get; }

        #endregion

        #region Drawing Methods

        /// <summary>
        /// Inicia un nuevo trazo en el punto especificado
        /// </summary>
        /// <param name="startPoint">Punto de inicio del trazo</param>
        /// <returns>El Path creado para el trazo</returns>
        Path StartStroke(Point startPoint);

        /// <summary>
        /// Continúa el trazo actual hacia el punto especificado
        /// </summary>
        /// <param name="currentPoint">Punto actual del trazo</param>
        void ContinueStroke(Point currentPoint);

        /// <summary>
        /// Finaliza el trazo actual
        /// </summary>
        /// <returns>El Path completado, o null si el trazo fue cancelado/inválido</returns>
        Path? EndStroke();

        /// <summary>
        /// Cancela el trazo actual sin guardarlo
        /// </summary>
        void CancelStroke();

        #endregion

        #region Validation Methods

        /// <summary>
        /// Valida si un punto está dentro del área de dibujo permitida
        /// </summary>
        /// <param name="point">Punto a validar</param>
        /// <param name="bounds">Límites del área de dibujo</param>
        /// <returns>True si el punto es válido</returns>
        bool IsPointValid(Point point, Rect bounds);

        /// <summary>
        /// Restringe un punto a los límites del área de dibujo
        /// </summary>
        /// <param name="point">Punto a restringir</param>
        /// <param name="bounds">Límites del área de dibujo</param>
        /// <returns>El punto restringido</returns>
        Point ClampPoint(Point point, Rect bounds);

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Activa la herramienta
        /// </summary>
        void Activate();

        /// <summary>
        /// Desactiva la herramienta
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Reinicia el estado de la herramienta
        /// </summary>
        void Reset();

        #endregion
    }
}
