using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SnipShot
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private static Mutex? _mutex;
        private const string MUTEX_NAME = "SnipShot_SingleInstance_Mutex";
        
        /// <summary>
        /// Propiedad estática para acceder a la instancia de App desde cualquier lugar
        /// </summary>
        public static new App Current => (App)Application.Current;
        
        /// <summary>
        /// Propiedad para acceder a la ventana principal
        /// </summary>
        public Window? MainWindow => _window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Verificar si ya hay una instancia en ejecución
            _mutex = new Mutex(true, MUTEX_NAME, out bool createdNew);
            
            if (!createdNew)
            {
                // Ya hay una instancia en ejecución, buscar y activar la ventana existente
                ActivateExistingInstance();
                
                // Cerrar esta instancia
                Environment.Exit(0);
                return;
            }

            _window = new MainWindow();
            _window.Activate();
        }

        /// <summary>
        /// Busca y activa la ventana de la instancia existente de SnipShot.
        /// </summary>
        private static void ActivateExistingInstance()
        {
            // Buscar la ventana por su título
            IntPtr existingWindow = FindWindow(null, "SnipShot");
            
            if (existingWindow != IntPtr.Zero)
            {
                // Restaurar la ventana si está minimizada
                if (IsIconic(existingWindow))
                {
                    ShowWindow(existingWindow, SW_RESTORE);
                }
                else
                {
                    ShowWindow(existingWindow, SW_SHOW);
                }
                
                // Traer la ventana al frente
                SetForegroundWindow(existingWindow);
            }
        }

        #region Native Imports

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        #endregion
    }
}
