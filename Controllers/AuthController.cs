using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services;
using Imagino.Api.DTOs;
using Imagino.Api.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly IUserService _userService;
        private readonly IJwtService _jwt;
        private readonly IConfiguration _config;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly FrontendSettings _frontendSettings;
        private readonly IOptions<RefreshTokenCookieSettings> _cookieSettings;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTokenRepository _emailTokens;
        private readonly EmailSettings _emailSettings;
        private readonly IGoogleAuthHelper _googleAuthHelper;

        public AuthController(IUserRepository users, IUserService userService, IJwtService jwt, IConfiguration config, IRefreshTokenRepository refreshTokens, IOptions<FrontendSettings> frontendSettings, IOptions<RefreshTokenCookieSettings> cookieSettings, IEmailSender emailSender, IEmailTokenRepository emailTokens, IOptions<EmailSettings> emailSettings, IGoogleAuthHelper googleAuthHelper)
        {
            _users = users;
            _userService = userService;
            _jwt = jwt;
            _config = config;
            _refreshTokens = refreshTokens;
            _frontendSettings = frontendSettings.Value;
            _cookieSettings = cookieSettings;
            _emailSender = emailSender;
            _emailTokens = emailTokens;
            _emailSettings = emailSettings.Value;
            _googleAuthHelper = googleAuthHelper;
        }

        public record RegisterRequest(string Email, string Password, string? Username, string? PhoneNumber, SubscriptionType Subscription, int Credits);
        public record LoginRequest(string Email, string Password);
        public record ResendVerificationRequest(string Email);
        public record VerifyEmailRequest(string Token);
        public record ForgotPasswordRequest(string Email);
        public record ResetPasswordRequest(string Token, string NewPassword);

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var existingEmail = await _users.GetByEmailAsync(request.Email);
            if (existingEmail != null)
                return BadRequest(new { message = "Email already in use" });

            try
            {
                var dto = new CreateUserDto
                {
                    Email = request.Email,
                    Password = request.Password,
                    Username = request.Username,
                    PhoneNumber = request.PhoneNumber,
                    Subscription = request.Subscription,
                    Credits = request.Credits
                };

                var user = await _userService.CreateAsync(dto);

                var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                await _emailTokens.CreateAsync(user.Id!, "verify_email", raw, TimeSpan.FromMinutes(60), HttpContext.Connection.RemoteIpAddress?.ToString());
                var link = $"{_frontendSettings.BaseUrl}/verify?token={raw}";
                var path = _emailSettings.Template.Verify ?? "EmailTemplates/VerifyEmail.html";
                var template = System.IO.File.ReadAllText(path);
                var html = template.Replace("{verifyLink}", link);
                await _emailSender.SendAsync(user.Email!, "Confirme seu e-mail", html);

                return StatusCode(201, new { message = "User created. Please verify your email." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null || user.EmailVerified)
                return Ok();

            var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await _emailTokens.CreateAsync(user.Id!, "verify_email", raw, TimeSpan.FromMinutes(60), ip);
            var link = $"{_frontendSettings.BaseUrl}/verify?token={raw}";
            var path = _emailSettings.Template.Verify ?? "EmailTemplates/VerifyEmail.html";
            var template = System.IO.File.ReadAllText(path);
            var html = template.Replace("{verifyLink}", link);
            await _emailSender.SendAsync(user.Email!, "Confirme seu e-mail", html);

            return Ok();
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var token = await _emailTokens.GetActiveByRawTokenAsync("verify_email", request.Token);
            if (token == null)
                return BadRequest(new { title = "Invalid token", code = "TOKEN_INVALID" });

            if (token.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { title = "Token expired", code = "TOKEN_EXPIRED" });

            if (token.ConsumedAt != null)
                return BadRequest(new { title = "Token consumed", code = "TOKEN_CONSUMED" });

            var user = await _users.GetByIdAsync(token.UserId);
            if (user == null)
                return BadRequest(new { title = "Invalid token", code = "TOKEN_INVALID" });

            user.EmailVerified = true;
            user.VerifiedAt = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            await _emailTokens.InvalidateByUserAsync(user.Id!, "verify_email");

            return Ok();
        }

        [HttpPost("password/forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null)
                return Ok();

            var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await _emailTokens.CreateAsync(user.Id!, "reset_password", raw, TimeSpan.FromMinutes(30), ip);
            var link = $"{_frontendSettings.BaseUrl}/reset-password?token={raw}";
            var path = _emailSettings.Template.Reset ?? "EmailTemplates/ResetPassword.html";
            var template = System.IO.File.ReadAllText(path);
            var html = template.Replace("{resetLink}", link);
            await _emailSender.SendAsync(user.Email!, "Redefinir senha", html);
            return Ok();
        }

        [HttpPost("password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var token = await _emailTokens.GetActiveByRawTokenAsync("reset_password", request.Token);
            if (token == null)
                return BadRequest(new { title = "Invalid token", code = "TOKEN_INVALID" });
            if (token.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { title = "Token expired", code = "TOKEN_EXPIRED" });
            if (token.ConsumedAt != null)
                return BadRequest(new { title = "Token consumed", code = "TOKEN_CONSUMED" });

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { title = "Weak password", code = "WEAK_PASSWORD" });

            var user = await _users.GetByIdAsync(token.UserId);
            if (user == null)
                return BadRequest(new { title = "Invalid token", code = "TOKEN_INVALID" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _users.UpdateAsync(user);
            await _emailTokens.InvalidateByUserAsync(user.Id!, "reset_password");
            await _refreshTokens.DeleteByUserIdAsync(user.Id!);

            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null) return Unauthorized();

            var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!valid) return Unauthorized();

            if (!user.EmailVerified)
                return StatusCode(403, new { title = "Email not verified", code = "EMAIL_NOT_VERIFIED" });

            var token = _jwt.GenerateToken(user.Id, user.Email);
            var refreshToken = Guid.NewGuid().ToString("N");
            await _refreshTokens.CreateAsync(new RefreshToken
            {
                UserId = user.Id!,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            var settings = _cookieSettings.Value;
            var cookieOptions = new CookieOptions
            {
                HttpOnly = settings.HttpOnly,
                Secure = settings.SameSite.Equals("None", StringComparison.OrdinalIgnoreCase) ? true : settings.Secure,
                SameSite = Enum.Parse<SameSiteMode>(settings.SameSite, true),
                Expires = DateTime.UtcNow.AddDays(settings.ExpiresDays)
            };
            if (!string.IsNullOrWhiteSpace(settings.Domain))
            {
                cookieOptions.Domain = settings.Domain;
            }
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
            return Ok(new { token, username = user.Username });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var oldToken))
                return Unauthorized();

            var existing = await _refreshTokens.GetByTokenAsync(oldToken);
            if (existing == null || existing.ExpiresAt <= DateTime.UtcNow)
                return Unauthorized();

            var user = await _users.GetByIdAsync(existing.UserId);
            if (user == null)
                return Unauthorized();

            await _refreshTokens.DeleteAsync(oldToken);
            var newRefresh = Guid.NewGuid().ToString("N");
            await _refreshTokens.CreateAsync(new RefreshToken
            {
                UserId = user.Id!,
                Token = newRefresh,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            var settings = _cookieSettings.Value;
            var cookieOptions = new CookieOptions
            {
                HttpOnly = settings.HttpOnly,
                Secure = settings.SameSite.Equals("None", StringComparison.OrdinalIgnoreCase) ? true : settings.Secure,
                SameSite = Enum.Parse<SameSiteMode>(settings.SameSite, true),
                Expires = DateTime.UtcNow.AddDays(settings.ExpiresDays)
            };
            if (!string.IsNullOrWhiteSpace(settings.Domain))
            {
                cookieOptions.Domain = settings.Domain;
            }
            Response.Cookies.Append("refreshToken", newRefresh, cookieOptions);

            var token = _jwt.GenerateToken(user.Id, user.Email);
            return Ok(new { token, username = user.Username });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
            {
                await _refreshTokens.DeleteAsync(refreshToken);
                Response.Cookies.Delete("refreshToken");
            }
            return Ok();
        }

        // Endpoint de callback do Google
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code)
        {
            // Troca o code pelo id_token usando biblioteca Google.Apis.Auth
            // Você precisará do clientId e clientSecret no appsettings.json
            var clientId = _config["Google:ClientId"];
            var clientSecret = _config["Google:ClientSecret"];
            var redirectUri = _config["Google:RedirectUri"];

            var payload = await _googleAuthHelper.ExchangeCodeForIdTokenAsync(code, clientId, clientSecret, redirectUri);
            if (payload == null) return Unauthorized();

            // payload.Subject é o Google User ID (sub)
            var user = await _users.GetByGoogleIdAsync(payload.Subject);
            if (user == null)
            {
                // cria novo usuário
                var username = await _userService.GenerateUsernameFromEmailAsync(payload.Email);
                user = new User
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    Username = username,
                    Subscription = SubscriptionType.Free,
                    Credits = 0,
                    EmailVerified = payload.EmailVerified,
                    VerifiedAt = payload.EmailVerified ? DateTime.UtcNow : null
                };
                await _users.CreateAsync(user);
            }
            else if (payload.EmailVerified && !user.EmailVerified)
            {
                user.EmailVerified = true;
                user.VerifiedAt = DateTime.UtcNow;
                await _users.UpdateAsync(user);
            }

            var token = _jwt.GenerateToken(user.Id, user.Email);
            var refreshToken = Guid.NewGuid().ToString("N");
            var settings = _cookieSettings.Value;
            await _refreshTokens.CreateAsync(new RefreshToken
            {
                UserId = user.Id!,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(settings.ExpiresDays)
            });
            var cookieOptions = new CookieOptions
            {
                HttpOnly = settings.HttpOnly,
                Secure = settings.SameSite.Equals("None", StringComparison.OrdinalIgnoreCase) ? true : settings.Secure,
                SameSite = Enum.Parse<SameSiteMode>(settings.SameSite, true),
                Expires = DateTime.UtcNow.AddDays(settings.ExpiresDays)
            };
            if (!string.IsNullOrWhiteSpace(settings.Domain))
            {
                cookieOptions.Domain = settings.Domain;
            }
            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

            var redirectUrl = $"{_frontendSettings.BaseUrl}/google-auth?token={token}&username={user.Username}";
            return Redirect(redirectUrl);
        }
    }
}
