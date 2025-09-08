using System.Threading.Tasks;

namespace Imagino.Api.Services
{
    public interface IEmailSender
    {
        Task<bool> SendAsync(string to, string subject, string htmlBody, string? textBody = null);
    }
}
