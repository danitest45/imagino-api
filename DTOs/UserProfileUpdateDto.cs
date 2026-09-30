namespace Imagino.Api.DTOs;

public class UserProfileUpdateDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
}
