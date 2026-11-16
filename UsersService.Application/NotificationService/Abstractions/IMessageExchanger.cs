using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UsersService.Application.NotificationService.Abstractions
{
    public interface IMessageExchanger
    {
        public Task SendEmailMessageAsync(EmailDto email, CancellationToken ct);
    }
}
