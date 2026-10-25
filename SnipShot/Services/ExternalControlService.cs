using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace SnipShot.Services
{
    /// <summary>
    /// Servidor del named pipe por el que el comando <c>snipshot</c> controla esta instancia.
    /// La E/S del pipe ocurre en segundo plano; cada petición se ejecuta en el hilo de UI,
    /// porque las capturas abren overlays y tocan la ventana.
    /// </summary>
    internal sealed class ExternalControlService : IDisposable
    {
        // Una conexión que no envía nada no debe ocupar una instancia del pipe para siempre.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Func<ControlRequest, Task<ControlResponse>> _handler;
        private readonly string _pipeName;
        private CancellationTokenSource? _cts;

        public ExternalControlService(DispatcherQueue dispatcherQueue, Func<ControlRequest, Task<ControlResponse>> handler)
        {
            _dispatcherQueue = dispatcherQueue;
            _handler = handler;
            _pipeName = ControlProtocol.GetPipeName(Process.GetCurrentProcess().SessionId);
        }

        public void Start()
        {
            if (_cts != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => Stop();

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    // CurrentUserOnly limita el pipe al usuario actual y a su mismo nivel de
                    // elevación: un CLI lanzado como administrador no conecta con una app sin elevar.
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    server?.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    server?.Dispose();
                    Debug.WriteLine($"ExternalControl: no se pudo abrir el pipe: {ex.Message}");

                    // Sin esta pausa, un pipe que no se puede crear dejaría el bucle girando en vacío.
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    continue;
                }

                _ = HandleClientAsync(server, ct);
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
        {
            using (server)
            {
                try
                {
                    using var reader = new StreamReader(server, Utf8WithoutBom, false, 1024, leaveOpen: true);
                    using var writer = new StreamWriter(server, Utf8WithoutBom, 1024, leaveOpen: true) { AutoFlush = true };

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(RequestTimeout);
                    var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);

                    var response = ControlProtocol.TryParseRequest(line, out var request, out var error)
                        ? await RunOnUiThreadAsync(request).ConfigureAwait(false)
                        : ControlResponse.Failure(error);

                    await writer.WriteLineAsync(ControlProtocol.SerializeResponse(response)).ConfigureAwait(false);

                    // Cerrar el servidor antes de que el cliente lea puede descartar la respuesta.
                    server.WaitForPipeDrain();
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException ex)
                {
                    // El cliente se desconectó antes de terminar; no hay a quién responder.
                    Debug.WriteLine($"ExternalControl: conexión interrumpida: {ex.Message}");
                }
            }
        }

        private Task<ControlResponse> RunOnUiThreadAsync(ControlRequest request)
        {
            var completion = new TaskCompletionSource<ControlResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            var enqueued = _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    completion.SetResult(await _handler(request));
                }
                catch (Exception ex)
                {
                    completion.SetResult(ControlResponse.Failure(ControlProtocol.ErrorCodes.Internal, ex.Message));
                }
            });

            if (!enqueued)
            {
                completion.SetResult(ControlResponse.Failure(ControlProtocol.ErrorCodes.Internal, "SnipShot se está cerrando."));
            }

            return completion.Task;
        }
    }
}
