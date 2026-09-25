using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Ecommerce.Payments.Application.Common;
using Microsoft.Extensions.Options;

namespace Ecommerce.Payments.Infrastructure.PayPal;

/// <summary>
/// PayPal usa OAuth2 client_credentials: el token dura ~9 horas, así que pedirlo en cada
/// request sería un desperdicio (y más lento). Se cachea en memoria dentro de esta instancia
/// singleton, protegido con un semáforo para que solicitudes concurrentes no pidan tokens
/// duplicados innecesariamente.
/// </summary>
public class PayPalAccessTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PayPalSettings _settings;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;

    public PayPalAccessTokenProvider(IHttpClientFactory httpClientFactory, IOptions<PayPalSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    private record TokenResponse(string access_token, int expires_in);

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // Margen de 60s antes de que expire de verdad, para no arriesgarnos a usar un token
        // que vence a mitad de una llamada.
        if (_cachedToken is not null && DateTime.UtcNow < _expiresAtUtc.AddSeconds(-60))
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Doble chequeo: otro request pudo haber refrescado el token mientras esperábamos el lock.
            if (_cachedToken is not null && DateTime.UtcNow < _expiresAtUtc.AddSeconds(-60))
            {
                return _cachedToken;
            }

            if (string.IsNullOrWhiteSpace(_settings.ClientId) || string.IsNullOrWhiteSpace(_settings.ClientSecret))
            {
                throw new PayPalCommunicationException(
                    "Faltan las credenciales de PayPal (ClientId/ClientSecret). Configúralas antes de usar Pagos.");
            }

            // Se usa el named client "PayPal-OAuth" (registrado en DependencyInjection) vía
            // IHttpClientFactory en vez de un HttpClient inyectado directamente — así este
            // proveedor puede ser un singleton limpio (para que el cacheo del token persista
            // entre requests) sin pelearse con el ciclo de vida que exige AddHttpClient<T>.
            var httpClient = _httpClientFactory.CreateClient("PayPal-OAuth");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            var response = await httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new PayPalCommunicationException(
                    $"PayPal rechazó la solicitud de token OAuth2 ({(int)response.StatusCode}): {errorBody}");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                ?? throw new PayPalCommunicationException("Respuesta de token de PayPal vacía o inválida.");

            _cachedToken = tokenResponse.access_token;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
