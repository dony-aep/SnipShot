using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SnipShot.Services;

namespace SnipShot.Cli
{
    /// <summary>
    /// Convierte la línea de comandos en una petición del protocolo. Solo comprueba la
    /// sintaxis: qué modos y valores son válidos lo decide la app al recibir la petición.
    /// </summary>
    internal static class CliArguments
    {
        public const string Usage =
            "Uso: snipshot status | snipshot capture fullscreen [--out <archivo.png>] [--clipboard] [--delay <segundos>]";

        /// <param name="currentDirectory">Directorio contra el que se resuelven las rutas
        /// relativas. La app tiene otro directorio de trabajo, así que se envían absolutas.</param>
        public static bool TryParse(
            string[] args,
            string currentDirectory,
            [NotNullWhen(true)] out ControlRequest? request,
            [NotNullWhen(false)] out string? error)
        {
            request = null;

            switch (args)
            {
                case [ControlProtocol.Commands.Status]:
                    request = new ControlRequest { Command = ControlProtocol.Commands.Status };
                    error = null;
                    return true;

                case [ControlProtocol.Commands.Capture, var mode, .. var options] when !mode.StartsWith("--", StringComparison.Ordinal):
                    return TryParseCapture(mode, options, currentDirectory, out request, out error);

                case [ControlProtocol.Commands.Capture, ..]:
                    error = $"Falta el modo de captura. {Usage}";
                    return false;

                default:
                    error = Usage;
                    return false;
            }
        }

        private static bool TryParseCapture(
            string mode,
            string[] options,
            string currentDirectory,
            [NotNullWhen(true)] out ControlRequest? request,
            [NotNullWhen(false)] out string? error)
        {
            request = null;
            string? outPath = null;
            var clipboard = false;
            var delaySeconds = 0;

            for (var i = 0; i < options.Length; i++)
            {
                switch (options[i])
                {
                    case "--clipboard":
                        clipboard = true;
                        break;

                    case "--out" when i + 1 < options.Length:
                        outPath = Path.GetFullPath(options[++i], currentDirectory);
                        break;

                    case "--delay" when i + 1 < options.Length:
                        if (!int.TryParse(options[++i], NumberStyles.None, CultureInfo.InvariantCulture, out delaySeconds))
                        {
                            error = $"El retardo debe ser un número entero de segundos: {options[i]}.";
                            return false;
                        }
                        break;

                    case "--out" or "--delay":
                        error = $"Falta el valor de {options[i]}.";
                        return false;

                    default:
                        error = $"Opción desconocida: {options[i]}. {Usage}";
                        return false;
                }
            }

            request = new ControlRequest
            {
                Command = ControlProtocol.Commands.Capture,
                Mode = mode,
                Out = outPath,
                Clipboard = clipboard,
                DelaySeconds = delaySeconds,
            };
            error = null;
            return true;
        }
    }
}
