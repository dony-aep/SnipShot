using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnipShot.Services
{
    /// <summary>
    /// Protocolo entre el comando <c>snipshot</c> y la instancia de SnipShot que vive en la
    /// bandeja: un mensaje JSON por línea a través de un named pipe.
    /// </summary>
    /// <remarks>
    /// Este archivo lo compilan la app, el CLI y las pruebas, así que no puede depender de
    /// WinUI ni del Windows App SDK. Se serializa con generación de código fuente para que el
    /// trimming de Release no rompa nada.
    /// </remarks>
    internal static class ControlProtocol
    {
        /// <summary>
        /// Versión del protocolo. Solo cambia si deja de ser compatible: el CLI y la app viajan
        /// en el mismo paquete, pero tras actualizar puede seguir abierta la instancia antigua.
        /// </summary>
        public const int Version = 1;

        public static class Commands
        {
            public const string Status = "status";
            public const string Capture = "capture";
            public const string ListWindows = "list-windows";
        }

        public static class CaptureModes
        {
            public const string FullScreen = "fullscreen";
            public const string Window = "window";
            public const string Region = "region";
            public const string FreeForm = "freeform";

            public const string Available = $"{FullScreen}, {Window}, {Region}, {FreeForm}";
        }

        public const int MaxDelaySeconds = 30;

        // Lo que se espera a que el usuario dibuje o elija en un overlay. Dos minutos dan para
        // cambiar de ventana y volver; el máximo evita dejar un overlay olvidado media hora.
        public const int DefaultTimeoutSeconds = 120;
        public const int MinTimeoutSeconds = 5;
        public const int MaxTimeoutSeconds = 600;

        /// <summary>
        /// Argumento con el que el CLI arranca la app cuando está cerrada: la deja en la
        /// bandeja, como el inicio con Windows, en vez de abrir la ventana.
        /// </summary>
        public const string BackgroundLaunchArgument = "--background";

        public static class ErrorCodes
        {
            public const string InvalidRequest = "invalid_request";
            public const string UnsupportedVersion = "unsupported_version";
            public const string UnknownCommand = "unknown_command";
            public const string NotRunning = "not_running";
            public const string AccessDenied = "access_denied";
            public const string Disabled = "disabled";
            public const string NotFound = "not_found";
            public const string Ambiguous = "ambiguous";
            public const string Cancelled = "cancelled";
            public const string Timeout = "timeout";
            public const string Busy = "busy";
            public const string Internal = "internal";
        }

        /// <summary>
        /// Las capturas que abren un overlay y esperan al usuario.
        /// </summary>
        public static bool IsInteractive(ControlRequest request) =>
            request.Command == Commands.Capture
            && (request.Mode == CaptureModes.FreeForm
                || (request.Mode == CaptureModes.Region && request.Rect is null)
                || (request.Mode == CaptureModes.Window && request.Interactive));

        /// <summary>
        /// Plazo de espera de una captura interactiva, con el valor por defecto si no se indicó.
        /// </summary>
        public static int GetTimeoutSeconds(ControlRequest request) =>
            request.TimeoutSeconds > 0 ? request.TimeoutSeconds : DefaultTimeoutSeconds;

        /// <summary>
        /// Nombre del pipe. Incluye la sesión de Windows para que dos sesiones abiertas a la vez
        /// no compartan canal.
        /// </summary>
        public static string GetPipeName(int sessionId) => $"SnipShot.Control.{sessionId}";

        public static string SerializeRequest(ControlRequest request) =>
            JsonSerializer.Serialize(request, ControlJsonContext.Default.ControlRequest);

        /// <summary>
        /// Interpreta una petición recibida por el pipe. Nunca lanza: lo que no se entiende se
        /// devuelve como error para responderlo al cliente.
        /// </summary>
        public static bool TryParseRequest(
            string? line,
            [NotNullWhen(true)] out ControlRequest? request,
            [NotNullWhen(false)] out ControlError? error)
        {
            request = null;

            ControlRequest? parsed;
            try
            {
                parsed = string.IsNullOrWhiteSpace(line)
                    ? null
                    : JsonSerializer.Deserialize(line, ControlJsonContext.Default.ControlRequest);
            }
            catch (JsonException)
            {
                parsed = null;
            }

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Command))
            {
                error = new ControlError(ErrorCodes.InvalidRequest, "La petición no es un JSON válido con versión y comando.");
                return false;
            }

            if (parsed.Version != Version)
            {
                error = new ControlError(ErrorCodes.UnsupportedVersion,
                    $"Versión de protocolo {parsed.Version} no admitida; esta instancia usa la {Version}. Reinicia SnipShot.");
                return false;
            }

            error = parsed.Command switch
            {
                Commands.Status or Commands.ListWindows => null,
                Commands.Capture => ValidateCapture(parsed),
                _ => new ControlError(ErrorCodes.UnknownCommand, $"Comando desconocido: {parsed.Command}."),
            };

            if (error is not null)
            {
                return false;
            }

            request = parsed;
            return true;
        }

        private static ControlError? ValidateCapture(ControlRequest request)
        {
            if (request.Mode is not (CaptureModes.FullScreen or CaptureModes.Window or CaptureModes.Region or CaptureModes.FreeForm))
            {
                return new ControlError(ErrorCodes.InvalidRequest,
                    request.Mode is null
                        ? $"Falta el modo de captura. Modos disponibles: {CaptureModes.Available}."
                        : $"Modo de captura no admitido: {request.Mode}. Modos disponibles: {CaptureModes.Available}.");
            }

            var selectors = (request.Hwnd is not null ? 1 : 0)
                + (string.IsNullOrWhiteSpace(request.Title) ? 0 : 1)
                + (string.IsNullOrWhiteSpace(request.Process) ? 0 : 1);

            if (request.Mode == CaptureModes.Window)
            {
                if (request.Interactive ? selectors != 0 : selectors != 1)
                {
                    return new ControlError(ErrorCodes.InvalidRequest,
                        "Para capturar una ventana indica exactamente una de estas opciones: hwnd, title, process o interactive.");
                }
            }
            else if (selectors != 0 || request.Interactive)
            {
                return new ControlError(ErrorCodes.InvalidRequest,
                    "hwnd, title, process e interactive solo se usan con el modo window.");
            }

            if (request.Rect is { } rect)
            {
                if (request.Mode != CaptureModes.Region)
                {
                    return new ControlError(ErrorCodes.InvalidRequest, "rect solo se usa con el modo region.");
                }

                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return new ControlError(ErrorCodes.InvalidRequest,
                        $"El ancho y el alto de la región deben ser mayores que cero: {rect.Width}x{rect.Height}.");
                }
            }

            if (request.TimeoutSeconds != 0 && request.TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
            {
                return new ControlError(ErrorCodes.InvalidRequest,
                    $"El tiempo de espera debe estar entre {MinTimeoutSeconds} y {MaxTimeoutSeconds} segundos.");
            }

            if (request.DelaySeconds is < 0 or > MaxDelaySeconds)
            {
                return new ControlError(ErrorCodes.InvalidRequest, $"El retardo debe estar entre 0 y {MaxDelaySeconds} segundos.");
            }

            if (request.Out is not null)
            {
                // La app tiene otro directorio de trabajo que el CLI: una ruta relativa acabaría
                // en otra carpeta. El CLI la resuelve antes de enviarla.
                if (!Path.IsPathFullyQualified(request.Out))
                {
                    return new ControlError(ErrorCodes.InvalidRequest, $"La ruta de salida debe ser absoluta: {request.Out}.");
                }

                if (!request.Out.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    return new ControlError(ErrorCodes.InvalidRequest, $"La captura se guarda como PNG; la ruta debe terminar en .png: {request.Out}.");
                }
            }

            return null;
        }

        /// <summary>
        /// Ventanas que encajan con el selector de la petición: el handle exacto, el título por
        /// subcadena sin distinguir mayúsculas, o el nombre del proceso con o sin ".exe".
        /// </summary>
        public static WindowEntry[] FindWindows(IEnumerable<WindowEntry> windows, ControlRequest request)
        {
            if (request.Hwnd is { } hwnd)
            {
                return windows.Where(w => w.Hwnd == hwnd).ToArray();
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                return windows.Where(w => w.Title.Contains(request.Title, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            if (!string.IsNullOrWhiteSpace(request.Process))
            {
                var process = StripExe(request.Process);
                return windows.Where(w => string.Equals(StripExe(w.Process), process, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return [];
        }

        private static string StripExe(string processName) =>
            processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName[..^4] : processName;

        public static string SerializeResponse(ControlResponse response) =>
            JsonSerializer.Serialize(response, ControlJsonContext.Default.ControlResponse);

        /// <summary>
        /// Interpreta la respuesta de la app. Devuelve null si no se entiende.
        /// </summary>
        public static ControlResponse? ParseResponse(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize(line, ControlJsonContext.Default.ControlResponse);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Código de salida del CLI para una respuesta. Un agente decide qué hacer por este
        /// código sin tener que interpretar el mensaje de error.
        /// </summary>
        public static int GetExitCode(ControlResponse response)
        {
            if (response.Ok)
            {
                return 0;
            }

            return response.Error?.Code switch
            {
                ErrorCodes.InvalidRequest or ErrorCodes.UnsupportedVersion or ErrorCodes.UnknownCommand
                    or ErrorCodes.NotFound or ErrorCodes.Ambiguous => 2,
                ErrorCodes.Cancelled or ErrorCodes.Timeout => 3,
                ErrorCodes.NotRunning or ErrorCodes.AccessDenied or ErrorCodes.Disabled => 4,
                ErrorCodes.Busy => 5,
                _ => 1,
            };
        }
    }

    internal sealed class ControlRequest
    {
        [JsonRequired]
        public int Version { get; init; } = ControlProtocol.Version;

        [JsonRequired]
        public string Command { get; init; } = string.Empty;

        // Opciones de capture; el resto de comandos las ignora.
        public string? Mode { get; init; }

        /// <summary>
        /// Ruta absoluta del PNG. Sin ella se guarda en la carpeta de autoguardado.
        /// </summary>
        public string? Out { get; init; }

        public bool Clipboard { get; init; }

        public int DelaySeconds { get; init; }

        // Selectores del modo window; se usa exactamente uno.
        public long? Hwnd { get; init; }

        public string? Title { get; init; }

        public string? Process { get; init; }

        /// <summary>
        /// En el modo window, abre el overlay para que el usuario elija la ventana.
        /// </summary>
        public bool Interactive { get; init; }

        /// <summary>
        /// En el modo region, captura este rectángulo sin overlay. Sin él, el usuario lo dibuja.
        /// </summary>
        public RegionRect? Rect { get; init; }

        /// <summary>
        /// Segundos de espera en las capturas interactivas; 0 usa el valor por defecto.
        /// </summary>
        public int TimeoutSeconds { get; init; }
    }

    /// <summary>
    /// Rectángulo en coordenadas de pantalla y píxeles físicos. X e Y pueden ser negativos en
    /// monitores situados a la izquierda o encima del principal.
    /// </summary>
    internal sealed record RegionRect(int X, int Y, int Width, int Height);

    internal sealed class ControlResponse
    {
        [JsonRequired]
        public int Version { get; init; } = ControlProtocol.Version;

        public bool Ok { get; init; }

        public ControlError? Error { get; init; }

        public StatusInfo? Status { get; init; }

        public CaptureResult? Capture { get; init; }

        /// <summary>
        /// Lista de list-windows, o las candidatas cuando el selector de una captura es ambiguo.
        /// </summary>
        public WindowEntry[]? Windows { get; init; }

        public static ControlResponse Failure(string code, string message) =>
            new() { Ok = false, Error = new ControlError(code, message) };

        public static ControlResponse Failure(ControlError error) =>
            new() { Ok = false, Error = error };
    }

    internal sealed record ControlError(string Code, string Message);

    /// <summary>
    /// Datos de la instancia en ejecución que devuelve el comando <c>status</c>.
    /// </summary>
    internal sealed record StatusInfo(string AppVersion, int ProcessId, string AutoSaveFolder);

    /// <summary>
    /// Captura guardada: ruta del PNG y tamaño en píxeles.
    /// </summary>
    internal sealed record CaptureResult(string Path, int Width, int Height, string Mode)
    {
        /// <summary>
        /// Ventana capturada, en el modo window.
        /// </summary>
        public WindowEntry? Window { get; init; }
    }

    /// <summary>
    /// Ventana capturable. Las coordenadas son de pantalla, en píxeles físicos.
    /// </summary>
    internal sealed record WindowEntry(long Hwnd, string Title, string Process, int ProcessId, int X, int Y, int Width, int Height);

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(ControlRequest))]
    [JsonSerializable(typeof(ControlResponse))]
    internal sealed partial class ControlJsonContext : JsonSerializerContext
    {
    }
}
