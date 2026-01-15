using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio para gestionar el inicio automático de la aplicación con Windows.
    /// Utiliza la API de StartupTask para aplicaciones empaquetadas (MSIX).
    /// </summary>
    public static class StartupService
    {
        private const string STARTUP_TASK_ID = "SnipShotStartupTask";

        /// <summary>
        /// Verifica si la aplicación está configurada para iniciar con Windows.
        /// </summary>
        /// <returns>True si está habilitado, False en caso contrario.</returns>
        public static async Task<bool> IsStartupEnabledAsync()
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                return startupTask.State == StartupTaskState.Enabled;
            }
            catch
            {
                // Si falla (por ejemplo, en desarrollo sin empaquetado), retornar false
                return false;
            }
        }

        /// <summary>
        /// Habilita o deshabilita el inicio automático con Windows.
        /// </summary>
        /// <param name="enable">True para habilitar, False para deshabilitar.</param>
        /// <returns>True si la operación fue exitosa, False en caso contrario.</returns>
        public static async Task<bool> SetStartupEnabledAsync(bool enable)
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);

                if (enable)
                {
                    switch (startupTask.State)
                    {
                        case StartupTaskState.Disabled:
                            // El usuario ha deshabilitado la tarea - solicitar habilitación
                            var newState = await startupTask.RequestEnableAsync();
                            return newState == StartupTaskState.Enabled;

                        case StartupTaskState.DisabledByUser:
                            // El usuario deshabilitó manualmente desde Configuración de Windows
                            // No podemos habilitarlo programáticamente
                            return false;

                        case StartupTaskState.DisabledByPolicy:
                            // Deshabilitado por política de grupo
                            return false;

                        case StartupTaskState.Enabled:
                            // Ya está habilitado
                            return true;

                        default:
                            return false;
                    }
                }
                else
                {
                    // Deshabilitar
                    startupTask.Disable();
                    return true;
                }
            }
            catch
            {
                // Si falla, retornar false
                return false;
            }
        }

        /// <summary>
        /// Obtiene información detallada sobre el estado de la tarea de inicio.
        /// </summary>
        /// <returns>Estado actual de la tarea de inicio.</returns>
        public static async Task<StartupTaskState> GetStartupStateAsync()
        {
            try
            {
                var startupTask = await StartupTask.GetAsync(STARTUP_TASK_ID);
                return startupTask.State;
            }
            catch
            {
                return StartupTaskState.Disabled;
            }
        }
    }
}
