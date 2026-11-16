
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using UserService.IntegrationTests.Infrastructure.MailPit.Mappers;
using UserService.IntegrationTests.Infrastructure.MailPit.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UserService.IntegrationTests.Infrastructure.MailPit
{
    public class MailPitClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _oldMessages = new();

        public MailPitClient(string baseUrl)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task ClearAsync()
        {
            _oldMessages.Clear();
            try
            {
                await _httpClient.DeleteAsync("/api/v1/messages");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"MailPit clear failed: {ex.Message}");
            }
        }

        public async Task<List<EmailDto>> GetAllCodesWithTargetEmailsAsync(
            string email,
            int timeoutSeconds = 10)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var messages = await GetMessagesForEmailAsync(email);

                    if (messages.Count == 0)
                    {
                        await Task.Delay(500);
                        continue;
                    }

                    var lastMessage = messages.First();

                    if (_oldMessages.TryGetValue(email, out var oldText))
                    {
                        if (lastMessage.Text != oldText)
                        {
                            
                            _oldMessages[email] = lastMessage.Text;
                            return messages;
                        }
                    }
                    else
                    {
                        
                        _oldMessages[email] = lastMessage.Text;
                        return messages;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(500);
            }

            return new List<EmailDto>();
        }

        public async Task<EmailDto?> GetLastEmailAsync(
            string email,
            int timeoutSeconds = 10)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var json = await _httpClient.GetStringAsync(
                        $"/api/v1/search?query=to:{email}");
                    Console.WriteLine($"MailPit response: {json}");

                    var response = JsonConvert.DeserializeObject<MailPitResponse>(json);

                    if (response?.Messages.Count > 0)
                    {
                        return response.ToEmailDtoList().First();
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(500);
            }

            return null;
        }

        private async Task<List<EmailDto>> GetAllMessagesAsync()
        {
            var json = await _httpClient.GetStringAsync("/api/v1/messages");
            var response = JsonConvert.DeserializeObject<MailPitResponse>(json);
            return response?.ToEmailDtoList() ?? new List<EmailDto>();
        }

        private async Task<List<EmailDto>> GetMessagesForEmailAsync(string email)
        {
            var json = await _httpClient.GetStringAsync(
                $"/api/v1/search?query=to:{email}");
            var response = JsonConvert.DeserializeObject<MailPitResponse>(json);
            return response?.ToEmailDtoList() ?? new List<EmailDto>();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
