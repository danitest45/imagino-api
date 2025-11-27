using Imagino.Api.Controllers;
using Imagino.Api.Settings;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Imagino.Api.Services
{
    public class ResendEmailSender : IEmailSender
    {
        private readonly HttpClient _http;
        private readonly EmailSettings _settings;
        private readonly string _apiKey;
        private readonly ILogger<BillingController> _logger;

        public ResendEmailSender(HttpClient http, IConfiguration config, IOptions<EmailSettings> options, ILogger<BillingController> logger)
        {
            _http = http;
            _apiKey = config["RESEND__API_KEY"] ?? config["RESEND:ApiKey"] ?? config["RESEND_API_KEY"] ?? string.Empty;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string to, string subject, string htmlBody, string? textBody = null)
        {
            var payload = new
            {
                from = string.IsNullOrEmpty(_settings.FromName) ? _settings.From : $"{_settings.FromName} <{_settings.From}>",
                to,
                subject,
                html = htmlBody,
                text = textBody
            };
            var json = JsonSerializer.Serialize(payload);
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            req.Headers.Add("Authorization", $"Bearer {_apiKey}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
    }
}
