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

        public AuthController(IUserRepository users, IUserService userService, IJwtService jwt, IConfiguration config, IRefreshTokenRepository refreshTokens, IOptions<FrontendSettings> frontendSettings, IOptions<RefreshTokenCookieSettings> cookieSettings)
        {
            _users = users;
            _userService = userService;
            _jwt = jwt;
            _config = config;
            _refreshTokens = refreshTokens;
            _frontendSettings = frontendSettings.Value;
            _cookieSettings = cookieSettings;
        }

        public record RegisterRequest(string Email, string Password, string? Username, string? PhoneNumber, SubscriptionType Subscription, int Credits);
        public record LoginRequest(string Email, string Password);

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
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null) return Unauthorized();

            var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!valid) return Unauthorized();

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

            var payload = await GoogleAuthHelper.ExchangeCodeForIdTokenAsync(code, clientId, clientSecret, redirectUri);
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
                    Credits = 0
                };
                await _users.CreateAsync(user);
            }

            var token = _jwt.GenerateToken(user.Id, user.Email);

            var redirectUrl = $"{_frontendSettings.BaseUrl}/google-auth?token={token}&username={user.Username}";
            return Redirect(redirectUrl);
        }
    }
}
