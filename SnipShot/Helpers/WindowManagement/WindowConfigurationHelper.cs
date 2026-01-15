using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace SnipShot.Helpers.WindowManagement
{
    /// <summary>
    /// Helper para configurar las ventanas de overlay con técnicas anti-parpadeo
    /// </summary>
    public static class WindowConfigurationHelper
    {
        #region P/Invoke Declarations

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        #endregion

        #region Constants

        // DWM Window Attributes
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        private const int DWMWA_CLOAK = 13;

        // Window Styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

        // Layered Window Attributes
        private const uint LWA_ALPHA = 0x2;

        #endregion

        /// <summary>
        /// Configura una ventana para que actúe como overlay sobre el área virtual de pantalla.
        /// Utiliza múltiples técnicas para evitar parpadeo negro/blanco al mostrar la ventana.
        /// </summary>
        /// <param name="window">Ventana a configurar</param>
        /// <param name="virtualBounds">Límites del área virtual de pantalla</param>
        public static void ConfigureOverlayWindow(Window window, RectInt32 virtualBounds)
        {
            try
            {
                var appWindow = window.AppWindow;
                if (appWindow == null)
                {
                    return;
                }

                IntPtr hwnd = WindowNative.GetWindowHandle(window);

                // 1. Usar DWM Cloak para ocultar la ventana completamente antes de configurarla
                //    Esto previene cualquier renderizado visible mientras se prepara
                CloakWindow(hwnd, true);

                // 2. Deshabilitar transiciones DWM para evitar animaciones de aparición
                DisableWindowTransitions(hwnd);

                // 3. Configurar como ventana en capas (layered) con alpha 0
                //    Esto asegura que incluso si se ve algo, sea completamente transparente
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
                SetLayeredWindowAttributes(hwnd, 0, 0, LWA_ALPHA);

                // 4. Ocultar de los switch de ventanas (Alt+Tab)
                appWindow.IsShownInSwitchers = false;

                // 5. Posicionar y dimensionar para cubrir el área virtual completa
                appWindow.MoveAndResize(virtualBounds);

                // 6. Configurar el presentador sin bordes ni controles
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsAlwaysOnTop = true;
                }

                // 7. Aplicar estilo sin bordes usando Win32 API
                WindowHelper.RemoveWindowBorders(window, hwnd);
            }
            catch
            {
                // Si las APIs de AppWindow no están disponibles, continuar con configuración por defecto
            }
        }

        /// <summary>
        /// Deshabilita las transiciones DWM para una ventana.
        /// Esto previene animaciones de aparición/desaparición que pueden causar parpadeo.
        /// </summary>
        private static void DisableWindowTransitions(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                int disabled = 1; // TRUE - deshabilitar transiciones
                DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disabled, sizeof(int));
            }
            catch
            {
                // Ignorar errores - la ventana funcionará con transiciones habilitadas
            }
        }

        /// <summary>
        /// Oculta una ventana usando DWM Cloak.
        /// La ventana sigue siendo compuesta por DWM pero no es visible para el usuario.
        /// </summary>
        public static void CloakWindow(IntPtr hwnd, bool cloak)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                int cloakValue = cloak ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref cloakValue, sizeof(int));
            }
            catch
            {
                // Ignorar errores de DWM
            }
        }

        /// <summary>
        /// Muestra una ventana previamente ocultada.
        /// Realiza el proceso inverso de ocultamiento: primero hace visible el contenido,
        /// luego quita el cloak de DWM.
        /// </summary>
        public static void UncloakWindow(Window window)
        {
            try
            {
                IntPtr hwnd = WindowNative.GetWindowHandle(window);

                // 1. Hacer la ventana completamente visible (alpha = 255)
                SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);

                // 2. Forzar actualización de la ventana para asegurar que el contenido esté renderizado
                UpdateWindow(hwnd);

                // 3. Quitar el cloak de DWM para hacer visible la ventana
                CloakWindow(hwnd, false);
            }
            catch
            {
                // Ignorar errores
            }
        }

        /// <summary>
        /// Espera a que se complete el ciclo de layout/render de XAML usando DispatcherQueue.
        /// Encola trabajo al final de la cola de prioridad baja para asegurar que todo el trabajo
        /// de mayor prioridad (incluyendo layout y renderizado) se complete primero.
        /// </summary>
        /// <param name="dispatcherQueue">DispatcherQueue de la ventana</param>
        /// <returns>Task que se completa cuando el ciclo de UI está listo</returns>
        public static Task WaitForUIThreadAsync(DispatcherQueue dispatcherQueue)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            // Encolar con prioridad baja para asegurar que todo el trabajo de layout/render
            // de mayor prioridad se complete primero
            dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                tcs.TrySetResult(true);
            });
            
            return tcs.Task;
        }

        /// <summary>
        /// Espera múltiples ciclos de UI para asegurar estabilidad visual.
        /// Útil para asegurar que el contenido XAML esté completamente renderizado.
        /// </summary>
        /// <param name="dispatcherQueue">DispatcherQueue de la ventana</param>
        /// <param name="cycles">Número de ciclos a esperar (por defecto 3)</param>
        /// <returns>Task que se completa después de los ciclos especificados</returns>
        public static async Task WaitForLayoutAsync(DispatcherQueue dispatcherQueue, int cycles = 3)
        {
            for (int i = 0; i < cycles; i++)
            {
                await WaitForUIThreadAsync(dispatcherQueue);
            }
        }

        /// <summary>
        /// Espera a que un control Image tenga su contenido completamente cargado y renderizado.
        /// Utiliza los eventos ImageOpened/ImageFailed con timeout de seguridad.
        /// </summary>
        /// <param name="image">Control Image a esperar</param>
        /// <param name="dispatcherQueue">DispatcherQueue para sincronización</param>
        /// <param name="timeoutMs">Tiempo máximo de espera en milisegundos (por defecto 500ms)</param>
        /// <returns>True si la imagen se cargó correctamente, false si hubo timeout o error</returns>
        public static async Task<bool> WaitForImageRenderAsync(
            Microsoft.UI.Xaml.Controls.Image image, 
            DispatcherQueue dispatcherQueue,
            int timeoutMs = 500)
        {
            if (image == null) return false;

            // Si la imagen ya tiene una fuente establecida, esperar ciclos de UI para el renderizado
            if (image.Source != null)
            {
                // Esperar múltiples ciclos para asegurar que el bitmap esté completamente renderizado
                await WaitForLayoutAsync(dispatcherQueue, 3);
                return true;
            }

            var tcs = new TaskCompletionSource<bool>();
            
            void OnImageOpened(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
            {
                image.ImageOpened -= OnImageOpened;
                image.ImageFailed -= OnImageFailed;
                tcs.TrySetResult(true);
            }
            
            void OnImageFailed(object sender, Microsoft.UI.Xaml.ExceptionRoutedEventArgs e)
            {
                image.ImageOpened -= OnImageOpened;
                image.ImageFailed -= OnImageFailed;
                tcs.TrySetResult(false);
            }
            
            image.ImageOpened += OnImageOpened;
            image.ImageFailed += OnImageFailed;
            
            // Crear timeout de seguridad
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            
            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                
                if (completedTask == tcs.Task)
                {
                    cts.Cancel(); // Cancelar el timeout si la imagen se cargó
                    return await tcs.Task;
                }
                
                // Timeout alcanzado - limpiar handlers
                image.ImageOpened -= OnImageOpened;
                image.ImageFailed -= OnImageFailed;
                
                // Aunque haya timeout, esperar ciclos de UI por si acaso
                await WaitForLayoutAsync(dispatcherQueue, 2);
                return image.Source != null;
            }
            catch (TaskCanceledException)
            {
                // El timeout fue cancelado porque la imagen se cargó
                return await tcs.Task;
            }
        }
    }
}
