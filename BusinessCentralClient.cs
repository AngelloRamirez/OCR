using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ConsoleApplication
{
    /// <summary>
    /// Cliente para enviar peticiones HTTP a Business Central utilizando la autenticación OAuth de Azure AD.
    /// </summary>
    public class BusinessCentralClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly AzureAdTokenProvider _tokenProvider;
        
        private readonly string _baseUrl;
        private readonly string _tenantId;
        private readonly string _environment;
        private readonly string _company;

        /// <summary>
        /// Inicializa una nueva instancia del cliente de Business Central.
        /// </summary>
        /// <param name="tokenProvider">Proveedor del token Azure AD.</param>
        /// <param name="configPath">Ruta opcional al archivo de configuración.</param>
        public BusinessCentralClient(AzureAdTokenProvider tokenProvider, string? configPath = null)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

            configPath ??= FindConfigPath("appsettings.Development.json");
            if (!File.Exists(configPath))
            {
                configPath = FindConfigPath("appsettings.json");
            }

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"No se encontró el archivo de configuración. Último intento: {configPath}");
            }

            string jsonContent = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(jsonContent);

            if (!doc.RootElement.TryGetProperty("BusinessCentral", out var bcElement))
            {
                throw new KeyNotFoundException("No se encontró la sección 'BusinessCentral' en el archivo de configuración.");
            }

            _baseUrl = bcElement.GetProperty("BaseUrl").GetString()
                ?? throw new InvalidOperationException("BusinessCentral:BaseUrl no está definido o es nulo.");
            _tenantId = bcElement.GetProperty("TenantId").GetString()
                ?? throw new InvalidOperationException("BusinessCentral:TenantId no está definido o es nulo.");
            _environment = bcElement.GetProperty("Environment").GetString()
                ?? throw new InvalidOperationException("BusinessCentral:Environment no está definido o es nulo.");
            _company = bcElement.GetProperty("Company").GetString()
                ?? throw new InvalidOperationException("BusinessCentral:Company no está definido o es nulo.");
        }

        /// <summary>
        /// Envía una petición POST a Business Central para un servicio específico con el cuerpo de datos provisto.
        /// </summary>
        /// <param name="serviceName">Nombre del servicio de OData o ruta del endpoint.</param>
        /// <param name="data">Objeto que contiene los datos a enviar en formato JSON.</param>
        /// <returns>La respuesta como cadena de texto JSON.</returns>
        public async Task<string> SendRequestAsync(string serviceName, object data)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("El nombre del servicio no puede estar vacío.", nameof(serviceName));
            }

            // 1. Obtener el token de acceso
            string token = await _tokenProvider.GetAccessTokenAsync();

            // 2. Construir la URL completa
            string url = BuildUrl(serviceName);

            // 3. Preparar la petición HTTP POST
            var jsonPayload = JsonSerializer.Serialize(data);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // 4. Enviar la petición
            using var response = await _httpClient.SendAsync(request);

            // 5. Leer y retornar la respuesta
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error al enviar petición a Business Central ({response.StatusCode}). Endpoint: {url}. Detalles: {responseContent}");
            }

            return responseContent;
        }

        /// <summary>
        /// Envía una petición GET a Business Central.
        /// </summary>
        /// <param name="serviceName">Nombre del servicio de OData o ruta del endpoint.</param>
        /// <returns>La respuesta como cadena de texto JSON.</returns>
        public async Task<string> GetRequestAsync(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentException("El nombre del servicio no puede estar vacío.", nameof(serviceName));
            }

            // 1. Obtener el token de acceso
            string token = await _tokenProvider.GetAccessTokenAsync();

            // 2. Construir la URL completa
            string url = BuildUrl(serviceName);

            // 3. Preparar la petición HTTP GET
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 4. Enviar la petición
            using var response = await _httpClient.SendAsync(request);

            // 5. Leer y retornar la respuesta
            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Error al enviar petición GET a Business Central ({response.StatusCode}). Endpoint: {url}. Detalles: {responseContent}");
            }

            return responseContent;
        }



        /// <summary>
        /// Construye la URL del endpoint según el servicio provisto.
        /// Soporta tanto nombres simples de servicio (usando ODataV4) como URLs completas o rutas específicas.
        /// </summary>
        public string BuildUrl(string serviceName)
        {
            // Si el nombre del servicio ya es una URL completa, usarla directamente
            if (serviceName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                serviceName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return serviceName;
            }

            string formattedBaseUrl = _baseUrl.EndsWith("/") ? _baseUrl : _baseUrl + "/";

            // Si es una ruta relativa específica que empieza con slash, unir directamente a la BaseUrl
            if (serviceName.StartsWith("/"))
            {
                return $"{formattedBaseUrl.TrimEnd('/')}{serviceName}";
            }

            string url;
            // Si contiene "api/" o "ODataV4/", asumimos que es una ruta parcial y la anteponemos con tenant y environment
            if (serviceName.Contains("api/", StringComparison.OrdinalIgnoreCase) || 
                serviceName.Contains("ODataV4/", StringComparison.OrdinalIgnoreCase))
            {
                url = $"{formattedBaseUrl}{_tenantId}/{_environment}/{serviceName}";
            }
            else
            {
                // Por defecto, asumimos que es un nombre de servicio OData V4 estándar
                url = $"{formattedBaseUrl}{_tenantId}/{_environment}/ODataV4/{serviceName}";
            }

            // Si es un endpoint de ODataV4 y no contiene "company=", le agregamos el parámetro de compañía
            if (url.Contains("/ODataV4/", StringComparison.OrdinalIgnoreCase) && 
                !url.Contains("company=", StringComparison.OrdinalIgnoreCase))
            {
                string escapedCompany = Uri.EscapeDataString(_company);
                string separator = url.Contains("?") ? "&" : "?";
                url = $"{url}{separator}company={escapedCompany}";
            }

            return url;
        }

        private static string FindConfigPath(string filename)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), filename);
            if (File.Exists(path)) return path;

            path = Path.Combine(AppContext.BaseDirectory, filename);
            if (File.Exists(path)) return path;

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
