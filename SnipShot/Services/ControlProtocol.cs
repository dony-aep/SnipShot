using System.Diagnostics.CodeAnalysis;
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
        }

        public static class ErrorCodes
        {
            public const string InvalidRequest = "invalid_request";
            public const string UnsupportedVersion = "unsupported_version";
            public const string UnknownCommand = "unknown_command";
            public const string NotRunning = "not_running";
            public const string AccessDenied = "access_denied";
            public const string Disabled = "disabled";
            public const string Internal = "internal";
        }

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

            if (parsed.Command != Commands.Status)
            {
                error = new ControlError(ErrorCodes.UnknownCommand, $"Comando desconocido: {parsed.Command}.");
                return false;
            }

            request = parsed;
            error = null;
            return true;
        }

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
                ErrorCodes.InvalidRequest or ErrorCodes.UnsupportedVersion or ErrorCodes.UnknownCommand => 2,
                ErrorCodes.NotRunning or ErrorCodes.AccessDenied or ErrorCodes.Disabled => 4,
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
    }

    internal sealed class ControlResponse
    {
        [JsonRequired]
        public int Version { get; init; } = ControlProtocol.Version;

        public bool Ok { get; init; }

        public ControlError? Error { get; init; }

        public StatusInfo? Status { get; init; }

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

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(ControlRequest))]
    [JsonSerializable(typeof(ControlResponse))]
    internal sealed partial class ControlJsonContext : JsonSerializerContext
    {
    }
}
