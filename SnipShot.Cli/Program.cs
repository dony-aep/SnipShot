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
        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private static async Task<int> Main(string[] args)
        {
            ControlResponse response = args is [ControlProtocol.Commands.Status]
                ? await SendAsync(new ControlRequest { Command = ControlProtocol.Commands.Status })
                : ControlResponse.Failure(ControlProtocol.ErrorCodes.InvalidRequest, "Uso: snipshot status");

            Console.WriteLine(ControlProtocol.SerializeResponse(response));
            return ControlProtocol.GetExitCode(response);
        }

        private static async Task<ControlResponse> SendAsync(ControlRequest request)
        {
            var pipeName = ControlProtocol.GetPipeName(Process.GetCurrentProcess().SessionId);
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await client.ConnectAsync(ConnectTimeoutMs);
            }
            catch (TimeoutException)
            {
                return ControlResponse.Failure(ControlProtocol.ErrorCodes.NotRunning,
                    "SnipShot no está abierto o tiene desactivado el control externo.");
            }
            catch (UnauthorizedAccessException)
            {
                return ControlResponse.Failure(ControlProtocol.ErrorCodes.AccessDenied,
                    "No se puede conectar con SnipShot: la terminal y la app deben ejecutarse con el mismo nivel de elevación.");
            }

            using var writer = new StreamWriter(client, Utf8WithoutBom, 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, Utf8WithoutBom, false, 1024, leaveOpen: true);

            using var timeout = new CancellationTokenSource(ResponseTimeout);
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
