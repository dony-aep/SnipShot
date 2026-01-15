using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using SnipShot.Helpers.Capture;
using SnipShot.Helpers.Utils;

namespace SnipShot.Helpers.UI
{
    /// <summary>
    /// Handler para las acciones de la toolbar (Capturar, Guardar, Copiar)
    /// </summary>
    public static class ToolbarActionHandler
    {
        /// <summary>
        /// Resultado de una acción de la toolbar
        /// </summary>
        public enum ActionResult
        {
            Success,        // Acción completada exitosamente
            Cancelled,      // Usuario canceló la acción
            Failed          // Error durante la ejecución
        }

        /// <summary>
        /// Maneja la acción de capturar (simplemente confirma la selección)
        /// </summary>
        /// <returns>Siempre retorna Success</returns>
        public static ActionResult HandleCaptureAction()
        {
            // Esta acción solo confirma la selección para mostrarla en la app
            return ActionResult.Success;
        }

        /// <summary>
        /// Maneja la acción de guardar en archivo
        /// </summary>
        /// <param name="bitmap">Bitmap ya capturado de la región seleccionada</param>
        /// <param name="windowHandle">Handle de la ventana para el diálogo</param>
        /// <returns>Resultado de la operación</returns>
        public static async Task<ActionResult> HandleSaveAction(SoftwareBitmap? bitmap, IntPtr windowHandle)
        {
            if (bitmap == null)
            {
                return ActionResult.Failed;
            }

            try
            {
                var saveResult = await FileHelper.SaveImageAsync(bitmap, windowHandle);
                return saveResult.saved ? ActionResult.Success : ActionResult.Cancelled;
            }
            catch (Exception)
            {
                return ActionResult.Failed;
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        /// <summary>
        /// Maneja la acción de copiar al portapapeles
        /// </summary>
        /// <param name="bitmap">Bitmap ya capturado de la región seleccionada</param>
        /// <returns>Resultado de la operación</returns>
        public static async Task<ActionResult> HandleCopyAction(SoftwareBitmap? bitmap)
        {
            if (bitmap == null)
            {
                return ActionResult.Failed;
            }

            try
            {
                await ClipboardHelper.CopyImageToClipboardAsync(bitmap);
                return ActionResult.Success;
            }
            catch (Exception)
            {
                return ActionResult.Failed;
            }
            finally
            {
                bitmap.Dispose();
            }
        }
    }
}
