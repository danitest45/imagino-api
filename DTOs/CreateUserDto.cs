namespace Imagino.Api.DTOs
{
    public class CreateUserDto
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string? Username { get; set; }
        public string? PhoneNumber { get; set; }
    }
}

