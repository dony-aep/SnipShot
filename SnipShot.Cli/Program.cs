using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using SnipShot.Services;

namespace SnipShot.Cli
{
    /// <summary>
    /// Punto de entrada del comando <c>snipshot</c>. Escribe siempre una respuesta JSON en la
    /// salida estándar y sale con el código de <see cref="ControlProtocol.GetExitCode"/>, para
    /// que lo pueda usar un agente o un script sin interpretar texto.
    /// </summary>
    internal static class Program
    {
        // Si el pipe no existe, Connect espera hasta agotar este plazo: es lo que tarda en
        // responder "no está abierto". Basta con poco porque la app crea el pipe al arrancar.
        private const int ConnectTimeoutMs = 1500;
        // Lo que puede tardar la app en arrancar y abrir el pipe cuando la lanza el propio CLI.
        private const int LaunchConnectTimeoutMs = 15000;
        // Margen sobre el retardo que pida la captura: ocultar la ventana y guardar el PNG.
        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private static async Task<int> Main(string[] args)
        {
            ControlResponse response = CliArguments.TryParse(args, Environment.CurrentDirectory, out var request, out var usageError)
                ? await SendAsync(request)
                : ControlResponse.Failure(ControlProtocol.ErrorCodes.InvalidRequest, usageError);

            Console.WriteLine(ControlProtocol.SerializeResponse(response));
            return ControlProtocol.GetExitCode(response);
        }

        private static async Task<ControlResponse> SendAsync(ControlRequest request)
        {
            var (client, failure) = await ConnectAsync(ConnectTimeoutMs);
            if (client is null)
            {
                if (failure!.Error!.Code != ControlProtocol.ErrorCodes.NotRunning)
                {
                    return failure;
                }

                // Abierta pero sin pipe: el control externo está desactivado. Lanzar otra
                // instancia no serviría de nada y haría aparecer la ventana de la primera.
                if (IsAppRunning())
                {
                    return ControlResponse.Failure(ControlProtocol.ErrorCodes.Disabled,
                        "SnipShot está abierto pero tiene desactivado el control externo. Actívalo en Configuración > General.");
                }

                // status solo informa; las capturas abren la app si hace falta.
                if (request.Command == ControlProtocol.Commands.Status)
                {
                    return failure;
                }

                if (TryLaunchApp() is { } launchFailure)
                {
                    return launchFailure;
                }

                (client, failure) = await ConnectAsync(LaunchConnectTimeoutMs);
                if (client is null)
                {
                    return failure!.Error!.Code == ControlProtocol.ErrorCodes.NotRunning
                        ? ControlResponse.Failure(ControlProtocol.ErrorCodes.NotRunning,
                            "Se abrió SnipShot pero no respondió. Comprueba que el control externo está activado en Configuración > General.")
                        : failure;
                }
            }

            using (client)
            {
                return await ExchangeAsync(client, request);
            }
        }

        private static async Task<(NamedPipeClientStream? Client, ControlResponse? Failure)> ConnectAsync(int timeoutMs)
        {
            var pipeName = ControlProtocol.GetPipeName(Process.GetCurrentProcess().SessionId);
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await client.ConnectAsync(timeoutMs);
                return (client, null);
            }
            catch (TimeoutException)
            {
                client.Dispose();
                return (null, ControlResponse.Failure(ControlProtocol.ErrorCodes.NotRunning, "SnipShot no está abierto."));
            }
            catch (UnauthorizedAccessException)
            {
                client.Dispose();
                return (null, ControlResponse.Failure(ControlProtocol.ErrorCodes.AccessDenied,
                    "No se puede conectar con SnipShot: la terminal y la app deben ejecutarse con el mismo nivel de elevación."));
            }
        }

        private static bool IsAppRunning()
        {
            var sessionId = Process.GetCurrentProcess().SessionId;
            var processes = Process.GetProcessesByName("SnipShot");
            try
            {
                return processes.Any(p => p.SessionId == sessionId);
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static ControlResponse? TryLaunchApp()
        {
            var appPath = Path.Combine(AppContext.BaseDirectory, "SnipShot.exe");
            try
            {
                // Se lanza desde la carpeta del paquete: el proceso hereda la identidad de
                // paquete del CLI, sin la que la app no puede arrancar. UseShellExecute evita
                // que herede la salida estándar: si no, quien lea la salida del CLI esperaría
                // a que se cerrara la app.
                using var process = Process.Start(new ProcessStartInfo(appPath, ControlProtocol.BackgroundLaunchArgument)
                {
                    UseShellExecute = true,
                });
                return null;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return ControlResponse.Failure(ControlProtocol.ErrorCodes.NotRunning, $"No se pudo abrir SnipShot: {ex.Message}");
            }
        }

        private static async Task<ControlResponse> ExchangeAsync(NamedPipeClientStream client, ControlRequest request)
        {
            using var writer = new StreamWriter(client, Utf8WithoutBom, 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Utf8WithoutBom, false, 1024, leaveOpen: true);

            // En una captura interactiva hay que esperar además lo que tarde el usuario; la app
            // cierra el overlay al agotar ese plazo y responde timeout antes de que venza este.
            var waitSeconds = request.DelaySeconds
                + (ControlProtocol.IsInteractive(request) ? ControlProtocol.GetTimeoutSeconds(request) : 0);
            using var timeout = new CancellationTokenSource(ResponseTimeout + TimeSpan.FromSeconds(waitSeconds));
            try
            {
                await writer.WriteLineAsync(ControlProtocol.SerializeRequest(request));
                var line = await reader.ReadLineAsync(timeout.Token);

                return ControlProtocol.ParseResponse(line)
                    ?? ControlResponse.Failure(ControlProtocol.ErrorCodes.Internal, "SnipShot devolvió una respuesta ilegible.");
            }
            catch (OperationCanceledException)
            {
                return ControlResponse.Failure(ControlProtocol.ErrorCodes.Internal, "SnipShot no respondió a tiempo.");
            }
            catch (IOException ex)
            {
                return ControlResponse.Failure(ControlProtocol.ErrorCodes.Internal, $"Se cortó la conexión con SnipShot: {ex.Message}");
            }
        }
    }
}
