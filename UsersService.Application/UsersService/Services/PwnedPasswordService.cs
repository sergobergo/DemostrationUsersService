using Microsoft.Extensions.Logging;
using System.Text;
using UsersService.Application.UsersService.Abstractions;

namespace UsersService.Application.UsersService.Services
{
    public class PwnedPasswordService : IPwnedPasswordService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PwnedPasswordService> _logger;

        public PwnedPasswordService(
            HttpClient httpClient,
            ILogger<PwnedPasswordService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> IsPasswordPwned(string password, CancellationToken ct)
        {
            var sha1 = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var hashPrefix = Convert.ToHexString(sha1)[..5];

            var requestUrl = $"https://api.pwnedpasswords.com/range/{hashPrefix}";
            var response = await _httpClient.GetAsync(requestUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Pawned Service unavailable.");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var hashSuffix = Convert.ToHexString(sha1)[5..];

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Any(line =>
                line.StartsWith(hashSuffix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
