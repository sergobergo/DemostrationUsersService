using Newtonsoft.Json;

namespace UserService.IntegrationTests.Infrastructure.MailPit.Models
{
    public class MailPitMessage
    {
        [JsonProperty("ID")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("Subject")]
        public string Subject { get; set; } = string.Empty;
        
        [JsonProperty("Snippet")]
        public string Text{ get; set; } = string.Empty;

        [JsonProperty("From")]
        public MailPitAddress From { get; set; } = new();

        [JsonProperty("To")]
        public List<MailPitAddress> To { get; set; } = new();

        [JsonProperty("Date")]
        public DateTime Date { get; set; }
    }
}
