using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication
{
    /// <summary>
    /// Proveedor de tokens para Azure AD (Microsoft Entra ID) usando Client Credentials Grant.
    /// Mantiene en caché el token y su expiración, y lo renueva de forma transparente cuando es necesario.
    /// </summary>
    public class AzureAdTokenProvider
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private string? _cachedToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        private readonly string _instance;
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _audience;
        private readonly string? _scope;

        public AzureAdTokenProvider(string? configPath = null)
        {
            configPath ??= FindConfigPath("appsettings.Development.json");
            if (!File.Exists(configPath))
            {
                configPath = FindConfigPath("appsettings.json");
            }

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo de configuración en ninguna ruta lógica. Último intento: {configPath}");
            }

            string jsonContent = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(jsonContent);

            if (!doc.RootElement.TryGetProperty("AzureAd", out var azureAdElement))
            {
                throw new KeyNotFoundException("No se encontró la sección 'AzureAd' en el archivo de configuración.");
            }

            _instance = azureAdElement.GetProperty("Instance").GetString()
                ?? throw new InvalidOperationException("AzureAd:Instance no está definido o es nulo.");
            _tenantId = azureAdElement.GetProperty("TenantId").GetString()
                ?? throw new InvalidOperationException("AzureAd:TenantId no está definido o es nulo.");
            _clientId = azureAdElement.GetProperty("ClientId").GetString()
                ?? throw new InvalidOperationException("AzureAd:ClientId no está definido o es nulo.");
            _clientSecret = azureAdElement.GetProperty("ClientSecret").GetString()
                ?? throw new InvalidOperationException("AzureAd:ClientSecret no está definido o es nulo.");
            _audience = azureAdElement.GetProperty("Audience").GetString()
                ?? throw new InvalidOperationException("AzureAd:Audience no está definido o es nulo.");

            if (azureAdElement.TryGetProperty("Scope", out var scopeElement))
            {
                _scope = scopeElement.GetString();
            }
        }

        /// <summary>
        /// Obtiene un token de acceso válido. Si el token está en caché y no ha expirado, se devuelve el de la caché.
        /// </summary>
        /// <param name="scope">El scope opcional. Si es nulo, se utiliza '[Audience]/.default'.</param>
        /// <param name="forceRefresh">Si es verdadero, fuerza la obtención de un token nuevo ignorando la caché.</param>
        /// <returns>El token de acceso JWT.</returns>
        public async Task<string> GetAccessTokenAsync(string? scope = null, bool forceRefresh = false)
        {
            // Usamos un buffer de 5 minutos antes de la expiración real para evitar que expire a mitad de una transacción.
            if (!forceRefresh && _cachedToken != null && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                return _cachedToken;
            }

            await _semaphore.WaitAsync();
            try
            {
                // Doble chequeo después de adquirir el semáforo para evitar llamadas concurrentes redundantes
                if (!forceRefresh && _cachedToken != null && DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
                {
                    return _cachedToken;
                }

                await FetchTokenAsync(scope);
                return _cachedToken!;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task FetchTokenAsync(string? scope)
        {
            string baseUrl = _instance.EndsWith("/") ? _instance : _instance + "/";
            string requestUrl = $"{baseUrl}{_tenantId}/oauth2/v2.0/token";

            // Si no se pasa un scope explícito al método, se usa el de la configuración (o '[Audience]/.default' como fallback)
            if (string.IsNullOrEmpty(scope))
            {
                scope = _scope ?? (_audience.EndsWith("/.default") ? _audience : $"{_audience}/.default");
            }

            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "scope", scope }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new FormUrlEncodedContent(requestBody)
            };

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error al obtener el token de Azure AD. Status: {response.StatusCode}. Detalles: {errorContent}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            _cachedToken = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("La respuesta de Azure AD no contiene el campo 'access_token'.");

            int expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn);
        }

        private static string FindConfigPath(string filename)
        {
            // 1. Intentar en el directorio de ejecución actual
            string path = Path.Combine(Directory.GetCurrentDirectory(), filename);
            if (File.Exists(path)) return path;

            // 2. Intentar en el directorio base de la aplicación (salida bin)
            path = Path.Combine(AppContext.BaseDirectory, filename);
            if (File.Exists(path)) return path;

            // 3. Intentar subiendo niveles desde el directorio base (para entornos de desarrollo/test)
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                path = Path.Combine(dir, filename);
                if (File.Exists(path)) return path;
                dir = Path.GetDirectoryName(dir);
            }

            return filename;
        }
    }
}
