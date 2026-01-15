using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
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
        
        private static readonly HttpClient _httpClient = new();
        
        /// <summary>
        /// Versión actual de la aplicación (extraída del Package.appxmanifest)
        /// </summary>
        public static Version CurrentVersion => new("1.0.0.0");

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
                // Configurar el User-Agent requerido por GitHub API
                if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "SnipShot-UpdateChecker");
                }

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
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Extraer información del release
                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var releasePageUrl = root.GetProperty("html_url").GetString() ?? "";
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
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                // Comparar versiones
                var isUpdateAvailable = latestVersion > CurrentVersion;

                return new UpdateCheckResult
                {
                    IsUpdateAvailable = isUpdateAvailable,
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleasePageUrl = releasePageUrl,
                    ReleaseNotes = releaseNotes
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
