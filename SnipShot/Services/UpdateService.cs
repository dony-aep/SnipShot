using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;

namespace SnipShot.Services
{
    /// <summary>
    /// Servicio para verificar y gestionar actualizaciones de la aplicación desde GitHub Releases.
    /// </summary>
    public class UpdateService
    {
        private const string GitHubOwner = "dony-aep";
        private const string GitHubRepo = "SnipShot";
        private const string GitHubApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        
        // El User-Agent lo exige la API de GitHub. Se configura una sola vez aquí en vez de
        // en cada llamada: mutar DefaultRequestHeaders no es seguro si hay peticiones en curso.
        // El timeout por defecto de HttpClient son 100 s, demasiado para no dejar la UI colgada.
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestHeaders = { { "User-Agent", "SnipShot-UpdateChecker" } }
        };
        
        /// <summary>
        /// Versión actual de la aplicación (leída del Package.appxmanifest automáticamente).
        /// Solo necesito actualizar la versión en Package.appxmanifest.
        /// </summary>
        public static Version CurrentVersion
        {
            get
            {
                try
                {
                    var packageVersion = Package.Current.Id.Version;
                    return new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
                }
                catch
                {
                    // Fallback para debugging sin paquete instalado
                    return new Version("1.0.0.0");
                }
            }
        }

        /// <summary>
        /// Resultado de la verificación de actualizaciones
        /// </summary>
        public class UpdateCheckResult
        {
            /// <summary>
            /// Indica si hay una actualización disponible
            /// </summary>
            public bool IsUpdateAvailable { get; set; }
            
            /// <summary>
            /// Versión más reciente disponible
            /// </summary>
            public Version? LatestVersion { get; set; }
            
            /// <summary>
            /// URL de descarga del instalador
            /// </summary>
            public string? DownloadUrl { get; set; }
            
            /// <summary>
            /// URL de la página del release en GitHub
            /// </summary>
            public string? ReleasePageUrl { get; set; }
            
            /// <summary>
            /// Notas del release
            /// </summary>
            public string? ReleaseNotes { get; set; }
            
            /// <summary>
            /// Mensaje de error si la verificación falló
            /// </summary>
            public string? ErrorMessage { get; set; }
            
            /// <summary>
            /// Indica si la verificación fue exitosa
            /// </summary>
            public bool Success => string.IsNullOrEmpty(ErrorMessage);
        }

        /// <summary>
        /// Verifica si hay actualizaciones disponibles consultando GitHub Releases.
        /// </summary>
        /// <returns>Resultado de la verificación con información del release más reciente</returns>
        public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult
                    {
                        ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                            ? "No se encontraron releases publicados."
                            : $"Error al contactar GitHub: {response.StatusCode}"
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                return ParseReleaseResponse(json, CurrentVersion);
            }
            catch (TaskCanceledException)
            {
                // HttpClient traduce el vencimiento del Timeout en una cancelación,
                // no en HttpRequestException.
                return new UpdateCheckResult
                {
                    ErrorMessage = "La comprobación tardó demasiado. Revisa tu conexión."
                };
            }
            catch (HttpRequestException ex)
            {
                return new UpdateCheckResult
                {
                    ErrorMessage = $"Error de conexión: {ex.Message}"
                };
            }
            catch (JsonException ex)
            {
                return new UpdateCheckResult
                {
                    ErrorMessage = $"Error al procesar respuesta: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    ErrorMessage = $"Error inesperado: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Interpreta la respuesta JSON de la API de releases de GitHub.
        /// </summary>
        /// <remarks>
        /// Separado de <see cref="CheckForUpdatesAsync"/> para que la lógica de parseo y
        /// comparación de versiones se pueda probar sin depender de la red ni del paquete
        /// instalado. Todos los campos se leen con TryGetProperty porque la respuesta viene de un
        /// servicio externo y un cambio en su forma no debe tirar la comprobación.
        /// </remarks>
        /// <param name="json">Cuerpo JSON devuelto por la API.</param>
        /// <param name="currentVersion">Versión instalada contra la que comparar.</param>
        internal static UpdateCheckResult ParseReleaseResponse(string json, Version currentVersion)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement))
            {
                return new UpdateCheckResult
                {
                    ErrorMessage = "La respuesta de GitHub no incluye la versión del release."
                };
            }

            var tagName = tagElement.GetString() ?? "";
            var releasePageUrl = root.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString() ?? ""
                : ReleasesPageUrl;
            var releaseNotes = root.TryGetProperty("body", out var bodyElement)
                ? bodyElement.GetString() ?? ""
                : "";

            // Parsear versión (eliminar 'v' si existe)
            var versionString = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(versionString, out var latestVersion))
            {
                return new UpdateCheckResult
                {
                    ErrorMessage = $"No se pudo parsear la versión: {tagName}"
                };
            }

            // Buscar el asset del instalador (.msixbundle)
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameElement))
                    {
                        continue;
                    }

                    var assetName = nameElement.GetString() ?? "";
                    if (assetName.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase)
                        && asset.TryGetProperty("browser_download_url", out var urlElement))
                    {
                        downloadUrl = urlElement.GetString();
                        break;
                    }
                }
            }

            // Comparar versiones. Un tag como "v1.2" se parsea con Build/Revision en -1,
            // que no es comparable con los 4 componentes que siempre trae el paquete;
            // se normaliza para que la comparación sea entre iguales.
            var normalizedLatest = new Version(
                latestVersion.Major,
                latestVersion.Minor,
                Math.Max(latestVersion.Build, 0),
                Math.Max(latestVersion.Revision, 0));

            return new UpdateCheckResult
            {
                IsUpdateAvailable = normalizedLatest > currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = downloadUrl,
                ReleasePageUrl = releasePageUrl,
                ReleaseNotes = releaseNotes
            };
        }

        /// <summary>
        /// Abre la página de descarga del release en el navegador predeterminado.
        /// </summary>
        /// <param name="url">URL de la página del release o descarga directa</param>
        public static async Task OpenDownloadPageAsync(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                await Launcher.LaunchUriAsync(new Uri(url));
            }
        }

        /// <summary>
        /// Obtiene la URL de la página de releases del repositorio.
        /// </summary>
        public static string ReleasesPageUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";
    }
}
