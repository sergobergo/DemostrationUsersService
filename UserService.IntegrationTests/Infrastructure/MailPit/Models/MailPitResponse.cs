using Newtonsoft.Json;

namespace UserService.IntegrationTests.Infrastructure.MailPit.Models
{
    public class MailPitResponse
    {
        [JsonProperty("messages")]
        public List<MailPitMessage> Messages { get; set; } = new();

        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
