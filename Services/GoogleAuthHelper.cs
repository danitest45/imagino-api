// Services/GoogleAuthHelper.cs
using Google.Apis.Auth;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Imagino.Api.Services
{
    public static class GoogleAuthHelper
    {
        public static async Task<GoogleJsonWebSignature.Payload> ExchangeCodeForIdTokenAsync(
            string code, string clientId, string clientSecret, string redirectUri)
        {
            using var http = new HttpClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("code", code),
                new KeyValuePair<string,string>("client_id", clientId),
                new KeyValuePair<string,string>("client_secret", clientSecret),
                new KeyValuePair<string,string>("redirect_uri", redirectUri),
                new KeyValuePair<string,string>("grant_type", "authorization_code")
            });

            var resp = await http.PostAsync("https://oauth2.googleapis.com/token", content);
            if (!resp.IsSuccessStatusCode) return null;

            var payload = JObject.Parse(await resp.Content.ReadAsStringAsync());
            var idToken = payload["id_token"]?.ToString();

            return await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
    }
}
