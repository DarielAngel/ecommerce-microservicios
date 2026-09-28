using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ecommerce.Notifications.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Notifications.Infrastructure.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient httpClient, IOptions<ResendSettings> settings, ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    private record SendEmailRequest(string from, string[] to, string subject, string html);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            // Falla clara y explícita (igual que con PayPal sin credenciales), en vez de un 401 confuso.
            throw new EmailSendException(
                "Falta la API key de Resend (Resend__ApiKey / RESEND_API_KEY). Configúrala para poder enviar emails.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = JsonContent.Create(new SendEmailRequest(_settings.FromAddress, new[] { toEmail }, subject, htmlBody));

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailSendException("No se pudo contactar a Resend (red).", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Resend rechazó el envío a {Email} ({Status}): {Body}", toEmail, (int)response.StatusCode, body);
            throw new EmailSendException($"Resend rechazó el envío ({(int)response.StatusCode}): {body}");
        }

        _logger.LogInformation("Email '{Subject}' enviado a {Email} vía Resend.", subject, toEmail);
    }
}
