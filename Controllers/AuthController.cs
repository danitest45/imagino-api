using Imagino.Api.Models;
using Imagino.Api.Repository;
using Imagino.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Imagino.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _users;
        private readonly IJwtService _jwt;
        private readonly IConfiguration _config;

        public AuthController(IUserRepository users, IJwtService jwt, IConfiguration config)
        {
            _users = users;
            _jwt = jwt;
            _config = config;
        }

        public record RegisterRequest(string Email, string Password);
        public record LoginRequest(string Email, string Password);

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var existing = await _users.GetByEmailAsync(request.Email);
            if (existing != null)
                return BadRequest(new { message = "Email already in use" });

            var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = hash
            };

            await _users.CreateAsync(user);

            var token = _jwt.GenerateToken(user.Id, user.Email);
            return Ok(new { token });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _users.GetByEmailAsync(request.Email);
            if (user == null) return Unauthorized();

            var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!valid) return Unauthorized();

            var token = _jwt.GenerateToken(user.Id, user.Email);
            return Ok(new { token });
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
                user = new User
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email
                };
                await _users.CreateAsync(user);
            }

            var token = _jwt.GenerateToken(user.Id, user.Email);
            return Ok(new { token });
        }
    }
}
