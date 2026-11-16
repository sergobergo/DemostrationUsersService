using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UsersService.Application.NotificationService.Models
{
    public class NotificationDto
    {
        public EmailContact Sender { get; set; } = new EmailContact();
        public List<EmailContact> Recipients { get; set; } = new List<EmailContact>();
        public string Subject { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
