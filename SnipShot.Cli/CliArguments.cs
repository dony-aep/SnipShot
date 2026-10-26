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
            "Uso: snipshot status | snipshot list-windows | "
            + "snipshot capture fullscreen|window|region|freeform "
            + "[--hwnd <h> | --title <texto> | --process <nombre> | --interactive] [--rect x,y,ancho,alto] "
            + "[--out <archivo.png>] [--clipboard] [--delay <segundos>] [--timeout <segundos>]";

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
                case [ControlProtocol.Commands.Status or ControlProtocol.Commands.ListWindows]:
                    request = new ControlRequest { Command = args[0] };
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
            long? hwnd = null;
            string? title = null;
            string? process = null;
            var interactive = false;
            RegionRect? rect = null;
            var timeoutSeconds = 0;

            for (var i = 0; i < options.Length; i++)
            {
                switch (options[i])
                {
                    case "--clipboard":
                        clipboard = true;
                        break;

                    case "--interactive":
                        interactive = true;
                        break;

                    case "--rect" when i + 1 < options.Length:
                        if (!TryParseRect(options[++i], out rect))
                        {
                            error = $"La región debe tener la forma x,y,ancho,alto con números enteros: {options[i]}.";
                            return false;
                        }
                        break;

                    case "--timeout" when i + 1 < options.Length:
                        if (!int.TryParse(options[++i], NumberStyles.None, CultureInfo.InvariantCulture, out timeoutSeconds))
                        {
                            error = $"El tiempo de espera debe ser un número entero de segundos: {options[i]}.";
                            return false;
                        }
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

                    case "--hwnd" when i + 1 < options.Length:
                        if (!TryParseHwnd(options[++i], out var parsedHwnd))
                        {
                            error = $"El handle debe ser un número, decimal o hexadecimal con 0x: {options[i]}.";
                            return false;
                        }
                        hwnd = parsedHwnd;
                        break;

                    case "--title" when i + 1 < options.Length:
                        title = options[++i];
                        break;

                    case "--process" when i + 1 < options.Length:
                        process = options[++i];
                        break;

                    case "--out" or "--delay" or "--hwnd" or "--title" or "--process" or "--rect" or "--timeout":
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
                Hwnd = hwnd,
                Title = title,
                Process = process,
                Interactive = interactive,
                Rect = rect,
                TimeoutSeconds = timeoutSeconds,
            };
            error = null;
            return true;
        }

        // X e Y admiten signo: un monitor a la izquierda o encima del principal tiene
        // coordenadas negativas. Que ancho y alto sean positivos lo comprueba la app.
        private static bool TryParseRect(string value, [NotNullWhen(true)] out RegionRect? rect)
        {
            rect = null;
            var parts = value.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            var numbers = new int[4];
            for (var i = 0; i < 4; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    return false;
                }
            }

            rect = new RegionRect(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        // list-windows los da en decimal, pero otras herramientas (Spy++, winapp) los muestran
        // en hexadecimal.
        private static bool TryParseHwnd(string value, out long hwnd)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.TryParse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out hwnd)
                : long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out hwnd);
        }
    }
}
