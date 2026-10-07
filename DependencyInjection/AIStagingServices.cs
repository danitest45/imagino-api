using Imagino.Api.Repository;
using Imagino.Api.Security;
using Imagino.Api.Services;
using Imagino.Api.Services.Storage;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Imagino.Api.DependencyInjection;

public static class AIStagingServices
{
    public static void AddAIStagingServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<R2StorageSettings>(config.GetSection("R2Settings"));
        services.Configure<EmailSettings>(_ => { });
        services.AddTransient<IUserRepository, UserRepository>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IJwtService, JwtService>();
        services.AddTransient<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddTransient<IEmailTokenRepository, EmailTokenRepository>();
        services.AddSingleton<IStorageService, R2StorageService>();
        services.AddSingleton<GoogleOAuthState>();
        services.AddSingleton<IGoogleOAuthClient, DisabledAIGoogleOAuth>();
        services.AddSingleton<IEmailSender, DisabledAIEmail>();
        // No billing gateway/service, legacy generators, webhooks or paid provider clients.
    }
    private sealed class DisabledAIGoogleOAuth : IGoogleOAuthClient
    {
        public Task<GoogleIdentity?> ExchangeAsync(string code, string verifier, string nonce) =>
            throw new InvalidOperationException("Google OAuth is disabled in AI staging.");
    }
    private sealed class DisabledAIEmail : IEmailSender
    {
        public Task<bool> SendAsync(string to, string subject, string htmlBody, string? textBody = null) =>
            throw new InvalidOperationException("Email sending is disabled in AI staging.");
    }
}

public sealed class AIStagingControllerConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers.ToList())
        {
            var allowed = controller.ControllerType.Name switch {
                "GenerationController" or "HealthController" => null,
                "AuthController" => new[] { "Login", "Refresh", "Logout" },
                "UsersController" => new[] { "GetMe", "GetCredits" },
                _ => Array.Empty<string>()
            };
            if (allowed == null) continue;
            foreach (var action in controller.Actions.Where(a => !allowed.Contains(a.ActionName)).ToList()) controller.Actions.Remove(action);
            if (controller.Actions.Count == 0) application.Controllers.Remove(controller);
        }
    }
}
