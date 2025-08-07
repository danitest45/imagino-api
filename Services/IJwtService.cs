namespace Imagino.Api.Services
{
    public interface IJwtService
    {
        string GenerateToken(string userId, string email);
    }
}